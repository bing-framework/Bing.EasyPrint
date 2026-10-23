using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Protocols;
using Xunit;
using ZXing;
using ZXing.Common;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 GS1 QR/DataMatrix 的共享编码首位标志和 payload。</summary>
public sealed class Gs1TwoDimensionalEncoderTests
{
    [Theory]
    [InlineData(false, "(01)12345678901231(10)LOT%20A(17)260101")]
    [InlineData(true, "011234567890123110LOT%20A\u001D17260101")]
    public void Qr_gs1_mode_uses_gs1_symbology_without_polluting_payload(bool gs1, string expected)
    {
        const string input = "(01)12345678901231(10)LOT%20A(17)260101";
        var matrix = Gs1TwoDimensionalEncoder.EncodeQr(input, "M", gs1, CancellationToken.None);
        var result = Decode(matrix, BarcodeFormat.QR_CODE);
        Assert.Equal(gs1 ? "]Q3" : "]Q2", Metadata(result, ResultMetadataType.SYMBOLOGY_IDENTIFIER));
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Gs1_qr_gtin_payload_matches_reference_encoding_without_extra_gs()
    {
        const string normalized = "0112345678901231";
        var matrix = Gs1TwoDimensionalEncoder.EncodeQr("(01)12345678901231", "M", true, CancellationToken.None);
        var result = Decode(matrix, BarcodeFormat.QR_CODE);
        Assert.Equal(normalized, result.Text);
        Assert.Equal("]Q3", Metadata(result, ResultMetadataType.SYMBOLOGY_IDENTIFIER));

        var reference = new MultiFormatWriter().encode(normalized, BarcodeFormat.QR_CODE, 1, 1,
            new Dictionary<EncodeHintType, object>
            {
                [EncodeHintType.GS1_FORMAT] = true,
                [EncodeHintType.ERROR_CORRECTION] = "M",
                [EncodeHintType.MARGIN] = 0
            });
        var referenceResult = Decode(reference, BarcodeFormat.QR_CODE);
        Assert.Equal(referenceResult.Text, result.Text);
        Assert.Equal(reference.Width, matrix.Width);
        Assert.Equal(reference.Height, matrix.Height);
    }

    [Theory]
    [InlineData("(10)100%", "10100%")]
    [InlineData("(10)100%%", "10100%%")]
    public void Gs1_qr_alphanumeric_literal_percent_round_trips(string input, string expected)
    {
        var matrix = Gs1TwoDimensionalEncoder.EncodeQr(input, "M", true, CancellationToken.None);
        var result = Decode(matrix, BarcodeFormat.QR_CODE);
        Assert.Equal(expected, result.Text);
        Assert.Equal("]Q3", Metadata(result, ResultMetadataType.SYMBOLOGY_IDENTIFIER));
    }

    [Fact]
    public void Gs1_qr_rejects_non_ai_payload()
    {
        Assert.Throws<ArgumentException>(() =>
            Gs1TwoDimensionalEncoder.EncodeQr("NOT-GS1", "M", true, CancellationToken.None));
    }

    [Fact]
    public void Gs1_data_matrix_rejects_non_ai_payload()
    {
        Assert.Throws<ArgumentException>(() =>
            Gs1TwoDimensionalEncoder.EncodeDataMatrix("NOT-GS1", 96, 96, true, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Data_matrix_gs1_mode_uses_gs1_symbology_and_preserves_separator(bool gs1)
    {
        const string input = "(01)12345678901231(10)LOT%20A(17)260101";
        var matrix = Gs1TwoDimensionalEncoder.EncodeDataMatrix(input, 96, 96, gs1, CancellationToken.None);
        var result = Decode(matrix, BarcodeFormat.DATA_MATRIX);
        Assert.Equal(gs1 ? "]d2" : "]d4", Metadata(result, ResultMetadataType.SYMBOLOGY_IDENTIFIER));
        Assert.Contains("LOT%20A", result.Text);
        if (gs1) Assert.Contains("\u001D17260101", result.Text);
        else Assert.Contains("(17)260101", result.Text);
    }

    [Fact]
    public void Gs1_qr_preserves_literal_double_percent_without_byte_mode_prefix()
    {
        var matrix = Gs1TwoDimensionalEncoder.EncodeQr("(10)100%%", "M", true, CancellationToken.None);
        var result = Decode(matrix, BarcodeFormat.QR_CODE);
        Assert.Equal("10100%%", result.Text);
        Assert.Equal("]Q3", Metadata(result, ResultMetadataType.SYMBOLOGY_IDENTIFIER));
        Assert.False(result.Text.StartsWith('\u001D'));
    }

    private static ZXing.Result Decode(BitMatrix matrix, BarcodeFormat format)
    {
        const int scale = 8;
        const int margin = 4 * scale;
        var width = matrix.Width * scale + margin * 2;
        var height = matrix.Height * scale + margin * 2;
        var rgb = new byte[width * height * 3];
        Array.Fill(rgb, (byte)255);
        for (var y = 0; y < matrix.Height; y++)
        for (var x = 0; x < matrix.Width; x++)
            if (matrix[x, y])
                for (var dy = 0; dy < scale; dy++)
                for (var dx = 0; dx < scale; dx++)
                {
                    var index = (((y * scale + dy + margin) * width) + x * scale + dx + margin) * 3;
                    rgb[index] = rgb[index + 1] = rgb[index + 2] = 0;
                }
        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { TryHarder = true, PossibleFormats = new[] { format } }
        };
        var result = reader.Decode(new RGBLuminanceSource(rgb, width, height, RGBLuminanceSource.BitmapFormat.RGB24));
        Assert.NotNull(result);
        return result!;
    }

    private static string Metadata(ZXing.Result result, ResultMetadataType type)
        => result.ResultMetadata != null && result.ResultMetadata.TryGetValue(type, out var value)
            ? Convert.ToString(value) ?? string.Empty : string.Empty;
}
