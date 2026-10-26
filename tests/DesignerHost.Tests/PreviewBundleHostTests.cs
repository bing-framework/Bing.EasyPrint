using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

public sealed class PreviewBundleHostTests
{
    private const string RequiredFeaturesHeader = "X-EasyPrint-Required-Features";
    private const string FeaturesHeader = "X-EasyPrint-Features";
    private const string OrdinaryTemplate =
        "{\"Version\":2,\"Width\":500,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
        "{\"Type\":7,\"Width\":40,\"Height\":30}," +
        "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"NarrowModuleWidth\":2,\"HumanReadable\":false," +
        "\"Field\":{\"Value\":\"12345\"}}," +
        "{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"Field\":{\"Value\":\"BUNDLE\"}}" +
        "]}]}]}";
    private const string CpclTemplate =
        "{\"Version\":2,\"Width\":500,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
        "{\"Type\":7,\"Width\":40,\"Height\":30}," +
        "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"HumanReadable\":false," +
        "\"Field\":{\"Value\":\"12345\"}}," +
        "{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"Field\":{\"Value\":\"BUNDLE\"}}" +
        "]}]}]}";
    private const string RepeatedTemplate =
        "{\"Version\":3,\"Width\":400,\"Height\":60,\"Rows\":[{\"Height\":60,\"RepeatSource\":\"Items\",\"Cells\":[{\"Elements\":[" +
        "{\"Type\":4,\"BarcodeType\":0,\"Height\":30,\"NarrowModuleWidth\":2,\"HumanReadable\":false," +
        "\"Field\":{\"Name\":\"Value\"}}" +
        "]}]}]}";

