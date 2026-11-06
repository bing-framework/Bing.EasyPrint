using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Templates
{
    /// <summary>
    /// 按 <see cref="BgFieldFormat"/> 把已绑定原始值转换为权威显示文本。
    /// </summary>
    /// <remarks>
    /// 转换顺序固定为：类型转换/枚举映射、格式化、补齐、前后缀。实际 null 在类型转换前单独处理。
    /// 格式化不执行脚本、表达式或运行机器当前时间/文化。
    /// </remarks>
    internal sealed class BgFieldFormatException : ArgumentException
    {
        internal BgFieldFormatException(string message, Exception inner) : base(message, inner) { }
    }

    internal static class BgFieldFormatter
    {
        private const int MaxPatternLength = 256;
        private const int MaxPrefixSuffixLength = 4096;
        private const int MaxMapEntries = 256;
        private const int MaxCultureNameLength = 64;

        internal static string Format(BgFieldFormat format, object value, long maxTextBytes,
            PrintStorageBudget storage)
        {
            try { return FormatCore(format, value, maxTextBytes, storage); }
            catch (BgFieldFormatException) { throw; }
            catch (ArgumentException error) { throw new BgFieldFormatException(error.Message, error); }
        }

        private static string FormatCore(BgFieldFormat format, object value, long maxTextBytes,
            PrintStorageBudget storage)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            ValidateConfiguration(format, maxTextBytes);
            var culture = ResolveCulture(format.Culture);
            if (format.Kind == BgFieldFormatKind.Number || format.Kind == BgFieldFormatKind.Currency)
                ValidateNumberPattern(format.Pattern);
            if (format.Kind == BgFieldFormatKind.Date)
                ValidateDateInputFormat(format.InputFormat);

            var maxChars = maxTextBytes == long.MaxValue ? 1_000_000L : Math.Max(0L, maxTextBytes / 2L);
            if (maxChars == 0 && value != null && !(value is string text && text.Length == 0))
                throw new ArgumentException("字段格式输出超过当前文本预算。");

            var coreEstimate = EstimateCore(format, value, culture);
            var padLength = format.PadLength ?? 0;
            var estimated = checked(Math.Max((long)padLength, coreEstimate) +
                (format.Prefix?.Length ?? 0) + (format.Suffix?.Length ?? 0));
            if (estimated > maxChars)
                throw new ArgumentException("字段格式输出超过当前文本预算。");
            if (storage != null)
                PrintStorageBudget.Reserve(checked(estimated * 2L + 128L), "Template.FieldFormat", storage);
            if (format.Kind == BgFieldFormatKind.Date)
                ValidateDateFormatPattern(format.Pattern, culture);

            string core;
            if (value == null)
            {
                core = format.NullText ?? string.Empty;
            }
            else
            {
                switch (format.Kind)
                {
                    case BgFieldFormatKind.Text:
                        core = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                        break;
                    case BgFieldFormatKind.Enum:
                        core = MapValue(format, value);
                        break;
                    case BgFieldFormatKind.Number:
                    case BgFieldFormatKind.Currency:
                        core = FormatDecimal(format, value, culture);
                        break;
                    case BgFieldFormatKind.Date:
                        core = FormatDate(format, value, culture);
                        break;
                    default:
                        throw new ArgumentException("字段格式类型无效。", nameof(format));
                }
            }

            if (core.Length > coreEstimate)
                throw new ArgumentException("字段格式输出长度超过预检上限。");
            var padded = padLength > 0 ? Pad(core, padLength, format.PadCharacter) : core;
            var result = (format.Prefix ?? string.Empty) + padded + (format.Suffix ?? string.Empty);
            if (result.Length > maxChars)
                throw new ArgumentException("字段格式输出超过当前文本预算。");
            return result;
        }

        private static long EstimateCore(BgFieldFormat format, object value, CultureInfo culture)
        {
            if (value == null) return format.NullText?.Length ?? 0;
            return format.Kind switch
            {
                BgFieldFormatKind.Text => EstimateText(value),
                BgFieldFormatKind.Enum => MapValue(format, value).Length,
                BgFieldFormatKind.Number or BgFieldFormatKind.Currency => EstimateNumber(format, value, culture),
                BgFieldFormatKind.Date => EstimateDate(format.Pattern, culture),
                _ => throw new ArgumentException("字段格式类型无效。", nameof(format))
            };
        }

        private static long EstimateText(object value)
        {
            if (value is string text) return text.Length;
            if (value is JsonRawNumber rawNumber) return rawNumber.Raw.Length;
            if (value is bool) return 5;
            if (value is char) return 1;
            if (value is decimal || value is double || value is float) return 32;
            if (value is DateTime || value is DateTimeOffset) return 64;
            if (value is TimeSpan) return 32;
            if (value is Guid) return 36;
            if (value is byte) return 3;
            if (value is sbyte) return 4;
            if (value is ushort) return 5;
            if (value is short) return 6;
            if (value is uint) return 10;
            if (value is int) return 11;
            if (value is ulong) return 20;
            if (value is long) return 20;
            if (value.GetType().IsEnum)
                return Enum.GetName(value.GetType(), value)?.Length ?? 20;
            throw new ArgumentException("字段格式 Text 只支持标量值。");
        }

        private static long EstimateNumber(BgFieldFormat format, object value, CultureInfo culture)
        {
            var number = ParseDecimal(value, culture);
            var integerDigits = IntegerDigits(number);
            var pattern = format.Pattern;
            if (IsStandardNumberPattern(pattern))
            {
                var formatCode = char.ToUpperInvariant(pattern[0]);
                var precision = pattern.Length == 1 ? -1 : int.Parse(pattern.Substring(1), CultureInfo.InvariantCulture);
                if (precision < 0)
                {
                    precision = formatCode == 'C' ? culture.NumberFormat.CurrencyDecimalDigits
                        : formatCode == 'N' ? culture.NumberFormat.NumberDecimalDigits
                        : formatCode == 'F' ? 2 : formatCode == 'P' ? culture.NumberFormat.PercentDecimalDigits : 0;
                }
                var group = (integerDigits - 1) / 3;
                return formatCode switch
                {
                    'C' => checked(1L + integerDigits + group + 1 + precision +
                        culture.NumberFormat.CurrencySymbol.Length + 8),
                    'N' => checked(1L + integerDigits + group + 1 + precision + 8),
                    'F' => checked(1L + integerDigits + 1 + precision + 8),
                    'P' => checked(1L + integerDigits + 2 + group + 1 + precision + 8),
                    'E' => checked(1L + 1 + 1 + precision + 12),
                    'G' => 45,
                    'D' => checked(Math.Max(integerDigits, Math.Max(0, precision)) + 2L),
                    'X' => 34,
                    'R' => 32,
                    _ => checked(pattern.Length + 64L)
                };
            }
            var multiplier = pattern.IndexOf('%') >= 0 || pattern.IndexOf('‰') >= 0 ? 2 : 0;
            if (pattern.IndexOf('E') >= 0 || pattern.IndexOf('e') >= 0)
                return checked(pattern.Length + 64L);
            var sections = pattern.Split(';');
            var bound = 0L;
            foreach (var section in sections)
            {
                var info = NumberSectionInfo(section);
                var sectionInteger = Math.Max(integerDigits + multiplier, info.IntegerPlaceholders);
                var sectionBound = checked(sectionInteger + (info.HasDecimal ? 1L : 0L) +
                    info.FractionPlaceholders + info.Literals + sectionInteger / 3 + 16L);
                if (sectionBound > bound) bound = sectionBound;
            }
            return Math.Max(bound, 16L);
        }

        private static NumberSectionInfoView NumberSectionInfo(string section)
        {
            var info = new NumberSectionInfoView();
            var inQuote = false;
            for (var index = 0; index < section.Length; index++)
            {
                var character = section[index];
                if (character == '\\' && index + 1 < section.Length)
                {
                    info.Literals++;
                    index++;
                    continue;
                }
                if (character == '"' || character == '\'')
                {
                    inQuote = !inQuote;
                    continue;
                }
                if (inQuote)
                {
                    info.Literals++;
                    continue;
                }
                if (character == '.')
                {
                    info.HasDecimal = true;
                    continue;
                }
                if (character == ',')
                {
                    if (!info.HasDecimal) info.Literals++;
                    continue;
                }
                if (character == '0' || character == '#')
                {
                    if (info.HasDecimal) info.FractionPlaceholders++;
                    else info.IntegerPlaceholders++;
                    continue;
                }
                info.Literals++;
            }
            return info;
        }

        private static bool IsStandardNumberPattern(string pattern)
            => pattern.Length > 0 && "CcDdEeFfGgNnPpRrXx".IndexOf(pattern[0]) >= 0;

        private static void ValidateDateInputFormat(string inputFormat)
        {
            if (string.IsNullOrWhiteSpace(inputFormat)) return;
            var tokens = ScanDateTokens(inputFormat);
            if (IsStandardDateInput(inputFormat))
            {
                var code = inputFormat[0];
                if (!"dDfFgGoOrRsUu".Contains(code))
                    throw new ArgumentException("日期 InputFormat 必须包含年、月、日，不能使用系统当前年份补全。");
                return;
            }
            if (!tokens.Year || !tokens.Month || !tokens.Day)
                throw new ArgumentException("日期 InputFormat 必须包含年、月、日，不能使用系统当前年份补全。");
        }

        private static void ValidateDateFormatPattern(string pattern, CultureInfo culture)
        {
            if (IsStandardDateInput(pattern))
            {
                try { _ = DateTimeOffset.MinValue.ToString(pattern, culture); }
                catch (FormatException error) { throw new ArgumentException("字段格式 Pattern 不是有效日期格式。", error); }
                return;
            }
            if (!ScanDateTokens(pattern).Any)
                throw new ArgumentException("字段格式 Pattern 不是有效日期格式。");
            try { _ = DateTimeOffset.MinValue.ToString(pattern, culture); }
            catch (FormatException error) { throw new ArgumentException("字段格式 Pattern 不是有效日期格式。", error); }
        }

        private static bool IsStandardDateInput(string format)
            => format.Length == 1 && "dDfFgGmMoOrRsStuUyY".IndexOf(format[0]) >= 0;

        private static DateTokenInfo ScanDateTokens(string format)
        {
            var result = new DateTokenInfo();
            for (var index = 0; index < format.Length; index++)
            {
                var character = format[index];
                if (character == '\\')
                {
                    index++;
                    continue;
                }
                if (character == '"' || character == '\'')
                {
                    var quote = character;
                    while (++index < format.Length && format[index] != quote) { }
                    continue;
                }
                if (character == '%')
                {
                    if (++index < format.Length) RecordDateToken(result, format[index], 1);
                    continue;
                }
                var run = 1;
                while (index + run < format.Length && format[index + run] == character) run++;
                index += run - 1;
                RecordDateToken(result, character, run);
            }
            return result;
        }

        private static void RecordDateToken(DateTokenInfo result, char token, int run)
        {
            result.Any = true;
            // .NET 自定义日期格式区分大小写；ddd/dddd 是星期名称，不提供日号。
            if (token == 'y') result.Year = true;
            else if (token == 'M') result.Month = true;
            else if (token == 'd' && run <= 2) result.Day = true;
            else if (token == 'z' || token == 'K') result.Offset = true;
        }

        private static long EstimateDate(string pattern, CultureInfo culture)
        {
            if (IsStandardDateInput(pattern)) return pattern[0] == 'O' || pattern[0] == 'o' ? 40 : 64;
            var bound = 0L;
            var month = culture.DateTimeFormat.MonthNames.Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name.Length).DefaultIfEmpty(0).Max();
            var day = culture.DateTimeFormat.DayNames.Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name.Length).DefaultIfEmpty(0).Max();
            var designator = Math.Max(culture.DateTimeFormat.AMDesignator.Length,
                culture.DateTimeFormat.PMDesignator.Length);
            for (var index = 0; index < pattern.Length; index++)
            {
                var character = pattern[index];
                if (character == '\\')
                {
                    bound++;
                    index++;
                    continue;
                }
                if (character == '"' || character == '\'')
                {
                    var quote = character;
                    while (++index < pattern.Length && pattern[index] != quote) bound++;
                    continue;
                }
                var run = 1;
                while (index + run < pattern.Length && pattern[index + run] == character) run++;
                index += run - 1;
                switch (character)
                {
                    case 'y': bound += run <= 2 ? 2 : Math.Max(5, run); break;
                    case 'M': bound += run >= 4 ? month : Math.Min(4, run); break;
                    case 'd': bound += run >= 4 ? day : Math.Min(4, run); break;
                    case 'H': case 'h': case 'm': case 's': bound += Math.Min(2, run); break;
                    case 'f': case 'F': bound += run; break;
                    case 't': bound += designator; break;
                    case 'z': bound += run >= 3 ? 6 : 3; break;
                    case 'K': bound += 6; break;
                    default: bound += run; break;
                }
            }
            return checked(bound + 8L);
        }

        private static void ValidateNumberPattern(string pattern)
        {
            if (pattern.Length == 1) return;
            const string standard = "CcDdEeFfGgNnPpRrXx";
            if (standard.IndexOf(pattern[0]) >= 0)
            {
                var precisionText = pattern.Substring(1);
                if (precisionText.Length == 0) return;
                if (!precisionText.All(char.IsDigit) ||
                    !int.TryParse(precisionText, NumberStyles.None, CultureInfo.InvariantCulture, out var precision) ||
                    precision > 100)
                    throw new ArgumentException("数字格式精度必须是 0–100 的整数。");
                return;
            }
            const string allowed = "0#.,%‰Ee+-;'\"\\$ ";
            foreach (var character in pattern)
                if (allowed.IndexOf(character) < 0)
                    throw new ArgumentException("数字格式包含不支持的字符。");
        }

        private static void ValidateConfiguration(BgFieldFormat format, long maxTextBytes)
        {
            if (format.Pattern != null && format.Pattern.Length > MaxPatternLength)
                throw new ArgumentException("字段格式 Pattern 最长 256 个字符。");
            if (format.InputFormat != null && format.InputFormat.Length > MaxPatternLength)
                throw new ArgumentException("字段格式 InputFormat 最长 256 个字符。");
            if (format.Culture != null && format.Culture.Length > MaxCultureNameLength)
                throw new ArgumentException("字段格式 Culture 名称过长。");
            if ((format.Prefix?.Length ?? 0) > MaxPrefixSuffixLength ||
                (format.Suffix?.Length ?? 0) > MaxPrefixSuffixLength)
                throw new ArgumentException("字段格式前缀或后缀最长 4096 个字符。");
            if ((format.NullText?.Length ?? 0) > MaxPrefixSuffixLength)
                throw new ArgumentException("字段格式空值文本最长 4096 个字符。");
            if (format.PadCharacter == null || format.PadCharacter.Length != 1)
                throw new ArgumentException("字段格式补齐字符必须为单个字符。");
            if (format.PadLength < 0)
                throw new ArgumentOutOfRangeException(nameof(format.PadLength), "字段格式补齐长度不能为负数。");
            if (format.PadLength is int padLength && maxTextBytes != long.MaxValue &&
                padLength > maxTextBytes / 2L)
                throw new ArgumentException("字段格式补齐长度超过当前文本预算。");
            if (format.Kind == BgFieldFormatKind.Number || format.Kind == BgFieldFormatKind.Currency ||
                format.Kind == BgFieldFormatKind.Date)
            {
                if (string.IsNullOrWhiteSpace(format.Pattern))
                    throw new ArgumentException("字段格式必须为数字、货币或日期提供 Pattern。");
            }
            if (format.Kind == BgFieldFormatKind.Enum)
            {
                if (format.Map == null || format.Map.Count == 0)
                    throw new ArgumentException("枚举字段格式必须提供非空 Map。");
                if (format.Map.Count > MaxMapEntries)
                    throw new ArgumentException("枚举字段格式 Map 最多 256 项。");
                foreach (var pair in format.Map)
                {
                    if (pair.Key == null) throw new ArgumentException("枚举字段格式 Map 键不能为空。");
                    if ((pair.Value?.Length ?? 0) > MaxPrefixSuffixLength)
                        throw new ArgumentException("枚举字段格式 Map 文本过长。");
                }
            }
        }

        private static CultureInfo ResolveCulture(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return CultureInfo.InvariantCulture;
            try { return CultureInfo.GetCultureInfo(name); }
            catch (CultureNotFoundException error)
            {
                throw new ArgumentException("字段格式 Culture 不受支持。", nameof(name), error);
            }
        }

        private static string MapValue(BgFieldFormat format, object value)
        {
            var key = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (key == null || !format.Map.TryGetValue(key, out var mapped))
                throw new ArgumentException("枚举值没有匹配的格式映射。");
            return mapped ?? string.Empty;
        }

        private static decimal ParseDecimal(object value, CultureInfo culture)
        {
            try
            {
                return value switch
                {
                    decimal item => item,
                    byte item => item,
                    sbyte item => item,
                    short item => item,
                    ushort item => item,
                    int item => item,
                    uint item => item,
                    long item => item,
                    ulong item => item,
                    float item when !float.IsNaN(item) && !float.IsInfinity(item) => (decimal)item,
                    double item when !double.IsNaN(item) && !double.IsInfinity(item) => (decimal)item,
                    JsonRawNumber item => item.ToDecimal(),
                    string item when !string.IsNullOrWhiteSpace(item) => decimal.Parse(item,
                        NumberStyles.Number | NumberStyles.AllowExponent, culture),
                    _ => throw new ArgumentException("字段值不能转换为十进制数字。")
                };
            }
            catch (Exception error) when (error is FormatException || error is OverflowException ||
                                           error is InvalidCastException)
            {
                throw new ArgumentException("字段值不能转换为十进制数字。", error);
            }
        }

        private static string FormatDecimal(BgFieldFormat format, object value, CultureInfo culture)
        {
            try { return ParseDecimal(value, culture).ToString(format.Pattern, culture); }
            catch (FormatException error)
            {
                throw new ArgumentException("数字格式 Pattern 无效。", error);
            }
        }

        private static string FormatDate(BgFieldFormat format, object value, CultureInfo culture)
        {
            try
            {
                var offsetPattern = HasDateOffsetToken(format.Pattern);
                if (value is DateTimeOffset offset) return offset.ToString(format.Pattern, culture);
                if (value is DateTime dateTime)
                {
                    if (offsetPattern && dateTime.Kind == DateTimeKind.Unspecified)
                        throw new ArgumentException("缺少 offset 的日期不能输出时区占位符。");
                    return dateTime.ToString(format.Pattern, culture);
                }
                if (value is not string text) throw new ArgumentException("字段值不能转换为日期。");
                var parsed = ParseDate(text, format, culture);
                return parsed switch
                {
                    DateTimeOffset offsetValue => offsetValue.ToString(format.Pattern, culture),
                    DateTime dateTimeValue => dateTimeValue.Kind == DateTimeKind.Unspecified &&
                                              offsetPattern
                        ? throw new ArgumentException("缺少 offset 的日期不能输出时区占位符。")
                        : dateTimeValue.ToString(format.Pattern, culture),
                    _ => throw new ArgumentException("字段值不能转换为日期。")
                };
            }
            catch (FormatException error)
            {
                throw new ArgumentException("字段值不符合日期输入格式。", error);
            }
        }

        private static object ParseDate(string text, BgFieldFormat format, CultureInfo culture)
        {
            if (!string.IsNullOrWhiteSpace(format.InputFormat))
            {
                var offsetFormat = HasDateOffsetToken(format.InputFormat) || IsStandardOffsetInput(format.InputFormat);
                if (offsetFormat)
                {
                    if (!HasExplicitOffset(text))
                        throw new ArgumentException("日期输入缺少 offset，不能使用本机时区补全。");
                    if (DateTimeOffset.TryParseExact(text, format.InputFormat, culture, DateTimeStyles.None,
                            out var offset)) return offset;
                }
                else if (DateTime.TryParseExact(text, format.InputFormat, culture, DateTimeStyles.None,
                             out var date)) return date;
                throw new ArgumentException("字段值不符合日期输入格式。");
            }

            if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var dateOnly))
                return dateOnly;
            var localFormats = new[] { "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF" };
            if (DateTime.TryParseExact(text, localFormats, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var local))
                return local;
            var offsetFormats = new[]
            {
                "O", "yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
            };
            if (DateTimeOffset.TryParseExact(text, offsetFormats, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var withOffset))
                return withOffset;
            throw new ArgumentException("字段值必须是明确 ISO 日期或带 offset 的 ISO 日期时间。");
        }

        private static bool IsStandardOffsetInput(string format)
            => format.Length == 1 && (format[0] == 'O' || format[0] == 'o');

        private static bool HasDateOffsetToken(string format)
        {
            if (IsStandardOffsetInput(format)) return true;
            return ScanDateTokens(format).Offset;
        }

        private static bool HasExplicitOffset(string text)
        {
            if (text.EndsWith("Z", StringComparison.Ordinal) || text.EndsWith("z", StringComparison.Ordinal))
                return true;
            for (var index = Math.Max(0, text.Length - 6); index < text.Length; index++)
            {
                var sign = text[index];
                if (sign != '+' && sign != '-') continue;
                var rest = text.Length - index;
                if (rest == 6 && text[index + 3] == ':' &&
                    char.IsDigit(text[index + 1]) && char.IsDigit(text[index + 2]) &&
                    char.IsDigit(text[index + 4]) && char.IsDigit(text[index + 5])) return true;
                if (rest == 5 &&
                    char.IsDigit(text[index + 1]) && char.IsDigit(text[index + 2]) &&
                    char.IsDigit(text[index + 3]) && char.IsDigit(text[index + 4])) return true;
            }
            return false;
        }

        private static int IntegerDigits(decimal value)
        {
            value = decimal.Truncate(Math.Abs(value));
            if (value == 0) return 1;
            var digits = 0;
            while (value >= 1)
            {
                value /= 10;
                digits++;
            }
            return digits;
        }

        private static string Pad(string value, int length, string padCharacter)
        {
            if (value.Length >= length) return value;
            if (padCharacter == "0" && value.Length > 0 && (value[0] == '-' || value[0] == '+'))
                return value.Substring(0, 1) + value.Substring(1).PadLeft(length - 1, '0');
            return value.PadLeft(length, padCharacter[0]);
        }

        private sealed class DateTokenInfo
        {
            internal bool Any;
            internal bool Year;
            internal bool Month;
            internal bool Day;
            internal bool Offset;
        }

        private sealed class NumberSectionInfoView
        {
            internal bool HasDecimal;
            internal int IntegerPlaceholders;
            internal int FractionPlaceholders;
            internal int Literals;
        }
    }
}
