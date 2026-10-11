using System;
using System.Collections.Generic;
using System.Threading;
using Bing.EasyPrint.Core;

namespace Bing.EasyPrint.Printing;

/// <summary>协议无关的打印文档。</summary>
public sealed class PrintDocument
{
    private readonly PrintStorageBudget _storageOwner;
    private readonly BudgetedList<PrintPage> _pages;

    /// <summary>创建不附带独立存储归属的打印文档。</summary>
    public PrintDocument()
        : this(null)
    {
    }

    /// <summary>创建将模型存储租约归属于指定生命周期的打印文档。</summary>
    internal PrintDocument(PrintStorageBudget owner)
    {
        _storageOwner = owner;
        _pages = new BudgetedList<PrintPage>("Document.Pages", itemBytes: PrintStorageCosts.PageObject,
            owner: owner);
    }

    /// <summary>获取文档页面。</summary>
    public IReadOnlyList<PrintPage> Pages => _pages.AsReadOnly();

    /// <summary>追加页面。</summary>
    public PrintPage AddPage(int width, int height)
    {
        PrintPage page = null;
        _pages.AddFactory(() => page = new PrintPage(width, height, _storageOwner));
        return page;
    }
}

/// <summary>协议无关的打印页面。</summary>
public sealed class PrintPage
{
    private readonly BudgetedList<PrintElement> _elements;
    private int _feedLines;

    /// <summary>获取页面宽度，单位为逻辑打印点。</summary>
    public int Width { get; }
    /// <summary>获取页面高度，单位为逻辑打印点。</summary>
    public int Height { get; }
    /// <summary>获取页面元素。</summary>
    public IReadOnlyList<PrintElement> Elements => _elements.AsReadOnly();
    /// <summary>获取或设置打印份数。</summary>
    public int Copies { get; set; } = 1;
    /// <summary>获取或设置打印后额外进纸行数。</summary>
    public int FeedLines
    {
        get => _feedLines;
        set { _feedLines = value; FeedLinesSpecified = true; }
    }
    internal bool FeedLinesSpecified { get; private set; }
    /// <summary>获取或设置打印后是否切纸。</summary>
    public bool CutAfterPrint { get; set; }
    /// <summary>可选显式裁切参数；仅在启用切纸时使用，渲染期间不得修改。</summary>
    public PrintCutOptions CutOptions { get; set; }

    internal PrintPage(int width, int height)
        : this(width, height, null)
    {
    }

    internal PrintPage(int width, int height, PrintStorageBudget owner)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        _elements = new BudgetedList<PrintElement>("Page.Elements", itemBytes: PrintStorageCosts.ElementObject,
            owner: owner);
    }

    /// <summary>追加元素的独立归属副本，允许同一元素复用于多个页面。</summary>
    /// <remarks>输入元素不会被修改。标准元素的打印数据不可变；自定义元素的额外引用状态须由其实现保持不可变。</remarks>
    public PrintPage Add(PrintElement element, string sourcePath = null)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        var retainedBytes = element.GetRetainedBytes(sourcePath);
        _elements.AddFactory(() => element.ForPage(sourcePath), retainedBytes);
        return this;
    }
}

internal static class PrintStorageCosts
{
    // Estimate object/reference fields retained by the document model itself.
    internal const int PageObject = 64;
    internal const int ElementObject = 64;
}

/// <summary>协议无关的打印元素基类。</summary>
public abstract class PrintElement
{
    /// <summary>元素左上角 X 坐标。</summary>
    public int X { get; }
    /// <summary>元素左上角 Y 坐标。</summary>
    public int Y { get; }
    /// <summary>元素旋转前的预留宽度；文字和原生码图由调用方声明，零表示未知。</summary>
    public int Width { get; }
    /// <summary>元素旋转前的预留高度；文字和二维码由调用方声明，零表示未知。</summary>
    public int Height { get; }
    /// <summary>元素旋转角度。</summary>
    public RotationAngle Rotation { get; }
    /// <summary>获取此元素在模板或调用方文档中的路径。</summary>
    public string SourcePath { get; internal set; }

