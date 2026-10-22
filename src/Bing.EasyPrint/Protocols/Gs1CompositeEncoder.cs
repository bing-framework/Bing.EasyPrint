using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;
using ZXing.Common;

namespace Bing.EasyPrint.Protocols;

/*
 * Managed port of Zint 2.15.0 backend/composite.c (CC-A/CC-B/CC-C).
 * Commit: 4896136bdbebb1a0908a5cc419513efa5bf4bf70.
 * Copyright (C) 2008-2025 Robin Stuart <rstuart114@gmail.com>
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 * 1. Redistributions of source code must retain the above copyright notice,
 *    this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions and the following disclaimer in the documentation
 *    and/or other materials provided with the distribution.
 * 3. Neither the name of the project nor the names of its contributors may be used
 *    to endorse or promote products derived from this software without specific
 *    prior written permission.
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
 * INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
 * OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * The upstream functions getBit, init928 and encode928 are copyright BSI:
 * "Copyright subsists in all BSI publications. BSI also holds the copyright, in the
 * UK, of the international standardisation bodies. Except as permitted under the
 * Copyright, Designs and Patents Act 1988 no extract may be reproduced, stored in a
 * retrieval system or transmitted in any form or by any means - electronic,
 * photocopying, recording or otherwise - without prior written permission from BSI.
 * This does not preclude the free use, in the course of implementing the standard,
 * of necessary details such as symbols, and size, type or grade designations. If
 * these details are to be used for any other purpose than implementation then the
 * prior written permission of BSI must be obtained." Published 31 May 2006.
 * Base-928 conversion below uses iterative multiplication instead of these functions.
 * The date-only FNC1 detail follows Zint/BWIPP, Copyright (c) 2004-2019 Terry Burton.
 */
internal static partial class Gs1CompositeEncoder
{
    private const char Fnc1 = '\u001D';
    private static readonly int[] AaThreeSizes = { 78, 98, 118, 138, 167 };
    private static readonly int[] AaFourSizes = { 78, 108, 138, 167, 197 };
    private static readonly int[] BbThreeSizes = { 32, 72, 112, 152, 208, 304, 416, 536, 648, 768 };
    private static readonly int[] BbFourSizes = { 56, 96, 152, 208, 264, 352, 496, 672, 840, 1016, 1184 };

