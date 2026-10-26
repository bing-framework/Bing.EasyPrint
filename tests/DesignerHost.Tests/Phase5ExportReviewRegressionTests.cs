using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

public sealed class Phase5ExportReviewRegressionTests
{
    private const string Code49Template =
        "{\"Version\":3,\"Width\":600,\"Height\":500,\"Rows\":[{\"Height\":500,\"Cells\":[{\"Width\":600,\"Elements\":[" +
        "{\"Type\":4,\"BarcodeType\":24,\"Height\":20,\"NarrowModuleWidth\":2,\"HumanReadable\":false," +
        "\"Field\":{\"Value\":\"ABCD1234\"}}]}]}]}";

    private const string TsplRepeatedTemplate =
        "{\"Version\":3,\"Width\":600,\"Height\":100,\"Rows\":[{\"RepeatSource\":\"Items\"," +
        "\"Cells\":[{\"Width\":600,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Value\":\"SOURCE-TEXT\"}}," +
        "{\"Type\":4,\"BarcodeType\":0,\"Height\":20,\"HumanReadable\":false,\"Field\":{\"Name\":\"Value\"}}]}]}]}";

    private const string Raster300Template =
        "{\"Version\":2,\"Width\":600,\"Height\":500,\"Rows\":[{\"Height\":500,\"Cells\":[{\"Width\":600,\"Elements\":[" +
        "{\"Type\":4,\"BarcodeType\":0,\"Height\":20,\"NarrowModuleWidth\":2,\"HumanReadable\":false," +
        "\"Field\":{\"Value\":\"DPI-300\"}}]}]}]}";

