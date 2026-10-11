using System;

namespace Bing.EasyPrint.Printing;

/// <summary>按当前 ZXing.Net 0.16.11 默认编码参数预估符号矩阵的数据区。</summary>
/// <remarks>上界包含下述显式矩阵存储，不包含对象头、集合或编码器的全部内部工作缓冲。</remarks>
internal static class PrintMatrixBudget
{
    // QR Model 2 最大版本为 40，边长 177；同时保留 ByteMatrix 和输出 BitMatrix。
    internal static long QrBytes => checked(177L * 177 + PackedBytes(177, 177));

    internal static long Code128Bytes(int characterCount)
    {
        if (characterCount < 1) throw new ArgumentOutOfRangeException(nameof(characterCount));
        // Compact 编码每字符最多一个切换/起始 pattern 和一个数据 pattern；校验/停止占 24 模块。
        // GS1_FORMAT 的 FNC1 由调用方按一个额外字符计入；输出请求 1×1、MARGIN=0。
        // bool[] pattern 与 BitMatrix 同时存在。
        var width = checked(22L * characterCount + 24);
        return checked(width + PackedBytes(width, 1));
    }

    internal static long PlesseyBytes(int characterCount)
    {
        if (characterCount < 1) throw new ArgumentOutOfRangeException(nameof(characterCount));
        // ZXing.Net 0.16.11 的 PlesseyWriter 以 100 个布尔像素表示每个输入字符，
        // 并保留两侧各 100 个像素及固定起止/CRC 区；Raster 只保留黑像素包围盒。
        var encodedWidth = checked(100L * characterCount + 625);
        var symbolWidth = checked(encodedWidth - 200);
        return checked(encodedWidth + PackedBytes(symbolWidth, 1));
    }

    internal static long Code11Bytes(int dataLength)
    {
        if (dataLength < 1) throw new ArgumentOutOfRangeException(nameof(dataLength));
        var checkCount = dataLength > 10 ? 2 : 1;
        var symbolCount = checked(dataLength + checkCount + 2);
        // Code 11's widest symbol has seven modules; each adjacent symbol adds
        // one narrow white separator, and the final stop symbol has no trailing gap.
        var width = checked(8L * symbolCount - 1);
        return checked(width + PackedBytes(width, 1));
    }

    internal static long TelepenBytes(int dataLength)
    {
        if (dataLength < 1 || dataLength > 30) throw new ArgumentOutOfRangeException(nameof(dataLength));
        // Telepen Alpha emits start, payload, Mod-127 check and stop symbols;
        // every symbol occupies exactly 16 expanded modules.
        var width = checked(16L * (dataLength + 3));
        return checked(width + PackedBytes(width, 1));
    }

    internal static long TelepennBytes(int dataLength)
    {
        if (dataLength < 1 || dataLength > 60) throw new ArgumentOutOfRangeException(nameof(dataLength));
        // Telepen Numeric packs two digits into one symbol and prepends a zero
        // for odd payload lengths, followed by one check and one stop symbol.
        var payloadSymbols = checked((dataLength + 1) / 2);
        var width = checked(16L * (payloadSymbols + 3));
        return checked(width + PackedBytes(width, 1));
    }

    internal static long ChinaPostBytes(int dataLength)
    {
        if (dataLength < 1) throw new ArgumentOutOfRangeException(nameof(dataLength));
        // Raster China Post uses a 3:1 wide:narrow ratio. Each data digit is
        // nine element modules plus one inter-character gap; the start/stop
        // and their adjoining gaps occupy the fixed nine-module prefix/suffix.
        var width = checked(10L * dataLength + 9);
        return checked(width + PackedBytes(width, 1));
    }

    internal static long Gs1DataBarBytes()
    {
        // GS1 DataBar Omnidirectional/Truncated has a fixed 96-module linear
        // symbol including its two one-module boundary guard elements.
        const long width = 96;
        return checked(width + PackedBytes(width, 1));
    }

    internal static long Gs1DataBarStackedBytes()
    {
        // GS1 DataBar Stacked contains 50 modules across 13 rows (5 + separator + 7).
        const long width = 50;
        const long height = 13;
        return checked(width * height + PackedBytes(width, height));
    }

    internal static long Gs1DataBarLimitedBytes()
    {
        // GS1 DataBar Limited has a fixed 79-module linear symbol including
        // the one-module guards and the five-module terminal guard.
        const long width = 79;
        return checked(width + PackedBytes(width, 1));
    }

    internal static long Gs1DataBarExpandedBytes()
    {
        return Gs1DataBarExpandedBytes(5);
    }

    internal static long Gs1DataBarExpandedBytes(int symbolCharacters)
    {
        if (symbolCharacters < 4 || symbolCharacters > 22)
            throw new ArgumentOutOfRangeException(nameof(symbolCharacters));

        // Each data/check character occupies 17 modules. A finder pattern
        // occupies 15 modules and is emitted once per two symbol characters;
        // the two guards on each side add four modules.
        var codeBlocks = checked((symbolCharacters + 1) / 2);
        var width = checked(17L * symbolCharacters + 15L * codeBlocks + 4);
        return checked(width + PackedBytes(width, 1));
    }

    internal static long Code49Bytes(int dataLength)
    {
        if (dataLength < 1 || dataLength > 81)
            throw new ArgumentOutOfRangeException(nameof(dataLength));

        // Code 49's ASCII chart expands a source character to at most two
        // codewords. Reserve the complete eight-row symbol so the budget also
        // covers the check-character grid and the rendered BitMatrix.
        var codewords = checked((long)dataLength * 2);
        var rows = Math.Max(1L, (codewords + 6) / 7);
        var padding = checked(rows * 7 - codewords);
        if (rows == 1 || rows > 6 || padding < 5) rows++;
        rows = Math.Max(2L, Math.Min(8L, rows));
        const long width = 70;
        var height = checked(rows);
        return checked(width * height + PackedBytes(width, height));
    }

    internal static long MatrixCodeBytes(int requestedWidth, int requestedHeight, bool pdf417)
    {
        if (requestedWidth < 1) throw new ArgumentOutOfRangeException(nameof(requestedWidth));
        if (requestedHeight < 1) throw new ArgumentOutOfRangeException(nameof(requestedHeight));
        if (!pdf417)
        {
            // 默认 DataMatrix 最大符号为 144×144，当前 MARGIN=2，未配置扩展符号或尺寸 hint。
            var width = Math.Max((long)requestedWidth, 148);
            var height = Math.Max((long)requestedHeight, 148);
            return checked(144L * 144 + PackedBytes(width, height));
        }

        // 默认 PDF417：2–30 列、3–90 行、非 compact、纵向倍率 4。
        // 固有行宽为 cols*17+69，最多 579；固有高度最多 360，旋转后各轴最多 579。
        // 整数放大后的内区不会超过 max(请求轴,579)，随后四边各加 MARGIN=2。
        var outputWidth = checked(Math.Max((long)requestedWidth, 579) + 4);
        var outputHeight = checked(Math.Max((long)requestedHeight, 579) + 4);
        // 编码行矩阵、放大矩阵及旋转时新旧 byte[][] 的数据区按保守上界同时计入。
        return checked(579L * 90 + 2 * outputWidth * outputHeight + PackedBytes(outputWidth, outputHeight));
    }

    private static long PackedBytes(long width, long height)
    {
        if (width > int.MaxValue || height > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "码图尺寸超过矩阵容量。");
        var words = checked(((width + 31) / 32) * height);
        if (words > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "码图尺寸超过矩阵容量。");
        return checked(words * sizeof(int));
    }
}
