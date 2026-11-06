using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class FieldFormatHostTests
{
    static FieldFormatHostTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const string Template =
        "{\"Version\":2,\"Width\":400,\"Height\":80,\"Rows\":[{\"Height\":80,\"Cells\":[{\"Width\":400,\"Elements\":[" +
        "{\"Type\":1,\"FontSize\":24,\"Field\":{\"Name\":\"Amount\",\"Format\":{\"Kind\":\"Number\",\"Pattern\":\"N2\",\"Culture\":\"en-US\"}}}" +
        "]}]}]}";

    [Fact]
    public async Task Preview_returns_authoritative_formatted_text_and_advertises_feature()
    {
        var context = CreateContext("{\"template\":" + Template + ",\"data\":{\"Amount\":\"1234.5\"}}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains("field-format", context.Response.Headers["X-EasyPrint-Features"].ToString());
        using var json = ReadJson(context);
        var element = json.RootElement.GetProperty("pages")[0].GetProperty("elements")
            .EnumerateArray().Single(item => item.GetProperty("type").GetInt32() == 1);
        Assert.Equal("1,234.50", element.GetProperty("displayText").GetString());
    }

    [Fact]
    public async Task Invalid_format_is_rejected_with_field_path()
    {
        var template = Template.Replace("\"Pattern\":\"N2\"", "\"Pattern\":\"Q2\"");
        var context = CreateContext("{\"template\":" + template + ",\"data\":{\"Amount\":\"12\"}}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        var error = json.RootElement.GetProperty("errors")[0];
        Assert.True(error.GetProperty("fieldPath").GetString() == "Amount", json.RootElement.GetRawText());
        Assert.Contains("数字格式", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Preview_preserves_high_precision_decimal_and_date_offset()
    {
        var decimalTemplate = Template.Replace("\"Pattern\":\"N2\"", "\"Pattern\":\"0.0000\"");
        var decimalContext = CreateContext("{\"template\":" + decimalTemplate +
            ",\"data\":{\"Amount\":123456789012345.6789}}");
        await DesignerRequestExecution.ExecuteAsync(decimalContext, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, decimalContext.Response.StatusCode);
        using (var json = ReadJson(decimalContext))
        {
            var text = json.RootElement.GetProperty("pages")[0].GetProperty("elements")
                .EnumerateArray().First(item => item.GetProperty("type").GetInt32() == 1)
                .GetProperty("displayText").GetString();
            Assert.Equal("123456789012345.6789", text);
        }

        var dateTemplate = Template.Replace("\"Kind\":\"Number\",\"Pattern\":\"N2\",\"Culture\":\"en-US\"",
            "\"Kind\":\"Date\",\"Pattern\":\"yyyy-MM-dd HH:mm zzz\"");
        var dateContext = CreateContext("{\"template\":" + dateTemplate +
            ",\"data\":{\"Amount\":\"2024-01-02T03:04:05-05:00\"}}");
        await DesignerRequestExecution.ExecuteAsync(dateContext, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, dateContext.Response.StatusCode);
        using var dateJson = ReadJson(dateContext);
        var dateText = dateJson.RootElement.GetProperty("pages")[0].GetProperty("elements")
            .EnumerateArray().First(item => item.GetProperty("type").GetInt32() == 1)
            .GetProperty("displayText").GetString();
        Assert.Equal("2024-01-02 03:04 -05:00", dateText);
    }

    [Fact]
    public async Task Preview_without_format_keeps_legacy_json_date_semantics()
    {
        var template = Template.Replace(
            "\"Name\":\"Amount\",\"Format\":{\"Kind\":\"Number\",\"Pattern\":\"N2\",\"Culture\":\"en-US\"}",
            "\"Name\":\"Amount\"");
        var context = CreateContext("{\"template\":" + template +
            ",\"data\":{\"Amount\":\"2024-01-02T03:04:05-05:00\"}}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var json = ReadJson(context);
        var text = json.RootElement.GetProperty("pages")[0].GetProperty("elements")
            .EnumerateArray().First(item => item.GetProperty("type").GetInt32() == 1)
            .GetProperty("displayText").GetString();
        var expected = DateTimeOffset.Parse("2024-01-02T03:04:05-05:00")
            .LocalDateTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, text);
    }

    [Fact]
    public async Task Preview_without_format_keeps_legacy_json_number_semantics()
    {
        var template = Template.Replace(
            "\"Name\":\"Amount\",\"Format\":{\"Kind\":\"Number\",\"Pattern\":\"N2\",\"Culture\":\"en-US\"}",
            "\"Name\":\"Amount\"");
        var context = CreateContext("{\"template\":" + template + ",\"data\":{\"Amount\":123456789012345.6789}}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
        using var json = ReadJson(context);
        var text = json.RootElement.GetProperty("pages")[0].GetProperty("elements")
            .EnumerateArray().First(item => item.GetProperty("type").GetInt32() == 1)
            .GetProperty("displayText").GetString();
        var expected = (123456789012345.6789d).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, text);
    }

    [Fact]
    public async Task Preview_preserves_legacy_large_numbers_and_only_converts_formatted_fields()
    {
        var plain = Template.Replace(
            "\"Name\":\"Amount\",\"Format\":{\"Kind\":\"Number\",\"Pattern\":\"N2\",\"Culture\":\"en-US\"}",
            "\"Name\":\"Amount\"");
        var large = CreateContext("{\"template\":" + plain + ",\"data\":{\"Amount\":1e40}}");
        await DesignerRequestExecution.ExecuteAsync(large, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, large.Response.StatusCode);
        using (var largeJson = ReadJson(large))
            Assert.Equal((1e40d).ToString(System.Globalization.CultureInfo.InvariantCulture),
                largeJson.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
                    .First(item => item.GetProperty("type").GetInt32() == 1).GetProperty("displayText").GetString());
        var unused = CreateContext("{\"template\":" + plain + ",\"data\":{\"Amount\":\"OK\",\"Unused\":1e40}}");
        await DesignerRequestExecution.ExecuteAsync(unused, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, unused.Response.StatusCode);
        using (var unusedJson = ReadJson(unused))
            Assert.Equal("OK", unusedJson.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
                .First(item => item.GetProperty("type").GetInt32() == 1).GetProperty("displayText").GetString());
        var formatted = CreateContext("{\"template\":" + Template + ",\"data\":{\"Amount\":1e40}}");
        await DesignerRequestExecution.ExecuteAsync(formatted, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status400BadRequest, formatted.Response.StatusCode);
        using var error = ReadJson(formatted);
        Assert.Equal("Amount", error.RootElement.GetProperty("errors")[0].GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task Preview_lowercase_standard_and_signed_text_min_values_fit_length_estimates()
    {
        var lowerTemplate = Template.Replace("\"Pattern\":\"N2\"", "\"Pattern\":\"f90\"");
        var lower = CreateContext("{\"template\":" + lowerTemplate + ",\"data\":{\"Amount\":1.2}}");
        await DesignerRequestExecution.ExecuteAsync(lower, DesignerRequestProcessor.Preview);
        Assert.Equal(StatusCodes.Status200OK, lower.Response.StatusCode);
        using (var lowerJson = ReadJson(lower))
            Assert.StartsWith("1.200000", lowerJson.RootElement.GetProperty("pages")[0].GetProperty("elements")
                .EnumerateArray().First(item => item.GetProperty("type").GetInt32() == 1).GetProperty("displayText").GetString());
        var textTemplate = Template.Replace(
            "\"Format\":{\"Kind\":\"Number\",\"Pattern\":\"N2\",\"Culture\":\"en-US\"}",
            "\"Format\":{\"Kind\":\"Text\"}");
        foreach (var value in new[] { "-128", "-32768", "-2147483648" })
        {
            var context = CreateContext("{\"template\":" + textTemplate + ",\"data\":{\"Amount\":" + value + "}}");
            await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            using var json = ReadJson(context);
            Assert.Equal(value, json.RootElement.GetProperty("pages")[0].GetProperty("elements").EnumerateArray()
                .First(item => item.GetProperty("type").GetInt32() == 1).GetProperty("displayText").GetString());
        }
    }

    [Theory]
    [InlineData("YYYY-MM-dd", "YYYY-02-01")]
    [InlineData("yyyy-MM-ddd", "2024-01-Mon")]
    [InlineData("yyyy-MM-dddd", "2024-01-Monday")]
    public async Task Preview_rejects_date_input_without_numeric_year_or_day_with_field_diagnostics(
        string inputFormat, string value)
    {
        var format = JsonSerializer.Serialize(new
        {
            Kind = "Date", Pattern = "yyyy-MM-dd", InputFormat = inputFormat, Culture = "en-US"
        });
        var template = Template.Replace(
            "{\"Kind\":\"Number\",\"Pattern\":\"N2\",\"Culture\":\"en-US\"}", format);
        var context = CreateContext("{\"template\":" + template + ",\"data\":" +
            JsonSerializer.Serialize(new { Amount = value }) + "}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var json = ReadJson(context);
        var error = json.RootElement.GetProperty("errors")[0];
        Assert.Equal("Amount", error.GetProperty("fieldPath").GetString());
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.GetProperty("sourcePath").GetString());
    }

    [Theory]
    [InlineData("yyyyyyyyyyyyyyyy-MM-dd", "0000000000002024-01-02")]
    [InlineData("yyyy-MM-dd zzz zzz zzz zzz", "2024-01-02 +08:00 +08:00 +08:00 +08:00")]
    public async Task Preview_returns_authoritative_date_text_for_repeated_custom_tokens(string pattern, string expected)
    {
        var format = JsonSerializer.Serialize(new { Kind = "Date", Pattern = pattern, Culture = "en-US" });
        var template = Template.Replace(
            "{\"Kind\":\"Number\",\"Pattern\":\"N2\",\"Culture\":\"en-US\"}", format)
            .Replace("\"Width\":400", "\"Width\":1600");
        var context = CreateContext("{\"template\":" + template +
            ",\"data\":{\"Amount\":\"2024-01-02T03:04:05+08:00\"}}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Preview);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var json = ReadJson(context);
        var element = json.RootElement.GetProperty("pages")[0].GetProperty("elements")
            .EnumerateArray().First(item => item.GetProperty("type").GetInt32() == 1);
        Assert.Equal(expected, element.GetProperty("text").GetString());
        Assert.Equal(expected, element.GetProperty("displayText").GetString());
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
}
