using System;
using System.Text;
using System.Collections.Generic;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

/// <summary>生成 ESC/POS 收据打印指令。</summary>
public sealed partial class EscPosPrintCommand : ProtocolPrintCommandBase<EscPosPrintCommand>
{
    /// <summary>获取 ESC/POS 的基础能力描述。</summary>
    public override PrintProtocol Protocol => PrintProtocol.EscPos;
    public override PrintCapabilities Capabilities { get; }
    public EscPosLayoutMode LayoutMode { get; }
    private readonly byte? _codePageIndex;
    private readonly int? _printAreaWidthUnits;
    private readonly EscPosPrinterProfile _profile;
    private readonly RasterFontCatalog _rasterFonts;
    private EscPosRasterCanvas _activeRasterPage;
    private bool _endingRasterPage;
    private int? _restrictedWidthDots;
    private int? _restrictedTabWidthDots;
    private int? _restrictedPrintAreaWidthUnits;
    private long _restrictedLineWidthDots;
    private int _restrictedAlignment;
    private int _restrictedTextWidthScale = 1;

    public EscPosPrintCommand() : this(null, EscPosLayoutMode.Native) { }

    public EscPosPrintCommand(Encoding encoding = null, EscPosLayoutMode layoutMode = EscPosLayoutMode.Native,
        EscPosDeviceOptions options = null)
        : base(encoding ?? Encoding.GetEncoding("gb18030"))
    {
        if (!PrintCapabilities.IsEscPosEncodingCompatible(Encoding))
            throw new NotSupportedException(
                $"ESC/POS 编码 {Encoding.WebName} 必须保持 ASCII 字符和 CR/LF 为单字节；请使用 ASCII 兼容编码。");
        if (layoutMode != EscPosLayoutMode.Native && layoutMode != EscPosLayoutMode.Raster)
            throw new ArgumentOutOfRangeException(nameof(layoutMode), layoutMode, "不支持的 ESC/POS 布局模式。");
        if (options?.RasterFonts != null && layoutMode != EscPosLayoutMode.Raster)
            throw new ArgumentException("固定主机字体配置只能用于 ESC/POS Raster。", nameof(options.RasterFonts));
        if (options?.RasterBarcodeHumanReadableFontSize != null && layoutMode != EscPosLayoutMode.Raster)
            throw new ArgumentException("Raster 条码可读文字字号只能用于 ESC/POS Raster。",
                nameof(options.RasterBarcodeHumanReadableFontSize));
        if (options?.RasterBarcodeHumanReadableFontSize is int humanReadableFontSize &&
            (humanReadableFontSize <= 0 || humanReadableFontSize > 255))
            throw new ArgumentOutOfRangeException(nameof(options.RasterBarcodeHumanReadableFontSize),
                "Raster 条码可读文字字号必须在 1–255 点范围内。");
        _rasterFonts = options?.RasterFonts;
        if (options != null && !Enum.IsDefined(typeof(EscPosNativeFlowMode), options.NativeFlow))
            throw new ArgumentOutOfRangeException(nameof(options.NativeFlow));
        if (layoutMode != EscPosLayoutMode.Native && options?.NativeFlow == EscPosNativeFlowMode.Sequential)
            throw new ArgumentException("Native 顺序流只能用于 ESC/POS Native 布局。", nameof(options));
        _profile = options?.Profile?.Snapshot();
        if (_profile?.Text != null)
        {
            if (layoutMode == EscPosLayoutMode.Native) _profile.Text.ValidateEncoding(Encoding);
            if (options?.CodePageIndex != null && options.CodePageIndex != _profile.Text.CodePageIndex)
                throw new ArgumentException("CodePageIndex 与设备文字契约声明的索引不一致。", nameof(options.CodePageIndex));
        }
        if (_profile != null && options.Dpi != _profile.HorizontalDpi)
            throw new ArgumentException("输入 DPI 必须与机型配置的水平 DPI 一致。", nameof(options));
        if (options?.PrintAreaWidthUnits != null && _profile?.SupportsPrintAreaWidth == false)
            throw new NotSupportedException($"设备 {_profile.Name} 不支持 GS W 打印区域设置。");
        if (options != null && options.Dpi <= 0) throw new ArgumentOutOfRangeException(nameof(options.Dpi));
        if (options?.PrintAreaWidthUnits is int printWidth && (printWidth < 0 || printWidth > ushort.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(options.PrintAreaWidthUnits));
        if (options?.NativeTabWidthDots is int tabWidth && (tabWidth < 8 || tabWidth % 8 != 0 ||
            layoutMode != EscPosLayoutMode.Native))
            throw new ArgumentOutOfRangeException(nameof(options.NativeTabWidthDots));
        LayoutMode = layoutMode;
        _codePageIndex = options?.CodePageIndex ?? _profile?.Text?.CodePageIndex;
        _printAreaWidthUnits = options?.PrintAreaWidthUnits;
        Capabilities = new PrintCapabilities(PrintProtocol.EscPos, options?.Dpi ?? 203,
            layoutMode == EscPosLayoutMode.Raster, layoutMode == EscPosLayoutMode.Raster,
            layoutMode == EscPosLayoutMode.Raster || _profile?.SupportsNativeQrCode != false, false,
            _profile?.SupportsCut != false && (_profile?.SupportedCutModes?.Length ?? 1) != 0,
            supportsText: true, supportsGraphics: layoutMode == EscPosLayoutMode.Raster,
            supportsBarcode: layoutMode == EscPosLayoutMode.Raster || _profile?.SupportsNativeBarcode != false,
            supportsNativeLayout: layoutMode == EscPosLayoutMode.Native,
            supportsRasterLayout: layoutMode == EscPosLayoutMode.Raster,
            // ESC/POS 的 ESC - n 可在 Native 模式切换单线下划线；Raster 由主机字体绘制。
            supportsUnderlineText: true,
            // Native 使用 GS B n；Raster 在主机生成带反白背景的文字位图。
            supportsReverseText: true,
            supportsHostFontFamily: layoutMode == EscPosLayoutMode.Raster,
            maxRasterRowBytes: layoutMode == EscPosLayoutMode.Raster
                ? (_profile?.ImageCommand == EscPosImageCommand.BitImageEscStar ? (ushort.MaxValue + 7) / 8 : ushort.MaxValue)
                : (int?)null,
            maxRasterHeight: layoutMode == EscPosLayoutMode.Raster ? ushort.MaxValue : (int?)null)
        {
            RasterFonts = _rasterFonts,
            RasterBarcodeHumanReadableFontSize = layoutMode == EscPosLayoutMode.Raster
                ? options?.RasterBarcodeHumanReadableFontSize ?? 12 : (int?)null,
            NativeTextProfile = layoutMode == EscPosLayoutMode.Native ? _profile?.Text : null,
            SupportsNativeBarcodeHumanReadableOverride = _profile?.SupportsNativeBarcodeHumanReadable != false,
            SupportedNativeBarcodeHumanReadableTypesOverride = _profile?.SupportedNativeBarcodeHumanReadableTypes == null
                ? null : Array.AsReadOnly((BarcodeType[])_profile.SupportedNativeBarcodeHumanReadableTypes.Clone()),
            SupportedNativeBarcodeHumanReadablePositionsOverride = _profile?.SupportedNativeBarcodeHumanReadablePositions == null
                ? null : Array.AsReadOnly((EscPosBarcodeTextPosition[])_profile.SupportedNativeBarcodeHumanReadablePositions.Clone()),
            NativeFlow = layoutMode == EscPosLayoutMode.Native
                ? options?.NativeFlow ?? EscPosNativeFlowMode.Rows : (EscPosNativeFlowMode?)null,
            JobFeedUnits = Array.AsReadOnly(_profile == null
                ? new[] { PrintFeedUnit.Lines, PrintFeedUnit.MotionUnits }
                : new[] { PrintFeedUnit.Lines, PrintFeedUnit.MotionUnits, PrintFeedUnit.Dots }),
            SupportsCommandBarcode = _profile?.SupportsNativeBarcode != false,
            SupportsCommandQrCode = _profile?.SupportsNativeQrCode != false,
            MaxPrintableWidthDots = _profile?.PrintableWidthDots,
            RasterVerticalDpi = layoutMode == EscPosLayoutMode.Raster ? _profile?.VerticalDpi ?? options?.Dpi ?? 203 : (int?)null,
            RasterImageCommand = layoutMode == EscPosLayoutMode.Raster
                ? _profile?.ImageCommand ?? EscPosImageCommand.RasterGsV0 : (EscPosImageCommand?)null,
            MaxImageBlockBytes = layoutMode == EscPosLayoutMode.Raster ? _profile?.MaxImageBlockBytes : null
        };
        _restrictedWidthDots = Capabilities.MaxPrintableWidthDots;
        _restrictedTabWidthDots = options?.NativeTabWidthDots;
        _restrictedPrintAreaWidthUnits = _printAreaWidthUnits;
        if (_printAreaWidthUnits.HasValue)
            _restrictedWidthDots = ConvertWidthUnitsToDots(_printAreaWidthUnits.Value);
        Capabilities.MaxGs1LinearPayloadCharacters = _profile?.MaxGs1LinearPayloadCharacters;
        Capabilities.MaxGs1DataBarExpandedPayloadCharacters = _profile?.MaxGs1DataBarExpandedPayloadCharacters;
        if (Capabilities.SupportsCut)
        {
            Capabilities.CutModes = Array.AsReadOnly(_profile?.SupportedCutModes == null
                ? new[] { PrintCutMode.Full, PrintCutMode.Partial } : (PrintCutMode[])_profile.SupportedCutModes.Clone());
            if (_profile?.SupportsFeedAndCut == true)
            {
                Capabilities.CutFeedUnits = Array.AsReadOnly(new[] { PrintFeedUnit.MotionUnits, PrintFeedUnit.Dots });
                Capabilities.CutFeedMotionUnits = new PrintParameterRange(0, 255);
            }
        }
    }

    /// <summary>
    /// 初始化打印机。
    /// </summary>
    /// <remarks>
    /// ESC/POS 没有跨设备通用的“字符列数”设置；字符宽度取决于字体、代码页和打印机型号。
    /// 因此传入列数会严格失败，避免把列数误当作 <c>GS !</c> 的字符放大倍数。
    /// 需要设置实际打印区域时，请使用 <see cref="SetPrintAreaWidth"/>，其单位为水平运动单位。
    /// </remarks>
    public EscPosPrintCommand Initialize(int? columns = null)
    {
        if (columns.HasValue)
            throw new NotSupportedException("ESC/POS 不支持可移植的字符列数设置；请使用 SetPrintAreaWidth 设置水平运动单位宽度。");

        var bytes = new List<byte> { 0x1B, 0x40 };
        if (_codePageIndex.HasValue) bytes.AddRange(new byte[] { 0x1B, 0x74, _codePageIndex.Value });
        if (_profile?.Text != null && _profile.Text.CharacterMode != EscPosCharacterMode.DeviceDefault)
            bytes.AddRange(new byte[] { 0x1C, _profile.Text.CharacterMode == EscPosCharacterMode.FsKanji ? (byte)0x26 : (byte)0x2E });
        if (_printAreaWidthUnits.HasValue)
        {
            if (_profile?.SupportsPrintAreaWidth == false)
                throw new NotSupportedException($"设备 {_profile.Name} 不支持 GS W 打印区域设置。");
            bytes.AddRange(new byte[] { 0x1D, 0x57, (byte)(_printAreaWidthUnits.Value & 255), (byte)(_printAreaWidthUnits.Value >> 8) });
        }
        var result = WriteEncoded(bytes.ToArray());
        _restrictedLineWidthDots = 0;
        _restrictedAlignment = 0;
        _restrictedTextWidthScale = 1;
        _restrictedWidthDots = _printAreaWidthUnits.HasValue
            ? ConvertWidthUnitsToDots(_printAreaWidthUnits.Value)
            : Capabilities.MaxPrintableWidthDots;
        return result;
    }

    /// <summary>使用 ESC t 选择设备代码页；编号由具体打印机决定。</summary>
    public EscPosPrintCommand SelectCodePage(byte codePageIndex)
    {
        if (_profile?.Text != null && _profile.Text.CodePageIndex != codePageIndex)
            throw new NotSupportedException("所选设备代码页不符合当前文字契约；请创建匹配的机型配置。");
        return WriteEncoded(new byte[] { 0x1B, 0x74, codePageIndex });
    }

    /// <summary>选择声明过的设备字符模式；未绑定文字契约时不推断 FS 指令支持。</summary>
    public EscPosPrintCommand SetCharacterMode(EscPosCharacterMode mode)
    {
        if (!Enum.IsDefined(typeof(EscPosCharacterMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (_profile?.Text == null || mode != _profile.Text.CharacterMode)
            throw new NotSupportedException("字符模式必须与目标机型文字契约一致。");
        if (mode == EscPosCharacterMode.DeviceDefault) return this;
        return WriteEncoded(new byte[] { 0x1C, mode == EscPosCharacterMode.FsKanji ? (byte)0x26 : (byte)0x2E });
    }

    /// <summary>
    /// 设置标准模式下的打印区域宽度。
    /// </summary>
    /// <param name="widthUnits">打印区域宽度，单位为打印机的水平运动单位，范围为 0 到 65535。</param>
    /// <remarks>对应 ESC/POS 的 <c>GS W nL nH</c> 命令，不是字符列数或固定 DPI 的点数。</remarks>
    public EscPosPrintCommand SetPrintAreaWidth(int widthUnits)
    {
        if (_profile?.SupportsPrintAreaWidth == false)
            throw new NotSupportedException($"设备 {_profile.Name} 不支持 GS W 打印区域设置。");
        if (widthUnits < 0 || widthUnits > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(widthUnits));

        var result = WriteEncoded(new byte[] { 0x1D, 0x57, (byte)(widthUnits & 255), (byte)(widthUnits >> 8) });
        _restrictedPrintAreaWidthUnits = widthUnits;
        _restrictedWidthDots = ConvertWidthUnitsToDots(widthUnits);
        return result;
    }

    public EscPosPrintCommand Align(byte alignment)
    {
        if (alignment > 2) throw new ArgumentOutOfRangeException(nameof(alignment));
        var result = WriteEncoded(new byte[] { 0x1B, 0x61, alignment });
        _restrictedAlignment = alignment;
        return result;
    }

    /// <summary>设置 ESC/POS 原生文字的加粗状态。</summary>
    public EscPosPrintCommand Bold(bool enabled = true) => WriteEncoded(new byte[] { 0x1B, 0x45, (byte)(enabled ? 1 : 0) });

    /// <summary>设置 ESC/POS 原生文字的单线下划线状态。</summary>
    /// <remarks>对应 ESC/POS <c>ESC - n</c>；Native 使用 0/1，Raster 仍由主机画布处理。</remarks>
    public EscPosPrintCommand Underline(bool enabled = true) =>
        WriteEncoded(new byte[] { 0x1B, 0x2D, (byte)(enabled ? 1 : 0) });

    /// <summary>设置 ESC/POS 原生文字的反白状态。</summary>
    /// <remarks>对应 ESC/POS <c>GS B n</c>；Native 使用 0/1，Raster 仍由主机画布处理。</remarks>
    public EscPosPrintCommand Reverse(bool enabled = true) =>
        WriteEncoded(new byte[] { 0x1D, 0x42, (byte)(enabled ? 1 : 0) });
    public EscPosPrintCommand Text(string value)
        => Text(value, System.Threading.CancellationToken.None);

    /// <summary>校验并记录可取消的原生文字及换行。</summary>
    public EscPosPrintCommand Text(string value, System.Threading.CancellationToken cancellationToken)
    {
        ValidateText(value, nameof(value), cancellationToken);
        ValidateNativeText(value, cancellationToken);
        ValidateEncodable(value, nameof(value));
        cancellationToken.ThrowIfCancellationRequested();
        var result = AddWithStorage(w => { w.Write(value); w.Write("\n"); }, Core.PrintStorageBudget.TextBytes(value));
        TrackRestrictedText(value);
        _restrictedLineWidthDots = 0;
        return result;
    }
    /// <summary>写入文字但不自动换行，便于组成原生行和制表列。</summary>
    public EscPosPrintCommand WriteText(string value)
        => WriteText(value, System.Threading.CancellationToken.None);

    /// <summary>校验并记录可取消的原生文字，不自动换行。</summary>
    public EscPosPrintCommand WriteText(string value, System.Threading.CancellationToken cancellationToken)
    {
        ValidateText(value, nameof(value), cancellationToken);
        ValidateNativeText(value, cancellationToken);
        ValidateEncodable(value, nameof(value));
        cancellationToken.ThrowIfCancellationRequested();
        var result = AddWithStorage(w => w.Write(value), Core.PrintStorageBudget.TextBytes(value));
        TrackRestrictedText(value);
        return result;
    }

    private void ValidateNativeText(string value, System.Threading.CancellationToken cancellationToken)
    {
        _profile?.Text?.ValidateEncoding(Encoding);
        _profile?.Text?.ValidateText(value, cancellationToken);
    }

    /// <summary>写入原生水平制表符。</summary>
    public EscPosPrintCommand Tab()
    {
        var result = WriteEncoded(new byte[] { 0x09 });
        AdvanceRestrictedTab();
        return result;
    }

    /// <summary>写入指定数量的原生换行符。</summary>
    public EscPosPrintCommand NewLine(int lines = 1)
    {
        if (lines < 1 || lines > 255) throw new ArgumentOutOfRangeException(nameof(lines));
        var result = Add(w => { for (var index = 0; index < lines; index++) w.Write(new byte[] { 0x0A }); });
        _restrictedLineWidthDots = 0;
        return result;
    }
    public EscPosPrintCommand Text(int x, int y, string value)
    {
        var canvas = RequireRasterPage("绝对坐标文本");
        ValidateText(value, nameof(value));
        ValidateRasterPoint(canvas, x, y);
        canvas.DrawText(x, y, value, 24);
        return this;
    }

    /// <summary>在 Raster 光栅页按指定字号、样式和顺时针角度绘制文字。</summary>
    /// <param name="x">旋转后文字墨迹外框的左上角 X 坐标。</param>
    /// <param name="y">旋转后文字墨迹外框的左上角 Y 坐标。</param>
    /// <param name="value">待绘制的文字。</param>
    /// <param name="fontSize">主机字体字号，单位为打印点。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    /// <param name="bold">是否使用粗体。</param>
    /// <param name="underline">是否绘制下划线。</param>
    /// <param name="fontFamily">可选的系统字体名称或固定 Raster 字体别名。</param>
    public EscPosPrintCommand Text(int x, int y, string value, int fontSize, int rotation = 0,
        bool bold = false, bool underline = false, string fontFamily = null)
    {
        var canvas = RequireRasterPage("绝对坐标文本");
        ValidateText(value, nameof(value));
        ValidateRasterPoint(canvas, x, y);
        canvas.DrawText(x, y, value, fontSize, rotation, bold, underline, fontFamily);
        return this;
    }

    /// <summary>在 Raster 光栅页绘制反白文字；背景和文字样式会随文字整体旋转。</summary>
    public EscPosPrintCommand Text(int x, int y, string value, int fontSize, int rotation,
        bool bold, bool reverse, bool underline, string fontFamily = null)
    {
        var canvas = RequireRasterPage("绝对坐标文本");
        ValidateText(value, nameof(value));
        ValidateRasterPoint(canvas, x, y);
        canvas.DrawText(x, y, value, fontSize, rotation, bold, reverse, underline, fontFamily);
        return this;
    }

    /// <summary>在 Raster 光栅页绘制不旋转的反白文字。</summary>
    public EscPosPrintCommand Text(int x, int y, string value, int fontSize,
        bool bold, bool reverse, bool underline, string fontFamily = null)
        => Text(x, y, value, fontSize, 0, bold, reverse, underline, fontFamily);

    /// <summary>在 Raster 光栅页绘制主机编码的 Code 128 条码。</summary>
    /// <param name="x">旋转后条码外框左上角 X 坐标。</param>
    /// <param name="y">旋转后条码外框左上角 Y 坐标。</param>
    /// <param name="data">非空的 Code 128 内容。</param>
    /// <param name="height">条码编码区高度。</param>
    /// <param name="moduleWidth">一个条码模块的宽度。</param>
    /// <param name="humanReadable">是否绘制条码下方可读文字。</param>
    /// <param name="humanReadableReserveHeight">可读文字预留高度。</param>
    /// <param name="fontFamily">可选 Raster 字体名称或固定别名。</param>
    /// <param name="humanReadableFontSize">可读文字字号。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    public EscPosPrintCommand RasterBarcode(int x, int y, string data, int height, int moduleWidth = 2,
        bool humanReadable = false, int humanReadableReserveHeight = 0, string fontFamily = null,
        int humanReadableFontSize = EscPosRasterCanvas.DefaultHumanReadableFontSize, int rotation = 0)
    {
        var canvas = RequireRasterPage("绝对坐标条码");
        ValidateRasterPoint(canvas, x, y);
        canvas.DrawBarcode(BarcodeType.Code128, x, y, data, height, moduleWidth, humanReadable,
            humanReadableReserveHeight, fontFamily, humanReadableFontSize, rotation);
        return this;
    }

    /// <summary>在 Raster 光栅页绘制能力快照允许的主机条码。</summary>
    /// <param name="type">主机编码条码类型；具体集合以 <see cref="PrintCapabilities"/> 为准。</param>
    /// <param name="x">旋转后条码外框左上角 X 坐标。</param>
    /// <param name="y">旋转后条码外框左上角 Y 坐标。</param>
    /// <param name="data">已按目标条码语义规范化的数据。</param>
    /// <param name="height">条码编码区高度。</param>
    /// <param name="moduleWidth">一个条码模块的宽度。</param>
    /// <param name="humanReadable">是否绘制条码下方可读文字。</param>
    /// <param name="humanReadableReserveHeight">可读文字预留高度。</param>
    /// <param name="fontFamily">可选 Raster 字体名称或固定别名。</param>
    /// <param name="humanReadableFontSize">可读文字字号。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    public EscPosPrintCommand RasterBarcode(BarcodeType type, int x, int y, string data, int height,
        int moduleWidth = 2, bool humanReadable = false, int humanReadableReserveHeight = 0,
        string fontFamily = null, int humanReadableFontSize = EscPosRasterCanvas.DefaultHumanReadableFontSize,
        int rotation = 0)
    {
        var canvas = RequireRasterPage("绝对坐标条码");
        ValidateRasterPoint(canvas, x, y);
        if (type == BarcodeType.UccEan128)
            Gs1BarcodeCapacity.Validate(type, data, _profile?.MaxGs1LinearPayloadCharacters, nameof(data));
        else if (type == BarcodeType.Gs1DataBarExpanded)
            Gs1BarcodeCapacity.Validate(type, data,
                _profile?.MaxGs1DataBarExpandedPayloadCharacters, nameof(data));
        canvas.DrawBarcode(type, x, y, data, height, moduleWidth, humanReadable,
            humanReadableReserveHeight, fontFamily, humanReadableFontSize, rotation);
        return this;
    }

    /// <summary>在 Raster 光栅页绘制主机编码的二维码。</summary>
    /// <param name="x">旋转后二维码外框左上角 X 坐标。</param>
    /// <param name="y">旋转后二维码外框左上角 Y 坐标。</param>
    /// <param name="data">非空的二维码内容。</param>
    /// <param name="moduleSize">二维码模块尺寸。</param>
    /// <param name="errorLevel">二维码纠错级别。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    public EscPosPrintCommand RasterQrCode(int x, int y, string data, int moduleSize = 4,
        QrCodeCorrectionLevel errorLevel = QrCodeCorrectionLevel.M, int rotation = 0)
    {
        var canvas = RequireRasterPage("绝对坐标二维码");
        ValidateRasterPoint(canvas, x, y);
        canvas.DrawQrCode(x, y, data, moduleSize, errorLevel, rotation);
        return this;
    }

    public EscPosPrintCommand TextSize(int width, int height)
    {
        if (_profile?.SupportsTextSize == false)
            throw new NotSupportedException($"设备 {_profile.Name} 不支持 GS ! 文字倍数。");
        if (width < 1 || width > 8) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 1 || height > 8) throw new ArgumentOutOfRangeException(nameof(height));
        var result = WriteEncoded(new byte[] { 0x1D, 0x21, (byte)(((width - 1) << 4) | (height - 1)) });
        _restrictedTextWidthScale = width;
        return result;
    }

