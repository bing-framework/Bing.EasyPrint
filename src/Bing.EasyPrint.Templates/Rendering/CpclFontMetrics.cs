using System;
using System.Globalization;
using System.Text;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 度量内置 CPCL 点阵字库的文本宽度。
/// </summary>
internal readonly struct CpclFontMetrics
{
    /// <summary>
    /// 获取所选字库的半角字符打印点宽度。
    /// </summary>
    private readonly int halfWidth;
    /// <summary>
    /// 获取非 Unicode 字距计算使用的编码。
    /// </summary>
    private readonly Encoding encoding;
    /// <summary>
    /// 获取是否按 Unicode 字符宽度度量。
    /// </summary>
    private readonly bool unicode;

    /// <summary>
    /// 初始化一个 <see cref="CpclFontMetrics"/> 类型的实例。
    /// </summary>
    /// <param name="size">内置字体大小。</param>
    /// <param name="font">内置字体编号。</param>
    /// <param name="encoding">文本输出编码。</param>
    internal CpclFontMetrics(int size, int font, Encoding encoding)
    {
        if (!Enum.IsDefined(typeof(FontSize), size)) throw new ArgumentException("不支持的字体大小。");
        var naturalFont = size % 24 == 0 ? 24 : 55;
        if (font != 0 && font != naturalFont) throw new ArgumentException("FontName 必须与内置字号对应（24 或 55）。");
        var scale = size / (naturalFont == 24 ? 24 : 16);
        halfWidth = (naturalFont == 24 ? 12 : 8) * scale;
        this.encoding = encoding;
        unicode = encoding.CodePage == 65001 || encoding.CodePage == 1200 || encoding.CodePage == 1201 ||
            encoding.CodePage == 12000 || encoding.CodePage == 12001;
    }

    /// <summary>
    /// 计算一个 Unicode 文本元素的字距。
    /// </summary>
    /// <param name="element">单个文本元素。</param>
    /// <returns>该元素占用的打印点数。</returns>
    internal int Advance(string element)
    {
        var cells = 0;
        var joined = element.IndexOf('\u200d') >= 0;
        for (var index = 0; index < element.Length;)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(element, index);
            var scalar = char.ConvertToUtf32(element, index);
            var length = scalar > 0xffff ? 2 : 1;
            if (category != UnicodeCategory.NonSpacingMark && category != UnicodeCategory.EnclosingMark &&
                category != UnicodeCategory.Format && !(scalar >= 0x1f3fb && scalar <= 0x1f3ff))
            {
                // 非 Unicode 代码页按实际单/双字节字距，Unicode 按半/全角字距。
                var width = scalar <= 0x7f ? 1 : unicode ? (IsWide(scalar) ? 2 : 1) :
                    encoding.GetByteCount(index == 0 && length == element.Length ? element : element.Substring(index, length)) > 1 ? 2 : 1;
                cells = joined ? Math.Max(cells, width) : checked(cells + width);
            }
            index += length;
        }
        return checked(cells * halfWidth);
    }

    /// <summary>
    /// 计算完整文本的打印宽度。
    /// </summary>
    /// <param name="text">待度量文本。</param>
    /// <returns>文本宽度，单位为打印点。</returns>
    internal long Measure(string text)
    {
        long width = 0;
        var iterator = StringInfo.GetTextElementEnumerator(text);
        while (iterator.MoveNext()) width += Advance(iterator.GetTextElement());
        return width;
    }

    // CJK、全角形式及补充平面的宽字符。半角片假名不属于 FF01–FF60。
    /// <summary>
    /// 判断 Unicode 标量是否按全角字距处理。
    /// </summary>
    /// <param name="value">Unicode 标量值。</param>
    /// <returns>全角返回 true，否则返回 false。</returns>
    private static bool IsWide(int value) => value >= 0x1100 && (
        value <= 0x115f || value == 0x2329 || value == 0x232a ||
        value >= 0x2e80 && value <= 0xa4cf && value != 0x303f ||
        value >= 0xac00 && value <= 0xd7a3 || value >= 0xf900 && value <= 0xfaff ||
        value >= 0xfe10 && value <= 0xfe19 || value >= 0xfe30 && value <= 0xfe6f ||
        value >= 0xff01 && value <= 0xff60 || value >= 0xffe0 && value <= 0xffe6 ||
        value >= 0x1f000 && value <= 0x1faff || value >= 0x20000 && value <= 0x3fffd);
}
