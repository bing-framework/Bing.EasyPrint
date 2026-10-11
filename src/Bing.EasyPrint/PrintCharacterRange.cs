using System;
using System.Collections.Generic;

namespace Bing.EasyPrint;

/// <summary>声明设备字库覆盖的 Unicode 标量闭区间；不包含 UTF-16 代理项。</summary>
public sealed class PrintCharacterRange
{
    /// <summary>区间首个 Unicode 标量。</summary>
    public int First { get; }
    /// <summary>区间最后一个 Unicode 标量。</summary>
    public int Last { get; }

    /// <summary>创建由设备手册或实测结果确认的字符覆盖区间。</summary>
    public PrintCharacterRange(int first, int last)
    {
        if (first < 0 || last < first || last > 0x10FFFF || first <= 0xDFFF && last >= 0xD800)
            throw new ArgumentOutOfRangeException(nameof(first), "字库区间必须由合法 Unicode 标量组成，不能包含代理项。");
        First = first;
        Last = last;
    }

    /// <summary>判断标量是否在声明区间内。</summary>
    public bool Contains(int scalar) => scalar >= First && scalar <= Last;
}

internal static class PrintUnicodeScalars
{
    internal static IEnumerable<int> Enumerate(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        for (var index = 0; index < text.Length; index++)
        {
            var first = text[index];
            if (char.IsHighSurrogate(first))
            {
                if (index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]))
                    throw new ArgumentException("文字包含孤立的 UTF-16 代理项。", nameof(text));
                yield return char.ConvertToUtf32(first, text[++index]);
            }
            else if (char.IsLowSurrogate(first))
                throw new ArgumentException("文字包含孤立的 UTF-16 代理项。", nameof(text));
            else yield return first;
        }
    }
}
