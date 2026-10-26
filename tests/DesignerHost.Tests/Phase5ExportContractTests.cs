using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class Phase5ExportContractTests
{
    private const string BarcodeTemplate =
        "{\"Version\":2,\"Width\":400,\"Height\":400,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
        "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2," +
        "\"Rotation\":90,\"QuietZone\":{\"Left\":4,\"Top\":2,\"Right\":6,\"Bottom\":3}," +
        "\"HumanReadable\":true,\"HumanReadableReserveHeight\":16," +
        "\"Field\":{\"Value\":\"Bing-F5-12345\"}}]}]}]}";
    private const string RepeatedTemplate =
        "{\"Version\":3,\"Width\":400,\"Height\":60,\"Rows\":[{\"Height\":60," +
        "\"RepeatSource\":\"Items\",\"Cells\":[{\"Elements\":[" +
        "{\"Type\":4,\"BarcodeType\":0,\"Height\":30,\"HumanReadable\":false," +
        "\"Field\":{\"Name\":\"Value\"}}]}]}]}";
    private const string HriPaginationTemplate =
        "{\"Version\":2,\"Width\":400,\"Height\":70,\"Rows\":[" +
        "{\"Cells\":[{\"Elements\":[{\"Type\":4,\"BarcodeType\":0,\"Height\":40," +
        "\"HumanReadable\":true,\"HumanReadableReserveHeight\":20,\"Field\":{\"Value\":\"12345\"}}]}]}," +
        "{\"Cells\":[{\"Elements\":[{\"Type\":4,\"BarcodeType\":0,\"Height\":20," +
        "\"HumanReadable\":false,\"Field\":{\"Value\":\"67890\"}}]}]}]}";

    [Fact]
    public void Host_advertises_dedicated_pdf_and_svg_export_features()
    {
        var contract = JsonSerializer.SerializeToElement(DesignerContract.Description);
        var features = contract.GetProperty("features").EnumerateArray()
            .Select(value => value.GetString()).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("page-pdf-export", features);
        Assert.Contains("barcode-svg-export", features);
    }

    [Theory]
    [InlineData("PagePdf")]
    [InlineData("ElementSvg")]
    public void Host_exposes_separate_export_processors(string name)
    {
        var method = typeof(DesignerRequestProcessor).GetMethod(name,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
    }

    [Fact]
    public void Batch_request_exposes_opt_in_export_formats()
    {
        var property = typeof(DesignerRequest).GetProperty("ExportFormats",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
    }

    [Fact]
    public async Task Page_pdf_expands_copies_and_page_selection_without_partial_output()
    {
        using var parent = PrintResourceScope.Begin(32 * 1024 * 1024);
        var data = "{\"Items\":[{\"Value\":\"12345\"},{\"Value\":\"67890\"}]}";
        var all = await Execute(() => DesignerRequestProcessor.PagePdf(
            Request(RepeatedTemplate, data, ",\"copies\":2"), CancellationToken.None));
        Assert.Equal(StatusCodes.Status200OK, all.Context.Response.StatusCode);
        Assert.Equal("application/pdf", all.Context.Response.ContentType);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(all.Bytes, 0, Math.Min(5, all.Bytes.Length)));
        Assert.Contains("easyprint-pages.pdf", all.Context.Response.Headers.ContentDisposition.ToString());

        var one = await Execute(() => DesignerRequestProcessor.PagePdf(
            Request(RepeatedTemplate, data, ",\"pageIndex\":1"), CancellationToken.None));
        Assert.Equal(StatusCodes.Status200OK, one.Context.Response.StatusCode);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(one.Bytes, 0, Math.Min(5, one.Bytes.Length)));
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Page_pdf_preserves_dot_to_point_media_box_precision()
    {
        using var parent = PrintResourceScope.Begin(32 * 1024 * 1024);
        var result = await Execute(() => DesignerRequestProcessor.PagePdf(
            Request(BarcodeTemplate, "{}", string.Empty), CancellationToken.None));

        Assert.True(result.Context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(result.Bytes));
        var pdf = Encoding.ASCII.GetString(result.Bytes);
        Assert.DoesNotContain("/Interpolate true", pdf, StringComparison.Ordinal);
        var match = Regex.Match(pdf, @"/MediaBox\s*\[\s*0\s+0\s+([0-9.]+)\s+([0-9.]+)\s*\]");
        Assert.True(match.Success, "PDF must contain one explicit media box.");
        var width = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var height = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(Math.Abs(width - 400d * 72d / 203d), 0d, 0.00000001d);
        Assert.InRange(Math.Abs(height - 400d * 72d / 203d), 0d, 0.00000001d);
        Assert.InRange(width * 203d / 72d, 400d - 0.0000000001d, 400d);
        Assert.InRange(height * 203d / 72d, 400d - 0.0000000001d, 400d);
    }

    [Fact]
    public async Task Page_pdf_rejects_non_raster_protocol_and_invalid_index()
    {
        using var parent = PrintResourceScope.Begin(16 * 1024 * 1024);
        var native = Request(BarcodeTemplate, "{}", ",\"protocol\":\"Zpl\"");
        var unsupported = await Execute(() => DesignerRequestProcessor.PagePdf(native, CancellationToken.None));
        Assert.Equal(StatusCodes.Status400BadRequest, unsupported.Context.Response.StatusCode);

        var invalid = await Execute(() => DesignerRequestProcessor.PagePdf(
            Request(BarcodeTemplate, "{}", ",\"pageIndex\":9"), CancellationToken.None));
        Assert.Equal(StatusCodes.Status400BadRequest, invalid.Context.Response.StatusCode);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Element_svg_uses_rotated_vector_geometry_and_has_no_hri_or_embedded_image()
    {
        using var parent = PrintResourceScope.Begin(24 * 1024 * 1024);
        var result = await Execute(() => DesignerRequestProcessor.ElementSvg(
            Request(BarcodeTemplate, "{}", ",\"pageIndex\":0,\"elementIndex\":0"),
            CancellationToken.None));
        Assert.True(result.Context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(result.Bytes));
        Assert.StartsWith("image/svg+xml", result.Context.Response.ContentType);
        var document = XDocument.Parse(Encoding.UTF8.GetString(result.Bytes));
        var svg = document.Root!;
        Assert.Equal(XNamespace.Get("http://www.w3.org/2000/svg"), svg.Name.Namespace);
        var viewBox = ((string?)svg.Attribute("viewBox"))!.Split(' ');
        Assert.Equal("0", viewBox[0]);
        Assert.Equal("0", viewBox[1]);
        Assert.Equal(45, int.Parse(viewBox[2]));
        Assert.True(int.Parse(viewBox[3]) > 45);
        Assert.NotEmpty(svg.Descendants(svg.Name.Namespace + "path"));
        Assert.Empty(svg.Descendants(svg.Name.Namespace + "image"));
        Assert.DoesNotContain("Bing-F5-12345", document.ToString());
        Assert.DoesNotContain("data:image", document.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public async Task Element_svg_physical_size_tracks_the_rotated_dpi_axes(int rotation)
    {
        var template = BarcodeTemplate.Replace("\"Rotation\":90", $"\"Rotation\":{rotation}");
        var request = Request(template, "{}",
            ",\"pageIndex\":0,\"elementIndex\":0,\"escPosDevice\":{\"profile\":{" +
            "\"imageCommand\":\"BitImageEscStar\",\"bitImageMode\":\"DoubleDensity8\"," +
            "\"horizontalDpi\":203,\"verticalDpi\":300,\"motionUnitsPerInch\":203}}");

        var result = await Execute(() => DesignerRequestProcessor.ElementSvg(request, CancellationToken.None));

        Assert.True(result.Context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(result.Bytes));
        var svg = XDocument.Parse(Encoding.UTF8.GetString(result.Bytes)).Root!;
        Assert.Equal("none", (string?)svg.Attribute("preserveAspectRatio"));
        var viewBox = ((string?)svg.Attribute("viewBox"))!.Split(' ');
        var widthDots = int.Parse(viewBox[2], System.Globalization.CultureInfo.InvariantCulture);
        var heightDots = int.Parse(viewBox[3], System.Globalization.CultureInfo.InvariantCulture);
        var quarterTurn = rotation is 90 or 270;
        var widthDpi = quarterTurn ? 300 : 203;
        var heightDpi = quarterTurn ? 203 : 300;
        var widthMm = ParseSvgMillimeters((string)svg.Attribute("width")!);
        var heightMm = ParseSvgMillimeters((string)svg.Attribute("height")!);

        Assert.InRange(Math.Abs(widthMm - widthDots * 25.4d / widthDpi), 0d, 0.000001d);
        Assert.InRange(Math.Abs(heightMm - heightDots * 25.4d / heightDpi), 0d, 0.000001d);
    }

    [Fact]
    public async Task Element_svg_rejects_a_source_path_that_does_not_match_the_selected_layout_element()
    {
        var result = await Execute(() => DesignerRequestProcessor.ElementSvg(
            Request(BarcodeTemplate, "{}", ",\"pageIndex\":0,\"elementIndex\":0,\"sourcePath\":\"Rows[0].Cells[0].Elements[7]\""),
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, result.Context.Response.StatusCode);
        Assert.Contains("ElementSourcePathMismatch", Encoding.UTF8.GetString(result.Bytes));
    }

    [Fact]
    public async Task Element_svg_preserves_preview_pagination_when_omitting_device_hri()
    {
        using var parent = PrintResourceScope.Begin(24 * 1024 * 1024);
        var result = await Execute(() => DesignerRequestProcessor.ElementSvg(
            Request(HriPaginationTemplate, "{}",
                ",\"pageIndex\":1,\"elementIndex\":0,\"sourcePath\":\"Rows[1].Cells[0].Elements[0]\""),
            CancellationToken.None));

        Assert.True(result.Context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(result.Bytes));
        Assert.StartsWith("image/svg+xml", result.Context.Response.ContentType);
        var document = XDocument.Parse(Encoding.UTF8.GetString(result.Bytes));
        Assert.DoesNotContain("67890", document.ToString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    private static DesignerRequest Request(string template, string data, string extra)
    {
        var json = "{\"template\":" + template + ",\"data\":" + data +
                   ",\"protocol\":\"EscPos\",\"escPosLayoutMode\":\"Raster\"" + extra + "}";
        return JsonSerializer.Deserialize<DesignerRequest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static double ParseSvgMillimeters(string value)
    {
        Assert.EndsWith("mm", value, StringComparison.Ordinal);
        return double.Parse(value[..^2], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<(DefaultHttpContext Context, byte[] Bytes)> Execute(Func<IResult> createResult)
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
