using System;
using System.Collections.Generic;
using System.Threading;
using Bing.EasyPrint.Protocols;

namespace Bing.EasyPrint.Printing;

/// <summary>Host 同源光栅预览结果；像素为 MSB 优先的单色位图。</summary>
internal sealed class PrintRasterPreviewResult : IDisposable
{
    private IDisposable _rasterLease;

    internal PrintRasterPreviewResult(int width, int height, int rowBytes, byte[] raster, IDisposable rasterLease)
    {
        Width = width;
        Height = height;
        RowBytes = rowBytes;
        Raster = raster;
        _rasterLease = rasterLease;
    }

    internal int Width { get; }
    internal int Height { get; }
    internal int RowBytes { get; }
    internal byte[] Raster { get; }

    public void Dispose()
    {
        var lease = Interlocked.Exchange(ref _rasterLease, null);
        lease?.Dispose();
    }
}

/// <summary>
/// 复用 Core 现有编码/绘制路径，将已布局的打印元素渲染为同源单色位图。
/// </summary>
/// <remarks>
/// 该入口只用于 Host 软件位图预览和 PNG 导出，不生成传输指令，也不模拟固件字体。
/// 设备字体文本会明确拒绝，避免用主机字体冒充设备像素。
/// </remarks>
internal static class PrintRasterPreview
{
    internal static PrintRasterPreviewResult RenderElements(IReadOnlyList<PrintElement> elements, int width,
        int height, RasterFontCatalog fonts, int humanReadableFontSize, CancellationToken cancellationToken)
        => RenderElements(elements, 0, 0, width, height, fonts, humanReadableFontSize, cancellationToken);

    internal static PrintRasterPreviewResult RenderElements(IReadOnlyList<PrintElement> elements, int originX,
        int originY, int width, int height, RasterFontCatalog fonts, int humanReadableFontSize,
        CancellationToken cancellationToken)
        => Render(elements, width, height, originX, originY, fonts, humanReadableFontSize, cancellationToken);

    internal static PrintRasterPreviewResult RenderElement(PrintElement element, int originX, int originY,
        int width, int height, RasterFontCatalog fonts, int humanReadableFontSize,
        CancellationToken cancellationToken)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        return Render(new[] { element }, width, height, originX, originY, fonts, humanReadableFontSize,
            cancellationToken);
    }

    private static PrintRasterPreviewResult Render(IReadOnlyList<PrintElement> elements, int width, int height,
        int originX, int originY, RasterFontCatalog fonts, int humanReadableFontSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (humanReadableFontSize <= 0 || humanReadableFontSize > 255)
            throw new ArgumentOutOfRangeException(nameof(humanReadableFontSize));
        byte[] bytes;
        using (var operation = PrintResourceScope.BeginTemporary())
        {
            var canvas = new EscPosRasterCanvas(width, height, fonts, cancellationToken);
            for (var index = 0; index < elements.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = elements[index];
                if (element == null) continue;
                var x = checked(element.X - originX);
                var y = checked(element.Y - originY);
                switch (element)
                {
                    case PrintTextElement text:
                        if (!string.IsNullOrEmpty(text.DeviceFontName))
                            throw new NotSupportedException(
                                "设备字体文本无法生成像素一致的软件位图；请改用主机 Raster 字体或查看布局示意。");
                        canvas.DrawText(x, y, text.Text, text.FontSize, (int)text.Rotation, text.Bold,
                            text.Reverse, text.Underline, text.FontFamily ?? HostFontName(text.Font));
                        break;
                    case PrintLineElement line:
                        canvas.DrawLine(x, y, checked(line.EndX - originX), checked(line.EndY - originY),
                            line.LineWidth);
                        break;
                    case PrintRectangleElement rectangle:
                        canvas.DrawRectangle(x, y, rectangle.Width, rectangle.Height, rectangle.LineWidth,
                            rectangle.Filled);
                        break;
                    case PrintImageElement image:
                        using (var rotated = PrintImageRotation.Prepare(image, cancellationToken))
                            canvas.DrawRaster(x, y, rotated.Width, rotated.Height, rotated.Raster);
                        break;
                    case PrintBarcodeElement barcode:
                        canvas.DrawBarcodeWithColumns(barcode.Type, x, y, barcode.Data, barcode.BarHeight,
                            barcode.NarrowWidth, barcode.HumanReadable == true, barcode.HumanReadableReserveHeight,
                            null, humanReadableFontSize, (int)barcode.Rotation, barcode.ExpandedStackedColumns,
                            barcode.ExpandedStackedRows);
                        break;
                    case PrintQrCodeElement qr:
                        canvas.DrawQrCode(x, y, qr.Data, qr.ModuleSize, qr.ErrorLevel, (int)qr.Rotation);
                        break;
                    case PrintGs1CompositeElement composite:
                    {
                        var image = Gs1CompositeEncoder.Rasterize(composite, cancellationToken);
                        canvas.DrawRaster(x, y, image.OutputWidth, image.OutputHeight, image.Raster);
                        break;
                    }
                    default:
                        throw new NotSupportedException(
                            $"元素类型 {element.GetType().Name} 暂不支持软件位图预览。");
                }
            }
            bytes = canvas.ToBytes(cancellationToken);
        }
        var rasterLease = PrintResourceScope.Reserve(bytes.LongLength, "Host.RasterPreview");
        return new PrintRasterPreviewResult(width, height, (width + 7) / 8, bytes, rasterLease);
    }

    private static string HostFontName(PrintFont font)
        => font != null && font.Kind == PrintFontKind.Host ? font.Name : null;
}
