using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Linq;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

public sealed class HostContractTests
{
    private const string ContractVersionHeader = "X-EasyPrint-Contract-Version";
    private const string RequiredFeaturesHeader = "X-EasyPrint-Required-Features";
    private const string FeaturesHeader = "X-EasyPrint-Features";
    private const string TemplateJson =
        "{\"Version\":1,\"Width\":80,\"Height\":40,\"Rows\":[]}";
    private const string ValidRequest =
        "{\"template\":" + TemplateJson + ",\"data\":{}}";
    private const string BindingFailureRequest =
        "{\"template\":{\"Version\":1,\"Width\":576,\"Height\":100," +
        "\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":1,\"FontSize\":24," +
        "\"Field\":{\"Name\":\"Missing\"}}]}]}]},\"data\":{}}";
    private const string UnknownImageBase64 = "bm90LWFuLWltYWdl";
    private const string TruncatedPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8A";
    private const string ValidPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    private static readonly string[] SupportedFeatures =
    {
        "structured-errors",
        "strict-options",
        "binding-diagnostics",
        "multi-protocol",
        "ordered-output-segments"
    };

    [Theory]
    [InlineData("Cpcl", "Native", true)]
    [InlineData("Tspl", "Native", true)]
    [InlineData("Zpl", "Native", true)]
    [InlineData("EscPos", "Raster", true)]
    [InlineData("EscPos", "Native", false)]
    public async Task Image_rotation_capabilities_and_preview_follow_the_output_mode(string protocol, string layout, bool supports)
    {
        var image = "{\"Version\":1,\"Width\":24,\"Height\":12,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":2,\"Width\":3,\"Height\":2,\"Rotation\":90,\"Field\":{\"Value\":\"logo\"}}]}]}]}";
        var request = "{\"template\":" + image + ",\"data\":{},\"protocol\":\"" + protocol + "\",\"escPosLayoutMode\":\"" + layout +
            "\",\"encoding\":\"ASCII\",\"resources\":{\"logo\":\"" + ValidPngBase64 + "\"},\"feedLines\":0,\"cutAfterPrint\":false}";
        var capabilities = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(capabilities, DesignerRequestProcessor.Capabilities);
        Assert.Equal(StatusCodes.Status200OK, capabilities.Response.StatusCode);
        capabilities.Response.Body.Position = 0;
        using var caps = JsonDocument.Parse(capabilities.Response.Body);
        Assert.Equal(supports ? 4 : 1, caps.RootElement.GetProperty("imageRotations").GetArrayLength());
        var qrSupports = protocol != "EscPos" || layout == "Raster";
        Assert.Equal(qrSupports ? 4 : 1, caps.RootElement.GetProperty("qrCodeRotations").GetArrayLength());
        var preview = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(supports ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, preview.Response.StatusCode);
        preview.Response.Body.Position = 0;
        using var result = JsonDocument.Parse(preview.Response.Body);
        if (supports)
        {
            var element = result.RootElement.GetProperty("pages")[0].GetProperty("elements")[0];
            Assert.Equal(90, element.GetProperty("rotation").GetInt32());
            Assert.Equal(2, element.GetProperty("width").GetInt32());
            Assert.Equal(3, element.GetProperty("height").GetInt32());
        }
        else Assert.Contains("Native", result.RootElement.ToString());
    }

