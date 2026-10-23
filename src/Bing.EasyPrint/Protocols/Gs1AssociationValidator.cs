using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.EasyPrint.Protocols;

/// <summary>
/// 对 GS1 元素串执行固定版本字典的 <c>req</c>/<c>ex</c> 关联校验。
/// </summary>
/// <remarks>
/// 逐字段语法由现有 GS1 解析器负责；本类型只处理完整数据集合中的 AI 关联。
/// 默认渲染路径不自动启用本校验；调用方必须显式调用或通过模板/Host 的严格关联选项启用。
/// 不要求、也不会把一个页面上的多个独立条码自动合并为同一物理对象。
/// </remarks>
public static class Gs1AssociationValidator
{
    /// <summary>
    /// 校验单个 GS1 载体的括号式或规范化 AI 元素串。
    /// </summary>
    /// <param name="elementString">括号式 AI 输入，或包含 ASCII 29 分隔符的规范化元素串。</param>
    /// <exception cref="ArgumentNullException"><paramref name="elementString"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">逐字段语法或 AI 关联校验失败。</exception>
    public static void Validate(string elementString)
    {
        if (elementString == null) throw new ArgumentNullException(nameof(elementString));
        ValidateElements(Gs1ElementStringParser.Parse(elementString, nameof(elementString)));
    }

    /// <summary>
    /// 校验一个物理对象所对应的多个 GS1 载体的合并数据集合。
    /// </summary>
    /// <param name="carrierElementStrings">各载体的括号式或规范化 AI 元素串；不会从页面自动发现或合并。</param>
    /// <exception cref="ArgumentNullException"><paramref name="carrierElementStrings"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">任一载体语法或合并后的 AI 关联校验失败。</exception>
    public static void ValidateCarriers(IEnumerable<string> carrierElementStrings)
    {
        if (carrierElementStrings == null) throw new ArgumentNullException(nameof(carrierElementStrings));
        var elements = new List<Gs1Element>();
        var index = 0;
        foreach (var carrier in carrierElementStrings)
        {
            if (carrier == null) throw new ArgumentNullException(nameof(carrierElementStrings),
                $"第 {index + 1} 个 GS1 载体不能为 null。");
            elements.AddRange(Gs1ElementStringParser.Parse(carrier, $"carrierElementStrings[{index}]"));
            index++;
        }
        if (elements.Count == 0) throw new ArgumentException("GS1 关联校验至少需要一个 AI 元素。",
            nameof(carrierElementStrings));
        ValidateElements(elements);
    }

    internal static void ValidateBarcodeData(string data, BarcodeType type)
    {
        if (type == BarcodeType.UccEan128 || type == BarcodeType.Gs1DataBarExpanded ||
            type == BarcodeType.Gs1DataBarExpandedStacked)
        {
            ValidateElements(Gs1ElementStringParser.Parse(data, nameof(data)));
            return;
        }
        if (type == BarcodeType.Ean8 || type == BarcodeType.Ean13 || type == BarcodeType.UpcA ||
            type == BarcodeType.UpcE || type == BarcodeType.Gs1DataBarOmnidirectional ||
            type == BarcodeType.Gs1DataBarTruncated || type == BarcodeType.Gs1DataBarLimited ||
            type == BarcodeType.Gs1DataBarStacked || type == BarcodeType.Gs1DataBarStackedOmnidirectional)
            ValidateElements(new[] { new Gs1Element("01", data) });
    }

    internal static void ValidateComposite(string primary, string secondary, BarcodeType linearType)
    {
        var elements = new List<Gs1Element>();
        if (linearType == BarcodeType.Ean8 || linearType == BarcodeType.Ean13 || linearType == BarcodeType.UpcA ||
            linearType == BarcodeType.UpcE || linearType == BarcodeType.Gs1DataBarOmnidirectional ||
            linearType == BarcodeType.Gs1DataBarTruncated || linearType == BarcodeType.Gs1DataBarLimited ||
            linearType == BarcodeType.Gs1DataBarStacked || linearType == BarcodeType.Gs1DataBarStackedOmnidirectional)
        {
            var gtin = primary.Split('+')[0];
            elements.Add(new Gs1Element("01", gtin));
        }
        else
        {
            elements.AddRange(Gs1ElementStringParser.Parse(primary, nameof(primary)));
        }
        elements.AddRange(Gs1ElementStringParser.Parse(secondary, nameof(secondary)));
        ValidateElements(elements);
    }
    internal static void ValidateElements(IReadOnlyList<Gs1Element> elements)
    {
        if (elements == null) throw new ArgumentNullException(nameof(elements));
        if (elements.Count == 0) throw new ArgumentException("GS1 关联校验至少需要一个 AI 元素。", nameof(elements));

        foreach (var element in elements)
        {
            if (!Gs1SyntaxDictionary.TryGet(element.Ai, out var definition))
                throw new ArgumentException($"GS1 AI {element.Ai} 不在固定版本 {Gs1SyntaxDictionary.DictionaryCommit} 的字典中。");
            foreach (var rule in definition.RequirementRules)
            {
                if (rule.Matches(elements, null)) continue;
                var missing = rule.MissingPatterns(elements, null);
                var actual = string.Join(",", elements.Select(item => item.Ai).Distinct(StringComparer.Ordinal));
                var need = missing.Count == 0 ? rule.Raw : string.Join("+", missing);
                throw new ArgumentException(
                    $"GS1 AI {element.Ai} 缺少必需关联 {need}（req={rule.Raw}；实际 AI：{actual}）。");
            }

            foreach (var rule in definition.ExclusionRules)
            {
                if (!rule.Matches(elements, element.Ai)) continue;
                var conflict = elements.First(item =>
                    !string.Equals(item.Ai, element.Ai, StringComparison.Ordinal) &&
                    rule.Alternatives.Any(alternative => alternative.All(pattern =>
                        Gs1AssociationPattern.Matches(pattern, item.Ai))));
                throw new ArgumentException(
                    $"GS1 AI {element.Ai} 与 AI {conflict.Ai} 互斥（ex={rule.Raw}）。");
            }
        }
    }
}

