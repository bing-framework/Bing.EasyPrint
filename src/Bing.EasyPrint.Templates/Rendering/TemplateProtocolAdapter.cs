using System;
using System.Collections.Generic;
using System.Linq;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 新协议在模板层的布局适配契约；真正决定文字测量、分页约束和可支持元素范围。
/// </summary>
/// <remarks>
/// 适配器实例由宿主启动阶段创建并冻结；每次模板渲染通过冻结的 PrintProtocolRegistry 创建独立后端会话。
/// </remarks>
public interface ITemplateProtocolAdapter
{
    /// <summary>获取稳定的新协议标识，须与核心注册表标识一致。</summary>
    string ProtocolId { get; }

    /// <summary>获取不可变的模板布局能力快照。</summary>
    TemplateCustomProtocolCapabilities Capabilities { get; }

    /// <summary>按本次文字样式创建测量器；分页、换行和占位必须使用该测量结果。</summary>
    /// <param name="request">文字内容、字号、样式及字体选择。</param>
    /// <returns>本次文字布局使用的测量器。</returns>
    ITemplateTextMetrics CreateTextMetrics(TemplateTextMeasurementRequest request);

    /// <summary>测量一维码的布局占位；支持声明由能力快照决定。</summary>
    /// <param name="request">条码元素、绑定数据和可用宽度。</param>
    /// <returns>目标协议实际需要的占位。</returns>
    TemplateBarcodeMeasurement MeasureBarcode(TemplateBarcodeMeasurementRequest request);

    /// <summary>把一维码转换为本次渲染的目标元素。</summary>
    /// <param name="request">条码元素、绑定数据和最终布局坐标。</param>
    /// <returns>交给后端会话的协议无关打印元素。</returns>
    PrintElement ConvertBarcode(TemplateBarcodeConversionRequest request);

    /// <summary>测量二维码的布局占位；支持声明由能力快照决定。</summary>
    /// <param name="request">二维码元素、绑定数据和可用宽度。</param>
    /// <returns>目标协议实际需要的占位。</returns>
    TemplateQrCodeMeasurement MeasureQrCode(TemplateQrCodeMeasurementRequest request);

    /// <summary>把二维码转换为本次渲染的目标元素。</summary>
    /// <param name="request">二维码元素、绑定数据和最终布局坐标。</param>
    /// <returns>交给后端会话的协议无关打印元素。</returns>
    PrintElement ConvertQrCode(TemplateQrCodeConversionRequest request);
}

/// <summary>一维码布局测量请求。</summary>
public sealed class TemplateBarcodeMeasurementRequest
{
    /// <summary>获取条码模板元素。</summary>
    public BpBarcodeElement Element { get; }
    /// <summary>获取已绑定的条码数据。</summary>
    public string Data { get; }
    /// <summary>获取单元格可用宽度。</summary>
    public int AvailableWidth { get; }

    /// <summary>初始化一维码布局测量请求。</summary>
    public TemplateBarcodeMeasurementRequest(BpBarcodeElement element, string data, int availableWidth)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        if (availableWidth <= 0) throw new ArgumentOutOfRangeException(nameof(availableWidth));
        AvailableWidth = availableWidth;
    }
}

/// <summary>一维码布局测量结果。</summary>
public readonly struct TemplateBarcodeMeasurement
{
    /// <summary>获取一维码占位宽度。</summary>
    public int Width { get; }
    /// <summary>获取一维码占位高度。</summary>
    public int Height { get; }

    /// <summary>初始化一维码布局测量结果。</summary>
    public TemplateBarcodeMeasurement(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
    }
}

/// <summary>一维码转换请求。</summary>
public sealed class TemplateBarcodeConversionRequest
{
    /// <summary>获取条码模板元素。</summary>
    public BpBarcodeElement Element { get; }
    /// <summary>获取已绑定的条码数据。</summary>
    public string Data { get; }
    /// <summary>获取最终 X 坐标。</summary>
    public int X { get; }
    /// <summary>获取最终 Y 坐标。</summary>
    public int Y { get; }
    /// <summary>获取占用宽度。</summary>
    public int Width { get; }
    /// <summary>获取占用高度。</summary>
    public int Height { get; }