    [Fact]
    public async Task Bundle_renders_once_and_matches_element_png_pixels()
    {
        using var parent = PrintResourceScope.Begin(16 * 1024 * 1024);
        var renderCalls = 0;
        var context = CreateContext(BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}",
            ",\"pageIndex\":0,\"imageTypes\":[3,4]"), "preview-bundle");
        await DesignerRequestExecution.ExecuteAsync(context, (request, cancellationToken) =>
            DesignerRequestProcessor.PreviewBundleCore(request, cancellationToken,
                (template, data, resources, options) =>
                {
                    renderCalls++;
                    return DesignerRequestProcessor.RenderPreviewBundle(template, data, resources, options,
                        cancellationToken);
                }));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("application/zip", context.Response.ContentType);
        Assert.Equal(1, renderCalls);
        Assert.Contains("preview-bundle", context.Response.Headers[FeaturesHeader].ToString());

        var bundle = ReadBundle(context);
        using var manifest = bundle.Manifest;
        Assert.Equal(0, manifest.RootElement.GetProperty("layout").GetProperty("pageIndex").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("layout").GetProperty("pageCount").GetInt32());
        Assert.Equal(0, manifest.RootElement.GetProperty("diagnostics").GetArrayLength());
        var images = manifest.RootElement.GetProperty("images");
        Assert.Equal(2, images.GetArrayLength());
        foreach (var image in images.EnumerateArray())
        {
            var file = image.GetProperty("file").GetString()!;
            Assert.True(bundle.Images.ContainsKey(file));
            var request = BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}",
                ",\"pageIndex\":0,\"elementIndex\":" + image.GetProperty("elementIndex").GetInt32() +
                ",\"sourcePath\":\"" + image.GetProperty("sourcePath").GetString() + "\"");
            var elementContext = CreateContext(request, "raster-element-png");
            await DesignerRequestExecution.ExecuteAsync(elementContext, DesignerRequestProcessor.ElementPng);
            Assert.Equal(StatusCodes.Status200OK, elementContext.Response.StatusCode);
            AssertPixelsEqual(bundle.Images[file], ReadResponseBytes(elementContext));
        }
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Bundle_selects_the_requested_page_and_only_emits_that_pages_images()
    {
        using var parent = PrintResourceScope.Begin(16 * 1024 * 1024);
        const string data = "{\"Items\":[{\"Value\":\"12345\"},{\"Value\":\"67890\"}]}";
        var context = CreateContext(BuildRequest("EscPos", "Raster", RepeatedTemplate, data,
            ",\"pageIndex\":1,\"imageTypes\":[4]"), "preview-bundle");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.PreviewBundle);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var bundle = ReadBundle(context);
        using var manifest = bundle.Manifest;
        var layout = manifest.RootElement.GetProperty("layout");
        Assert.Equal(1, layout.GetProperty("pageIndex").GetInt32());
        Assert.Equal(2, layout.GetProperty("pageCount").GetInt32());
        Assert.Equal(2, layout.GetProperty("pages").GetArrayLength());
        var images = manifest.RootElement.GetProperty("images");
        Assert.Equal(1, images.GetArrayLength());
        foreach (var image in images.EnumerateArray())
        {
            Assert.Equal(1, image.GetProperty("pageIndex").GetInt32());
            Assert.StartsWith("1:", image.GetProperty("key").GetString());
        }
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Bundle_keeps_layout_when_an_ordinary_element_has_no_software_image()
    {
        using var parent = PrintResourceScope.Begin(16 * 1024 * 1024);
        var context = CreateContext(BuildRequest("Cpcl", "", CpclTemplate, "{}",
            ",\"pageIndex\":0,\"imageTypes\":[4,7]"), "preview-bundle");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.PreviewBundle);

        Assert.True(context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(ReadResponseBytes(context)));
        var bundle = ReadBundle(context);
        using var manifest = bundle.Manifest;
        var elements = manifest.RootElement.GetProperty("layout").GetProperty("pages")[0]
            .GetProperty("elements");
        Assert.True(elements.GetArrayLength() >= 3);
        Assert.Contains(Enumerable.Range(0, elements.GetArrayLength()),
            index => elements[index].GetProperty("type").GetInt32() == 4);
        Assert.Contains(Enumerable.Range(0, elements.GetArrayLength()),
            index => elements[index].GetProperty("type").GetInt32() == 7);
        var images = manifest.RootElement.GetProperty("images");
        Assert.Equal(1, images.GetArrayLength());
        Assert.Equal(7, images[0].GetProperty("type").GetInt32());
        var diagnostics = manifest.RootElement.GetProperty("diagnostics");
        Assert.Equal(1, diagnostics.GetArrayLength());
        Assert.Equal(4, diagnostics[0].GetProperty("type").GetInt32());
        Assert.Equal("UnsupportedCapability", diagnostics[0].GetProperty("code").GetString());
        Assert.Equal("PreviewImage", diagnostics[0].GetProperty("phase").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Bundle_cancellation_after_render_releases_resources_without_output()
    {
        using var parent = PrintResourceScope.Begin(16 * 1024 * 1024);
        var context = CreateContext(BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}",
            ",\"pageIndex\":0,\"imageTypes\":[3,4]"));
        using var cancellation = new CancellationTokenSource();
        context.RequestAborted = cancellation.Token;
        var renderCalls = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            DesignerRequestExecution.ExecuteAsync(context, (request, cancellationToken) =>
                DesignerRequestProcessor.PreviewBundleCore(request, cancellationToken,
                    (template, data, resources, options) =>
                    {
                        renderCalls++;
                        var result = DesignerRequestProcessor.RenderPreviewBundle(template, data, resources,
                            options, cancellationToken);
                        cancellation.Cancel();
                        return result;
                    })));
        Assert.Equal(1, renderCalls);
        Assert.Empty(ReadResponseBytes(context));
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Bundle_keeps_layout_when_real_element_encoding_throws()
    {
        using var parent = PrintResourceScope.Begin(16 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":500,\"Height\":200,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":1,\"FontSize\":24,\"Field\":{\"Value\":\"AA\"}}," +
            "{\"Type\":7,\"Width\":40,\"Height\":30}" +
            "]}]}]}";
        var previewContext = CreateContext(BuildRequest("EscPos", "Raster", template, "{}"));
        await DesignerRequestExecution.ExecuteAsync(previewContext, DesignerRequestProcessor.Preview);
        int textIndex;
        int rectangleIndex;
        using (var preview = ReadJson(previewContext))
        {
            var elements = preview.RootElement.GetProperty("pages")[0].GetProperty("elements");
            textIndex = Enumerable.Range(0, elements.GetArrayLength())
                .First(index => elements[index].GetProperty("type").GetInt32() == 1);
            rectangleIndex = Enumerable.Range(0, elements.GetArrayLength())
                .First(index => elements[index].GetProperty("type").GetInt32() == 7);
        }

        var context = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            ",\"pageIndex\":0,\"imageTypes\":[1,7]"), "preview-bundle");
        await DesignerRequestExecution.ExecuteAsync(context, (request, cancellationToken) =>
            DesignerRequestProcessor.PreviewBundleCore(request, cancellationToken, null,
                index => index == textIndex
                    ? new InvalidOperationException("controlled element encoding failure")
                    : null));
        Assert.True(context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(ReadResponseBytes(context)));
        var bundle = ReadBundle(context);
        using var manifest = bundle.Manifest;
        var layoutElements = manifest.RootElement.GetProperty("layout").GetProperty("pages")[0]
            .GetProperty("elements");
        Assert.Contains(Enumerable.Range(0, layoutElements.GetArrayLength()),
            index => layoutElements[index].GetProperty("type").GetInt32() == 1);
        Assert.Contains(Enumerable.Range(0, layoutElements.GetArrayLength()),
            index => layoutElements[index].GetProperty("type").GetInt32() == 7);
        var images = manifest.RootElement.GetProperty("images");
        Assert.Equal(1, images.GetArrayLength());
        Assert.Equal(7, images[0].GetProperty("type").GetInt32());
        var diagnostics = manifest.RootElement.GetProperty("diagnostics");
        Assert.Equal(1, diagnostics.GetArrayLength());
        Assert.Equal(1, diagnostics[0].GetProperty("type").GetInt32());
        Assert.Equal("InternalError", diagnostics[0].GetProperty("code").GetString());
        Assert.Equal("PreviewImage", diagnostics[0].GetProperty("phase").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }
    [Fact]
    public async Task Bundle_cancellation_releases_resources_without_output()
    {
        using var parent = PrintResourceScope.Begin(16 * 1024 * 1024);
        var context = CreateContext(BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}",
            ",\"pageIndex\":0,\"imageTypes\":[3,4]"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.RequestAborted = cancellation.Token;
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.PreviewBundle));
        Assert.Empty(ReadResponseBytes(context));
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Bundle_resource_budget_failure_does_not_return_a_partial_zip()
    {
        using var parent = PrintResourceScope.Begin(16 * 1024 * 1024);
        const string template = "{\"Version\":2,\"Width\":1000,\"Height\":1000,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":7,\"Width\":1000,\"Height\":1000,\"Filled\":true}]}]}]}";
        var context = CreateContext(BuildRequest("EscPos", "Raster", template, "{}",
            ",\"maxWorkingBytes\":1048576,\"pageIndex\":0,\"imageTypes\":[7]"), "preview-bundle");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.PreviewBundle);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        using var response = ReadJson(context);
        Assert.Equal("ResourceLimitExceeded", response.RootElement.GetProperty("errors")[0]
            .GetProperty("code").GetString());
        var bytes = ReadResponseBytes(context);
        Assert.NotEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, bytes.Take(4).ToArray());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Bundle_rejects_invalid_image_types_and_page_index()
    {
        using var parent = PrintResourceScope.Begin(16 * 1024 * 1024);
        var types = CreateContext(BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}",
            ",\"imageTypes\":[4,4]"), "preview-bundle");
        await DesignerRequestExecution.ExecuteAsync(types, DesignerRequestProcessor.PreviewBundle);
        Assert.Equal(StatusCodes.Status400BadRequest, types.Response.StatusCode);
        using (var response = ReadJson(types))
            Assert.Equal("InvalidImageTypes", response.RootElement.GetProperty("errors")[0]
                .GetProperty("code").GetString());

        var page = CreateContext(BuildRequest("EscPos", "Raster", OrdinaryTemplate, "{}",
            ",\"pageIndex\":9,\"imageTypes\":[4]"), "preview-bundle");
        await DesignerRequestExecution.ExecuteAsync(page, DesignerRequestProcessor.PreviewBundle);
        Assert.Equal(StatusCodes.Status400BadRequest, page.Response.StatusCode);
        using (var response = ReadJson(page))
            Assert.Equal("InvalidPageIndex", response.RootElement.GetProperty("errors")[0]
                .GetProperty("code").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Bundle_metadata_budget_failure_rejects_before_partial_zip()
    {
        using var parent = PrintResourceScope.Begin(64 * 1024 * 1024);
        var elements = string.Join(",", Enumerable.Repeat(
            "{\"Type\":1,\"FontSize\":24,\"Field\":{\"Value\":\"\"}}", 1000));
        var template = "{\"Version\":2,\"Width\":1000,\"Height\":1000,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            elements + "]}]}]}";
        const int limit = 3342336;
        var request = BuildRequest("Cpcl", "", template, "{}",
            ",\"pageIndex\":0,\"imageTypes\":[],\"maxWorkingBytes\":" + limit);
        var previewContext = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(previewContext, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, previewContext.Response.StatusCode);

        var bundleContext = CreateContext(request, "preview-bundle");
        await DesignerRequestExecution.ExecuteAsync(bundleContext, DesignerRequestProcessor.PreviewBundle);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, bundleContext.Response.StatusCode);
        using var response = ReadJson(bundleContext);
        Assert.Equal("ResourceLimitExceeded", response.RootElement.GetProperty("errors")[0]
            .GetProperty("code").GetString());
        var resourceName = response.RootElement.GetProperty("errors")[0].GetProperty("resourceName").GetString();
        Assert.True(resourceName is "Host.PreviewBundleMetadata" or "Host.PreviewBundleManifestBuffer",
            resourceName);
        var bytes = ReadResponseBytes(bundleContext);
        Assert.NotEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, bytes.Take(4).ToArray());
        Assert.Equal(0, parent.CurrentBytes);
    }

    private static void AssertPixelsEqual(byte[] bundlePng, byte[] elementPng)
    {
        using var bundleImage = Image.Load<Rgba32>(bundlePng);
        using var elementImage = Image.Load<Rgba32>(elementPng);
        Assert.Equal(elementImage.Width, bundleImage.Width);
        Assert.Equal(elementImage.Height, bundleImage.Height);
        for (var y = 0; y < elementImage.Height; y++)
        for (var x = 0; x < elementImage.Width; x++)
            Assert.Equal(elementImage[x, y], bundleImage[x, y]);
    }

    private static (JsonDocument Manifest, Dictionary<string, byte[]> Images) ReadBundle(
        DefaultHttpContext context)
    {
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(context)), ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("manifest.json") ??
            throw new InvalidOperationException("Bundle manifest.json is missing.");
        using var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8);
        var manifest = JsonDocument.Parse(reader.ReadToEnd());
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith("images/",
                     StringComparison.Ordinal)))
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            images.Add(entry.FullName, buffer.ToArray());
        }
        return (manifest, images);
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
