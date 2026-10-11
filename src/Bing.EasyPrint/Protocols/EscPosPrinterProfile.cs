using System;

namespace Bing.EasyPrint.Protocols;

/// <summary>选择设备实际支持的单色图片指令。</summary>
public enum EscPosImageCommand
{
    /// <summary>使用逐行光栅 GS v 0。</summary>
    RasterGsV0,
    /// <summary>使用按列编码的 ESC *，按带输出并控制进纸。</summary>
    BitImageEscStar
}

/// <summary>ESC * 的点阵密度模式。</summary>
public enum EscPosBitImageMode
{
    /// <summary>8 点单密度。</summary>
    SingleDensity8 = 0,
    /// <summary>8 点双密度。</summary>
    DoubleDensity8 = 1,
    /// <summary>24 点单密度。</summary>
    SingleDensity24 = 32,
    /// <summary>24 点双密度。</summary>
    DoubleDensity24 = 33
}

/// <summary>ESC/POS 原生条码可读文字的设备位置。</summary>
public enum EscPosBarcodeTextPosition
{
    /// <summary>不输出可读文字。</summary>
    None = 0,
    /// <summary>输出在条码上方。</summary>
    Above = 1,
    /// <summary>输出在条码下方。</summary>
    Below = 2,
    /// <summary>同时输出在条码上方和下方。</summary>
    Both = 3
}

/// <summary>
/// 描述目标 ESC/POS 机型的图片格式、物理分辨率和可用指令。
/// 必须按设备手册填写；它不探测打印机，也不声称设备已经验收。
/// </summary>
public sealed class EscPosPrinterProfile
{
    /// <summary>配置的名称，用于识别目标机型。</summary>
    public string Name { get; set; } = "Custom";
    /// <summary>可选的原生文字契约；与主机 Raster 字体能力分开声明。</summary>
    public EscPosTextProfile Text { get; set; }
    /// <summary>图片指令类型。</summary>
    public EscPosImageCommand ImageCommand { get; set; }
    /// <summary>ESC * 的点阵模式；使用 GS v 0 时保留默认值。</summary>
    public EscPosBitImageMode BitImageMode { get; set; } = EscPosBitImageMode.DoubleDensity24;
    /// <summary>图片水平分辨率；模板和输入位图的点数使用此分辨率。</summary>
    public int HorizontalDpi { get; set; } = 203;
    /// <summary>图片垂直分辨率；ESC * 编码时显式转换输入位图的纵向采样。</summary>
    public int VerticalDpi { get; set; } = 203;
    /// <summary>ESC 3 / ESC J 的每英寸进纸单位数。</summary>
    public int MotionUnitsPerInch { get; set; } = 203;
    /// <summary>设备可打印宽度（水平点数）；为空时只检查协议字段范围。</summary>
    public int? PrintableWidthDots { get; set; }
    /// <summary>设备手册声明的 GS1-128 payload 字符上限；统计规范化数据及 ASCII 29 分隔符，不含符号 framing。</summary>
    public int? MaxGs1LinearPayloadCharacters { get; set; }
    /// <summary>设备手册声明的 GS1 DataBar Expanded payload 字符上限；为空时只检查协议/编码器范围。</summary>
    public int? MaxGs1DataBarExpandedPayloadCharacters { get; set; }
    /// <summary>单条图片指令的最大数据字节数（不含命令头）；为空时不分块。</summary>
    public int? MaxImageBlockBytes { get; set; }
    /// <summary>是否支持 GS V 切纸。</summary>
    public bool SupportsCut { get; set; } = true;
    /// <summary>已确认的切纸形状；null 保留原有全切/半切入口，空数组禁用切纸。</summary>
    public PrintCutMode[] SupportedCutModes { get; set; }
    /// <summary>是否支持进纸到切刀位置后裁切的 GS V Function B；默认不声明支持。</summary>
    public bool SupportsFeedAndCut { get; set; }
    /// <summary>是否支持 GS k 原生条码。</summary>
    public bool SupportsNativeBarcode { get; set; } = true;
    /// <summary>是否支持 GS k 原生条码的设备可读文字（HRI）。</summary>
    public bool SupportsNativeBarcodeHumanReadable { get; set; } = true;
    /// <summary>
    /// 已确认支持原生 HRI 的条码制式；null 表示不按制式收窄，空数组表示全部禁用。
    /// Raster 模式的 HRI 由主机绘制，不使用此列表。
    /// </summary>
    public BarcodeType[] SupportedNativeBarcodeHumanReadableTypes { get; set; }
    /// <summary>
    /// 已确认支持的 ESC/POS Native HRI 位置；null 表示不按位置收窄，空数组表示所有 HRI 位置均禁用。
    /// <see cref="EscPosBarcodeTextPosition.None"/> 不需要列入列表；Raster 模式忽略此列表。
    /// </summary>
    public EscPosBarcodeTextPosition[] SupportedNativeBarcodeHumanReadablePositions { get; set; }
    /// <summary>是否支持 GS ( k 原生二维码。</summary>
    public bool SupportsNativeQrCode { get; set; } = true;
    /// <summary>是否支持 GS W 打印区域。</summary>
    public bool SupportsPrintAreaWidth { get; set; } = true;
    /// <summary>是否支持 GS ! 文字倍数。</summary>
    public bool SupportsTextSize { get; set; } = true;
    /// <summary>ESC * 绘制期间是否使用单向打印；结束后恢复双向打印。</summary>
    public bool UnidirectionalBitImage { get; set; }