    /// <summary>初始化一维码转换请求。</summary>
    public TemplateBarcodeConversionRequest(BpBarcodeElement element, string data, int x, int y,
        int width, int height)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

/// <summary>二维码布局测量请求。</summary>
public sealed class TemplateQrCodeMeasurementRequest
{
    /// <summary>获取二维码模板元素。</summary>
    public BpQrCodeElement Element { get; }
    /// <summary>获取已绑定的二维码数据。</summary>
    public string Data { get; }
    /// <summary>获取单元格可用宽度。</summary>
    public int AvailableWidth { get; }

    /// <summary>初始化二维码布局测量请求。</summary>
    public TemplateQrCodeMeasurementRequest(BpQrCodeElement element, string data, int availableWidth)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        if (availableWidth <= 0) throw new ArgumentOutOfRangeException(nameof(availableWidth));
        AvailableWidth = availableWidth;
    }
}

/// <summary>二维码布局测量结果。</summary>
public readonly struct TemplateQrCodeMeasurement
{
    /// <summary>获取二维码占位宽度。</summary>
    public int Width { get; }
    /// <summary>获取二维码占位高度。</summary>
    public int Height { get; }

    /// <summary>初始化二维码布局测量结果。</summary>
    public TemplateQrCodeMeasurement(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
    }
}

/// <summary>二维码转换请求。</summary>
public sealed class TemplateQrCodeConversionRequest
{
    /// <summary>获取二维码模板元素。</summary>
    public BpQrCodeElement Element { get; }
    /// <summary>获取已绑定的二维码数据。</summary>
    public string Data { get; }
    /// <summary>获取最终 X 坐标。</summary>
    public int X { get; }
    /// <summary>获取最终 Y 坐标。</summary>
    public int Y { get; }
    /// <summary>获取占用宽度。</summary>
    public int Width { get; }
    /// <summary>获取占用高度。</summary>
    public int Height { get; }

    /// <summary>初始化二维码转换请求。</summary>
    public TemplateQrCodeConversionRequest(BpQrCodeElement element, string data, int x, int y,
        int width, int height)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

/// <summary>新协议模板适配器声明的不可变能力快照。</summary>
public sealed class TemplateCustomProtocolCapabilities
{
    private readonly int[] _elementTypes;
    private readonly int[] _textSizes;
    private readonly RotationAngle[] _textRotations;
    private readonly int[] _softwareBitmapElementTypes;
    private readonly int[] _fontKinds;
    private readonly BarcodeType[] _barcodeTypes;
    private readonly RotationAngle[] _barcodeRotations;
    private readonly RotationAngle[] _qrCodeRotations;

    /// <summary>获取布局 DPI。</summary>
    public int Dpi { get; }

    /// <summary>获取支持的模板元素类型编号副本。</summary>
    public IReadOnlyList<int> ElementTypes => Array.AsReadOnly((int[])_elementTypes.Clone());

    /// <summary>获取支持的离散字号副本；空数组表示任意正整数字号。</summary>
    public IReadOnlyList<int> TextSizes => Array.AsReadOnly((int[])_textSizes.Clone());

    /// <summary>获取支持的文字旋转角度副本。</summary>
    public IReadOnlyList<RotationAngle> TextRotations => Array.AsReadOnly((RotationAngle[])_textRotations.Clone());

    /// <summary>获取是否允许模板使用主机字体族。</summary>
    public bool SupportsHostFontFamily { get; }

    /// <summary>获取支持的图片旋转角度副本。</summary>
    public IReadOnlyList<RotationAngle> ImageRotations { get; }

    /// <summary>获取声明的最大页面宽度；null 表示未额外收窄。</summary>
    public int? MaxPageWidthDots { get; }

    /// <summary>获取声明的最大页面高度；null 表示沿用模板 MaxPageHeight。</summary>
    public int? MaxPageHeightDots { get; }

    /// <summary>获取是否支持 Host 软件位图预览。</summary>
    public bool SupportsSoftwareBitmap { get; }

    /// <summary>获取可生成软件位图的元素类型副本；声明支持软件位图时不能为空。</summary>
    public IReadOnlyList<int> SoftwareBitmapElementTypes => Array.AsReadOnly((int[])_softwareBitmapElementTypes.Clone());

    /// <summary>获取不支持软件位图时的明确原因。</summary>
    public string SoftwareBitmapReason { get; }

