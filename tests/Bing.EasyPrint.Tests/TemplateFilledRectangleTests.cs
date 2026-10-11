using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TemplateFilledRectangleTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Public_solid_factory_matches_template_fill_and_survives_page_snapshot(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(10, 7).Add(PrintRectangleElement.CreateFilled(2, 4, 8, 3), "Solid");
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster);
        var actual = Assert.Single(provider.Render(document));
        var expected = new TemplateRenderer().Render(Template(new BpRectangleElement
        { Width = 8, Height = 3, Filled = true, StartCoordinate = new BpCoordinate { X = 2, Y = 4 } }),
            null, options: Options(protocol)).Pages[0].Data;
        Assert.Equal(expected, actual);
        Assert.Equal(actual, Assert.Single(provider.Render(document)));
    }

    [Fact]
    public void Public_solid_factory_rejects_empty_negative_and_overflow_geometry()
    {
        foreach (var geometry in new[] { (-1, 0, 1, 1), (0, -1, 1, 1), (0, 0, 0, 1),
            (0, 0, 1, 0), (0, 0, -1, 1), (0, 0, 1, -1), (int.MaxValue, 0, 1, 1), (0, int.MaxValue, 1, 1) })
            Assert.Throws<ArgumentOutOfRangeException>(() => PrintRectangleElement.CreateFilled(
                geometry.Item1, geometry.Item2, geometry.Item3, geometry.Item4));
    }

    [Theory]
    [InlineData(32001, 1)]
    [InlineData(1, 32001)]
    public void Zpl_fill_size_is_rejected_before_binding_or_opening_earlier_images(int width, int height)
    {
        var template = Template(new BpRectangleElement { Width = width, Height = height, Filled = true });
        template.Width = Math.Max(10, width);
        template.Height = Math.Max(7, height);
        template.Rows[0].Cells[0].Elements.Insert(0,
            new BpImageElement { Width = 1, Height = 1, Field = new BgField { Value = "missing" } });
        var resources = new NeverOpened();
        var options = Options(PrintProtocol.Zpl);
        options.MaxPageHeight = template.Height;
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, null, resources, options));
        Assert.Equal("Rows[0].Cells[0].Elements[1]", error.SourcePath);
        Assert.Contains("32000", error.Message);
        Assert.Equal(0, resources.Count);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Solid_fill_moves_to_the_next_page_as_one_exact_occupied_region(PrintProtocol protocol)
    {
        var template = Template(new BpRectangleElement { Width = 8, Height = 3, Filled = true });
        template.Rows.Insert(0, new BpRow { Height = 5, Cells = new List<BpCell> { new BpCell() } });
        var pages = new TemplateRenderer().RenderWithLayout(template, null, options: Options(protocol)).Pages;
        Assert.Equal(2, pages.Count);
        Assert.DoesNotContain(pages[0].Elements, element => element.Type == 7);
        var trace = Assert.Single(pages[1].Elements.Where(element => element.Type == 7));
        Assert.Equal((0, 0, 8, 3), (trace.X, trace.Y, trace.Width, trace.Height));
        Assert.Equal("Rows[1].Cells[0].Elements[0]", trace.SourcePath);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Filled_template_round_trips_with_exact_occupation_and_protocol_output(PrintProtocol protocol)
    {
        var template = Template(new BpRectangleElement { Width = 8, Height = 3, Filled = true,
            LineWidth = 16, StartCoordinate = new BpCoordinate { X = 2, Y = 4 } });
        var loader = new TemplateLoader();
        var loaded = loader.LoadJson(loader.SaveJson(template));
        Assert.True(Assert.IsType<BpRectangleElement>(loaded.Rows[0].Cells[0].Elements[0]).Filled);
        var options = Options(protocol);
        Assert.True(TemplateProtocolCapabilities.Create(options).SupportsFilledRectangles);
        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(loaded, null, options: options).Pages);
        var trace = Assert.Single(page.Elements.Where(element => element.Type == 7));
        Assert.Equal((2, 4, 8, 3), (trace.X, trace.Y, trace.Width, trace.Height));
        Assert.Equal(page.Data, new TemplateRenderer().Render(template, null, options: options).Pages[0].Data);
        var text = Encoding.ASCII.GetString(page.Data);
        if (protocol == PrintProtocol.Cpcl) Assert.Contains("L 2 4 9 4 3\r\n", text);
        else if (protocol == PrintProtocol.Tspl) Assert.Contains("BAR 2,4,8,3\r\n", text);
        else if (protocol == PrintProtocol.Zpl) Assert.Contains("^FO2,4^GB8,3,3,B,0^FS\r\n", text);
        else
        {
            // ESC @ + GS v 0: two bytes per row, seven rows; only rows 4–6 are black at x=2–9.
            Assert.Equal(new byte[] { 0x1B, 0x40, 0x1D, 0x76, 0x30, 0, 2, 0, 7, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0x3F, 0xC0, 0x3F, 0xC0, 0x3F, 0xC0 }, page.Data);
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Omitted_or_false_fill_preserves_existing_outline_bytes(PrintProtocol protocol)
    {
        var template = Template(new BpRectangleElement { Width = 8, Height = 3, StartCoordinate = new BpCoordinate { X = 1, Y = 2 } });
        var loader = new TemplateLoader();
        var json = JObject.Parse(loader.SaveJson(template));
        ((JObject)json["Rows"][0]["Cells"][0]["Elements"][0]).Remove("Filled");
        var loaded = loader.LoadJson(json.ToString());
        Assert.False(Assert.IsType<BpRectangleElement>(loaded.Rows[0].Cells[0].Elements[0]).Filled);
        Assert.Equal(new TemplateRenderer().Render(template, null, options: Options(protocol)).Pages[0].Data,
            new TemplateRenderer().Render(loaded, null, options: Options(protocol)).Pages[0].Data);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_fill_remains_native_when_the_device_disables_images(PrintProtocol protocol)
    {
        var options = Options(protocol);
        if (protocol == PrintProtocol.Tspl) options.TsplMedia = new TsplMediaOptions { Profile = new TsplPrinterProfile { SupportsImages = false } };
        else options.ZplMedia = new ZplMediaOptions { Profile = new ZplPrinterProfile { SupportsImages = false } };
        var page = new TemplateRenderer().Render(Template(new BpRectangleElement { Width = 1, Height = 1, Filled = true }), null, options: options).Pages[0];
        Assert.Contains(protocol == PrintProtocol.Tspl ? "BAR 0,0,1,1" : "^GB1,1,1,B,0", Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void New_cpcl_template_solid_fill_keeps_legacy_document_inverse_fill_bytes()
    {
        var page = new TemplateRenderer().Render(Template(new BpRectangleElement { Width = 1, Height = 1, Filled = true }), null,
            options: Options(PrintProtocol.Cpcl)).Pages[0];
        Assert.Contains("L 0 0 0 0 1\r\n", Encoding.ASCII.GetString(page.Data));
        var document = new PrintDocument();
        document.AddPage(10, 7).Add(new PrintRectangleElement(2, 4, 8, 3, filled: true));
        Assert.Contains("IL 2 4 10 7 3\r\n", Encoding.ASCII.GetString(new CpclPrintProtocolProvider(Encoding.ASCII).Render(document)[0]));
    }

    [Fact]
    public void Native_fill_is_rejected_with_the_template_path()
    {
        var options = Options(PrintProtocol.EscPos);
        options.EscPosLayout = EscPosLayoutMode.Native;
        Assert.False(TemplateProtocolCapabilities.Create(options).SupportsFilledRectangles);
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Template(new BpRectangleElement { Width = 1, Height = 1, Filled = true }), null, options: options));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Disabled_graphics_are_rejected_before_opening_earlier_image_resources(PrintProtocol protocol)
    {
        var template = Template(new BpRectangleElement { Width = 1, Height = 1, Filled = true });
        template.Rows[0].Cells[0].Elements.Insert(0, new BpImageElement { Width = 1, Height = 1, Field = new BgField { Value = "missing" } });
        var options = Options(protocol);
        if (protocol == PrintProtocol.Tspl) options.TsplMedia = new TsplMediaOptions { Profile = new TsplPrinterProfile { SupportsGraphics = false } };
        else options.ZplMedia = new ZplMediaOptions { Profile = new ZplPrinterProfile { SupportsGraphics = false } };
        var resources = new NeverOpened();
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(template, null, resources, options));
        Assert.Equal("Rows[0].Cells[0].Elements[1]", error.SourcePath);
        Assert.Equal(0, resources.Count);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"true\"")]
    [InlineData("null")]
    public void Loader_rejects_non_boolean_fill(string value)
        => Assert.Throws<JsonSerializationException>(() => new TemplateLoader().LoadJson(
            "{\"Version\":1,\"Width\":10,\"Height\":7,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":7,\"Width\":1,\"Height\":1,\"Filled\":" + value + "}]}]}]}"));

    [Fact]
    public void Loader_rejects_fill_on_other_element_types()
        => Assert.Throws<JsonSerializationException>(() => new TemplateLoader().LoadJson(
            "{\"Version\":1,\"Width\":10,\"Height\":7,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":2,\"Filled\":true}]}]}]}"));

    private static TemplateRenderOptions Options(PrintProtocol protocol) => new TemplateRenderOptions
    { Protocol = protocol, Encoding = Encoding.ASCII, EscPosLayout = EscPosLayoutMode.Raster, FeedLines = 0, CutAfterPrint = false };

    private static BpTemplate Template(BpElement element) => new BpTemplate { Version = 2, Width = 10, Height = 7,
        Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> { element } } } } } };

    private sealed class NeverOpened : ITemplateResourceProvider
    {
        internal int Count;
        public Stream OpenRead(string key, CancellationToken cancellationToken) { Count++; throw new IOException("Should not open"); }
    }
}
