using System;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using ZXing.Common;

namespace Bing.EasyPrint.Protocols;

public sealed partial class EscPosRasterCanvas
{
    // Layout and separator algorithm adapted from Zint 2.15.0 rss.c (BSD-3-Clause).
    // The full copyright and license are in ThirdPartyNotices.txt.
    /// <summary>编码固定两列的 Expanded Stacked 逻辑行；每四行中的首行为数据，其余为分隔行。</summary>
    /// <param name="data">普通入口接受与 Expanded 相同的输入；Composite 调用方传入已校验的规范化 AI。</param>
    /// <param name="cancellationToken">在字符、行和模块循环中观察的取消令牌。</param>
    /// <param name="composite">是否设置线性组件连接标志。</param>
    /// <returns>等宽的独立模块串，不含外围静区或 Composite 外部分隔行。</returns>
    internal static string[] EncodeGs1DataBarExpandedStackedPattern(string data,
        CancellationToken cancellationToken = default, bool composite = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = composite ? data : NormalizeBarcodeData(BarcodeType.Gs1DataBarExpandedStacked, data);
        return BuildExpandedStackedRows(normalized, cancellationToken, composite);
    }

    private static string[] BuildExpandedStackedRows(string normalized, CancellationToken cancellationToken, bool composite)
    {
        var elements = EncodeExpandedElements(normalized, cancellationToken, composite, true);
        var blocks = (elements.Length - 4 + 20) / 21;
        var characters = (elements.Length - 4 - blocks * 5) / 8;
        var rowCount = (blocks + 1) / 2;
        var rows = new string[rowCount * 4 - 3];
        var block = 0;
        var versionTwo = false;
        for (var row = 0; row < rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var columns = Math.Min(2, blocks - block);
            var special = row == rowCount - 1 && row % 2 == 1 && columns == 1;
            var forward = row % 2 == 0 || special;
            var sub = new int[columns * 21 + 4];
            sub[0] = special ? 2 : 1;
            sub[1] = sub[sub.Length - 2] = sub[sub.Length - 1] = 1;
            for (var column = 0; column < columns; column++, block++)
            {
                for (var element = 0; element < 21; element++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = 2 + block * 21 + element;
                    if (source >= elements.Length) continue;
                    var target = forward ? 2 + column * 21 + element
                        : 2 + (columns - column - 1) * 21 + 20 - element;
                    sub[target] = elements[source];
                }
            }
            var pattern = new StringBuilder(102);
            var black = !forward;
            foreach (var width in sub)
            {
                pattern.Append(black ? '1' : '0', width);
                black = !black; // Zero-width tail elements still toggle the alternating run state.
            }
            var linear = pattern.ToString();
            rows[row * 4] = linear;
            if (row > 0)
            {
                var middle = new char[102];
                for (var x = 0; x < middle.Length; x++) middle[x] = x >= 5 && x < 98 && x % 2 == 1 ? '1' : '0';
                rows[row * 4 - 2] = new string(middle);
                rows[row * 4 - 1] = ExpandedStackedSeparator(linear, columns, special, forward,
                    row == rowCount - 1 && characters % 2 == 1, versionTwo, cancellationToken);
            }
            if (row < rowCount - 1)
            {
                rows[row * 4 + 1] = ExpandedStackedSeparator(linear, columns, false, forward,
                    false, versionTwo, cancellationToken);
                if (columns % 2 != 0) versionTwo = !versionTwo;
            }
        }
        var widthModules = rows.Max(value => value.Length);
        for (var row = 0; row < rows.Length; row++) rows[row] = rows[row].PadRight(widthModules, '0');
        cancellationToken.ThrowIfCancellationRequested();
        return rows;
    }

    private static string ExpandedStackedSeparator(string linear, int columns, bool special,
        bool forward, bool oddLast, bool versionTwo, CancellationToken cancellationToken)
    {
        var separator = new char[linear.Length];
        var offset = special ? 1 : 0;
        for (var x = 0; x < linear.Length; x++)
            separator[x] = x >= 4 + offset && x < linear.Length - 4 && linear[x] == '0' ? '1' : '0';
        var space = false;
        for (var column = 0; column < columns; column++)
        {
            var start = 49 * column + 19 + offset - (!forward && oddLast ? 17 : 0);
            var first = forward ? versionTwo ? 2 : 0 : versionTwo ? 14 : 12;
            var last = forward ? versionTwo ? 14 : 12 : versionTwo ? 2 : 0;
            var step = forward ? 1 : -1;
            for (var index = first; forward ? index <= last : index >= last; index += step)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var x = start + index;
                if (x < 0 || x >= linear.Length) continue;
                separator[x] = linear[x] == '0' && !space ? '1' : '0';
                space = linear[x] == '0' && !space;
            }
            versionTwo = !versionTwo;
        }
        return new string(separator);
    }

    private static (BitMatrix Matrix, int First, int Last) EncodeGs1DataBarExpandedStacked(
        string normalizedData, CancellationToken cancellationToken)
    {
        // Includes the bounded width arrays, strings, checksum workspace and binary padding.
        using var workspace = PrintResourceScope.Reserve(16384, "ExpandedStacked.EncodingWorkspace");
        // Already normalized by EncodeBarcode; composite=true here would change the linkage bit.
        var elements = BuildExpandedStackedRows(normalizedData, cancellationToken, false);
        PrintResourceScope.Retain(checked((elements[0].Length + 31L) / 32 * elements.Length * sizeof(int)),
            "Matrix.Gs1DataBarExpandedStacked");
        var matrix = new BitMatrix(elements[0].Length, elements.Length);
        for (var row = 0; row < elements.Length; row++)
            for (var column = 0; column < elements[row].Length; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (elements[row][column] == '1') matrix[column, row] = true;
            }
        return (matrix, 0, matrix.Width - 1);
    }

    private static int ExpandedStackedHeight(int logicalRows, int rowHeight, int moduleWidth)
    {
        if (rowHeight < moduleWidth)
            throw new ArgumentOutOfRangeException(nameof(rowHeight), "ExpandedStacked 每条数据行高度至少为一个模块宽度。");
        var dataRows = (logicalRows + 3) / 4;
        return checked(dataRows * rowHeight + (dataRows - 1) * 3 * moduleWidth);
    }

    private void DrawSeparatedBarcodeRowsCore(int x, int y, int rowHeight,
        (BitMatrix Matrix, int First, int Last, int ModuleWidth, int SymbolWidth, int SymbolHeight) encoded)
    {
        var top = y;
        for (var row = 0; row < encoded.Matrix.Height; row++)
        {
            var height = row % 4 == 0 ? rowHeight : encoded.ModuleWidth;
            for (var column = 0; column < encoded.Matrix.Width; column++)
            {
                ThrowIfCancellationRequested();
                if (!encoded.Matrix[column, row]) continue;
                for (var offset = 0; offset < encoded.ModuleWidth; offset++)
                    for (var pixelY = top; pixelY < top + height; pixelY++)
                        SetPixel(x + column * encoded.ModuleWidth + offset, pixelY);
            }
            top += height;
        }
    }
}
