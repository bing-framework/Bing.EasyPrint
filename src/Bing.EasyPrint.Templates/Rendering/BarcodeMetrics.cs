using System;
using System.Linq;
using Bing.EasyPrint.Elements;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 按制式校验内容并估算一维条码尺寸。
/// </summary>
internal static class BarcodeMetrics
{
    /// <summary>
    /// 计算条码、左静区及实际编码数据。
    /// </summary>
    /// <param name="element">条码制式和尺寸设置。</param>
    /// <param name="data">待编码的可打印 ASCII 数据。</param>
    /// <returns>总宽度、左静区宽度及规范化的数据，宽度单位为打印点。</returns>
    // CPCL Link-OS Programming Guide, 6.2.1: width 转换为点后加一；ratio 为枚举值。
    // https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/cpcl-link-os-pg-en.pdf
    internal static (int Width, int Left, string Data) Measure(BpBarcodeElement element, string data)
    {
        if (!Enum.IsDefined(typeof(BarcodeType), element.BarcodeType) || element.LineWidth < 1 || element.Height < 1 ||
            !(element.Ratio >= 0 && element.Ratio <= 4 || element.Ratio >= 20 && element.Ratio <= 30))
            throw new ArgumentException("条码参数无效；Ratio 使用 CPCL 编码 0–4 或 20–30。");
        if (string.IsNullOrEmpty(data) || data.Any(c => c < 32 || c > 126))
            throw new ArgumentException("一维条码内容必须为非空可打印 ASCII。");
        checked
        {
            var narrow = element.LineWidth + 1;
            var ratioTenths = element.Ratio <= 4 ? 15 + element.Ratio * 5 : element.Ratio;
            var wide = (int)(((long)narrow * ratioTenths + 9) / 10); // 对非整数宽度向上取整，避免低估。
            var left = 10 * narrow;
            var right = left;
            int bars;
            switch (element.BarcodeType)
            {
                case BarcodeType.Ean13:
                    data = Retail(data, 13);
                    bars = 95 * narrow; left = 11 * narrow; right = 7 * narrow;
                    break;
                case BarcodeType.Ean8:
                    if (data.Length == 6) data = "0" + data;
                    data = Retail(data, 8);
                    bars = 67 * narrow; left = right = 7 * narrow;
                    break;
                case BarcodeType.UpcA:
                    data = Retail(data, 12);
                    bars = 95 * narrow; left = right = 9 * narrow;
                    break;
                case BarcodeType.UpcE:
                    Digits(data);
                    if (data.Length == 6) data = "0" + data;
                    if (data.Length != 7 || data[0] != '0' && data[0] != '1')
                        throw new ArgumentException("UPC-E 使用 6 位数据或 7 位含号码系统的数据（号码系统为 0/1）。");
                    bars = 51 * narrow; left = 9 * narrow; right = 7 * narrow;
                    break;
                case BarcodeType.Code39:
                    Alphabet(data, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%");
                    bars = (data.Length + 2) * (6 * narrow + 3 * wide) + (data.Length + 1) * narrow;
                    break;
                case BarcodeType.Code93:
                    // Extended Code 93 将基本字母表外的 ASCII 编码成两个符号，另有 C/K 两个校验符。
                    var symbols = data.Sum(c => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%".IndexOf(c) >= 0 ? 1 : 2);
                    bars = ((symbols + 4) * 9 + 1) * narrow;
                    break;
                case BarcodeType.Code128:
                    bars = (Code128Symbols(data) * 11 + 35) * narrow;
                    break;
                case BarcodeType.UccEan128:
                    if (data.Length < 3 || data[0] < '0' || data[0] > '9' || data[1] < '0' || data[1] > '9')
                        throw new ArgumentException("GS1-128 需要以数字应用标识开头的元素串，不接受括号标注的显示文本。");
                    // GS1 应用标识与分隔结构由业务数据提供，保留自动编码的安全上界及 FNC1。
                    bars = (Code128Symbols(data) * 11 + 46) * narrow;
                    break;
                case BarcodeType.Codabar:
                    if (data.Length < 3 || "ABCD".IndexOf(data[0]) < 0 || "ABCD".IndexOf(data[data.Length - 1]) < 0)
                        throw new ArgumentException("Codabar 必须包含 A/B/C/D 起止符和至少一个数据字符。");
                    Alphabet(data.Substring(1, data.Length - 2), "0123456789-$:/.+");
                    bars = (data.Length - 1) * narrow;
                    foreach (var c in data)
                    {
                        var wideCount = "0123456789-$".IndexOf(c) >= 0 ? 2 : 3;
                        bars += wideCount * wide + (7 - wideCount) * narrow;
                    }
                    break;
                case BarcodeType.I2OF5:
                    Digits(data);
                    if (data.Length % 2 != 0) data = "0" + data;
                    bars = InterleavedWidth(data.Length, narrow, wide);
                    break;
                case BarcodeType.I2OF5C:
                    Digits(data);
                    if (data.Length % 2 == 0) data = "0" + data;
                    bars = InterleavedWidth(data.Length + 1, narrow, wide);
                    break;
                case BarcodeType.I2OF5G:
                    Digits(data);
                    if (data.Length < 11 || data.Length > 14) throw new ArgumentException("I2OF5G 需要 11–14 位数字。");
                    // 奇数位时固件补校验位，偶数位已包含校验位；预留完整符号长度。
                    bars = InterleavedWidth(data.Length + data.Length % 2, narrow, wide);
                    break;
                case BarcodeType.Msi:
                    Digits(data);
                    if (data.Length > 13) throw new ArgumentException("MSI 最多支持 13 位数字。");
                    // 每个数据位为一窄一宽；起止合计三窄两宽，MSI 不追加校验位。
                    bars = data.Length * 4 * (narrow + wide) + 3 * narrow + 2 * wide;
                    break;
                case BarcodeType.Postnet:
                    var digits = data.Replace("-", "");
                    Digits(digits);
                    if (digits.Length != 5 && digits.Length != 9 && digits.Length != 11)
                        throw new ArgumentException("POSTNET 需要 5、9 或 11 位邮政数字，可用连字符分组。");
                    var count = (digits.Length + 1) * 5 + 2; // 校验位及两根起止条。
                    bars = count * narrow + (count - 1) * (element.Ratio + 1);
                    break;
                case BarcodeType.Fim:
                    if (data.Length != 1 || "ABC".IndexOf(data[0]) < 0) throw new ArgumentException("FIM 仅支持 A/B/C。");
                    bars = 17 * narrow;
                    break;
                default:
                    throw new ArgumentException("不支持的条码类型。");
            }
            return (bars + left + right, left, data);
        }
    }

    /// <summary>
    /// 估算交叉二五条码的条空宽度。
    /// </summary>
    /// <param name="digits">编码后的数字位数。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度，单位为打印点。</param>
    /// <returns>条空总宽度，单位为打印点。</returns>
    private static int InterleavedWidth(int digits, int narrow, int wide) => checked((3 * digits + 6) * narrow + (2 * digits + 1) * wide);

    /// <summary>
    /// 估算 Code 128 自动编码所需的数据符号数。
    /// </summary>
    /// <param name="data">待编码内容。</param>
    /// <returns>数据符号数，不含起止及校验符号。</returns>
    private static int Code128Symbols(string data)
    {
        // 纯偶数数字由 CPCL Auto 使用子集 C；混合输入按全 B 的上界，避免依赖固件的切换策略。
        return data.Length % 2 == 0 && data.All(c => c >= '0' && c <= '9') ? data.Length / 2 : data.Length;
    }

    /// <summary>
    /// 校验零售条码并补齐缺少的校验位。
    /// </summary>
    /// <param name="data">仅含数字的条码内容。</param>
    /// <param name="length">包含校验位的目标长度。</param>
    /// <returns>包含有效校验位的条码内容。</returns>
    private static string Retail(string data, int length)
    {
        Digits(data);
        if (data.Length != length - 1 && data.Length != length) throw new ArgumentException($"条码需要 {length - 1} 或 {length} 位数字。");
        var sum = 0;
        for (var i = length - 2; i >= 0; i--) sum += (data[i] - '0') * ((length - 2 - i) % 2 == 0 ? 3 : 1);
        var check = (char)('0' + (10 - sum % 10) % 10);
        if (data.Length == length && data[length - 1] != check) throw new ArgumentException("条码校验位错误。");
        return data.Length == length ? data : data + check;
    }

    /// <summary>
    /// 校验条码内容只包含非空 ASCII 数字。
    /// </summary>
    /// <param name="data">待校验内容。</param>
    private static void Digits(string data)
    {
        if (data.Length == 0 || data.Any(c => c < '0' || c > '9')) throw new ArgumentException("该制式仅接受 ASCII 数字。");
    }

    /// <summary>
    /// 校验内容属于指定条码制式的字符集。
    /// </summary>
    /// <param name="data">待校验内容。</param>
    /// <param name="alphabet">允许使用的字符集。</param>
    private static void Alphabet(string data, string alphabet)
    {
        if (data.Any(c => alphabet.IndexOf(c) < 0)) throw new ArgumentException("条码包含该制式不支持的字符。");
    }
}
