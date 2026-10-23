using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ExpandedStacked 最大数据行数与固定 Zint 参考矩阵。</summary>
public sealed class ExpandedStackedRowsTests
{
    internal const string Primary = "[91]1234567890123456789012345678901234";

    public static IEnumerable<object[]> Vectors() => ExpandedStackedRowVectors.All();

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Every_max_row_matrix_matches_pinned_external_encoder(string name, int maximumRows, int mode,
        string primary, string secondary, string[] expected)
    {
        Assert.NotEmpty(name);
        if (mode == 0)
        {
            var actual = EscPosRasterCanvas.EncodeGs1DataBarExpandedStackedPattern(primary,
                maximumRows: maximumRows);
            Assert.Equal(expected, actual);
            var dataRows = (actual.Length + 3) / 4;
            Assert.InRange(dataRows, 1, maximumRows);
            Assert.Equal(4 * dataRows - 3, actual.Length);
            var size = EscPosRasterCanvas.MeasureExpandedStackedSizeByRows(primary, 35, 2, maximumRows);
            Assert.Equal(expected[0].Length * 2, size.Width);
            Assert.Equal(dataRows * 35 + (dataRows - 1) * 6, size.Height);
            return;
        }

        var element = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked,
            primary, secondary, 35, 2, (Gs1CompositeMode)mode).WithExpandedStackedRows(maximumRows);
        var encoded = Gs1CompositeEncoder.Encode(element);
        Assert.Equal(expected.Length, encoded.Matrix.Height);
        Assert.Equal(expected[0].Length, encoded.Matrix.Width);
        for (var row = 0; row < expected.Length; row++)
            Assert.Equal(expected[row], string.Concat(Enumerable.Range(0, encoded.Matrix.Width)
                .Select(column => encoded.Matrix[column, row] ? '1' : '0')));
        Assert.InRange(encoded.ExpandedStackedRows, 1, maximumRows);
    }

    [Fact]
    public void Default_layout_remains_the_two_column_output()
    {
        var ordinary = new PrintBarcodeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked, Primary, 35);
        Assert.Null(ordinary.ExpandedStackedRows);
        Assert.Equal(EscPosRasterCanvas.EncodeGs1DataBarExpandedStackedPattern(Primary),
            EscPosRasterCanvas.EncodeGs1DataBarExpandedStackedPattern(Primary, columns: 2));
        Assert.Equal(EscPosRasterCanvas.EncodeGs1DataBarExpandedStackedPattern(Primary),
            EscPosRasterCanvas.EncodeGs1DataBarExpandedStackedPattern(Primary, maximumRows: 11));
    }

    [Fact]
    public void Rows_copy_is_immutable_and_preserves_other_configuration()
    {
        var zone = new PrintQuietZone(1, 2, 3, 4);
        var original = new PrintBarcodeElement(3, 5, BarcodeType.Gs1DataBarExpandedStacked, Primary, 35, zone,
            humanReadable: false, reservedWidth: 204) { SourcePath = "Rows[0].Cells[0].Elements[0]" };
        var copy = original.WithExpandedStackedRows(4);
        Assert.Null(original.ExpandedStackedRows);
        Assert.Equal(4, copy.ExpandedStackedRows);
        Assert.Same(zone, copy.QuietZone);
        Assert.Equal(original.Width, copy.Width);
        Assert.Equal(original.SourcePath, copy.SourcePath);
        var composite = Composite();
        var compositeCopy = composite.WithExpandedStackedRows(4);
        Assert.Null(composite.ExpandedStackedRows);
        Assert.Equal(4, compositeCopy.ExpandedStackedRows);
        Assert.Equal(composite.SourcePath, compositeCopy.SourcePath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(int.MaxValue)]
    public void Invalid_max_rows_fail_before_encoding(int rows)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EscPosRasterCanvas.MeasureExpandedStackedSizeByRows(Primary, 35, 2, rows));
        Assert.Throws<ArgumentOutOfRangeException>(() => Composite().WithExpandedStackedRows(rows));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintBarcodeElement(0, 0,
            BarcodeType.Gs1DataBarExpandedStacked, Primary, 35).WithExpandedStackedRows(rows));
    }

    [Fact]
    public void Columns_and_rows_are_mutually_exclusive_and_never_silently_ignored()
    {
        var ordinary = new PrintBarcodeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked, Primary, 35);
        Assert.Throws<InvalidOperationException>(() => ordinary.WithExpandedStackedColumns(3).WithExpandedStackedRows(3));
        Assert.Throws<InvalidOperationException>(() => ordinary.WithExpandedStackedRows(3).WithExpandedStackedColumns(3));
        var composite = Composite();
        Assert.Throws<InvalidOperationException>(() => composite.WithExpandedStackedColumns(3).WithExpandedStackedRows(3));
        Assert.Throws<InvalidOperationException>(() => composite.WithExpandedStackedRows(3).WithExpandedStackedColumns(3));
        Assert.Throws<NotSupportedException>(() => new PrintBarcodeElement(0, 0, BarcodeType.Code128, "123", 35)
            .WithExpandedStackedRows(3));
        Assert.Throws<NotSupportedException>(() => new PrintGs1CompositeElement(0, 0,
            BarcodeType.Gs1DataBarExpanded, Primary, "[20]12", 35).WithExpandedStackedRows(3));
    }

    [Fact]
    public void Over_capacity_data_is_rejected_instead_of_truncated_or_expanded()
    {
        var tooLong = "[91]" + new string('A', 80);
        Assert.ThrowsAny<ArgumentException>(() =>
            EscPosRasterCanvas.MeasureExpandedStackedSizeByRows(tooLong, 35, 2, 2));
        Assert.Throws<OperationCanceledException>(() =>
            EscPosRasterCanvas.MeasureExpandedStackedSizeByRows(Primary, 35, 2, 2,
                new System.Threading.CancellationToken(true)));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(3, 11)]
    public void Template_json_roundtrip_preserves_rows_and_rendered_bytes(int version, int maximumRows)
    {
        var element = new BpBarcodeElement
        {
            BarcodeType = BarcodeType.Gs1DataBarExpandedStacked, Height = 35, NarrowModuleWidth = 2,
            HumanReadable = false, QuietZone = new PrintQuietZone(0, 0, 0, 0),
            ExpandedStackedRows = maximumRows, Field = new BgField { Value = Primary }
        };
        var template = Template(element, version);
        var options = Options(PrintProtocol.EscPos);
        var renderer = new TemplateRenderer();
        var first = renderer.RenderWithLayout(template, new { }, options: options);
        var size = EscPosRasterCanvas.MeasureExpandedStackedSizeByRows(Primary, 35, 2, maximumRows);
        var trace = Assert.Single(first.Pages[0].Elements.Where(item => item.Type == 4));
        Assert.Equal(size.Width, trace.Width);
        Assert.Equal(size.Height, trace.Height);
        var loader = new TemplateLoader();
        var restored = loader.LoadJson(loader.SaveJson(template, options), options);
        Assert.Equal(maximumRows, ((BpBarcodeElement)restored.Rows[0].Cells[0].Elements[0]).ExpandedStackedRows);
        Assert.Equal(first.Pages[0].Data,
            renderer.RenderWithLayout(restored, new { }, options: options).Pages[0].Data);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.5")]
    [InlineData("\"3\"")]
    [InlineData("true")]
    [InlineData("12")]
    [InlineData("{}")]
    public void Json_rows_reject_coercion_and_invalid_ranges(string value)
    {
        foreach (var name in new[] { "ExpandedStackedRows", "expandedStackedRows" })
        {
            var json = "{\"Version\":2,\"Width\":500,\"Height\":500,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":4,\"BarcodeType\":31,\"Height\":35,\"" +
                name + "\":" + value + ",\"Field\":{\"Value\":\"[91]123\"}}]}]}]}";
            var error = Assert.Throws<JsonSerializationException>(() =>
                new TemplateLoader().LoadJson(json, Options(PrintProtocol.EscPos)));
            Assert.Contains("ExpandedStackedRows", error.Message);
        }
    }

    [Fact]
    public void Json_rejects_columns_and_rows_together_or_rows_on_other_types()
    {
        var both = "{\"Version\":2,\"Width\":500,\"Height\":500,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":4,\"BarcodeType\":31,\"Height\":35,\"ExpandedStackedColumns\":3,\"ExpandedStackedRows\":3,\"Field\":{\"Value\":\"[91]123\"}}]}]}]}";
        var error = Assert.Throws<JsonSerializationException>(() =>
            new TemplateLoader().LoadJson(both, Options(PrintProtocol.EscPos)));
        Assert.Contains("互斥", error.Message);

        var other = "{\"Version\":2,\"Width\":500,\"Height\":500,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":4,\"BarcodeType\":25,\"Height\":35,\"ExpandedStackedRows\":3,\"Field\":{\"Value\":\"[91]123\"}}]}]}]}";
        Assert.Throws<NotSupportedException>(() =>
            new TemplateLoader().LoadJson(other, Options(PrintProtocol.EscPos)));
    }

    [Fact]
    public void Composite_rows_all_protocols_rotations_and_quiet_zones_use_actual_dimensions()
    {
        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.EscPos, PrintProtocol.Tspl, PrintProtocol.Zpl })
        foreach (var rotation in new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 })
        {
            var zone = new PrintQuietZone(3, 5, 7, 11);
            var model = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpandedStacked, Primary,
                "[20]12", 35, 2, Gs1CompositeMode.CcA, rotation, zone).WithExpandedStackedRows(3);
            var size = Gs1CompositeEncoder.Measure(model);
            var quarter = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270;
            var width = quarter ? size.Height : size.Width;
            var height = quarter ? size.Width : size.Height;
            var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster);
            Assert.Equal(Assert.Single(provider.Render(Document(Gs1CompositeEncoder.Rasterize(model), width, height))),
                Assert.Single(provider.Render(Document(model, width, height))));
            Assert.Throws<PrintElementValidationException>(() => provider.Render(Document(model, width - 1, height)));
            Assert.Throws<PrintElementValidationException>(() => provider.Render(Document(model, width, height - 1)));
        }
    }

    private static PrintGs1CompositeElement Composite() => new PrintGs1CompositeElement(0, 0,
        BarcodeType.Gs1DataBarExpandedStacked, Primary, "[20]12", 35);

    private static PrintDocument Document(PrintElement element, int width, int height)
    {
        var document = new PrintDocument();
        document.AddPage(width, height).Add(element);
        return document;
    }

    private static BpTemplate Template(BpElement element, int version) => new BpTemplate
    {
        Version = version, Width = 1200, Height = 1200,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> { element } } }
            }
        }
    };

    private static TemplateRenderOptions Options(PrintProtocol protocol) => new TemplateRenderOptions
    {
        Protocol = protocol, Encoding = Encoding.ASCII, EscPosLayout = EscPosLayoutMode.Raster,
        FeedLines = 0, CutAfterPrint = false
    };
}
