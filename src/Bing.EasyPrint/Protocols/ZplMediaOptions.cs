using System;

namespace Bing.EasyPrint.Protocols;

/// <summary>
/// ZPL 标签介质和设备设置。
/// </summary>
/// <remarks>
/// <see cref="Dpi"/> 是主机侧的设备分辨率元数据，ZPL 不提供通用的运行时 DPI 设置命令，
/// 因此不会直接写入指令流。<see cref="PrintSpeed"/> 会在未显式指定标签打印速度时由
/// <see cref="ZplPrintCommand.Label(int, int, int?)"/> 写入 <c>^PR</c>。
/// </remarks>
public sealed class ZplMediaOptions
{
    /// <summary>可选机型声明；为空时保留协议通用能力和默认字节。</summary>
    public ZplPrinterProfile Profile { get; set; }
    /// <summary>
    /// ZPL 常用的默认打印分辨率，单位为 DPI。
    /// </summary>
    public const int DefaultDpi = 203;

    /// <summary>
    /// 获取或设置打印机分辨率元数据，单位为 DPI。
    /// </summary>
    public int Dpi { get; set; } = DefaultDpi;

    /// <summary>允许斜线以主机生成的位图输出；默认关闭。</summary>
    public bool AllowRasterizedLines { get; set; }

    /// <summary>允许粗体、下划线文字以主机位图输出；默认关闭并严格拒绝。</summary>
    public bool AllowRasterizedTextStyles { get; set; }

    /// <summary>
    /// 获取或设置默认标签打印速度；必须为正数。
    /// </summary>
    public int? PrintSpeed { get; set; }

    /// <summary>获取或设置标签打印方向；ZPL 仅支持 None 和 Down；为空时保留打印机当前方向。</summary>
    public PrintOrientation? Orientation { get; set; }

    /// <summary>
    /// 获取或设置介质跟踪方式：N 连续介质，Y/W 网格或间隙介质，M 黑标介质，A 自动检测，V 可变长度连续介质。
    /// </summary>
    public char? MediaTracking { get; set; }

    /// <summary>获取或设置打印浓度，ZPL <c>^MD</c> 范围为 -30 到 30。</summary>
    public int? Darkness { get; set; }

    internal void Validate()
    {
        Profile?.Snapshot();
        if (Dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(Dpi), "ZPL DPI 必须为正数。");
        if (PrintSpeed.HasValue && PrintSpeed.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(PrintSpeed));
        if (Orientation.HasValue && Orientation.Value != PrintOrientation.None &&
            Orientation.Value != PrintOrientation.Down)
            throw new ArgumentOutOfRangeException(nameof(Orientation),
                "ZPL ^PO 仅支持 None（N）或 Down（I）；Left/Right 无法表示为整页方向。");
        if (MediaTracking.HasValue && MediaTracking.Value != 'N' && MediaTracking.Value != 'Y' &&
            MediaTracking.Value != 'W' && MediaTracking.Value != 'M' && MediaTracking.Value != 'A' &&
            MediaTracking.Value != 'V')
            throw new ArgumentOutOfRangeException(nameof(MediaTracking));
        if (Darkness.HasValue && (Darkness.Value < -30 || Darkness.Value > 30))
            throw new ArgumentOutOfRangeException(nameof(Darkness));
    }

    internal ZplMediaOptions Clone()
    {
        return new ZplMediaOptions
        {
            Profile = Profile?.Clone(),
            Dpi = Dpi,
            PrintSpeed = PrintSpeed,
            Orientation = Orientation,
            MediaTracking = MediaTracking,
            Darkness = Darkness,
            AllowRasterizedLines = AllowRasterizedLines,
            AllowRasterizedTextStyles = AllowRasterizedTextStyles
        };
    }
}