    /// <summary>获取是否支持图片元素。</summary>
    public bool SupportsImages { get; }
    /// <summary>获取是否支持一维码元素。</summary>
    public bool SupportsBarcodes { get; }
    /// <summary>获取是否支持二维码元素。</summary>
    public bool SupportsQrCode { get; }
    /// <summary>获取是否支持 GS1 QR（FNC1）编码。</summary>
    public bool SupportsGs1QrCode { get; }
    /// <summary>获取支持的字体来源类型。</summary>
    public IReadOnlyList<int> FontKinds => Array.AsReadOnly((int[])_fontKinds.Clone());
    /// <summary>获取支持的最大文字字号。</summary>
    public int MaxTextSize { get; }
    /// <summary>获取是否支持显式静区。</summary>
    public bool SupportsExplicitQuietZone { get; }
    /// <summary>获取是否支持 GS1 Composite 元素。</summary>
    public bool SupportsComposite { get; }
    /// <summary>获取支持的一维码制式副本。</summary>
    public IReadOnlyList<BarcodeType> BarcodeTypes => Array.AsReadOnly((BarcodeType[])_barcodeTypes.Clone());
    /// <summary>获取支持的一维码旋转角度副本。</summary>
    public IReadOnlyList<RotationAngle> BarcodeRotations => Array.AsReadOnly((RotationAngle[])_barcodeRotations.Clone());
    /// <summary>获取支持的二维码旋转角度副本。</summary>
    public IReadOnlyList<RotationAngle> QrCodeRotations => Array.AsReadOnly((RotationAngle[])_qrCodeRotations.Clone());
    /// <summary>获取是否支持坐标布局。</summary>
    public bool SupportsCoordinates { get; }
    /// <summary>获取是否支持填充矩形。</summary>
    public bool SupportsFilledRectangles { get; }
    /// <summary>获取是否支持粗体文字。</summary>
    public bool SupportsBoldText { get; }
    /// <summary>获取是否支持下划线文字。</summary>
    public bool SupportsUnderlineText { get; }
    /// <summary>获取是否支持反白文字。</summary>
    public bool SupportsReverseText { get; }
    /// <summary>获取是否支持额外进纸。</summary>
    public bool SupportsExtraFeed { get; }
    /// <summary>获取是否支持旧设备字体标识。</summary>
    public bool SupportsLegacyDeviceFont { get; }
    /// <summary>获取是否支持条码可读文字。</summary>
    public bool SupportsBarcodeHumanReadable { get; }
    /// <summary>获取是否支持显式窄模块宽度。</summary>
    public bool SupportsExplicitBarcodeModuleWidth { get; }
    /// <summary>获取是否支持显式宽模块宽度。</summary>
    public bool SupportsWideModuleWidth { get; }
/// <summary>获取是否声明任务收尾能力。</summary>
    public bool SupportsFinishing { get; }

