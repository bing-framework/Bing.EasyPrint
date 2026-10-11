using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Protocols;

namespace Bing.EasyPrint;

/// <summary>描述一个打印协议能够表达的基础能力。</summary>
public sealed class PrintCapabilities
{
    private Protocols.LabelDeviceContract _labelProfile;
    private bool? _nativeBarcodeHumanReadableOverride;
    private IReadOnlyList<BarcodeType> _nativeBarcodeHumanReadableTypesOverride;
    private IReadOnlyList<Protocols.EscPosBarcodeTextPosition> _nativeBarcodeHumanReadablePositionsOverride;
    /// <summary>可选的标签机型配置名称。</summary>
    public string DeviceProfileName => _labelProfile?.Name;
    /// <summary>调用方记录的固件版本，不证明实机兼容。</summary>
    public string DeviceFirmwareVersion => _labelProfile?.FirmwareVersion;
    /// <summary>标签机型声明的页高上限；null 表示未声明。</summary>
    public int? MaxLabelHeightDots => _labelProfile?.Height;
    /// <summary>标签机型声明的单条单色图片数据上限。</summary>
    public int? MaxImageDataBytes => _labelProfile?.ImageBytes;
    /// <summary>设备 Profile 声明的 GS1-128 payload 字符上限；null 表示不按设备收窄。</summary>
    public int? MaxGs1LinearPayloadCharacters { get; internal set; }
    /// <summary>设备 Profile 声明的 GS1 DataBar Expanded payload 字符上限；null 表示不按设备收窄。</summary>
    public int? MaxGs1DataBarExpandedPayloadCharacters { get; internal set; }
    /// <summary>标签机型声明的公共字库覆盖区间；null 表示未声明。</summary>
    public IReadOnlyList<PrintCharacterRange> DeviceCharacterRanges => _labelProfile?.Characters;
    internal void ApplyLabelProfile(Protocols.LabelDeviceContract profile)
    {
        _labelProfile = profile;
        _nativeBarcodeHumanReadableOverride = null;
        _nativeBarcodeHumanReadableTypesOverride = null;
        _nativeBarcodeHumanReadablePositionsOverride = null;
        MaxPrintableWidthDots = profile?.Width;
        MaxGs1LinearPayloadCharacters = profile?.MaxGs1LinearPayloadCharacters;
        MaxGs1DataBarExpandedPayloadCharacters = profile?.MaxGs1DataBarExpandedPayloadCharacters;
    }
    /// <summary>当前 Raster 固定字体配置；为空时使用既有系统字体策略。配置及资源不可变。</summary>
    public Protocols.RasterFontCatalog RasterFonts { get; internal set; }
    /// <summary>获取 ESC/POS Raster 条码可读文字实际使用的主机字号；其他布局或协议为 null。</summary>
    public int? RasterBarcodeHumanReadableFontSize { get; internal set; }
    private Protocols.EscPosTextProfile _nativeTextProfile;
    /// <summary>目标机型的 Native 文字契约副本；未声明或使用主机光栅布局时为 null。</summary>
    public Protocols.EscPosTextProfile NativeTextProfile
    {
        get => _nativeTextProfile?.Clone();
        internal set => _nativeTextProfile = value?.Clone();
    }
    /// <summary>当前 ESC/POS Native 页面的顺序布局方式；其他布局为 null。</summary>
    public EscPosNativeFlowMode? NativeFlow { get; internal set; }
    /// <summary>显式任务策略可以编码的进纸单位；空集合表示不支持额外任务进纸。</summary>
    public IReadOnlyList<PrintFeedUnit> JobFeedUnits { get; internal set; } = Array.Empty<PrintFeedUnit>();
    /// <summary>获取协议类型。</summary>
    public PrintProtocol Protocol { get; }
    /// <summary>获取协议使用的默认 DPI。</summary>
    public int Dpi { get; }
    /// <summary>获取是否支持坐标布局。</summary>
    public bool SupportsCoordinates { get; }
    /// <summary>获取是否支持显式码图静区及坐标占位检查；Native 顺序布局不支持非零静区。</summary>
    public bool SupportsExplicitQuietZone => SupportsCoordinates;
    /// <summary>获取是否支持图片。</summary>
    public bool SupportsImages { get; }
    /// <summary>获取是否支持二维码。</summary>
    public bool SupportsQrCode { get; }
    /// <summary>获取是否支持旋转。</summary>
    public bool SupportsRotation { get; }
    /// <summary>获取是否支持裁切。</summary>
    public bool SupportsCut { get; }
    /// <summary>获取显式裁切选项可请求的形状；空列表不证明旧切纸入口的物理形状。</summary>
    public IReadOnlyList<PrintCutMode> CutModes { get; internal set; } = Array.AsReadOnly(Array.Empty<PrintCutMode>());
    /// <summary>获取进纸到切刀位置后的补偿单位；空列表表示此功能未声明。</summary>
    public IReadOnlyList<PrintFeedUnit> CutFeedUnits { get; internal set; } = Array.AsReadOnly(Array.Empty<PrintFeedUnit>());
    /// <summary>获取换算后的切前补偿运动单位范围。</summary>
    public PrintParameterRange CutFeedMotionUnits { get; internal set; }
    /// <summary>TSPL 实测切刀位置以外可追加的打印点补偿范围。</summary>
    public PrintParameterRange CutFeedDots { get; internal set; }
    private Protocols.TsplCutterProfile _tsplCutter;
    /// <summary>TSPL 切刀策略的独立副本；为空时保留旧无切刀配置行为。</summary>
    public Protocols.TsplCutterProfile TsplCutter
    {
        get => _tsplCutter?.Snapshot();
        internal set => _tsplCutter = value?.Snapshot();
    }
    private Protocols.ZplCutterProfile _zplCutter;
    /// <summary>ZPL 受控切刀契约的独立副本；为空时仅保留旧入口。</summary>
    public Protocols.ZplCutterProfile ZplCutter
    {
        get => _zplCutter?.Snapshot();
        internal set => _zplCutter = value?.Snapshot();
    }
    /// <summary>当前配置可能生成必须独立发送的控制文件。</summary>
    public bool SupportsIndependentOutputSegments => _zplCutter?.Strategy == Protocols.ZplCutStrategy.Delayed;
    /// <summary>获取是否支持文字。</summary>
    public bool SupportsText { get; }
    /// <summary>获取是否支持线段和矩形。</summary>
    public bool SupportsGraphics { get; }
    /// <summary>获取是否支持一维条码。</summary>
    public bool SupportsBarcode { get; }
    /// <summary>获取机型是否声明支持原生一维条码下方的可读文字（HRI）。</summary>
    /// <remarks>未绑定标签机型时沿用协议默认能力；具体条码制式仍可能有独立限制。</remarks>
    public bool SupportsNativeBarcodeHumanReadable => _labelProfile?.BarcodeHumanReadable ??
        _nativeBarcodeHumanReadableOverride ?? true;
    /// <summary>
    /// 获取已确认支持原生 HRI 的条码制式；null 表示未按制式收窄。
    /// ESC/POS Raster 的 HRI 由主机绘制，不受该列表限制。
    /// </summary>
    public IReadOnlyList<BarcodeType> SupportedNativeBarcodeHumanReadableTypes =>
        _labelProfile?.BarcodeHumanReadableTypes ?? _nativeBarcodeHumanReadableTypesOverride;
    /// <summary>获取标签机型声明的原生 HRI 最小横向占位宽度；null 表示只按条码主体估算。</summary>
    /// <remarks>宽度从条码字段原点向右计量，不代表固件字体宽度或居中 HRI 的左侧外伸。</remarks>
    public int? NativeBarcodeHumanReadableReserveWidthDots => _labelProfile?.BarcodeHumanReadableReserveWidthDots;
    /// <summary>获取标签机型声明的原生 HRI 页面 X 轴左侧外伸；null 等同于零。</summary>
    public int? NativeBarcodeHumanReadableLeftOverhangDots => _labelProfile?.BarcodeHumanReadableLeftOverhangDots;
    /// <summary>获取标签机型声明的原生 HRI 页面上侧外伸；null 等同于零。</summary>
    public int? NativeBarcodeHumanReadableTopOverhangDots => _labelProfile?.BarcodeHumanReadableTopOverhangDots;
    /// <summary>获取标签机型声明的原生 HRI 页面右侧外伸；null 等同于零。</summary>
    public int? NativeBarcodeHumanReadableRightOverhangDots => _labelProfile?.BarcodeHumanReadableRightOverhangDots;
    /// <summary>获取标签机型声明的原生 HRI 页面下侧外伸；null 等同于零。</summary>
    public int? NativeBarcodeHumanReadableBottomOverhangDots => _labelProfile?.BarcodeHumanReadableBottomOverhangDots;
    /// <summary>获取原生条码旋转 90 度时的 HRI 页面边距覆盖；null 表示沿用通用边距。</summary>
    public Protocols.NativeBarcodeHumanReadableOverhang? NativeBarcodeHumanReadableRotation90Overhang =>
        _labelProfile?.BarcodeHumanReadableRotation90Overhang;
    /// <summary>获取原生条码旋转 180 度时的 HRI 页面边距覆盖；null 表示沿用通用边距。</summary>
    public Protocols.NativeBarcodeHumanReadableOverhang? NativeBarcodeHumanReadableRotation180Overhang =>
        _labelProfile?.BarcodeHumanReadableRotation180Overhang;
    /// <summary>获取原生条码旋转 270 度时的 HRI 页面边距覆盖；null 表示沿用通用边距。</summary>
    public Protocols.NativeBarcodeHumanReadableOverhang? NativeBarcodeHumanReadableRotation270Overhang =>
        _labelProfile?.BarcodeHumanReadableRotation270Overhang;

