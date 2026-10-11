namespace Bing.EasyPrint.Protocols;

/// <summary>ESC/POS 设备专用设置；代码页编号应以目标打印机手册为准。</summary>
public sealed class EscPosDeviceOptions
{
    private int? _dpi;
    /// <summary>获取或设置输入点阵的分辨率；有机型配置时默认使用其水平 DPI，否则为 203。</summary>
    public int Dpi { get => _dpi ?? Profile?.HorizontalDpi ?? 203; set => _dpi = value; }
    /// <summary>目标机型的能力与图片指令配置；为空时保留既有 GS v 0 行为。</summary>
    public EscPosPrinterProfile Profile { get; set; }
    /// <summary>显式固定的主机字体及替代策略；仅用于 Raster，未设置时使用既有系统字体策略。</summary>
    public RasterFontCatalog RasterFonts { get; set; }
    /// <summary>获取或设置 ESC/POS Raster 条码可读文字的主机字号；未设置时使用 12 点。</summary>
    public int? RasterBarcodeHumanReadableFontSize { get; set; }
    /// <summary>获取或设置 ESC t 使用的设备代码页编号。</summary>
    public byte? CodePageIndex { get; set; }
    /// <summary>获取或设置 GS W 使用的水平运动单位宽度。</summary>
    public int? PrintAreaWidthUnits { get; set; }
    /// <summary>Native 模板水平制表位的逻辑点距；未设置时只接受从左侧原点开始的文字。</summary>
    public int? NativeTabWidthDots { get; set; }
    /// <summary>Native 顺序布局方式；默认保留旧文字行行为，Sequential 显式启用混合收据。</summary>
    public EscPosNativeFlowMode NativeFlow { get; set; } = EscPosNativeFlowMode.Rows;
}
