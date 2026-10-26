using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Bing.EasyPrint.Protocols;
using ZXing;
using ZXing.Common;
using Newtonsoft.Json;

namespace DemoProtocol;

/// <summary>示例协议注册入口；不修改核心枚举、内置分发或前端协议列表。</summary>
public static class DemoProtocolExtension
{
    /// <summary>默认配置 JSON。</summary>
    public const string DefaultConfigJson = "{\"characterWidth\":6,\"lineHeight\":24,\"maxPageHeight\":512,\"supportsFinishing\":true,\"barcodeModuleWidth\":2,\"qrModuleWidth\":4}";

    /// <summary>配置字段 JSON Schema 描述。</summary>
    public const string ConfigSchemaJson = "{\"type\":\"object\",\"properties\":{\"characterWidth\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":128,\"default\":6,\"title\":\"字符宽度\"},\"lineHeight\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":512,\"default\":24,\"title\":\"行高\"},\"maxPageHeight\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":100000,\"default\":512,\"title\":\"最大页高\"},\"supportsFinishing\":{\"type\":\"boolean\",\"default\":true,\"title\":\"声明收尾能力\"},\"barcodeModuleWidth\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":16,\"default\":2,\"title\":\"条码模块宽度\"},\"qrModuleWidth\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":32,\"default\":4,\"title\":\"二维码模块宽度\"},\"dpi\":{\"type\":\"integer\",\"enum\":[203,300,600],\"default\":203,\"title\":\"DPI\"}},\"required\":[\"characterWidth\",\"lineHeight\",\"maxPageHeight\"],\"additionalProperties\":false}";

    /// <summary>约束 JSON 描述。</summary>
    public const string ConstraintsJson = "{\"maxPageHeight\":100000,\"elementTypes\":[1,3,4,5,7],\"supportsImages\":false,\"supportsBarcodes\":true,\"supportsQr\":true,\"supportsComposite\":false}";

    /// <summary>创建核心注册表使用的协议注册项。</summary>
    public static PrintProtocolRegistration CreateRegistration()
        => new PrintProtocolRegistration("demo.protocol", "Demo Protocol (sample)", configurationJson =>
                new DemoProtocolBackend(configurationJson),
            ConfigSchemaJson, ConstraintsJson, null,
            capabilities: CreateCoreCapabilities(DemoConfig.Parse(DefaultConfigJson)),
            configurationValidator: configurationJson => _ = DemoConfig.Parse(configurationJson));

    /// <summary>创建模板适配器。</summary>
    public static ITemplateProtocolAdapter CreateAdapter(string configurationJson)
        => new DemoProtocolAdapter(configurationJson);

    internal static PrintProtocolCapabilities CreateCoreCapabilities(DemoConfig config)
        => new PrintProtocolCapabilities(config.Dpi, new[] { 1, 3, 4, 5, 7 },
            textSizes: Array.Empty<int>(), fontKinds: new[] { 0 },
            textRotations: new[] { RotationAngle.None },
            imageRotations: new[] { RotationAngle.None },
            barcodeTypes: new[] { BarcodeType.Code128, BarcodeType.Code39, BarcodeType.Ean13 },
            barcodeRotations: new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 },
            qrCodeRotations: new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 },
            maxPageHeightDots: config.MaxPageHeight, supportsImages: false, supportsBarcodes: true,
            supportsQrCode: true, supportsComposite: false, supportsCoordinates: true,
            supportsFilledRectangles: true, supportsFinishing: config.SupportsFinishing,
            supportsExtraFeed: true, supportsSoftwareBitmap: true,
            softwareBitmapElementTypes: new[] { 3, 4 });
}

/// <summary>示例模板适配器；字符宽度、行高和最大页高来自配置快照。</summary>
public sealed class DemoProtocolAdapter : ITemplateProtocolAdapter
{
    private readonly DemoConfig _config;
    private readonly string _configurationJson;

