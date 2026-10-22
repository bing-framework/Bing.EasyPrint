using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class Gs1CompositeHostTests
{
    private const string ContractVersionHeader = "X-EasyPrint-Contract-Version";
    private const string FeaturesHeader = "X-EasyPrint-Features";
    private const string RequiredFeaturesHeader = "X-EasyPrint-Required-Features";
    private const string ValidData =
        "{\"Primary\":\"1234567890123\",\"Secondary\":\"(91)123456\"}";
    private const string CompositeTemplate =
        "{\"Version\":3,\"Width\":500,\"Height\":500,\"Rows\":[{\"Cells\":[{\"Width\":500,\"Elements\":[" +
        "{\"Type\":11,\"LinearType\":25,\"Mode\":1,\"ModuleWidth\":2,\"LinearBarHeight\":20," +
        "\"Rotation\":90,\"StartCoordinate\":{\"X\":7,\"Y\":9}," +
        "\"Field\":{\"Name\":\"Primary\"},\"SecondaryField\":{\"Name\":\"Secondary\"}}]}]}]}";

    [Theory]
    [InlineData("Cpcl", "")]
    [InlineData("Tspl", "")]
    [InlineData("Zpl", "")]
    [InlineData("EscPos", "Raster")]
    public async Task Composite_capabilities_are_negotiated_for_all_raster_protocols(
        string protocol, string escPosLayout)
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest(protocol, escPosLayout), "gs1-composite");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        AssertContractFeature(context);
        using var response = ReadJson(context);
        var root = response.RootElement;
        Assert.True(root.GetProperty("supportsGs1Composite").GetBoolean());
        Assert.Contains(11, root.GetProperty("elementTypes").EnumerateArray()
            .Select(value => value.GetInt32()));

        var composite = root.GetProperty("gs1Composite");
        Assert.Equal(11, composite.GetProperty("elementType").GetInt32());
        Assert.Equal(new[] { "Gs1DataBarOmnidirectional", "Gs1DataBarLimited", "UccEan128", "Gs1DataBarExpanded" },
            composite.GetProperty("linearTypes").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { "Auto", "CcA", "CcB", "CcC" },
            composite.GetProperty("modes").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { 0, 90, 180, 270 },
            composite.GetProperty("rotations").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal("GTIN-13-data", composite.GetProperty("primaryFormats")[0].GetString());
        Assert.Equal("bracketed-gs1-ai", composite.GetProperty("secondaryFormat").GetString());
        Assert.False(composite.GetProperty("includesHumanReadable").GetBoolean());
        Assert.True(composite.GetProperty("dimensionsFromEncoding").GetBoolean());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("Cpcl", "")]
    [InlineData("Tspl", "")]
    [InlineData("Zpl", "")]
    [InlineData("EscPos", "Raster")]
    public async Task Expanded_negotiates_previews_and_exports_across_protocols(string protocol, string layout)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", "\"LinearType\":28,\"Mode\":2");
        var data = ValidData.Replace("1234567890123", "(01)09506000134352(10)LOT-A");
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var preview = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite,gs1-composite-expanded");
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, preview.Response.StatusCode);
        Assert.Contains("gs1-composite-expanded", preview.Response.Headers[FeaturesHeader].ToString());
        using var json = ReadJson(preview);
        var element = Assert.Single(json.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
            .Where(value => value.GetProperty("type").GetInt32() == 11));
        Assert.True(element.GetProperty("width").GetInt32() > 0);
        Assert.True(element.GetProperty("height").GetInt32() > 0);
        var render = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite,gs1-composite-expanded");
        await DesignerRequestExecution.ExecuteAsync(render, DesignerRequestProcessor.Render);
        Assert.Equal(StatusCodes.Status200OK, render.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(render)), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("page-001.") && entry.Length > 0);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("Cpcl", "")]
    [InlineData("Tspl", "")]
    [InlineData("Zpl", "")]
    [InlineData("EscPos", "Raster")]
    public async Task Cc_c_negotiates_previews_and_exports_across_protocols(string protocol, string layout)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", "\"LinearType\":11,\"Mode\":3");
        var data = ValidData.Replace("1234567890123", "(01)12345678901231");
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var preview = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite,gs1-composite-cc-c");
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, preview.Response.StatusCode);
        Assert.Contains("gs1-composite-cc-c", preview.Response.Headers[FeaturesHeader].ToString());
        using var json = ReadJson(preview);
        var element = Assert.Single(json.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
            .Where(value => value.GetProperty("type").GetInt32() == 11));
        Assert.True(element.GetProperty("width").GetInt32() > 0);
        Assert.True(element.GetProperty("height").GetInt32() > 0);
        var render = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite,gs1-composite-cc-c");
        await DesignerRequestExecution.ExecuteAsync(render, DesignerRequestProcessor.Render);
        Assert.Equal(StatusCodes.Status200OK, render.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(render)), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("page-001.") && entry.Length > 0);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData(11, 1)]
    [InlineData(11, 2)]
    [InlineData(25, 3)]
    [InlineData(28, 3)]
    public async Task Invalid_cc_c_pair_returns_structured_error(int linearType, int mode)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1",
            $"\"LinearType\":{linearType},\"Mode\":{mode}");
        var context = CreateContext(BuildRequest("Cpcl", "", template));
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        Assert.Contains("Composite", json.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("(01)12345678901230")]
    [InlineData("0112345678901231")]
    public async Task Expanded_primary_errors_keep_bound_field_path(string primary)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", "\"LinearType\":28,\"Mode\":0");
        var data = JsonSerializer.Serialize(new { Primary = primary, Secondary = "(91)123" });
        var context = CreateContext(BuildRequest("Cpcl", "", template, data));
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        var error = response.RootElement.GetProperty("errors")[0];
        Assert.Equal("Primary", error.GetProperty("fieldPath").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
    }

    [Fact]
    public async Task Gs1_auto_capacity_error_keeps_cc_c_diagnostic()
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", "\"LinearType\":11,\"Mode\":0");
        var secondary = string.Concat(Enumerable.Range(91, 9).Select(ai => $"({ai})" + new string('A', 90)));
        var data = JsonSerializer.Serialize(new { Primary = "(01)12345678901231", Secondary = secondary + secondary });
        var context = CreateContext(BuildRequest("Cpcl", "", template, data));
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        var error = json.RootElement.GetProperty("errors")[0];
        Assert.Contains("CcC 容量", error.GetProperty("message").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
    }

    [Theory]
    [InlineData("EscPos", "Native", "")]
    [InlineData("Tspl", "", "tsplMedia")]
    [InlineData("Zpl", "", "zplMedia")]
    public async Task Native_or_profile_without_images_rejects_composite_before_binding(
        string protocol, string escPosLayout, string mediaProperty)
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var media = string.IsNullOrEmpty(mediaProperty)
            ? null
            : "\"" + mediaProperty + "\":{\"profile\":{\"name\":\"NoImages\",\"supportsImages\":false}}";
        var request = BuildRequest(protocol, escPosLayout, mediaJson: media);

        var capabilities = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(capabilities, DesignerRequestProcessor.Capabilities);
        Assert.Equal(StatusCodes.Status200OK, capabilities.Response.StatusCode);
        using (var response = ReadJson(capabilities))
        {
            var root = response.RootElement;
            Assert.False(root.GetProperty("supportsGs1Composite").GetBoolean());
            Assert.DoesNotContain(11, root.GetProperty("elementTypes").EnumerateArray()
                .Select(value => value.GetInt32()));
            Assert.Equal(JsonValueKind.Null, root.GetProperty("gs1Composite").ValueKind);
        }

        var preview = CreateContext(request);
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, preview.Response.StatusCode);
        AssertContractFeature(preview);
        using var errorResponse = ReadJson(preview);
        var error = errorResponse.RootElement.GetProperty("errors")[0];
        Assert.Equal("UnsupportedCapability", error.GetProperty("code").GetString());
        Assert.Equal("Render", error.GetProperty("phase").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
        Assert.Contains("不支持", error.GetProperty("message").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Preview_reports_the_encoded_composite_size_after_rotation()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest("Cpcl", ""));

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var response = ReadJson(context);
        var element = Assert.Single(response.RootElement.GetProperty("pages")[0]
            .GetProperty("elements").EnumerateArray()
            .Where(value => value.GetProperty("type").GetInt32() == 11));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", element.GetProperty("sourcePath").GetString());
        Assert.Equal(7, element.GetProperty("x").GetInt32());
        Assert.Equal(9, element.GetProperty("y").GetInt32());
        Assert.Equal(34, element.GetProperty("width").GetInt32());
        Assert.Equal(200, element.GetProperty("height").GetInt32());
        Assert.Equal(90, element.GetProperty("rotation").GetInt32());
        Assert.Equal("1234567890123", element.GetProperty("text").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Preview_binding_failure_preserves_composite_component_ownership()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest("Cpcl", "", RepeatedCompositeTemplate(),
            "{\"Items\":[{\"Primary\":\"1234567890123\",\"Secondary\":\"(91)123456\"}," +
            "{\"Primary\":\"not-a-gtin\",\"Secondary\":\"(91)123456\"}]}"));

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        AssertContractFeature(context);
        using var response = ReadJson(context);
        var error = response.RootElement.GetProperty("errors")[0];
        Assert.Equal("BindingFailed", error.GetProperty("code").GetString());
        Assert.Equal("Primary", error.GetProperty("fieldPath").GetString());
        Assert.Equal("Components[0].Elements[0]", error.GetProperty("definitionPath").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("componentInstancePath").GetString());
        Assert.Equal(new[] { 1 }, error.GetProperty("instanceIndices").EnumerateArray()
            .Select(value => value.GetInt32()));
        Assert.Equal("Rows[0].Cells[0].Elements[0].Elements[0]",
            error.GetProperty("sourcePath").GetString());
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("Cpcl", "", "page-001.cpcl")]
    [InlineData("Tspl", "", "page-001.tspl")]
    [InlineData("Zpl", "", "page-001.zpl")]
    [InlineData("EscPos", "Raster", "page-001.escpos")]
    public async Task Render_returns_nonempty_composite_bytes_for_all_protocols(
        string protocol, string escPosLayout, string expectedFile)
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest(protocol, escPosLayout));

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(context), writable: false),
            ZipArchiveMode.Read);
        var page = Assert.Single(archive.Entries, entry => entry.FullName == expectedFile);
        Assert.True(page.Length > 0);
        Assert.Equal(0, parent.CurrentBytes);
    }

    private static string RepeatedCompositeTemplate()
        => "{\"Version\":3,\"Width\":500,\"Height\":500,\"Components\":[" +
        "{\"Id\":\"composite\",\"Width\":500,\"Height\":80,\"Elements\":[" +
        "{\"Type\":11,\"LinearType\":25,\"Mode\":1,\"ModuleWidth\":2,\"LinearBarHeight\":20," +
        "\"Field\":{\"Name\":\"Primary\"},\"SecondaryField\":{\"Name\":\"Secondary\"}}]}]," +
        "\"Rows\":[{\"RepeatSource\":\"Items\",\"Cells\":[{\"Elements\":[" +
        "{\"Type\":6,\"ComponentId\":\"composite\"}]}]}]}";

    private static string BuildRequest(string protocol, string escPosLayout,
        string template = CompositeTemplate, string data = ValidData, string? mediaJson = null)
    {
        var options = "\"protocol\":\"" + protocol + "\",\"encoding\":\"ASCII\"," +
            "\"feedLines\":0,\"cutAfterPrint\":false";
        if (!string.IsNullOrEmpty(escPosLayout))
            options += ",\"escPosLayoutMode\":\"" + escPosLayout + "\"";
        if (!string.IsNullOrEmpty(mediaJson)) options += "," + mediaJson;
        return "{\"template\":" + template + ",\"data\":" + data + "," + options + "}";
    }

    private static DefaultHttpContext CreateContext(string json, string? requiredFeatures = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();
        var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = "application/json; charset=utf-8";
        if (requiredFeatures != null)
            context.Request.Headers[RequiredFeaturesHeader] = requiredFeatures;
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

    private static void AssertContractFeature(DefaultHttpContext context)
    {
        Assert.Equal("1", context.Response.Headers[ContractVersionHeader].ToString());
        Assert.Contains("gs1-composite", context.Response.Headers[FeaturesHeader].ToString());
    }
}
