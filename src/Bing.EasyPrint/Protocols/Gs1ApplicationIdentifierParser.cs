using System;
using System.Text;

namespace Bing.EasyPrint.Protocols;

/// <summary>
/// GS1 括号 AI 元素串解析器。
/// </summary>
/// <remarks>
/// AI 集合、字段长度、字符集、固定/变长及 FNC1 边界来自固定版本
/// <see cref="Gs1SyntaxDictionary"/>（GS1 Barcode Syntax Dictionary 提交
/// d1702ffb2fa5060f8460f7d581b01e377deaa1d6）；未知 AI、越界字段、无效校验位和
/// 不完整字段会严格失败，不会猜测字段边界。字段值中的字面左括号使用 \( 表示；
/// 反斜杠只作输入转义，不属于输出数据。跨 AI 的 req/ex 关联、具体码制容量和
/// 业务语义由调用方或对应编码器处理，不属于通用 AI 语法校验。
/// </remarks>
internal static class Gs1ApplicationIdentifierParser
{
    private const char GroupSeparator = '\u001D';

    internal static bool IsBracketed(string data)
        => data != null && data.Length > 0 && (data[0] == '(' || data[0] == '[');

    /// <summary>
    /// 判断输入是否需要严格解析而非沿用单元素括号兼容行为。
    /// </summary>
    /// <param name="data">待检查的括号 AI 输入。</param>
    /// <returns>包含多个 AI 或反斜杠字符时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    internal static bool ShouldNormalize(string data)
    {
        if (!IsBracketed(data)) return false;
        if (data.IndexOf('\\') >= 0) return true;
        var closing = data[0] == '(' ? ')' : ']';
        var firstEnd = data.IndexOf(closing, 1);
        if (firstEnd > 0 && IsExtendedIdentifierAi(data.Substring(1, firstEnd - 1)))
            return true;
        return firstEnd >= 0 && FindNextElement(data, firstEnd + 1) >= 0;
    }

    /// <summary>
    /// 将括号 AI 展示文本中的左括号转义还原为数据字符。
    /// </summary>
    /// <param name="data">经过严格解析验证的括号 AI 文本。</param>
    /// <returns>移除字面左括号转义符后的文本；输入为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
    internal static string DecodeDisplayEscapes(string data)
        => data?.Replace("\\(", "(");

    internal static bool IsExpandedGenericGtin(string data)
    {
        if (!IsBracketed(data) ||
            (!data.StartsWith("(01)", StringComparison.Ordinal) &&
             !data.StartsWith("[01]", StringComparison.Ordinal)))
            return false;

        var firstClosing = data[0] == '(' ? ')' : ']';
        var firstEnd = data.IndexOf(firstClosing, 1);
        if (firstEnd < 0)
            return false;
        var nextElement = FindNextElement(data, firstEnd + 1);
        if (nextElement < 0)
            return false;
        var nextClosing = data[nextElement] == '(' ? ')' : ']';
        var nextEnd = data.IndexOf(nextClosing, nextElement + 1);
        if (nextEnd < 0)
            return false;
        var ai = data.Substring(nextElement + 1, nextEnd - nextElement - 1);
        return !IsExpandedCompressedAi(ai);
    }

    /// <summary>
    /// 校验受支持的括号 AI 元素串并生成原始 AI/FNC1 数据。
    /// </summary>
    /// <param name="data">统一括号样式的 AI 元素串。</param>
    /// <param name="name">异常中的参数名称。</param>
    /// <returns>去除 AI 展示括号并按需插入 FNC1 后的数据。</returns>
    /// <exception cref="ArgumentException">AI 子集、字段值或括号结构不符合当前规则时抛出。</exception>
    internal static string Normalize(string data, string name)
        => Normalize(data, name, false);

    /// <summary>
    /// 按当前已声明的 AI 子集规范化协议输入。
    /// </summary>
    /// <param name="data">统一括号样式的 AI 元素串。</param>
    /// <param name="name">异常中的参数名称。</param>
    /// <param name="maximumLength">规范化结果允许的最大长度。</param>
    /// <returns>去除 AI 展示括号并按需插入 FNC1 后的数据。</returns>
    /// <exception cref="ArgumentOutOfRangeException">最大长度小于 3 时抛出。</exception>
    /// <exception cref="ArgumentException">AI、字段值或规范化长度不符合当前子集时抛出。</exception>
    /// <remarks>
    /// Raster Expanded 的符号上限仍由 <see cref="Normalize(string, string)"/> 保持为 77；
    /// 原生 GS1-128/DataBar 固件可接受更长 payload 时，协议层使用此入口只放宽总长度，
    /// 不放宽 AI 字典、字符集或字段长度校验。
    /// </remarks>
    internal static string NormalizeForProtocol(string data, string name, int maximumLength)
    {
        if (maximumLength < 3)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        return Normalize(data, name, false, maximumLength);
    }

