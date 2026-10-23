using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Bing.EasyPrint.Protocols;

/// <summary>
/// 固定版本 GS1 Barcode Syntax Dictionary 的共享 AI 字典与逐字段校验。
/// </summary>
/// <remarks>
/// 数据来源：https://github.com/gs1/gs1-syntax-dictionary 提交
/// d1702ffb2fa5060f8460f7d581b01e377deaa1d6（2025-02-09，Apache-2.0）。
/// 该版本与 Zint 2.15.0（提交 4896136bdbebb1a0908a5cc419513efa5bf4bf70）内置的
/// <c>gs1_lint.h</c> 生成源一致。字典文件以嵌入资源随程序集发布；更新步骤见
/// docs/gs1-ai-dictionary.md。
/// 本类型只实现该版本字典声明的通用 AI 语法、字段长度/字符集/Linter 校验；
/// 具体码制容量、跨码制限制和业务语义仍由对应编码器校验，不在此处放宽或推断。
/// </remarks>
internal static class Gs1SyntaxDictionary
{
    /// <summary>固定字典的数据来源提交。</summary>
    internal const string DictionaryCommit = "d1702ffb2fa5060f8460f7d581b01e377deaa1d6";

    /// <summary>固定字典文件的 SHA-256 小写十六进制值，用于一致性检查。</summary>
    internal const string DictionarySha256 = "8d01d6596c77dbbfbea25939b2ff4558854d36a4d528dc5195f692a4539e8921";

    private const string ResourceSuffix =
        ".gs1-syntax-dictionary-d1702ffb2fa5060f8460f7d581b01e377deaa1d6.txt";

    private const string Cset82Characters =
        "!\"%&'()*+,-./0123456789:;<=>?ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";

    private static readonly string[] KnownLinters =
    {
        "couponcode", "couponposoffer", "csum", "csumalpha", "hasnondigit", "hh", "hhmi", "hyphen",
        "iban", "importeridx", "iso3166", "iso3166999", "iso3166alpha2", "iso4217", "iso5218", "key",
        "keyoff1", "latitude", "longitude", "mediatype", "mi", "nozeroprefix", "nonzero", "packagetype",
        "pcenc", "pieceoftotal", "posinseqslash", "ss", "winding", "yesno", "yymmd0", "yymmdd",
        "yyyymmd0", "yyyymmdd", "zero",
    };

    private static readonly Lazy<Dictionary<string, Gs1SyntaxAiDefinition>> Definitions =
        new Lazy<Dictionary<string, Gs1SyntaxAiDefinition>>(LoadDefinitions);

    private static readonly byte[] Iso3166NumericCodes =
    {
        0x10, 0x15, 0x11, 0x91, 0x11, 0x11, 0x1D, 0x11,
        0x51, 0x15, 0x50, 0x14, 0x11, 0x11, 0x11, 0x11,
        0x10, 0x11, 0x11, 0x51, 0x44, 0xC4, 0x14, 0x91,
        0x11, 0x18, 0x51, 0x44, 0x84, 0xC7, 0x44, 0x45,
        0x54, 0x54, 0x18, 0x00, 0x11, 0x11, 0x11, 0x11,
        0x11, 0x51, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11,
        0x11, 0x41, 0x11, 0x45, 0x46, 0x54, 0x44, 0x45,
        0x44, 0x44, 0x44, 0x44, 0x11, 0x10, 0x1D, 0x11,
        0x11, 0x11, 0xE9, 0x10, 0x10, 0x44, 0x44, 0x44,
        0xB4, 0x87, 0x40, 0x11, 0x11, 0x11, 0x45, 0x44,
        0x4C, 0x50, 0xD8, 0x44, 0x44, 0x44, 0x45, 0xC0,
        0x47, 0x10, 0x10, 0x13, 0x10, 0x11, 0x11, 0x15,
        0x11, 0x11, 0x11, 0x59, 0x91, 0x00, 0x04, 0x84,
        0x07, 0x01, 0x44, 0x54, 0x00, 0x10, 0x84, 0x40,
    };

