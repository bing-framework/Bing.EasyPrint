using System;
using System.Linq;

namespace Bing.EasyPrint.Protocols;

/// <summary>依据目标设备手册确认的 ZPL 裁切模式。</summary>
public enum ZplCutStrategy
{
    /// <summary>^MMC 与 ^PQ 分组裁切；每个 PrintWithCut 批次切一次。</summary>
    Cutter = 0,
    /// <summary>^MMD 后单独发送 ~JK 文件。</summary>
    Delayed = 1,
    /// <summary>设备确认支持 ^MMK、^KV 与 ^CN 的 Kiosk 裁切。</summary>
    Kiosk = 2
}

/// <summary>调用方确认的切刀契约；不从型号或固件字符串推断支持。</summary>
public sealed class ZplCutterProfile
{
    /// <summary>裁切策略。</summary>
    public ZplCutStrategy Strategy { get; set; } = ZplCutStrategy.Cutter;
    /// <summary>确认的切法；Cutter/Delayed 仅映射 Full。</summary>
    public PrintCutMode[] SupportedCutModes { get; set; } = new[] { PrintCutMode.Full };
    /// <summary>确认支持 ^MMT，供非裁切格式关闭自动裁切；须显式确认。</summary>
    public bool SupportsTearOff { get; set; }
    /// <summary>Kiosk 半切留下未切介质的毫米数，10–60；其他策略不接受。</summary>
    public int? KioskPartialUncutMillimeters { get; set; }
    /// <summary>Kiosk 裁切边距，2–9 毫米；Kiosk 策略必须明确设置。</summary>
    public int? KioskCutMarginMillimeters { get; set; }

    internal ZplCutterProfile Snapshot()
    {
        if (!Enum.IsDefined(typeof(ZplCutStrategy), Strategy)) throw new ArgumentOutOfRangeException(nameof(Strategy));
        if (!SupportsTearOff) throw new NotSupportedException("ZPL 受控裁切要求显式确认 ^MMT 非裁切模式。");
        if (SupportedCutModes == null || SupportedCutModes.Length == 0 ||
            SupportedCutModes.Any(mode => !Enum.IsDefined(typeof(PrintCutMode), mode)) ||
            SupportedCutModes.Distinct().Count() != SupportedCutModes.Length)
            throw new ArgumentException("ZPL 切法列表必须非空、有效且不重复。", nameof(SupportedCutModes));
        if (Strategy != ZplCutStrategy.Kiosk)
        {
            if (SupportedCutModes.Any(mode => mode != PrintCutMode.Full))
                throw new NotSupportedException("Cutter/Delayed 只映射已确认的全切，不以 Kiosk 参数代替半切。");
            if (KioskCutMarginMillimeters.HasValue || KioskPartialUncutMillimeters.HasValue)
                throw new NotSupportedException("Kiosk 参数只能用于 Kiosk 裁切。");
        }
        else
        {
            if (!KioskCutMarginMillimeters.HasValue || KioskCutMarginMillimeters < 2 || KioskCutMarginMillimeters > 9)
                throw new ArgumentOutOfRangeException(nameof(KioskCutMarginMillimeters));
            if (KioskPartialUncutMillimeters.HasValue &&
                (KioskPartialUncutMillimeters < 10 || KioskPartialUncutMillimeters > 60))
                throw new ArgumentOutOfRangeException(nameof(KioskPartialUncutMillimeters));
            if (SupportedCutModes.Contains(PrintCutMode.Partial) && !KioskPartialUncutMillimeters.HasValue)
                throw new ArgumentException("Kiosk 半切须声明未切介质毫米数。", nameof(KioskPartialUncutMillimeters));
            if (!SupportedCutModes.Contains(PrintCutMode.Partial) && KioskPartialUncutMillimeters.HasValue)
                throw new ArgumentException("未声明半切时不能设置半切距离。", nameof(KioskPartialUncutMillimeters));
        }
        var copy = (ZplCutterProfile)MemberwiseClone();
        copy.SupportedCutModes = (PrintCutMode[])SupportedCutModes.Clone();
        return copy;
    }
}