    internal PrintElement ForPage(string sourcePath)
    {
        var copy = (PrintElement)MemberwiseClone();
        copy.SourcePath = sourcePath;
        return copy;
    }

    internal long GetRetainedBytes(string sourcePath)
    {
        var bytes = PrintStorageBudget.TextBytes(sourcePath);
        switch (this)
        {
            case PrintTextElement text:
                bytes = checked(bytes + PrintStorageBudget.TextBytes(text.Text));
                bytes = checked(bytes + PrintStorageBudget.TextBytes(text.FontFamily));
                bytes = checked(bytes + PrintStorageBudget.TextBytes(text.DeviceFontName));
                break;
            case PrintBarcodeElement barcode:
                bytes = checked(bytes + PrintStorageBudget.TextBytes(barcode.Data));
                break;
            case PrintQrCodeElement qrCode:
                bytes = checked(bytes + PrintStorageBudget.TextBytes(qrCode.Data));
                break;
        }
        return bytes;
    }

    protected PrintElement(int x, int y, int width, int height, RotationAngle rotation = RotationAngle.None)
    {
        if (x < 0 || y < 0) throw new ArgumentOutOfRangeException(x < 0 ? nameof(x) : nameof(y));
        if (width < 0 || height < 0) throw new ArgumentOutOfRangeException(width < 0 ? nameof(width) : nameof(height));
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Rotation = rotation;
    }
}

/// <summary>协议无关的文本元素。</summary>
public sealed class PrintTextElement : PrintElement
{
    /// <summary>获取文本内容。</summary>
    public string Text { get; }
    /// <summary>获取字体大小。</summary>
    public int FontSize { get; }
    /// <summary>获取是否加粗。</summary>
    public bool Bold { get; }
    /// <summary>获取是否下划线。</summary>
    public bool Underline { get; }
    /// <summary>是否反白输出文字。</summary>
    public bool Reverse { get; }
    /// <summary>获取 ESC/POS Raster 的系统字体族或固定资源别名；其他 Provider 会拒绝非空值。</summary>
    public string FontFamily { get; }
    /// <summary>获取设备字体标识；旧构造器用于 TSPL/ZPL，语义字体选择还支持 CPCL 24/55。</summary>
    public string DeviceFontName { get; }
    /// <summary>获取统一字体描述；使用旧构造器创建时为 <see langword="null" />。</summary>
    public PrintFont Font { get; }

    public PrintTextElement(int x, int y, string text, int fontSize, bool bold = false, bool underline = false,
        RotationAngle rotation = RotationAngle.None, int width = 0, int height = 0, string fontFamily = null,
        bool reverse = false, string deviceFontName = null)
        : base(x, y, width, height, rotation)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        Text = text;
        FontSize = fontSize;
        Bold = bold;
        Underline = underline;
        Reverse = reverse;
        FontFamily = fontFamily;
        DeviceFontName = deviceFontName;
    }

    /// <summary>
    /// 创建使用统一字体描述的文本元素。
    /// </summary>
    /// <param name="x">元素左上角的 X 坐标。</param>
    /// <param name="y">元素左上角的 Y 坐标。</param>
    /// <param name="text">要输出的文本内容。</param>
    /// <param name="fontSize">字体大小，单位由目标协议的 Provider 解释。</param>
    /// <param name="font">字体来源和名称；不能为 <see langword="null" />。</param>
    /// <param name="bold">是否请求加粗。</param>
    /// <param name="underline">是否请求下划线。</param>
    /// <param name="rotation">文本旋转角度。</param>
    /// <param name="width">文本预留宽度；零表示未知。</param>
    /// <param name="height">文本预留高度；零表示未知。</param>
    /// <param name="reverse">是否反白输出文本。</param>
    /// <exception cref="ArgumentNullException"><paramref name="font" /> 为 <see langword="null" />。</exception>
    public PrintTextElement(int x, int y, string text, int fontSize, PrintFont font, bool bold = false,
        bool underline = false, RotationAngle rotation = RotationAngle.None, int width = 0, int height = 0,
        bool reverse = false)
        : this(x, y, text, fontSize, bold, underline, rotation, width, height,
            FontFamilyFor(font), reverse, DeviceFontNameFor(font))
    {
        Font = font;
    }

    private static string FontFamilyFor(PrintFont font)
    {
        if (font == null) throw new ArgumentNullException(nameof(font));
        return font.Kind == PrintFontKind.Host ? font.Name : null;
    }

    private static string DeviceFontNameFor(PrintFont font)
    {
        if (font == null) throw new ArgumentNullException(nameof(font));
        return font.Kind == PrintFontKind.Device ? font.Name : null;
    }
}

