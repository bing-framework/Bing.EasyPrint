/// <summary>定义设计器 HTTP 契约；版本与 NuGet/模板文件版本相互独立。</summary>
internal static class DesignerContract
{
    internal const string VersionHeader = "X-EasyPrint-Contract-Version";
    internal const string FeaturesHeader = "X-EasyPrint-Features";
    internal const string RequiredFeaturesHeader = "X-EasyPrint-Required-Features";
    internal const string Version = "1";
    private static readonly string[] Features =
        ["structured-errors", "strict-options", "binding-diagnostics", "multi-protocol", "ordered-output-segments"];

    internal static object Description => new { version = 1, features = Features.ToArray() };

    internal static void SetResponseHeaders(HttpResponse response)
    {
        response.Headers[VersionHeader] = Version;
        response.Headers[FeaturesHeader] = string.Join(",", Features);
    }

    internal static void Validate(HttpRequest request)
    {
        if (request.Headers.TryGetValue(VersionHeader, out var version) && version.ToString() != Version)
            throw new DesignerContractException("ContractVersionMismatch", "打印服务契约版本不兼容。", VersionHeader);
        if (!request.Headers.TryGetValue(RequiredFeaturesHeader, out var required)) return;
        foreach (var feature in required.ToString().Split(','))
            if (!Features.Contains(feature.Trim(), StringComparer.Ordinal))
                throw new DesignerContractException("ContractFeatureUnavailable", "打印服务未提供请求所需的契约特性。", RequiredFeaturesHeader);
    }
}

internal sealed class DesignerContractException(string code, string message, string header) : Exception(message)
{
    internal string Code { get; } = code;
    internal string FieldPath { get; } = "headers." + header;
}

internal sealed class DesignerOptionException(string code, string message, string fieldPath) : ArgumentException(message)
{
    internal string Code { get; } = code;
    internal string FieldPath { get; } = fieldPath;
}

internal sealed class DesignerTemplateException(Exception cause) : Exception(cause.Message, cause);
