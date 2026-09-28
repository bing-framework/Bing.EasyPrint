using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class TemplateLayoutTests
{
    [Fact]
    public void Layout_capture_keeps_print_bytes_and_maps_repeated_rows_across_pages()
    {
        var header = Row(new BpFontElement { FontSize = 24, Field = new BgField { Value = "ITEMS" } });
        header.IsHeader = true;
        var detail = Row(new BpFontElement { FontSize = 24, Field = new BgField { Name = "Name" } });
        detail.RepeatSource = "Items";
        var template = new BpTemplate { Width = 192, Height = 60, Rows = new List<BpRow> { header, detail } };
        var data = new { Items = new[] { new { Name = "one" }, new { Name = "two" }, new { Name = "three" } } };
        var renderer = new TemplateRenderer();
        var options = new TemplateRenderOptions { Encoding = Encoding.UTF8 };

        var printed = renderer.Render(template, data, options: options);
        var traced = renderer.RenderWithLayout(template, data, options: options);

        Assert.Equal(printed.PageCount, traced.PageCount);
        Assert.True(traced.PageCount > 1);
        for (var page = 0; page < printed.PageCount; page++)
        {
            Assert.Equal(printed.Pages[page].Data, traced.Pages[page].Data);
            Assert.Equal(template.Width, traced.Pages[page].Width);
            Assert.All(traced.Pages[page].Elements, element =>
            {
                Assert.InRange(element.X, 0, template.Width);
                Assert.InRange(element.Width, 0, template.Width - element.X);
                Assert.True(element.Height >= 0);
            });
            Assert.Contains(traced.Pages[page].Elements, element => element.SourcePath == "Rows[0].Cells[0].Elements[0]" && element.Text == "ITEMS");
        }

        var details = traced.Pages.SelectMany(page => page.Elements)
            .Where(element => element.SourcePath == "Rows[1].Cells[0].Elements[0]" && element.Kind == "element")
            .ToArray();
        Assert.Equal(new[] { "one", "two", "three" }, details.Select(element => element.Text));
        Assert.Equal(new[] { 0, 1, 2 }, details.Select(element => element.InstanceIndices.Single()));
        Assert.All(details, element => Assert.Equal(1, element.Type));
    }

    [Fact]
    public void Layout_capture_reports_cell_and_row_bounds_for_fixed_template()
    {
        var template = new BpTemplate
        {
            Width = 200,
            Height = 100,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 32,
                    Cells = new List<BpCell>
                    {
                        new BpCell { Width = 80, Elements = new List<BpElement> { new BpFontElement { FontSize = 24, Field = new BgField { Value = "A" } } } },
                        new BpCell { Width = 120, Elements = new List<BpElement> { new BpLineElement { StartCoordinate = new BpCoordinate { X = 0, Y = 0 }, LineEndCoordinate = new BpCoordinate { X = 119, Y = 0 } } } }
                    }
                }
            }
        };

        var page = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions { Encoding = Encoding.UTF8 }).Pages.Single();

        var row = Assert.Single(page.Elements.Where(element => element.Kind == "row"));
        Assert.Equal((0, 0, 200, 32), (row.X, row.Y, row.Width, row.Height));
        Assert.Equal(2, page.Elements.Count(element => element.Kind == "cell"));
        Assert.Contains(page.Elements, element => element.SourcePath == "Rows[0].Cells[1].Elements[0]" && element.Type == 5);
    }

    [Fact]
    public void Layout_capture_attaches_template_path_to_binding_errors()
    {
        var template = new BpTemplate
        {
            Width = 120,
            Height = 80,
            Rows = new List<BpRow> { Row(new BpFontElement { FontSize = 24, Field = new BgField { Name = "Missing" } }) }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(template,
            new { Present = "value" }, options: new TemplateRenderOptions { Encoding = Encoding.UTF8 }));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("字段不存在", error.Message);
    }

    private static BpRow Row(params BpElement[] elements) => new BpRow
    {
        Cells = new List<BpCell> { new BpCell { Elements = elements.ToList() } }
    };
}
