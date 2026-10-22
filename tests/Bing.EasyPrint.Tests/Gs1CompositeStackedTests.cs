using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class Gs1CompositeStackedTests
{
    public static IEnumerable<object[]> Vectors()
        => Gs1CompositeStackedVectors.All.Select(vector => new object[] { vector.Name });

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Complete_matrix_matches_independent_stacked_reference(string name)
    {
        var vector = Gs1CompositeStackedVectors.All.Single(value => value.Name == name);
        vector.Validate();
        var mode = vector.Mode == "CC-A" ? Gs1CompositeMode.CcA : Gs1CompositeMode.CcB;
        var symbol = Gs1CompositeEncoder.Encode(Element(vector.PrimaryData, vector.SecondaryData, mode));
        Assert.Equal(mode, symbol.Mode);
        Assert.Equal(vector.Height - 4, symbol.ComponentRows);
        Assert.Equal(2, symbol.ComponentRowHeight);
        Assert.Equal(vector.Width, symbol.Matrix.Width);
        Assert.Equal(vector.Height, symbol.Matrix.Height);
        for (var row = 0; row < vector.Height; row++)
        {
            var actual = new StringBuilder(vector.Width);
            for (var column = 0; column < vector.Width; column++) actual.Append(symbol.Matrix[column, row] ? '1' : '0');
            Assert.Equal(vector.Rows[row], actual.ToString());
        }
        if (name != "zint-stacked-34") // Explicit B fits in A; Auto must prefer A.
            Assert.Equal(symbol.Matrix, Gs1CompositeEncoder.Encode(
                Element(vector.PrimaryData, vector.SecondaryData)).Matrix);
    }

    [Fact]
    public void Auto_prefers_a_and_falls_back_to_b_then_rejects_capacity_without_leaking()
    {
        var a = Gs1CompositeEncoder.Encode(Element(secondary: "(91)123456789012345678901234567"));
        Assert.Equal(Gs1CompositeMode.CcA, a.Mode);
        var bData = Gs1CompositeStackedVectors.All.Last().SecondaryData;
        Assert.Equal(Gs1CompositeMode.CcB, Gs1CompositeEncoder.Encode(Element(secondary: bData)).Mode);
        using var scope = PrintResourceScope.Begin(128 * 1024);
        Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(secondary: bData, mode: Gs1CompositeMode.CcA)));
        Assert.Equal(0, scope.CurrentBytes);
        var error = Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(secondary: "(91)" + new string('A', 90))));
        Assert.Equal("secondaryData", error.ParamName);
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Encode(Element(), new CancellationToken(true)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<PrintResourceLimitException>(() => Gs1CompositeEncoder.Rasterize(
            new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarStacked, "12345678901231", "(91)123", 1200, 100)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.NotEmpty(Gs1CompositeEncoder.Rasterize(Element()).Raster);
    }

    [Fact]
    public void Gtin_forms_preserve_linkage_and_minimum_height_is_enforced()
    {
        var expected = Gs1CompositeEncoder.Encode(Element()).Matrix;
        foreach (var data in new[] { "1234567890123", "12345678901231", "(01)12345678901231", "[01]12345678901231" })
            Assert.Equal(expected, Gs1CompositeEncoder.Encode(Element(data)).Matrix);
        var ordinary = EscPosRasterCanvas.EncodeGs1DataBarStackedPattern("1234567890123");
        Assert.NotEqual(ordinary[0], string.Concat(Enumerable.Range(0, 50).Select(x => expected[x, expected.Height - 3] ? '1' : '0')));
        Assert.Throws<NotSupportedException>(() => Element(mode: Gs1CompositeMode.CcC));
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new PrintGs1CompositeElement(
            0, 0, BarcodeType.Gs1DataBarStacked, "12345678901231", "(91)123", 23, 2));
        Assert.Equal("linearBarHeight", error.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintGs1CompositeElement(
            0, 0, BarcodeType.Gs1DataBarStacked, "12345678901231", "(91)123", int.MaxValue, int.MaxValue));
        Assert.NotEmpty(Gs1CompositeEncoder.Rasterize(new PrintGs1CompositeElement(
            0, 0, BarcodeType.Gs1DataBarStacked, "12345678901231", "(91)123", 24, 2)).Raster);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345678901230")]
    [InlineData("(01)1234567890123")]
    [InlineData("(01)12345678901231(10)A")]
    [InlineData("1234567890123A")]
    public void Invalid_primary_releases_budget_and_identifies_field(string primary)
    {
        using var scope = PrintResourceScope.Begin(128 * 1024);
        Assert.Equal("primaryData", Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(primary))).ParamName);
        Assert.Equal(0, scope.CurrentBytes);
        Assert.NotNull(Gs1CompositeEncoder.Encode(Element()));
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_pixels_match_reference_including_row_height_quiet_zones_and_rotation(RotationAngle rotation)
    {
        var vector = Gs1CompositeStackedVectors.All[0];
        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarStacked, vector.PrimaryData,
            vector.SecondaryData, 31, 2, Gs1CompositeMode.CcA, rotation, new PrintQuietZone(3, 5, 7, 11));
        var image = Gs1CompositeEncoder.Rasterize(element);
        Assert.Equal(vector.Width * 2 + 10, image.Width);
        Assert.Equal((vector.Height - 4) * 4 + 4 + 31 + 16, image.Height);
        using var output = PrintImageRotation.Prepare(image, CancellationToken.None);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var expected = false;
            if (x >= 3 && x < image.Width - 7 && y >= 5 && y < image.Height - 11)
            {
                var yy = y - 5;
                var componentHeight = (vector.Height - 4) * 4;
                var row = yy < componentHeight ? yy / 4
                    : yy < componentHeight + 2 ? vector.Height - 4
                    : yy < componentHeight + 2 + 12 ? vector.Height - 3 // floor(31 * 5 / 12)
                    : yy < componentHeight + 2 + 12 + 2 ? vector.Height - 2 : vector.Height - 1;
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

    internal static PrintGs1CompositeElement Element(string primary = "12345678901231",
        string secondary = "(91)123", Gs1CompositeMode mode = Gs1CompositeMode.Auto)
        => new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarStacked, primary, secondary, 40, 2, mode);
}