    internal Protocols.NativeBarcodeHumanReadableOverhang ResolveNativeBarcodeHumanReadableOverhang(
        RotationAngle rotation)
        => _labelProfile?.ResolveBarcodeHumanReadableOverhang((int)rotation) ?? default;
    /// <summary>获取标签机型已确认支持的原生二维码非零旋转角度；null 表示未按角度收窄。</summary>
    public IReadOnlyList<RotationAngle> SupportedNativeQrCodeRotations => _labelProfile?.QrCodeRotations;
    /// <summary>
    /// 获取 ESC/POS Native 已确认支持的 HRI 位置；null 表示未按位置收窄。
    /// Raster HRI 由主机绘制，不使用此列表。
    /// </summary>
    public IReadOnlyList<Protocols.EscPosBarcodeTextPosition> SupportedNativeBarcodeHumanReadablePositions =>
        Protocol == PrintProtocol.EscPos && SupportsNativeLayout
            ? _nativeBarcodeHumanReadablePositionsOverride : null;
    internal bool SupportsNativeBarcodeHumanReadableOverride
    {
        set => _nativeBarcodeHumanReadableOverride = value;
    }
    internal IReadOnlyList<BarcodeType> SupportedNativeBarcodeHumanReadableTypesOverride
    {
        set => _nativeBarcodeHumanReadableTypesOverride = value;
    }
    internal IReadOnlyList<Protocols.EscPosBarcodeTextPosition> SupportedNativeBarcodeHumanReadablePositionsOverride
    {
        set => _nativeBarcodeHumanReadablePositionsOverride = value;
    }
    /// <summary>获取是否支持由独立协议命令直接编码一维条码。</summary>
    /// <remarks>
    /// 该属性与统一打印模型的 <see cref="SupportsBarcode"/> 分开表示。
    /// 例如 ESC/POS Raster 可以通过主机光栅化输出条码，但目标设备可能不支持
    /// GS k 原生条码命令。
    /// </remarks>
    public bool SupportsCommandBarcode { get; internal set; }
    /// <summary>获取是否支持由独立协议命令直接编码二维码。</summary>
    /// <remarks>
    /// 该属性与统一打印模型的 <see cref="SupportsQrCode"/> 分开表示。
    /// 例如 ESC/POS Raster 可以通过主机光栅化输出二维码，但目标设备可能不支持
    /// GS ( k 原生二维码命令。
    /// </remarks>
    public bool SupportsCommandQrCode { get; internal set; }
    /// <summary>获取是否支持原生行式布局。</summary>
    public bool SupportsNativeLayout { get; }
    /// <summary>获取是否支持光栅布局。</summary>
    public bool SupportsRasterLayout { get; }
    /// <summary>获取是否支持加粗文字。</summary>
    public bool SupportsBoldText { get; }
    /// <summary>获取是否支持下划线文字。</summary>
    public bool SupportsUnderlineText { get; }
    /// <summary>获取是否支持反白文字。</summary>
    /// <remarks>ESC/POS Native 使用 <c>GS B n</c>；不支持的布局必须在预检阶段拒绝。</remarks>
    public bool SupportsReverseText { get; }
    /// <summary>获取是否支持由主机字体族生成文字位图。</summary>
    public bool SupportsHostFontFamily { get; }
    /// <summary>获取光栅命令每行字节数的协议上限；null 表示未声明通用上限。</summary>
    public int? MaxRasterRowBytes { get; }
    /// <summary>获取光栅命令高度的协议上限；null 表示未声明通用上限。</summary>
    public int? MaxRasterHeight { get; }
    /// <summary>获取目标设备的可打印宽度（水平点数）；未配置机型时为 null。</summary>
    public int? MaxPrintableWidthDots { get; internal set; }
    /// <summary>获取 ESC/POS 图片的垂直分辨率；输入布局仍使用 <see cref="Dpi"/>。</summary>
    public int? RasterVerticalDpi { get; internal set; }
    /// <summary>获取 ESC/POS 实际使用的图片指令；其他协议为 null。</summary>
    public Protocols.EscPosImageCommand? RasterImageCommand { get; internal set; }
    /// <summary>获取单条图片指令的数据字节预算；未配置时为 null。</summary>
    public int? MaxImageBlockBytes { get; internal set; }
    /// <summary>获取统一打印模型是否支持旋转文字。</summary>
    public bool SupportsTextRotation => Protocol == PrintProtocol.EscPos
        ? SupportsRasterLayout
        : SupportsRotation;
    /// <summary>获取是否支持主机单色图片旋转；不依赖设备原生文字或条码旋转能力。</summary>
    public bool SupportsImageRotation => SupportsImages && SupportsCoordinates;
    /// <summary>获取协议命令是否支持旋转一维条码。</summary>
    public bool SupportsBarcodeRotation =>
        (Protocol == PrintProtocol.Cpcl ||
         Protocol == PrintProtocol.EscPos && SupportsRasterLayout ||
         (Protocol == PrintProtocol.Tspl || Protocol == PrintProtocol.Zpl) && SupportsRotation) && SupportsBarcode;
    /// <summary>获取协议命令是否支持统一模型二维码的任一非零旋转。</summary>
    public bool SupportsQrCodeRotation =>
        SupportsQrCodeRotationAngle(RotationAngle.Rotate90) ||
        SupportsQrCodeRotationAngle(RotationAngle.Rotate180) ||
        SupportsQrCodeRotationAngle(RotationAngle.Rotate270);

