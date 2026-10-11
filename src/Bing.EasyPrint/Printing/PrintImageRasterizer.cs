using System;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace Bing.EasyPrint.Printing;

/// <summary>将图片按 CPCL 兼容的抖动规则转换为 MSB 优先单色位图。</summary>
public static class PrintImageRasterizer
{
    public static byte[] ToCpclMonochrome(Image source)
        => ToCpclMonochrome(source, CancellationToken.None);

    /// <summary>可取消的 CPCL 抖动转换；第三方图像处理在调用前后检查取消。</summary>
    public static byte[] ToCpclMonochrome(Image source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source == null) throw new ArgumentNullException(nameof(source));
        using (var memory = PrintResourceScope.Reserve(checked((long)source.Width * source.Height * 4), "Image.CpclRgba"))
        using (var image = source.CloneAs<Rgba32>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            image.Mutate(context =>
            {
                context.Grayscale(GrayscaleMode.Bt601);
                context.BinaryDither(ErrorDither.FloydSteinberg);
            });
            cancellationToken.ThrowIfCancellationRequested();
            var rowBytes = checked((int)(((long)image.Width + 7) / 8));
            return Core.BudgetedBuffer.Create(checked(rowBytes * image.Height), "Image.CpclMonochrome", result =>
            {
                image.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < accessor.Height; y++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var pixels = accessor.GetRowSpan(y);
                        var offset = y * rowBytes;
                        for (var x = 0; x < pixels.Length; x++)
                        {
                            if ((x & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                            if (pixels[x].R < 128)
                                result[offset + x / 8] |= (byte)(0x80 >> (x % 8));
                        }
                    }
                });
                cancellationToken.ThrowIfCancellationRequested();
            });
        }
    }
}