    [Theory]
    [InlineData("Cpcl", "Native", true)]
    [InlineData("Tspl", "Native", true)]
    [InlineData("Zpl", "Native", true)]
    [InlineData("EscPos", "Raster", true)]
    [InlineData("EscPos", "Native", false)]
    public async Task Filled_rectangle_capability_matches_preview_support(string protocol, string layout, bool supports)
    {
        var template = "{\"Version\":2,\"Width\":10,\"Height\":7,\"Rows\":[{\"Cells\":[{\"Elements\":[" +
            "{\"Type\":7,\"Width\":8,\"Height\":3,\"Filled\":true,\"StartCoordinate\":{\"X\":2,\"Y\":4}}]}]}]}";
        var request = "{\"template\":" + template + ",\"data\":{},\"protocol\":\"" + protocol +
            "\",\"escPosLayoutMode\":\"" + layout + "\",\"encoding\":\"ASCII\",\"feedLines\":0,\"cutAfterPrint\":false}";
        var capabilities = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(capabilities, DesignerRequestProcessor.Capabilities);
        Assert.Equal(StatusCodes.Status200OK, capabilities.Response.StatusCode);
        capabilities.Response.Body.Position = 0;
        using var caps = JsonDocument.Parse(capabilities.Response.Body);
        Assert.Equal(supports, caps.RootElement.GetProperty("supportsFilledRectangles").GetBoolean());
        var preview = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(supports ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, preview.Response.StatusCode);
        preview.Response.Body.Position = 0;
        using var result = JsonDocument.Parse(preview.Response.Body);
        if (supports)
        {
            var element = result.RootElement.GetProperty("pages")[0].GetProperty("elements")[0];
            Assert.Equal(8, element.GetProperty("width").GetInt32());
            Assert.Equal(3, element.GetProperty("height").GetInt32());
        }
        else Assert.Contains("Rows[0].Cells[0].Elements[0]", result.RootElement.ToString());
    }

    [Fact]
    public async Task Zpl_preview_uses_text_element_calibration_from_request()
    {
        var template = "{\"Version\":1,\"Width\":80,\"Height\":40,\"Rows\":[{\"Cells\":[{\"Width\":80,\"Elements\":[" +
            "{\"Type\":1,\"FontSize\":24,\"DeviceFontName\":\"A\",\"Field\":{\"Value\":\"AA\"}}]}]}]}";
        var request = "{\"template\":" + template + ",\"data\":{},\"protocol\":\"zpl\",\"encoding\":\"ascii\"," +
            "\"requireDeclaredNativeBounds\":true,\"deviceFontCalibrations\":{\"A\":{" +
            "\"halfCellWidthAt24\":10,\"fullCellWidthAt24\":20,\"lineHeightAt24\":24," +
            "\"textElements\":{\"A\":{\"advanceAt24\":7}}}}}";
        var context = CreateContext(request);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = JsonDocument.Parse(context.Response.Body);
        var elements = response.RootElement.GetProperty("pages")[0]
            .GetProperty("elements").EnumerateArray()
            .Where(element => element.GetProperty("type").GetInt32() == 1);
        var element = Assert.Single(elements);
        Assert.Equal(14, element.GetProperty("width").GetInt32());
        Assert.Equal(24, element.GetProperty("height").GetInt32());
    }

    [Fact]
    public async Task Escpos_raster_hri_font_size_is_forwarded_to_capabilities_and_validated()
    {
        var request = "{\"template\":" + TemplateJson +
            ",\"data\":{},\"protocol\":\"EscPos\",\"escPosLayoutMode\":\"Raster\",\"encoding\":\"ASCII\"," +
            "\"escPosDevice\":{\"rasterBarcodeHumanReadableFontSize\":16}}";
        var context = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using (var capabilities = JsonDocument.Parse(context.Response.Body))
            Assert.Equal(16, capabilities.RootElement.GetProperty("rasterBarcodeHumanReadableFontSize").GetInt32());

        var invalid = CreateContext(request.Replace("16", "256", StringComparison.Ordinal));
        await DesignerRequestExecution.ExecuteAsync(invalid, DesignerRequestProcessor.Capabilities);
        AssertError(invalid, StatusCodes.Status400BadRequest, "InvalidOptions", "Options",
            "RasterBarcodeHumanReadableFontSize");
    }