    /// <summary>判断统一模型二维码是否支持指定旋转角度；标签机型 profile 可以进一步收窄 TSPL/ZPL 原生角度。</summary>
    public bool SupportsQrCodeRotationAngle(RotationAngle rotation)
    {
        if (!System.Enum.IsDefined(typeof(RotationAngle), rotation)) return false;
        if (!SupportsQrCode) return false;
        if (rotation == RotationAngle.None) return true;
        if (Protocol == PrintProtocol.Cpcl) return true;
        if (Protocol == PrintProtocol.EscPos) return SupportsRasterLayout;
        if (Protocol != PrintProtocol.Tspl && Protocol != PrintProtocol.Zpl) return false;
        return SupportsRotation && (_labelProfile == null || _labelProfile.SupportsQrCodeRotation(rotation));
    }
    /// <summary>获取统一打印模型是否支持启用条码下方的可读文字。</summary>
    /// <remarks>
    /// CPCL 使用设备的 <c>BT</c> 条码文字命令；ESC/POS Native 使用原生 HRI，
    /// Raster 使用主机字体绘制。<c>null</c> 仍由各 Provider 保留既有默认行为，
    /// 此能力只表示显式开启可以被表达。
    /// </remarks>
    public bool SupportsBarcodeHumanReadable => SupportsBarcode &&
        (Protocol == PrintProtocol.EscPos && SupportsRasterLayout || SupportsNativeBarcodeHumanReadable) &&
        (Protocol == PrintProtocol.Cpcl || Protocol == PrintProtocol.Tspl || Protocol == PrintProtocol.Zpl ||
         Protocol == PrintProtocol.EscPos && (SupportsNativeLayout || SupportsRasterLayout));