    /// <summary>生成真实模块矩阵；二维行、分隔行、线性行分别占用一行。</summary>
    internal static Gs1CompositeSymbol Encode(PrintGs1CompositeElement element,
        CancellationToken cancellationToken = default)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = PrintResourceScope.BeginTemporary();
        var gs1 = element.LinearType == BarcodeType.UccEan128;
        var expanded = element.LinearType == BarcodeType.Gs1DataBarExpanded;
        if (element.PrimaryData.Length > (expanded ? 2990 : gs1 ? 192 : 18))
            throw new ArgumentException("Composite 主数据超过线性组件输入长度限制。", "primaryData");
        if (element.SecondaryData.Length > 2990)
            throw new ArgumentException("Composite 次数据超过编码容量。", "secondaryData");
        // Parser fields, normalized copies, binary builders (13 bits/character),
        // general field, codewords, correction arrays and linear patterns coexist.
        using var workspace = PrintResourceScope.Reserve(
            checked(16384L + 128L * (element.PrimaryData.Length + element.SecondaryData.Length)),
            "Composite.EncodingWorkspace");
        var primary = NormalizePrimary(element.LinearType, element.PrimaryData);
        if (!Gs1ApplicationIdentifierParser.IsBracketed(element.SecondaryData))
            throw new ArgumentException("Composite 次数据必须为括号式 GS1 AI。", "secondaryData");
        var secondary = Gs1ApplicationIdentifierParser.NormalizeForProtocol(element.SecondaryData,
            "secondaryData", 2990);
        cancellationToken.ThrowIfCancellationRequested();
        var columns = element.LinearType == BarcodeType.Gs1DataBarLimited ? 3 : 4;
        var mode = gs1 ? Gs1CompositeMode.CcC
            : element.Mode == Gs1CompositeMode.CcB ? Gs1CompositeMode.CcB : Gs1CompositeMode.CcA;
        string linear;
        try
        {
            linear = gs1 ? EncodeGs1Linear(primary, cancellationToken)
                : EscPosRasterCanvas.EncodeGs1CompositeLinearPattern(element.LinearType, primary, cancellationToken);
        }
        catch (ArgumentException error) when (expanded)
        {
            throw new ArgumentException(error.Message, "primaryData", error);
        }
        var ccC = gs1 ? new CcCLayout(linear.Length) : null;
        var bits = BuildBinary(secondary, columns, mode, cancellationToken, ccC);
        if (bits == null && !gs1 && element.Mode == Gs1CompositeMode.Auto)
        {
            mode = Gs1CompositeMode.CcB;
            bits = BuildBinary(secondary, columns, mode, cancellationToken);
        }
        if (bits == null)
            throw new ArgumentException($"Composite 次数据超过 {mode} 容量。", "secondaryData");
        var twoDimensional = gs1 ? EncodeCcC(bits, ccC, cancellationToken)
            : EncodeComponent(bits, columns, mode, cancellationToken);
        var separator = BuildSeparator(element.LinearType, linear, cancellationToken);
        var topShift = element.LinearType == BarcodeType.Gs1DataBarLimited && mode == Gs1CompositeMode.CcA ? 1 : 0;
        if (expanded)
        {
            // ISO/IEC 24723 12.3: align after the first space/bar transition of the linear component.
            topShift = 1;
            while (topShift < linear.Length && linear[topShift - 1] == '0' && linear[topShift] == '1') topShift++;
        }
        var bottomShift = gs1 ? 7 : element.LinearType == BarcodeType.Gs1DataBarOmnidirectional ? 4
            : element.LinearType == BarcodeType.Gs1DataBarLimited && mode == Gs1CompositeMode.CcB ? 9 : 0;
        var width = Math.Max(twoDimensional[0].Length + topShift, linear.Length + bottomShift);
        var height = twoDimensional.Length + 2;
        PrintResourceScope.Retain(checked(((width + 31L) / 32) * height * sizeof(int)),
            "Composite.ModuleMatrix");
        var matrix = new BitMatrix(width, height);
        for (var row = 0; row < height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pattern = row < twoDimensional.Length ? twoDimensional[row]
                : row == twoDimensional.Length ? separator : linear;
            var shift = row < twoDimensional.Length ? topShift : bottomShift;
            for (var column = 0; column < pattern.Length; column++)
                if (pattern[column] == '1') matrix[column + shift, row] = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        var result = new Gs1CompositeSymbol(matrix, mode);
        operation?.CommitReservations();
        return result;
    }

    /// <summary>只编码模块矩阵，测量含静区的旋转前实际尺寸。</summary>
    internal static (int Width, int Height) Measure(PrintGs1CompositeElement element,
        CancellationToken cancellationToken = default)
    {
        using var measurement = PrintResourceScope.BeginTemporary();
        var symbol = Encode(element, cancellationToken);
        return Dimensions(element, symbol);
    }

    private static (int Width, int Height) Dimensions(PrintGs1CompositeElement element, Gs1CompositeSymbol symbol)
    {
        var zone = element.QuietZone;
        var width = checked((long)symbol.Matrix.Width * element.ModuleWidth + (zone?.Left ?? 0) + (zone?.Right ?? 0));
        var height = checked((long)(symbol.ComponentRows * symbol.ComponentRowHeight + 1) * element.ModuleWidth + element.LinearBarHeight
            + (zone?.Top ?? 0) + (zone?.Bottom ?? 0));
        if (width > int.MaxValue || height > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(element), "Composite 尺寸超过矩阵容量。");
        return ((int)width, (int)height);
    }