    [Theory]
    [InlineData("Tspl", "tsplMedia")]
    [InlineData("Zpl", "zplMedia")]
    public async Task Label_profile_hri_capability_is_forwarded_to_host_capabilities(string protocol, string mediaKey)
    {
        var request = "{\"template\":" + TemplateJson + ",\"data\":{},\"protocol\":\"" + protocol +
            "\",\"encoding\":\"ASCII\",\"" + mediaKey + "\":{\"profile\":{\"name\":\"No-Hri\",\"supportsNativeBarcodeHumanReadable\":false}}}";
        var context = CreateContext(request);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = JsonDocument.Parse(context.Response.Body);
        Assert.False(response.RootElement.GetProperty("supportsBarcodeHumanReadable").GetBoolean());
        Assert.False(response.RootElement.GetProperty("labelProfile").GetProperty("supportsNativeBarcodeHumanReadable").GetBoolean());
    }

    [Theory]
    [InlineData("Tspl", "tsplMedia")]
    [InlineData("Zpl", "zplMedia")]
    public async Task Label_profile_hri_type_allowlist_is_forwarded_to_host_capabilities(string protocol, string mediaKey)
    {
        var request = "{\"template\":" + TemplateJson + ",\"data\":{},\"protocol\":\"" + protocol +
            "\",\"encoding\":\"ASCII\",\"" + mediaKey +
            "\":{\"profile\":{\"name\":\"Code128-Hri\",\"supportedNativeBarcodeHumanReadableTypes\":[\"Code128\"]}}}";
        var context = CreateContext(request);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = JsonDocument.Parse(context.Response.Body);
        var types = response.RootElement.GetProperty("labelProfile")
            .GetProperty("supportedNativeBarcodeHumanReadableTypes");
        Assert.Equal(1, types.GetArrayLength());
        Assert.Equal("Code128", types[0].GetString());
    }

    [Theory]
    [InlineData("Tspl", "tsplMedia")]
    [InlineData("Zpl", "zplMedia")]
    public async Task Label_profile_qr_rotation_allowlist_is_forwarded_to_host_capabilities(string protocol, string mediaKey)
    {
        var request = "{\"template\":" + TemplateJson + ",\"data\":{},\"protocol\":\"" + protocol +
            "\",\"encoding\":\"ASCII\",\"" + mediaKey +
            "\":{\"profile\":{\"name\":\"Qr-Rotation\",\"supportedNativeQrCodeRotations\":[\"Rotate90\",\"Rotate270\"]}}}";
        var context = CreateContext(request);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = JsonDocument.Parse(context.Response.Body);
        var rotations = response.RootElement.GetProperty("labelProfile")
            .GetProperty("supportedNativeQrCodeRotations");
        Assert.Equal(2, rotations.GetArrayLength());
        Assert.Equal("Rotate90", rotations[0].GetString());
        Assert.Equal("Rotate270", rotations[1].GetString());
    }

    [Fact]
    public async Task Escpos_native_profile_hri_type_allowlist_is_forwarded_to_capabilities()
    {
        var request = "{\"template\":" + TemplateJson +
            ",\"data\":{},\"protocol\":\"EscPos\",\"escPosLayoutMode\":\"Native\",\"encoding\":\"ASCII\",\"escPosDevice\":{\"profile\":{\"name\":\"Code128-Hri\",\"supportedNativeBarcodeHumanReadableTypes\":[\"Code128\"]}}}";
        var context = CreateContext(request);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = JsonDocument.Parse(context.Response.Body);
        var types = response.RootElement.GetProperty("supportedNativeBarcodeHumanReadableTypes");
        Assert.Equal(1, types.GetArrayLength());
        Assert.Equal("Code128", types[0].GetString());
    }