    /// <summary>判断指定条码制式是否支持启用条码下方的可读文字。</summary>
    public bool SupportsBarcodeHumanReadableType(BarcodeType type)
    {
        if (!SupportsBarcodeType(type)) return false;
        if (Protocol == PrintProtocol.Zpl &&
            type >= BarcodeType.Gs1DataBarOmnidirectional && type <= BarcodeType.Gs1DataBarExpanded)
            return false;
        if (Protocol == PrintProtocol.EscPos && SupportsRasterLayout) return true;
        if (!SupportsBarcodeHumanReadable) return false;
        if (Protocol == PrintProtocol.EscPos && SupportsNativeLayout &&
            !SupportsNativeBarcodeHumanReadablePosition(Protocols.EscPosBarcodeTextPosition.Below)) return false;
        var supportedTypes = SupportedNativeBarcodeHumanReadableTypes;
        return supportedTypes == null || supportedTypes.Contains(type);
    }
    /// <summary>判断 ESC/POS Native 是否支持指定的 HRI 位置。</summary>
    public bool SupportsNativeBarcodeHumanReadablePosition(Protocols.EscPosBarcodeTextPosition position)
    {
        if (!Enum.IsDefined(typeof(Protocols.EscPosBarcodeTextPosition), position)) return false;
        if (position == Protocols.EscPosBarcodeTextPosition.None) return true;
        if (Protocol != PrintProtocol.EscPos || !SupportsNativeLayout) return false;
        if (!SupportsNativeBarcodeHumanReadable) return false;
        var supported = SupportedNativeBarcodeHumanReadablePositions;
        return supported == null || supported.Contains(position);
    }
    /// <summary>获取统一打印模型是否支持填充矩形。</summary>
    public bool SupportsFilledRectangles => SupportsGraphics;
    /// <summary>获取 Provider 是否支持单页多份输出。</summary>
    public bool SupportsPageCopies => true;
    /// <summary>获取 Provider 是否支持页级额外进纸。</summary>
    public bool SupportsExtraFeed => Protocol == PrintProtocol.EscPos;

