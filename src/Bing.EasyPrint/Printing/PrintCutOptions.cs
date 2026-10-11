using System;

namespace Bing.EasyPrint.Printing;

/// <summary>显式裁切模式和切刀位置补偿；省略时保留原有页面或任务切纸行为。</summary>
public sealed class PrintCutOptions
{
    /// <summary>请求的裁切形状，不自动改用另一种模式。</summary>
    public PrintCutMode Mode { get; set; } = PrintCutMode.Full;
    /// <summary>先由设备进纸到切刀位置，再追加补偿距离并裁切。</summary>
    public bool FeedToCutPosition { get; set; }
    /// <summary>切刀位置以外的额外进纸量；零仍可显式请求进纸到切刀位置。</summary>
    public int FeedBeforeCutAmount { get; set; }
    /// <summary>补偿量单位；设备运动单位与打印点的换算须由机型声明。</summary>
    public PrintFeedUnit FeedBeforeCutUnit { get; set; } = PrintFeedUnit.MotionUnits;

    /// <summary>校验参数和内部组合；协议及机型能力由输出端继续检查。</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(typeof(PrintCutMode), Mode)) throw new ArgumentOutOfRangeException(nameof(Mode));
        if (!Enum.IsDefined(typeof(PrintFeedUnit), FeedBeforeCutUnit)) throw new ArgumentOutOfRangeException(nameof(FeedBeforeCutUnit));
        if (FeedBeforeCutAmount < 0) throw new ArgumentOutOfRangeException(nameof(FeedBeforeCutAmount));
        if (FeedBeforeCutAmount > 0 && !FeedToCutPosition)
            throw new ArgumentException("正数切前补偿必须启用进纸到切刀位置。", nameof(FeedToCutPosition));
    }

    /// <summary>创建经过校验的独立副本，后续修改原选项不会影响副本。</summary>
    public PrintCutOptions Snapshot()
    {
        Validate();
        return (PrintCutOptions)MemberwiseClone();
    }
}
