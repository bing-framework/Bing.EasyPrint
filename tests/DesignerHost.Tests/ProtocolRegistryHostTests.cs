using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class ProtocolRegistryHostTests
{
    private const string Template =
        "{\"Version\":2,\"Width\":576,\"Height\":360,\"Rows\":[{\"Height\":360,\"Cells\":[{\"Width\":576,\"Elements\":[" +
        "{\"Type\":1,\"FontSize\":24,\"FontName\":0,\"Field\":{\"Value\":\"DEMO CUSTOM PROTOCOL\"}," +
        "\"StartCoordinate\":{\"X\":20,\"Y\":20}}," +
        "{\"Type\":5,\"LineWidth\":2,\"StartCoordinate\":{\"X\":20,\"Y\":80},\"LineEndCoordinate\":{\"X\":500,\"Y\":80}}," +
        "{\"Type\":7,\"Width\":180,\"Height\":60,\"LineWidth\":1,\"StartCoordinate\":{\"X\":20,\"Y\":100}}" +
        "]}]}]}";

    private const string Config =
        "\"protocolConfig\":{\"characterWidth\":10,\"lineHeight\":30,\"maxPageHeight\":600,\"supportsFinishing\":true}";

    private static string Request(string extra = "")
        => "{\"template\":" + Template + ",\"data\":{},\"protocolId\":\"demo.protocol\"," + Config + extra + "}";

    [Fact]
    public void Directory_default_and_request_configuration_capabilities_are_distinct()
    {
        var registry = DesignerProtocolRegistry.CreateDefault();
        var descriptor = registry.Descriptors.Single(item => item.Id == "demo.protocol");
        Assert.Equal(203, descriptor.Capabilities!.Dpi);
        Assert.True(registry.TryGetAdapter("demo.protocol",
            "{\"dpi\":300,\"maxPageHeight\":700}", out var adapter, out _));
        Assert.Equal(300, adapter!.Capabilities.Dpi);
        Assert.Equal(700, adapter.Capabilities.MaxPageHeightDots);
        Assert.False(adapter.Capabilities.SupportsGs1QrCode);
    }
    [Fact]
    public void Registry_exposes_builtins_and_the_independent_sample()
    {
        var registry = DesignerProtocolRegistry.CreateDefault();
        var descriptors = registry.Descriptors;
        Assert.Contains(descriptors, item => item.Id == "cpcl" && item.IsBuiltIn);
        Assert.Contains(descriptors, item => item.Id == "demo.protocol" && !item.IsBuiltIn);
        Assert.True(registry.IsFrozen);
        Assert.True(registry.TryGetAdapter("demo.protocol", "{\"characterWidth\":8}", out var adapter, out _));
        Assert.Equal("demo.protocol", adapter!.ProtocolId);
        Assert.True(adapter.Capabilities.SupportsElementType(1));
        Assert.True(adapter.Capabilities.SupportsElementType(4));
        Assert.True(adapter.Capabilities.SupportsElementType(3));
        Assert.False(adapter.Capabilities.SupportsElementType(11));
    }

    [Fact]
    public async Task Custom_capabilities_are_generated_from_the_adapter_snapshot()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(Request());
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var json = ReadJson(context);
        var root = json.RootElement;
        Assert.Equal("demo.protocol", root.GetProperty("protocolId").GetString());
        Assert.False(root.TryGetProperty("protocol", out _));
        Assert.Equal(new[] { 0, 1, 5 }, root.GetProperty("barcodeTypes").EnumerateArray()
            .Select(item => item.GetInt32()).ToArray());
        Assert.Equal(new[] { 1, 3, 4, 5, 7 }, root.GetProperty("elementTypes").EnumerateArray()
            .Select(item => item.GetInt32()).ToArray());
        Assert.False(root.GetProperty("supportsCut").GetBoolean());
        Assert.True(root.GetProperty("rasterPreview").GetProperty("softwareBitmap").GetBoolean());
        Assert.False(root.GetProperty("rasterPreview").GetProperty("pagePng").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("gs1TwoDimensional").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("rasterPreview").GetProperty("pagePngReason").GetString()));
        var dpiContext = CreateContext("{\"protocolId\":\"demo.protocol\",\"protocolConfig\":{\"dpi\":300}}");
        await DesignerRequestExecution.ExecuteAsync(dpiContext, DesignerRequestProcessor.Capabilities);
        using var dpiJson = ReadJson(dpiContext);
        Assert.Equal(300, dpiJson.RootElement.GetProperty("dpi").GetInt32());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Custom_capabilities_use_non_default_adapter_configuration_and_limits()
    {
        var options = new TemplateRenderOptions
        {
            ProtocolId = "independent.test",
            CustomProtocol = new IndependentProtocolAdapter()
        };
        var context = CreateContext("{}");
        await DesignerRequestProcessor.CustomCapabilities(options, CancellationToken.None)
            .ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var json = ReadJson(context);
        var root = json.RootElement;
        Assert.Equal(300, root.GetProperty("dpi").GetInt32());
        Assert.False(root.GetProperty("supportsCoordinates").GetBoolean());
        Assert.True(root.GetProperty("supportsHostFontFamily").GetBoolean());
        Assert.Equal(new[] { 1, 2 }, root.GetProperty("fontKinds").EnumerateArray()
            .Select(item => item.GetInt32()).ToArray());
        Assert.Equal(96, root.GetProperty("textSize").GetProperty("maximum").GetInt32());
        Assert.True(root.GetProperty("supportsLegacyDeviceFont").GetBoolean());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("gs1TwoDimensional").ValueKind);
    }

    [Fact]
    public async Task Custom_text_size_over_the_declared_maximum_is_rejected_before_render()
    {
        var context = CreateContext(Request().Replace("\"FontSize\":24", "\"FontSize\":513"));
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        Assert.Contains("513", json.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task Custom_copies_over_the_physical_page_limit_are_rejected_before_render()
    {
        var context = CreateContext(Request(",\"copies\":101"));
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        Assert.Contains("页数上限",
            json.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public void Sample_backend_supports_finishing_and_rejects_it_when_not_declared()
    {
        var document = new PrintDocument();
        document.AddPage(100, 100);
        var request = new PrintProtocolFinishingRequest
        {
            FeedAmount = 3,
            Cut = true,
            Placement = PrintProtocolFinishingPlacement.JobEnd,
        };

        var config = "{\"characterWidth\":6,\"lineHeight\":24,\"maxPageHeight\":512,\"supportsFinishing\":true}";
        var registry = new PrintProtocolRegistry();
        registry.Register(DemoProtocol.DemoProtocolExtension.CreateRegistration());
        registry.Freeze();
        using (var backend = registry.CreateBackend("demo.protocol", config))
        {
            var output = PrintProtocolOutputComposer.Render(backend, document, request);
            Assert.Single(output.Pages);
            Assert.Single(output.FinishingFiles);
            Assert.Equal(PrintOutputSegmentKind.Control, output.FinishingFiles[0].Output.Segments[0].Kind);
            Assert.Contains("FEED 3 Lines",
                Encoding.UTF8.GetString(output.FinishingFiles[0].Output.Segments[0].Data));
        }

        var legacyDocument = new PrintDocument();
        var legacyPage = legacyDocument.AddPage(100, 100);
        legacyPage.FeedLines = 3;
        legacyPage.CutAfterPrint = true;
        using (var backend = registry.CreateBackend("demo.protocol", config))
        {
            Assert.Throws<PrintProtocolBackendException>(() =>
                backend.Validate(legacyDocument, CancellationToken.None));
        }
        var noFinishConfig =
            "{\"characterWidth\":6,\"lineHeight\":24,\"maxPageHeight\":512,\"supportsFinishing\":false}";
        using (var backend = registry.CreateBackend("demo.protocol", noFinishConfig))
        {
            Assert.Throws<NotSupportedException>(() =>
                PrintProtocolOutputComposer.Render(backend, document, request));
        }
    }

    [Fact]
    public async Task Custom_auto_height_uses_the_declared_maximum_page_height()
    {
        const string template = "{\"Version\":3,\"Width\":100,\"Height\":0,\"AutoHeight\":true," +
            "\"Rows\":[{\"Height\":24,\"RepeatSource\":\"Items\",\"Cells\":[{\"Elements\":[" +
            "{\"Type\":1,\"FontSize\":24,\"Field\":{\"Name\":\"Value\"}}]}]}]}";
        var body = "{\"template\":" + template + ",\"data\":{\"Items\":[" +
            "{\"Value\":\"A\"},{\"Value\":\"B\"},{\"Value\":\"C\"},{\"Value\":\"D\"}]}," +
            "\"protocolId\":\"demo.protocol\",\"protocolConfig\":{\"maxPageHeight\":60}}";
        var context = CreateContext(body);
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var json = ReadJson(context);
        var pages = json.RootElement.GetProperty("pages");
        Assert.Equal(2, pages.GetArrayLength());
        Assert.All(pages.EnumerateArray(), page => Assert.True(page.GetProperty("height").GetInt32() <= 60));
    }
    [Fact]
    public async Task Custom_preview_render_and_bundle_use_the_registered_backend()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var preview = CreateContext(Request());
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, preview.Response.StatusCode);
        using var previewJson = ReadJson(preview);
        Assert.Equal("demo.protocol", previewJson.RootElement.GetProperty("protocolId").GetString());
        Assert.True(previewJson.RootElement.GetProperty("pages").GetArrayLength() > 0);

        var render = CreateContext(Request());
        await DesignerRequestExecution.ExecuteAsync(render, DesignerRequestProcessor.Render);
        Assert.Equal(StatusCodes.Status200OK, render.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(render)), ZipArchiveMode.Read);
        var page = archive.GetEntry("page-001.demo-protocol");
        Assert.NotNull(page);
        using var reader = new StreamReader(page!.Open());
        Assert.Contains("DEMO 1", reader.ReadToEnd());

        var bundle = CreateContext(Request(",\"pageIndex\":0,\"imageTypes\":[]"));
        await DesignerRequestExecution.ExecuteAsync(bundle, DesignerRequestProcessor.PreviewBundle);
        Assert.Equal(StatusCodes.Status200OK, bundle.Response.StatusCode);
        using var bundleArchive = new ZipArchive(new MemoryStream(ReadResponseBytes(bundle)), ZipArchiveMode.Read);
        var manifest = bundleArchive.GetEntry("manifest.json");
        Assert.NotNull(manifest);
        using var manifestReader = new StreamReader(manifest!.Open());
        using var manifestJson = JsonDocument.Parse(manifestReader.ReadToEnd());
        Assert.Equal("demo.protocol", manifestJson.RootElement.GetProperty("protocolId").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Custom_qr_code_exposes_software_png_and_bundle()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":200,\"Height\":200,\"Rows\":[{\"Height\":200," +
            "\"Cells\":[{\"Width\":200,\"Elements\":[{\"Type\":3,\"UnitWidth\":2,\"ErrorLevel\":\"M\"," +
            "\"Field\":{\"Value\":\"HELLO\"},\"StartCoordinate\":{\"X\":10,\"Y\":10}}]}]}]}";
        const string request = "{\"template\":" + template + ",\"data\":{},\"protocolId\":\"demo.protocol\"," +
            "\"protocolConfig\":{\"qrModuleWidth\":4,\"maxPageHeight\":600},\"pageIndex\":0,\"elementIndex\":0,\"imageTypes\":[3]}";
        var context = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var png = ReadResponseBytes(context);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());

        var bundle = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(bundle, DesignerRequestProcessor.PreviewBundle);
        Assert.Equal(StatusCodes.Status200OK, bundle.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(bundle)), ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);
        using var reader = new StreamReader(manifestEntry!.Open());
        using var manifest = JsonDocument.Parse(reader.ReadToEnd());
        Assert.Equal(1, manifest.RootElement.GetProperty("images").GetArrayLength());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Custom_finishing_is_a_separate_file_and_must_be_declared()
    {
        var template = "{\"Version\":2,\"Width\":100,\"Height\":100,\"Rows\":[{\"Height\":24," +
            "\"Cells\":[{\"Width\":100,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Value\":\"A\"}}]}]}]}";
        var body = "{\"template\":" + template + ",\"data\":{},\"protocolId\":\"demo.protocol\"," +
            "\"protocolConfig\":{\"characterWidth\":6,\"lineHeight\":24,\"maxPageHeight\":200,\"supportsFinishing\":true}," +
            "\"finishing\":{\"feedAmount\":3,\"feedUnit\":\"Lines\",\"cut\":true,\"placement\":\"JobEnd\"}}";
        var context = CreateContext(body);
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(context)), ZipArchiveMode.Read);
        Assert.Single(archive.Entries.Where(entry => entry.FullName == "finish-001.demo-protocol"));
        var pageEntry = archive.GetEntry("page-001.demo-protocol");
        var finishEntry = archive.GetEntry("finish-001.demo-protocol");
        Assert.NotNull(pageEntry);
        Assert.NotNull(finishEntry);
        Assert.NotEqual(0, finishEntry!.Length);
        using var finishReader = new StreamReader(finishEntry.Open());
        Assert.Contains("FEED 3 Lines", finishReader.ReadToEnd());
        var manifestEntry = archive.GetEntry("job.json");
        Assert.NotNull(manifestEntry);
        using var reader = new StreamReader(manifestEntry!.Open());
        using var manifest = JsonDocument.Parse(reader.ReadToEnd());
        Assert.Equal(2, manifest.RootElement.GetProperty("outputVersion").GetInt32());
        Assert.True(manifest.RootElement.GetProperty("preserveFileBoundaries").GetBoolean());
        var files = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(new[] { "page-001.demo-protocol", "finish-001.demo-protocol" },
            files.Select(file => file.GetProperty("file").GetString()).ToArray());
        Assert.Equal(1, manifest.RootElement.GetProperty("pages").GetArrayLength());
        Assert.Equal(1, manifest.RootElement.GetProperty("finishing").GetArrayLength());

        var unsupported = body.Replace("\"supportsFinishing\":true", "\"supportsFinishing\":false");
        var error = await ExecuteError(unsupported, DesignerRequestProcessor.Render);
        Assert.Equal(StatusCodes.Status400BadRequest, error.Status);
        using var errorJson = JsonDocument.Parse(error.Body);
        Assert.Equal("UnsupportedCapability",
            errorJson.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Custom_copies_and_page_end_finishing_preserve_physical_copy_order()
    {
        var template = "{\"Version\":3,\"Width\":100,\"Height\":100,\"Rows\":[{\"Height\":24," +
            "\"Cells\":[{\"Width\":100,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Value\":\"A\"}}]}]}]}";
        var body = "{\"template\":" + template + ",\"data\":{},\"protocolId\":\"demo.protocol\"," +
            "\"protocolConfig\":{\"maxPageHeight\":200,\"supportsFinishing\":true},\"copies\":2," +
            "\"finishing\":{\"feedAmount\":3,\"feedUnit\":\"Lines\",\"cut\":true,\"placement\":\"PageEnd\"}}";
        var context = CreateContext(body);
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(context)), ZipArchiveMode.Read);
        Assert.Equal(1, archive.Entries.Count(entry => entry.FullName == "page-001.demo-protocol"));
        Assert.Equal(1, archive.Entries.Count(entry => entry.FullName == "page-002.demo-protocol"));
        Assert.Equal(1, archive.Entries.Count(entry => entry.FullName == "finish-001.demo-protocol"));
        Assert.Equal(1, archive.Entries.Count(entry => entry.FullName == "finish-002.demo-protocol"));
        using (var first = new StreamReader(archive.GetEntry("page-001.demo-protocol")!.Open()))
            Assert.Equal(1, first.ReadToEnd().Split("DEMO 1", StringSplitOptions.None).Length - 1);
        using (var second = new StreamReader(archive.GetEntry("page-002.demo-protocol")!.Open()))
            Assert.Equal(1, second.ReadToEnd().Split("DEMO 1", StringSplitOptions.None).Length - 1);
        using var manifestReader = new StreamReader(archive.GetEntry("job.json")!.Open());
        using var manifest = JsonDocument.Parse(manifestReader.ReadToEnd());
        var files = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(new[]
        {
            "page-001.demo-protocol", "finish-001.demo-protocol",
            "page-002.demo-protocol", "finish-002.demo-protocol"
        }, files.Select(file => file.GetProperty("file").GetString()).ToArray());
        var pages = manifest.RootElement.GetProperty("pages").EnumerateArray().ToArray();
        Assert.Equal(new[] { 0, 0 }, pages.Select(page => page.GetProperty("sourcePageIndex").GetInt32()).ToArray());
        Assert.Equal(new[] { 0, 1 }, pages.Select(page => page.GetProperty("copyIndex").GetInt32()).ToArray());
    }

    [Fact]
    public async Task Custom_undeclared_gs1_qr_and_text_styles_are_rejected()
    {
        var gs1 = "{\"template\":{\"Version\":3,\"Width\":400,\"Height\":400,\"Rows\":[{\"Height\":400," +
            "\"Cells\":[{\"Width\":400,\"Elements\":[{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\"," +
            "\"Gs1\":true,\"Field\":{\"Value\":\"NOT-GS1\"}}]}]}]},\"data\":{},\"protocolId\":\"demo.protocol\"," +
            "\"protocolConfig\":{\"maxPageHeight\":600,\"qrModuleWidth\":4}}";
        var gs1Error = await ExecuteError(gs1);
        Assert.Equal(StatusCodes.Status400BadRequest, gs1Error.Status);
        using (var json = JsonDocument.Parse(gs1Error.Body))
            Assert.Contains("GS1 QR", json.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());

        var style = "{\"template\":{\"Version\":3,\"Width\":400,\"Height\":400,\"Rows\":[{\"Height\":400," +
            "\"Cells\":[{\"Width\":400,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Bold\":true," +
            "\"Reverse\":true,\"Field\":{\"Value\":\"A\"}}]}]}]},\"data\":{},\"protocolId\":\"demo.protocol\"," +
            "\"protocolConfig\":{\"maxPageHeight\":600}}";
        var styleError = await ExecuteError(style);
        Assert.Equal(StatusCodes.Status400BadRequest, styleError.Status);
        using (var json = JsonDocument.Parse(styleError.Body))
            Assert.Contains("粗体", json.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task Custom_qr_metrics_match_shared_raster_geometry()
    {
        const string template = "{\"Version\":3,\"Width\":400,\"Height\":400,\"Rows\":[{\"Height\":400," +
            "\"Cells\":[{\"Width\":400,\"Elements\":[{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\"," +
            "\"Field\":{\"Value\":\"HELLO\"}}]}]}]}";
        var custom = CreateContext("{\"template\":" + template + ",\"data\":{},\"protocolId\":\"demo.protocol\"," +
            "\"protocolConfig\":{\"maxPageHeight\":600,\"qrModuleWidth\":4}}");
        await DesignerRequestExecution.ExecuteAsync(custom, DesignerRequestProcessor.Preview);
        using var customJson = ReadJson(custom);
        var customWidth = customJson.RootElement.GetProperty("pages")[0].GetProperty("elements")[0]
            .GetProperty("width").GetInt32();

        var raster = CreateContext("{\"template\":" + template + ",\"data\":{},\"protocol\":\"EscPos\"," +
            "\"escPosLayoutMode\":\"Raster\"}");
        await DesignerRequestExecution.ExecuteAsync(raster, DesignerRequestProcessor.Preview);
        using var rasterJson = ReadJson(raster);
        var rasterWidth = rasterJson.RootElement.GetProperty("pages")[0].GetProperty("elements")[0]
            .GetProperty("width").GetInt32();
        Assert.Equal(116, customWidth);
        Assert.Equal(rasterWidth, customWidth);
    }
    [Fact]
    public async Task Custom_rotated_barcode_bundle_has_a_real_image_and_unrotated_conversion()
    {
        var body = "{\"template\":{\"Version\":3,\"Width\":400,\"Height\":400,\"Rows\":[{\"Height\":400," +
            "\"Cells\":[{\"Width\":400,\"Elements\":[{\"Type\":4,\"Height\":40,\"BarcodeType\":0," +
            "\"Rotation\":90,\"Field\":{\"Value\":\"ABC123\"}}]}]}]},\"data\":{},\"protocolId\":\"demo.protocol\"," +
            "\"protocolConfig\":{\"maxPageHeight\":600},\"pageIndex\":0,\"imageTypes\":[4]}";
        var context = CreateContext(body);
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.PreviewBundle);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(context)), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("images/0001.png"));
        using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open());
        using var manifest = JsonDocument.Parse(reader.ReadToEnd());
        Assert.Equal(1, manifest.RootElement.GetProperty("images").GetArrayLength());
        var element = manifest.RootElement.GetProperty("layout").GetProperty("pages")[0].GetProperty("elements")[0];
        Assert.Equal(40, element.GetProperty("width").GetInt32());
        Assert.Equal(202, element.GetProperty("height").GetInt32());
        Assert.Equal(90, element.GetProperty("rotation").GetInt32());
    }
    [Fact]
    public async Task Blank_protocol_id_is_rejected_before_capabilities_dispatch()
    {
        var error = await ExecuteError("{\"protocolId\":\"   \"}", DesignerRequestProcessor.Capabilities);
        Assert.Equal(StatusCodes.Status400BadRequest, error.Status);
        Assert.Contains("InvalidProtocolSelection", error.Body);
    }
    [Fact]
    public async Task Custom_requests_reject_unknown_invalid_and_conflicting_selection()
    {
        var unknown = await ExecuteError("{\"template\":" + Template +
            ",\"data\":{},\"protocolId\":\"missing.protocol\"}");
        Assert.Equal(StatusCodes.Status400BadRequest, unknown.Status);
        Assert.Contains("InvalidProtocolConfiguration", unknown.Body);

        var invalid = await ExecuteError("{\"template\":" + Template + ",\"data\":{},\"protocolId\":\"demo.protocol\"," +
            "\"protocolConfig\":{\"characterWidth\":0,\"lineHeight\":30,\"maxPageHeight\":600}}");
        Assert.Equal(StatusCodes.Status400BadRequest, invalid.Status);
        Assert.Contains("InvalidProtocolConfiguration", invalid.Body);

        var conflict = await ExecuteError("{\"template\":" + Template +
            ",\"data\":{},\"protocol\":\"Cpcl\",\"protocolId\":\"demo.protocol\"," + Config + "}");
        Assert.Equal(StatusCodes.Status400BadRequest, conflict.Status);
        Assert.Contains("ProtocolSelectionConflict", conflict.Body);
        var nullId = await ExecuteError("{\"template\":" + Template + ",\"data\":{},\"protocolId\":null}",
            DesignerRequestProcessor.Capabilities);
        Assert.Equal(StatusCodes.Status400BadRequest, nullId.Status);
        Assert.Contains("InvalidProtocolSelection", nullId.Body);
        var nullConfig = await ExecuteError("{\"template\":" + Template +
            ",\"data\":{},\"protocolId\":\"demo.protocol\",\"protocolConfig\":null}");
        Assert.Equal(StatusCodes.Status400BadRequest, nullConfig.Status);
        Assert.Contains("InvalidProtocolConfiguration", nullConfig.Body);
        var nullFinishing = await ExecuteError("{\"template\":" + Template +
            ",\"data\":{},\"protocolId\":\"demo.protocol\"," + Config + ",\"finishing\":null}");
        Assert.Equal(StatusCodes.Status400BadRequest, nullFinishing.Status);
        Assert.Contains("InvalidProtocolConfiguration", nullFinishing.Body);
        var finishingConflict = await ExecuteError("{\"template\":" + Template +
            ",\"data\":{},\"protocolId\":\"demo.protocol\"," + Config +
            ",\"feedLines\":1,\"finishing\":{\"feedAmount\":1,\"cut\":true}}");
        Assert.Equal(StatusCodes.Status400BadRequest, finishingConflict.Status);
        Assert.Contains("ProtocolSelectionConflict", finishingConflict.Body);

        var builtInFinishing = await ExecuteError("{\"template\":" + Template + ",\"data\":{},\"protocol\":\"Cpcl\"," +
            "\"finishing\":{\"feedAmount\":1,\"cut\":true}}");
        Assert.Equal(StatusCodes.Status400BadRequest, builtInFinishing.Status);
        Assert.Contains("ProtocolSelectionConflict", builtInFinishing.Body);
    }

    [Fact]
    public async Task Custom_page_png_and_element_png_report_missing_software_bitmap_capability()
    {
        var element = await ExecuteError(Request(",\"pageIndex\":0,\"elementIndex\":0"), DesignerRequestProcessor.ElementPng);
        Assert.Equal(StatusCodes.Status400BadRequest, element.Status);
        using var elementJson = JsonDocument.Parse(element.Body);
        var elementError = elementJson.RootElement.GetProperty("errors")[0];
        Assert.Equal("UnsupportedCapability", elementError.GetProperty("code").GetString());
        Assert.Contains("没有准确", elementError.GetProperty("message").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", elementError.GetProperty("fieldPath").GetString());

        var page = await ExecuteError(Request(",\"pageIndex\":0"), DesignerRequestProcessor.PagePng);
        Assert.Equal(StatusCodes.Status400BadRequest, page.Status);
        using var pageJson = JsonDocument.Parse(page.Body);
        Assert.Contains("新协议未声明完整页面 PNG",
            pageJson.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    private static async Task<(int Status, string Body)> ExecuteError(string json,
        Func<DesignerRequest, CancellationToken, IResult>? handler = null)
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(json);
        await DesignerRequestExecution.ExecuteAsync(context,
            handler ?? DesignerRequestProcessor.Preview);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private sealed class IndependentProtocolAdapter : ITemplateProtocolAdapter
    {
        public string ProtocolId => "independent.test";

        public TemplateCustomProtocolCapabilities Capabilities { get; } = new(
            300,
            new[] { 1 },
            textSizes: new[] { 12, 24 },
            textRotations: new[] { RotationAngle.None },
            supportsHostFontFamily: true,
            imageRotations: new[] { RotationAngle.None },
            supportsFinishing: true,
            supportsBarcodes: true,
            barcodeTypes: new[] { BarcodeType.Code128 },
            barcodeRotations: new[] { RotationAngle.None },
            supportsQrCode: true,
            supportsGs1QrCode: true,
            qrCodeRotations: new[] { RotationAngle.None },
            supportsCoordinates: false,
            supportsLegacyDeviceFont: true,
            fontKinds: new[] { 1, 2 },
            maxTextSize: 96);

        public ITemplateTextMetrics CreateTextMetrics(TemplateTextMeasurementRequest request)
            => throw new NotSupportedException();

        public TemplateBarcodeMeasurement MeasureBarcode(TemplateBarcodeMeasurementRequest request)
            => throw new NotSupportedException();

        public PrintElement ConvertBarcode(TemplateBarcodeConversionRequest request)
            => throw new NotSupportedException();

        public TemplateQrCodeMeasurement MeasureQrCode(TemplateQrCodeMeasurementRequest request)
            => throw new NotSupportedException();

        public PrintElement ConvertQrCode(TemplateQrCodeConversionRequest request)
            => throw new NotSupportedException();
    }
    private static DefaultHttpContext CreateContext(string json)
    {
        var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = "application/json; charset=utf-8";
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