    public EscPosPrintCommand Feed(int lines = 1)
    {
        if (lines < 0 || lines > 255) throw new ArgumentOutOfRangeException(nameof(lines));
        var result = WriteEncoded(new byte[] { 0x1B, 0x64, (byte)lines });
        _restrictedLineWidthDots = 0;
        return result;
    }

    public EscPosPrintCommand Cut(bool partial = false)
    {
        return Cut(new PrintCutOptions { Mode = partial ? PrintCutMode.Partial : PrintCutMode.Full });
    }

    /// <summary>按显式机型裁切形状与切刀位置补偿生成 GS V；失败时不追加指令。</summary>
    public EscPosPrintCommand Cut(PrintCutOptions options)
        => WriteEncoded(EscPosCutEncoder.Encode(options, _profile));

    /// <summary>打印缓冲区并进纸指定纵向运动单位；对应 ESC J，不修改当前行距。</summary>
    public EscPosPrintCommand FeedMotionUnits(int units)
    {
        if (units < 0 || units > 255) throw new ArgumentOutOfRangeException(nameof(units));
        var result = WriteEncoded(new byte[] { 0x1B, 0x4A, (byte)units });
        _restrictedLineWidthDots = 0;
        return result;
    }

    public EscPosPrintCommand Line(int x0, int y0, int x1, int y1, int width = 1)
    {
        var canvas = RequireRasterPage("线段");
        ValidateRasterPoint(canvas, x0, y0);
        ValidateRasterPoint(canvas, x1, y1);
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        canvas.DrawLine(x0, y0, x1, y1, width);
        return this;
    }

