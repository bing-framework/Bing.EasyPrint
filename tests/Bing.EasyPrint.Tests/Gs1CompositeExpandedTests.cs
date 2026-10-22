using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class Gs1CompositeExpandedTests
{
    public static IEnumerable<object[]> Vectors()
        => Gs1CompositeExpandedVectors.All.Select(vector => new object[] { vector.Name });

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Complete_matrix_matches_independent_expanded_reference(string name)
    {
        var vector = Gs1CompositeExpandedVectors.All.Single(value => value.Name == name);
        vector.Validate();
        var mode = vector.Mode == "CC-A" ? Gs1CompositeMode.CcA : Gs1CompositeMode.CcB;
        var symbol = Gs1CompositeEncoder.Encode(Element(vector.PrimaryData, vector.SecondaryData, mode));
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
        Assert.Equal(symbol.Matrix, Gs1CompositeEncoder.Encode(
            Element(vector.PrimaryData, vector.SecondaryData, Gs1CompositeMode.Auto)).Matrix);
        if (mode == Gs1CompositeMode.CcB)
            Assert.Contains("CcA 容量", Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(
                Element(vector.PrimaryData, vector.SecondaryData, Gs1CompositeMode.CcA))).Message);
    }

    [Theory]
    [InlineData("(01)12345678901231", "(01)12345678901231")]
    [InlineData("(01)09506000134352(10)LOT-A", "(01)09506000134352(10)LOT-A")]
    [InlineData("(10)123(21)456", "(10)123(21)456")]
    [InlineData("(20)12", "(20)12")]
    [InlineData("(01)93712345678904(3103)001234", "(01)93712345678904(3103)001234")]
    [InlineData("(01)93712345678904(3202)001234", "(01)93712345678904(3202)001234")]
    [InlineData("(01)93712345678904(3203)001234", "(01)93712345678904(3203)001234")]
    [InlineData("(01)93712345678904(3922)12345", "(01)93712345678904(3922)12345")]
    [InlineData("(01)93712345678904(3932)84012345", "(01)93712345678904(3932)84012345")]
    [InlineData("(01)93712345678904(3102)001234", "(01)93712345678904(3102)001234")]
    [InlineData("(01)93712345678904(3201)001234", "(01)93712345678904(3201)001234")]
    [InlineData("(01)93712345678904(3103)001234(11)270101", "(01)93712345678904(3103)001234(11)270101")]
    [InlineData("(01)93712345678904(3203)001234(11)270101", "(01)93712345678904(3203)001234(11)270101")]
    [InlineData("(01)93712345678904(3103)001234(13)270101", "(01)93712345678904(3103)001234(13)270101")]
    [InlineData("(01)93712345678904(3203)001234(13)270101", "(01)93712345678904(3203)001234(13)270101")]
    [InlineData("(01)93712345678904(3103)001234(15)270101", "(01)93712345678904(3103)001234(15)270101")]
    [InlineData("(01)93712345678904(3203)001234(15)270101", "(01)93712345678904(3203)001234(15)270101")]
    [InlineData("(01)93712345678904(3103)001234(17)270101", "(01)93712345678904(3103)001234(17)270101")]
    [InlineData("(01)93712345678904(3203)001234(17)270101", "(01)93712345678904(3203)001234(17)270101")]
    [InlineData("(01)12345678901231(3103)001234", "(01)12345678901231(3103)001234")]
    [InlineData("(01)93712345678904(3103)001234(10)A", "(01)93712345678904(3103)001234(10)A")]
    [InlineData("(01)93712345678904(3203)001234(17)270101(10)A", "(01)93712345678904(3203)001234(17)270101(10)A")]
    [InlineData("(01)93712345678904(3103)100000", "(01)93712345678904(3103)100000")]
    public void Independent_expanded_decoder_recovers_primary_for_all_methods_and_fallbacks(string primary, string expected)
    {
        var matrix = Gs1CompositeEncoder.Encode(Element(primary)).Matrix;
        var row = new ZXing.Common.BitArray(matrix.Width + 20);
        for (var x = 0; x < matrix.Width; x++) row[x + 10] = matrix[x, matrix.Height - 1];
        var result = new ZXing.OneD.RSS.Expanded.RSSExpandedReader().decodeRow(0, row, null);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Alphanumeric_fnc1_switches_to_numeric_and_retains_the_following_ai()
    {
        // ZXing 0.16.11 GeneralAppIdDecoder leaves Alpha mode active after FNC1 and
        // truncates this valid payload. Check the complete standard bit sequence instead.
        // linkage + method 2 + numeric "10" + Alpha + ABC + FNC1 + numeric "21", "12", final "3"/FNC1 pair.
        const string expected = "10000" + "0010011" + "0000" + "100000100001100010" + "01111" +
            "0011111" + "0010101" + "0110011";
        var method = typeof(EscPosRasterCanvas).GetMethod("EncodeGs1DataBarExpandedBinary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var binary = method.Invoke(null, new object[] { "10ABC\u001D21123", CancellationToken.None, true, false });
        var bits = (string)binary.GetType().GetProperty("Bits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(binary);
        Assert.Equal(expected, bits.Substring(0, 60));
        Assert.Equal(Gs1CompositeEncoder.Encode(Element("(10)ABC(21)123")).Matrix,
            Gs1CompositeEncoder.Encode(Element("[10]ABC[21]123")).Matrix);
        Assert.NotEqual(Gs1CompositeEncoder.Encode(Element("(10)ABC")).Matrix,
            Gs1CompositeEncoder.Encode(Element("(10)ABC(21)123")).Matrix);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0112345678901231")]
    [InlineData("(01)12345678901230")]
    [InlineData("(10)ABC[21]123")]
    [InlineData("(03)1")]
    [InlineData("(91)中文")]
    [InlineData("(91)ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    public void Invalid_primary_reports_field_and_releases_resources(string primary)
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        var error = Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(primary)));
        Assert.Equal("primaryData", error.ParamName);
        Assert.Equal(0, scope.CurrentBytes);
        Assert.NotNull(Gs1CompositeEncoder.Encode(Element()));
    }

    [Fact]
    public void Cc_c_pair_is_rejected_and_capacity_cancellation_and_budget_failures_are_recoverable()
    {
        Assert.Throws<NotSupportedException>(() => Element(mode: Gs1CompositeMode.CcC));
        using var scope = PrintResourceScope.Begin(128 * 1024);
        var longData = string.Concat(Enumerable.Range(91, 4).Select(ai => "(" + ai + ")" + new string('A', 90)));
        var error = Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(secondary: longData)));
        Assert.Equal("secondaryData", error.ParamName);
        Assert.Contains("CcB 容量", error.Message);
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Encode(Element(), new CancellationToken(true)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<PrintResourceLimitException>(() => Gs1CompositeEncoder.Rasterize(
            new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpanded, "(01)12345678901231", "(91)123", 10000, 100)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.NotEmpty(Gs1CompositeEncoder.Rasterize(Element()).Raster);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_pixels_match_reference_including_row_height_quiet_zones_and_rotation(RotationAngle rotation)
    {
        var vector = Gs1CompositeExpandedVectors.All[0];
        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpanded, vector.PrimaryData,
            vector.SecondaryData, 7, 2, Gs1CompositeMode.CcA, rotation, new PrintQuietZone(3, 5, 7, 11));
        var image = Gs1CompositeEncoder.Rasterize(element);
        Assert.Equal(vector.Width * 2 + 10, image.Width);
        Assert.Equal((vector.Height - 2) * 4 + 2 + 7 + 16, image.Height);
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

    internal static PrintGs1CompositeElement Element(string primary = "(01)09506000134352(10)LOT-A",
        string secondary = "(91)123456", Gs1CompositeMode mode = Gs1CompositeMode.Auto)
        => new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpanded, primary, secondary, 40, mode: mode);
}
