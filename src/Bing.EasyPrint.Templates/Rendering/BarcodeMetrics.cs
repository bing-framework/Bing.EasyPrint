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
    /// <param name="narrowModuleWidth">非 CPCL 协议显式指定的窄条点宽。</param>
    /// <param name="wideModuleWidth">TSPL 显式指定的宽条点宽。</param>
    /// <returns>总宽度、左静区宽度及规范化的数据，宽度单位为打印点。</returns>
    // CPCL Link-OS Programming Guide, 6.2.1: width 转换为点后加一；ratio 为枚举值。
    // https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/cpcl-link-os-pg-en.pdf
    internal static (int Width, int Left, string Data) Measure(BpBarcodeElement element, string data,
        int? narrowModuleWidth = null, int? wideModuleWidth = null)
    {
        if (!Enum.IsDefined(typeof(BarcodeType), element.BarcodeType) || element.LineWidth < 1 || element.Height < 1 ||
            !(element.Ratio >= 0 && element.Ratio <= 4 || element.Ratio >= 20 && element.Ratio <= 30))
            throw new ArgumentException("条码参数无效；Ratio 使用 CPCL 编码 0–4 或 20–30。");
        var allowGroupSeparator = element.BarcodeType == BarcodeType.UccEan128;
        var allowTelepenDel = element.BarcodeType == BarcodeType.Telepen || element.BarcodeType == BarcodeType.Code49;
        var allowEscPosControl = element.BarcodeType == BarcodeType.Code128Auto ||
                                 element.BarcodeType == BarcodeType.Gs1DataBarExpanded;
        if (string.IsNullOrEmpty(data) || data.Any(c =>
                c > (allowTelepenDel || allowEscPosControl ? 127 : 126) ||
                c < 32 && !allowEscPosControl && (!allowGroupSeparator || c != '\u001D')))
            throw new ArgumentException("一维条码内容必须为非空可打印 ASCII。");
        checked
        {
            var narrow = narrowModuleWidth ?? element.LineWidth + 1;
            var ratioTenths = element.Ratio <= 4 ? 15 + element.Ratio * 5 : element.Ratio;
            var wide = wideModuleWidth ?? (int)(((long)narrow * ratioTenths + 9) / 10); // 对非整数宽度向上取整，避免低估。
            if ((element.BarcodeType == BarcodeType.Ean14 || element.BarcodeType == BarcodeType.ChinaPost) &&
                !wideModuleWidth.HasValue)
                wide = narrow;
            if (narrow <= 0 || wide <= 0) throw new ArgumentOutOfRangeException(nameof(narrowModuleWidth));
            if (element.BarcodeType == BarcodeType.ChinaPost && wide != narrow)
                throw new ArgumentException("TSPL CPOST 要求窄条:宽条为 1:1。");
            if ((element.BarcodeType == BarcodeType.Telepen || element.BarcodeType == BarcodeType.Telepenn ||
                 element.BarcodeType == BarcodeType.Code49) && !IsTsplVariableRatio(narrow, wide))
                throw new ArgumentException("TSPL TELEPEN/TELEPENN/CODE49 仅支持 1:1、1:2、1:3、2:5 或 3:7 的窄宽比。");
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
                    // GS1 应用标识与分隔结构由业务数据提供；每个 ASCII 29 会编码为额外 FNC1 符号。
                    var groupSeparators = data.Count(c => c == '\u001D');
                    bars = checked((Code128Symbols(data) * 11 + 46 + groupSeparators * 11) * narrow);
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
                case BarcodeType.Code11:
                    if (data.Length == 0 || data.Any(character =>
                            !((character >= '0' && character <= '9') || character == '-')))
                        throw new ArgumentException("Code 11 仅接受非空 ASCII 数字和连字符。");
                    // TSPL 根据长度追加一个或两个 Modulo-11 校验位；按每个符号的
                    // 最大两个宽元素和一个字符间隔预留，保证页面边界不会低估。
                    var code11Symbols = checked(data.Length + (data.Length > 10 ? 2 : 1) + 2);
                    bars = checked(code11Symbols * (4 * narrow + 2 * wide));
                    break;
                case BarcodeType.ChinaPost:
                    Digits(data);
                    // CPOST uses a fixed 1:1 module ratio. Reserve ten equal
                    // modules per data symbol plus start/stop symbol slots.
                    var chinaPostSymbols = checked(data.Length + 2);
                    bars = checked(chinaPostSymbols * 10 * narrow);
                    break;
                case BarcodeType.Telepen:
                    TelepenAscii(data);
                    var telepenSymbols = checked(data.Length + 2);
                    // The device uses a 16-element Telepen symbol. Reserve the
                    // maximum module width for every element plus start/stop.
                    bars = checked(telepenSymbols * 16 * Math.Max(narrow, wide));
                    break;
                case BarcodeType.Telepenn:
                    Digits(data);
                    if (data.Length > 60)
                        throw new ArgumentException("TELEPENN 最多支持 60 位 ASCII 数字。");
                    var telepennSymbols = checked(data.Length + 2);
                    bars = checked(telepennSymbols * 16 * Math.Max(narrow, wide));
                    break;
                case BarcodeType.Code49:
                    Code49Ascii(data);
                    var code49Symbols = checked(data.Length + 2);
                    // CODE49 row count and stacking are device-selected; reserve
                    // a conservative 49-module content slot for each character.
                    bars = checked(code49Symbols * 49 * Math.Max(narrow, wide));
                    break;
                case BarcodeType.Gs1DataBarOmnidirectional:
                    Digits(data);
                    if (data.Length != 13) throw new ArgumentException("GS1 DataBar Omnidirectional 需要 13 位数字。");
                    bars = checked((13 * 14 + 34) * narrow);
                    break;
                case BarcodeType.Gs1DataBarTruncated:
                    Digits(data);
                    if (data.Length != 13) throw new ArgumentException("GS1 DataBar Truncated 需要 13 位数字。");
                    bars = checked((13 * 14 + 34) * narrow);
                    break;
                case BarcodeType.Gs1DataBarStacked:
                    Digits(data);
                    if (data.Length != 13) throw new ArgumentException("GS1 DataBar Stacked 需要 13 位数字。");
                    bars = checked(50 * narrow);
                    break;
                case BarcodeType.Gs1DataBarLimited:
                    Digits(data);
                    if (data.Length != 13 || data[0] != '0' && data[0] != '1')
                        throw new ArgumentException("GS1 DataBar Limited 需要 13 位数字且首位必须为 0 或 1。");
                    bars = checked((13 * 14 + 34) * narrow);
                    break;
                case BarcodeType.Gs1DataBarExpanded:
                    Gs1DataBarExpandedAscii(data);
                    var expandedLength = checked(data.Length + data.Count(c => c == '(' || c == ')' ||
                        c == '[' || c == ']' || c == '\u001D'));
                    bars = checked(((expandedLength + 2) * 16 + 34) * narrow);
                    break;
                case BarcodeType.Code128Auto:
                    if (data.Length > 255 || data.Any(c => c > 127))
                        throw new ArgumentException("CODE128 auto 需要 1–255 字节的 7-bit ASCII 数据。");
                    bars = checked((data.Length * 11 + 35) * narrow);
                    break;
                case BarcodeType.Plessey:
                    Digits(data);
                    // Plessey 为 4 位起始 + 4 位/数据位 + 8 位 CRC + 1 位终止 + 4 位反向起始；
                    // 每个二值单元由一窄一宽组成，TSPL 设备负责最终 CRC 编码。
                    bars = checked((4 * data.Length + 17) * (narrow + wide));
                    break;
                case BarcodeType.Itf14:
                    Digits(data);
                    if (data.Length != 13)
                        throw new ArgumentException("ITF14 需要 13 位数字 payload。");
                    // TSPL 设备将 13 位 payload 补成 14 位 ITF-14；宽度按完整交叉二五符号预留。
                    bars = InterleavedWidth(14, narrow, wide);
                    break;
                case BarcodeType.Ean14:
                    Digits(data);
                    if (data.Length != 13)
                        throw new ArgumentException("EAN14 需要 13 位数字 payload。");
                    // TSPL 设备生成带 AI 01 和校验位的固定 GS1-128 符号；
                    // 手册固定使用 1:1，完整符号占 134 个模块。
                    bars = checked(134 * narrow);
                    break;
                case BarcodeType.Postnet:
                    var digits = data.Replace("-", "");
                    Digits(digits);
                    if (digits.Length != 5 && digits.Length != 9 && digits.Length != 11)
                        throw new ArgumentException("POSTNET 需要 5、9 或 11 位邮政数字，可用连字符分组。");
                    var count = (digits.Length + 1) * 5 + 2; // 校验位及两根起止条。
                    bars = count * narrow + (count - 1) * (element.Ratio + 1);
                    break;
                case BarcodeType.Planet:
                    var planet = data.Replace("-", "");
                    Digits(planet);
                    if (planet.Length != 12 && planet.Length != 14)
                        throw new ArgumentException("PLANET 需要 12 或 14 位邮政数字，可用连字符分组。");
                    var checksum = planet.Sum(character => character - '0');
                    if (checksum % 10 != 0)
                        throw new ArgumentException("PLANET 校验位错误。");
                    var planetCount = planet.Length * 5 + 2;
                    bars = planetCount * narrow + (planetCount - 1) * (element.Ratio + 1);
                    data = planet;
                    break;
                case BarcodeType.IntelligentMail:
                    var intelligentMail = data.Replace("-", "");
                    Digits(intelligentMail);
                    if (intelligentMail.Length != 20 && intelligentMail.Length != 25 &&
                        intelligentMail.Length != 29 && intelligentMail.Length != 31)
                        throw new ArgumentException("Intelligent Mail 条码需要 20、25、29 或 31 位邮政数字，可用连字符分组。");
                    // IMb always encodes 65 four-state bars; reserve the upper ^BZ ratio.
                    bars = 65 * narrow + 64 * (element.Ratio + 1);
                    data = intelligentMail;
                    break;
                case BarcodeType.Fim:
                    if (data.Length != 1 || "ABCDE".IndexOf(data[0]) < 0) throw new ArgumentException("FIM 仅支持 A/B/C/D/E。");
                    bars = 17 * narrow;
                    break;
                default:
                    throw new ArgumentException("不支持的条码类型。");
            }
            if (element.QuietZone != null)
            {
                left = element.QuietZone.Left;
                right = element.QuietZone.Right;
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

    private static void TelepenAscii(string data)
    {
        if (data.Length == 0 || data.Length > 30 || data.Any(c => c < 32 || c > 127 || c == '"'))
            throw new ArgumentException("TELEPEN 需要 1–30 个可安全嵌入 TSPL 字段的 ASCII 字符（32–127，不含引号）。");
    }

    private static void Code49Ascii(string data)
    {
        if (data.Length == 0 || data.Length > 81 || data.Any(c => c < 32 || c > 127 || c == '"'))
            throw new ArgumentException("CODE49 需要 1–81 个可安全嵌入 TSPL 字段的 ASCII 字符（32–127，不含引号）。");
    }

    private static void Gs1DataBarExpandedAscii(string data)
    {
        if (data.Length < 2 || data.Length > 255)
            throw new ArgumentException("GS1 DataBar Expanded 需要 2–255 个字符。");
        if (data[0] == '(' || data[0] == '[')
        {
            var closing = data[0] == '(' ? ')' : ']';
            if (data.Length < 3 || data[1] < '0' || data[1] > '9' ||
                data[2] < '0' || data[2] > '9' ||
                data.IndexOf(closing) < 0)
                throw new ArgumentException("GS1 DataBar Expanded 以括号或方括号开头时，第二和第三个字符必须为数字且括号必须闭合。");
        }
        else if (data[0] < '0' || data[0] > '9' || data[1] < '0' || data[1] > '9')
            throw new ArgumentException("GS1 DataBar Expanded 数据必须以两个数字或括号和两个数字开头。");

        const string alphabet = "0123456789ABCDabcd !\"%$'()*+,-./:;<=>?_{}[]";
        if (data.Any(c => c != '\u001D' && alphabet.IndexOf(c) < 0))
            throw new ArgumentException("GS1 DataBar Expanded 包含不支持的字符；括号、方括号和 FNC1 由 ESC/POS Provider 转义，裸 { 按 Epson 字符集保留。");
        if (data.Count(c => c == '(' || c == ')' || c == '[' || c == ']' || c == '\u001D') + data.Length > 255)
            throw new ArgumentException("GS1 DataBar Expanded 转义后长度不能超过 255 字节。");
    }

    private static bool IsTsplVariableRatio(int narrow, int wide)
        => (long)wide == narrow || (long)wide == 2 * narrow ||
           (long)wide == 3 * narrow || 2L * wide == 5L * narrow ||
           3L * wide == 7L * narrow;

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
