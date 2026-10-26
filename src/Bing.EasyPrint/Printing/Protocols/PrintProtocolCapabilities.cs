using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.EasyPrint.Printing;

/// <summary>
/// 描述新注册协议的协议中立能力快照；注册表、模板适配器和 Host 目录共享同一对象。
/// </summary>
/// <remarks>
/// 能力集合在构造时复制并按稳定顺序排序。未声明能力必须由调用方明确拒绝，不能借用内置协议默认值。
/// </remarks>
public sealed class PrintProtocolCapabilities
{
    private readonly int[] _elementTypes;
    private readonly int[] _textSizes;
    private readonly int[] _fontKinds;
    private readonly RotationAngle[] _textRotations;
    private readonly RotationAngle[] _imageRotations;
    private readonly BarcodeType[] _barcodeTypes;
    private readonly RotationAngle[] _barcodeRotations;
    private readonly RotationAngle[] _qrCodeRotations;
    private readonly int[] _softwareBitmapElementTypes;

    /// <summary>获取布局 DPI。</summary>
    public int Dpi { get; }
    /// <summary>获取支持的模板元素类型副本。</summary>
    public IReadOnlyList<int> ElementTypes => Array.AsReadOnly((int[])_elementTypes.Clone());
    /// <summary>获取支持的离散字号副本；空数组表示在最大字号内任意正整数字号。</summary>
    public IReadOnlyList<int> TextSizes => Array.AsReadOnly((int[])_textSizes.Clone());
    /// <summary>获取支持的最大文字字号。</summary>
    public int MaxTextSize { get; private set; } = 512;
    /// <summary>获取是否支持显式静区。</summary>
    public bool SupportsExplicitQuietZone { get; private set; }
    /// <summary>获取支持的字体来源类型副本。</summary>
    public IReadOnlyList<int> FontKinds => Array.AsReadOnly((int[])_fontKinds.Clone());
    /// <summary>获取支持的文字旋转角度副本。</summary>
    public IReadOnlyList<RotationAngle> TextRotations => Array.AsReadOnly((RotationAngle[])_textRotations.Clone());
    /// <summary>获取支持的图片旋转角度副本。</summary>
    public IReadOnlyList<RotationAngle> ImageRotations => Array.AsReadOnly((RotationAngle[])_imageRotations.Clone());
    /// <summary>获取支持的一维码制式副本。</summary>
    public IReadOnlyList<BarcodeType> BarcodeTypes => Array.AsReadOnly((BarcodeType[])_barcodeTypes.Clone());
    /// <summary>获取支持的一维码旋转角度副本。</summary>
    public IReadOnlyList<RotationAngle> BarcodeRotations => Array.AsReadOnly((RotationAngle[])_barcodeRotations.Clone());
    /// <summary>获取是否支持 GS1 QR（FNC1）编码。</summary>
    public bool SupportsGs1QrCode { get; }
    /// <summary>获取支持的二维码旋转角度副本。</summary>
    public IReadOnlyList<RotationAngle> QrCodeRotations => Array.AsReadOnly((RotationAngle[])_qrCodeRotations.Clone());
    /// <summary>获取可生成 Host 软件位图的元素类型副本。</summary>
    public IReadOnlyList<int> SoftwareBitmapElementTypes => Array.AsReadOnly((int[])_softwareBitmapElementTypes.Clone());

    /// <summary>获取可选的最大页面宽度；null 表示未额外收窄。</summary>
    public int? MaxPageWidthDots { get; }
    /// <summary>获取可选的最大页面高度；null 表示沿用模板或渲染选项上限。</summary>
    public int? MaxPageHeightDots { get; }
    /// <summary>获取是否支持图片元素。</summary>
    public bool SupportsImages { get; }
    /// <summary>获取是否支持一维码元素。</summary>
    public bool SupportsBarcodes { get; }
    /// <summary>获取是否支持二维码元素。</summary>
    public bool SupportsQrCode { get; }
    /// <summary>获取是否支持 GS1 Composite 元素。</summary>
    public bool SupportsComposite { get; }
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
    /// <summary>获取是否支持主机字体族。</summary>
    public bool SupportsHostFontFamily { get; }
    /// <summary>获取是否支持旧设备字体标识。</summary>
    public bool SupportsLegacyDeviceFont { get; }
    /// <summary>获取是否支持条码可读文字。</summary>
    public bool SupportsBarcodeHumanReadable { get; }
    /// <summary>获取是否支持显式窄模块宽度。</summary>
    public bool SupportsExplicitBarcodeModuleWidth { get; }
    /// <summary>获取是否支持显式宽模块宽度。</summary>
    public bool SupportsWideModuleWidth { get; }
    /// <summary>获取是否支持任务收尾。</summary>
    public bool SupportsFinishing { get; }
    /// <summary>获取是否支持额外进纸。</summary>
    public bool SupportsExtraFeed { get; }
    /// <summary>获取是否声明 Host 软件位图预览。</summary>
    public bool SupportsSoftwareBitmap { get; }
    /// <summary>获取不支持软件位图时的明确原因。</summary>
    public string SoftwareBitmapReason { get; }