internal static class Gs1ElementStringParser
{
    internal static IReadOnlyList<Gs1Element> Parse(string data, string name)
    {
        if (string.IsNullOrWhiteSpace(data))
            throw new ArgumentException("GS1 AI 元素串不能为空。", name);
        return Gs1ApplicationIdentifierParser.IsBracketed(data)
            ? Gs1ApplicationIdentifierParser.ParseElements(data, name)
            : ParseNormalized(data, name);
    }

    /// <summary>
    /// 将已解析的元素重建为带必要 ASCII 29 分隔符的规范化串。
    /// </summary>
    internal static string Rebuild(IReadOnlyList<Gs1Element> elements)
    {
        var normalized = new System.Text.StringBuilder();
        for (var index = 0; index < elements.Count; index++)
        {
            var element = elements[index];
            if (!Gs1SyntaxDictionary.TryGet(element.Ai, out var definition))
                throw new ArgumentException($"GS1 AI {element.Ai} 不在固定版本 {Gs1SyntaxDictionary.DictionaryCommit} 的字典中。");
            normalized.Append(element.Ai).Append(element.Value);
            if (index + 1 < elements.Count && definition.RequiresSeparator) normalized.Append('\u001D');
        }
        return normalized.ToString();
    }

    private static IReadOnlyList<Gs1Element> ParseNormalized(string data, string name)
    {
        var result = new List<Gs1Element>();
        var index = 0;
        while (index < data.Length)
        {
            if (data[index] == '\u001D')
                throw new ArgumentException($"GS1 规范化元素串在位置 {index} 处包含多余或缺失 AI 的 ASCII 29 分隔符。", name);

            var ai = ReadAi(data, index, name);
            if (!Gs1SyntaxDictionary.TryGet(ai, out var definition))
                throw new ArgumentException($"GS1 AI {ai} 不在固定版本 {Gs1SyntaxDictionary.DictionaryCommit} 的字典中。", name);
            index += ai.Length;

            var next = data.IndexOf('\u001D', index);
            string value;
            if (definition.RequiresSeparator)
            {
                var end = next < 0 ? data.Length : next;
                if (end == index)
                    throw new ArgumentException($"GS1 AI {ai} 在规范化元素串中数据为空。", name);
                value = data.Substring(index, end - index);
                index = next < 0 ? data.Length : next + 1;
                if (next >= 0 && index == data.Length)
                    throw new ArgumentException("GS1 规范化元素串不能以 ASCII 29 分隔符结尾。", name);
            }
            else
            {
                var end = checked(index + definition.MinimumLength);
                if (end > data.Length)
                    throw new ArgumentException($"GS1 AI {ai} 在规范化元素串中数据不足。", name);
                value = data.Substring(index, end - index);
                index = end;
                if (index < data.Length && data[index] == '\u001D')
                    throw new ArgumentException($"GS1 AI {ai} 为预定义长度，后面不应出现 ASCII 29 分隔符。", name);
            }

            definition.Validate(value, name);
            result.Add(new Gs1Element(ai, value));
        }
        if (result.Count == 0) throw new ArgumentException("GS1 AI 元素串不能为空。", name);
        return result;
    }

    private static string ReadAi(string data, int index, string name)
    {
        for (var length = 4; length >= 2; length--)
        {
            if (index + length > data.Length) continue;
            var candidate = data.Substring(index, length);
            if (candidate.Any(character => character < '0' || character > '9')) continue;
            if (Gs1SyntaxDictionary.TryGet(candidate, out _)) return candidate;
        }
        throw new ArgumentException($"GS1 规范化元素串在位置 {index} 处包含未知 AI。", name);
    }
}