    [Fact]
    public async Task Escpos_native_profile_hri_position_allowlist_is_forwarded_to_capabilities()
    {
        var request = "{\"template\":" + TemplateJson +
            ",\"data\":{},\"protocol\":\"EscPos\",\"escPosLayoutMode\":\"Native\",\"encoding\":\"ASCII\",\"escPosDevice\":{\"profile\":{\"name\":\"Below-Hri\",\"supportedNativeBarcodeHumanReadablePositions\":[\"Below\"]}}}";
        var context = CreateContext(request);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = JsonDocument.Parse(context.Response.Body);
        var positions = response.RootElement.GetProperty("supportedNativeBarcodeHumanReadablePositions");
        Assert.Equal(1, positions.GetArrayLength());
        Assert.Equal("Below", positions[0].GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("structured-errors, strict-options, binding-diagnostics, multi-protocol, ordered-output-segments")]
    public async Task Legacy_and_feature_negotiated_requests_execute_with_contract_headers(string? requiredFeatures)
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var body = new TrackingBodyStream(Encoding.UTF8.GetBytes(RequestWithArbitraryData()));
        var context = CreateContext(body, requiredFeatures: requiredFeatures);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(body.ReadCount > 0);
        Assert.False(body.IsDisposed);
        AssertContractHeaders(context);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("unknown")]
    public async Task Unknown_contract_version_is_rejected_before_body_read(string version)
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var body = new TrackingBodyStream(Encoding.UTF8.GetBytes(ValidRequest));
        var context = CreateContext(body, version: version);
        var invoked = false;

        await DesignerRequestExecution.ExecuteAsync(context, (request, token) =>
        {
            invoked = true;
            return DesignerRequestProcessor.Capabilities(request, token);
        });

        AssertError(context, StatusCodes.Status409Conflict, "ContractVersionMismatch", "Contract");
        Assert.False(invoked);
        Assert.Equal(0, body.ReadCount);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Unknown_required_feature_is_rejected_before_body_read()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var body = new TrackingBodyStream(Encoding.UTF8.GetBytes(ValidRequest));
        var context = CreateContext(body, requiredFeatures: "structured-errors,unknown-feature");
        var invoked = false;

        await DesignerRequestExecution.ExecuteAsync(context, (request, token) =>
        {
            invoked = true;
            return DesignerRequestProcessor.Capabilities(request, token);
        });