    [Theory]
    [InlineData(0, false)]
    [InlineData(90, false)]
    [InlineData(180, false)]
    [InlineData(270, false)]
    [InlineData(0, true)]
    [InlineData(90, true)]
    [InlineData(180, true)]
    [InlineData(270, true)]
    public async Task Code49_svg_geometry_matches_shared_raster(int rotation, bool explicitQuietZone)
    {
        var quietZone = explicitQuietZone ? ",\"QuietZone\":{\"Left\":4,\"Top\":3,\"Right\":6,\"Bottom\":5}" : string.Empty;
        var template = Code49Template.Replace("\"HumanReadable\":false,",
            $"\"Rotation\":{rotation},\"HumanReadable\":false{quietZone},");
        var request = Request("EscPos", template, "{}", ",\"escPosLayoutMode\":\"Raster\",\"pageIndex\":0,\"elementIndex\":0");

        var result = await Execute(() => DesignerRequestProcessor.ElementSvg(request, CancellationToken.None));
        var preview = await Execute(() => DesignerRequestProcessor.Preview(request, CancellationToken.None));
        var raster = await Execute(() => DesignerRequestProcessor.PagePng(request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, result.Context.Response.StatusCode);
        Assert.True(preview.Context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(preview.Bytes));
        Assert.True(raster.Context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(raster.Bytes));
        var document = XDocument.Parse(Encoding.UTF8.GetString(result.Bytes));
        var root = document.Root!;
        var viewBox = ((string)root.Attribute("viewBox")!).Split(' ').Select(int.Parse).ToArray();
        var width = viewBox[2];
        var height = viewBox[3];
        var geometry = Assert.Single(root.Elements(root.Name.Namespace + "path")).Attribute("d")!.Value;
        var bounds = Regex.Matches(geometry, @"M(-?\d+) (-?\d+)h(-?\d+)v(-?\d+)h(-?\d+)z")
            .Select(match =>
            {
                var x = int.Parse(match.Groups[1].Value);
                var y = int.Parse(match.Groups[2].Value);
                var rectWidth = int.Parse(match.Groups[3].Value);
                var rectHeight = int.Parse(match.Groups[4].Value);
                return (Left: Math.Min(x, x + rectWidth), Top: y, Right: Math.Max(x, x + rectWidth), Bottom: y + rectHeight);
            }).ToArray();

        Assert.NotEmpty(bounds);
        Assert.All(bounds, box =>
        {
            Assert.InRange(box.Left, 0, width);
            Assert.InRange(box.Top, 0, height);
            Assert.InRange(box.Right, 0, width);
            Assert.InRange(box.Bottom, 0, height);
        });

        using var previewJson = JsonDocument.Parse(preview.Bytes);
        var layout = previewJson.RootElement.GetProperty("pages")[0].GetProperty("elements")
            .EnumerateArray().Single(element => element.GetProperty("kind").GetString() == "element");
        var layoutX = layout.GetProperty("x").GetInt32();
        var layoutY = layout.GetProperty("y").GetInt32();
        // Default Code49 layout reserves 10X before the primitive on the data axis.
        var implicitLeadingQuiet = explicitQuietZone ? 0 : 10 * 2;
        var rasterOriginX = checked(layoutX + (rotation == 0 ? implicitLeadingQuiet : 0));
        var rasterOriginY = checked(layoutY + (rotation == 90 ? implicitLeadingQuiet : 0));
        using var rasterImage = Image.Load<Rgba32>(raster.Bytes);
        var rasterLeft = rasterImage.Width;
        var rasterTop = rasterImage.Height;
        var rasterRight = -1;
        var rasterBottom = -1;
        for (var y = 0; y < rasterImage.Height; y++)
        for (var x = 0; x < rasterImage.Width; x++)
            if (rasterImage[x, y].R < 128)
            {
                rasterLeft = Math.Min(rasterLeft, x);
                rasterTop = Math.Min(rasterTop, y);
                rasterRight = Math.Max(rasterRight, x);
                rasterBottom = Math.Max(rasterBottom, y);
            }
        Assert.InRange(rasterOriginX, 0, rasterImage.Width - width);
        Assert.InRange(rasterOriginY, 0, rasterImage.Height - height);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var vectorBlack = bounds.Any(box => x >= box.Left && x < box.Right && y >= box.Top && y < box.Bottom);
            var rasterBlack = rasterImage[rasterOriginX + x, rasterOriginY + y].R < 128;
            Assert.True(vectorBlack == rasterBlack,
                $"Pixel mismatch rotation={rotation}, explicitQuietZone={explicitQuietZone}, svg=({x},{y}), " +
                $"page=({rasterOriginX},{rasterOriginY}), viewBox={width}x{height}, vectorBlack={vectorBlack}, " +
                $"rasterBlack={rasterBlack}, rasterBounds=({rasterLeft},{rasterTop})-({rasterRight},{rasterBottom}).");
        }
    }

