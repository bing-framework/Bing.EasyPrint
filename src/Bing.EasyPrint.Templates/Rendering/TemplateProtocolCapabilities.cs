using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Templates;

/// <summary>描述模板层可表达的协议选项；不替代文本、占位、资源及实机校验。</summary>
public sealed class TemplateProtocolCapabilities
{
    private readonly PrintCapabilities _provider;
    /// <summary>获取目标协议。</summary>
    public PrintProtocol Protocol => _provider.Protocol;
    /// <summary>获取 ESC/POS 布局模式；其他协议为 null。</summary>
    public EscPosLayoutMode? EscPosLayoutMode { get; }
    /// <summary>获取当前 Native 顺序布局方式；其他布局为 null。</summary>
    public EscPosNativeFlowMode? NativeFlow => _provider.NativeFlow;
    /// <summary>目标 Native 字库的文字契约副本；未声明或使用 Raster 时为 null。</summary>
    public Protocols.EscPosTextProfile NativeTextProfile => _provider.NativeTextProfile;
    /// <summary>获取不可变的固定 Raster 字体配置；未配置时为 null。</summary>
    public Protocols.RasterFontCatalog RasterFonts => _provider.RasterFonts;
    /// <summary>获取 ESC/POS Raster 条码可读文字的主机字号；其他布局或协议为 null。</summary>
    public int? RasterBarcodeHumanReadableFontSize => _provider.RasterBarcodeHumanReadableFontSize;
    /// <summary>标签机型配置名称；未声明时为 null。</summary>
    public string DeviceProfileName => _provider.DeviceProfileName;
    /// <summary>调用方记录的标签机型固件版本。</summary>
    public string DeviceFirmwareVersion => _provider.DeviceFirmwareVersion;
    /// <summary>声明的可打印宽度上限，单位为布局点。</summary>
    public int? MaxPrintableWidthDots => _provider.MaxPrintableWidthDots;
    /// <summary>声明的标签高度上限，单位为布局点。</summary>
    public int? MaxLabelHeightDots => _provider.MaxLabelHeightDots;
    /// <summary>声明的单条单色图片字节上限。</summary>
    public int? MaxImageDataBytes => _provider.MaxImageDataBytes;
    /// <summary>Profile 声明的 GS1-128 payload 字符上限；null 表示未收窄。</summary>
    public int? MaxGs1LinearPayloadCharacters => _provider.MaxGs1LinearPayloadCharacters;
    /// <summary>Profile 声明的 GS1 DataBar Expanded payload 字符上限；null 表示未收窄。</summary>
    public int? MaxGs1DataBarExpandedPayloadCharacters => _provider.MaxGs1DataBarExpandedPayloadCharacters;
    /// <summary>声明的标签设备公共字库区间。</summary>
    public IReadOnlyList<PrintCharacterRange> DeviceCharacterRanges => _provider.DeviceCharacterRanges;
    /// <summary>获取显式任务策略支持的进纸单位。</summary>
    public IReadOnlyList<PrintFeedUnit> JobFeedUnits => _provider.JobFeedUnits;
    /// <summary>显式裁切可请求的形状。</summary>
    public IReadOnlyList<PrintCutMode> CutModes => _provider.CutModes;
    /// <summary>设备进纸到切刀位置后的补偿单位。</summary>
    public IReadOnlyList<PrintFeedUnit> CutFeedUnits => _provider.CutFeedUnits;
    /// <summary>补偿量换算为运动单位后的范围。</summary>
    public PrintParameterRange CutFeedMotionUnits => _provider.CutFeedMotionUnits;
    /// <summary>TSPL 实测切刀位置以外可追加的打印点补偿范围。</summary>
    public PrintParameterRange CutFeedDots => _provider.CutFeedDots;
    /// <summary>TSPL 切刀契约独立副本。</summary>
    public Protocols.TsplCutterProfile TsplCutter => _provider.TsplCutter;
    /// <summary>ZPL 受控切刀契约独立副本。</summary>
    public Protocols.ZplCutterProfile ZplCutter => _provider.ZplCutter;
    /// <summary>当前配置可能需要独立控制文件。</summary>
    public bool SupportsIndependentOutputSegments => _provider.SupportsIndependentOutputSegments;
    /// <summary>获取水平布局 DPI。</summary>
    public int Dpi => _provider.Dpi;
    /// <summary>获取是否支持任意绝对坐标布局；Native 仍可按校准制表位输出文字列。</summary>
    public bool SupportsCoordinates => _provider.SupportsCoordinates;
    /// <summary>获取是否支持显式码图静区；空静区仍可用于保留原生命令原点。</summary>
    public bool SupportsExplicitQuietZone => _provider.SupportsExplicitQuietZone;
    /// <summary>获取支持的模板元素类型，包括逻辑组合类型 6。</summary>
    public IReadOnlyList<int> ElementTypes { get; }
    /// <summary>获取支持的语义字体来源。</summary>
    public IReadOnlyList<PrintFontKind> FontKinds { get; }
    /// <summary>获取设备字体及其字号范围；字体安装情况需按设备确认。</summary>
    public IReadOnlyList<TemplateDeviceFontCapability> DeviceFonts { get; }
    /// <summary>获取离散字号；null 表示使用整数范围。</summary>
    public IReadOnlyList<int> TextSizes { get; }
    /// <summary>获取默认字体的字号范围；存在离散字号时还需匹配 <see cref="TextSizes"/>。</summary>
    public PrintParameterRange TextSize { get; }
    /// <summary>获取模板文字接受的旋转角度。</summary>
    public IReadOnlyList<RotationAngle> TextRotations { get; }
    /// <summary>获取模板接受的条码制式。</summary>
    public IReadOnlyList<BarcodeType> BarcodeTypes { get; }
    /// <summary>获取模板条码接受的旋转角度。</summary>
    public IReadOnlyList<RotationAngle> BarcodeRotations { get; }
    /// <summary>获取模板二维码接受的旋转角度。</summary>
    public IReadOnlyList<RotationAngle> QrCodeRotations { get; }
    /// <summary>获取主机单色图片支持的顺时针旋转角度。</summary>
    public IReadOnlyList<RotationAngle> ImageRotations { get; }
    /// <summary>获取模板二维码的模块尺寸范围；不支持时为 null。</summary>
    public PrintParameterRange QrModuleSize { get; }
    /// <summary>获取目标 Provider 的条码窄条点宽范围；不支持时为 null。</summary>
    public PrintParameterRange BarcodeModuleWidth => _provider.BarcodeModuleWidth;
    /// <summary>获取目标 Provider 的条码条高范围。</summary>
    public PrintParameterRange BarcodeHeight => _provider.BarcodeHeight;
    /// <summary>获取模板是否支持显式窄条模块宽度。</summary>
    public bool SupportsExplicitBarcodeModuleWidth => _provider.SupportsBarcode && Protocol != PrintProtocol.Cpcl;
    /// <summary>获取模板是否支持独立宽条模块宽度。</summary>
    public bool SupportsWideModuleWidth => _provider.SupportsBarcode && Protocol == PrintProtocol.Tspl;
    /// <summary>获取是否支持粗体文字。</summary>
    public bool SupportsBoldText => _provider.SupportsBoldText;
    /// <summary>获取是否支持下划线文字。</summary>
    public bool SupportsUnderlineText => _provider.SupportsUnderlineText;
    /// <summary>获取是否支持反白文字。</summary>
    public bool SupportsReverseText => _provider.SupportsReverseText;
    /// <summary>获取是否支持非零角度文字的下划线。</summary>
    public bool SupportsRotatedUnderlineText => SupportsUnderlineText && Protocol != PrintProtocol.Cpcl && _provider.SupportsTextRotation;
    /// <summary>获取是否支持主机字体。</summary>
    public bool SupportsHostFontFamily => _provider.SupportsHostFontFamily;
    /// <summary>获取是否支持旧 DeviceFontName 字段；CPCL 的设备字体须使用语义字段。</summary>
    public bool SupportsLegacyDeviceFont => Protocol == PrintProtocol.Tspl || Protocol == PrintProtocol.Zpl;
    /// <summary>获取是否支持启用条码可读文字。</summary>
    public bool SupportsBarcodeHumanReadable => _provider.SupportsBarcodeHumanReadable;
    /// <summary>获取已确认支持原生 HRI 的条码制式；Raster 模式的主机 HRI 不受此列表限制。</summary>
    public IReadOnlyList<BarcodeType> SupportedNativeBarcodeHumanReadableTypes =>
        _provider.SupportedNativeBarcodeHumanReadableTypes;
    /// <summary>获取标签机型已确认支持的原生二维码非零旋转角度；未按角度收窄时为 null。</summary>
    public IReadOnlyList<RotationAngle> SupportedNativeQrCodeRotations => _provider.SupportedNativeQrCodeRotations;
    /// <summary>获取 ESC/POS Native 已确认支持的 HRI 位置；Raster 模式由主机绘制。</summary>
    public IReadOnlyList<Protocols.EscPosBarcodeTextPosition> SupportedNativeBarcodeHumanReadablePositions =>
        _provider.SupportedNativeBarcodeHumanReadablePositions;
    /// <summary>获取是否支持切纸。</summary>
    public bool SupportsCut => _provider.SupportsCut;
    /// <summary>获取是否支持按行额外进纸。</summary>
    public bool SupportsExtraFeed => _provider.SupportsExtraFeed;
    /// <summary>获取是否支持填充矩形。</summary>
    public bool SupportsFilledRectangles => _provider.SupportsFilledRectangles;

