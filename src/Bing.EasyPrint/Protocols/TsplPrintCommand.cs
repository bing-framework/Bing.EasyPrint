using System;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

/// <summary>生成 TSPL 标签打印指令。</summary>
public sealed partial class TsplPrintCommand : ProtocolPrintCommandBase<TsplPrintCommand>
{
    /// <summary>获取 TSPL 的基础能力描述。</summary>
    public override PrintProtocol Protocol => PrintProtocol.Tspl;
    public override PrintCapabilities Capabilities => _capabilities;

    private PrintCapabilities _capabilities;
    private TsplMediaOptions _pendingMediaOptions;
    private bool _sizeWritten;
    private bool _allowRasterizedLines;
    private bool _allowRasterizedTextStyles;
    private LabelDeviceContract _profile;
    private TsplCutterProfile _cutter;
    private int _pageWidth;
    private int _pageHeight;
    private string _pageUnit;
    private bool _hasPageSize;
    private int _direction;
    private int _referenceX;
    private int _referenceY;
    private bool _validatePageBounds = true;

    public TsplPrintCommand() : this((Encoding)null, null) { }
    public TsplPrintCommand(Encoding encoding = null) : this(encoding, null) { }

    internal TsplPrintCommand DisablePageBoundsValidation()
    {
        _validatePageBounds = false;
        return this;
    }

    /// <summary>
    /// 使用指定编码和介质设置创建 TSPL 命令构建器。
    /// </summary>
    /// <param name="encoding">写入文本时使用的编码；为空时使用 GBK。</param>
    /// <param name="mediaOptions">介质和设备设置；为空时不追加额外设置。</param>
    public TsplPrintCommand(Encoding encoding, TsplMediaOptions mediaOptions)
        : base(encoding ?? Encoding.GetEncoding("gbk"))
    {
        if (mediaOptions != null)
        {
            mediaOptions.Validate();
            _pendingMediaOptions = mediaOptions.Clone();
            _allowRasterizedLines = mediaOptions.AllowRasterizedLines;
            _allowRasterizedTextStyles = mediaOptions.AllowRasterizedTextStyles;
        }

        _profile = mediaOptions?.Profile?.Snapshot();
        _cutter = mediaOptions?.Profile?.Cutter?.Snapshot();
        _profile?.ValidateEncoding(Encoding);
        _capabilities = CreateCapabilities(mediaOptions == null ? TsplMediaOptions.DefaultDpi : mediaOptions.Dpi,
            _profile, _cutter, _allowRasterizedTextStyles);
    }

    /// <summary>
    /// 将 TSPL 介质和设备设置应用到命令流。
    /// </summary>
    /// <param name="mediaOptions">待应用的设置；不能为 <c>null</c>。</param>
    /// <returns>当前命令构建器。</returns>
    /// <remarks>
    /// 设置会在调用前一次性完成校验。尚未写入标签尺寸时，设置会延后到
    /// <see cref="Size(int, int, string)"/> 之后，以保持 TSPL 页面设置顺序。
    /// </remarks>
    public TsplPrintCommand ApplyMediaOptions(TsplMediaOptions mediaOptions)
    {
        if (mediaOptions == null) throw new ArgumentNullException(nameof(mediaOptions));
        mediaOptions.Validate();
        var profile = mediaOptions.Profile?.Snapshot();
        profile?.ValidateEncoding(Encoding);
        // 已记录的动作不能在更换机型后逃过新限制；仅允许在追加输出前选择机型。
        if ((_sizeWritten || _hasStructuredOutput) && (profile != null || _profile != null))
            throw new InvalidOperationException("记录 TSPL 输出后不能更换机型配置；请创建新的命令实例。");
        var copy = mediaOptions.Clone();
        if (_sizeWritten)
        {
            var commands = MediaCommands(copy);
            if (commands.Length != 0) WriteRaw(commands);
            ApplyCoordinateOptions(copy);
        }
        _profile = profile;
        _cutter = mediaOptions.Profile?.Cutter?.Snapshot();
        _capabilities = CreateCapabilities(mediaOptions.Dpi, _profile, _cutter, mediaOptions.AllowRasterizedTextStyles);
        _allowRasterizedLines = mediaOptions.AllowRasterizedLines;
        _allowRasterizedTextStyles = mediaOptions.AllowRasterizedTextStyles;
        _pendingMediaOptions = _sizeWritten ? null : copy;
        return this;
    }

