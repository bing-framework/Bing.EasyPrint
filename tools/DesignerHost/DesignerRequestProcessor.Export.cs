using System.Globalization;
using System.Text;
using System.Xml;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Microsoft.AspNetCore.Http;
using PdfSharp.Drawing;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf;
using ZXing.Common;

internal static partial class DesignerRequestProcessor
{
    internal static IResult PagePdf(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Prepare(request, cancellationToken);
            if (input.Options.CustomProtocol != null || input.Options.Protocol != PrintProtocol.EscPos ||
                input.Options.EscPosLayout != EscPosLayoutMode.Raster)
                throw new NotSupportedException("整页 PDF 仅支持 ESC/POS Raster，当前协议未提供准确的共享光栅页面。");

            var result = new TemplateRenderer().RenderWithLayoutAndPrimitives(input.Template, input.Data,
                input.Resources, input.Options, cancellationToken);
            try
            {
                var bytes = EncodePagePdf(result.Pages, input.Options, request.PageIndex,
                    input.Options.MaxOutputBytes, cancellationToken);
                return Results.File(bytes, "application/pdf", request.PageIndex.HasValue
                    ? $"easyprint-page-{request.PageIndex.Value + 1}.pdf"
                    : "easyprint-pages.pdf");
            }
            finally { result.ReleaseRenderElements(); }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    private static DesignerOptionException InvalidPdfPageIndex()
        => new("InvalidPageIndex", "pageIndex 超出渲染页范围。", "pageIndex");

    private static byte[] EncodePagePdf(IReadOnlyList<TemplatePage> logicalPages, TemplateRenderOptions options,
        int? requestedPageIndex, int maxBytes, CancellationToken cancellationToken)
    {
        IReadOnlyList<TemplatePage> selectedPages;
        var expandCopies = options.Job == null;
        if (requestedPageIndex is int pageIndex)
        {
            if (pageIndex < 0) throw InvalidPdfPageIndex();
            if (options.Job == null)
            {
                if (pageIndex >= logicalPages.Count) throw InvalidPdfPageIndex();
                selectedPages = new[] { logicalPages[pageIndex] };
            }
            else
            {
                var matching = logicalPages.Where(page => page.SourcePageIndex == pageIndex).ToArray();
                if (matching.Length == 0) throw InvalidPdfPageIndex();
                selectedPages = matching;
                expandCopies = false;
            }
        }
        else selectedPages = logicalPages;

        var copies = expandCopies ? options.Copies : 1;
        var pageCount = checked((long)selectedPages.Count * copies);
        if (pageCount < 1 || pageCount > options.MaxPages)
            throw new DesignerOptionException("PdfPageLimit",
                $"PDF 实际页数超过最大页数 {options.MaxPages}。", "copies");

        var capabilities = TemplateProtocolCapabilities.Create(options);
        var dpiX = RasterDpiX(options, capabilities);
        var dpiY = RasterDpiY(options, capabilities);
        if (dpiX <= 0) dpiX = 203;
        if (dpiY <= 0) dpiY = 203;
        var font = options.EscPosDevice?.RasterBarcodeHumanReadableFontSize ??
            capabilities.RasterBarcodeHumanReadableFontSize ?? 12;
        using var output = new BudgetedHostStream("Host.PdfOutput", cancellationToken, maxBytes);
        using var document = new PdfDocument();
        var images = new List<XImage>(checked((int)pageCount));
        var imageStreams = new List<MemoryStream>(checked((int)pageCount));
        var imageMemoryReservations = new List<IDisposable>(checked((int)pageCount));
        try
        {
            for (var copyIndex = 0; copyIndex < copies; copyIndex++)
            {
                foreach (var page in selectedPages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var raster = PrintRasterPreview.RenderElements(page.RenderElements,
                        page.Width, page.Height, options.EscPosDevice?.RasterFonts, font, cancellationToken);
                    var png = EncodePng(raster, dpiX, dpiY, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    imageMemoryReservations.Add(PrintResourceScope.Reserve(
                        checked((long)raster.Width * raster.Height * 4), "Host.PdfDecodedImage"));
                    var pdfPage = document.AddPage();
                    var pageWidth = RasterDotsToPdfPoints(raster.Width, dpiX);
                    var pageHeight = RasterDotsToPdfPoints(raster.Height, dpiY);
                    pdfPage.Width = XUnit.FromPoint(pageWidth);
                    pdfPage.Height = XUnit.FromPoint(pageHeight);
                    var imageStream = new MemoryStream(png, writable: false);
                    imageStreams.Add(imageStream);
                    var image = XImage.FromStream(imageStream);
                    image.Interpolate = false;
                    images.Add(image);
                    using (var graphics = XGraphics.FromPdfPage(pdfPage))
                        graphics.DrawImage(image, 0, 0, pageWidth, pageHeight);
                    var content = ContentReader.ReadContent(pdfPage);
                    var transform = content.OfType<COperator>().Single(operation => operation.Name == "cm");
                    ((CReal)transform.Operands[0]).Value = pageWidth;
                    ((CReal)transform.Operands[3]).Value = pageHeight;
                    // XGraphics writes transforms to four decimals, which can shift exact raster pixel boundaries.
                    pdfPage.Contents.ReplaceContent(content);
                    SetExactMediaBox(pdfPage, pageWidth, pageHeight);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            document.Save(output, closeStream: false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            foreach (var image in images) image.Dispose();
            foreach (var imageStream in imageStreams) imageStream.Dispose();
            foreach (var reservation in imageMemoryReservations) reservation.Dispose();
        }

        return output.ToArray("Host.PdfOutputSnapshot");
    }

    private static void SetExactMediaBox(PdfPage page, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new InvalidOperationException("PDF page dimensions must be finite positive point values.");

        // PdfReal writes only three decimal places; the media box keeps the exact dot-to-point dimensions.
        var mediaBox = new PdfArray(page.Owner);
        mediaBox.Elements.Add(new PdfInteger(0));
        mediaBox.Elements.Add(new PdfInteger(0));
        mediaBox.Elements.Add(new PdfLiteral(width.ToString("R", CultureInfo.InvariantCulture)));
        mediaBox.Elements.Add(new PdfLiteral(height.ToString("R", CultureInfo.InvariantCulture)));
        page.Elements["/MediaBox"] = mediaBox;
    }

    private static double RasterDotsToPdfPoints(int dots, int dpi)
    {
        var points = dots * 72d / dpi;
        return points * dpi / 72d > dots ? Math.BitDecrement(points) : points;
    }

    internal static IResult ElementSvg(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!request.PageIndex.HasValue || !request.ElementIndex.HasValue)
                throw new DesignerOptionException("MissingElementSelection",
                    "SVG 导出必须指定 pageIndex 和 elementIndex。", "elementIndex");
            var input = Prepare(request, cancellationToken);
            if (input.Options.CustomProtocol != null)
                throw new NotSupportedException("新协议未声明共享码图 SVG 编码。");

            var capabilities = TemplateProtocolCapabilities.Create(input.Options);
            var dpiX = RasterDpiX(input.Options, capabilities);
            var dpiY = RasterDpiY(input.Options, capabilities);
            var result = new TemplateRenderer().RenderWithLayoutAndPrimitives(input.Template, input.Data,
                input.Resources, input.Options, cancellationToken);
            try
            {
                var pageIndex = request.PageIndex.Value;
                if (pageIndex < 0 || pageIndex >= result.Pages.Count)
                    throw new DesignerOptionException("InvalidPageIndex", "pageIndex 超出渲染页范围。", "pageIndex");
                var page = result.Pages[pageIndex];
                var elementIndex = request.ElementIndex.Value;
                if (elementIndex < 0 || elementIndex >= page.Elements.Count)
                    throw new DesignerOptionException("InvalidElementIndex", "elementIndex 超出页面码图范围。", "elementIndex");
                var layout = page.Elements[elementIndex];
                if (!string.IsNullOrEmpty(request.SourcePath) &&
                    !string.Equals(layout.SourcePath, request.SourcePath, StringComparison.Ordinal))
                    throw new DesignerOptionException("ElementSourcePathMismatch",
                        "sourcePath 与 elementIndex 指向的元素不一致。", "sourcePath");
                var owned = new List<PrintElement>();
                for (var index = 0; index < page.RenderElements.Count; index++)
                    if (index < page.RenderElementOwnership.Count &&
                        ReferenceEquals(page.RenderElementOwnership[index], layout))
                        owned.Add(page.RenderElements[index]);
                if (owned.Count != 1)
                    throw new NotSupportedException("所选元素没有唯一的共享码图；SVG 不会省略或合并不明确的输出元素。");
                var svg = EncodeBarcodeSvg(owned[0], dpiX, dpiY,
                    input.Options.MaxOutputBytes, cancellationToken);
                return Results.File(svg, "image/svg+xml; charset=utf-8",
                    $"easyprint-page-{pageIndex + 1}-element-{elementIndex + 1}.svg");
            }
            finally { result.ReleaseRenderElements(); }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    private static byte[] EncodeBarcodeSvg(PrintElement element, int dpiX, int dpiY, int maxBytes,
        CancellationToken cancellationToken)
    {
        if (dpiX <= 0) dpiX = 203;
        if (dpiY <= 0) dpiY = 203;
        using var symbol = BarcodeSvgSymbol.Create(element, dpiX, dpiY, maxBytes, cancellationToken);
        using var output = new BudgetedHostStream("Host.SvgOutput", cancellationToken, maxBytes);
        using var pathLease = PrintResourceScope.Reserve(symbol.PathByteEstimate,
            "Host.SvgPathCharacters");
        var path = symbol.PathData(cancellationToken);
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            CloseOutput = false,
            OmitXmlDeclaration = true,
            Indent = false
        }))
        {
            writer.WriteStartElement("svg", "http://www.w3.org/2000/svg");
            writer.WriteAttributeString("width", ToMillimeters(symbol.Width, symbol.DpiX));
            writer.WriteAttributeString("height", ToMillimeters(symbol.Height, symbol.DpiY));
            writer.WriteAttributeString("viewBox", string.Create(CultureInfo.InvariantCulture,
                $"0 0 {symbol.Width} {symbol.Height}"));
            writer.WriteAttributeString("preserveAspectRatio", "none");
            writer.WriteAttributeString("role", "img");
            writer.WriteAttributeString("aria-label", "Barcode without device HRI");
            writer.WriteStartElement("path");
            writer.WriteAttributeString("fill", "#000");
            writer.WriteAttributeString("d", path);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        return output.ToArray("Host.SvgOutputSnapshot");
    }

    private static string ToMillimeters(int dots, int dpi)
        => (dots * 25.4d / dpi).ToString("0.######", CultureInfo.InvariantCulture) + "mm";

    private sealed class BarcodeSvgSymbol : IDisposable
    {
        private readonly SvgRectBuffer _rectangles;

        private BarcodeSvgSymbol(int width, int height, int dpiX, int dpiY, SvgRectBuffer rectangles,
            long pathByteEstimate)
        {
            Width = width;
            Height = height;
            DpiX = dpiX;
            DpiY = dpiY;
            _rectangles = rectangles;
            PathByteEstimate = pathByteEstimate;
        }

        internal int Width { get; }
        internal int Height { get; }
        internal int DpiX { get; }
        internal int DpiY { get; }
        internal long PathByteEstimate { get; }

        internal static BarcodeSvgSymbol Create(PrintElement element, int dpiX, int dpiY, int maxBytes,
            CancellationToken cancellationToken)
        {
            var rectangles = new SvgRectBuffer(maxBytes);
            int width;
            int height;
            try
            {
                switch (element)
                {
                    case PrintBarcodeElement barcode:
                        AddBarcode(rectangles, barcode, cancellationToken, out width, out height);
                        break;
                    case PrintQrCodeElement qr:
                    {
                        var matrix = EscPosRasterCanvas.EncodeVectorQrMatrix(qr.Data, qr.ErrorLevel, cancellationToken);
                        var quiet = qr.QuietZone ?? PrintQuietZone.Uniform(4 * qr.ModuleSize);
                        AddMatrix(rectangles, matrix, qr.ModuleSize, quiet, cancellationToken);
                        width = checked(matrix.Width * qr.ModuleSize + quiet.Left + quiet.Right);
                        height = checked(matrix.Height * qr.ModuleSize + quiet.Top + quiet.Bottom);
                        Rotate(rectangles, ref width, ref height, qr.Rotation);
                        break;
                    }
                    case PrintGs1CompositeElement composite:
                        AddComposite(rectangles, composite, cancellationToken, out width, out height);
                        break;
                    case PrintImageElement image when image.VectorCodeInfo is not null:
                        AddMatrix(rectangles, image.VectorCodeInfo, cancellationToken);
                        width = image.Width;
                        height = image.Height;
                        Rotate(rectangles, ref width, ref height, image.Rotation);
                        break;
                    default:
                        throw new NotSupportedException("仅支持软件编码的一维码、QR、DataMatrix、PDF417 与 GS1 Composite；SVG 不包含设备 HRI。");
                }
                var pathEstimate = checked(Math.Max(64L, rectangles.Count * 56L));
                if (pathEstimate > maxBytes)
                    throw new PrintResourceLimitException("Host.SvgPath", pathEstimate, 0, maxBytes);
                var rotation = element switch
                {
                    PrintBarcodeElement barcode => barcode.Rotation,
                    PrintQrCodeElement qr => qr.Rotation,
                    PrintGs1CompositeElement composite => composite.Rotation,
                    PrintImageElement image => image.Rotation,
                    _ => RotationAngle.None
                };
                // A quarter turn maps source vertical dots onto the SVG horizontal axis and vice versa.
                var outputDpiX = rotation is RotationAngle.Rotate90 or RotationAngle.Rotate270 ? dpiY : dpiX;
                var outputDpiY = rotation is RotationAngle.Rotate90 or RotationAngle.Rotate270 ? dpiX : dpiY;
                return new BarcodeSvgSymbol(width, height, outputDpiX, outputDpiY, rectangles, pathEstimate);
            }
            catch { rectangles.Dispose(); throw; }
        }

        internal string PathData(CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            foreach (var rectangle in _rectangles.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append('M').Append(rectangle.X).Append(' ').Append(rectangle.Y)
                    .Append('h').Append(rectangle.Width).Append('v').Append(rectangle.Height)
                    .Append('h').Append(-rectangle.Width).Append('z');
            }
            return builder.ToString();
        }

        public void Dispose() => _rectangles.Dispose();
    }

    private readonly record struct SvgRect(int X, int Y, int Width, int Height);

    private sealed class SvgRectBuffer : IDisposable
    {
        private readonly long _maxBytes;
        private IDisposable? _reservation;
        internal List<SvgRect> Items { get; } = new();
        internal int Count => Items.Count;

        internal SvgRectBuffer(long maxBytes) => _maxBytes = maxBytes;

        internal void Add(SvgRect rectangle)
        {
            if (Items.Count == Items.Capacity)
            {
                var nextCapacity = Items.Capacity == 0 ? 32 : checked(Items.Capacity * 2);
                var bytes = checked((long)nextCapacity * 16);
                if (bytes > _maxBytes)
                    throw new PrintResourceLimitException("Host.SvgGeometry", bytes,
                        _reservation == null ? 0 : (long)Items.Capacity * 16, _maxBytes);
                var nextReservation = PrintResourceScope.Reserve(bytes, "Host.SvgGeometry");
                _reservation?.Dispose();
                _reservation = nextReservation;
                Items.Capacity = nextCapacity;
            }
            Items.Add(rectangle);
        }

        public void Dispose() => _reservation?.Dispose();
    }

    private static void AddMatrix(SvgRectBuffer rectangles, BitMatrix matrix, int moduleSize,
        PrintQuietZone quietZone, CancellationToken cancellationToken)
    {
        for (var y = 0; y < matrix.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var x = 0;
            while (x < matrix.Width)
            {
                while (x < matrix.Width && !matrix[x, y]) x++;
                var start = x;
                while (x < matrix.Width && matrix[x, y]) x++;
                if (x > start)
                    rectangles.Add(new SvgRect(checked(quietZone.Left + start * moduleSize),
                        checked(quietZone.Top + y * moduleSize), checked((x - start) * moduleSize), moduleSize));
            }
        }
    }

    private static void AddMatrix(SvgRectBuffer rectangles, PrintVectorCodeInfo code,
        CancellationToken cancellationToken)
    {
        for (var y = 0; y < code.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var x = 0;
            while (x < code.Width)
            {
                while (x < code.Width && !code.IsSet(x, y)) x++;
                var start = x;
                while (x < code.Width && code.IsSet(x, y)) x++;
                if (x > start)
                    rectangles.Add(new SvgRect(checked(code.Left + start * code.ModuleSize),
                        checked(code.Top + y * code.ModuleSize),
                        checked((x - start) * code.ModuleSize), code.ModuleSize));
            }
        }
    }

    private static void AddBarcode(SvgRectBuffer rectangles, PrintBarcodeElement barcode,
        CancellationToken cancellationToken, out int width, out int height)
    {
        var encoded = EscPosRasterCanvas.EncodeVectorBarcodeMatrix(barcode, cancellationToken);
        var module = barcode.NarrowWidth;
        var intrinsicLeft = barcode.Type == BarcodeType.Code49 ? 10 * module : 0;
        var intrinsicRight = barcode.Type == BarcodeType.Code49 ? module : 0;
        var symbolWidth = checked((encoded.Last - encoded.First + 1) * module + intrinsicLeft + intrinsicRight);
        var symbolHeight = barcode.Type switch
        {
            BarcodeType.Code49 => checked(encoded.Matrix.Height * barcode.BarHeight + (encoded.Matrix.Height + 1) * module),
            BarcodeType.Gs1DataBarExpandedStacked or BarcodeType.Gs1DataBarStackedOmnidirectional =>
                EscPosRasterCanvas.MeasureBarcodeHeight(barcode.Type, barcode.Data, barcode.BarHeight, module,
                    cancellationToken, barcode.ExpandedStackedColumns, barcode.ExpandedStackedRows),
            BarcodeType.Gs1DataBarStacked => EscPosRasterCanvas.MeasureBarcodeHeight(barcode.Type,
                barcode.Data, barcode.BarHeight, module, cancellationToken),
            _ => barcode.BarHeight
        };
        var quiet = barcode.QuietZone ?? DefaultBarcodeQuietZone(barcode.Type, module);
        var left = quiet.Left;
        var top = quiet.Top;
        width = checked(symbolWidth + quiet.Left + quiet.Right);
        height = checked(symbolHeight + quiet.Top + quiet.Bottom);

        if (barcode.Type == BarcodeType.Code49)
        {
            AddRect(rectangles, left, top, symbolWidth, module, cancellationToken);
            for (var row = 0; row < encoded.Matrix.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowTop = checked(top + module + row * (barcode.BarHeight + module));
                AddMatrixRow(rectangles, encoded.Matrix, encoded.First, encoded.Last, module,
                    left + intrinsicLeft, row, rowTop, barcode.BarHeight, cancellationToken);
                if (row + 1 < encoded.Matrix.Height)
                    AddRect(rectangles, left + intrinsicLeft, rowTop + barcode.BarHeight,
                        (encoded.Last - encoded.First + 1) * module, module, cancellationToken);
            }
            AddRect(rectangles, left, top + symbolHeight - module, symbolWidth, module, cancellationToken);
        }
        else if (barcode.Type is BarcodeType.Gs1DataBarExpandedStacked or BarcodeType.Gs1DataBarStackedOmnidirectional)
        {
            var y = top;
            for (var row = 0; row < encoded.Matrix.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowHeight = row % 4 == 0 ? barcode.BarHeight : module;
                AddMatrixRow(rectangles, encoded.Matrix, 0, encoded.Matrix.Width - 1, module,
                    left, row, y, rowHeight, cancellationToken);
                y = checked(y + rowHeight);
            }
        }
        else if (barcode.Type == BarcodeType.Gs1DataBarStacked)
        {
            for (var row = 0; row < encoded.Matrix.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowTop = checked(top + (int)((long)row * barcode.BarHeight / encoded.Matrix.Height));
                var rowBottom = checked(top + (int)((long)(row + 1) * barcode.BarHeight / encoded.Matrix.Height));
                AddMatrixRow(rectangles, encoded.Matrix, encoded.First, encoded.Last, module, left,
                    row, rowTop, rowBottom - rowTop, cancellationToken);
            }
        }
        else
        {
            for (var column = encoded.First; column <= encoded.Last; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var black = false;
                for (var row = 0; row < encoded.Matrix.Height; row++)
                    if (encoded.Matrix[column, row]) { black = true; break; }
                if (!black) continue;
                var x = checked(left + (column - encoded.First) * module);
                var y = top;
                var barHeight = barcode.BarHeight;
                if (barcode.Type == BarcodeType.IntelligentMail)
                {
                    var ascender = encoded.Matrix[column, 0];
                    var tracker = encoded.Matrix[column, 1];
                    var descender = encoded.Matrix[column, 2];
                    var centerHeight = Math.Max(1, barHeight / 2);
                    var centerTop = (barHeight - centerHeight) / 2;
                    y = checked(top + (ascender ? 0 : centerTop));
                    barHeight = checked((descender ? barcode.BarHeight : centerTop + centerHeight) - (y - top));
                    if (tracker && !ascender && !descender) { y = top + centerTop; barHeight = centerHeight; }
                }
                else if ((barcode.Type == BarcodeType.Postnet || barcode.Type == BarcodeType.Planet) &&
                         !encoded.Matrix[column, 1])
                {
                    barHeight = Math.Max(1, barcode.BarHeight / 2);
                    y = checked(top + barcode.BarHeight - barHeight);
                }
                AddRect(rectangles, x, y, module, barHeight, cancellationToken);
            }
        }

        Rotate(rectangles, ref width, ref height, barcode.Rotation);
    }

    private static PrintQuietZone DefaultBarcodeQuietZone(BarcodeType type, int module)
    {
        if (type == BarcodeType.Code49) return new PrintQuietZone();
        if (type == BarcodeType.Ean8) return new PrintQuietZone(7 * module, 0, 7 * module, 0);
        if (type == BarcodeType.Ean13) return new PrintQuietZone(11 * module, 0, 7 * module, 0);
        if (type == BarcodeType.UpcA) return new PrintQuietZone(9 * module, 0, 9 * module, 0);
        if (type == BarcodeType.UpcE) return new PrintQuietZone(9 * module, 0, 7 * module, 0);
        if (type is BarcodeType.Gs1DataBarOmnidirectional or BarcodeType.Gs1DataBarTruncated or
            BarcodeType.Gs1DataBarLimited or BarcodeType.Gs1DataBarExpanded or
            BarcodeType.Gs1DataBarExpandedStacked or BarcodeType.Gs1DataBarStacked or
            BarcodeType.Gs1DataBarStackedOmnidirectional)
            return new PrintQuietZone(module, 0, module, 0);
        return new PrintQuietZone(10 * module, 0, 10 * module, 0);
    }

    private static void AddComposite(SvgRectBuffer rectangles, PrintGs1CompositeElement composite,
        CancellationToken cancellationToken, out int width, out int height)
    {
        var symbol = Gs1CompositeEncoder.Encode(composite, cancellationToken);
        var quiet = composite.QuietZone ?? new PrintQuietZone();
        width = checked(symbol.Matrix.Width * composite.ModuleWidth + quiet.Left + quiet.Right);
        height = checked(Enumerable.Range(0, symbol.Matrix.Height)
            .Sum(row => symbol.RowHeight(row, composite)) + quiet.Top + quiet.Bottom);
        var y = quiet.Top;
        for (var row = 0; row < symbol.Matrix.Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowHeight = symbol.RowHeight(row, composite);
            var x = 0;
            while (x < symbol.Matrix.Width)
            {
                while (x < symbol.Matrix.Width && !symbol.Matrix[x, row]) x++;
                var start = x;
                while (x < symbol.Matrix.Width && symbol.Matrix[x, row]) x++;
                if (x > start)
                    rectangles.Add(new SvgRect(checked(quiet.Left + start * composite.ModuleWidth), y,
                        checked((x - start) * composite.ModuleWidth), rowHeight));
            }
            y = checked(y + rowHeight);
        }
        Rotate(rectangles, ref width, ref height, composite.Rotation);
    }

    private static void AddMatrixRow(SvgRectBuffer rectangles, BitMatrix matrix, int first, int last,
        int module, int left, int row, int top, int height, CancellationToken cancellationToken)
    {
        var column = first;
        while (column <= last)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (column <= last && !matrix[column, row]) column++;
            var start = column;
            while (column <= last && matrix[column, row]) column++;
            if (column > start)
                AddRect(rectangles, checked(left + (start - first) * module), top,
                    checked((column - start) * module), height, cancellationToken);
        }
    }

    private static void AddRect(SvgRectBuffer rectangles, int x, int y, int width, int height,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (width > 0 && height > 0) rectangles.Add(new SvgRect(x, y, width, height));
    }

    private static void Rotate(SvgRectBuffer rectangles, ref int width, ref int height, RotationAngle rotation)
    {
        if (rotation == RotationAngle.None) return;
        for (var index = 0; index < rectangles.Items.Count; index++)
        {
            var rectangle = rectangles.Items[index];
            rectangles.Items[index] = rotation switch
            {
                RotationAngle.Rotate90 => new SvgRect(height - rectangle.Y - rectangle.Height,
                    rectangle.X, rectangle.Height, rectangle.Width),
                RotationAngle.Rotate180 => new SvgRect(width - rectangle.X - rectangle.Width,
                    height - rectangle.Y - rectangle.Height, rectangle.Width, rectangle.Height),
                RotationAngle.Rotate270 => new SvgRect(rectangle.Y,
                    width - rectangle.X - rectangle.Width, rectangle.Height, rectangle.Width),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation))
            };
        }
        if (rotation is RotationAngle.Rotate90 or RotationAngle.Rotate270) (width, height) = (height, width);
    }
}
