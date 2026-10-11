using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;

/// <summary>按异常类型映射稳定 HTTP 诊断，不从本地化文案推断错误类别。</summary>
internal sealed record DesignerError(string Code, string Phase, string? FieldPath, int StatusCode, string Message)
{
    internal static T? Find<T>(Exception error) where T : Exception
    {
        for (var current = error; current != null; current = current.InnerException)
            if (current is T match) return match;
        return null;
    }

    internal static DesignerError Describe(Exception error)
    {
        if (Find<PrintResourceLimitException>(error) != null)
            return new("ResourceLimitExceeded", "Resource", null, 413, error.Message);
        if (error is DesignerContractException contract)
            return new(contract.Code, "Contract", contract.FieldPath, 409, error.Message);
        if (error is DesignerAdmissionRejectedException)
            return new("ResourceAdmissionRejected", "Admission", null, 503, error.Message);
        if (error is BadHttpRequestException request)
            return new(request.StatusCode switch
            {
                413 => "RequestTooLarge", 415 => "UnsupportedMediaType", _ => "InvalidRequest"
            }, "Request", null, request.StatusCode, error.Message);
        if (error is System.Text.Json.JsonException json)
            return new("RequestJsonInvalid", "Request", json.Path, 400,
                "请求 JSON 格式无效、字段类型错误或包含不支持的配置字段。");
        if (error is DecoderFallbackException)
            return new("InvalidRequestEncoding", "Request", null, 400, "请求包含声明编码下的非法字节。");
        if (error is DesignerOptionException option)
            return new(option.Code, "Options", option.FieldPath, 400, error.Message);
        if (Find<SixLabors.ImageSharp.ImageFormatException>(error) != null ||
            Find<TemplateRenderException>(error)?.ResourceName != null)
            return new("InvalidImageResource", "Resource", null, 400, "图片资源格式无效或内容损坏。");
        if (error is DesignerTemplateException)
            return new("TemplateInvalid", "Template", null, 400,
                Find<Newtonsoft.Json.JsonException>(error) != null
                    ? "模板 JSON 格式无效或包含不支持的字段。" : error.Message);
        if (error is TemplateRenderException render)
        {
            if (render.FieldPath != null)
                return new("BindingFailed", "Binding", render.FieldPath, 400, error.Message);
            if (Find<PrintCapabilityException>(error) != null || Find<NotSupportedException>(error) != null)
                return new("UnsupportedCapability", "Render", null, 400, error.Message);
            return new("TemplateInvalid", "Render", null, 400, error.Message);
        }
        if (error is Newtonsoft.Json.JsonException)
            return new("TemplateInvalid", "Template", error switch
            {
                Newtonsoft.Json.JsonReaderException reader => reader.Path,
                Newtonsoft.Json.JsonSerializationException serialization => serialization.Path,
                _ => null
            }, 400, "模板 JSON 格式无效或包含不支持的字段。");
        if (error is PrintCapabilityException || error is NotSupportedException)
            return new("UnsupportedCapability", "Render", null, 400, error.Message);
        if (error is ArgumentException argument)
            return new("InvalidOptions", "Options", argument.ParamName, 400, error.Message);
        // 服务器内部错误不应伪装成客户端参数问题，也不暴露内部对象或文件路径。
        return new("InternalError", "Server", null, 500, "打印服务处理失败。");
    }
}
