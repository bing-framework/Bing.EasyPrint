using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bing.EasyPrint.Templates;

/// <summary>模板布局使用的协议无关绘制目标。</summary>
internal interface IPrintCanvas
{
    void SetSourcePath(string sourcePath);
    void SetSourceOwnership(TemplateLayoutElement ownership);
    void InitCommand(int offset, int width, int height, int qty);
    void Line(int x0, int y0, int x1, int y1, int width);
    void FillRectangle(int x, int y, int width, int height);
    void DrawText(int x, int y, string text, int fontSize, int rotation, bool bold, bool reverse, bool underline,
        string fontFamily = null, int reservedWidth = 0, int reservedHeight = 0,
        string deviceFontName = null, PrintFont font = null);
    void DrawBarcode1D(BarcodeType type, int x, int y, string text, int lineWidth, int height, int ratio,
        RotationAngle rotation, bool? humanReadable = null, int humanReadableReserveHeight = 0,
        int reservedWidth = 0, int? narrowModuleWidth = null, int? wideModuleWidth = null, PrintQuietZone quietZone = null);
    void DrawQrCode(int x, int y, string text, int unitWidth, string errorLevel, int rotation,
        int reservedWidth = 0, int reservedHeight = 0, PrintQuietZone quietZone = null);
    void DrawImage(int x, int y, Image image, ImageProcessingOptions options, RotationAngle rotation = RotationAngle.None);
    void DrawCompressedImage(int x, int y, Image image, ImageProcessingOptions options, RotationAngle rotation = RotationAngle.None);
    byte[] Build();
}

/// <summary>创建模板使用的协议绘制目标。</summary>
internal static class PrintCanvasFactory
{
    internal static IPrintCanvas Create(PrintProtocol protocol, Encoding encoding, int width, int height, TemplateRenderOptions options,
        CancellationToken cancellationToken = default(CancellationToken), PrintStorageBudget storage = null)
    {
        switch (protocol)
        {
            case PrintProtocol.Cpcl:
                if (options.FeedLines > 0 || options.CutAfterPrint == true)
                    throw new TemplateRenderException("Template", "协议 Cpcl 不支持模板级额外进纸或切纸。",
                        new NotSupportedException("CPCL 模板页没有对应的进纸或切纸命令。"));
                return new DocumentPrintCanvas(protocol, encoding, width, options, cancellationToken, storage);
            case PrintProtocol.Tspl:
            case PrintProtocol.Zpl:
            case PrintProtocol.EscPos:
                return new DocumentPrintCanvas(protocol, encoding, width, options, cancellationToken, storage);
            default: throw new System.ArgumentOutOfRangeException(nameof(protocol), protocol, "不支持的打印协议。");
        }
    }
}

/// <summary>将模板绘制记录为协议无关的页面，再交给目标 Provider 编码。</summary>
internal sealed class DocumentPrintCanvas : IPrintCanvas
{
    private readonly PrintProtocol _protocol;
    private readonly Encoding _encoding;
    private readonly int _width;
    private readonly TemplateRenderOptions _options;
    private readonly CancellationToken _cancellationToken;
    private readonly BudgetedList<(PrintElement Element, string SourcePath)> _elements;
    private string _sourcePath;
    private TemplateLayoutElement _sourceOwnership;
    private readonly BudgetedList<TemplateLayoutElement> _elementOwnership;
    internal IReadOnlyList<TemplateLayoutElement> ElementOwnership => _elementOwnership.AsReadOnly();
    private int _height;
    private int _copies;

    internal DocumentPrintCanvas(PrintProtocol protocol, Encoding encoding, int width, TemplateRenderOptions options,
        CancellationToken cancellationToken = default(CancellationToken), PrintStorageBudget storage = null)
    {
        _elements = new BudgetedList<(PrintElement Element, string SourcePath)>("Template.CanvasElements", slotBytes: 16,
            itemBytes: 64, owner: storage, retainedSize: item => item.Element.GetRetainedBytes(item.SourcePath));
        _elementOwnership = new BudgetedList<TemplateLayoutElement>("Template.CanvasOwnership", owner: storage);
        _protocol = protocol;
        _encoding = encoding;
        _width = width;
        _options = options;
        _cancellationToken = cancellationToken;
    }

    public void SetSourcePath(string sourcePath)
    {
        _sourcePath = sourcePath;
        _sourceOwnership = null;
    }

    public void SetSourceOwnership(TemplateLayoutElement ownership)
    {
        _sourceOwnership = ownership;
        _sourcePath = ownership?.SourcePath;
    }

    public void InitCommand(int offset, int width, int height, int qty)
    {
        if (offset != 0 || width != _width || height <= 0 || qty <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "打印页设置无效。");
        _height = height;
        _copies = qty;
    }

