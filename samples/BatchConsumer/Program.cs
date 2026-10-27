using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length < 4)
{
    Console.Error.WriteLine("usage: BatchConsumer <base-url> <template.json> <records.json> <result.zip>");
    return 2;
}

var baseUrl = args[0].TrimEnd('/') + "/";
var template = JsonNode.Parse(await File.ReadAllTextAsync(args[1]))
    ?? throw new InvalidDataException("template JSON is empty.");
var source = JsonNode.Parse(await File.ReadAllTextAsync(args[2])) as JsonArray
    ?? throw new InvalidDataException("records JSON must be an object array.");
if (source.Count is < 1 or > 1000) throw new InvalidDataException("records must contain 1-1000 items.");
var outputPath = Path.GetFullPath(args[3]);
var outputDirectory = Path.GetDirectoryName(outputPath)!;
Directory.CreateDirectory(outputDirectory);

var requiredFeatures = new[] { "batch-export", "page-pdf-export", "barcode-svg-export" };
var chunks = new List<(string Path, int Offset, int Count)>();
var stagedOutputPath = Path.Combine(outputDirectory,
    $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
var remainingPages = 1000;
long remainingBytes = 32 * 1024 * 1024;

using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
try
{
    for (var offset = 0; offset < source.Count; offset += 25)
    {
        var count = Math.Min(25, source.Count - offset);
        var records = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var value = source[offset + index] ?? throw new InvalidDataException("record is null.");
            records.Add(value is JsonObject item && item["data"] is not null
                ? Clone(item)
                : new JsonObject { ["index"] = offset + index, ["data"] = Clone(value) });
        }
        var body = new JsonObject
        {
            ["template"] = Clone(template),
            ["data"] = new JsonObject(),
            ["protocol"] = "EscPos",
            ["escPosLayoutMode"] = "Raster",
            ["records"] = records,
            ["recordOffset"] = offset,
            ["totalRecords"] = source.Count,
            ["remainingPages"] = remainingPages,
            ["remainingBytes"] = remainingBytes,
            ["exportFormats"] = new JsonArray(JsonValue.Create("pdf"), JsonValue.Create("svg")),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/designer/batch")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation(
            "X-EasyPrint-Required-Features", string.Join(',', requiredFeatures));
        using var response = await client.SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(Encoding.UTF8.GetString(bytes));
        ValidateContract(response, requiredFeatures);

        remainingPages = Header(response, "X-EasyPrint-Batch-Remaining-Pages", remainingPages);
        remainingBytes = long.Parse(HeaderText(response, "X-EasyPrint-Batch-Remaining-Bytes",
            remainingBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            System.Globalization.CultureInfo.InvariantCulture);
        if (remainingPages < 0 || remainingBytes < 0)
            throw new InvalidOperationException("batch budget became negative.");
        var chunk = Path.Combine(Path.GetTempPath(), "easyprint-batch-" + Guid.NewGuid().ToString("N") + ".zip");
        await File.WriteAllBytesAsync(chunk, bytes);
        chunks.Add((chunk, offset, count));
    }

    var mergedRecords = new JsonArray();
    var mergedErrors = new JsonArray();
    var physicalPages = 0;
    var byteLength = 0L;
    using (var output = new ZipArchive(File.Create(stagedOutputPath), ZipArchiveMode.Create))
    {
        foreach (var chunk in chunks)
        {
            using var archive = ZipFile.OpenRead(chunk.Path);
            var manifestEntry = archive.GetEntry("manifest.json")
                ?? throw new InvalidDataException("chunk is missing manifest.json.");
            JsonNode manifest;
            using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
                manifest = JsonNode.Parse(await reader.ReadToEndAsync())
                    ?? throw new InvalidDataException("chunk manifest is empty.");
            ValidateChunkManifest(manifest, archive, chunk.Offset, chunk.Count);

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName is "manifest.json" or "errors.csv" || entry.Name.Length == 0) continue;
                var target = output.CreateEntry(entry.FullName, CompressionLevel.Fastest);
                using var sourceStream = entry.Open();
                using var targetStream = target.Open();
                await sourceStream.CopyToAsync(targetStream);
            }
            foreach (var record in (JsonArray)manifest["records"]!)
                mergedRecords.Add(Clone(record ?? throw new InvalidDataException("record result is null.")));
            foreach (var error in manifest["errors"] as JsonArray ?? new JsonArray())
                mergedErrors.Add(Clone(error ?? throw new InvalidDataException("record error is null.")));
            physicalPages += manifest["physicalPages"]?.GetValue<int>() ?? 0;
            byteLength += manifest["byteLength"]?.GetValue<long>() ?? 0;
            remainingPages = manifest["remainingPages"]?.GetValue<int>() ?? remainingPages;
            remainingBytes = manifest["remainingBytes"]?.GetValue<long>() ?? remainingBytes;
        }

        var succeeded = 0;
        var failed = 0;
        foreach (var record in mergedRecords)
        {
            if (record?["status"]?.GetValue<string>() == "completed") succeeded++;
            else failed++;
        }
        var status = failed == 0 ? "completed" : succeeded == 0 ? "all_failed" : "partial";
        var exportedFiles = new JsonArray();
        foreach (var record in mergedRecords)
        {
            var recordIndex = record?["index"]?.GetValue<int>()
                ?? throw new InvalidDataException("record result is missing its index.");
            foreach (var file in record?["files"] as JsonArray ?? new JsonArray())
            {
                if (file?["format"]?.GetValue<string>() is not ("pdf" or "svg")) continue;
                var fileSummary = (JsonObject)Clone(file
                    ?? throw new InvalidDataException("record file result is null."));
                fileSummary["recordIndex"] = recordIndex;
                exportedFiles.Add(fileSummary);
            }
        }
        var resultManifest = new JsonObject
        {
            ["status"] = status,
            ["recordOffset"] = 0,
            ["totalRecords"] = mergedRecords.Count,
            ["succeeded"] = succeeded,
            ["failed"] = failed,
            ["physicalPages"] = physicalPages,
            ["byteLength"] = byteLength,
            ["remainingPages"] = remainingPages,
            ["remainingBytes"] = remainingBytes,
            ["records"] = mergedRecords,
            ["files"] = exportedFiles,
            ["errors"] = mergedErrors,
        };
        var manifestTarget = output.CreateEntry("manifest.json", CompressionLevel.Fastest);
        await using (var stream = manifestTarget.Open())
            await JsonSerializer.SerializeAsync(stream, resultManifest,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var errorsTarget = output.CreateEntry("errors.csv", CompressionLevel.Fastest);
        await using (var stream = errorsTarget.Open())
        {
            var csv = BuildErrorsCsv(mergedErrors);
            await stream.WriteAsync(csv);
        }
    }

    var completed = mergedRecords.Count(record => record?["status"]?.GetValue<string>() == "completed");
    var failedCount = mergedRecords.Count - completed;
    var resultStatus = failedCount == 0 ? "completed" : completed == 0 ? "all_failed" : "partial";
    File.Move(stagedOutputPath, outputPath, overwrite: true);
    Console.WriteLine($"{resultStatus}: {completed} succeeded, {failedCount} failed, {physicalPages} pages, {byteLength} raw bytes");
    return failedCount == 0 ? 0 : 1;
}
catch (Exception error)
{
    Console.Error.WriteLine($"BatchConsumer failed: {error.Message}");
    return 1;
}
finally
{
    foreach (var chunk in chunks)
        if (File.Exists(chunk.Path)) File.Delete(chunk.Path);
    if (File.Exists(stagedOutputPath)) File.Delete(stagedOutputPath);
}