    /// <summary>
    /// 创建需要调用方确认纸宽的 8 点针式 ESC/POS 配置。
    /// 默认使用 180x60 DPI 和 1/144 英寸进纸单位，不启用切刀或原生条码。
    /// </summary>
    public static EscPosPrinterProfile EightDotImpact(int printableWidthDots,
        int horizontalDpi = 180, int verticalDpi = 60, int motionUnitsPerInch = 144)
        => new EscPosPrinterProfile {
            Name = "8-dot impact", ImageCommand = EscPosImageCommand.BitImageEscStar,
            BitImageMode = EscPosBitImageMode.DoubleDensity8, HorizontalDpi = horizontalDpi,
            VerticalDpi = verticalDpi, MotionUnitsPerInch = motionUnitsPerInch,
            PrintableWidthDots = printableWidthDots, SupportsCut = false,
            SupportsNativeBarcode = false, SupportsNativeBarcodeHumanReadable = false,
            SupportsNativeQrCode = false,
            SupportsPrintAreaWidth = false, SupportsTextSize = false, UnidirectionalBitImage = true
        };

    internal int BandHeight => (int)BitImageMode < 32 ? 8 : 24;

    internal bool SupportsNativeBarcodeHumanReadableType(BarcodeType type)
        => SupportsNativeBarcodeHumanReadable &&
           (SupportedNativeBarcodeHumanReadableTypes == null ||
            Array.IndexOf(SupportedNativeBarcodeHumanReadableTypes, type) >= 0);

    internal bool SupportsNativeBarcodeHumanReadablePosition(EscPosBarcodeTextPosition position)
        => position == EscPosBarcodeTextPosition.None ||
           SupportsNativeBarcodeHumanReadable &&
           (SupportedNativeBarcodeHumanReadablePositions == null ||
            Array.IndexOf(SupportedNativeBarcodeHumanReadablePositions, position) >= 0);