    [Fact]
    public async Task Element_svg_uses_original_tspl_layout_for_repeated_second_page()
    {
        var request = Request("Tspl", TsplRepeatedTemplate,
            "{\"Items\":[{\"Value\":\"FIRST\"},{\"Value\":\"SECOND\"}]}",
            ",\"deviceLineHeightAt24\":60,\"pageIndex\":1,\"elementIndex\":1," +
            "\"sourcePath\":\"Rows[0].Cells[0].Elements[1]\"");

        var result = await Execute(() => DesignerRequestProcessor.ElementSvg(request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, result.Context.Response.StatusCode);
        var document = XDocument.Parse(Encoding.UTF8.GetString(result.Bytes));
        Assert.StartsWith("image/svg+xml", result.Context.Response.ContentType);
        Assert.DoesNotContain("SECOND", document.ToString());
        Assert.DoesNotContain("SOURCE-TEXT", document.ToString());
    }

    [Fact]
    public async Task Batch_svg_keeps_tspl_pages_and_repeated_instances_from_the_print_render()
    {
        var request = Request("Tspl", TsplRepeatedTemplate,
            "{}", ",\"deviceLineHeightAt24\":60,\"records\":[{\"index\":0,\"data\":{" +
            "\"Items\":[{\"Value\":\"FIRST\"},{\"Value\":\"SECOND\"}]}}]," +
            "\"totalRecords\":1,\"exportFormats\":[\"svg\"]");

        var result = await Execute(() => DesignerRequestProcessor.BatchRender(request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, result.Context.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(result.Bytes), ZipArchiveMode.Read);
        using var manifestStream = archive.GetEntry("manifest.json")!.Open();
        using var manifest = await JsonDocument.ParseAsync(manifestStream);
        var root = manifest.RootElement;
        Assert.Equal(2, root.GetProperty("physicalPages").GetInt32());
        var files = root.GetProperty("records")[0].GetProperty("files").EnumerateArray()
            .Where(file => file.TryGetProperty("format", out var format) && format.GetString() == "svg").ToArray();
        Assert.Equal(new[] { 0, 1 }, files.Select(file => file.GetProperty("pageIndex").GetInt32()).Order().ToArray());
        Assert.All(files, file => Assert.Equal(1, file.GetProperty("elementIndex").GetInt32()));
        Assert.Contains(archive.Entries, entry => entry.FullName.Contains("page-0002-element-0002.svg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Batch_svg_matches_single_svg_physical_dimensions_at_300_dpi()
    {
        const string device = ",\"escPosDevice\":{\"profile\":{\"imageCommand\":\"BitImageEscStar\"," +
            "\"bitImageMode\":\"DoubleDensity8\",\"horizontalDpi\":300,\"verticalDpi\":300," +
            "\"motionUnitsPerInch\":203,\"printableWidthDots\":600}}";
        var singleRequest = Request("EscPos", Raster300Template, "{}",
            ",\"escPosLayoutMode\":\"Raster\",\"pageIndex\":0,\"elementIndex\":0" + device);
        var single = await Execute(() => DesignerRequestProcessor.ElementSvg(singleRequest, CancellationToken.None));
        var batchRequest = Request("EscPos", Raster300Template, "{}",
            ",\"escPosLayoutMode\":\"Raster\",\"records\":[{\"index\":0,\"data\":{}}]," +
            "\"totalRecords\":1,\"exportFormats\":[\"svg\"]" + device);
        var batch = await Execute(() => DesignerRequestProcessor.BatchRender(batchRequest, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, single.Context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, batch.Context.Response.StatusCode);
        var singleSvg = XDocument.Parse(Encoding.UTF8.GetString(single.Bytes)).Root!;
        using var archive = new ZipArchive(new MemoryStream(batch.Bytes), ZipArchiveMode.Read);
        using var manifestStream = archive.GetEntry("manifest.json")!.Open();
        using var manifest = await JsonDocument.ParseAsync(manifestStream);
        var svgFile = Assert.Single(manifest.RootElement.GetProperty("records")[0].GetProperty("files")
            .EnumerateArray().Where(file => file.TryGetProperty("format", out var format) && format.GetString() == "svg"));
        using var svgStream = archive.GetEntry(svgFile.GetProperty("file").GetString()!)!.Open();
        var batchSvg = XDocument.Load(svgStream).Root!;
        Assert.Equal((string)singleSvg.Attribute("viewBox")!, (string)batchSvg.Attribute("viewBox")!);
        Assert.Equal((string)singleSvg.Attribute("width")!, (string)batchSvg.Attribute("width")!);
        Assert.Equal((string)singleSvg.Attribute("height")!, (string)batchSvg.Attribute("height")!);
    }

    private static DesignerRequest Request(string protocol, string template, string data, string extra)
    {
        var json = "{\"template\":" + template + ",\"data\":" + data + ",\"protocol\":\"" + protocol + "\"" + extra + "}";
        return JsonSerializer.Deserialize<DesignerRequest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static async Task<(DefaultHttpContext Context, byte[] Bytes)> Execute(Func<Microsoft.AspNetCore.Http.IResult> createResult)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var requestScope = PrintResourceScope.Begin(32 * 1024 * 1024);
        var context = new DefaultHttpContext();
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        context.RequestServices = services;
        using var body = new MemoryStream();
        context.Response.Body = body;
        await createResult().ExecuteAsync(context);
        return (context, body.ToArray());
    }
}
