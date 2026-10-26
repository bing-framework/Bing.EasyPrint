using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using ZXing;
using ZXing.Common;

public sealed class Gs1DigitalLinkHostTests
{
    static Gs1DigitalLinkHostTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const string Gtin = "09520123456788";
    private const string Template = "{\"Version\":2,\"Width\":300,\"Height\":120,\"Rows\":[{" +
        "\"Cells\":[{\"Width\":300,\"Elements\":[{\"Type\":3,\"Gs1\":false,\"Field\":{" +
        "\"Gs1Input\":{\"Mode\":1,\"BaseUri\":\"https://id.gs1.org\",\"Items\":[{" +
        "\"Ai\":\"01\",\"Name\":\"Product.Gtin\"},{\"Ai\":\"10\",\"Value\":\"LOT-A\"}]}}}]}]}]}";

    [Fact]
    public async Task Catalog_returns_every_fixed_ai_and_the_digital_link_roles()
    {
        var context = CreateContext("{}");
        await DesignerRequestExecution.ExecuteAsync(context, (_, _) => DesignerRequestProcessor.Gs1Catalog());

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains("gs1-ai-catalog", context.Response.Headers["X-EasyPrint-Features"].ToString());
        using var json = ReadJson(context);
        var entries = json.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(536, entries.Length);
        Assert.Equal("d1702ffb2fa5060f8460f7d581b01e377deaa1d6",
            json.RootElement.GetProperty("dictionaryCommit").GetString());
        foreach (var ai in new[] { "03", "8014", "8200" })
            Assert.Equal("Unsupported", entries.Single(entry => entry.GetProperty("ai").GetString() == ai)
                .GetProperty("role").GetString());
    }

