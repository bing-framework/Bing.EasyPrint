namespace Bing.EasyPrint.Templates;

/// <summary>
/// 同时保存旧兼容语义数据和保留 JSON 十进制/日期原文的数据。
/// </summary>
internal sealed class TemplateDataPair
{
    internal TemplateDataPair(object legacy, object raw)
    {
        Legacy = legacy;
        Raw = raw;
    }

    /// <summary>获取按旧 JSON 日期/浮点语义解析的数据。</summary>
    internal object Legacy { get; }

    /// <summary>获取保留 decimal 和 ISO offset 原文的数据。</summary>
    internal object Raw { get; }
}