    /// <summary>获取统一文本模型可以表达的字体来源；主机字体仍需检查运行时字形。</summary>
    public IReadOnlyList<PrintFontKind> FontKinds => Array.AsReadOnly(!SupportsText
        ? Array.Empty<PrintFontKind>()
        : _labelProfile?.Fonts != null && !DeviceFontNames.Contains("0")
            ? new[] { PrintFontKind.Device }
        : SupportsHostFontFamily ? new[] { PrintFontKind.Default, PrintFontKind.Host }
        : Protocol == PrintProtocol.Cpcl || Protocol == PrintProtocol.Tspl || Protocol == PrintProtocol.Zpl
            ? new[] { PrintFontKind.Default, PrintFontKind.Device } : new[] { PrintFontKind.Default });

    /// <summary>获取统一文本模型接受的设备字体标识；不证明目标固件包含该字体。</summary>
    public IReadOnlyList<string> DeviceFontNames => _labelProfile?.Fonts ?? Array.AsReadOnly(!SupportsText ? Array.Empty<string>()
        : Protocol == PrintProtocol.Cpcl ? new[] { "24", "55" }
        : Protocol == PrintProtocol.Tspl ? Enumerable.Range(0, 9).Select(value => value.ToString()).ToArray()
        : Protocol == PrintProtocol.Zpl ? "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".Select(value => value.ToString()).ToArray()
        : Array.Empty<string>());

    /// <summary>获取统一模型的二维码模块尺寸范围；模板层可能进一步限制上限。</summary>
    public PrintParameterRange QrModuleSize => !SupportsQrCode ? null : new PrintParameterRange(1,
        Protocol == PrintProtocol.Tspl || Protocol == PrintProtocol.Zpl ? 10
        : Protocol == PrintProtocol.EscPos && !SupportsRasterLayout ? 16 : (int?)null);

    /// <summary>获取统一模型的一维条码窄条点宽范围。</summary>
    public PrintParameterRange BarcodeModuleWidth => !SupportsBarcode ? null :
        Protocol == PrintProtocol.EscPos && !SupportsRasterLayout ? new PrintParameterRange(2, 6)
        : new PrintParameterRange(1, Protocol == PrintProtocol.Zpl ? 10 : (int?)null);

    /// <summary>获取统一模型的一维条码条高范围。</summary>
    public PrintParameterRange BarcodeHeight => !SupportsBarcode ? null : new PrintParameterRange(1,
        Protocol == PrintProtocol.EscPos && !SupportsRasterLayout ? 255 : (int?)null);

