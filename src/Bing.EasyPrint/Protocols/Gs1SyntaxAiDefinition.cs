using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bing.EasyPrint.Protocols;

/// <summary>
/// 固定版本 GS1 Syntax Dictionary 中的单个 AI 定义。
/// </summary>
internal sealed class Gs1SyntaxAiDefinition
{
    internal Gs1SyntaxAiDefinition(string ai, bool predefinedLength,
        IReadOnlyList<Gs1SyntaxComponent> components, string attributes, string title, string specification)
    {
        Ai = ai;
        PredefinedLength = predefinedLength;
        Components = components;
        Attributes = attributes;
        RequirementRules = Gs1AssociationRuleParser.Parse(attributes, "req", ai);
        ExclusionRules = Gs1AssociationRuleParser.Parse(attributes, "ex", ai);
        Title = title;
        Specification = specification;
        var minimum = 0;
        var maximum = 0;
        foreach (var component in components)
        {
            if (!component.Optional) minimum += component.MinimumLength;
            maximum += component.MaximumLength;
        }
        MinimumLength = minimum;
        MaximumLength = maximum;
    }

    /// <summary>获取规范化 AI。</summary>
    internal string Ai { get; }

    /// <summary>获取是否为预定义长度（无需 FNC1 分隔）。</summary>
    internal bool PredefinedLength { get; }

    /// <summary>获取字段是否需要 FNC1 分隔。</summary>
    /// <remarks>
    /// 固定字典的 '*' 表示“预定义长度、无需 FNC1”；没有 '*' 的 AI 即使字段总长度固定，
    /// 在后续还有元素时仍必须插入 FNC1。该规则与 GS1 Syntax Dictionary 第 41 行一致。
    /// </remarks>
    internal bool RequiresSeparator => !PredefinedLength;

    /// <summary>获取字段总最小长度。</summary>
    internal int MinimumLength { get; }

    /// <summary>获取字段总最大长度。</summary>
    internal int MaximumLength { get; }

    /// <summary>获取按顺序排列的字段组件。</summary>
    internal IReadOnlyList<Gs1SyntaxComponent> Components { get; }

    /// <summary>获取字典中的关联属性原文，如 req/ex/dlpkey。</summary>
    internal string Attributes { get; }

    /// <summary>获取字典 req 属性的必需关联规则。</summary>
    internal IReadOnlyList<Gs1AssociationRule> RequirementRules { get; }

    /// <summary>获取字典 ex 属性的互斥关联规则。</summary>
    internal IReadOnlyList<Gs1AssociationRule> ExclusionRules { get; }

    /// <summary>获取字典标题。</summary>
    internal string Title { get; }

    /// <summary>获取字典原始规格。</summary>
    internal string Specification { get; }