    /// <summary>按配置 JSON 创建适配器。</summary>
    public DemoProtocolAdapter(string configurationJson)
    {
        _configurationJson = configurationJson ?? string.Empty;
        _config = DemoConfig.Parse(_configurationJson);
        Capabilities = TemplateCustomProtocolCapabilities.FromCore(DemoProtocolExtension.CreateCoreCapabilities(_config));
    }

    /// <inheritdoc />
    public string ProtocolId => "demo.protocol";

    /// <inheritdoc />
    public TemplateCustomProtocolCapabilities Capabilities { get; }

    /// <inheritdoc />
    public ITemplateTextMetrics CreateTextMetrics(TemplateTextMeasurementRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        return new DemoTemplateTextMetrics(request, _config);
    }

    /// <inheritdoc />
    public TemplateBarcodeMeasurement MeasureBarcode(TemplateBarcodeMeasurementRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var element = request.Element;
        var module = element.NarrowModuleWidth ?? _config.BarcodeModuleWidth;
        int bodyWidth;
        try
        {
            bodyWidth = EscPosRasterCanvas.MeasureBarcodeWidth(element.BarcodeType, request.Data, module);
        }
        catch (Exception error) when (error is ArgumentException || error is NotSupportedException)
        {
            throw new NotSupportedException("示例协议无法测量该一维码：" + error.Message, error);
        }
        var quiet = element.QuietZone;
        var width = checked(bodyWidth + (quiet == null ? 0 : quiet.Left + quiet.Right));
        var height = checked((element.Height > 0 ? element.Height : 40) +
            element.HumanReadableReserveHeight + (quiet == null ? 0 : quiet.Top + quiet.Bottom));
        var rotated = element.Rotation == RotationAngle.Rotate90 || element.Rotation == RotationAngle.Rotate270;
        return new TemplateBarcodeMeasurement(rotated ? height : width, rotated ? width : height);
    }

    /// <inheritdoc />
    public PrintElement ConvertBarcode(TemplateBarcodeConversionRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var element = request.Element;
        var module = element.NarrowModuleWidth ?? _config.BarcodeModuleWidth;
        var wide = element.WideModuleWidth ?? checked(module * 2);
        var rotated = element.Rotation == RotationAngle.Rotate90 || element.Rotation == RotationAngle.Rotate270;
        var reservedWidth = rotated ? request.Height : request.Width;
        var rawHeight = element.Height > 0 ? element.Height : (rotated ? request.Width : request.Height);
        var height = Math.Max(1, rawHeight -
            (element.QuietZone == null ? 0 : element.QuietZone.Top + element.QuietZone.Bottom) -
            element.HumanReadableReserveHeight);
        return element.QuietZone == null
            ? new PrintBarcodeElement(request.X, request.Y, element.BarcodeType, request.Data, height,
                module, wide, element.Rotation, humanReadable: element.HumanReadable,
                humanReadableReserveHeight: element.HumanReadableReserveHeight,
                reservedWidth: reservedWidth)
            : new PrintBarcodeElement(request.X, request.Y, element.BarcodeType, request.Data, height,
                element.QuietZone, module, wide, element.Rotation, humanReadable: element.HumanReadable,
                humanReadableReserveHeight: element.HumanReadableReserveHeight,
                reservedWidth: reservedWidth);
    }

    /// <inheritdoc />
    public TemplateQrCodeMeasurement MeasureQrCode(TemplateQrCodeMeasurementRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var element = request.Element;
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.ERROR_CORRECTION] = element.ErrorLevel,
        };
        var matrix = new MultiFormatWriter().encode(request.Data, BarcodeFormat.QR_CODE, 1, 1, hints);
        var module = _config.QrModuleWidth;
        var quiet = element.QuietZone;
        var size = quiet == null
            ? checked(matrix.Width * module)
            : checked((matrix.Width - 8) * module + quiet.Left + quiet.Right);
        var height = quiet == null
            ? checked(matrix.Width * module)
            : checked((matrix.Width - 8) * module + quiet.Top + quiet.Bottom);
        var rotated = element.Rotation == RotationAngle.Rotate90 || element.Rotation == RotationAngle.Rotate270;
        return new TemplateQrCodeMeasurement(rotated ? height : size, rotated ? size : height);
    }

    /// <inheritdoc />
    public PrintElement ConvertQrCode(TemplateQrCodeConversionRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var element = request.Element;
        if (!Enum.TryParse(element.ErrorLevel, out QrCodeCorrectionLevel level))
            throw new NotSupportedException("示例协议不支持该二维码纠错等级。");
        var module = _config.QrModuleWidth;
        return element.QuietZone == null
            ? new PrintQrCodeElement(request.X, request.Y, request.Data, module, level,
                request.Width, request.Height, element.Rotation)
            : new PrintQrCodeElement(request.X, request.Y, request.Data, module, level, element.QuietZone,
                request.Width, request.Height, element.Rotation);
    }
}

