using System.Text.Json;

/// <summary>在写出 HTTP 头和内容前完成 JSON 生成与预算检查。</summary>
internal sealed class BudgetedJsonResult(object value, JsonSerializerOptions options) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.RequestAborted.ThrowIfCancellationRequested();
        using var stream = new BudgetedHostStream("Host.JsonBuffer", httpContext.RequestAborted);
        JsonSerializer.Serialize(stream, value, options);
        var bytes = stream.ToArray("Host.JsonOutput");
        httpContext.RequestAborted.ThrowIfCancellationRequested();
        await Results.Bytes(bytes, "application/json; charset=utf-8").ExecuteAsync(httpContext);
    }
}
