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
    /// 获取或设置绑定次数据的字段；继承的 Field 绑定主数据。
    /// </summary>
    public BgField SecondaryField { get; set; }
    /// <summary>
    /// 获取或设置线性制式，默认 Omnidirectional。
    /// </summary>
    public BarcodeType LinearType { get; set; } = BarcodeType.Gs1DataBarOmnidirectional;
    /// <summary>
    /// 获取或设置二维模式；默认 Auto，DataBar 选择 A/B，GS1-128 选择 CC-C。
    /// </summary>
    public Gs1CompositeMode Mode { get; set; }
    /// <summary>
    /// 获取或设置模块宽度，单位为打印点，默认 2。
    /// </summary>
    public int ModuleWidth { get; set; } = 2;
    /// <summary>
    /// 获取或设置线性条高，单位为打印点，默认 40。
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