internal sealed class DemoConfig
{
    public int CharacterWidth { get; set; } = 6;
    public int LineHeight { get; set; } = 24;
    public int MaxPageHeight { get; set; } = 512;
    public bool SupportsFinishing { get; set; } = true;
    public int Dpi { get; set; } = 203;
    public int BarcodeModuleWidth { get; set; } = 2;
    public int QrModuleWidth { get; set; } = 4;

    internal static DemoConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DemoConfig();
        DemoConfig config;
        try
        {
            config = JsonConvert.DeserializeObject<DemoConfig>(json, new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
                DateParseHandling = DateParseHandling.None
            }) ?? new DemoConfig();
        }
        catch (JsonException error)
        {
            throw new ArgumentException("示例协议配置不是有效 JSON 或包含未知字段。", nameof(json), error);
        }
        if (config.CharacterWidth < 1 || config.CharacterWidth > 128)
            throw new ArgumentOutOfRangeException(nameof(config.CharacterWidth), "字符宽度必须为 1–128。");
        if (config.LineHeight < 1 || config.LineHeight > 512)
            throw new ArgumentOutOfRangeException(nameof(config.LineHeight), "行高必须为 1–512。");
        if (config.MaxPageHeight < 1 || config.MaxPageHeight > 100000)
            throw new ArgumentOutOfRangeException(nameof(config.MaxPageHeight), "最大页高必须为 1–100000。");
        if (config.Dpi != 203 && config.Dpi != 300 && config.Dpi != 600)
            throw new ArgumentOutOfRangeException(nameof(config.Dpi), "示例协议 DPI 仅支持 203、300 或 600。");
        if (config.BarcodeModuleWidth < 1 || config.BarcodeModuleWidth > 16)
            throw new ArgumentOutOfRangeException(nameof(config.BarcodeModuleWidth), "条码模块宽度必须为 1～16。");
        if (config.QrModuleWidth < 1 || config.QrModuleWidth > 32)
            throw new ArgumentOutOfRangeException(nameof(config.QrModuleWidth), "二维码模块宽度必须为 1～32。");
        return config;
    }
}

internal sealed class DemoTemplateTextMetrics : ITemplateTextMetrics
{
    private readonly int _characterWidth;

    internal DemoTemplateTextMetrics(TemplateTextMeasurementRequest request, DemoConfig config)
    {
        if (request.Rotation != RotationAngle.None)
            throw new NotSupportedException("示例协议不支持非零文字旋转。");
        if (!configSupportsFont(request)) throw new NotSupportedException("示例协议不支持设备字体或主机字体。");
        _characterWidth = config.CharacterWidth + (request.Bold ? 1 : 0);
        LineHeight = config.LineHeight + (request.Bold ? 1 : 0);
    }

    private static bool configSupportsFont(TemplateTextMeasurementRequest request)
        => request.Font == null || request.Font.Kind == PrintFontKind.Default;

    public int LineHeight { get; }
    public string FontFamily => null!;
    public bool HasDeviceTextElementCalibrations => false;
    public bool NeedsFullMeasure => false;
    public int Advance(string textElement) => textElement == null ? 0 : textElement.Length * _characterWidth;
    public long Measure(string text) => text == null ? 0 : (long)text.Length * _characterWidth;
    public TemplateTextBounds MeasureBounds(string text)
        => new TemplateTextBounds(0, 0, checked((int)Measure(text)), LineHeight);
}

