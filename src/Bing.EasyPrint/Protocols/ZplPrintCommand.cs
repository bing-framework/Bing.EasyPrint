using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

/// <summary>生成 Zebra ZPL 标签打印指令。</summary>
public sealed partial class ZplPrintCommand : ProtocolPrintCommandBase<ZplPrintCommand>
{
    /// <summary>获取 ZPL 的基础能力描述。</summary>
    public override PrintProtocol Protocol => PrintProtocol.Zpl;
    public override PrintCapabilities Capabilities { get; }
    private readonly ZplMediaOptions _mediaOptions;
    private readonly LabelDeviceContract _profile;
    private readonly ZplCutterProfile _cutter;
    private bool _formatOpen;
    private bool _formatPrinted;
    private bool _delayedCut;
    private char? _restrictedCutMode;
    private int? _restrictedKioskUncutMillimeters;
    private int? _restrictedKioskCutMarginMillimeters;
    private bool _restrictedKioskCutCommandSeen;
    private bool _allowRasterizedTextStyles;
    private int _labelWidth;
    private int _labelHeight;
    private bool _hasLabelSize;
    private bool _validatePageBounds = true;
    private PrintOrientation? _restrictedOrientation;
    private readonly BudgetedList<(int End, bool Cut)> _outputBoundaries =
        new BudgetedList<(int End, bool Cut)>("Zpl.OutputBoundaries", slotBytes: 16);

    public ZplPrintCommand() : this((Encoding)null, null) { }
    public ZplPrintCommand(Encoding encoding = null) : this(encoding, null) { }

    internal ZplPrintCommand DisablePageBoundsValidation()
    {
        _validatePageBounds = false;
        return this;
    }

    /// <summary>
    /// 使用指定编码和介质设置创建 ZPL 命令构建器。
    /// </summary>
    /// <param name="encoding">写入文本时使用的编码；为空时使用 UTF-8。</param>
    /// <param name="mediaOptions">介质和设备设置；为空时使用默认设置。</param>
    public ZplPrintCommand(Encoding encoding, ZplMediaOptions mediaOptions)
        : base(encoding ?? Encoding.GetEncoding("utf-8"))
    {
        if (mediaOptions != null)
        {
            mediaOptions.Validate();
            _mediaOptions = mediaOptions.Clone();
            _allowRasterizedTextStyles = mediaOptions.AllowRasterizedTextStyles;
        }
        _restrictedOrientation = _mediaOptions?.Orientation;

        _profile = _mediaOptions?.Profile?.Snapshot();
        _cutter = _mediaOptions?.Profile?.Cutter?.Snapshot();
        _profile?.ValidateEncoding(Encoding);
        Capabilities = new PrintCapabilities(PrintProtocol.Zpl,
            _mediaOptions == null ? ZplMediaOptions.DefaultDpi : _mediaOptions.Dpi,
            true, _profile?.Images ?? true, _profile?.QrCode ?? true, _profile?.Rotation ?? true, _profile?.Cut ?? true,
            supportsText: _profile?.Fonts == null || _profile.Fonts.Count != 0,
            supportsGraphics: _profile?.Graphics ?? true, supportsBarcode: _profile?.Barcode ?? true,
            supportsBoldText: _allowRasterizedTextStyles && _profile?.Images != false,
            supportsUnderlineText: _allowRasterizedTextStyles && _profile?.Images != false,
            supportsReverseText: _allowRasterizedTextStyles && _profile?.Images != false,
            supportsHostFontFamily: _allowRasterizedTextStyles && _profile?.Images != false);
        Capabilities.ApplyLabelProfile(_profile);
        Capabilities.ZplCutter = _cutter;
        if (_cutter != null) Capabilities.CutModes = Array.AsReadOnly((PrintCutMode[])_cutter.SupportedCutModes.Clone());
    }

