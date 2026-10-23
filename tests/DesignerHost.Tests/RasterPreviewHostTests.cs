using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using Xunit;

public sealed class RasterPreviewHostTests
{
    private const string RequiredFeaturesHeader = "X-EasyPrint-Required-Features";
    private const string OrdinaryTemplate =
        "{\"Version\":2,\"Width\":400,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
        "{\"Type\":4,\"BarcodeType\":25,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false," +
        "\"Field\":{\"Value\":\"0950600013435\"}}]}]}]}";
    private const string CompositeTemplate =
        "{\"Version\":3,\"Width\":400,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
        "{\"Type\":11,\"LinearType\":31,\"ExpandedStackedRows\":2,\"Mode\":1,\"ModuleWidth\":2,\"LinearBarHeight\":35," +
        "\"Field\":{\"Value\":\"[91]1234567890123456789012345678901234\"}," +
        "\"SecondaryField\":{\"Value\":\"[20]12\"}}]}]}]}";
    private const string RepeatedTemplate =
        "{\"Version\":3,\"Width\":400,\"Height\":60,\"Rows\":[{\"Height\":60,\"RepeatSource\":\"Items\",\"Cells\":[{\"Elements\":[" +
        "{\"Type\":4,\"BarcodeType\":0,\"Height\":30,\"HumanReadable\":false,\"Field\":{\"Name\":\"Value\"}}]}]}]}";