    internal EscPosPrinterProfile Snapshot()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("设备配置名称不能为空。", nameof(Name));
        if (SupportedCutModes != null)
        {
            var seenModes = new System.Collections.Generic.HashSet<PrintCutMode>();
            foreach (var mode in SupportedCutModes)
                if (!Enum.IsDefined(typeof(PrintCutMode), mode) || !seenModes.Add(mode))
                    throw new ArgumentException("切纸模式必须有效且不能重复。", nameof(SupportedCutModes));
        }
        if (SupportedNativeBarcodeHumanReadableTypes != null)
        {
            var seenTypes = new System.Collections.Generic.HashSet<BarcodeType>();
            foreach (var type in SupportedNativeBarcodeHumanReadableTypes)
                if (!Enum.IsDefined(typeof(BarcodeType), type) || !seenTypes.Add(type))
                    throw new ArgumentException("原生 HRI 条码制式必须有效且不能重复。",
                        nameof(SupportedNativeBarcodeHumanReadableTypes));
        }
        if (SupportedNativeBarcodeHumanReadablePositions != null)
        {
            var seenPositions = new System.Collections.Generic.HashSet<EscPosBarcodeTextPosition>();
            foreach (var position in SupportedNativeBarcodeHumanReadablePositions)
                if (!Enum.IsDefined(typeof(EscPosBarcodeTextPosition), position) ||
                    position == EscPosBarcodeTextPosition.None || !seenPositions.Add(position))
                    throw new ArgumentException("原生 HRI 位置必须为 Above、Below 或 Both，且不能重复。",
                        nameof(SupportedNativeBarcodeHumanReadablePositions));
        }
        if (!Enum.IsDefined(typeof(EscPosImageCommand), ImageCommand)) throw new ArgumentOutOfRangeException(nameof(ImageCommand));
        if (!Enum.IsDefined(typeof(EscPosBitImageMode), BitImageMode)) throw new ArgumentOutOfRangeException(nameof(BitImageMode));
        if (HorizontalDpi <= 0 || VerticalDpi <= 0 || MotionUnitsPerInch <= 0)
            throw new ArgumentOutOfRangeException(nameof(HorizontalDpi), "设备分辨率和进纸单位必须为正数。");
        if (ImageCommand == EscPosImageCommand.RasterGsV0 && HorizontalDpi != VerticalDpi)
            throw new NotSupportedException("GS v 0 配置必须使用相同的横纵分辨率；非正方形点阵请显式使用 ESC *。");
        if (ImageCommand == EscPosImageCommand.RasterGsV0 &&
            (BitImageMode != EscPosBitImageMode.DoubleDensity24 || UnidirectionalBitImage))
            throw new ArgumentException("GS v 0 不使用 ESC * 点阵模式或单向打印设置。", nameof(ImageCommand));
        if (PrintableWidthDots.HasValue && PrintableWidthDots.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(PrintableWidthDots));
        Gs1BarcodeCapacity.ValidateLimit(MaxGs1LinearPayloadCharacters, nameof(MaxGs1LinearPayloadCharacters));
        Gs1BarcodeCapacity.ValidateLimit(MaxGs1DataBarExpandedPayloadCharacters,
            nameof(MaxGs1DataBarExpandedPayloadCharacters));
        if (MaxImageBlockBytes.HasValue && MaxImageBlockBytes.Value <
            (ImageCommand == EscPosImageCommand.BitImageEscStar ? BandHeight / 8 : 1))
            throw new ArgumentOutOfRangeException(nameof(MaxImageBlockBytes), "图片分块预算必须至少容纳一列或一个字节。");
        var copy = (EscPosPrinterProfile)MemberwiseClone();
        copy.Text = Text?.Snapshot();
        copy.SupportedCutModes = SupportedCutModes == null ? null : (PrintCutMode[])SupportedCutModes.Clone();
        copy.SupportedNativeBarcodeHumanReadableTypes = SupportedNativeBarcodeHumanReadableTypes == null
            ? null : (BarcodeType[])SupportedNativeBarcodeHumanReadableTypes.Clone();
        copy.SupportedNativeBarcodeHumanReadablePositions = SupportedNativeBarcodeHumanReadablePositions == null
            ? null : (EscPosBarcodeTextPosition[])SupportedNativeBarcodeHumanReadablePositions.Clone();
        return copy;
    }
}
