using System;
using Bing.EasyPrint.Protocols;

namespace Bing.EasyPrint.Printing;

/// <summary>
/// 由 EAN/UPC、DataBar 或 GS1-128 线性组件、分隔图案和二维组件组成的独立复合码。
/// </summary>
/// <remarks>
/// 坐标为含静区的旋转后完整符号左上角。模块宽度、线性条高和静区均以打印点计；
/// CC-A/B 每行高为两倍模块宽度，CC-C 为三倍；EAN/UPC 的三条分隔行各为两倍模块宽度，其他分隔行高为模块宽度。尺寸由实际编码结果决定，
/// 不内置 HRI；需要可读文字时使用独立文字元素。
/// </remarks>
public sealed class PrintGs1CompositeElement : PrintElement
{
    /// <summary>
    /// 获取线性制式，支持 Ean8/Ean13/UpcA/UpcE、Omnidirectional、Truncated、Limited、Stacked/StackedOmnidirectional、Expanded/ExpandedStacked 和 UccEan128（GS1-128）。
    /// </summary>
    public BarcodeType LinearType { get; }
    /// <summary>
    /// 获取主数据：EAN/UPC 使用数字主码（含或不含校验位），可追加 + 及 2/5 位数字附加码，UPC-E 支持 6/7/8 位且号码系统仅 0/1；Omnidirectional/Truncated/Limited/Stacked/StackedOmnidirectional 使用 GTIN；Expanded/ExpandedStacked 使用括号式 AI；GS1-128 使用规范化后最多 48 字符的括号式 AI。
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
    /// 获取线性组件条高，单位为打印点；Stacked 为上下数据行总高；ExpandedStacked/StackedOmnidirectional 为每条数据行高度；均不含分隔行。
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
    /// <param name="linearType">EAN-8/EAN-13/UPC-A/UPC-E、DataBar Omnidirectional、Truncated、Limited、Stacked/StackedOmnidirectional、Expanded/ExpandedStacked 或 GS1-128。</param>
    /// <param name="primaryData">EAN/UPC 使用纯数字，可省略校验位（EAN-8 为 7/8 位、EAN-13 为 12/13 位、UPC-A 为 11/12 位、UPC-E 为 6/7/8 位），可追加一个加号及恰好 2/5 位 ASCII 数字附加码，不补零、不修剪；Omnidirectional/Truncated/Limited/Stacked/StackedOmnidirectional 的 GTIN，或 Expanded/ExpandedStacked/GS1-128 的括号 AI 主数据；不可为 null，格式和容量在编码时校验。</param>
    /// <param name="secondaryData">括号 AI 次数据；不可为 null，格式及容量在编码时校验。</param>
    /// <param name="linearBarHeight">线性组件条高，单位为点，必须大于零；Truncated 至少 13X；StackedOmnidirectional 为每行高度，至少 33X，中间另加三个 1X 分隔行；Stacked 至少为模块宽度的 12 倍，上行取总高的 5/12 向下取整，下行取余量；ExpandedStacked 为每条数据行高度，至少 1X，建议 34X，行间另加三个 1X 分隔行。</param>
    /// <param name="moduleWidth">模块宽度，单位为点，默认 2，必须大于零。</param>
    /// <param name="mode">默认 Auto；EAN/UPC 与 DataBar 自动选择 A/B，GS1-128 自动选择 CC-C，也可显式选择 CC-A/B；显式模式容量不足时失败。</param>
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
        if (!Gs1CompositeEncoder.IsEanUpc(linearType) && linearType != BarcodeType.Gs1DataBarOmnidirectional && linearType != BarcodeType.Gs1DataBarLimited &&
            linearType != BarcodeType.Gs1DataBarTruncated && linearType != BarcodeType.Gs1DataBarStackedOmnidirectional &&
            linearType != BarcodeType.Gs1DataBarExpandedStacked && linearType != BarcodeType.Gs1DataBarStacked && linearType != BarcodeType.Gs1DataBarExpanded && linearType != BarcodeType.UccEan128)
            throw new NotSupportedException("Composite 仅支持 EAN-8/EAN-13/UPC-A/UPC-E、DataBar Omnidirectional、Truncated、Limited、Stacked/StackedOmnidirectional、Expanded/ExpandedStacked 和 GS1-128 线性组件。");
        if (primaryData == null) throw new ArgumentNullException(nameof(primaryData));
        if (secondaryData == null) throw new ArgumentNullException(nameof(secondaryData));
        if (linearBarHeight <= 0) throw new ArgumentOutOfRangeException(nameof(linearBarHeight));
        if (moduleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        if (linearType == BarcodeType.Gs1DataBarStacked && linearBarHeight < 12L * moduleWidth)
            throw new ArgumentOutOfRangeException(nameof(linearBarHeight), "Stacked 上下数据行总高必须至少为模块宽度的 12 倍。");
        if (linearType == BarcodeType.Gs1DataBarExpandedStacked && linearBarHeight < moduleWidth)
            throw new ArgumentOutOfRangeException(nameof(linearBarHeight), "ExpandedStacked 每条数据行高度至少为一个模块宽度。");
        if (linearType == BarcodeType.Gs1DataBarTruncated && linearBarHeight < 13L * moduleWidth)
            throw new ArgumentOutOfRangeException(nameof(linearBarHeight), "Truncated Composite 条高至少为 13X。");
        if (linearType == BarcodeType.Gs1DataBarStackedOmnidirectional && linearBarHeight < 33L * moduleWidth)
            throw new ArgumentOutOfRangeException(nameof(linearBarHeight), "Stacked Omnidirectional 每条数据行高度至少为 33X。");
        if (!Enum.IsDefined(typeof(Gs1CompositeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (!IsSupportedCombination(linearType, mode))
            throw new NotSupportedException("GS1-128 Composite 支持 Auto/CC-A/CC-B/CC-C；EAN/UPC 和 DataBar 仅支持 Auto/CC-A/CC-B。");
        if (!Enum.IsDefined(typeof(RotationAngle), rotation)) throw new ArgumentOutOfRangeException(nameof(rotation));
        LinearType = linearType;
        PrimaryData = primaryData;
        SecondaryData = secondaryData;
        LinearBarHeight = linearBarHeight;
        ModuleWidth = moduleWidth;
        Mode = mode;
        QuietZone = quietZone;
    }

    /// <summary>获取 ExpandedStacked 每行字符对数上限；null 沿用两列，仅适用于 ExpandedStacked。</summary>
    public int? ExpandedStackedColumns { get; private set; }

    /// <summary>创建指定 ExpandedStacked 列数的不可变副本；原元素不变，尺寸在编码时确定。</summary>
    /// <param name="columns">每行字符对数上限，范围 2–11；不是打印点宽或二维组件列数。</param>
    /// <returns>保留其余配置的新元素；不持有外部资源，元数据计入当前打印资源作用域。</returns>
    /// <exception cref="NotSupportedException">元素制式不是 ExpandedStacked。</exception>
    /// <exception cref="ArgumentOutOfRangeException">列数超出支持范围。</exception>
    public PrintGs1CompositeElement WithExpandedStackedColumns(int columns)
    {
        EscPosRasterCanvas.ValidateExpandedStackedColumns(LinearType, columns, true);
        var copy = new PrintGs1CompositeElement(X, Y, LinearType, PrimaryData, SecondaryData,
            LinearBarHeight, ModuleWidth, Mode, Rotation, QuietZone);
        copy.SourcePath = SourcePath;
        copy.ExpandedStackedColumns = columns;
        return copy;
    }

    /// <summary>
    /// 校验当前实现支持的线性组件与二维模式组合。
    /// </summary>
    internal static bool IsSupportedCombination(BarcodeType type, Gs1CompositeMode mode)
        => type == BarcodeType.UccEan128
            ? mode == Gs1CompositeMode.Auto || mode == Gs1CompositeMode.CcA || mode == Gs1CompositeMode.CcB || mode == Gs1CompositeMode.CcC
            : (type == BarcodeType.Gs1DataBarTruncated || type == BarcodeType.Gs1DataBarStackedOmnidirectional ||
               Gs1CompositeEncoder.IsEanUpc(type) || type == BarcodeType.Gs1DataBarLimited || type == BarcodeType.Gs1DataBarOmnidirectional ||
               type == BarcodeType.Gs1DataBarExpandedStacked || type == BarcodeType.Gs1DataBarStacked || type == BarcodeType.Gs1DataBarExpanded) &&
              (mode == Gs1CompositeMode.Auto || mode == Gs1CompositeMode.CcA || mode == Gs1CompositeMode.CcB);
}
