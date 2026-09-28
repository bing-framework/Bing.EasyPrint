using System;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 提供生成价签内容所需的商品和价格数据。
/// </summary>
/// <remarks>
/// 此数据模型不访问打印机或 ERP 服务；价格范围在 <see cref="PriceLabelRenderer.PrepareData(PriceLabelData)" /> 中校验。
/// </remarks>
public sealed class PriceLabelData
{
    /// <summary>
    /// 获取或设置价签上显示的商品名称。
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 获取或设置用于生成商品一维条码的内容。
    /// </summary>
    public string Barcode { get; set; }
    /// <summary>
    /// 获取或设置价签二维码要编码的内容。
    /// </summary>
    public string QrCode { get; set; }
    /// <summary>
    /// 获取或设置价签上显示的商品编码。
    /// </summary>
    public string Code { get; set; }
    /// <summary>
    /// 获取或设置商品原价，不得为负数。
    /// </summary>
    public decimal OriginPrice { get; set; }
    /// <summary>
    /// 获取或设置商品当前售价，不得为负数。
    /// </summary>
    public decimal SalePrice { get; set; }
    /// <summary>
    /// 获取或设置可选会员价；大于零时作为会员价显示。
    /// </summary>
    /// <remarks>
    /// 未指定或设置为零时不显示会员价，负数会在准备价签数据时被拒绝。
    /// </remarks>
    public decimal? MemberPrice { get; set; }
    /// <summary>
    /// 获取或设置商品的销售单位。
    /// </summary>
    public string Unit { get; set; }
    /// <summary>
    /// 获取或设置商品规格或辅助属性文本。
    /// </summary>
    public string Specification { get; set; }
    /// <summary>
    /// 获取或设置商品产地文本。
    /// </summary>
    public string Origin { get; set; }
    /// <summary>
    /// 获取或设置促销开始时间。
    /// </summary>
    /// <remarks>
    /// 只有开始和结束时间均已指定时才生成促销日期文本。
    /// </remarks>
    public DateTime? PromotionStart { get; set; }
    /// <summary>
    /// 获取或设置促销结束时间。
    /// </summary>
    /// <remarks>
    /// 只有开始和结束时间均已指定时才生成促销日期文本。
    /// </remarks>
    public DateTime? PromotionEnd { get; set; }
}

/// <summary>
/// 保存适配通用模板字段绑定的价签展示文本和状态。
/// </summary>
/// <remarks>
/// 属性对外只读，由当前程序集在准备价签数据时设置。
/// </remarks>
public sealed class PriceLabelViewData
{
    /// <summary>
    /// 初始化一个 <see cref="PriceLabelViewData"/> 类型的实例。
    /// </summary>
    internal PriceLabelViewData() { }

    /// <summary>
    /// 获取商品名称。
    /// </summary>
    public string Name { get; internal set; }
    /// <summary>
    /// 获取商品一维条码内容。
    /// </summary>
    public string Barcode { get; internal set; }
    /// <summary>
    /// 获取带有说明文字的商品条码显示文本。
    /// </summary>
    public string BarcodeDisplay { get; internal set; }
    /// <summary>
    /// 获取二维码要编码的内容；无内容时为 <see langword="null" />。
    /// </summary>
    /// <remarks>
    /// 空内容转换为 <see langword="null" />，可供条件表达式跳过二维码元素。
    /// </remarks>
    public string QrCode { get; internal set; }
    /// <summary>
    /// 获取商品编码。
    /// </summary>
    public string Code { get; internal set; }
    /// <summary>
    /// 获取商品销售单位。
    /// </summary>
    public string Unit { get; internal set; }
    /// <summary>
    /// 获取商品规格或辅助属性文本。
    /// </summary>
    public string Specification { get; internal set; }
    /// <summary>
    /// 获取商品产地文本。
    /// </summary>
    public string Origin { get; internal set; }
    /// <summary>
    /// 获取金额显示使用的货币符号，默认值为人民币符号 <c>¥</c>。
    /// </summary>
    public string CurrencySymbol { get; internal set; } = "¥";
    /// <summary>
    /// 获取固定保留两位小数的原价文本。
    /// </summary>
    public string OriginPriceText { get; internal set; }
    /// <summary>
    /// 获取带货币符号的原价显示文本。
    /// </summary>
    public string OriginPriceDisplay { get; internal set; }
    /// <summary>
    /// 获取售价的整数部分文本。
    /// </summary>
    public string SalePriceInteger { get; internal set; }
    /// <summary>
    /// 获取售价的小数部分文本，包含小数点。
    /// </summary>
    public string SalePriceFraction { get; internal set; }
    /// <summary>
    /// 获取会员价的整数部分文本。
    /// </summary>
    public string MemberPriceInteger { get; internal set; }
    /// <summary>
    /// 获取会员价的小数部分文本，包含小数点。
    /// </summary>
    public string MemberPriceFraction { get; internal set; }
    /// <summary>
    /// 获取带货币符号的会员价显示文本。
    /// </summary>
    public string MemberPriceDisplay { get; internal set; }
    /// <summary>
    /// 获取是否显示促销布局；原价与售价不相等时为 <see langword="true" />，相等时为 <see langword="false" />。
    /// </summary>
    public bool IsPromotion { get; internal set; }
    /// <summary>
    /// 获取是否存在可显示的会员价；会员价大于零时为 <see langword="true" />，否则为 <see langword="false" />。
    /// </summary>
    public bool HasMemberPrice { get; internal set; }
    /// <summary>
    /// 获取促销日期范围的显示文本；任一日期缺失时为 <see langword="null" />。
    /// </summary>
    public string PromotionPeriod { get; internal set; }
}