    public ZplPrintCommand Start()
    {
        if (_formatOpen)
            throw new InvalidOperationException(_cutter == null
                ? "须先 End 当前格式，再开始下一格式。"
                : "须先 End 当前受控格式，再开始下一格式。");
        if (Encoding.CodePage != Encoding.UTF8.CodePage && Encoding.CodePage != Encoding.ASCII.CodePage)
            throw new NotSupportedException($"ZPL 尚未映射编码 {Encoding.WebName} 的设备字符集；请使用 UTF-8 或 ASCII。");
        WriteRaw(Encoding.CodePage == Encoding.UTF8.CodePage ? "^XA\r\n^CI28\r\n" : "^XA\r\n");
        _formatOpen = true;
        _formatPrinted = false;
        _delayedCut = false;
        _restrictedCutMode = null;
        _restrictedKioskUncutMillimeters = null;
        _restrictedKioskCutMarginMillimeters = null;
        _restrictedKioskCutCommandSeen = false;
        return this;
    }
    public ZplPrintCommand End()
    {
        if (!_formatOpen)
            throw new InvalidOperationException(_cutter == null
                ? "格式须先 Start。"
                : "受控格式须先 Start。");
        if (_cutter != null)
        {
            var explicitPrint = _formatPrinted;
            if (explicitPrint) ValidateRestrictedFormatClose();
            var delayedCut = explicitPrint && _delayedCut;
            var end = (!_formatPrinted ? ZplCutEncoder.NeutralPrint(1, _cutter) : "") + "^XZ\r\n";
            // 边界扩容和动作记录均先预留，超限时不留下已关闭但缺少边界的格式。
            _outputBoundaries.AddFactory(() =>
            {
                WriteRaw(end);
                return (RecordedActionCount, delayedCut);
            });
            _formatPrinted = true;
            _delayedCut = delayedCut;
            if (!explicitPrint)
            {
                _restrictedCutMode = 'T';
                _restrictedKioskUncutMillimeters = null;
                _restrictedKioskCutMarginMillimeters = null;
                _restrictedKioskCutCommandSeen = false;
            }
        }
        else WriteRawLine("^XZ");
        _formatOpen = false;
        return this;
    }
    public ZplPrintCommand Label(int width, int height, int? printSpeed = null)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
        _profile?.ValidatePage(width, height);
        var effectivePrintSpeed = printSpeed ?? _mediaOptions?.PrintSpeed;
        if (effectivePrintSpeed.HasValue && effectivePrintSpeed.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(printSpeed));
        var settings = new StringBuilder();
        settings.Append($"^PW{width}\r\n^LL{height}\r\n");
        if (effectivePrintSpeed.HasValue) settings.Append($"^PR{effectivePrintSpeed.Value}\r\n");
        if (_mediaOptions?.Orientation is PrintOrientation orientation)
            settings.Append($"^PO{ZplOrientation(orientation)}\r\n");
        if (_mediaOptions?.MediaTracking is char mediaTracking)
            settings.Append($"^MN{mediaTracking}\r\n");
        if (_mediaOptions?.Darkness is int darkness)
            settings.Append($"^MD{darkness}\r\n");
        var result = WriteRaw(settings.ToString());
        _labelWidth = width;
        _labelHeight = height;
        _hasLabelSize = true;
        return result;
    }
    public ZplPrintCommand Text(int x, int y, string data, int fontHeight = 30, int fontWidth = 30, string font = "0", int rotation = 0)
    {
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidateField(data, nameof(data), '^', '~'); ValidateZplFont(font);
        _profile?.ValidateText(font, data);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "文字旋转");
        if (fontHeight <= 0 || fontWidth <= 0) throw new ArgumentOutOfRangeException();
        long textWidth = 0;
        foreach (var scalar in PrintUnicodeScalars.Enumerate(data))
            textWidth = checked(textWidth + fontWidth);
        ValidatePageExtent(x, y, textWidth, fontHeight, rotation,
            "ZPL 原生文字", estimated: true);
        var orientation = Orientation(rotation);
        return WriteRawLine($"^FO{x},{y}^A{font}{orientation},{fontHeight},{fontWidth}^FD{data}^FS");
    }

    /// <summary>
    /// 将带粗体或下划线的文字以主机位图写入 ZPL。
    /// </summary>
    /// <remarks>
    /// 只有显式设置 <see cref="ZplMediaOptions.AllowRasterizedTextStyles" /> 后才允许使用；
    /// 输出使用 ^GFA，文字不再依赖打印机内置字体。
    /// </remarks>
    public ZplPrintCommand TextRasterized(int x, int y, string data, int fontSize,
        bool bold = false, bool underline = false, int rotation = 0, string fontFamily = null,
        CancellationToken cancellationToken = default(CancellationToken))
        => TextRasterized(x, y, data, fontSize, bold, underline, false, rotation, fontFamily,
            cancellationToken);

    /// <summary>将文字样式光栅化到 ZPL；反白文字使用黑色布局背景和白色字形。</summary>
    public ZplPrintCommand TextRasterized(int x, int y, string data, int fontSize,
        bool bold, bool underline, bool reverse, int rotation = 0, string fontFamily = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        cancellationToken.ThrowIfCancellationRequested();
        if ((bold || underline || reverse) && !_allowRasterizedTextStyles)
            throw new NotSupportedException("ZPL 粗体、下划线或反白需要显式设置 AllowRasterizedTextStyles。");
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        ValidateField(data, nameof(data), '^', '~');
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270)
            throw new ArgumentOutOfRangeException(nameof(rotation));
        _profile?.Require(_profile.Images, "文字样式光栅化");
        if (rotation != 0) _profile?.Require(_profile.Rotation, "文字旋转");
        var raster = EscPosRasterCanvas.RasterizeText(data, fontSize, bold, underline, fontFamily, rotation,
            reverse, cancellationToken: cancellationToken);
        return GraphicField(x, y, raster.RowBytes, raster.Height, raster.Data, cancellationToken);
    }
    /// <summary>按端点绘制线段；显式启用后斜线以单色位图输出。</summary>
    public ZplPrintCommand Line(int x0, int y0, int x1, int y1, int thickness = 2)
        => Line(x0, y0, x1, y1, thickness, CancellationToken.None);

    /// <summary>绘制线段；斜线光栅化过程支持取消。</summary>
    public ZplPrintCommand Line(int x0, int y0, int x1, int y1, int thickness,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _profile?.Require(_profile.Graphics, "线框绘图");
        ValidateCoordinate(x0, nameof(x0)); ValidateCoordinate(y0, nameof(y0));
        ValidateCoordinate(x1, nameof(x1)); ValidateCoordinate(y1, nameof(y1));
        if (thickness <= 0) throw new ArgumentOutOfRangeException(nameof(thickness));
        CheckedRasterDimension(thickness, nameof(thickness));
        if (x0 == x1)
        {
            ValidatePageExtent(x0, Math.Min(y0, y1), thickness,
                (long)Math.Max(y0, y1) - Math.Min(y0, y1) + 1, 0, "ZPL 竖线", estimated: false);
            return WriteRawLineCancellable($"^FO{x0},{Math.Min(y0, y1)}^GB{thickness},{CheckedRasterDimension((long)Math.Max(y0, y1) - Math.Min(y0, y1) + 1, nameof(thickness))},{thickness}^FS", cancellationToken);
        }
        if (y0 == y1)
        {
            ValidatePageExtent(Math.Min(x0, x1), y0,
                (long)Math.Max(x0, x1) - Math.Min(x0, x1) + 1, thickness, 0, "ZPL 横线", estimated: false);
            return WriteRawLineCancellable($"^FO{Math.Min(x0, x1)},{y0}^GB{CheckedRasterDimension((long)Math.Max(x0, x1) - Math.Min(x0, x1) + 1, nameof(thickness))},{thickness},{thickness}^FS", cancellationToken);
        }
        if (_mediaOptions?.AllowRasterizedLines != true)
            throw new NotSupportedException("ZPL 斜线需要显式设置 ZplMediaOptions.AllowRasterizedLines。");
        var radius = thickness / 2;
        var left = Math.Max(0, Math.Min(x0, x1) - radius);
        var top = Math.Max(0, Math.Min(y0, y1) - radius);
        var width = CheckedRasterDimension((long)Math.Max(x0, x1) + radius - left + 1, nameof(thickness));
        var height = CheckedRasterDimension((long)Math.Max(y0, y1) + radius - top + 1, nameof(thickness));
        ValidatePageEdges((long)Math.Min(x0, x1) - radius, (long)Math.Min(y0, y1) - radius,
            (long)Math.Max(x0, x1) + radius + 1, (long)Math.Max(y0, y1) + radius + 1,
            "ZPL 斜线", estimated: false);
        ValidateRasterBufferSize(width, height);
        _profile?.ValidateImage(left, top, ((long)width + 7) / 8, height);
        var canvas = new EscPosRasterCanvas(width, height, null, cancellationToken);
        canvas.DrawLine(x0 - left, y0 - top, x1 - left, y1 - top, thickness);
        cancellationToken.ThrowIfCancellationRequested();
        return GraphicField(left, top, canvas.RowBytes, height,
            canvas.ToBytes(cancellationToken), cancellationToken);
    }

    public ZplPrintCommand Box(int x0, int y0, int x1, int y1, int thickness = 2)
    {
        _profile?.Require(_profile.Graphics, "线框绘图");
        ValidateCoordinate(x0, nameof(x0)); ValidateCoordinate(y0, nameof(y0));
        ValidateCoordinate(x1, nameof(x1)); ValidateCoordinate(y1, nameof(y1));
        if (x1 <= x0 || y1 <= y0) throw new ArgumentOutOfRangeException();
        if (thickness <= 0) throw new ArgumentOutOfRangeException(nameof(thickness));
        ValidatePageExtent(x0, y0, (long)x1 - x0, (long)y1 - y0, 0,
            "ZPL 线框", estimated: false);
        return WriteRawLine($"^FO{x0},{y0}^GB{x1 - x0},{y1 - y0},{thickness}^FS");
    }

    /// <summary>
    /// 绘制填充矩形。
    /// </summary>
    /// <param name="x">矩形左边界坐标，单位为 dots。</param>
    /// <param name="y">矩形上边界坐标，单位为 dots。</param>
    /// <param name="width">矩形宽度，单位为 dots；范围为 1 到 32000。</param>
    /// <param name="height">矩形高度，单位为 dots；范围为 1 到 32000。</param>
    /// <returns>当前命令构建器。</returns>
    /// <remarks>
    /// 宽度和高度表示半开区域 [x, x + width) × [y, y + height)，与
    /// <see cref="Box(int, int, int, int, int)"/> 使用的右下端点坐标不同。
    /// </remarks>
    public ZplPrintCommand FillRectangle(int x, int y, int width, int height)
        => FillRectangle(x, y, width, height, CancellationToken.None);

    /// <summary>
    /// 绘制填充矩形，并支持在记录前取消操作。
    /// </summary>
    /// <param name="x">矩形左边界坐标，单位为 dots。</param>
    /// <param name="y">矩形上边界坐标，单位为 dots。</param>
    /// <param name="width">矩形宽度，单位为 dots；范围为 1 到 32000。</param>
    /// <param name="height">矩形高度，单位为 dots；范围为 1 到 32000。</param>
    /// <param name="cancellationToken">用于取消记录的令牌。</param>
    /// <returns>当前命令构建器。</returns>
    /// <remarks>
    /// 宽度和高度表示半开区域 [x, x + width) × [y, y + height)，与
    /// <see cref="Box(int, int, int, int, int)"/> 使用的右下端点坐标不同；所有尺寸均以 dots 表示。
    /// </remarks>
    public ZplPrintCommand FillRectangle(int x, int y, int width, int height,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _profile?.Require(_profile.Graphics, "填充矩形");
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        if (width < 1 || width > 32000) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 1 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));

        var right = (long)x + width;
        var bottom = (long)y + height;
        if (right > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(width), "矩形右边界超出可表示范围。");
        if (bottom > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(height), "矩形下边界超出可表示范围。");
        _profile?.ValidatePage(right, bottom);
        ValidatePageEdges(x, y, right, bottom, "ZPL 填充矩形", estimated: false);
        return WriteRawLineCancellable(
            $"^FO{x},{y}^GB{width},{height},{Math.Min(width, height)},B,0^FS",
            cancellationToken);
    }

    public ZplPrintCommand Barcode(int x, int y, string data, int height = 80, bool printText = true, int moduleWidth = 2)
        => Barcode(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以原生 N/R/I/B 方向生成 Code 128 条码；角度以顺时针度数表示。</summary>
    public ZplPrintCommand Barcode(int x, int y, string data, int height, bool printText, int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Code128);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidateCode128Data(data);
        if (height <= 0 || moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException();
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^BC{orientation},{height},{(printText ? "Y" : "N")},N,N^FD{data}^FS",
            x, y, BarcodeType.Code128, data, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^BC</c> 输出 GS1-128（UCC/EAN-128）条码。</summary>
    /// <remarks>
    /// ZPL 用 <c>&gt;;&gt;8</c> 选择 Code 128 子集 C 并插入首个 FNC1；输入中的 ASCII 29
    /// 分隔符会转换为后续的 <c>&gt;8</c>。应用标识和业务校验由调用方按 GS1 规则提供。
    /// </remarks>
    public ZplPrintCommand BarcodeUccEan128(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodeUccEan128(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度输出 GS1-128（UCC/EAN-128）条码。</summary>
    public ZplPrintCommand BarcodeUccEan128(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.UccEan128);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        var normalized = NormalizeUccEan128Data(data, nameof(data));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^BC{orientation},{height},{(printText ? "Y" : "N")},N,N^FD>;>8{normalized}^FS",
            x, y, BarcodeType.UccEan128, normalized.Replace(">8", "\u001D").Replace(">0", ">"),
            height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^B3</c> 指令生成 Code 39 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">Code 39 基础字符集数据；起止星号由设备自动生成。</param>
    /// <param name="height">条码高度，单位为点。</param>
    /// <param name="printText">是否打印下方可读文字。</param>
    /// <param name="moduleWidth">窄条宽度，范围为 1 到 10 点。</param>
    /// <returns>当前命令构建器。</returns>
    public ZplPrintCommand Barcode39(int x, int y, string data, int height = 80, bool printText = true,
        int moduleWidth = 2)
        => Barcode39(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成 Code 39 条码。</summary>
    public ZplPrintCommand Barcode39(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Code39);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidateCode39Data(data);
        if (height <= 0 || height > 32000 || moduleWidth <= 0 || moduleWidth > 10)
            throw new ArgumentOutOfRangeException();
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^B3{orientation},N,{height},{(printText ? "Y" : "N")},N^FD{data}^FS",
            x, y, BarcodeType.Code39, data, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^B1</c> 指令生成 Code 11 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">Code 11 数据；仅接受 ASCII 数字和连字符。</param>
    /// <param name="height">条码高度，单位为点，范围为 1 到 32000。</param>
    /// <param name="printText">是否打印下方可读文字。</param>
    /// <param name="moduleWidth">窄条宽度，范围为 1 到 10 点。</param>
    /// <returns>当前命令构建器。</returns>
    /// <remarks>
    /// ZPL 的 Code 11 参数使用两个 Modulo-11 校验位（<c>e=N</c>），由打印机生成；
    /// 主机只发送业务数据，避免将设备校验位重复编码。
    /// </remarks>
    public ZplPrintCommand BarcodeCode11(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodeCode11(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成 Code 11 条码。</summary>
    public ZplPrintCommand BarcodeCode11(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Code11);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidateCode11Data(data);
        if (height <= 0 || height > 32000 || moduleWidth <= 0 || moduleWidth > 10)
            throw new ArgumentOutOfRangeException();
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^B1{orientation},N,{height},{(printText ? "Y" : "N")},N^FD{data}^FS",
            x, y, BarcodeType.Code11, data, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^BP</c> 指令生成数字 Plessey 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">Plessey 数据；当前统一模型只接受非空 ASCII 数字。</param>
    /// <param name="height">条码高度，单位为点，范围为 1 到 32000。</param>
    /// <param name="printText">是否打印下方可读文字。</param>
    /// <param name="moduleWidth">窄条宽度，范围为 1 到 10 点；ZPL 默认使用 3:1 宽窄比。</param>
    /// <returns>当前命令构建器。</returns>
    /// <remarks>
    /// <c>e=N</c> 保留调用方提供的数据，不请求打印机额外输出校验位；<c>^BY</c> 的默认
    /// 3:1 比例落在 Plessey 规定的 2:1 到 3:1 范围内。
    /// </remarks>
    public ZplPrintCommand BarcodePlessey(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodePlessey(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成数字 Plessey 条码。</summary>
    public ZplPrintCommand BarcodePlessey(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Plessey);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidatePlesseyData(data);
        if (height <= 0 || height > 32000 || moduleWidth <= 0 || moduleWidth > 10)
            throw new ArgumentOutOfRangeException();
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^BP{orientation},N,{height},{(printText ? "Y" : "N")},N^FD{data}^FS",
            x, y, BarcodeType.Plessey, data, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^B4</c> 指令生成 Code 49 堆叠条码。</summary>
    /// <remarks>
    /// Code 49 的起始模式默认交给打印机自动选择；可读文字使用 ZPL 的 B（下方）位置。
    /// ZPL 的 <c>^B4</c> 使用单一模块宽度和固定条空比例，统一模型的宽条参数不会被静默传入。
    /// </remarks>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">1 到 81 个可安全嵌入 ZPL 字段的 ASCII 字符。</param>
    /// <param name="height">目标 Code 49 单行高度，单位为点，范围为 1 到 32000；会向上取整到模块宽度的整数倍。</param>
    /// <param name="printText">是否在条码下方输出可读文字。</param>
    /// <param name="moduleWidth">窄模块宽度，范围为 1 到 10 点。</param>
    /// <returns>当前命令构建器。</returns>
    public ZplPrintCommand BarcodeCode49(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodeCode49(x, y, data, height, printText, moduleWidth, 'A', 0);

    /// <summary>以指定顺时针角度生成 Code 49 条码。</summary>
    public ZplPrintCommand BarcodeCode49(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
        => BarcodeCode49(x, y, data, height, printText, moduleWidth, 'A', rotation);

    /// <summary>以指定起始模式和顺时针角度生成 Code 49 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">1 到 81 个可安全嵌入 ZPL 字段的 ASCII 字符。</param>
    /// <param name="height">目标 Code 49 单行高度，单位为点，范围为 1 到 32000；会向上取整到模块宽度的整数倍。</param>
    /// <param name="printText">是否在条码下方输出可读文字。</param>
    /// <param name="moduleWidth">窄模块宽度，范围为 1 到 10 点。</param>
    /// <param name="startMode">ZPL 起始模式：A 或 0 到 5。</param>
    /// <param name="rotation">顺时针旋转角度，只允许 0、90、180 或 270 度。</param>
    public ZplPrintCommand BarcodeCode49(int x, int y, string data, int height, bool printText,
        int moduleWidth, char startMode, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Code49);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidateCode49Data(data);
        if (height <= 0 || height > 32000)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10)
            throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var mode = char.ToUpperInvariant(startMode);
        if (mode != 'A' && (mode < '0' || mode > '5'))
            throw new ArgumentOutOfRangeException(nameof(startMode), "ZPL Code 49 起始模式必须为 A 或 0 到 5。");
        var orientation = Orientation(rotation);
        var interpretation = printText ? "B" : "N";
        var heightMultiplier = PrintProtocolProviderBase.ZplCode49HeightMultiplierForDots(height, moduleWidth);
        var actualRowHeightDots = checked(heightMultiplier * moduleWidth);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^B4{orientation},{heightMultiplier},{interpretation},{mode}^FD{data}^FS",
            x, y, BarcodeType.Code49, data, actualRowHeightDots, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^BR</c> 输出 GS1 DataBar Omnidirectional。</summary>
    /// <remarks>
    /// ZPL 的 <c>^BR</c> 没有普通一维条码的 HRI 参数；调用方需要可读文字时应显式追加文字元素。
    /// 统一模板因此要求该制式的 <c>HumanReadable</c> 不得显式设为 true。
    /// </remarks>
    public ZplPrintCommand BarcodeGs1DataBarOmnidirectional(int x, int y, string data,
        int height = 80, int moduleWidth = 2)
        => BarcodeGs1DataBarOmnidirectional(x, y, data, height, moduleWidth, 0);

    /// <summary>以指定顺时针角度输出 GS1 DataBar Omnidirectional。</summary>
    public ZplPrintCommand BarcodeGs1DataBarOmnidirectional(int x, int y, string data,
        int height, int moduleWidth, int rotation)
        => BarcodeGs1DataBar(x, y, data, height, moduleWidth, 1, 22, rotation);

    /// <summary>以 ZPL 原生 <c>^BR</c> 输出 GS1 DataBar Truncated。</summary>
    public ZplPrintCommand BarcodeGs1DataBarTruncated(int x, int y, string data,
        int height = 80, int moduleWidth = 2)
        => BarcodeGs1DataBarTruncated(x, y, data, height, moduleWidth, 0);

    /// <summary>以指定顺时针角度输出 GS1 DataBar Truncated。</summary>
    public ZplPrintCommand BarcodeGs1DataBarTruncated(int x, int y, string data,
        int height, int moduleWidth, int rotation)
        => BarcodeGs1DataBar(x, y, data, height, moduleWidth, 2, 22, rotation);

    /// <summary>以 ZPL 原生 <c>^BR</c> 输出 GS1 DataBar Limited。</summary>
    public ZplPrintCommand BarcodeGs1DataBarLimited(int x, int y, string data,
        int height = 80, int moduleWidth = 2)
        => BarcodeGs1DataBarLimited(x, y, data, height, moduleWidth, 0);

    /// <summary>以指定顺时针角度输出 GS1 DataBar Limited。</summary>
    public ZplPrintCommand BarcodeGs1DataBarLimited(int x, int y, string data,
        int height, int moduleWidth, int rotation)
        => BarcodeGs1DataBar(x, y, data, height, moduleWidth, 5, 22, rotation);

    /// <summary>以 ZPL 原生 <c>^BR</c> 输出 GS1 DataBar Expanded。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">GS1 DataBar Expanded 数据。</param>
    /// <param name="height">条码高度，单位为点。</param>
    /// <param name="moduleWidth">放大倍数，范围为 1 到 10。</param>
    /// <param name="segmentWidth">每行段数；必须为 2–22 的偶数。</param>
    public ZplPrintCommand BarcodeGs1DataBarExpanded(int x, int y, string data,
        int height = 80, int moduleWidth = 2, int segmentWidth = 22)
        => BarcodeGs1DataBarExpanded(x, y, data, height, moduleWidth, segmentWidth, 0);

    /// <summary>以指定顺时针角度输出 GS1 DataBar Expanded。</summary>
    public ZplPrintCommand BarcodeGs1DataBarExpanded(int x, int y, string data,
        int height, int moduleWidth, int segmentWidth, int rotation)
        => BarcodeGs1DataBar(x, y, data, height, moduleWidth, 6, segmentWidth, rotation);

    private ZplPrintCommand BarcodeGs1DataBar(int x, int y, string data, int height,
        int moduleWidth, int symbology, int segmentWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生 GS1 DataBar");
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth < 1 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        if (symbology != 1 && symbology != 2 && symbology != 5 && symbology != 6)
            throw new ArgumentOutOfRangeException(nameof(symbology));
        if (symbology == 6 && (segmentWidth < 2 || segmentWidth > 22 || (segmentWidth & 1) != 0))
            throw new ArgumentOutOfRangeException(nameof(segmentWidth), "ZPL GS1 DataBar Expanded 的分段宽度必须为 2–22 的偶数。");
        var normalized = NormalizeGs1DataBarData(data, symbology, nameof(data));
        var orientation = Orientation(rotation);
        var parameters = symbology == 6
            ? $"{orientation},{symbology},{moduleWidth},1,{height},{segmentWidth}"
            : $"{orientation},{symbology},{moduleWidth},1,{height}";
        var barcodeType = symbology switch
        {
            1 => BarcodeType.Gs1DataBarOmnidirectional,
            2 => BarcodeType.Gs1DataBarTruncated,
            5 => BarcodeType.Gs1DataBarLimited,
            _ => BarcodeType.Gs1DataBarExpanded
        };
        return WriteBarcodeField($"^FO{x},{y}^BR{parameters}^FD{normalized}^FS",
            x, y, barcodeType, normalized, height, moduleWidth, rotation, humanReadable: false);
    }

    /// <summary>以 ZPL 原生 <c>^BA</c> 指令生成 Code 93 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">Code 93 的非空可打印 ASCII 数据。</param>
    /// <param name="height">条码高度，单位为点，范围为 1 到 32000。</param>
    /// <param name="printText">是否打印下方可读文字。</param>
    /// <param name="moduleWidth">窄条宽度，范围为 1 到 10 点。</param>
    /// <returns>当前命令构建器。</returns>
    public ZplPrintCommand Barcode93(int x, int y, string data, int height = 80, bool printText = true,
        int moduleWidth = 2)
        => Barcode93(x, y, data, height, printText, moduleWidth, false, 0);

    /// <summary>以指定顺时针角度生成 Code 93 条码。</summary>
    public ZplPrintCommand Barcode93(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
        => Barcode93(x, y, data, height, printText, moduleWidth, false, rotation);

    /// <summary>以指定方向和校验位显示选项生成 Code 93 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">Code 93 的非空可打印 ASCII 数据。</param>
    /// <param name="height">条码高度，单位为点，范围为 1 到 32000。</param>
    /// <param name="printText">是否打印下方可读文字。</param>
    /// <param name="moduleWidth">窄条宽度，范围为 1 到 10 点。</param>
    /// <param name="printCheckDigit">是否在可读文字中显示设备生成的校验位。</param>
    /// <param name="rotation">顺时针旋转角度，只允许 0、90、180 或 270 度。</param>
    public ZplPrintCommand Barcode93(int x, int y, string data, int height, bool printText,
        int moduleWidth, bool printCheckDigit, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Code93);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidateCode93Data(data);
        if (height <= 0 || height > 32000 || moduleWidth <= 0 || moduleWidth > 10)
            throw new ArgumentOutOfRangeException();
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^BA{orientation},{height},{(printText ? "Y" : "N")},N,{(printCheckDigit ? "Y" : "N")}^FD{data}^FS",
            x, y, BarcodeType.Code93, data, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^B8</c> 指令生成 EAN-8 条码。</summary>
    /// <remarks>输入可为 7 位数据或带有效校验位的 8 位数据；写入 ZPL 时只发送 7 位数据。</remarks>
    public ZplPrintCommand BarcodeEan8(int x, int y, string data, int height = 80, bool printText = true,
        int moduleWidth = 2)
        => BarcodeEan8(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成 EAN-8 条码。</summary>
    public ZplPrintCommand BarcodeEan8(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
        => RetailBarcode(x, y, data, height, printText, moduleWidth, rotation, "B8", 7, 8, false, BarcodeType.Ean8);

    /// <summary>以 ZPL 原生 <c>^BE</c> 指令生成 EAN-13 条码。</summary>
    /// <remarks>输入可为 12 位数据或带有效校验位的 13 位数据；写入 ZPL 时只发送 12 位数据。</remarks>
    public ZplPrintCommand BarcodeEan13(int x, int y, string data, int height = 80, bool printText = true,
        int moduleWidth = 2)
        => BarcodeEan13(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成 EAN-13 条码。</summary>
    public ZplPrintCommand BarcodeEan13(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
        => RetailBarcode(x, y, data, height, printText, moduleWidth, rotation, "BE", 12, 13, false, BarcodeType.Ean13);

    /// <summary>以 ZPL 原生 <c>^BU</c> 指令生成 UPC-A 条码。</summary>
    /// <remarks>输入可为 11 位数据或带有效校验位的 12 位数据；写入 ZPL 时只发送 11 位数据。</remarks>
    public ZplPrintCommand BarcodeUpcA(int x, int y, string data, int height = 80, bool printText = true,
        int moduleWidth = 2)
        => BarcodeUpcA(x, y, data, height, printText, moduleWidth, true, 0);

    /// <summary>以指定顺时针角度生成 UPC-A 条码。</summary>
    public ZplPrintCommand BarcodeUpcA(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
        => BarcodeUpcA(x, y, data, height, printText, moduleWidth, true, rotation);

    /// <summary>以指定方向和校验位显示选项生成 UPC-A 条码。</summary>
    public ZplPrintCommand BarcodeUpcA(int x, int y, string data, int height, bool printText,
        int moduleWidth, bool printCheckDigit, int rotation)
        => RetailBarcode(x, y, data, height, printText, moduleWidth, rotation, "BU", 11, 12, printCheckDigit, BarcodeType.UpcA);

    /// <summary>以 ZPL 原生 <c>^B9</c> 指令生成 UPC-E 条码。</summary>
    /// <remarks>
    /// 输入使用统一模型的 6 位压缩数据或带号码系统的 7 位数据；号码系统必须为 0，
    /// 命令会展开为 ZPL 要求的 10 位厂商/商品数据，校验位由设备计算。
    /// </remarks>
    public ZplPrintCommand BarcodeUpcE(int x, int y, string data, int height = 80, bool printText = true,
        int moduleWidth = 2)
        => BarcodeUpcE(x, y, data, height, printText, moduleWidth, true, 0);

    /// <summary>以指定顺时针角度生成 UPC-E 条码。</summary>
    public ZplPrintCommand BarcodeUpcE(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
        => BarcodeUpcE(x, y, data, height, printText, moduleWidth, true, rotation);

    /// <summary>以指定方向和校验位显示选项生成 UPC-E 条码。</summary>
    public ZplPrintCommand BarcodeUpcE(int x, int y, string data, int height, bool printText,
        int moduleWidth, bool printCheckDigit, int rotation)
        => RetailBarcode(x, y, data, height, printText, moduleWidth, rotation, "B9", 0, 0, printCheckDigit, BarcodeType.UpcE);

    /// <summary>以 ZPL 原生 <c>^BK</c> 指令生成 Codabar 条码。</summary>
    /// <remarks>输入必须包含 A-D 起止符；起止符通过 ^BK 参数发送，正文写入 ^FD。</remarks>
    public ZplPrintCommand BarcodeCodabar(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodeCodabar(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成 Codabar 条码。</summary>
    public ZplPrintCommand BarcodeCodabar(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Codabar);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var normalized = NormalizeCodabarData(data, nameof(data));
        var orientation = Orientation(rotation);
        var start = normalized[0];
        var stop = normalized[normalized.Length - 1];
        var body = normalized.Substring(1, normalized.Length - 2);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^BK{orientation},N,{height},{(printText ? "Y" : "N")},N,{start},{stop}^FD{body}^FS",
            x, y, BarcodeType.Codabar, normalized, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^B2</c> 指令生成 Interleaved 2 of 5 条码。</summary>
    /// <remarks>输入必须为偶数位数字；可选由打印机计算并打印 Mod 10 校验位。</remarks>
    public ZplPrintCommand BarcodeI2OF5(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodeI2OF5(x, y, data, height, printText, moduleWidth, false, 0);

    /// <summary>以指定顺时针角度生成 Interleaved 2 of 5 条码。</summary>
    public ZplPrintCommand BarcodeI2OF5(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
        => BarcodeI2OF5(x, y, data, height, printText, moduleWidth, false, rotation);

    /// <summary>以指定方向和校验位显示选项生成 Interleaved 2 of 5 条码。</summary>
    public ZplPrintCommand BarcodeI2OF5(int x, int y, string data, int height, bool printText,
        int moduleWidth, bool printCheckDigit, int rotation)
    {
        return BarcodeI2OF5Core(x, y, data, height, printText, moduleWidth, printCheckDigit, rotation,
            BarcodeType.I2OF5);
    }

    private ZplPrintCommand BarcodeI2OF5Core(int x, int y, string data, int height, bool printText,
        int moduleWidth, bool printCheckDigit, int rotation, BarcodeType barcodeType)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, barcodeType);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var normalized = NormalizeI2OF5Data(data, nameof(data));
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^B2{orientation},{height},{(printText ? "Y" : "N")},N,{(printCheckDigit ? "Y" : "N")}^FD{normalized}^FS",
            x, y, barcodeType, normalized, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^B2</c> 计算并打印 CPCL 语义的 I2OF5C 校验位。</summary>
    /// <remarks>
    /// ZPL 设备负责计算 Mod 10 校验位；为保持统一 I2OF5C 语义，偶数位输入会在发送前补一个前导零，
    /// 奇数位输入保持不变。旧的 <see cref="BarcodeI2OF5(int, int, string, int, bool, int, bool, int)"/>
    /// 入口保留原有的偶数位校验和字节行为。
    /// </remarks>
    public ZplPrintCommand BarcodeI2OF5C(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodeI2OF5C(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成带 Mod 10 校验位的 I2OF5C 条码。</summary>
    public ZplPrintCommand BarcodeI2OF5C(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.I2OF5C);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var normalized = NormalizeI2OF5CheckDigitData(data, nameof(data));
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^B2{orientation},{height},{(printText ? "Y" : "N")},N,Y^FD{normalized}^FS",
            x, y, BarcodeType.I2OF5C, normalized, height, moduleWidth, rotation, printText);
    }

    /// <summary>以主机生成的 German Post 4/9 校验位输出 I2OF5G。</summary>
    /// <remarks>
    /// ZPL 原生 <c>^B2</c> 只表达基础 Interleaved 2 of 5；调用方的 CPCL I2OF5G 语义
    /// 在主机侧生成或校验 4/9 校验位，再关闭 ZPL 的 Mod 10 选项，避免使用错误的校验算法。
    /// </remarks>
    public ZplPrintCommand BarcodeI2OF5G(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodeI2OF5G(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度输出带 German Post 4/9 校验位的 I2OF5G。</summary>
    public ZplPrintCommand BarcodeI2OF5G(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        var normalized = NormalizeI2OF5GermanData(data, nameof(data));
        return BarcodeI2OF5Core(x, y, normalized, height, printText, moduleWidth, false, rotation,
            BarcodeType.I2OF5G);
    }

    /// <summary>以 ZPL 原生 <c>^BM</c> 指令生成 MSI 条码。</summary>
    /// <remarks>
    /// 默认使用 A 方案，即保留输入数字且不追加校验位；需要设备计算校验位时，
    /// 请使用带校验方案参数的重载并选择 B、C 或 D。
    /// </remarks>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">MSI 非空 ASCII 数字数据，A 方案最多 13 位，其他方案最多 14 位。</param>
    /// <param name="height">条码高度，单位为点，范围为 1 到 32000。</param>
    /// <param name="printText">是否打印下方可读文字。</param>
    /// <param name="moduleWidth">窄条宽度，范围为 1 到 10 点。</param>
    /// <returns>当前命令构建器。</returns>
    public ZplPrintCommand BarcodeMsi(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodeMsi(x, y, data, height, printText, moduleWidth, 'A', false, 0);

    /// <summary>以指定方向和校验方案生成 MSI 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">MSI 非空 ASCII 数字数据。</param>
    /// <param name="height">条码高度，单位为点，范围为 1 到 32000。</param>
    /// <param name="printText">是否打印下方可读文字。</param>
    /// <param name="moduleWidth">窄条宽度，范围为 1 到 10 点。</param>
    /// <param name="checkDigit">ZPL MSI 校验方案：A、B、C 或 D。</param>
    /// <param name="printCheckDigit">是否在可读文字中显示设备生成的校验位。</param>
    /// <param name="rotation">顺时针旋转角度，只允许 0、90、180 或 270 度。</param>
    public ZplPrintCommand BarcodeMsi(int x, int y, string data, int height, bool printText,
        int moduleWidth, char checkDigit, bool printCheckDigit, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Msi);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidateMsiData(data, checkDigit, nameof(data));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var orientation = Orientation(rotation);
        var scheme = char.ToUpperInvariant(checkDigit);
        var measuredData = scheme == 'A' ? data : data + "0";
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^BM{orientation},{scheme},{height},{(printText ? "Y" : "N")},N,{(printCheckDigit ? "Y" : "N")}^FD{data}^FS",
            x, y, BarcodeType.Msi, measuredData, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^BZ</c> 指令生成 POSTNET 邮政条码。</summary>
    /// <remarks>统一模型只接受 5、9 或 11 位邮政数字；连字符会在写入 ZPL 前移除。</remarks>
    public ZplPrintCommand BarcodePostnet(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodePostnet(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成 POSTNET 条码。</summary>
    public ZplPrintCommand BarcodePostnet(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Postnet);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var normalized = NormalizePostnetData(data, nameof(data));
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^BZ{orientation},{height},{(printText ? "Y" : "N")},N^FD{normalized}^FS",
            x, y, BarcodeType.Postnet, normalized, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^B5</c> 指令生成 PLANET 邮政条码。</summary>
    /// <remarks>PLANET 必须为 12 或 14 位数字并包含有效校验位；连字符会在写入 ZPL 前移除。</remarks>
    public ZplPrintCommand BarcodePlanet(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodePlanet(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成 PLANET 条码。</summary>
    public ZplPrintCommand BarcodePlanet(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.Planet);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var normalized = NormalizePlanetData(data, nameof(data));
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^B5{orientation},{height},{(printText ? "Y" : "N")},N^FD{normalized}^FS",
            x, y, BarcodeType.Planet, normalized, height, moduleWidth, rotation, printText);
    }

    /// <summary>以 ZPL 原生 <c>^BZ</c> 类型 3 指令生成 USPS Intelligent Mail（IMb）条码。</summary>
    /// <remarks>统一模型接受 20、25、29 或 31 位数字；连字符会在写入 ZPL 前移除，IMb 不含校验位。</remarks>
    public ZplPrintCommand BarcodeIntelligentMail(int x, int y, string data, int height = 80,
        bool printText = false, int moduleWidth = 2)
        => BarcodeIntelligentMail(x, y, data, height, printText, moduleWidth, 0);

    /// <summary>以指定顺时针角度生成 USPS Intelligent Mail（IMb）条码。</summary>
    public ZplPrintCommand BarcodeIntelligentMail(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, BarcodeType.IntelligentMail);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var normalized = NormalizeIntelligentMailData(data, nameof(data));
        var orientation = Orientation(rotation);
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^BZ{orientation},{height},{(printText ? "Y" : "N")},N,3^FD{normalized}^FS",
            x, y, BarcodeType.IntelligentMail, normalized, height, moduleWidth, rotation, printText);
    }

    /// <summary>兼容常见小写缩写命名的 Interleaved 2 of 5 入口。</summary>
    public ZplPrintCommand BarcodeI2of5(int x, int y, string data, int height = 80,
        bool printText = true, int moduleWidth = 2)
        => BarcodeI2OF5(x, y, data, height, printText, moduleWidth);

    /// <summary>兼容常见小写缩写命名的旋转 Interleaved 2 of 5 入口。</summary>
    public ZplPrintCommand BarcodeI2of5(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation)
        => BarcodeI2OF5(x, y, data, height, printText, moduleWidth, rotation);

    private void RequireHumanReadable(bool enabled, BarcodeType type)
    {
        if (enabled) _profile?.Require(_profile.SupportsBarcodeHumanReadable(type), "条码可读文字");
    }

    private ZplPrintCommand RetailBarcode(int x, int y, string data, int height, bool printText,
        int moduleWidth, int rotation, string command, int payloadLength, int fullLength,
        bool printCheckDigit, BarcodeType barcodeType)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        RequireHumanReadable(printText, barcodeType);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        if (height <= 0 || height > 32000) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0 || moduleWidth > 10) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        var normalized = command == "B9"
            ? NormalizeUpcEData(data, nameof(data))
            : NormalizeRetailPayload(data, payloadLength, fullLength, nameof(data));
        var orientation = Orientation(rotation);
        var suffix = command == "B9" || command == "BU"
            ? $",{(printCheckDigit ? "Y" : "N")}"
            : string.Empty;
        return WriteBarcodeField($"^FO{x},{y}^BY{moduleWidth}^{command}{orientation},{height},{(printText ? "Y" : "N")},N{suffix}^FD{normalized}^FS",
            x, y, barcodeType, normalized, height, moduleWidth, rotation, printText);
    }
    public ZplPrintCommand QrCode(int x, int y, string data, int magnification = 4, char errorCorrection = 'M')
        => QrCode(x, y, data, magnification, errorCorrection, 0);

    /// <summary>以原生 BQ 指令输出二维码；角度按顺时针度数表示。</summary>
    public ZplPrintCommand QrCode(int x, int y, string data, int magnification, char errorCorrection, int rotation)
    {
        _profile?.Require(_profile.QrCode, "原生二维码");
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidateNonEmptyField(data, nameof(data), '^', '~');
        if (magnification < 1 || magnification > 10) throw new ArgumentOutOfRangeException(nameof(magnification));
        if (errorCorrection != 'L' && errorCorrection != 'M' && errorCorrection != 'Q' && errorCorrection != 'H')
            throw new ArgumentOutOfRangeException(nameof(errorCorrection));
        var correction = errorCorrection == 'L' ? QrCodeCorrectionLevel.L : errorCorrection == 'M'
            ? QrCodeCorrectionLevel.M : errorCorrection == 'Q' ? QrCodeCorrectionLevel.Q : QrCodeCorrectionLevel.H;
        var measuredSize = EscPosRasterCanvas.MeasureQrCodeSize(data, magnification, correction);
        ValidatePageExtent(x, y, measuredSize, measuredSize, rotation,
            "ZPL 二维码", estimated: true);
        var orientation = Orientation(rotation);
        _profile?.Require(_profile.SupportsQrCodeRotation((RotationAngle)rotation), "二维码旋转");
        return WriteRawLine($"^FO{x},{y}^BQ{orientation},2,{magnification}^FD{errorCorrection}A,{data}^FS");
    }

    /// <summary>写入 ZPL ^GFA ASCII 十六进制图片数据。</summary>
    public ZplPrintCommand GraphicField(int x, int y, int totalBytes, int rowBytes, string asciiHex)
        => GraphicField(x, y, totalBytes, rowBytes, asciiHex, CancellationToken.None);

    /// <summary>写入 ZPL ^GFA ASCII 十六进制图片数据；校验过程支持取消。</summary>
    public ZplPrintCommand GraphicField(int x, int y, int totalBytes, int rowBytes, string asciiHex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        ValidateField(asciiHex, nameof(asciiHex), '^', '~', ',');
        if (totalBytes <= 0 || rowBytes <= 0) throw new ArgumentOutOfRangeException();
        if (rowBytes > totalBytes) throw new ArgumentOutOfRangeException(nameof(rowBytes));
        ValidatePageExtent(x, y, (long)rowBytes * 8,
            ((long)totalBytes + rowBytes - 1) / rowBytes, 0, "ZPL 图形字段", estimated: false);
        _profile?.ValidateImage(x, y, rowBytes, ((long)totalBytes + rowBytes - 1) / rowBytes);
        if (totalBytes > int.MaxValue / 2) throw new ArgumentOutOfRangeException(nameof(totalBytes), "图像十六进制数据超出可表示范围。");
        for (var i = 0; i < asciiHex.Length; i++)
        {
            if ((i & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!Uri.IsHexDigit(asciiHex[i])) throw new ArgumentException("图像数据必须为十六进制文本。", nameof(asciiHex));
        }
        if ((long)asciiHex.Length != (long)totalBytes * 2) throw new ArgumentException("图像数据长度与总字节数不匹配。", nameof(asciiHex));
        cancellationToken.ThrowIfCancellationRequested();
        var prefix = $"^FO{x},{y}^GFA,{totalBytes},{totalBytes},{rowBytes},";
        const string suffix = "^FS";
        var commandBytes = checked(((long)prefix.Length + asciiHex.Length + suffix.Length) * 2L);
        var commandReservation = PrintResourceScope.Reserve(commandBytes, "Zpl.GraphicFieldCommand");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = prefix + asciiHex + suffix;
            cancellationToken.ThrowIfCancellationRequested();
            ValidateEncodable(command, nameof(asciiHex));
            var result = Add(w => { w.Write(command); w.Write("\r\n"); });
            commandReservation = null;
            return result;
        }
        catch
        {
            if (commandReservation != null) commandReservation.Dispose();
            throw;
        }
    }

    public ZplPrintCommand GraphicField(int x, int y, int widthBytes, int height, byte[] raster)
        => GraphicField(x, y, widthBytes, height, raster, CancellationToken.None);

    /// <summary>写入 ZPL ^GFA 二进制光栅；输入快照和十六进制转换支持取消。</summary>
    public ZplPrintCommand GraphicField(int x, int y, int widthBytes, int height, byte[] raster,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (raster == null) throw new ArgumentNullException(nameof(raster));
        ValidateCoordinate(x, nameof(x)); ValidateCoordinate(y, nameof(y));
        if (widthBytes <= 0) throw new ArgumentOutOfRangeException(nameof(widthBytes));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        var expected = CheckedRasterByteCount(widthBytes, height);
        if (expected > int.MaxValue / 2) throw new ArgumentOutOfRangeException(nameof(height), "图像十六进制数据超出可表示范围。");
        if (raster.Length != expected) throw new ArgumentException("图片数据长度与尺寸不匹配。", nameof(raster));
        _profile?.ValidateImage(x, y, widthBytes, height);
        using var operation = PrintResourceScope.BeginTemporary();
        var copy = BudgetedBuffer.Copy(raster, "Zpl.GraphicFieldSnapshot", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var result = GraphicField(x, y, copy.Length, widthBytes, ToHex(copy, cancellationToken), cancellationToken);
        operation?.CommitReservations();
        return result;
    }
    public ZplPrintCommand Image(int x, int y, int widthBytes, int height, byte[] raster)
        => GraphicField(x, y, widthBytes, height, raster);
    public ZplPrintCommand Image(int x, int y, int widthBytes, int height, byte[] raster,
        CancellationToken cancellationToken)
        => GraphicField(x, y, widthBytes, height, raster, cancellationToken);
    public ZplPrintCommand Print(int quantity = 1)
    {
        ZplCutEncoder.ValidateQuantity(quantity);
        RequireUnprintedFormat();
        WriteRaw(ZplCutEncoder.NeutralPrint(quantity, _cutter));
        _formatPrinted = true;
        _delayedCut = false;
        _restrictedCutMode = _cutter == null ? (char?)null : 'T';
        _restrictedKioskUncutMillimeters = null;
        _restrictedKioskCutMarginMillimeters = null;
        _restrictedKioskCutCommandSeen = false;
        return this;
    }
    /// <summary>按声明策略在一个打印批次末尾裁切；Delayed 的控制命令由 BuildOutput 独立返回。</summary>
    public ZplPrintCommand PrintWithCut(PrintCutOptions options, int quantity = 1)
    {
        var commands = ZplCutEncoder.CutPrint(quantity, options, _cutter);
        RequireUnprintedFormat();
        WriteRaw(commands);
        _formatPrinted = true;
        _delayedCut = _cutter.Strategy == ZplCutStrategy.Delayed;
        _restrictedCutMode = _cutter.Strategy switch
        {
            ZplCutStrategy.Cutter => 'C',
            ZplCutStrategy.Delayed => 'D',
            _ => 'K'
        };
        if (_cutter.Strategy == ZplCutStrategy.Kiosk)
        {
            _restrictedKioskUncutMillimeters = options.Mode == PrintCutMode.Full
                ? 0 : _cutter.KioskPartialUncutMillimeters;
            _restrictedKioskCutMarginMillimeters = _cutter.KioskCutMarginMillimeters;
            _restrictedKioskCutCommandSeen = true;
        }
        else
        {
            _restrictedKioskUncutMillimeters = null;
            _restrictedKioskCutMarginMillimeters = null;
            _restrictedKioskCutCommandSeen = false;
        }
        return this;
    }
    /// <summary>保留旧裸 ^CN 入口；配置受控切刀时须使用 PrintWithCut。</summary>
    public ZplPrintCommand Cut()
    {
        if (_cutter != null) throw new NotSupportedException("受控切刀须使用 PrintWithCut，以声明打印模式和文件边界。");
        _profile?.Require(_profile.Cut, "切纸");
        return WriteRawLine("^CN");
    }

    private void RequireUnprintedFormat()
    {
        if (_cutter == null) return;
        if (!_formatOpen) throw new InvalidOperationException("受控打印须先 Start。");
        if (_formatPrinted) throw new InvalidOperationException("一个受控格式只能声明一次打印；请 End 后开始下一格式。");
    }

    /// <summary>构建旧单文件输出；需要独立 ~JK 时明确拒绝，不合并文件。</summary>
    public override IBufferWriter Build()
        => Build(System.Threading.CancellationToken.None);

    /// <summary>可取消构建旧单文件输出；独立控制文件仍须使用 BuildOutput。</summary>
    public override IBufferWriter Build(System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireCompleteFormat();
        if (_outputBoundaries.Any(boundary => boundary.Cut))
            throw new NotSupportedException("Delayed 裁切的 ~JK 须独立发送；请使用 BuildOutput().Segments。");
        return base.Build(cancellationToken);
    }

    /// <summary>可重复构建完整的有序独立文件；失败不修改已记录动作。</summary>
    public PrintPageOutput BuildOutput()
        => BuildOutput(System.Threading.CancellationToken.None);

    /// <summary>可取消构建有序独立文件；取消后可重新构建完整输出。</summary>
    public PrintPageOutput BuildOutput(System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = PrintResourceScope.BeginTemporary();
        RequireCompleteFormat();
        if (_outputBoundaries.Count == 0)
        {
            var page = PrintPageOutput.FromOwned(base.Build(cancellationToken).GetBytes());
            operation?.CommitReservations();
            return page;
        }
        using var segmentStorage = new PrintStorageBudget();
        var segments = new BudgetedList<PrintOutputSegment>("Zpl.Segments", owner: segmentStorage);
        var start = 0;
        foreach (var boundary in _outputBoundaries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            segments.Add(PrintOutputSegment.FromOwned(BuildRecordedActions(start, boundary.End - start, cancellationToken).GetBytes()));
            if (boundary.Cut) segments.Add(PrintOutputSegment.FromOwned(
                BudgetedBuffer.Create(Encoding.GetByteCount(ZplCutEncoder.DelayedControl), "Zpl.DelayedControl",
                    bytes => Encoding.GetBytes(ZplCutEncoder.DelayedControl, 0, ZplCutEncoder.DelayedControl.Length, bytes, 0)),
                PrintOutputSegmentKind.Control));
            start = boundary.End;
        }
        if (start < RecordedActionCount)
            segments.Add(PrintOutputSegment.FromOwned(BuildRecordedActions(start, RecordedActionCount - start, cancellationToken).GetBytes()));
        cancellationToken.ThrowIfCancellationRequested();
        var output = new PrintPageOutput(segments);
        operation?.CommitReservations();
        return output;
    }

    private void RequireCompleteFormat()
    {
        if (_formatOpen)
            throw new InvalidOperationException(_cutter == null
                ? "格式尚未 End，不能输出未完成的页面。"
                : "受控格式尚未 End，不能输出未完成的页面。");
    }

    private static string ToHex(byte[] data)
        => ToHex(data, CancellationToken.None);

    private static string ToHex(byte[] data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var characterBytes = checked((long)data.Length * 4L);
        var characterReservation = PrintResourceScope.Reserve(characterBytes, "Zpl.HexCharacters");
        IDisposable stringReservation = null;
        try
        {
            var chars = new char[checked(data.Length * 2)];
            const string hex = "0123456789ABCDEF";
            for (var i = 0; i < data.Length; i++)
            {
                if ((i & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                chars[i * 2] = hex[data[i] >> 4];
                chars[i * 2 + 1] = hex[data[i] & 15];
            }
            cancellationToken.ThrowIfCancellationRequested();
            stringReservation = PrintResourceScope.Reserve(characterBytes, "Zpl.HexString");
            var result = new string(chars);
            cancellationToken.ThrowIfCancellationRequested();
            stringReservation = null;
            return result;
        }
        catch
        {
            if (stringReservation != null) stringReservation.Dispose();
            throw;
        }
        finally { characterReservation.Dispose(); }
    }

    private ZplPrintCommand WriteRawLineCancellable(string raw, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return WriteRawLine(raw);
    }

    private ZplPrintCommand WriteBarcodeField(string command, int x, int y, BarcodeType type,
        string data, int height, int moduleWidth, int rotation, bool humanReadable)
    {
        if (_validatePageBounds && _hasLabelSize)
        {
            var extent = PrintProtocolProviderBase.EstimateNativeBarcodeExtent(PrintProtocol.Zpl,
                type, data, height, moduleWidth, checked(moduleWidth * 3), humanReadable, rotation,
                _profile?.BarcodeHumanReadableReserveHeightDots,
                _profile?.BarcodeHumanReadableReserveWidthDots);
            var overhang = humanReadable ? _profile?.ResolveBarcodeHumanReadableOverhang(rotation) : null;
            var leftOverhang = overhang?.LeftDots ?? 0;
            var topOverhang = overhang?.TopDots ?? 0;
            var rightOverhang = overhang?.RightDots ?? 0;
            var bottomOverhang = overhang?.BottomDots ?? 0;
            if (leftOverhang > 0 || topOverhang > 0 || rightOverhang > 0 || bottomOverhang > 0)
                ValidatePageEdges((long)x - leftOverhang, (long)y - topOverhang,
                    (long)x + extent.Width + rightOverhang,
                    (long)y + extent.Height + bottomOverhang, "ZPL 原生条码", estimated: true);
            else
                ValidatePageExtent(x, y, extent.Width, extent.Height, 0,
                    "ZPL 原生条码", estimated: true);
        }
        return WriteRawLine(command);
    }

    private void ValidatePageExtent(long x, long y, long width, long height, int rotation,
        string element, bool estimated)
    {
        if (!_hasLabelSize || !_validatePageBounds) return;
        PrintCommandPageBounds.ValidateExtent(_labelWidth, _labelHeight, x, y, width, height,
            rotation, element, estimated, orientation: _restrictedOrientation ?? PrintOrientation.None);
    }

    private void ValidatePageEdges(long left, long top, long right, long bottom,
        string element, bool estimated)
    {
        if (!_hasLabelSize || !_validatePageBounds) return;
        PrintCommandPageBounds.ValidateEdges(_labelWidth, _labelHeight, left, top, right, bottom,
            element, estimated, orientation: _restrictedOrientation ?? PrintOrientation.None);
    }

    private static char Orientation(int rotation)
    {
        switch (rotation)
        {
            case 0: return 'N';
            case 90: return 'R';
            case 180: return 'I';
            case 270: return 'B';
            default: throw new ArgumentOutOfRangeException(nameof(rotation));
        }
    }

    private static char ZplOrientation(PrintOrientation orientation)
    {
        switch (orientation)
        {
            case PrintOrientation.None: return 'N';
            case PrintOrientation.Down: return 'I';
            default: throw new ArgumentOutOfRangeException(nameof(orientation),
                "ZPL ^PO 仅支持 None（N）或 Down（I）；Left/Right 无法表示为整页方向。");
        }
    }

    private static void ValidateCoordinate(int value, string name)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateNonEmptyField(string value, string name, params char[] forbidden)
    {
        ValidateField(value, name, forbidden);
        if (value.Length == 0) throw new ArgumentException("协议字段不能为空。", name);
    }

    private static void ValidateZplFont(string font)
    {
        if (font == null) throw new ArgumentNullException(nameof(font));
        if (font.Length != 1 ||
            !(font[0] >= '0' && font[0] <= '9' || font[0] >= 'A' && font[0] <= 'Z'))
            throw new ArgumentException("ZPL 字体标识必须是单个 ASCII 数字或大写字母。", nameof(font));
    }

    private static void ValidateCode128Data(string data)
    {
        // ZPL ^BC 将 > 作为子集切换等调用码的前缀；结构化数据不接受隐式调用码。
        ValidateNonEmptyField(data, nameof(data), '^', '~', '>');
        for (var index = 0; index < data.Length; index++)
        {
            if (data[index] < 32 || data[index] > 126)
                throw new ArgumentException("ZPL Code 128 条码必须为非空可打印 ASCII。", nameof(data));
        }
    }

    private string NormalizeUccEan128Data(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        if (Gs1ApplicationIdentifierParser.ShouldNormalize(data))
            data = Gs1ApplicationIdentifierParser.NormalizeForProtocol(data, name, 255);

        if (data.Length < 3 || data[0] < '0' || data[0] > '9' || data[1] < '0' || data[1] > '9')
            throw new ArgumentException("GS1-128 需要以数字应用标识开头的元素串。", name);
        Gs1BarcodeCapacity.Validate(BarcodeType.UccEan128, data,
            _profile?.MaxGs1LinearPayloadCharacters, name);

        var normalized = new StringBuilder(data.Length);
        for (var index = 0; index < data.Length; index++)
        {
            var character = data[index];
            if (character == '\u001D')
            {
                normalized.Append(">8");
                continue;
            }
            if (character == '^' || character == '~' || character < 32 || character > 126)
                throw new ArgumentException("ZPL GS1-128 仅接受可打印 ASCII 或 ASCII 29 分隔符。", name);
            // ZPL uses '>' as its invocation prefix; >0 represents a literal
            // greater-than character in Code 128 data.
            if (character == '>') normalized.Append(">0");
            else normalized.Append(character);
        }
        return normalized.ToString();
    }

    private static void ValidateCode39Data(string data)
    {
        ValidateNonEmptyField(data, nameof(data), '^', '~');
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%";
        for (var index = 0; index < data.Length; index++)
        {
            if (alphabet.IndexOf(data[index]) < 0)
                throw new ArgumentException("ZPL Code 39 条码仅接受基础大写 ASCII 字符集。", nameof(data));
        }
    }

    private static void ValidateCode11Data(string data)
    {
        ValidateNonEmptyField(data, nameof(data), '^', '~');
        for (var index = 0; index < data.Length; index++)
        {
            var character = data[index];
            if (!((character >= '0' && character <= '9') || character == '-'))
                throw new ArgumentException("ZPL Code 11 条码仅接受 ASCII 数字和连字符。", nameof(data));
        }
    }

    private static void ValidatePlesseyData(string data)
    {
        ValidateNonEmptyField(data, nameof(data), '^', '~');
        for (var index = 0; index < data.Length; index++)
        {
            if (data[index] < '0' || data[index] > '9')
                throw new ArgumentException("ZPL Plessey 条码仅接受 ASCII 数字。", nameof(data));
        }
    }

    private static void ValidateCode49Data(string data)
    {
        ValidateNonEmptyField(data, nameof(data), '^', '~', '"');
        if (data.Length > 81)
            throw new ArgumentException("ZPL Code 49 条码最多接受 81 个 ASCII 字符。", nameof(data));
        for (var index = 0; index < data.Length; index++)
        {
            var character = data[index];
            if (character < 32 || character > 127)
                throw new ArgumentException("ZPL Code 49 条码必须为 32 到 127 的 ASCII 字符。", nameof(data));
        }
    }

    private string NormalizeGs1DataBarData(string data, int symbology, string name)
    {
        ValidateNonEmptyField(data, name, '^', '~');
        if (symbology != 6)
        {
            ValidateDigits(data, name);
            if (data.Length != 13)
                throw new ArgumentException("ZPL GS1 DataBar Omnidirectional/Truncated/Limited 需要 13 位数字。", name);
            if (symbology == 5 && data[0] != '0' && data[0] != '1')
                throw new ArgumentException("ZPL GS1 DataBar Limited 首位必须为 0 或 1。", name);
            return data;
        }

        if (Gs1ApplicationIdentifierParser.ShouldNormalize(data))
            data = Gs1ApplicationIdentifierParser.NormalizeForProtocol(data, name, 255);

        if (data.Length < 2 || data.Length > 255)
            throw new ArgumentException("ZPL GS1 DataBar Expanded 需要 2–255 个字符。", name);
        if (data[0] == '(')
        {
            if (data.Length < 3 || data[1] < '0' || data[1] > '9' ||
                data[2] < '0' || data[2] > '9')
                throw new ArgumentException("ZPL GS1 DataBar Expanded 以括号开头时，第二和第三个字符必须为数字。", name);
        }
        else if (data[0] < '0' || data[0] > '9' || data[1] < '0' || data[1] > '9')
            throw new ArgumentException("ZPL GS1 DataBar Expanded 数据必须以两个数字或括号和两个数字开头。", name);

        const string alphabet = "0123456789ABCDabcd !\"%$'()*+,-./:;<=>?_{|}";
        for (var index = 0; index < data.Length; index++)
        {
            var character = data[index];
            if (character != '\u001D' && (character < 32 || character > 126 || alphabet.IndexOf(character) < 0))
                throw new ArgumentException("ZPL GS1 DataBar Expanded 包含不支持的字符。", name);
        }
        Gs1BarcodeCapacity.Validate(BarcodeType.Gs1DataBarExpanded, data,
            _profile?.MaxGs1DataBarExpandedPayloadCharacters, name);
        return data;
    }

    private static void ValidateCode93Data(string data)
    {
        ValidateNonEmptyField(data, nameof(data), '^', '~');
        for (var index = 0; index < data.Length; index++)
        {
            if (data[index] < 32 || data[index] > 126)
                throw new ArgumentException("ZPL Code 93 条码必须为非空可打印 ASCII；控制字符和扩展 ASCII 需先显式转义。", nameof(data));
        }
    }

    private static string NormalizeCodabarData(string data, string name)
    {
        ValidateNonEmptyField(data, name, '^', '~');
        if (data.Length < 3 || "ABCD".IndexOf(data[0]) < 0 ||
            "ABCD".IndexOf(data[data.Length - 1]) < 0)
            throw new ArgumentException("Codabar 必须包含 A-D 起止符和至少一个数据字符。", name);

        const string alphabet = "0123456789-$:/.+";
        for (var index = 1; index < data.Length - 1; index++)
            if (alphabet.IndexOf(data[index]) < 0)
                throw new ArgumentException("Codabar 正文仅接受数字及 -$:/.+ 字符。", name);
        return data;
    }

    private static string NormalizeI2OF5Data(string data, string name)
    {
        ValidateDigits(data, name);
        if (data.Length < 2 || (data.Length & 1) != 0)
            throw new ArgumentException("I2OF5 需要至少 2 位且为偶数位数字。", name);
        return data;
    }

    private static string NormalizeI2OF5CheckDigitData(string data, string name)
    {
        ValidateDigits(data, name);
        return (data.Length & 1) == 0 ? "0" + data : data;
    }

    private static string NormalizeI2OF5GermanData(string data, string name)
    {
        ValidateDigits(data, name);
        if (data.Length < 11 || data.Length > 14)
            throw new ArgumentException("I2OF5G 需要 11、12、13 或 14 位数字。", name);

        if ((data.Length & 1) == 1)
            return data + CalculateI2OF5CheckDigit(data, 4, 9);

        var expected = CalculateI2OF5CheckDigit(data.Substring(0, data.Length - 1), 4, 9);
        if (data[data.Length - 1] != expected)
            throw new ArgumentException("I2OF5G 校验位错误。", name);
        return data;
    }

    private static char CalculateI2OF5CheckDigit(string data, int firstWeight, int secondWeight)
    {
        var sum = 0;
        for (var index = 0; index < data.Length; index++)
            sum += (data[index] - '0') * (index % 2 == 0 ? firstWeight : secondWeight);
        return (char)('0' + (10 - sum % 10) % 10);
    }

    private static string NormalizePostnetData(string data, string name)
    {
        ValidateNonEmptyField(data, name, '^', '~');
        var normalized = data.Replace("-", string.Empty);
        ValidateDigits(normalized, name);
        if (normalized.Length != 5 && normalized.Length != 9 && normalized.Length != 11)
            throw new ArgumentException("POSTNET 需要 5、9 或 11 位邮政数字。", name);
        return normalized;
    }

    private static string NormalizePlanetData(string data, string name)
    {
        ValidateNonEmptyField(data, name, '^', '~');
        var normalized = data.Replace("-", string.Empty);
        ValidateDigits(normalized, name);
        if (normalized.Length != 12 && normalized.Length != 14)
            throw new ArgumentException("PLANET 需要 12 或 14 位邮政数字。", name);

        var sum = 0;
        foreach (var character in normalized) sum += character - '0';
        if (sum % 10 != 0)
            throw new ArgumentException("PLANET 校验位错误。", name);
        return normalized;
    }

    private static string NormalizeIntelligentMailData(string data, string name)
    {
        ValidateNonEmptyField(data, name, '^', '~');
        var normalized = data.Replace("-", string.Empty);
        ValidateDigits(normalized, name);
        if (normalized.Length != 20 && normalized.Length != 25 &&
            normalized.Length != 29 && normalized.Length != 31)
            throw new ArgumentException("Intelligent Mail 条码需要 20、25、29 或 31 位邮政数字。", name);
        return normalized;
    }

    private static void ValidateMsiData(string data, char checkDigit, string name)
    {
        ValidateDigits(data, name);
        var scheme = char.ToUpperInvariant(checkDigit);
        if (scheme < 'A' || scheme > 'D')
            throw new ArgumentOutOfRangeException(nameof(checkDigit), "ZPL MSI 校验方案必须为 A、B、C 或 D。");
        var maxLength = scheme == 'A' ? 13 : 14;
        if (data.Length > maxLength)
            throw new ArgumentException($"ZPL MSI {scheme} 校验方案最多接受 {maxLength} 位数字。", name);
    }

    private static string NormalizeRetailPayload(string data, int payloadLength, int fullLength, string name)
    {
        ValidateDigits(data, name);
        if (data.Length != payloadLength && data.Length != fullLength)
            throw new ArgumentException($"条码需要 {payloadLength} 位数据或带校验位的 {fullLength} 位数字。", name);
        if (data.Length == fullLength)
        {
            var check = RetailCheckDigit(data, payloadLength);
            if (data[payloadLength] != check) throw new ArgumentException("条码校验位错误。", name);
            return data.Substring(0, payloadLength);
        }
        return data;
    }

    private static string NormalizeUpcEData(string data, string name)
    {
        ValidateDigits(data, name);
        if (data.Length == 6) data = "0" + data;
        if (data.Length != 7 || (data[0] != '0' && data[0] != '1'))
            throw new ArgumentException("UPC-E 使用 6 位数据或 7 位含号码系统的数据（号码系统为 0/1）。", name);
        if (data[0] != '0')
            throw new ArgumentException("ZPL ^B9 只定义号码系统 0；号码系统 1 不能安全映射为 10 位 ZPL 输入。", name);

        // ZPL ^B9 接受五位厂商码和五位商品码，打印机再计算 UPC-E 校验位。
        var numberSystem = data[0];
        var first = data[1];
        var second = data[2];
        var third = data[3];
        var fourth = data[4];
        var fifth = data[5];
        var sixth = data[6];
        string expanded;
        switch (sixth)
        {
            case '0':
            case '1':
            case '2':
                expanded = string.Concat(numberSystem, first, second, sixth, "0000", third, fourth, fifth);
                break;
            case '3':
                expanded = string.Concat(numberSystem, first, second, third, "00000", fourth, fifth);
                break;
            case '4':
                expanded = string.Concat(numberSystem, first, second, third, fourth, "00000", fifth);
                break;
            default:
                expanded = string.Concat(numberSystem, first, second, third, fourth, fifth, "0000", sixth);
                break;
        }
        return expanded.Substring(1);
    }

    private static char RetailCheckDigit(string data, int payloadLength)
    {
        var sum = 0;
        for (var index = payloadLength - 1; index >= 0; index--)
            sum += (data[index] - '0') * ((payloadLength - 1 - index) % 2 == 0 ? 3 : 1);
        return (char)('0' + (10 - sum % 10) % 10);
    }

    private static void ValidateDigits(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        if (data.Length == 0) throw new ArgumentException("条码必须为非空 ASCII 数字。", name);
        for (var index = 0; index < data.Length; index++)
            if (data[index] < '0' || data[index] > '9')
                throw new ArgumentException("该制式仅接受 ASCII 数字。", name);
    }

    private static int CheckedRasterDimension(long value, string name)
    {
        if (value <= 0 || value > int.MaxValue - 7) throw new ArgumentOutOfRangeException(name, "光栅尺寸超出可表示范围。");
        return (int)value;
    }

    private static int CheckedRasterByteCount(int widthBytes, int height)
    {
        var count = (long)widthBytes * height;
        if (count > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(height), "图片尺寸超出可表示范围。");
        return (int)count;
    }

    private static void ValidateRasterBufferSize(int width, int height)
    {
        var bytes = ((long)width + 7) / 8 * height;
        if (bytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(height), "光栅缓冲区超出可表示范围。");
    }
}
