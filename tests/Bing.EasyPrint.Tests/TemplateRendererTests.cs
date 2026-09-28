using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
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
    [InlineData("{\"Version\":3,\"Width\":100,\"Height\":100}")]
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
