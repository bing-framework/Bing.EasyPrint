using System;

namespace Bing.EasyPrint.Protocols;

/// <summary>TSPL 内置字体的点阵尺寸与 TEXT 放大倍率。</summary>
public static class TsplFontSizing
{
    /// <summary>取得固定字体的基础单元尺寸；字体 0 为可缩放字体。</summary>
    public static (int Width, int Height) FixedCell(string font)
    {
        switch (font)
        {
            case "1": return (8, 12);
            case "2": return (12, 20);
            case "3": return (16, 24);
            case "4": return (24, 32);
            case "5": return (32, 48);
            case "6": return (14, 19);
            case "7": return (21, 27);
            case "8": return (14, 25);
            default: throw new ArgumentException("TSPL 固定字体标识必须为 1 至 8。", nameof(font));
        }
    }

    /// <summary>将期望的打印点高度转换为固定字体的整数放大倍率。</summary>
    public static int FixedScale(string font, int height)
    {
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        var cell = FixedCell(font);
        var scale = Math.Max(1, (int)Math.Round((double)height / cell.Height,
            MidpointRounding.AwayFromZero));
        if (scale > 10) throw new ArgumentOutOfRangeException(nameof(height), "TSPL 固定字体放大倍率不能超过 10。");
        return scale;
    }
}