    /// <summary>
    /// 规范化 GS1 日期字段后的括号 AI 尾部。
    /// </summary>
    /// <param name="data">日期后的括号 AI 元素串。</param>
    /// <param name="name">异常中的参数名称。</param>
    /// <returns>去除展示括号并按需插入 FNC1 后的数据。</returns>
    /// <exception cref="ArgumentException">AI 子集、字段值或括号结构不符合当前规则时抛出。</exception>
    internal static string NormalizeDateTail(string data, string name)
        => Normalize(data, name, true);

    /// <summary>
    /// 使用默认协议长度上限规范化括号 AI 元素串。
    /// </summary>
    /// <param name="data">括号 AI 元素串。</param>
    /// <param name="name">异常中的参数名称。</param>
    /// <param name="rejectDateIdentifiers">是否拒绝日期类型的应用标识。</param>
    /// <returns>去除展示括号并按需插入 FNC1 后的数据。</returns>
    /// <exception cref="ArgumentException">AI 子集或字段值不符合当前规则时抛出。</exception>
    private static string Normalize(string data, string name, bool rejectDateIdentifiers)
        => Normalize(data, name, rejectDateIdentifiers, 77);

    /// <summary>
    /// 解析括号 AI 元素、校验字段，并生成原始 AI/FNC1 数据。
    /// </summary>
    /// <param name="data">统一括号样式的 AI 元素串。</param>
    /// <param name="name">异常中的参数名称。</param>
    /// <param name="rejectDateIdentifiers">是否拒绝日期类型的应用标识。</param>
    /// <param name="maximumLength">规范化结果允许的最大长度。</param>
    /// <returns>原始 AI 数据及必要的 FNC1 分隔符。</returns>
    /// <exception cref="ArgumentException">AI、字段值或规范化长度不符合当前规则时抛出。</exception>
    private static string Normalize(string data, string name, bool rejectDateIdentifiers, int maximumLength)
    {
        if (!IsBracketed(data))
            throw new ArgumentException("GS1 括号 AI 元素串必须以 (AI) 或 [AI] 开头。", name);

        var normalized = new StringBuilder(data.Length);
        var index = 0;
        var elementCount = 0;
        var hasSscc = false;
        var hasTransportInformation = false;
        var hasGlnExtension = false;
        var hasPhysicalLocationGln = false;
        var hasShipToAddressLine1 = false;
        var hasShipToAddressLine2 = false;
        var hasReturnAddressLine1 = false;
        var hasReturnAddressLine2 = false;
        char? bracketStyle = null;
        while (index < data.Length)
        {
            var opening = data[index];
            var closing = opening == '(' ? ')' : opening == '[' ? ']' : '\0';
            if (closing == '\0')
                throw new ArgumentException("GS1 AI 元素串中的 AI 必须使用匹配的圆括号或方括号。", name);
            if (bracketStyle.HasValue && bracketStyle.Value != opening)
                throw new ArgumentException("GS1 AI 元素串不能混用圆括号和方括号。", name);
            bracketStyle = opening;

            var aiEnd = data.IndexOf(closing, index + 1);
            if (aiEnd < 0)
                throw new ArgumentException("GS1 AI 元素串包含未闭合的 AI。", name);

            var ai = data.Substring(index + 1, aiEnd - index - 1);
            if (!TryGetDefinition(ai, out var definition))
                throw new ArgumentException($"GS1 AI {ai} 不在当前支持的元数据子集中。", name);
            if (rejectDateIdentifiers && IsDateApplicationIdentifier(ai))
                throw new ArgumentException(
                    $"GS1 日期后的括号尾部不支持额外日期 AI {ai}，请使用已编码的原始通用字段。", name);

            var valueStart = aiEnd + 1;
            var nextElement = FindNextElement(data, valueStart);
            var valueEnd = nextElement < 0 ? data.Length : nextElement;
            var value = DecodeValue(ai, data.Substring(valueStart, valueEnd - valueStart), name);
            ValidateValue(ai, value, definition, name);

            hasSscc |= ai == "00";
            hasTransportInformation |= IsScan4Transport(ai);
            hasGlnExtension |= ai == "254";
            hasPhysicalLocationGln |= ai == "414";
            hasShipToAddressLine1 |= ai == "4302";
            hasShipToAddressLine2 |= ai == "4303";
            hasReturnAddressLine1 |= ai == "4312";
            hasReturnAddressLine2 |= ai == "4313";

            normalized.Append(ai).Append(value);
            if (definition.RequiresSeparator && nextElement >= 0)
                normalized.Append(GroupSeparator);

            elementCount++;
            index = nextElement < 0 ? data.Length : nextElement;
        }

        if (elementCount == 0)
            throw new ArgumentException("GS1 AI 元素串不能为空。", name);
        if (hasTransportInformation && !hasSscc)
            throw new ArgumentException("GS1 AI 4300–4326 必须与 AI 00（SSCC）一起使用。", name);
        if (hasGlnExtension && !hasPhysicalLocationGln)
            throw new ArgumentException("GS1 AI 254（GLN 扩展组件）必须与 AI 414（物理位置 GLN）一起使用。", name);
        if (hasShipToAddressLine2 && !hasShipToAddressLine1)
            throw new ArgumentException("GS1 AI 4303 地址第二行必须与 AI 4302 第一行一起使用。", name);
        if (hasReturnAddressLine2 && !hasReturnAddressLine1)
            throw new ArgumentException("GS1 AI 4313 回运地址第二行必须与 AI 4312 第一行一起使用。", name);
        if (normalized.Length < 3 || normalized.Length > maximumLength)
            throw new ArgumentException(
                $"GS1 AI 规范化后长度必须为 3–{maximumLength} 个字符。", name);
        return normalized.ToString();
    }