        AssertError(context, StatusCodes.Status409Conflict, "ContractFeatureUnavailable", "Contract");
        Assert.False(invoked);
        Assert.Equal(0, body.ReadCount);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("{\"protcol\":\"cpcl\",\"template\":" + TemplateJson + ",\"data\":{}}", "$.protcol")]
    [InlineData("{\"protocol\":\"escpos\",\"escPosDevice\":{\"nativFlow\":\"line\"},\"template\":" + TemplateJson + ",\"data\":{}}", "$.escPosDevice.nativFlow")]
    [InlineData("{\"deviceFontCalibrations\":{\"default\":{\"bad\":1}},\"template\":" + TemplateJson + ",\"data\":{}}", "$.deviceFontCalibrations.default.bad")]
    [InlineData("{\"deviceFontCalibrations\":{\"A\":{\"textElements\":{\"A\":{\"unknown\":1}}}},\"template\":" + TemplateJson + ",\"data\":{}}", "$.deviceFontCalibrations.A.textElements.A.unknown")]
    public async Task Unknown_root_or_nested_options_are_rejected_with_json_path(string json, string fieldPath)
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var body = new TrackingBodyStream(Encoding.UTF8.GetBytes(json));
        var context = CreateContext(body);
        var invoked = false;

        await DesignerRequestExecution.ExecuteAsync(context, (request, token) =>
        {
            invoked = true;
            return DesignerRequestProcessor.Capabilities(request, token);
        });

        AssertError(context, StatusCodes.Status400BadRequest, "RequestJsonInvalid", "Request", fieldPath);
        Assert.False(invoked);
        Assert.True(body.ReadCount > 0);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Malformed_json_reports_request_json_error_and_releases_resources()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var body = new TrackingBodyStream(Encoding.UTF8.GetBytes("{\"template\":"));
        var context = CreateContext(body);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        AssertError(context, StatusCodes.Status400BadRequest, "RequestJsonInvalid", "Request", "$.template");
        Assert.True(body.ReadCount > 0);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Unsupported_protocol_reports_options_field_path()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var context = CreateContext(RequestWithProperties("\"protocol\":\"not-a-protocol\","));

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        AssertError(context, StatusCodes.Status400BadRequest, "UnsupportedProtocol", "Options", "protocol");
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Preview_binding_failure_reports_business_field_and_template_source_paths()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var context = CreateContext(BindingFailureRequest);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        AssertError(context, StatusCodes.Status400BadRequest, "BindingFailed", "Binding", "Missing",
            "Rows[0].Cells[0].Elements[0]");
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("preview", UnknownImageBase64)]
    [InlineData("render", UnknownImageBase64)]
    [InlineData("preview", TruncatedPngBase64)]
    [InlineData("render", TruncatedPngBase64)]
    public async Task Invalid_image_resources_report_the_element_and_recover(
        string operation, string imageBase64)
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var failed = CreateContext(ImageRequest(imageBase64));
        var action = operation == "preview"
            ? (Func<DesignerRequest, CancellationToken, IResult>)DesignerRequestProcessor.Preview
            : DesignerRequestProcessor.Render;

        await DesignerRequestExecution.ExecuteAsync(failed, action);

        AssertError(failed, StatusCodes.Status400BadRequest, "InvalidImageResource", "Resource",
            sourcePath: "Rows[0].Cells[0].Elements[0]", resourceName: "logo");
        AssertNoPixelDiagnostics(failed);
        AssertNoPartialPage(failed);
        Assert.Equal(0, parent.CurrentBytes);

        var recovered = CreateContext(ImageRequest(ValidPngBase64));
        await DesignerRequestExecution.ExecuteAsync(recovered, action);

        Assert.Equal(StatusCodes.Status200OK, recovered.Response.StatusCode);
        if (operation == "preview")
        {
            using var document = JsonDocument.Parse(ReadResponseBytes(recovered));
            Assert.NotEmpty(document.RootElement.GetProperty("pages").EnumerateArray());
        }
        else
        {
            using var archive = new ZipArchive(
                new MemoryStream(ReadResponseBytes(recovered), writable: false), ZipArchiveMode.Read);
            var page = Assert.Single(archive.Entries,
                entry => entry.FullName.EndsWith(".zpl", StringComparison.OrdinalIgnoreCase));
            Assert.True(page.Length > 0);
        }

        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("render")]
    public async Task Out_of_bounds_image_crop_reports_pixel_metadata_and_recovers(
        string operation)
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var failed = CreateContext(OutOfBoundsImageRequest());
        var action = operation == "preview"
            ? (Func<DesignerRequest, CancellationToken, IResult>)DesignerRequestProcessor.Preview
            : DesignerRequestProcessor.Render;

        await DesignerRequestExecution.ExecuteAsync(failed, action);

        AssertError(failed, StatusCodes.Status400BadRequest, "InvalidImageResource", "Resource",
            sourcePath: "Rows[0].Cells[0].Elements[0]", resourceName: "logo",
            sourcePixelWidth: 1, sourcePixelHeight: 1,
            cropX: 1, cropY: 0, cropWidth: 1, cropHeight: 1);
        AssertNoPartialPage(failed);
        Assert.Equal(0, parent.CurrentBytes);

        var recovered = CreateContext(ImageRequest(ValidPngBase64));
        await DesignerRequestExecution.ExecuteAsync(recovered, action);
        Assert.Equal(StatusCodes.Status200OK, recovered.Response.StatusCode);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Missing_image_resource_keeps_invalid_image_classification()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var context = CreateContext(MissingImageRequest());

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        AssertError(context, StatusCodes.Status400BadRequest, "InvalidImageResource", "Resource",
            sourcePath: "Rows[0].Cells[0].Elements[0]", resourceName: "logo");
        AssertNoPixelDiagnostics(context);
        AssertNoPartialPage(context);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Invalid_template_reports_template_phase()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var context = CreateContext(
            "{\"template\":{\"Version\":4,\"Width\":80,\"Height\":40,\"Rows\":[]},\"data\":{}}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        AssertError(context, StatusCodes.Status400BadRequest, "TemplateInvalid", "Template");
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Unexpected_processor_exception_reports_internal_error_and_releases_resources()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var context = CreateContext(ValidRequest);

        await DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
            throw new InvalidOperationException("unexpected host failure"));

        AssertError(context, StatusCodes.Status500InternalServerError, "InternalError");
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Cancellation_remains_transparent_through_contract_execution()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var body = new TrackingBodyStream(Encoding.UTF8.GetBytes(ValidRequest));
        var context = CreateContext(body, cancellationToken: cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities));

        Assert.Equal(0, body.ReadCount);
        Assert.NotEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, parent.CurrentBytes);
    }

    private static string RequestWithArbitraryData()
        => "{\"template\":" + TemplateJson +
            ",\"data\":{\"freeKey\":{\"nested\":true},\"otherValue\":7}" +
            ",\"resources\":{\"arbitrary-key\":\"not-a-template-option\"}}";

    private static string RequestWithProperties(string properties)
        => "{" + properties + "\"template\":" + TemplateJson + ",\"data\":{}}";

    private static string ImageRequest(string imageBase64)
        => "{\"template\":{\"Version\":1,\"Width\":80,\"Height\":40," +
            "\"Rows\":[{\"Cells\":[{\"Width\":80,\"Elements\":[{" +
            "\"Type\":2,\"Width\":1,\"Height\":1," +
            "\"Field\":{\"Value\":\"logo\"}}]}]}]}," +
            "\"data\":{},\"protocol\":\"zpl\",\"encoding\":\"ascii\"," +
            "\"resources\":{\"logo\":\"" + imageBase64 + "\"}}";

    private static string OutOfBoundsImageRequest()
        => "{\"template\":{\"Version\":1,\"Width\":80,\"Height\":40," +
            "\"Rows\":[{\"Cells\":[{\"Width\":80,\"Elements\":[{" +
            "\"Type\":2,\"Width\":1,\"Height\":1," +
            "\"Crop\":{\"X\":1,\"Y\":0,\"Width\":1,\"Height\":1}," +
            "\"Field\":{\"Value\":\"logo\"}}]}]}]}," +
            "\"data\":{},\"protocol\":\"zpl\",\"encoding\":\"ascii\"," +
            "\"resources\":{\"logo\":\"" + ValidPngBase64 + "\"}}";

    private static string MissingImageRequest()
        => "{\"template\":{\"Version\":1,\"Width\":80,\"Height\":40," +
            "\"Rows\":[{\"Cells\":[{\"Width\":80,\"Elements\":[{" +
            "\"Type\":2,\"Width\":1,\"Height\":1," +
            "\"Field\":{\"Value\":\"logo\"}}]}]}]}," +
            "\"data\":{},\"protocol\":\"zpl\",\"encoding\":\"ascii\"}";

    private static DefaultHttpContext CreateContext(TrackingBodyStream body,
        string? version = null, string? requiredFeatures = null,
        CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = "application/json; charset=utf-8";
        context.RequestAborted = cancellationToken;
        if (version != null)
            context.Request.Headers[ContractVersionHeader] = version;
        if (requiredFeatures != null)
            context.Request.Headers[RequiredFeaturesHeader] = requiredFeatures;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static DefaultHttpContext CreateContext(string json,
        string? version = null, string? requiredFeatures = null,
        CancellationToken cancellationToken = default)
        => CreateContext(new TrackingBodyStream(Encoding.UTF8.GetBytes(json)), version,
            requiredFeatures, cancellationToken);

    private static void AssertContractHeaders(HttpContext context)
    {
        Assert.Equal("1", context.Response.Headers[ContractVersionHeader].ToString());
        var featureHeader = GetFeatureHeader(context);
        var features = featureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var feature in SupportedFeatures)
            Assert.Contains(feature, features);
    }

    private static string GetFeatureHeader(HttpContext context)
    {
        if (context.Response.Headers.TryGetValue(FeaturesHeader, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.ToString();

        throw new XunitException("响应缺少 EasyPrint 契约特性 header。");
    }

    private static void AssertError(HttpContext context, int statusCode, string code,
        string? phase = null, string? fieldPath = null, string? sourcePath = null,
        string? resourceName = null, int? sourcePixelWidth = null,
        int? sourcePixelHeight = null, int? renderedPixelWidth = null,
        int? renderedPixelHeight = null, int? cropX = null, int? cropY = null,
        int? cropWidth = null, int? cropHeight = null)
    {
        Assert.Equal(statusCode, context.Response.StatusCode);
        AssertContractHeaders(context);
        context.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(context.Response.Body);
        var error = document.RootElement.GetProperty("errors")[0];
        Assert.Equal(code, error.GetProperty("code").GetString());
        if (phase != null)
            Assert.Equal(phase, error.GetProperty("phase").GetString());
        if (fieldPath != null)
            Assert.Equal(fieldPath, error.GetProperty("fieldPath").GetString());
        if (sourcePath != null)
            Assert.Equal(sourcePath, error.GetProperty("sourcePath").GetString());
        if (resourceName != null)
            Assert.Equal(resourceName, error.GetProperty("resourceName").GetString());
        if (sourcePixelWidth.HasValue)
            Assert.Equal(sourcePixelWidth.Value, error.GetProperty("sourcePixelWidth").GetInt32());
        if (sourcePixelHeight.HasValue)
            Assert.Equal(sourcePixelHeight.Value, error.GetProperty("sourcePixelHeight").GetInt32());
        if (renderedPixelWidth.HasValue)
            Assert.Equal(renderedPixelWidth.Value, error.GetProperty("renderedPixelWidth").GetInt32());
        if (renderedPixelHeight.HasValue)
            Assert.Equal(renderedPixelHeight.Value, error.GetProperty("renderedPixelHeight").GetInt32());
        if (cropX.HasValue)
            Assert.Equal(cropX.Value, error.GetProperty("cropX").GetInt32());
        if (cropY.HasValue)
            Assert.Equal(cropY.Value, error.GetProperty("cropY").GetInt32());
        if (cropWidth.HasValue)
            Assert.Equal(cropWidth.Value, error.GetProperty("cropWidth").GetInt32());
        if (cropHeight.HasValue)
            Assert.Equal(cropHeight.Value, error.GetProperty("cropHeight").GetInt32());
    }

    private static void AssertNoPixelDiagnostics(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(context.Response.Body);
        var error = document.RootElement.GetProperty("errors")[0];
        foreach (var name in new[]
        {
            "sourcePixelWidth", "sourcePixelHeight", "renderedPixelWidth", "renderedPixelHeight",
            "cropX", "cropY", "cropWidth", "cropHeight"
        })
        {
            if (error.TryGetProperty(name, out var value))
                Assert.Equal(JsonValueKind.Null, value.ValueKind);
        }
    }

    private static void AssertNoPartialPage(HttpContext context)
    {
        using var document = JsonDocument.Parse(ReadResponseBytes(context));
        Assert.False(document.RootElement.TryGetProperty("pages", out _));
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
    }

    private static byte[] ReadResponseBytes(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return ((MemoryStream)context.Response.Body).ToArray();
    }

    private sealed class TrackingBodyStream : MemoryStream
    {
        internal TrackingBodyStream(byte[] bytes) : base(bytes, writable: false) { }

        internal int ReadCount { get; private set; }
        internal bool IsDisposed { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;
            return base.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            ReadCount++;
            return base.Read(buffer);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
