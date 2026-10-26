using System;
using ZXing.Common;

namespace Bing.EasyPrint.Printing;

/// <summary>仅供 Host 导出使用的已编码模块矩阵快照。</summary>
internal sealed class PrintVectorCodeInfo
{
    private readonly byte[] _modules;

    internal PrintVectorCodeInfo(string format, BitMatrix matrix, int moduleSize, int canvasWidth,
        int canvasHeight, int left, int top, bool gs1)
    {
        if (string.IsNullOrWhiteSpace(format)) throw new ArgumentException("码制不能为空。", nameof(format));
        if (matrix == null) throw new ArgumentNullException(nameof(matrix));
        if (moduleSize <= 0 || canvasWidth <= 0 || canvasHeight <= 0 || left < 0 || top < 0)
            throw new ArgumentOutOfRangeException(nameof(moduleSize));
        var symbolWidth = checked(matrix.Width * moduleSize);
        var symbolHeight = checked(matrix.Height * moduleSize);
        if ((long)left + symbolWidth > canvasWidth || (long)top + symbolHeight > canvasHeight)
            throw new ArgumentException("模块矩阵超出码图画布。", nameof(matrix));

        Format = format;
        Width = matrix.Width;
        Height = matrix.Height;
        ModuleSize = moduleSize;
        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
        Left = left;
        Top = top;
        Right = canvasWidth - left - symbolWidth;
        Bottom = canvasHeight - top - symbolHeight;
        Gs1 = gs1;
        RowBytes = checked((matrix.Width + 7) / 8);
        _modules = new byte[checked(RowBytes * matrix.Height)];
        for (var y = 0; y < matrix.Height; y++)
            for (var x = 0; x < matrix.Width; x++)
                if (matrix[x, y]) _modules[y * RowBytes + x / 8] |= (byte)(0x80 >> (x & 7));
        PrintResourceScope.Retain(_modules.LongLength, "Document.VectorCodeMatrix");
    }

    internal string Format { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal int RowBytes { get; }
    internal int ModuleSize { get; }
    internal int CanvasWidth { get; }
    internal int CanvasHeight { get; }
    internal int Left { get; }
    internal int Top { get; }
    internal int Right { get; }
    internal int Bottom { get; }
    internal bool Gs1 { get; }
    internal long StorageBytes => checked(64L + _modules.LongLength +
        Core.PrintStorageBudget.TextBytes(Format));
    internal bool IsSet(int x, int y) => (_modules[y * RowBytes + x / 8] & (0x80 >> (x & 7))) != 0;
}
