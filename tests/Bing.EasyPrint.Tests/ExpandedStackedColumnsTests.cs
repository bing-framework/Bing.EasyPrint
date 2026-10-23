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
using Newtonsoft.Json;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ExpandedStackedColumnsTests
{
    internal const string Primary = "[91]1234567890123456789012345678901234";
    public static IEnumerable<object[]> OrdinaryVectors() => ExpandedStackedColumnVectors.Ordinary();
    public static IEnumerable<object[]> CompositeVectors() => ExpandedStackedCompositeColumnVectors.All();

    [Theory]
    [MemberData(nameof(OrdinaryVectors))]
    public void All_modules_match_independent_Zint_matrices(string name, int columns, string data, string[] expected)
    {
        Assert.NotEmpty(name);
        // These six upstream cases have an additional AI 17 outside our bracket-parser subset.
        // All their fields are fixed length; use the existing explicitly normalized raw input path.
        if (data.Contains("[3203]001234[17]010203")) data = data.Replace("[", "").Replace("]", "");
        var actual = EscPosRasterCanvas.EncodeGs1DataBarExpandedStackedPattern(data, columns: columns);
        Assert.Equal(expected, actual);
        var size = EscPosRasterCanvas.MeasureExpandedStackedSize(data, 35, 2, columns);
        Assert.Equal(expected[0].Length * 2, size.Width);
        var rows = (expected.Length + 3) / 4;
        Assert.Equal(rows * 35 + (rows - 1) * 6, size.Height);
    }

    [Theory]
    [MemberData(nameof(CompositeVectors))]
    public void Composite_full_matrix_matches_pinned_external_encoder(string name, int columns, int mode,
        string primary, string secondary, string[] expected)
    {
        Assert.NotEmpty(name);
        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked,
            primary, secondary, 35, 2, (Gs1CompositeMode)mode).WithExpandedStackedColumns(columns);
        var encoded = Gs1CompositeEncoder.Encode(element);
        Assert.Equal(expected.Length, encoded.Matrix.Height);
        Assert.Equal(expected[0].Length, encoded.Matrix.Width);
        for (var row = 0; row < expected.Length; row++)
            Assert.Equal(expected[row], string.Concat(Enumerable.Range(0, encoded.Matrix.Width)
                .Select(column => encoded.Matrix[column, row] ? '1' : '0')));
    }

    [Theory]
    [InlineData(1, RotationAngle.None)]
    [InlineData(3, RotationAngle.Rotate90)]
    [InlineData(4, RotationAngle.Rotate180)]
    [InlineData(11, RotationAngle.Rotate270)]
    public void Raster_pixels_provider_command_and_exact_bounds_use_requested_columns(int columns, RotationAngle rotation)
    {
        var size = EscPosRasterCanvas.MeasureExpandedStackedSize(Primary, 35, 2, columns);
        var quarter = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270;
        var width = quarter ? size.Height : size.Width;
        var height = quarter ? size.Width : size.Height;
        var source = new EscPosRasterCanvas(size.Width, size.Height);
        source.DrawExpandedStackedBarcode(0, 0, Primary, 35, columns);
        var rotated = new EscPosRasterCanvas(width, height);
        rotated.DrawExpandedStackedBarcode(0, 0, Primary, 35, columns, rotation: (int)rotation);
        var before = source.ToBytes();
        var after = rotated.ToBytes();
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            var dx = rotation == RotationAngle.Rotate90 ? size.Height - 1 - y
                : rotation == RotationAngle.Rotate180 ? size.Width - 1 - x
                : rotation == RotationAngle.Rotate270 ? y : x;
            var dy = rotation == RotationAngle.Rotate90 ? x
                : rotation == RotationAngle.Rotate180 ? size.Height - 1 - y
                : rotation == RotationAngle.Rotate270 ? size.Width - 1 - x : y;
            Assert.Equal((before[y * source.RowBytes + x / 8] & (0x80 >> (x % 8))) != 0,
                (after[dy * rotated.RowBytes + dx / 8] & (0x80 >> (dx % 8))) != 0);
        }
        var element = new PrintBarcodeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked, Primary, 35,
            rotation: rotation, humanReadable: false).WithExpandedStackedColumns(columns);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster);
        var exact = Document(element, width, height);
        var output = Assert.Single(provider.Render(exact));
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Initialize().BeginRasterPage(width, height)
            .RasterExpandedStackedBarcode(0, 0, Primary, 35, columns, rotation: (int)rotation).EndRasterPage().Build().GetBytes();
        Assert.Equal(command, output);
        Assert.Throws<PrintElementValidationException>(() => provider.Render(Document(element, width - 1, height)));
        Assert.Throws<PrintElementValidationException>(() => provider.Render(Document(element, width, height - 1)));
    }

    [Fact]
    public void Default_two_columns_remains_byte_compatible_and_copy_is_immutable()
    {
        var old = new PrintBarcodeElement(3, 5, BarcodeType.Gs1DataBarExpandedStacked, Primary, 35,
            new PrintQuietZone(1, 2, 3, 4), humanReadable: false, reservedWidth: 204);
        old.SourcePath = "Rows[0].Cells[0].Elements[0]";
        var copy = old.WithExpandedStackedColumns(2);
        Assert.Null(old.ExpandedStackedColumns);
        Assert.Equal(2, copy.ExpandedStackedColumns);
        Assert.Same(old.QuietZone, copy.QuietZone);
        Assert.Equal(old.Width, copy.Width);
        Assert.Equal(old.X, copy.X);
        Assert.Equal(old.Y, copy.Y);
        Assert.Equal(old.SourcePath, copy.SourcePath);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster);
        Assert.Equal(Assert.Single(provider.Render(Document(old, 500, 500))), Assert.Single(provider.Render(Document(copy, 500, 500))));
        var composite = Composite();
        Assert.Null(composite.ExpandedStackedColumns);
        Assert.Equal(Gs1CompositeEncoder.Rasterize(composite).Raster,
            Gs1CompositeEncoder.Rasterize(composite.WithExpandedStackedColumns(2)).Raster);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(12)]
    [InlineData(int.MaxValue)]
    public void Invalid_columns_fail_before_encoding(int columns)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EscPosRasterCanvas.MeasureExpandedStackedSize(Primary, 35, 2, columns));
        Assert.Throws<ArgumentOutOfRangeException>(() => Composite().WithExpandedStackedColumns(columns));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintBarcodeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked,
            Primary, 35).WithExpandedStackedColumns(columns));
    }

    [Fact]
    public void Columns_are_not_silently_accepted_for_other_types_or_single_column_composite()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Composite().WithExpandedStackedColumns(1));
        Assert.Throws<NotSupportedException>(() => new PrintBarcodeElement(0, 0, BarcodeType.Code128, "123", 35).WithExpandedStackedColumns(2));
        Assert.Throws<NotSupportedException>(() => new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpanded,
            Primary, "[20]12", 35).WithExpandedStackedColumns(2));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(3, 11)]
    public void Ordinary_template_json_roundtrip_preserves_dimensions_and_bytes(int version, int columns)
    {
        var element = new BpBarcodeElement { BarcodeType = BarcodeType.Gs1DataBarExpandedStacked, Height = 35,
            NarrowModuleWidth = 2, HumanReadable = false, QuietZone = new PrintQuietZone(0, 0, 0, 0),
            ExpandedStackedColumns = columns, Field = new BgField { Value = Primary } };
        var template = Template(element, version);
        var options = Options(PrintProtocol.EscPos);
        var first = new TemplateRenderer().RenderWithLayout(template, new { }, options: options);
        var size = EscPosRasterCanvas.MeasureExpandedStackedSize(Primary, 35, 2, columns);
        var trace = Assert.Single(first.Pages[0].Elements.Where(item => item.Type == 4));
        Assert.Equal(size.Width, trace.Width);
        Assert.Equal(size.Height, trace.Height);
        var loader = new TemplateLoader();
        var restored = loader.LoadJson(loader.SaveJson(template, options), options);
        Assert.Equal(columns, ((BpBarcodeElement)restored.Rows[0].Cells[0].Elements[0]).ExpandedStackedColumns);
        Assert.Equal(first.Pages[0].Data, new TemplateRenderer().RenderWithLayout(restored, new { }, options: options).Pages[0].Data);
    }

    public static IEnumerable<object[]> CompositeCases()
    {
        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.EscPos, PrintProtocol.Tspl, PrintProtocol.Zpl })
        foreach (var columns in new[] { 2, 3, 4, 11 })
        foreach (var rotation in new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 })
            yield return new object[] { protocol, columns, rotation };
    }

    [Theory]
    [MemberData(nameof(CompositeCases))]
    public void Composite_templates_all_protocols_rotations_and_quiet_zones_use_columns(PrintProtocol protocol, int columns, RotationAngle rotation)
    {
        var zone = new PrintQuietZone(3, 5, 7, 11);
        var model = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked, Primary, "[20]12",
            35, 2, Gs1CompositeMode.CcA, rotation, zone).WithExpandedStackedColumns(columns);
        var size = Gs1CompositeEncoder.Measure(model);
        var quarter = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270;
        var width = quarter ? size.Height : size.Width;
        var height = quarter ? size.Width : size.Height;
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster);
        Assert.Equal(Assert.Single(provider.Render(Document(Gs1CompositeEncoder.Rasterize(model), width, height))),
            Assert.Single(provider.Render(Document(model, width, height))));
        Assert.Throws<PrintElementValidationException>(() => provider.Render(Document(model, width - 1, height)));
        Assert.Throws<PrintElementValidationException>(() => provider.Render(Document(model, width, height - 1)));
        var template = Template(new BpGs1CompositeElement { LinearType = BarcodeType.Gs1DataBarExpandedStacked,
            Mode = Gs1CompositeMode.CcA, LinearBarHeight = 35, ModuleWidth = 2, Rotation = rotation, QuietZone = zone,
            ExpandedStackedColumns = columns, Field = new BgField { Value = Primary }, SecondaryField = new BgField { Value = "[20]12" } }, 3);
        var options = Options(protocol);
        var loader = new TemplateLoader();
        var first = new TemplateRenderer().RenderWithLayout(template, new { }, options: options);
        var trace = Assert.Single(first.Pages[0].Elements.Where(item => item.Type == 11));
        Assert.Equal(width, trace.Width);
        Assert.Equal(height, trace.Height);
        var restored = loader.LoadJson(loader.SaveJson(template, options), options);
        Assert.Equal(columns, ((BpGs1CompositeElement)restored.Rows[0].Cells[0].Elements[0]).ExpandedStackedColumns);
        Assert.Equal(first.Pages[0].Data, new TemplateRenderer().RenderWithLayout(restored, new { }, options: options).Pages[0].Data);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("\"3\"")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("0")]
    [InlineData("12")]
    public void Json_columns_reject_coercion_and_invalid_ranges(string value)
    {
        foreach (var name in new[] { "ExpandedStackedColumns", "expandedStackedColumns" })
        {
            var json = "{\"Version\":2,\"Width\":500,\"Height\":500,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":4,\"BarcodeType\":31,\"Height\":35,\"" +
                name + "\":" + value + ",\"Field\":{\"Value\":\"[91]123\"}}]}]}]}";
            var error = Assert.Throws<JsonSerializationException>(() => new TemplateLoader().LoadJson(json, Options(PrintProtocol.EscPos)));
            Assert.Contains("ExpandedStackedColumns", error.Message);
        }
    }

    [Fact]
    public void Json_null_preserves_default_and_other_symbologies_cannot_silently_ignore_columns()
    {
        var loader = new TemplateLoader();
        var options = Options(PrintProtocol.EscPos);
        var ordinary = new BpBarcodeElement { BarcodeType = BarcodeType.Gs1DataBarExpandedStacked, Height = 35,
            NarrowModuleWidth = 2, HumanReadable = false, Field = new BgField { Value = Primary } };
        var json = loader.SaveJson(Template(ordinary, 2), options);
        var withNull = Newtonsoft.Json.Linq.JObject.Parse(json);
        var node = withNull["Rows"][0]["Cells"][0]["Elements"][0];
        node["ExpandedStackedColumns"] = null;
        var restored = loader.LoadJson(withNull.ToString(), options);
        Assert.Null(((BpBarcodeElement)restored.Rows[0].Cells[0].Elements[0]).ExpandedStackedColumns);
        var renderer = new TemplateRenderer();
        Assert.Equal(renderer.Render(Template(ordinary, 2), new { }, options: options).Pages[0].Data,
            renderer.Render(restored, new { }, options: options).Pages[0].Data);
        node["BarcodeType"] = (int)BarcodeType.Code128;
        node["ExpandedStackedColumns"] = 2;
        Assert.Throws<NotSupportedException>(() => loader.LoadJson(withNull.ToString(), options));
        node["BarcodeType"] = 31;
        node["expandedStackedColumns"] = 3;
        Assert.Throws<JsonSerializationException>(() => loader.LoadJson(withNull.ToString(), options));
    }

    [Fact]
    public void Custom_column_quiet_zone_rejects_neighbor_ink_and_hri_is_below_all_rows()
    {
        var size = EscPosRasterCanvas.MeasureExpandedStackedSize(Primary, 35, 2, 3);
        var model = new PrintBarcodeElement(10, 10, BarcodeType.Gs1DataBarExpandedStacked, Primary, 35,
            new PrintQuietZone(10, 10, 10, 10), humanReadable: false).WithExpandedStackedColumns(3);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster);
        var document = Document(model, size.Width + 20, size.Height + 20);
        Assert.NotEmpty(Assert.Single(provider.Render(document)));
        document.Pages[0].Add(PrintRectangleElement.CreateFilled(9, 12, 1, 1));
        Assert.Throws<PrintElementValidationException>(() => provider.Render(document));
        var hri = new PrintBarcodeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked, Primary, 35,
            humanReadable: true, humanReadableReserveHeight: 20).WithExpandedStackedColumns(3);
        Assert.NotEmpty(Assert.Single(provider.Render(Document(hri, size.Width, size.Height + 20))));
        Assert.Throws<PrintElementValidationException>(() => provider.Render(Document(hri, size.Width, size.Height + 19)));
    }

    [Fact]
    public void Cancellation_budget_failure_and_drawing_error_release_resources_and_allow_recovery()
    {
        using var scope = PrintResourceScope.Begin(4 * 1024 * 1024);
        using (var child = PrintResourceScope.BeginTemporary())
        {
            var canvas = new EscPosRasterCanvas(400, 400);
            canvas.DrawRectangle(0, 0, 5, 5);
            var snapshot = canvas.ToBytes();
            var retained = child.CurrentBytes;
            Assert.Throws<ArgumentException>(() => canvas.DrawExpandedStackedBarcode(0, 0, "[91]" + new string('A', 100), 35, 3));
            Assert.Equal(retained, child.CurrentBytes);
            Assert.Equal(snapshot, canvas.ToBytes());
            var current = child.CurrentBytes;
            Assert.Throws<OperationCanceledException>(() => EscPosRasterCanvas.MeasureExpandedStackedSize(Primary, 35, 2, 11, new CancellationToken(true)));
            Assert.Equal(current, child.CurrentBytes);
            var model = Composite().WithExpandedStackedColumns(3);
            current = child.CurrentBytes;
            Assert.Throws<OperationCanceledException>(() => Gs1CompositeEncoder.Encode(model, new CancellationToken(true)));
            Assert.Equal(current, child.CurrentBytes);
            using (var tiny = PrintResourceScope.Begin(256))
            {
                Assert.Throws<PrintResourceLimitException>(() => EscPosRasterCanvas.MeasureExpandedStackedSize(Primary, 35, 2, 1));
                Assert.Equal(0, tiny.CurrentBytes);
            }
            canvas.DrawExpandedStackedBarcode(0, 0, Primary, 35, 3);
            Assert.NotEqual(snapshot, canvas.ToBytes());
        }
        Assert.Equal(0, scope.CurrentBytes);
    }

    private static PrintGs1CompositeElement Composite() => new PrintGs1CompositeElement(0, 0,
        BarcodeType.Gs1DataBarExpandedStacked, Primary, "[20]12", 35);
    private static PrintDocument Document(PrintElement element, int width, int height)
    {
        var document = new PrintDocument();
        document.AddPage(width, height).Add(element);
        return document;
    }
    private static BpTemplate Template(BpElement element, int version) => new BpTemplate { Version = version,
        Width = 1200, Height = 1200, Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
            new BpCell { Elements = new List<BpElement> { element } } } } } };
    private static TemplateRenderOptions Options(PrintProtocol protocol) => new TemplateRenderOptions {
        Protocol = protocol, Encoding = Encoding.ASCII, EscPosLayout = EscPosLayoutMode.Raster,
        FeedLines = 0, CutAfterPrint = false };
}