    /// <summary>以实际码图尺寸、完整静区和旋转生成不可拆分的图片模型。</summary>
    internal static PrintImageElement Rasterize(PrintGs1CompositeElement element,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = PrintResourceScope.BeginTemporary();
        var symbol = Encode(element, cancellationToken);
        var module = element.ModuleWidth;
        var zone = element.QuietZone;
        var left = zone?.Left ?? 0;
        var top = zone?.Top ?? 0;
        var dimensions = Dimensions(element, symbol);
        var width = dimensions.Width;
        var componentHeight = checked(symbol.ComponentRows * symbol.ComponentRowHeight * module);
        var height = dimensions.Height;
        var rowBytes = checked((int)((width + 7L) / 8));
        var raster = BudgetedBuffer.Create(checked(rowBytes * height), "Composite.Raster", data =>
        {
            for (var y = 0; y < height - top - (zone?.Bottom ?? 0); y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = y < componentHeight ? y / (symbol.ComponentRowHeight * module)
                    : y < componentHeight + module ? symbol.ComponentRows : symbol.ComponentRows + 1;
                for (var x = 0; x < symbol.Matrix.Width; x++)
                {
                    if (!symbol.Matrix[x, row]) continue;
                    var startX = checked(left + x * module);
                    for (var dx = 0; dx < module; dx++)
                    {
                        if ((dx & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                        data[(top + y) * rowBytes + (startX + dx) / 8] |= (byte)(0x80 >> ((startX + dx) & 7));
                    }
                }
            }
        });
        cancellationToken.ThrowIfCancellationRequested();
        var result = new PrintImageElement(element.X, element.Y, width, height, raster, element.Rotation,
            compressed: true) { SourcePath = element.SourcePath };
        cancellationToken.ThrowIfCancellationRequested();
        operation?.CommitReservations();
        return result;
    }

    private static string NormalizePrimary(BarcodeType type, string data)
    {
        if (type == BarcodeType.UccEan128)
            return Gs1ApplicationIdentifierParser.NormalizeForProtocol(data, "primaryData", 48);
        if (type == BarcodeType.Gs1DataBarExpanded)
        {
            if (!Gs1ApplicationIdentifierParser.IsBracketed(data))
                throw new ArgumentException("Expanded Composite 主数据必须为括号式 GS1 AI。", "primaryData");
            var normalized = Gs1ApplicationIdentifierParser.NormalizeForProtocol(data, "primaryData", 2990);
            if (normalized.StartsWith("01", StringComparison.Ordinal))
            {
                var checksum = 0;
                for (var index = 2; index < 15; index++) checksum += (normalized[index] - '0') * (index % 2 == 0 ? 3 : 1);
                if (normalized[15] - '0' != (10 - checksum % 10) % 10)
                    throw new ArgumentException("Expanded Composite GTIN 校验位无效。", "primaryData");
            }
            return EscPosRasterCanvas.NormalizeCompositeExpanded(normalized);
        }
        var bracketed = data.StartsWith("(01)", StringComparison.Ordinal) || data.StartsWith("[01]", StringComparison.Ordinal);
        if (bracketed) data = data.Substring(4);
        if (data.Length != 13 && data.Length != 14 || bracketed && data.Length != 14)
            throw new ArgumentException("Composite 主数据必须为 13 位数据或有效的 14 位 GTIN。", "primaryData");
        var sum = 0;
        for (var index = 0; index < data.Length; index++)
        {
            if (data[index] < '0' || data[index] > '9')
                throw new ArgumentException("Composite GTIN 仅允许 ASCII 数字。", "primaryData");
            if (index < 13) sum += (data[index] - '0') * (index % 2 == 0 ? 3 : 1);
        }
        if (data.Length == 14 && data[13] - '0' != (10 - sum % 10) % 10)
            throw new ArgumentException("Composite GTIN 校验位无效。", "primaryData");
        if (type == BarcodeType.Gs1DataBarLimited && data[0] > '1')
            throw new ArgumentException("DataBar Limited 主数据范围为 0 至 1999999999999。", "primaryData");
        return data.Length == 14 ? data.Substring(0, 13) : data;
    }

    private static string BuildBinary(string source, int columns, Gs1CompositeMode mode,
        CancellationToken cancellationToken, CcCLayout ccC = null)
    {
        var binary = new StringBuilder(checked(13 * source.Length + 1000));
        var read = 0;
        var generalMode = 0;
        var cropPosition = -1;
        var crop = 0;
        var fnc1Latch = false;
        var alphaPad = false;
        var dateMethod = (source.StartsWith("11", StringComparison.Ordinal) ||
            source.StartsWith("17", StringComparison.Ordinal)) && source.Length >= 8 &&
            ReadNumber(source, 4, 2) >= 1 && ReadNumber(source, 4, 2) <= 12 && ReadNumber(source, 6, 2) <= 31;
        if (source.StartsWith("10", StringComparison.Ordinal) || dateMethod)
        {
            Append(binary, 2, 2);
            if (source[1] == '0')
            {
                Append(binary, 3, 2);
                read = 2;
            }
            else
            {
                var date = ReadNumber(source, 2, 2) * 384 + (ReadNumber(source, 4, 2) - 1) * 32 + ReadNumber(source, 6, 2);
                Append(binary, date, 16);
                Append(binary, source[1] == '1' ? 0 : 1, 1);
                read = 8;
                if (source.Length > read + 1 && source[read] == '1' && source[read + 1] == '0') read += 2;
                else if (source.Length > read) fnc1Latch = true;
                else Append(binary, 15, 9); // Date-only alphanumeric latch and FNC1.
            }
        }
        else if (!TryAi90(source, binary, ref read, ref generalMode, out cropPosition, out crop, out alphaPad,
                     cancellationToken))
            binary.Append('0');

        var general = new StringBuilder(source.Length + 1);
        if (fnc1Latch) general.Append(Fnc1);
        for (var index = read; index < source.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index == cropPosition) index += crop;
            else general.Append(source[index]);
        }
        var lastDigit = '\0';
        if (general.Length != 0)
        {
            alphaPad = false;
            EscPosRasterCanvas.EncodeGs1DataBarExpandedGeneralField(general.ToString(), 0, binary,
                cancellationToken, out generalMode, out lastDigit, generalMode);
        }
        var target = TargetBits(binary.Length, columns, mode, ccC);
        if (target == 0) return null;
        var remainder = target - binary.Length;
        if (lastDigit != '\0')
        {
            if (remainder >= 4 && remainder <= 6)
            {
                Append(binary, lastDigit - '0' + 1, 4);
                if (remainder > 4) Append(binary, 0, remainder - 4);
            }
            else Append(binary, 11 * (lastDigit - '0') + 18, 7);
        }
        target = TargetBits(binary.Length, columns, mode, ccC);
        if (target == 0) return null;
        if (binary.Length < target)
        {
            if (alphaPad) Append(binary, 31, 5);
            if (generalMode == 0) Append(binary, 0, 4);
            while (binary.Length < target) Append(binary, 4, 5);
        }
        return binary.ToString(0, target);
    }

    private static bool TryAi90(string source, StringBuilder binary, ref int read, ref int generalMode,
        out int cropPosition, out int crop, out bool alphaPad, CancellationToken cancellationToken)
    {
        cropPosition = -1;
        crop = 0;
        alphaPad = false;
        if (!source.StartsWith("90", StringComparison.Ordinal)) return false;
        var end = source.IndexOf(Fnc1, 2);
        if (end < 0) end = source.Length;
        var alpha = 0;
        var numeric = 0;
        var other = 0;
        var letterPosition = -1;
        for (var index = 2; index < end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = source[index];
            if (value >= 'A' && value <= 'Z') alpha++;
            else if (value >= '0' && value <= '9') numeric++;
            else other++;
        }
        if (source[2] != '0')
        {
            for (var index = 2; index < end && index < 6; index++)
            {
                if (source[index] >= 'A' && source[index] <= 'Z') { letterPosition = index; break; }
                if (source[index] < '0' || source[index] > '9') break;
            }
        }
        if (letterPosition < 0) return false;
        Append(binary, 3, 2);
        numeric -= letterPosition - 2;
        alpha--;
        var alphaMode = other == 0 && alpha > numeric;
        if (alphaMode) Append(binary, 3, 2);
        else if (other == 0 && alpha == 0) Append(binary, 2, 2);
        else { binary.Append('0'); generalMode = 1; }
        if (end < source.Length)
        {
            if (source.Length >= end + 3 && source.Substring(end + 1, 2) == "21") crop = 1;
            else if (source.Length >= end + 5 && source.Substring(end + 1, 4) == "8004") crop = 3;
        }
        if (crop == 0) binary.Append('0');
        else { Append(binary, crop == 1 ? 2 : 3, 2); cropPosition = end + 1; }
        var number = ReadNumber(source, 2, letterPosition - 2);
        var tableLetter = number < 31 ? "BDHIJKLNPQRSTVWZ".IndexOf(source[letterPosition]) : -1;
        if (tableLetter >= 0) { Append(binary, number, 5); Append(binary, tableLetter, 4); }
        else { Append(binary, 31, 5); Append(binary, number, 10); Append(binary, source[letterPosition] - 'A', 5); }
        read = letterPosition + 1;
        if (alphaMode)
        {
            while (read < source.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = source[read++];
                if (value == Fnc1) { Append(binary, 31, 5); break; }
                if (value >= 'A' && value <= 'Z') Append(binary, value - 'A', 5);
                else Append(binary, value + 4, 6);
            }
            alphaPad = true;
        }
        return true;
    }

    private static int TargetBits(int length, int columns, Gs1CompositeMode mode, CcCLayout ccC)
    {
        if (mode == Gs1CompositeMode.CcC) return ccC.TargetBits(length);
        var sizes = mode == Gs1CompositeMode.CcA
            ? columns == 3 ? AaThreeSizes : AaFourSizes
            : columns == 3 ? BbThreeSizes : BbFourSizes;
        foreach (var size in sizes) if (length <= size) return size;
        return 0;
    }

    private static string[] EncodeComponent(string bits, int columns, Gs1CompositeMode mode,
        CancellationToken cancellationToken)
    {
        var codewords = new List<int>(180);
        int variant, rows, correctionCount, correctionOffset, left, centre, right, cluster;
        int[] coefficients;
        if (mode == Gs1CompositeMode.CcA)
        {
            // Each block has at most 69 bits. Repeated multiply/add avoids large
            // integers and keeps seven base-928 words including leading zeroes.
            for (var start = 0; start < bits.Length; start += 69)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bitCount = Math.Min(69, bits.Length - start);
                var words = new int[bitCount / 10 + 1];
                for (var bit = 0; bit < bitCount; bit++)
                {
                    var carry = bits[start + bit] - '0';
                    for (var index = words.Length - 1; index >= 0; index--)
                    {
                        var value = words[index] * 2 + carry;
                        words[index] = value % 928;
                        carry = value / 928;
                    }
                }
                codewords.AddRange(words);
            }
            var dataCounts = columns == 3 ? new[] { 8, 10, 12, 14, 17 } : new[] { 8, 11, 14, 17, 20 };
            variant = Array.IndexOf(dataCounts, codewords.Count) + (columns == 3 ? 7 : 12);
            rows = Gs1CompositeTables.AaVariants[variant];
            correctionCount = Gs1CompositeTables.AaVariants[17 + variant];
            correctionOffset = Gs1CompositeTables.AaVariants[34 + variant];
            coefficients = Gs1CompositeTables.AaCoefficients;
            left = Gs1CompositeTables.AaRap[variant];
            centre = Gs1CompositeTables.AaRap[17 + variant];
            right = Gs1CompositeTables.AaRap[34 + variant];
            cluster = Gs1CompositeTables.AaRap[51 + variant] / 3;
        }
        else
        {
            codewords.Add(920);
            var byteCount = bits.Length / 8;
            codewords.Add(byteCount % 6 == 0 ? 924 : 901);
            var bytes = new byte[byteCount];
            for (var index = 0; index < byteCount; index++) bytes[index] = (byte)ReadBits(bits, index * 8, 8);
            for (var index = 0; index < bytes.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (bytes.Length - index >= 6)
                {
                    var value = 0L;
                    for (var count = 0; count < 6; count++) value = (value << 8) | bytes[index++];
                    var words = new int[5];
                    for (var count = 4; count >= 0; count--) { words[count] = (int)(value % 900); value /= 900; }
                    codewords.AddRange(words);
                }
                else codewords.Add(bytes[index++]);
            }
            var dataCounts = columns == 3 ? new[] { 6, 10, 14, 18, 24, 34, 46, 58, 70, 82 }
                : new[] { 8, 12, 18, 24, 30, 39, 54, 72, 90, 108, 126 };
            variant = Array.FindIndex(dataCounts, count => codewords.Count <= count) + (columns == 3 ? 13 : 23);
            rows = Gs1CompositeTables.MicroVariants[34 + variant];
            correctionCount = Gs1CompositeTables.MicroVariants[68 + variant];
            correctionOffset = Gs1CompositeTables.MicroVariants[102 + variant];
            coefficients = Gs1CompositeTables.MicroCoefficients;
            while (codewords.Count < columns * rows - correctionCount) codewords.Add(900);
            left = Gs1CompositeTables.MicroRap[variant];
            centre = Gs1CompositeTables.MicroRap[34 + variant];
            right = Gs1CompositeTables.MicroRap[68 + variant];
            cluster = Gs1CompositeTables.MicroRap[102 + variant] / 3;
        }
        var correction = new int[correctionCount];
        foreach (var word in codewords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var total = (word + correction[correctionCount - 1]) % 929;
            for (var index = correctionCount - 1; index >= 0; index--)
                correction[index] = ((index == 0 ? 0 : correction[index - 1]) + 929
                    - total * coefficients[correctionOffset + index] % 929) % 929;
        }
        for (var index = correctionCount - 1; index >= 0; index--)
            codewords.Add(correction[index] == 0 ? 0 : 929 - correction[index]);
        var result = new string[rows];
        for (var row = 0; row < rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pattern = new StringBuilder(100);
            if (mode != Gs1CompositeMode.CcA || columns != 3)
                Append(pattern, Gs1CompositeTables.SideRap[left - 1], 10);
            for (var column = 0; column < columns; column++)
            {
                if (columns == 3 && column == 1 || columns == 4 && column == 2)
                    Append(pattern, Gs1CompositeTables.CentreRap[centre - 1], 10);
                Append(pattern, Gs1CompositeTables.BitPatterns[929 * cluster + codewords[row * columns + column]], 16);
                pattern.Append('0');
            }
            Append(pattern, Gs1CompositeTables.SideRap[right - 1], 10);
            pattern.Append('1');
            result[row] = pattern.ToString();
            left = left % 52 + 1;
            centre = centre % 52 + 1;
            right = right % 52 + 1;
            cluster = (cluster + 1) % 3;
        }
        return result;
    }