    public TsplPrintCommand Size(int width, int height, string unit = "mm")
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
        if (unit != "mm" && unit != "dot") throw new ArgumentException("TSPL SIZE 单位必须为 mm 或 dot。", nameof(unit));
        _profile?.ValidatePage(unit == "dot" ? width : width * (_capabilities.Dpi / 25.4),
            unit == "dot" ? height : height * (_capabilities.Dpi / 25.4));
        var codePage = Encoding.CodePage == Encoding.UTF8.CodePage ? "UTF-8"
            : Encoding.CodePage == 936 ? "936"
            : Encoding.CodePage == Encoding.ASCII.CodePage ? "USA"
            : throw new NotSupportedException($"TSPL 尚未映射编码 {Encoding.WebName} 的设备代码页。");
        var pendingMediaOptions = _pendingMediaOptions;
        WriteRaw($"SIZE {width} {unit},{height} {unit}\r\nCODEPAGE {codePage}\r\nCLS\r\n" + MediaCommands(pendingMediaOptions));
        _pageWidth = width;
        _pageHeight = height;
        _pageUnit = unit;
        _hasPageSize = true;
        ApplyCoordinateOptions(pendingMediaOptions);
        _sizeWritten = true;
        _pendingMediaOptions = null;
        return this;
    }
    /// <summary>清空打印机的标签图像缓冲区。</summary>
    public TsplPrintCommand ClearImageBuffer() => WriteTsplLine("CLS");
    public TsplPrintCommand Gap(int distance, int offset = 0)
    {
        if (distance < 0 || offset < 0) throw new ArgumentOutOfRangeException();
        return WriteTsplLine($"GAP {distance},{offset}");
    }
    public TsplPrintCommand Direction(int direction = 0)
    {
        if (direction < 0 || direction > 1) throw new ArgumentOutOfRangeException(nameof(direction));
        var result = WriteTsplLine($"DIRECTION {direction}");
        _direction = direction;
        return result;
    }
    public TsplPrintCommand Reference(int x, int y)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        var result = WriteTsplLine($"REFERENCE {x},{y}");
        _referenceX = x;
        _referenceY = y;
        return result;
    }
    public TsplPrintCommand Speed(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        return WriteTsplLine($"SPEED {value}");
    }
    public TsplPrintCommand Density(int value)
    {
        if (value < 0 || value > 15) throw new ArgumentOutOfRangeException(nameof(value));
        return WriteTsplLine($"DENSITY {value}");
    }
    public TsplPrintCommand Text(int x, int y, string font, int rotation, int xScale, int yScale, string data)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        if (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270) throw new ArgumentOutOfRangeException(nameof(rotation));
        var maxSize = font == "0" ? 256 : 10;
        if (xScale < 1 || xScale > maxSize) throw new ArgumentOutOfRangeException(nameof(xScale));
        if (yScale < 1 || yScale > maxSize) throw new ArgumentOutOfRangeException(nameof(yScale));
        ValidateNonEmptyField(font, nameof(font), '"', '\\');
        ValidateField(data, nameof(data), '"');
        _profile?.ValidateText(font, data);
        if (rotation != 0) _profile?.Require(_profile.Rotation, "文字旋转");
        var textExtent = TsplTextExtent(font, xScale, yScale, data);
        ValidatePageExtent(x, y, textExtent.Width, textExtent.Height, rotation,
            "TSPL 原生文字", estimated: true);
        return WriteTsplLine($"TEXT {x},{y},\"{font}\",{rotation},{xScale},{yScale},\"{data}\"");
    }
    public TsplPrintCommand Text(int x, int y, string data) => Text(x, y, "0", 0, 1, 1, data);

    /// <summary>
    /// 将带粗体或下划线的文字以主机位图写入 TSPL。
    /// </summary>
    /// <remarks>
    /// 只有显式设置 <see cref="TsplMediaOptions.AllowRasterizedTextStyles" /> 后才允许使用；
    /// 输出使用 BITMAP，文字不再依赖打印机内置字体。
    /// </remarks>
    public TsplPrintCommand TextRasterized(int x, int y, string data, int fontSize,
        bool bold = false, bool underline = false, int rotation = 0, string fontFamily = null,
        CancellationToken cancellationToken = default(CancellationToken))
        => TextRasterized(x, y, data, fontSize, bold, underline, false, rotation, fontFamily,
            cancellationToken);

    /// <summary>将文字样式光栅化到 TSPL；反白文字使用黑色布局背景和白色字形。</summary>
    public TsplPrintCommand TextRasterized(int x, int y, string data, int fontSize,
        bool bold, bool underline, bool reverse, int rotation = 0, string fontFamily = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        cancellationToken.ThrowIfCancellationRequested();
        if ((bold || underline || reverse) && !_allowRasterizedTextStyles)
            throw new NotSupportedException("TSPL 粗体、下划线或反白需要显式设置 AllowRasterizedTextStyles。");
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        ValidateField(data, nameof(data), '"');
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270)
            throw new ArgumentOutOfRangeException(nameof(rotation));
        _profile?.Require(_profile.Images, "文字样式光栅化");
        if (rotation != 0) _profile?.Require(_profile.Rotation, "文字旋转");
        var raster = EscPosRasterCanvas.RasterizeText(data, fontSize, bold, underline, fontFamily, rotation,
            reverse, cancellationToken: cancellationToken);
        return Bitmap(x, y, raster.RowBytes, raster.Height, 0, raster.Data, cancellationToken);
    }
    public TsplPrintCommand Line(int x0, int y0, int x1, int y1, int width = 1)
        => Line(x0, y0, x1, y1, width, CancellationToken.None);

    /// <summary>绘制线段；斜线光栅化过程支持取消。</summary>
    public TsplPrintCommand Line(int x0, int y0, int x1, int y1, int width,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _profile?.Require(_profile.Graphics, "线框绘图");
        ValidateCoordinate(x0, nameof(x0));
        ValidateCoordinate(y0, nameof(y0));
        ValidateCoordinate(x1, nameof(x1));
        ValidateCoordinate(y1, nameof(y1));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (x0 == x1)
        {
            ValidatePageExtent(x0, Math.Min(y0, y1), width,
                (long)Math.Max(y0, y1) - Math.Min(y0, y1) + 1, 0, "TSPL 竖线", estimated: false);
            return WriteTsplLine($"BAR {x0},{Math.Min(y0, y1)},{width},{CheckedRasterDimension((long)Math.Max(y0, y1) - Math.Min(y0, y1) + 1, nameof(width))}", cancellationToken);
        }
        if (y0 == y1)
        {
            ValidatePageExtent(Math.Min(x0, x1), y0,
                (long)Math.Max(x0, x1) - Math.Min(x0, x1) + 1, width, 0, "TSPL 横线", estimated: false);
            return WriteTsplLine($"BAR {Math.Min(x0, x1)},{y0},{CheckedRasterDimension((long)Math.Max(x0, x1) - Math.Min(x0, x1) + 1, nameof(width))},{width}", cancellationToken);
        }

        if (!_allowRasterizedLines)
            throw new NotSupportedException("TSPL 斜线需要显式设置 TsplMediaOptions.AllowRasterizedLines。");

        var radius = width / 2;
        var left = Math.Max(0, Math.Min(x0, x1) - radius);
        var top = Math.Max(0, Math.Min(y0, y1) - radius);
        var bitmapWidth = CheckedRasterDimension((long)Math.Max(x0, x1) + radius - left + 1, nameof(width));
        var bitmapHeight = CheckedRasterDimension((long)Math.Max(y0, y1) + radius - top + 1, nameof(width));
        ValidatePageEdges((long)Math.Min(x0, x1) - radius, (long)Math.Min(y0, y1) - radius,
            (long)Math.Max(x0, x1) + radius + 1, (long)Math.Max(y0, y1) + radius + 1,
            "TSPL 斜线", estimated: false);
        ValidateRasterBufferSize(bitmapWidth, bitmapHeight);
        _profile?.ValidateImage(left, top, ((long)bitmapWidth + 7) / 8, bitmapHeight);
        var canvas = new EscPosRasterCanvas(bitmapWidth, bitmapHeight, null, cancellationToken);
        canvas.DrawLine(x0 - left, y0 - top, x1 - left, y1 - top, width);
        cancellationToken.ThrowIfCancellationRequested();
        return Bitmap(left, top, canvas.RowBytes, bitmapHeight, 1,
            canvas.ToBytes(cancellationToken), cancellationToken);
    }
    public TsplPrintCommand Box(int x0, int y0, int x1, int y1, int width = 1)
    {
        _profile?.Require(_profile.Graphics, "线框绘图");
        ValidateRectangle(x0, y0, x1, y1, width);
        ValidatePageEdges(x0, y0, (long)x1 + 1, (long)y1 + 1, "TSPL 线框", estimated: false);
        return WriteTsplLine($"BOX {x0},{y0},{x1},{y1},{width}");
    }

    /// <summary>
    /// 绘制填充矩形。
    /// </summary>
    /// <param name="x">矩形左边界坐标，单位为 dots。</param>
    /// <param name="y">矩形上边界坐标，单位为 dots。</param>
    /// <param name="width">矩形宽度，单位为 dots；必须为正数。</param>
    /// <param name="height">矩形高度，单位为 dots；必须为正数。</param>
    /// <returns>当前命令构建器。</returns>
    /// <remarks>
    /// 宽度和高度表示半开区域 [x, x + width) × [y, y + height)，与
    /// <see cref="Box(int, int, int, int, int)"/> 使用的右下端点坐标不同。
    /// </remarks>
    public TsplPrintCommand FillRectangle(int x, int y, int width, int height)
        => FillRectangle(x, y, width, height, CancellationToken.None);

    /// <summary>
    /// 绘制填充矩形，并支持在记录前取消操作。
    /// </summary>
    /// <param name="x">矩形左边界坐标，单位为 dots。</param>
    /// <param name="y">矩形上边界坐标，单位为 dots。</param>
    /// <param name="width">矩形宽度，单位为 dots；必须为正数。</param>
    /// <param name="height">矩形高度，单位为 dots；必须为正数。</param>
    /// <param name="cancellationToken">用于取消记录的令牌。</param>
    /// <returns>当前命令构建器。</returns>
    /// <remarks>
    /// 宽度和高度表示半开区域 [x, x + width) × [y, y + height)，与
    /// <see cref="Box(int, int, int, int, int)"/> 使用的右下端点坐标不同；所有尺寸均以 dots 表示。
    /// </remarks>
    public TsplPrintCommand FillRectangle(int x, int y, int width, int height,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _profile?.Require(_profile.Graphics, "填充矩形");
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        var right = (long)x + width;
        var bottom = (long)y + height;
        if (right > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(width), "矩形右边界超出可表示范围。");
        if (bottom > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(height), "矩形下边界超出可表示范围。");
        _profile?.ValidatePage(right, bottom);
        ValidatePageEdges(x, y, right, bottom, "TSPL 填充矩形", estimated: false);
        return WriteTsplLine($"BAR {x},{y},{width},{height}", cancellationToken);
    }

    public TsplPrintCommand Barcode(int x, int y, string type, int height, int humanReadable, int rotation, int narrow, int wide, string data)
    {
        _profile?.Require(_profile.Barcode, "原生条码");
        if (rotation != 0) _profile?.Require(_profile.Rotation, "条码旋转");
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        if (height <= 0 || (humanReadable != 0 && humanReadable != 1) || (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270) || narrow <= 0 || wide <= 0)
            throw new ArgumentOutOfRangeException();
        if (humanReadable != 0)
        {
            var barcodeType = BarcodeTypeFromTspl(type);
            if (barcodeType.HasValue)
                _profile?.Require(_profile.SupportsBarcodeHumanReadable(barcodeType.Value), "条码可读文字");
            else
                _profile?.Require(_profile.BarcodeHumanReadable, "条码可读文字");
        }
        ValidateNonEmptyField(type, nameof(type), '"', '\\');
        ValidateNonEmptyField(data, nameof(data), '"');
        ValidateTsplBarcodeRatio(type, narrow, wide);
        data = NormalizeSupportedBarcodeData(type, data);
        if (_validatePageBounds && _hasPageSize)
        {
            var barcodeType = BarcodeTypeFromTspl(type);
            if (!barcodeType.HasValue)
                throw new NotSupportedException($"TSPL 原生条码类型 {type} 没有可用的页面宽度估算；请使用受支持的条码类型或原始命令接口。");
            var measureData = barcodeType.Value == BarcodeType.UccEan128
                ? data.Replace("!102", "\u001D")
                : data;
            var extent = PrintProtocolProviderBase.EstimateNativeBarcodeExtent(PrintProtocol.Tspl,
                barcodeType.Value, measureData, height, narrow, wide, humanReadable != 0, rotation,
                _profile?.BarcodeHumanReadableReserveHeightDots,
                _profile?.BarcodeHumanReadableReserveWidthDots);
            var nativeHriEnabled = humanReadable != 0;
            var overhang = nativeHriEnabled ? _profile?.ResolveBarcodeHumanReadableOverhang(rotation) : null;
            var leftOverhang = overhang?.LeftDots ?? 0;
            var topOverhang = overhang?.TopDots ?? 0;
            var rightOverhang = overhang?.RightDots ?? 0;
            var bottomOverhang = overhang?.BottomDots ?? 0;
            if (leftOverhang > 0 || topOverhang > 0 || rightOverhang > 0 || bottomOverhang > 0)
                ValidatePageEdges((long)x - leftOverhang, (long)y - topOverhang,
                    (long)x + extent.Width + rightOverhang,
                    (long)y + extent.Height + bottomOverhang, "TSPL 原生条码", estimated: true);
            else
                ValidatePageExtent(x, y, extent.Width, extent.Height, 0,
                    "TSPL 原生条码", estimated: true);
        }
        return WriteTsplLine($"BARCODE {x},{y},\"{type}\",{height},{humanReadable},{rotation},{narrow},{wide},\"{data}\"");
    }

    /// <summary>以 TSPL 原生 DPI/DPL 输出 German Post 4/9 校验语义的 I2OF5G。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">11/13 位 payload，或带 4/9 校验位的 12/14 位完整数据。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度，单位为打印点。</param>
    /// <remarks>
    /// TSPL 的 DPI/DPL 分别要求 11/13 位内容，并由设备完成邮政条码的最终编码。
    /// 主机仍验证统一模型的 4/9 校验语义，然后只发送固定长度 payload，避免将完整值误当作 TSPL 内容。
    /// </remarks>
    public TsplPrintCommand BarcodeI2OF5G(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 2)
    {
        var normalized = NormalizeI2OF5GermanData(data, nameof(data));
        var type = normalized.Length == 12 ? "DPI" : "DPL";
        return Barcode(x, y, type, height, humanReadable, rotation, narrow, wide,
            normalized.Substring(0, normalized.Length - 1));
    }

    /// <summary>以 TSPL 原生 POST 输出 POSTNET 邮政条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">5、9 或 11 位邮政数字；连字符会在写入 TSPL 前移除。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度，TSPL POST 类型保留该参数以保持统一命令签名。</param>
    /// <remarks>
    /// TSC 将 POST 定义为 5、9 或 11 位输入的 POSTNET 类型，并由设备编码最终校验位；
    /// 主机只负责统一模型的长度、数字和协议字段校验。
    /// </remarks>
    public TsplPrintCommand BarcodePostnet(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 2)
    {
        var normalized = NormalizePostnetData(data, nameof(data));
        return Barcode(x, y, "POST", height, humanReadable, rotation, narrow, wide, normalized);
    }

    /// <summary>以 TSPL 原生 PLANET 输出完整校验位的邮政条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">12 或 14 位 PLANET 数字；连字符会在写入 TSPL 前移除，末位必须为有效校验位。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度；PLANET 设备通常按 1:1 邮政码比例处理，保留统一命令签名。</param>
    public TsplPrintCommand BarcodePlanet(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 2)
    {
        var normalized = NormalizePlanetData(data, nameof(data));
        return Barcode(x, y, "PLANET", height, humanReadable, rotation, narrow, wide, normalized);
    }

    /// <summary>以 TSPL 原生 PLESSEY 输出数字 Plessey 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">非空 ASCII 数字；Plessey 的校验与条形编码由设备完成。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度；TSPL PLESSEY 固定要求窄条:宽条为 1:2。</param>
    public TsplPrintCommand BarcodePlessey(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 4)
        => Barcode(x, y, "PLESSEY", height, humanReadable, rotation, narrow, wide, data);

    /// <summary>以 TSPL 原生 11 输出 Code 11 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">非空数字或连字符；Code 11 的校验位由设备生成。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度，TSPL Code 11 支持的宽窄比例。</param>
    /// <remarks>
    /// Code 11 支持数字和连字符。TSPL 固件会根据数据长度生成一个或两个 Modulo-11
    /// 校验位；主机不把设备生成的校验位拼入可读数据，避免重复编码。
    /// </remarks>
    public TsplPrintCommand BarcodeCode11(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 4)
    {
        var normalized = NormalizeCode11Data(data, nameof(data));
        return Barcode(x, y, "11", height, humanReadable, rotation, narrow, wide, normalized);
    }

    /// <summary>以 TSPL 原生 CPOST 输出 China Post 邮政条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">非空 ASCII 数字；China Post 的校验和条形编码由设备完成。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度；TSPL CPOST 固定要求窄条:宽条为 1:1。</param>
    /// <remarks>
    /// CPOST 的数据长度由目标固件决定，主机不擅自截断或追加校验位；统一模型只校验
    /// 非空 ASCII 数字、固定 1:1 模块宽度和页面边界。
    /// </remarks>
    public TsplPrintCommand BarcodeChinaPost(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 2)
    {
        var normalized = NormalizeChinaPostData(data, nameof(data));
        return Barcode(x, y, "CPOST", height, humanReadable, rotation, narrow, wide, normalized);
    }

    /// <summary>以 TSPL 原生 TELEPEN 输出 ASCII Telepen 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">非空 ASCII 数据，最多 30 个字符；协议字段只接受可安全嵌入 TSPL 引号的字符。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度，必须使用 TSPL 支持的宽窄比例。</param>
    /// <remarks>
    /// TSC 手册将 TELEPEN 定义为 ASCII 0–127、最多 30 个字符。TSPL 的
    /// <c>BARCODE</c> 行本身使用引号和换行分隔；NUL、换行、转义字符和引号
    /// 无法安全保留为原始字段，因此命令层会在写入前明确拒绝这些 framing 冲突字符。
    /// </remarks>
    public TsplPrintCommand BarcodeTelepen(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 4)
    {
        var normalized = NormalizeTelepenData(data, nameof(data));
        return Barcode(x, y, "TELEPEN", height, humanReadable, rotation, narrow, wide, normalized);
    }

    /// <summary>以 TSPL 原生 TELEPENN 输出纯数字 Telepen 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">非空 ASCII 数字，最多 60 位。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度，必须使用 TSPL 支持的宽窄比例。</param>
    public TsplPrintCommand BarcodeTelepenn(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 4)
    {
        var normalized = NormalizeTelepennData(data, nameof(data));
        return Barcode(x, y, "TELEPENN", height, humanReadable, rotation, narrow, wide, normalized);
    }

    /// <summary>以 TSPL 原生 CODE49 输出 ASCII Code 49 条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">非空 ASCII 数据，最多 81 个字符；协议字段只接受可安全嵌入 TSPL 引号的字符。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度，必须使用 TSPL 支持的宽窄比例。</param>
    /// <remarks>
    /// TSC 手册将 CODE49 定义为 ASCII 0–127、最多 81 个字符。TSPL 的
    /// <c>BARCODE</c> 行使用引号和换行分隔；NUL、ESC、CR/LF 和引号会在字段
    /// framing 层明确拒绝。Code 49 的实际堆叠行数和设备宽度由固件决定，Provider
    /// 只使用保守的模块上界进行页面预检。
    /// </remarks>
    public TsplPrintCommand BarcodeCode49(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 2)
    {
        var normalized = NormalizeCode49Data(data, nameof(data));
        return Barcode(x, y, "CODE49", height, humanReadable, rotation, narrow, wide, normalized);
    }

    /// <summary>以 TSPL 原生 ITF14 输出 13 位数字 payload。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">恰好 13 位 ASCII 数字；设备负责追加 ITF-14 最终校验位。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度，TSPL ITF14 支持手册声明的宽窄比例。</param>
    public TsplPrintCommand BarcodeItf14(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 4)
    {
        var normalized = NormalizeItf14Data(data, nameof(data));
        return Barcode(x, y, "ITF14", height, humanReadable, rotation, narrow, wide, normalized);
    }

    /// <summary>以 TSPL 原生 EAN14 输出 13 位数字 payload。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">恰好 13 位 ASCII 数字；设备负责追加 EAN-14 最终校验位。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度；TSPL EAN14 固定要求窄条:宽条为 1:1。</param>
    public TsplPrintCommand BarcodeEan14(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 8, int wide = 8)
    {
        var normalized = NormalizeEan14Data(data, nameof(data));
        return Barcode(x, y, "EAN14", height, humanReadable, rotation, narrow, wide, normalized);
    }

    /// <summary>以 TSPL 原生 EAN128 输出 GS1-128（UCC/EAN-128）条码。</summary>
    /// <param name="x">条码左上角 X 坐标。</param>
    /// <param name="y">条码左上角 Y 坐标。</param>
    /// <param name="data">以数字应用标识开头的 GS1 元素串；ASCII 29 表示后续 FNC1。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="humanReadable">是否输出可读文字，1 表示输出。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="narrow">窄条宽度，单位为打印点。</param>
    /// <param name="wide">宽条宽度，单位为打印点。</param>
    public TsplPrintCommand BarcodeUccEan128(int x, int y, string data, int height = 80,
        int humanReadable = 1, int rotation = 0, int narrow = 2, int wide = 2)
        => Barcode(x, y, "EAN128", height, humanReadable, rotation, narrow, wide, data);

    public TsplPrintCommand Barcode(int x, int y, string data) => Barcode(x, y, "128", 80, 1, 0, 2, 2, data);
    public TsplPrintCommand QrCode(int x, int y, int ecc, int cellWidth, string mode, string data)
        => QrCode(x, y, ecc, cellWidth, mode, 0, data);

    /// <summary>以 TSPL 原生 QRCODE 指令输出二维码；旋转角度按顺时针度数表示。</summary>
    public TsplPrintCommand QrCode(int x, int y, int ecc, int cellWidth, string mode, int rotation, string data)
    {
        _profile?.Require(_profile.QrCode, "原生二维码");
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        if (ecc < 0 || ecc > 3) throw new ArgumentOutOfRangeException(nameof(ecc));
        if (cellWidth < 1 || cellWidth > 10) throw new ArgumentOutOfRangeException(nameof(cellWidth));
        if (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270)
            throw new ArgumentOutOfRangeException(nameof(rotation));
        _profile?.Require(_profile.SupportsQrCodeRotation((RotationAngle)rotation), "二维码旋转");
        ValidateField(mode, nameof(mode), ',', '"', '\\');
        if (!string.Equals(mode, "A", StringComparison.OrdinalIgnoreCase) && !string.Equals(mode, "M", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("TSPL 二维码模式必须为 A 或 M。", nameof(mode));
        ValidateNonEmptyField(data, nameof(data), '"');
        var levels = new[] { "L", "M", "Q", "H" };
        var correction = (QrCodeCorrectionLevel)ecc;
        var measuredSize = EscPosRasterCanvas.MeasureQrCodeSize(data, cellWidth, correction);
        ValidatePageExtent(x, y, measuredSize, measuredSize, rotation,
            "TSPL 二维码", estimated: true);
        return WriteTsplLine($"QRCODE {x},{y},{levels[ecc]},{cellWidth},{mode},{rotation},\"{data}\"");
    }
    public TsplPrintCommand QrCode(int x, int y, string data) => QrCode(x, y, 0, 4, "A", data);

    /// <summary>将十六进制位图输入解码后写入 TSPL BITMAP 二进制数据。</summary>
    public TsplPrintCommand Bitmap(int x, int y, int widthBytes, int height, int mode, string asciiHex)
        => Bitmap(x, y, widthBytes, height, mode, asciiHex, CancellationToken.None);

    /// <summary>将十六进制位图输入解码后写入 TSPL BITMAP 二进制数据；解码支持取消。</summary>
    public TsplPrintCommand Bitmap(int x, int y, int widthBytes, int height, int mode, string asciiHex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (asciiHex == null) throw new ArgumentNullException(nameof(asciiHex));
        ValidateBitmapDimensions(x, y, widthBytes, height, mode);
        var expectedBytes = CheckedBitmapByteCount(widthBytes, height);
        _profile?.ValidateImage(x, y, widthBytes, height);
        ValidatePageExtent(x, y, (long)widthBytes * 8, height, 0,
            "TSPL 位图", estimated: false);
        if ((long)asciiHex.Length != (long)expectedBytes * 2)
            throw new ArgumentException("BITMAP 数据长度与尺寸不匹配。", nameof(asciiHex));
        using var operation = PrintResourceScope.BeginTemporary();
        var data = BudgetedBuffer.Create(asciiHex.Length / 2, "Tspl.BitmapHex", output =>
        {
            for (var i = 0; i < output.Length; i++)
            {
                if ((i & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                var high = HexDigit(asciiHex[i * 2]);
                var low = HexDigit(asciiHex[i * 2 + 1]);
                if (high < 0 || low < 0) throw new ArgumentException("BITMAP 数据必须为十六进制文本。", nameof(asciiHex));
                output[i] = (byte)((high << 4) | low);
            }
        });
        cancellationToken.ThrowIfCancellationRequested();
        var result = AddTsplCancellable((writer, buildCancellationToken) =>
        {
            buildCancellationToken.ThrowIfCancellationRequested();
            writer.Write($"BITMAP {x},{y},{widthBytes},{height},{mode},");
            writer.Write(data);
            writer.Write("\r\n");
        }, cancellationToken);
        operation?.CommitReservations();
        return result;
    }

    /// <summary>写入 TSPL BITMAP 的原始二进制数据。</summary>
    public TsplPrintCommand Bitmap(int x, int y, int widthBytes, int height, int mode, byte[] data)
        => Bitmap(x, y, widthBytes, height, mode, data, CancellationToken.None);

    /// <summary>写入 TSPL BITMAP 的原始二进制数据；输入快照支持取消。</summary>
    public TsplPrintCommand Bitmap(int x, int y, int widthBytes, int height, int mode, byte[] data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (data == null) throw new ArgumentNullException(nameof(data));
        ValidateBitmapDimensions(x, y, widthBytes, height, mode);
        var expectedBytes = CheckedBitmapByteCount(widthBytes, height);
        _profile?.ValidateImage(x, y, widthBytes, height);
        if (data.Length != expectedBytes) throw new ArgumentException("BITMAP 数据长度与尺寸不匹配。", nameof(data));
        using var operation = PrintResourceScope.BeginTemporary();
        var copy = BudgetedBuffer.Copy(data, "Tspl.BitmapSnapshot", cancellationToken);
        var result = AddTsplCancellable((writer, buildCancellationToken) =>
        {
            buildCancellationToken.ThrowIfCancellationRequested();
            writer.Write($"BITMAP {x},{y},{widthBytes},{height},{mode},");
            writer.Write(copy);
            writer.Write("\r\n");
        }, cancellationToken);
        operation?.CommitReservations();
        return result;
    }
    public TsplPrintCommand Image(int x, int y, int widthBytes, int height, int mode, byte[] data)
        => Bitmap(x, y, widthBytes, height, mode, data);
    public TsplPrintCommand Image(int x, int y, int widthBytes, int height, int mode, byte[] data,
        CancellationToken cancellationToken)
        => Bitmap(x, y, widthBytes, height, mode, data, cancellationToken);
    public TsplPrintCommand Feed(int distance = 0)
    {
        if (distance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
        return WriteTsplLine($"FEED {distance}");
    }
    /// <summary>打印当前标签；声明切刀契约时同步并关闭已声明的自动切纸设置。</summary>
    public TsplPrintCommand Print(int sets = 1, int copies = 1)
        => WriteControlledPrint(TsplCutEncoder.Print(_cutter, sets, copies));

    /// <summary>打印一个 PRINT 批次后裁切一次；预检失败不会追加任何命令。</summary>
    public TsplPrintCommand PrintWithCut(PrintCutOptions options, int sets = 1, int copies = 1)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        return WriteControlledPrint(TsplCutEncoder.Print(_cutter, sets, copies, options));
    }

    /// <summary>等待 EOJ 后使用机型已确认的立即 CUT；Batch 使用 PrintWithCut。</summary>
    public TsplPrintCommand Cut(PrintCutOptions options)
        => WriteControlledPrint(TsplCutEncoder.Cut(options, _cutter));

    /// <summary>按机型声明的固定切法执行立即 CUT。</summary>
    public TsplPrintCommand Cut() => Cut(new PrintCutOptions
    { Mode = _cutter?.SupportedCutModes[0] ?? PrintCutMode.Full });

    private TsplPrintCommand WriteControlledPrint(string data)
    {
        ValidateEncodable(data, nameof(data));
        var result = WriteRaw(MediaCommands(_pendingMediaOptions) + data);
        _hasStructuredOutput = true;
        _pendingMediaOptions = null;
        return result;
    }
    public TsplPrintCommand SelfTest() => WriteTsplLine("SELFTEST");

    private static PrintCapabilities CreateCapabilities(int dpi, LabelDeviceContract profile, TsplCutterProfile cutter,
        bool allowRasterizedTextStyles)
    {
        var supportsRasterizedTextStyles = allowRasterizedTextStyles && profile?.Images != false;
        var capabilities = new PrintCapabilities(PrintProtocol.Tspl, dpi, true, profile?.Images ?? true,
            profile?.QrCode ?? true, profile?.Rotation ?? true, cutter != null,
            supportsText: profile?.Fonts == null || profile.Fonts.Count != 0,
            supportsGraphics: profile?.Graphics ?? true, supportsBarcode: profile?.Barcode ?? true,
            supportsNativeLayout: true, supportsRasterLayout: true,
            supportsBoldText: supportsRasterizedTextStyles, supportsUnderlineText: supportsRasterizedTextStyles,
            supportsReverseText: supportsRasterizedTextStyles,
            supportsHostFontFamily: supportsRasterizedTextStyles)
        { JobFeedUnits = Array.AsReadOnly(new[] { PrintFeedUnit.Dots }) };
        capabilities.ApplyLabelProfile(profile);
        if (cutter != null)
        {
            capabilities.TsplCutter = cutter;
            capabilities.CutModes = Array.AsReadOnly((PrintCutMode[])cutter.SupportedCutModes.Clone());
            if (cutter.CutPositionFeedDots.HasValue)
            {
                capabilities.CutFeedUnits = Array.AsReadOnly(new[] { PrintFeedUnit.Dots });
                capabilities.CutFeedDots = new PrintParameterRange(0, 9999 - cutter.CutPositionFeedDots.Value);
            }
        }
        return capabilities;
    }

    private bool _hasStructuredOutput;

    private TsplPrintCommand WriteTsplLine(string line)
        => WriteTsplLine(line, CancellationToken.None);

    private TsplPrintCommand WriteTsplLine(string line, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateEncodable(line, nameof(line));
        var commands = MediaCommands(_pendingMediaOptions) + line + "\r\n";
        cancellationToken.ThrowIfCancellationRequested();
        var result = WriteRaw(commands);
        _hasStructuredOutput = true;
        _pendingMediaOptions = null;
        return result;
    }

    private void ApplyCoordinateOptions(TsplMediaOptions options)
    {
        if (options == null) return;
        if (options.Direction.HasValue) _direction = options.Direction.Value;
        if (options.ReferenceX.HasValue) _referenceX = options.ReferenceX.Value;
        if (options.ReferenceY.HasValue) _referenceY = options.ReferenceY.Value;
    }

    private (long Width, long Height) TsplTextExtent(string font, int xScale, int yScale, string data)
    {
        var cell = font == "0"
            ? (Width: Math.Max(1, (int)Math.Ceiling((double)xScale * Capabilities.Dpi / 72)),
                Height: Math.Max(1, (int)Math.Ceiling((double)yScale * Capabilities.Dpi / 72)))
            : TsplFontSizing.FixedCell(font);
        long width = 0;
        foreach (var scalar in PrintUnicodeScalars.Enumerate(data))
            width = checked(width + (long)cell.Width * (font == "0" ? 1 : xScale) *
                            (scalar <= 127 ? 1 : 2));
        return (width, font == "0" ? cell.Height : checked((long)cell.Height * yScale));
    }

    private void ValidatePageExtent(long x, long y, long width, long height, int rotation,
        string element, bool estimated)
    {
        if (!_hasPageSize || !_validatePageBounds) return;
        PrintCommandPageBounds.ValidateExtent(CurrentPageWidthDots, CurrentPageHeightDots,
            x, y, width, height, rotation, element, estimated, _referenceX, _referenceY,
            _direction == 0 ? PrintOrientation.None : PrintOrientation.Down);
    }

    private void ValidatePageEdges(long left, long top, long right, long bottom,
        string element, bool estimated)
    {
        if (!_hasPageSize || !_validatePageBounds) return;
        PrintCommandPageBounds.ValidateEdges(CurrentPageWidthDots, CurrentPageHeightDots,
            left, top, right, bottom, element, estimated, _referenceX, _referenceY,
            _direction == 0 ? PrintOrientation.None : PrintOrientation.Down);
    }

    private double CurrentPageWidthDots => _pageUnit == "dot"
        ? _pageWidth
        : _pageWidth * (_capabilities.Dpi / 25.4);

    private double CurrentPageHeightDots => _pageUnit == "dot"
        ? _pageHeight
        : _pageHeight * (_capabilities.Dpi / 25.4);

    private TsplPrintCommand AddTspl(Action<IBufferWriter> action)
    {
        var commands = MediaCommands(_pendingMediaOptions);
        var result = AddWithStorage(writer => { writer.Write(commands); action(writer); }, PrintStorageBudget.TextBytes(commands));
        _hasStructuredOutput = true;
        _pendingMediaOptions = null;
        return result;
    }

    private TsplPrintCommand AddTsplCancellable(Action<IBufferWriter, CancellationToken> action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = PrintResourceScope.BeginTemporary();
        var commands = MediaCommands(_pendingMediaOptions);
        PrintStorageBudget.Reserve(PrintStorageBudget.TextBytes(commands), "Command.RecordData");
        cancellationToken.ThrowIfCancellationRequested();
        var result = AddCancellable((writer, token) => { writer.Write(commands); action(writer, token); });
        _hasStructuredOutput = true;
        _pendingMediaOptions = null;
        operation?.CommitReservations();
        return result;
    }

    private static string MediaCommands(TsplMediaOptions options)
    {
        if (options == null) return string.Empty;
        var commands = new StringBuilder();
        if (options.GapDistance.HasValue || options.GapOffset.HasValue)
            commands.Append($"GAP {options.GapDistance.GetValueOrDefault()},{options.GapOffset.GetValueOrDefault()}\r\n");
        if (options.Direction.HasValue)
            commands.Append($"DIRECTION {options.Direction.Value}\r\n");
        if (options.ReferenceX.HasValue || options.ReferenceY.HasValue)
            commands.Append($"REFERENCE {options.ReferenceX.GetValueOrDefault()},{options.ReferenceY.GetValueOrDefault()}\r\n");
        if (options.Speed.HasValue)
            commands.Append($"SPEED {options.Speed.Value}\r\n");
        if (options.Density.HasValue)
            commands.Append($"DENSITY {options.Density.Value}\r\n");
        return commands.ToString();
    }

    private static int HexDigit(char value)
    {
        if (value >= '0' && value <= '9') return value - '0';
        if (value >= 'A' && value <= 'F') return value - 'A' + 10;
        if (value >= 'a' && value <= 'f') return value - 'a' + 10;
        return -1;
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

    /// <summary>
    /// 校验 TSPL 内置条码的数据格式，并补齐允许省略的零售条码校验位。
    /// </summary>
    /// <remarks>
    /// 未列入统一能力描述的 TSPL 类型仍保留原始命令扩展入口；已声明支持的类型
    /// 使用与模板 <c>BarcodeMetrics</c> 一致的字符、长度和校验位规则。
    /// </remarks>
    private string NormalizeSupportedBarcodeData(string type, string data)
    {
        var normalizedType = type.ToUpperInvariant();
        switch (normalizedType)
        {
            case "128":
                ValidatePrintableAscii(data, "Code128 条码必须为非空可打印 ASCII。", nameof(data));
                return data;
            case "93":
                ValidatePrintableAscii(data, "Code93 条码必须为非空可打印 ASCII。", nameof(data));
                return data;
            case "39":
                ValidateAlphabet(data, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%",
                    "Code39 条码包含不支持的字符。", nameof(data));
                return data;
            case "25":
                ValidateDigits(data, nameof(data));
                // TSPL 的 Interleaved 2 of 5 要求成对数字；统一模型允许省略前导零。
                return data.Length % 2 == 0 ? data : "0" + data;
            case "25C":
                ValidateDigits(data, nameof(data));
                // TSPL 25C 由设备生成校验位，输入数据需保持奇数位。
                return data.Length % 2 == 1 ? data : "0" + data;
            case "EAN13":
                return NormalizeRetailBarcode(data, 13, nameof(data));
            case "EAN8":
                if (data.Length == 6) data = "0" + data;
                return NormalizeRetailBarcode(data, 8, nameof(data));
            case "UPCA":
                return NormalizeRetailBarcode(data, 12, nameof(data));
            case "UPCE":
                ValidateDigits(data, nameof(data));
                if (data.Length == 6) data = "0" + data;
                if (data.Length != 7 || (data[0] != '0' && data[0] != '1'))
                    throw new ArgumentException("UPC-E 使用 6 位数据或 7 位含号码系统的数据（号码系统为 0/1）。", nameof(data));
                return data;
            case "MSI":
                ValidateDigits(data, nameof(data));
                if (data.Length > 13)
                    throw new ArgumentException("MSI 最多支持 13 位数字。", nameof(data));
                return data;
            case "11":
                return NormalizeCode11Data(data, nameof(data));
            case "CPOST":
                return NormalizeChinaPostData(data, nameof(data));
            case "TELEPEN":
                return NormalizeTelepenData(data, nameof(data));
            case "TELEPENN":
                return NormalizeTelepennData(data, nameof(data));
            case "CODE49":
                return NormalizeCode49Data(data, nameof(data));
            case "EAN128":
                return NormalizeUccEan128Data(data);
            case "DPI":
                ValidateDigits(data, nameof(data));
                if (data.Length != 11)
                    throw new ArgumentException("DPI 需要 11 位数字 payload。", nameof(data));
                return data;
            case "DPL":
                ValidateDigits(data, nameof(data));
                if (data.Length != 13)
                    throw new ArgumentException("DPL 需要 13 位数字 payload。", nameof(data));
                return data;
            case "POST":
                return NormalizePostnetData(data, nameof(data));
            case "PLANET":
                return NormalizePlanetData(data, nameof(data));
            case "PLESSEY":
                ValidateDigits(data, nameof(data));
                return data;
            case "ITF14":
                return NormalizeItf14Data(data, nameof(data));
            case "EAN14":
                return NormalizeEan14Data(data, nameof(data));
            case "CODABAR":
                if (data.Length < 3 || "ABCD".IndexOf(data[0]) < 0 ||
                    "ABCD".IndexOf(data[data.Length - 1]) < 0)
                    throw new ArgumentException("Codabar 必须包含 A/B/C/D 起止符和至少一个数据字符。", nameof(data));
                ValidateAlphabet(data.Substring(1, data.Length - 2), "0123456789-$:/.+",
                    "Codabar 条码内部包含不支持的字符。", nameof(data));
                return data;
            default:
                return data;
        }
    }

    private static BarcodeType? BarcodeTypeFromTspl(string type)
    {
        switch (type.ToUpperInvariant())
        {
            case "128": return BarcodeType.Code128;
            case "39": return BarcodeType.Code39;
            case "93": return BarcodeType.Code93;
            case "EAN8": return BarcodeType.Ean8;
            case "EAN13": return BarcodeType.Ean13;
            case "UPCA": return BarcodeType.UpcA;
            case "UPCE": return BarcodeType.UpcE;
            case "CODABAR": return BarcodeType.Codabar;
            case "25": return BarcodeType.I2OF5;
            case "25C": return BarcodeType.I2OF5C;
            case "DPI":
            case "DPL": return BarcodeType.I2OF5G;
            case "MSI": return BarcodeType.Msi;
            case "EAN128": return BarcodeType.UccEan128;
            case "POST": return BarcodeType.Postnet;
            case "PLANET": return BarcodeType.Planet;
            case "PLESSEY": return BarcodeType.Plessey;
            case "ITF14": return BarcodeType.Itf14;
            case "EAN14": return BarcodeType.Ean14;
            case "11": return BarcodeType.Code11;
            case "CPOST": return BarcodeType.ChinaPost;
            case "TELEPEN": return BarcodeType.Telepen;
            case "TELEPENN": return BarcodeType.Telepenn;
            case "CODE49": return BarcodeType.Code49;
            default: return null;
        }
    }

    private string NormalizeUccEan128Data(string data)
    {
        if (Gs1ApplicationIdentifierParser.ShouldNormalize(data))
            data = Gs1ApplicationIdentifierParser.NormalizeForProtocol(data, nameof(data), 255);

        if (data.Length < 3 || data[0] < '0' || data[0] > '9' ||
            data[1] < '0' || data[1] > '9')
            throw new ArgumentException("EAN128 需要以数字应用标识开头的 GS1 元素串。", nameof(data));
        Gs1BarcodeCapacity.Validate(BarcodeType.UccEan128, data,
            _profile?.MaxGs1LinearPayloadCharacters, nameof(data));

        var builder = new StringBuilder(data.Length);
        for (var index = 0; index < data.Length; index++)
        {
            var character = data[index];
            if (character == '\u001D')
            {
                builder.Append("!102");
                continue;
            }
            if (character == '!')
                throw new ArgumentException("EAN128 数据不能包含字面 !；该字符是 TSPL 控制码前缀。", nameof(data));
            if (character < 32 || character > 126)
                throw new ArgumentException("EAN128 仅接受 7-bit ASCII 或 ASCII 29 分隔符。", nameof(data));
            builder.Append(character);
        }
        return builder.ToString();
    }

    private static string NormalizeRetailBarcode(string data, int length, string name)
    {
        ValidateDigits(data, name);
        if (data.Length != length - 1 && data.Length != length)
            throw new ArgumentException($"条码需要 {length - 1} 或 {length} 位数字。", name);

        var sum = 0;
        for (var index = length - 2; index >= 0; index--)
            sum += (data[index] - '0') * ((length - 2 - index) % 2 == 0 ? 3 : 1);
        var check = (char)('0' + (10 - sum % 10) % 10);
        if (data.Length == length && data[length - 1] != check)
            throw new ArgumentException("条码校验位错误。", name);
        return data.Length == length ? data : data + check;
    }

    private static string NormalizeI2OF5GermanData(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        ValidateDigits(data, name);
        if (data.Length != 11 && data.Length != 12 && data.Length != 13 && data.Length != 14)
            throw new ArgumentException("I2OF5G 需要 11、12、13 或 14 位数字。", name);

        if ((data.Length & 1) == 1)
            return data + CalculateI2OF5CheckDigit(data, 4, 9);

        var expected = CalculateI2OF5CheckDigit(data.Substring(0, data.Length - 1), 4, 9);
        if (data[data.Length - 1] != expected)
            throw new ArgumentException("I2OF5G 校验位错误。", name);
        return data;
    }

    private static string NormalizePostnetData(string data, string name)
    {
        ValidateNonEmptyField(data, name, '"');
        var normalized = data.Replace("-", string.Empty);
        ValidateDigits(normalized, name);
        if (normalized.Length != 5 && normalized.Length != 9 && normalized.Length != 11)
            throw new ArgumentException("POSTNET 需要 5、9 或 11 位邮政数字。", name);
        return normalized;
    }

    private static string NormalizePlanetData(string data, string name)
    {
        ValidateNonEmptyField(data, name, '"');
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

    private static string NormalizeItf14Data(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        ValidateDigits(data, name);
        if (data.Length != 13)
            throw new ArgumentException("ITF14 需要 13 位数字 payload。", name);
        return data;
    }

    private static string NormalizeEan14Data(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        ValidateDigits(data, name);
        if (data.Length != 13)
            throw new ArgumentException("EAN14 需要 13 位数字 payload。", name);
        return data;
    }

    private static string NormalizeCode11Data(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        if (data.Length == 0)
            throw new ArgumentException("Code 11 必须包含至少一个数字或连字符。", name);
        for (var index = 0; index < data.Length; index++)
        {
            var character = data[index];
            if (!((character >= '0' && character <= '9') || character == '-'))
                throw new ArgumentException("Code 11 仅接受 ASCII 数字和连字符。", name);
        }
        return data;
    }

    private static string NormalizeChinaPostData(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        ValidateDigits(data, name);
        return data;
    }

    private static string NormalizeTelepenData(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        if (data.Length == 0 || data.Length > 30)
            throw new ArgumentException("TELEPEN 需要 1–30 个 ASCII 字符。", name);
        for (var index = 0; index < data.Length; index++)
        {
            var character = data[index];
            if (character < 32 || character > 127 || character == '"')
                throw new ArgumentException("TELEPEN 仅接受可安全嵌入 TSPL 字段的 ASCII 字符（32–127，不含引号）。", name);
        }
        return data;
    }

    private static string NormalizeTelepennData(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        ValidateDigits(data, name);
        if (data.Length > 60)
            throw new ArgumentException("TELEPENN 最多支持 60 位 ASCII 数字。", name);
        return data;
    }

    private static string NormalizeCode49Data(string data, string name)
    {
        if (data == null) throw new ArgumentNullException(name);
        if (data.Length == 0 || data.Length > 81)
            throw new ArgumentException("CODE49 需要 1–81 个 ASCII 字符。", name);
        for (var index = 0; index < data.Length; index++)
        {
            var character = data[index];
            if (character < 32 || character > 127 || character == '"')
                throw new ArgumentException("CODE49 仅接受可安全嵌入 TSPL 字段的 ASCII 字符（32–127，不含引号）。", name);
        }
        return data;
    }

    private static char CalculateI2OF5CheckDigit(string data, int firstWeight, int secondWeight)
    {
        var sum = 0;
        for (var index = 0; index < data.Length; index++)
            sum += (data[index] - '0') * (index % 2 == 0 ? firstWeight : secondWeight);
        return (char)('0' + (10 - sum % 10) % 10);
    }

    private static void ValidatePrintableAscii(string data, string message, string name)
    {
        if (data.Length == 0) throw new ArgumentException(message, name);
        for (var index = 0; index < data.Length; index++)
            if (data[index] < 32 || data[index] > 126)
                throw new ArgumentException(message, name);
    }

    private static void ValidateDigits(string data, string name)
    {
        if (data.Length == 0)
            throw new ArgumentException("条码必须为非空 ASCII 数字。", name);
        for (var index = 0; index < data.Length; index++)
            if (data[index] < '0' || data[index] > '9')
                throw new ArgumentException("该制式仅接受 ASCII 数字。", name);
    }

    private static void ValidateTsplBarcodeRatio(string type, int narrow, int wide)
    {
        if (string.Equals(type, "PLESSEY", StringComparison.OrdinalIgnoreCase) &&
            (long)wide != (long)narrow * 2)
            throw new ArgumentException("TSPL PLESSEY 要求窄条:宽条为 1:2。", nameof(wide));
        if (string.Equals(type, "EAN14", StringComparison.OrdinalIgnoreCase) &&
            (long)wide != narrow)
            throw new ArgumentException("TSPL EAN14 要求窄条:宽条为 1:1。", nameof(wide));
        if (string.Equals(type, "CPOST", StringComparison.OrdinalIgnoreCase) &&
            (long)wide != narrow)
            throw new ArgumentException("TSPL CPOST 要求窄条:宽条为 1:1。", nameof(wide));
        if ((string.Equals(type, "TELEPEN", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type, "TELEPENN", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type, "CODE49", StringComparison.OrdinalIgnoreCase)) &&
            !IsTsplVariableRatio(narrow, wide))
            throw new ArgumentException("TSPL TELEPEN/TELEPENN/CODE49 仅支持 1:1、1:2、1:3、2:5 或 3:7 的窄宽比。", nameof(wide));
    }

    private static bool IsTsplVariableRatio(int narrow, int wide)
        => (long)wide == narrow || (long)wide == 2 * narrow ||
           (long)wide == 3 * narrow || 2L * wide == 5L * narrow ||
           3L * wide == 7L * narrow;

    private static void ValidateAlphabet(string data, string alphabet, string message, string name)
    {
        if (data.Length == 0) throw new ArgumentException(message, name);
        for (var index = 0; index < data.Length; index++)
            if (alphabet.IndexOf(data[index]) < 0)
                throw new ArgumentException(message, name);
    }

    private static void ValidateRectangle(int x0, int y0, int x1, int y1, int width)
    {
        ValidateCoordinate(x0, nameof(x0));
        ValidateCoordinate(y0, nameof(y0));
        ValidateCoordinate(x1, nameof(x1));
        ValidateCoordinate(y1, nameof(y1));
        if (x1 <= x0) throw new ArgumentOutOfRangeException(nameof(x1));
        if (y1 <= y0) throw new ArgumentOutOfRangeException(nameof(y1));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
    }

    private static void ValidateBitmapDimensions(int x, int y, int widthBytes, int height, int mode)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        if (widthBytes <= 0) throw new ArgumentOutOfRangeException(nameof(widthBytes));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (mode < 0 || mode > 2) throw new ArgumentOutOfRangeException(nameof(mode));
    }

    private static int CheckedBitmapByteCount(int widthBytes, int height)
    {
        var count = (long)widthBytes * height;
        if (count > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(height), "BITMAP 尺寸超出可表示范围。");
        return (int)count;
    }

    private static int CheckedRasterDimension(long value, string name)
    {
        if (value <= 0 || value > int.MaxValue - 7) throw new ArgumentOutOfRangeException(name, "光栅尺寸超出可表示范围。");
        return (int)value;
    }

    private static void ValidateRasterBufferSize(int width, int height)
    {
        var bytes = ((long)width + 7) / 8 * height;
        if (bytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(height), "光栅缓冲区超出可表示范围。");
    }
}