    [Theory]
    [InlineData("EscPos", "Raster", true, 203)]
    [InlineData("Cpcl", "", false, 200)]
    public async Task Capabilities_classify_real_element_preview_and_page_png(string protocol, string layout,
        bool pagePng, int dpi)
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest(protocol, layout, OrdinaryTemplate, "{}"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var response = ReadJson(context);
        var raster = response.RootElement.GetProperty("rasterPreview");
        Assert.True(raster.GetProperty("softwareBitmap").GetBoolean());
        Assert.True(raster.GetProperty("elementPng").GetBoolean());
        Assert.Equal(pagePng, raster.GetProperty("pagePng").GetBoolean());
        var types = raster.GetProperty("elementTypes").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        Assert.Contains(0, types);
        Assert.Equal(pagePng, types.Contains(1));
        Assert.Equal(pagePng, types.Contains(4));
        Assert.Equal(dpi, raster.GetProperty("dpiX").GetInt32());
        if (!pagePng) Assert.False(string.IsNullOrWhiteSpace(raster.GetProperty("pagePngReason").GetString()));
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Element_png_uses_real_encoder_raster_dimensions_and_dpi()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var body = BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}",
            ",\"pageIndex\":0,\"elementIndex\":0");
        var context = CreateContext(body, "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("image/png", context.Response.ContentType);
        var bytes = ReadResponseBytes(context);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes.Take(8).ToArray());
        using var image = Image.Load<Rgba32>(bytes);
        Assert.True(image.Width > 0 && image.Height > 0);
        Assert.Equal(203, (int)Math.Round(image.Metadata.HorizontalResolution * 0.0254));
        Assert.Equal(203, (int)Math.Round(image.Metadata.VerticalResolution * 0.0254));
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Composite_element_png_uses_expanded_stacked_rows_and_quiet_zone()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var previewContext = CreateContext(BuildRequest("Cpcl", "", CompositeTemplate, "{}"));
        await DesignerRequestExecution.ExecuteAsync(previewContext, DesignerRequestProcessor.Preview);
        using var previewJson = ReadJson(previewContext);
        var previewElement = previewJson.RootElement.GetProperty("pages")[0].GetProperty("elements")[0];
        var expectedWidth = previewElement.GetProperty("width").GetInt32();
        var expectedHeight = previewElement.GetProperty("height").GetInt32();
        var body = BuildRequest("Cpcl", "", CompositeTemplate, "{}", ",\"pageIndex\":0,\"elementIndex\":0");
        var context = CreateContext(body, "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var bytes = ReadResponseBytes(context);
        using var image = Image.Load<Rgba32>(bytes);
        Assert.Equal(expectedWidth, image.Width);
        Assert.Equal(expectedHeight, image.Height);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Single_page_png_and_multi_page_png_zip_are_both_available()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var single = CreateContext(BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}", ",\"pageIndex\":0"),
            "raster-page-png");
        await DesignerRequestExecution.ExecuteAsync(single, DesignerRequestProcessor.PagePng);
        Assert.Equal(StatusCodes.Status200OK, single.Response.StatusCode);
        Assert.Equal("image/png", single.Response.ContentType);
        using (var image = Image.Load<Rgba32>(ReadResponseBytes(single))) Assert.True(image.Width > 0);

        var data = "{\"Items\":[{\"Value\":\"12345\"},{\"Value\":\"67890\"}]}";
        var multi = CreateContext(BuildRequest("EscPos", "Raster", RepeatedTemplate, data), "raster-page-png");
        await DesignerRequestExecution.ExecuteAsync(multi, DesignerRequestProcessor.PagePng);
        Assert.Equal(StatusCodes.Status200OK, multi.Response.StatusCode);
        Assert.Equal("application/zip", multi.Response.ContentType);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(multi)), ZipArchiveMode.Read);
        var entries = archive.Entries.Where(entry => entry.FullName.EndsWith(".png")).ToArray();
        Assert.Equal(2, entries.Length);
        foreach (var entry in entries)
        {
            using var stream = entry.Open();
            using var image = Image.Load<Rgba32>(stream);
            Assert.True(image.Width > 0 && image.Height > 0);
        }
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Page_png_rejects_rgba_pixels_over_request_budget_without_partial_output()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":1000,\"Height\":1000,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":7,\"Width\":10,\"Height\":10,\"Filled\":true}]}]}]}";
        var context = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            ",\"maxWorkingBytes\":1048576,\"pageIndex\":0"), "raster-page-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.PagePng);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        using var response = ReadJson(context);
        Assert.Equal("ResourceLimitExceeded", response.RootElement.GetProperty("errors")[0]
            .GetProperty("code").GetString());
        var bytes = ReadResponseBytes(context);
        Assert.NotEmpty(bytes);
        Assert.NotEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Page_png_cancellation_releases_resources_without_output()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}",
            ",\"pageIndex\":0"), "raster-page-png");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.RequestAborted = cancellation.Token;
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.PagePng));
        Assert.Empty(ReadResponseBytes(context));
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Native_protocols_report_page_png_unavailable_without_fake_image()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest("Cpcl", "", OrdinaryTemplate, "{}"), "raster-page-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.PagePng);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        Assert.Equal("UnsupportedCapability", response.RootElement.GetProperty("errors")[0]
            .GetProperty("code").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData(",\"pageIndex\":1,\"elementIndex\":0", "InvalidPageIndex")]
    [InlineData(",\"pageIndex\":0,\"elementIndex\":99", "InvalidElementIndex")]
    [InlineData(",\"pageIndex\":0,\"elementIndex\":0,\"sourcePath\":\"Rows[0].Cells[0].Elements[9]\"",
        "ElementSourcePathMismatch")]
    public async Task Invalid_selection_returns_structured_error_without_output(string extra, string code)
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}", extra),
            "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        Assert.Equal(code, response.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Cpcl_rotated_or_ratiod_barcode_is_rejected_instead_of_drawing_wrong_pixels()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":400,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"HumanReadable\":false,\"Rotation\":270,\"Field\":{\"Value\":\"12345\"}}" +
            "]}]}]}";
        var context = CreateContext(BuildRequest("Cpcl", "", template, "{}",
            ",\"pageIndex\":0,\"elementIndex\":0"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        Assert.Equal("UnsupportedCapability", response.RootElement.GetProperty("errors")[0]
            .GetProperty("code").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Tspl_explicit_wide_module_barcode_is_rejected_instead_of_ignoring_ratio()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"WideModuleWidth\":4,\"Field\":{\"Value\":\"12345\"}}" +
            "]}]}]}";
        var context = CreateContext(BuildRequest("Tspl", "", template, "{}",
            ",\"pageIndex\":0,\"elementIndex\":0"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        Assert.Equal("UnsupportedCapability", response.RootElement.GetProperty("errors")[0]
            .GetProperty("code").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task EscPos_native_barcode_with_hri_is_rejected_instead_of_using_host_text()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"HumanReadable\":true,\"Field\":{\"Value\":\"12345\"}}" +
            "]}]}]}";
        var context = CreateContext(BuildRequest("EscPos", "Native", template, "{}",
            ",\"pageIndex\":0,\"elementIndex\":0"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        Assert.Equal("UnsupportedCapability", response.RootElement.GetProperty("errors")[0]
            .GetProperty("code").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Invalid_barcode_data_fails_before_png_output()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var template = OrdinaryTemplate.Replace("0950600013435", "not-a-barcode");
        var context = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            ",\"pageIndex\":0,\"elementIndex\":0"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Element_png_honors_ordinary_expanded_stacked_columns()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string expanded = "{\"Type\":4,\"BarcodeType\":31,\"ExpandedStackedColumns\":1,\"Height\":35,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"Field\":{\"Value\":\"[91]1234567890123456789012345678901234\"}}";
        var template = "{\"Version\":2,\"Width\":700,\"Height\":500,\"Rows\":[{\"Cells\":[{\"Elements\":[" + expanded + "]}]}]}";
        var previewContext = CreateContext(BuildRequest("EscPos", "Raster", template, "{}"));
        await DesignerRequestExecution.ExecuteAsync(previewContext, DesignerRequestProcessor.Preview);
        using var previewJson = ReadJson(previewContext);
        var expectedHeight = previewJson.RootElement.GetProperty("pages")[0].GetProperty("elements")[0]
            .GetProperty("height").GetInt32();
        var context = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            ",\"pageIndex\":0,\"elementIndex\":0"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var image = Image.Load<Rgba32>(ReadResponseBytes(context));
        Assert.Equal(expectedHeight, image.Height);
        var lastBlackRow = 0;
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
            if (image[x, y].R < 128) lastBlackRow = y;
        Assert.True(lastBlackRow > image.Height * 3 / 4, $"lastBlackRow={lastBlackRow}, height={image.Height}");
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Element_png_maps_every_primitive_owned_by_selected_layout_element()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":7,\"Width\":40,\"Height\":30}," +
            "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"Field\":{\"Value\":\"12345\"}}" +
            "]}]}]}";
        var rectangle = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            ",\"pageIndex\":0,\"elementIndex\":0"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(rectangle, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status200OK, rectangle.Response.StatusCode);
        using (var image = Image.Load<Rgba32>(ReadResponseBytes(rectangle)))
        {
            Assert.Equal(40, image.Width);
            Assert.Equal(30, image.Height);
            Assert.True(IsDark(image[0, 0]), "top-left border missing");
            Assert.True(IsDark(image[39, 0]), "top-right border missing");
            Assert.True(IsDark(image[0, 29]), "bottom-left border missing");
            Assert.True(IsDark(image[39, 29]), "bottom-right border missing");
            Assert.False(IsDark(image[20, 15]), "unfilled rectangle should have white center");
        }

        var barcode = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            ",\"pageIndex\":0,\"elementIndex\":1"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(barcode, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status200OK, barcode.Response.StatusCode);
        using var barcodeImage = Image.Load<Rgba32>(ReadResponseBytes(barcode));
        var rgb = new byte[checked(barcodeImage.Width * barcodeImage.Height * 3)];
        for (var y = 0; y < barcodeImage.Height; y++)
        for (var x = 0; x < barcodeImage.Width; x++)
        {
            var pixel = barcodeImage[x, y];
            var index = (y * barcodeImage.Width + x) * 3;
            rgb[index] = pixel.R;
            rgb[index + 1] = pixel.G;
            rgb[index + 2] = pixel.B;
        }
        var result = new BarcodeReaderGeneric { Options = new ZXing.Common.DecodingOptions { TryHarder = true } }
            .Decode(new RGBLuminanceSource(rgb, barcodeImage.Width, barcodeImage.Height,
                RGBLuminanceSource.BitmapFormat.RGB24));
        Assert.NotNull(result);
        Assert.Equal("12345", result!.Text);
        Assert.Equal(0, parent.CurrentBytes);
    }

    private static bool IsDark(Rgba32 pixel) => pixel.R < 128;
    [Fact]
    public async Task Element_png_selects_frame_border_and_empty_text_without_index_shift()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Border\":{" +
            "\"TopStyle\":1,\"RightStyle\":1,\"LeftStyle\":1,\"BottomStyle\":1}," +
            "\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Value\":\"\"}}]}]}]}";
        var previewContext = CreateContext(BuildRequest("EscPos", "Raster", template, "{}"));
        await DesignerRequestExecution.ExecuteAsync(previewContext, DesignerRequestProcessor.Preview);
        using var preview = ReadJson(previewContext);
        var elements = preview.RootElement.GetProperty("pages")[0].GetProperty("elements");
        var frameIndex = -1;
        var textIndex = -1;
        for (var index = 0; index < elements.GetArrayLength(); index++)
        {
            var item = elements[index];
            if (item.GetProperty("type").GetInt32() == 0 && item.GetProperty("kind").GetString() == "cell") frameIndex = index;
            if (item.GetProperty("type").GetInt32() == 1) textIndex = index;
        }
        Assert.True(frameIndex >= 0);
        Assert.True(textIndex >= 0);

        var frame = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            $",\"pageIndex\":0,\"elementIndex\":{frameIndex}"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(frame, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status200OK, frame.Response.StatusCode);
        using (var image = Image.Load<Rgba32>(ReadResponseBytes(frame)))
        {
            Assert.True(IsDark(image[0, 0]), $"size={image.Width}x{image.Height} p00={image[0, 0].R} p01={image[0, 1].R} p10={image[1, 0].R}");
            Assert.True(IsDark(image[image.Width - 1, 0]));
            Assert.True(IsDark(image[0, image.Height - 1]));
            Assert.True(IsDark(image[image.Width - 1, image.Height - 1]));
        }

        var text = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            $",\"pageIndex\":0,\"elementIndex\":{textIndex}"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(text, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status200OK, text.Response.StatusCode);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Element_png_keeps_repeated_instances_on_their_own_pages()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":3,\"Width\":400,\"Height\":60,\"Rows\":[{\"Height\":60,\"RepeatSource\":\"Items\",\"Cells\":[{\"Elements\":[" +
            "{\"Type\":7,\"Width\":40,\"Height\":30}," +
            "{\"Type\":4,\"BarcodeType\":0,\"Height\":30,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"Field\":{\"Name\":\"Value\"}}" +
            "]}]}]}";
        const string data = "{\"Items\":[{\"Value\":\"12345\"},{\"Value\":\"67890\"}]}";
        var previewContext = CreateContext(BuildRequest("EscPos", "Raster", template, data));
        await DesignerRequestExecution.ExecuteAsync(previewContext, DesignerRequestProcessor.Preview);
        using var preview = ReadJson(previewContext);
        var pages = preview.RootElement.GetProperty("pages");
        Assert.Equal(2, pages.GetArrayLength());
        for (var pageIndex = 0; pageIndex < pages.GetArrayLength(); pageIndex++)
        {
            var elements = pages[pageIndex].GetProperty("elements");
            var rectangleIndex = -1;
            var barcodeIndex = -1;
            for (var index = 0; index < elements.GetArrayLength(); index++)
            {
                if (elements[index].GetProperty("type").GetInt32() == 7) rectangleIndex = index;
                if (elements[index].GetProperty("type").GetInt32() == 4) barcodeIndex = index;
            }
            Assert.True(rectangleIndex >= 0);
            Assert.True(barcodeIndex >= 0);
            var rectangle = CreateContext(BuildRequest("EscPos", "Raster", template, data,
                $",\"pageIndex\":{pageIndex},\"elementIndex\":{rectangleIndex}"), "raster-element-png");
            await DesignerRequestExecution.ExecuteAsync(rectangle, DesignerRequestProcessor.ElementPng);
            Assert.Equal(StatusCodes.Status200OK, rectangle.Response.StatusCode);
            using (var image = Image.Load<Rgba32>(ReadResponseBytes(rectangle)))
                Assert.Equal((40, 30), (image.Width, image.Height));
            var barcode = CreateContext(BuildRequest("EscPos", "Raster", template, data,
                $",\"pageIndex\":{pageIndex},\"elementIndex\":{barcodeIndex}"), "raster-element-png");
            await DesignerRequestExecution.ExecuteAsync(barcode, DesignerRequestProcessor.ElementPng);
            Assert.Equal(StatusCodes.Status200OK, barcode.Response.StatusCode);
            using var barcodeImage = Image.Load<Rgba32>(ReadResponseBytes(barcode));
            Assert.True(barcodeImage.Width > 0 && barcodeImage.Height > 0);
        }
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"Field\":{\"Value\":\"12345\"}}", "12345")]
    [InlineData("{\"Type\":4,\"BarcodeType\":5,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false,\"Field\":{\"Value\":\"1234567890128\"}}", "1234567890128")]
    [InlineData("{\"Type\":3,\"UnitWidth\":6,\"ErrorLevel\":\"M\",\"Field\":{\"Value\":\"HELLO\"}}", "HELLO")]
    [InlineData("{\"Type\":10,\"Format\":\"DataMatrix\",\"Width\":160,\"Height\":160,\"Field\":{\"Value\":\"MATRIX\"}}", "MATRIX")]
    [InlineData("{\"Type\":10,\"Format\":\"PDF417\",\"Width\":240,\"Height\":120,\"Field\":{\"Value\":\"PDF417\"}}", "PDF417")]
    public async Task Element_png_decodes_with_independent_zxing_reader(string element, string expected)
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var template = "{\"Version\":2,\"Width\":400,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            element + "]}]}]}";
        var context = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            ",\"pageIndex\":0,\"elementIndex\":0"), "raster-element-png");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var image = Image.Load<Rgba32>(ReadResponseBytes(context));
        var rgb = new byte[checked(image.Width * image.Height * 3)];
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];
            var index = (y * image.Width + x) * 3;
            rgb[index] = pixel.R;
            rgb[index + 1] = pixel.G;
            rgb[index + 2] = pixel.B;
        }
        var source = new RGBLuminanceSource(rgb, image.Width, image.Height,
            RGBLuminanceSource.BitmapFormat.RGB24);
        var reader = new BarcodeReaderGeneric { Options = new ZXing.Common.DecodingOptions { TryHarder = true } };
        var result = reader.Decode(source);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Text);
        Assert.Equal(0, parent.CurrentBytes);
    }

    private static string BuildRequest(string protocol, string layout, string template, string data,
        string extra = "")
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
        if (requiredFeatures != null) context.Request.Headers[RequiredFeaturesHeader] = requiredFeatures;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static JsonDocument ReadJson(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body);
    }

    private static byte[] ReadResponseBytes(DefaultHttpContext context)
        => Assert.IsType<MemoryStream>(context.Response.Body).ToArray();
}
