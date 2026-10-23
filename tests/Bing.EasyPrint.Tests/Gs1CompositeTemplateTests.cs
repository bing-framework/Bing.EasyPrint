using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 GS1 Composite 模板元素的绑定、布局、分页和 JSON 契约。</summary>
public sealed class Gs1CompositeTemplateTests
{
    [Theory]
    [InlineData(2, BarcodeType.Gs1DataBarTruncated, "12345678901231")]
    [InlineData(2, BarcodeType.Gs1DataBarStackedOmnidirectional, "12345678901231")]
    [InlineData(3, BarcodeType.Gs1DataBarTruncated, "12345678901231")]
    [InlineData(3, BarcodeType.Gs1DataBarStackedOmnidirectional, "12345678901231")]
    public void Remaining_databar_roundtrip_keeps_actual_dimensions_and_output(int version, BarcodeType type, string primary)
    {
        foreach (var mode in new[] { Gs1CompositeMode.Auto, Gs1CompositeMode.CcA, Gs1CompositeMode.CcB })
        {
            var template = SingleComposite(version, Composite(type, mode), width: 600, height: 300);
            ((BpGs1CompositeElement)template.Rows[0].Cells[0].Elements[0]).LinearBarHeight = 68;
            var options = Options();
            var loader = new TemplateLoader();
            var restored = loader.LoadJson(loader.SaveJson(template, options), options);
            var element = Assert.IsType<BpGs1CompositeElement>(restored.Rows[0].Cells[0].Elements[0]);
            Assert.Equal(type, element.LinearType);
            Assert.Equal(mode, element.Mode);
            var data = new { Primary = primary, Secondary = "(91)123456" };
            var renderer = new TemplateRenderer();
            var expected = renderer.RenderWithLayout(template, data, options: options);
            var actual = renderer.RenderWithLayout(restored, data, options: options);
            Assert.Equal(expected.Pages[0].Data, actual.Pages[0].Data);
            var trace = Assert.Single(actual.Pages[0].Elements.Where(value => value.Type == 11));
            var size = Gs1CompositeEncoder.Measure(new PrintGs1CompositeElement(0, 0, type,
                data.Primary, data.Secondary, 68, mode: mode));
            Assert.Equal((size.Width, size.Height), (trace.Width, trace.Height));
        }
    }

