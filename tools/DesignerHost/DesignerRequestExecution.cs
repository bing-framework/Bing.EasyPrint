using System.Globalization;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint.Printing;
using Microsoft.Net.Http.Headers;

/// <summary>将请求读取、资源准备、处理及响应写出置于同一资源作用域。</summary>
internal static class DesignerRequestExecution
{
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web);

    internal static long? ReadServerLimit()
    {
        var value = Environment.GetEnvironmentVariable("EASYPRINT_DESIGNER_MAX_WORKING_BYTES");
        if (string.IsNullOrEmpty(value)) return null;
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit < 0)
            throw new ArgumentException("EASYPRINT_DESIGNER_MAX_WORKING_BYTES 必须是非负整数。");
        return limit;
    }

    internal static async Task ExecuteAsync(HttpContext context,
        Func<DesignerRequest, CancellationToken, IResult> process, long? serverMaxWorkingBytes = null,
        DesignerRequestAdmission? admission = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        DesignerContract.SetResponseHeaders(context.Response);
        try { DesignerContract.Validate(context.Request); }
        catch (DesignerContractException error)
        {
            await DesignerRequestProcessor.Error(error).ExecuteAsync(context);
            return;
        }
        IDisposable? admissionLease;
        try { admissionLease = admission == null ? null : await admission.EnterAsync(context.RequestAborted); }
        catch (DesignerAdmissionRejectedException error)
        {
            await DesignerRequestProcessor.Error(error).ExecuteAsync(context);
            return;
        }
        using var heldAdmission = admissionLease;
        var requestLimit = serverMaxWorkingBytes ?? long.MaxValue;
        if (admission?.RequestMaxWorkingBytes is long admissionLimit)
            requestLimit = Math.Min(requestLimit, admissionLimit);
        using var scope = PrintResourceScope.Begin(requestLimit, context.RequestAborted);
        IResult result;
        try
        {
            var request = await ReadAsync(context.Request, context.RequestAborted);
            if (request.MaxWorkingBytes.HasValue)
                scope.LimitTo(Math.Min(scope.MaxBytes, request.MaxWorkingBytes.Value));
            context.RequestAborted.ThrowIfCancellationRequested();
            result = process(request, context.RequestAborted);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { result = DesignerRequestProcessor.Error(error); }

        try { await result.ExecuteAsync(context); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (!context.Response.HasStarted)
        {
            await DesignerRequestProcessor.Error(error).ExecuteAsync(context);
        }
    }

    private static async Task<DesignerRequest> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.ContentLength > DesignerRequestProcessor.MaxRequestBytes)
            throw new BadHttpRequestException("请求超过 44 MiB 上限。", StatusCodes.Status413PayloadTooLarge);
        if (!request.HasJsonContentType())
            throw new BadHttpRequestException("请求必须使用 JSON Content-Type。", StatusCodes.Status415UnsupportedMediaType);
        var encoding = RequestEncoding(request);

        using var bufferLease = PrintResourceScope.Reserve(8192, "Host.RequestReadBuffer");
        var buffer = new byte[8192];
        using var input = new BudgetedHostStream("Host.RequestBuffer", cancellationToken);
        while (true)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0) break;
            if (input.Length + read > DesignerRequestProcessor.MaxRequestBytes)
                throw new BadHttpRequestException("请求超过 44 MiB 上限。", StatusCodes.Status413PayloadTooLarge);
            input.Write(buffer, 0, read);
        }
        input.Position = 0;
        if (encoding.CodePage == Encoding.UTF8.CodePage)
            ValidateUtf8(input, buffer, cancellationToken);
        using var transcoded = encoding.CodePage == Encoding.UTF8.CodePage
            ? null : Transcode(input, buffer, encoding, cancellationToken);
        var json = transcoded ?? input;
        // DTO 字符串及 JsonElement 原始数据按 UTF-8 输入长度的两倍估计；不包含全部对象/集合开销。
        PrintResourceScope.Retain(checked(json.Length * 2), "Host.RequestModel");
        json.Position = 0;
        cancellationToken.ThrowIfCancellationRequested();
        var model = JsonSerializer.Deserialize<DesignerRequest>(json, RequestJson)
            ?? throw new ArgumentException("请求必须为 JSON 对象。");
        cancellationToken.ThrowIfCancellationRequested();
        model.EncodedRequestBytes = json.Length;
        return model;
    }

    private static Encoding RequestEncoding(HttpRequest request)
    {
        var charset = HeaderUtilities.RemoveQuotes(request.GetTypedHeaders().ContentType!.Charset);
        if (!charset.HasValue) return Encoding.UTF8;
        try
        {
            return Encoding.GetEncoding(charset.Value!, EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        {
            throw new BadHttpRequestException("请求 JSON charset 不受支持。", StatusCodes.Status415UnsupportedMediaType);
        }
    }

    // JsonElement 延迟解码字符串；在绑定前验证完整输入，避免未读取的字段绕过 UTF-8 校验。
    private static void ValidateUtf8(BudgetedHostStream input, byte[] readBuffer,
        CancellationToken cancellationToken)
    {
        const int characterCapacity = 512;
        using var lease = PrintResourceScope.Reserve(characterCapacity * 2L, "Host.RequestUtf8Validation");
        var characters = new char[characterCapacity];
        var decoder = new UTF8Encoding(false, true).GetDecoder();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = input.Read(readBuffer, 0, readBuffer.Length);
            var end = read == 0;
            var offset = 0;
            bool completed;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                decoder.Convert(readBuffer, offset, read - offset, characters, 0, characters.Length, end,
                    out var consumed, out _, out completed);
                offset += consumed;
            } while (!completed);
            if (end) return;
        }
    }

    private static BudgetedHostStream Transcode(BudgetedHostStream input, byte[] readBuffer,
        Encoding encoding, CancellationToken cancellationToken)
    {
        const int characterCapacity = 4096;
        var utf8 = new UTF8Encoding(false, true);
        var byteCapacity = utf8.GetMaxByteCount(characterCapacity);
        using var characterLease = PrintResourceScope.Reserve(characterCapacity * 2L, "Host.RequestTranscodeCharacters");
        using var byteLease = PrintResourceScope.Reserve(byteCapacity, "Host.RequestTranscodeBytes");
        var characters = new char[characterCapacity];
        var bytes = new byte[byteCapacity];
        var decoder = encoding.GetDecoder();
        var encoder = utf8.GetEncoder();
        var output = new BudgetedHostStream("Host.RequestUtf8Buffer", cancellationToken);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = input.Read(readBuffer, 0, readBuffer.Length);
                var end = read == 0;
                var offset = 0;
                bool decoded;
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    decoder.Convert(readBuffer, offset, read - offset, characters, 0, characters.Length, end,
                        out var consumed, out var characterCount, out decoded);
                    offset += consumed;
                    var characterOffset = 0;
                    bool encoded;
                    do
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        encoder.Convert(characters, characterOffset, characterCount - characterOffset,
                            bytes, 0, bytes.Length, end && decoded, out var consumedCharacters,
                            out var byteCount, out encoded);
                        characterOffset += consumedCharacters;
                        if (output.Length + byteCount > DesignerRequestProcessor.MaxRequestBytes)
                            throw new BadHttpRequestException("转为 UTF-8 后的请求超过 44 MiB 上限。",
                                StatusCodes.Status413PayloadTooLarge);
                        output.Write(bytes, 0, byteCount);
                    } while (!encoded);
                } while (!decoded);
                if (end) break;
            }
            cancellationToken.ThrowIfCancellationRequested();
            output.Position = 0;
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