/// <summary>协议无关的线元素。</summary>
public sealed class PrintLineElement : PrintElement
{
    /// <summary>线条终点 X 坐标。</summary>
    public int EndX { get; }
    /// <summary>线条终点 Y 坐标。</summary>
    public int EndY { get; }
    /// <summary>线条宽度。</summary>
    public int LineWidth { get; }

    public PrintLineElement(int x, int y, int endX, int endY, int lineWidth = 1)
        : base(x, y, Math.Abs(endX - x), Math.Abs(endY - y))
    {
        if (lineWidth <= 0) throw new ArgumentOutOfRangeException(nameof(lineWidth));
        if (endX == x && endY == y) throw new ArgumentException("线段长度必须大于零。");
        EndX = endX;
        EndY = endY;
        LineWidth = lineWidth;
    }
}

/// <summary>协议无关的矩形元素。</summary>
public sealed class PrintRectangleElement : PrintElement
{
    /// <summary>边框线宽。</summary>
    public int LineWidth { get; }
    /// <summary>是否填充；旧构造器在 CPCL 下保留反色端点语义。</summary>
    public bool Filled { get; }

    // 实心填充工厂及模板使用精确点数；旧 CPCL 文档的 IL 端点与反色语义保持兼容。
    internal bool ExactFillBounds { get; private set; }

    /// <summary>创建按精确宽高绘制实心黑色区域的矩形。</summary>
    /// <param name="x">填充区域左边界，单位为打印点。</param>
    /// <param name="y">填充区域上边界，单位为打印点。</param>
    /// <param name="width">填充宽度，必须为正数。</param>
    /// <param name="height">填充高度，必须为正数。</param>
    /// <returns>占用半开区域 [x, x + width) × [y, y + height) 的实心矩形。</returns>
    /// <remarks>
    /// 此入口与模板的 Filled=true 语义一致。旧构造器的 CPCL filled=true 输出仍保留
    /// IL 反色及端点行为；新代码需要跨协议实心填充时应使用本工厂。
    /// </remarks>
    public static PrintRectangleElement CreateFilled(int x, int y, int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if ((long)x + width > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(width));
        if ((long)y + height > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(height));
        return new PrintRectangleElement(x, y, width, height, filled: true) { ExactFillBounds = true };
    }

    public PrintRectangleElement(int x, int y, int width, int height, int lineWidth = 1, bool filled = false)
        : base(x, y, width, height)
    {
        if (lineWidth <= 0) throw new ArgumentOutOfRangeException(nameof(lineWidth));
        LineWidth = lineWidth;
        Filled = filled;
    }
}