    [Fact]
    public async Task Validation_reports_pending_then_uses_the_real_binding_data()
    {
        var pending = CreateContext("{\"template\":" + Template + "}");
        await DesignerRequestExecution.ExecuteAsync(pending, DesignerRequestProcessor.Gs1Validate);
        using (var json = ReadJson(pending))
        {
            Assert.True(json.RootElement.TryGetProperty("status", out _), json.RootElement.GetRawText());
            Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("pending-data", json.RootElement.GetProperty("items")[0].GetProperty("status").GetString());
        }

        var valid = CreateContext("{\"template\":" + Template +
            ",\"data\":{\"Product\":{\"Gtin\":\"" + Gtin + "\"}}}");
        await DesignerRequestExecution.ExecuteAsync(valid, DesignerRequestProcessor.Gs1Validate);
        using (var json = ReadJson(valid))
        {
            Assert.Equal("valid", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("https://id.gs1.org/01/" + Gtin + "/10/LOT-A",
                json.RootElement.GetProperty("items")[0].GetProperty("output").GetString());
        }

        var invalid = CreateContext("{\"template\":" + Template +
            ",\"data\":{\"Product\":{\"Gtin\":\"09520123456780\"}}}");
        await DesignerRequestExecution.ExecuteAsync(invalid, DesignerRequestProcessor.Gs1Validate);
        using var invalidJson = ReadJson(invalid);
        Assert.Equal("invalid", invalidJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("01", invalidJson.RootElement.GetProperty("errors")[0].GetProperty("ai").GetString());
    }

    [Fact]
    public async Task Validation_without_data_marks_dynamic_repeat_condition_and_component_paths_pending()
    {
        const string repeated = """
        {"Version":2,"Width":300,"Height":240,"Rows":[{"RepeatSource":"Items","Cells":[{"Width":300,"Elements":[{"Type":3,"Gs1":false,"Field":{"Gs1Input":{"Mode":1,"BaseUri":"https://id.gs1.org","Items":[{"Ai":"01","Name":"$item.Gtin"}]}}}]}]}]}
        """;
        const string conditional = """
        {"Version":2,"Width":300,"Height":240,"Rows":[{"Cells":[{"Width":300,"Elements":[{"Type":3,"Gs1":false,"Condition":{"Path":"Product.Visible","Operator":"eq","Value":true},"Field":{"Gs1Input":{"Mode":1,"BaseUri":"https://id.gs1.org","Items":[{"Ai":"01","Name":"Product.Gtin"}]}}}]}]}]}
        """;
        const string component = """
        {"Version":3,"Width":300,"Height":240,"Components":[{"Id":"label","Width":300,"Height":240,"Parameters":{"gtin":{"Required":true}},"Elements":[{"Type":3,"Gs1":false,"Field":{"Gs1Input":{"Mode":1,"BaseUri":"https://id.gs1.org","Items":[{"Ai":"01","Name":"$param.gtin"}]}}}]}],"Rows":[{"Cells":[{"Width":300,"Elements":[{"Type":6,"ComponentId":"label","Width":300,"Height":240,"Parameters":{"gtin":{"Path":"Product.Gtin"}}}]}]}]}
        """;

        foreach (var template in new[] { repeated, conditional, component })
        {
            var context = CreateContext("{\"template\":" + template + "}");
            await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Validate);

            using var json = ReadJson(context);
            Assert.Equal("pending", json.RootElement.GetProperty("status").GetString());
            Assert.False(json.RootElement.GetProperty("dataProvided").GetBoolean());
            Assert.Contains(json.RootElement.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("status").GetString() == "pending-data");
        }

        var missingPath = CreateContext("{\"template\":" + repeated + ",\"data\":{}}");
        await DesignerRequestExecution.ExecuteAsync(missingPath, DesignerRequestProcessor.Gs1Validate);
        using var missingPathJson = ReadJson(missingPath);
        Assert.Equal("invalid", missingPathJson.RootElement.GetProperty("status").GetString());
        Assert.Contains("Items", missingPathJson.RootElement.GetProperty("errors")[0]
            .GetProperty("message").GetString());

        var staticInvalid = repeated.Replace("\"Ai\":\"01\"", "\"Ai\":\"9999\"")
            .Replace("\"Name\":\"$item.Gtin\"", "\"Value\":\"BAD\"");
        var invalidConfig = CreateContext("{\"template\":" + staticInvalid + "}");
        await DesignerRequestExecution.ExecuteAsync(invalidConfig, DesignerRequestProcessor.Gs1Validate);
        using var invalidConfigJson = ReadJson(invalidConfig);
        Assert.True(invalidConfigJson.RootElement.TryGetProperty("errors", out var staticErrors));
        Assert.NotEmpty(staticErrors.EnumerateArray());
        Assert.Contains("9999", staticErrors[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task Conversion_returns_template_ready_items_and_preserves_duplicate_extensions()
    {
        const string input = "[01]09520123456788[22]2A[10]LOT-A[17]260101";
        var context = CreateContext("{\"gs1Conversion\":{\"input\":\"" + input +
            "\",\"targetFormat\":\"digital-link\",\"baseUri\":\"https://id.gs1.org\"," +
            "\"queryParameters\":[{\"key\":\"campaign\",\"value\":\"label\"},{\"key\":\"campaign\",\"value\":\"scan\"}]}}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Convert);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var json = ReadJson(context);
        Assert.Equal("https://id.gs1.org/01/" + Gtin + "/22/2A/10/LOT-A?17=260101&campaign=label&campaign=scan",
            json.RootElement.GetProperty("output").GetString());
        var structured = json.RootElement.GetProperty("structuredInput");
        Assert.Equal(1, structured.GetProperty("mode").GetInt32());
        Assert.Equal(new[] { "01", "22", "10", "17" }, structured.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("ai").GetString()));
        Assert.Equal(new[] { "label", "scan" }, structured.GetProperty("queryParameters").EnumerateArray()
            .Select(item => item.GetProperty("value").GetString()));
    }

    [Fact]
    public async Task Conversion_accepts_a_qualifier_value_equal_to_an_ai()
    {
        const string input = "https://id.gs1.org/01/09520123456788/10/01";
        var context = CreateContext("{\"gs1Conversion\":{\"input\":\"" + input +
            "\",\"targetFormat\":\"digital-link\"}}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Convert);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var json = ReadJson(context);
        Assert.Equal(input, json.RootElement.GetProperty("output").GetString());
    }

    [Theory]
    [InlineData("https://id.gs1.org/01/09520123456788/10/LOT?10=OTHER")]
    [InlineData("https://[bad/01/09520123456788")]
    [InlineData("https://id.gs1.org/tenant%GG/01/09520123456788")]
    public async Task Conversion_rejects_duplicate_gs1_ai_and_malformed_uri_structure(string input)
    {
        var context = CreateContext("{\"gs1Conversion\":{\"input\":\"" + input +
            "\",\"targetFormat\":\"digital-link\"}}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Convert);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        Assert.Equal("invalid", json.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("/10/LOT", "10", "LOT", 1)]
    [InlineData("?10=LOT", "10", "LOT", 2)]
    [InlineData("/21/SERIAL", "21", "SERIAL", 1)]
    [InlineData("?21=SERIAL", "21", "SERIAL", 2)]
    [InlineData("/235/TPX-1", "235", "TPX-1", 1)]
    [InlineData("?235=TPX-1", "235", "TPX-1", 2)]
    public async Task Conversion_preserves_each_ai_path_or_query_placement_after_template_validation(
        string suffix, string ai, string value, int expectedPlacement)
    {
        var input = "https://id.gs1.org/01/" + Gtin + suffix;
        var conversion = CreateContext("{\"gs1Conversion\":{\"input\":\"" + input +
            "\",\"targetFormat\":\"digital-link\"}}");
        await DesignerRequestExecution.ExecuteAsync(conversion, DesignerRequestProcessor.Gs1Convert);
        Assert.Equal(StatusCodes.Status200OK, conversion.Response.StatusCode);

        using var converted = ReadJson(conversion);
        var structured = converted.RootElement.GetProperty("structuredInput").GetRawText();
        var template = "{\"Version\":2,\"Width\":300,\"Height\":120,\"Rows\":[{" +
            "\"Cells\":[{\"Width\":300,\"Elements\":[{\"Type\":3,\"Gs1\":false,\"Field\":{" +
            "\"Gs1Input\":" + structured + "}}]}]}]}";

        var convertedItem = converted.RootElement.GetProperty("structuredInput").GetProperty("items")
            .EnumerateArray().Single(item => item.GetProperty("ai").GetString() == ai);
        Assert.Equal(value, convertedItem.GetProperty("value").GetString());
        Assert.Equal(expectedPlacement, convertedItem.GetProperty("placement").GetInt32());

        var validation = CreateContext("{\"template\":" + template + ",\"data\":{}}");
        await DesignerRequestExecution.ExecuteAsync(validation, DesignerRequestProcessor.Gs1Validate);

        Assert.Equal(StatusCodes.Status200OK, validation.Response.StatusCode);
        using var checkedJson = ReadJson(validation);
        Assert.Equal("valid", checkedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(input, checkedJson.RootElement.GetProperty("items")[0].GetProperty("output").GetString());
    }

    [Fact]
    public async Task Validation_expands_repeat_rows_and_uses_each_item_binding_context()
    {
        const string template = """
        {"Version":2,"Width":300,"Height":240,"Rows":[{"RepeatSource":"Items","Cells":[{"Width":300,"Elements":[{"Type":3,"Gs1":false,"Field":{"Gs1Input":{"Mode":1,"BaseUri":"https://id.gs1.org","Items":[{"Ai":"01","Name":"$item.Gtin"},{"Ai":"10","Name":"$item.Lot"}]}}}]}]}]}
        """;
        const string data = "{\"Items\":[{\"Gtin\":\"09520123456788\",\"Lot\":\"LOT-A\"},{\"Gtin\":\"09520123456788\",\"Lot\":\"LOT-B\"}]}";
        var context = CreateContext("{\"template\":" + template + ",\"data\":" + data + "}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Validate);

        using var json = ReadJson(context);
        Assert.Equal("valid", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(new[]
        {
            "https://id.gs1.org/01/09520123456788/10/LOT-A",
            "https://id.gs1.org/01/09520123456788/10/LOT-B"
        }, json.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("output").GetString()));

        Assert.Equal(new[]
        {
            "https://id.gs1.org/01/09520123456788/10/LOT-A",
            "https://id.gs1.org/01/09520123456788/10/LOT-B"
        }, await RenderedGs1Outputs(template, data));
    }

    [Fact]
    public async Task Validation_matches_nested_repeat_conditions_root_variables_and_instance_indices()
    {
        const string template = """
        {"Version":3,"Width":300,"Height":240,"Variables":{"primary":"Gtin"},"Rows":[{"RepeatSource":"Items","Cells":[{"Width":300,"Rows":[{"RepeatSource":"$item.Lots","Condition":{"Path":"$item.Enabled","Operator":"eq","Value":true},"Cells":[{"Width":300,"Elements":[{"Type":3,"Gs1":false,"Field":{"Gs1Input":{"Mode":1,"BaseUri":"https://id.gs1.org","Items":[{"Ai":"01","Name":"$var.primary"},{"Ai":"10","Name":"$item.Lot"},{"Ai":"17","Name":"$root.Expiry"}]}}}]}]}]}]}]}
        """;
        const string data = "{\"Gtin\":\"09520123456788\",\"Expiry\":\"260101\",\"Items\":[{\"Lots\":[{" +
            "\"Lot\":\"LOT-A\",\"Enabled\":true},{\"Lot\":\"LOT-X\",\"Enabled\":false},{\"Lot\":\"LOT-B\",\"Enabled\":true}]}]}";
        var context = CreateContext("{\"template\":" + template + ",\"data\":" + data + "}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Validate);

        using var json = ReadJson(context);
        Assert.Equal("valid", json.RootElement.GetProperty("status").GetString());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[]
        {
            "https://id.gs1.org/01/09520123456788/10/LOT-A?17=260101",
            "https://id.gs1.org/01/09520123456788/10/LOT-B?17=260101"
        }, items.Select(item => item.GetProperty("output").GetString()));
        Assert.Equal(new[] { "0,0", "0,2" }, items.Select(item => string.Join(",",
            item.GetProperty("instanceIndices").EnumerateArray().Select(index => index.GetInt32()))));
        Assert.Equal(new[]
        {
            "https://id.gs1.org/01/09520123456788/10/LOT-A?17=260101",
            "https://id.gs1.org/01/09520123456788/10/LOT-B?17=260101"
        }, await RenderedGs1Outputs(template, data));
    }

    [Fact]
    public async Task Validation_uses_component_parameters_and_ignores_uninstantiated_definitions()
    {
        const string template = """
        {"Version":3,"Width":300,"Height":240,"Components":[
          {"Id":"label","Width":300,"Height":240,"Parameters":{"gtin":{"Required":true},"lot":{"Required":true}},"Elements":[{"Type":3,"Gs1":false,"Field":{"Gs1Input":{"Mode":1,"BaseUri":"https://id.gs1.org","Items":[{"Ai":"01","Name":"$param.gtin"},{"Ai":"10","Name":"$param.lot"}]}}}]},
          {"Id":"unused","Width":300,"Height":240,"Elements":[{"Type":3,"Gs1":false,"Field":{"Gs1Input":{"Mode":1,"BaseUri":"https://id.gs1.org","Items":[{"Ai":"01","Name":"Missing.Gtin"}]}}}]}
        ],"Rows":[{"RepeatSource":"Items","Cells":[{"Width":300,"Elements":[{"Type":6,"ComponentId":"label","Width":300,"Height":240,"Parameters":{"gtin":{"Path":"$item.Gtin"},"lot":{"Path":"$item.Lot"}}}]}]}]}
        """;
        const string data = "{\"Items\":[{\"Gtin\":\"09520123456788\",\"Lot\":\"LOT-C\"}]}";
        var context = CreateContext("{\"template\":" + template + ",\"data\":" + data + "}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Validate);

        using var json = ReadJson(context);
        Assert.Equal("valid", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("https://id.gs1.org/01/09520123456788/10/LOT-C",
            json.RootElement.GetProperty("items")[0].GetProperty("output").GetString());
        Assert.Equal(new[] { "https://id.gs1.org/01/09520123456788/10/LOT-C" },
            await RenderedGs1Outputs(template, data));
    }

    [Fact]
    public async Task Validation_maps_dynamic_qualifier_errors_to_the_source_item_index()
    {
        const string tooLongLot = "LOT-12345678901234567890";
        var template = Template.Replace("\"Value\":\"LOT-A\"", "\"Name\":\"Lot\"");
        var context = CreateContext("{\"template\":" + template +
            ",\"data\":{\"Product\":{\"Gtin\":\"09520123456788\"},\"Lot\":\"" + tooLongLot + "\"}}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Validate);

        using var json = ReadJson(context);
        var error = json.RootElement.GetProperty("errors")[0];
        Assert.Equal("10", error.GetProperty("ai").GetString());
        Assert.Equal(1, error.GetProperty("itemIndex").GetInt32());
        Assert.EndsWith("Items[1]", error.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task Validation_maps_a_primary_key_error_to_its_original_item_index()
    {
        var context = CreateContext("{\"template\":" + Template +
            ",\"data\":{\"Product\":{\"Gtin\":\"09520123456780\"}}}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Validate);

        using var json = ReadJson(context);
        var error = json.RootElement.GetProperty("errors")[0];
        Assert.Equal("01", error.GetProperty("ai").GetString());
        Assert.Equal(0, error.GetProperty("itemIndex").GetInt32());
        Assert.EndsWith("Items[0]", error.GetProperty("fieldPath").GetString());
    }

    [Theory]
    [InlineData("", "gtin-required")]
    [InlineData("INVALID", "gtin-characters")]
    [InlineData("12345", "gtin-length")]
    [InlineData("09520123456780", "gtin-check-digit")]
    public async Task Validation_maps_all_primary_key_errors_to_ai01_and_its_nonfirst_source_item(
        string gtin, string expectedCode)
    {
        const string template = """
        {"Version":2,"Width":300,"Height":120,"Rows":[{"Cells":[{"Width":300,"Elements":[{"Type":3,"Gs1":false,"Field":{"Gs1Input":{"Mode":1,"BaseUri":"https://id.gs1.org","Items":[{"Ai":"10","Value":"LOT-A"},{"Ai":"01","Name":"Product.Gtin"}]}}}]}]}]}
        """;
        var context = CreateContext("{\"template\":" + template + ",\"data\":{\"Product\":{\"Gtin\":\"" + gtin + "\"}}}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Validate);

        using var json = ReadJson(context);
        Assert.Equal("invalid", json.RootElement.GetProperty("status").GetString());
        var error = json.RootElement.GetProperty("errors")[0];
        Assert.Equal(expectedCode, error.GetProperty("code").GetString());
        Assert.Equal("01", error.GetProperty("ai").GetString());
        Assert.Equal(1, error.GetProperty("itemIndex").GetInt32());
        Assert.EndsWith("Items[1]", error.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task Elements_conversion_refuses_to_drop_uri_extensions_and_keeps_original_input()
    {
        const string input = "https://id.gs1.org/01/09520123456788?campaign=label#scan";
        var context = CreateContext("{\"gs1Conversion\":{\"input\":\"" + input +
            "\",\"targetFormat\":\"elements\"}}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Gs1Convert);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        Assert.Equal("invalid", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(input, json.RootElement.GetProperty("input").GetString());
    }

    [Fact]
    public async Task Rendered_digital_link_qr_decodes_as_an_ordinary_uri_without_gs1_fnc1()
    {
        const string uri = "https://id.gs1.org/01/09520123456788/10/LOT-A";
        const string template = "{\"Version\":2,\"Width\":400,\"Height\":600,\"Rows\":[{" +
            "\"Height\":600,\"Cells\":[{\"Width\":400,\"Elements\":[{" +
            "\"Type\":3,\"UnitWidth\":2,\"Gs1\":false,\"Field\":{\"Gs1Input\":{" +
            "\"Mode\":1,\"BaseUri\":\"https://id.gs1.org\",\"Items\":[{" +
            "\"Ai\":\"01\",\"Value\":\"09520123456788\"},{\"Ai\":\"10\",\"Value\":\"LOT-A\"}]}}}]}]}]}";
        var context = CreateContext("{\"template\":" + template +
            ",\"data\":{},\"protocol\":\"EscPos\",\"escPosLayoutMode\":\"Raster\",\"pageIndex\":0,\"elementIndex\":0}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.ElementPng);

        Assert.True(context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
        Assert.Equal("image/png", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var image = Image.Load<Rgb24>(context.Response.Body);
        var pixels = new byte[checked(image.Width * image.Height * 3)];
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];
            var offset = (y * image.Width + x) * 3;
            pixels[offset] = pixel.R;
            pixels[offset + 1] = pixel.G;
            pixels[offset + 2] = pixel.B;
        }
        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { TryHarder = true, PossibleFormats = new[] { BarcodeFormat.QR_CODE } }
        };
        var decoded = reader.Decode(new RGBLuminanceSource(pixels, image.Width, image.Height,
            RGBLuminanceSource.BitmapFormat.RGB24));

        Assert.NotNull(decoded);
        Assert.Equal(uri, decoded!.Text);
        Assert.Equal("]Q2", decoded.ResultMetadata![ResultMetadataType.SYMBOLOGY_IDENTIFIER].ToString());
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

    private static async Task<string[]> RenderedGs1Outputs(string template, string data)
    {
        var context = CreateContext("{\"template\":" + template + ",\"data\":" + data + "}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        using var json = ReadJson(context);
        Assert.True(context.Response.StatusCode == StatusCodes.Status200OK,
            Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
        return json.RootElement.GetProperty("pages").EnumerateArray()
            .SelectMany(page => page.GetProperty("elements").EnumerateArray())
            .Where(element => element.GetProperty("type").GetInt32() == 3)
            .Select(element => element.GetProperty("text").GetString())
            .Where(text => text != null && text.StartsWith("https://", StringComparison.Ordinal))
            .Select(text => text!)
            .ToArray();
    }
}
