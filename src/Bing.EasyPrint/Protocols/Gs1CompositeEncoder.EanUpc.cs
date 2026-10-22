using System;
using System.Collections.Generic;
using System.Threading;
using ZXing;

namespace Bing.EasyPrint.Protocols;

internal static partial class Gs1CompositeEncoder
{
    /// <summary>判断是否为不带附加码的 EAN/UPC 线性组件。</summary>
    internal static bool IsEanUpc(BarcodeType type)
        => type == BarcodeType.Ean8 || type == BarcodeType.Ean13 ||
           type == BarcodeType.UpcA || type == BarcodeType.UpcE;

    /// <summary>补齐或验证校验位，拒绝非数字、附加码及非规范 UPC-E 零压缩。</summary>
    private static string NormalizeEanUpc(BarcodeType type, string data)
    {
        foreach (var character in data)
            if (character < '0' || character > '9')
                throw new ArgumentException("EAN/UPC Composite 主数据仅允许 ASCII 数字，不支持附加码或括号 AI。", "primaryData");
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

    /// <summary>生成带左右各一模块空间的线性行；不增加用户静区。</summary>
    private static string EncodeEanUpcLinear(BarcodeType type, string primary, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var format = type == BarcodeType.Ean8 ? BarcodeFormat.EAN_8 :
            type == BarcodeType.Ean13 ? BarcodeFormat.EAN_13 :
            type == BarcodeType.UpcA ? BarcodeFormat.UPC_A : BarcodeFormat.UPC_E;
        var encoded = new MultiFormatWriter().encode(primary, format, 0, 1,
            new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 });
        var row = new char[encoded.Width + 2];
        row[0] = row[row.Length - 1] = '0';
        for (var column = 0; column < encoded.Width; column++) row[column + 1] = encoded[column, 0] ? '1' : '0';
        cancellationToken.ThrowIfCancellationRequested();
        return new string(row);
    }

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