    public void Line(int x0, int y0, int x1, int y1, int width)
        => Add(new PrintLineElement(x0, y0, x1, y1, width));

    public void DrawText(int x, int y, string text, int fontSize, int rotation, bool bold, bool reverse, bool underline,
        string fontFamily = null, int reservedWidth = 0, int reservedHeight = 0,
        string deviceFontName = null, PrintFont font = null)
    {
        if (!Enum.IsDefined(typeof(RotationAngle), rotation))
            throw new NotSupportedException("统一模板文字模型不支持此旋转角度。");
        if (_protocol == PrintProtocol.Cpcl && rotation != 0)
        {
            // 模板使用顺时针角度与左上角占位；旧 CPCL 命令继续采用原生逆时针锚点。
            if (rotation == 90) x = checked(x + reservedHeight);
            else if (rotation == 180) { x = checked(x + reservedWidth); y = checked(y + reservedHeight); }
            else y = checked(y + reservedWidth);
            rotation = (360 - rotation) % 360;
        }
        // 记录已经用于排版的实际字体族，避免默认字体在分行绘制时重新选择。
        if (_protocol == PrintProtocol.EscPos && font != null && !string.IsNullOrWhiteSpace(fontFamily))
            font = new PrintFont(PrintFontKind.Host, fontFamily);
        Add(font == null
            ? new PrintTextElement(x, y, text, fontSize, bold, underline, (RotationAngle)rotation,
                width: reservedWidth, height: reservedHeight, fontFamily: fontFamily, reverse: reverse,
                deviceFontName: deviceFontName)
            : new PrintTextElement(x, y, text, fontSize, font, bold, underline, (RotationAngle)rotation,
                width: reservedWidth, height: reservedHeight, reverse: reverse));
    }

    public void FillRectangle(int x, int y, int width, int height)
        => Add(PrintRectangleElement.CreateFilled(x, y, width, height));

    public void DrawBarcode1D(BarcodeType type, int x, int y, string text, int lineWidth, int height,
        int ratio, RotationAngle rotation, bool? humanReadable = null, int humanReadableReserveHeight = 0,
        int reservedWidth = 0, int? narrowModuleWidth = null, int? wideModuleWidth = null, PrintQuietZone quietZone = null)
    {
        var narrow = narrowModuleWidth ?? checked(lineWidth + 1);
        var ratioTenths = ratio <= 4 ? 15 + ratio * 5 : ratio;
        // ZPL 和 ESC/POS 没有独立的宽条参数；旧模板 Ratio 在这些协议中只能
        // 保留兼容默认值，避免把不可表达的宽度传入统一模型后被静默忽略。
        var wide = _protocol == PrintProtocol.Cpcl || _protocol == PrintProtocol.Tspl
            ? wideModuleWidth ?? checked((int)(((long)narrow * ratioTenths + 9) / 10))
            : 2;
        // TSPL EAN14 is a fixed 1:1 symbol; an omitted wide-module value must
        // not inherit the legacy ratio default used by other linear barcodes.
        if (_protocol == PrintProtocol.Tspl &&
            (type == BarcodeType.Ean14 || type == BarcodeType.ChinaPost) &&
            !wideModuleWidth.HasValue)
        {
            wide = narrow;
        }
        if (_protocol == PrintProtocol.Cpcl && rotation != RotationAngle.None)
        {
            if (rotation != RotationAngle.Rotate270)
                throw new NotSupportedException("CPCL 模板条码仅支持 0 或顺时针 270 度。");
            y = checked(y + reservedWidth);
            rotation = RotationAngle.Rotate90;
        }
        var moduleWidth = _protocol == PrintProtocol.Cpcl ? lineWidth : narrow;
        var cpclRatio = _protocol == PrintProtocol.Cpcl ? ratio : (int?)null;
        var element = quietZone == null
            ? new PrintBarcodeElement(x, y, type, text, height, moduleWidth, wide, rotation,
                cpclRatio, humanReadable, humanReadableReserveHeight, reservedWidth)
            : new PrintBarcodeElement(x, y, type, text, height, quietZone, moduleWidth, wide, rotation,
                cpclRatio, humanReadable, humanReadableReserveHeight, reservedWidth);
        if (_protocol == PrintProtocol.Cpcl && rotation == RotationAngle.Rotate90)
            element.MarkCpclTemplateRotation();
        Add(element);
    }