    private static readonly byte[] Iso3166Alpha2Codes =
    {
        0x78, 0x59, 0xDF, 0xEE, 0xEF, 0xBD, 0xDD, 0xDE,
        0x27, 0x3F, 0x84, 0x15, 0x80, 0xD4, 0x00, 0x0E,
        0x00, 0x5C, 0x09, 0xB0, 0x9F, 0xFB, 0x15, 0x00,
        0x8D, 0x06, 0x18, 0x78, 0x0F, 0x40, 0x40, 0x03,
        0x00, 0x1D, 0x2B, 0xF4, 0x41, 0x81, 0x4F, 0xFD,
        0xFC, 0xFF, 0xD7, 0x25, 0x4B, 0x08, 0x00, 0x01,
        0x40, 0x3C, 0x8F, 0x53, 0x01, 0x00, 0x00, 0x40,
        0x00, 0x51, 0xF1, 0xFD, 0xE7, 0x3A, 0xBB, 0x9F,
        0x9A, 0x41, 0x10, 0x04, 0x57, 0x85, 0x40, 0x00,
        0x02, 0x40, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00,
        0x08, 0x04, 0x40, 0x00, 0x01,
    };

    private static readonly byte[] Iso4217NumericCodes =
    {
        0x00, 0x11, 0x00, 0x00, 0x11, 0x10, 0x1D, 0x10,
        0x11, 0x01, 0x10, 0x04, 0x01, 0x11, 0x10, 0x10,
        0x10, 0x01, 0x01, 0x11, 0x00, 0x44, 0x00, 0x10,
        0x01, 0x08, 0x41, 0x40, 0x40, 0x41, 0x04, 0x00,
        0x40, 0x40, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00,
        0x11, 0x11, 0x10, 0x11, 0x11, 0x11, 0x01, 0x01,
        0x10, 0x41, 0x11, 0x45, 0x46, 0x44, 0x04, 0x40,
        0x40, 0x44, 0x00, 0x00, 0x11, 0x00, 0x05, 0x01,
        0x11, 0x10, 0x30, 0x00, 0x10, 0x44, 0x40, 0x00,
        0x04, 0x44, 0x40, 0x11, 0x01, 0x00, 0x00, 0x04,
        0x48, 0x40, 0x00, 0x00, 0x00, 0x04, 0x04, 0x40,
        0x45, 0x00, 0x00, 0x01, 0x00, 0x10, 0x11, 0x11,
        0x00, 0x11, 0x11, 0x00, 0x81, 0x00, 0x04, 0x04,
        0x04, 0x01, 0x00, 0x14, 0x00, 0x00, 0x44, 0x00,
        0x20, 0x00, 0x00, 0xF0, 0x67, 0xB5, 0xFD, 0xFB,
        0xBF, 0xBF, 0x3F, 0x47, 0xA4,
    };

