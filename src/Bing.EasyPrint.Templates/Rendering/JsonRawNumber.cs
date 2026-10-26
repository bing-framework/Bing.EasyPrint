using System.Globalization;

namespace Bing.EasyPrint.Templates;

/// <summary>保留 JSON 数字的原始词法，只在需要数字格式时转换为 decimal。</summary>
internal sealed class JsonRawNumber
{
    internal JsonRawNumber(string raw) => Raw = raw;

    /// <summary>获取原始 JSON 数字词法。</summary>
    internal string Raw { get; }

    /// <summary>按 JSON 数字语义转换为 decimal；溢出由调用方转换为字段诊断。</summary>
    internal decimal ToDecimal()
        => decimal.Parse(Raw, NumberStyles.Float, CultureInfo.InvariantCulture);

    public override string ToString() => Raw;
}
