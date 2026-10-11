using System;
using System.Linq;

namespace Bing.EasyPrint.Protocols;

/// <summary>TSPL 裁切时序：立即命令或每个 PRINT 批次的自动裁切。</summary>
public enum TsplCutStrategy
{
    /// <summary>PRINT 后等待 EOJ，再执行 CUT；只接受设备确认的固定切法。</summary>
    Immediate = 0,
    /// <summary>PRINT 前启用 SET CUTTER/SET PARTIAL_CUTTER BATCH，结束后关闭。</summary>
    Batch = 1
}

/// <summary>按目标设备手册及实测声明的 TSPL 切刀契约；未配置时不启用切纸。</summary>
public sealed class TsplCutterProfile
{
    /// <summary>选择立即 CUT 或批次自动裁切，不根据固件字符串推断。</summary>
    public TsplCutStrategy Strategy { get; set; } = TsplCutStrategy.Batch;
    /// <summary>确认支持的切法；Immediate 必须仅声明 CUT 的一种实际固定形状。</summary>
    public PrintCutMode[] SupportedCutModes { get; set; } = new[] { PrintCutMode.Full };
    /// <summary>设备支持 EOJ 完成同步；当前受控裁切要求显式确认此能力。</summary>
    public bool SupportsEndOfJob { get; set; }
    /// <summary>从 PRINT 完成后的介质位置送到切刀的实测纵向点数；只用于 Immediate，null 禁用补偿。</summary>
    public int? CutPositionFeedDots { get; set; }

    internal TsplCutterProfile Snapshot()
    {
        if (!Enum.IsDefined(typeof(TsplCutStrategy), Strategy)) throw new ArgumentOutOfRangeException(nameof(Strategy));
        if (SupportedCutModes == null || SupportedCutModes.Length == 0 ||
            SupportedCutModes.Any(mode => !Enum.IsDefined(typeof(PrintCutMode), mode)) ||
            SupportedCutModes.Distinct().Count() != SupportedCutModes.Length)
            throw new ArgumentException("TSPL 切法列表必须非空、有效且不重复。", nameof(SupportedCutModes));
        if (!SupportsEndOfJob)
            throw new NotSupportedException("TSPL 受控裁切要求显式确认固件支持 EOJ 完成同步。");
        if (Strategy == TsplCutStrategy.Immediate && SupportedCutModes.Length != 1)
            throw new ArgumentException("立即 CUT 没有切法参数，只能声明设备的一种固定切法。", nameof(SupportedCutModes));
        if (CutPositionFeedDots.HasValue && (CutPositionFeedDots < 0 || CutPositionFeedDots > 9999))
            throw new ArgumentOutOfRangeException(nameof(CutPositionFeedDots));
        if (Strategy == TsplCutStrategy.Batch && CutPositionFeedDots.HasValue)
            throw new NotSupportedException("批次自动裁切无法在 PRINT 完成与自动切纸之间追加补偿进纸。");
        var copy = (TsplCutterProfile)MemberwiseClone();
        copy.SupportedCutModes = (PrintCutMode[])SupportedCutModes.Clone();
        return copy;
    }
}