    private static readonly string[] PackageTypes2 =
    {
        "1A", "1B", "1D", "1F", "1G", "1W", "2C", "3A", "3H", "43", "44", "4A", "4B", "4C", "4D",
        "4F", "4G", "4H", "5H", "5L", "5M", "6H", "6P", "7A", "7B", "8A", "8B", "8C", "AA", "AB",
        "AC", "AD", "AF", "AG", "AH", "AI", "AJ", "AL", "AM", "AP", "AT", "AV", "B4", "BB", "BC",
        "BD", "BE", "BF", "BG", "BH", "BI", "BJ", "BK", "BL", "BM", "BN", "BO", "BP", "BQ", "BR",
        "BS", "BT", "BU", "BV", "BW", "BX", "BY", "BZ", "CA", "CB", "CC", "CD", "CE", "CF", "CG",
        "CH", "CI", "CJ", "CK", "CL", "CM", "CN", "CO", "CP", "CQ", "CR", "CS", "CT", "CU", "CV",
        "CW", "CX", "CY", "CZ", "DA", "DB", "DC", "DG", "DH", "DI", "DJ", "DK", "DL", "DM", "DN",
        "DP", "DR", "DS", "DT", "DU", "DV", "DW", "DX", "DY", "E1", "E2", "E3", "EC", "ED", "EE",
        "EF", "EG", "EH", "EI", "EN", "FB", "FC", "FD", "FE", "FI", "FL", "FO", "FP", "FR", "FT",
        "FW", "FX", "GB", "GI", "GL", "GR", "GU", "GY", "GZ", "HA", "HB", "HC", "HG", "HN", "HR",
        "IA", "IB", "IC", "ID", "IE", "IF", "IG", "IH", "IK", "IL", "IN", "IZ", "JB", "JC", "JG",
        "JR", "JT", "JY", "KG", "KI", "LE", "LG", "LT", "LU", "LV", "LZ", "MA", "MB", "MC", "ME",
        "MR", "MS", "MT", "MW", "MX", "NA", "NE", "NF", "NG", "NS", "NT", "NU", "NV", "OA", "OB",
        "OC", "OD", "OE", "OF", "OK", "OT", "OU", "P2", "PA", "PB", "PC", "PD", "PE", "PF", "PG",
        "PH", "PI", "PJ", "PK", "PL", "PN", "PO", "PP", "PR", "PT", "PU", "PV", "PX", "PY", "PZ",
        "QA", "QB", "QC", "QD", "QF", "QG", "QH", "QJ", "QK", "QL", "QM", "QN", "QP", "QQ", "QR",
        "QS", "RD", "RG", "RJ", "RK", "RL", "RO", "RT", "RZ", "S1", "SA", "SB", "SC", "SD", "SE",
        "SH", "SI", "SK", "SL", "SM", "SO", "SP", "SS", "ST", "SU", "SV", "SW", "SX", "SY", "SZ",
        "T1", "TB", "TC", "TD", "TE", "TG", "TI", "TK", "TL", "TN", "TO", "TR", "TS", "TT", "TU",
        "TV", "TW", "TY", "TZ", "UC", "UN", "VA", "VG", "VI", "VK", "VL", "VN", "VO", "VP", "VQ",
        "VR", "VS", "VY", "WA", "WB", "WC", "WD", "WF", "WG", "WH", "WJ", "WK", "WL", "WM", "WN",
        "WP", "WQ", "WR", "WS", "WT", "WU", "WV", "WW", "WX", "WY", "WZ", "X3", "XA", "XB", "XC",
        "XD", "XF", "XG", "XH", "XJ", "XK", "YA", "YB", "YC", "YD", "YF", "YG", "YH", "YJ", "YK",
        "YL", "YM", "YN", "YP", "YQ", "YR", "YS", "YT", "YV", "YW", "YX", "YY", "YZ", "ZA", "ZB",
        "ZC", "ZD", "ZF", "ZG", "ZH", "ZJ", "ZK", "ZL", "ZM", "ZN", "ZP", "ZQ", "ZR", "ZS", "ZT",
        "ZU", "ZV", "ZW", "ZX", "ZY", "ZZ",
    };

    private static readonly string[] PackageTypes3 =
    {
        "200", "201", "202", "203", "204", "205", "206", "210", "211", "212",
        "APE", "BGE", "BME", "BRI", "CBL", "CCE", "DPE", "FOB", "FPE", "LAB",
        "MPE", "OPE", "PAE", "PLP", "POP", "PPE", "PUE", "RB1", "RB2", "RB3",
        "RCB", "SEC", "STL", "TEV", "THE", "TRE", "TTE", "TWE", "UUE", "WRP",
        "X11", "X12", "X15", "X16", "X17", "X18", "X19", "X20",
    };

    private static readonly HashSet<string> PackageTypeSet = CreatePackageTypeSet();

    /// <summary>获取固定版本字典中的 AI 数量。</summary>
    internal static int Count => Definitions.Value.Count;

    /// <summary>判断 AI 是否在固定版本字典中定义。</summary>
    /// <param name="ai">2–4 位 ASCII 数字 AI。</param>
    /// <param name="definition">匹配的定义；未定义时为 <see langword="null"/>。</param>
    /// <returns>字典包含该 AI 时返回 <see langword="true"/>。</returns>
    internal static bool TryGet(string ai, out Gs1SyntaxAiDefinition definition)
        => Definitions.Value.TryGetValue(ai, out definition);

