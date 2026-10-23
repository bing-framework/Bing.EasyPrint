using System;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using ZXing;
using ZXing.Common;

namespace DesignerHost.Tests;

/// <summary>验证 GS1 二维共享编码、Host 能力与严格关联合并。</summary>
public sealed class Gs1TwoDimensionalHostTests
{
    private const string RequiredFeatures = "raster-element-png,gs1-two-dimensional,gs1-association-validation";

    [Fact]
    public async Task Capabilities_expose_gs1_two_dimensional_and_association_flags()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest("EscPos", "Raster", EmptyTemplate(), "{}"), RequiredFeatures);
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var response = ReadJson(context);
        var gs1 = response.RootElement.GetProperty("gs1TwoDimensional");
        Assert.True(gs1.GetProperty("qr").GetBoolean());
        Assert.True(gs1.GetProperty("dataMatrix").GetBoolean());
        Assert.False(gs1.GetProperty("native").GetBoolean());
        Assert.True(gs1.GetProperty("associationValidation").GetBoolean());
        Assert.Contains("gs1-two-dimensional", context.Response.Headers["X-EasyPrint-Features"].ToString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Gs1_qr_and_data_matrix_element_png_decode_with_gs1_symbology()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":400,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"Gs1\":true,\"Field\":{\"Value\":\"(01)12345678901231(10)LOT%20A\"}}," +
            "{\"Type\":10,\"Format\":\"DataMatrix\",\"Gs1\":true,\"Width\":96,\"Height\":96,\"Field\":{\"Value\":\"(01)12345678901231(10)LOT%20A\"}}" +
            "]}]}]}";
        var previewContext = CreateContext(BuildRequest("EscPos", "Raster", template, "{}"), RequiredFeatures);
        await DesignerRequestExecution.ExecuteAsync(previewContext, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, previewContext.Response.StatusCode);
        using var preview = ReadJson(previewContext);
        var elements = preview.RootElement.GetProperty("pages")[0].GetProperty("elements");
        var qrIndex = -1;
        var dataMatrixIndex = -1;
        for (var index = 0; index < elements.GetArrayLength(); index++)
        {
            var type = elements[index].GetProperty("type").GetInt32();
            if (type == 3) qrIndex = index;
            if (type == 10) dataMatrixIndex = index;
        }
        Assert.True(qrIndex >= 0);
        Assert.True(dataMatrixIndex >= 0);

        Assert.Equal("]Q3", DecodeElement(template, qrIndex).SymbologyIdentifier);
        Assert.Equal("]d2", DecodeElement(template, dataMatrixIndex).SymbologyIdentifier);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("Cpcl", "")]
    [InlineData("Tspl", "")]
    [InlineData("Zpl", "")]
    [InlineData("EscPos", "Raster")]
    public async Task Gs1_qr_and_data_matrix_render_through_image_protocols(string protocol, string layout)
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":400,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"Gs1\":true,\"Field\":{\"Value\":\"(01)12345678901231(10)LOT%20A\"}}," +
            "{\"Type\":10,\"Format\":\"DataMatrix\",\"Gs1\":true,\"Width\":96,\"Height\":96,\"Field\":{\"Value\":\"(01)12345678901231(10)LOT%20A\"}}" +
            "]}]}]}";
        var context = CreateContext(BuildRequest(protocol, layout, template, "{}", ",\"pageIndex\":0"),
            RequiredFeatures);
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.NotEmpty(ReadResponseBytes(context));
        Assert.Equal(0, parent.CurrentBytes);
    }
    [Fact]
    public async Task Strict_association_merges_composite_primary_and_secondary()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":300,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":11,\"LinearType\":11,\"Mode\":0,\"ModuleWidth\":2,\"LinearBarHeight\":40," +
            "\"Field\":{\"Value\":\"(01)12345678901231\"},\"SecondaryField\":{\"Value\":\"(20)12\"}}" +
            "]}]}]}";
        var valid = CreateContext(BuildRequest("Cpcl", "", template, "{}",
            ",\"validateGs1Associations\":true"), "gs1-association-validation");
        await DesignerRequestExecution.ExecuteAsync(valid, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, valid.Response.StatusCode);

        var invalidTemplate = template.Replace("\"Field\":{\"Value\":\"(01)12345678901231\"}",
            "\"Field\":{\"Value\":\"(02)12345678901231\"}").Replace("\"SecondaryField\":{\"Value\":\"(20)12\"}",
            "\"SecondaryField\":{\"Value\":\"(10)LOT\"}");
        var invalid = CreateContext(BuildRequest("Cpcl", "", invalidTemplate, "{}",
            ",\"validateGs1Associations\":true"), "gs1-association-validation");
        await DesignerRequestExecution.ExecuteAsync(invalid, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, invalid.Response.StatusCode);
        using var response = ReadJson(invalid);
        var message = response.RootElement.GetProperty("errors")[0].GetProperty("message").GetString();
        Assert.Contains("02", message);
        Assert.Contains("37", message);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Data_matrix_strict_association_rejects_missing_required_ai_with_element_path()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":300,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":10,\"Format\":\"DataMatrix\",\"Gs1\":true,\"Width\":96,\"Height\":96,\"Field\":{\"Value\":\"(02)12345678901231\"}}" +
            "]}]}]}";
        var invalid = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            ",\"validateGs1Associations\":true"), RequiredFeatures);
        await DesignerRequestExecution.ExecuteAsync(invalid, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, invalid.Response.StatusCode);
        using var response = ReadJson(invalid);
        var error = response.RootElement.GetProperty("errors")[0];
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
        Assert.Contains("02", error.GetProperty("message").GetString());
        Assert.Contains("37", error.GetProperty("message").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Duplicate_gs1_qr_instances_render_and_unit_width_zero_is_rejected()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":500,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"Gs1\":true,\"Field\":{\"Value\":\"(01)12345678901231\"}}," +
            "{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"Gs1\":true,\"Field\":{\"Value\":\"(01)12345678901231\"}}" +
            "]}]}]}";
        var valid = CreateContext(BuildRequest("EscPos", "Raster", template, "{}"), RequiredFeatures);
        await DesignerRequestExecution.ExecuteAsync(valid, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, valid.Response.StatusCode);

        var invalid = CreateContext(BuildRequest("EscPos", "Raster",
            template.Replace("\"UnitWidth\":4", "\"UnitWidth\":0").Replace("\"Width\":400", "\"Width\":1200"),
            "{}"), RequiredFeatures);
        await DesignerRequestExecution.ExecuteAsync(invalid, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, invalid.Response.StatusCode);
        using var response = ReadJson(invalid);
        Assert.Equal("Rows[0].Cells[0].Elements[0]",
            response.RootElement.GetProperty("errors")[0].GetProperty("sourcePath").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(270)]
    public async Task Gs1_qr_rotation_uses_transposed_layout_for_preview_and_png(int rotation)
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var template = "{\"Version\":2,\"Width\":400,\"Height\":500,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":3,\"UnitWidth\":1,\"Width\":450,\"Height\":40,\"Rotation\":" + rotation +
            ",\"Gs1\":true,\"Field\":{\"Value\":\"(01)12345678901231\"}}" +
            "]}]}]}";
        var preview = CreateContext(BuildRequest("EscPos", "Raster", template, "{}"), RequiredFeatures);
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, preview.Response.StatusCode);
        using var json = ReadJson(preview);
        var element = Assert.Single(json.RootElement.GetProperty("pages")[0]
            .GetProperty("elements").EnumerateArray()
            .Where(value => value.GetProperty("type").GetInt32() == 3));
        Assert.Equal(40, element.GetProperty("width").GetInt32());
        Assert.Equal(450, element.GetProperty("height").GetInt32());

        var decoded = DecodeElement(template, 0, scale: 4);
        Assert.Equal(40, decoded.Width);
        Assert.Equal(450, decoded.Height);
        Assert.Equal("]Q3", decoded.SymbologyIdentifier);
        Assert.Equal("0112345678901231", decoded.Text);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Gs1_qr_rotated_occupancy_over_cell_width_is_rejected_before_preview()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":500,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":3,\"UnitWidth\":1,\"Width\":40,\"Height\":450,\"Rotation\":90,\"Gs1\":true," +
            "\"Field\":{\"Value\":\"(01)12345678901231\"}}" +
            "]}]}]}";
        var context = CreateContext(BuildRequest("EscPos", "Raster", template, "{}"), RequiredFeatures);
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        var error = json.RootElement.GetProperty("errors")[0];
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
        Assert.Contains("GS1 QR", error.GetProperty("message").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Gs1_two_dimensional_native_and_pdf417_are_rejected()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string qr = "{\"Version\":2,\"Width\":400,\"Height\":300,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"Gs1\":true,\"Field\":{\"Value\":\"(01)12345678901231\"}}" +
            "]}]}]}";
        var native = CreateContext(BuildRequest("EscPos", "Native", qr, "{}"), "gs1-two-dimensional");
        await DesignerRequestExecution.ExecuteAsync(native, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, native.Response.StatusCode);

        var pdf = CreateContext(BuildRequest("Cpcl", "", "{\"Version\":2,\"Width\":400,\"Height\":300,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":10,\"Format\":\"PDF417\",\"Gs1\":true,\"Width\":240,\"Height\":120,\"Field\":{\"Value\":\"(01)12345678901231\"}}" +
            "]}]}]}", "{}"), "gs1-two-dimensional");
        await DesignerRequestExecution.ExecuteAsync(pdf, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, pdf.Response.StatusCode);
        Assert.Equal(0, parent.CurrentBytes);
    }

    private (string SymbologyIdentifier, string Text, int Width, int Height) DecodeElement(
        string template, int elementIndex, int scale = 1)
    {
        var context = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            $",\"pageIndex\":0,\"elementIndex\":{elementIndex}"), RequiredFeatures);
        DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng)
            .GetAwaiter().GetResult();
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var image = Image.Load<Rgba32>(ReadResponseBytes(context));
        if (scale < 1) throw new ArgumentOutOfRangeException(nameof(scale));
        var width = checked(image.Width * scale);
        var height = checked(image.Height * scale);
        var rgb = new byte[checked(width * height * 3)];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var pixel = image[x / scale, y / scale];
            var index = (y * width + x) * 3;
            rgb[index] = pixel.R;
            rgb[index + 1] = pixel.G;
            rgb[index + 2] = pixel.B;
        }
        var result = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { TryHarder = true }
        }.Decode(new RGBLuminanceSource(rgb, width, height, RGBLuminanceSource.BitmapFormat.RGB24));
        Assert.NotNull(result);
        var symbology = result!.ResultMetadata != null &&
            result.ResultMetadata.TryGetValue(ResultMetadataType.SYMBOLOGY_IDENTIFIER, out var value)
            ? Convert.ToString(value) ?? string.Empty : string.Empty;
        return (symbology, result.Text, image.Width, image.Height);
    }

    private static string EmptyTemplate()
        => "{\"Version\":2,\"Width\":400,\"Height\":200,\"Rows\":[]}";

    private static string BuildRequest(string protocol, string layout, string template, string data, string extra = "")
    {
        var options = "\"protocol\":\"" + protocol + "\",\"encoding\":\"ASCII\",\"feedLines\":0,\"cutAfterPrint\":false";
        if (!string.IsNullOrEmpty(layout)) options += ",\"escPosLayoutMode\":\"" + layout + "\"";
        return "{\"template\":" + template + ",\"data\":" + data + "," + options + extra + "}";
    }

    private static DefaultHttpContext CreateContext(string json, string? requiredFeatures = null)
    {
        var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = "application/json; charset=utf-8";
        if (requiredFeatures != null) context.Request.Headers["X-EasyPrint-Required-Features"] = requiredFeatures;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static System.Text.Json.JsonDocument ReadJson(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return System.Text.Json.JsonDocument.Parse(context.Response.Body);
    }

    private static byte[] ReadResponseBytes(DefaultHttpContext context)
        => Assert.IsType<MemoryStream>(context.Response.Body).ToArray();
}
