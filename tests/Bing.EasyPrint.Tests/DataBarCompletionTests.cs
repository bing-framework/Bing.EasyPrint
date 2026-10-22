using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class DataBarCompletionTests
{
    private const string Primary = "0003456789012";
    public static IEnumerable<object[]> OrdinaryVectors() => DataBarCompletionVectors.Ordinary();
    public static IEnumerable<object[]> CompositeVectors() => DataBarCompletionVectors.Composite();

    [Theory]
    [MemberData(nameof(OrdinaryVectors))]
    public void Ordinary_matrix_matches_independent_reference(string name, string data, string[] rows)
    {
        Assert.False(string.IsNullOrEmpty(name));
        Assert.Equal(rows, EscPosRasterCanvas.EncodeGs1DataBarStackedOmnidirectionalPattern(data));
        Assert.Equal(rows[0].Length * 2, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarStackedOmnidirectional, data, 2));
        const int count = 2;
        Assert.Equal(count * 68 + (count - 1) * 6, EscPosRasterCanvas.MeasureBarcodeHeight(
            BarcodeType.Gs1DataBarStackedOmnidirectional, data, 68, 2, default));
    }

    [Theory]
    [MemberData(nameof(CompositeVectors))]
    public void Composite_matrix_matches_independent_reference(string name, int mode, string primary,
        string secondary, string[] rows)
    {
        Assert.False(string.IsNullOrEmpty(name));
        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarStackedOmnidirectional,
            primary, secondary, 68, 2, (Gs1CompositeMode)mode);
        var encoded = Gs1CompositeEncoder.Encode(element);
        Assert.Equal(rows.Length, encoded.Matrix.Height);
        Assert.Equal(rows[0].Length, encoded.Matrix.Width);
        for (var row = 0; row < rows.Length; row++)
            Assert.Equal(rows[row], string.Concat(Enumerable.Range(0, encoded.Matrix.Width)
                .Select(column => encoded.Matrix[column, row] ? '1' : '0')));
        Assert.Equal((Gs1CompositeMode)mode, encoded.Mode);
        const int dataRows = 2;
        var size = Gs1CompositeEncoder.Measure(element);
        Assert.Equal(dataRows * 68 + ((dataRows - 1) * 3 + 1 + encoded.ComponentRows * 2) * 2, size.Height);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Ordinary_raster_pixels_and_exact_bounds_follow_each_logical_row(RotationAngle rotation)
    {
        var rows = (string[])OrdinaryVectors().Single(value => (string)value[0] == "rss-7")[2];
        const int width = 100;
        const int height = 2 * 67 + 6;
        var quarter = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270;
        var canvas = new EscPosRasterCanvas(quarter ? height : width, quarter ? width : height);
        canvas.DrawBarcode(BarcodeType.Gs1DataBarStackedOmnidirectional, 0, 0, Primary, 67, 2, false, 0,
            null, 12, (int)rotation);
        var bytes = canvas.ToBytes();
        var top = 0;
        for (var row = 0; row < rows.Length; row++)
        {
            var rowHeight = row % 4 == 0 ? 67 : 2;
            for (var y = top; y < top + rowHeight; y++)
            for (var x = 0; x < width; x++)
            {
                var dx = x; var dy = y;
                if (rotation == RotationAngle.Rotate90) { dx = height - 1 - y; dy = x; }
                if (rotation == RotationAngle.Rotate180) { dx = width - 1 - x; dy = height - 1 - y; }
                if (rotation == RotationAngle.Rotate270) { dx = y; dy = width - 1 - x; }
                Assert.Equal(rows[row][x / 2] == '1', (bytes[dy * canvas.RowBytes + dx / 8] & (0x80 >> (dx % 8))) != 0);
            }
            top += rowHeight;
        }
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster);
        var document = new PrintDocument();
        document.AddPage(canvas.Width, canvas.Height).Add(new PrintBarcodeElement(0, 0,
            BarcodeType.Gs1DataBarStackedOmnidirectional, Primary, 67, 2, rotation: rotation, humanReadable: false));
        Assert.NotEmpty(provider.Render(document));
        var clipped = new PrintDocument();
        clipped.AddPage(canvas.Width, canvas.Height - 1).Add(document.Pages[0].Elements[0]);
        Assert.Throws<PrintElementValidationException>(() => provider.Render(clipped));
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Composite_raster_pixels_follow_reference_row_heights_and_quiet_zones(RotationAngle rotation)
    {
        var vector = CompositeVectors().First();
        var rows = (string[])vector[4];
        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarStackedOmnidirectional,
            (string)vector[2], (string)vector[3], 67, 2, Gs1CompositeMode.CcA, rotation, new PrintQuietZone(3, 5, 7, 11));
        var image = Gs1CompositeEncoder.Rasterize(element);
        Assert.Equal(122, image.Width);
        Assert.Equal(5 * 4 + 2 + 2 * 67 + 6 + 16, image.Height);
        using var rotated = PrintImageRotation.Prepare(image, default);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var black = false;
            if (x >= 3 && x < image.Width - 7 && y >= 5 && y < image.Height - 11)
            {
                var position = y - 5;
                for (var row = 0; row < rows.Length; row++)
                {
                    var rowHeight = row < 5 ? 4 : row == 5 ? 2 : (row - 6) % 4 == 0 ? 67 : 2;
                    if (position < rowHeight) { black = rows[row][(x - 3) / 2] == '1'; break; }
                    position -= rowHeight;
                }
            }
            var dx = x; var dy = y;
            if (rotation == RotationAngle.Rotate90) { dx = image.Height - 1 - y; dy = x; }
            if (rotation == RotationAngle.Rotate180) { dx = image.Width - 1 - x; dy = image.Height - 1 - y; }
            if (rotation == RotationAngle.Rotate270) { dx = y; dy = image.Width - 1 - x; }
            Assert.Equal(black, (rotated.Raster[dy * rotated.RowBytes + dx / 8] & (0x80 >> (dx % 8))) != 0);
        }
    }

    [Fact]
    public void Auto_fallback_explicit_capacity_input_and_cancel_failures_release_budget()
    {
        var longSecondary = (string)CompositeVectors().Last()[3];
        using var scope = PrintResourceScope.Begin(128 * 1024);
        Assert.Equal(Gs1CompositeMode.CcA, Gs1CompositeEncoder.Encode(Element()).Mode);
        // Successful encodes deliberately retain their returned matrix until the scope ends.
        var retained = scope.CurrentBytes;
        Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(Gs1CompositeMode.CcA, longSecondary)));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(primary: "(01)12345678901230")));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(primary: "(91)" + new string('A', 90))));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Encode(Element(), new CancellationToken(true)));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => EscPosRasterCanvas.EncodeGs1DataBarStackedOmnidirectionalPattern(Primary, new CancellationToken(true)));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Equal(Gs1CompositeMode.CcB, Gs1CompositeEncoder.Encode(Element(secondary: longSecondary)).Mode);
        Assert.Throws<NotSupportedException>(() => Element(Gs1CompositeMode.CcC));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintGs1CompositeElement(0, 0,
            BarcodeType.Gs1DataBarStackedOmnidirectional, Primary, "(10)A", 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => EscPosRasterCanvas.MeasureBarcodeHeight(
            BarcodeType.Gs1DataBarStackedOmnidirectional, Primary, 1, 2, default));
        using (var limited = PrintResourceScope.Begin(256))
        {
            Assert.Throws<PrintResourceLimitException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarStackedOmnidirectional, Primary, 2));
            Assert.Equal(0, limited.CurrentBytes);
        }
        Assert.Equal(100, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Gs1DataBarStackedOmnidirectional, Primary, 2));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Ordinary_template_roundtrip_reserves_full_multiline_height(int version)
    {
        var template = new BpTemplate { Version = version, Width = 300, Height = 300,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell {
                Elements = new List<BpElement> { new BpBarcodeElement { BarcodeType = BarcodeType.Gs1DataBarStackedOmnidirectional,
                    Field = new BgField { Name = "Value" }, Height = 67, NarrowModuleWidth = 2, HumanReadable = false } }
            } } } } };
        var options = new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos, EscPosLayout = EscPosLayoutMode.Raster };
        var loader = new TemplateLoader();
        var renderer = new TemplateRenderer();
        var result = renderer.RenderWithLayout(template, new { Value = Primary }, options: options);
        var trace = Assert.Single(result.Pages[0].Elements.Where(value => value.Type == 4));
        Assert.Equal(140, trace.Height);
        var restored = renderer.RenderWithLayout(loader.LoadJson(loader.SaveJson(template, options), options),
            new { Value = Primary }, options: options);
        Assert.Equal(result.Pages[0].Data, restored.Pages[0].Data);
    }

    [Fact]
    public void Ordinary_capability_is_raster_only_and_command_emits_image()
    {
        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.EscPos, PrintProtocol.Tspl, PrintProtocol.Zpl })
        foreach (var layout in new[] { EscPosLayoutMode.Native, EscPosLayoutMode.Raster })
        {
            var capabilities = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout).Capabilities;
            Assert.Equal(protocol == PrintProtocol.EscPos && layout == EscPosLayoutMode.Raster,
                capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarStackedOmnidirectional));
            Assert.Equal(protocol == PrintProtocol.EscPos && layout == EscPosLayoutMode.Raster,
                capabilities.SupportsCommandBarcodeType(BarcodeType.Gs1DataBarStackedOmnidirectional));
        }
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).BeginRasterPage(100, 140)
            .RasterBarcode(BarcodeType.Gs1DataBarStackedOmnidirectional, 0, 0, Primary, 67, 2).EndRasterPage().Build().GetBytes();
        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    internal static PrintGs1CompositeElement Element(Gs1CompositeMode mode = Gs1CompositeMode.Auto,
        string secondary = "(10)LOT123", string primary = Primary)
        => new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarStackedOmnidirectional, primary, secondary, 67, 2, mode);

    [Theory]
    [InlineData(Gs1CompositeMode.Auto)]
    [InlineData(Gs1CompositeMode.CcA)]
    [InlineData(Gs1CompositeMode.CcB)]
    public void Truncated_retains_omnidirectional_linkage_matrix_with_its_own_minimum_height(Gs1CompositeMode mode)
    {
        var truncated = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarTruncated,
            "12345678901231", "(91)123456", 26, 2, mode);
        var omni = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarOmnidirectional,
            "12345678901231", "(91)123456", 26, 2, mode);
        var expected = Gs1CompositeEncoder.Encode(omni);
        Assert.Equal(expected.Matrix, Gs1CompositeEncoder.Encode(truncated).Matrix);
        var raster = Gs1CompositeEncoder.Rasterize(truncated);
        Assert.Equal(200, raster.Width);
        Assert.Equal(26 + (2 * expected.ComponentRows + 1) * 2, raster.Height);
        Assert.Equal(Gs1CompositeEncoder.Rasterize(omni).Raster, raster.Raster);
        foreach (var vector in Gs1CompositeVectors.All.Where(value => value.LinearComponent == "Omnidirectional"))
        {
            var symbol = Gs1CompositeEncoder.Encode(new PrintGs1CompositeElement(0, 0,
                BarcodeType.Gs1DataBarTruncated, vector.PrimaryData, vector.SecondaryData, 26, 2,
                vector.Mode == "CC-A" ? Gs1CompositeMode.CcA : Gs1CompositeMode.CcB));
            for (var row = 0; row < vector.Height; row++)
                Assert.Equal(vector.Rows[row], string.Concat(Enumerable.Range(0, vector.Width)
                    .Select(column => symbol.Matrix[column, row] ? '1' : '0')));
        }
    }

    [Theory]
    [InlineData(BarcodeType.Gs1DataBarTruncated, 26)]
    [InlineData(BarcodeType.Gs1DataBarStackedOmnidirectional, 66)]
    public void New_composite_input_cancel_size_and_budget_failures_leave_no_reservations(BarcodeType type, int minimum)
    {
        PrintGs1CompositeElement Create(string primary = "12345678901231", int height = 68, int module = 2,
            Gs1CompositeMode mode = Gs1CompositeMode.Auto)
            => new PrintGs1CompositeElement(0, 0, type, primary, "(91)123", height, module, mode);
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(height: minimum - 1));
        Assert.Throws<NotSupportedException>(() => Create(mode: Gs1CompositeMode.CcC));
        Assert.NotEmpty(Gs1CompositeEncoder.Rasterize(Create(height: minimum)).Raster);
        using var scope = PrintResourceScope.Begin(128 * 1024);
        Assert.Equal("primaryData", Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Create("12345678901230"))).ParamName);
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Rasterize(Create(), new CancellationToken(true)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<PrintResourceLimitException>(() => Gs1CompositeEncoder.Rasterize(Create(height: 3300, module: 100)));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => Gs1CompositeEncoder.Measure(Create(height: int.MaxValue)));
        Assert.Equal(0, scope.CurrentBytes);
        var expected = Gs1CompositeEncoder.Encode(Create()).Matrix;
        foreach (var primary in new[] { "1234567890123", "(01)12345678901231", "[01]12345678901231" })
            Assert.Equal(expected, Gs1CompositeEncoder.Encode(Create(primary)).Matrix);
    }

    [Fact]
    public void Ordinary_per_row_minimum_and_hri_are_explicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EscPosRasterCanvas.MeasureBarcodeHeight(
            BarcodeType.Gs1DataBarStackedOmnidirectional, Primary, 65, 2, default));
        Assert.Equal(138, EscPosRasterCanvas.MeasureBarcodeHeight(
            BarcodeType.Gs1DataBarStackedOmnidirectional, Primary, 66, 2, default));
        Assert.Throws<OverflowException>(() => EscPosRasterCanvas.MeasureBarcodeHeight(
            BarcodeType.Gs1DataBarStackedOmnidirectional, Primary, int.MaxValue, 2, default));
        Assert.Throws<ArgumentException>(() => EscPosRasterCanvas.EncodeGs1DataBarStackedOmnidirectionalPattern("12345678901231"));
        Assert.Throws<ArgumentException>(() => EscPosRasterCanvas.EncodeGs1DataBarStackedOmnidirectionalPattern("123456789012A"));
        Assert.Equal("(01)12345678901231", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarStackedOmnidirectional, "1234567890123"));
    }
}
