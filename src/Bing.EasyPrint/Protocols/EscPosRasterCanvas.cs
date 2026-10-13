using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using Bing.EasyPrint;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;
using SixLabors.Fonts;
using SixLabors.Fonts.Unicode;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ZXing;
using ZXing.Common;
using ZXing.OneD;
using ZXing.QrCode.Internal;

namespace Bing.EasyPrint.Protocols;

/// <summary>
/// 描述 ESC/POS Raster 文字实际产生的黑色像素范围。
/// </summary>
/// <remarks>
/// 坐标相对于传给 <see cref="EscPosRasterCanvas.DrawText(int, int, string, int, bool, bool, string)"/>
/// 的原点，并使用左闭右开、上闭下开的范围约定。该范围表示已光栅化的 ink，
/// 不包含没有像素的字体 advance 空间。
/// </remarks>
public readonly struct EscPosRasterTextBounds
{
    /// <summary>相对于绘制原点的左边界。</summary>
    public int Left { get; }

    /// <summary>相对于绘制原点的上边界。</summary>
    public int Top { get; }

    /// <summary>实际黑色像素范围的宽度。</summary>
    public int Width { get; }

    /// <summary>实际黑色像素范围的高度。</summary>
    public int Height { get; }

    /// <summary>相对于绘制原点的右边界（不包含该列）。</summary>
    public int Right => checked(Left + Width);

    /// <summary>相对于绘制原点的下边界（不包含该行）。</summary>
    public int Bottom => checked(Top + Height);

    /// <summary>获取是否没有可见黑色像素。</summary>
    public bool IsEmpty => Width == 0 || Height == 0;

    internal EscPosRasterTextBounds(int left, int top, int width, int height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }
}

/// <summary>用于 ESC/POS Raster 输出的单色位图画布。</summary>
public sealed class EscPosRasterCanvas
{
    internal const int DefaultHumanReadableFontSize = 12;
    private const int Code49LeadingQuietZoneModules = 10;
    private const int Code49TrailingQuietZoneModules = 1;
    private readonly byte[] _bytes;
    private readonly RasterFontCatalog _fonts;
    private readonly CancellationToken _cancellationToken;
    private readonly List<QuietZoneArea> _quietZones = new List<QuietZoneArea>();
    private int _transactionDepth;
    private byte[] _transactionBytes;
    private IDisposable _transactionReservation;
    private int _transactionQuietZoneCount;

    /// <summary>画布宽度。</summary>
    public int Width { get; }
    /// <summary>画布高度。</summary>
    public int Height { get; }
    /// <summary>每行字节数。</summary>
    public int RowBytes { get; }

    public EscPosRasterCanvas(int width, int height) : this(width, height, null, CancellationToken.None) { }

    /// <summary>使用固定字体配置创建画布；配置为空时保留系统字体选择。</summary>
    public EscPosRasterCanvas(int width, int height, RasterFontCatalog fonts)
        : this(width, height, fonts, CancellationToken.None) { }

    /// <summary>使用固定字体配置和取消令牌创建画布。</summary>
    public EscPosRasterCanvas(int width, int height, RasterFontCatalog fonts,
        CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        ThrowIfCancellationRequested();
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        _fonts = fonts;
        RowBytes = (width + 7) / 8;
        _bytes = BudgetedBuffer.Allocate(checked(RowBytes * height), "Raster.Canvas");
    }

    /// <summary>设置一个黑色像素；越界坐标会被忽略。</summary>
    public void SetPixel(int x, int y, bool black = true)
    {
        ThrowIfCancellationRequested();
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        if (black) EnsureBlackPixelAllowed(x, y);
        SetPixelCore(x, y, black);
    }

    /// <summary>绘制线段。</summary>
    public void DrawLine(int x0, int y0, int x1, int y1, int thickness = 1)
        => ExecuteDrawing(() => DrawLineCore(x0, y0, x1, y1, thickness));

    private void DrawLineCore(int x0, int y0, int x1, int y1, int thickness)
    {
        if (thickness <= 0) throw new ArgumentOutOfRangeException(nameof(thickness));
        EnsureStrokeFits(x0, y0, x1, y1, thickness, nameof(thickness));
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            ThrowIfCancellationRequested();
            DrawBrush(x0, y0, thickness);
            if (x0 == x1 && y0 == y1) break;
            var twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    /// <summary>绘制矩形边框或填充矩形。</summary>
    public void DrawRectangle(int x, int y, int width, int height, int thickness = 1, bool filled = false)
        => ExecuteDrawing(() => DrawRectangleCore(x, y, width, height, thickness, filled));

    private void DrawRectangleCore(int x, int y, int width, int height, int thickness, bool filled)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
        if (thickness <= 0) throw new ArgumentOutOfRangeException(nameof(thickness));
        if (filled)
            EnsureFits(x, y, width, height, nameof(width));
        else
            EnsureStrokeFits(x, y, (long)x + width - 1, (long)y + height - 1, thickness,
                nameof(thickness));
        if (filled)
        {
            for (var row = y; row < y + height; row++)
            {
                ThrowIfCancellationRequested();
                for (var column = x; column < x + width; column++) SetPixel(column, row);
            }
            return;
        }
        DrawLineCore(x, y, x + width - 1, y, thickness);
        DrawLineCore(x, y + height - 1, x + width - 1, y + height - 1, thickness);
        DrawLineCore(x, y, x, y + height - 1, thickness);
        DrawLineCore(x + width - 1, y, x + width - 1, y + height - 1, thickness);
    }

    /// <summary>将另一张 MSB 优先单色位图复制到画布。</summary>
    public void DrawRaster(int x, int y, int width, int height, byte[] raster)
        => ExecuteDrawing(() => DrawRasterCore(x, y, width, height, raster));

