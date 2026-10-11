using System;
using System.Linq;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

/// <summary>共用 GS V 参数预检，确保独立命令、页面和任务采用同一种切纸语义。</summary>
internal static class EscPosCutEncoder
{
    internal static byte[] Encode(PrintCutOptions options, EscPosPrinterProfile profile)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        if (profile?.SupportsCut == false || profile?.SupportedCutModes != null &&
            !profile.SupportedCutModes.Contains(options.Mode))
            throw new NotSupportedException($"设备 {profile?.Name ?? "ESC/POS"} 未声明支持 {options.Mode} 切纸。");
        var mode = options.Mode == PrintCutMode.Partial ? 1 : 0;
        if (!options.FeedToCutPosition) return new byte[] { 0x1D, 0x56, (byte)mode };
        if (profile?.SupportsFeedAndCut != true)
            throw new NotSupportedException("进纸到切刀位置要求显式机型支持 GS V Function B。");
        long amount;
        switch (options.FeedBeforeCutUnit)
        {
            case PrintFeedUnit.MotionUnits: amount = options.FeedBeforeCutAmount; break;
            case PrintFeedUnit.Dots:
                amount = checked(((long)options.FeedBeforeCutAmount * profile.MotionUnitsPerInch +
                    profile.VerticalDpi - 1) / profile.VerticalDpi); break;
            default: throw new NotSupportedException("GS V 切前补偿只支持设备运动单位或显式机型的纵向打印点。");
        }
        if (amount > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(options.FeedBeforeCutAmount),
                "切前补偿换算后必须在 0–255 个纵向运动单位内；不能通过重复切纸分块。");
        return new byte[] { 0x1D, 0x56, (byte)(65 + mode), (byte)amount };
    }
}