    /// <summary>
    /// 初始化一个不可变的新协议模板能力快照。
    /// </summary>
    /// <param name="dpi">布局 DPI，必须为正数。</param>
    /// <param name="elementTypes">支持的模板元素类型编号；不能为 null 或包含重复/无效值。</param>
    /// <param name="textSizes">支持的离散字号；空数组表示任意正整数字号。</param>
    /// <param name="textRotations">支持的文字旋转角度；不能为 null 或包含未定义值。</param>
    /// <param name="supportsHostFontFamily">是否允许主机字体族。</param>
    /// <param name="imageRotations">支持的图片旋转角度；不能为 null。</param>
    /// <param name="maxPageWidthDots">可选最大页面宽度。</param>
    /// <param name="maxPageHeightDots">可选最大页面高度。</param>
    /// <param name="supportsSoftwareBitmap">是否支持软件位图预览。</param>
    /// <param name="softwareBitmapElementTypes">可生成软件位图的元素类型。</param>
    /// <param name="softwareBitmapReason">不支持软件位图时的原因。</param>
    /// <param name="supportsFinishing">是否声明任务收尾能力。</param>
    /// <param name="supportsImages">是否支持图片元素。</param>
    /// <param name="supportsBarcodes">是否支持一维码元素。</param>
    /// <param name="barcodeTypes">支持的一维码制式。</param>
    /// <param name="barcodeRotations">支持的一维码旋转角度。</param>
    /// <param name="supportsQrCode">是否支持二维码元素。</param>
    /// <param name="supportsGs1QrCode">是否支持 GS1 QR（FNC1）编码。</param>
    /// <param name="qrCodeRotations">支持的二维码旋转角度。</param>
    /// <param name="supportsComposite">是否支持 GS1 Composite。</param>
    /// <param name="supportsCoordinates">是否支持坐标布局。</param>
    /// <param name="supportsFilledRectangles">是否支持填充矩形。</param>
    /// <param name="supportsBoldText">是否支持粗体文字。</param>
    /// <param name="supportsUnderlineText">是否支持下划线文字。</param>
    /// <param name="supportsReverseText">是否支持反白文字。</param>
    /// <param name="supportsExtraFeed">是否支持额外进纸。</param>
    /// <param name="supportsLegacyDeviceFont">是否支持旧设备字体。</param>
    /// <param name="supportsBarcodeHumanReadable">是否支持条码可读文字。</param>
    /// <param name="supportsExplicitBarcodeModuleWidth">是否支持显式窄模块宽度。</param>
    /// <param name="supportsWideModuleWidth">是否支持显式宽模块宽度。</param>
    /// <param name="fontKinds">支持的字体来源类型。</param>
    /// <param name="maxTextSize">支持的最大文字字号。</param>
    /// <param name="supportsExplicitQuietZone">是否支持显式静区。</param>
    public TemplateCustomProtocolCapabilities(int dpi, IEnumerable<int> elementTypes,
        IEnumerable<int> textSizes = null, IEnumerable<RotationAngle> textRotations = null,
        bool supportsHostFontFamily = false, IEnumerable<RotationAngle> imageRotations = null,
        int? maxPageWidthDots = null, int? maxPageHeightDots = null, bool supportsSoftwareBitmap = false,
        IEnumerable<int> softwareBitmapElementTypes = null, string softwareBitmapReason = null,
        bool supportsFinishing = false, bool supportsImages = false, bool supportsBarcodes = false,
        IEnumerable<BarcodeType> barcodeTypes = null, IEnumerable<RotationAngle> barcodeRotations = null,
        bool supportsQrCode = false, bool supportsGs1QrCode = false, IEnumerable<RotationAngle> qrCodeRotations = null,
        bool supportsComposite = false, bool supportsCoordinates = true, bool supportsFilledRectangles = false,
        bool supportsBoldText = false, bool supportsUnderlineText = false, bool supportsReverseText = false,
        bool supportsExtraFeed = false, bool supportsLegacyDeviceFont = false,
        bool supportsBarcodeHumanReadable = false, bool supportsExplicitBarcodeModuleWidth = false,
        bool supportsWideModuleWidth = false, IEnumerable<int> fontKinds = null, int maxTextSize = 512,
        bool supportsExplicitQuietZone = false)
    {
        if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        if (elementTypes == null) throw new ArgumentNullException(nameof(elementTypes));
        _elementTypes = elementTypes.Distinct().OrderBy(value => value).ToArray();
        if (_elementTypes.Length == 0 || _elementTypes.Any(value => value < 1 || value > 11))
            throw new ArgumentException("元素类型必须为 1–11 的非空集合。", nameof(elementTypes));
        _textSizes = (textSizes ?? Array.Empty<int>()).Distinct().OrderBy(value => value).ToArray();
        if (_textSizes.Any(value => value <= 0))
            throw new ArgumentOutOfRangeException(nameof(textSizes), "字号必须为正整数。");
        _textRotations = (textRotations ?? new[] { RotationAngle.None }).Distinct().ToArray();
        if (_textRotations.Any(value => !Enum.IsDefined(typeof(RotationAngle), value)))
            throw new ArgumentOutOfRangeException(nameof(textRotations));
        ImageRotations = Array.AsReadOnly((imageRotations ?? new[] { RotationAngle.None }).Distinct().ToArray());
        if (ImageRotations.Any(value => !Enum.IsDefined(typeof(RotationAngle), value)))
            throw new ArgumentOutOfRangeException(nameof(imageRotations));
        if (maxPageWidthDots <= 0) throw new ArgumentOutOfRangeException(nameof(maxPageWidthDots));
        if (maxPageHeightDots <= 0) throw new ArgumentOutOfRangeException(nameof(maxPageHeightDots));
        MaxPageWidthDots = maxPageWidthDots;
        MaxPageHeightDots = maxPageHeightDots;
        Dpi = dpi;
        SupportsHostFontFamily = supportsHostFontFamily;
        SupportsSoftwareBitmap = supportsSoftwareBitmap;
        _softwareBitmapElementTypes = (softwareBitmapElementTypes ?? Array.Empty<int>()).Distinct().ToArray();
        if (supportsSoftwareBitmap && _softwareBitmapElementTypes.Length == 0)
            throw new ArgumentException("声明软件位图时必须列出可生成位图的元素类型。", nameof(softwareBitmapElementTypes));
        SoftwareBitmapReason = softwareBitmapReason;
        SupportsFinishing = supportsFinishing;
        SupportsImages = supportsImages;
        SupportsBarcodes = supportsBarcodes;
        SupportsQrCode = supportsQrCode;
        SupportsGs1QrCode = supportsGs1QrCode;
        SupportsComposite = supportsComposite;
        SupportsCoordinates = supportsCoordinates;
        SupportsFilledRectangles = supportsFilledRectangles;
        SupportsBoldText = supportsBoldText;
        SupportsUnderlineText = supportsUnderlineText;
        SupportsReverseText = supportsReverseText;
        SupportsExtraFeed = supportsExtraFeed;
        SupportsLegacyDeviceFont = supportsLegacyDeviceFont;
        SupportsBarcodeHumanReadable = supportsBarcodeHumanReadable;
        SupportsExplicitBarcodeModuleWidth = supportsExplicitBarcodeModuleWidth;
        SupportsWideModuleWidth = supportsWideModuleWidth;
        _fontKinds = (fontKinds ?? new[] { 0 }).Distinct().OrderBy(value => value).ToArray();
        if (_fontKinds.Any(value => value < 0 || value > 2))
            throw new ArgumentOutOfRangeException(nameof(fontKinds));
        if (maxTextSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxTextSize));
        MaxTextSize = maxTextSize;
        SupportsExplicitQuietZone = supportsExplicitQuietZone;
        _barcodeTypes = (barcodeTypes ?? Array.Empty<BarcodeType>()).Distinct().ToArray();
        _barcodeRotations = (barcodeRotations ?? new[] { RotationAngle.None }).Distinct().ToArray();
        _qrCodeRotations = (qrCodeRotations ?? new[] { RotationAngle.None }).Distinct().ToArray();
    }

