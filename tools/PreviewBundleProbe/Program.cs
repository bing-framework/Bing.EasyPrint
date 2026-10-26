using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var destination = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(AppContext.BaseDirectory, "results");
var label = args.Length > 1 ? args[1] : "confirmation";
Directory.CreateDirectory(destination);

const string template =
    "{\"Version\":2,\"Width\":800,\"Height\":600,\"Rows\":[{\"Cells\":[{\"Width\":800,\"Elements\":[" +
    "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"StartCoordinate\":{\"X\":10,\"Y\":10},\"Field\":{\"Value\":\"12345\"}}," +
    "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"StartCoordinate\":{\"X\":300,\"Y\":10},\"Field\":{\"Value\":\"12345\"}}," +
    "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"StartCoordinate\":{\"X\":10,\"Y\":210},\"Field\":{\"Value\":\"12345\"}}," +
    "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"StartCoordinate\":{\"X\":10,\"Y\":110},\"Field\":{\"Value\":\"12345\"}}," +
    "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"StartCoordinate\":{\"X\":300,\"Y\":110},\"Field\":{\"Value\":\"12345\"}}," +
    "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"StartCoordinate\":{\"X\":10,\"Y\":210},\"Field\":{\"Value\":\"12345\"}}," +
    "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"StartCoordinate\":{\"X\":300,\"Y\":210},\"Field\":{\"Value\":\"12345\"}}," +
    "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"StartCoordinate\":{\"X\":10,\"Y\":310},\"Field\":{\"Value\":\"12345\"}}," +
    "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"StartCoordinate\":{\"X\":300,\"Y\":310},\"Field\":{\"Value\":\"12345\"}}," +
    "{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"StartCoordinate\":{\"X\":100,\"Y\":430},\"Field\":{\"Value\":\"PREVIEW-BUNDLE-A\"}}," +
    "{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"StartCoordinate\":{\"X\":400,\"Y\":430},\"Field\":{\"Value\":\"PREVIEW-BUNDLE-B\"}}" +
    "]}]}]}";
const string baseBody = "{\"template\":" + template +
    ",\"data\":{},\"protocol\":\"EscPos\",\"escPosLayoutMode\":\"Raster\",\"encoding\":\"ASCII\"," +
    "\"feedLines\":0,\"cutAfterPrint\":false";

var oldChain = Measure(() => RunOldChain(baseBody));
var bundle = Measure(() => RunBundle(baseBody));
var report = new
{
    label,
    runtime = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription,
    processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
    template = "fixed ESC/POS Raster page with 8 Code128 elements and 2 QR elements",
    samples = 5,
    iterationsPerSample = 3,
    oldChain = new
    {
        endpointCalls = oldChain.Calls,
        renderEquivalentCalls = oldChain.Calls,
        medianMilliseconds = Math.Round(oldChain.MedianMilliseconds, 3),
        medianAllocatedBytes = oldChain.MedianAllocatedBytes,
        responseBytes = oldChain.ResponseBytes
    },
    bundle = new
    {
        endpointCalls = bundle.Calls,
        renderEquivalentCalls = bundle.Calls,
        medianMilliseconds = Math.Round(bundle.MedianMilliseconds, 3),
        medianAllocatedBytes = bundle.MedianAllocatedBytes,
        responseBytes = bundle.ResponseBytes
    },
    note = "No performance threshold is applied. Both paths use the same net10 candidate and fixed workload."
};
var output = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
var path = Path.Combine(destination, $"preview-bundle-{label}.json");
File.WriteAllText(path, output, new UTF8Encoding(false));
Console.WriteLine(output);
Console.WriteLine($"Probe written: {path}");

static (int Calls, long ResponseBytes) RunOldChain(string baseBody)
{
    var preview = Execute(baseBody + "}", DesignerRequestProcessor.Preview);
    using var document = JsonDocument.Parse(preview);
    var pages = document.RootElement.GetProperty("pages");
    var elements = pages[0].GetProperty("elements");
    var calls = 1;
    long bytes = preview.LongLength;
    for (var index = 0; index < elements.GetArrayLength(); index++)
    {
        var element = elements[index];
        var type = element.GetProperty("type").GetInt32();
        if (type is not (3 or 4)) continue;
        var sourcePath = element.GetProperty("sourcePath").GetString();
        var request = baseBody + ",\"pageIndex\":0,\"elementIndex\":" + index +
            ",\"sourcePath\":\"" + sourcePath + "\"}";
        bytes += Execute(request, DesignerRequestProcessor.ElementPng).LongLength;
        calls++;
    }
    return (calls, bytes);
}

static (int Calls, long ResponseBytes) RunBundle(string baseBody)
{
    var request = baseBody + ",\"pageIndex\":0,\"imageTypes\":[3,4]}";
    var response = Execute(request, DesignerRequestProcessor.PreviewBundle);
    return (1, response.LongLength);
}

static byte[] Execute(string json, Func<DesignerRequest, CancellationToken, IResult> process)
{
    var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
    var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
    var context = new DefaultHttpContext { RequestServices = services };
    context.Request.Body = body;
    context.Request.ContentLength = body.Length;
    context.Request.ContentType = "application/json; charset=utf-8";
    context.Response.Body = new MemoryStream();
    DesignerRequestExecution.ExecuteAsync(context, process).GetAwaiter().GetResult();
    var response = ((MemoryStream)context.Response.Body).ToArray();
    if (context.Response.StatusCode is < 200 or >= 300)
        throw new InvalidOperationException("Probe request failed with HTTP " + context.Response.StatusCode +
            ": " + Encoding.UTF8.GetString(response));
    return response;
}

static Measurement Measure(Func<(int Calls, long ResponseBytes)> run)
{
    for (var warmup = 0; warmup < 2; warmup++) run();
    var times = new double[5];
    var allocations = new long[5];
    var calls = 0;
    long responseBytes = 0;
    for (var sample = 0; sample < 5; sample++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocated = GC.GetTotalAllocatedBytes(true);
        var watch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var result = run();
            calls = result.Calls;
            responseBytes = result.ResponseBytes;
        }
        watch.Stop();
        times[sample] = watch.Elapsed.TotalMilliseconds / 3;
        allocations[sample] = (GC.GetTotalAllocatedBytes(true) - allocated) / 3;
    }
    Array.Sort(times);
    Array.Sort(allocations);
    return new Measurement(calls, responseBytes, times[2], allocations[2]);
}

internal sealed record Measurement(int Calls, long ResponseBytes, double MedianMilliseconds,
    long MedianAllocatedBytes);
