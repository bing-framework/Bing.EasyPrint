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
    /// 获取共享的半角/全角字符单元分类器。
    /// </summary>
    private readonly TextCellMetrics cells;
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
        cells = new TextCellMetrics(encoding);
    }

    /// <summary>
    /// 计算一个 Unicode 文本元素的字距。
    /// </summary>
    /// <param name="element">单个文本元素。</param>
    /// <returns>该元素占用的打印点数。</returns>
    internal int Advance(string element)
    {
        return checked(cells.Advance(element) * halfWidth);
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

}