/// <summary>协议无关的一维条码元素。</summary>
/// <remarks>
/// ESC/POS Raster Code 49 的坐标表示含固有静区和边界分隔条的完整符号外框；
/// 其余协议遵循各自 Provider 对原生条码坐标的定义。
/// </remarks>
public sealed class PrintBarcodeElement : PrintElement
{
    // CPCL 模板适配层将用户顺时针 270 度转换为原生 VB 方向；该标记不对外暴露，
    // 使 Provider 能区分这个内部布局转换和直连统一模型的角度请求。
    internal bool IsCpclTemplateRotation { get; private set; }

    /// <summary>获取条码占用区四周的显式静区；null 沿用调用方的旧留白行为。</summary>
    public PrintQuietZone QuietZone { get; }
    /// <summary>条码类型。</summary>
    public BarcodeType Type { get; }
    /// <summary>条码内容。</summary>
    public string Data { get; }
    /// <summary>条码高度。</summary>
    public int BarHeight { get; }
    /// <summary>条码窄条宽度。</summary>
    public int NarrowWidth { get; }
    /// <summary>条码宽条宽度。</summary>
    public int WideWidth { get; }
    /// <summary>旧 CPCL 模板使用的比例枚举；未指定时沿用 WideWidth 的旧映射。</summary>
    public int? CpclRatioCode { get; }
    /// <summary>是否输出条码下方的可读文字；null 表示沿用目标协议默认行为。</summary>
    public bool? HumanReadable { get; }
    /// <summary>为设备绘制的条码可读文字预留的高度，单位为打印点；零表示不额外预留。</summary>
    public int HumanReadableReserveHeight { get; }

    public PrintBarcodeElement(int x, int y, BarcodeType type, string data, int barHeight, int narrowWidth = 2,
        int wideWidth = 2, RotationAngle rotation = RotationAngle.None, int? cpclRatioCode = null,
        bool? humanReadable = null, int humanReadableReserveHeight = 0, int reservedWidth = 0)
        : base(x, y, reservedWidth, checked(barHeight + humanReadableReserveHeight), rotation)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (barHeight <= 0 || narrowWidth <= 0 || wideWidth <= 0) throw new ArgumentOutOfRangeException();
        if (humanReadableReserveHeight < 0) throw new ArgumentOutOfRangeException(nameof(humanReadableReserveHeight));
        if (cpclRatioCode.HasValue && !(cpclRatioCode.Value >= 0 && cpclRatioCode.Value <= 4 ||
                                        cpclRatioCode.Value >= 20 && cpclRatioCode.Value <= 30))
            throw new ArgumentOutOfRangeException(nameof(cpclRatioCode));
        Type = type;
        Data = data;
        BarHeight = barHeight;
        NarrowWidth = narrowWidth;
        WideWidth = wideWidth;
        CpclRatioCode = cpclRatioCode;
        HumanReadable = humanReadable;
        HumanReadableReserveHeight = humanReadableReserveHeight;
    }

    /// <summary>创建包含显式静区校验的条码；坐标与预留宽度仍指向编码区，静区向外扩展。</summary>
    public PrintBarcodeElement(int x, int y, BarcodeType type, string data, int barHeight,
        PrintQuietZone quietZone, int narrowWidth = 2, int wideWidth = 2,
        RotationAngle rotation = RotationAngle.None, int? cpclRatioCode = null,
        bool? humanReadable = null, int humanReadableReserveHeight = 0, int reservedWidth = 0)
        : this(x, y, type, data, barHeight, narrowWidth, wideWidth, rotation, cpclRatioCode,
            humanReadable, humanReadableReserveHeight, reservedWidth)
    {
        QuietZone = quietZone ?? throw new ArgumentNullException(nameof(quietZone));
    }

    internal void MarkCpclTemplateRotation()
    {
        IsCpclTemplateRotation = true;
    }
}

/// <summary>协议无关的二维码元素。</summary>
public sealed class PrintQrCodeElement : PrintElement
{
    /// <summary>获取编码区四周的显式静区；null 沿用调用方的旧留白行为。</summary>
    public PrintQuietZone QuietZone { get; }
    /// <summary>二维码内容。</summary>
    public string Data { get; }
    /// <summary>二维码模块尺寸。</summary>
    public int ModuleSize { get; }
    /// <summary>二维码纠错级别。</summary>
    public QrCodeCorrectionLevel ErrorLevel { get; }