    [Theory]
    [InlineData(2, BarcodeType.Ean8, "12345670")]
    [InlineData(3, BarcodeType.Ean8, "12345670")]
    [InlineData(2, BarcodeType.Ean13, "1234567890128")]
    [InlineData(3, BarcodeType.Ean13, "1234567890128")]
    [InlineData(2, BarcodeType.UpcA, "012345678905")]
    [InlineData(3, BarcodeType.UpcA, "012345678905")]
    [InlineData(2, BarcodeType.UpcE, "01234565")]
    [InlineData(3, BarcodeType.UpcE, "01234565")]
    [InlineData(2, BarcodeType.Ean8, "12345670+12")]
    [InlineData(2, BarcodeType.Ean8, "12345670+51234")]
    [InlineData(2, BarcodeType.Ean13, "1234567890128+12")]
    [InlineData(2, BarcodeType.Ean13, "1234567890128+51234")]
    [InlineData(2, BarcodeType.UpcA, "012345678905+12")]
    [InlineData(2, BarcodeType.UpcA, "012345678905+51234")]
    [InlineData(2, BarcodeType.UpcE, "01234565+12")]
    [InlineData(2, BarcodeType.UpcE, "01234565+51234")]
    [InlineData(3, BarcodeType.Ean8, "12345670+12")]
    [InlineData(3, BarcodeType.Ean8, "12345670+51234")]
    [InlineData(3, BarcodeType.Ean13, "1234567890128+12")]
    [InlineData(3, BarcodeType.Ean13, "1234567890128+51234")]
    [InlineData(3, BarcodeType.UpcA, "012345678905+12")]
    [InlineData(3, BarcodeType.UpcA, "012345678905+51234")]
    [InlineData(3, BarcodeType.UpcE, "01234565+12")]
    [InlineData(3, BarcodeType.UpcE, "01234565+51234")]
    public void Ean_upc_roundtrip_keeps_actual_dimensions_and_output(int version, BarcodeType type, string primary)
    {
        foreach (var mode in new[] { Gs1CompositeMode.Auto, Gs1CompositeMode.CcA, Gs1CompositeMode.CcB })
        {
            var template = SingleComposite(version, Composite(type, mode), width: 600);
            var options = Options();
            var loader = new TemplateLoader();
            var restored = loader.LoadJson(loader.SaveJson(template, options), options);
            var element = Assert.IsType<BpGs1CompositeElement>(restored.Rows[0].Cells[0].Elements[0]);
            Assert.Equal(type, element.LinearType);
            Assert.Equal(mode, element.Mode);
            var data = new { Primary = primary, Secondary = "(91)123456" };
            var renderer = new TemplateRenderer();
            var expected = renderer.RenderWithLayout(template, data, options: options);
            var actual = renderer.RenderWithLayout(restored, data, options: options);
            Assert.Equal(expected.Pages[0].Data, actual.Pages[0].Data);
            var trace = Assert.Single(actual.Pages[0].Elements.Where(value => value.Type == 11));
            var size = Gs1CompositeEncoder.Measure(new PrintGs1CompositeElement(0, 0, type,
                data.Primary, data.Secondary, 20, mode: mode));
            Assert.Equal((size.Width, size.Height), (trace.Width, trace.Height));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Stacked_roundtrip_keeps_fields_mode_and_exact_output(int version)
    {
        var template = SingleComposite(version, Composite(BarcodeType.Gs1DataBarStacked, Gs1CompositeMode.CcB), width: 600);
        ((BpGs1CompositeElement)template.Rows[0].Cells[0].Elements[0]).LinearBarHeight = 31;
        var options = Options();
        var loader = new TemplateLoader();
        var restored = loader.LoadJson(loader.SaveJson(template, options), options);
        var element = Assert.IsType<BpGs1CompositeElement>(restored.Rows[0].Cells[0].Elements[0]);
        Assert.Equal(BarcodeType.Gs1DataBarStacked, element.LinearType);
        Assert.Equal(Gs1CompositeMode.CcB, element.Mode);
        var data = new { Primary = "(01)12345678901231", Secondary = "(91)123456" };
        var renderer = new TemplateRenderer();
        var expected = renderer.RenderWithLayout(template, data, options: options);
        var actual = renderer.RenderWithLayout(restored, data, options: options);
        Assert.Equal(expected.Pages[0].Data, actual.Pages[0].Data);
        var trace = Assert.Single(actual.Pages[0].Elements.Where(value => value.Type == 11));
        var size = Gs1CompositeEncoder.Measure(new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarStacked, data.Primary, data.Secondary, 31, mode: Gs1CompositeMode.CcB));
        Assert.Equal((size.Width, size.Height), (trace.Width, trace.Height));
    }

    [Theory]
    [InlineData(2, BarcodeType.Gs1DataBarExpanded)]
    [InlineData(3, BarcodeType.Gs1DataBarExpanded)]
    [InlineData(2, BarcodeType.Gs1DataBarExpandedStacked)]
    [InlineData(3, BarcodeType.Gs1DataBarExpandedStacked)]
    public void Expanded_roundtrip_keeps_fields_mode_and_exact_output(int version, BarcodeType type)
    {
        var template = SingleComposite(version, Composite(type, Gs1CompositeMode.CcB), width: 600);
        var options = Options();
        var loader = new TemplateLoader();
        var restored = loader.LoadJson(loader.SaveJson(template, options), options);
        var element = Assert.IsType<BpGs1CompositeElement>(restored.Rows[0].Cells[0].Elements[0]);
        Assert.Equal(type, element.LinearType);
        Assert.Equal(Gs1CompositeMode.CcB, element.Mode);
        var data = new { Primary = "(01)09506000134352(10)LOT-A", Secondary = "(91)123456" };
        var renderer = new TemplateRenderer();
        var expected = renderer.RenderWithLayout(template, data, options: options);
        var actual = renderer.RenderWithLayout(restored, data, options: options);
        Assert.Equal(expected.Pages[0].Data, actual.Pages[0].Data);
        var trace = Assert.Single(actual.Pages[0].Elements.Where(value => value.Type == 11));
        var size = Gs1CompositeEncoder.Measure(new PrintGs1CompositeElement(0, 0, type, data.Primary, data.Secondary, 20, mode: Gs1CompositeMode.CcB));
        Assert.Equal((size.Width, size.Height), (trace.Width, trace.Height));
    }

    [Theory]
    [InlineData(2, Gs1CompositeMode.CcA)]
    [InlineData(2, Gs1CompositeMode.CcB)]
    [InlineData(2, Gs1CompositeMode.CcC)]
    [InlineData(3, Gs1CompositeMode.CcA)]
    [InlineData(3, Gs1CompositeMode.CcB)]
    [InlineData(3, Gs1CompositeMode.CcC)]
    public void Gs1_128_roundtrip_keeps_fields_mode_and_exact_output(int version, Gs1CompositeMode mode)
    {
        var template = SingleComposite(version, Composite(BarcodeType.UccEan128, mode));
        var options = Options();
        var loader = new TemplateLoader();
        var restored = loader.LoadJson(loader.SaveJson(template, options), options);
        var element = Assert.IsType<BpGs1CompositeElement>(restored.Rows[0].Cells[0].Elements[0]);
        Assert.Equal(BarcodeType.UccEan128, element.LinearType);
        Assert.Equal(mode, element.Mode);
        var data = new { Primary = "(01)12345678901231", Secondary = "(91)123456" };
        var renderer = new TemplateRenderer();
        var expected = renderer.RenderWithLayout(template, data, options: options);
        var actual = renderer.RenderWithLayout(restored, data, options: options);
        Assert.Equal(expected.Pages[0].Data, actual.Pages[0].Data);
        var trace = Assert.Single(actual.Pages[0].Elements.Where(value => value.Type == 11));
        var size = Gs1CompositeEncoder.Measure(Gs1CompositeGs1128AbTests.Element(mode));
        Assert.Equal((size.Width, size.Height), (trace.Width, trace.Height));
    }

    [Theory]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional, Gs1CompositeMode.CcC)]
    [InlineData(BarcodeType.Gs1DataBarLimited, Gs1CompositeMode.CcC)]
    [InlineData(BarcodeType.Gs1DataBarExpanded, Gs1CompositeMode.CcC)]
    [InlineData(BarcodeType.Gs1DataBarStacked, Gs1CompositeMode.CcC)]
    [InlineData(BarcodeType.Gs1DataBarExpandedStacked, Gs1CompositeMode.CcC)]
    [InlineData(BarcodeType.Ean8, Gs1CompositeMode.CcC)]
    [InlineData(BarcodeType.Ean13, Gs1CompositeMode.CcC)]
    [InlineData(BarcodeType.UpcA, Gs1CompositeMode.CcC)]
    [InlineData(BarcodeType.UpcE, Gs1CompositeMode.CcC)]
    public void Cc_c_invalid_pair_is_rejected_before_binding(BarcodeType type, Gs1CompositeMode mode)
        => Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(
            SingleComposite(3, Composite(type, mode)), new { }, options: Options()));

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void V2_and_v3_bind_primary_and_secondary_fields_as_one_layout_element(int version)
    {
        const string primary = "1234567890123";
        const string secondary = "(91)123456";
        var template = SingleComposite(version);
        var renderer = new TemplateRenderer();

        var result = renderer.RenderWithLayout(template,
            new { Primary = primary, Secondary = secondary }, options: Options());
        var trace = Assert.Single(result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type == 11));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", trace.SourcePath);
        Assert.Equal(primary, trace.Text);
        Assert.Equal(RotationAngle.None, trace.Rotation);
        Assert.True(trace.Width > 0);
        Assert.True(trace.Height > 0);
        Assert.NotEmpty(result.Pages[0].Data);

        var changed = renderer.RenderWithLayout(template,
            new { Primary = primary, Secondary = "(91)654321" }, options: Options());
        Assert.NotEqual(result.Pages[0].Data, changed.Pages[0].Data);
    }

    [Theory]
    [InlineData("not-a-gtin", "(91)123456", "Primary")]
    [InlineData("1234567890123", "(03)1", "Secondary")]
    public void Composite_encoding_errors_report_the_bound_field_path(
        string primary, string secondary, string fieldPath)
    {
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            SingleComposite(3), new { Primary = primary, Secondary = secondary }, options: Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Equal(fieldPath, error.FieldPath);
    }

    [Fact]
    public void Repeated_component_composite_error_keeps_definition_instance_and_item_paths()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 240,
            Height = 120,
            Components = new List<BpComponentDefinition>
            {
                new BpComponentDefinition
                {
                    Id = "composite",
                    Width = 240,
                    Height = 80,
                    Elements = new List<BpElement> { Composite() }
                }
            },
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    RepeatSource = "Items",
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Elements = new List<BpElement>
                            {
                                new BpGroupElement { ComponentId = "composite" }
                            }
                        }
                    }
                }
            }
        };

        var data = new
        {
            Items = new[]
            {
                new { Primary = "1234567890123", Secondary = "(91)123456" },
                new { Primary = "not-a-gtin", Secondary = "(91)123456" }
            }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, data, options: Options()));

        Assert.Equal("Primary", error.FieldPath);
        Assert.Equal("Components[0].Elements[0]", error.DefinitionPath);
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ComponentInstancePath);
        Assert.Equal(new[] { 1 }, error.InstanceIndices);
        Assert.Equal("Rows[0].Cells[0].Elements[0].Elements[0]", error.SourcePath);
    }

    [Fact]
    public void V1_composite_requires_template_upgrade()
    {
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            SingleComposite(1), new { Primary = "1234567890123", Secondary = "(91)123456" },
            options: Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("升级", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Composite_moves_to_the_next_page_as_an_indivisible_block()
    {
        var model = Model();
        var size = Gs1CompositeEncoder.Measure(model);
        var template = new BpTemplate
        {
            Version = 3,
            Width = size.Width + 20,
            Height = 30 + size.Height - 1,
            Rows = new List<BpRow>
            {
                Row(new BpFontElement { FontSize = 24, Field = new BgField { Value = "HEAD" } }, height: 30),
                Row(Composite())
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, Data(), options: Options());

        Assert.Equal(2, result.PageCount);
        Assert.DoesNotContain(result.Pages[0].Elements, element => element.Type == 11);
        var trace = Assert.Single(result.Pages[1].Elements.Where(element => element.Type == 11));
        Assert.Equal((0, 0, size.Width, size.Height),
            (trace.X, trace.Y, trace.Width, trace.Height));
    }

    [Fact]
    public void Composite_larger_than_a_page_fails_without_returning_partial_pages()
    {
        var model = Model();
        var size = Gs1CompositeEncoder.Measure(model);
        var template = new BpTemplate
        {
            Version = 3,
            Width = size.Width + 20,
            Height = size.Height - 1,
            Rows = new List<BpRow> { Row(Composite()) }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, Data(), options: Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("超过可打印页高", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Composite_rotation_uses_measured_transposed_size_and_start_alignment(RotationAngle rotation)
    {
        var quietZone = new PrintQuietZone(2, 3, 4, 5);
        var model = Model(rotation, quietZone);
        var size = Gs1CompositeEncoder.Measure(model);
        var quarterTurn = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270;
        var expectedWidth = quarterTurn ? size.Height : size.Width;
        var expectedHeight = quarterTurn ? size.Width : size.Height;
        var element = Composite(rotation: rotation, startX: 7, startY: 9,
            quietZone: quietZone);
        var template = SingleComposite(3, element, 7 + expectedWidth + 10, 9 + expectedHeight + 10);

        var result = new TemplateRenderer().RenderWithLayout(template, Data(), options: Options());
        var trace = Assert.Single(result.Pages.SelectMany(page => page.Elements)
            .Where(item => item.Type == 11));

        Assert.Equal((7, 9, expectedWidth, expectedHeight, rotation),
            (trace.X, trace.Y, trace.Width, trace.Height, trace.Rotation));
    }

    [Fact]
    public void Composite_json_round_trip_preserves_fields_parameters_layout_and_output()
    {
        var element = Composite(type: BarcodeType.Gs1DataBarLimited, mode: Gs1CompositeMode.CcB,
            moduleWidth: 3, linearBarHeight: 28, rotation: RotationAngle.Rotate270,
            quietZone: new PrintQuietZone(1, 2, 3, 4), startX: 12, startY: 8);
        var template = SingleComposite(3, element, 360, 300);
        var options = Options();
        var loader = new TemplateLoader();
        var loaded = loader.LoadJson(loader.SaveJson(template, options), options);
        var copy = Assert.IsType<BpGs1CompositeElement>(loaded.Rows[0].Cells[0].Elements[0]);

        Assert.Equal(11, copy.Type);
        Assert.Equal("Primary", copy.Field.Name);
        Assert.Equal("Secondary", copy.SecondaryField.Name);
        Assert.Equal(element.LinearType, copy.LinearType);
        Assert.Equal(element.Mode, copy.Mode);
        Assert.Equal(element.ModuleWidth, copy.ModuleWidth);
        Assert.Equal(element.LinearBarHeight, copy.LinearBarHeight);
        Assert.Equal(element.Rotation, copy.Rotation);
        Assert.Equal((1, 2, 3, 4), Edges(copy.QuietZone));
        Assert.Equal((element.StartCoordinate.X, element.StartCoordinate.Y),
            (copy.StartCoordinate.X, copy.StartCoordinate.Y));

        var data = new { Primary = "1234567890123", Secondary = "(91)123456" };
        var renderer = new TemplateRenderer();
        var original = renderer.RenderWithLayout(template, data, options: options);
        var roundTrip = renderer.RenderWithLayout(loaded, data, options: options);
        Assert.Equal(original.PageCount, roundTrip.PageCount);
        Assert.Equal(original.Pages.Select(page => page.Data), roundTrip.Pages.Select(page => page.Data));
        var originalTrace = Assert.Single(original.Pages.SelectMany(page => page.Elements)
            .Where(item => item.Type == 11));
        var copiedTrace = Assert.Single(roundTrip.Pages.SelectMany(page => page.Elements)
            .Where(item => item.Type == 11));
        Assert.Equal((originalTrace.SourcePath, originalTrace.X, originalTrace.Y,
                originalTrace.Width, originalTrace.Height, originalTrace.Rotation),
            (copiedTrace.SourcePath, copiedTrace.X, copiedTrace.Y,
                copiedTrace.Width, copiedTrace.Height, copiedTrace.Rotation));
    }

    private static BpTemplate SingleComposite(int version, BpGs1CompositeElement element = null,
        int width = 320, int height = 160)
        => new BpTemplate
        {
            Version = version,
            Width = width,
            Height = height,
            Rows = new List<BpRow> { Row(element ?? Composite()) }
        };

    private static BpRow Row(BpElement element, int height = 0)
        => new BpRow
        {
            Height = height,
            Cells = new List<BpCell>
            {
                new BpCell { Elements = new List<BpElement> { element } }
            }
        };

    private static BpGs1CompositeElement Composite(
        BarcodeType type = BarcodeType.Gs1DataBarOmnidirectional,
        Gs1CompositeMode mode = Gs1CompositeMode.CcA,
        int moduleWidth = 2,
        int linearBarHeight = 20,
        RotationAngle rotation = RotationAngle.None,
        PrintQuietZone quietZone = null,
        int startX = 0,
        int startY = 0)
        => new BpGs1CompositeElement
        {
            Field = new BgField { Name = "Primary" },
            SecondaryField = new BgField { Name = "Secondary" },
            LinearType = type,
            Mode = mode,
            ModuleWidth = moduleWidth,
            LinearBarHeight = linearBarHeight,
            Rotation = rotation,
            QuietZone = quietZone,
            StartCoordinate = new BpCoordinate { X = startX, Y = startY }
        };

    private static PrintGs1CompositeElement Model(RotationAngle rotation = RotationAngle.None,
        PrintQuietZone quietZone = null)
        => new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarOmnidirectional,
            "1234567890123", "(91)123456", 20, moduleWidth: 2,
            mode: Gs1CompositeMode.CcA, rotation: rotation, quietZone: quietZone);

    private static TemplateRenderOptions Options()
        => new TemplateRenderOptions
        {
            Encoding = Encoding.ASCII,
            Protocol = PrintProtocol.Cpcl,
            FeedLines = 0,
            CutAfterPrint = false
        };

    private static object Data()
        => new { Primary = "1234567890123", Secondary = "(91)123456" };

    private static (int Left, int Top, int Right, int Bottom) Edges(PrintQuietZone zone)
        => (zone.Left, zone.Top, zone.Right, zone.Bottom);
}
