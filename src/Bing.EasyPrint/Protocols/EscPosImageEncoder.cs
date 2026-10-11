using System;
using System.Threading;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

/// <summary>将逐行单色数据编码为设备选择的图片指令。</summary>
internal static class EscPosImageEncoder
{
    internal static void Validate(int width, int height, EscPosPrinterProfile profile, bool doubleWidth, bool doubleHeight)
    {
        var effectiveWidth = (long)width * (doubleWidth ? 2 : 1);
        if (profile?.PrintableWidthDots is int maximum && effectiveWidth > maximum)
            throw new ArgumentOutOfRangeException(nameof(width), $"图片宽度超过设备 {profile.Name} 的 {maximum} 点可打印区域。");
        if (profile?.ImageCommand == EscPosImageCommand.BitImageEscStar)
        {
            if (doubleWidth || doubleHeight)
                throw new NotSupportedException("ESC * 不接受 GS v 0 的倍宽/倍高标志；请在布局中显式缩放图片。");
            if (width > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width), "ESC * 图片宽度不能超过 65535 列。");
            var outputHeight = ((long)height * profile.VerticalDpi + profile.HorizontalDpi - 1) / profile.HorizontalDpi;
            if (outputHeight > int.MaxValue - profile.BandHeight)
                throw new ArgumentOutOfRangeException(nameof(height), "ESC * 重采样后的图片高度超出可表示范围。");
        }
        else if (profile?.MaxImageBlockBytes is int budget && ((long)width + 7) / 8 > budget)
            throw new ArgumentOutOfRangeException(nameof(width), "单行图片数据超过设备分块预算。");
    }

    internal static void Write(IBufferWriter writer, int width, int height, byte[] data,
        EscPosPrinterProfile profile, bool doubleWidth, bool doubleHeight)
        => Write(writer, width, height, data, profile, doubleWidth, doubleHeight, CancellationToken.None);

    internal static void Write(IBufferWriter writer, int width, int height, byte[] data,
        EscPosPrinterProfile profile, bool doubleWidth, bool doubleHeight, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (profile?.ImageCommand == EscPosImageCommand.BitImageEscStar)
        {
            WriteColumns(writer, width, height, data, profile, cancellationToken);
            return;
        }
        var rowBytes = checked((width + 7) / 8);
        var rowsPerBlock = profile?.MaxImageBlockBytes is int budget ? Math.Min(height, budget / rowBytes) : height;
        var mode = (byte)((doubleWidth ? 1 : 0) | (doubleHeight ? 2 : 0));
        for (var top = 0; top < height; top += rowsPerBlock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = Math.Min(rowsPerBlock, height - top);
            writer.Write(new byte[] { 0x1D, 0x76, 0x30, mode, (byte)rowBytes, (byte)(rowBytes >> 8), (byte)rows, (byte)(rows >> 8) });
            cancellationToken.ThrowIfCancellationRequested();
            if (rows == height) writer.Write(data);
            else
            {
                using var memory = PrintResourceScope.Reserve(checked(rows * rowBytes), "EscPos.RasterBlock");
                var block = new byte[checked(rows * rowBytes)];
                Core.BufferCopy.Copy(data, checked(top * rowBytes), block, 0, block.Length, cancellationToken);
                writer.Write(block);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static void WriteColumns(IBufferWriter writer, int width, int height, byte[] source,
        EscPosPrinterProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rowBytes = (width + 7) / 8;
        var bandHeight = profile.BandHeight;
        var bytesPerColumn = bandHeight / 8;
        var columnsPerBlock = profile.MaxImageBlockBytes is int budget ? Math.Min(width, budget / bytesPerColumn) : width;
        // 输入坐标使用水平 DPI 的正方形点阵。显式选择非正方形设备模式时，
        // 纵向重采样保留黑点，并使进纸长度保持相同的物理尺寸。
        var outputHeight = checked((int)(((long)height * profile.VerticalDpi + profile.HorizontalDpi - 1) / profile.HorizontalDpi));
        if (profile.UnidirectionalBitImage)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(new byte[] { 0x1B, 0x55, 1 });
            cancellationToken.ThrowIfCancellationRequested();
        }
        long fedUnits = 0;
        for (var top = 0; top < outputHeight; top += bandHeight)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = Math.Min(bandHeight, outputHeight - top);
            var targetUnits = ((long)(top + rows) * profile.MotionUnitsPerInch + profile.VerticalDpi / 2) / profile.VerticalDpi;
            var advance = targetUnits - fedUnits;
            // 以累计值取整，避免每带都取整造成长图片的进纸误差不断累积。
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(new byte[] { 0x1B, 0x33, (byte)Math.Min(255, advance) });
            cancellationToken.ThrowIfCancellationRequested();
            for (var left = 0; left < width; left += columnsPerBlock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var columns = Math.Min(columnsPerBlock, width - left);
                using var memory = PrintResourceScope.Reserve(checked(columns * bytesPerColumn), "EscPos.ColumnBlock");
                var block = new byte[checked(columns * bytesPerColumn)];
                for (var column = 0; column < columns; column++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (var row = 0; row < rows; row++)
                    {
                        var outputRow = top + row;
                        var first = (int)((long)outputRow * profile.HorizontalDpi / profile.VerticalDpi);
                        var last = Math.Min(height, (int)Math.Min(int.MaxValue,
                            ((long)(outputRow + 1) * profile.HorizontalDpi + profile.VerticalDpi - 1) / profile.VerticalDpi));
                        var x = left + column;
                        for (var inputRow = first; inputRow < last; inputRow++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if ((source[inputRow * rowBytes + x / 8] & (0x80 >> (x % 8))) != 0)
                            {
                                block[column * bytesPerColumn + row / 8] |= (byte)(0x80 >> (row % 8));
                                break;
                            }
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                writer.Write(new byte[] { 0x1B, 0x2A, (byte)profile.BitImageMode, (byte)columns, (byte)(columns >> 8) });
                cancellationToken.ThrowIfCancellationRequested();
                writer.Write(block);
                cancellationToken.ThrowIfCancellationRequested();
            }
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(new byte[] { 0x0A });
            cancellationToken.ThrowIfCancellationRequested();
            for (var remaining = advance - Math.Min(255, advance); remaining > 0;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var feed = (byte)Math.Min(255, remaining);
                writer.Write(new byte[] { 0x1B, 0x4A, feed });
                cancellationToken.ThrowIfCancellationRequested();
                remaining -= feed;
            }
            fedUnits = targetUnits;
        }
        cancellationToken.ThrowIfCancellationRequested();
        writer.Write(new byte[] { 0x1B, 0x32 });
        cancellationToken.ThrowIfCancellationRequested();
        if (profile.UnidirectionalBitImage)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write(new byte[] { 0x1B, 0x55, 0 });
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
