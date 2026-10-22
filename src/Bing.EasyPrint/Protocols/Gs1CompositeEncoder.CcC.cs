using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

// CC-C capacity, error correction and row placement follow Zint 2.15.0
// backend/composite.c. Copyright and BSD-3-Clause terms are retained in
// Gs1CompositeEncoder.cs and Gs1CompositeTables.cs.
internal static partial class Gs1CompositeEncoder
{
    /// <summary>
    /// 保存一次 CC-C 编码的列数及纠错级别，按线性组件宽度和次数据容量计算。
    /// </summary>
    private sealed class CcCLayout
    {
        private readonly int linearWidth;
        internal int Columns { get; private set; }
        internal int EccLevel { get; private set; }

        internal CcCLayout(int linearWidth) => this.linearWidth = linearWidth;

        /// <summary>
        /// 计算可容纳当前二进制串的填充长度；超过 30×30 个码字时返回零。
        /// </summary>
        internal int TargetBits(int length)
        {
            var bytes = (length + 7) / 8;
            var words = bytes / 6 * 5 + bytes % 6;
            if (words > 865) return 0;
            EccLevel = words <= 40 ? 2 : words <= 160 ? 3 : words <= 320 ? 4 : words <= 833 ? 5 : 4;
            var correction = 1 << (EccLevel + 1);
            words += correction + 3; // Length descriptor, CC identifier and byte latch.
            Columns = Math.Min(30, Math.Max(1, (linearWidth - 52) / 17));
            var rows = (words + Columns - 1) / Columns;
            while (rows > 30 && Columns < 30)
            {
                Columns++;
                rows = (words + Columns - 1) / Columns;
            }
            if (rows > 30) return 0;
            var targetWords = Columns * Math.Max(3, rows) - correction - 3;
            return 8 * (targetWords / 5 * 6 + targetWords % 5);
        }
    }

    /// <summary>
    /// 将已填充的 CC-C 数据压缩为 PDF417 码字，附加纠错、行标识和起止图案。
    /// </summary>
    private static string[] EncodeCcC(string bits, CcCLayout layout, CancellationToken cancellationToken)
    {
        var words = new List<int>(900) { 0, 920 };
        var byteCount = bits.Length / 8;
        words.Add(byteCount % 6 == 0 ? 924 : 901);
        for (var index = 0; index < byteCount;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (byteCount - index >= 6)
            {
                var value = 0L;
                for (var count = 0; count < 6; count++) value = (value << 8) | (uint)ReadBits(bits, index++ * 8, 8);
                var block = new int[5];
                for (var count = 4; count >= 0; count--) { block[count] = (int)(value % 900); value /= 900; }
                words.AddRange(block);
            }
            else words.Add(ReadBits(bits, index++ * 8, 8));
        }
        words[0] = words.Count;
        var correctionCount = 1 << (layout.EccLevel + 1);
        var correction = new int[correctionCount];
        foreach (var word in words)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var total = (word + correction[correctionCount - 1]) % 929;
            for (var index = correctionCount - 1; index >= 0; index--)
                correction[index] = ((index == 0 ? 0 : correction[index - 1]) + 929 -
                    total * Gs1CompositeTables.PdfCoefficients[correctionCount - 2 + index] % 929) % 929;
        }
        for (var index = correctionCount - 1; index >= 0; index--)
            words.Add(correction[index] == 0 ? 0 : 929 - correction[index]);
        if (words.Count % layout.Columns != 0)
            throw new InvalidOperationException("CC-C 填充未对齐码字行。");
        var rows = words.Count / layout.Columns;
        var indicators = new[] { (rows - 1) / 3, layout.EccLevel * 3 + (rows - 1) % 3, layout.Columns - 1 };
        var result = new string[rows];
        for (var row = 0; row < rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cluster = row % 3;
            var prefix = row / 3 * 30;
            var pattern = new StringBuilder(layout.Columns * 17 + 69);
            Append(pattern, 0x1FEA8, 17);
            AppendWord(prefix + indicators[cluster]);
            for (var column = 0; column < layout.Columns; column++) AppendWord(words[row * layout.Columns + column]);
            AppendWord(prefix + indicators[(cluster + 2) % 3]);
            Append(pattern, 0x3FA29, 18);
            result[row] = pattern.ToString();

            void AppendWord(int word)
            {
                Append(pattern, Gs1CompositeTables.BitPatterns[929 * cluster + word], 16);
                pattern.Append('0');
            }
        }
        return result;
    }

    /// <summary>
    /// 对规范化的 GS1 主数据选择 B/C 码集，追加对应二维模式的连接标志并重新计算校验码。
    /// </summary>
    /// <remarks>共享 AI 解析器已排除控制字符；ASCII 29 是字段分隔 FNC1，不是普通 Code A 字符。</remarks>
    private static string EncodeGs1Linear(string data, Gs1CompositeMode mode, CancellationToken cancellationToken)
    {
        var length = data.Length;
        var costB = new int[length + 1];
        var costC = new int[length + 1];
        var switchB = new bool[length];
        var switchC = new bool[length];
        for (var index = length - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pair = HasPair(index);
            costB[index] = 1 + costB[index + 1];
            if (pair && 2 + costC[index + 2] < costB[index])
            {
                costB[index] = 2 + costC[index + 2];
                switchB[index] = true;
            }
            var stayC = data[index] == Fnc1 ? 1 + costC[index + 1]
                : pair ? 1 + costC[index + 2] : int.MaxValue;
            var changeC = 2 + costB[index + 1];
            // FNC1 can stay in either code set without a latch.
            costC[index] = Math.Min(stayC, changeC);
            switchC[index] = changeC < stayC;
        }
        var inC = costC[0] <= costB[0];
        var words = new List<int>(length + 5) { inC ? 105 : 104, 102 };
        for (var index = 0; index < length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (inC ? switchC[index] : switchB[index])
            {
                words.Add(inC ? 100 : 99);
                inC = !inC;
            }
            if (data[index] == Fnc1) { words.Add(102); index++; }
            else if (inC) { words.Add((data[index] - '0') * 10 + data[index + 1] - '0'); index += 2; }
            else words.Add(data[index++] - 32);
        }
        // ISO/IEC 24723 linkage flag depends on both the final code set and component mode.
        words.Add(mode == Gs1CompositeMode.CcC ? (inC ? 100 : 101) : (inC ? 101 : 99));
        var checksum = words[0];
        for (var index = 1; index < words.Count; index++) checksum += words[index] * index;
        words.Add(checksum % 103);
        var pattern = new StringBuilder(words.Count * 11 + 13);
        foreach (var word in words) Append(pattern, Gs1CompositeTables.Code128Patterns[word], 11);
        Append(pattern, 0x18EB, 13); // 2331112 stop pattern.
        return pattern.ToString();

        bool HasPair(int index) => index + 1 < length && data[index] >= '0' && data[index] <= '9' &&
            data[index + 1] >= '0' && data[index + 1] <= '9';
    }
}
