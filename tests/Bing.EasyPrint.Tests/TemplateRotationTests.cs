using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证旋转模板的实际占位、原生锚点和分页。</summary>
public sealed class TemplateRotationTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl, 90, 24, 36, "T270 24 0 29 10 ABC")]
    [InlineData(PrintProtocol.Cpcl, 180, 36, 24, "T180 24 0 41 34 ABC")]
    [InlineData(PrintProtocol.Cpcl, 270, 24, 36, "T90 24 0 5 46 ABC")]
    [InlineData(PrintProtocol.Tspl, 90, 24, 30, "TEXT 29,10,\"0\",90,9,9,\"ABC\"")]
    [InlineData(PrintProtocol.Tspl, 180, 30, 24, "TEXT 35,34,\"0\",180,9,9,\"ABC\"")]
    [InlineData(PrintProtocol.Tspl, 270, 24, 30, "TEXT 5,40,\"0\",270,9,9,\"ABC\"")]
    [InlineData(PrintProtocol.Zpl, 90, 24, 30, "^FO5,10^A0R,24,24^FDABC^FS")]
    [InlineData(PrintProtocol.Zpl, 180, 30, 24, "^FO5,10^A0I,24,24^FDABC^FS")]
    [InlineData(PrintProtocol.Zpl, 270, 24, 30, "^FO5,10^A0B,24,24^FDABC^FS")]
    public void Text_rotation_uses_transformed_trace_and_native_origin(PrintProtocol protocol,
        int rotation, int width, int height, string command)
    {
        var font = Text("ABC", rotation);
        font.StartCoordinate = new BpCoordinate { X = 5, Y = 10 };
        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(Template(100, 100, font),
            null, options: Options(protocol)).Pages);
        var trace = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal((5, 10, width, height), (trace.X, trace.Y, trace.Width, trace.Height));
        Assert.Equal((RotationAngle)rotation, trace.Rotation);
        Assert.Contains(command, Encoding.ASCII.GetString(page.Data));
    }

    [Theory]
    [InlineData(90, 30, 26, 0)]
    [InlineData(180, 50, 0, 26)]
    [InlineData(270, 30, 0, 0)]
    public void Multiline_rotation_preserves_columns_and_tracks_individual_lines(int rotation,
        int totalHeight, int firstX, int firstY)
    {
        var font = Text("ABC\nDE", rotation);
        font.Width = 50;
        var template = Template(100, 100, font);
        template.AutoHeight = true;
        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(template,
            null, options: Options(PrintProtocol.Zpl)).Pages);
        var traces = page.Elements.Where(element => element.Type == 1).ToArray();

        Assert.Equal(2, traces.Length);
        Assert.Equal((firstX, firstY), (traces[0].X, traces[0].Y));
        Assert.Equal(rotation == 180 ? (30, 24) : (24, 30), (traces[0].Width, traces[0].Height));
        Assert.Equal(totalHeight, page.Height);
        Assert.True(traces.All(trace => trace.Y + trace.Height <= page.Height));
    }

    [Fact]
    public void Quarter_turn_wraps_against_unrotated_line_width_and_checks_column_width()
    {
        var font = Text("ABCD", 90);
        font.Width = 20;
        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(Template(50, 60, font),
            null, options: Options(PrintProtocol.Zpl)).Pages);
        var traces = page.Elements.Where(element => element.Type == 1).ToArray();
        Assert.Equal(new[] { "AB", "CD" }, traces.Select(trace => trace.Text));
        Assert.Equal(new[] { 26, 0 }, traces.Select(trace => trace.X));
        Assert.All(traces, trace => Assert.Equal((24, 20), (trace.Width, trace.Height)));

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(49, 60, font), null, options: Options(PrintProtocol.Zpl)));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("旋转", error.Message);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(270)]
    public void End_coordinate_is_a_post_rotation_box(int rotation)
    {
        var font = Text("ABC", rotation);
        font.StartCoordinate = new BpCoordinate { X = 5, Y = 10 };
        font.EndCoordinate = new BpCoordinate { X = 29, Y = 40 };
        new TemplateRenderer().Render(Template(100, 100, font), null, options: Options(PrintProtocol.Zpl));

        font.EndCoordinate.Y = 39;
        font.Width = 30;
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(100, 100, font), null, options: Options(PrintProtocol.Zpl)));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("结束坐标", error.Message);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Rotated_multiline_block_moves_to_next_page_without_splitting(int rotation)
    {
        var rotated = Text("ABC\nDE", rotation);
        var template = Template(100, 60, Text("HEAD", 0), rotated);
        template.Rows = template.Rows[0].Cells[0].Elements.Select(element => Row(element)).ToList();
        template.Rows[0].Height = 40;
        var result = new TemplateRenderer().RenderWithLayout(template, null, options: Options(PrintProtocol.Zpl));

        Assert.Equal(2, result.PageCount);
        Assert.Equal("HEAD", Assert.Single(result.Pages[0].Elements.Where(element => element.Type == 1)).Text);
        Assert.Equal(new[] { "ABC", "DE" }, result.Pages[1].Elements.Where(element => element.Type == 1).Select(element => element.Text));
        Assert.All(result.Pages[1].Elements.Where(element => element.Type == 1),
            element => Assert.True(element.Y + element.Height <= 60));
    }

    [Fact]
    public void Quarter_turn_flow_height_uses_text_length()
    {
        var template = Template(100, 100, Text("ABCDE", 90), Text("NEXT", 0));
        var traces = new TemplateRenderer().RenderWithLayout(template, null,
            options: Options(PrintProtocol.Zpl)).Pages[0].Elements.Where(element => element.Type == 1).ToArray();
        Assert.Equal((24, 50), (traces[0].Width, traces[0].Height));
        Assert.Equal(50, traces[1].Y);
    }

    [Fact]
    public void Oversize_rotated_block_reports_element_path()
    {
        var font = Text("ABCDE", 90);
        font.Width = 50;
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(100, 49, font), null, options: Options(PrintProtocol.Zpl)));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("页高", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 90)]
    [InlineData(PrintProtocol.Tspl, 180)]
    [InlineData(PrintProtocol.Tspl, 270)]
    [InlineData(PrintProtocol.Zpl, 90)]
    [InlineData(PrintProtocol.Zpl, 180)]
    [InlineData(PrintProtocol.Zpl, 270)]
    [InlineData(PrintProtocol.Cpcl, 270)]
    public void Barcode_occupancy_rotates_quiet_zones_and_readable_reservation(PrintProtocol protocol, int rotation)
    {
        var barcode = new BpBarcodeElement { Width = 180, Height = 30, LineWidth = 1,
            Rotation = (RotationAngle)rotation, Field = new BgField { Value = "AB" },
            HumanReadable = protocol == PrintProtocol.Cpcl ? (bool?)null : true,
            HumanReadableReserveHeight = protocol == PrintProtocol.Cpcl ? 0 : 12 };
        var template = Template(rotation == 180 ? 180 : 50, 220, barcode);
        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(template, null,
            options: Options(protocol)).Pages);
        var trace = Assert.Single(page.Elements.Where(element => element.Type == 4));
        var h = protocol == PrintProtocol.Cpcl ? 30 : 42;
        Assert.Equal(rotation == 180 ? (180, h) : (h, 180), (trace.Width, trace.Height));
        Assert.Equal((RotationAngle)rotation, trace.Rotation);
    }

    [Theory]
    [InlineData(EscPosLayoutMode.Native)]
    public void EscPos_rejects_rotated_elements_before_resource_loading(EscPosLayoutMode mode)
    {
        var options = new TemplateRenderOptions { Protocol = PrintProtocol.EscPos, EscPosLayout = mode,
            Encoding = Encoding.ASCII, CutAfterPrint = false };
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(100, 100, Text("ABC", 90)), null, options: options));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains(PrintProtocol.EscPos.ToString(), error.Message);
        Assert.Contains("旋转", error.Message);
    }

    [Fact]
    public void Escpos_raster_rotated_underlined_text_reserves_actual_ink_height()
    {
        var fonts = FixedTestFonts.Catalog();
        var family = EscPosRasterCanvas.ResolveFontFamilyName("Ag", null, false, fonts);
        var ink = EscPosRasterCanvas.MeasureTextInkBounds("Ag", 24, false, true, family,
            long.MaxValue, fonts);
        var font = new BpFontElement
        {
            FontSize = 24,
            FontStyle = 4,
            Rotation = RotationAngle.Rotate90,
            Font = new PrintFont(PrintFontKind.Host, family),
            Field = new BgField { Value = "Ag" }
        };
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII,
            FeedLines = 0,
            CutAfterPrint = false,
            EscPosDevice = new EscPosDeviceOptions { RasterFonts = fonts }
        };

        var result = new TemplateRenderer().RenderWithLayout(Template(100, 100, font), null,
            options: options);
        var trace = Assert.Single(result.Pages[0].Elements.Where(element => element.Type == 1));

        Assert.Equal(Math.Max(24, ink.Height), trace.Width);
        Assert.True(trace.Height >= ink.Width);
        Assert.True(Contains(result.Pages[0].Data, new byte[] { 0x1D, 0x76, 0x30 }));
    }

    [Fact]
    public void Escpos_raster_rotated_barcode_uses_host_raster_output()
    {
        var barcode = new BpBarcodeElement
        {
            Height = 24,
            Rotation = RotationAngle.Rotate90,
            HumanReadable = false,
            Field = new BgField { Value = "1234" }
        };
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII,
            FeedLines = 0,
            CutAfterPrint = false
        };

        var result = new TemplateRenderer().RenderWithLayout(Template(240, 320, barcode), null,
            options: options);
        var trace = Assert.Single(result.Pages[0].Elements.Where(element => element.Type == 4));

        Assert.Equal(RotationAngle.Rotate90, trace.Rotation);
        Assert.True(trace.Width > 0 && trace.Height > trace.Width);
        Assert.True(Contains(result.Pages[0].Data, new byte[] { 0x1D, 0x76, 0x30 }));
    }

    [Fact]
    public void Escpos_raster_rotated_qr_uses_host_raster_output()
    {
        var qr = new BpQrCodeElement
        {
            UnitWidth = 2,
            Rotation = RotationAngle.Rotate270,
            Field = new BgField { Value = "QR" }
        };
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII,
            FeedLines = 0,
            CutAfterPrint = false
        };

        var result = new TemplateRenderer().RenderWithLayout(Template(160, 160, qr), null,
            options: options);
        var trace = Assert.Single(result.Pages[0].Elements.Where(element => element.Type == 3));

        Assert.Equal(RotationAngle.Rotate270, trace.Rotation);
        Assert.True(trace.Width > 0 && trace.Height > 0);
        Assert.True(Contains(result.Pages[0].Data, new byte[] { 0x1D, 0x76, 0x30 }));
    }

    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    public void Cpcl_rejects_unexpressible_barcode_angles(int rotation)
    {
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(400, 400, new BpBarcodeElement { Height = 30, Rotation = (RotationAngle)rotation,
                Field = new BgField { Value = "AB" } }), null));
        Assert.Contains("270", error.Message);
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(45)]
    [InlineData(360)]
    public void Invalid_rotation_is_rejected_on_load_and_save(int rotation)
    {
        var template = Template(100, 100, Text("ABC", rotation));
        var loader = new TemplateLoader();
        Assert.Throws<ArgumentException>(() => loader.SaveJson(template));
        Assert.Throws<ArgumentException>(() => loader.LoadJson(
            "{\"Version\":1,\"Width\":100,\"Height\":100,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":1,\"Rotation\":" + rotation + "}]}]}]}"));
    }

    [Fact]
    public void Rotation_and_line_width_round_trip_without_changing_page_bytes()
    {
        var font = Text("ABCD", 90);
        font.Width = 20;
        var template = Template(100, 100, font);
        var loader = new TemplateLoader();
        var copy = loader.LoadJson(loader.SaveJson(template));
        var loaded = Assert.IsType<BpFontElement>(copy.Rows[0].Cells[0].Elements[0]);
        Assert.Equal(RotationAngle.Rotate90, loaded.Rotation);
        Assert.Equal(20, loaded.Width);
        var renderer = new TemplateRenderer();
        Assert.Equal(renderer.Render(template, null, options: Options(PrintProtocol.Zpl)).Pages[0].Data,
            renderer.Render(copy, null, options: Options(PrintProtocol.Zpl)).Pages[0].Data);
    }

    [Fact]
    public void Unsupported_element_rotation_is_not_silently_discarded_from_json()
    {
        Assert.Throws<JsonSerializationException>(() => new TemplateLoader().LoadJson(
            "{\"Version\":1,\"Width\":100,\"Height\":100,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":7,\"Width\":10,\"Height\":10,\"Rotation\":90}]}]}]}"));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Rotated_qr_round_trips_through_json_and_provider(PrintProtocol protocol)
    {
        var template = Template(200, 200, new BpQrCodeElement
        {
            UnitWidth = 2,
            Rotation = RotationAngle.Rotate90,
            Field = new BgField { Value = "QR" }
        });
        var loader = new TemplateLoader();
        var copy = loader.LoadJson(loader.SaveJson(template));
        var loaded = Assert.IsType<BpQrCodeElement>(copy.Rows[0].Cells[0].Elements[0]);
        Assert.Equal(RotationAngle.Rotate90, loaded.Rotation);
        var output = Encoding.ASCII.GetString(new TemplateRenderer().Render(copy, null,
            options: Options(protocol)).Pages[0].Data);
        Assert.Contains(protocol == PrintProtocol.Tspl ? "QRCODE" : "^BQ", output);
        Assert.Contains(protocol == PrintProtocol.Tspl ? ",90,\"QR\"" : "^BQR,", output);
    }

    [Fact]
    public void Rotation_fit_reduces_font_for_the_transformed_available_width()
    {
        var font = Text("ABC", 90);
        font.Width = 80;
        font.Field.Fit = true;
        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(Template(12, 80, font),
            null, options: Options(PrintProtocol.Zpl)).Pages);
        var trace = Assert.Single(page.Elements.Where(element => element.Type == 1));
        Assert.Equal((12, 15), (trace.Width, trace.Height));
        Assert.Contains("^A0R,12,12", Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void Rotated_readable_barcode_moves_as_one_block_at_page_boundary()
    {
        var barcode = new BpBarcodeElement { Width = 180, Height = 30, LineWidth = 1,
            Rotation = RotationAngle.Rotate90, HumanReadable = true, HumanReadableReserveHeight = 12,
            Field = new BgField { Value = "AB" } };
        var template = Template(100, 200);
        template.Rows = new List<BpRow> { Row(Text("HEAD", 0)), Row(barcode) };
        template.Rows[0].Height = 30;
        var result = new TemplateRenderer().RenderWithLayout(template, null, options: Options(PrintProtocol.Tspl));
        Assert.Equal(2, result.PageCount);
        Assert.DoesNotContain(result.Pages[0].Elements, element => element.Type == 4);
        var trace = Assert.Single(result.Pages[1].Elements.Where(element => element.Type == 4));
        Assert.Equal((0, 0, 42, 180), (trace.X, trace.Y, trace.Width, trace.Height));
        Assert.Contains("BARCODE 42,20,\"128\",30,1,90,2,5,\"AB\"", Encoding.ASCII.GetString(result.Pages[1].Data));
    }

    [Theory]
    [InlineData(90, 76, 30)]
    [InlineData(180, 30, 76)]
    [InlineData(270, 76, 30)]
    public void Rotated_empty_lines_keep_spacing_without_requiring_device_glyph_bounds(
        int rotation, int width, int height)
    {
        var font = Text("ABC\n\nDE", rotation);
        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(Template(100, 100, font),
            null, options: Options(PrintProtocol.Tspl)).Pages);
        var traces = page.Elements.Where(element => element.Type == 1 && element.Text.Length != 0).ToArray();
        Assert.Equal(new[] { "ABC", "DE" }, traces.Select(element => element.Text));
        Assert.Equal(width, traces.Max(element => element.X + element.Width));
        Assert.Equal(height, traces.Max(element => element.Y + element.Height));
        Assert.Equal(2, Encoding.ASCII.GetString(page.Data).Split(new[] { "TEXT " },
            StringSplitOptions.None).Length - 1);
    }

    private static BpFontElement Text(string text, int rotation) => new BpFontElement { FontSize = 24,
        Rotation = (RotationAngle)rotation, Field = new BgField { Value = text } };
    private static BpRow Row(params BpElement[] elements) => new BpRow { Cells = new List<BpCell> {
        new BpCell { Elements = elements.ToList() } } };
    private static BpTemplate Template(int width, int height, params BpElement[] elements)
        => new BpTemplate { Version = 3, Width = width, Height = height, Rows = new List<BpRow> { Row(elements) } };
    private static TemplateRenderOptions Options(PrintProtocol protocol)
    {
        var options = new TemplateRenderOptions { Protocol = protocol, Encoding = Encoding.ASCII };
        if (protocol != PrintProtocol.Cpcl)
        { options.DeviceHalfCellWidthAt24 = 10; options.DeviceFullCellWidthAt24 = 20; options.DeviceLineHeightAt24 = 24; }
        return options;
    }

    private static bool Contains(byte[] data, byte[] pattern)
    {
        for (var index = 0; index <= data.Length - pattern.Length; index++)
        {
            var matched = true;
            for (var offset = 0; offset < pattern.Length; offset++)
                if (data[index + offset] != pattern[offset]) { matched = false; break; }
            if (matched) return true;
        }
        return false;
    }
}
