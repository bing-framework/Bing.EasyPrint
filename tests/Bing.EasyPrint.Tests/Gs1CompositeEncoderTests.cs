using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class Gs1CompositeEncoderTests
{
    public static IEnumerable<object[]> ReferenceVectors()
    {
        foreach (var vector in Gs1CompositeVectors.All) yield return new object[] { vector.Name };
    }

    [Theory]
    [MemberData(nameof(ReferenceVectors))]
    public void Complete_matrix_matches_fixed_independent_zint_vector(string name)
    {
        var vector = Find(name);
        vector.Validate();
        var element = FromVector(vector);
        var symbol = Gs1CompositeEncoder.Encode(element);
        Assert.Equal(vector.Width, symbol.Matrix.Width);
        Assert.Equal(vector.Height, symbol.Matrix.Height);
        Assert.Equal(element.Mode, symbol.Mode);
        for (var row = 0; row < vector.Height; row++)
        {
            var actual = new StringBuilder(vector.Width);
            for (var column = 0; column < vector.Width; column++)
                actual.Append(symbol.Matrix[column, row] ? '1' : '0');
            Assert.Equal(vector.Rows[row], actual.ToString());
        }
    }

    [Theory]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional, "12345678901231")]
    [InlineData(BarcodeType.Gs1DataBarLimited, "12345678901231")]
    public void Primary_gtin_forms_produce_the_same_linked_matrix(BarcodeType type, string gtin)
    {
        var reference = Gs1CompositeEncoder.Encode(Element(type, gtin.Substring(0, 13))).Matrix;
        foreach (var primary in new[] { gtin, "(01)" + gtin, "[01]" + gtin })
            Assert.Equal(reference, Gs1CompositeEncoder.Encode(Element(type, primary)).Matrix);
    }

    [Theory]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional, "0000000000000")]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional, "9999999999999")]
    [InlineData(BarcodeType.Gs1DataBarLimited, "0000000000000")]
    [InlineData(BarcodeType.Gs1DataBarLimited, "1999999999999")]
    public void Primary_extremes_are_valid(BarcodeType type, string primary)
        => Assert.True(Gs1CompositeEncoder.Encode(Element(type, primary)).ComponentRows > 0);

    [Theory]
    [InlineData("")]
    [InlineData("123456789012")]
    [InlineData("12345678901230")]
    [InlineData("(01)1234567890123")]
    [InlineData("(01)12345678901231(10)A")]
    [InlineData("123456789012A")]
    [InlineData("１２３４５６７８９０１２３")]
    public void Invalid_gtin_reports_primary_parameter_and_releases_resources(string primary)
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        var error = Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(
            Element(BarcodeType.Gs1DataBarOmnidirectional, primary)));
        Assert.Equal("primaryData", error.ParamName);
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Fact]
    public void Limited_rejects_gtin_out_of_its_numeric_range()
    {
        var error = Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(
            Element(BarcodeType.Gs1DataBarLimited, "2000000000000")));
        Assert.Equal("primaryData", error.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("911234")]
    [InlineData("(03)123")]
    [InlineData("(10)")]
    [InlineData("(10)ABCDEFGHIJKLMNOPQRSTU")]
    [InlineData("(10)中文")]
    [InlineData("(91)A\u001DB")]
    [InlineData("(91)A[21]B")]
    public void Invalid_secondary_is_rejected_by_shared_strict_ai_parser(string secondary)
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        var error = Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(
            Element(secondary: secondary)));
        Assert.Equal("secondaryData", error.ParamName);
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Theory]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional)]
    [InlineData(BarcodeType.Gs1DataBarLimited)]
    public void Auto_uses_cc_a_then_cc_b_and_explicit_modes_enforce_capacity(BarcodeType type)
    {
        var shortElement = Element(type, secondary: "(91)123456");
        Assert.Equal(Gs1CompositeMode.CcA, Gs1CompositeEncoder.Encode(shortElement).Mode);
        // A 57-digit AI 91 value exceeds both selected CC-A column capacities.
        var longer = "(91)" + new string('1', 57);
        var automatic = Gs1CompositeEncoder.Encode(Element(type, secondary: longer));
        var explicitB = Gs1CompositeEncoder.Encode(Element(type, secondary: longer, mode: Gs1CompositeMode.CcB));
        Assert.Equal(Gs1CompositeMode.CcB, automatic.Mode);
        Assert.Equal(explicitB.Matrix, automatic.Matrix);
        Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(
            Element(type, secondary: longer, mode: Gs1CompositeMode.CcA)));
        var overCapacity = "(91)" + new string('a', 90) + "(92)" + new string('b', 90) + "(93)" + new string('c', 90);
        foreach (var mode in new[] { Gs1CompositeMode.Auto, Gs1CompositeMode.CcA, Gs1CompositeMode.CcB })
            Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(type,
                secondary: overCapacity, mode: mode)));
    }

    [Theory]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional)]
    [InlineData(BarcodeType.Gs1DataBarLimited)]
    public void Cc_a_capacity_boundary_is_not_hidden_by_auto(BarcodeType type)
    {
        var lastAccepted = 0;
        for (var length = 1; length <= 90; length++)
        {
            var secondary = "(91)" + new string('1', length);
            try { Gs1CompositeEncoder.Encode(Element(type, secondary: secondary, mode: Gs1CompositeMode.CcA)); }
            catch (ArgumentException)
            {
                Assert.True(lastAccepted > 0);
                Assert.Equal(Gs1CompositeMode.CcB, Gs1CompositeEncoder.Encode(Element(type, secondary: secondary)).Mode);
                return;
            }
            lastAccepted = length;
        }
        Assert.True(false, "CC-A must reach a capacity boundary within 90 numeric characters.");
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_and_asymmetric_quiet_zones_rotate_as_one_exact_symbol(RotationAngle rotation)
    {
        var vector = Find("limited-cc-a");
        const int module = 2, barHeight = 7, left = 3, top = 5, right = 7, bottom = 11;
        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarLimited,
            vector.PrimaryData, vector.SecondaryData, barHeight, module, Gs1CompositeMode.CcA, rotation,
            new PrintQuietZone(left, top, right, bottom));
        var image = Gs1CompositeEncoder.Rasterize(element);
        Assert.Equal(vector.Width * module + left + right, image.Width);
        Assert.Equal((vector.Height - 2) * 2 * module + module + barHeight + top + bottom, image.Height);
        using var output = PrintImageRotation.Prepare(image, CancellationToken.None);
        var quarterTurn = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270;
        Assert.Equal(quarterTurn ? image.Height : image.Width, output.Width);
        Assert.Equal(quarterTurn ? image.Width : image.Height, output.Height);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var expected = false;
                if (x >= left && x < image.Width - right && y >= top && y < image.Height - bottom)
                {
                    var codeY = y - top;
                    var componentHeight = (vector.Height - 2) * 2 * module;
                    var row = codeY < componentHeight ? codeY / (2 * module)
                        : codeY < componentHeight + module ? vector.Height - 2 : vector.Height - 1;
                    expected = vector.Rows[row][(x - left) / module] == '1';
                }
                var dx = x;
                var dy = y;
                if (rotation == RotationAngle.Rotate90) { dx = image.Height - 1 - y; dy = x; }
                else if (rotation == RotationAngle.Rotate180) { dx = image.Width - 1 - x; dy = image.Height - 1 - y; }
                else if (rotation == RotationAngle.Rotate270) { dx = y; dy = image.Width - 1 - x; }
                Assert.Equal(expected, (output.Raster[dy * output.RowBytes + dx / 8] & (0x80 >> (dx & 7))) != 0);
            }
        }
        // Public snapshots cannot mutate the symbol retained by the image model.
        var snapshot = image.Raster;
        Array.Clear(snapshot, 0, snapshot.Length);
        Assert.Contains(image.RasterData, value => value != 0);
    }

    [Fact]
    public void Budget_rejection_and_invalid_encoding_allow_successful_retry()
    {
        using (var insufficient = PrintResourceScope.Begin(10))
        {
            Assert.Throws<PrintResourceLimitException>(() => Gs1CompositeEncoder.Encode(Element()));
            Assert.Equal(0, insufficient.CurrentBytes);
        }
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Rasterize(Element(secondary: "(03)1")));
        Assert.Equal(0, scope.CurrentBytes);
        var image = Gs1CompositeEncoder.Rasterize(Element());
        Assert.NotEmpty(image.Raster);
        Assert.True(scope.PeakBytes > scope.CurrentBytes);
    }

    [Fact]
    public void Raster_allocation_is_budgeted_before_large_output_and_failure_is_atomic()
    {
        using var scope = PrintResourceScope.Begin(128 * 1024);
        Assert.Throws<PrintResourceLimitException>(() => Gs1CompositeEncoder.Rasterize(
            new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarOmnidirectional,
                "1234567890123", "(91)123456", 10000, 100)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.NotEmpty(Gs1CompositeEncoder.Rasterize(Element()).Raster);
    }

    [Fact]
    public void Cancellation_does_not_retain_output_or_prevent_retry()
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Encode(Element(), cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Rasterize(Element(), cancellation.Token));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.NotEmpty(Gs1CompositeEncoder.Rasterize(Element()).Raster);
    }

    [Fact]
    public void Constructor_rejects_unsupported_or_invalid_contract_values()
    {
        Assert.Throws<NotSupportedException>(() => Element(BarcodeType.Gs1DataBarExpanded));
        Assert.Throws<ArgumentOutOfRangeException>(() => Element((BarcodeType)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Element(mode: (Gs1CompositeMode)4));
        Assert.Throws<ArgumentNullException>(() => Element(primary: null));
        Assert.Throws<ArgumentNullException>(() => Element(secondary: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintGs1CompositeElement(0, 0,
            BarcodeType.Gs1DataBarLimited, "1234567890123", "(91)1", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintGs1CompositeElement(0, 0,
            BarcodeType.Gs1DataBarLimited, "1234567890123", "(91)1", 10, moduleWidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintGs1CompositeElement(0, 0,
            BarcodeType.Gs1DataBarLimited, "1234567890123", "(91)1", 10, rotation: (RotationAngle)45));
    }

    [Fact]
    public void Page_owns_both_field_strings_and_independent_instance_path()
    {
        var element = Element();
        Assert.Equal(48L + 2L * (element.PrimaryData.Length + element.SecondaryData.Length), element.GetRetainedBytes(null));
        var document = new PrintDocument();
        var first = document.AddPage(500, 500).Add(element, "First");
        var second = document.AddPage(500, 500).Add(element, "Second");
        Assert.Null(element.SourcePath);
        Assert.Equal("First", first.Elements[0].SourcePath);
        Assert.Equal("Second", second.Elements[0].SourcePath);
        Assert.NotSame(first.Elements[0], second.Elements[0]);
    }

    private static PrintGs1CompositeElement Element(BarcodeType type = BarcodeType.Gs1DataBarOmnidirectional,
        string primary = "1234567890123", string secondary = "(91)123456", Gs1CompositeMode mode = Gs1CompositeMode.Auto)
        => new PrintGs1CompositeElement(0, 0, type, primary, secondary, 20, mode: mode);

    private static Gs1CompositeVector Find(string name)
    {
        foreach (var vector in Gs1CompositeVectors.All) if (vector.Name == name) return vector;
        throw new ArgumentException("Unknown vector", nameof(name));
    }

    private static PrintGs1CompositeElement FromVector(Gs1CompositeVector vector)
        => Element(vector.LinearComponent == "Limited" ? BarcodeType.Gs1DataBarLimited : BarcodeType.Gs1DataBarOmnidirectional,
            vector.PrimaryData, vector.SecondaryData, vector.Mode == "CC-A" ? Gs1CompositeMode.CcA : Gs1CompositeMode.CcB);
}
