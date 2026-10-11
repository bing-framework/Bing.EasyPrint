using System;
using System.Globalization;
using System.Text;

namespace Bing.EasyPrint.Templates;

/// <summary>按输出编码计算文本元素占用的半角字符单元数。</summary>
internal readonly struct TextCellMetrics
{
    private readonly Encoding _encoding;
    private readonly bool _unicode;

    internal TextCellMetrics(Encoding encoding)
    {
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        _unicode = encoding.CodePage == 65001 || encoding.CodePage == 1200 || encoding.CodePage == 1201 ||
            encoding.CodePage == 12000 || encoding.CodePage == 12001;
    }

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
                var width = scalar <= 0x7f ? 1 : _unicode ? (IsWide(scalar) ? 2 : 1) :
                    _encoding.GetByteCount(index == 0 && length == element.Length ? element : element.Substring(index, length)) > 1 ? 2 : 1;
                cells = joined ? Math.Max(cells, width) : checked(cells + width);
            }
            index += length;
        }
        return cells;
    }

    // CJK、全角形式及补充平面的宽字符。半角片假名不属于 FF01-FF60。
    private static bool IsWide(int value) => value >= 0x1100 && (
        value <= 0x115f || value == 0x2329 || value == 0x232a ||
        value >= 0x2e80 && value <= 0xa4cf && value != 0x303f ||
        value >= 0xac00 && value <= 0xd7a3 || value >= 0xf900 && value <= 0xfaff ||
        value >= 0xfe10 && value <= 0xfe19 || value >= 0xfe30 && value <= 0xfe6f ||
        value >= 0xff01 && value <= 0xff60 || value >= 0xffe0 && value <= 0xffe6 ||
        value >= 0x1f000 && value <= 0x1faff || value >= 0x20000 && value <= 0x3fffd);
}