    public EscPosPrintCommand Box(int x0, int y0, int x1, int y1, int width = 1)
    {
        var canvas = RequireRasterPage("矩形");
        ValidateRasterPoint(canvas, x0, y0);
        ValidateRasterPoint(canvas, x1, y1);
        if (x1 <= x0) throw new ArgumentOutOfRangeException(nameof(x1));
        if (y1 <= y0) throw new ArgumentOutOfRangeException(nameof(y1));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        canvas.DrawRectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1, width);
        return this;
    }

    public EscPosPrintCommand Barcode(string data, byte symbology = 73, byte height = 80, byte width = 2, byte textPosition = 0)
    {
        if (_profile?.SupportsNativeBarcode == false)
            throw new NotSupportedException($"设备 {_profile.Name} 不支持 GS k 原生条码。");
        var barcodeData = PrepareBarcodeData(data, symbology);
        return WriteBarcodeData(barcodeData, symbology, height, width, textPosition);
    }

    /// <summary>使用 ESC/POS Function B 75 输出 GS1 DataBar Omnidirectional。</summary>
    public EscPosPrintCommand BarcodeGs1DataBarOmnidirectional(string data, byte height = 80, byte width = 2,
        byte textPosition = 0)
        => Barcode(data, 75, height, width, textPosition);

    /// <summary>使用 ESC/POS Function B 76 输出 GS1 DataBar Truncated。</summary>
    public EscPosPrintCommand BarcodeGs1DataBarTruncated(string data, byte height = 80, byte width = 2,
        byte textPosition = 0)
        => Barcode(data, 76, height, width, textPosition);

    /// <summary>使用 ESC/POS Function B 77 输出 GS1 DataBar Limited。</summary>
    public EscPosPrintCommand BarcodeGs1DataBarLimited(string data, byte height = 80, byte width = 2,
        byte textPosition = 0)
        => Barcode(data, 77, height, width, textPosition);

    /// <summary>使用 ESC/POS Function B 78 输出 GS1 DataBar Expanded。</summary>
    public EscPosPrintCommand BarcodeGs1DataBarExpanded(string data, byte height = 80, byte width = 2,
        byte textPosition = 0)
        => Barcode(data, 78, height, width, textPosition);

    /// <summary>使用 ESC/POS Function B 79 输出 7-bit ASCII 的 Code 128 auto。</summary>
    /// <remarks>需要任意 8-bit 数据时使用 <see cref="BarcodeCode128Auto(byte[], byte, byte, byte)"/>。</remarks>
    public EscPosPrintCommand BarcodeCode128Auto(string data, byte height = 80, byte width = 2,
        byte textPosition = 0)
        => Barcode(data, 79, height, width, textPosition);

    /// <summary>使用 ESC/POS Function B 79 输出原始 Code 128 auto 字节。</summary>
    public EscPosPrintCommand BarcodeCode128Auto(byte[] data, byte height = 80, byte width = 2,
        byte textPosition = 0)
    {
        if (_profile?.SupportsNativeBarcode == false)
            throw new NotSupportedException($"设备 {_profile.Name} 不支持 GS k 原生条码。");
        if (data == null) throw new ArgumentNullException(nameof(data));
        ValidateLength(data, 1, 255, "CODE128 auto 条码长度必须为 1 到 255 字节。");
        using var operation = PrintResourceScope.BeginTemporary();
        var copy = BudgetedBuffer.Copy(data, "EscPos.BarcodeCode128Auto", System.Threading.CancellationToken.None);
        var result = WriteBarcodeData(copy, 79, height, width, textPosition);
        operation?.CommitReservations();
        return result;
    }

    private EscPosPrintCommand WriteBarcodeData(byte[] barcodeData, byte symbology, byte height, byte width,
        byte textPosition)
    {
        if (height == 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (width < 2 || width > 6) throw new ArgumentOutOfRangeException(nameof(width));
        if (textPosition > 3) throw new ArgumentOutOfRangeException(nameof(textPosition));
        if (textPosition != 0 && _profile != null)
        {
            var position = (EscPosBarcodeTextPosition)textPosition;
            if (!_profile.SupportsNativeBarcodeHumanReadablePosition(position))
                throw new NotSupportedException($"设备 {_profile.Name} 不支持 {position} 位置的条码可读文字。");
            var type = BarcodeTypeFromSymbology(symbology);
            if (type.HasValue && !_profile.SupportsNativeBarcodeHumanReadableType(type.Value))
                throw new NotSupportedException($"设备 {_profile.Name} 不支持 {type.Value} 条码的可读文字。");
            if (!type.HasValue && !_profile.SupportsNativeBarcodeHumanReadable)
                throw new NotSupportedException($"设备 {_profile.Name} 不支持条码可读文字。");
        }
        var result = AddWithStorage(w => { w.Write(new byte[] { 0x1D, 0x48, textPosition, 0x1D, 0x68, height, 0x1D, 0x77, width, 0x1D, 0x6B, symbology, (byte)barcodeData.Length }); w.Write(barcodeData); }, barcodeData.LongLength);
        _restrictedLineWidthDots = 0;
        return result;
    }

    private static BarcodeType? BarcodeTypeFromSymbology(byte symbology)
    {
        switch (symbology)
        {
            case 65: return BarcodeType.UpcA;
            case 66: return BarcodeType.UpcE;
            case 67: return BarcodeType.Ean13;
            case 68: return BarcodeType.Ean8;
            case 69: return BarcodeType.Code39;
            case 70: return BarcodeType.I2OF5;
            case 71: return BarcodeType.Codabar;
            case 72: return BarcodeType.Code93;
            case 73: return BarcodeType.Code128;
            case 74: return BarcodeType.UccEan128;
            case 75: return BarcodeType.Gs1DataBarOmnidirectional;
            case 76: return BarcodeType.Gs1DataBarTruncated;
            case 77: return BarcodeType.Gs1DataBarLimited;
            case 78: return BarcodeType.Gs1DataBarExpanded;
            case 79: return BarcodeType.Code128Auto;
            default: return null;
        }
    }

    /// <summary>使用 ESC/POS Function B 的 <c>GS k 74</c> 输出 GS1-128（UCC/EAN-128）。</summary>
    /// <remarks>
    /// 数据必须是 2–255 字节的 7-bit ASCII；AI、分隔符和可选校验标记按目标 Epson
    /// 固件约定传入。设备会补齐起始符、FNC1、校验和停止符，方法不会替调用方生成 GS1 业务数据。
    /// </remarks>
    public EscPosPrintCommand BarcodeUccEan128(string data, byte height = 80, byte width = 2,
        byte textPosition = 0)
        => Barcode(data, 74, height, width, textPosition);

    public EscPosPrintCommand QrCode(string data, byte moduleSize = 4, byte errorCorrection = 48)
    {
        if (_profile?.SupportsNativeQrCode == false)
            throw new NotSupportedException($"设备 {_profile.Name} 不支持 GS ( k 原生二维码。");
        ValidateField(data, nameof(data));
        if (data.Length == 0) throw new ArgumentException("二维码内容不能为空。", nameof(data));
        if (moduleSize < 1 || moduleSize > 16) throw new ArgumentOutOfRangeException(nameof(moduleSize));
        if (errorCorrection < 48 || errorCorrection > 51) throw new ArgumentOutOfRangeException(nameof(errorCorrection));
        using var operation = Printing.PrintResourceScope.BeginTemporary();
        var payload = GetEncodedBytes(data, nameof(data));
        var length = checked(payload.Length + 3);
        if (length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(data), "二维码数据超过 ESC/POS 长度字段范围。");
        var result = Add(w =>
        {
            w.Write(new byte[] { 0x1D, 0x28, 0x6B, 4, 0, 0x31, 0x41, 0x32, 0 });
            w.Write(new byte[] { 0x1D, 0x28, 0x6B, 3, 0, 0x31, 0x43, moduleSize });
            w.Write(new byte[] { 0x1D, 0x28, 0x6B, 3, 0, 0x31, 0x45, errorCorrection });
            w.Write(new byte[] { 0x1D, 0x28, 0x6B, (byte)(length & 255), (byte)(length >> 8), 0x31, 0x50, 0x30 }); w.Write(payload);
            w.Write(new byte[] { 0x1D, 0x28, 0x6B, 3, 0, 0x31, 0x51, 0x30 });
        });
        operation?.CommitReservations();
        _restrictedLineWidthDots = 0;
        return result;
    }

    /// <summary>写入 ESC/POS 光栅位图（每行按 MSB 优先排列）。</summary>
    public EscPosPrintCommand RasterImage(int width, int height, byte[] raster, bool doubleWidth = false, bool doubleHeight = false)
        => RasterImage(width, height, raster, System.Threading.CancellationToken.None, doubleWidth, doubleHeight);

    /// <summary>复制并记录可取消的位图；取消发生在追加指令之前。</summary>
    public EscPosPrintCommand RasterImage(int width, int height, byte[] raster,
        System.Threading.CancellationToken cancellationToken, bool doubleWidth = false, bool doubleHeight = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (LayoutMode != EscPosLayoutMode.Raster) throw new InvalidOperationException("RasterImage 需要 Raster 布局模式。");
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (raster == null) throw new ArgumentNullException(nameof(raster));

        var bytesPerRow = (width + 7L) / 8L;
        if (bytesPerRow > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "ESC/POS 光栅图每行的字节数不能超过 65535。");
        if (height > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(height), "ESC/POS 光栅图高度不能超过 65535。");

        var expectedLength = bytesPerRow * height;
        if (expectedLength > int.MaxValue || raster.LongLength != expectedLength)
            throw new ArgumentException("图片数据长度与尺寸不匹配。", nameof(raster));

        EscPosImageEncoder.Validate(width, height, _profile, doubleWidth, doubleHeight);
        using var operation = Printing.PrintResourceScope.BeginTemporary();
        var data = Bing.EasyPrint.Core.BudgetedBuffer.Copy(raster, "EscPos.ImageSnapshot", cancellationToken);
        var result = AddCancellable((w, token) => EscPosImageEncoder.Write(w, width, height, data, _profile,
            doubleWidth, doubleHeight, token));
        operation?.CommitReservations();
        return result;
    }

    public EscPosPrintCommand Image(int width, int height, byte[] raster, bool doubleWidth = false, bool doubleHeight = false)
        => RasterImage(width, height, raster, doubleWidth, doubleHeight);

    /// <summary>创建并输出一张由画布绘制的 ESC/POS 光栅页。</summary>
    public EscPosPrintCommand RasterPage(int width, int height, Action<EscPosRasterCanvas> draw,
        bool doubleWidth = false, bool doubleHeight = false)
        => RasterPage(width, height, draw, System.Threading.CancellationToken.None, doubleWidth, doubleHeight);

    /// <summary>绘制可取消的光栅页；取消时不追加该页指令。</summary>
    public EscPosPrintCommand RasterPage(int width, int height, Action<EscPosRasterCanvas> draw,
        System.Threading.CancellationToken cancellationToken, bool doubleWidth = false, bool doubleHeight = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (draw == null) throw new ArgumentNullException(nameof(draw));
        if (LayoutMode != EscPosLayoutMode.Raster)
            throw new InvalidOperationException("RasterPage 需要 Raster 布局模式。");
        if (_activeRasterPage != null)
            throw new InvalidOperationException("上一张光栅页尚未结束。");
        ValidateRasterPageDimensions(width, height, doubleWidth, doubleHeight);
        var canvas = new EscPosRasterCanvas(width, height, _rasterFonts, cancellationToken);
        draw(canvas);
        cancellationToken.ThrowIfCancellationRequested();
        return RasterImage(width, height, canvas.ToBytes(cancellationToken), cancellationToken, doubleWidth, doubleHeight);
    }

    /// <summary>开始一张可通过坐标文字、Line 和 Box 绘制的光栅页。</summary>
    public EscPosPrintCommand BeginRasterPage(int width, int height)
        => BeginRasterPage(width, height, System.Threading.CancellationToken.None);

    /// <summary>开始可取消的坐标光栅页。</summary>
    public EscPosPrintCommand BeginRasterPage(int width, int height, System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (LayoutMode != EscPosLayoutMode.Raster)
            throw new NotSupportedException("ESC/POS Native 模式不支持坐标光栅页。");
        if (_activeRasterPage != null)
            throw new InvalidOperationException("上一张光栅页尚未结束。");
        ValidateRasterPageDimensions(width, height, false, false);
        _activeRasterPage = new EscPosRasterCanvas(width, height, _rasterFonts, cancellationToken);
        return this;
    }

    private void ValidateRasterPageDimensions(int width, int height, bool doubleWidth, bool doubleHeight)
    {
        if (width <= 0 || ((long)width + 7) / 8 > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0 || height > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(height));
        var bytes = ((long)width + 7) / 8 * height;
        if (bytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(height), "光栅页缓冲区超出可表示范围。");
        EscPosImageEncoder.Validate(width, height, _profile, doubleWidth, doubleHeight);
    }

    /// <summary>将当前光栅页编码为一条 ESC/POS 光栅指令。</summary>
    public EscPosPrintCommand EndRasterPage()
        => EndRasterPage(System.Threading.CancellationToken.None);

    /// <summary>结束光栅页；取消时保留当前页，允许再次结束。</summary>
    public EscPosPrintCommand EndRasterPage(System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var canvas = RequireRasterPage("结束光栅页");
        _endingRasterPage = true;
        try { RasterImage(canvas.Width, canvas.Height, canvas.ToBytes(cancellationToken), cancellationToken); }
        finally { _endingRasterPage = false; }
        _activeRasterPage = null;
        return this;
    }

    protected override EscPosPrintCommand Add(Action<IBufferWriter> action)
    {
        if (_activeRasterPage != null && !_endingRasterPage)
            throw new InvalidOperationException("光栅页绘制期间不能插入原生或原始指令；请先调用 EndRasterPage。");
        return base.Add(action);
    }

    protected override EscPosPrintCommand AddCancellable(Action<IBufferWriter, System.Threading.CancellationToken> action)
    {
        if (_activeRasterPage != null && !_endingRasterPage)
            throw new InvalidOperationException("光栅页绘制期间不能插入原生或原始指令；请先调用 EndRasterPage。");
        return base.AddCancellable(action);
    }

    public override IBufferWriter Build()
        => Build(System.Threading.CancellationToken.None);

    /// <summary>可取消构建完整命令；未结束的光栅页仍拒绝构建。</summary>
    public override IBufferWriter Build(System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_activeRasterPage != null)
            throw new InvalidOperationException("光栅页尚未结束；请调用 EndRasterPage。");
        return base.Build(cancellationToken);
    }

    public EscPosPrintCommand Print() => Feed(1);

    private EscPosRasterCanvas RequireRasterPage(string operation)
    {
        if (LayoutMode != EscPosLayoutMode.Raster)
            throw new NotSupportedException($"ESC/POS Native 模式不支持{operation}；请使用 Raster 模式。");
        return _activeRasterPage ?? throw new InvalidOperationException($"{operation}需要先调用 BeginRasterPage。");
    }

    private static void ValidateRasterPoint(EscPosRasterCanvas canvas, int x, int y)
    {
        if (x < 0 || x >= canvas.Width) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= canvas.Height) throw new ArgumentOutOfRangeException(nameof(y));
    }

    private byte[] PrepareBarcodeData(string data, byte symbology)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (symbology < 65 || symbology > 79)
            throw new ArgumentOutOfRangeException(nameof(symbology), symbology, "ESC/POS 仅支持 Function B 条码制式 65 到 79。");

        if ((symbology == 74 || symbology == 78) &&
            Gs1ApplicationIdentifierParser.ShouldNormalize(data))
        {
            // Native firmware receives the same raw AI/FNC1 contract as Raster;
            // only the final protocol escape sequence remains provider-specific.
            data = Gs1ApplicationIdentifierParser.NormalizeForProtocol(data, nameof(data), 255);
        }
        if (symbology == 74)
            Gs1BarcodeCapacity.Validate(BarcodeType.UccEan128, data,
                _profile?.MaxGs1LinearPayloadCharacters, nameof(data));
        else if (symbology == 78)
            Gs1BarcodeCapacity.Validate(BarcodeType.Gs1DataBarExpanded, data,
                _profile?.MaxGs1DataBarExpandedPayloadCharacters, nameof(data));

        var ascii = ReadAscii(data);
        if (ascii.Length == 0)
            throw new ArgumentException("条码内容不能为空。", nameof(data));

        switch (symbology)
        {
            case 65:
                ValidateDigits(ascii, 11, 12, "UPC-A 条码必须包含 11 或 12 位数字。");
                break;
            case 66:
                ValidateDigits(ascii, 6, 7, 8, 11, 12);
                if ((ascii.Length == 7 || ascii.Length == 8 || ascii.Length == 11 || ascii.Length == 12) && ascii[0] != (byte)'0')
                    throw new ArgumentException("UPC-E 条码为 7、8、11 或 12 位时首位必须为数字 0。", nameof(data));
                break;
            case 67:
                ValidateDigits(ascii, 12, 13, "JAN13 条码必须包含 12 或 13 位数字。");
                break;
            case 68:
                ValidateDigits(ascii, 7, 8, "JAN8 条码必须包含 7 或 8 位数字。");
                break;
            case 69:
                ValidateVariable(ascii, 1, 255, IsCode39, "CODE39 条码包含不支持的字符或长度超出范围。");
                break;
            case 70:
                ValidateVariable(ascii, 2, 254, IsDigits, "ITF 条码必须包含 2 到 254 位偶数数字。");
                if ((ascii.Length & 1) != 0)
                    throw new ArgumentException("ITF 条码数据长度必须为偶数。", nameof(data));
                break;
            case 71:
                ValidateVariable(ascii, 2, 255, IsCodabar, "CODABAR 条码包含不支持的字符或长度超出范围。");
                if (!IsCodabarGuard(ascii[0]) || !IsCodabarGuard(ascii[ascii.Length - 1]))
                    throw new ArgumentException("CODABAR 条码首尾必须使用 A-D 或 a-d 起止字符。", nameof(data));
                break;
            case 72:
                ValidateVariable(ascii, 1, 255, static _ => true, "CODE93 条码长度超出范围。");
                break;
            case 73:
                ascii = PrepareCode128(ascii);
                ValidateLength(ascii, 2, 255, "CODE128 条码长度必须为 2 到 255 字节。");
                break;
            case 74:
                ValidateLength(ascii, 2, 255, "GS1-128 条码长度必须为 2 到 255 字节。");
                break;
            case 75:
                ValidateDigits(ascii, 13, "GS1 DataBar Omnidirectional 条码必须包含 13 位数字。");
                break;
            case 76:
                ValidateDigits(ascii, 13, "GS1 DataBar Truncated 条码必须包含 13 位数字。");
                break;
            case 77:
                ValidateDigits(ascii, 13, "GS1 DataBar Limited 条码必须包含 13 位数字。");
                if (ascii[0] != (byte)'0' && ascii[0] != (byte)'1')
                    throw new ArgumentException("GS1 DataBar Limited 条码首位必须为 0 或 1。", nameof(data));
                break;
            case 78:
                var expandedStartsWithParenthesis = data.Length > 0 && data[0] == '(';
                ascii = PrepareGs1DataBarExpanded(ascii);
                if (expandedStartsWithParenthesis)
                {
                    if (data.Length < 3 || data[1] < '0' || data[1] > '9' ||
                        data[2] < '0' || data[2] > '9')
                        throw new ArgumentException("GS1 DataBar Expanded 以括号开头时，第二和第三个字符必须为数字。", nameof(data));
                }
                else if (data[0] < '0' || data[0] > '9' || data.Length < 2 ||
                         data[1] < '0' || data[1] > '9')
                    throw new ArgumentException("GS1 DataBar Expanded 数据必须以两个数字或括号和两个数字开头。", nameof(data));
                break;
            case 79:
                ValidateLength(ascii, 1, 255, "CODE128 auto 条码长度必须为 1 到 255 字节。");
                break;
        }

        return ascii;
    }

    private static byte[] ReadAscii(string data)
    {
        var bytes = new byte[data.Length];
        for (var index = 0; index < data.Length; index++)
        {
            var value = data[index];
            if (value > 0x7F)
                throw new ArgumentException("ESC/POS Function B 条码数据必须是 7-bit ASCII。", nameof(data));
            bytes[index] = (byte)value;
        }
        return bytes;
    }

    private static byte[] PrepareCode128(byte[] data)
    {
        var hasStart = data.Length >= 2 && data[0] == (byte)'{' && IsCodeSet(data[1]);
        var startLength = hasStart ? 2 : 0;
        var codeSet = hasStart ? data[1] : (byte)'B';
        var shifted = false;
        var output = new System.Collections.Generic.List<byte>(data.Length + (hasStart ? 0 : 2));
        output.Add((byte)'{');
        output.Add(codeSet);

        for (var index = startLength; index < data.Length;)
        {
            var value = data[index];
            if (value == (byte)'{')
            {
                if (index + 1 >= data.Length)
                {
                    if (shifted) throw new ArgumentException("CODE128 SHIFT 后必须跟随一个有效的数据字符。", nameof(data));
                    output.Add((byte)'{');
                    output.Add((byte)'{');
                    index++;
                    continue;
                }

                var special = data[index + 1];
                if (special == (byte)'{')
                {
                    if (shifted && codeSet == (byte)'B')
                    {
                        output.Add((byte)'{');
                        output.Add((byte)'{');
                        index += 2;
                        shifted = false;
                        continue;
                    }

                    output.Add((byte)'{');
                    output.Add((byte)'{');
                    index += 2;
                    shifted = false;
                    continue;
                }

                if (IsCodeSet(special))
                {
                    if (shifted || special == codeSet)
                        throw new ArgumentException("CODE128 码集切换序列与当前码集状态不匹配。", nameof(data));
                    output.Add((byte)'{');
                    output.Add(special);
                    codeSet = special;
                    index += 2;
                    continue;
                }

                if (special >= (byte)'1' && special <= (byte)'4')
                {
                    if (shifted)
                        throw new ArgumentException("CODE128 SHIFT 后不能直接跟随功能字符。", nameof(data));
                    output.Add((byte)'{');
                    output.Add(special);
                    index += 2;
                    continue;
                }

                if (special == (byte)'S')
                {
                    if (shifted || codeSet == (byte)'C')
                        throw new ArgumentException("CODE128 SHIFT 只能从 Code Set A 或 B 使用。", nameof(data));
                    output.Add((byte)'{');
                    output.Add(special);
                    shifted = true;
                    index += 2;
                    continue;
                }

                if (shifted)
                    throw new ArgumentException("CODE128 SHIFT 后必须跟随一个有效的数据字符。", nameof(data));

                output.Add((byte)'{');
                output.Add((byte)'{');
                index++;
                continue;
            }

            var activeCodeSet = shifted
                ? (codeSet == (byte)'A' ? (byte)'B' : (byte)'A')
                : codeSet;
            if (activeCodeSet == (byte)'C')
            {
                if (!IsDigits(value) || index + 1 >= data.Length || !IsDigits(data[index + 1]))
                    throw new ArgumentException("CODE128 Code Set C 只能包含成对数字。", nameof(data));
                output.Add(value);
                output.Add(data[index + 1]);
                index += 2;
            }
            else
            {
                if (activeCodeSet == (byte)'A' && value > 95 ||
                    activeCodeSet == (byte)'B' && value < 32)
                    throw new ArgumentException("CODE128 数据字符不属于当前码集。", nameof(data));
                output.Add(value);
                index++;
            }

            shifted = false;
        }

        return output.ToArray();
    }

    private static bool IsCodeSet(byte value) => value == (byte)'A' || value == (byte)'B' || value == (byte)'C';

    private static void ValidateDigits(byte[] data, int firstLength, int secondLength, string message)
    {
        if ((data.Length != firstLength && data.Length != secondLength) || !IsDigits(data))
            throw new ArgumentException(message, nameof(data));
    }

    private static void ValidateDigits(byte[] data, int length, string message)
    {
        if (data.Length != length || !IsDigits(data))
            throw new ArgumentException(message, nameof(data));
    }

    private static void ValidateDigits(byte[] data, params int[] lengths)
    {
        var lengthMatches = false;
        foreach (var length in lengths)
            if (data.Length == length) { lengthMatches = true; break; }
        if (!lengthMatches || !IsDigits(data))
            throw new ArgumentException("条码必须使用规定长度的数字数据。", nameof(data));
    }

    private static void ValidateVariable(byte[] data, int minLength, int maxLength, Func<byte, bool> predicate, string message)
    {
        if (data.Length < minLength || data.Length > maxLength)
            throw new ArgumentException(message, nameof(data));
        for (var index = 0; index < data.Length; index++)
            if (!predicate(data[index]))
                throw new ArgumentException(message, nameof(data));
    }

    private static void ValidateLength(byte[] data, int minLength, int maxLength, string message)
    {
        if (data.Length < minLength || data.Length > maxLength)
            throw new ArgumentException(message, nameof(data));
    }

    private static bool IsDigits(byte value) => value >= (byte)'0' && value <= (byte)'9';

    private static bool IsDigits(byte[] data)
    {
        for (var index = 0; index < data.Length; index++)
            if (!IsDigits(data[index])) return false;
        return true;
    }

    private static bool IsCode39(byte value)
        => IsDigits(value) || value >= (byte)'A' && value <= (byte)'Z' || value == (byte)' ' ||
           value == (byte)'$' || value == (byte)'%' || value == (byte)'*' || value == (byte)'+' ||
           value == (byte)'-' || value == (byte)'.' || value == (byte)'/';

    private static bool IsCodabar(byte value)
        => IsDigits(value) || value >= (byte)'A' && value <= (byte)'D' || value >= (byte)'a' && value <= (byte)'d' ||
           value == (byte)'$' || value == (byte)'+' || value == (byte)'-' || value == (byte)'.' ||
           value == (byte)'/' || value == (byte)':';

    private static bool IsCodabarGuard(byte value)
        => value >= (byte)'A' && value <= (byte)'D' || value >= (byte)'a' && value <= (byte)'d';

    private static bool IsGs1DataBarExpanded(byte value)
        => IsDigits(value) || value >= (byte)'A' && value <= (byte)'D' ||
           value >= (byte)'a' && value <= (byte)'d' || value == (byte)' ' ||
           value == (byte)'!' || value == (byte)'"' || value == (byte)'%' ||
           value == (byte)'$' || value == (byte)'\'' || value == (byte)'(' ||
           value == (byte)')' || value == (byte)'*' || value == (byte)'+' ||
           value == (byte)',' || value == (byte)'-' || value == (byte)'.' ||
           value == (byte)'/' || value == (byte)':' || value == (byte)';' ||
           value == (byte)'<' || value == (byte)'=' || value == (byte)'>' ||
           value == (byte)'?' || value == (byte)'_' || value == (byte)'{';

    private static byte[] PrepareGs1DataBarExpanded(byte[] data)
    {
        if (data.Length < 2 || data.Length > 255)
            throw new ArgumentException("GS1 DataBar Expanded 条码长度必须为 2 到 255 个字符。", nameof(data));

        var output = new List<byte>(data.Length);
        for (var index = 0; index < data.Length; index++)
        {
            var value = data[index];
            if (value == 0x1D)
            {
                output.Add((byte)'{');
                output.Add((byte)'1');
                continue;
            }
            if (value == (byte)'(' || value == (byte)')')
            {
                output.Add((byte)'{');
                output.Add(value);
                continue;
            }
            if (!IsGs1DataBarExpanded(value))
                throw new ArgumentException("GS1 DataBar Expanded 条码包含不支持的字符；括号和 FNC1 由命令自动转义，裸 { 按 Epson 字符集保留。", nameof(data));
            output.Add(value);
        }

        if (output.Count > 255)
            throw new ArgumentException("GS1 DataBar Expanded 转义后长度不能超过 255 字节。", nameof(data));
        return output.ToArray();
    }

    private static void ValidateText(string value, string name,
        System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(value, name);
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (char.IsControl(value[index]))
                throw new ArgumentException("文本不能包含控制字符。", name);
        }
    }
}
