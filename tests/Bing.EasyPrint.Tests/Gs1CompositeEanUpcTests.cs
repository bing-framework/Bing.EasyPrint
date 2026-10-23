using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class Gs1CompositeEanUpcTests
{
    public static IEnumerable<object[]> Vectors() => Gs1CompositeEanUpcVectors.All().Concat(Gs1CompositeEanUpcAddonVectors.All());

    [Fact]
    public void Independent_decoder_recovers_all_two_digit_values_and_all_five_digit_parities()
    {
        var addons = Enumerable.Range(0, 100).Select(value => value.ToString("D2"))
            .Concat(Enumerable.Range(0, 10).Select(value => value.ToString("D5")));
        foreach (var addon in addons)
        {
            var symbol = Gs1CompositeEncoder.Encode(Element(BarcodeType.Ean13, "1234567890128+" + addon));
            var row = new ZXing.Common.BitArray(symbol.Matrix.Width + 40);
            for (var x = 0; x < symbol.Matrix.Width; x++) row[x + 20] = symbol.Matrix[x, symbol.Matrix.Height - 1];
            var decoded = new ZXing.OneD.EAN13Reader().decodeRow(0, row,
                new Dictionary<ZXing.DecodeHintType, object> { [ZXing.DecodeHintType.ALLOWED_EAN_EXTENSIONS] = new[] { addon.Length } });
            Assert.NotNull(decoded);
            Assert.Equal("1234567890128", decoded.Text);
            Assert.Equal(addon, decoded.ResultMetadata[ZXing.ResultMetadataType.UPC_EAN_EXTENSION]);
        }
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Full_matrix_and_dimensions_match_independent_reference(string name, BarcodeType type, int mode,
        string primary, string secondary, string[] rows)
    {
        Assert.StartsWith("zint-retail-", name);
        var element = Element(type, primary, secondary, (Gs1CompositeMode)mode);
        var symbol = Gs1CompositeEncoder.Encode(element);
        Assert.Equal((Gs1CompositeMode)mode, symbol.Mode);
        Assert.Equal(rows.Length, symbol.Matrix.Height);
        Assert.Equal(rows[0].Length, symbol.Matrix.Width);
        for (var row = 0; row < rows.Length; row++)
            Assert.Equal(rows[row], string.Concat(Enumerable.Range(0, symbol.Matrix.Width)
                .Select(column => symbol.Matrix[column, row] ? '1' : '0')));
        Assert.Equal(rows.Length - 4, symbol.ComponentRows);
        Assert.Equal((rows[0].Length * 2, 35 + (rows.Length - 1) * 4), Gs1CompositeEncoder.Measure(element));
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "1234567", "12345670")]
    [InlineData(BarcodeType.Ean13, "123456789012", "1234567890128")]
    [InlineData(BarcodeType.UpcA, "01234567890", "012345678905")]
    [InlineData(BarcodeType.UpcE, "123456", "01234565")]
    [InlineData(BarcodeType.UpcE, "0123456", "01234565")]
    [InlineData(BarcodeType.UpcE, "1234567", "12345670")]
    [InlineData(BarcodeType.UpcE, "123453", "01234531")]
    [InlineData(BarcodeType.UpcE, "123454", "01234543")]
    [InlineData(BarcodeType.UpcE, "123455", "01234558")]
    public void Missing_check_digit_is_completed_without_changing_symbol(BarcodeType type, string shortInput, string fullInput)
    {
        foreach (var suffix in new[] { "", "+12", "+51234" })
            Assert.Equal(Gs1CompositeEncoder.Rasterize(Element(type, fullInput + suffix)).Raster.ToArray(),
                Gs1CompositeEncoder.Rasterize(Element(type, shortInput + suffix)).Raster.ToArray());
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "12345670", 98, 125, 108, 135)]
    [InlineData(BarcodeType.Ean13, "1234567890128", 125, 152, 125, 152)]
    [InlineData(BarcodeType.UpcA, "012345678905", 127, 154, 127, 154)]
    [InlineData(BarcodeType.UpcE, "01234565", 81, 108, 81, 108)]
    public void Addons_keep_component_rows_and_height_and_use_exact_width(BarcodeType type, string primary,
        int a2, int a5, int b2, int b5)
    {
        foreach (var mode in new[] { Gs1CompositeMode.CcA, Gs1CompositeMode.CcB })
        foreach (var addon in new[] { "+12", "+51234" })
        {
            var original = Gs1CompositeEncoder.Encode(Element(type, primary, mode: mode));
            var symbol = Gs1CompositeEncoder.Encode(Element(type, primary + addon, mode: mode));
            Assert.Equal(original.ComponentRows, symbol.ComponentRows);
            Assert.Equal(original.Matrix.Height, symbol.Matrix.Height);
            var width = mode == Gs1CompositeMode.CcA ? (addon.Length == 3 ? a2 : a5) : (addon.Length == 3 ? b2 : b5);
            Assert.Equal(width, symbol.Matrix.Width);
            Assert.Equal(Gs1CompositeEncoder.Measure(Element(type, primary, mode: mode)).Height,
                Gs1CompositeEncoder.Measure(Element(type, primary + addon, mode: mode)).Height);
        }
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "12345670")]
    [InlineData(BarcodeType.Ean13, "1234567890128")]
    [InlineData(BarcodeType.UpcA, "012345678905")]
    [InlineData(BarcodeType.UpcE, "01234565")]
    public void Malformed_addons_and_main_codes_fail_without_poisoning_provider(BarcodeType type, string primary)
    {
        using var scope = PrintResourceScope.Begin(128 * 1024);
        foreach (var invalid in new[] { primary + "+", primary + "+1", primary + "+123", primary + "+1234",
            primary + "+123456", primary + "+１２", primary + "+ 1", primary + "+12 ", primary + "+1+",
            primary + "+12+12", primary + " +12", "+12", primary.Substring(0, primary.Length - 1) + "9+12" })
        {
            Assert.Equal("primaryData", Assert.Throws<ArgumentException>(() =>
                Gs1CompositeEncoder.Encode(Element(type, invalid))).ParamName);
            Assert.Equal(0, scope.CurrentBytes);
        }
        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.EscPos, PrintProtocol.Tspl, PrintProtocol.Zpl })
        {
            using (var request = PrintResourceScope.Begin(128 * 1024))
            {
                var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster);
                var bad = new PrintDocument();
                bad.AddPage(400, 200).Add(Element(type, primary + "+12"));
                bad.Pages[0].Add(Element(type, primary + "+123"));
                var documentBytes = request.CurrentBytes;
                Assert.Throws<PrintElementValidationException>(() => provider.Render(bad));
                Assert.Equal(documentBytes, request.CurrentBytes);
                var good = new PrintDocument();
                good.AddPage(400, 200).Add(Element(type, primary + "+12"));
                Assert.NotEmpty(Assert.Single(provider.Render(good)));
            }
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "12345671")]
    [InlineData(BarcodeType.Ean8, "123456")]
    [InlineData(BarcodeType.Ean8, "123456789")]
    [InlineData(BarcodeType.Ean8, "１２３４５６７")]
    [InlineData(BarcodeType.Ean8, "1234567+1")]
    [InlineData(BarcodeType.Ean13, "(01)12345678901231")]
    [InlineData(BarcodeType.Ean13, "1234567890129")]
    [InlineData(BarcodeType.UpcA, "012345678906")]
    [InlineData(BarcodeType.UpcE, "2123456")]
    [InlineData(BarcodeType.UpcE, "01234566")]
    [InlineData(BarcodeType.UpcE, "121453")]
    [InlineData(BarcodeType.UpcE, "123054")]
    [InlineData(BarcodeType.UpcE, "123405")]
    [InlineData(BarcodeType.UpcE, "12345")]
    [InlineData(BarcodeType.UpcE, "012345678")]
    [InlineData(BarcodeType.UpcE, "")]
    public void Invalid_primary_releases_budget_and_keeps_parameter(BarcodeType type, string input)
    {
        using var scope = PrintResourceScope.Begin(128 * 1024);
        Assert.Equal("primaryData", Assert.Throws<ArgumentException>(
            () => Gs1CompositeEncoder.Encode(Element(type, input))).ParamName);
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "1234567")]
    [InlineData(BarcodeType.Ean13, "123456789012")]
    [InlineData(BarcodeType.UpcA, "01234567890")]
    [InlineData(BarcodeType.UpcE, "123456")]
    [InlineData(BarcodeType.Ean8, "1234567+12")]
    [InlineData(BarcodeType.Ean13, "1234567890128+51234")]
    [InlineData(BarcodeType.UpcA, "01234567890+12")]
    [InlineData(BarcodeType.UpcE, "123456+51234")]
    public void Auto_fallback_capacity_cancellation_and_budget_recovery(BarcodeType type, string primary)
    {
        using var scope = PrintResourceScope.Begin(128 * 1024);
        Assert.Equal(Gs1CompositeMode.CcA, Gs1CompositeEncoder.Encode(Element(type, primary)).Mode);
        var retained = scope.CurrentBytes;
        var longer = "(91)" + new string('1', 70);
        Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(type, primary, longer, Gs1CompositeMode.CcA)));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Rasterize(Element(type, primary), new CancellationToken(true)));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Equal(Gs1CompositeMode.CcB, Gs1CompositeEncoder.Encode(Element(type, primary, longer)).Mode);
        Assert.Throws<NotSupportedException>(() => Element(type, primary, mode: Gs1CompositeMode.CcC));
        using (var limited = PrintResourceScope.Begin(256))
        {
            Assert.Throws<PrintResourceLimitException>(() => Gs1CompositeEncoder.Rasterize(Element(type, primary)));
            Assert.Equal(0, limited.CurrentBytes);
        }
        Assert.True(Gs1CompositeEncoder.Measure(Element(type, primary)).Width > 0);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Reference_pixels_keep_three_2X_separators_quiet_zones_and_rotation(RotationAngle rotation)
    {
        foreach (var vector in Vectors().Where(value => new[] { "zint-retail-8", "zint-retail-9", "zint-retail-28", "zint-retail-50" }.Contains(value[0]) || ((string)value[0]).StartsWith("zint-retail-addon-")))
        {
            var rows = (string[])vector[5];
            var element = new PrintGs1CompositeElement(0, 0, (BarcodeType)vector[1], (string)vector[3],
                (string)vector[4], 35, 2, (Gs1CompositeMode)vector[2], rotation, new PrintQuietZone(3, 5, 7, 11));
            var image = Gs1CompositeEncoder.Rasterize(element);
            using var rotated = PrintImageRotation.Prepare(image, default);
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var black = false;
                if (x >= 3 && x < image.Width - 7 && y >= 5 && y < image.Height - 11)
                {
                    var row = Math.Min((y - 5) / 4, rows.Length - 1);
                    black = rows[row][(x - 3) / 2] == '1';
                }
                var dx = x; var dy = y;
                if (rotation == RotationAngle.Rotate90) { dx = image.Height - 1 - y; dy = x; }
                if (rotation == RotationAngle.Rotate180) { dx = image.Width - 1 - x; dy = image.Height - 1 - y; }
                if (rotation == RotationAngle.Rotate270) { dx = y; dy = image.Width - 1 - x; }
                Assert.Equal(black, (rotated.Raster[dy * rotated.RowBytes + dx / 8] & (0x80 >> (dx % 8))) != 0);
            }
            foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.EscPos, PrintProtocol.Tspl, PrintProtocol.Zpl })
            {
                var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster);
                var document = new PrintDocument();
                document.AddPage(image.OutputWidth, image.OutputHeight).Add(element);
                var output = Assert.Single(provider.Render(document));
                var rasterDocument = new PrintDocument();
                rasterDocument.AddPage(image.OutputWidth, image.OutputHeight).Add(image);
                Assert.Equal(output, Assert.Single(provider.Render(rasterDocument)));
                var clipped = new PrintDocument();
                clipped.AddPage(image.OutputWidth - 1, image.OutputHeight).Add(element);
                Assert.Throws<PrintElementValidationException>(() => provider.Render(clipped));
                Assert.Equal(output, Assert.Single(provider.Render(document)));
            }
        }
    }

    internal static PrintGs1CompositeElement Element(BarcodeType type, string primary,
        string secondary = "(91)123456", Gs1CompositeMode mode = Gs1CompositeMode.Auto)
        => new PrintGs1CompositeElement(0, 0, type, primary, secondary, 35, 2, mode);
}
