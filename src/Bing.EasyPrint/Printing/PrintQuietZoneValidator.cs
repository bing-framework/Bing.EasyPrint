using System;
using System.Collections.Generic;
using System.Threading;
using Bing.EasyPrint.Protocols;

namespace Bing.EasyPrint.Printing;

/// <summary>在生成任何字节前检查显式静区的页面边界及相邻元素占位。</summary>
internal static class PrintQuietZoneValidator
{
    internal static void Validate(PrintPage page, int pageIndex, PrintProtocol protocol, bool supported)
        => Validate(page, pageIndex, protocol, supported, CancellationToken.None, null);

    internal static void Validate(PrintPage page, int pageIndex, PrintProtocol protocol, bool supported,
        CancellationToken cancellationToken, RasterFontCatalog rasterFonts = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var protectedCodes = new List<int>();
        for (var index = 0; index < page.Elements.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var element = page.Elements[index];
            var zone = Zone(element);
            var hasIntrinsicCode49Zones = HasIntrinsicCode49Zones(element, protocol, supported);
            cancellationToken.ThrowIfCancellationRequested();
            if (!hasIntrinsicCode49Zones && (zone == null || zone.IsEmpty())) continue;
            if (zone != null && !zone.IsEmpty() && !supported)
                throw new PrintCapabilityException(protocol, Path(page, pageIndex, index),
                    new NotSupportedException("此模式不支持非零静区；请显式选择坐标协议或 ESC/POS Raster。"), pageIndex, index);
            protectedCodes.Add(index);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (protectedCodes.Count == 0) return;

        // 未配置静区的页面不进入此检查，保持历史原生坐标和未知字体行为。
        var occupied = new IReadOnlyList<QuietZoneArea>[page.Elements.Count];
        for (var index = 0; index < page.Elements.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { occupied[index] = Occupied(page.Elements[index], protocol, cancellationToken, rasterFonts); }
            catch (NotSupportedException error)
            { throw new PrintCapabilityException(protocol, Path(page, pageIndex, index), error, pageIndex, index); }
            catch (ArgumentException error)
            { throw new PrintElementValidationException(protocol, Path(page, pageIndex, index), error, pageIndex, index); }
            cancellationToken.ThrowIfCancellationRequested();
        }

        foreach (var index in protectedCodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var element = page.Elements[index];
            var body = occupied[index][0];
            var zone = Zone(element) ?? new PrintQuietZone();
            var rotation = protocol == PrintProtocol.Cpcl
                ? (RotationAngle)((360 - (int)element.Rotation) % 360) : element.Rotation;
            zone = zone.Rotate(rotation);
            cancellationToken.ThrowIfCancellationRequested();
            if (body.X - zone.Left < 0 || body.Y - zone.Top < 0 ||
                body.Right + zone.Right > page.Width || body.Bottom + zone.Bottom > page.Height)
                throw Invalid("码图及其显式静区超出打印页边界。", index);
            var areas = new List<QuietZoneArea>(zone.Areas(body.X, body.Y, body.Width, body.Height));
            if (HasIntrinsicCode49Zones(element, protocol, supported))
                areas.AddRange(IntrinsicCode49QuietZones((PrintBarcodeElement)element,
                    cancellationToken));
            foreach (var area in areas)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (area.X < 0 || area.Y < 0 || area.Right > page.Width || area.Bottom > page.Height)
                    throw Invalid("码图的显式静区超出打印页边界。", index);
                for (var otherIndex = 0; otherIndex < occupied.Length; otherIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (otherIndex == index) continue;
                    foreach (var other in occupied[otherIndex])
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (area.Intersects(other))
                            throw Invalid("码图的显式静区与元素 " + Path(page, pageIndex, otherIndex) + " 的占位相交。", index);
                    }
                }
            }
        }