    private void DrawRasterCore(int x, int y, int width, int height, byte[] raster)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
        if (raster == null) throw new ArgumentNullException(nameof(raster));
        EnsureFits(x, y, width, height, nameof(raster));
        var rowBytesLong = ((long)width + 7) / 8;
        var expectedLength = rowBytesLong * height;
        if (expectedLength > int.MaxValue || raster.LongLength != expectedLength)
            throw new ArgumentException("图片数据长度与尺寸不匹配。", nameof(raster));
        var rowBytes = (int)rowBytesLong;
        for (var row = 0; row < height; row++)
        {
            ThrowIfCancellationRequested();
            for (var column = 0; column < width; column++)
                SetPixel(x + column, y + row, (raster[row * rowBytes + column / 8] & (0x80 >> (column & 7))) != 0);
        }
    }

    // Copy a fully rendered symbol through the same pixel path as other drawing
    // operations. This keeps quiet-zone protection and transaction rollback active
    // while allowing symbols with non-square bounds to rotate around their visual box.
    private void DrawRotatedCanvasCore(int x, int y, EscPosRasterCanvas source, int rotation)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        ValidateRotation(rotation);
        var outputWidth = rotation == 90 || rotation == 270 ? source.Height : source.Width;
        var outputHeight = rotation == 90 || rotation == 270 ? source.Width : source.Height;
        EnsureFits(x, y, outputWidth, outputHeight, nameof(source));

        for (var sourceY = 0; sourceY < source.Height; sourceY++)
        {
            ThrowIfCancellationRequested();
            for (var sourceX = 0; sourceX < source.Width; sourceX++)
            {
                ThrowIfCancellationRequested();
                if (!source.IsBlack(sourceX, sourceY)) continue;
                int targetX;
                int targetY;
                switch (rotation)
                {
                    case 90:
                        targetX = outputWidth - 1 - sourceY;
                        targetY = sourceX;
                        break;
                    case 180:
                        targetX = outputWidth - 1 - sourceX;
                        targetY = outputHeight - 1 - sourceY;
                        break;
                    case 270:
                        targetX = sourceY;
                        targetY = outputHeight - 1 - sourceX;
                        break;
                    default:
                        targetX = sourceX;
                        targetY = sourceY;
                        break;
                }
                SetPixel(checked(x + targetX), checked(y + targetY));
            }
        }
    }

    /// <summary>判断是否有稳定的主机光栅编码实现；邮政制式按各自主机编码器输出。</summary>
    internal static bool SupportsBarcodeType(BarcodeType type)
        => type == BarcodeType.Code11 || type == BarcodeType.Code128 || type == BarcodeType.Code39 ||
           type == BarcodeType.Code93 || type == BarcodeType.Ean8 ||
           type == BarcodeType.Ean13 || type == BarcodeType.UpcA ||
           type == BarcodeType.UpcE || type == BarcodeType.Codabar ||
           type == BarcodeType.I2OF5 || type == BarcodeType.I2OF5C ||
           type == BarcodeType.I2OF5G || type == BarcodeType.Msi ||
           type == BarcodeType.Plessey ||
           type == BarcodeType.UccEan128 || type == BarcodeType.Postnet || type == BarcodeType.Fim ||
           type == BarcodeType.Planet || type == BarcodeType.IntelligentMail ||
           type == BarcodeType.Itf14 ||
           type == BarcodeType.Code128Auto || type == BarcodeType.Telepen || type == BarcodeType.Telepenn ||
           type == BarcodeType.ChinaPost ||
           type == BarcodeType.Gs1DataBarOmnidirectional || type == BarcodeType.Gs1DataBarTruncated ||
           type == BarcodeType.Gs1DataBarLimited || type == BarcodeType.Gs1DataBarExpanded ||
           type == BarcodeType.Gs1DataBarStacked ||
           type == BarcodeType.Code49;

    /// <summary>将 Code 128 条码按指定模块宽度绘制到画布。</summary>
    /// <param name="x">条码编码区左上角 X 坐标。</param>
    /// <param name="y">条码编码区左上角 Y 坐标。</param>
    /// <param name="data">非空的 Code 128 内容。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="moduleWidth">一个条码模块的宽度，单位为打印点。</param>
    public void DrawBarcode(int x, int y, string data, int height, int moduleWidth = 2)
    {
        DrawBarcode(BarcodeType.Code128, x, y, data, height, moduleWidth, false, 0, null);
    }

    /// <summary>绘制 Code 128 条码，并可使用主机字体绘制条码下方的可读文字；保留旧公开签名。</summary>
    /// <param name="x">条码编码区左上角 X 坐标。</param>
    /// <param name="y">条码编码区左上角 Y 坐标。</param>
    /// <param name="data">非空的 Code 128 内容。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="moduleWidth">一个条码模块的宽度，单位为打印点。</param>
    /// <param name="humanReadable">是否绘制可读文字；false 保持旧的纯条码输出。</param>
    /// <param name="humanReadableReserveHeight">调用方为可读文字声明的布局高度；仅用于输入校验。</param>
    /// <param name="fontFamily">可选的固定 Raster 字体别名；为空时自动选择字体。</param>
    /// <param name="humanReadableFontSize">可读文字字号，单位为打印点。</param>
    public void DrawBarcode(int x, int y, string data, int height, int moduleWidth,
        bool humanReadable, int humanReadableReserveHeight = 0, string fontFamily = null,
        int humanReadableFontSize = DefaultHumanReadableFontSize)
        => DrawBarcode(BarcodeType.Code128, x, y, data, height, moduleWidth, humanReadable, humanReadableReserveHeight,
            fontFamily, humanReadableFontSize, 0);

    /// <summary>绘制 Code 128 条码，并按顺时针角度旋转完整编码区和可读文字预留区。</summary>
    /// <param name="x">旋转后条码外框左上角 X 坐标。</param>
    /// <param name="y">旋转后条码外框左上角 Y 坐标。</param>
    /// <param name="data">非空的 Code 128 内容。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <param name="moduleWidth">一个条码模块的宽度，单位为打印点。</param>
    /// <param name="humanReadable">是否绘制可读文字。</param>
    /// <param name="humanReadableReserveHeight">调用方为可读文字声明的布局高度。</param>
    /// <param name="fontFamily">可选的固定 Raster 字体别名。</param>
    /// <param name="humanReadableFontSize">可读文字字号，单位为打印点。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    public void DrawBarcode(int x, int y, string data, int height, int moduleWidth,
        bool humanReadable, int humanReadableReserveHeight, string fontFamily,
        int humanReadableFontSize, int rotation)
        => DrawBarcode(BarcodeType.Code128, x, y, data, height, moduleWidth, humanReadable,
            humanReadableReserveHeight, fontFamily, humanReadableFontSize, rotation);

    /// <summary>绘制支持的主机条码；保留不带 HRI 的便捷重载。</summary>
    public void DrawBarcode(BarcodeType type, int x, int y, string data, int height, int moduleWidth = 2)
        => DrawBarcode(type, x, y, data, height, moduleWidth, false, 0, null,
            DefaultHumanReadableFontSize, 0);

    /// <summary>绘制支持的主机条码，并可绘制可读文字和旋转结果。</summary>
    public void DrawBarcode(BarcodeType type, int x, int y, string data, int height, int moduleWidth,
        bool humanReadable, int humanReadableReserveHeight = 0, string fontFamily = null,
        int humanReadableFontSize = DefaultHumanReadableFontSize, int rotation = 0)
        => DrawBarcode(type, x, y, data, height, moduleWidth, humanReadable,
            humanReadableReserveHeight, fontFamily, humanReadableFontSize, rotation, true);

    private void DrawBarcode(BarcodeType type, int x, int y, string data, int height, int moduleWidth,
        bool humanReadable, int humanReadableReserveHeight, string fontFamily,
        int humanReadableFontSize, int rotation, bool _)
    {
        if (!SupportsBarcodeType(type))
            throw new NotSupportedException($"ESC/POS Raster 不支持主机条码类型 {type}。");
        ValidateRotation(rotation);
        if (humanReadableReserveHeight < 0) throw new ArgumentOutOfRangeException(nameof(humanReadableReserveHeight));
        if (!humanReadable && humanReadableReserveHeight > 0)
            throw new ArgumentException("未绘制可读文字时不能声明其预留高度。", nameof(humanReadableReserveHeight));
        if (humanReadableFontSize <= 0 || humanReadableFontSize > 255)
            throw new ArgumentOutOfRangeException(nameof(humanReadableFontSize),
                "条码可读文字字号必须在 1–255 点范围内。");
        var readableData = humanReadable ? HumanReadableBarcodeData(type, data) : data;
        if (humanReadable && (type == BarcodeType.UccEan128 || type == BarcodeType.Gs1DataBarExpanded))
            readableData = readableData.Replace("\u001D", string.Empty);
        using var operation = PrintResourceScope.BeginTemporary();
        var encoded = PrepareBarcode(type, x, y, data, height, moduleWidth, rotation == 0);
        ExecuteDrawing(() =>
        {
            if (rotation != 0)
            {
                var sourceHeight = checked(encoded.SymbolHeight + (humanReadable ? humanReadableReserveHeight : 0));
                var source = new EscPosRasterCanvas(encoded.SymbolWidth, sourceHeight, _fonts,
                    _cancellationToken);
                source.DrawBarcode(type, 0, 0, data, height, moduleWidth, humanReadable,
                    humanReadableReserveHeight, fontFamily, humanReadableFontSize);
                DrawRotatedCanvasCore(x, y, source, rotation);
                return;
            }
            DrawBarcodeCore(x, y, type == BarcodeType.Gs1DataBarStacked ? encoded.SymbolHeight : height,
                encoded, type);
            if (!humanReadable) return;
            var ink = MeasureTextInkBounds(readableData, humanReadableFontSize, false, false, fontFamily,
                long.MaxValue, _fonts, _cancellationToken);
            if (humanReadableReserveHeight > 0 && !ink.IsEmpty && ink.Bottom > humanReadableReserveHeight)
                throw new ArgumentOutOfRangeException(nameof(humanReadableReserveHeight),
                    "条码可读文字墨迹超出声明的预留高度。");
            var textWidth = MeasureTextWidth(readableData, humanReadableFontSize, false, fontFamily, _fonts,
                _cancellationToken);
            var textX = checked(x + HumanReadableBarcodeTextOffset(type, moduleWidth,
                encoded.SymbolWidth, textWidth));
            DrawTextCore(textX, checked(y + encoded.SymbolHeight), readableData, humanReadableFontSize, false,
                false, false, fontFamily, 0);
        });
    }

    /// <summary>将 Code 128 条码绘制到画布，并为编码区登记显式静区。</summary>
    public void DrawBarcode(int x, int y, string data, int height, PrintQuietZone quietZone, int moduleWidth = 2)
    {
        ThrowIfCancellationRequested();
        if (quietZone == null) throw new ArgumentNullException(nameof(quietZone));
        using var operation = PrintResourceScope.BeginTemporary();
        var encoded = PrepareBarcode(BarcodeType.Code128, x, y, data, height, moduleWidth);
        var areas = PrepareQuietZone(x, y, encoded.SymbolWidth, height, quietZone, nameof(data));
        ExecuteDrawing(() =>
        {
            RegisterQuietZones(areas);
            DrawBarcodeCore(x, y, height, encoded, BarcodeType.Code128);
        }, areas.Count > 0);
    }

    private (BitMatrix Matrix, int First, int Last, int ModuleWidth, int SymbolWidth, int SymbolHeight) PrepareBarcode(
        BarcodeType type, int x, int y, string data, int height, int moduleWidth, bool ensureFits = true)
    {
        ThrowIfCancellationRequested();
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("条码内容不能为空。", nameof(data));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        ValidateOrigin(x, y);
        if (type == BarcodeType.Code49) ValidateCode49RowHeight(height, moduleWidth);

        var (matrix, first, last) = EncodeBarcode(type, data, _cancellationToken);
        var symbolWidth = CheckedSymbolSize(last - first + 1 +
            (type == BarcodeType.Code49
                ? Code49LeadingQuietZoneModules + Code49TrailingQuietZoneModules
                : 0), moduleWidth, nameof(moduleWidth));
        var symbolHeight = type == BarcodeType.Code49
            ? checked(matrix.Height * height + (matrix.Height + 1) * moduleWidth)
            : type == BarcodeType.Gs1DataBarStacked
                ? MeasureBarcodeHeight(type, data, height, moduleWidth, _cancellationToken)
                : height;
        if (ensureFits) EnsureFits(x, y, symbolWidth, symbolHeight, nameof(data));
        return (matrix, first, last, moduleWidth, symbolWidth, symbolHeight);
    }

    private void DrawBarcodeCore(int x, int y, int height,
        (BitMatrix Matrix, int First, int Last, int ModuleWidth, int SymbolWidth, int SymbolHeight) encoded,
        BarcodeType type)
    {
        if (type == BarcodeType.Code49)
        {
            DrawCode49Core(x, y, height, encoded);
            return;
        }
        if (type == BarcodeType.Gs1DataBarStacked)
        {
            DrawGs1DataBarStackedCore(x, y, height, encoded);
            return;
        }

        for (var column = encoded.First; column <= encoded.Last; column++)
        {
            ThrowIfCancellationRequested();
            var black = false;
            for (var row = 0; row < encoded.Matrix.Height; row++)
            {
                ThrowIfCancellationRequested();
                if (encoded.Matrix[column, row]) { black = true; break; }
            }
            if (!black) continue;
            var left = checked(x + (column - encoded.First) * encoded.ModuleWidth);

            if (type == BarcodeType.IntelligentMail)
            {
                // IMb uses four states over a shared centre tracking region:
                // ascender, tracker, descender and full. The matrix stores
                // those regions as top, centre and bottom rows respectively.
                var ascender = encoded.Matrix[column, 0];
                var tracker = encoded.Matrix[column, 1];
                var descender = encoded.Matrix[column, 2];
                var centreHeight = Math.Max(1, height / 2);
                var centreTop = checked(y + (height - centreHeight) / 2);
                var centreBottom = checked(centreTop + centreHeight);
                var imTop = ascender ? y : centreTop;
                var imBottom = descender ? checked(y + height) : centreBottom;
                if (tracker && !ascender && !descender)
                {
                    imTop = centreTop;
                    imBottom = centreBottom;
                }
                for (var offset = 0; offset < encoded.ModuleWidth; offset++)
                {
                    ThrowIfCancellationRequested();
                    for (var targetY = imTop; targetY < imBottom; targetY++) SetPixel(left + offset, targetY);
                }
                continue;
            }

            var top = y;
            var bottom = checked(y + height);
            if ((type == BarcodeType.Postnet || type == BarcodeType.Planet) && !encoded.Matrix[column, 1])
            {
                // POSTNET short bars are baseline-aligned and occupy half the
                // requested bar height; start/end bars remain full height.
                top = checked(bottom - Math.Max(1, height / 2));
            }
            for (var offset = 0; offset < encoded.ModuleWidth; offset++)
            {
                ThrowIfCancellationRequested();
                for (var targetY = top; targetY < bottom; targetY++) SetPixel(left + offset, targetY);
            }
        }
    }

    private void DrawGs1DataBarStackedCore(int x, int y, int height,
        (BitMatrix Matrix, int First, int Last, int ModuleWidth, int SymbolWidth, int SymbolHeight) encoded)
    {
        for (var row = 0; row < encoded.Matrix.Height; row++)
        {
            ThrowIfCancellationRequested();
            var top = checked(y + (int)((long)row * height / encoded.Matrix.Height));
            var bottom = checked(y + (int)((long)(row + 1) * height / encoded.Matrix.Height));
            for (var column = encoded.First; column <= encoded.Last; column++)
            {
                ThrowIfCancellationRequested();
                if (!encoded.Matrix[column, row]) continue;
                var left = checked(x + (column - encoded.First) * encoded.ModuleWidth);
                for (var offset = 0; offset < encoded.ModuleWidth; offset++)
                {
                    ThrowIfCancellationRequested();
                    for (var targetY = top; targetY < bottom; targetY++) SetPixel(left + offset, targetY);
                }
            }
        }
    }

    private void DrawCode49Core(int x, int y, int rowHeight,
        (BitMatrix Matrix, int First, int Last, int ModuleWidth, int SymbolWidth, int SymbolHeight) encoded)
    {
        var separatorHeight = Math.Max(1, encoded.ModuleWidth);
        var patternLeft = CheckedSymbolSize(Code49LeadingQuietZoneModules, encoded.ModuleWidth,
            nameof(encoded.ModuleWidth));
        var patternWidth = checked(encoded.SymbolWidth - patternLeft -
            Code49TrailingQuietZoneModules * encoded.ModuleWidth);
        DrawCode49Separator(x, y, encoded.SymbolWidth, separatorHeight);
        for (var row = 0; row < encoded.Matrix.Height; row++)
        {
            ThrowIfCancellationRequested();
            var top = checked(y + separatorHeight + row * (rowHeight + separatorHeight));
            for (var column = encoded.First; column <= encoded.Last; column++)
            {
                ThrowIfCancellationRequested();
                if (!encoded.Matrix[column, row]) continue;
                var left = checked(x + patternLeft + (column - encoded.First) * encoded.ModuleWidth);
                for (var offset = 0; offset < encoded.ModuleWidth; offset++)
                {
                    ThrowIfCancellationRequested();
                    for (var targetY = top; targetY < top + rowHeight; targetY++)
                        SetPixel(left + offset, targetY);
                }
            }

            if (row + 1 >= encoded.Matrix.Height) continue;
            var separatorTop = checked(top + rowHeight);
            for (var separator = 0; separator < separatorHeight; separator++)
            {
                ThrowIfCancellationRequested();
                for (var separatorX = 0; separatorX < patternWidth; separatorX++)
                    SetPixel(checked(x + patternLeft + separatorX), separatorTop + separator);
            }
        }
        DrawCode49Separator(x, checked(y + encoded.SymbolHeight - separatorHeight),
            encoded.SymbolWidth, separatorHeight);
    }

    private void DrawCode49Separator(int x, int y, int width, int height)
    {
        for (var separatorY = 0; separatorY < height; separatorY++)
        {
            ThrowIfCancellationRequested();
            for (var separatorX = 0; separatorX < width; separatorX++)
                SetPixel(checked(x + separatorX), checked(y + separatorY));
        }
    }

    internal static int HumanReadableBarcodeTextOffset(BarcodeType type, int moduleWidth,
        int symbolWidth, int textWidth)
    {
        if (type != BarcodeType.Code49)
            return Math.Max(0, (symbolWidth - textWidth) / 2);

        var left = CheckedSymbolSize(Code49LeadingQuietZoneModules, moduleWidth, nameof(moduleWidth));
        var barWidth = checked(symbolWidth - left -
            Code49TrailingQuietZoneModules * moduleWidth);
        return checked(left + Math.Max(0, (barWidth - textWidth) / 2));
    }

    /// <summary>测量 ESC/POS Raster Code 128 实际编码区宽度，单位为打印点。</summary>
    public static int MeasureBarcodeWidth(string data, int moduleWidth)
        => MeasureBarcodeWidth(data, moduleWidth, CancellationToken.None);

    /// <summary>可取消地测量 ESC/POS Raster Code 128 实际编码区宽度。</summary>
    public static int MeasureBarcodeWidth(string data, int moduleWidth, CancellationToken cancellationToken)
        => MeasureBarcodeWidth(BarcodeType.Code128, data, moduleWidth, cancellationToken);

    /// <summary>测量支持的 ESC/POS Raster 主机条码符号输出宽度。</summary>
    /// <remarks>Code 49 的结果包含 10X 左静区和 1X 右静区。</remarks>
    public static int MeasureBarcodeWidth(BarcodeType type, string data, int moduleWidth)
        => MeasureBarcodeWidth(type, data, moduleWidth, CancellationToken.None);

    /// <summary>可取消地测量支持的 ESC/POS Raster 主机条码符号输出宽度。</summary>
    public static int MeasureBarcodeWidth(BarcodeType type, string data, int moduleWidth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (moduleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        using var operation = PrintResourceScope.BeginTemporary();
        var (_, first, last) = EncodeBarcode(type, data, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return CheckedSymbolSize(last - first + 1 +
            (type == BarcodeType.Code49
                ? Code49LeadingQuietZoneModules + Code49TrailingQuietZoneModules
                : 0), moduleWidth, nameof(moduleWidth));
    }

    /// <summary>测量支持的 ESC/POS Raster 主机条码符号输出高度。</summary>
    /// <remarks>
    /// Code 49 的 <paramref name="height"/> 表示每个堆叠行的条高，结果包含顶部、行间和底部 1X 分隔条；
    /// 其它制式沿用原有条高语义。
    /// </remarks>
    internal static int MeasureBarcodeHeight(BarcodeType type, string data, int height, int moduleWidth,
        CancellationToken cancellationToken)
    {
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (moduleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        if (type == BarcodeType.Gs1DataBarStacked && (long)height < 13L * moduleWidth)
            throw new ArgumentOutOfRangeException(nameof(height),
                "GS1 DataBar Stacked 总条高至少为模块宽度的 13 倍。顶部行、分隔行和底部行按 5:1:7 排列。");
        if (type != BarcodeType.Code49) return height;
        ValidateCode49RowHeight(height, moduleWidth);
        return MeasureCode49SymbolHeight(data, height, moduleWidth, cancellationToken);
    }

    internal static int MeasureCode49SymbolHeight(string data, int rowHeight, int moduleWidth,
        CancellationToken cancellationToken)
    {
        if (rowHeight <= 0) throw new ArgumentOutOfRangeException(nameof(rowHeight));
        if (moduleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        using var operation = PrintResourceScope.BeginTemporary();
        var (matrix, _, _) = EncodeBarcode(BarcodeType.Code49, data, cancellationToken);
        return checked(matrix.Height * rowHeight + (matrix.Height + 1) * moduleWidth);
    }

    private static void ValidateCode49RowHeight(int height, int moduleWidth)
    {
        if ((long)height < 8L * moduleWidth)
            throw new ArgumentOutOfRangeException(nameof(height),
                "Code 49 每行条高至少为模块宽度的 8 倍。");
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeBarcode(BarcodeType type, string data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(data)) throw new ArgumentException("条码内容不能为空。", nameof(data));
        if (!SupportsBarcodeType(type))
            throw new NotSupportedException($"ESC/POS Raster 不支持主机条码类型 {type}。");
        var normalizedData = NormalizeBarcodeData(type, data);
        if (type == BarcodeType.Postnet)
            return EncodePostnet(normalizedData, cancellationToken);
        if (type == BarcodeType.Planet)
            return EncodePlanet(normalizedData, cancellationToken);
        if (type == BarcodeType.Fim)
            return EncodeFim(normalizedData, cancellationToken);
        if (type == BarcodeType.IntelligentMail)
            return EncodeIntelligentMail(normalizedData, cancellationToken);
        if (type == BarcodeType.Plessey)
            return EncodePlessey(normalizedData, cancellationToken);
        if (type == BarcodeType.Code11)
            return EncodeCode11(normalizedData, cancellationToken);
        if (type == BarcodeType.Telepen)
            return EncodeTelepen(normalizedData, cancellationToken);
        if (type == BarcodeType.Telepenn)
            return EncodeTelepenn(normalizedData, cancellationToken);
        if (type == BarcodeType.ChinaPost)
            return EncodeChinaPost(normalizedData, cancellationToken);
        if (type == BarcodeType.Gs1DataBarOmnidirectional || type == BarcodeType.Gs1DataBarTruncated)
            return EncodeGs1DataBar(normalizedData, cancellationToken);
        if (type == BarcodeType.Gs1DataBarStacked)
            return EncodeGs1DataBarStacked(normalizedData, cancellationToken);
        if (type == BarcodeType.Gs1DataBarLimited)
            return EncodeGs1DataBarLimited(normalizedData, cancellationToken);
        if (type == BarcodeType.Gs1DataBarExpanded)
            return EncodeGs1DataBarExpanded(normalizedData, cancellationToken);
        if (type == BarcodeType.Code49)
        {
            var code49Matrix = Code49Encoder.Encode(normalizedData, cancellationToken);
            return (code49Matrix, 0, code49Matrix.Width - 1);
        }
        var resourceName = type == BarcodeType.Code128 || type == BarcodeType.Code128Auto || type == BarcodeType.UccEan128
            ? "Matrix.Code128"
            : "Matrix." + type;
        var matrixCharacterCount = type == BarcodeType.UccEan128
            ? checked(normalizedData.Length + 1)
            : normalizedData.Length;
        PrintResourceScope.Retain(PrintMatrixBudget.Code128Bytes(matrixCharacterCount), resourceName);
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = 0,
        };
        var format = ToBarcodeFormat(type);
        if (format == BarcodeFormat.CODE_128)
        {
            hints[EncodeHintType.CODE128_COMPACT] = true;
            if (type == BarcodeType.UccEan128)
                hints[EncodeHintType.GS1_FORMAT] = true;
        }
        BitMatrix matrix;
        try
        {
            // Request the native one-module matrix. Scaling here keeps every bar an
            // integer number of pixels and avoids ZXing stretching the symbol.
            matrix = new MultiFormatWriter().encode(normalizedData, format, 1, 1, hints);
        }
        catch (WriterException error)
        {
            throw new ArgumentException($"{type} 条码内容无法编码。", nameof(data), error);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var first = matrix.Width;
        var last = -1;
        for (var column = 0; column < matrix.Width; column++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var black = false;
            for (var row = 0; row < matrix.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (matrix[column, row]) { black = true; break; }
            }
            if (!black) continue;
            if (first == matrix.Width) first = column;
            last = column;
        }
        if (last < first) throw new ArgumentException($"{type} 条码未生成有效编码。", nameof(data));

        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, first, last);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodePlessey(
        string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrintResourceScope.Retain(PrintMatrixBudget.PlesseyBytes(normalizedData.Length), "Matrix.Plessey");

        bool[] encoded;
        try
        {
            encoded = new PlesseyWriter().encode(normalizedData);
        }
        catch (Exception error) when (error is ArgumentException || error is WriterException)
        {
            throw new ArgumentException("Plessey 条码内容无法编码。", nameof(normalizedData), error);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (encoded == null || encoded.Length == 0)
            throw new ArgumentException("Plessey 条码未生成有效编码。", nameof(normalizedData));

        var first = encoded.Length;
        var last = -1;
        for (var index = 0; index < encoded.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!encoded[index]) continue;
            if (first == encoded.Length) first = index;
            last = index;
        }
        if (last < first)
            throw new ArgumentException("Plessey 条码未生成有效编码。", nameof(normalizedData));

        var matrix = new BitMatrix(checked(last - first + 1), 1);
        for (var index = first; index <= last; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (encoded[index]) matrix[index - first, 0] = true;
        }
        return (matrix, 0, matrix.Width - 1);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeCode11(
        string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrintResourceScope.Retain(PrintMatrixBudget.Code11Bytes(normalizedData.Length), "Matrix.Code11");

        var pattern = EncodeCode11Pattern(normalizedData, cancellationToken);
        var matrix = new BitMatrix(pattern.Length, 1);
        for (var index = 0; index < pattern.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern[index] == '1') matrix[index, 0] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrix.Width - 1);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeTelepen(
        string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrintResourceScope.Retain(PrintMatrixBudget.TelepenBytes(normalizedData.Length), "Matrix.Telepen");

        var pattern = EncodeTelepenPattern(normalizedData, cancellationToken);
        var matrix = new BitMatrix(pattern.Length, 1);
        for (var index = 0; index < pattern.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern[index] == '1') matrix[index, 0] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrix.Width - 1);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeTelepenn(
        string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrintResourceScope.Retain(PrintMatrixBudget.TelepennBytes(normalizedData.Length), "Matrix.Telepenn");

        var pattern = EncodeTelepennPattern(normalizedData, cancellationToken);
        var matrix = new BitMatrix(pattern.Length, 1);
        for (var index = 0; index < pattern.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern[index] == '1') matrix[index, 0] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrix.Width - 1);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeChinaPost(
        string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrintResourceScope.Retain(PrintMatrixBudget.ChinaPostBytes(normalizedData.Length), "Matrix.ChinaPost");

        var pattern = EncodeChinaPostPattern(normalizedData, cancellationToken);
        var matrix = new BitMatrix(pattern.Length, 1);
        for (var index = 0; index < pattern.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern[index] == '1') matrix[index, 0] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrix.Width - 1);
    }

    /// <summary>生成 Datalogic 2 of 5（China Post）的完整模块串。</summary>
    /// <remarks>
    /// Raster 没有独立宽条参数，因此固定使用 3:1 宽窄比；这是 Datalogic 2 of 5
    /// 允许的上界比例，适合点阵/热敏输出。数字字符沿用 Matrix 2 of 5，字符间保留
    /// 一个窄空白；起始为两根窄条，结束为一根宽条和一根窄条，不追加可选校验位。
    /// </remarks>
    internal static string EncodeChinaPostPattern(string data)
        => EncodeChinaPostPattern(NormalizeBarcodeData(BarcodeType.ChinaPost, data),
            CancellationToken.None);

    private static string EncodeChinaPostPattern(string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Start: narrow bar, narrow space, narrow bar (NNN).
        // Stop: wide bar, narrow space, narrow bar (WNN), with a 3:1 ratio.
        var pattern = new StringBuilder(checked(normalizedData.Length * 10 + 9));
        pattern.Append("101");
        pattern.Append('0');
        for (var index = 0; index < normalizedData.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index > 0) pattern.Append('0');
            AppendChinaPostDigit(pattern, normalizedData[index], cancellationToken);
        }
        pattern.Append('0');
        pattern.Append("11101");
        var expectedLength = checked(normalizedData.Length * 10 + 9);
        if (pattern.Length != expectedLength)
            throw new InvalidOperationException("China Post 编码宽度不是预期的 10n+9 模块。");
        return pattern.ToString();
    }

    private static void AppendChinaPostDigit(StringBuilder pattern, char digit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var widths = ChinaPostDigitPatterns[digit - '0'];
        for (var index = 0; index < widths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var width = widths[index] == 'W' ? 3 : 1;
            pattern.Append(index % 2 == 0 ? '1' : '0', width);
        }
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeGs1DataBar(
        string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrintResourceScope.Retain(PrintMatrixBudget.Gs1DataBarBytes(), "Matrix.Gs1DataBar");

        var pattern = EncodeGs1DataBarPattern(normalizedData, cancellationToken);
        var matrix = new BitMatrix(pattern.Length, 1);
        for (var index = 0; index < pattern.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern[index] == '1') matrix[index, 0] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrix.Width - 1);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeGs1DataBarStacked(
        string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrintResourceScope.Retain(PrintMatrixBudget.Gs1DataBarStackedBytes(), "Matrix.Gs1DataBarStacked");

        var rows = BuildGs1DataBarStackedRows(
            EncodeGs1DataBarPattern(normalizedData, cancellationToken), cancellationToken);
        var matrix = new BitMatrix(50, 13);
        for (var column = 0; column < matrix.Width; column++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var row = 0; row < matrix.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceRow = row < 5 ? rows[0] : row == 5 ? rows[1] : rows[2];
                if (sourceRow[column] == '1') matrix[column, row] = true;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrix.Width - 1);
    }

    internal static string[] EncodeGs1DataBarStackedPattern(string data)
    {
        var normalized = NormalizeBarcodeData(BarcodeType.Gs1DataBarStacked, data);
        return BuildGs1DataBarStackedRows(EncodeGs1DataBarPattern(normalized, CancellationToken.None),
            CancellationToken.None);
    }

    private static string[] BuildGs1DataBarStackedRows(string linearPattern,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (linearPattern.Length != 96)
            throw new InvalidOperationException("GS1 DataBar Stacked 需要 96 模块的 DataBar Omnidirectional 编码。");

        var top = new char[50];
        var separator = new char[50];
        var bottom = new char[50];
        for (var index = 0; index < 50; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            top[index] = separator[index] = bottom[index] = '0';
        }
        for (var index = 0; index < 48; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            top[index] = linearPattern[index];
            bottom[index + 2] = linearPattern[index + 48];
        }
        top[48] = '1';
        bottom[0] = '1';

        for (var index = 1; index <= 45; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (top[index] == bottom[index])
            {
                if (top[index] == '0') separator[index] = '1';
            }
            else if (separator[index - 1] == '0')
                separator[index] = '1';
        }
        separator[1] = separator[2] = separator[3] = '0';
        cancellationToken.ThrowIfCancellationRequested();
        return new[] { new string(top), new string(separator), new string(bottom) };
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeGs1DataBarLimited(
        string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrintResourceScope.Retain(PrintMatrixBudget.Gs1DataBarLimitedBytes(), "Matrix.Gs1DataBarLimited");

        var pattern = EncodeGs1DataBarLimitedPattern(normalizedData, cancellationToken);
        var matrix = new BitMatrix(pattern.Length, 1);
        for (var index = 0; index < pattern.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern[index] == '1') matrix[index, 0] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrix.Width - 1);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeGs1DataBarExpanded(
        string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbolCharacters = Gs1DataBarExpandedSymbolCharacters(normalizedData);
        PrintResourceScope.Retain(PrintMatrixBudget.Gs1DataBarExpandedBytes(symbolCharacters),
            "Matrix.Gs1DataBarExpanded");

        var pattern = EncodeGs1DataBarExpandedPattern(normalizedData, cancellationToken);
        var matrix = new BitMatrix(pattern.Length, 1);
        for (var index = 0; index < pattern.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern[index] == '1') matrix[index, 0] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrix.Width - 1);
    }

    /// <summary>生成 Code 11 的完整模块串；包括起止星号和协议校验位。</summary>
    /// <remarks>保留 internal 供协议回归测试验证 C/K 校验与窄空白位序。</remarks>
    internal static string EncodeCode11Pattern(string data)
        => EncodeCode11Pattern(NormalizeBarcodeData(BarcodeType.Code11, data), CancellationToken.None);

    private static string EncodeCode11Pattern(string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbols = new StringBuilder(checked((normalizedData.Length +
            (normalizedData.Length > 10 ? 2 : 1) + 2) * 8));
        symbols.Append(Code11Patterns[11]);
        symbols.Append('0');

        var encodedData = normalizedData + CalculateCode11CheckDigit(normalizedData, 10);
        if (normalizedData.Length > 10)
            encodedData += CalculateCode11CheckDigit(encodedData, 9);

        for (var index = 0; index < encodedData.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            symbols.Append(Code11Patterns[Code11Value(encodedData[index])]);
            if (index + 1 < encodedData.Length) symbols.Append('0');
        }
        symbols.Append('0');
        symbols.Append(Code11Patterns[11]);
        return symbols.ToString();
    }

    /// <summary>生成 Telepen Alpha 的完整模块串；包括起始、Mod-127 校验和结束符号。</summary>
    /// <remarks>保留 internal 供协议回归测试验证符号宽度、校验位和起止模式。</remarks>
    internal static string EncodeTelepenPattern(string data)
        => EncodeTelepenPattern(NormalizeBarcodeData(BarcodeType.Telepen, data), CancellationToken.None);

    private static string EncodeTelepenPattern(string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbolCount = checked(normalizedData.Length + 3);
        var pattern = new StringBuilder(checked(symbolCount * 16));
        AppendTelepenSymbol(pattern, 95, cancellationToken); // '_': start
        for (var index = 0; index < normalizedData.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendTelepenSymbol(pattern, normalizedData[index], cancellationToken);
        }
        AppendTelepenSymbol(pattern, CalculateTelepenCheckDigit(normalizedData), cancellationToken);
        AppendTelepenSymbol(pattern, 122, cancellationToken); // 'z': stop
        return pattern.ToString();
    }

    /// <summary>生成 Telepen Numeric 的完整模块串；输入为数字，奇数位前补零。</summary>
    /// <remarks>保留 internal 供协议回归测试验证数字压缩、校验位和固定符号宽度。</remarks>
    internal static string EncodeTelepennPattern(string data)
        => EncodeTelepennPattern(NormalizeBarcodeData(BarcodeType.Telepenn, data), CancellationToken.None);

    private static string EncodeTelepennPattern(string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paddedData = (normalizedData.Length & 1) == 0 ? normalizedData : "0" + normalizedData;
        var pattern = new StringBuilder(checked(((paddedData.Length / 2) + 3) * 16));
        AppendTelepenSymbol(pattern, 95, cancellationToken); // '_': start

        var sum = 0;
        for (var index = 0; index < paddedData.Length; index += 2)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var glyph = 27 + (paddedData[index] - '0') * 10 + paddedData[index + 1] - '0';
            sum += glyph;
            AppendTelepenSymbol(pattern, glyph, cancellationToken);
        }

        var check = 127 - sum % 127;
        if (check == 127) check = 0;
        AppendTelepenSymbol(pattern, check, cancellationToken);
        AppendTelepenSymbol(pattern, 122, cancellationToken); // 'z': stop
        return pattern.ToString();
    }

    /// <summary>生成 GS1 DataBar Omnidirectional/Truncated 的 96 模块串。</summary>
    /// <remarks>
    /// 算法和数据表依据 libzint 的 GS1 DataBar 实现（BSD-3-Clause）；项目只
    /// 保留线性 Omnidirectional/Truncated 形式，输入为统一模型中的 13 位数字 payload。
    /// </remarks>
    internal static string EncodeGs1DataBarPattern(string data)
        => EncodeGs1DataBarPattern(NormalizeBarcodeData(BarcodeType.Gs1DataBarOmnidirectional, data),
            CancellationToken.None);

    private static string EncodeGs1DataBarPattern(string normalizedData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = 0L;
        for (var index = 0; index < normalizedData.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            value = checked(value * 10 + normalizedData[index] - '0');
        }

        var leftPair = (int)(value / 4537077L);
        var rightPair = (int)(value % 4537077L);
        var dataCharacters = new[]
        {
            leftPair / 1597,
            leftPair % 1597,
            rightPair / 1597,
            rightPair % 1597
        };
        var widths = new int[4, 8];
        for (var character = 0; character < dataCharacters.Length; character++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outside = (character & 1) == 0;
            var group = Gs1DataBarGroup(dataCharacters[character], outside);
            var valueInGroup = dataCharacters[character] - Gs1DataBarGroupSums[group];
            var divisor = Gs1DataBarEvenOdd[group];
            var odd = valueInGroup / divisor;
            var even = valueInGroup % divisor;
            Gs1DataBarWidths(widths, character, outside ? odd : even, outside ? even : odd,
                Gs1DataBarModules[group], Gs1DataBarModules[group + 9],
                Gs1DataBarWidest[group], !outside, cancellationToken);
        }

        var checksum = 0;
        for (var character = 0; character < 4; character++)
        {
            for (var element = 0; element < 8; element++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                checksum += Gs1DataBarChecksumWeights[character, element] * widths[character, element];
            }
        }
        checksum %= 79;
        if (checksum >= 8) checksum++;
        if (checksum >= 72) checksum++;
        var leftFinder = checksum / 9;
        var rightFinder = checksum % 9;

        var total = new int[46];
        total[0] = total[1] = total[44] = total[45] = 1;
        for (var element = 0; element < 8; element++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total[element + 2] = widths[0, element];
            total[element + 15] = widths[1, 7 - element];
            total[element + 23] = widths[3, element];
            total[element + 36] = widths[2, 7 - element];
        }
        for (var element = 0; element < 5; element++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total[element + 10] = Gs1DataBarFinderPatterns[leftFinder, element];
            total[element + 31] = Gs1DataBarFinderPatterns[rightFinder, 4 - element];
        }

        var pattern = new StringBuilder(96);
        var black = false;
        for (var element = 0; element < total.Length; element++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pattern.Append(black ? '1' : '0', total[element]);
            black = !black;
        }
        if (pattern.Length != 96)
            throw new InvalidOperationException("GS1 DataBar 编码宽度不是 96 模块。");
        return pattern.ToString();
    }

    /// <summary>生成 GS1 DataBar Limited 的 79 模块串。</summary>
    /// <remarks>
    /// 算法和数据表依据 libzint 的 GS1 DataBar Limited 实现（BSD-3-Clause）。
    /// 输入为统一模型中的 13 位数字 payload，并由调用方负责完成严格校验。
    /// </remarks>
    internal static string EncodeGs1DataBarLimitedPattern(string data)
        => EncodeGs1DataBarLimitedPattern(NormalizeBarcodeData(BarcodeType.Gs1DataBarLimited, data),
            CancellationToken.None);

    private static string EncodeGs1DataBarLimitedPattern(string normalizedData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = 0L;
        for (var index = 0; index < normalizedData.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            value = checked(value * 10 + normalizedData[index] - '0');
        }

        var pairValues = new[]
        {
            (int)(value / 2013571L),
            (int)(value % 2013571L)
        };
        var pairWidths = new int[2, 14];
        for (var pair = 0; pair < pairValues.Length; pair++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = Gs1DataBarLimitedGroup(ref pairValues[pair]);
            var odd = pairValues[pair] / Gs1DataBarLimitedTEven[group];
            var even = pairValues[pair] % Gs1DataBarLimitedTEven[group];
            var widths = new int[2, 7];
            Gs1DataBarGetWidths(widths, 0, odd, Gs1DataBarLimitedModules[group], 7,
                Gs1DataBarLimitedWidest[group], false, cancellationToken);
            Gs1DataBarGetWidths(widths, 1, even, 26 - Gs1DataBarLimitedModules[group], 7,
                9 - Gs1DataBarLimitedWidest[group], false, cancellationToken);
            for (var element = 0; element < 7; element++)
            {
                pairWidths[pair, element * 2] = widths[0, element];
                pairWidths[pair, element * 2 + 1] = widths[1, element];
            }
        }

        var checksum = 0;
        for (var element = 0; element < 14; element++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checksum += Gs1DataBarLimitedChecksumWeights[0, element] * pairWidths[0, element];
            checksum += Gs1DataBarLimitedChecksumWeights[1, element] * pairWidths[1, element];
        }
        checksum %= 89;

        var total = new int[47];
        total[0] = total[1] = total[44] = total[45] = 1;
        total[46] = 5;
        for (var element = 0; element < 14; element++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total[element + 2] = pairWidths[0, element];
            total[element + 16] = Gs1DataBarLimitedFinderPatterns[checksum, element];
            total[element + 30] = pairWidths[1, element];
        }

        var pattern = new StringBuilder(79);
        var black = false;
        for (var element = 0; element < total.Length; element++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pattern.Append(black ? '1' : '0', total[element]);
            black = !black;
        }
        if (pattern.Length != 79)
            throw new InvalidOperationException("GS1 DataBar Limited 编码宽度不是 79 模块。");
        return pattern.ToString();
    }

    /// <summary>生成 GS1 DataBar Expanded Method 1/2/3/4/5/6/7/8/9/10/11/12/13/14 的线性模块串。</summary>
    /// <remarks>
    /// Raster 主机编码器支持 `(01)` GTIN-14 后接 Expanded 通用字段字符集子集、显式 FNC1
    /// 分隔的 Method 2 原始 GS1 数据，以及受支持常用 AI 子集的圆括号/方括号输入（自动规范化为 FNC1），
    /// 以及显式 `(3103)` 公斤重量的 Method 3、`(3202)/(3203)` 英制重量的 Method 4；
    /// `(392x)` 本地价格的 Method 5、`(393x)` 货币代码加价格的 Method 6，以及 `(310x)`/
    /// `(320x)` 重量加虚拟日期的 Method 7/8，以及带日期和可选通用尾部的 Method 9–14；日期后的
    /// 常用括号 AI 尾部会按变长/定长规则规范化为 FNC1 或连续字段；其它 AI、
    /// 压缩方法、组合码和堆叠形式在规范化阶段严格拒绝。
    /// </remarks>
    internal static string EncodeGs1DataBarExpandedPattern(string data)
        => EncodeGs1DataBarExpandedPattern(NormalizeBarcodeData(BarcodeType.Gs1DataBarExpanded, data),
            CancellationToken.None);

    private static string EncodeGs1DataBarExpandedPattern(string normalizedData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var encoded = EncodeGs1DataBarExpandedBinary(normalizedData, cancellationToken);
        var dataCharacters = encoded.DataCharacters;
        var symbolCharacters = encoded.SymbolCharacters;
        var binary = encoded.Bits;

        var characterWidths = new int[dataCharacters, 8];
        for (var character = 0; character < dataCharacters; character++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = 0;
            for (var bit = 0; bit < 12; bit++)
                value = (value << 1) | (binary[character * 12 + bit] == '1' ? 1 : 0);
            var group = Gs1DataBarExpandedGroup(value);
            var groupValue = value - Gs1DataBarExpandedGroupSums[group];
            var odd = groupValue / Gs1DataBarExpandedTEven[group];
            var even = groupValue % Gs1DataBarExpandedTEven[group];
            Gs1DataBarExpandedWidths(characterWidths, character, odd, even,
                Gs1DataBarExpandedModules[group], Gs1DataBarExpandedWidest[group], cancellationToken);
        }

        var checksum = 0;
        var checksumWeightRow = (dataCharacters - 2) / 2;
        for (var character = 0; character < dataCharacters; character++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var checksumWeight = Gs1DataBarExpandedWeightRows[checksumWeightRow, character];
            for (var element = 0; element < 8; element++)
                checksum += characterWidths[character, element] *
                    Gs1DataBarExpandedChecksumWeights[checksumWeight, element];
        }

        var checkCharacter = 211 * (symbolCharacters - 4) + checksum % 211;
        var checkGroup = Gs1DataBarExpandedGroup(checkCharacter);
        var checkValue = checkCharacter - Gs1DataBarExpandedGroupSums[checkGroup];
        var checkWidths = new int[8];
        Gs1DataBarExpandedWidths(checkWidths, checkValue / Gs1DataBarExpandedTEven[checkGroup],
            checkValue % Gs1DataBarExpandedTEven[checkGroup], Gs1DataBarExpandedModules[checkGroup],
            Gs1DataBarExpandedWidest[checkGroup], cancellationToken);

        var codeBlocks = (symbolCharacters + 1) / 2;
        var patternWidth = codeBlocks * 5 + symbolCharacters * 8 + 4;
        var elements = new int[patternWidth];
        for (var element = 0; element < 8; element++) elements[element + 2] = checkWidths[element];

        var finderSequence = (symbolCharacters - 1) / 2 - 1;
        for (var block = 0; block < codeBlocks; block++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var finderValue = Gs1DataBarExpandedFinderSequence[finderSequence, block];
            // Preserve the R108 GTIN-only byte stream. That historical
            // five-character path used the legacy value-4 table slot; all
            // variable-length symbols use the ISO/Zint table ordering.
            var finder = symbolCharacters == 5 && finderValue == 4 ? 1 : finderValue - 1;
            for (var element = 0; element < 5; element++)
                elements[21 * block + element + 10] =
                    Gs1DataBarExpandedFinderPatterns[finder, element];
        }

        for (var character = 1; character < dataCharacters; character += 2)
        {
            var start = ((character - 1) / 2) * 21 + 23;
            for (var element = 0; element < 8; element++)
                elements[start + element] = characterWidths[character, element];
        }

        for (var character = 0; character < dataCharacters; character += 2)
        {
            var start = (character / 2) * 21 + 15;
            for (var element = 0; element < 8; element++)
                elements[start + element] = characterWidths[character, 7 - element];
        }

        elements[0] = elements[1] = elements[patternWidth - 2] = elements[patternWidth - 1] = 1;
        var pattern = new StringBuilder(patternWidth);
        var black = false;
        for (var element = 0; element < patternWidth; element++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pattern.Append(black ? '1' : '0', elements[element]);
            black = !black;
        }
        var expectedWidth = checked(17 * symbolCharacters + 15 * codeBlocks + 4);
        if (pattern.Length != expectedWidth)
            throw new InvalidOperationException($"GS1 DataBar Expanded 编码宽度不是 {expectedWidth} 模块，实际为 {pattern.Length}。");
        return pattern.ToString();
    }

    private static int Gs1DataBarExpandedSymbolCharacters(string normalizedData)
        => EncodeGs1DataBarExpandedBinary(normalizedData, CancellationToken.None).SymbolCharacters;

    private const int ExpandedGeneralFieldNumeric = 0;
    private const int ExpandedGeneralFieldAlphaNumeric = 1;
    private const int ExpandedGeneralFieldIsoIec646 = 2;
    private const char ExpandedGeneralFieldFnc1 = '\u001D';
    // Internal markers retained only after explicit compressed-field AIs are normalized.
    private const char ExpandedMethod3Marker = '\u001E';
    private const char ExpandedMethod4Marker = '\u001F';
    private const char ExpandedMethod5Marker = '\u000B';
    private const char ExpandedMethod6Marker = '\u000C';
    private const char ExpandedMethod7Marker = '\u000D';
    private const char ExpandedMethod8Marker = '\u000E';
    private const char ExpandedMethod9Marker = '\u0015';
    private const char ExpandedMethod10Marker = '\u0016';
    private const char ExpandedMethod11Marker = '\u0017';
    private const char ExpandedMethod12Marker = '\u0018';
    private const char ExpandedMethod13Marker = '\u0019';
    private const char ExpandedMethod14Marker = '\u001A';
    private const string ExpandedGeneralFieldAlphaPunctuation = "*,-./";
    private const string ExpandedGeneralFieldIsoPunctuation = "!\"%&'()*+,-./:;<=>?_ ";

    // General-field transitions and character values adapted from libzint
    // general_field.c (BSD-3-Clause); see the protocol documentation for the source link.

    private static int ExpandedGeneralFieldType(char value)
    {
        if (value == ExpandedGeneralFieldFnc1 || (value >= '0' && value <= '9'))
            return ExpandedGeneralFieldNumeric;
        if ((value >= 'A' && value <= 'Z') || ExpandedGeneralFieldAlphaPunctuation.IndexOf(value) >= 0)
            return ExpandedGeneralFieldAlphaNumeric;
        if ((value >= 'a' && value <= 'z') || ExpandedGeneralFieldIsoPunctuation.IndexOf(value) >= 0)
            return ExpandedGeneralFieldIsoIec646;
        return -1;
    }

    private static bool IsExpandedStructuredAi(string data, int index)
    {
        if (data.Length < index + 6)
            return false;
        var opening = data[index];
        var closing = opening == '(' ? ')' : opening == '[' ? ']' : '\0';
        if (closing == '\0' || data[index + 5] != closing)
            return false;
        for (var offset = 1; offset <= 4; offset++)
        {
            if (data[index + offset] < '0' || data[index + offset] > '9')
                return false;
        }
        return true;
    }

    private static bool IsExpandedTwoDigitStructuredAi(string data, int index)
    {
        if (data.Length < index + 4)
            return false;
        var opening = data[index];
        var closing = opening == '(' ? ')' : opening == '[' ? ']' : '\0';
        if (closing == '\0' || data[index + 3] != closing)
            return false;
        return data[index + 1] >= '0' && data[index + 1] <= '9' &&
            data[index + 2] >= '0' && data[index + 2] <= '9';
    }

    private static bool IsExpandedDateAi(string value) =>
        value == "11" || value == "13" || value == "15" || value == "17";

    private static bool HasExpandedBracketedDateTail(string data)
    {
        if (string.IsNullOrEmpty(data))
            return false;
        for (var index = 0; index + 10 < data.Length; index++)
        {
            var opening = data[index];
            var closing = opening == '(' ? ')' : opening == '[' ? ']' : '\0';
            if (closing == '\0' || data[index + 3] != closing ||
                !IsExpandedDateAi(data.Substring(index + 1, 2)))
                continue;
            var dateStart = index + 4;
            if (data.Substring(dateStart, 6).Any(character => character < '0' || character > '9'))
                continue;
            var tailStart = dateStart + 6;
            if (tailStart < data.Length && (data[tailStart] == '(' || data[tailStart] == '['))
                return true;
        }
        return false;
    }

    private static string NormalizeExpandedDateTail(string dateTail, char expectedOpening, string name)
    {
        if (!Gs1ApplicationIdentifierParser.IsBracketed(dateTail))
            return dateTail;
        if (dateTail[0] != expectedOpening)
            throw new ArgumentException("GS1 日期 AI 与日期后的括号尾部不能混用圆括号和方括号。", name);
        return Gs1ApplicationIdentifierParser.NormalizeDateTail(dateTail, name);
    }

    /// <summary>
    /// 校验 DataBar Expanded Method 2 的原始 AI/FNC1 数据和通用字段字符集。
    /// </summary>
    /// <param name="normalized">以两位数字 AI 开头的原始元素串。</param>
    /// <param name="name">异常中的参数名称。</param>
    /// <param name="requireFnc1">是否要求元素串包含 FNC1 分隔符。</param>
    private static void ValidateExpandedMethod2Data(string normalized, string name, bool requireFnc1 = true)
    {
        if (normalized.Length < 3 || normalized.Length > 77 ||
            normalized[0] < '0' || normalized[0] > '9' ||
            normalized[1] < '0' || normalized[1] > '9' ||
            requireFnc1 && normalized.IndexOf(ExpandedGeneralFieldFnc1) < 0)
            throw new ArgumentException(
                requireFnc1
                    ? "GS1 DataBar Expanded Method 2 需要以两位数字 AI 开头、包含显式 ASCII 29/FNC1 分隔符，长度为 3–77 个字符。"
                    : "GS1 DataBar Expanded Method 2 需要以两位数字 AI 开头，规范化后长度为 3–77 个字符。",
                name);

        if (normalized[0] == ExpandedGeneralFieldFnc1 ||
            normalized[normalized.Length - 1] == ExpandedGeneralFieldFnc1)
            throw new ArgumentException("GS1 DataBar Expanded Method 2 不能以 FNC1 开头或结尾。", name);

        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];
            if (character == '[' || character == ']')
                throw new ArgumentException(
                    "GS1 DataBar Expanded Method 2 使用原始 AI/FNC1 数据，不接受方括号展示语法。",
                    name);
            if (ExpandedGeneralFieldType(character) < 0)
                throw new ArgumentException(
                    "GS1 DataBar Expanded Method 2 仅支持数字、A-Z、a-z、ISO/IEC 646 标点、空格和 FNC1。",
                    name);
            if (index > 0 && character == ExpandedGeneralFieldFnc1 &&
                normalized[index - 1] == ExpandedGeneralFieldFnc1)
                throw new ArgumentException("GS1 DataBar Expanded Method 2 不能包含连续 FNC1。", name);
        }
    }

    private static bool TryParseExpandedDate(string value, out int encoded)
    {
        encoded = 0;
        if (value.Length != 6 || value.Any(character => character < '0' || character > '9'))
            return false;
        var year = int.Parse(value.Substring(0, 2), System.Globalization.CultureInfo.InvariantCulture);
        var month = int.Parse(value.Substring(2, 2), System.Globalization.CultureInfo.InvariantCulture);
        var day = int.Parse(value.Substring(4, 2), System.Globalization.CultureInfo.InvariantCulture);
        if (month < 1 || month > 12 || day > 31)
            return false;
        encoded = year * 384 + (month - 1) * 32 + day;
        return true;
    }

    private static bool ExpandedGeneralFieldNext(string data, int index, int end, int count,
        int type, int secondaryType = -1)
    {
        if (index + count > end) return false;
        for (var cursor = index; cursor < end && count > 0; cursor++, count--)
        {
            var cursorType = ExpandedGeneralFieldType(data[cursor]);
            if (secondaryType < 0 ? cursorType != type : cursorType != type && cursorType != secondaryType)
                return false;
        }
        return count == 0;
    }

    private static bool ExpandedGeneralFieldNextTerminate(string data, int index, int end,
        int minimumCount, int maximumCount, int type)
    {
        if (index + maximumCount < end) return false;
        var count = 0;
        for (var cursor = index; cursor < end; cursor++, count++)
            if (ExpandedGeneralFieldType(data[cursor]) != type) return false;
        return count >= minimumCount && count <= maximumCount;
    }

    private static bool ExpandedGeneralFieldNextNone(string data, int index, int end, int count, int type)
    {
        var cursor = index;
        for (; cursor < end && count > 0; cursor++, count--)
            if (ExpandedGeneralFieldType(data[cursor]) == type) return false;
        return count == 0 || cursor == end;
    }

    /// <summary>按 ISO/IEC 24724 的 Expanded 通用字段状态机追加编码位。</summary>
    private static void EncodeGs1DataBarExpandedGeneralField(string normalizedData, int startIndex,
        StringBuilder binary, CancellationToken cancellationToken, out int mode, out char lastDigit)
    {
        mode = ExpandedGeneralFieldNumeric;
        lastDigit = '\0';
        var end = normalizedData.Length;
        for (var index = startIndex; index < end;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var type = ExpandedGeneralFieldType(normalizedData[index]);
            if (type < 0)
                throw new ArgumentException("GS1 DataBar Expanded 通用字段包含不支持的 ASCII 字符。",
                    nameof(normalizedData));

            switch (mode)
            {
                case ExpandedGeneralFieldNumeric:
                    if (index < end - 1)
                    {
                        var nextType = ExpandedGeneralFieldType(normalizedData[index + 1]);
                        if (type != ExpandedGeneralFieldNumeric || nextType != ExpandedGeneralFieldNumeric)
                        {
                            AppendBinary(binary, 0, 4); // Alphanumeric latch 0000.
                            mode = ExpandedGeneralFieldAlphaNumeric;
                        }
                        else
                        {
                            var first = normalizedData[index] == ExpandedGeneralFieldFnc1
                                ? 10 : normalizedData[index] - '0';
                            var second = normalizedData[index + 1] == ExpandedGeneralFieldFnc1
                                ? 10 : normalizedData[index + 1] - '0';
                            AppendBinary(binary, 11 * first + second + 8, 7);
                            index += 2;
                        }
                    }
                    else if (type != ExpandedGeneralFieldNumeric)
                    {
                        AppendBinary(binary, 0, 4); // Alphanumeric latch 0000.
                        mode = ExpandedGeneralFieldAlphaNumeric;
                    }
                    else
                    {
                        lastDigit = normalizedData[index++];
                    }
                    break;

                case ExpandedGeneralFieldAlphaNumeric:
                    if (normalizedData[index] == ExpandedGeneralFieldFnc1)
                    {
                        AppendBinary(binary, 15, 5); // Numeric latch 01111.
                        mode = ExpandedGeneralFieldNumeric;
                        index++;
                    }
                    else if (type == ExpandedGeneralFieldIsoIec646)
                    {
                        AppendBinary(binary, 4, 5); // ISO/IEC 646 latch 00100.
                        mode = ExpandedGeneralFieldIsoIec646;
                    }
                    else if (ExpandedGeneralFieldNext(normalizedData, index, end, 6,
                                 ExpandedGeneralFieldNumeric))
                    {
                        AppendBinary(binary, 0, 3); // Numeric latch 000.
                        mode = ExpandedGeneralFieldNumeric;
                    }
                    else if (ExpandedGeneralFieldNextTerminate(normalizedData, index, end, 4, 5,
                                 ExpandedGeneralFieldNumeric))
                    {
                        AppendBinary(binary, 0, 3); // Numeric latch 000.
                        mode = ExpandedGeneralFieldNumeric;
                    }
                    else if (normalizedData[index] >= '0' && normalizedData[index] <= '9')
                    {
                        AppendBinary(binary, normalizedData[index] - 43, 5);
                        index++;
                    }
                    else if (normalizedData[index] >= 'A' && normalizedData[index] <= 'Z')
                    {
                        AppendBinary(binary, normalizedData[index] - 33, 6);
                        index++;
                    }
                    else
                    {
                        var punctuation = ExpandedGeneralFieldAlphaPunctuation.IndexOf(normalizedData[index]);
                        if (punctuation < 0)
                            throw new ArgumentException("GS1 DataBar Expanded 通用字段包含无效的字母数字字符。",
                                nameof(normalizedData));
                        AppendBinary(binary, punctuation + 58, 6);
                        index++;
                    }
                    break;

                case ExpandedGeneralFieldIsoIec646:
                    if (normalizedData[index] == ExpandedGeneralFieldFnc1)
                    {
                        AppendBinary(binary, 15, 5); // Numeric latch 01111.
                        mode = ExpandedGeneralFieldNumeric;
                        index++;
                    }
                    else
                    {
                        var nextTenNotIso = ExpandedGeneralFieldNextNone(normalizedData, index, end, 10,
                            ExpandedGeneralFieldIsoIec646);
                        if (nextTenNotIso && ExpandedGeneralFieldNext(normalizedData, index, end, 4,
                                ExpandedGeneralFieldNumeric))
                        {
                            AppendBinary(binary, 0, 3); // Numeric latch 000.
                            mode = ExpandedGeneralFieldNumeric;
                        }
                        else if (nextTenNotIso && ExpandedGeneralFieldNext(normalizedData, index, end, 5,
                                     ExpandedGeneralFieldAlphaNumeric, ExpandedGeneralFieldNumeric))
                        {
                            AppendBinary(binary, 4, 5); // Alphanumeric latch 00100.
                            mode = ExpandedGeneralFieldAlphaNumeric;
                        }
                        else if (normalizedData[index] >= '0' && normalizedData[index] <= '9')
                        {
                            AppendBinary(binary, normalizedData[index] - 43, 5);
                            index++;
                        }
                        else if (normalizedData[index] >= 'A' && normalizedData[index] <= 'Z')
                        {
                            AppendBinary(binary, normalizedData[index] - 1, 7);
                            index++;
                        }
                        else if (normalizedData[index] >= 'a' && normalizedData[index] <= 'z')
                        {
                            AppendBinary(binary, normalizedData[index] - 7, 7);
                            index++;
                        }
                        else
                        {
                            var punctuation = ExpandedGeneralFieldIsoPunctuation.IndexOf(normalizedData[index]);
                            if (punctuation < 0)
                                throw new ArgumentException("GS1 DataBar Expanded 通用字段包含无效的 ISO/IEC 646 字符。",
                                    nameof(normalizedData));
                            AppendBinary(binary, punctuation + 232, 8);
                            index++;
                        }
                    }
                    break;

                default:
                    throw new InvalidOperationException("GS1 DataBar Expanded 通用字段编码状态无效。");
            }
        }
    }

    private static ExpandedBinary EncodeGs1DataBarExpandedBinary(string normalizedData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var method3 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod3Marker;
        var method4 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod4Marker;
        var method5 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod5Marker;
        var method6 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod6Marker;
        var method7 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod7Marker;
        var method8 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod8Marker;
        var method9 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod9Marker;
        var method10 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod10Marker;
        var method11 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod11Marker;
        var method12 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod12Marker;
        var method13 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod13Marker;
        var method14 = normalizedData.Length > 16 && normalizedData[16] == ExpandedMethod14Marker;
        var compressedMethod = method3 || method4 || method5 || method6 || method7 || method8 ||
            method9 || method10 || method11 || method12 || method13 || method14;
        var method2 = !compressedMethod &&
            (normalizedData.Length < 16 || normalizedData[0] != '0' || normalizedData[1] != '1' ||
             normalizedData.Substring(0, Math.Min(16, normalizedData.Length))
                 .Any(character => character < '0' || character > '9'));
        var encodedData = compressedMethod ? normalizedData.Remove(16, 1) : normalizedData;
        var dateMethod = method9 || method10 || method11 || method12 || method13 || method14 ||
            ((method7 || method8) && encodedData.Length >= 34);
        var generalStart = method2 ? 0 : method3 || method4 ? 26 : method5 ? 20 : method6 ? 23 :
            dateMethod ? 34 : method7 || method8 ? 26 : 16;
        // Method 1: linkage flag 0, method field 1XX. Method 2 uses 0000 and
        // sends the complete input through the general field. Methods 3 and 4 use
        // fixed variable-weight fields 0100 and 0101; Methods 5 and 6 use
        // 01100XX and 01101XX before the same GTIN triplets.
        var generalLength = encodedData.Length - generalStart;
        var binary = new StringBuilder(checked(64 + generalLength * 8 + 32));
        binary.Append('0');
        if (method2)
            AppendBinary(binary, 0, 4); // 0000; general-purpose AI data.
        else if (method3)
            AppendBinary(binary, 4, 4); // 0100; method 3, variable weight in kilograms.
        else if (method4)
            AppendBinary(binary, 5, 4); // 0101; method 4, variable weight in pounds.
        else if (method5)
            AppendBinary(binary, 0x30, 7); // 01100XX; method 5, variable measure price.
        else if (method6)
            AppendBinary(binary, 0x34, 7); // 01101XX; method 6, currency and price.
        else if (method7)
            AppendBinary(binary, 56, 7); // 0111000; method 7, metric weight and dummy date.
        else if (method8)
            AppendBinary(binary, 57, 7); // 0111001; method 8, imperial weight and dummy date.
        else if (dateMethod)
        {
            var methodNumber = method9 ? 9 : method10 ? 10 : method11 ? 11 : method12 ? 12 : method13 ? 13 : 14;
            AppendBinary(binary, 56 + methodNumber - 7, 7);
        }
        else
            AppendBinary(binary, 4, 3); // 1XX; the two variable bits are patched below.
        if (!method2)
        {
            AppendBinary(binary, encodedData[2] - '0', 4);
            for (var index = 3; index < 15; index += 3)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = (encodedData[index] - '0') * 100 +
                    (encodedData[index + 1] - '0') * 10 + encodedData[index + 2] - '0';
                AppendBinary(binary, value, 10);
            }
        }
        if (method3 || method4)
        {
            var weight = int.Parse(encodedData.Substring(20, 6), System.Globalization.CultureInfo.InvariantCulture);
            if (method4 && encodedData[19] == '3')
                weight += 10000;
            AppendBinary(binary, weight, 15);
        }
        else if (method5 || method6)
        {
            AppendBinary(binary, encodedData[19] - '0', 2);
            if (method6)
            {
                var currency = int.Parse(encodedData.Substring(20, 3),
                    System.Globalization.CultureInfo.InvariantCulture);
                AppendBinary(binary, currency, 10);
            }
        }
        else if (method7 || method8)
        {
            var weight = int.Parse(string.Concat(encodedData[19], encodedData.Substring(21, 5)),
                System.Globalization.CultureInfo.InvariantCulture);
            AppendBinary(binary, weight, 20);
            if (dateMethod)
            {
                if (!TryParseExpandedDate(encodedData.Substring(28, 6), out var date))
                    throw new ArgumentException("GS1 DataBar Expanded 日期字段无效。", nameof(normalizedData));
                AppendBinary(binary, date, 16);
            }
            else
            {
                AppendBinary(binary, 38400, 16); // Dummy date 00/00/00, per Methods 7/8.
            }
        }
        else if (dateMethod)
        {
            var weight = int.Parse(string.Concat(encodedData[19], encodedData.Substring(21, 5)),
                System.Globalization.CultureInfo.InvariantCulture);
            AppendBinary(binary, weight, 20);
            if (!TryParseExpandedDate(encodedData.Substring(28, 6), out var date))
                throw new ArgumentException("GS1 DataBar Expanded 日期字段无效。", nameof(normalizedData));
            AppendBinary(binary, date, 16);
        }

        int generalMode;
        char lastDigit;
        EncodeGs1DataBarExpandedGeneralField(encodedData, generalStart, binary, cancellationToken,
            out generalMode, out lastDigit);

        var bitPosition = binary.Length;
        var remainder = 12 - bitPosition % 12;
        if (remainder == 12) remainder = 0;
        var symbolCharacters = ((bitPosition + remainder) / 12) + 1;
        if (symbolCharacters < 4) symbolCharacters = 4;
        remainder = 12 * (symbolCharacters - 1) - bitPosition;

        if (lastDigit != '\0')
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (lastDigit == ExpandedGeneralFieldFnc1)
                throw new ArgumentException("GS1 DataBar Expanded 通用字段不能以 FNC1 结束。", nameof(normalizedData));
            if (remainder >= 4 && remainder <= 6)
                AppendBinary(binary, lastDigit - '0' + 1, 4);
            else
                AppendBinary(binary, (lastDigit - '0') * 11 + 18, 7);

            bitPosition = binary.Length;
            remainder = 12 - bitPosition % 12;
            if (remainder == 12) remainder = 0;
            symbolCharacters = ((bitPosition + remainder) / 12) + 1;
            if (symbolCharacters < 4) symbolCharacters = 4;
            remainder = 12 * (symbolCharacters - 1) - bitPosition;
        }

        if (bitPosition > 252)
            throw new ArgumentException("GS1 DataBar Expanded 通用字段过长，最多生成 22 个符号字符。",
                nameof(normalizedData));

        // Numeric mode gets a four-bit terminator; every remaining bit is 00100
        // padding. The final five-bit chunk may cross the data-character edge.
        var padding = remainder;
        if (generalMode == ExpandedGeneralFieldNumeric)
        {
            AppendBinary(binary, 0, 4);
            padding -= 4;
        }
        while (padding > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendBinary(binary, 4, 5);
            padding -= 5;
        }

        if (method5 || method6)
        {
            binary[6] = (symbolCharacters & 1) != 0 ? '1' : '0';
            binary[7] = symbolCharacters > 14 ? '1' : '0';
        }
        else if (method2)
        {
            binary[3] = (symbolCharacters & 1) != 0 ? '1' : '0';
            binary[4] = symbolCharacters > 14 ? '1' : '0';
        }
        else if (!compressedMethod)
        {
            binary[2] = (symbolCharacters & 1) != 0 ? '1' : '0';
            binary[3] = symbolCharacters > 14 ? '1' : '0';
        }
        var dataCharacters = symbolCharacters - 1;
        return new ExpandedBinary(binary.ToString(), dataCharacters, symbolCharacters);
    }

    private readonly struct ExpandedBinary
    {
        internal ExpandedBinary(string bits, int dataCharacters, int symbolCharacters)
        {
            Bits = bits;
            DataCharacters = dataCharacters;
            SymbolCharacters = symbolCharacters;
        }

        internal string Bits { get; }
        internal int DataCharacters { get; }
        internal int SymbolCharacters { get; }
    }

    private static void Gs1DataBarExpandedWidths(int[,] destination, int character, int odd,
        int even, int oddModules, int widest, CancellationToken cancellationToken)
    {
        var widths = new int[2, 4];
        Gs1DataBarGetWidths(widths, 0, odd, oddModules, 4, widest, true, cancellationToken);
        Gs1DataBarGetWidths(widths, 1, even, 17 - oddModules, 4, 9 - widest, false,
            cancellationToken);
        for (var element = 0; element < 4; element++)
        {
            destination[character, element * 2] = widths[0, element];
            destination[character, element * 2 + 1] = widths[1, element];
        }
    }

    private static void Gs1DataBarExpandedWidths(int[] destination, int odd, int even,
        int oddModules, int widest, CancellationToken cancellationToken)
    {
        var widths = new int[2, 4];
        Gs1DataBarGetWidths(widths, 0, odd, oddModules, 4, widest, true, cancellationToken);
        Gs1DataBarGetWidths(widths, 1, even, 17 - oddModules, 4, 9 - widest, false,
            cancellationToken);
        for (var element = 0; element < 4; element++)
        {
            destination[element * 2] = widths[0, element];
            destination[element * 2 + 1] = widths[1, element];
        }
    }

    private static int Gs1DataBarExpandedGroup(int value)
    {
        for (var group = 0; group < Gs1DataBarExpandedGroupSums.Length - 1; group++)
            if (value < Gs1DataBarExpandedGroupSums[group + 1]) return group;
        return Gs1DataBarExpandedGroupSums.Length - 1;
    }

    private static void AppendBinary(StringBuilder target, int value, int bits)
    {
        for (var bit = bits - 1; bit >= 0; bit--)
            target.Append((value & (1 << bit)) == 0 ? '0' : '1');
    }

    private static int Gs1DataBarLimitedGroup(ref int value)
    {
        for (var group = Gs1DataBarLimitedGroupSums.Length - 1; group > 0; group--)
        {
            if (value < Gs1DataBarLimitedGroupSums[group]) continue;
            value -= Gs1DataBarLimitedGroupSums[group];
            return group;
        }
        return 0;
    }

    private static int Gs1DataBarGroup(int value, bool outside)
    {
        var start = outside ? 0 : 5;
        var end = outside ? 4 : 8;
        for (var group = start; group < end; group++)
        {
            if (value < Gs1DataBarGroupSums[group + 1]) return group;
        }
        return end;
    }

    private static void Gs1DataBarWidths(int[,] destination, int character, int oddValue, int evenValue,
        int oddModules, int evenModules, int widest, bool noNarrow, CancellationToken cancellationToken)
    {
        var widths = new int[2, 4];
        Gs1DataBarGetWidths(widths, 0, oddValue, oddModules, 4, widest, noNarrow, cancellationToken);
        Gs1DataBarGetWidths(widths, 1, evenValue, evenModules, 4, 9 - widest, !noNarrow,
            cancellationToken);
        for (var element = 0; element < 4; element++)
        {
            destination[character, element * 2] = widths[0, element];
            destination[character, element * 2 + 1] = widths[1, element];
        }
    }

    private static void Gs1DataBarGetWidths(int[,] widths, int row, int value, int modules,
        int elements, int maxWidth, bool noNarrow, CancellationToken cancellationToken)
    {
        var narrowMask = 0;
        var remainingModules = modules;
        var element = 0;
        for (; element < elements - 1; element++)
        {
            var elementWidth = 1;
            narrowMask |= 1 << element;
            int subValue;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                subValue = Gs1DataBarCombinations(remainingModules - elementWidth - 1,
                    elements - element - 2);
                if (noNarrow && narrowMask == 0 &&
                    remainingModules - elementWidth - (elements - element - 1) >= elements - element - 1)
                {
                    subValue -= Gs1DataBarCombinations(remainingModules - elementWidth - (elements - element),
                        elements - element - 2);
                }
                if (elements - element - 1 > 1)
                {
                    var less = 0;
                    for (var maxElement = remainingModules - elementWidth - (elements - element - 2);
                         maxElement > maxWidth; maxElement--)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        less += Gs1DataBarCombinations(remainingModules - elementWidth - maxElement - 1,
                            elements - element - 3);
                    }
                    subValue -= less * (elements - 1 - element);
                }
                else if (remainingModules - elementWidth > maxWidth)
                {
                    subValue--;
                }
                value -= subValue;
                if (value < 0) break;
                elementWidth++;
                narrowMask &= ~(1 << element);
            }
            value += subValue;
            remainingModules -= elementWidth;
            widths[row, element] = elementWidth;
        }
        widths[row, element] = remainingModules;
    }

    private static int Gs1DataBarCombinations(int n, int r)
    {
        if (n < 0 || r < 0 || r > n) return 0;
        r = Math.Min(r, n - r);
        var result = 1;
        for (var index = 1; index <= r; index++)
            result = checked(result * (n - r + index) / index);
        return result;
    }

    private static void AppendTelepenSymbol(StringBuilder pattern, int value,
        CancellationToken cancellationToken)
    {
        if ((uint)value >= TelepenPatterns.Length)
            throw new ArgumentOutOfRangeException(nameof(value));

        var widths = TelepenPatterns[value];
        var bar = true;
        for (var index = 0; index < widths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var run = widths[index] - '0';
            for (var offset = 0; offset < run; offset++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pattern.Append(bar ? '1' : '0');
            }
            bar = !bar;
        }
    }

    private static int CalculateTelepenCheckDigit(string data)
    {
        var sum = 0;
        for (var index = 0; index < data.Length; index++) sum += data[index];
        var check = 127 - sum % 127;
        return check == 127 ? 0 : check;
    }

    private static char CalculateCode11CheckDigit(string data, int maximumWeight)
    {
        var sum = 0;
        var weight = 1;
        for (var index = data.Length - 1; index >= 0; index--)
        {
            sum += Code11Value(data[index]) * weight;
            weight++;
            if (weight > maximumWeight) weight = 1;
        }
        var check = sum % 11;
        return check == 10 ? '-' : (char)('0' + check);
    }

    private static int Code11Value(char value)
    {
        if (value >= '0' && value <= '9') return value - '0';
        if (value == '-') return 10;
        throw new ArgumentException("Code 11 仅接受 ASCII 数字和连字符。", nameof(value));
    }

    private static (BitMatrix Matrix, int First, int Last) EncodePostnet(string normalizedData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var digits = normalizedData + CalculatePostnetCheckDigit(normalizedData);
        var barCount = checked(digits.Length * 5 + 2);
        // One logical column per bar and one per inter-bar gap. The caller
        // applies the requested module width to both, preserving a stable
        // gap while allowing bars to use different heights.
        var matrixWidth = checked(barCount * 2 - 1);
        PrintResourceScope.Retain(PrintMatrixBudget.Code128Bytes(digits.Length), "Matrix.Postnet");
        var matrix = new BitMatrix(matrixWidth, 2);
        var column = 0;
        SetPostnetBar(matrix, column, true);
        column = 2;
        for (var digitIndex = 0; digitIndex < digits.Length; digitIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pattern = PostnetDigitPatterns[digits[digitIndex] - '0'];
            for (var patternIndex = 0; patternIndex < pattern.Length; patternIndex++)
            {
                SetPostnetBar(matrix, column, pattern[patternIndex] == '1');
                column += 2;
            }
        }
        SetPostnetBar(matrix, column, true);
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrixWidth - 1);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodePlanet(string normalizedData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var barCount = checked(normalizedData.Length * 5 + 2);
        // PLANET uses the inverse bar-height convention of POSTNET. Keep one
        // logical column for every bar and inter-bar gap so the requested
        // module width scales both consistently.
        var matrixWidth = checked(barCount * 2 - 1);
        PrintResourceScope.Retain(PrintMatrixBudget.Code128Bytes(normalizedData.Length), "Matrix.Planet");
        var matrix = new BitMatrix(matrixWidth, 2);
        var column = 0;
        SetPostnetBar(matrix, column, true);
        column = 2;
        for (var digitIndex = 0; digitIndex < normalizedData.Length; digitIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pattern = PlanetDigitPatterns[normalizedData[digitIndex] - '0'];
            for (var patternIndex = 0; patternIndex < pattern.Length; patternIndex++)
            {
                SetPostnetBar(matrix, column, pattern[patternIndex] == '1');
                column += 2;
            }
        }
        SetPostnetBar(matrix, column, true);
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrixWidth - 1);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeFim(string normalizedData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // FIM is a nine-bit USPS pattern. Each bit occupies one bar slot and
        // one inter-bar gap, with no trailing gap after the final slot.
        const int patternLength = 9;
        var matrixWidth = checked(patternLength * 2 - 1);
        PrintResourceScope.Retain(PrintMatrixBudget.Code128Bytes(normalizedData.Length), "Matrix.Fim");
        var matrix = new BitMatrix(matrixWidth, 1);
        var pattern = FimPatterns[normalizedData[0] - 'A'];
        for (var index = 0; index < patternLength; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern[index] == '1') matrix[index * 2, 0] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrixWidth - 1);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeIntelligentMail(string normalizedData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pattern = EncodeIntelligentMailPattern(normalizedData, cancellationToken);
        const int barCount = 65;
        var matrixWidth = checked(barCount * 2 - 1);
        // The matrix keeps one empty column between adjacent bars. This gives
        // the Raster provider a stable pitch and lets moduleWidth scale bars
        // and spaces together without stretching a rendered bitmap.
        PrintResourceScope.Retain(PrintMatrixBudget.Code128Bytes(barCount), "Matrix.IntelligentMail");
        var matrix = new BitMatrix(matrixWidth, 3);
        for (var index = 0; index < pattern.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var column = index * 2;
            var state = pattern[index];
            matrix[column, 1] = true;
            if (state == 'A' || state == 'F') matrix[column, 0] = true;
            if (state == 'D' || state == 'F') matrix[column, 2] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (matrix, 0, matrixWidth - 1);
    }

    /// <summary>
    /// 将 20/25/29/31 位 IMb 数字转换为 USPS 四状态条字符。
    /// </summary>
    /// <remarks>
    /// 该实现对应 USPS-B-3200 第 2.2 节：路由/跟踪数字进入 102 位整数，
    /// 通过 CRC-11 和 10 个混合进制 codeword，再经 5-of-13/2-of-13 表及
    /// FCS 位取反得到 65 根 A/D/T/F 条。方法保持 internal，避免把 USPS
    /// 构造细节扩展为公共 API。
    /// </remarks>
    internal static string EncodeIntelligentMailPattern(string data)
        => EncodeIntelligentMailPattern(NormalizeBarcodeData(BarcodeType.IntelligentMail, data),
            CancellationToken.None);

    private static string EncodeIntelligentMailPattern(string normalizedData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tracking = normalizedData.Substring(0, 20);
        var routing = normalizedData.Substring(20);
        BigInteger binary = ConvertRoutingCode(routing);
        for (var index = 0; index < tracking.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            binary = binary * (index == 1 ? 5 : 10) + (tracking[index] - '0');
        }

        var binaryBytes = ToRightJustifiedBinary(binary);
        var fcs = GenerateIntelligentMailCrc(binaryBytes);
        var codewords = ConvertToIntelligentMailCodewords(binary);
        codewords[9] = checked(codewords[9] * 2);
        if ((fcs & (1 << 10)) != 0) codewords[0] = checked(codewords[0] + 659);

        var characters = new int[10];
        for (var index = 0; index < codewords.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            characters[index] = CodewordToIntelligentMailCharacter(codewords[index]);
            if ((fcs & (1 << index)) != 0) characters[index] ^= 0x1FFF;
        }

        var result = new char[65];
        for (var index = 0; index < IntelligentMailDescenderCharacters.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descender = (characters[IntelligentMailDescenderCharacters[index]] &
                             (1 << IntelligentMailDescenderBits[index])) != 0;
            var ascender = (characters[IntelligentMailAscenderCharacters[index]] &
                            (1 << IntelligentMailAscenderBits[index])) != 0;
            result[index] = descender
                ? (ascender ? 'F' : 'D')
                : (ascender ? 'A' : 'T');
        }
        return new string(result);
    }

    private static BigInteger ConvertRoutingCode(string routing)
    {
        if (routing.Length == 0) return BigInteger.Zero;
        var value = BigInteger.Parse(routing);
        if (routing.Length == 5) return value + 1;
        if (routing.Length == 9) return value + 100001;
        return value + 1000100001;
    }

    private static byte[] ToRightJustifiedBinary(BigInteger value)
    {
        var bytes = value.ToByteArray();
        if (bytes.Length > 13) throw new ArgumentException("Intelligent Mail 数据超出 102 位编码范围。", nameof(value));
        var result = new byte[13];
        for (var index = 0; index < bytes.Length; index++) result[result.Length - 1 - index] = bytes[index];
        return result;
    }

    private static int GenerateIntelligentMailCrc(byte[] binaryBytes)
    {
        var generator = 0x0F35;
        var fcs = 0x07FF;
        var data = binaryBytes[0] << 5;
        for (var bit = 2; bit < 8; bit++)
        {
            fcs = ((fcs ^ data) & 0x400) != 0 ? ((fcs << 1) ^ generator) : fcs << 1;
            fcs &= 0x7FF;
            data <<= 1;
        }
        for (var byteIndex = 1; byteIndex < binaryBytes.Length; byteIndex++)
        {
            data = binaryBytes[byteIndex] << 3;
            for (var bit = 0; bit < 8; bit++)
            {
                fcs = ((fcs ^ data) & 0x400) != 0 ? ((fcs << 1) ^ generator) : fcs << 1;
                fcs &= 0x7FF;
                data <<= 1;
            }
        }
        return fcs;
    }

    private static int[] ConvertToIntelligentMailCodewords(BigInteger binary)
    {
        var codewords = new int[10];
        var value = binary;
        codewords[9] = (int)(value % 636);
        value /= 636;
        for (var index = 8; index >= 1; index--)
        {
            codewords[index] = (int)(value % 1365);
            value /= 1365;
        }
        codewords[0] = (int)value;
        if (codewords[0] < 0 || codewords[0] > 658)
            throw new ArgumentException("Intelligent Mail 数据无法转换为合法 codeword。", nameof(binary));
        return codewords;
    }

    private static int CodewordToIntelligentMailCharacter(int codeword)
    {
        if (codeword <= 1286) return IntelligentMailFiveOfThirteen[codeword];
        if (codeword <= 1364) return IntelligentMailTwoOfThirteen[codeword - 1287];
        throw new ArgumentOutOfRangeException(nameof(codeword));
    }

    private static int[] BuildIntelligentMailNofThirteenTable(int ones, int length)
    {
        var table = new int[length];
        var lower = 0;
        var upper = length - 1;
        for (var count = 0; count < 8192; count++)
        {
            if (CountBits(count) != ones) continue;
            var reverse = ReverseThirteenBits(count);
            if (reverse < count) continue;
            if (count == reverse) table[upper--] = count;
            else
            {
                table[lower++] = count;
                table[lower++] = reverse;
            }
        }
        if (lower != upper + 1) throw new InvalidOperationException("USPS IMb character table 初始化失败。");
        return table;
    }

    private static int CountBits(int value)
    {
        var count = 0;
        for (var bit = 0; bit < 13; bit++) if ((value & (1 << bit)) != 0) count++;
        return count;
    }

    private static int ReverseThirteenBits(int value)
    {
        var reversed = 0;
        for (var bit = 0; bit < 13; bit++)
        {
            reversed = (reversed << 1) | ((value >> bit) & 1);
        }
        return reversed;
    }

    private static readonly int[] IntelligentMailFiveOfThirteen =
        BuildIntelligentMailNofThirteenTable(5, 1287);
    private static readonly int[] IntelligentMailTwoOfThirteen =
        BuildIntelligentMailNofThirteenTable(2, 78);

    private static readonly int[] IntelligentMailDescenderCharacters =
    {
        7, 1, 9, 5, 8, 0, 2, 4, 6, 3, 5, 8, 9, 7, 3, 0, 6, 1, 7, 4,
        6, 8, 9, 2, 5, 1, 7, 5, 4, 3, 8, 7, 6, 0, 2, 5, 4, 9, 3, 0,
        1, 6, 8, 2, 0, 4, 5, 9, 6, 7, 5, 2, 6, 3, 8, 5, 1, 9, 8, 7,
        4, 0, 2, 6, 3
    };


    private static readonly int[] IntelligentMailDescenderBits =
    {
        2, 10, 12, 5, 9, 1, 5, 4, 3, 9, 11, 5, 10, 1, 6, 3, 4, 1, 10, 0,
        2, 11, 8, 6, 1, 12, 3, 8, 6, 4, 4, 11, 0, 6, 1, 9, 11, 5, 3, 7,
        3, 10, 7, 11, 8, 2, 10, 3, 5, 8, 0, 3, 12, 11, 8, 4, 5, 1, 3, 0,
        7, 12, 9, 8, 10
    };

    private static readonly int[] IntelligentMailAscenderCharacters =
    {
        4, 0, 2, 6, 3, 5, 1, 9, 8, 7, 1, 2, 0, 6, 4, 8, 2, 9, 5, 3,
        0, 1, 3, 7, 4, 6, 8, 9, 2, 0, 5, 1, 9, 4, 3, 8, 6, 7, 1, 2,
        4, 3, 9, 5, 7, 8, 3, 0, 2, 1, 4, 0, 9, 1, 7, 0, 2, 4, 6, 3,
        7, 1, 9, 5, 8
    };

    private static readonly int[] IntelligentMailAscenderBits =
    {
        3, 0, 8, 11, 1, 12, 8, 11, 10, 6, 4, 12, 2, 7, 9, 6, 7, 9, 2, 8,
        4, 0, 12, 7, 10, 9, 0, 7, 10, 5, 7, 9, 6, 8, 2, 12, 1, 4, 2, 0,
        1, 5, 4, 6, 12, 1, 0, 9, 4, 7, 5, 10, 2, 6, 9, 11, 2, 12, 6, 7,
        5, 11, 0, 3, 2
    };

    private static void SetPostnetBar(BitMatrix matrix, int column, bool fullHeight)
    {
        matrix[column, 0] = true;
        if (fullHeight) matrix[column, 1] = true;
    }

    private static readonly string[] PostnetDigitPatterns =
    {
        "11000", "00011", "00101", "00110", "01001",
        "01010", "01100", "10001", "10010", "10100"
    };

    private static readonly string[] PlanetDigitPatterns =
    {
        "00111", "11100", "11010", "11001", "10110",
        "10101", "10011", "01110", "01101", "01011"
    };

    private static readonly string[] FimPatterns =
    {
        "110010011", "101101101", "110101011", "111010111", "101000101"
    };

    // Code 11 symbols use three bars, two spaces and a narrow inter-symbol gap.
    // Values 0-9 map to digits, 10 to '-', and 11 to the start/stop '*'.
    private static readonly string[] Code11Patterns =
    {
        "101011", "1101011", "1001011", "1100101", "1011011", "1101101",
        "1001101", "1010011", "1101001", "110101", "101101", "1011001"
    };

    // Datalogic/China Post uses the Matrix 2 of 5 digit patterns. The two
    // wide elements are expanded to three modules by EncodeChinaPostPattern.
    private static readonly string[] ChinaPostDigitPatterns =
    {
        "NNWWN", "WNNNW", "NWNNW", "WWNNN", "NNWNW",
        "WNWNN", "NWWNN", "NNNWW", "WNNWN", "NWNWN"
    };

    /*
     * GS1 DataBar tables adapted from libzint rss.c/rss.h.
     * Copyright (C) 2007-2025 Robin Stuart. The source is distributed under
     * the BSD-3-Clause license; the required notice is retained here because
     * these tables are embedded rather than linked at runtime.
     *
     * Redistribution and use in source and binary forms, with or without
     * modification, are permitted provided that the following conditions are met:
     * 1. Redistributions of source code must retain the above copyright notice,
     *    this list of conditions and the following disclaimer.
     * 2. Redistributions in binary form must reproduce the above copyright notice,
     *    this list of conditions and the following disclaimer in the documentation
     *    and/or other materials provided with the distribution.
     * 3. Neither the name of the project nor the names of its contributors may be
     *    used to endorse or promote products derived from this software without
     *    specific prior written permission.
     * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
     * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
     * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
     * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
     * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
     * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
     * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
     * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
     * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
     * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
     * POSSIBILITY OF SUCH DAMAGE.
     * https://github.com/zint/zint/blob/master/backend/rss.c
     * https://github.com/zint/zint/blob/master/backend/rss.h
     */
    private static readonly int[] Gs1DataBarGroupSums =
        { 0, 161, 961, 2015, 2715, 0, 336, 1036, 1516 };

    private static readonly int[] Gs1DataBarEvenOdd =
        { 1, 10, 34, 70, 126, 4, 20, 48, 81 };

    private static readonly int[] Gs1DataBarModules =
        { 12, 10, 8, 6, 4, 5, 7, 9, 11, 4, 6, 8, 10, 12, 10, 8, 6, 4 };

    private static readonly int[] Gs1DataBarWidest =
        { 8, 6, 4, 3, 1, 2, 4, 6, 8 };

    private static readonly int[,] Gs1DataBarFinderPatterns =
    {
        { 3, 8, 2, 1, 1 }, { 3, 5, 5, 1, 1 }, { 3, 3, 7, 1, 1 },
        { 3, 1, 9, 1, 1 }, { 2, 7, 4, 1, 1 }, { 2, 5, 6, 1, 1 },
        { 2, 3, 8, 1, 1 }, { 1, 5, 7, 1, 1 }, { 1, 3, 9, 1, 1 }
    };

    private static readonly int[,] Gs1DataBarChecksumWeights =
    {
        { 1, 3, 9, 27, 2, 6, 18, 54 },
        { 4, 12, 36, 29, 8, 24, 72, 58 },
        { 16, 48, 65, 37, 32, 17, 51, 74 },
        { 64, 34, 23, 69, 49, 68, 46, 59 }
    };

    private static readonly int[] Gs1DataBarLimitedGroupSums =
        { 0, 183064, 820064, 1000776, 1491021, 1979845, 1996939 };

    private static readonly int[] Gs1DataBarLimitedTEven =
        { 28, 728, 6454, 203, 2408, 1, 16632 };

    private static readonly int[] Gs1DataBarLimitedModules =
        { 17, 13, 9, 15, 11, 19, 7 };

    private static readonly int[] Gs1DataBarLimitedWidest =
        { 6, 5, 3, 5, 4, 8, 1 };

    private static readonly int[] Gs1DataBarExpandedGroupSums =
        { 0, 348, 1388, 2948, 3988 };

    private static readonly int[] Gs1DataBarExpandedTEven =
        { 4, 20, 52, 104, 204 };

    private static readonly int[] Gs1DataBarExpandedModules =
        { 12, 10, 8, 6, 4 };

    private static readonly int[] Gs1DataBarExpandedWidest =
        { 7, 5, 4, 3, 1 };

    private static readonly int[,] Gs1DataBarExpandedChecksumWeights =
    {
        { 1, 3, 9, 27, 81, 32, 96, 77 },
        { 20, 60, 180, 118, 143, 7, 21, 63 },
        { 189, 145, 13, 39, 117, 140, 209, 205 },
        { 193, 157, 49, 147, 19, 57, 171, 91 },
        { 62, 186, 136, 197, 169, 85, 44, 132 },
        { 185, 133, 188, 142, 4, 12, 36, 108 },
        { 113, 128, 173, 97, 80, 29, 87, 50 },
        { 150, 28, 84, 41, 123, 158, 52, 156 },
        { 46, 138, 203, 187, 139, 206, 196, 166 },
        { 76, 17, 51, 153, 37, 111, 122, 155 },
        { 43, 129, 176, 106, 107, 110, 119, 146 },
        { 16, 48, 144, 10, 30, 90, 59, 177 },
        { 109, 116, 137, 200, 178, 112, 125, 164 },
        { 70, 210, 208, 202, 184, 130, 179, 115 },
        { 134, 191, 151, 31, 93, 68, 204, 190 },
        { 148, 22, 66, 198, 172, 94, 71, 2 },
        { 6, 18, 54, 162, 64, 192, 154, 40 },
        { 120, 149, 25, 75, 14, 42, 126, 167 },
        { 79, 26, 78, 23, 69, 207, 199, 175 },
        { 103, 98, 83, 38, 114, 131, 182, 124 },
        { 161, 61, 183, 127, 170, 88, 53, 159 },
        { 55, 165, 73, 8, 24, 72, 5, 15 },
        { 45, 135, 194, 160, 58, 174, 100, 89 }
    };

    private static readonly int[,] Gs1DataBarExpandedWeightRows =
    {
        { 0, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 5, 6, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 9, 10, 3, 4, 13, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 17, 18, 3, 4, 13, 14, 7, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 17, 18, 3, 4, 13, 14, 11, 12, 21, 22, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 17, 18, 3, 4, 13, 14, 15, 16, 21, 22, 19, 20, 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 0, 0, 0, 0, 0, 0 },
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 17, 18, 15, 16, 0, 0, 0, 0 },
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 17, 18, 19, 20, 21, 22, 0, 0 },
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 13, 14, 11, 12, 17, 18, 15, 16, 21, 22, 19, 20 }
    };

    private static readonly int[,] Gs1DataBarExpandedFinderPatterns =
    {
        { 1, 8, 4, 1, 1 },
        { 1, 1, 4, 8, 1 },
        { 3, 6, 4, 1, 1 },
        { 1, 1, 4, 6, 3 },
        { 3, 4, 6, 1, 1 },
        { 1, 1, 6, 4, 3 },
        { 3, 2, 8, 1, 1 },
        { 1, 1, 8, 2, 3 },
        { 2, 6, 5, 1, 1 },
        { 1, 1, 5, 6, 2 },
        { 2, 2, 9, 1, 1 },
        { 1, 1, 9, 2, 2 }
    };

    private static readonly int[,] Gs1DataBarExpandedFinderSequence =
    {
        { 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        { 1, 4, 3, 0, 0, 0, 0, 0, 0, 0, 0 },
        { 1, 6, 3, 8, 0, 0, 0, 0, 0, 0, 0 },
        { 1, 10, 3, 8, 5, 0, 0, 0, 0, 0, 0 },
        { 1, 10, 3, 8, 7, 12, 0, 0, 0, 0, 0 },
        { 1, 10, 3, 8, 9, 12, 11, 0, 0, 0, 0 },
        { 1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0 },
        { 1, 2, 3, 4, 5, 6, 7, 10, 9, 0, 0 },
        { 1, 2, 3, 4, 5, 6, 7, 10, 11, 12, 0 },
        { 1, 2, 3, 4, 5, 8, 7, 10, 9, 12, 11 }
    };

    private static readonly int[,] Gs1DataBarLimitedChecksumWeights =
    {
        { 1, 3, 9, 27, 81, 65, 17, 51, 64, 14, 42, 37, 22, 66 },
        { 20, 60, 2, 6, 18, 54, 73, 41, 34, 13, 39, 28, 84, 74 }
    };

    private static readonly int[,] Gs1DataBarLimitedFinderPatterns =
    {
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 3, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 3, 2, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 3, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 2, 1, 1, 3, 2, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 2, 1, 2, 3, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 3, 1, 1, 3, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 3, 2, 1, 1 },
        { 1, 1, 1, 1, 1, 2, 1, 1, 1, 2, 3, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 2, 1, 2, 1, 1, 3, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 3, 1, 1, 1, 1, 3, 1, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 3, 2, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 1, 1, 1, 2, 3, 1, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 3, 1, 1, 1 },
        { 1, 1, 1, 2, 1, 2, 1, 1, 1, 1, 3, 1, 1, 1 },
        { 1, 1, 1, 3, 1, 1, 1, 1, 1, 1, 3, 1, 1, 1 },
        { 1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 3, 2, 1, 1 },
        { 1, 2, 1, 1, 1, 1, 1, 1, 1, 2, 3, 1, 1, 1 },
        { 1, 2, 1, 1, 1, 1, 1, 2, 1, 1, 3, 1, 1, 1 },
        { 1, 2, 1, 1, 1, 2, 1, 1, 1, 1, 3, 1, 1, 1 },
        { 1, 2, 1, 2, 1, 1, 1, 1, 1, 1, 3, 1, 1, 1 },
        { 1, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 2, 1, 2, 3, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 2, 3, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 2, 2, 1, 2, 2, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 3, 2, 1, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 2, 1, 1, 2, 1, 2, 2, 1, 1 },
        { 1, 1, 1, 1, 1, 2, 1, 1, 2, 2, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 2, 1, 2, 2, 1, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 3, 1, 1, 2, 1, 2, 1, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 1, 1, 2, 1, 2, 2, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 1, 2, 2, 1, 2, 1, 1, 1 },
        { 1, 1, 1, 2, 1, 2, 1, 1, 2, 1, 2, 1, 1, 1 },
        { 1, 1, 1, 3, 1, 1, 1, 1, 2, 1, 2, 1, 1, 1 },
        { 1, 2, 1, 1, 1, 1, 1, 1, 2, 1, 2, 2, 1, 1 },
        { 1, 2, 1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1 },
        { 1, 2, 1, 1, 1, 1, 1, 2, 2, 1, 2, 1, 1, 1 },
        { 1, 2, 1, 1, 1, 2, 1, 1, 2, 1, 2, 1, 1, 1 },
        { 1, 2, 1, 2, 1, 1, 1, 1, 2, 1, 2, 1, 1, 1 },
        { 1, 3, 1, 1, 1, 1, 1, 1, 2, 1, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 3, 1, 1, 3, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 3, 2, 1, 2, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 2, 3, 1, 1, 2, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 1, 1, 3, 1, 1, 2, 1, 1 },
        { 1, 2, 1, 1, 1, 1, 1, 1, 3, 1, 1, 2, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 2, 3, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 2, 1, 1, 2, 2, 2, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 2, 1, 1, 3, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 2, 1, 1, 1, 2, 2, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 2, 1, 1, 2, 2, 1, 1, 1 },
        { 1, 1, 1, 2, 1, 1, 2, 2, 1, 1, 2, 1, 1, 1 },
        { 1, 1, 1, 2, 1, 2, 2, 1, 1, 1, 2, 1, 1, 1 },
        { 1, 1, 1, 3, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1 },
        { 1, 2, 1, 1, 1, 1, 2, 1, 1, 1, 2, 2, 1, 1 },
        { 1, 2, 1, 1, 1, 1, 2, 1, 1, 2, 2, 1, 1, 1 },
        { 1, 2, 1, 2, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 2, 3, 1, 1 },
        { 1, 1, 1, 1, 2, 1, 1, 1, 1, 2, 2, 2, 1, 1 },
        { 1, 1, 1, 1, 2, 1, 1, 1, 1, 3, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 2, 1, 1, 2, 1, 1, 2, 2, 1, 1 },
        { 1, 1, 1, 1, 2, 1, 1, 2, 1, 2, 2, 1, 1, 1 },
        { 1, 1, 1, 1, 2, 2, 1, 1, 1, 1, 2, 2, 1, 1 },
        { 1, 2, 1, 1, 2, 1, 1, 1, 1, 1, 2, 2, 1, 1 },
        { 1, 2, 1, 1, 2, 1, 1, 1, 1, 2, 2, 1, 1, 1 },
        { 1, 2, 1, 1, 2, 1, 1, 2, 1, 1, 2, 1, 1, 1 },
        { 1, 2, 1, 1, 2, 2, 1, 1, 1, 1, 2, 1, 1, 1 },
        { 1, 2, 1, 2, 2, 1, 1, 1, 1, 1, 2, 1, 1, 1 },
        { 1, 3, 1, 1, 2, 1, 1, 1, 1, 1, 2, 1, 1, 1 },
        { 1, 1, 2, 1, 1, 1, 1, 1, 1, 1, 2, 3, 1, 1 },
        { 1, 1, 2, 1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1 },
        { 1, 1, 2, 1, 1, 1, 1, 1, 1, 3, 2, 1, 1, 1 },
        { 1, 1, 2, 1, 1, 1, 1, 2, 1, 1, 2, 2, 1, 1 },
        { 1, 1, 2, 1, 1, 1, 1, 2, 1, 2, 2, 1, 1, 1 },
        { 1, 1, 2, 1, 1, 1, 1, 3, 1, 1, 2, 1, 1, 1 },
        { 1, 1, 2, 1, 1, 2, 1, 1, 1, 1, 2, 2, 1, 1 },
        { 1, 1, 2, 1, 1, 2, 1, 1, 1, 2, 2, 1, 1, 1 },
        { 1, 1, 2, 2, 1, 1, 1, 1, 1, 1, 2, 2, 1, 1 },
        { 2, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1 },
        { 2, 1, 1, 1, 1, 1, 1, 1, 1, 3, 2, 1, 1, 1 },
        { 2, 1, 1, 1, 1, 1, 1, 2, 1, 1, 2, 2, 1, 1 },
        { 2, 1, 1, 1, 1, 1, 1, 2, 1, 2, 2, 1, 1, 1 },
        { 2, 1, 1, 1, 1, 1, 1, 3, 1, 1, 2, 1, 1, 1 },
        { 2, 1, 1, 1, 1, 2, 1, 1, 1, 2, 2, 1, 1, 1 },
        { 2, 1, 1, 1, 1, 2, 1, 2, 1, 1, 2, 1, 1, 1 },
        { 2, 1, 1, 2, 1, 1, 1, 1, 1, 2, 2, 1, 1, 1 },
        { 2, 1, 1, 1, 1, 1, 1, 1, 2, 2, 1, 2, 1, 1 }
    };

    /*
     * Telepen width table adapted from libzint telepen.c.
     * Copyright (C) 2008-2021 Robin Stuart. The source is distributed under
     * the BSD-3-Clause license; the required notice and disclaimer are kept
     * here because this table is embedded rather than linked at runtime.
     *
     * Redistribution and use in source and binary forms, with or without
     * modification, are permitted provided that the following conditions are met:
     * 1. Redistributions of source code must retain the above copyright notice,
     *    this list of conditions and the following disclaimer.
     * 2. Redistributions in binary form must reproduce the above copyright notice,
     *    this list of conditions and the following disclaimer in the documentation
     *    and/or other materials provided with the distribution.
     * 3. Neither the name of the project nor the names of its contributors may be
     *    used to endorse or promote products derived from this software without
     *    specific prior written permission.
     * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
     * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
     * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
     * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
     * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
     * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
     * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
     * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
     * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
     * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
     * POSSIBILITY OF SUCH DAMAGE.
     * https://github.com/zint/zint/blob/master/backend/telepen.c
     */
    private static readonly string[] TelepenPatterns =
    {
        "31313131", "1131313111", "33313111", "1111313131", "3111313111", "11333131", "13133131", "111111313111",
        "31333111", "1131113131", "33113131", "1111333111", "3111113131", "1113133111", "1311133111", "111111113131",
        "3131113111", "11313331", "333331", "111131113111", "31113331", "1133113111", "1313113111", "1111113331",
        "31131331", "113111113111", "3311113111", "1111131331", "311111113111", "1113111331", "1311111331", "11111111113111",
        "31313311", "1131311131", "33311131", "1111313311", "3111311131", "11333311", "13133311", "111111311131",
        "31331131", "1131113311", "33113311", "1111331131", "3111113311", "1113131131", "1311131131", "111111113311",
        "3131111131", "1131131311", "33131311", "111131111131", "3111131311", "1133111131", "1313111131", "111111131311",
        "3113111311", "113111111131", "3311111131", "111113111311", "311111111131", "111311111311", "131111111311", "11111111111131",
        "3131311111", "11313133", "333133", "111131311111", "31113133", "1133311111", "1313311111", "1111113133",
        "313333", "113111311111", "3311311111", "11113333", "311111311111", "11131333", "13111333", "11111111311111",
        "31311133", "1131331111", "33331111", "1111311133", "3111331111", "11331133", "13131133", "111111331111",
        "3113131111", "1131111133", "33111133", "111113131111", "3111111133", "111311131111", "131111131111", "111111111133",
        "31311313", "113131111111", "3331111111", "1111311313", "311131111111", "11331313", "13131313", "11111131111111",
        "3133111111", "1131111313", "33111313", "111133111111", "3111111313", "111313111111", "131113111111", "111111111313",
        "313111111111", "1131131113", "33131113", "11113111111111", "3111131113", "113311111111", "131311111111", "111111131113",
        "3113111113", "11311111111111", "331111111111", "111113111113", "31111111111111", "111311111113", "131111111113", "1111111111111111"
    };

    /// <summary>按统一模型语义规范化 Raster I2OF5 校验变体的数据。</summary>
    /// <remarks>
    /// I2OF5C 使用 CPCL 3/1 Mod10；I2OF5G 使用 German Post 4/9 Mod10。
    /// 规范化后的数据始终是 ZXing ITF 可编码的偶数位数字。
    /// </remarks>
    internal static string NormalizeBarcodeData(BarcodeType type, string data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (type == BarcodeType.Postnet)
        {
            var normalized = data.Replace("-", string.Empty);
            if ((normalized.Length != 5 && normalized.Length != 9 && normalized.Length != 11) ||
                normalized.Any(character => character < '0' || character > '9'))
                throw new ArgumentException("POSTNET 需要 5、9 或 11 位 ASCII 数字，可用连字符分组。", nameof(data));
            return normalized;
        }
        if (type == BarcodeType.Planet)
        {
            var normalized = data.Replace("-", string.Empty);
            if ((normalized.Length != 12 && normalized.Length != 14) ||
                normalized.Any(character => character < '0' || character > '9'))
                throw new ArgumentException("PLANET 需要 12 或 14 位 ASCII 数字，可用连字符分组。", nameof(data));
            var sum = 0;
            for (var index = 0; index < normalized.Length; index++) sum += normalized[index] - '0';
            if (sum % 10 != 0)
                throw new ArgumentException("PLANET 校验位错误。", nameof(data));
            return normalized;
        }
        if (type == BarcodeType.Fim)
        {
            if (data.Length != 1 || data[0] < 'A' || data[0] > 'E')
                throw new ArgumentException("FIM 仅支持 A、B、C、D 或 E 模式。", nameof(data));
            return data;
        }
        if (type == BarcodeType.IntelligentMail)
        {
            var normalized = data.Replace("-", string.Empty);
            if ((normalized.Length != 20 && normalized.Length != 25 &&
                 normalized.Length != 29 && normalized.Length != 31) ||
                normalized.Any(character => character < '0' || character > '9'))
                throw new ArgumentException("Intelligent Mail 条码需要 20、25、29 或 31 位 ASCII 数字，可用连字符分组。", nameof(data));
            return normalized;
        }
        if (type == BarcodeType.UccEan128)
        {
            if (Gs1ApplicationIdentifierParser.ShouldNormalize(data))
                data = Gs1ApplicationIdentifierParser.NormalizeForProtocol(data, nameof(data), 255);

            if (data.Length < 3 || data[0] < '0' || data[0] > '9' ||
                data[1] < '0' || data[1] > '9')
                throw new ArgumentException("GS1-128 需要以数字应用标识开头的元素串。", nameof(data));
            for (var index = 0; index < data.Length; index++)
            {
                var character = data[index];
                if (character != '\u001D' && (character < 32 || character > 126))
                    throw new ArgumentException("GS1-128 仅接受 7-bit ASCII 或 ASCII 29 分隔符。", nameof(data));
            }
            return data;
        }
        if (type == BarcodeType.Code128Auto)
        {
            if (data.Length == 0 || data.Length > 255 || data.Any(character => character > 127))
                throw new ArgumentException("CODE128 auto 条码需要 1–255 字节的 7-bit ASCII 数据。", nameof(data));
            return data;
        }
        if (type == BarcodeType.Code49)
        {
            if (data.Length == 0 || data.Length > 81 || data.Any(character => character > 127))
                throw new ArgumentException("Code 49 条码需要 1–81 个 7-bit ASCII 字符。", nameof(data));
            return data;
        }
        if (type == BarcodeType.Itf14)
        {
            if (data.Length != 13 || data.Any(character => character < '0' || character > '9'))
                throw new ArgumentException("ITF-14 需要 13 位数字 payload。", nameof(data));
            return data + CalculateI2of5CheckDigit(data, 3, 1);
        }
        if (type == BarcodeType.Plessey)
        {
            if (data.Length == 0 || data.Any(character => character < '0' || character > '9'))
                throw new ArgumentException("Plessey 条码只接受非空 ASCII 数字。", nameof(data));
            return data;
        }
        if (type == BarcodeType.Code11)
        {
            if (data.Length == 0 || data.Length > 255 ||
                data.Any(character => (character < '0' || character > '9') && character != '-'))
                throw new ArgumentException("Code 11 条码只接受 1–255 个 ASCII 数字或连字符。", nameof(data));
            return data;
        }
        if (type == BarcodeType.Telepen)
        {
            if (data.Length == 0 || data.Length > 30 || data.Any(character => character > 127))
                throw new ArgumentException("Telepen Alpha 条码需要 1–30 个 7-bit ASCII 字符。", nameof(data));
            return data;
        }
        if (type == BarcodeType.Telepenn)
        {
            if (data.Length == 0 || data.Length > 60 || data.Any(character => character < '0' || character > '9'))
                throw new ArgumentException("Telepen Numeric 条码需要 1–60 位 ASCII 数字。", nameof(data));
            return data;
        }
        if (type == BarcodeType.ChinaPost)
        {
            if (data.Length == 0 || data.Any(character => character < '0' || character > '9'))
                throw new ArgumentException("China Post 条码需要非空 ASCII 数字。", nameof(data));
            return data;
        }
        if (type == BarcodeType.Gs1DataBarOmnidirectional || type == BarcodeType.Gs1DataBarTruncated ||
            type == BarcodeType.Gs1DataBarStacked)
        {
            if (data.Length != 13 || data.Any(character => character < '0' || character > '9'))
                throw new ArgumentException("GS1 DataBar Omnidirectional/Truncated/Stacked 条码需要 13 位 ASCII 数字 payload。", nameof(data));
            return data;
        }
        if (type == BarcodeType.Gs1DataBarLimited)
        {
            if (data.Length != 13 || data.Any(character => character < '0' || character > '9') ||
                (data[0] != '0' && data[0] != '1'))
                throw new ArgumentException("GS1 DataBar Limited 条码需要以 0 或 1 开头的 13 位 ASCII 数字 payload。", nameof(data));
            return data;
        }
        if (type == BarcodeType.Gs1DataBarExpanded)
        {
            if (Gs1ApplicationIdentifierParser.IsBracketed(data) &&
                !data.StartsWith("(01)", StringComparison.Ordinal) &&
                !data.StartsWith("[01]", StringComparison.Ordinal))
            {
                var bracketed = Gs1ApplicationIdentifierParser.Normalize(data, nameof(data));
                ValidateExpandedMethod2Data(bracketed, nameof(data), false);
                Gs1DataBarExpandedSymbolCharacters(bracketed);
                return bracketed;
            }

            var hasBracketedAiSequence = Gs1ApplicationIdentifierParser.IsExpandedGenericGtin(data);
            var normalized = hasBracketedAiSequence
                ? Gs1ApplicationIdentifierParser.Normalize(data, nameof(data))
                : data;
            if (normalized.StartsWith("(01)", StringComparison.Ordinal) ||
                normalized.StartsWith("[01]", StringComparison.Ordinal))
                normalized = "01" + normalized.Substring(4);

            var method3 = false;
            var method4 = false;
            var method5 = false;
            var method6 = false;
            var method7 = false;
            var method8 = false;
            var method9 = false;
            var method10 = false;
            var method11 = false;
            var method12 = false;
            var method13 = false;
            var method14 = false;
            if (normalized.Length >= 22 &&
                ((normalized[16] == '(' && normalized[21] == ')') ||
                 (normalized[16] == '[' && normalized[21] == ']')))
            {
                var ai = normalized.Substring(17, 4);
                var structuredSuffix = normalized.Length >= 28 ? normalized.Substring(22) : string.Empty;
                var weightText = structuredSuffix.Length >= 6 ? structuredSuffix.Substring(0, 6) : string.Empty;
                var weightIsNumeric = weightText.Length == 6 &&
                    weightText.All(character => character >= '0' && character <= '9');
                var weight = weightIsNumeric
                    ? int.Parse(weightText, System.Globalization.CultureInfo.InvariantCulture)
                    : -1;
                var selectedDateMethod = 0;
                var dateAi = string.Empty;
                if (structuredSuffix.Length >= 16 &&
                    ((structuredSuffix[6] == '(' && structuredSuffix[9] == ')') ||
                     (structuredSuffix[6] == '[' && structuredSuffix[9] == ']')))
                {
                    dateAi = structuredSuffix.Substring(7, 2);
                    if (IsExpandedDateAi(dateAi))
                    {
                        if (ai.StartsWith("310", StringComparison.Ordinal))
                            selectedDateMethod = 6 + (dateAi[1] - '0');
                        else if (ai.StartsWith("320", StringComparison.Ordinal))
                            selectedDateMethod = 7 + (dateAi[1] - '0');
                    }
                }
                if (ai.StartsWith("310", StringComparison.Ordinal) && ai[3] >= '0' && ai[3] <= '9')
                {
                    if (selectedDateMethod != 0)
                    {
                        var marker = selectedDateMethod == 7 ? ExpandedMethod7Marker :
                            selectedDateMethod == 9 ? ExpandedMethod9Marker :
                            selectedDateMethod == 11 ? ExpandedMethod11Marker : ExpandedMethod13Marker;
                        var dateValue = structuredSuffix.Substring(10, 6);
                        var dateTail = NormalizeExpandedDateTail(
                            structuredSuffix.Substring(16), structuredSuffix[6], nameof(data));
                        normalized = normalized.Substring(0, 16) + marker + ai +
                            structuredSuffix.Substring(0, 6) + dateAi + dateValue + dateTail;
                        method7 = selectedDateMethod == 7;
                        method9 = selectedDateMethod == 9;
                        method11 = selectedDateMethod == 11;
                        method13 = selectedDateMethod == 13;
                    }
                    else
                    {
                        var useMethod3 = ai == "3103" && weightIsNumeric && weight <= 32767;
                        normalized = normalized.Substring(0, 16) +
                            (useMethod3 ? ExpandedMethod3Marker : ExpandedMethod7Marker) + ai +
                            normalized.Substring(22);
                        method3 = useMethod3;
                        method7 = !useMethod3;
                    }
                }
                else if (ai.StartsWith("320", StringComparison.Ordinal) && ai[3] >= '0' && ai[3] <= '9')
                {
                    if (selectedDateMethod != 0)
                    {
                        var marker = selectedDateMethod == 8 ? ExpandedMethod8Marker :
                            selectedDateMethod == 10 ? ExpandedMethod10Marker :
                            selectedDateMethod == 12 ? ExpandedMethod12Marker : ExpandedMethod14Marker;
                        var dateValue = structuredSuffix.Substring(10, 6);
                        var dateTail = NormalizeExpandedDateTail(
                            structuredSuffix.Substring(16), structuredSuffix[6], nameof(data));
                        normalized = normalized.Substring(0, 16) + marker + ai +
                            structuredSuffix.Substring(0, 6) + dateAi + dateValue + dateTail;
                        method8 = selectedDateMethod == 8;
                        method10 = selectedDateMethod == 10;
                        method12 = selectedDateMethod == 12;
                        method14 = selectedDateMethod == 14;
                    }
                    else
                    {
                        var useMethod4 = (ai == "3202" && weightIsNumeric && weight <= 9999) ||
                            (ai == "3203" && weightIsNumeric && weight <= 22767);
                        normalized = normalized.Substring(0, 16) +
                            (useMethod4 ? ExpandedMethod4Marker : ExpandedMethod8Marker) + ai +
                            normalized.Substring(22);
                        method4 = useMethod4;
                        method8 = !useMethod4;
                    }
                }
                else if (ai.StartsWith("392", StringComparison.Ordinal) && ai[3] >= '0' && ai[3] <= '3')
                {
                    normalized = normalized.Substring(0, 16) + ExpandedMethod5Marker + ai +
                        normalized.Substring(22);
                    method5 = true;
                }
                else if (ai.StartsWith("393", StringComparison.Ordinal) && ai[3] >= '0' && ai[3] <= '3')
                {
                    normalized = normalized.Substring(0, 16) + ExpandedMethod6Marker + ai +
                        normalized.Substring(22);
                    method6 = true;
                }
            }

            var hasGtinMethod1Prefix = normalized.Length >= 16 && normalized[0] == '0' && normalized[1] == '1' &&
                normalized.Substring(0, 16).All(character => character >= '0' && character <= '9');
            if (!hasGtinMethod1Prefix)
            {
                ValidateExpandedMethod2Data(normalized, data);
                Gs1DataBarExpandedSymbolCharacters(normalized);
                return normalized;
            }
            var expandedExpected = CalculateI2of5CheckDigit(normalized.Substring(2, 13), 3, 1);
            if (normalized[15] != expandedExpected)
                throw new ArgumentException("GS1 DataBar Expanded GTIN-14 校验位错误。", nameof(data));

            var compressedMethod = method3 || method4 || method5 || method6 || method7 || method8 ||
                method9 || method10 || method11 || method12 || method13 || method14;
            if (!compressedMethod && IsExpandedStructuredAi(normalized, 16))
                throw new ArgumentException(
                    "GS1 DataBar Expanded 通用字段起点的结构化 AI 仅支持 (3103)、(3202)、(3203)、(392x)、(393x) 和带日期的 (310x)/(320x)。",
                    nameof(data));
            var expandedData = compressedMethod ? normalized.Remove(16, 1) : normalized;
            var dateMethod = method9 || method10 || method11 || method12 || method13 || method14 ||
                ((method7 || method8) && expandedData.Length >= 34);
            var generalStart = method3 || method4 ? 26 :
                method5 ? 20 : method6 ? 23 : dateMethod ? 34 : method7 || method8 ? 26 : 16;
            if (method3 || method4)
            {
                var ai = expandedData.Length >= 20 ? expandedData.Substring(16, 4) : string.Empty;
                if (expandedData.Length < 26 ||
                    (method3 && ai != "3103") ||
                    (method4 && ai != "3202" && ai != "3203"))
                    throw new ArgumentException(
                        "GS1 DataBar Expanded Method 3/4 需要 (3103)、(3202) 或 (3203) 后接 6 位重量。",
                        nameof(data));
                var weightText = expandedData.Substring(20, 6);
                if (IsExpandedTwoDigitStructuredAi(expandedData, 26))
                    throw new ArgumentException(
                        "GS1 DataBar Expanded Method 3/4 的重量后不接受未实现的结构化日期 AI。",
                        nameof(data));
                var maxWeight = method3 ? 32767 : (ai == "3203" ? 22767 : 9999);
                if (weightText.Any(character => character < '0' || character > '9') ||
                    int.Parse(weightText, System.Globalization.CultureInfo.InvariantCulture) > maxWeight)
                    throw new ArgumentException(
                        method3
                            ? "GS1 DataBar Expanded Method 3 需要 (3103) 后接 6 位、范围 000000-032767 的公斤重量。"
                            : ai == "3203"
                                ? "GS1 DataBar Expanded Method 4 需要 (3203) 后接 6 位、范围 000000-022767 的千分之一磅重量。"
                                : "GS1 DataBar Expanded Method 4 需要 (3202) 后接 6 位、范围 000000-009999 的百分之一磅重量。",
                        nameof(data));
            }
            else if (dateMethod)
            {
                var ai = expandedData.Length >= 20 ? expandedData.Substring(16, 4) : string.Empty;
                var dateAi = expandedData.Length >= 28 ? expandedData.Substring(26, 2) : string.Empty;
                var expectedDateAi = method7 || method8 ? "11" :
                    method9 || method10 ? "13" : method11 || method12 ? "15" : "17";
                if (expandedData.Length < 34 ||
                    (method7 || method9 || method11 || method13) &&
                        (ai.Length != 4 || !ai.StartsWith("310", StringComparison.Ordinal) ||
                         ai[3] < '0' || ai[3] > '9') ||
                    (method8 || method10 || method12 || method14) &&
                        (ai.Length != 4 || !ai.StartsWith("320", StringComparison.Ordinal) ||
                         ai[3] < '0' || ai[3] > '9') ||
                    dateAi != expectedDateAi)
                    throw new ArgumentException(
                        "GS1 DataBar Expanded Method 7-14 需要 (310x)/(320x) 后接日期 AI 和 6 位日期。",
                        nameof(data));
                var weightText = expandedData.Substring(20, 6);
                if (weightText.Any(character => character < '0' || character > '9') ||
                    int.Parse(weightText, System.Globalization.CultureInfo.InvariantCulture) > 99999 ||
                    !TryParseExpandedDate(expandedData.Substring(28, 6), out _))
                    throw new ArgumentException(
                        "GS1 DataBar Expanded Method 7-14 需要 000000-099999 范围的 6 位重量和有效日期。",
                        nameof(data));
            }
            else if (method7 || method8)
            {
                var ai = expandedData.Length >= 20 ? expandedData.Substring(16, 4) : string.Empty;
                if (expandedData.Length != 26 ||
                    (method7 && (ai.Length != 4 || !ai.StartsWith("310", StringComparison.Ordinal) ||
                                 ai[3] < '0' || ai[3] > '9')) ||
                    (method8 && (ai.Length != 4 || !ai.StartsWith("320", StringComparison.Ordinal) ||
                                 ai[3] < '0' || ai[3] > '9')))
                    throw new ArgumentException(
                        "GS1 DataBar Expanded Method 7/8 需要 (310x) 或 (320x) 后接 6 位重量。",
                        nameof(data));
                var weightText = expandedData.Substring(20, 6);
                if (IsExpandedTwoDigitStructuredAi(expandedData, 26))
                    throw new ArgumentException(
                        "GS1 DataBar Expanded Method 7/8 的重量后不接受未实现的结构化日期 AI。",
                        nameof(data));
                if (weightText.Any(character => character < '0' || character > '9') ||
                    int.Parse(weightText, System.Globalization.CultureInfo.InvariantCulture) > 99999)
                    throw new ArgumentException(
                        "GS1 DataBar Expanded Method 7/8 需要 000000-099999 范围的 6 位重量。",
                        nameof(data));
            }
            else if (method5 || method6)
            {
                var ai = expandedData.Length >= 20 ? expandedData.Substring(16, 4) : string.Empty;
                if (expandedData.Length < generalStart ||
                    (method5 && (ai.Length != 4 || !ai.StartsWith("392", StringComparison.Ordinal) ||
                                 ai[3] < '0' || ai[3] > '3')) ||
                    (method6 && (ai.Length != 4 || !ai.StartsWith("393", StringComparison.Ordinal) ||
                                 ai[3] < '0' || ai[3] > '3')))
                    throw new ArgumentException(
                        "GS1 DataBar Expanded Method 5/6 需要 (392x) 或 (393x) 后接价格字段。",
                        nameof(data));
                if (method6)
                {
                    var currency = expandedData.Substring(20, 3);
                    if (currency.Any(character => character < '0' || character > '9'))
                        throw new ArgumentException(
                            "GS1 DataBar Expanded Method 6 需要 3 位 ASCII 数字 ISO 4217 货币代码。",
                            nameof(data));
                }
                var price = expandedData.Substring(generalStart);
                if (price.Length == 0 || price.Any(character => character < '0' || character > '9'))
                    throw new ArgumentException(
                        "GS1 DataBar Expanded Method 5/6 价格字段需要非空 ASCII 数字。",
                        nameof(data));
            }

            for (var index = generalStart; index < expandedData.Length; index++)
            {
                if (ExpandedGeneralFieldType(expandedData[index]) < 0)
                    throw new ArgumentException(
                        "GS1 DataBar Expanded 通用字段仅支持数字、A-Z、a-z、*,-./、ISO/IEC 646 标点、空格和 FNC1。",
                        nameof(data));
                if (index > generalStart && expandedData[index] == ExpandedGeneralFieldFnc1 &&
                    expandedData[index - 1] == ExpandedGeneralFieldFnc1)
                    throw new ArgumentException("GS1 DataBar Expanded 通用字段不能包含连续 FNC1。", nameof(data));
            }
            if (expandedData.Length > generalStart &&
                expandedData[expandedData.Length - 1] == ExpandedGeneralFieldFnc1)
                throw new ArgumentException("GS1 DataBar Expanded 通用字段不能以 FNC1 结束。", nameof(data));
            Gs1DataBarExpandedSymbolCharacters(normalized);
            return normalized;
        }
        if (type == BarcodeType.Msi)
        {
            if (data.Length == 0 || data.Length > 13 || data.Any(character => character < '0' || character > '9'))
                throw new ArgumentException("MSI 条码只接受 1-13 位数字，且不自动追加校验位。", nameof(data));
            return data;
        }
        if (type != BarcodeType.I2OF5C && type != BarcodeType.I2OF5G)
            return data;
        if (data.Length == 0 || data.Any(character => character < '0' || character > '9'))
            throw new ArgumentException("I2OF5 系列条码仅接受非空 7-bit ASCII 数字。", nameof(data));

        if (type == BarcodeType.I2OF5C)
        {
            if ((data.Length & 1) == 0) data = "0" + data;
            return data + CalculateI2of5CheckDigit(data, 3, 1);
        }

        if (data.Length < 11 || data.Length > 14)
            throw new ArgumentException("I2OF5G 需要 11、12、13 或 14 位数字。", nameof(data));
        if ((data.Length & 1) == 1)
            return data + CalculateI2of5CheckDigit(data, 4, 9);

        var expected = CalculateI2of5CheckDigit(data.Substring(0, data.Length - 1), 4, 9);
        if (data[data.Length - 1] != expected)
            throw new ArgumentException("I2OF5G 校验位错误。", nameof(data));
        return data;
    }

    /// <summary>
    /// 返回 Raster 条码的可读文字语义。
    /// </summary>
    /// <param name="type">条码制式。</param>
    /// <param name="data">待编码的数据或括号 AI 输入。</param>
    /// <returns>可读文字内容；DataBar 会补齐隐含 AI 和 GTIN 校验位，并还原输入转义。</returns>
    internal static string HumanReadableBarcodeData(BarcodeType type, string data)
    {
        var normalized = NormalizeBarcodeData(type, data);
        if (type == BarcodeType.Gs1DataBarExpanded)
        {
            var readableInput = Gs1ApplicationIdentifierParser.IsBracketed(data)
                ? Gs1ApplicationIdentifierParser.DecodeDisplayEscapes(data)
                : data;
            if (Gs1ApplicationIdentifierParser.IsExpandedGenericGtin(data))
                return readableInput;
            var bracketedDateTail = HasExpandedBracketedDateTail(data);
            if (Gs1ApplicationIdentifierParser.IsBracketed(data) &&
                !data.StartsWith("(01)", StringComparison.Ordinal) &&
                !data.StartsWith("[01]", StringComparison.Ordinal))
                return readableInput;

            var hasGtinMethod1Prefix = normalized.Length >= 16 && normalized[0] == '0' && normalized[1] == '1' &&
                normalized.Substring(0, 16).All(character => character >= '0' && character <= '9');
            if (!hasGtinMethod1Prefix)
                return normalized;
            if (normalized.Length > 16 &&
                (normalized[16] == ExpandedMethod3Marker || normalized[16] == ExpandedMethod4Marker ||
                 normalized[16] == ExpandedMethod5Marker || normalized[16] == ExpandedMethod6Marker ||
                 normalized[16] == ExpandedMethod7Marker || normalized[16] == ExpandedMethod8Marker ||
                 normalized[16] == ExpandedMethod9Marker || normalized[16] == ExpandedMethod10Marker ||
                 normalized[16] == ExpandedMethod11Marker || normalized[16] == ExpandedMethod12Marker ||
                 normalized[16] == ExpandedMethod13Marker || normalized[16] == ExpandedMethod14Marker))
            {
                var expandedData = normalized.Remove(16, 1);
                var method6 = normalized[16] == ExpandedMethod6Marker;
                var method3Or4 = normalized[16] == ExpandedMethod3Marker ||
                    normalized[16] == ExpandedMethod4Marker;
                var method7Or8 = normalized[16] == ExpandedMethod7Marker ||
                    normalized[16] == ExpandedMethod8Marker;
                var dateMethod = normalized[16] == ExpandedMethod9Marker ||
                    normalized[16] == ExpandedMethod10Marker || normalized[16] == ExpandedMethod11Marker ||
                    normalized[16] == ExpandedMethod12Marker || normalized[16] == ExpandedMethod13Marker ||
                    normalized[16] == ExpandedMethod14Marker || (method7Or8 && expandedData.Length >= 34);
                if (dateMethod)
                {
                    if (bracketedDateTail)
                        return readableInput;
                    return "(01)" + expandedData.Substring(2, 14) + "(" +
                        expandedData.Substring(16, 4) + ")" + expandedData.Substring(20, 6) + "(" +
                        expandedData.Substring(26, 2) + ")" + expandedData.Substring(28, 6) +
                        expandedData.Substring(34);
                }
                var generalStart = method3Or4 || method7Or8 ? 26 : method6 ? 23 : 20;
                var compressedTail = method3Or4 || method7Or8
                    ? expandedData.Substring(20, 6)
                    : method6 ? expandedData.Substring(20, 3) : string.Empty;
                return "(01)" + expandedData.Substring(2, 14) + "(" +
                    expandedData.Substring(16, 4) + ")" + compressedTail +
                    expandedData.Substring(generalStart);
            }
            return "(01)" + normalized.Substring(2);
        }
        if (type != BarcodeType.Gs1DataBarOmnidirectional && type != BarcodeType.Gs1DataBarTruncated &&
            type != BarcodeType.Gs1DataBarStacked &&
            type != BarcodeType.Gs1DataBarLimited)
            return normalized;
        return "(01)" + normalized + CalculateI2of5CheckDigit(normalized, 3, 1);
    }

    private static char CalculatePostnetCheckDigit(string data)
    {
        var sum = 0;
        for (var index = 0; index < data.Length; index++) sum += data[index] - '0';
        return (char)('0' + (10 - sum % 10) % 10);
    }

    private static char CalculateI2of5CheckDigit(string data, int firstWeight, int secondWeight)
    {
        var sum = 0;
        for (var index = 0; index < data.Length; index++)
            sum += (data[index] - '0') * (index % 2 == 0 ? firstWeight : secondWeight);
        return (char)('0' + (10 - sum % 10) % 10);
    }

    private static BarcodeFormat ToBarcodeFormat(BarcodeType type)
    {
        switch (type)
        {
            case BarcodeType.Code128: return BarcodeFormat.CODE_128;
            case BarcodeType.Code128Auto: return BarcodeFormat.CODE_128;
            case BarcodeType.Code39: return BarcodeFormat.CODE_39;
            case BarcodeType.Code93: return BarcodeFormat.CODE_93;
            case BarcodeType.Ean8: return BarcodeFormat.EAN_8;
            case BarcodeType.Ean13: return BarcodeFormat.EAN_13;
            case BarcodeType.UpcA: return BarcodeFormat.UPC_A;
            case BarcodeType.UpcE: return BarcodeFormat.UPC_E;
            case BarcodeType.Codabar: return BarcodeFormat.CODABAR;
            case BarcodeType.I2OF5:
            case BarcodeType.I2OF5C:
            case BarcodeType.I2OF5G:
            case BarcodeType.Itf14: return BarcodeFormat.ITF;
            case BarcodeType.Msi: return BarcodeFormat.MSI;
            case BarcodeType.UccEan128: return BarcodeFormat.CODE_128;
            default: throw new NotSupportedException($"ESC/POS Raster 不支持主机条码类型 {type}。");
        }
    }

    /// <summary>将 QR Code 按指定模块尺寸绘制到画布，默认使用 M 级纠错。</summary>
    public void DrawQrCode(int x, int y, string data, int moduleSize)
        => DrawQrCode(x, y, data, moduleSize, QrCodeCorrectionLevel.M);

    /// <summary>将 QR Code 按指定模块尺寸和纠错级别绘制到画布。</summary>
    public void DrawQrCode(int x, int y, string data, int moduleSize, QrCodeCorrectionLevel errorLevel)
        => DrawQrCode(x, y, data, moduleSize, errorLevel, 0);

    /// <summary>将 QR Code 按顺时针角度绘制到画布。</summary>
    /// <param name="x">旋转后二维码外框左上角 X 坐标。</param>
    /// <param name="y">旋转后二维码外框左上角 Y 坐标。</param>
    /// <param name="data">非空的二维码内容。</param>
    /// <param name="moduleSize">二维码模块尺寸。</param>
    /// <param name="errorLevel">二维码纠错级别。</param>
    /// <param name="rotation">旋转角度，只接受 0、90、180 或 270。</param>
    public void DrawQrCode(int x, int y, string data, int moduleSize, QrCodeCorrectionLevel errorLevel,
        int rotation)
    {
        ValidateRotation(rotation);
        using var operation = PrintResourceScope.BeginTemporary();
        if (rotation == 0)
        {
            var encoded = PrepareQrCode(x, y, data, moduleSize, errorLevel);
            ExecuteDrawing(() => DrawQrCodeCore(x, y, encoded));
            return;
        }

        ValidateOrigin(x, y);
        ExecuteDrawing(() =>
        {
            var size = MeasureQrCodeSize(data, moduleSize, errorLevel, _cancellationToken);
            var source = new EscPosRasterCanvas(size, size, _fonts, _cancellationToken);
            source.DrawQrCode(0, 0, data, moduleSize, errorLevel);
            DrawRotatedCanvasCore(x, y, source, rotation);
        });
    }

    /// <summary>将 QR Code 绘制到画布，并为编码区登记显式静区。</summary>
    public void DrawQrCode(int x, int y, string data, int moduleSize, QrCodeCorrectionLevel errorLevel,
        PrintQuietZone quietZone)
    {
        ThrowIfCancellationRequested();
        if (quietZone == null) throw new ArgumentNullException(nameof(quietZone));
        using var operation = PrintResourceScope.BeginTemporary();
        var encoded = PrepareQrCode(x, y, data, moduleSize, errorLevel);
        var areas = PrepareQuietZone(x, y, encoded.SymbolWidth, encoded.SymbolHeight, quietZone, nameof(data));
        ExecuteDrawing(() =>
        {
            RegisterQuietZones(areas);
            DrawQrCodeCore(x, y, encoded);
        }, areas.Count > 0);
    }

    private (BitMatrix Matrix, int ModuleSize, int SymbolWidth, int SymbolHeight) PrepareQrCode(
        int x, int y, string data, int moduleSize, QrCodeCorrectionLevel errorLevel)
    {
        ThrowIfCancellationRequested();
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("二维码内容不能为空。", nameof(data));
        if (moduleSize <= 0) throw new ArgumentOutOfRangeException(nameof(moduleSize));
        ValidateOrigin(x, y);

        var matrix = EncodeQrCode(data, errorLevel, _cancellationToken);
        var symbolWidth = CheckedSymbolSize(matrix.Width, moduleSize, nameof(moduleSize));
        var symbolHeight = CheckedSymbolSize(matrix.Height, moduleSize, nameof(moduleSize));
        EnsureFits(x, y, symbolWidth, symbolHeight, nameof(data));
        return (matrix, moduleSize, symbolWidth, symbolHeight);
    }

    private void DrawQrCodeCore(int x, int y,
        (BitMatrix Matrix, int ModuleSize, int SymbolWidth, int SymbolHeight) encoded)
    {
        for (var row = 0; row < encoded.Matrix.Height; row++)
        {
            ThrowIfCancellationRequested();
            for (var column = 0; column < encoded.Matrix.Width; column++)
            {
                ThrowIfCancellationRequested();
                if (encoded.Matrix[column, row])
                {
                    var left = checked(x + column * encoded.ModuleSize);
                    var top = checked(y + row * encoded.ModuleSize);
                    for (var offsetY = 0; offsetY < encoded.ModuleSize; offsetY++)
                    {
                        ThrowIfCancellationRequested();
                        for (var offsetX = 0; offsetX < encoded.ModuleSize; offsetX++) SetPixel(left + offsetX, top + offsetY);
                    }
                }
            }
        }
    }

    internal static int MeasureQrCodeSize(string data, int moduleSize, QrCodeCorrectionLevel errorLevel)
        => MeasureQrCodeSize(data, moduleSize, errorLevel, CancellationToken.None);

    /// <summary>可取消地测量 ESC/POS Raster QR Code 的编码区尺寸。</summary>
    internal static int MeasureQrCodeSize(string data, int moduleSize, QrCodeCorrectionLevel errorLevel,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (moduleSize <= 0) throw new ArgumentOutOfRangeException(nameof(moduleSize));
        using var operation = PrintResourceScope.BeginTemporary();
        var matrix = EncodeQrCode(data, errorLevel, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return CheckedSymbolSize(matrix.Width, moduleSize, nameof(moduleSize));
    }

    private static BitMatrix EncodeQrCode(string data, QrCodeCorrectionLevel errorLevel,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(data)) throw new ArgumentException("二维码内容不能为空。", nameof(data));
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.ERROR_CORRECTION] = ToZxingErrorCorrection(errorLevel),
            [EncodeHintType.CHARACTER_SET] = "UTF-8",
            [EncodeHintType.MARGIN] = 0
        };
        PrintResourceScope.Retain(PrintMatrixBudget.QrBytes, "Matrix.Qr");
        BitMatrix matrix;
        try
        {
            // The zero margin is intentional: callers can reserve a quiet zone in
            // layout coordinates and the canvas preserves the requested origin.
            matrix = new MultiFormatWriter().encode(data, BarcodeFormat.QR_CODE, 1, 1, hints);
        }
        catch (WriterException error)
        {
            throw new ArgumentException("二维码内容无法编码。", nameof(data), error);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return matrix;
    }

    /// <summary>将系统字体文字绘制到当前单色画布。</summary>
    public void DrawText(int x, int y, string text, int fontSize, bool bold = false, bool underline = false,
        string fontFamily = null)
        => DrawText(x, y, text, fontSize, bold, underline, fontFamily, 0);

    /// <summary>将系统字体文字绘制到当前单色画布；反白时以文字布局区域为黑底。</summary>
    public void DrawText(int x, int y, string text, int fontSize, bool bold, bool reverse, bool underline,
        string fontFamily = null)
        => DrawText(x, y, text, fontSize, 0, bold, reverse, underline, fontFamily);

    /// <summary>将系统字体文字按顺时针角度绘制；该重载把角度放在字号之后，便于调用方显式指定角度。</summary>
    public void DrawText(int x, int y, string text, int fontSize, int rotation, bool bold = false,
        bool underline = false, string fontFamily = null)
        => DrawText(x, y, text, fontSize, bold, underline, fontFamily, rotation);

    /// <summary>将文字按顺时针角度绘制；反白背景、文字及下划线作为整体旋转。</summary>
    public void DrawText(int x, int y, string text, int fontSize, int rotation, bool bold, bool reverse,
        bool underline, string fontFamily = null)
        => ExecuteDrawing(() => DrawTextCore(x, y, text, fontSize, bold, reverse, underline, fontFamily, rotation));

    /// <summary>将系统字体文字按顺时针角度绘制到当前单色画布。</summary>
    /// <param name="x">旋转后文字墨迹外框的左上角 X 坐标。</param>
    /// <param name="y">旋转后文字墨迹外框的左上角 Y 坐标。</param>
    /// <param name="text">待绘制的文字。</param>
    /// <param name="fontSize">字体大小，单位为打印点。</param>
    /// <param name="bold">是否使用粗体。</param>
    /// <param name="underline">是否绘制下划线；下划线随文字一起旋转。</param>
    /// <param name="fontFamily">可选的系统字体名称或固定 Raster 字体别名。</param>
    /// <param name="rotation">顺时针旋转角度，只接受 0、90、180 或 270。</param>
    public void DrawText(int x, int y, string text, int fontSize, bool bold, bool underline,
        string fontFamily, int rotation)
        => ExecuteDrawing(() => DrawTextCore(x, y, text, fontSize, bold, false, underline, fontFamily, rotation));

    /// <summary>
    /// 将文字光栅化为紧致的单色位图，供不具备原生文字样式的标签协议显式降级使用。
    /// </summary>
    /// <param name="text">待绘制文字。</param>
    /// <param name="fontSize">字体大小，单位为打印点。</param>
    /// <param name="bold">是否使用粗体。</param>
    /// <param name="underline">是否绘制下划线。</param>
    /// <param name="fontFamily">可选主机字体名称。</param>
    /// <param name="rotation">顺时针旋转角度，只允许 0、90、180 或 270。</param>
    /// <param name="fonts">可选固定字体目录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已按旋转角度变换并裁切空白边缘的单色位图。</returns>
    internal static (int Width, int Height, int RowBytes, byte[] Data) RasterizeText(
        string text, int fontSize, bool bold, bool underline, string fontFamily, int rotation,
        RasterFontCatalog fonts = null, CancellationToken cancellationToken = default(CancellationToken))
        => RasterizeText(text, fontSize, bold, underline, fontFamily, rotation, false, fonts,
            cancellationToken);

    internal static (int Width, int Height, int RowBytes, byte[] Data) RasterizeText(
        string text, int fontSize, bool bold, bool underline, string fontFamily, int rotation, bool reverse,
        RasterFontCatalog fonts = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        ValidateRotation(rotation);
        if (text.Length == 0)
            return (1, 1, 1, new byte[] { 0 });

        var ink = MeasureTextInkBounds(text, fontSize, bold, underline, fontFamily, long.MaxValue,
            fonts, cancellationToken);
        if (!reverse && ink.IsEmpty)
            return (1, 1, 1, new byte[] { 0 });

        var layout = reverse
            ? MeasureReverseTextBounds(text, fontSize, bold, underline, fontFamily, long.MaxValue, fonts,
                cancellationToken)
            : default(EscPosRasterTextBounds);
        var left = reverse ? layout.Left : Math.Min(0, ink.Left);
        var top = reverse ? layout.Top : Math.Min(0, ink.Top);
        var sourceWidth = reverse ? layout.Width : checked(Math.Max(0, ink.Right) - left);
        var sourceHeight = reverse ? layout.Height : checked(Math.Max(0, ink.Bottom) - top);
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return (1, 1, 1, new byte[] { 0 });

        if (reverse)
        {
            var sourceRowBytes = checked((sourceWidth + 7) / 8);
            var sourceData = BudgetedBuffer.Allocate(checked(sourceRowBytes * sourceHeight), "Raster.ReverseText");
            for (var index = 0; index < sourceData.Length; index++) sourceData[index] = 0xff;
            var originX = checked(-left);
            var originY = checked(-top);
            var family = ResolveFontFamily(text, fontFamily, bold, fonts, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            using (var memory = PrintResourceScope.Reserve(checked((long)sourceWidth * sourceHeight * 4),
                       "Raster.ReverseTextRgba"))
            using (var image = new Image<Rgba32>(sourceWidth, sourceHeight))
            {
                image.Mutate(context =>
                {
                    context.Clear(Color.Black);
                    context.DrawText(text, family.CreateFont(fontSize, bold ? FontStyle.Bold : FontStyle.Regular),
                        Color.White, new PointF(originX, originY));
                });
                if (underline)
                {
                    var underlineWidth = GetUnderlineWidth(text, fontSize);
                    var underlineY = checked(originY + fontSize);
                    for (var underlineX = originX; underlineX < originX + underlineWidth; underlineX++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (underlineX >= 0 && underlineX < sourceWidth && underlineY >= 0 && underlineY < sourceHeight)
                            image[underlineX, underlineY] = new Rgba32(255, 255, 255, 255);
                    }
                }
                for (var y = 0; y < sourceHeight; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (var x = 0; x < sourceWidth; x++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (image[x, y].R >= 128)
                            sourceData[y * sourceRowBytes + x / 8] &= (byte)~(0x80 >> (x & 7));
                    }
                }
            }
            ClearRasterPadding(sourceData, sourceRowBytes, sourceWidth, sourceHeight);
            return RotateTextRaster(sourceWidth, sourceHeight, sourceRowBytes, sourceData, rotation,
                cancellationToken, opaqueBackground: true);
        }

        var source = new EscPosRasterCanvas(sourceWidth, sourceHeight, fonts, cancellationToken);
        source.DrawText(checked(-left), checked(-top), text, fontSize, bold, underline, fontFamily);
        var sourceBytes = source.ToBytes(cancellationToken);

        var minX = sourceWidth;
        var minY = sourceHeight;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < sourceHeight; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < sourceWidth; x++)
            {
                if ((sourceBytes[y * source.RowBytes + x / 8] & (0x80 >> (x & 7))) == 0) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }
        if (maxX < minX || maxY < minY)
            return (1, 1, 1, new byte[] { 0 });

        var croppedWidth = checked(maxX - minX + 1);
        var croppedHeight = checked(maxY - minY + 1);
        var croppedRowBytes = (croppedWidth + 7) / 8;
        var cropped = BudgetedBuffer.Allocate(checked(croppedRowBytes * croppedHeight), "Raster.StyledText");
        for (var y = 0; y < croppedHeight; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < croppedWidth; x++)
            {
                if ((sourceBytes[(y + minY) * source.RowBytes + (x + minX) / 8] &
                     (0x80 >> ((x + minX) & 7))) != 0)
                    cropped[y * croppedRowBytes + x / 8] |= (byte)(0x80 >> (x & 7));
            }
        }

        return RotateTextRaster(croppedWidth, croppedHeight, croppedRowBytes, cropped, rotation,
            cancellationToken);
    }

    private static (int Width, int Height, int RowBytes, byte[] Data) RotateTextRaster(int width, int height,
        int rowBytes, byte[] data, int rotation, CancellationToken cancellationToken,
        bool opaqueBackground = false)
    {
        if (rotation == 0) return (width, height, rowBytes, data);
        var outputWidth = rotation == 90 || rotation == 270 ? height : width;
        var outputHeight = rotation == 90 || rotation == 270 ? width : height;
        var outputRowBytes = (outputWidth + 7) / 8;
        var output = BudgetedBuffer.Allocate(checked(outputRowBytes * outputHeight), "Raster.StyledTextRotated");
        if (opaqueBackground)
            for (var index = 0; index < output.Length; index++) output[index] = 0xff;
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var black = (data[y * rowBytes + x / 8] & (0x80 >> (x & 7))) != 0;
                int targetX;
                int targetY;
                switch (rotation)
                {
                    case 90:
                        targetX = outputWidth - 1 - y;
                        targetY = x;
                        break;
                    case 180:
                        targetX = outputWidth - 1 - x;
                        targetY = outputHeight - 1 - y;
                        break;
                    default:
                        targetX = y;
                        targetY = outputHeight - 1 - x;
                        break;
                }
                var mask = (byte)(0x80 >> (targetX & 7));
                if (black) output[targetY * outputRowBytes + targetX / 8] |= mask;
                else if (opaqueBackground) output[targetY * outputRowBytes + targetX / 8] &= (byte)~mask;
            }
        }
        ClearRasterPadding(output, outputRowBytes, outputWidth, outputHeight);
        return (outputWidth, outputHeight, outputRowBytes, output);
    }

    private static void ClearRasterPadding(byte[] data, int rowBytes, int width, int height)
    {
        var usedBits = width & 7;
        if (usedBits == 0) return;
        var paddingMask = (byte)(0xff << (8 - usedBits));
        for (var y = 0; y < height; y++) data[y * rowBytes + rowBytes - 1] &= paddingMask;
    }

    private void DrawTextCore(int x, int y, string text, int fontSize, bool bold, bool reverse, bool underline,
        string fontFamily, int rotation)
    {
        ThrowIfCancellationRequested();
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        ValidateRotation(rotation);
        ValidateOrigin(x, y);

        if (reverse)
        {
            if (text.Length == 0) return;
            var raster = RasterizeText(text, fontSize, bold, underline, fontFamily, rotation, true, _fonts,
                _cancellationToken);
            DrawRasterCore(x, y, raster.Width, raster.Height, raster.Data);
            return;
        }

        if (rotation != 0)
        {
            DrawRotatedTextCore(x, y, text, fontSize, bold, underline, fontFamily, rotation);
            return;
        }

        var family = ResolveFontFamily(text, fontFamily, bold, _fonts, _cancellationToken);
        ThrowIfCancellationRequested();
        var ink = MeasureTextInkBounds(text, fontSize, bold, underline, fontFamily, long.MaxValue, _fonts,
            _cancellationToken);
        ThrowIfCancellationRequested();
        if (!ink.IsEmpty)
            EnsureFits(checked((long)x + ink.Left), checked((long)y + ink.Top), ink.Width, ink.Height,
                nameof(text));

        var font = family.CreateFont(fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
        ThrowIfCancellationRequested();
        using (var memory = PrintResourceScope.Reserve(checked((long)Width * Height * 4), "Raster.TextRgba"))
        using (var image = new Image<Rgba32>(Width, Height))
        {
            image.Mutate(context => context.DrawText(text, font, Color.Black, new PointF(x, y)));
            ThrowIfCancellationRequested();
            for (var py = 0; py < Height; py++)
            {
                ThrowIfCancellationRequested();
                for (var px = 0; px < Width; px++)
                {
                    ThrowIfCancellationRequested();
                    var pixel = image[px, py];
                    if (pixel.A != 0 && pixel.R < 250) SetPixel(px, py, true);
                }
            }
        }

        ThrowIfCancellationRequested();

        if (underline)
        {
            var width = Math.Max(1, (int)Math.Ceiling(text.Length * fontSize * 0.7));
            DrawLineCore(x, checked(y + fontSize), checked(x + width - 1), checked(y + fontSize), 1);
        }
    }

    private void DrawRotatedTextCore(int x, int y, string text, int fontSize, bool bold, bool underline,
        string fontFamily, int rotation)
    {
        var family = ResolveFontFamily(text, fontFamily, bold, _fonts, _cancellationToken);
        ThrowIfCancellationRequested();
        var ink = MeasureTextInkBounds(text, fontSize, bold, underline, fontFamily, long.MaxValue, _fonts,
            _cancellationToken);
        if (ink.IsEmpty) return;

        // Render into a tight temporary mask first. This keeps font bearings and
        // the optional underline identical to the unrotated path, then maps only
        // visible pixels into the caller's rotated bounding box.
        var padding = Math.Max(8, checked(fontSize * 2));
        var maskWidth = checked(ink.Width + padding * 2);
        var maskHeight = checked(ink.Height + padding * 2);
        var originX = checked(padding - ink.Left);
        var originY = checked(padding - ink.Top);
        var maskLeft = maskWidth;
        var maskTop = maskHeight;
        var maskRight = -1;
        var maskBottom = -1;

        var maskPixels = checked((long)maskWidth * maskHeight);
        using (var memory = PrintResourceScope.Reserve(checked(maskPixels * 4), "Raster.RotatedTextRgba"))
        using (var image = new Image<Rgba32>(maskWidth, maskHeight))
        {
            image.Mutate(context => context.DrawText(text,
                family.CreateFont(fontSize, bold ? FontStyle.Bold : FontStyle.Regular),
                Color.Black, new PointF(originX, originY)));
            if (underline)
            {
                var underlineWidth = GetUnderlineWidth(text, fontSize);
                var underlineY = checked(originY + fontSize);
                for (var underlineX = originX; underlineX < originX + underlineWidth; underlineX++)
                {
                    ThrowIfCancellationRequested();
                    if (underlineX >= 0 && underlineX < maskWidth && underlineY >= 0 && underlineY < maskHeight)
                        image[underlineX, underlineY] = new Rgba32(0, 0, 0, 255);
                }
            }

            for (var py = 0; py < maskHeight; py++)
            {
                ThrowIfCancellationRequested();
                for (var px = 0; px < maskWidth; px++)
                {
                    ThrowIfCancellationRequested();
                    var pixel = image[px, py];
                    if (pixel.A == 0 || pixel.R >= 250) continue;
                    if (px < maskLeft) maskLeft = px;
                    if (py < maskTop) maskTop = py;
                    if (px > maskRight) maskRight = px;
                    if (py > maskBottom) maskBottom = py;
                }
            }

            if (maskRight < maskLeft || maskBottom < maskTop) return;
            var sourceWidth = checked(maskRight - maskLeft + 1);
            var sourceHeight = checked(maskBottom - maskTop + 1);
            var outputWidth = rotation == 90 || rotation == 270 ? sourceHeight : sourceWidth;
            var outputHeight = rotation == 90 || rotation == 270 ? sourceWidth : sourceHeight;
            EnsureFits(x, y, outputWidth, outputHeight, nameof(text));

            for (var sourceY = maskTop; sourceY <= maskBottom; sourceY++)
            {
                ThrowIfCancellationRequested();
                for (var sourceX = maskLeft; sourceX <= maskRight; sourceX++)
                {
                    ThrowIfCancellationRequested();
                    var pixel = image[sourceX, sourceY];
                    if (pixel.A == 0 || pixel.R >= 250) continue;
                    var relativeX = sourceX - maskLeft;
                    var relativeY = sourceY - maskTop;
                    int targetX;
                    int targetY;
                    switch (rotation)
                    {
                        case 90:
                            targetX = outputWidth - 1 - relativeY;
                            targetY = relativeX;
                            break;
                        case 180:
                            targetX = outputWidth - 1 - relativeX;
                            targetY = outputHeight - 1 - relativeY;
                            break;
                        case 270:
                            targetX = relativeY;
                            targetY = outputHeight - 1 - relativeX;
                            break;
                        default:
                            targetX = relativeX;
                            targetY = relativeY;
                            break;
                    }
                    SetPixel(checked(x + targetX), checked(y + targetY));
                }
            }
        }
    }

    private static void ValidateRotation(int rotation)
    {
        if (rotation != 0 && rotation != 90 && rotation != 180 && rotation != 270)
            throw new ArgumentOutOfRangeException(nameof(rotation), "旋转角度必须为 0、90、180 或 270。");
    }

    /// <summary>解析适合给定文本的 ESC/POS Raster 系统字体名称。</summary>
    /// <param name="text">待输出的文本。</param>
    /// <returns>实际选用的系统字体名称。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text" /> 为 <c>null</c>。</exception>
    /// <exception cref="NotSupportedException">系统中没有可用于该文本的字体。</exception>
    public static string ResolveFontFamilyName(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        return ResolveFontFamily(text, null, false, null, CancellationToken.None).Name;
    }

    /// <summary>解析指定的系统字体族，并返回实际字体族名称。</summary>
    public static string ResolveFontFamilyName(string text, string fontFamily)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        return ResolveFontFamily(text, fontFamily, false, null, CancellationToken.None).Name;
    }

    /// <summary>按实际粗体样式校验字形覆盖，并返回选用的系统字体名称。</summary>
    public static string ResolveFontFamilyName(string text, string fontFamily, bool bold)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        return ResolveFontFamily(text, fontFamily, bold, null, CancellationToken.None).Name;
    }

    /// <summary>按固定配置解析整段文字字体；返回资源别名，供度量和绘制复用。</summary>
    public static string ResolveFontFamilyName(string text, string fontFamily, bool bold, RasterFontCatalog fonts)
        => ResolveFontFamilyName(text, fontFamily, bold, fonts, CancellationToken.None);

    /// <summary>使用固定字体配置解析整段文字字体，并支持取消。</summary>
    public static string ResolveFontFamilyName(string text, string fontFamily, bool bold,
        RasterFontCatalog fonts, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (fonts != null)
        {
            var selection = fonts.Resolve(text, fontFamily, bold, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return selection.Name;
        }
        var result = ResolveFontFamily(text, fontFamily, bold, fonts, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Name;
    }

    /// <summary>测量 ESC/POS Raster 文字的水平前进宽度，单位为打印点。</summary>
    /// <param name="text">待测量的文本。</param>
    /// <param name="fontSize">字体大小，单位为打印点。</param>
    /// <param name="bold">是否使用粗体。</param>
    /// <param name="fontFamily">可选的系统字体名称；为空时按文本内容选择字体。</param>
    /// <returns>足以容纳字体前进宽度的整数打印点数。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text" /> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fontSize" /> 不为正数。</exception>
    /// <exception cref="NotSupportedException">指定字体不存在，或系统中没有可用于该文本的字体。</exception>
    public static int MeasureTextWidth(string text, int fontSize, bool bold = false, string fontFamily = null)
        => MeasureTextWidth(text, fontSize, bold, fontFamily, null);

    /// <summary>使用固定字体配置测量文字前进宽度。</summary>
    public static int MeasureTextWidth(string text, int fontSize, bool bold, string fontFamily, RasterFontCatalog fonts)
        => MeasureTextWidth(text, fontSize, bold, fontFamily, fonts, CancellationToken.None);

    /// <summary>使用固定字体配置并支持取消地测量文字前进宽度。</summary>
    public static int MeasureTextWidth(string text, int fontSize, bool bold, string fontFamily,
        RasterFontCatalog fonts, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));

        var family = ResolveFontFamily(text, fontFamily, bold, fonts, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var font = family.CreateFont(fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
        cancellationToken.ThrowIfCancellationRequested();
        var bounds = TextMeasurer.Measure(text, new TextOptions(font));
        cancellationToken.ThrowIfCancellationRequested();
        return checked((int)Math.Ceiling(bounds.Width));
    }

    /// <summary>
    /// 测量文字经过 ESC/POS Raster 实际光栅化后的黑色像素范围。
    /// </summary>
    /// <param name="text">待测量的文本。</param>
    /// <param name="fontSize">字体大小，单位为打印点。</param>
    /// <param name="bold">是否使用粗体。</param>
    /// <param name="underline">是否把当前画布使用的下划线笔画并入范围。</param>
    /// <param name="fontFamily">可选的系统字体名称；为空时按文本内容选择字体。</param>
    /// <param name="maxPixels">临时测量位图允许使用的最大像素数。</param>
    /// <returns>相对于 <c>DrawText(x, y, ...)</c> 原点的实际像素范围。</returns>
    /// <remarks>
    /// 该方法使用与 <see cref="DrawText(int, int, string, int, bool, bool, string)"/> 相同的
    /// ImageSharp 绘制调用和黑色像素判定，因此返回的是可见 ink 范围，而不是字体的 advance 宽度。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="text" /> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fontSize" /> 不为正数。</exception>
    /// <exception cref="NotSupportedException">指定字体不存在，或系统中没有可用于该文本的字体。</exception>
    public static EscPosRasterTextBounds MeasureTextInkBounds(string text, int fontSize, bool bold = false,
        bool underline = false, string fontFamily = null, long maxPixels = long.MaxValue)
        => MeasureTextInkBounds(text, fontSize, bold, underline, fontFamily, maxPixels, null);

    /// <summary>测量反白文字的黑色布局外框，包括字体 advance、行高和越界墨迹。</summary>
    internal static EscPosRasterTextBounds MeasureReverseTextBounds(string text, int fontSize, bool bold,
        bool underline, string fontFamily, long maxPixels, RasterFontCatalog fonts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (maxPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maxPixels));
        if (text.Length == 0) return new EscPosRasterTextBounds(0, 0, 0, 0);

        var ink = MeasureTextInkBounds(text, fontSize, bold, underline, fontFamily, maxPixels, fonts,
            cancellationToken);
        var advance = MeasureTextWidth(text, fontSize, bold, fontFamily, fonts, cancellationToken);
        var left = ink.IsEmpty ? 0 : Math.Min(0, ink.Left);
        var top = ink.IsEmpty ? 0 : Math.Min(0, ink.Top);
        var right = Math.Max(advance, ink.IsEmpty ? 0 : ink.Right);
        var bottom = Math.Max(fontSize, ink.IsEmpty ? 0 : ink.Bottom);
        var width = checked(right - left);
        var height = checked(bottom - top);
        if ((long)width * height > maxPixels)
            throw new ArgumentOutOfRangeException(nameof(text), "反白文字位图超过像素预算。");
        return new EscPosRasterTextBounds(left, top, width, height);
    }

    /// <summary>使用与固定配置画布相同的字体测量实际墨迹范围。</summary>
    public static EscPosRasterTextBounds MeasureTextInkBounds(string text, int fontSize, bool bold,
        bool underline, string fontFamily, long maxPixels, RasterFontCatalog fonts)
        => MeasureTextInkBounds(text, fontSize, bold, underline, fontFamily, maxPixels, fonts,
            CancellationToken.None);

    /// <summary>使用固定字体配置并支持取消地测量实际墨迹范围。</summary>
    public static EscPosRasterTextBounds MeasureTextInkBounds(string text, int fontSize, bool bold,
        bool underline, string fontFamily, long maxPixels, RasterFontCatalog fonts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (maxPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maxPixels));

        var family = ResolveFontFamily(text, fontFamily, bold, fonts, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var font = family.CreateFont(fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
        cancellationToken.ThrowIfCancellationRequested();
        var textBounds = TextMeasurer.MeasureBounds(text, new TextOptions(font));
        cancellationToken.ThrowIfCancellationRequested();
        var padding = Math.Max(8, checked(fontSize * 2));
        var underlineWidth = underline ? GetUnderlineWidth(text, fontSize) : 0;

        // The extra padding keeps bearings that extend to the left or above the
        // requested origin inside the temporary image. The reported coordinates
        // are translated back to the original DrawText origin after scanning.
        var minimumX = Math.Min(0d, Math.Floor(textBounds.X));
        var minimumY = Math.Min(0d, Math.Floor(textBounds.Y));
        var maximumX = Math.Max(0d, Math.Max(textBounds.Right, underlineWidth));
        var maximumY = Math.Max(0d, Math.Max(textBounds.Bottom, underline ? fontSize + 1d : 0d));
        var originX = checked(padding + (int)-minimumX);
        var originY = checked(padding + (int)-minimumY);
        var imageWidth = checked(originX + Math.Max(1, (int)Math.Ceiling(maximumX)) + padding);
        var imageHeight = checked(originY + Math.Max(1, (int)Math.Ceiling(maximumY)) + padding);
        if ((long)imageWidth * imageHeight > maxPixels)
            throw new ArgumentOutOfRangeException(nameof(text), "文字测量位图超过像素预算。");

        var left = imageWidth;
        var top = imageHeight;
        var right = -1;
        var bottom = -1;
        using (var memory = PrintResourceScope.Reserve(checked((long)imageWidth * imageHeight * 4), "Raster.TextMeasurementRgba"))
        using (var image = new Image<Rgba32>(imageWidth, imageHeight))
        {
            image.Mutate(context => context.DrawText(text, font, Color.Black, new PointF(originX, originY)));
            cancellationToken.ThrowIfCancellationRequested();
            for (var py = 0; py < imageHeight; py++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var px = 0; px < imageWidth; px++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pixel = image[px, py];
                    if (pixel.A == 0 || pixel.R >= 250) continue;
                    if (px < left) left = px;
                    if (py < top) top = py;
                    if (px > right) right = px;
                    if (py > bottom) bottom = py;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (underline)
        {
            // DrawText uses a one-pixel horizontal stroke at y + fontSize.
            left = Math.Min(left, originX);
            top = Math.Min(top, originY + fontSize);
            right = Math.Max(right, originX + underlineWidth - 1);
            bottom = Math.Max(bottom, originY + fontSize);
        }

        if (right < left || bottom < top)
            return new EscPosRasterTextBounds(0, 0, 0, 0);

        var relativeLeft = checked(left - originX);
        var relativeTop = checked(top - originY);
        var width = checked(right - left + 1);
        var height = checked(bottom - top + 1);
        return new EscPosRasterTextBounds(relativeLeft, relativeTop, width, height);
    }

    private static int GetUnderlineWidth(string text, int fontSize)
    {
        return Math.Max(1, (int)Math.Ceiling(text.Length * fontSize * 0.7));
    }

    private static FontFamily ResolveFontFamily(string text, string fontFamily, bool bold = false,
        RasterFontCatalog fonts = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (fonts != null)
        {
            var result = fonts.Resolve(text, fontFamily, bold, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result.Family;
        }
        if (!string.IsNullOrEmpty(fontFamily))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SystemFonts.TryGet(fontFamily, out var family))
                throw new NotSupportedException("系统字体不存在：" + fontFamily);
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasGlyphCoverage(text, family, bold, out var missing, cancellationToken))
                throw new NotSupportedException($"系统字体 {fontFamily} 不包含 U+{missing:X4}；请显式选择覆盖该字符的字体。");
            cancellationToken.ThrowIfCancellationRequested();
            return family;
        }

        var fallback = FindFontFamily(text, bold, cancellationToken);
        if (fallback == default(FontFamily))
            throw new NotSupportedException("未找到完整覆盖文字字形的 ESC/POS Raster 系统字体；请显式提供可用字体。");
        return fallback;
    }

    private static FontFamily FindFontFamily(string text, bool bold, CancellationToken cancellationToken)
    {
        var containsNonAscii = false;
        foreach (var character in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (character > 127) { containsNonAscii = true; break; }
        }
        var preferred = containsNonAscii
            ? new[] { "Microsoft YaHei", "SimSun", "Noto Sans CJK SC", "Arial" }
            : new[] { "Arial", "DejaVu Sans", "Liberation Sans" };
        foreach (var name in preferred)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (SystemFonts.TryGet(name, out var family) &&
                HasGlyphCoverage(text, family, bold, out _, cancellationToken)) return family;
        }
        foreach (var family in SystemFonts.Families)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (HasGlyphCoverage(text, family, bold, out _, cancellationToken)) return family;
        }
        return default(FontFamily);
    }

    internal static bool HasGlyphCoverage(string text, FontFamily family, bool bold, out int missing)
        => HasGlyphCoverage(text, family, bold, out missing, CancellationToken.None);

    internal static bool HasGlyphCoverage(string text, FontFamily family, bool bold, out int missing,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var font = family.CreateFont(24, bold ? FontStyle.Bold : FontStyle.Regular);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var scalar in PrintUnicodeScalars.Enumerate(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 模板换行和制表属于布局控制，不要求字体提供可见字形。
            if (scalar == '\r' || scalar == '\n' || scalar == '\t') continue;
            var glyphs = font.GetGlyphs(new CodePoint(scalar), ColorFontSupport.None).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            if (glyphs.Length == 0 || glyphs.Any(glyph => glyph.GlyphMetrics.GlyphType == GlyphType.Fallback))
            {
                missing = scalar;
                return false;
            }
        }
        missing = 0;
        return true;
    }

    /// <summary>获取画布的独立位图副本。</summary>
    public byte[] ToBytes() => ToBytes(CancellationToken.None);

    /// <summary>使用调用方令牌获取画布的独立位图副本。</summary>
    public byte[] ToBytes(CancellationToken cancellationToken)
    {
        return BudgetedBuffer.Copy(_bytes, "Raster.CanvasSnapshot", cancellationToken);
    }

    private IReadOnlyList<QuietZoneArea> PrepareQuietZone(int x, int y, int width, int height,
        PrintQuietZone quietZone, string parameterName)
    {
        ThrowIfCancellationRequested();
        if (quietZone == null) throw new ArgumentNullException(nameof(quietZone));

        var left = (long)x - quietZone.Left;
        var top = (long)y - quietZone.Top;
        var right = (long)x + width + quietZone.Right;
        var bottom = (long)y + height + quietZone.Bottom;
        if (left < 0 || top < 0 || right > Width || bottom > Height)
            throw new ArgumentOutOfRangeException(parameterName, "码图静区超出 ESC/POS Raster 画布边界。");

        var areas = quietZone.Areas(x, y, width, height);
        ThrowIfCancellationRequested();
        return areas;
    }

    private void RegisterQuietZones(IReadOnlyList<QuietZoneArea> areas)
    {
        ThrowIfCancellationRequested();
        if (areas == null || areas.Count == 0) return;

        // Validate every area before adding any of them so a failed registration
        // cannot leave a partial protection record behind.
        foreach (var area in areas)
        {
            ThrowIfCancellationRequested();
            if (area.X < 0 || area.Y < 0 || area.Right > Width || area.Bottom > Height)
                throw new ArgumentOutOfRangeException(nameof(areas), "码图静区超出 ESC/POS Raster 画布边界。");
            EnsureAreaBlank(area);
        }

        for (var index = 0; index < areas.Count; index++)
        {
            ThrowIfCancellationRequested();
            _quietZones.Add(areas[index]);
        }
    }

    private void EnsureAreaBlank(QuietZoneArea area)
    {
        for (var y = area.Y; y < area.Bottom; y++)
        {
            ThrowIfCancellationRequested();
            for (var x = area.X; x < area.Right; x++)
            {
                ThrowIfCancellationRequested();
                if (IsBlack((int)x, (int)y))
                    throw new InvalidOperationException("码图静区不能覆盖已有黑色像素。");
            }
        }
    }

    private void EnsureBlackPixelAllowed(int x, int y)
    {
        foreach (var area in _quietZones)
        {
            ThrowIfCancellationRequested();
            if (area.Contains(x, y))
                throw new InvalidOperationException("绘制内容不能进入已登记的码图静区。");
        }
    }

    private bool IsBlack(int x, int y)
    {
        var index = checked(y * RowBytes + x / 8);
        return (_bytes[index] & (0x80 >> (x & 7))) != 0;
    }

    private void SetPixelCore(int x, int y, bool black)
    {
        var index = checked(y * RowBytes + x / 8);
        var mask = (byte)(0x80 >> (x & 7));
        if (black) _bytes[index] |= mask;
        else _bytes[index] &= (byte)~mask;
    }

    private void ThrowIfCancellationRequested()
    {
        if (_cancellationToken.CanBeCanceled)
            _cancellationToken.ThrowIfCancellationRequested();
    }

    private void ExecuteDrawing(Action action, bool requiresTransaction = false)
    {
        ThrowIfCancellationRequested();
        var useTransaction = _cancellationToken.CanBeCanceled || requiresTransaction ||
            _quietZones.Count > 0 || _transactionDepth > 0;
        if (!useTransaction)
        {
            action();
            return;
        }

        var ownsTransaction = _transactionDepth == 0;
        if (ownsTransaction)
        {
            _transactionReservation = PrintResourceScope.Reserve(_bytes.Length, "Raster.Rollback");
            try { _transactionBytes = (byte[])_bytes.Clone(); }
            catch { _transactionReservation.Dispose(); _transactionReservation = null; throw; }
            _transactionQuietZoneCount = _quietZones.Count;
        }

        _transactionDepth++;
        try
        {
            action();
            ThrowIfCancellationRequested();
        }
        catch
        {
            if (ownsTransaction)
            {
                Buffer.BlockCopy(_transactionBytes, 0, _bytes, 0, _bytes.Length);
                if (_quietZones.Count > _transactionQuietZoneCount)
                    _quietZones.RemoveRange(_transactionQuietZoneCount,
                        _quietZones.Count - _transactionQuietZoneCount);
            }
            throw;
        }
        finally
        {
            _transactionDepth--;
            if (ownsTransaction)
            {
                _transactionBytes = null;
                _transactionReservation.Dispose();
                _transactionReservation = null;
                _transactionQuietZoneCount = 0;
            }
        }
    }

    private void ValidateOrigin(int x, int y)
    {
        if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
    }

    private void EnsureFits(long x, long y, int width, int height, string parameterName)
    {
        if (width <= 0 || height <= 0 || x > Width - width || y > Height - height)
            throw new ArgumentOutOfRangeException(parameterName, "绘制内容超出 ESC/POS Raster 画布边界。");
    }

    private void EnsureStrokeFits(long x0, long y0, long x1, long y1, int thickness, string parameterName)
    {
        var radius = (long)thickness / 2;
        var left = Math.Min((long)x0, x1) - radius;
        var top = Math.Min((long)y0, y1) - radius;
        var right = Math.Max((long)x0, x1) + radius;
        var bottom = Math.Max((long)y0, y1) + radius;
        if (left < 0 || top < 0 || right >= Width || bottom >= Height)
            throw new ArgumentOutOfRangeException(parameterName, "线条画笔超出 ESC/POS Raster 画布边界。");
    }

    private static int CheckedSymbolSize(int modules, int moduleSize, string parameterName)
    {
        var size = (long)modules * moduleSize;
        if (size <= 0 || size > int.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, "编码图形尺寸超出可表示范围。");
        return (int)size;
    }

    private static ErrorCorrectionLevel ToZxingErrorCorrection(QrCodeCorrectionLevel level)
    {
        switch (level)
        {
            case QrCodeCorrectionLevel.L: return ErrorCorrectionLevel.L;
            case QrCodeCorrectionLevel.M: return ErrorCorrectionLevel.M;
            case QrCodeCorrectionLevel.Q: return ErrorCorrectionLevel.Q;
            case QrCodeCorrectionLevel.H: return ErrorCorrectionLevel.H;
            default: throw new ArgumentOutOfRangeException(nameof(level), level, "二维码纠错级别无效。");
        }
    }

    private void DrawBrush(int x, int y, int thickness)
    {
        var radius = thickness / 2;
        // Clip the brush in long coordinates before iterating. This preserves the
        // existing inclusive radius semantics while keeping a very large brush
        // proportional to the visible canvas instead of its requested diameter.
        var left = Math.Max(0L, (long)x - radius);
        var right = Math.Min((long)Width - 1, (long)x + radius);
        var top = Math.Max(0L, (long)y - radius);
        var bottom = Math.Min((long)Height - 1, (long)y + radius);
        if (left > right || top > bottom) return;

        for (var targetY = top; targetY <= bottom; targetY++)
        {
            ThrowIfCancellationRequested();
            for (var targetX = left; targetX <= right; targetX++)
            {
                ThrowIfCancellationRequested();
                SetPixel((int)targetX, (int)targetY);
            }
        }
    }
}