    /// <summary>
    /// 查找下一个未转义的括号 AI 起点。
    /// </summary>
    /// <param name="data">待扫描的元素串。</param>
    /// <param name="start">开始扫描的位置。</param>
    /// <returns>下一个 AI 起点的索引；不存在时返回 -1。</returns>
    private static int FindNextElement(string data, int start)
    {
        for (var index = start; index < data.Length; index++)
        {
            if (data[index] == '\\' && index + 1 < data.Length && data[index + 1] == '(')
            {
                index++;
                continue;
            }
            if (data[index] == '(' || data[index] == '[')
                return index;
        }
        return -1;
    }

    /// <summary>
    /// 解码 AI 值中用于表示字面左括号的转义序列。
    /// </summary>
    /// <param name="ai">当前应用标识。</param>
    /// <param name="value">当前元素的编码值。</param>
    /// <param name="name">异常中的参数名称。</param>
    /// <returns>替换转义序列后的值。</returns>
    /// <exception cref="ArgumentException">值包含不支持的反斜杠转义时抛出。</exception>
    private static string DecodeValue(string ai, string value, string name)
    {
        if (value.IndexOf('\\') < 0)
            return value;

        var decoded = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\')
            {
                decoded.Append(value[index]);
                continue;
            }

            if (index + 1 >= value.Length || value[index + 1] != '(')
                throw new ArgumentException(
                    $"GS1 AI {ai} 的反斜杠只可用于转义字面左括号 \\(。", name);
            decoded.Append('(');
            index++;
        }

        return decoded.ToString();
    }

    private static bool IsExpandedCompressedAi(string ai)
        => ai.Length == 4 && ai[3] >= '0' && ai[3] <= '9' &&
           (ai.StartsWith("310", StringComparison.Ordinal) ||
            ai.StartsWith("320", StringComparison.Ordinal) ||
            ai.StartsWith("392", StringComparison.Ordinal) ||
            ai.StartsWith("393", StringComparison.Ordinal));

    private static bool IsExtendedIdentifierAi(string ai)
        => ai == "253" || ai == "254" || ai == "255";

    private static void ValidateValue(string ai, string value, Gs1SyntaxAiDefinition definition, string name)
        => Gs1SyntaxDictionary.Validate(ai, value, name);

    private static bool TryGetDefinition(string ai, out Gs1SyntaxAiDefinition definition)
        => Gs1SyntaxDictionary.TryGet(ai, out definition);

    private static bool IsScan4Transport(string ai)
        => ai.Length == 4 && ai[0] == '4' && ai[1] == '3' &&
           (ai[2] == '0' || ai[2] == '1' ||
            ai[2] == '2' && ai[3] >= '0' && ai[3] <= '6');

    private static bool IsDateApplicationIdentifier(string ai)
        => ai == "11" || ai == "12" || ai == "13" || ai == "15" ||
           ai == "16" || ai == "17";
}
