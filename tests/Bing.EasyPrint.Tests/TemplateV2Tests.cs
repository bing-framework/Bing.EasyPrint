using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;
using ZXing;
using ZXing.Common;

namespace Bing.EasyPrint.Tests;

public class TemplateV2Tests
{
    private static BpRow Row(string section, params BpElement[] elements) => new BpRow
    {
        Section = section,
        Cells = new List<BpCell> { new BpCell { Elements = elements.ToList() } }
    };

    private static BpFontElement Text(string value = null, string path = null) => new BpFontElement
    {
        FontSize = 24, Field = new BgField { Value = value, Name = path }
    };

    private static string Output(TemplateRenderResult result) => string.Concat(result.Pages.Select(page => Encoding.UTF8.GetString(page.Data)));

    [Fact]
    public void Version_two_sections_and_variables_survive_json_round_trip()
    {
        var template = new BpTemplate { Version = 2, Width = 200, Height = 80,
            Variables = new Dictionary<string, string> { ["shop"] = "Store.Name" },
            Rows = new List<BpRow> {
                Row("PageHeader", Text(path: "$var.shop")),
                Row("Body", Text(path: "Name")),
                Row("PageFooter", Text("FOOT"))
            } };
        template.Rows[1].RepeatSource = "Items";
        var data = new { Store = new { Name = "SHOP" }, Items = new[] { new { Name = "One" }, new { Name = "Two" }, new { Name = "Three" } } };
        var options = new TemplateRenderOptions { Encoding = Encoding.UTF8 };
        var loader = new TemplateLoader();
        var copy = loader.LoadJson(loader.SaveJson(template, options), options);
        var renderer = new TemplateRenderer();
        var original = renderer.Render(template, data, options: options);
        var restored = renderer.Render(copy, data, options: options);
        Assert.Equal(original.PageCount, restored.PageCount);
        Assert.True(original.PageCount >= 2);
        Assert.Equal(original.Pages.Select(page => page.Data), restored.Pages.Select(page => page.Data));
        Assert.All(original.Pages, page => { var text = Encoding.UTF8.GetString(page.Data); Assert.Contains("SHOP", text); Assert.Contains("FOOT", text); });
        Assert.Contains("Three", Output(original));
    }