    /// <summary>获取独立命令原生二维码的模块范围；与主机 Raster 模型的范围分开。</summary>
    public PrintParameterRange CommandQrModuleSize => !SupportsCommandQrCode ? null
        : Protocol == PrintProtocol.EscPos ? new PrintParameterRange(1, 16) : QrModuleSize;
    /// <summary>获取独立命令原生条码的窄条点宽范围。</summary>
    public PrintParameterRange CommandBarcodeModuleWidth => !SupportsCommandBarcode ? null
        : Protocol == PrintProtocol.EscPos ? new PrintParameterRange(2, 6) : BarcodeModuleWidth;
    /// <summary>获取独立命令原生条码的条高范围。</summary>
    public PrintParameterRange CommandBarcodeHeight => !SupportsCommandBarcode ? null
        : Protocol == PrintProtocol.EscPos ? new PrintParameterRange(1, 255) : BarcodeHeight;

    /// <summary>获取统一模型指定字体可接受的字号参数范围；离散字号另由 <see cref="SupportsFont"/> 检查。</summary>
    /// <remarks>固定 TSPL 字体按现有整数倍率取整规则限制请求高度，实际字高可能与请求值不同。</remarks>
    public PrintParameterRange GetTextSizeRange(PrintFont font)
    {
        if (_labelProfile?.Fonts != null && font?.Kind == PrintFontKind.Default && !DeviceFontNames.Contains("0")) return null;
        if (font == null || !FontKinds.Contains(font.Kind) ||
            font.Kind == PrintFontKind.Device && !DeviceFontNames.Contains(font.Name)) return null;
        if (font.Kind == PrintFontKind.Host && RasterFonts != null && !RasterFonts.SupportsSelection(font.Name, false))
            return null;
        if (Protocol == PrintProtocol.EscPos && !SupportsRasterLayout) return new PrintParameterRange(24, 24);
        if (Protocol == PrintProtocol.Tspl)
        {
            if (font.Kind == PrintFontKind.Device && font.Name != "0")
                return new PrintParameterRange(1, (21 * TsplFontSizing.FixedCell(font.Name).Height - 1) / 2);
            // 字体 0 转换到点数后四舍五入，必须不超过 TEXT 的 256 点参数上限。
            return new PrintParameterRange(1, (int)Math.Min(int.MaxValue, (513L * Dpi - 1) / 144));
        }
        return new PrintParameterRange(1);
    }

    /// <summary>判断统一模型的字体选择和字号能否由当前 Provider 表达；不检查文本字形或页面占位。</summary>
    public bool SupportsFont(PrintFont font, int size)
    {
        var range = GetTextSizeRange(font);
        if (range == null || !range.Contains(size)) return false;
        if (Protocol == PrintProtocol.Cpcl && font.Kind == PrintFontKind.Device)
            return Enum.IsDefined(typeof(FontSize), size) && font.Name == (size % 24 == 0 ? "24" : "55");
        return true;
    }

    /// <summary>初始化能力描述。</summary>
    public PrintCapabilities(PrintProtocol protocol, int dpi, bool supportsCoordinates,
        bool supportsImages, bool supportsQrCode, bool supportsRotation, bool supportsCut,
        bool supportsText = true, bool supportsGraphics = true, bool supportsBarcode = true,
        bool supportsNativeLayout = true, bool supportsRasterLayout = true,
        bool supportsBoldText = true, bool supportsUnderlineText = true,
        bool supportsHostFontFamily = false, int? maxRasterRowBytes = null, int? maxRasterHeight = null,
        bool supportsReverseText = false)
    {
        if (dpi <= 0) throw new System.ArgumentOutOfRangeException(nameof(dpi));
        if (maxRasterRowBytes.HasValue && maxRasterRowBytes.Value <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(maxRasterRowBytes));
        if (maxRasterHeight.HasValue && maxRasterHeight.Value <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(maxRasterHeight));
        Protocol = protocol;
        Dpi = dpi;
        SupportsCoordinates = supportsCoordinates;
        SupportsImages = supportsImages;
        SupportsQrCode = supportsQrCode;
        SupportsRotation = supportsRotation;
        SupportsCut = supportsCut;
        SupportsText = supportsText;
        SupportsGraphics = supportsGraphics;
        SupportsBarcode = supportsBarcode;
        SupportsCommandBarcode = supportsBarcode;
        SupportsCommandQrCode = supportsQrCode;
        SupportsNativeLayout = supportsNativeLayout;
        SupportsRasterLayout = supportsRasterLayout;
        SupportsBoldText = supportsBoldText;
        SupportsUnderlineText = supportsUnderlineText;
        SupportsReverseText = supportsReverseText;
        SupportsHostFontFamily = supportsHostFontFamily;
        MaxRasterRowBytes = maxRasterRowBytes;
        MaxRasterHeight = maxRasterHeight;
    }