    /// <summary>由同一协议能力快照投影模板层能力，避免核心和模板分别手工维护同一事实。</summary>
    /// <param name="capabilities">核心注册表使用的不可变协议能力快照。</param>
    /// <returns>包含相同 DPI、元素、字体来源、字号、码制、收尾和位图事实的模板能力快照。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> 为 null 时抛出。</exception>
    public static TemplateCustomProtocolCapabilities FromCore(PrintProtocolCapabilities capabilities)
    {
        if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));
        return new TemplateCustomProtocolCapabilities(capabilities.Dpi, capabilities.ElementTypes,
            capabilities.TextSizes, capabilities.TextRotations, capabilities.SupportsHostFontFamily,
            capabilities.ImageRotations, capabilities.MaxPageWidthDots, capabilities.MaxPageHeightDots,
            capabilities.SupportsSoftwareBitmap, capabilities.SoftwareBitmapElementTypes,
            capabilities.SoftwareBitmapReason, capabilities.SupportsFinishing, capabilities.SupportsImages,
            capabilities.SupportsBarcodes, capabilities.BarcodeTypes, capabilities.BarcodeRotations,
            capabilities.SupportsQrCode, capabilities.SupportsGs1QrCode, capabilities.QrCodeRotations,
            capabilities.SupportsComposite, capabilities.SupportsCoordinates,
            capabilities.SupportsFilledRectangles, capabilities.SupportsBoldText,
            capabilities.SupportsUnderlineText, capabilities.SupportsReverseText, capabilities.SupportsExtraFeed,
            capabilities.SupportsLegacyDeviceFont, capabilities.SupportsBarcodeHumanReadable,
            capabilities.SupportsExplicitBarcodeModuleWidth, capabilities.SupportsWideModuleWidth,
            capabilities.FontKinds, capabilities.MaxTextSize, capabilities.SupportsExplicitQuietZone);
    }

    /// <summary>判断模板元素类型是否受支持。</summary>
    public bool SupportsElementType(int type) => Array.IndexOf(_elementTypes, type) >= 0;

    /// <summary>判断指定字号是否受支持；空离散集表示在最大字号内任意正整数。</summary>
    public bool SupportsTextSize(int size) => size > 0 && size <= MaxTextSize &&
        (_textSizes.Length == 0 || Array.IndexOf(_textSizes, size) >= 0);

    /// <summary>判断是否声明指定字体来源类型。</summary>
    public bool SupportsFontKind(PrintFontKind kind) => Array.IndexOf(_fontKinds, (int)kind) >= 0;

    /// <summary>判断指定文字旋转角度是否受支持。</summary>
    public bool SupportsTextRotation(RotationAngle rotation) => Array.IndexOf(_textRotations, rotation) >= 0;

    /// <summary>判断指定图片旋转角度是否受支持。</summary>
    public bool SupportsImageRotation(RotationAngle rotation) => ImageRotations.Contains(rotation);

    /// <summary>判断指定元素类型是否可生成 Host 软件位图。</summary>
    public bool SupportsSoftwareBitmapElementType(int type) =>
        SupportsSoftwareBitmap && Array.IndexOf(_softwareBitmapElementTypes, type) >= 0;
    /// <summary>判断是否支持指定一维码制式。</summary>
    public bool SupportsBarcodeType(BarcodeType type) => Array.IndexOf(_barcodeTypes, type) >= 0;
    /// <summary>判断是否支持指定一维码旋转角度。</summary>
    public bool SupportsBarcodeRotation(RotationAngle rotation) => Array.IndexOf(_barcodeRotations, rotation) >= 0;
    /// <summary>判断是否支持指定二维码旋转角度。</summary>
    public bool SupportsQrCodeRotation(RotationAngle rotation) => Array.IndexOf(_qrCodeRotations, rotation) >= 0;
}