    [Fact]
    public void Document_footer_is_emitted_after_details_even_when_declared_first()
    {
        var template = new BpTemplate { Version = 2, Width = 200, Height = 120,
            Rows = new List<BpRow> { Row("DocumentFooter", Text("END")), Row("Body", Text("DETAIL")) } };
        var output = Output(new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions { Encoding = Encoding.UTF8 }));
        Assert.True(output.IndexOf("DETAIL", StringComparison.Ordinal) < output.IndexOf("END", StringComparison.Ordinal));
    }

    [Fact]
    public void Reusable_group_and_shapes_have_layout_and_cycle_validation()
    {
        var template = new BpTemplate { Version = 2, Width = 200, Height = 100,
            Components = new List<BpComponentDefinition> { new BpComponentDefinition { Id = "badge", Width = 70, Height = 40,
                Elements = new List<BpElement> { new BpRectangleElement { Width = 70, Height = 40 },
                    new BpIconElement { Icon = "check", Size = 20, StartCoordinate = new BpCoordinate { X = 4, Y = 4 } } } } },
            Rows = new List<BpRow> { Row("Body", new BpGroupElement { ComponentId = "badge" },
                new BpEllipseElement { Width = 30, Height = 20, StartCoordinate = new BpCoordinate { X = 85, Y = 0 } }) } };
        var options = new TemplateRenderOptions { Encoding = Encoding.UTF8 };
        var loader = new TemplateLoader();
        var result = new TemplateRenderer().RenderWithLayout(loader.LoadJson(loader.SaveJson(template, options), options), null, options: options);
        Assert.Contains("L 0 0 69 0 1", Output(result));
        Assert.Contains(result.Pages[0].Elements, item => item.Type == 6);
        Assert.Contains(result.Pages[0].Elements, item => item.Type == 8);
        template.Components[0].Elements.Add(new BpGroupElement { ComponentId = "badge" });
        Assert.Throws<ArgumentException>(() => loader.SaveJson(template, options));
    }

    [Fact]
    public void Group_moves_whole_to_next_page_and_rejects_oversize()
    {
        var group = new BpGroupElement { Width = 60, Height = 50,
            Elements = new List<BpElement> { new BpRectangleElement { Width = 60, Height = 50 } } };
        var first = Row("Body", Text("FIRST")); first.Height = 70;
        var template = new BpTemplate { Version = 2, Width = 120, Height = 100,
            Rows = new List<BpRow> { first, Row("Body", group) } };
        var result = new TemplateRenderer().RenderWithLayout(template, null, options: new TemplateRenderOptions { Encoding = Encoding.UTF8 });
        Assert.Equal(2, result.PageCount);
        Assert.DoesNotContain(result.Pages[0].Elements, element => element.Type == 6);
        Assert.Contains(result.Pages[1].Elements, element => element.Type == 6);
        group.Height = 110;
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(template, null));
    }

    [Fact]
    public void Table_factory_renders_repeated_details()
    {
        var rows = TemplateTableFactory.Create("Items", new[] {
            new TemplateTableColumn { Title = "Item", FieldPath = "Name", Width = 100 },
            new TemplateTableColumn { Title = "Qty", FieldPath = "Quantity", Width = 50 }
        });
        var template = new BpTemplate { Version = 2, Width = 150, Height = 90, Rows = rows.ToList() };
        var result = new TemplateRenderer().Render(template, new { Items = new[] { new { Name = "Tea", Quantity = 2 }, new { Name = "Milk", Quantity = 3 } } },
            options: new TemplateRenderOptions { Encoding = Encoding.UTF8 });
        Assert.Contains("Tea", Output(result));
        Assert.Contains("Milk", Output(result));
        Assert.All(result.Pages, page => Assert.Contains("Item", Encoding.UTF8.GetString(page.Data)));
    }

    [Fact]
    public void Table_header_stays_with_its_details_and_not_later_pages()
    {
        var rows = new List<BpRow> { Row("Body", Text("INTRO")) };
        rows[0].Height = 50;
        rows.AddRange(TemplateTableFactory.Create("Items", new[] {
            new TemplateTableColumn { Title = "TABLE-HEAD", FieldPath = "Name", Width = 160 }
        }));
        rows.Add(Row("Body", Text("AFTER")));
        rows[3].Height = 24;
        rows.Add(Row("Body", Text("TAIL")));
        var template = new BpTemplate { Version = 2, Width = 160, Height = 90, Rows = rows };
        var data = new { Items = new[] { new { Name = "One" }, new { Name = "Two" }, new { Name = "Three" } } };
        var result = new TemplateRenderer().Render(template, data, options: new TemplateRenderOptions { Encoding = Encoding.UTF8 });
        Assert.True(result.PageCount >= 3);
        Assert.DoesNotContain("TABLE-HEAD", Encoding.UTF8.GetString(result.Pages[0].Data));
        Assert.Contains("TABLE-HEAD", Encoding.UTF8.GetString(result.Pages[1].Data));
        Assert.DoesNotContain("TABLE-HEAD", Encoding.UTF8.GetString(result.Pages.Last().Data));
        Assert.Contains("TAIL", Encoding.UTF8.GetString(result.Pages.Last().Data));

        var empty = new TemplateRenderer().Render(template, new { Items = Array.Empty<object>() },
            options: new TemplateRenderOptions { Encoding = Encoding.UTF8 });
        Assert.DoesNotContain("TABLE-HEAD", Output(empty));
    }

    [Fact]
    public void Table_header_reserves_height_for_overlapping_detail_elements()
    {
        var intro = Row("Body", Text("INTRO")); intro.Height = 35;
        var detail = Row("Body", Text(path: "Name"), new BpRectangleElement { Width = 20, Height = 35,
            StartCoordinate = new BpCoordinate { X = 70, Y = 0 } });
        detail.RepeatSource = "Items";
        var template = new BpTemplate { Version = 2, Width = 120, Height = 90,
            Rows = new List<BpRow> { intro, Row("TableHeader", Text("HEAD")), detail } };
        var pages = new TemplateRenderer().Render(template, new { Items = new[] { new { Name = "ITEM" } } },
            options: new TemplateRenderOptions { Encoding = Encoding.UTF8 }).Pages;
        Assert.Equal(2, pages.Count);
        Assert.DoesNotContain("HEAD", Encoding.UTF8.GetString(pages[0].Data));
        Assert.Contains("HEAD", Encoding.UTF8.GetString(pages[1].Data));
        Assert.Contains("ITEM", Encoding.UTF8.GetString(pages[1].Data));
    }

    [Fact]
    public void Document_sections_and_root_variables_have_expected_scope_across_pages()
    {
        var template = new BpTemplate { Version = 2, Width = 160, Height = 80,
            Variables = new Dictionary<string, string> { ["shop"] = "Shop" },
            Rows = new List<BpRow> {
                Row("DocumentHeader", Text("ONCE-TOP")),
                new BpRow { Section = "Body", RepeatSource = "Items", Height = 45, Cells = new List<BpCell> {
                    new BpCell { Elements = new List<BpElement> { Text(path: "$var.shop"),
                        new BpFontElement { FontSize = 24, Field = new BgField { Name = "Name" },
                            StartCoordinate = new BpCoordinate { X = 75, Y = 0 } } } } } },
                Row("DocumentFooter", Text("ONCE-END"))
            } };
        var pages = new TemplateRenderer().Render(template, new { Shop = "STORE", Items = new[] {
            new { Name = "A" }, new { Name = "B" }, new { Name = "C" } } },
            options: new TemplateRenderOptions { Encoding = Encoding.UTF8 }).Pages;
        var text = string.Concat(pages.Select(page => Encoding.UTF8.GetString(page.Data)));
        Assert.True(pages.Count >= 2);
        Assert.Equal(1, text.Split(new[] { "ONCE-TOP" }, StringSplitOptions.None).Length - 1);
        Assert.Equal(1, text.Split(new[] { "ONCE-END" }, StringSplitOptions.None).Length - 1);
        Assert.Equal(3, text.Split(new[] { "STORE" }, StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Designer_loader_locates_invalid_rows_and_elements_without_changing_regular_loader_errors()
    {
        var loader = new TemplateLoader();
        const string header = "{\"Version\":2,\"Width\":120,\"Height\":100,\"Rows\":[{\"Section\":\"TableHeader\",\"Cells\":[]}]}";
        Assert.Equal("Rows[0]", Assert.Throws<TemplateRenderException>(() => loader.LoadJsonWithPaths(header)).SourcePath);
        Assert.Throws<ArgumentException>(() => loader.LoadJson(header));

        const string shape = "{\"Version\":2,\"Width\":120,\"Height\":100,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":7,\"Width\":0,\"Height\":20}]}]}]}";
        Assert.Equal("Rows[0].Cells[0].Elements[0]",
            Assert.Throws<TemplateRenderException>(() => loader.LoadJsonWithPaths(shape)).SourcePath);

        const string component = "{\"Version\":2,\"Width\":120,\"Height\":100,\"Components\":[{\"Id\":\"bad\",\"Width\":0,\"Height\":20,\"Elements\":[]}],\"Rows\":[]}";
        Assert.Equal("Components[0]",
            Assert.Throws<TemplateRenderException>(() => loader.LoadJsonWithPaths(component)).SourcePath);
    }

    [Fact]
    public void Designer_layout_locates_an_element_that_cannot_fit_on_one_page()
    {
        var template = new BpTemplate { Version = 2, Width = 120, Height = 100,
            Rows = new List<BpRow> { Row("Body", new BpRectangleElement { Width = 40, Height = 110 }) } };
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions { Encoding = Encoding.UTF8 }));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    [Theory]
    [InlineData("DataMatrix")]
    [InlineData("PDF417")]
    public void Matrix_codes_render_as_bounded_bitmaps(string format)
    {
        var template = new BpTemplate { Version = 2, Width = 300, Height = 150,
            Rows = new List<BpRow> { Row("Body", new BpMatrixCodeElement { Format = format, Width = format == "PDF417" ? 256 : 120, Height = 80,
                Field = new BgField { Value = "ORDER-12345" } }) } };
        var options = new TemplateRenderOptions { Encoding = Encoding.UTF8 };
        var loader = new TemplateLoader();
        var copy = loader.LoadJson(loader.SaveJson(template, options), options);
        var original = new TemplateRenderer().RenderWithLayout(template, null, options: options);
        var restored = new TemplateRenderer().Render(copy, null, options: options);
        Assert.Equal(original.Pages[0].Data, restored.Pages[0].Data);
        Assert.Contains("EG ", Output(original));
        Assert.Contains(original.Pages[0].Elements, element => element.Type == 10);
        template.Rows[0].Cells[0].Elements[0].Field.Value = "ORDER\nPRINT";
        Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(template, null, options: options));
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions { MaxImagePixels = 100 }));
    }

    [Theory]
    [InlineData("DataMatrix", 120, 80)]
    [InlineData("PDF417", 256, 80)]
    public void Matrix_code_cpcl_bitmap_decodes_to_original_text(string format, int width, int height)
    {
        const string value = "ORDER-12345";
        var template = new BpTemplate { Version = 2, Width = 300, Height = 150,
            Rows = new List<BpRow> { Row("Body", new BpMatrixCodeElement { Format = format, Width = width, Height = height,
                Field = new BgField { Value = value } }) } };
        var output = Output(new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions { Encoding = Encoding.UTF8 }));
        var graphic = Regex.Match(output, @"EG (\d+) (\d+) \d+ \d+ ([0-9A-Fa-f]+)");
        Assert.True(graphic.Success);
        var rowBytes = int.Parse(graphic.Groups[1].Value);
        var bitmapHeight = int.Parse(graphic.Groups[2].Value);
        var hex = graphic.Groups[3].Value;
        Assert.Equal(rowBytes * bitmapHeight * 2, hex.Length);
        var pixels = new byte[rowBytes * 8 * bitmapHeight];
        for (var y = 0; y < bitmapHeight; y++) for (var x = 0; x < rowBytes * 8; x++)
        {
            var packed = Convert.ToByte(hex.Substring((y * rowBytes + x / 8) * 2, 2), 16);
            pixels[y * rowBytes * 8 + x] = (packed & (0x80 >> (x % 8))) == 0 ? (byte)255 : (byte)0;
        }
        var luminance = new RGBLuminanceSource(pixels, rowBytes * 8, bitmapHeight, RGBLuminanceSource.BitmapFormat.Gray8);
        var decoded = new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(luminance)));
        Assert.Equal(value, decoded?.Text);
    }
}