    /// <summary>初始化一个不可变的协议能力快照。</summary>
    public PrintProtocolCapabilities(int dpi, IEnumerable<int> elementTypes,
        IEnumerable<int> textSizes = null, IEnumerable<int> fontKinds = null,
        IEnumerable<RotationAngle> textRotations = null, IEnumerable<RotationAngle> imageRotations = null,
        IEnumerable<BarcodeType> barcodeTypes = null, IEnumerable<RotationAngle> barcodeRotations = null,
        IEnumerable<RotationAngle> qrCodeRotations = null, int? maxPageWidthDots = null,
        int? maxPageHeightDots = null, bool supportsImages = false, bool supportsBarcodes = false,
        bool supportsQrCode = false, bool supportsGs1QrCode = false, bool supportsComposite = false, bool supportsCoordinates = true,
        bool supportsFilledRectangles = false, bool supportsBoldText = false, bool supportsUnderlineText = false,
        bool supportsReverseText = false, bool supportsHostFontFamily = false,
        bool supportsLegacyDeviceFont = false, bool supportsBarcodeHumanReadable = false,
        bool supportsExplicitBarcodeModuleWidth = false, bool supportsWideModuleWidth = false,
        bool supportsFinishing = false, bool supportsExtraFeed = false, bool supportsSoftwareBitmap = false,
        IEnumerable<int> softwareBitmapElementTypes = null, string softwareBitmapReason = null)
    {
        if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        if (elementTypes == null) throw new ArgumentNullException(nameof(elementTypes));
        _elementTypes = elementTypes.Distinct().OrderBy(value => value).ToArray();
        if (_elementTypes.Length == 0 || _elementTypes.Any(value => value < 1 || value > 11))
            throw new ArgumentException("元素类型必须为 1 到 11 的非空集合。", nameof(elementTypes));
        _textSizes = (textSizes ?? Array.Empty<int>()).Distinct().OrderBy(value => value).ToArray();
        if (_textSizes.Any(value => value <= 0)) throw new ArgumentOutOfRangeException(nameof(textSizes));
        _fontKinds = (fontKinds ?? Array.Empty<int>()).Distinct().OrderBy(value => value).ToArray();
        if (_fontKinds.Any(value => value < 0 || value > 2)) throw new ArgumentOutOfRangeException(nameof(fontKinds));
        _textRotations = NormalizeRotations(textRotations, nameof(textRotations));
        _imageRotations = NormalizeRotations(imageRotations, nameof(imageRotations));
        _barcodeTypes = (barcodeTypes ?? Array.Empty<BarcodeType>()).Distinct()
            .OrderBy(value => (int)value).ToArray();
        _barcodeRotations = NormalizeRotations(barcodeRotations, nameof(barcodeRotations));
        _qrCodeRotations = NormalizeRotations(qrCodeRotations, nameof(qrCodeRotations));
        if (maxPageWidthDots <= 0) throw new ArgumentOutOfRangeException(nameof(maxPageWidthDots));
        if (maxPageHeightDots <= 0) throw new ArgumentOutOfRangeException(nameof(maxPageHeightDots));
        MaxPageWidthDots = maxPageWidthDots;
        MaxPageHeightDots = maxPageHeightDots;
        Dpi = dpi;
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
        SupportsHostFontFamily = supportsHostFontFamily;
        SupportsLegacyDeviceFont = supportsLegacyDeviceFont;
        SupportsBarcodeHumanReadable = supportsBarcodeHumanReadable;
        SupportsExplicitBarcodeModuleWidth = supportsExplicitBarcodeModuleWidth;
        SupportsWideModuleWidth = supportsWideModuleWidth;
        SupportsFinishing = supportsFinishing;
        SupportsExtraFeed = supportsExtraFeed;
        SupportsSoftwareBitmap = supportsSoftwareBitmap;
        _softwareBitmapElementTypes = (softwareBitmapElementTypes ?? Array.Empty<int>()).Distinct().ToArray();
        if (SupportsSoftwareBitmap && _softwareBitmapElementTypes.Length == 0)
            throw new ArgumentException("声明软件位图时必须列出可生成位图的元素类型。", nameof(softwareBitmapElementTypes));
        if (_softwareBitmapElementTypes.Any(value => !_elementTypes.Contains(value)))
            throw new ArgumentException("软件位图元素类型必须包含在支持元素集合中。", nameof(softwareBitmapElementTypes));
        SoftwareBitmapReason = softwareBitmapReason;
        if (SupportsBarcodes && _barcodeTypes.Length == 0)
            throw new ArgumentException("声明一维码支持时必须列出支持的码制。", nameof(barcodeTypes));
    }

