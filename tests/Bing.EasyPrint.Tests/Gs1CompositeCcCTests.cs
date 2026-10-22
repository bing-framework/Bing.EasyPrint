using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class Gs1CompositeCcCTests
{
    public static IEnumerable<object[]> Vectors()
        => Gs1CompositeCcCVectors.All.Select(vector => new object[] { vector.Name });

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Complete_matrix_matches_independent_cc_c_reference(string name)
    {
        var vector = Gs1CompositeCcCVectors.All.Single(value => value.Name == name);
        vector.Validate();
        var symbol = Gs1CompositeEncoder.Encode(Element(vector.PrimaryData, vector.SecondaryData));
        Assert.Equal(Gs1CompositeMode.CcC, symbol.Mode);
        Assert.Equal(3, symbol.ComponentRowHeight);
        Assert.Equal(vector.Width, symbol.Matrix.Width);
        Assert.Equal(vector.Height, symbol.Matrix.Height);
        for (var row = 0; row < vector.Height; row++)
        {
            var actual = new StringBuilder(vector.Width);
            for (var column = 0; column < vector.Width; column++) actual.Append(symbol.Matrix[column, row] ? '1' : '0');
            Assert.Equal(vector.Rows[row], actual.ToString());
        }
    }

    [Fact]
    public void Auto_uses_cc_c_and_equivalent_brackets_preserve_output()
    {
        var expected = Gs1CompositeEncoder.Encode(Element()).Matrix;
        Assert.Equal(expected, Gs1CompositeEncoder.Encode(Element(mode: Gs1CompositeMode.Auto)).Matrix);
        Assert.Equal(expected, Gs1CompositeEncoder.Encode(Element("[01]12345678901231")).Matrix);
        Assert.Equal(0, (int)Gs1CompositeMode.Auto);
        Assert.Equal(1, (int)Gs1CompositeMode.CcA);
        Assert.Equal(2, (int)Gs1CompositeMode.CcB);
        Assert.Equal(3, (int)Gs1CompositeMode.CcC);
    }

    [Theory]
    [InlineData(BarcodeType.UccEan128, Gs1CompositeMode.CcA)]
    [InlineData(BarcodeType.UccEan128, Gs1CompositeMode.CcB)]
    [InlineData(BarcodeType.Gs1DataBarLimited, Gs1CompositeMode.CcC)]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional, Gs1CompositeMode.CcC)]
    public void Unsupported_pairs_fail_at_construction(BarcodeType type, Gs1CompositeMode mode)
        => Assert.Throws<NotSupportedException>(() => new PrintGs1CompositeElement(0, 0, type, "", "", 20, mode: mode));

    [Theory]
    [InlineData("12345678901231")]
    [InlineData("(01)1234567890123")]
    [InlineData("(03)1")]
    [InlineData("(91)ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    public void Invalid_primary_is_rejected_and_budget_released(string primary)
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        var error = Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(primary)));
        Assert.Equal("primaryData", error.ParamName);
        Assert.Equal(0, scope.CurrentBytes);
        Assert.NotNull(Gs1CompositeEncoder.Encode(Element()));
    }

    [Theory]
    [InlineData(Gs1CompositeMode.CcC)]
    [InlineData(Gs1CompositeMode.Auto)]
    public void Capacity_failure_and_precancel_leave_budget_reusable(Gs1CompositeMode mode)
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        var data = string.Concat(Enumerable.Range(91, 9).Select(ai => "(" + ai + ")" + new string('A', 90)));
        // Repetition is permitted for these company-internal fields; use a valid parser payload beyond CC-C capacity.
        data += data;
        var error = Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(secondary: data, mode: mode)));
        Assert.Equal("secondaryData", error.ParamName);
        Assert.Contains("CcC 容量", error.Message);
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Encode(Element(), new CancellationToken(true)));
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Fact]
    public void Raster_scales_cc_c_rows_by_three_and_separator_by_one()
    {
        var element = Element();
        var symbol = Gs1CompositeEncoder.Encode(element);
        var dimensions = Gs1CompositeEncoder.Measure(element);
        Assert.Equal(symbol.Matrix.Width * 2, dimensions.Width);
        Assert.Equal((symbol.ComponentRows * 3 + 1) * 2 + 20, dimensions.Height);
        var raster = Gs1CompositeEncoder.Rasterize(element);
        Assert.Equal(dimensions.Width, raster.Width);
        Assert.Equal(dimensions.Height, raster.Height);
    }

    [Theory]
    [InlineData("(10)ABC", "]C110ABC", 101)]
    [InlineData("(01)12345678901231", "]C10112345678901231", 100)]
    [InlineData("(10)ABC(21)123456", "]C110ABC\u001D21123456", 100)]
    [InlineData("(10)12345(21)ABC", "]C11012345\u001D21ABC", 101)]
    public void Independent_code128_decoder_reads_primary_with_correct_linkage(string primary, string expected, int linkage)
    {
        var matrix = Gs1CompositeEncoder.Encode(Element(primary)).Matrix;
        // The linear component is shifted right by seven modules, independently of PDF417 row width.
        var row = matrix.Height - 1;
        var end = matrix.Width - 1;
        while (!matrix[end, row]) end--;
        var width = end - 7 + 1;
        var bits = new ZXing.Common.BitArray(width + 20);
        for (var x = 0; x < width; x++) bits[x + 10] = matrix[x + 7, row];
        var result = new ZXing.OneD.Code128Reader().decodeRow(0, bits,
            new Dictionary<ZXing.DecodeHintType, object> { [ZXing.DecodeHintType.ASSUME_GS1] = true });
        Assert.NotNull(result);
        Assert.Equal(expected, result.Text);
        Assert.Equal(linkage, result.RawBytes[result.RawBytes.Length - 3]);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_pixels_match_reference_including_row_height_quiet_zones_and_rotation(RotationAngle rotation)
    {
        var vector = Gs1CompositeCcCVectors.All[0];
        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.UccEan128, vector.PrimaryData,
            vector.SecondaryData, 7, 2, Gs1CompositeMode.CcC, rotation, new PrintQuietZone(3, 5, 7, 11));
        var image = Gs1CompositeEncoder.Rasterize(element);
        Assert.Equal(vector.Width * 2 + 10, image.Width);
        Assert.Equal((vector.Height - 2) * 6 + 2 + 7 + 16, image.Height);
        using var output = PrintImageRotation.Prepare(image, CancellationToken.None);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var expected = false;
            if (x >= 3 && x < image.Width - 7 && y >= 5 && y < image.Height - 11)
            {
                var yy = y - 5;
                var componentHeight = (vector.Height - 2) * 6;
                var row = yy < componentHeight ? yy / 6 : yy < componentHeight + 2 ? vector.Height - 2 : vector.Height - 1;
                expected = vector.Rows[row][(x - 3) / 2] == '1';
            }
            var dx = x;
            var dy = y;
            if (rotation == RotationAngle.Rotate90) { dx = image.Height - 1 - y; dy = x; }
            else if (rotation == RotationAngle.Rotate180) { dx = image.Width - 1 - x; dy = image.Height - 1 - y; }
            else if (rotation == RotationAngle.Rotate270) { dx = y; dy = image.Width - 1 - x; }
            Assert.Equal(expected, (output.Raster[dy * output.RowBytes + dx / 8] & (0x80 >> (dx & 7))) != 0);
        }
    }

    [Fact]
    public void Rejected_raster_allocation_releases_budget_and_allows_retry()
    {
        using var scope = PrintResourceScope.Begin(128 * 1024);
        Assert.Throws<PrintResourceLimitException>(() => Gs1CompositeEncoder.Rasterize(
            new PrintGs1CompositeElement(0, 0, BarcodeType.UccEan128, "(01)12345678901231",
                "(91)123456", 10000, 100, Gs1CompositeMode.CcC)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.NotEmpty(Gs1CompositeEncoder.Rasterize(Element()).Raster);
    }

    internal static PrintGs1CompositeElement Element(string primary = "(01)12345678901231",
        string secondary = "(91)123456", Gs1CompositeMode mode = Gs1CompositeMode.CcC)
        => new PrintGs1CompositeElement(0, 0, BarcodeType.UccEan128, primary, secondary, 20, mode: mode);
}
