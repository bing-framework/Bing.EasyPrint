using System;
using System.Threading;

namespace Bing.EasyPrint.Printing;

/// <summary>内置 Provider 共用的单色图片旋转结果，临时位图预留在指令复制或画布绘制后释放。</summary>
internal readonly struct PrintImageRotation : IDisposable
{
    internal readonly int Width, Height, RowBytes;
    internal readonly byte[] Raster;
    private readonly IDisposable _reservation;

    private PrintImageRotation(PrintImageElement image, byte[] raster, IDisposable reservation)
    {
        Width = image.OutputWidth;
        Height = image.OutputHeight;
        RowBytes = image.OutputRowBytes;
        Raster = raster;
        _reservation = reservation;
    }

    internal static PrintImageRotation Prepare(PrintImageElement image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (image.Rotation == RotationAngle.None)
            return new PrintImageRotation(image, image.RasterData, null);

        var length = checked(image.OutputRowBytes * image.OutputHeight);
        var reservation = PrintResourceScope.Reserve(length, "Image.RotatedMonochrome");
        try
        {
            var output = new byte[length];
            var input = image.RasterData;
            for (var y = 0; y < image.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < image.Width; x++)
                {
                    if ((x & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if ((input[y * image.RowBytes + x / 8] & (0x80 >> (x % 8))) == 0) continue;
                    int dx, dy;
                    switch (image.Rotation)
                    {
                        case RotationAngle.Rotate90: dx = image.Height - 1 - y; dy = x; break;
                        case RotationAngle.Rotate180: dx = image.Width - 1 - x; dy = image.Height - 1 - y; break;
                        case RotationAngle.Rotate270: dx = y; dy = image.Width - 1 - x; break;
                        default: throw new ArgumentOutOfRangeException(nameof(image));
                    }
                    output[dy * image.OutputRowBytes + dx / 8] |= (byte)(0x80 >> (dx % 8));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new PrintImageRotation(image, output, reservation);
        }
        catch { reservation.Dispose(); throw; }
    }

    public void Dispose() => _reservation?.Dispose();
}
