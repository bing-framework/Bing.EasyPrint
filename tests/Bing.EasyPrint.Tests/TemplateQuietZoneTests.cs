using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证模板码图静区的布局、协议预检和 JSON 契约。</summary>
public sealed class TemplateQuietZoneTests
{
    [Fact]
    public void Quiet_zone_json_round_trip_preserves_asymmetric_edges_and_explicit_zero()
    {
        var quiet = new PrintQuietZone(2, 3, 4, 5);
        var template = Template(480, 180,
            new BpBarcodeElement
            {
                Width = 180, Height = 24, LineWidth = 1, HumanReadable = false,
                QuietZone = quiet, Field = Value("1234"),
                StartCoordinate = Point(20, 20)
            },
            new BpQrCodeElement
            {
                Width = 64, Height = 64, UnitWidth = 2, ErrorLevel = "M",
                QuietZone = quiet, Field = Value("Q"),
                StartCoordinate = Point(240, 20)
            });
        var loader = new TemplateLoader();

        var json = loader.SaveJson(template, Options(PrintProtocol.Cpcl));
        var copy = loader.LoadJson(json, Options(PrintProtocol.Cpcl));
        var barcode = Assert.IsType<BpBarcodeElement>(copy.Rows[0].Cells[0].Elements[0]);
        var qr = Assert.IsType<BpQrCodeElement>(copy.Rows[0].Cells[0].Elements[1]);
        Assert.Equal((2, 3, 4, 5), Edges(barcode.QuietZone));
        Assert.Equal((2, 3, 4, 5), Edges(qr.QuietZone));

        var zero = Template(480, 180, new BpBarcodeElement
        {
            Width = 180, Height = 24, LineWidth = 1, HumanReadable = false,
            QuietZone = new PrintQuietZone(), Field = Value("1234"),
            StartCoordinate = Point(20, 20)
        });
        var legacy = Template(480, 180, new BpBarcodeElement
        {
            Width = 180, Height = 24, LineWidth = 1, HumanReadable = false,
            Field = Value("1234"), StartCoordinate = Point(20, 20)
        });
        var renderer = new TemplateRenderer();
        var legacyBytes = renderer.Render(legacy, null, options: Options(PrintProtocol.Cpcl)).Pages[0].Data;
        var zeroBytes = renderer.Render(zero, null, options: Options(PrintProtocol.Cpcl)).Pages[0].Data;
        Assert.False(legacyBytes.SequenceEqual(zeroBytes));
        Assert.Contains("B 128", Encoding.ASCII.GetString(zeroBytes));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Raster)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    public void Template_quiet_zone_rejects_a_page_edge_before_rendering(PrintProtocol protocol,
        EscPosLayoutMode layout)
    {
        var template = Template(240, 120, new BpBarcodeElement
        {
            Width = 220, Height = 24, LineWidth = 1, HumanReadable = false,
            QuietZone = PrintQuietZone.Uniform(2), Field = Value("1234"),
            StartCoordinate = Point(30, 0)
        });

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, null, options: Options(protocol, layout)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("区域", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Template_quiet_zone_rejects_a_neighbor_in_either_element_order(bool barcodeFirst)
    {
        var barcode = new BpBarcodeElement
        {
            Width = 180, Height = 24, LineWidth = 1, HumanReadable = false,
            QuietZone = new PrintQuietZone(12, 2, 2, 2), Field = Value("1234"),
            StartCoordinate = Point(20, 20)
        };
        var neighbor = new BpRectangleElement
        {
            Width = 16, Height = 24, StartCoordinate = Point(10, 20)
        };
        var template = barcodeFirst
            ? Template(240, 120, barcode, neighbor)
            : Template(240, 120, neighbor, barcode);

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, null, options: Options(PrintProtocol.Cpcl)));

        Assert.Equal("Rows[0].Cells[0].Elements[" + (barcodeFirst ? "0" : "1") + "]", error.SourcePath);
        Assert.Contains("静区", error.Message);
        Assert.Contains("Elements[" + (barcodeFirst ? "1" : "0") + "]", error.Message);
    }

    [Fact]
    public void Cpcl_rotated_template_barcode_keeps_outer_origin_and_converts_to_native_vertical_command()
    {
        var barcode = new BpBarcodeElement
        {
            Width = 160, Height = 24, LineWidth = 1, HumanReadable = false,
            Rotation = RotationAngle.Rotate270, QuietZone = new PrintQuietZone(1, 2, 3, 4),
            Field = Value("ABC"), StartCoordinate = Point(40, 20)
        };
        var result = new TemplateRenderer().RenderWithLayout(Template(240, 200, barcode), null,
            options: Options(PrintProtocol.Cpcl));
        var trace = Assert.Single(result.Pages[0].Elements.Where(item => item.Type == 4));
        var output = Encoding.ASCII.GetString(result.Pages[0].Data);

        Assert.Equal((40, 20), (trace.X, trace.Y));
        Assert.Equal((RotationAngle.Rotate270, 30, 160), (trace.Rotation, trace.Width, trace.Height));
        Assert.Contains("VB 128", output);
        Assert.Contains(" 42 179 ", output);
    }

