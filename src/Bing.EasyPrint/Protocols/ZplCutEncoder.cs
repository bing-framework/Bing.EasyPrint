using System;
using System.Linq;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

internal static class ZplCutEncoder
{
    internal const string DelayedControl = "~JK\r\n";
    internal static void Validate(PrintCutOptions options, ZplCutterProfile cutter)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        options.Validate();
        if (cutter == null) throw new NotSupportedException("ZPL 显式裁切须声明 Cutter 契约。");
        if (!cutter.SupportedCutModes.Contains(options.Mode)) throw new NotSupportedException("机型未声明请求的 ZPL 切法。");
        if (options.FeedToCutPosition || options.FeedBeforeCutAmount != 0)
            throw new NotSupportedException("ZPL 不映射通用切前进纸补偿；Kiosk 边距须通过切刀契约声明。");
    }

    internal static void ValidateQuantity(int quantity)
    {
        if (quantity < 1 || quantity > 99999999) throw new ArgumentOutOfRangeException(nameof(quantity));
    }

    internal static string NeutralPrint(int quantity, ZplCutterProfile cutter)
        => (cutter == null ? "" : "^MMT\r\n") + $"^PQ{quantity}\r\n";

    internal static string CutPrint(int quantity, PrintCutOptions options, ZplCutterProfile cutter)
    {
        ValidateQuantity(quantity);
        Validate(options, cutter);
        switch (cutter.Strategy)
        {
            case ZplCutStrategy.Cutter:
                return $"^MMC\r\n^PQ{quantity},{quantity},0,Y\r\n";
            case ZplCutStrategy.Delayed:
                return $"^MMD\r\n^PQ{quantity}\r\n";
            default:
                var amount = options.Mode == PrintCutMode.Full ? 0 : cutter.KioskPartialUncutMillimeters.Value;
                // 只设置裁切相关 a/b；不改写 presenter 行为、超时或 loop length。
                return $"^MMK\r\n^KV{amount},{cutter.KioskCutMarginMillimeters.Value}\r\n^PQ{quantity}\r\n" +
                    (options.Mode == PrintCutMode.Full ? "^CN1\r\n" : "^CN0\r\n");
        }
    }
}