internal sealed class DemoProtocolBackend : IPrintProtocolFinishingBackend
{
    private readonly DemoConfig _config;
    private bool _disposed;

    internal DemoProtocolBackend(string configurationJson)
    {
        _config = DemoConfig.Parse(configurationJson);
    }

    public string ProtocolId => "demo.protocol";
    public bool SupportsFinishing => _config.SupportsFinishing;

    public void Validate(PrintDocument document, CancellationToken cancellationToken)
    {
        EnsureActive();
        if (document == null) throw new ArgumentNullException(nameof(document));
        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (page.Width <= 0 || page.Height <= 0 || page.Height > _config.MaxPageHeight)
                throw new PrintProtocolBackendException("demo.protocol", "示例协议页面尺寸无效或超过配置上限。",
                    "Template", null, null, false,
                    new ArgumentOutOfRangeException(nameof(page.Height)));
            if (page.Copies < 1) throw new ArgumentOutOfRangeException(nameof(page.Copies));
            if (page.FeedLines != 0 || page.CutAfterPrint)
                throw new PrintProtocolBackendException(ProtocolId, "示例协议要求通过收尾契约表达进纸和切纸。",
                    "Template", null, null, true, new ArgumentException("旧页面 feed/cut 字段未声明。"));
            foreach (var element in page.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (element)
                {
                    case PrintTextElement text:
                        if (text.Rotation != RotationAngle.None || text.FontFamily != null || text.DeviceFontName != null)
                            throw new PrintProtocolBackendException(ProtocolId, "示例协议只支持默认字体的横排文字。",
                                text.SourcePath ?? "Template", null, null, true);
                        if (text.Bold || text.Underline || text.Reverse)
                            throw new PrintProtocolBackendException(ProtocolId, "示例协议未声明文字样式支持。",
                                text.SourcePath ?? "Template", null, null, true);
                        break;
                    case PrintBarcodeElement barcode:
                        if (barcode.HumanReadable == true || barcode.HumanReadableReserveHeight > 0)
                            throw new PrintProtocolBackendException(ProtocolId, "示例协议未声明条码可读文字支持。",
                                barcode.SourcePath ?? "Template", null, null, true);
                        break;
                    case PrintLineElement:
                    case PrintRectangleElement:
                    case PrintQrCodeElement:
                        break;
                    default:
                        throw new PrintProtocolBackendException(ProtocolId,
                            "示例协议只支持文字、线段和矩形；其他元素必须在模板预检阶段拒绝。",
                            element.SourcePath ?? "Template", null, null, true);
                }
            }
        }
    }

    public IReadOnlyList<PrintPageOutput> Render(PrintDocument document, CancellationToken cancellationToken)
    {
        EnsureActive();
        Validate(document, cancellationToken);
        var result = new List<PrintPageOutput>(document.Pages.Count);
        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var buffer = new MemoryStream();
            using (var writer = new StreamWriter(buffer, new UTF8Encoding(false), 1024, true) { NewLine = "\n" })
            {
                for (var copy = 0; copy < page.Copies; copy++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.WriteLine("DEMO 1");
                    writer.WriteLine("PAGE " + page.Width.ToString(CultureInfo.InvariantCulture) + " " +
                        page.Height.ToString(CultureInfo.InvariantCulture));
                    foreach (var element in page.Elements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        switch (element)
                        {
                            case PrintTextElement text:
                                writer.WriteLine("T " + text.X.ToString(CultureInfo.InvariantCulture) + " " +
                                    text.Y.ToString(CultureInfo.InvariantCulture) + " " +
                                    text.FontSize.ToString(CultureInfo.InvariantCulture) + " " +
                                    Escape(text.Text));
                                break;
                            case PrintLineElement line:
                                writer.WriteLine("L " + line.X.ToString(CultureInfo.InvariantCulture) + " " +
                                    line.Y.ToString(CultureInfo.InvariantCulture) + " " +
                                    line.EndX.ToString(CultureInfo.InvariantCulture) + " " +
                                    line.EndY.ToString(CultureInfo.InvariantCulture) + " " +
                                    line.LineWidth.ToString(CultureInfo.InvariantCulture));
                                break;
                            case PrintRectangleElement rectangle:
                                writer.WriteLine("R " + rectangle.X.ToString(CultureInfo.InvariantCulture) + " " +
                                    rectangle.Y.ToString(CultureInfo.InvariantCulture) + " " +
                                    rectangle.Width.ToString(CultureInfo.InvariantCulture) + " " +
                                    rectangle.Height.ToString(CultureInfo.InvariantCulture) + " " +
                                    rectangle.LineWidth.ToString(CultureInfo.InvariantCulture) + " " +
                                    (rectangle.Filled ? "1" : "0"));
                                break;
                            case PrintBarcodeElement barcode:
                                writer.WriteLine("B " + barcode.X.ToString(CultureInfo.InvariantCulture) + " " +
                                    barcode.Y.ToString(CultureInfo.InvariantCulture) + " " +
                                    barcode.Type.ToString() + " " + barcode.NarrowWidth.ToString(CultureInfo.InvariantCulture) + " " +
                                    barcode.Rotation.ToString() + " " + Escape(barcode.Data));
                                break;
                            case PrintQrCodeElement qrCode:
                                writer.WriteLine("Q " + qrCode.X.ToString(CultureInfo.InvariantCulture) + " " +
                                    qrCode.Y.ToString(CultureInfo.InvariantCulture) + " " +
                                    qrCode.ModuleSize.ToString(CultureInfo.InvariantCulture) + " " +
                                    qrCode.ErrorLevel.ToString() + " " + qrCode.Rotation.ToString() + " " + Escape(qrCode.Data));
                                break;
                        }
                    }
                    if (page.FeedLines > 0) writer.WriteLine("FEED " + page.FeedLines.ToString(CultureInfo.InvariantCulture));
                    if (page.CutAfterPrint) writer.WriteLine("CUT");
                }
                writer.Flush();
            }
            var bytes = buffer.ToArray();
            PrintResourceScope.Reserve(bytes.Length, "Demo.Output");
            result.Add(new PrintPageOutput(new[] { new PrintOutputSegment(bytes) }));
        }
        return result.AsReadOnly();
    }

    public void ValidateFinishing(PrintDocument document, PrintProtocolFinishingRequest request,
        CancellationToken cancellationToken)
    {
        EnsureActive();
        if (!SupportsFinishing) throw new NotSupportedException("示例协议未声明任务收尾能力。");
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        if (request.FeedAmount > 10000)
            throw new ArgumentOutOfRangeException(nameof(request.FeedAmount), "示例协议单次收尾进纸不能超过 10000。");
    }

    public long GetFinishingLength(PrintProtocolFinishingRequest request, CancellationToken cancellationToken)
    {
        EnsureActive();
        if (!SupportsFinishing) throw new NotSupportedException("示例协议未声明任务收尾能力。");
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        return Encoding.UTF8.GetByteCount(FinishingText(request));
    }

    public PrintPageOutput RenderFinishing(PrintProtocolFinishingRequest request, CancellationToken cancellationToken)
    {
        EnsureActive();
        ValidateFinishing(null!, request, cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(FinishingText(request));
        PrintResourceScope.Reserve(bytes.Length, "Demo.Finishing");
        return new PrintPageOutput(new[] { new PrintOutputSegment(bytes, PrintOutputSegmentKind.Control) });
    }

    private static string FinishingText(PrintProtocolFinishingRequest request)
        => "FEED " + request.FeedAmount.ToString(CultureInfo.InvariantCulture) + " " +
           request.FeedUnit.ToString() + "\n" + (request.Cut ? "CUT\n" : string.Empty);

    private static string Escape(string value)
        => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n");

    private void EnsureActive()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DemoProtocolBackend));
    }

    public void Dispose() => _disposed = true;
}