    /// <summary>按固定版本字典校验 AI 值并给出准确诊断。</summary>
    /// <param name="ai">字典中的 AI。</param>
    /// <param name="value">不含 AI 和 FNC1 的字段值。</param>
    /// <param name="name">异常中的参数名称。</param>
    /// <exception cref="ArgumentException">长度、字符集、字段结构或 Linter 校验失败。</exception>
    internal static void Validate(string ai, string value, string name)
    {
        if (!TryGet(ai, out var definition))
            throw new ArgumentException($"GS1 AI {ai} 不在固定版本 {DictionaryCommit} 的 GS1 Syntax Dictionary 中。", name);
        definition.Validate(value, name);
    }

    /// <summary>获取固定版本字典声明的全部 Linter 名称，用于一致性测试。</summary>
    internal static IReadOnlyList<string> SupportedLinters => KnownLinters;

    private static HashSet<string> CreatePackageTypeSet()
    {
        var set = new HashSet<string>(StringComparer.Ordinal) { "8", "9" };
        foreach (var value in PackageTypes2) set.Add(value);
        foreach (var value in PackageTypes3) set.Add(value);
        return set;
    }

    private static Dictionary<string, Gs1SyntaxAiDefinition> LoadDefinitions()
    {
        var assembly = typeof(Gs1SyntaxDictionary).GetTypeInfo().Assembly;
        string resourceName = null;
        foreach (var name in assembly.GetManifestResourceNames())
            if (name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            {
                resourceName = name;
                break;
            }
        if (resourceName == null)
            throw new InvalidOperationException("未找到嵌入的 GS1 Syntax Dictionary 资源。");

        string text;
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null) throw new InvalidOperationException("无法读取嵌入的 GS1 Syntax Dictionary 资源。");
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                text = reader.ReadToEnd();
        }

