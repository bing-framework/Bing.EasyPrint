using System;
using System.Globalization;
using System.Threading;
using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 准备价签显示数据并渲染价签模板。
/// </summary>
public sealed class PriceLabelRenderer
{
    /// <summary>
    /// 保存用于价签输出的模板渲染器。
    /// </summary>
    private readonly TemplateRenderer renderer;

    /// <summary>
    /// 初始化一个 <see cref="PriceLabelRenderer"/> 类型的实例。
    /// </summary>
    public PriceLabelRenderer() : this(new TemplateRenderer()) { }

    /// <summary>
    /// 初始化一个 <see cref="PriceLabelRenderer"/> 类型的实例。
    /// </summary>
    /// <param name="renderer">用于生成 CPCL 页面且不能为 null 的模板渲染器。</param>
    public PriceLabelRenderer(TemplateRenderer renderer)
    {
        this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    /// <summary>
    /// 将业务价签数据转换为模板显示字段。
    /// </summary>
    /// <param name="data">包含商品内容与价格的业务价签数据。</param>
    /// <returns>可用于通用模板绑定的价签显示数据。</returns>
    /// <remarks>
    /// 不修改传入的业务数据；原价与售价不同时标记促销，会员价大于零时显示会员价。
    /// </remarks>
    public PriceLabelViewData PrepareData(PriceLabelData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ValidatePrice(data.OriginPrice, nameof(data.OriginPrice));
        ValidatePrice(data.SalePrice, nameof(data.SalePrice));
        if (data.MemberPrice < 0) throw new ArgumentOutOfRangeException(nameof(data.MemberPrice), "价格不能为负数。");

        var member = data.MemberPrice.GetValueOrDefault();
        var saleParts = SplitPrice(data.SalePrice);
        var memberParts = SplitPrice(member);
        var originText = FormatPrice(data.OriginPrice);
        return new PriceLabelViewData
        {
            Name = data.Name ?? string.Empty,
            Barcode = EmptyAsNull(data.Barcode),
            BarcodeDisplay = string.IsNullOrWhiteSpace(data.Barcode) ? null : "商品条码：" + data.Barcode,
            QrCode = EmptyAsNull(data.QrCode),
            Code = EmptyAsNull(data.Code),
            Unit = data.Unit ?? string.Empty,
            Specification = data.Specification ?? string.Empty,
            Origin = data.Origin ?? string.Empty,
            OriginPriceText = originText,
            OriginPriceDisplay = "¥" + originText,
            SalePriceInteger = saleParts.integer,
            SalePriceFraction = saleParts.fraction,
            MemberPriceInteger = memberParts.integer,
            MemberPriceFraction = memberParts.fraction,
            MemberPriceDisplay = "¥" + FormatPrice(member),
            IsPromotion = data.OriginPrice != data.SalePrice,
            HasMemberPrice = member > 0,
            PromotionPeriod = FormatPeriod(data.PromotionStart, data.PromotionEnd)
        };
    }

    /// <summary>
    /// 将价签模板与业务数据渲染为 CPCL 页面。
    /// </summary>
    /// <param name="template">可编辑的通用价签模板。</param>
    /// <param name="data">商品及价格业务数据。</param>
    /// <param name="resources">可选的图片资源提供器。</param>
    /// <param name="options">本次渲染的可选资源限制及编码配置。</param>
    /// <param name="cancellationToken">取消渲染的令牌。</param>
    /// <returns>包含逐页 CPCL 字节的渲染结果。</returns>
    public TemplateRenderResult Render(Bing.EasyPrint.Model.BpTemplate template, PriceLabelData data,
        ITemplateResourceProvider resources = null, TemplateRenderOptions options = null,
        CancellationToken cancellationToken = default)
        => renderer.Render(template, PrepareData(data), resources, options, cancellationToken);

    /// <summary>
    /// 将价格拆成整数和带小数点的两位小数文本。
    /// </summary>
    /// <param name="value">非负价格。</param>
    /// <returns>按远离零方向舍入后的整数和小数部分。</returns>
    private static (string integer, string fraction) SplitPrice(decimal value)
    {
        var parts = decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture).Split('.');
        return (parts[0], "." + parts[1]);
    }

    /// <summary>
    /// 将价格格式化为固定两位小数。
    /// </summary>
    /// <param name="value">非负价格。</param>
    /// <returns>不随区域设置变化的价格文本。</returns>
    private static string FormatPrice(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// 格式化完整的促销起止日期。
    /// </summary>
    /// <param name="start">促销开始日期。</param>
    /// <param name="end">促销结束日期。</param>
    /// <returns>日期区间文本；任一日期缺失时返回 null。</returns>
    private static string FormatPeriod(DateTime? start, DateTime? end)
    {
        if (!start.HasValue || !end.HasValue) return null;
        return start.Value.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) + " - " + end.Value.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 将空白业务字段转换为可用于显隐条件的 null。
    /// </summary>
    /// <param name="value">业务字段内容。</param>
    /// <returns>非空白原值；否则返回 null。</returns>
    private static string EmptyAsNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// 校验价格为非负数。
    /// </summary>
    /// <param name="value">待校验价格。</param>
    /// <param name="name">异常中使用的参数名。</param>
    private static void ValidatePrice(decimal value, string name)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(name, "价格不能为负数。");
    }
}
