using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Bing.EasyPrint.Protocols;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class TemplateRendererTests
{
    private static TemplateRenderOptions Options() => new TemplateRenderOptions { Encoding = Encoding.UTF8 };
    private static BpFontElement Text(string value = "hello", string name = null) => new BpFontElement { FontSize = 24, Field = new BgField { Value = value, Name = name } };
    private static BpRow Row(params BpElement[] elements) => new BpRow { Cells = new List<BpCell> { new BpCell { Elements = elements.ToList() } } };
    private static BpTemplate Template(params BpRow[] rows) => new BpTemplate { Width = 576, Height = 200, Rows = rows.ToList() };
    private static string Output(TemplateRenderResult result) => string.Concat(result.Pages.Select(p => Encoding.UTF8.GetString(p.Data)));
    private static TemplateRenderResult Render(BpTemplate template, object data = null, TemplateRenderOptions options = null) => new TemplateRenderer().Render(template, data, options: options ?? Options());

    [Fact]
    public void Json_round_trip_preserves_bytes_and_all_element_types()
    {
        using var image = new Image<Rgba32>(9, 8, new Rgba32(0, 0, 0));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        var provider = new Resources(stream.ToArray());
        var template = Template(Row(Text(name: "Customer.Name")), Row(new BpImageElement { Field = new BgField { Value = "logo" }, Width = 9, Height = 8 }),
            Row(new BpQrCodeElement { UnitWidth = 1, Field = new BgField { Value = "abc" } }),
            Row(new BpBarcodeElement { LineWidth = 1, Height = 30, Field = new BgField { Value = "123" } }));
        var data = new { Customer = new { Name = "张三" } };
        var loader = new TemplateLoader();
        var json = loader.SaveJson(template, Options());
        var copy = loader.LoadJson(json, Options());
        var renderer = new TemplateRenderer();
        var original = renderer.Render(template, data, provider, Options());
        var roundTrip = renderer.Render(copy, data, provider, Options());
        Assert.Equal(original.Pages[0].Data, roundTrip.Pages[0].Data);
        Assert.Contains("张三", Output(original));
        Assert.Contains("EG 2 8", Output(original));
        Assert.Contains("B QR", Output(original));
        Assert.Contains("B 128", Output(original));
        Assert.Equal(2, provider.OpenCount);
        Assert.True(provider.LastStreamDisposed);
    }

    [Fact]
    public void Repeated_rows_split_text_and_repeat_headers_without_mutating_template()
    {
        var header = Row(Text("HEADER")); header.IsHeader = true;
        var detail = Row(Text(name: "Name")); detail.RepeatSource = "Items";
        var template = Template(header, detail); template.Width = 72; template.Height = 80;
        var loader = new TemplateLoader(); var before = loader.SaveJson(template);
        var result = Render(template, new { Items = new[] { new { Name = "abcdefghijklmnopqr" }, new { Name = "uvwxyz" } } });
        Assert.True(result.PageCount >= 2);
        Assert.All(result.Pages, page => Assert.Contains("HEADER", Encoding.UTF8.GetString(page.Data)));
        Assert.Contains("abcdef", Output(result)); Assert.Contains("mnopqr", Output(result));
        Assert.Equal(before, loader.SaveJson(template));
        Assert.Equal(Output(result), Output(Render(template, new { Items = new[] { new { Name = "abcdefghijklmnopqr" }, new { Name = "uvwxyz" } } })));
    }

    [Fact]
    public void Dynamic_pages_use_actual_height()
    {
        var template = Template(Row(Text())); template.AutoHeight = true; template.Height = 0;
        var result = Render(template);
        Assert.Equal(24, result.Pages[0].Height);
        Assert.StartsWith("! 0 200 200 24 1\r\n", Output(result));
    }

    [Fact]
    public void Template_can_render_text_to_tspl_and_zpl()
    {
        var template = Template(Row(Text("HELLO")));
        var tspl = Render(template, options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Tspl });
        var zpl = Render(template, options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Zpl });

        Assert.Contains("SIZE 576 dot,200 dot", Output(tspl));
        Assert.Contains("TEXT 0,0", Output(tspl));
        Assert.Equal(PrintProtocol.Tspl, tspl.Protocol);
        Assert.Equal(PrintProtocol.Tspl, tspl.Pages[0].Protocol);
        Assert.Contains("^XA", Output(zpl));
        Assert.Contains("^FO0,0", Output(zpl));
    }

    [Fact]
    public void Template_preflight_reports_unsupported_element_on_later_page()
    {
        var second = Text("SECOND");
        second.Bold = true;
        var template = Template(Row(Text("FIRST")), Row(second));
        template.Width = 120;
        template.Height = 26;

        var error = Assert.Throws<TemplateRenderException>(() => Render(template,
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Tspl }));

        Assert.Contains("Rows[1]", error.SourcePath);
        Assert.Contains(PrintProtocol.Tspl.ToString(), error.Message);
    }

    [Fact]
    public void Template_page_options_control_tspl_copies_and_reject_line_feed_units()
    {
        var options = new TemplateRenderOptions
        {
            Encoding = Encoding.ASCII, Protocol = PrintProtocol.Tspl, Copies = 2, FeedLines = 3
        };

        var error = Assert.Throws<TemplateRenderException>(() => Render(Template(Row(Text("OK"))), options: options));
        Assert.Contains("FEED", error.Message);
        options.FeedLines = 0;
        var result = Render(Template(Row(Text("OK"))), options: options);
        Assert.Contains("PRINT 1,2\r\n", Output(result));
    }

    [Fact]
    public void Template_tspl_media_options_are_emitted_before_content()
    {
        var result = Render(Template(Row(Text("OK"))), options: new TemplateRenderOptions
        {
            Encoding = Encoding.ASCII, Protocol = PrintProtocol.Tspl,
            TsplMedia = new TsplMediaOptions { GapDistance = 3, Direction = 1, Density = 8 }
        });

        var output = Output(result);
        Assert.Contains("CLS\r\nGAP 3,0\r\nDIRECTION 1\r\nDENSITY 8\r\nTEXT", output);
    }

    [Fact]
    public void Template_zpl_media_speed_is_emitted_before_content()
    {
        var result = Render(Template(Row(Text("OK"))), options: new TemplateRenderOptions
        {
            Encoding = Encoding.ASCII, Protocol = PrintProtocol.Zpl,
            ZplMedia = new ZplMediaOptions { Dpi = 300, PrintSpeed = 4 }
        });

        Assert.Contains("^LL200\r\n^PR4\r\n^FO", Output(result));
    }

    [Fact]
    public void Template_page_options_can_disable_esc_pos_feed_and_cut()
    {
        var result = Render(Template(Row(Text("OK"))), options: new TemplateRenderOptions
        {
            Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Native, FeedLines = 0, CutAfterPrint = false
        });

        Assert.EndsWith("OK\n", Encoding.ASCII.GetString(result.Pages[0].Data));
    }

    [Fact]
    public void Template_esc_pos_device_options_are_emitted_before_content()
    {
        var result = Render(Template(Row(Text("OK"))), options: new TemplateRenderOptions
        {
            Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Native,
            EscPosDevice = new EscPosDeviceOptions { CodePageIndex = 17, PrintAreaWidthUnits = 384 }
        });

        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 17, 0x1D, 0x57, 0x80, 0x01 },
            result.Pages[0].Data.Take(9).ToArray());
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Unsupported_template_page_feed_is_rejected(PrintProtocol protocol)
    {
        var error = Assert.Throws<TemplateRenderException>(() => Render(Template(Row(Text("OK"))),
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = protocol, FeedLines = 2 }));

        Assert.Equal(protocol == PrintProtocol.Cpcl ? "Template" : "Pages[0]", error.SourcePath);
    }

    [Fact]
    public void Template_zpl_defaults_to_utf8_and_declares_encoding()
    {
        var result = Render(Template(Row(Text("中文"))), options: new TemplateRenderOptions { Protocol = PrintProtocol.Zpl });

        Assert.StartsWith("^XA\r\n^CI28\r\n", Output(result));
        Assert.Contains("^FD中文^FS", Output(result));
    }

    [Fact]
    public void Template_tspl_lines_use_legal_bar_dimensions_and_normalize_reversed_axes()
    {
        var horizontal = new BpLineElement { LineWidth = 3,
            StartCoordinate = new BpCoordinate { X = 20, Y = 10 },
            LineEndCoordinate = new BpCoordinate { X = 2, Y = 10 } };
        var vertical = new BpLineElement { LineWidth = 3,
            StartCoordinate = new BpCoordinate { X = 40, Y = 35 },
            LineEndCoordinate = new BpCoordinate { X = 40, Y = 5 } };
        var diagonal = new BpLineElement { LineWidth = 2,
            StartCoordinate = new BpCoordinate { X = 70, Y = 50 },
            LineEndCoordinate = new BpCoordinate { X = 50, Y = 30 } };
        var output = Output(Render(Template(Row(horizontal, vertical, diagonal)),
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Tspl,
                TsplMedia = new TsplMediaOptions { AllowRasterizedLines = true } }));

        Assert.Contains("BAR 2,10,19,3\r\n", output);
        Assert.Contains("BAR 40,5,3,31\r\n", output);
        Assert.DoesNotContain("BAR 40,5,3,31,3", output);
        Assert.Contains("BITMAP", output);
    }

    [Fact]
    public void Template_tspl_ellipse_and_icon_render_diagonal_segments_as_bitmaps()
    {
        var ellipse = new BpEllipseElement { Width = 40, Height = 24, LineWidth = 2 };
        var icon = new BpIconElement { Icon = "check", Size = 32, LineWidth = 2 };
        var template = Template(Row(ellipse, icon));
        template.Version = 2;
        var output = Output(Render(template,
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Tspl,
                TsplMedia = new TsplMediaOptions { AllowRasterizedLines = true } }));

        Assert.True(output.Split(new[] { "BITMAP " }, StringSplitOptions.None).Length > 3);
    }

    [Fact]
    public void Template_zpl_ellipse_and_icon_render_diagonal_segments_as_graphics()
    {
        var template = Template(Row(
            new BpEllipseElement { Width = 40, Height = 24, LineWidth = 1 },
            new BpIconElement { Icon = "check", Size = 32, LineWidth = 1 }));
        template.Version = 2;

        var output = Output(Render(template,
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Zpl,
                ZplMedia = new ZplMediaOptions { AllowRasterizedLines = true } }));

        Assert.True(output.Split(new[] { "^GFA," }, StringSplitOptions.None).Length > 3);
        Assert.DoesNotContain("^GB40,24", output);
    }

    [Fact]
    public void Template_can_render_origin_text_to_esc_pos_native()
    {
        var result = Render(Template(Row(Text("HELLO"))),
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos, EscPosLayout = EscPosLayoutMode.Native });

        Assert.Contains("HELLO\n", Encoding.ASCII.GetString(result.Pages[0].Data));
    }

    [Fact]
    public void Template_esc_pos_native_renders_consecutive_text_rows()
    {
        var template = Template(Row(Text("FIRST")), Row(Text("SECOND")));
        var result = Render(template, options: new TemplateRenderOptions {
            Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Native, FeedLines = 0, CutAfterPrint = false
        });

        Assert.EndsWith("FIRST\nSECOND\n", Encoding.ASCII.GetString(result.Pages[0].Data));
    }

    [Fact]
    public void Unsupported_native_border_reports_protocol_and_template_path()
    {
        var row = Row(Text("HELLO"));
        row.Border = new BpBorder { TopStyle = 1 };

        var error = Assert.Throws<TemplateRenderException>(() => Render(Template(row),
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Native }));

        Assert.Contains("Rows[0]", error.SourcePath);
        Assert.Contains(PrintProtocol.EscPos.ToString(), error.Message);
    }

    [Fact]
    public void Template_can_render_text_to_esc_pos_raster()
    {
        var result = Render(Template(Row(Text("HELLO"))),
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos, EscPosLayout = EscPosLayoutMode.Raster });

        var data = result.Pages[0].Data;
        Assert.Equal(new byte[] { 0x1D, 0x76, 0x30, 0x00 }, data.Skip(2).Take(4).ToArray());
        Assert.Contains(data.Skip(10).Take(72 * 200), value => value != 0);
    }

    [Fact]
    public void Template_can_render_codes_to_esc_pos_raster()
    {
        var template = Template(
            Row(new BpBarcodeElement { BarcodeType = BarcodeType.Code128, LineWidth = 1, Height = 30,
                Field = new BgField { Value = "123456" } }),
            Row(new BpQrCodeElement { UnitWidth = 2, Field = new BgField { Value = "hello" } }));
        var result = Render(template,
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos, EscPosLayout = EscPosLayoutMode.Raster });

        Assert.Contains(result.Pages[0].Data.Skip(10), value => value != 0);
    }

    [Fact]
    public void Template_images_are_encoded_by_coordinate_providers()
    {
        using var image = new Image<Rgba32>(8, 1, new Rgba32(0, 0, 0));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        var template = Template(Row(new BpImageElement { Field = new BgField { Value = "logo" }, Width = 8, Height = 1 }));
        var provider = new Resources(stream.ToArray());

        var renderer = new TemplateRenderer();
        var tspl = renderer.Render(template, null, provider, new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Tspl });
        var zpl = renderer.Render(template, null, new Resources(stream.ToArray()), new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Zpl });

        Assert.Contains("BITMAP", Output(tspl));
        Assert.Contains("^GFA", Output(zpl));
    }

    [Fact]
    public void Template_images_are_composed_by_esc_pos_raster()
    {
        using var image = new Image<Rgba32>(8, 1, new Rgba32(0, 0, 0));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        var template = Template(Row(new BpImageElement { Field = new BgField { Value = "logo" }, Width = 8, Height = 1 }));
        var result = new TemplateRenderer().Render(template, null, new Resources(stream.ToArray()),
            new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos, EscPosLayout = EscPosLayoutMode.Raster });

        Assert.Contains((byte)0x1D, result.Pages[0].Data);
        Assert.Contains((byte)0x76, result.Pages[0].Data);
    }

    [Fact]
    public void Conditions_and_root_item_index_bindings_work()
    {
        var detail = Row(Text(name: "$root.Title"), Text(name: "$index"), Text(name: "$item.Name"));
        detail.RepeatSource = "Items";
        detail.Condition = new BpCondition { Operator = "and", Conditions = new List<BpCondition> {
            new BpCondition { Path = "Price", Operator = "ge", Value = 10m },
            new BpCondition { Operator = "or", Conditions = new List<BpCondition> {
                new BpCondition { Path = "Name", Operator = "eq", Value = "visible" },
                new BpCondition { Path = "Name", Operator = "isNull" }
            } }
        } };
        var result = Render(Template(detail), new { Title = "ROOT", Items = new[] { new { Name = "hidden", Price = 5 }, new { Name = "visible", Price = 20 } } });
        Assert.Contains("ROOT", Output(result)); Assert.Contains("visible", Output(result)); Assert.DoesNotContain("hidden", Output(result));
        Assert.Contains(" 1\r\n", Output(result));
    }

    [Fact]
    public void Missing_fields_fail_but_null_fields_render_empty()
    {
        var template = Template(Row(Text(name: "Name")));
        Assert.Throws<ArgumentException>(() => Render(template, new { Other = "x" }));
        Assert.NotEmpty(Render(template, new Dictionary<string, object> { ["Name"] = null }).Pages);
    }

    [Fact]
    public void Numeric_comparisons_do_not_coerce_strings()
    {
        var row = Row(Text()); row.Condition = new BpCondition { Path = "Price", Operator = "gt", Value = 10 };
        Assert.Throws<ArgumentException>(() => Render(Template(row), new { Price = "20" }));
    }

    [Fact]
    public void Delegate_has_priority_and_cannot_be_serialized()
    {
        var text = Text("static", "missing"); text.Field.ValueFunc = _ => "delegate";
        var template = Template(Row(text));
        Assert.Contains("delegate", Output(Render(template)));
        Assert.Throws<ArgumentException>(() => new TemplateLoader().SaveJson(template));
    }

    [Theory]
    [InlineData("{\"Version\":4,\"Width\":100,\"Height\":100}")]
    [InlineData("{\"Version\":1,\"Width\":100,\"Height\":100,\"$type\":\"System.Object\"}")]
    [InlineData("{\"Version\":1,\"Width\":100,\"Height\":100,\"ValueFunc\":\"code\"}")]
    [InlineData("{\"Version\":1,\"Version\":1,\"Width\":100,\"Height\":100}")]
    public void Unsafe_json_is_rejected(string json) => Assert.ThrowsAny<Exception>(() => new TemplateLoader().LoadJson(json));

    [Fact]
    public void Legacy_elements_normalize_only_once()
    {
        var json = "{\"Version\":1,\"Width\":100,\"Height\":100,\"Rows\":[{\"Cells\":[{\"ElementAlls\":[{\"FontElement\":{\"Field\":{\"Value\":\"legacy\"}}}]}]}]}";
        var template = new TemplateLoader().LoadJson(json);
        Assert.Single(template.Rows[0].Cells[0].Elements);
        Assert.Null(template.Rows[0].Cells[0].ElementAlls);
        Assert.Contains("legacy", Output(Render(template)));
    }

    [Fact]
    public void Nested_cells_use_relative_coordinates_and_borders()
    {
        var nested = Row(Text("nested"));
        var parent = new BpRow { Cells = new List<BpCell> { new BpCell { Width = 100 }, new BpCell {
            Rows = new List<BpRow> { nested }, Border = new BpBorder { TopStyle = 1, RightStyle = 2 }
        } } };
        var output = Output(Render(Template(parent)));
        Assert.Contains("T 24 0 100 0 nested", output);
        Assert.Contains("L 100 0 575 0 1", output);
    }

    [Fact]
    public void Unicode_text_elements_are_not_split_and_newlines_are_layout()
    {
        var template = Template(Row(Text("A😀B\nPRINT"))); template.Width = 60;
        var output = Output(Render(template));
        Assert.Contains("A😀B", output);
        Assert.Contains("T 24 0 0 26 PRINT", output);
        Assert.Equal(1, output.Split(new[] { "\r\nPRINT\r\n" }, StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Fit_selects_smaller_font_before_no_wrap_validation()
    {
        var text = Text("abcd"); text.Field.NeedFeedLine = false; text.Field.Fit = true;
        var template = Template(Row(text)); template.Width = 32;
        Assert.Contains("T 55 0", Output(Render(template)));
    }

    [Fact]
    public void Oversize_atomic_element_fails_without_partial_result()
    {
        var template = Template(Row(new BpQrCodeElement { UnitWidth = 4, Field = new BgField { Value = "x" } })); template.Height = 30;
        Assert.Throws<ArgumentException>(() => Render(template));
    }

    [Fact]
    public void Cycles_and_depth_limits_are_rejected()
    {
        var row = Row(); row.Cells[0].Rows = new List<BpRow> { row };
        Assert.Throws<ArgumentException>(() => Render(Template(row)));
        row.Cells[0].Rows = new List<BpRow> { Row(Text()) };
        var options = Options(); options.MaxDepth = 1;
        Assert.Throws<ArgumentException>(() => Render(Template(row), options: options));
        var condition = new BpCondition { Operator = "and" }; condition.Conditions = new List<BpCondition> { condition };
        row.Condition = condition;
        Assert.Throws<ArgumentException>(() => Render(Template(row)));
    }

    [Fact]
    public void Limits_and_cancellation_are_enforced()
    {
        var template = Template(Row(Text()));
        var options = Options(); options.MaxOutputBytes = 1;
        Assert.Throws<ArgumentException>(() => Render(template, options: options));
        options = Options(); options.MaxJsonBytes = 1;
        Assert.Throws<ArgumentException>(() => new TemplateLoader().LoadJson("{}", options));
        var repeat = Row(Text(name: "$item")); repeat.RepeatSource = "$root";
        options = Options(); options.MaxDetailRows = 1;
        Assert.Throws<ArgumentException>(() => Render(Template(repeat), new[] { "a", "b" }, options));
        options = Options(); options.MaxPages = 1;
        var tall = Template(Row(Text("a\nb\nc"))); tall.Height = 25;
        Assert.Throws<ArgumentException>(() => Render(tall, options: options));
        Assert.Throws<OperationCanceledException>(() => new TemplateRenderer().Render(template, null, options: Options(), cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void Image_limits_and_resource_disposal_are_enforced()
    {
        using var image = new Image<Rgba32>(10, 10);
        using var stream = new MemoryStream(); image.SaveAsPng(stream);
        var resources = new Resources(stream.ToArray());
        var template = Template(Row(new BpImageElement { Field = new BgField { Value = "key" } }));
        var options = Options(); options.MaxImageBytes = 1;
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(template, null, resources, options));
        Assert.True(resources.LastStreamDisposed);
        options = Options(); options.MaxImagePixels = 99;
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(template, null, resources, options));
    }

    [Fact]
    public void Compressed_images_preserve_binary_data()
    {
        using var image = new Image<Rgba32>(8, 1, new Rgba32(0, 0, 0));
        using var stream = new MemoryStream(); image.SaveAsPng(stream);
        var template = Template(Row(new BpImageElement { IsCompress = true, Field = new BgField { Value = "key" } }));
        var result = new TemplateRenderer().Render(template, null, new Resources(stream.ToArray()), Options());
        var bytes = result.Pages[0].Data;
        var prefix = Encoding.ASCII.GetBytes("CG 1 1 0 0 ");
        var start = Encoding.GetEncoding(28591).GetString(bytes).IndexOf("CG 1 1 0 0 ", StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.Equal(255, bytes[start + prefix.Length]);
        Assert.Equal(13, bytes[start + prefix.Length + 1]);
    }

    [Fact]
    public void Parallel_columns_keep_every_text_line_exactly_once()
    {
        var row = new BpRow { Cells = new List<BpCell> {
            new BpCell { Width = 120, Elements = new List<BpElement> { Text("LEFT1\nLEFT2\nLEFT3") } },
            new BpCell { Width = 120, Elements = new List<BpElement> { Text("RIGHT1\nRIGHT2") } }
        } };
        var template = Template(row); template.Height = 50;
        var result = Render(template);
        Assert.Equal(2, result.PageCount);
        var output = Output(result);
        foreach (var marker in new[] { "LEFT1", "LEFT2", "LEFT3", "RIGHT1", "RIGHT2" })
            Assert.Equal(1, output.Split(new[] { marker }, StringSplitOptions.None).Length - 1);
        Assert.Contains(" 120 26 RIGHT2", Encoding.UTF8.GetString(result.Pages[0].Data));
    }

    [Fact]
    public void Shared_template_can_render_independently_in_parallel()
    {
        var template = Template(Row(Text(name: "Name")));
        var renderer = new TemplateRenderer();
        System.Threading.Tasks.Parallel.For(0, 20, index => {
            var result = renderer.Render(template, new { Name = "name-" + index }, options: Options());
            Assert.Contains("name-" + index + "\r\n", Output(result));
        });
        Assert.Equal("Name", template.Rows[0].Cells[0].Elements[0].Field.Name);
    }

    [Theory]
    [InlineData("eq", 10, true)]
    [InlineData("ne", 10, false)]
    [InlineData("gt", 9, true)]
    [InlineData("ge", 10, true)]
    [InlineData("lt", 11, true)]
    [InlineData("le", 10, true)]
    public void Comparison_operators_match_json_numeric_constants(string operation, int right, bool expected)
    {
        var row = Row(Text("MATCH")); row.Condition = new BpCondition { Path = "Number", Operator = operation, Value = right };
        var loader = new TemplateLoader();
        var template = loader.LoadJson(loader.SaveJson(Template(row)));
        Assert.Equal(expected, Output(Render(template, new { Number = 10m })).Contains("MATCH"));
    }

    [Fact]
    public void Image_moves_to_next_page_as_a_whole()
    {
        using var image = new Image<Rgba32>(8, 30);
        using var stream = new MemoryStream(); image.SaveAsPng(stream);
        var template = Template(Row(Text("before")), Row(new BpImageElement { Field = new BgField { Value = "key" } }));
        template.Height = 40;
        var result = new TemplateRenderer().Render(template, null, new Resources(stream.ToArray()), Options());
        Assert.Equal(2, result.PageCount);
        Assert.DoesNotContain("EG ", Encoding.UTF8.GetString(result.Pages[0].Data));
        Assert.Contains("EG 1 30 0 0", Encoding.UTF8.GetString(result.Pages[1].Data));
    }

    private sealed class Resources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;
        private MemoryStream last;
        internal int OpenCount;
        internal bool LastStreamDisposed => last != null && !last.CanRead;
        internal Resources(byte[] bytes) { this.bytes = bytes; }
        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken) { OpenCount++; return last = new MemoryStream(bytes, false); }
    }
}
