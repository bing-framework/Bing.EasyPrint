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

public sealed class ExpandedStackedTests
{
    private const string Primary = "(01)09501101530003(17)140704(10)AB-123";
    public static IEnumerable<object[]> OrdinaryVectors() => ExpandedStackedVectors.Ordinary();
    public static IEnumerable<object[]> CompositeVectors() => ExpandedStackedVectors.Composite();

    [Theory]
    [MemberData(nameof(OrdinaryVectors))]
    public void Ordinary_matrix_matches_independent_reference(string name, string data, string[] rows)
    {
        Assert.False(string.IsNullOrEmpty(name));
        Assert.Equal(rows, EscPosRasterCanvas.EncodeGs1DataBarExpandedStackedPattern(data));
        Assert.Equal(rows[0].Length * 2, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpandedStacked, data, 2));
        var count = (rows.Length + 3) / 4;
        Assert.Equal(count * 68 + (count - 1) * 6, EscPosRasterCanvas.MeasureBarcodeHeight(
            BarcodeType.Gs1DataBarExpandedStacked, data, 68, 2, default));
    }

    [Theory]
    [MemberData(nameof(CompositeVectors))]
    public void Composite_matrix_matches_independent_reference(string name, int mode, string primary,
        string secondary, string[] rows)
    {
        Assert.False(string.IsNullOrEmpty(name));
        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked,
            primary, secondary, 68, 2, (Gs1CompositeMode)mode);
        var encoded = Gs1CompositeEncoder.Encode(element);
        Assert.Equal(rows.Length, encoded.Matrix.Height);
        Assert.Equal(rows[0].Length, encoded.Matrix.Width);
        for (var row = 0; row < rows.Length; row++)
            Assert.Equal(rows[row], string.Concat(Enumerable.Range(0, encoded.Matrix.Width)
                .Select(column => encoded.Matrix[column, row] ? '1' : '0')));
        Assert.Equal((Gs1CompositeMode)mode, encoded.Mode);
        var dataRows = (rows.Length - encoded.ComponentRows + 2) / 4;
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
        var rows = (string[])OrdinaryVectors().Single(value => (string)value[0] == "rss-24")[2];
        const int width = 204;
        const int height = 3 * 35 + 2 * 6;
        var quarter = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270;
        var canvas = new EscPosRasterCanvas(quarter ? height : width, quarter ? width : height);
        canvas.DrawBarcode(BarcodeType.Gs1DataBarExpandedStacked, 0, 0, Primary, 35, 2, false, 0,
            null, 12, (int)rotation);
        var bytes = canvas.ToBytes();
        var top = 0;
        for (var row = 0; row < rows.Length; row++)
        {
            var rowHeight = row % 4 == 0 ? 35 : 2;
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
            BarcodeType.Gs1DataBarExpandedStacked, Primary, 35, 2, rotation: rotation, humanReadable: false));
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
        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked,
            (string)vector[2], (string)vector[3], 35, 2, Gs1CompositeMode.CcA, rotation, new PrintQuietZone(3, 5, 7, 11));
        var image = Gs1CompositeEncoder.Rasterize(element);
        Assert.Equal(214, image.Width);
        Assert.Equal(3 * 4 + 2 + 3 * 35 + 2 * 6 + 16, image.Height);
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
                    var rowHeight = row < 3 ? 4 : row == 3 ? 2 : (row - 4) % 4 == 0 ? 35 : 2;
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
        Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(primary: "(01)09501101530004")));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Throws<ArgumentException>(() => Gs1CompositeEncoder.Encode(Element(primary: "(91)" + new string('A', 90))));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Encode(Element(), new CancellationToken(true)));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Throws<OperationCanceledException>(() => EscPosRasterCanvas.EncodeGs1DataBarExpandedStackedPattern(Primary, new CancellationToken(true)));
        Assert.Equal(retained, scope.CurrentBytes);
        Assert.Equal(Gs1CompositeMode.CcB, Gs1CompositeEncoder.Encode(Element(secondary: longSecondary)).Mode);
        Assert.Throws<NotSupportedException>(() => Element(Gs1CompositeMode.CcC));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintGs1CompositeElement(0, 0,
            BarcodeType.Gs1DataBarExpandedStacked, Primary, "(10)A", 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => EscPosRasterCanvas.MeasureBarcodeHeight(
            BarcodeType.Gs1DataBarExpandedStacked, Primary, 1, 2, default));
        using (var limited = PrintResourceScope.Begin(256))
        {
            Assert.Throws<PrintResourceLimitException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarExpandedStacked, Primary, 2));
            Assert.Equal(0, limited.CurrentBytes);
        }
        Assert.Equal(204, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Gs1DataBarExpandedStacked, Primary, 2));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Ordinary_template_roundtrip_reserves_full_multiline_height(int version)
    {
        var template = new BpTemplate { Version = version, Width = 300, Height = 300,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell {
                Elements = new List<BpElement> { new BpBarcodeElement { BarcodeType = BarcodeType.Gs1DataBarExpandedStacked,
                    Field = new BgField { Name = "Value" }, Height = 35, NarrowModuleWidth = 2, HumanReadable = false } }
            } } } } };
        var options = new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos, EscPosLayout = EscPosLayoutMode.Raster };
        var loader = new TemplateLoader();
        var renderer = new TemplateRenderer();
        var result = renderer.RenderWithLayout(template, new { Value = Primary }, options: options);
        var trace = Assert.Single(result.Pages[0].Elements.Where(value => value.Type == 4));
        Assert.Equal(117, trace.Height);
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
                capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarExpandedStacked));
            Assert.Equal(protocol == PrintProtocol.EscPos && layout == EscPosLayoutMode.Raster,
                capabilities.SupportsCommandBarcodeType(BarcodeType.Gs1DataBarExpandedStacked));
        }
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).BeginRasterPage(204, 117)
            .RasterBarcode(BarcodeType.Gs1DataBarExpandedStacked, 0, 0, Primary, 35, 2).EndRasterPage().Build().GetBytes();
        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    internal static PrintGs1CompositeElement Element(Gs1CompositeMode mode = Gs1CompositeMode.Auto,
        string secondary = "(10)LOT123", string primary = Primary)
        => new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked, primary, secondary, 35, 2, mode);
}