        PrintElementValidationException Invalid(string message, int index)
            => new PrintElementValidationException(protocol, Path(page, pageIndex, index),
                new ArgumentException(message), pageIndex, index);
    }

    private static bool HasIntrinsicCode49Zones(PrintElement element, PrintProtocol protocol, bool supported)
        => supported && protocol == PrintProtocol.EscPos &&
           element is PrintBarcodeElement barcode && barcode.Type == BarcodeType.Code49;

    private static IReadOnlyList<QuietZoneArea> IntrinsicCode49QuietZones(PrintBarcodeElement barcode,
        CancellationToken cancellationToken)
    {
        var moduleWidth = barcode.NarrowWidth;
        var width = EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code49, barcode.Data,
            moduleWidth, cancellationToken);
        var height = EscPosRasterCanvas.MeasureBarcodeHeight(BarcodeType.Code49, barcode.Data,
            barcode.BarHeight, moduleWidth, cancellationToken);
        var leftWidth = checked(10L * moduleWidth);
        var rightWidth = (long)moduleWidth;
        var interiorTop = (long)moduleWidth;
        var interiorHeight = height - 2L * moduleWidth;
        var areas = new[]
        {
            new QuietZoneArea(barcode.X, checked(barcode.Y + interiorTop), leftWidth, interiorHeight),
            new QuietZoneArea(checked((long)barcode.X + width - rightWidth),
                checked(barcode.Y + interiorTop), rightWidth, interiorHeight)
        };
        var rotation = barcode.Rotation;
        if (rotation == RotationAngle.None) return areas;

        var rotated = new QuietZoneArea[areas.Length];
        for (var index = 0; index < areas.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var area = areas[index];
            var relativeX = area.X - barcode.X;
            var relativeY = area.Y - barcode.Y;
            switch (rotation)
            {
                case RotationAngle.Rotate90:
                    rotated[index] = new QuietZoneArea(
                        checked((long)barcode.X + height - relativeY - area.Height),
                        checked((long)barcode.Y + relativeX), area.Height, area.Width);
                    break;
                case RotationAngle.Rotate180:
                    rotated[index] = new QuietZoneArea(
                        checked((long)barcode.X + width - relativeX - area.Width),
                        checked((long)barcode.Y + height - relativeY - area.Height),
                        area.Width, area.Height);
                    break;
                case RotationAngle.Rotate270:
                    rotated[index] = new QuietZoneArea(
                        checked((long)barcode.X + relativeY),
                        checked((long)barcode.Y + width - relativeX - area.Width),
                        area.Height, area.Width);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(barcode.Rotation));
            }
        }
        return rotated;
    }

    private static string Path(PrintPage page, int pageIndex, int index)
        => page.Elements[index].SourcePath ?? $"Pages[{pageIndex}].Elements[{index}]";

    private static PrintQuietZone Zone(PrintElement element)
        => element is PrintBarcodeElement barcode ? barcode.QuietZone :
            element is PrintQrCodeElement qr ? qr.QuietZone : null;

    private static IReadOnlyList<QuietZoneArea> Occupied(PrintElement element, PrintProtocol protocol,
        CancellationToken cancellationToken, RasterFontCatalog rasterFonts)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (element)
        {
            case PrintBarcodeElement barcode:
            {
                cancellationToken.ThrowIfCancellationRequested();
                var width = protocol == PrintProtocol.EscPos
                    ? Math.Max(barcode.Width, EscPosRasterCanvas.MeasureBarcodeWidth(barcode.Type, barcode.Data,
                        barcode.NarrowWidth, cancellationToken))
                    : PrintProtocolProviderBase.MeasureBarcodeWidth(barcode, protocol, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (width <= 0)
                    throw new ArgumentException("含显式静区的页面要求声明无法测量的原生条码 reservedWidth。" );
                if ((protocol == PrintProtocol.Tspl || protocol == PrintProtocol.Zpl) &&
                    barcode.HumanReadable != false && barcode.HumanReadableReserveHeight <= 0)
                    throw new ArgumentException("含显式静区的页面要求为启用或默认启用可读文字的条码声明 HumanReadableReserveHeight，或关闭可读文字。" );
                var height = protocol == PrintProtocol.EscPos && barcode.Type == BarcodeType.Code49
                    ? checked(EscPosRasterCanvas.MeasureBarcodeHeight(barcode.Type, barcode.Data,
                        barcode.BarHeight, barcode.NarrowWidth, cancellationToken) +
                        barcode.HumanReadableReserveHeight)
                    : barcode.Height;
                return new[] { Body(barcode, width, height, protocol) };
            }
            case PrintQrCodeElement qr:
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (protocol == PrintProtocol.Cpcl && qr.Rotation != RotationAngle.None)
                    throw new NotSupportedException("显式静区的 CPCL 二维码仅支持无旋转输出。" );
                var size = protocol == PrintProtocol.Cpcl ? 0
                    : EscPosRasterCanvas.MeasureQrCodeSize(qr.Data, qr.ModuleSize, qr.ErrorLevel,
                        cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var width = Math.Max(qr.Width, size);
                var height = Math.Max(qr.Height, size);
                if (width <= 0 || height <= 0)
                    throw new ArgumentException("含显式静区的页面要求声明 CPCL 二维码编码区 width 和 height。" );
                return new[] { Body(qr, width, height, protocol) };
            }
            case PrintTextElement text:
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (text.Text.Length == 0) return Array.Empty<QuietZoneArea>();
                if (protocol == PrintProtocol.EscPos)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var ink = EscPosRasterCanvas.MeasureTextInkBounds(text.Text, text.FontSize,
                        text.Bold, text.Underline, text.FontFamily, long.MaxValue, rasterFonts,
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ink.IsEmpty) return Array.Empty<QuietZoneArea>();

                    // Raster 旋转文字以旋转后的紧致墨迹框左上角为坐标；未旋转文字仍保留
                    // DrawText 的字体 bearing 偏移，避免改变既有静区边界语义。
                    var quarterTurn = text.Rotation == RotationAngle.Rotate90 ||
                                      text.Rotation == RotationAngle.Rotate270;
                    var width = quarterTurn ? ink.Height : ink.Width;
                    var height = quarterTurn ? ink.Width : ink.Height;
                    return text.Rotation == RotationAngle.None
                        ? new[] { new QuietZoneArea((long)text.X + ink.Left, (long)text.Y + ink.Top,
                            ink.Width, ink.Height) }
                        : new[] { new QuietZoneArea(text.X, text.Y, width, height) };
                }
                cancellationToken.ThrowIfCancellationRequested();
                var extent = PrintProtocolProviderBase.MeasureTextExtent(text, protocol);
                cancellationToken.ThrowIfCancellationRequested();
                if (extent.Width <= 0 || extent.Height <= 0)
                    throw new ArgumentException("含显式静区的页面要求声明无法测量的原生文字 width 和 height。" );
                return new[] { Body(text, extent.Width, extent.Height, protocol) };
            }
            case PrintImageElement image:
                cancellationToken.ThrowIfCancellationRequested();
                // 原生图片指令以整字节发送行宽，填充位也可能含有调用方提供的黑点。
                return new[] { new QuietZoneArea(image.X, image.Y,
                    protocol == PrintProtocol.EscPos || image.Rotation != RotationAngle.None
                        ? image.OutputWidth : image.RowBytes * 8L, image.OutputHeight) };
            case PrintLineElement line:
            {
                cancellationToken.ThrowIfCancellationRequested();
                var radius = protocol == PrintProtocol.EscPos || protocol == PrintProtocol.Cpcl ||
                    line.X != line.EndX && line.Y != line.EndY ? line.LineWidth / 2 : 0;
                var width = (long)Math.Abs(line.X - line.EndX) + 1 + 2L * radius;
                var height = (long)Math.Abs(line.Y - line.EndY) + 1 + 2L * radius;
                if (radius == 0 && line.X == line.EndX) width = line.LineWidth;
                if (radius == 0 && line.Y == line.EndY) height = line.LineWidth;
                return new[] { new QuietZoneArea((long)Math.Min(line.X, line.EndX) - radius,
                    (long)Math.Min(line.Y, line.EndY) - radius, width, height) };
            }
            case PrintRectangleElement rectangle:
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rectangle.Filled)
                    return new[] { new QuietZoneArea(rectangle.X, rectangle.Y,
                        rectangle.Width + (protocol == PrintProtocol.Cpcl && !rectangle.ExactFillBounds ? 1L : 0),
                        rectangle.Height + (protocol == PrintProtocol.Cpcl && !rectangle.ExactFillBounds ? 1L : 0)) };
                // 只计算四条边，允许在外部边框中放置带静区的码图。
                // CPCL/TSPL 的原生 BOX 还绘制调用中 x + Width、y + Height 的端点。
                var inclusiveEnd = protocol == PrintProtocol.Cpcl || protocol == PrintProtocol.Tspl ? 1L : 0;
                var radius = protocol == PrintProtocol.EscPos || protocol == PrintProtocol.Cpcl ? rectangle.LineWidth / 2 : 0;
                var left = (long)rectangle.X - radius;
                var top = (long)rectangle.Y - radius;
                var stroke = radius > 0 ? 2L * radius + 1 : rectangle.LineWidth;
                var width = rectangle.Width + inclusiveEnd + 2L * radius;
                var height = rectangle.Height + inclusiveEnd + 2L * radius;
                return new[] {
                    new QuietZoneArea(left, top, width, stroke),
                    new QuietZoneArea(left, (long)rectangle.Y + rectangle.Height + inclusiveEnd - 1 - radius, width, stroke),
                    new QuietZoneArea(left, top, stroke, height),
                    new QuietZoneArea((long)rectangle.X + rectangle.Width + inclusiveEnd - 1 - radius, top, stroke, height)
                };
            }
            default:
                throw new NotSupportedException("含显式静区的页面不能校验未知元素的占位。" );
        }
    }

    private static QuietZoneArea Body(PrintElement element, long width, long height, PrintProtocol protocol)
    {
        long x = element.X, y = element.Y;
        var quarterTurn = element.Rotation == RotationAngle.Rotate90 || element.Rotation == RotationAngle.Rotate270;
        if (protocol == PrintProtocol.Cpcl)
        {
            // CPCL 保留原生逆时针锚点，模板适配器已经把统一左上角换算到该锚点。
            if (element.Rotation == RotationAngle.Rotate90) y -= width;
            else if (element.Rotation == RotationAngle.Rotate180) { x -= width; y -= height; }
            else if (element.Rotation == RotationAngle.Rotate270) x -= height;
        }
        return new QuietZoneArea(x, y, quarterTurn ? height : width, quarterTurn ? width : height);
    }
}
