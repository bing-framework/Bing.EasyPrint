using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Elements;

/// <summary>
/// 绑定主次 GS1 数据的不可拆分复合码模板元素，支持 V2/V3。
/// </summary>
public sealed class BpGs1CompositeElement : BpElement
{
    /// <summary>
    /// 初始化复合码元素。
    /// </summary>
    public BpGs1CompositeElement() { Type = 11; }
    /// <summary>
    /// 获取或设置绑定次数据的字段；继承的 Field 绑定主数据；EAN/UPC 主码可追加一个加号和恰好 2/5 位 ASCII 数字附加码，不补零、不修剪。
    /// </summary>
    public BgField SecondaryField { get; set; }
    /// <summary>
    /// 获取或设置线性制式，支持 Ean8/Ean13/UpcA/UpcE、Omnidirectional、Truncated、Limited、Stacked/StackedOmnidirectional、Expanded/ExpandedStacked 和 GS1-128；默认 Omnidirectional。
    /// </summary>
    public BarcodeType LinearType { get; set; } = BarcodeType.Gs1DataBarOmnidirectional;
    /// <summary>
    /// 获取或设置二维模式；默认 Auto，EAN/UPC 与 DataBar 自动选择 A/B，GS1-128 自动选择 CC-C；GS1-128 也支持显式 CC-A/B，显式模式容量不足时失败。
    /// </summary>
    public Gs1CompositeMode Mode { get; set; }
    /// <summary>
    /// 获取或设置模块宽度，单位为打印点，默认 2。
    /// </summary>
    public int ModuleWidth { get; set; } = 2;
    /// <summary>
    /// 获取或设置线性条高，单位为打印点，默认 40；Truncated 至少 13X；StackedOmnidirectional 表示每条数据行高度，至少 33X，中间另加三个 1X 分隔行；EAN/UPC 不含三条 2X 分隔行；Stacked 表示上下数据行总高，至少为模块宽度的 12 倍，不含内部分隔行，上行取总高的 5/12 向下取整，下行取余量；ExpandedStacked 表示每条数据行高度，至少 1X，建议 34X，行间另加三个 1X 分隔行。
    /// </summary>
    public int LinearBarHeight { get; set; } = 40;
    /// <summary>
    /// 获取或设置整个符号的顺时针旋转角度。
    /// </summary>
    public RotationAngle Rotation { get; set; }
    /// <summary>
    /// 获取或设置随完整符号旋转的静区，单位为打印点。
    /// </summary>
    public PrintQuietZone QuietZone { get; set; }
}
