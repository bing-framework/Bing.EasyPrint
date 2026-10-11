using System;
using System.Linq;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

// 所有结构化入口复用同一时序；不假定在 SET CUTTER OFF 后所有机型的 CUT 都可用。
internal static class TsplCutEncoder
{
    internal static void Validate(PrintCutOptions options, TsplCutterProfile profile)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        if (profile == null) throw new NotSupportedException("TSPL 裁切要求显式机型切刀契约。");
        if (!profile.SupportedCutModes.Contains(options.Mode))
            throw new NotSupportedException($"TSPL 机型未声明支持 {options.Mode} 切纸。");
        if (!options.FeedToCutPosition) return;
        if (profile.Strategy != TsplCutStrategy.Immediate || !profile.CutPositionFeedDots.HasValue)
            throw new NotSupportedException("TSPL 切前补偿要求立即裁切策略及实测切刀位置点数。");
        if (options.FeedBeforeCutUnit != PrintFeedUnit.Dots)
            throw new NotSupportedException("TSPL 切前补偿只支持纵向打印点 Dots。");
        if ((long)profile.CutPositionFeedDots.Value + options.FeedBeforeCutAmount > 9999)
            throw new ArgumentOutOfRangeException(nameof(options.FeedBeforeCutAmount), "TSPL 切刀位置与补偿合计不得超过 9999 点。");
    }

    internal static string Reset(TsplCutterProfile profile) => "SET CUTTER OFF\r\n" +
        (profile.SupportedCutModes.Contains(PrintCutMode.Partial) ? "SET PARTIAL_CUTTER OFF\r\n" : "");

    internal static string Cut(PrintCutOptions options, TsplCutterProfile profile)
    {
        Validate(options, profile);
        if (profile.Strategy != TsplCutStrategy.Immediate)
            throw new NotSupportedException("Batch 必须使用 PrintWithCut，在 PRINT 前设置自动切纸。");
        var dots = options.FeedToCutPosition ? profile.CutPositionFeedDots.Value + options.FeedBeforeCutAmount : 0;
        return "EOJ\r\n" + (dots == 0 ? "" : $"FEED {dots}\r\nEOJ\r\n") + "CUT\r\n";
    }

    internal static string Print(TsplCutterProfile profile, int sets, int copies, PrintCutOptions cut = null)
    {
        if (sets < 1 || copies < 1) throw new ArgumentOutOfRangeException(nameof(copies));
        if (cut != null) Validate(cut, profile);
        if (profile == null) return $"PRINT {sets},{copies}\r\n";
        // 等待之前排队的 PRINT 完成后再更改持久切刀设置。
        var result = "EOJ\r\n" + Reset(profile);
        if (cut != null && profile.Strategy == TsplCutStrategy.Batch)
            result += cut.Mode == PrintCutMode.Partial ? "SET PARTIAL_CUTTER BATCH\r\n" : "SET CUTTER BATCH\r\n";
        result += $"PRINT {sets},{copies}\r\nEOJ\r\n";
        if (cut == null) return result;
        if (profile.Strategy == TsplCutStrategy.Batch) return result + Reset(profile);
        // Cut() 的 EOJ 是独立 API 的同步点；此处 PRINT 已完成，不重复等待。
        return result + Cut(cut, profile).Substring("EOJ\r\n".Length);
    }
}
