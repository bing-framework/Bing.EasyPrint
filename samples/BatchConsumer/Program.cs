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
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
client.DefaultRequestHeaders.Add("X-EasyPrint-Required-Features", "batch-export");
var chunks = new List<string>();
var remainingPages = 1000;
long remainingBytes = 32L * 1024 * 1024;
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
        request.Headers.Add("X-EasyPrint-Required-Features", "batch-export");
        using var response = await client.SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(Encoding.UTF8.GetString(bytes));
        remainingPages = Header(response, "X-EasyPrint-Batch-Remaining-Pages", remainingPages);
        remainingBytes = long.Parse(HeaderText(response, "X-EasyPrint-Batch-Remaining-Bytes",
            remainingBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            System.Globalization.CultureInfo.InvariantCulture);
        if (remainingPages < 0 || remainingBytes < 0)
            throw new InvalidOperationException("batch budget became negative.");
        var chunk = Path.Combine(Path.GetTempPath(), "easyprint-batch-" + Guid.NewGuid().ToString("N") + ".zip");
        await File.WriteAllBytesAsync(chunk, bytes);
        chunks.Add(chunk);
    }

    using var output = new ZipArchive(File.Create(outputPath), ZipArchiveMode.Create);
    var mergedRecords = new JsonArray();
    var mergedErrors = new JsonArray();
    var physicalPages = 0;
    var byteLength = 0L;
    foreach (var chunk in chunks)
    {
        using var archive = ZipFile.OpenRead(chunk);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException("chunk is missing manifest.json.");
        JsonNode manifest;
        using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
            manifest = JsonNode.Parse(await reader.ReadToEndAsync())
                ?? throw new InvalidDataException("chunk manifest is empty.");
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName is "manifest.json" or "errors.csv" || entry.Name.Length == 0) continue;
            var target = output.CreateEntry(entry.FullName, CompressionLevel.Fastest);
            using var sourceStream = entry.Open();
            using var targetStream = target.Open();
            await sourceStream.CopyToAsync(targetStream);
        }
        foreach (var record in manifest["records"] as JsonArray ?? new JsonArray())
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
        await JsonSerializer.SerializeAsync(stream, resultManifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    var errorsTarget = output.CreateEntry("errors.csv", CompressionLevel.Fastest);
    await using (var stream = errorsTarget.Open())
    {
        var csv = BuildErrorsCsv(mergedErrors);
        await stream.WriteAsync(csv);
    }
    Console.WriteLine($"{status}: {succeeded} succeeded, {failed} failed, {physicalPages} pages, {byteLength} raw bytes");
    return failed == 0 ? 0 : 1;
}
finally
{
    foreach (var chunk in chunks) File.Delete(chunk);
}

static JsonNode Clone(JsonNode value) => JsonNode.Parse(value.ToJsonString())
    ?? throw new InvalidDataException("JSON clone failed.");

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
