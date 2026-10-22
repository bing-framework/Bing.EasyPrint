using System;
using System.Threading;
using Bing.EasyPrint.Printing;
using ZXing.Common;

namespace Bing.EasyPrint.Protocols;

public sealed partial class EscPosRasterCanvas
{
    // Zint 2.15.0 rss.c layout/separators; copyright/license in ThirdPartyNotices.txt.
    /// <summary>将 96 模块 DataBar 串折为两条数据行及三个一模块分隔行，保留连接标志。</summary>
    internal static string[] BuildGs1DataBarStackedOmnidirectionalRows(string linear,
        CancellationToken cancellationToken)
    {
        var folded = BuildGs1DataBarStackedRows(linear, cancellationToken);
        var top = folded[0];
        var bottom = folded[2];
        var middle = new char[50];
        for (var index = 0; index < middle.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            middle[index] = index >= 5 && index < 46 && index % 2 == 1 ? '1' : '0';
        }
        // Reversed finder value 3 has run widths 1,1,9,1,3, starting with a bar.
        // ISO/IEC 24724 5.3.2.2 moves its sole separator bar one module right.
        var finderThree = string.CompareOrdinal(bottom, 17, "101111111110111", 0, 15) == 0;
        return new[] { top, StackedOmnidirectionalSeparator(top, 18, false, cancellationToken),
            new string(middle), StackedOmnidirectionalSeparator(bottom, 19, finderThree, cancellationToken), bottom };
    }

    private static string StackedOmnidirectionalSeparator(string linear, int finderStart,
        bool finderThree, CancellationToken cancellationToken)
    {
        var separator = new char[50];
        for (var index = 0; index < separator.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            separator[index] = index >= 4 && index < 46 && linear[index] == '0' ? '1' : '0';
        }
        var latch = true;
        for (var index = finderStart; index < finderStart + 13; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            separator[index] = finderThree ? (index == finderStart + 10 ? '1' : '0')
                : linear[index] == '0' && latch ? '1' : '0';
            latch = linear[index] == '1' || !latch;
        }
        return new string(separator);
    }

    /// <summary>普通条码接受十三位 ASCII 数字 payload；返回两数据行与三个分隔行。</summary>
    internal static string[] EncodeGs1DataBarStackedOmnidirectionalPattern(string data,
        CancellationToken cancellationToken = default)
        => BuildGs1DataBarStackedOmnidirectionalRows(EncodeGs1DataBarPattern(
            NormalizeBarcodeData(BarcodeType.Gs1DataBarStackedOmnidirectional, data), cancellationToken), cancellationToken);

    private static (BitMatrix Matrix, int First, int Last) EncodeGs1DataBarStackedOmnidirectional(
        string normalizedData, CancellationToken cancellationToken)
    {
        using var workspace = PrintResourceScope.Reserve(4096, "StackedOmnidirectional.EncodingWorkspace");
        var rows = EncodeGs1DataBarStackedOmnidirectionalPattern(normalizedData, cancellationToken);
        PrintResourceScope.Retain(40, "Matrix.Gs1DataBarStackedOmnidirectional");
        var matrix = new BitMatrix(50, 5);
        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rows[row][column] == '1') matrix[column, row] = true;
            }
        return (matrix, 0, 49);
    }

    private static int StackedOmnidirectionalHeight(int rowHeight, int moduleWidth)
    {
        if (rowHeight < 33L * moduleWidth)
            throw new ArgumentOutOfRangeException(nameof(rowHeight), "Stacked Omnidirectional 每条数据行高度至少为 33X。");
        return checked(2 * rowHeight + 3 * moduleWidth);
    }
}