/// <summary>新协议文字测量请求。</summary>
public sealed class TemplateTextMeasurementRequest
{
    /// <summary>获取待测量文字。</summary>
    public string Text { get; }
    /// <summary>获取字号。</summary>
    public int FontSize { get; }
    /// <summary>获取是否粗体。</summary>
    public bool Bold { get; }
    /// <summary>获取是否下划线。</summary>
    public bool Underline { get; }
    /// <summary>获取是否反白。</summary>
    public bool Reverse { get; }
    /// <summary>获取文字旋转角度。</summary>
    public RotationAngle Rotation { get; }
    /// <summary>获取可选主机字体族。</summary>
    public string FontFamily { get; }
    /// <summary>获取原始语义字体选择；旧字段模板可能为 null。</summary>
    public PrintFont Font { get; }

    /// <summary>初始化一个文字测量请求。</summary>
    public TemplateTextMeasurementRequest(string text, int fontSize, bool bold, bool underline, bool reverse,
        RotationAngle rotation, string fontFamily, PrintFont font)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (!Enum.IsDefined(typeof(RotationAngle), rotation)) throw new ArgumentOutOfRangeException(nameof(rotation));
        Text = text;
        FontSize = fontSize;
        Bold = bold;
        Underline = underline;
        Reverse = reverse;
        Rotation = rotation;
        FontFamily = fontFamily;
        Font = font;
    }
}

/// <summary>新协议文字测量器；测量结果必须与实际后端绘制和断行一致。</summary>
public interface ITemplateTextMetrics
{
    /// <summary>获取单行逻辑行高。</summary>
    int LineHeight { get; }
    /// <summary>获取已解析的主机字体族；无显式字体时可为 null。</summary>
    string FontFamily { get; }
    /// <summary>获取是否提供逐字符设备字库校准边界；新协议通常为 false。</summary>
    bool HasDeviceTextElementCalibrations { get; }
    /// <summary>获取是否需要完整光栅测量；新协议通常为 false。</summary>
    bool NeedsFullMeasure { get; }
    /// <summary>测量单个文本元素宽度。</summary>
    int Advance(string textElement);
    /// <summary>测量整段文字宽度。</summary>
    long Measure(string text);
    /// <summary>测量设备校准文字边界；无设备校准时返回逻辑行边界。</summary>
    TemplateTextBounds MeasureBounds(string text);
}

/// <summary>文字测量边界；坐标为相对文字原点。</summary>
public readonly struct TemplateTextBounds
{
    /// <summary>获取左边界。</summary>
    public int Left { get; }
    /// <summary>获取上边界。</summary>
    public int Top { get; }
    /// <summary>获取右边界。</summary>
    public int Right { get; }
    /// <summary>获取下边界。</summary>
    public int Bottom { get; }
    /// <summary>获取宽度。</summary>
    public int Width => checked(Right - Left);
    /// <summary>获取高度。</summary>
    public int Height => checked(Bottom - Top);
    /// <summary>初始化文字测量边界。</summary>
    public TemplateTextBounds(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}
