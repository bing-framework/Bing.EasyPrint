using System.Diagnostics;
using System.Text;
using Bing.EasyPrint.CPCL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
#if TEMPLATES
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Templates;
#endif

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var destination = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(AppContext.BaseDirectory, "results");
Directory.CreateDirectory(destination);
using var image = new Image<Rgba32>(512, 256);
for (var y = 0; y < image.Height; y++)
    for (var x = 0; x < image.Width; x++) image[x, y] = new Rgba32((byte)(x % 256), (byte)y, (byte)((x + y) % 256));
using var stream = new MemoryStream(); image.SaveAsPng(stream);
var imageBytes = stream.ToArray();
Console.WriteLine($"Runtime: {Environment.Version}; OS: {Environment.OSVersion}; CPU: {Environment.ProcessorCount}; warmups=3 samples=5 iterations=10");
Probe("text-1000", () => {
    var command = new CPCLPrintCommand().SetPage(576, 1000);
    for (var i = 0; i < 1000; i++) command.DrawText(0, i, "标签 ABC 0123456789", 24, 0, false, false, false);
    return command.Build().GetBytes();
});
Probe("image-512x256", () => new CPCLPrintCommand().SetPage(576, 300).DrawImage(0, 0, imageBytes).Build().GetBytes());
var componentBuildCases = new (string Name, Action<CPCLPrintCommand> AddComponent)[]
{
    ("component-text-short", command => command.DrawText(0, 0, "ABC123", 24, 0, false, false, false)),
    ("component-text-long", command => command.DrawText(0, 0, new string('A', 4096), 24, 0, false, false, false)),
    ("component-barcode", command => command.DrawBarcode1D("128", 0, 0, "123456789012", 1, 60, 2, 0)),
    ("component-qrcode", command => command.DrawQrCode(0, 0, "QR-1234567890", 4, "M", 0))
};
foreach (var buildCase in componentBuildCases)
    ProbeComponentBuildPaths(buildCase.Name, buildCase.AddComponent);
#if TEMPLATES
var template = new BpTemplate { Width = 576, Height = 1000, Rows = new List<BpRow> {
    new BpRow { RepeatSource = "Items", Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> { new BpFontElement { Field = new BgField { Name = "Name" } } } } } }
} };
var data = new { Items = Enumerable.Range(0, 100).Select(i => new { Name = "明细 " + i }).ToArray() };
var renderer = new TemplateRenderer();
var firstWatch = Stopwatch.StartNew();
var first = renderer.Render(template, data);
firstWatch.Stop();
Console.WriteLine($"template-100 first-call-ms={firstWatch.Elapsed.TotalMilliseconds:F3}; pages={first.PageCount}");
Probe("template-100", () => renderer.Render(template, data).Pages.SelectMany(page => page.Data).ToArray());
#endif

void Probe(string name, Func<byte[]> run)
{
    var reference = run();
    Measure(name, run, reference);
}

void ProbeComponentBuildPaths(string name, Action<CPCLPrintCommand> addComponent)
{
    byte[] BuildNewCommand()
    {
        var command = new CPCLPrintCommand().SetPage(576, 1000);
        addComponent(command);
        return command.Build().GetBytes();
    }

    var firstBuildOutput = BuildNewCommand();
    Measure(name + "-add-first-build", BuildNewCommand, firstBuildOutput);

    var repeatedCommand = new CPCLPrintCommand().SetPage(576, 1000);
    addComponent(repeatedCommand);
    var repeatedBuildReference = repeatedCommand.Build().GetBytes();
    if (!firstBuildOutput.AsSpan().SequenceEqual(repeatedBuildReference))
        throw new Exception("First and repeated-build setup output differ: " + name);

    Measure(name + "-same-command-repeat-build", () => repeatedCommand.Build().GetBytes(), repeatedBuildReference);
}

void Measure(string name, Func<byte[]> run, byte[] reference)
{
    File.WriteAllBytes(Path.Combine(destination, name + ".bin"), reference);
    for (var i = 0; i < 3; i++) run();
    var times = new double[5]; var allocations = new long[5];
    for (var sample = 0; sample < 5; sample++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var allocated = GC.GetTotalAllocatedBytes(true);
        var watch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var actual = run();
            if (!actual.AsSpan().SequenceEqual(reference)) throw new Exception("Output changed between repetitions: " + name);
        }
        watch.Stop();
        times[sample] = watch.Elapsed.TotalMilliseconds / 10;
        allocations[sample] = (GC.GetTotalAllocatedBytes(true) - allocated) / 10;
    }
    Array.Sort(times); Array.Sort(allocations);
    Console.WriteLine($"{name}: median-ms={times[2]:F3}; median-allocated-bytes={allocations[2]}; output-bytes={reference.Length}");
}
