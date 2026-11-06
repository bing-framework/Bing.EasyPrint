using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// 独立于旧探针口径；基线与候选加载同一 harness，仅替换被引用的 Host/库。
internal static class AcceptanceProbe
{
    internal static void Run(string[] args)
    {
        var runWatch = Stopwatch.StartNew();
        if (args.Length < 4) throw new ArgumentException("--acceptance output.json label contract.json [--smoke]");
        var outputPath = Path.GetFullPath(args[1]);
        if (File.Exists(outputPath)) throw new IOException("Evidence already exists: " + outputPath);
        var contractBytes = File.ReadAllBytes(args[3]);
        using var contract = JsonDocument.Parse(contractBytes);
        var smoke = args.Contains("--smoke");
        var samples = smoke ? 2 : contract.RootElement.GetProperty("samples").GetInt32();
        var warmups = smoke ? 1 : contract.RootElement.GetProperty("warmups").GetInt32();
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var results = new List<object>();

        var textReference = Text1000();
        results.Add(Measure("text-1000", Text1000, bytes => Equal(textReference, bytes), warmups, samples));

        using var image = new Image<Rgba32>(512, 256);
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++) image[x, y] = new Rgba32((byte)x, (byte)y, (byte)(x + y));
        using var pngStream = new MemoryStream();
        image.SaveAsPng(pngStream);
        var png = pngStream.ToArray();
        byte[] ImageOperation() => new CPCLPrintCommand().SetPage(576, 300).DrawImage(0, 0, png).Build().GetBytes();
        var imageReference = ImageOperation();
        results.Add(Measure("image-512x256", ImageOperation, bytes => Equal(imageReference, bytes), warmups, samples));

        var template = new BpTemplate { Version = 2, Width = 576, Height = 1000, Rows = new List<BpRow> {
            new BpRow { Height = 24, RepeatSource = "Items", Cells = new List<BpCell> {
                new BpCell { Width = 576, Elements = new List<BpElement> {
                    new BpFontElement { FontSize = 16, Field = new BgField { Name = "Date", Format = new BgFieldFormat {
                        Kind = BgFieldFormatKind.Date, Pattern = "yyyy-MM-dd", Culture = "en-US" } } }
                } }
            } }
        } };
        var data = new { Items = Enumerable.Range(0, 100).Select(_ => new { Date = "2024-01-02T03:04:05+08:00" }).ToArray() };
        var renderer = new TemplateRenderer();
        byte[] TemplateOperation() => renderer.Render(template, data).Pages.SelectMany(page => page.Data).ToArray();
        var templateReference = TemplateOperation();
        if (templateReference.Length == 0) throw new InvalidOperationException("Empty template output");
        results.Add(Measure("date-template-100", TemplateOperation, bytes => Equal(templateReference, bytes), warmups, samples));

        var elements = new List<object>();
        for (var i = 0; i < 8; i++) elements.Add(new { Type = 4, BarcodeType = 0, Height = 40,
            NarrowModuleWidth = 2, HumanReadable = false, StartCoordinate = new { X = 10 + i % 2 * 350, Y = 10 + i / 2 * 100 },
            Field = new { Value = "PERF-" + i.ToString("0000") } });
        for (var i = 0; i < 2; i++) elements.Add(new { Type = 3, UnitWidth = 4, ErrorLevel = "M",
            StartCoordinate = new { X = 100 + i * 300, Y = 430 }, Field = new { Value = "PERF-QR-" + i } });
        var bundleTemplate = new { Version = 2, Width = 800, Height = 600,
            Rows = new[] { new { Cells = new[] { new { Width = 800, Elements = elements } } } } };
        var bundleRequest = JsonSerializer.Serialize(new { template = bundleTemplate, data = new { }, protocol = "EscPos",
            escPosLayoutMode = "Raster", encoding = "ASCII", feedLines = 0, cutAfterPrint = false,
            pageIndex = 0, imageTypes = new[] { 3, 4 } });
        results.Add(Measure("preview-bundle", () => Execute(bundleRequest, DesignerRequestProcessor.PreviewBundle, services),
            bytes => {
                using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                if (zip.Entries.Count(entry => entry.FullName.EndsWith(".png", StringComparison.Ordinal)) != 10)
                    throw new InvalidOperationException("Expected exactly ten preview images");
            }, warmups, samples));