    private TemplateProtocolCapabilities(PrintCapabilities provider, EscPosLayoutMode layout)
    {
        _provider = provider;
        EscPosLayoutMode = Protocol == PrintProtocol.EscPos ? layout : (EscPosLayoutMode?)null;
        var native = Protocol == PrintProtocol.EscPos && layout == Bing.EasyPrint.EscPosLayoutMode.Native;
        ElementTypes = Array.AsReadOnly(Enumerable.Range(1, 10).Where(type =>
            (!native || type == 1 || type == 6 || type == 3 && provider.SupportsQrCode || type == 4 && provider.SupportsBarcode) &&
            (type == 6 || type == 1 && provider.SupportsText || type == 2 && provider.SupportsImages ||
             type == 3 && provider.SupportsQrCode || type == 4 && provider.SupportsBarcode ||
             (type == 5 || type == 7) && provider.SupportsGraphics ||
             type == 8 && provider.SupportsGraphics && provider.SupportsImages ||
             type == 9 && provider.SupportsGraphics ||
             type == 10 && provider.SupportsImages)).ToArray());
        FontKinds = provider.FontKinds;
        TextSizes = Protocol == PrintProtocol.Cpcl
            ? Array.AsReadOnly(Enum.GetValues(typeof(FontSize)).Cast<FontSize>().Select(size => (int)size).ToArray())
            : native ? Array.AsReadOnly(new[] { 24 }) : null;
        DeviceFonts = Array.AsReadOnly(provider.DeviceFontNames.Select(name => new TemplateDeviceFontCapability(name,
            provider.GetTextSizeRange(new PrintFont(PrintFontKind.Device, name)),
            Protocol == PrintProtocol.Cpcl ? Array.AsReadOnly(TextSizes.Where(size =>
                provider.SupportsFont(new PrintFont(PrintFontKind.Device, name), size)).ToArray()) : null)).ToArray());
        TextSize = provider.GetTextSizeRange(PrintFont.Default);
        TextRotations = Array.AsReadOnly(provider.SupportsTextRotation
            ? new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 }
            : new[] { RotationAngle.None });
        BarcodeTypes = Array.AsReadOnly(Enum.GetValues(typeof(BarcodeType)).Cast<BarcodeType>()
            .Where(provider.SupportsBarcodeType).ToArray());
        ImageRotations = Array.AsReadOnly(provider.SupportsImageRotation
            ? new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 }
            : new[] { RotationAngle.None });
        BarcodeRotations = Array.AsReadOnly(Protocol == PrintProtocol.Cpcl
            ? new[] { RotationAngle.None, RotationAngle.Rotate270 }
            : provider.SupportsBarcodeRotation
                ? new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 }
                : new[] { RotationAngle.None });
        var qrCodeAngles = new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 };
        QrCodeRotations = Array.AsReadOnly(!provider.SupportsQrCode
            ? new[] { RotationAngle.None }
            : qrCodeAngles.Where(provider.SupportsQrCodeRotationAngle).ToArray());
        QrModuleSize = provider.QrModuleSize == null ? null
            : new PrintParameterRange(1, provider.QrModuleSize.Maximum ?? 32);
    }

    /// <summary>从输出配置建立能力快照；不会读取模板、业务数据或图片资源。</summary>
    /// <remarks>机型能力与输出参数沿用实际 Provider 校验。后续修改配置不会改变此快照。</remarks>
    public static TemplateProtocolCapabilities Create(TemplateRenderOptions options = null)
    {
        options = options ?? new TemplateRenderOptions();
        options.Validate();
        // 查询不编码文本；未显式选编码时避免依赖调用方尚未注册的设备代码页。
        var queryEncoding = options.Encoding ??
            (options.Protocol == PrintProtocol.EscPos && options.EscPosDevice?.Profile?.Text != null &&
             options.EscPosLayout == Bing.EasyPrint.EscPosLayoutMode.Native
                ? Encoding.GetEncoding(options.EscPosDevice.Profile.Text.EncodingCodePage) : Encoding.ASCII);
        var labelEncodings = options.Protocol == PrintProtocol.Tspl ? options.TsplMedia?.Profile?.EncodingCodePages :
            options.Protocol == PrintProtocol.Zpl ? options.ZplMedia?.Profile?.EncodingCodePages : null;
        if (options.Encoding == null && labelEncodings != null && !labelEncodings.Contains(queryEncoding.CodePage))
            queryEncoding = Encoding.GetEncoding(labelEncodings[0]);
        var provider = PrintProtocolProviderFactory.Create(options.Protocol, queryEncoding, options.EscPosLayout,
            options.EscPosDevice, options.TsplMedia, options.ZplMedia, options.RequireDeclaredNativeBounds);
        if (options.Encoding != null && !provider.Capabilities.SupportsEncoding(options.Encoding))
            throw new NotSupportedException($"协议 {options.Protocol} 尚未映射编码 {options.Encoding.WebName}。");
        return new TemplateProtocolCapabilities(provider.Capabilities, options.EscPosLayout);
    }

    /// <summary>判断模板是否支持指定元素类型。</summary>
    public bool SupportsElementType(int type) => ElementTypes.Contains(type);
    /// <summary>判断模板字体和字号是否受支持；主机字体的字形仍在渲染时校验。</summary>
    public bool SupportsFont(PrintFont font, int size) => _provider.SupportsFont(font, size) &&
        (TextSizes == null || TextSizes.Contains(size));
    /// <summary>判断模板文字旋转角度是否受支持。</summary>
    public bool SupportsTextRotation(RotationAngle rotation) => TextRotations.Contains(rotation);
    /// <summary>判断模板条码旋转角度是否受支持。</summary>
    public bool SupportsBarcodeRotation(RotationAngle rotation) => BarcodeRotations.Contains(rotation);
    /// <summary>判断指定条码制式是否可启用可读文字。</summary>
    public bool SupportsBarcodeHumanReadableType(BarcodeType type) => _provider.SupportsBarcodeHumanReadableType(type);
    /// <summary>判断 ESC/POS Native 是否支持指定的 HRI 位置。</summary>
    public bool SupportsNativeBarcodeHumanReadablePosition(Protocols.EscPosBarcodeTextPosition position) =>
        _provider.SupportsNativeBarcodeHumanReadablePosition(position);
    /// <summary>判断模板二维码旋转角度是否受支持。</summary>
    public bool SupportsQrCodeRotation(RotationAngle rotation) => QrCodeRotations.Contains(rotation);
    /// <summary>判断当前模板输出是否支持指定图片角度；图片元素能力仍须单独检查。</summary>
    public bool SupportsImageRotation(RotationAngle rotation) => ImageRotations.Contains(rotation);
}

/// <summary>描述模板设备字体及其主机侧请求字号范围。</summary>
public sealed class TemplateDeviceFontCapability
{
    /// <summary>获取设备字体名称。</summary>
    public string Name { get; }
    /// <summary>获取请求字号范围；CPCL 还需匹配离散字号与字体的对应关系。</summary>
    public PrintParameterRange Size { get; }
    /// <summary>获取该字体的离散字号；null 表示仅使用范围。</summary>
    public IReadOnlyList<int> Sizes { get; }
    internal TemplateDeviceFontCapability(string name, PrintParameterRange size, IReadOnlyList<int> sizes)
    { Name = name; Size = size; Sizes = sizes; }
}