    /// <summary>判断统一打印模型能否使用指定的一维条码制式。</summary>
    public bool SupportsBarcodeType(BarcodeType type)
    {
        if (!SupportsBarcode || !System.Enum.IsDefined(typeof(BarcodeType), type)) return false;
        switch (Protocol)
        {
            case PrintProtocol.Cpcl:
                return type == BarcodeType.Code128 || type == BarcodeType.Code39 ||
                    type == BarcodeType.Code93 || type == BarcodeType.Codabar ||
                    type == BarcodeType.Ean8 || type == BarcodeType.Ean13 ||
                    type == BarcodeType.UpcA || type == BarcodeType.UpcE ||
                    type == BarcodeType.I2OF5 || type == BarcodeType.I2OF5C ||
                    type == BarcodeType.I2OF5G || type == BarcodeType.UccEan128 ||
                    type == BarcodeType.Msi || type == BarcodeType.Postnet ||
                    type == BarcodeType.Fim;
            case PrintProtocol.Tspl:
                return type == BarcodeType.Code128 || type == BarcodeType.Code39 ||
                    type == BarcodeType.Code93 ||
                    type == BarcodeType.Ean8 || type == BarcodeType.Ean13 ||
                    type == BarcodeType.UpcA || type == BarcodeType.UpcE ||
                    type == BarcodeType.Codabar || type == BarcodeType.I2OF5 ||
                    type == BarcodeType.I2OF5C || type == BarcodeType.I2OF5G || type == BarcodeType.Msi ||
                    type == BarcodeType.UccEan128 || type == BarcodeType.Postnet ||
                    type == BarcodeType.Planet || type == BarcodeType.Plessey || type == BarcodeType.Itf14 ||
                    type == BarcodeType.Ean14 || type == BarcodeType.Code11 || type == BarcodeType.ChinaPost ||
                    type == BarcodeType.Telepen || type == BarcodeType.Telepenn || type == BarcodeType.Code49;
            case PrintProtocol.EscPos:
                // Raster 由主机编码 Code128、Code39、Code93、EAN/UPC、Codabar、ITF、MSI、POSTNET、PLANET、China Post；
                // GS1 DataBar Expanded 当前声明 GTIN-14 (01) 加标准通用字段字符集子集，或
                // 显式 FNC1 分隔的 Method 2 原始 GS1 通用字段，
                // 以及显式 (3103) 公斤重量 Method 3、(3202)/(3203) 英制重量 Method 4、
                // (392x) 本地价格 Method 5、(393x) ISO 4217 货币+价格 Method 6，
                // 以及 (310x)/(320x) 重量+虚拟日期 Method 7/8、生产/包装/最佳食用/有效期日期 Method 9–14 的主机编码；
                // GS1 DataBar Stacked 由主机将 96 模块线性符号分成两行并生成分隔行；
                // I2OF5C/I2OF5G 先由主机补齐或验证校验位，再复用 ITF。
                return (SupportsRasterLayout && Protocols.EscPosRasterCanvas.SupportsBarcodeType(type)) ||
                    (SupportsNativeLayout && (type == BarcodeType.Code128 ||
                     type == BarcodeType.Code39 || type == BarcodeType.Code93 ||
                     type == BarcodeType.Ean8 || type == BarcodeType.Ean13 ||
                     type == BarcodeType.UpcA || type == BarcodeType.UpcE ||
                     type == BarcodeType.Codabar || type == BarcodeType.I2OF5 ||
                     type == BarcodeType.I2OF5C || type == BarcodeType.I2OF5G ||
                     type == BarcodeType.UccEan128 ||
                     type == BarcodeType.Gs1DataBarOmnidirectional ||
                     type == BarcodeType.Gs1DataBarTruncated || type == BarcodeType.Gs1DataBarLimited ||
                     type == BarcodeType.Gs1DataBarExpanded || type == BarcodeType.Code128Auto));
            case PrintProtocol.Zpl:
                return type == BarcodeType.Code128 || type == BarcodeType.Code39 ||
                    type == BarcodeType.Code93 || type == BarcodeType.Ean8 ||
                    type == BarcodeType.Ean13 || type == BarcodeType.UpcA ||
                    type == BarcodeType.UpcE || type == BarcodeType.Codabar ||
                    type == BarcodeType.I2OF5 || type == BarcodeType.I2OF5C || type == BarcodeType.I2OF5G || type == BarcodeType.Msi ||
                    type == BarcodeType.UccEan128 || type == BarcodeType.Postnet || type == BarcodeType.Planet ||
                    type == BarcodeType.IntelligentMail || type == BarcodeType.Code11 ||
                    type == BarcodeType.Plessey || type == BarcodeType.Code49 ||
                    type == BarcodeType.Gs1DataBarOmnidirectional ||
                    type == BarcodeType.Gs1DataBarTruncated ||
                    type == BarcodeType.Gs1DataBarLimited ||
                    type == BarcodeType.Gs1DataBarExpanded;
            default: return false;
        }
    }