        var batchTemplate = JsonDocument.Parse("""
            {"Version":2,"Width":400,"Height":120,"Rows":[{"Height":120,"Cells":[{"Width":400,"Elements":[
              {"Type":4,"BarcodeType":0,"Height":40,"HumanReadable":false,"Field":{"Name":"Value"}}
            ]}]}]}
            """);
        using (batchTemplate)
        {
            var batchRequest = JsonSerializer.Serialize(new { template = batchTemplate.RootElement, data = new { },
                protocol = "EscPos", escPosLayoutMode = "Raster", encoding = "ASCII", feedLines = 0,
                records = Enumerable.Range(0, 25).Select(index => new { index, data = new { Value = "P" + index.ToString("000") } }).ToArray(),
                totalRecords = 25, exportFormats = new[] { "pdf", "svg" } });
            results.Add(Measure("batch-25-pdf-svg", () => Execute(batchRequest, DesignerRequestProcessor.BatchRender, services),
                bytes => {
                    using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                    using var manifestStream = (zip.GetEntry("manifest.json") ?? throw new InvalidOperationException("No manifest")).Open();
                    using var manifest = JsonDocument.Parse(manifestStream);
                    if (manifest.RootElement.GetProperty("status").GetString() != "completed" ||
                        manifest.RootElement.GetProperty("succeeded").GetInt32() != 25 ||
                        manifest.RootElement.GetProperty("failed").GetInt32() != 0 ||
                        zip.Entries.Count(entry => entry.FullName.EndsWith(".pdf", StringComparison.Ordinal)) != 25 ||
                        zip.Entries.Count(entry => entry.FullName.EndsWith(".svg", StringComparison.Ordinal)) != 25)
                        throw new InvalidOperationException("Incomplete batch exports");
                }, warmups, samples));
        }

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var assembly = Assembly.GetExecutingAssembly();
        var report = new {
            label = args[2], mode = smoke ? "SMOKE" : "MEASUREMENT", contractSha256 = Convert.ToHexString(SHA256.HashData(contractBytes)),
            harnessSha256 = FileHash(assembly.Location), runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            processorCount = Environment.ProcessorCount, warmups, samples, iterationsPerSample = 1,
            machineHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName))),
            buildConfiguration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            osPlatform = OperatingSystem.IsWindows() ? "Windows" : "Other",
            elapsedSeconds = runWatch.Elapsed.TotalSeconds,
            sourceIdentity = new {
                kind = Environment.GetEnvironmentVariable("EASYPRINT_PROBE_SOURCE_KIND"),
                commit = Environment.GetEnvironmentVariable("EASYPRINT_PROBE_SOURCE_COMMIT"),
                hash = Environment.GetEnvironmentVariable("EASYPRINT_PROBE_SOURCE_HASH")
            },
            requestedConcurrency = 1, maxActualParallelism = 1, maxQueuedRequests = 0,
            peakWorkingSetBytes = process.PeakWorkingSet64,
            assemblies = new[] { typeof(TemplateRenderer).Assembly, typeof(CPCLPrintCommand).Assembly, typeof(DesignerRequestProcessor).Assembly }
                .Select(item => new { name = item.GetName().Name, sha256 = FileHash(item.Location) }).ToArray(),
            results
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Console.WriteLine("Acceptance measurements written: " + outputPath);
    }

    private static object Measure(string id, Func<byte[]> operation, Action<byte[]> validate, int warmups, int count)
    {
        for (var i = 0; i < warmups; i++) validate(operation());
        var times = new double[count];
        var allocations = new long[count];
        var collections = new int[count][];
        var outputBytes = new int[count];
        for (var i = 0; i < count; i++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var beforeCollections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            var beforeAllocated = GC.GetTotalAllocatedBytes(true);
            var started = Stopwatch.GetTimestamp();
            var output = operation();
            times[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            allocations[i] = GC.GetTotalAllocatedBytes(true) - beforeAllocated;
            collections[i] = Enumerable.Range(0, 3).Select(gen => GC.CollectionCount(gen) - beforeCollections[gen]).ToArray();
            outputBytes[i] = output.Length;
            validate(output); // 校验在计时/分配采集之后，失败使整个运行失效。
        }
        var ordered = times.Order().ToArray();
        var orderedAllocations = allocations.Order().ToArray();
        return new { id, correctness = "PASS", milliseconds = times, allocatedBytes = allocations,
            gcCollections = collections, outputBytes, medianMilliseconds = (ordered[(count - 1) / 2] + ordered[count / 2]) / 2,
            p95Milliseconds = ordered[(int)Math.Ceiling(count * .95) - 1],
            operationsPerSecond = count * 1000 / times.Sum(),
            medianAllocatedBytes = (orderedAllocations[(count - 1) / 2] + orderedAllocations[count / 2]) / 2 };
    }

    private static byte[] Text1000()
    {
        var command = new CPCLPrintCommand().SetPage(576, 1000);
        for (var i = 0; i < 1000; i++) command.DrawText(0, i, "PERF ABC 0123456789", 24, 0, false, false, false);
        return command.Build().GetBytes();
    }

    private static byte[] Execute(string json, Func<DesignerRequest, CancellationToken, IResult> processor, IServiceProvider services)
    {
        using var requestBody = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var responseBody = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = requestBody; context.Request.ContentLength = requestBody.Length;
        context.Request.ContentType = "application/json; charset=utf-8"; context.Response.Body = responseBody;
        DesignerRequestExecution.ExecuteAsync(context, processor).GetAwaiter().GetResult();
        if (context.Response.StatusCode != 200) throw new InvalidOperationException("Host returned " + context.Response.StatusCode + ": " + Encoding.UTF8.GetString(responseBody.ToArray()));
        return responseBody.ToArray();
    }

    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Equal(byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual)) throw new InvalidOperationException("Unstable Core output");
    }
}
