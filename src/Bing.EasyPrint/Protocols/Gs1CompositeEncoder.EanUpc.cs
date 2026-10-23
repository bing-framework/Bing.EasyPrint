using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ZXing;

namespace Bing.EasyPrint.Protocols;

internal static partial class Gs1CompositeEncoder
{
    /// <summary>判断是否为 EAN/UPC 线性组件。</summary>
    internal static bool IsEanUpc(BarcodeType type)
        => type == BarcodeType.Ean8 || type == BarcodeType.Ean13 ||
           type == BarcodeType.UpcA || type == BarcodeType.UpcE;

    /// <summary>分离可选的严格 2/5 位附加码，再补齐或验证主码校验位。</summary>
    private static string NormalizeEanUpc(BarcodeType type, string data)
    {
        var separator = data.IndexOf('+');
        if (separator < 0) return NormalizeEanUpcMain(type, data);
        var addon = data.Substring(separator + 1);
        if (addon.Length != 2 && addon.Length != 5)
            throw new ArgumentException("EAN/UPC Composite 附加码必须恰好为 2 或 5 位 ASCII 数字。", "primaryData");
        foreach (var character in addon)
            if (character < '0' || character > '9')
                throw new ArgumentException("EAN/UPC Composite 附加码必须恰好为 2 或 5 位 ASCII 数字，且仅允许一个加号。", "primaryData");
        return NormalizeEanUpcMain(type, data.Substring(0, separator)) + "+" + addon;
    }

    /// <summary>验证主码数字、长度、校验位及 UPC-E 零压缩。</summary>
    private static string NormalizeEanUpcMain(BarcodeType type, string data)
    {
        foreach (var character in data)
            if (character < '0' || character > '9')
                throw new ArgumentException("EAN/UPC Composite 主码仅允许 ASCII 数字，不支持括号 AI。", "primaryData");
        if (type == BarcodeType.UpcE)
        {
            if (data.Length < 6 || data.Length > 8)
                throw new ArgumentException("UPC-E Composite 主数据必须为 6、7 或 8 位。", "primaryData");
            if (data.Length == 6) data = "0" + data;
            if (data[0] != '0' && data[0] != '1')
                throw new ArgumentException("UPC-E 号码系统必须为 0 或 1。", "primaryData");
            var payload = data.Substring(1, 6);
            var last = payload[5];
            // ISO/IEC 15420 zero suppression restrictions; also checked by Zint upcean.c.
            if (last == '3' && payload[2] <= '2' || last == '4' && payload[3] == '0' ||
                last >= '5' && payload[4] == '0')
                throw new ArgumentException("UPC-E 主数据不符合规范的零压缩规则。", "primaryData");
            string expanded;
            if (last <= '2') expanded = data[0] + payload.Substring(0, 2) + last + "0000" + payload.Substring(2, 3);
            else if (last == '3') expanded = data[0] + payload.Substring(0, 3) + "00000" + payload.Substring(3, 2);
            else if (last == '4') expanded = data[0] + payload.Substring(0, 4) + "00000" + payload[4];
            else expanded = data[0] + payload.Substring(0, 5) + "0000" + last;
            return CompleteRetailCheckDigit(data, 7, RetailCheckDigit(expanded));
        }
        var length = type == BarcodeType.Ean8 ? 7 : type == BarcodeType.Ean13 ? 12 : 11;
        if (data.Length != length && data.Length != length + 1)
            throw new ArgumentException($"{type} Composite 主数据必须为 {length} 或 {length + 1} 位。", "primaryData");
        return CompleteRetailCheckDigit(data, length, RetailCheckDigit(data.Substring(0, length)));
    }

    private static char RetailCheckDigit(string data)
    {
        var sum = 0;
        for (var index = data.Length - 1; index >= 0; index--)
            sum += (data[index] - '0') * ((data.Length - index) % 2 == 1 ? 3 : 1);
        return (char)('0' + (10 - sum % 10) % 10);
    }

    private static string CompleteRetailCheckDigit(string data, int payloadLength, char checkDigit)
    {
        if (data.Length == payloadLength) return data + checkDigit;
        if (data[payloadLength] != checkDigit)
            throw new ArgumentException("EAN/UPC Composite 主数据校验位无效。", "primaryData");
        return data;
    }

    /// <summary>生成线性行；主码左侧留一模块，无附加码时右侧也留一模块。不增加用户静区。</summary>
    private static string EncodeEanUpcLinear(BarcodeType type, string primary, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var format = type == BarcodeType.Ean8 ? BarcodeFormat.EAN_8 :
            type == BarcodeType.Ean13 ? BarcodeFormat.EAN_13 :
            type == BarcodeType.UpcA ? BarcodeFormat.UPC_A : BarcodeFormat.UPC_E;
        var separator = primary.IndexOf('+');
        var encoded = new MultiFormatWriter().encode(separator < 0 ? primary : primary.Substring(0, separator), format, 0, 1,
            new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 });
        var row = new StringBuilder(encoded.Width + 60).Append('0');
        for (var column = 0; column < encoded.Width; column++) row.Append(encoded[column, 0] ? '1' : '0');
        if (separator < 0) row.Append('0');
        else
        {
            row.Append('0', type == BarcodeType.UpcA ? 9 : 7);
            AppendEanUpcAddon(row, primary.Substring(separator + 1));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return row.ToString();
    }

    /// <summary>追加附加码；奇偶表及间隔来自固定版本 Zint upcean.c，许可见 ThirdPartyNotices.txt。</summary>
    private static void AppendEanUpcAddon(StringBuilder row, string addon)
    {
        var parity = addon.Length == 2
            ? new[] { "AA", "AB", "BA", "BB" }[((addon[0] - '0') * 10 + addon[1] - '0') % 4]
            : new[] { "BBAAA", "BABAA", "BAABA", "BAAAB", "ABBAA", "AABBA", "AAABB", "ABABA", "ABAAB", "AABAB" }
                [(3 * (addon[0] + addon[2] + addon[4] - 3 * '0') + 9 * (addon[1] + addon[3] - 2 * '0')) % 10];
        var setA = new[] { "3211", "2221", "2122", "1411", "1132", "1231", "1114", "1312", "1213", "3112" };
        var setB = new[] { "1123", "1222", "2212", "1141", "2311", "1321", "4111", "2131", "3121", "2113" };
        row.Append("1011");
        for (var index = 0; index < addon.Length; index++)
        {
            if (index > 0) row.Append("01");
            var widths = (parity[index] == 'A' ? setA : setB)[addon[index] - '0'];
            for (var run = 0; run < widths.Length; run++)
                row.Append(run % 2 == 0 ? '0' : '1', widths[run] - '0');
        }
    }

    /// <summary>分隔图案仅围住主码，附加码不改变其位置。</summary>
    private static int EanUpcSeparatorWidth(BarcodeType type)
        => type == BarcodeType.Ean8 ? 69 : type == BarcodeType.UpcE ? 53 : 97;

    /// <summary>生成 EAN/UPC 的三行 2X 分隔图案；参照固定版本 Zint upcean.c，许可见 ThirdPartyNotices.txt。</summary>
    private static string[] BuildEanUpcSeparators(int width)
    {
        var inner = new string('0', width).ToCharArray();
        var outer = new string('0', width).ToCharArray();
        inner[1] = inner[width - 2] = '1';
        outer[0] = outer[width - 1] = '1';
        return new[] { new string(inner), new string(outer), new string(inner) };
    }
}
