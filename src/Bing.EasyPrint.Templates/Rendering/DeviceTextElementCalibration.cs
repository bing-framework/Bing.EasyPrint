using System;

namespace Bing.EasyPrint.Templates;

/// <summary>目标设备字体在 24 点字号下的单个 Unicode 文本单元校准值。</summary>
public sealed class DeviceTextElementCalibration
{
    /// <summary>文本单元在 24 点字号下的前进宽度；零表示组合单元不增加字距。</summary>
    public double AdvanceAt24 { get; set; }
    /// <summary>可选墨迹矩形相对设备文字命令原点的左边界。</summary>
    public double? InkLeftAt24 { get; set; }
    /// <summary>可选墨迹矩形相对设备文字命令原点的上边界。</summary>
    public double? InkTopAt24 { get; set; }
    /// <summary>可选墨迹矩形相对设备文字命令原点的右边界。</summary>
    public double? InkRightAt24 { get; set; }
    /// <summary>可选墨迹矩形相对设备文字命令原点的下边界。</summary>
    public double? InkBottomAt24 { get; set; }

    internal void Validate(string fontName, string textElement)
    {
        if (!NonNegative(AdvanceAt24))
            throw new ArgumentException($"设备字体 {fontName} 的文本单元“{textElement}”前进宽度必须为有限非负数。",
                nameof(DeviceTextElementCalibration));

        var values = new[] { InkLeftAt24, InkTopAt24, InkRightAt24, InkBottomAt24 };
        if (Array.TrueForAll(values, value => !value.HasValue)) return;
        if (Array.Exists(values, value => !value.HasValue) ||
            Array.Exists(values, value => !Finite(value.Value)) ||
            InkRightAt24.Value < InkLeftAt24.Value || InkBottomAt24.Value < InkTopAt24.Value)
            throw new ArgumentException($"设备字体 {fontName} 的文本单元“{textElement}”墨迹矩形必须完整、有限且边界顺序有效。",
                nameof(DeviceTextElementCalibration));
    }

    private static bool NonNegative(double value) => value >= 0 && Finite(value);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