        var result = new Dictionary<string, Gs1SyntaxAiDefinition>(StringComparer.Ordinal);
        foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var hash = line.IndexOf('#');
            var body = (hash >= 0 ? line.Substring(0, hash) : line).Trim();
            var title = hash >= 0 ? line.Substring(hash + 1).Trim() : string.Empty;
            var tokens = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) continue;
            if (!LooksLikeAi(tokens[0])) continue;

            var hasFlags = !LooksLikeComponent(tokens[1]);
            var flags = hasFlags ? tokens[1] : string.Empty;
            var components = new List<Gs1SyntaxComponent>();
            var attributeIndex = hasFlags ? 2 : 1;
            for (; attributeIndex < tokens.Length; attributeIndex++)
            {
                if (!TryParseComponent(tokens[attributeIndex], out var component)) break;
                components.Add(component);
            }
            if (components.Count == 0) continue;

            var attributes = attributeIndex < tokens.Length
                ? string.Join(" ", tokens, attributeIndex, tokens.Length - attributeIndex)
                : string.Empty;
            var predefined = flags.IndexOf('*') >= 0;
            var expandedAis = ExpandAi(tokens[0]);
            foreach (var ai in expandedAis)
            {
                if (result.ContainsKey(ai))
                    throw new InvalidOperationException($"GS1 Syntax Dictionary 包含重复 AI {ai}。");
                result.Add(ai, new Gs1SyntaxAiDefinition(ai, predefined, components, attributes, title, body));
            }
        }

        if (result.Count == 0)
            throw new InvalidOperationException("GS1 Syntax Dictionary 未解析出任何 AI。");
        return result;
    }

    private static bool LooksLikeAi(string token)
    {
        var dash = token.IndexOf('-');
        var first = dash < 0 ? token : token.Substring(0, dash);
        if (first.Length < 2 || first.Length > 4) return false;
        for (var index = 0; index < first.Length; index++)
            if (first[index] < '0' || first[index] > '9') return false;
        if (dash < 0) return true;
        var second = token.Substring(dash + 1);
        if (second.Length != first.Length) return false;
        for (var index = 0; index < second.Length; index++)
            if (second[index] < '0' || second[index] > '9') return false;
        return true;
    }

    private static bool LooksLikeComponent(string token)
        => token.Length > 0 && (token[0] == 'N' || token[0] == 'X' || token[0] == 'Y' ||
                                token[0] == 'Z' || token[0] == '[');

    private static IEnumerable<string> ExpandAi(string token)
    {
        var dash = token.IndexOf('-');
        if (dash < 0)
        {
            yield return token;
            yield break;
        }
        var first = token.Substring(0, dash);
        var second = token.Substring(dash + 1);
        var width = first.Length;
        var start = int.Parse(first, CultureInfo.InvariantCulture);
        var end = int.Parse(second, CultureInfo.InvariantCulture);
        if (end < start) throw new InvalidOperationException($"GS1 Syntax Dictionary AI 范围无效：{token}。");
        for (var value = start; value <= end; value++)
            yield return value.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
    }

    private static bool TryParseComponent(string token, out Gs1SyntaxComponent component)
    {
        component = null;
        var optional = token.Length > 0 && token[0] == '[';
        var parts = token.Split(',');
        var spec = parts[0].Trim('[', ']');
        if (spec.Length < 2) return false;
        var type = spec[0];
        if (type != 'N' && type != 'X' && type != 'Y' && type != 'Z') return false;
        int minimum;
        int maximum;
        var length = spec.Substring(1);
        var range = length.IndexOf("..", StringComparison.Ordinal);
        if (range >= 0)
        {
            minimum = 1;
            if (!int.TryParse(length.Substring(range + 2), NumberStyles.None, CultureInfo.InvariantCulture, out maximum))
                return false;
        }
        else
        {
            if (!int.TryParse(length, NumberStyles.None, CultureInfo.InvariantCulture, out maximum)) return false;
            minimum = maximum;
        }
        if (maximum < minimum || maximum <= 0) return false;

        var linters = new List<string>();
        for (var index = 1; index < parts.Length; index++)
        {
            var linter = parts[index].Trim('[', ']');
            if (linter.Length == 0) continue;
            if (Array.IndexOf(KnownLinters, linter) < 0)
                throw new InvalidOperationException(
                    $"固定 GS1 Syntax Dictionary 使用未实现的 Linter {linter}（{token}）。");
            linters.Add(linter);
        }
        component = new Gs1SyntaxComponent(type, minimum, maximum, optional, linters.ToArray());
        return true;
    }

    internal static bool IsCset82(char value) => Cset82Characters.IndexOf(value) >= 0;

    internal static int Cset82Value(char value) => Cset82Characters.IndexOf(value);

    internal static bool IsDigits(string value, int start, int count)
    {
        if (start < 0 || count < 0 || start + count > value.Length) return false;
        for (var index = start; index < start + count; index++)
            if (value[index] < '0' || value[index] > '9') return false;
        return true;
    }

    internal static bool HasValidNumericCheckDigit(string value)
    {
        if (!IsDigits(value, 0, value.Length) || value.Length < 2) return false;
        var factor = (value.Length & 1) == 1 ? 1 : 3;
        var sum = 0;
        for (var index = 0; index < value.Length - 1; index++)
        {
            sum += (value[index] - '0') * factor;
            factor ^= 2;
        }
        var check = (10 - sum % 10) % 10;
        return check == value[value.Length - 1] - '0';
    }

    internal static bool IsIso3166Numeric(int code)
    {
        var index = code >> 3;
        return index >= 0 && index < Iso3166NumericCodes.Length &&
               (Iso3166NumericCodes[index] & (1 << (code & 7))) != 0;
    }

    internal static bool IsIso3166Alpha2(string value)
    {
        if (value == null || value.Length < 2 || value[0] < 'A' || value[0] > 'Z' ||
            value[1] < 'A' || value[1] > 'Z') return false;
        var index = (value[0] - 'A') * 26 + value[1] - 'A';
        return index >= 0 && index < Iso3166Alpha2Codes.Length * 8 &&
               (Iso3166Alpha2Codes[index >> 3] & (1 << (index & 7))) != 0;
    }

    internal static bool IsIso4217Numeric(int code)
    {
        var index = code >> 3;
        return index >= 0 && index < Iso4217NumericCodes.Length &&
               (Iso4217NumericCodes[index] & (1 << (code & 7))) != 0;
    }

    internal static bool IsPackageType(string value) => PackageTypeSet.Contains(value);
}
