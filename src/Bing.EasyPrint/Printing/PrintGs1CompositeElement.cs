using System;

namespace Bing.EasyPrint.Printing;

/// <summary>
/// 由 DataBar 或 GS1-128 线性组件、分隔图案和二维组件组成的独立复合码。
/// </summary>
/// <remarks>
/// 坐标为含静区的旋转后完整符号左上角。模块宽度、线性条高和静区均以打印点计；
/// CC-A/B 每行高为两倍模块宽度，CC-C 为三倍；分隔行高为模块宽度。尺寸由实际编码结果决定，
/// 不内置 HRI；需要可读文字时使用独立文字元素。
/// </remarks>
public sealed class PrintGs1CompositeElement : PrintElement
{
    /// <summary>
    /// 获取线性制式，支持 Omnidirectional、Limited、单行 Expanded 和 UccEan128（GS1-128）。
    /// </summary>
    public BarcodeType LinearType { get; }
    /// <summary>
    /// 获取主数据：Omnidirectional/Limited 使用 GTIN；Expanded 使用括号式 AI；GS1-128 使用规范化后最多 48 字符的括号式 AI。
    /// </summary>
    public string PrimaryData { get; }
    /// <summary>
    /// 获取次数据：当前共享解析器支持的括号式 GS1 AI 元素串。
    /// </summary>
    public string SecondaryData { get; }
    /// <summary>
    /// 获取二维组件模式。
    /// </summary>
    public Gs1CompositeMode Mode { get; }
    /// <summary>
    /// 获取模块宽度，单位为打印点。
    /// </summary>
    public int ModuleWidth { get; }
    /// <summary>
    /// 获取线性组件条高，单位为打印点。
    /// </summary>
    public int LinearBarHeight { get; }
    /// <summary>
    /// 获取随整个符号旋转的四边静区；null 表示未增加显式静区。
    /// </summary>
    public PrintQuietZone QuietZone { get; }

    /// <summary>
    /// 初始化一个 <see cref="PrintGs1CompositeElement"/> 类型的实例。
    /// </summary>
    /// <param name="x">含静区符号左上角的水平坐标，单位为点。</param>
    /// <param name="y">含静区符号左上角的垂直坐标，单位为点。</param>
    /// <param name="linearType">DataBar Omnidirectional、Limited、单行 Expanded 或 GS1-128。</param>
    /// <param name="primaryData">Omnidirectional/Limited 的 GTIN，或 Expanded/GS1-128 的括号 AI 主数据；不可为 null，格式和容量在编码时校验。</param>
    /// <param name="secondaryData">括号 AI 次数据；不可为 null，格式及容量在编码时校验。</param>
    /// <param name="linearBarHeight">线性组件条高，单位为点，必须大于零。</param>
    /// <param name="moduleWidth">模块宽度，单位为点，默认 2，必须大于零。</param>
    /// <param name="mode">默认 Auto；DataBar 选择 A/B，GS1-128 选择 CC-C。</param>
    /// <param name="rotation">完整符号顺时针旋转角度，默认不旋转。</param>
    /// <param name="quietZone">四边静区，单位为点；null 不额外增加静区。</param>
    /// <exception cref="ArgumentNullException">主数据或次数据为 null。</exception>
    /// <exception cref="ArgumentOutOfRangeException">坐标、尺寸或枚举值无效。</exception>
    /// <exception cref="NotSupportedException">线性制式或制式与模式配对不受支持。</exception>
    public PrintGs1CompositeElement(int x, int y, BarcodeType linearType, string primaryData,
        string secondaryData, int linearBarHeight, int moduleWidth = 2,
        Gs1CompositeMode mode = Gs1CompositeMode.Auto, RotationAngle rotation = RotationAngle.None,
        PrintQuietZone quietZone = null)
        : base(x, y, 0, 0, rotation)
    {
        if (!Enum.IsDefined(typeof(BarcodeType), linearType)) throw new ArgumentOutOfRangeException(nameof(linearType));
        if (linearType != BarcodeType.Gs1DataBarOmnidirectional && linearType != BarcodeType.Gs1DataBarLimited &&
            linearType != BarcodeType.Gs1DataBarExpanded && linearType != BarcodeType.UccEan128)
            throw new NotSupportedException("Composite 仅支持 DataBar Omnidirectional、Limited、单行 Expanded 和 GS1-128 线性组件。");
        if (primaryData == null) throw new ArgumentNullException(nameof(primaryData));
        if (secondaryData == null) throw new ArgumentNullException(nameof(secondaryData));
        if (linearBarHeight <= 0) throw new ArgumentOutOfRangeException(nameof(linearBarHeight));
        if (moduleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        if (!Enum.IsDefined(typeof(Gs1CompositeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (!IsSupportedCombination(linearType, mode))
            throw new NotSupportedException("GS1-128 Composite 仅支持 Auto/CC-C；DataBar 仅支持 Auto/CC-A/CC-B。");
        if (!Enum.IsDefined(typeof(RotationAngle), rotation)) throw new ArgumentOutOfRangeException(nameof(rotation));
        LinearType = linearType;
        PrimaryData = primaryData;
        SecondaryData = secondaryData;
        LinearBarHeight = linearBarHeight;
        ModuleWidth = moduleWidth;
        Mode = mode;
        QuietZone = quietZone;
    }

    /// <summary>
    /// 校验当前实现支持的线性组件与二维模式组合。
    /// </summary>
    internal static bool IsSupportedCombination(BarcodeType type, Gs1CompositeMode mode)
        => type == BarcodeType.UccEan128
            ? mode == Gs1CompositeMode.Auto || mode == Gs1CompositeMode.CcC
            : (type == BarcodeType.Gs1DataBarLimited || type == BarcodeType.Gs1DataBarOmnidirectional ||
               type == BarcodeType.Gs1DataBarExpanded) &&
              (mode == Gs1CompositeMode.Auto || mode == Gs1CompositeMode.CcA || mode == Gs1CompositeMode.CcB);
}