static JsonNode Clone(JsonNode value) => JsonNode.Parse(value.ToJsonString())
    ?? throw new InvalidDataException("JSON clone failed.");

static void ValidateContract(HttpResponseMessage response, IReadOnlyCollection<string> requiredFeatures)
{
    const string versionHeader = "X-EasyPrint-Contract-Version";
    const string featuresHeader = "X-EasyPrint-Features";
    if (!response.Headers.TryGetValues(versionHeader, out var versions))
        throw new InvalidDataException($"successful response must declare {versionHeader}: 1.");
    var versionValues = versions.ToArray();
    if (versionValues.Length != 1 || !string.Equals(versionValues[0].Trim(), "1", StringComparison.Ordinal))
        throw new InvalidDataException($"successful response must declare {versionHeader}: 1.");

    var features = response.Headers.TryGetValues(featuresHeader, out var values)
        ? values.SelectMany(value => value.Split(',')).Select(value => value.Trim())
            .Where(value => value.Length > 0).ToHashSet(StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);
    var missing = requiredFeatures.Where(feature => !features.Contains(feature)).ToArray();
    if (missing.Length > 0)
        throw new InvalidDataException($"successful response is missing required features: {string.Join(", ", missing)}.");
}

static void ValidateChunkManifest(JsonNode manifest, ZipArchive archive, int expectedOffset, int expectedCount)
{
    var recordOffset = manifest["recordOffset"]?.GetValue<int>()
        ?? throw new InvalidDataException("chunk manifest is missing recordOffset.");
    if (recordOffset != expectedOffset)
        throw new InvalidDataException($"chunk manifest returned recordOffset {recordOffset}; expected {expectedOffset}.");
    var records = manifest["records"] as JsonArray
        ?? throw new InvalidDataException("chunk manifest is missing records.");
    if (records.Count != expectedCount)
        throw new InvalidDataException($"chunk manifest returned {records.Count} records; expected {expectedCount}.");

    foreach (var record in records)
    {
        var status = record?["status"]?.GetValue<string>()
            ?? throw new InvalidDataException("chunk record is missing its status.");
        if (status is not ("completed" or "failed"))
            throw new InvalidDataException($"chunk record has unsupported status '{status}'.");
        if (status == "failed") continue;
        var index = record["index"]?.GetValue<int>()
            ?? throw new InvalidDataException("completed record is missing its index.");
        var files = record["files"] as JsonArray
            ?? throw new InvalidDataException($"completed record {index} is missing files.");
        var pdfCount = 0;
        var svgCount = 0;
        foreach (var file in files)
        {
            var format = file?["format"]?.GetValue<string>();
            if (format is not ("pdf" or "svg")) continue;
            var path = file?["file"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException($"completed record {index} has a {format} file without a path.");
            var entry = archive.GetEntry(path)
                ?? throw new InvalidDataException($"completed record {index} references missing ZIP item '{path}'.");
            if (entry.Length == 0)
                throw new InvalidDataException($"completed record {index} references empty ZIP item '{path}'.");
            if (format == "pdf") pdfCount++;
            else svgCount++;
        }
        if (pdfCount == 0 || svgCount == 0)
            throw new InvalidDataException($"completed record {index} must include non-empty PDF and SVG exports.");
    }
}

static string HeaderText(HttpResponseMessage response, string name, string fallback)
    => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() ?? fallback : fallback;

static int Header(HttpResponseMessage response, string name, int fallback)
    => int.TryParse(HeaderText(response, name, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
        ? value : fallback;

static byte[] BuildErrorsCsv(JsonArray errors)
{
    var builder = new StringBuilder();
    builder.Append('\uFEFF');
    builder.AppendLine("recordIndex,code,phase,fieldPath,sourcePath,message");
    foreach (var error in errors)
    {
        builder.Append(error?["recordIndex"]?.GetValue<int>() ?? 0).Append(',')
            .Append(Csv(error?["code"]?.GetValue<string>())).Append(',')
            .Append(Csv(error?["phase"]?.GetValue<string>())).Append(',')
            .Append(Csv(error?["fieldPath"]?.GetValue<string>())).Append(',')
            .Append(Csv(error?["sourcePath"]?.GetValue<string>())).Append(',')
            .Append(Csv(error?["message"]?.GetValue<string>())).AppendLine();
    }
    return Encoding.UTF8.GetBytes(builder.ToString());
}

static string Csv(string? value)
{
    value ??= string.Empty;
    if (value.Length > 0 && "=+-@\t\r".IndexOf(value[0]) >= 0) value = "'" + value;
    return "\"" + value.Replace("\"", "\"\"") + "\"";
}