    private static string BuildSeparator(BarcodeType type, string linear, CancellationToken cancellationToken)
    {
        if (type == BarcodeType.Gs1DataBarExpanded)
            return BuildExpandedSeparator(linear, cancellationToken);
        var separator = new char[linear.Length];
        for (var index = 0; index < separator.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            separator[index] = (type == BarcodeType.UccEan128 ||
                index >= 4 && index < (type == BarcodeType.Gs1DataBarLimited ? 70 : 92))
                && linear[index] == '0' ? '1' : '0';
        }
        if (type == BarcodeType.Gs1DataBarOmnidirectional)
        {
            foreach (var start in new[] { 18, 63 })
            {
                var latch = true;
                for (var index = start; index < start + 13; index++)
                {
                    separator[index] = linear[index] == '0' && latch ? '1' : '0';
                    latch = linear[index] == '1' || !latch;
                }
            }
        }
        return new string(separator);
    }

    /// <summary>
    /// 生成单行 Expanded 的 Composite 分隔图案，调整二维组件下方的前四组交替定位符。
    /// </summary>
    /// <param name="linear">包含连接标志和保护条的完整线性模块串。</param>
    /// <param name="cancellationToken">观察调用方取消，不持有调用方资源。</param>
    /// <returns>与线性组件等宽的一模块高分隔行。</returns>
    private static string BuildExpandedSeparator(string linear, CancellationToken cancellationToken)
    {
        // Zint 2.15.0 rss.c dbar_exp_separator, BSD-3-Clause (see ThirdPartyNotices.txt).
        var separator = new char[linear.Length];
        for (var index = 0; index < linear.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            separator[index] = index >= 4 && index < linear.Length - 4 && linear[index] == '0' ? '1' : '0';
        }
        var spaceLatch = false;
        for (var block = 0; block < 4; block++)
        {
            var start = 49 * block + 19 + (block % 2 == 0 ? 0 : 2);
            for (var index = start; index < start + 13 && index < linear.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                separator[index] = linear[index] == '0' && !spaceLatch ? '1' : '0';
                spaceLatch = linear[index] == '0' && !spaceLatch;
            }
        }
        return new string(separator);
    }

    private static int ReadNumber(string data, int start, int count)
    {
        var result = 0;
        for (var index = start; index < start + count; index++) result = result * 10 + data[index] - '0';
        return result;
    }

    private static int ReadBits(string data, int start, int count)
    {
        var result = 0;
        for (var index = start; index < start + count; index++) result = result * 2 + data[index] - '0';
        return result;
    }

    private static void Append(StringBuilder binary, int value, int count)
    {
        for (var bit = count - 1; bit >= 0; bit--) binary.Append((value & (1 << bit)) != 0 ? '1' : '0');
    }
}

internal sealed class Gs1CompositeSymbol
{
    internal BitMatrix Matrix { get; }
    internal Gs1CompositeMode Mode { get; }
    internal int ComponentRows => Matrix.Height - 2;
    /// <summary>
    /// 获取二维组件行高对应的模块数；CC-C 为三，CC-A/B 为二。
    /// </summary>
    internal int ComponentRowHeight => Mode == Gs1CompositeMode.CcC ? 3 : 2;

    internal Gs1CompositeSymbol(BitMatrix matrix, Gs1CompositeMode mode)
    {
        Matrix = matrix;
        Mode = mode;
    }
}