    /// <summary>按固定版本字典校验字段值。</summary>
    /// <param name="value">不含 AI 和 FNC1 的字段值。</param>
    /// <param name="name">异常中的参数名称。</param>
    /// <exception cref="ArgumentException">长度、字符集、字段结构或 Linter 校验失败。</exception>
    internal void Validate(string value, string name)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value.Length < MinimumLength || value.Length > MaximumLength)
        {
            var expected = MinimumLength == MaximumLength
                ? $"{MinimumLength} 位"
                : $"{MinimumLength}–{MaximumLength} 位";
            throw Error($"GS1 AI {Ai} 的数据长度必须为 {expected}（{Title}）。", name);
        }

        var offset = 0;
        foreach (var component in Components)
        {
            var remaining = value.Length - offset;
            if (remaining == 0)
            {
                if (component.Optional) continue;
                throw Error($"GS1 AI {Ai} 缺少字段 {component.Type} 的数据。", name);
            }
            if (remaining < component.MinimumLength)
                throw Error(
                    $"GS1 AI {Ai} 的字段 {component.Type} 至少需要 {component.MinimumLength} 个字符，实际剩余 {remaining} 个。",
                    name);

            var length = component.MinimumLength == component.MaximumLength
                ? component.MinimumLength
                : Math.Min(remaining, component.MaximumLength);
            if (length < component.MinimumLength)
                throw Error(
                    $"GS1 AI {Ai} 的字段 {component.Type} 至少需要 {component.MinimumLength} 个字符。", name);
            var segment = value.Substring(offset, length);
            ValidateCharacterSet(component, segment, offset, name);
            foreach (var linter in component.Linters)
                ValidateLinter(linter, segment, offset, name);
            offset += length;
        }
    }

    private void ValidateCharacterSet(Gs1SyntaxComponent component, string value, int offset, string name)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var valid = component.Type == 'N'
                ? character >= '0' && character <= '9'
                : component.Type == 'X'
                    ? Gs1SyntaxDictionary.IsCset82(character)
                    : component.Type == 'Y'
                        ? IsCset39(character)
                        : IsBase64Url(value, index);
            if (valid) continue;
            throw Error(
                $"GS1 AI {Ai} 的字段 {component.Type} 第 {offset + index + 1} 个字符 '{character}' 不在允许的字符集中。",
                name);
        }
    }

    private void ValidateLinter(string linter, string value, int offset, string name)
    {
        switch (linter)
        {
            case "csum":
                if (!Gs1SyntaxDictionary.HasValidNumericCheckDigit(value))
                    throw Error($"GS1 AI {Ai} 的校验位无效（期望值按 GS1 模 10 校验计算）。", name);
                break;
            case "csumalpha":
                if (!HasValidAlphaCheck(value))
                    throw Error($"GS1 AI {Ai} 的字母数字校验字符无效。", name);
                break;
            case "key":
                if (value.Length < 4 || !Gs1SyntaxDictionary.IsDigits(value, 0, 4))
                    throw Error($"GS1 AI {Ai} 的 GS1 公司前缀至少为 4 位数字。", name);
                break;
            case "keyoff1":
                if (value.Length < 5 || !Gs1SyntaxDictionary.IsDigits(value, 1, 4))
                    throw Error($"GS1 AI {Ai} 从第 2 位起的 GS1 公司前缀至少为 4 位数字。", name);
                break;
            case "iso3166":
                ValidateIso3166(value, false, name);
                break;
            case "iso3166999":
                ValidateIso3166(value, true, name);
                break;
            case "iso3166alpha2":
                if (!Gs1SyntaxDictionary.IsIso3166Alpha2(value))
                    throw Error($"GS1 AI {Ai} 必须为有效的 ISO 3166-1 二位字母国家代码。", name);
                break;
            case "iso4217":
                ValidateIso4217(value, name);
                break;
            case "iso5218":
                if (value.Length != 1 || "0129".IndexOf(value[0]) < 0)
                    throw Error($"GS1 AI {Ai} 的性别代码仅允许 0、1、2 或 9。", name);
                break;
            case "yesno":
                if (value.Length != 1 || value[0] != '0' && value[0] != '1')
                    throw Error($"GS1 AI {Ai} 只允许 0 或 1。", name);
                break;
            case "nonzero":
                if (IsAllZero(value)) throw Error($"GS1 AI {Ai} 不允许全零。", name);
                break;
            case "zero":
                if (value.Length != 1 || value[0] != '0')
                    throw Error($"GS1 AI {Ai} 必须为单个 0。", name);
                break;
            case "winding":
                if (value.Length != 1 || "019".IndexOf(value[0]) < 0)
                    throw Error($"GS1 AI {Ai} 的卷绕方向仅允许 0、1 或 9。", name);
                break;
            case "hyphen":
                for (var index = 0; index < value.Length; index++)
                    if (value[index] != '-')
                        throw Error($"GS1 AI {Ai} 的负温度指示符只允许 '-'。", name);
                break;
            case "pieceoftotal":
                ValidatePieceOfTotal(value, name);
                break;
            case "posinseqslash":
                ValidatePositionInSequence(value, name);
                break;
            case "hasnondigit":
                if (AllDigits(value)) throw Error($"GS1 AI {Ai} 至少需要包含一个非数字字符。", name);
                break;
            case "nozeroprefix":
                if (value.Length > 1 && value[0] == '0')
                    throw Error($"GS1 AI {Ai} 的序列号不能以 0 开头，除非整个序列号就是单个 0。", name);
                break;
            case "mediatype":
                ValidateMediaType(value, name);
                break;
            case "importeridx":
                if (value.Length != 1 || !IsImporterIndex(value[0]))
                    throw Error($"GS1 AI {Ai} 的进口商索引字符无效。", name);
                break;
            case "latitude":
                ValidateCoordinate(value, 1800000000L, "纬度", name);
                break;
            case "longitude":
                ValidateCoordinate(value, 3600000000L, "经度", name);
                break;
            case "pcenc":
                ValidatePercentEncoding(value, name);
                break;
            case "iban":
                ValidateIban(value, name);
                break;
            case "yymmd0":
                ValidateDate(value, 2, true, name);
                break;
            case "yymmdd":
                ValidateDate(value, 2, false, name);
                break;
            case "yyyymmd0":
                ValidateDate(value, 4, true, name);
                break;
            case "yyyymmdd":
                ValidateDate(value, 4, false, name);
                break;
            case "hhmi":
                ValidateTime(value, 4, name);
                break;
            case "hh":
                ValidateTime(value, 2, true, name);
                break;
            case "mi":
                ValidateTime(value, 2, false, name);
                break;
            case "ss":
                ValidateTime(value, 2, false, name);
                break;
            case "couponcode":
                ValidateCouponCode(value, name);
                break;
            case "couponposoffer":
                ValidateCouponPositiveOffer(value, name);
                break;
            case "packagetype":
                if (!Gs1SyntaxDictionary.IsPackageType(value))
                    throw Error($"GS1 AI {Ai} 的包装类型代码 '{value}' 无效。", name);
                break;
            default:
                throw new InvalidOperationException($"未实现的 GS1 AI Linter：{linter}。");
        }
    }

    private void ValidateIso3166(string value, bool allowNineNineNine, string name)
    {
        if (value.Length != 3 || !Gs1SyntaxDictionary.IsDigits(value, 0, 3))
            throw Error($"GS1 AI {Ai} 必须为 3 位 ISO 3166-1 数字国家代码。", name);
        var code = int.Parse(value, CultureInfo.InvariantCulture);
        if (!(allowNineNineNine && code == 999) && !Gs1SyntaxDictionary.IsIso3166Numeric(code))
            throw Error($"GS1 AI {Ai} 的国家代码 '{value}' 不在 ISO 3166-1 列表中。", name);
    }

    private void ValidateIso4217(string value, string name)
    {
        if (value.Length != 3 || !Gs1SyntaxDictionary.IsDigits(value, 0, 3))
            throw Error($"GS1 AI {Ai} 必须为 3 位 ISO 4217 数字货币代码。", name);
        if (!Gs1SyntaxDictionary.IsIso4217Numeric(int.Parse(value, CultureInfo.InvariantCulture)))
            throw Error($"GS1 AI {Ai} 的货币代码 '{value}' 不在 ISO 4217 列表中。", name);
    }

    private void ValidatePieceOfTotal(string value, string name)
    {
        if (value.Length != 4 || !Gs1SyntaxDictionary.IsDigits(value, 0, 4))
            throw Error($"GS1 AI {Ai} 的件数/总件数必须为 4 位数字。", name);
        var pieces = int.Parse(value.Substring(0, 2), CultureInfo.InvariantCulture);
        var total = int.Parse(value.Substring(2, 2), CultureInfo.InvariantCulture);
        if (pieces == 0 || total == 0 || pieces > total)
            throw Error($"GS1 AI {Ai} 的件数必须为 1–总件数，且总件数不能为零。", name);
    }

    private void ValidatePositionInSequence(string value, string name)
    {
        var slash = value.IndexOf('/');
        if (slash < 0 || slash != value.LastIndexOf('/'))
            throw Error($"GS1 AI {Ai} 必须包含且仅包含一个 '/' 序列分隔符。", name);
        if (slash == 0 || slash == value.Length - 1)
            throw Error($"GS1 AI {Ai} 的序列分隔符不能位于开头或结尾。", name);
        var position = value.Substring(0, slash);
        var total = value.Substring(slash + 1);
        if (!AllDigits(position) || !AllDigits(total))
            throw Error($"GS1 AI {Ai} 的序列位置和总数必须为数字。", name);
        var pos = int.Parse(position, CultureInfo.InvariantCulture);
        var all = int.Parse(total, CultureInfo.InvariantCulture);
        if (pos == 0 || all == 0 || pos > all)
            throw Error($"GS1 AI {Ai} 的序列位置必须在 1–总数范围内。", name);
    }

    private void ValidateMediaType(string value, string name)
    {
        if (value.Length != 2 || !Gs1SyntaxDictionary.IsDigits(value, 0, 2))
            throw Error($"GS1 AI {Ai} 的 AIDC 媒体类型必须为 2 位数字。", name);
        var type = int.Parse(value, CultureInfo.InvariantCulture);
        if (type == 0 || type > 10 && type < 80)
            throw Error($"GS1 AI {Ai} 的 AIDC 媒体类型 '{value}' 无效。", name);
    }

    private void ValidateCoordinate(string value, long maximum, string label, string name)
    {
        if (value.Length != 10 || !Gs1SyntaxDictionary.IsDigits(value, 0, 10))
            throw Error($"GS1 AI {Ai} 的{label}必须为 10 位数字。", name);
        if (long.Parse(value, CultureInfo.InvariantCulture) > maximum)
            throw Error($"GS1 AI {Ai} 的{label}超出 WGS 84 允许范围。", name);
    }

    private void ValidatePercentEncoding(string value, string name)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%') continue;
            if (index + 2 >= value.Length || !IsHex(value[index + 1]) || !IsHex(value[index + 2]))
                throw Error($"GS1 AI {Ai} 包含无效的百分比编码转义。", name);
            index += 2;
        }
    }

    private void ValidateIban(string value, string name)
    {
        if (value.Length < 5)
            throw Error($"GS1 AI {Ai} 的 IBAN 至少需要 5 个字符。", name);
        if (value[0] < 'A' || value[0] > 'Z' || value[1] < 'A' || value[1] > 'Z' ||
            !Gs1SyntaxDictionary.IsIso3166Alpha2(value.Substring(0, 2)))
            throw Error($"GS1 AI {Ai} 的 IBAN 国家代码无效。", name);
        if (!Gs1SyntaxDictionary.IsDigits(value, 2, 2))
            throw Error($"GS1 AI {Ai} 的 IBAN 校验位必须为两位数字。", name);

        var checksum = 0;
        for (var index = 4; index < value.Length; index++)
        {
            var character = value[index];
            if (character >= '0' && character <= '9')
                checksum = (checksum * 10 + character - '0') % 97;
            else if (character >= 'A' && character <= 'Z')
                checksum = (checksum * 100 + character - 'A' + 10) % 97;
            else
                throw Error($"GS1 AI {Ai} 的 IBAN 包含无效字符 '{character}'。", name);
        }
        checksum = ((checksum * 100) % 97 + value[0] - 'A' + 10) * 100 + value[1] - 'A' + 10;
        checksum = (checksum % 97 * 100) % 97;
        checksum = 98 - checksum;
        var given = int.Parse(value.Substring(2, 2), CultureInfo.InvariantCulture);
        if (given != checksum)
            throw Error($"GS1 AI {Ai} 的 IBAN 校验位无效（期望 {checksum:00}）。", name);
    }

    private void ValidateDate(string value, int yearDigits, bool allowZeroDay, string name)
    {
        var expected = yearDigits + 4;
        if (value.Length != expected || !Gs1SyntaxDictionary.IsDigits(value, 0, expected))
            throw Error($"GS1 AI {Ai} 的日期必须为 {expected} 位数字。", name);
        var year = int.Parse(value.Substring(0, yearDigits), CultureInfo.InvariantCulture);
        if (yearDigits == 2) year += 2000;
        var month = int.Parse(value.Substring(yearDigits, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(value.Substring(yearDigits + 2, 2), CultureInfo.InvariantCulture);
        if (month < 1 || month > 12)
            throw Error($"GS1 AI {Ai} 的月份 '{value.Substring(yearDigits, 2)}' 无效。", name);
        if (day == 0 && allowZeroDay) return;
        if (day < 1 || day > DaysInMonth(year, month))
            throw Error($"GS1 AI {Ai} 的日期 '{value.Substring(yearDigits + 2, 2)}' 无效。", name);
    }

    private void ValidateTime(string value, int length, string name)
        => ValidateTime(value, length, false, name);

    private void ValidateTime(string value, int length, bool hourOnly, string name)
    {
        if (value.Length != length || !Gs1SyntaxDictionary.IsDigits(value, 0, length))
            throw Error($"GS1 AI {Ai} 的时间字段必须为 {length} 位数字。", name);
        if (length == 4)
        {
            if (int.Parse(value.Substring(0, 2), CultureInfo.InvariantCulture) > 23 ||
                int.Parse(value.Substring(2, 2), CultureInfo.InvariantCulture) > 59)
                throw Error($"GS1 AI {Ai} 的时间 '{value}' 无效。", name);
            return;
        }
        var maximum = hourOnly ? 23 : 59;
        var parsed = int.Parse(value, CultureInfo.InvariantCulture);
        if (parsed > maximum)
            throw Error($"GS1 AI {Ai} 的{(hourOnly ? "小时" : "时间值")} '{value}' 无效。", name);
    }

    private void ValidateCouponCode(string value, string name)
    {
        const int minimumRequiredLength = 21;
        if (value.Length < minimumRequiredLength)
            throw Error($"GS1 AI {Ai} 的北美优惠券代码至少需要 {minimumRequiredLength} 位。", name);
        var index = 0;
        CouponVariableLength(ref index, value, "GS1 公司前缀", 6, 0, 6, false, name);
        CouponNumber(value, "优惠码", 6, ref index, name);
        CouponVariableLength(ref index, value, "优惠值", 0, 1, 5, false, name);
        CouponVariableLength(ref index, value, "主购买条件", 0, 1, 5, false, name);
        var primaryCode = CouponNumber(value, "主购买条件代码", 1, ref index, name);
        if (primaryCode > 5 && primaryCode < 9)
            throw Error($"GS1 AI {Ai} 的主购买条件代码 '{primaryCode}' 无效。", name);
        CouponNumber(value, "主购买家族代码", 3, ref index, name);

        while (index < value.Length)
        {
            var field = value[index] - '0';
            index++;
            switch (field)
            {
                case 1:
                    if (CouponNumber(value, "附加购买规则代码", 1, ref index, name) > 3)
                        throw Error($"GS1 AI {Ai} 的附加购买规则代码无效。", name);
                    CouponVariableLength(ref index, value, "第二购买条件", 0, 1, 5, false, name);
                    var secondCode = CouponNumber(value, "第二购买条件代码", 1, ref index, name);
                    if (secondCode > 4 && secondCode < 9)
                        throw Error($"GS1 AI {Ai} 的第二购买条件代码无效。", name);
                    CouponNumber(value, "第二购买家族代码", 3, ref index, name);
                    CouponVariableLength(ref index, value, "第二购买 GS1 公司前缀", 6, 0, 6, true, name);
                    break;
                case 2:
                    CouponVariableLength(ref index, value, "第三购买条件", 0, 1, 5, false, name);
                    var thirdCode = CouponNumber(value, "第三购买条件代码", 1, ref index, name);
                    if (thirdCode > 4 && thirdCode < 9)
                        throw Error($"GS1 AI {Ai} 的第三购买条件代码无效。", name);
                    CouponNumber(value, "第三购买家族代码", 3, ref index, name);
                    CouponVariableLength(ref index, value, "第三购买 GS1 公司前缀", 6, 0, 6, true, name);
                    break;
                case 3:
                    CouponNumber(value, "到期日期", 6, ref index, name);
                    ValidateDate(value.Substring(index - 6, 6), 2, true, name);
                    break;
                case 4:
                    CouponNumber(value, "起始日期", 6, ref index, name);
                    ValidateDate(value.Substring(index - 6, 6), 2, true, name);
                    break;
                case 5:
                    CouponVariableLength(ref index, value, "序列号", 6, 0, 9, false, name);
                    break;
                case 6:
                    CouponVariableLength(ref index, value, "零售商 ID", 6, 1, 7, false, name);
                    break;
                case 9:
                    if (CouponNumber(value, "优惠值代码", 1, ref index, name) is var saveCode &&
                        ((saveCode > 2 && saveCode < 5) || saveCode > 6))
                        throw Error($"GS1 AI {Ai} 的优惠值代码无效。", name);
                    if (CouponNumber(value, "优惠值适用对象", 1, ref index, name) > 2)
                        throw Error($"GS1 AI {Ai} 的优惠值适用对象无效。", name);
                    CouponNumber(value, "门店优惠标志", 1, ref index, name);
                    if (CouponNumber(value, "不叠加标志", 1, ref index, name) > 1)
                        throw Error($"GS1 AI {Ai} 的不叠加标志无效。", name);
                    break;
                default:
                    throw Error($"GS1 AI {Ai} 包含未知优惠券数据字段 '{field}'。", name);
            }
        }
    }

    private void ValidateCouponPositiveOffer(string value, string name)
    {
        const int minimumLength = 21;
        const int maximumLength = 36;
        if (value.Length < minimumLength || value.Length > maximumLength)
            throw Error($"GS1 AI {Ai} 的正面优惠文件必须为 {minimumLength}–{maximumLength} 位。", name);
        var index = 0;
        var format = CouponNumber(value, "优惠券格式", 1, ref index, name);
        if (format != 0 && format != 1)
            throw Error($"GS1 AI {Ai} 的优惠券格式必须为 0 或 1。", name);
        CouponVariableLength(ref index, value, "优惠券出资方 ID", 6, 0, 6, false, name);
        CouponNumber(value, "优惠码", 6, ref index, name);
        CouponVariableLength(ref index, value, "序列号", 6, 0, 9, false, name);
        if (index != value.Length)
            throw Error($"GS1 AI {Ai} 的正面优惠文件包含保留的尾随字符。", name);
    }

    private void CouponVariableLength(ref int index, string value, string field, int offset, int minimum,
        int maximum, bool nineMeansAbsent, string name)
    {
        if (index >= value.Length)
            throw Error($"GS1 AI {Ai} 的{field}长度指示符缺失。", name);
        if (value[index] < '0' || value[index] > '9')
            throw Error($"GS1 AI {Ai} 的{field}长度指示符必须为数字。", name);
        var length = value[index] - '0';
        index++;
        var absent = nineMeansAbsent && length == 9;
        if (!absent && (length < minimum || length > maximum))
            throw Error($"GS1 AI {Ai} 的{field}长度指示符 '{length}' 无效。", name);
        if (absent) return;
        var total = length + offset;
        if (index + total > value.Length)
            throw Error($"GS1 AI {Ai} 的{field}数据不完整。", name);
        for (var count = 0; count < total; count++)
        {
            if (value[index] < '0' || value[index] > '9')
                throw Error($"GS1 AI {Ai} 的{field}必须为数字。", name);
            index++;
        }
    }

    private int CouponNumber(string value, string field, int length, ref int index, string name)
    {
        if (index + length > value.Length)
            throw Error($"GS1 AI {Ai} 的{field}数据不完整。", name);
        var number = 0;
        for (var count = 0; count < length; count++)
        {
            var character = value[index++];
            if (character < '0' || character > '9')
                throw Error($"GS1 AI {Ai} 的{field}必须为数字。", name);
            number = number * 10 + character - '0';
        }
        return number;
    }

    private bool HasValidAlphaCheck(string value)
    {
        if (value.Length < 2) return false;
        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        var weights = new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83 };
        var dataLength = value.Length - 2;
        if (dataLength > weights.Length) return false;
        var checksum = 0;
        for (var index = 0; index < dataLength; index++)
        {
            var characterValue = Gs1SyntaxDictionary.Cset82Value(value[index]);
            if (characterValue < 0) return false;
            checksum += characterValue * weights[dataLength - 1 - index];
        }
        checksum %= 1021;
        return value[value.Length - 2] == alphabet[checksum >> 5] &&
               value[value.Length - 1] == alphabet[checksum & 0x1F];
    }

    private static bool IsCset39(char value)
        => value >= '0' && value <= '9' || value >= 'A' && value <= 'Z' ||
           value == '#' || value == '-' || value == '/';

    private static bool IsBase64Url(string value, int index)
    {
        var character = value[index];
        if (character >= '0' && character <= '9' || character >= 'A' && character <= 'Z' ||
            character >= 'a' && character <= 'z' || character == '-' || character == '_') return true;
        if (character != '=') return false;
        // 只在末尾允许一个或两个 '='，且总长度为 3 的倍数。
        var remaining = value.Length - index;
        return value.Length % 3 == 0 &&
               (remaining == 1 || remaining == 2 && value[index + 1] == '=');
    }

    private static bool AllDigits(string value)
        => Gs1SyntaxDictionary.IsDigits(value, 0, value.Length);

    private static bool IsAllZero(string value)
    {
        if (value.Length == 0) return false;
        for (var index = 0; index < value.Length; index++)
            if (value[index] != '0') return false;
        return true;
    }

    private static bool IsHex(char value)
        => value >= '0' && value <= '9' || value >= 'A' && value <= 'F' || value >= 'a' && value <= 'f';

    private static bool IsImporterIndex(char value)
        => value >= '0' && value <= '9' || value >= 'A' && value <= 'Z' ||
           value >= 'a' && value <= 'z' || value == '-' || value == '_';

    private static int DaysInMonth(int year, int month)
    {
        switch (month)
        {
            case 2:
                return (year & 3) == 0 && (year % 100 != 0 || year % 400 == 0) ? 29 : 28;
            case 4:
            case 6:
            case 9:
            case 11:
                return 30;
            default:
                return 31;
        }
    }

    private ArgumentException Error(string message, string name) => new ArgumentException(message, name);
}

/// <summary>GS1 AI 的单个字段组件。</summary>
internal sealed class Gs1SyntaxComponent
{
    internal Gs1SyntaxComponent(char type, int minimumLength, int maximumLength, bool optional,
        IReadOnlyList<string> linters)
    {
        Type = type;
        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
        Optional = optional;
        Linters = linters;
    }

    /// <summary>获取字段类型：N（数字）、X（CSET 82）、Y（CSET 39）或 Z（base64url）。</summary>
    internal char Type { get; }

    /// <summary>获取字段最小长度。</summary>
    internal int MinimumLength { get; }

    /// <summary>获取字段最大长度。</summary>
    internal int MaximumLength { get; }

    /// <summary>获取字段是否可选。</summary>
    internal bool Optional { get; }

    /// <summary>获取字段 Linter 名称。</summary>
    internal IReadOnlyList<string> Linters { get; }
}