    private static RotationAngle[] NormalizeRotations(IEnumerable<RotationAngle> values, string name)
    {
        var result = (values ?? new[] { RotationAngle.None }).Distinct().ToArray();
        if (result.Any(value => !Enum.IsDefined(typeof(RotationAngle), value)))
            throw new ArgumentOutOfRangeException(name);
        return result;
    }

    /// <summary>创建带有不同最大字号的能力副本；其他能力事实保持不变。</summary>
    /// <param name="maxTextSize">最大文字字号，必须为正数。</param>
    /// <returns>新的不可变能力快照。</returns>
    public PrintProtocolCapabilities WithMaxTextSize(int maxTextSize)
    {
        if (maxTextSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxTextSize));
        var copy = Clone();
        copy.MaxTextSize = maxTextSize;
        return copy;
    }

    /// <summary>创建带有不同显式静区声明的能力副本；其他能力事实保持不变。</summary>
    /// <param name="supportsExplicitQuietZone">是否支持显式静区。</param>
    /// <returns>新的不可变能力快照。</returns>
    public PrintProtocolCapabilities WithExplicitQuietZone(bool supportsExplicitQuietZone)
    {
        var copy = Clone();
        copy.SupportsExplicitQuietZone = supportsExplicitQuietZone;
        return copy;
    }

    private PrintProtocolCapabilities Clone()
        => new PrintProtocolCapabilities(Dpi, _elementTypes, _textSizes, _fontKinds, _textRotations,
            _imageRotations, _barcodeTypes, _barcodeRotations, _qrCodeRotations, MaxPageWidthDots,
            MaxPageHeightDots, SupportsImages, SupportsBarcodes, SupportsQrCode, SupportsGs1QrCode,
            SupportsComposite, SupportsCoordinates, SupportsFilledRectangles, SupportsBoldText,
            SupportsUnderlineText, SupportsReverseText, SupportsHostFontFamily, SupportsLegacyDeviceFont,
            SupportsBarcodeHumanReadable, SupportsExplicitBarcodeModuleWidth, SupportsWideModuleWidth,
            SupportsFinishing, SupportsExtraFeed, SupportsSoftwareBitmap, _softwareBitmapElementTypes,
            SoftwareBitmapReason)
        { MaxTextSize = MaxTextSize, SupportsExplicitQuietZone = SupportsExplicitQuietZone };

    /// <summary>判断是否支持指定模板元素类型。</summary>
    public bool SupportsElementType(int type) => Array.IndexOf(_elementTypes, type) >= 0;
    /// <summary>判断是否支持指定字号；空字号集合表示在最大字号内任意正整数。</summary>
    public bool SupportsTextSize(int size) => size > 0 && size <= MaxTextSize &&
        (_textSizes.Length == 0 || Array.IndexOf(_textSizes, size) >= 0);
    /// <summary>判断是否声明指定字体来源类型。</summary>
    public bool SupportsFontKind(PrintFontKind kind) => Array.IndexOf(_fontKinds, (int)kind) >= 0;
    /// <summary>判断是否支持指定文字旋转角度。</summary>
    public bool SupportsTextRotation(RotationAngle rotation) => Array.IndexOf(_textRotations, rotation) >= 0;
    /// <summary>判断是否支持指定图片旋转角度。</summary>
    public bool SupportsImageRotation(RotationAngle rotation) => Array.IndexOf(_imageRotations, rotation) >= 0;
    /// <summary>判断是否支持指定一维码制式。</summary>
    public bool SupportsBarcodeType(BarcodeType type) => Array.IndexOf(_barcodeTypes, type) >= 0;
    /// <summary>判断是否支持指定一维码旋转角度。</summary>
    public bool SupportsBarcodeRotation(RotationAngle rotation) => Array.IndexOf(_barcodeRotations, rotation) >= 0;
    /// <summary>判断是否支持指定二维码旋转角度。</summary>
    public bool SupportsQrCodeRotation(RotationAngle rotation) => Array.IndexOf(_qrCodeRotations, rotation) >= 0;
    /// <summary>判断指定元素类型是否可生成 Host 软件位图。</summary>
    public bool SupportsSoftwareBitmapElementType(int type) =>
        SupportsSoftwareBitmap && Array.IndexOf(_softwareBitmapElementTypes, type) >= 0;
}