    public void DrawQrCode(int x, int y, string text, int unitWidth, string errorLevel, int rotation,
        int reservedWidth = 0, int reservedHeight = 0, PrintQuietZone quietZone = null)
    {
        if (!Enum.IsDefined(typeof(RotationAngle), rotation) ||
            !Enum.TryParse(errorLevel, out QrCodeCorrectionLevel correctionLevel) ||
            !Enum.IsDefined(typeof(QrCodeCorrectionLevel), correctionLevel))
            throw new NotSupportedException("统一模板二维码模型不支持此旋转角度或纠错等级。");
        Add(quietZone == null
            ? new PrintQrCodeElement(x, y, text, unitWidth, correctionLevel,
                reservedWidth, reservedHeight, (RotationAngle)rotation)
            : new PrintQrCodeElement(x, y, text, unitWidth, correctionLevel, quietZone,
                reservedWidth, reservedHeight, (RotationAngle)rotation));
    }

    public void DrawImage(int x, int y, Image image, ImageProcessingOptions options, RotationAngle rotation = RotationAngle.None)
    {
        var raster = _protocol == PrintProtocol.Cpcl
            ? PrintImageRasterizer.ToCpclMonochrome(image, _cancellationToken)
            : PrintCanvasRasterizer.ToMonochrome(image, _options.ImageThreshold, _cancellationToken).Bytes;
        Add(new PrintImageElement(x, y, image.Width, image.Height, raster, rotation));
    }

    public void DrawCompressedImage(int x, int y, Image image, ImageProcessingOptions options, RotationAngle rotation = RotationAngle.None)
    {
        var raster = _protocol == PrintProtocol.Cpcl
            ? PrintImageRasterizer.ToCpclMonochrome(image, _cancellationToken)
            : PrintCanvasRasterizer.ToMonochrome(image, _options.ImageThreshold, _cancellationToken).Bytes;
        Add(new PrintImageElement(x, y, image.Width, image.Height, raster, rotation, compressed: true));
    }

    internal PrintPage AppendPage(PrintDocument document)
    {
        if (_height <= 0) throw new InvalidOperationException("打印页尚未结束。");
        var page = document.AddPage(_width, _height);
        page.Copies = _copies;
        page.FeedLines = _options.Job == null ? _options.FeedLines ?? (_protocol == PrintProtocol.EscPos ? 1 : 0) : 0;
        page.CutAfterPrint = _options.Job == null && (_options.CutAfterPrint ?? (_protocol == PrintProtocol.EscPos));
        page.CutOptions = _options.CutOptions?.Snapshot();
        foreach (var item in _elements) page.Add(item.Element, item.SourcePath);
        return page;
    }

    public byte[] Build()
    {
        var document = new PrintDocument();
        AppendPage(document);
        var provider = PrintProtocolProviderFactory.Create(_protocol, _encoding, _options.EscPosLayout,
            _options.EscPosDevice, _options.TsplMedia, _options.ZplMedia,
            _options.RequireDeclaredNativeBounds);
        try
        {
            return provider.Render(document, _cancellationToken)[0];
        }
        catch (PrintCapabilityException error)
        {
            throw new TemplateRenderException(error.ElementPath, error.Message, error);
        }
        catch (PrintElementValidationException error)
        {
            throw new TemplateRenderException(error.ElementPath, error.Message, error);
        }
        catch (NotSupportedException error)
        {
            throw new TemplateRenderException("Template", $"协议 {_protocol} 无法生成页面：{error.Message}", error);
        }
    }

    private void Add(PrintElement element)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _elements.Add((element, _sourcePath));
        _elementOwnership.Add(_sourceOwnership);
    }
}

internal static class PrintCanvasRasterizer
{
    internal static (byte[] Bytes, int RowBytes, int Height) ToMonochrome(Image source, int threshold = 128,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (threshold < 0 || threshold > 255) throw new ArgumentOutOfRangeException(nameof(threshold));
        using (var memory = PrintResourceScope.Reserve(checked((long)source.Width * source.Height * 4), "Image.ThresholdRgba"))
        using (var image = source.CloneAs<Rgba32>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowBytes = (image.Width + 7) / 8;
            var length = checked(rowBytes * image.Height);
            var bytes = Bing.EasyPrint.Core.BudgetedBuffer.Create(length, "Image.ThresholdMonochrome", output =>
            {
                for (var y = 0; y < image.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (var x = 0; x < image.Width; x++)
                    {
                        if ((x & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                        var pixel = image[x, y];
                        // 打印介质默认是白色。先将透明度合成到白色背景，避免透明黑色被误判为黑点。
                        var inverseAlpha = 255 - pixel.A;
                        var red = (pixel.R * pixel.A + 255 * inverseAlpha) / 255;
                        var green = (pixel.G * pixel.A + 255 * inverseAlpha) / 255;
                        var blue = (pixel.B * pixel.A + 255 * inverseAlpha) / 255;
                        var luminance = (red * 299 + green * 587 + blue * 114) / 1000;
                        if (luminance < threshold) output[y * rowBytes + x / 8] |= (byte)(0x80 >> (x & 7));
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
            });
            return (bytes, rowBytes, image.Height);
        }
    }
}
