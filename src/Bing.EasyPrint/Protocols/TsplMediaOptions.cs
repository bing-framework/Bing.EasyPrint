using System;

namespace Bing.EasyPrint.Protocols;

/// <summary>
/// TSPL 标签介质和设备设置。
/// </summary>
/// <remarks>
/// 介质设置会在命令写入前完成校验。<see cref="Dpi"/> 是主机侧的设备元数据，TSPL
/// 不提供通用的运行时 DPI 设置命令，因此不会直接写入指令流。
/// </remarks>
public sealed class TsplMediaOptions
{
    /// <summary>可选机型声明；为空时保留协议通用能力和默认字节。</summary>
    public TsplPrinterProfile Profile { get; set; }
    /// <summary>
    /// TSPL 常用的默认打印分辨率，单位为 DPI。
    /// </summary>
    public const int DefaultDpi = 203;

    /// <summary>
    /// 获取或设置标签间隙距离，单位由打印机的标签设置决定。
    /// </summary>
    public int? GapDistance { get; set; }

    /// <summary>
    /// 获取或设置标签间隙偏移量，单位由打印机的标签设置决定。
    /// </summary>
    public int? GapOffset { get; set; }

    /// <summary>
    /// 获取或设置打印方向；有效值为 0 或 1。
    /// </summary>
    public int? Direction { get; set; }

    /// <summary>
    /// 获取或设置打印参考点的水平坐标。
    /// </summary>
    public int? ReferenceX { get; set; }

    /// <summary>
    /// 获取或设置打印参考点的垂直坐标。
    /// </summary>
    public int? ReferenceY { get; set; }

    /// <summary>
    /// 获取或设置打印速度级别；必须为正数。
    /// </summary>
    public int? Speed { get; set; }

    /// <summary>
    /// 获取或设置打印浓度级别；有效范围为 0 到 15。
    /// </summary>
    public int? Density { get; set; }

    /// <summary>
    /// 获取或设置打印机分辨率元数据，单位为 DPI。
    /// </summary>
    public int Dpi { get; set; } = DefaultDpi;

    /// <summary>允许斜线以主机生成的位图输出；默认关闭。</summary>
    public bool AllowRasterizedLines { get; set; }

    /// <summary>允许粗体、下划线文字以主机位图输出；默认关闭并严格拒绝。</summary>
    public bool AllowRasterizedTextStyles { get; set; }

    internal void Validate()
    {
        Profile?.Snapshot();
        if (Dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(Dpi), "TSPL DPI 必须为正数。");
        if (GapDistance.HasValue && GapDistance.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(GapDistance));
        if (GapOffset.HasValue && GapOffset.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(GapOffset));
        if (Direction.HasValue && (Direction.Value < 0 || Direction.Value > 1))
            throw new ArgumentOutOfRangeException(nameof(Direction));
        if (ReferenceX.HasValue && ReferenceX.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(ReferenceX));
        if (ReferenceY.HasValue && ReferenceY.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(ReferenceY));
        if (Speed.HasValue && Speed.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(Speed));
        if (Density.HasValue && (Density.Value < 0 || Density.Value > 15))
            throw new ArgumentOutOfRangeException(nameof(Density));
    }

    internal TsplMediaOptions Clone()
    {
        return new TsplMediaOptions
        {
            Profile = Profile?.Clone(),
            GapDistance = GapDistance,
            GapOffset = GapOffset,
            Direction = Direction,
            ReferenceX = ReferenceX,
            ReferenceY = ReferenceY,
            Speed = Speed,
            Density = Density,
            Dpi = Dpi,
            AllowRasterizedLines = AllowRasterizedLines,
            AllowRasterizedTextStyles = AllowRasterizedTextStyles
        };
    }
}