    [Fact]
    public void Template_quiet_zone_moves_a_whole_barcode_to_the_next_page_and_preserves_outer_layout()
    {
        var header = Row(new BpFontElement { FontSize = 24, Field = Value("HEAD") });
        header.Height = 30;
        var barcode = new BpBarcodeElement
        {
            Width = 180, Height = 24, LineWidth = 1, HumanReadable = false,
            QuietZone = PrintQuietZone.Uniform(5), Margin = new BpMargin { Left = 6, Top = 6 },
            Field = Value("1234")
        };
        var template = new BpTemplate
        {
            Version = 3, Width = 240, Height = 55,
            Rows = new List<BpRow> { header, Row(barcode) }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: Options(PrintProtocol.Cpcl));

        Assert.Equal(2, result.PageCount);
        Assert.Contains(result.Pages[0].Elements, item => item.Type == 1 && item.Text == "HEAD");
        Assert.DoesNotContain(result.Pages[0].Elements, item => item.Type == 4);
        var trace = Assert.Single(result.Pages[1].Elements.Where(item => item.Type == 4));
        Assert.Equal((6, 0, 180, 34), (trace.X, trace.Y, trace.Width, trace.Height));
    }

    [Fact]
    public void Native_nonzero_template_quiet_zone_fails_before_later_resource_open_but_zero_renders()
    {
        var template = Template(240, 120,
            new BpBarcodeElement
            {
                Width = 180, Height = 24, LineWidth = 1, HumanReadable = false,
                QuietZone = PrintQuietZone.Uniform(2), Field = Value("1234")
            },
            new BpImageElement { Width = 8, Height = 8, Field = Value("later") });
        var resources = new CountingResources();
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(template, null,
            resources, Options(PrintProtocol.EscPos, EscPosLayoutMode.Native)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("静区", error.Message);
        Assert.Equal(0, resources.OpenCount);

        var zero = Template(240, 120, new BpBarcodeElement
        {
            Width = 180, Height = 24, LineWidth = 1, HumanReadable = false,
            QuietZone = new PrintQuietZone(), Field = Value("1234")
        });
        Assert.NotEmpty(new TemplateRenderer().Render(zero, null,
            options: Options(PrintProtocol.EscPos, EscPosLayoutMode.Native)).Pages[0].Data);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Template_tspl_and_zpl_explicit_quiet_zone_requires_hri_reserve_or_explicit_disable(PrintProtocol protocol)
    {
        var template = Template(260, 120, new BpBarcodeElement
        {
            Width = 220, Height = 24, LineWidth = 1,
            QuietZone = PrintQuietZone.Uniform(2), Field = Value("1234")
        });
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(template, null,
            options: Options(protocol)));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("HumanReadableReserveHeight", error.Message);

        ((BpBarcodeElement)template.Rows[0].Cells[0].Elements[0]).HumanReadable = false;
        Assert.NotEmpty(new TemplateRenderer().Render(template, null, options: Options(protocol)).Pages[0].Data);
    }

    private static BpTemplate Template(int width, int height, params BpElement[] elements)
        => new BpTemplate { Version = 3, Width = width, Height = height, Rows = new List<BpRow> { Row(elements) } };

    private static BpRow Row(params BpElement[] elements)
        => new BpRow { Cells = new List<BpCell> { new BpCell { Width = 0, Elements = elements.ToList() } } };

    private static BgField Value(string value) => new BgField { Value = value };

    private static BpCoordinate Point(int x, int y) => new BpCoordinate { X = x, Y = y };

    private static (int Left, int Top, int Right, int Bottom) Edges(PrintQuietZone zone)
        => (zone.Left, zone.Top, zone.Right, zone.Bottom);

    private static TemplateRenderOptions Options(PrintProtocol protocol,
        EscPosLayoutMode layout = EscPosLayoutMode.Native)
    {
        var options = new TemplateRenderOptions
        {
            Encoding = Encoding.ASCII,
            Protocol = protocol,
            EscPosLayout = layout,
            FeedLines = protocol == PrintProtocol.EscPos ? 0 : null,
            CutAfterPrint = protocol == PrintProtocol.EscPos ? false : null
        };
        if (protocol == PrintProtocol.Tspl || protocol == PrintProtocol.Zpl)
        {
            options.DeviceHalfCellWidthAt24 = 10;
            options.DeviceFullCellWidthAt24 = 20;
            options.DeviceLineHeightAt24 = 24;
        }
        return options;
    }

    private sealed class CountingResources : ITemplateResourceProvider
    {
        public int OpenCount { get; private set; }

        public System.IO.Stream OpenRead(string resourceKey, System.Threading.CancellationToken cancellationToken)
        {
            OpenCount++;
            throw new InvalidOperationException("resource should not be opened");
        }
    }
}
