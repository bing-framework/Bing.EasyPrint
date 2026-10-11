using System.Text;
using System.Text.Json;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class HostRequestCharsetTests
{
    private const string ExpectedValue = "中文绑定";
    private const string RequestPrefix =
        "{\"template\":{\"Version\":1,\"Width\":80,\"Height\":40,\"Rows\":[]},\"data\":{\"Name\":";
    private const string RequestSuffix = "}}";

    static HostRequestCharsetTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public async Task Utf8_json_without_charset_binds_data_and_keeps_request_body_open()
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        var body = new TrackingBodyStream(Encoding.UTF8.GetBytes(RequestJson(ExpectedValue)));
        var context = CreateContext(body, "application/json");

        await DesignerRequestExecution.ExecuteAsync(context, EchoData);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(ExpectedValue, await ReadResponseText(context));
        Assert.True(body.ReadCount > 0);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Quoted_utf8_charset_binds_data()
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        var body = new TrackingBodyStream(Encoding.UTF8.GetBytes(RequestJson(ExpectedValue)));
        var context = CreateContext(body, "application/json; charset=\"utf-8\"");

        await DesignerRequestExecution.ExecuteAsync(context, EchoData);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(ExpectedValue, await ReadResponseText(context));
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("utf-16le")]
    [InlineData("utf-16be")]
    [InlineData("utf-32le")]
    [InlineData("utf-32be")]
    [InlineData("gbk")]
    public async Task Supported_non_utf8_charsets_bind_chinese_data(string charset)
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var encoding = EncodingFor(charset);
        var json = RequestJson(ExpectedValue);
        Assert.Contains(ExpectedValue, json, StringComparison.Ordinal);
        var body = new TrackingBodyStream(encoding.GetBytes(json));
        var context = CreateContext(body, $"application/json; charset={charset}");

        await DesignerRequestExecution.ExecuteAsync(context, EchoData);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(ExpectedValue, await ReadResponseText(context));
        Assert.True(body.ReadCount > 0);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Utf16_large_json_crosses_read_buffer_and_surrogate_boundary()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var value = CreateUtf16BoundaryValue();
        var json = RequestJson(value);
        var boundary = 4095;

        Assert.Equal('\uD83D', json[boundary]);
        Assert.Equal('\uDE00', json[boundary + 1]);
        Assert.Contains("😀", json, StringComparison.Ordinal);

        var body = new TrackingBodyStream(EncodingFor("utf-16le").GetBytes(json));
        var context = CreateContext(body, "application/json; charset=utf-16le");

        await DesignerRequestExecution.ExecuteAsync(context, EchoData);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(value, await ReadResponseText(context));
        Assert.True(body.ReadCount > 1);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Gbk_large_json_crosses_read_and_character_buffer_boundaries()
    {
        using var parent = PrintResourceScope.Begin(8 * 1024 * 1024);
        var value = new string('中', 5000);
        var json = RequestJson(value);
        var encoding = EncodingFor("gbk");

        Assert.Contains(value, json, StringComparison.Ordinal);
        var body = new TrackingBodyStream(encoding.GetBytes(json));
        var context = CreateContext(body, "application/json; charset=gbk");

        await DesignerRequestExecution.ExecuteAsync(context, EchoData);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(value, await ReadResponseText(context));
        Assert.True(body.ReadCount > 1);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Utf8_bom_json_keeps_existing_read_semantics()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var body = new TrackingBodyStream(WithPreamble(Encoding.UTF8, RequestJson(ExpectedValue)));
        var context = CreateContext(body, "application/json; charset=utf-8");

        await DesignerRequestExecution.ExecuteAsync(context, EchoData);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(ExpectedValue, await ReadResponseText(context));
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("utf-16le")]
    [InlineData("utf-16be")]
    public async Task Utf16_bom_json_keeps_existing_read_semantics(string charset)
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var body = new TrackingBodyStream(WithPreamble(EncodingWithPreamble(charset), RequestJson(ExpectedValue)));
        var context = CreateContext(body, $"application/json; charset={charset}");

        await DesignerRequestExecution.ExecuteAsync(context, EchoData);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(ExpectedValue, await ReadResponseText(context));
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Invalid_utf8_inside_json_string_returns_400_before_action_and_recovers_scope()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var body = new TrackingBodyStream(CreateInvalidUtf8Json());
        var context = CreateContext(body, "application/json; charset=utf-8");
        var invoked = false;

        await DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
        {
            invoked = true;
            return Results.Text("unexpected");
        });

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(invoked);
        Assert.True(body.ReadCount > 0);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("application/json; charset=unknown-easyprint-charset")]
    [InlineData("text/plain; charset=utf-8")]
    public async Task Unsupported_charset_or_content_type_returns_415_before_reading_body(string contentType)
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        var body = new TrackingBodyStream(Encoding.UTF8.GetBytes(RequestJson(ExpectedValue)));
        var context = CreateContext(body, contentType);
        var invoked = false;

        await DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
        {
            invoked = true;
            return Results.Text("unexpected");
        });

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);
        Assert.False(invoked);
        Assert.Equal(0, body.ReadCount);
        Assert.False(body.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Malformed_non_utf8_body_returns_400_and_following_request_recovers_scope()
    {
        using var parent = PrintResourceScope.Begin(4 * 1024 * 1024);
        var malformedBody = new TrackingBodyStream(new byte[] { 0x7B });
        var malformed = CreateContext(malformedBody, "application/json; charset=utf-16le");
        var invoked = false;

        await DesignerRequestExecution.ExecuteAsync(malformed, (_, _) =>
        {
            invoked = true;
            return Results.Text("unexpected");
        });

        Assert.Equal(StatusCodes.Status400BadRequest, malformed.Response.StatusCode);
        Assert.False(invoked);
        Assert.True(malformedBody.ReadCount > 0);
        Assert.False(malformedBody.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);

        var validBody = new TrackingBodyStream(Encoding.UTF8.GetBytes(RequestJson(ExpectedValue)));
        var valid = CreateContext(validBody, "application/json");
        await DesignerRequestExecution.ExecuteAsync(valid, EchoData);

        Assert.Equal(StatusCodes.Status200OK, valid.Response.StatusCode);
        Assert.Equal(ExpectedValue, await ReadResponseText(valid));
        Assert.False(validBody.IsDisposed);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Non_utf8_conversion_budget_failure_returns_413_and_following_request_recovers()
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        var body = new TrackingBodyStream(Encoding.Unicode.GetBytes(RequestJson("中")));
        var context = CreateContext(body, "application/json; charset=utf-16le");
        var invoked = false;

        // 8192-byte read buffer + 256-byte initial input capacity leaves no room
        // for the conversion character buffer, so conversion is rejected before
        // allocating its working arrays.
        await DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
        {
            invoked = true;
            return Results.Text("unexpected");
        }, serverMaxWorkingBytes: 8448);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.False(invoked);
        Assert.False(body.IsDisposed);
        AssertResourceNameContains(context, "Host.RequestTranscode");
        Assert.Equal(0, parent.CurrentBytes);

        var validBody = new TrackingBodyStream(Encoding.UTF8.GetBytes(RequestJson(ExpectedValue)));
        var valid = CreateContext(validBody, "application/json");
        await DesignerRequestExecution.ExecuteAsync(valid, EchoData, serverMaxWorkingBytes: 1024 * 1024);

        Assert.Equal(StatusCodes.Status200OK, valid.Response.StatusCode);
        Assert.Equal(ExpectedValue, await ReadResponseText(valid));
        Assert.Equal(0, parent.CurrentBytes);
    }

    private static IResult EchoData(DesignerRequest request, CancellationToken _)
        => Results.Text(request.Data.GetProperty("Name").GetString() ?? string.Empty);

    private static string RequestJson(string value)
        => RequestPrefix + "\"" + value + "\"" + RequestSuffix;

    private static byte[] CreateInvalidUtf8Json()
    {
        var prefix = Encoding.UTF8.GetBytes(RequestPrefix + "\"");
        var suffix = Encoding.UTF8.GetBytes("\"" + RequestSuffix);
        var result = new byte[prefix.Length + 1 + suffix.Length];
        Buffer.BlockCopy(prefix, 0, result, 0, prefix.Length);
        result[prefix.Length] = 0xFF;
        Buffer.BlockCopy(suffix, 0, result, prefix.Length + 1, suffix.Length);
        return result;
    }

    private static string CreateUtf16BoundaryValue()
    {
        const int highSurrogateIndex = 4095;
        var openingQuoteLength = RequestPrefix.Length + 1;
        var fillerLength = highSurrogateIndex - openingQuoteLength;
        Assert.True(fillerLength > 0);
        Assert.Equal(highSurrogateIndex, openingQuoteLength + fillerLength);
        return new string('甲', fillerLength) + "😀" + new string('乙', 4096);
    }

    private static Encoding EncodingFor(string charset)
        => charset switch
        {
            "utf-16le" => new UnicodeEncoding(false, false, true),
            "utf-16be" => new UnicodeEncoding(true, false, true),
            "utf-32le" => new UTF32Encoding(false, false, true),
            "utf-32be" => new UTF32Encoding(true, false, true),
            "gbk" => Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback),
            _ => throw new ArgumentOutOfRangeException(nameof(charset))
        };

    private static Encoding EncodingWithPreamble(string charset)
        => charset switch
        {
            "utf-16le" => new UnicodeEncoding(false, true, true),
            "utf-16be" => new UnicodeEncoding(true, true, true),
            _ => throw new ArgumentOutOfRangeException(nameof(charset))
        };

    private static byte[] WithPreamble(Encoding encoding, string json)
    {
        var preamble = encoding.GetPreamble();
        var payload = encoding.GetBytes(json);
        var result = new byte[preamble.Length + payload.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(payload, 0, result, preamble.Length, payload.Length);
        return result;
    }

    private static DefaultHttpContext CreateContext(Stream body, string contentType)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = body;
        context.Request.ContentLength = body.CanSeek ? body.Length : null;
        context.Request.ContentType = contentType;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseText(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, false, 1024, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static void AssertResourceNameContains(HttpContext context, string expected)
    {
        context.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(context.Response.Body);
        var resourceName = document.RootElement.GetProperty("errors")[0]
            .GetProperty("resourceName").GetString();
        Assert.NotNull(resourceName);
        Assert.Contains(expected, resourceName!, StringComparison.Ordinal);
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
