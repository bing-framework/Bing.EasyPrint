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
        Assert.Contains("gs1-composite-ean-upc", context.Response.Headers[FeaturesHeader].ToString());
        Assert.Contains("gs1-composite-gs1-128-ab", context.Response.Headers[FeaturesHeader].ToString());
        Assert.Contains("gs1-composite-expanded-stacked", context.Response.Headers[FeaturesHeader].ToString());
        using var response = ReadJson(context);
        var root = response.RootElement;
        Assert.True(root.GetProperty("supportsGs1Composite").GetBoolean());
        Assert.Contains(11, root.GetProperty("elementTypes").EnumerateArray()
            .Select(value => value.GetInt32()));
        var ordinaryBarcodeTypes = root.GetProperty("barcodeTypes").EnumerateArray()
            .Select(value => value.GetInt32()).ToArray();
        if (protocol == "EscPos" && escPosLayout == "Raster")
            { Assert.Contains(31, ordinaryBarcodeTypes); Assert.Contains(32, ordinaryBarcodeTypes); }
        else
            { Assert.DoesNotContain(31, ordinaryBarcodeTypes); Assert.DoesNotContain(32, ordinaryBarcodeTypes); }

        var composite = root.GetProperty("gs1Composite");
        Assert.Equal(11, composite.GetProperty("elementType").GetInt32());
        Assert.Equal(new[] { "Gs1DataBarOmnidirectional", "Gs1DataBarLimited", "UccEan128", "Gs1DataBarExpanded", "Gs1DataBarStacked", "Gs1DataBarExpandedStacked", "Ean8", "Ean13", "UpcA", "UpcE", "Gs1DataBarTruncated", "Gs1DataBarStackedOmnidirectional" },
            composite.GetProperty("linearTypes").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { "Auto", "CcA", "CcB", "CcC" },
            composite.GetProperty("modes").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { 0, 90, 180, 270 },
            composite.GetProperty("rotations").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal("GTIN-13-data", composite.GetProperty("primaryFormats")[0].GetString());
        Assert.Contains("ean-upc-digits-with-addon", composite.GetProperty("primaryFormats").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("gs1-composite-ean-upc-addons", context.Response.Headers[FeaturesHeader].ToString());
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
    public async Task Remaining_databar_negotiates_previews_and_exports_across_protocols(string protocol, string layout)
    {
        foreach (var pair in new[] { (26, "12345678901231"), (32, "12345678901231") })
        foreach (var mode in new[] { 0, 1, 2 })
        {
            var feature = pair.Item1 == 26 ? "gs1-composite-truncated" : "gs1-composite-stacked-omnidirectional";
            var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", $"\"LinearType\":{pair.Item1},\"Mode\":{mode}").Replace("\"LinearBarHeight\":20", "\"LinearBarHeight\":68");
            var data = ValidData.Replace("1234567890123", pair.Item2);
            using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
            var preview = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite," + feature);
            await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
            Assert.Equal(StatusCodes.Status200OK, preview.Response.StatusCode);
            Assert.Contains(feature, preview.Response.Headers[FeaturesHeader].ToString());
            using var json = ReadJson(preview);
            var element = Assert.Single(json.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
                .Where(value => value.GetProperty("type").GetInt32() == 11));
            Assert.True(element.GetProperty("width").GetInt32() > 0);
            Assert.True(element.GetProperty("height").GetInt32() > 0);
            var render = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite," + feature);
            await DesignerRequestExecution.ExecuteAsync(render, DesignerRequestProcessor.Render);
            Assert.Equal(StatusCodes.Status200OK, render.Response.StatusCode);
            using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(render)), ZipArchiveMode.Read);
            Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("page-001.") && entry.Length > 0);
            Assert.Equal(0, parent.CurrentBytes);
        }
    }

    [Theory]
    [InlineData("Cpcl", "")]
    [InlineData("Tspl", "")]
    [InlineData("Zpl", "")]
    [InlineData("EscPos", "Raster")]
    public async Task Ean_upc_negotiates_previews_and_exports_across_protocols(string protocol, string layout)
    {
        foreach (var pair in new[] { (4, "12345670"), (5, "1234567890128"), (6, "012345678905"), (7, "01234565") })
        foreach (var suffix in new[] { "", "+12", "+51234" })
        foreach (var mode in new[] { 0, 1, 2 })
        {
            var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", $"\"LinearType\":{pair.Item1},\"Mode\":{mode}").Replace("\"LinearBarHeight\":20", "\"LinearBarHeight\":31");
            var data = ValidData.Replace("1234567890123", pair.Item2 + suffix);
            using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
            var preview = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite,gs1-composite-ean-upc,gs1-composite-ean-upc-addons");
            await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
            Assert.Equal(StatusCodes.Status200OK, preview.Response.StatusCode);
            Assert.Contains("gs1-composite-ean-upc", preview.Response.Headers[FeaturesHeader].ToString());
            using var json = ReadJson(preview);
            var element = Assert.Single(json.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
                .Where(value => value.GetProperty("type").GetInt32() == 11));
            Assert.True(element.GetProperty("width").GetInt32() > 0);
            Assert.True(element.GetProperty("height").GetInt32() > 0);
            var render = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite,gs1-composite-ean-upc,gs1-composite-ean-upc-addons");
            await DesignerRequestExecution.ExecuteAsync(render, DesignerRequestProcessor.Render);
            Assert.Equal(StatusCodes.Status200OK, render.Response.StatusCode);
            using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(render)), ZipArchiveMode.Read);
            Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("page-001.") && entry.Length > 0);
            Assert.Equal(0, parent.CurrentBytes);
        }
    }

    [Theory]
    [InlineData("Cpcl", "")]
    [InlineData("Tspl", "")]
    [InlineData("Zpl", "")]
    [InlineData("EscPos", "Raster")]
    public async Task Stacked_negotiates_previews_and_exports_across_protocols(string protocol, string layout)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", "\"LinearType\":30,\"Mode\":2").Replace("\"LinearBarHeight\":20", "\"LinearBarHeight\":31");
        var data = ValidData.Replace("1234567890123", "(01)12345678901231");
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var preview = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite,gs1-composite-stacked");
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, preview.Response.StatusCode);
        Assert.Contains("gs1-composite-stacked", preview.Response.Headers[FeaturesHeader].ToString());
        using var json = ReadJson(preview);
        var element = Assert.Single(json.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
            .Where(value => value.GetProperty("type").GetInt32() == 11));
        Assert.True(element.GetProperty("width").GetInt32() > 0);
        Assert.True(element.GetProperty("height").GetInt32() > 0);
        var render = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite,gs1-composite-stacked");
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
    public async Task Expanded_stacked_negotiates_previews_and_exports_across_protocols(string protocol, string layout)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", "\"LinearType\":31,\"Mode\":1")
            .Replace("\"LinearBarHeight\":20", "\"LinearBarHeight\":34");
        var data = ValidData.Replace("1234567890123", "(01)09506000134352(10)LOT-A");
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        const string feature = "gs1-composite-expanded-stacked";
        var preview = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite," + feature);
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, preview.Response.StatusCode);
        Assert.Contains(feature, preview.Response.Headers[FeaturesHeader].ToString());
        using var json = ReadJson(preview);
        var element = Assert.Single(json.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
            .Where(value => value.GetProperty("type").GetInt32() == 11));
        Assert.True(element.GetProperty("width").GetInt32() > 0);
        Assert.True(element.GetProperty("height").GetInt32() >= 34);
        var render = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite," + feature);
        await DesignerRequestExecution.ExecuteAsync(render, DesignerRequestProcessor.Render);
        Assert.Equal(StatusCodes.Status200OK, render.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(render)), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("page-001.") && entry.Length > 0);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("Cpcl", "", 1)]
    [InlineData("Cpcl", "", 2)]
    [InlineData("Cpcl", "", 3)]
    [InlineData("Tspl", "", 1)]
    [InlineData("Tspl", "", 2)]
    [InlineData("Tspl", "", 3)]
    [InlineData("Zpl", "", 1)]
    [InlineData("Zpl", "", 2)]
    [InlineData("Zpl", "", 3)]
    [InlineData("EscPos", "Raster", 1)]
    [InlineData("EscPos", "Raster", 2)]
    [InlineData("EscPos", "Raster", 3)]
    public async Task Gs1_128_negotiates_previews_and_exports_across_protocols(string protocol, string layout, int mode)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", $"\"LinearType\":11,\"Mode\":{mode}");
        var feature = mode == 3 ? "gs1-composite-cc-c" : "gs1-composite-gs1-128-ab";
        var data = ValidData.Replace("1234567890123", "(01)12345678901231");
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var preview = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite," + feature);
        await DesignerRequestExecution.ExecuteAsync(preview, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, preview.Response.StatusCode);
        Assert.Contains(feature, preview.Response.Headers[FeaturesHeader].ToString());
        using var json = ReadJson(preview);
        var element = Assert.Single(json.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
            .Where(value => value.GetProperty("type").GetInt32() == 11));
        Assert.True(element.GetProperty("width").GetInt32() > 0);
        Assert.True(element.GetProperty("height").GetInt32() > 0);
        var render = CreateContext(BuildRequest(protocol, layout, template, data), "gs1-composite," + feature);
        await DesignerRequestExecution.ExecuteAsync(render, DesignerRequestProcessor.Render);
        Assert.Equal(StatusCodes.Status200OK, render.Response.StatusCode);
        using var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(render)), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("page-001.") && entry.Length > 0);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData(27, 3)]
    [InlineData(30, 3)]
    [InlineData(25, 3)]
    [InlineData(28, 3)]
    [InlineData(4, 3)]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    [InlineData(7, 3)]
    [InlineData(26, 3)]
    [InlineData(32, 3)]
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
    [InlineData(26, 25)]
    [InlineData(32, 65)]
    public async Task Remaining_databar_invalid_height_returns_source_diagnostic(int type, int height)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25", $"\"LinearType\":{type}")
            .Replace("\"LinearBarHeight\":20", $"\"LinearBarHeight\":{height}");
        var context = CreateContext(BuildRequest("Cpcl", "", template));
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        var error = response.RootElement.GetProperty("errors")[0];
        Assert.Contains(type == 26 ? "13X" : "33X", error.GetProperty("message").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
    }

    [Theory]
    [InlineData(26)]
    [InlineData(32)]
    public async Task Remaining_databar_primary_error_keeps_field_diagnostic(int type)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25", $"\"LinearType\":{type}")
            .Replace("\"LinearBarHeight\":20", "\"LinearBarHeight\":68");
        var context = CreateContext(BuildRequest("Cpcl", "", template,
            JsonSerializer.Serialize(new { Primary = "12345678901230", Secondary = "(91)123" })));
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        var error = response.RootElement.GetProperty("errors")[0];
        Assert.Equal("Primary", error.GetProperty("fieldPath").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
    }

    [Theory]
    [InlineData("12345671")]
    [InlineData("1234567+123")]
    [InlineData("1234567+1")]
    [InlineData("1234567+123456")]
    [InlineData("1234567+１２")]
    [InlineData("1234567+12+12")]
    [InlineData("12345671+12")]
    [InlineData("1234567+12 ")]
    public async Task Ean_upc_primary_errors_keep_bound_field_path(string primary)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", "\"LinearType\":4,\"Mode\":0");
        var data = JsonSerializer.Serialize(new { Primary = primary, Secondary = "(91)123" });
        var context = CreateContext(BuildRequest("Cpcl", "", template, data));
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        var error = response.RootElement.GetProperty("errors")[0];
        Assert.Equal("Primary", error.GetProperty("fieldPath").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
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
    [InlineData(1, "CcA")]
    [InlineData(2, "CcB")]
    public async Task Gs1_explicit_capacity_error_keeps_mode_and_secondary_binding(int mode, string modeName)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25,\"Mode\":1", $"\"LinearType\":11,\"Mode\":{mode}");
        var secondary = string.Concat(Enumerable.Range(91, 3).Select(ai => $"({ai})" + new string('A', 90)));
        var data = JsonSerializer.Serialize(new { Primary = "(01)12345678901231", Secondary = secondary });
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var context = CreateContext(BuildRequest("Cpcl", "", template, data), "gs1-composite,gs1-composite-gs1-128-ab");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        var error = json.RootElement.GetProperty("errors")[0];
        Assert.Contains(modeName + " 容量", error.GetProperty("message").GetString());
        Assert.Equal("Secondary", error.GetProperty("fieldPath").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
        Assert.Equal(0, parent.CurrentBytes);
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

    [Theory]
    [InlineData("Cpcl", "", false, true)]
    [InlineData("EscPos", "Native", false, false)]
    [InlineData("EscPos", "Raster", true, true)]
    [InlineData("Tspl", "", false, true)]
    [InlineData("Zpl", "", false, true)]
    public async Task Expanded_stacked_column_ranges_match_all_five_modes(string protocol, string layout,
        bool ordinary, bool composite)
    {
        var context = CreateContext(BuildRequest(protocol, layout), "gs1-databar-expanded-stacked-columns");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Capabilities);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains("gs1-databar-expanded-stacked-columns", context.Response.Headers[FeaturesHeader].ToString());
        using var response = ReadJson(context);
        var range = response.RootElement.GetProperty("expandedStackedColumns");
        Assert.Equal(ordinary, range.ValueKind != JsonValueKind.Null);
        if (ordinary)
        {
            Assert.Equal(1, range.GetProperty("minimum").GetInt32());
            Assert.Equal(11, range.GetProperty("maximum").GetInt32());
            Assert.Equal(2, range.GetProperty("default").GetInt32());
        }
        var gs1 = response.RootElement.GetProperty("gs1Composite");
        Assert.Equal(composite, gs1.ValueKind != JsonValueKind.Null);
        if (composite)
        {
            range = gs1.GetProperty("expandedStackedColumns");
            Assert.Equal(2, range.GetProperty("minimum").GetInt32());
            Assert.Equal(11, range.GetProperty("maximum").GetInt32());
            Assert.Equal(2, range.GetProperty("default").GetInt32());
        }
    }

    [Theory]
    [InlineData(2, 204, 131)]
    [InlineData(3, 302, 90)]
    [InlineData(4, 400, 90)]
    [InlineData(11, 596, 49)]
    public async Task Expanded_stacked_columns_preview_uses_actual_encoded_dimensions(int columns, int width, int height)
    {
        var template = CompositeTemplate.Replace("\"LinearType\":25", "\"LinearType\":31,\"ExpandedStackedColumns\":" + columns)
            .Replace("\"LinearBarHeight\":20", "\"LinearBarHeight\":35").Replace("\"Rotation\":90", "\"Rotation\":0")
            .Replace("\"Width\":500", "\"Width\":1200");
        var context = CreateContext(BuildRequest("EscPos", "Raster", template,
            "{\"Primary\":\"[91]1234567890123456789012345678901234\",\"Secondary\":\"[20]12\"}"),
            "gs1-databar-expanded-stacked-columns");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var response = ReadJson(context);
        var element = response.RootElement.GetProperty("pages")[0].GetProperty("elements")[0];
        Assert.Equal(11, element.GetProperty("type").GetInt32());
        Assert.Equal(width, element.GetProperty("width").GetInt32());
        Assert.Equal(height, element.GetProperty("height").GetInt32());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("12")]
    [InlineData("2.5")]
    [InlineData("\"3\"")]
    public async Task Invalid_composite_column_requests_are_structured_errors(string columns)
    {
        using var budget = PrintResourceScope.Begin(8 * 1024 * 1024);
        var template = CompositeTemplate.Replace("\"LinearType\":25", "\"LinearType\":31,\"ExpandedStackedColumns\":" + columns);
        var context = CreateContext(BuildRequest("Cpcl", "", template));
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var response = ReadJson(context);
        Assert.True(response.RootElement.GetProperty("errors").GetArrayLength() > 0);
        Assert.Equal(0, budget.CurrentBytes);
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
