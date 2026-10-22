using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class Gs1CompositeGs1128AbTests
{
    public static IEnumerable<object[]> Vectors()
        => Gs1CompositeGs1128AbVectors.All.Select(vector => new object[] { vector.Name });

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Complete_matrix_matches_fixed_reference_including_alignment(string name)
    {
        var vector = Gs1CompositeGs1128AbVectors.All.Single(value => value.Name == name);
        vector.Validate();
        var mode = vector.Mode == "CC-A" ? Gs1CompositeMode.CcA : Gs1CompositeMode.CcB;
        var symbol = Gs1CompositeEncoder.Encode(Element(mode, vector.PrimaryData, vector.SecondaryData));
        Assert.Equal(mode, symbol.Mode);
        Assert.Equal(2, symbol.ComponentRowHeight);
        Assert.Equal(vector.Width, symbol.Matrix.Width);
        Assert.Equal(vector.Height, symbol.Matrix.Height);
        for (var row = 0; row < vector.Height; row++)
        {
            var actual = new StringBuilder(vector.Width);
            for (var column = 0; column < vector.Width; column++) actual.Append(symbol.Matrix[column, row] ? '1' : '0');
            Assert.Equal(vector.Rows[row], actual.ToString());
        }
    }

    [Theory]
    [InlineData("(10)ABC", "]C110ABC", 99)]
    [InlineData("(01)12345678901231", "]C10112345678901231", 101)]
    [InlineData("(10)ABC(21)123456", "]C110ABC\u001D21123456", 101)]
    [InlineData("(10)12345(21)ABC", "]C11012345\u001D21ABC", 99)]
    public void Independent_decoder_reads_primary_with_a_b_linkage(string primary, string expected, int linkage)
    {
        foreach (var mode in new[] { Gs1CompositeMode.CcA, Gs1CompositeMode.CcB })
        {
            var matrix = Gs1CompositeEncoder.Encode(Element(mode, primary)).Matrix;
            var row = matrix.Height - 1;
            var bits = new ZXing.Common.BitArray(matrix.Width + 20);
            for (var x = 0; x < matrix.Width; x++) bits[x + 10] = matrix[x, row];
            var result = new ZXing.OneD.Code128Reader().decodeRow(0, bits,
                new Dictionary<ZXing.DecodeHintType, object> { [ZXing.DecodeHintType.ASSUME_GS1] = true });
            Assert.NotNull(result);
            Assert.Equal(expected, result.Text);
            Assert.Equal(linkage, result.RawBytes[result.RawBytes.Length - 3]);
        }
    }

    [Theory]
    [InlineData(Gs1CompositeMode.CcA)]
    [InlineData(Gs1CompositeMode.CcB)]
    public void Explicit_capacity_failure_cancel_and_resource_failure_release_budget(Gs1CompositeMode mode)
    {
        using var scope = PrintResourceScope.Begin(128 * 1024);
        var secondary = mode == Gs1CompositeMode.CcA ? "(91)" + new string('A', 90)
            : "(91)" + new string('A', 90) + "(92)" + new string('A', 90) + "(93)" + new string('A', 90);
        var error = Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(mode, secondary: secondary)));
        Assert.Equal("secondaryData", error.ParamName);
        Assert.Contains(mode + " 容量", error.Message);
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Encode(Element(mode), new CancellationToken(true)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<PrintResourceLimitException>(() => Gs1CompositeEncoder.Rasterize(new PrintGs1CompositeElement(
            0, 0, BarcodeType.UccEan128, "(01)12345678901231", "(91)123", 10000, 100, mode)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.NotEmpty(Gs1CompositeEncoder.Rasterize(Element(mode)).Raster);
    }

    [Fact]
    public void Auto_remains_cc_c_while_explicit_modes_are_preserved()
    {
        var auto = Gs1CompositeEncoder.Encode(Element(Gs1CompositeMode.Auto));
        Assert.Equal(Gs1CompositeMode.CcC, auto.Mode);
        Assert.Equal(Gs1CompositeEncoder.Encode(Element(Gs1CompositeMode.CcC)).Matrix, auto.Matrix);
        foreach (var mode in new[] { Gs1CompositeMode.CcA, Gs1CompositeMode.CcB })
        {
            var actual = Gs1CompositeEncoder.Encode(Element(mode));
            Assert.Equal(mode, actual.Mode);
            Assert.NotEqual(auto.Matrix, actual.Matrix);
            Assert.Equal(actual.Matrix, Gs1CompositeEncoder.Encode(Element(mode, "[01]12345678901231")).Matrix);
        }
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_pixels_match_fixed_references_with_quiet_zones_and_rotation(RotationAngle rotation)
    {
        foreach (var vector in Gs1CompositeGs1128AbVectors.All.Take(2))
        {
            var mode = vector.Mode == "CC-A" ? Gs1CompositeMode.CcA : Gs1CompositeMode.CcB;
            var element = new PrintGs1CompositeElement(0, 0, BarcodeType.UccEan128, vector.PrimaryData,
                vector.SecondaryData, 7, 2, mode, rotation, new PrintQuietZone(3, 5, 7, 11));
            var image = Gs1CompositeEncoder.Rasterize(element);
            Assert.Equal(vector.Width * 2 + 10, image.Width);
            Assert.Equal((vector.Height - 2) * 4 + 2 + 7 + 16, image.Height);
            Assert.Equal((image.Width, image.Height), Gs1CompositeEncoder.Measure(element));
            using var output = PrintImageRotation.Prepare(image, CancellationToken.None);
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var expected = false;
                if (x >= 3 && x < image.Width - 7 && y >= 5 && y < image.Height - 11)
                {
                    var yy = y - 5;
                    var componentHeight = (vector.Height - 2) * 4;
                    var row = yy < componentHeight ? yy / 4 : yy < componentHeight + 2 ? vector.Height - 2 : vector.Height - 1;
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
    }

    internal static PrintGs1CompositeElement Element(Gs1CompositeMode mode = Gs1CompositeMode.CcA,
        string primary = "(01)12345678901231", string secondary = "(91)123456")
        => new PrintGs1CompositeElement(0, 0, BarcodeType.UccEan128, primary, secondary, 20, mode: mode);
}
