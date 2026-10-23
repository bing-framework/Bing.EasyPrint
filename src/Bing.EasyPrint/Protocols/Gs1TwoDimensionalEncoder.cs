using System;
using System.Collections.Generic;
using System.Threading;
using Bing.EasyPrint.Printing;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;

namespace Bing.EasyPrint.Protocols;

internal static class Gs1TwoDimensionalEncoder
{
    internal static BitMatrix EncodeQr(string data, string errorLevel, bool gs1, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(data)) throw new ArgumentException("二维码数据不能为空。", nameof(data));
        var payload = gs1 ? PrepareGs1QrPayload(Normalize(data)) : data;
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = 0,
            [EncodeHintType.ERROR_CORRECTION] = Correction(errorLevel)
        };
        if (gs1) hints[EncodeHintType.GS1_FORMAT] = true;
        else hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
        PrintResourceScope.Retain(PrintMatrixBudget.QrBytes, gs1 ? "Matrix.Gs1Qr" : "Matrix.Qr");
        try
        {
            return new MultiFormatWriter().encode(payload, BarcodeFormat.QR_CODE, 1, 1, hints);
        }
        catch (WriterException error)
        {
            throw new ArgumentException("GS1 QR Code 无法编码。", nameof(data), error);
        }
    }

    internal static BitMatrix EncodeDataMatrix(string data, int width, int height, bool gs1,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(data)) throw new ArgumentException("二维点阵数据不能为空。", nameof(data));
        if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
        var payload = gs1 ? Normalize(data) : data;
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = 2
        };
        if (gs1)
        {
            hints[EncodeHintType.GS1_FORMAT] = true;
            hints[EncodeHintType.DATA_MATRIX_COMPACT] = true;
        }
        else hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
        PrintResourceScope.Retain(PrintMatrixBudget.MatrixCodeBytes(width, height, false),
            gs1 ? "Matrix.Gs1DataMatrix" : "Matrix.DataMatrix");
        try
        {
            return new MultiFormatWriter().encode(payload, BarcodeFormat.DATA_MATRIX, width, height, hints);
        }
        catch (WriterException error)
        {
            throw new ArgumentException("GS1 DataMatrix 无法编码。", nameof(data), error);
        }
    }

    private static string Normalize(string data)
        => Gs1ElementStringParser.Rebuild(Gs1ElementStringParser.Parse(data, nameof(data)));

    private static string PrepareGs1QrPayload(string data)
    {
        if (!IsQrAlphanumeric(data)) return data;
        var escaped = new System.Text.StringBuilder(data.Length);
        foreach (var character in data)
        {
            escaped.Append(character);
            if (character == '%') escaped.Append('%');
        }
        return escaped.ToString();
    }

    private static bool IsQrAlphanumeric(string data)
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";
        foreach (var character in data)
            if (alphabet.IndexOf(character) < 0) return false;
        return data.Length > 0;
    }

    private static ErrorCorrectionLevel Correction(string level)
    {
        if (level == null || level.Length != 1 || "LMQH".IndexOf(level, StringComparison.Ordinal) < 0)
            throw new ArgumentException("二维码纠错级别无效。", nameof(level));
        return level == "L" ? ErrorCorrectionLevel.L : level == "M" ? ErrorCorrectionLevel.M :
            level == "Q" ? ErrorCorrectionLevel.Q : ErrorCorrectionLevel.H;
    }
}
