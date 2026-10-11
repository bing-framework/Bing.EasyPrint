using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Bing.EasyPrint.Templates;

/// <summary>目标设备的单个字体在 24 点设置下实测的文字占位，单位为打印点。</summary>
public sealed class DeviceFontCalibration
{
    /// <summary>半角字符占位宽度。</summary>
    public double HalfCellWidthAt24 { get; set; }
    /// <summary>全角字符占位宽度。</summary>
    public double FullCellWidthAt24 { get; set; }
    /// <summary>单行文字占位高度。</summary>
    public double LineHeightAt24 { get; set; }
    /// <summary>按 Unicode 文本单元精确校准字距和墨迹边界。</summary>
    public Dictionary<string, DeviceTextElementCalibration> TextElements { get; set; }

    internal void Validate(string fontName)
    {
        if (!Positive(HalfCellWidthAt24) || !Positive(FullCellWidthAt24) || !Positive(LineHeightAt24))
            throw new ArgumentException($"设备字体 {fontName} 的半角宽度、全角宽度和行高都必须为有限正数。",
                nameof(DeviceFontCalibration));
        if (TextElements == null) return;
        foreach (var entry in TextElements)
        {
            if (string.IsNullOrEmpty(entry.Key) || entry.Key.Any(char.IsControl) ||
                StringInfo.ParseCombiningCharacters(entry.Key).Length != 1 || entry.Value == null)
                throw new ArgumentException($"设备字体 {fontName} 的文本单元校准键或值无效。",
                    nameof(TextElements));
            entry.Value.Validate(fontName, entry.Key);
        }
    }

    private static bool Positive(double value) => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
}