    /// <summary>判断独立协议命令是否支持指定的一维条码制式。</summary>
    /// <remarks>此方法描述命令编码能力；统一打印模型应使用 <see cref="SupportsBarcodeType"/> 查询。</remarks>
    public bool SupportsCommandBarcodeType(BarcodeType type)
    {
        if (!SupportsCommandBarcode || !System.Enum.IsDefined(typeof(BarcodeType), type)) return false;
        if (Protocol == PrintProtocol.EscPos)
        {
            if (SupportsRasterLayout && (type == BarcodeType.I2OF5C || type == BarcodeType.I2OF5G ||
                                          type == BarcodeType.Msi || type == BarcodeType.ChinaPost ||
                                          type == BarcodeType.Gs1DataBarStacked))
                return true;
            return type == BarcodeType.Code128 || type == BarcodeType.Code39 ||
                 type == BarcodeType.Code93 || type == BarcodeType.Codabar ||
                 type == BarcodeType.Ean8 || type == BarcodeType.Ean13 ||
                 type == BarcodeType.UpcA || type == BarcodeType.UpcE ||
                 type == BarcodeType.I2OF5 || type == BarcodeType.UccEan128 ||
                 type == BarcodeType.Gs1DataBarOmnidirectional ||
                 type == BarcodeType.Gs1DataBarTruncated || type == BarcodeType.Gs1DataBarLimited ||
                 type == BarcodeType.Gs1DataBarExpanded || type == BarcodeType.Code128Auto;
        }
        return SupportsBarcodeType(type);
    }

    /// <summary>判断指定 .NET 编码是否有明确的协议字符集表达。</summary>
    /// <remarks>
    /// ESC/POS 未绑定 Native 文字契约时只检查主机字节布局；绑定后还匹配声明的编码代码页。
    /// 设备索引和实际字库覆盖仍需由机型配置、文字预检及实机验收确认。
    /// </remarks>
    public bool SupportsEncoding(Encoding encoding)
    {
        if (encoding == null) return false;
        if (_labelProfile?.Encodings != null && !_labelProfile.Encodings.Contains(encoding.CodePage)) return false;
        switch (Protocol)
        {
            case PrintProtocol.Cpcl:
                return true;
            case PrintProtocol.EscPos:
                return IsEscPosEncodingCompatible(encoding) &&
                    (_nativeTextProfile == null || encoding.CodePage == _nativeTextProfile.EncodingCodePage);
            case PrintProtocol.Tspl:
                return encoding.CodePage == Encoding.UTF8.CodePage ||
                    encoding.CodePage == Encoding.ASCII.CodePage || encoding.CodePage == 936;
            case PrintProtocol.Zpl:
                return encoding.CodePage == Encoding.UTF8.CodePage || encoding.CodePage == Encoding.ASCII.CodePage;
            default:
                return false;
        }
    }

    internal static bool IsEscPosEncodingCompatible(Encoding encoding)
    {
        if (encoding == null) return false;
        var bytes = encoding.GetBytes("A\r\n");
        return bytes.Length == 3 && bytes[0] == (byte)'A' &&
            bytes[1] == 0x0D && bytes[2] == 0x0A;
    }
}
