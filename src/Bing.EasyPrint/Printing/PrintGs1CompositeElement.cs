using System;

namespace Bing.EasyPrint.Printing;

/// <summary>由 DataBar 线性组件、分隔图案和 CC-A/CC-B 组成的独立复合码。</summary>
/// <remarks>
/// 坐标为含静区的旋转后完整符号左上角。模块宽度、线性条高和静区均以打印点计；
/// 二维组件每行高为两倍模块宽度，分隔行高为模块宽度。尺寸由实际编码结果决定，
/// 不内置 HRI；需要可读文字时使用独立文字元素。
/// </remarks>
public sealed class PrintGs1CompositeElement : PrintElement
{
    /// <summary>获取线性制式，仅支持 Omnidirectional 和 Limited。</summary>
    public BarcodeType LinearType { get; }
    /// <summary>获取主数据：13 位 GTIN 数据、有效 14 位 GTIN 或单个 (01) 元素串。</summary>
    public string PrimaryData { get; }
    /// <summary>获取次数据：当前共享解析器支持的括号式 GS1 AI 元素串。</summary>
    public string SecondaryData { get; }
    /// <summary>获取二维组件模式。</summary>
    public Gs1CompositeMode Mode { get; }
    /// <summary>获取模块宽度，单位为打印点。</summary>
    public int ModuleWidth { get; }
    /// <summary>获取线性组件条高，单位为打印点。</summary>
    public int LinearBarHeight { get; }
    /// <summary>获取随整个符号旋转的四边静区；null 表示未增加显式静区。</summary>
    public PrintQuietZone QuietZone { get; }

    /// <summary>创建独立复合码，主次数据的语义和容量在编码时校验。</summary>
    public PrintGs1CompositeElement(int x, int y, BarcodeType linearType, string primaryData,
        string secondaryData, int linearBarHeight, int moduleWidth = 2,
        Gs1CompositeMode mode = Gs1CompositeMode.Auto, RotationAngle rotation = RotationAngle.None,
        PrintQuietZone quietZone = null)
        : base(x, y, 0, 0, rotation)
    {
        if (!Enum.IsDefined(typeof(BarcodeType), linearType)) throw new ArgumentOutOfRangeException(nameof(linearType));
        if (linearType != BarcodeType.Gs1DataBarOmnidirectional && linearType != BarcodeType.Gs1DataBarLimited)
            throw new NotSupportedException("Composite 仅支持 DataBar Omnidirectional 和 Limited 线性组件。");
        if (primaryData == null) throw new ArgumentNullException(nameof(primaryData));
        if (secondaryData == null) throw new ArgumentNullException(nameof(secondaryData));
        if (linearBarHeight <= 0) throw new ArgumentOutOfRangeException(nameof(linearBarHeight));
        if (moduleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        if (!Enum.IsDefined(typeof(Gs1CompositeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Enum.IsDefined(typeof(RotationAngle), rotation)) throw new ArgumentOutOfRangeException(nameof(rotation));
        LinearType = linearType;
        PrimaryData = primaryData;
        SecondaryData = secondaryData;
        LinearBarHeight = linearBarHeight;
        ModuleWidth = moduleWidth;
        Mode = mode;
        QuietZone = quietZone;
    }
}