    public PrintQrCodeElement(int x, int y, string data, int moduleSize, QrCodeCorrectionLevel errorLevel,
        int width = 0, int height = 0, RotationAngle rotation = RotationAngle.None)
        : base(x, y, width, height, rotation)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (moduleSize <= 0) throw new ArgumentOutOfRangeException(nameof(moduleSize));
        if (!Enum.IsDefined(typeof(QrCodeCorrectionLevel), errorLevel)) throw new ArgumentOutOfRangeException(nameof(errorLevel));
        Data = data;
        ModuleSize = moduleSize;
        ErrorLevel = errorLevel;
    }

    /// <summary>创建包含显式静区校验的二维码；坐标与预留尺寸指向编码区，静区向外扩展。</summary>
    public PrintQrCodeElement(int x, int y, string data, int moduleSize, QrCodeCorrectionLevel errorLevel,
        PrintQuietZone quietZone, int width = 0, int height = 0, RotationAngle rotation = RotationAngle.None)
        : this(x, y, data, moduleSize, errorLevel, width, height, rotation)
    {
        QuietZone = quietZone ?? throw new ArgumentNullException(nameof(quietZone));
    }
}

/// <summary>协议无关的单色位图元素。</summary>
public sealed class PrintImageElement : PrintElement
{
    private readonly byte[] _raster;
    /// <summary>获取按行排列的 MSB 优先单色位图副本；修改返回数组不会改变元素。</summary>
    public byte[] Raster => Core.BudgetedBuffer.Copy(_raster, "Document.ImageSnapshot", CancellationToken.None);
    // 内置 Provider 仅只读访问，避免输出时再次分配公共访问器的副本。
    internal byte[] RasterData => _raster;
    /// <summary>每行字节数。</summary>
    public int RowBytes { get; }
    /// <summary>是否使用 CPCL 的二进制 CG 图片命令；其他协议忽略此编码偏好。</summary>
    public bool Compressed { get; }

    // 模型保留旋转前尺寸；所有图片命令使用主机旋转后的左上角占位。
    internal int OutputWidth => Rotation == RotationAngle.Rotate90 || Rotation == RotationAngle.Rotate270 ? Height : Width;
    internal int OutputHeight => Rotation == RotationAngle.Rotate90 || Rotation == RotationAngle.Rotate270 ? Width : Height;
    internal int OutputRowBytes => checked((int)(((long)OutputWidth + 7) / 8));

    public PrintImageElement(int x, int y, int width, int height, byte[] raster, bool compressed = false)
        : this(x, y, width, height, raster, RotationAngle.None, compressed)
    {
    }

    /// <summary>创建按顺时针旋转的单色图片；宽高和位图为旋转前数据，坐标为旋转后占位左上角。</summary>
    /// <remarks>旋转由主机执行，旧构造器保留原始方向和输出字节；非零角度的行尾填充位清零。</remarks>
    public PrintImageElement(int x, int y, int width, int height, byte[] raster,
        RotationAngle rotation, bool compressed = false)
        : base(x, y, width, height, rotation)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
        if (raster == null) throw new ArgumentNullException(nameof(raster));
        if (!Enum.IsDefined(typeof(RotationAngle), rotation)) throw new ArgumentOutOfRangeException(nameof(rotation));
        RowBytes = checked((int)(((long)width + 7) / 8));
        if (raster.Length != checked(RowBytes * height)) throw new ArgumentException("图片数据长度与尺寸不匹配。", nameof(raster));
        _raster = Core.BudgetedBuffer.Copy(raster, "Document.ImageData", CancellationToken.None);
        Compressed = compressed;
    }
}
