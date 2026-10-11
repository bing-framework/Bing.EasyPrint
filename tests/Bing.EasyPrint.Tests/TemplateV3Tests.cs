using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 V3 组合组件、流式布局和分页行为。
/// </summary>
public sealed class TemplateV3Tests
{
    /// <summary>
    /// 保存测试使用的 UTF-8 渲染选项。
    /// </summary>
    private static readonly TemplateRenderOptions Options = new TemplateRenderOptions { Encoding = Encoding.UTF8 };

    /// <summary>
    /// 创建使用字段路径的文字元素。
    /// </summary>
    /// <param name="path">文字字段路径。</param>
    /// <returns>配置了字段绑定的文字元素。</returns>
    private static BpFontElement Text(string path) => new BpFontElement { FontSize = 24, Field = new BgField { Name = path } };

    /// <summary>
    /// 创建包含指定元素的单元格行。
    /// </summary>
    /// <param name="elements">要放入行中的元素。</param>
    /// <returns>包含一个单元格的模板行。</returns>
    private static BpRow Row(params BpElement[] elements) => new BpRow { Cells = new List<BpCell> {
        new BpCell { Elements = elements.ToList() } } };

    /// <summary>
    /// 合并渲染结果中的页面文字。
    /// </summary>
    /// <param name="result">待读取的渲染结果。</param>
    /// <returns>按页面顺序合并的 UTF-8 文本。</returns>
    private static string Output(TemplateRenderResult result)
        => string.Concat(result.Pages.Select(page => Encoding.UTF8.GetString(page.Data)));

    /// <summary>
    /// 验证组件实例在 JSON 往返后仍使用各自的数据和参数。
    /// </summary>
    [Fact]
    public void Component_instances_bind_separate_data_and_parameters_after_json_round_trip()
    {
        var definition = new BpComponentDefinition { Id = "price", Width = 160,
            Parameters = new Dictionary<string, BpComponentParameterDefinition> {
                ["title"] = new BpComponentParameterDefinition { Required = true },
                ["unit"] = new BpComponentParameterDefinition { DefaultValue = JValue.CreateString("each") }
            },
            Elements = new List<BpElement> { Text("Name"),
                new BpFontElement { FontSize = 24, Field = new BgField { Name = "$param.title" },
                    StartCoordinate = new BpCoordinate { X = 50, Y = 0 } },
                new BpFontElement { FontSize = 24, Field = new BgField { Name = "$param.unit" },
                    StartCoordinate = new BpCoordinate { X = 100, Y = 0 } } } };
        var template = new BpTemplate { Version = 3, Width = 160, Height = 100,
            Components = new List<BpComponentDefinition> { definition }, Rows = new List<BpRow> {
                Row(new BpGroupElement { ComponentId = "price", DataPath = "First",
                    Parameters = new Dictionary<string, BpComponentParameterBinding> {
                        ["title"] = new BpComponentParameterBinding { Path = "First.Label" } } }),
                Row(new BpGroupElement { ComponentId = "price", DataPath = "Second",
                    Parameters = new Dictionary<string, BpComponentParameterBinding> {
                        ["title"] = new BpComponentParameterBinding { Value = JValue.CreateString("SALE") },
                        ["unit"] = new BpComponentParameterBinding { Value = JValue.CreateString("kg") } } })
            } };
        var data = new { First = new { Name = "Tea", Label = "NEW" }, Second = new { Name = "Rice", Label = "OLD" } };
        var renderer = new TemplateRenderer();
        var loader = new TemplateLoader();
        var original = renderer.Render(template, data, options: Options);
        var copy = renderer.Render(loader.LoadJson(loader.SaveJson(template, Options), Options), data, options: Options);
        Assert.Equal(original.Pages.Select(page => page.Data), copy.Pages.Select(page => page.Data));
        foreach (var value in new[] { "Tea", "NEW", "each", "Rice", "SALE", "kg" }) Assert.Contains(value, Output(original));
        Assert.DoesNotContain("OLD", Output(original));
    }

    /// <summary>
    /// 验证流式明细会推动尾部内容并重复内部表头。
    /// </summary>
    [Fact]
    public void Flowing_list_pushes_tail_and_internal_table_header_repeats()
    {
        var table = TemplateTableFactory.CreateGroup("Items", new[] {
            new TemplateTableColumn { Title = "PRODUCT", FieldPath = "Name", Width = 160 }
        }, headerHeight: 24, detailHeight: 28);
        var template = new BpTemplate { Version = 3, Width = 160, Height = 80,
            Rows = new List<BpRow> { Row(table, new BpFontElement { FontSize = 24,
                Field = new BgField { Value = "END" } }) } };
        var shortResult = new TemplateRenderer().RenderWithLayout(template,
            new { Items = new[] { new { Name = "A" } } }, options: Options);
        var longResult = new TemplateRenderer().RenderWithLayout(template,
            new { Items = new[] { new { Name = "A" }, new { Name = "B" }, new { Name = "C" } } }, options: Options);
        Assert.True(longResult.PageCount > shortResult.PageCount);
        Assert.Contains("END", Output(longResult));
        Assert.All(longResult.Pages.Where(page => page.Elements.Any(element => element.Text == "A" || element.Text == "B" || element.Text == "C")),
            page => Assert.Contains("PRODUCT", Encoding.UTF8.GetString(page.Data)));
        Assert.DoesNotContain("PRODUCT", Output(new TemplateRenderer().Render(template,
            new { Items = Array.Empty<object>() }, options: Options)));
    }

    /// <summary>
    /// 验证缺少必填参数和组件循环能够报告节点路径。
    /// </summary>
    [Fact]
    public void Missing_required_parameter_and_component_cycle_report_node_paths()
    {
        var definition = new BpComponentDefinition { Id = "one", Width = 100,
            Parameters = new Dictionary<string, BpComponentParameterDefinition> {
                ["required"] = new BpComponentParameterDefinition { Required = true } },
            Elements = new List<BpElement> { Text("$param.required") } };
        var template = new BpTemplate { Version = 3, Width = 100, Height = 100,
            Components = new List<BpComponentDefinition> { definition },
            Rows = new List<BpRow> { Row(new BpGroupElement { ComponentId = "one" }) } };
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(template, null, options: Options));
        Assert.Contains("Rows[0]", error.SourcePath);
        definition.Elements.Add(new BpGroupElement { ComponentId = "one" });
        error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(template, null, options: Options));
        Assert.Contains("Components[0]", error.SourcePath);
    }

    /// <summary>
    /// 验证嵌套组合移动时保留子元素的相对坐标。
    /// </summary>
    [Fact]
    public void Nested_groups_preserve_relative_coordinates_when_parent_moves()
    {
        var group = new BpGroupElement { Width = 120, Height = 50, StartCoordinate = new BpCoordinate { X = 5, Y = 7 },
            Elements = new List<BpElement> { new BpGroupElement { Width = 90, Height = 40,
                StartCoordinate = new BpCoordinate { X = 10, Y = 5 },
                Elements = new List<BpElement> { new BpLineElement { LineWidth = 1,
                    StartCoordinate = new BpCoordinate { X = 3, Y = 4 },
                    LineEndCoordinate = new BpCoordinate { X = 30, Y = 4 } },
                    new BpFontElement { FontSize = 24, Field = new BgField { Value = "MOVE" },
                        StartCoordinate = new BpCoordinate { X = 3, Y = 8 } } } } } };
        var template = new BpTemplate { Version = 3, Width = 160, Height = 100,
            Rows = new List<BpRow> { Row(group) } };
        var first = new TemplateRenderer().RenderWithLayout(template, null, options: Options).Pages[0].Elements;
        group.StartCoordinate.X += 20;
        group.StartCoordinate.Y += 12;
        var second = new TemplateRenderer().RenderWithLayout(template, null, options: Options).Pages[0].Elements;
        foreach (var type in new[] { 1, 5 })
        {
            var before = Assert.Single(first.Where(element => element.Type == type));
            var after = Assert.Single(second.Where(element => element.Type == type));
            Assert.Equal(before.X + 20, after.X);
            Assert.Equal(before.Y + 12, after.Y);
        }
    }

    /// <summary>
    /// 验证组件行能够使用不同的数据项上下文。
    /// </summary>
    [Fact]
    public void Component_rows_can_render_with_distinct_item_contexts()
    {
        var component = new BpComponentDefinition { Id = "list", Rows = new List<BpRow> {
            new BpRow { RepeatSource = "Items", Height = 25, Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> { Text("Name") } } } } } };
        var template = new BpTemplate { Version = 3, Width = 160, Height = 90,
            Components = new List<BpComponentDefinition> { component }, Rows = new List<BpRow> {
                Row(new BpGroupElement { ComponentId = "list", DataPath = "First" }),
                Row(new BpGroupElement { ComponentId = "list", DataPath = "Second" }) } };
        var output = Output(new TemplateRenderer().Render(template, new {
            First = new { Items = new[] { new { Name = "Tea" } } },
            Second = new { Items = new[] { new { Name = "Rice" } } }
        }, options: Options));
        Assert.Contains("Tea", output);
        Assert.Contains("Rice", output);
    }

    /// <summary>
    /// 验证组合内表头随首条明细分页并关联定义路径。
    /// </summary>
    [Fact]
    public void Scoped_table_header_moves_with_first_detail_and_trace_links_definition()
    {
        var component = new BpComponentDefinition { Id = "table", Rows = TemplateTableFactory.Create("Items",
            new[] { new TemplateTableColumn { Title = "HEAD", FieldPath = "Name", Width = 160 } },
            headerHeight: 24, detailHeight: 28).ToList() };
        var intro = Row(new BpFontElement { FontSize = 24, Field = new BgField { Value = "INTRO" } });
        intro.Height = 65;
        var template = new BpTemplate { Version = 3, Width = 160, Height = 80,
            Components = new List<BpComponentDefinition> { component },
            Rows = new List<BpRow> { intro, Row(new BpGroupElement { ComponentId = "table" }) } };
        var result = new TemplateRenderer().RenderWithLayout(template,
            new { Items = new[] { new { Name = "FIRST" }, new { Name = "SECOND" } } }, options: Options);
        Assert.DoesNotContain("HEAD", Encoding.UTF8.GetString(result.Pages[0].Data));
        Assert.Contains("HEAD", Encoding.UTF8.GetString(result.Pages[1].Data));
        Assert.Contains("FIRST", Encoding.UTF8.GetString(result.Pages[1].Data));
        var detail = result.Pages.SelectMany(page => page.Elements).First(element => element.Text == "FIRST");
        Assert.StartsWith("Rows[1].Cells[0].Elements[0].Rows[1]", detail.SourcePath);
        Assert.StartsWith("Components[0].Rows[1]", detail.DefinitionPath);
        Assert.Equal("Rows[1].Cells[0].Elements[0]", detail.ComponentInstancePath);
        Assert.Equal(1, detail.FragmentIndex);
    }

    /// <summary>
    /// 验证并列组件表格在续页同时重复表头。
    /// </summary>
    [Fact]
    public void Parallel_component_tables_repeat_both_headers_on_continuation_pages()
    {
        BpGroupElement Table(string title) => new BpGroupElement { Rows = new List<BpRow> {
            Row(new BpFontElement { FontSize = 24, Field = new BgField { Value = title } }),
            new BpRow { RepeatSource = "Items", Height = 28, Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> { Text("Name") } } } } } };
        var left = Table("LEFT");
        left.Rows[0].Section = "TableHeader";
        left.Rows[0].Height = 24;
        var right = Table("RIGHT");
        right.Rows[0].Section = "TableHeader";
        right.Rows[0].Height = 24;
        var template = new BpTemplate { Version = 3, Width = 240, Height = 80,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Width = 120, Elements = new List<BpElement> { left } },
                new BpCell { Width = 120, Elements = new List<BpElement> { right } } } } } };
        var result = new TemplateRenderer().RenderWithLayout(template,
            new { Items = new[] { new { Name = "A" }, new { Name = "B" }, new { Name = "C" } } }, options: Options);
        Assert.True(result.PageCount > 1);
        Assert.All(result.Pages.Where(page => page.Elements.Any(element => element.Text == "A" || element.Text == "B" || element.Text == "C")),
            page =>
            {
                Assert.Contains("LEFT", Encoding.UTF8.GetString(page.Data));
                Assert.Contains("RIGHT", Encoding.UTF8.GetString(page.Data));
            });
    }

    /// <summary>
    /// 验证并列表格行高不同时表头仍保持对齐。
    /// </summary>
    [Fact]
    public void Unequal_parallel_table_rows_keep_headers_aligned_on_continuation_pages()
    {
        BpGroupElement Table(string title, int detailHeight) => new BpGroupElement { Rows = new List<BpRow> {
            new BpRow { Section = "TableHeader", Height = 24, Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> { new BpFontElement { FontSize = 24,
                    Field = new BgField { Value = title } } } } } },
            new BpRow { RepeatSource = "Items", Height = detailHeight, Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> { Text("Name") } } } } } };
        var template = new BpTemplate { Version = 3, Width = 240, Height = 200,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Width = 120, Elements = new List<BpElement> { Table("LEFT", 24) } },
                new BpCell { Width = 120, Elements = new List<BpElement> { Table("RIGHT", 32) } } } } } };
        var result = new TemplateRenderer().RenderWithLayout(template,
            new { Items = Enumerable.Range(1, 12).Select(index => new { Name = "N" + index }).ToArray() }, options: Options);
        Assert.True(result.PageCount > 1);
        foreach (var page in result.Pages.Skip(1))
        {
            var left = page.Elements.FirstOrDefault(element => element.Text == "LEFT");
            var right = page.Elements.FirstOrDefault(element => element.Text == "RIGHT");
            if (left != null && right != null) Assert.Equal(left.Y, right.Y);
        }
    }

    /// <summary>
    /// 验证显式 null 参数经过 JSON 往返后保持为空值。
    /// </summary>
    [Fact]
    public void Explicit_null_parameter_survives_json_round_trip()
    {
        var template = new BpTemplate { Version = 3, Width = 160, Height = 100,
            Components = new List<BpComponentDefinition> { new BpComponentDefinition { Id = "nullable",
                Parameters = new Dictionary<string, BpComponentParameterDefinition> {
                    ["text"] = new BpComponentParameterDefinition { Required = true } },
                Elements = new List<BpElement> { Text("$param.text"),
                    new BpFontElement { FontSize = 24, Field = new BgField { Value = "END" },
                        StartCoordinate = new BpCoordinate { X = 0, Y = 24 } } } } },
            Rows = new List<BpRow> { Row(new BpGroupElement { ComponentId = "nullable",
                Parameters = new Dictionary<string, BpComponentParameterBinding> {
                    ["text"] = new BpComponentParameterBinding { Value = null } } }) } };
        var loader = new TemplateLoader();
        var json = loader.SaveJson(template, Options);
        Assert.Contains("\"Value\": null", json);
        var loaded = loader.LoadJson(json, Options);
        Assert.Equal(JTokenType.Null, ((BpGroupElement)loaded.Rows[0].Cells[0].Elements[0]).Parameters["text"].Value.Type);
        var result = new TemplateRenderer().RenderWithLayout(loaded, null, options: Options);
        Assert.Contains("END", Output(result));
        Assert.Contains(result.Pages.SelectMany(page => page.Elements), element => element.Type == 1 && element.Text == string.Empty);
        template.Components[0].Parameters["text"].DefaultValue = null;
        ((BpGroupElement)template.Rows[0].Cells[0].Elements[0]).Parameters = null;
        var defaultJson = loader.SaveJson(template, Options);
        Assert.Contains("\"DefaultValue\": null", defaultJson);
        var withDefault = loader.LoadJson(defaultJson, Options);
        Assert.Equal(JTokenType.Null, withDefault.Components[0].Parameters["text"].DefaultValue.Type);
        Assert.Contains("END", Output(new TemplateRenderer().Render(withDefault, null, options: Options)));
    }

    /// <summary>
    /// 验证嵌套组合错误保留最内层节点路径。
    /// </summary>
    [Fact]
    public void Nested_group_error_keeps_the_leaf_source_path()
    {
        var template = new BpTemplate { Version = 3, Width = 160, Height = 100,
            Rows = new List<BpRow> { Row(new BpGroupElement { Elements = new List<BpElement> {
                new BpGroupElement { Elements = new List<BpElement> { Text("Missing") } } } }) } };
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(template, new { }, options: Options));
        Assert.Equal("Rows[0].Cells[0].Elements[0].Elements[0].Elements[0]", error.SourcePath);
    }

    /// <summary>
    /// 验证嵌套共享组件错误保留最内层定义、实例和数据归属。
    /// </summary>
    [Fact]
    public void Nested_component_error_keeps_innermost_ownership()
    {
        var inner = new BpComponentDefinition { Id = "inner", Width = 100,
            Elements = new List<BpElement> { Text("Missing") } };
        var outer = new BpComponentDefinition { Id = "outer", Width = 100,
            Elements = new List<BpElement> { new BpGroupElement { ComponentId = "inner" } } };
        var template = new BpTemplate { Version = 3, Width = 100, Height = 100,
            Components = new List<BpComponentDefinition> { outer, inner },
            Rows = new List<BpRow> { Row(new BpGroupElement { ComponentId = "outer" }) } };

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(template, new { }, options: Options));

        Assert.Equal("Rows[0].Cells[0].Elements[0].Elements[0].Elements[0]", error.SourcePath);
        Assert.Equal("Components[1].Elements[0]", error.DefinitionPath);
        Assert.Equal("Rows[0].Cells[0].Elements[0].Elements[0]", error.ComponentInstancePath);
        Assert.Empty(error.InstanceIndices);
    }

    /// <summary>
    /// 验证共享组件的多个实例发生数据错误时返回出错实例的路径。
    /// </summary>
    [Fact]
    public void Component_data_error_identifies_the_failing_instance()
    {
        var template = new BpTemplate { Version = 3, Width = 100, Height = 100,
            Components = new List<BpComponentDefinition> { new BpComponentDefinition { Id = "item", Width = 100,
                Elements = new List<BpElement> { Text("Name") } } },
            Rows = new List<BpRow> {
                Row(new BpGroupElement { ComponentId = "item", DataPath = "First" }),
                Row(new BpGroupElement { ComponentId = "item", DataPath = "Second" }) } };

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(template,
                new { First = new { Name = "OK" }, Second = new { Other = "BAD" } }, options: Options));

        Assert.Equal("Rows[1].Cells[0].Elements[0].Elements[0]", error.SourcePath);
        Assert.Equal("Components[0].Elements[0]", error.DefinitionPath);
        Assert.Equal("Rows[1].Cells[0].Elements[0]", error.ComponentInstancePath);
        Assert.Empty(error.InstanceIndices);
    }

    /// <summary>
    /// 验证 Provider 生成阶段的能力错误也能回溯到组件定义和实例。
    /// </summary>
    [Fact]
    public void Component_provider_error_keeps_definition_and_instance_ownership()
    {
        var template = new BpTemplate { Version = 3, Width = 160, Height = 100,
            Components = new List<BpComponentDefinition> { new BpComponentDefinition { Id = "label", Width = 160,
                Elements = new List<BpElement> { new BpFontElement { FontSize = 24,
                    Field = new BgField { Value = "BAD\"TEXT" } } } } },
            Rows = new List<BpRow> { Row(new BpGroupElement { ComponentId = "label" }) } };

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(template, null,
                options: new TemplateRenderOptions { Encoding = Encoding.UTF8, Protocol = PrintProtocol.Tspl }));

        Assert.Equal("Rows[0].Cells[0].Elements[0].Elements[0]", error.SourcePath);
        Assert.Equal("Components[0].Elements[0]", error.DefinitionPath);
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ComponentInstancePath);
        Assert.Empty(error.InstanceIndices);
    }

    /// <summary>
    /// 验证展开组件遵守明细上限并响应取消请求。
    /// </summary>
    [Fact]
    public void Expanded_component_checks_detail_limit_and_cancellation()
    {
        var template = new BpTemplate { Version = 3, Width = 160, Height = 100,
            Components = new List<BpComponentDefinition> { new BpComponentDefinition { Id = "lines",
                Rows = new List<BpRow> { new BpRow { RepeatSource = "Items", Height = 24,
                    Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> { Text("Name") } } } } } } },
            Rows = new List<BpRow> { Row(new BpGroupElement { ComponentId = "lines" }) } };
        var data = new { Items = new[] { new { Name = "A" }, new { Name = "B" } } };
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(template, data,
            options: new TemplateRenderOptions { Encoding = Encoding.UTF8, MaxDetailRows = 1 }));
        Assert.Throws<OperationCanceledException>(() => new TemplateRenderer().Render(template, data,
            options: Options, cancellationToken: new System.Threading.CancellationToken(true)));
    }

    /// <summary>
    /// 验证嵌套固定标签和流式表格保留偏移、资源及尾部内容。
    /// </summary>
    [Fact]
    public void Nested_fixed_label_and_flowing_table_keep_offsets_resources_and_footer()
    {
        using var image = new Image<Rgba32>(8, 8, new Rgba32(0, 0, 0));
        using var encoded = new MemoryStream();
        image.SaveAsPng(encoded);
        var resources = new ImageResources(encoded.ToArray());
        var label = new BpGroupElement { Width = 220, Height = 110,
            Elements = new List<BpElement> {
                new BpFontElement { FontSize = 24, Field = new BgField { Value = "LABEL" } },
                new BpLineElement { StartCoordinate = new BpCoordinate { X = 0, Y = 25 },
                    LineEndCoordinate = new BpCoordinate { X = 100, Y = 25 } },
                new BpImageElement { Width = 8, Height = 8, Field = new BgField { Value = "logo" },
                    StartCoordinate = new BpCoordinate { X = 0, Y = 32 } },
                new BpQrCodeElement { UnitWidth = 1, Field = new BgField { Value = "abc" },
                    StartCoordinate = new BpCoordinate { X = 24, Y = 32 } },
                new BpBarcodeElement { LineWidth = 1, Height = 30, Field = new BgField { Value = "123" },
                    StartCoordinate = new BpCoordinate { X = 0, Y = 72 } }
            } };
        var table = TemplateTableFactory.CreateGroup("Items", new[] {
            new TemplateTableColumn { Title = "PRODUCT", FieldPath = "Name", Width = 220 }
        }, headerHeight: 24, detailHeight: 28);
        var intro = Row(new BpFontElement { FontSize = 24, Field = new BgField { Value = "INTRO" } });
        intro.Height = 90;
        var template = new BpTemplate { Version = 3, Width = 240, Height = 160,
            Rows = new List<BpRow> {
                intro,
                Row(new BpGroupElement { Width = 220, Margin = new BpMargin { Left = 10 },
                    Rows = new List<BpRow> { Row(label), Row(table) } }),
                Row(new BpFontElement { FontSize = 24, Field = new BgField { Value = "END" } })
            } };
        var data = new { Items = Enumerable.Range(1, 7).Select(index => new { Name = "ITEM " + index }).ToArray() };
        var loader = new TemplateLoader();
        var before = loader.SaveJson(template, Options);
        var renderer = new TemplateRenderer();
        var first = renderer.RenderWithLayout(template, data, resources, Options);
        var second = renderer.RenderWithLayout(template, data, resources, Options);

        Assert.True(first.PageCount > 1);
        Assert.DoesNotContain(first.Pages[0].Elements, element => element.Type is 2 or 3 or 4 or 5);
        Assert.Equal(new[] { 2, 3, 4, 5 }, first.Pages[1].Elements.Where(element => element.Type is >= 2 and <= 5)
            .Select(element => element.Type).Distinct().OrderBy(type => type));
        Assert.Equal(before, loader.SaveJson(template, Options));
        Assert.Equal(first.Pages.Select(page => page.Data), second.Pages.Select(page => page.Data));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, first.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type is >= 1 and <= 5).Select(element => element.Type).Distinct().OrderBy(type => type));
        Assert.All(first.Pages.SelectMany(page => page.Elements).Where(element => element.Type is 2 or 3 or 4 or 5),
            element => Assert.True(element.X >= 10));
        Assert.Contains(first.Pages.SelectMany(page => page.Elements), element => element.ResourceKey == "logo");
        Assert.Contains("END", Output(first));
        Assert.True(resources.OpenCount >= 2);
    }

    /// <summary>
    /// 验证嵌套组件显式传递参数且实例不修改输入数据。
    /// </summary>
    [Fact]
    public void Nested_component_parameters_are_explicit_and_instances_do_not_mutate_inputs()
    {
        var inner = new BpComponentDefinition { Id = "inner", Width = 150, Height = 24,
            Parameters = new Dictionary<string, BpComponentParameterDefinition> {
                ["caption"] = new BpComponentParameterDefinition { Required = true } },
            Elements = new List<BpElement> { Text("$param.caption") } };
        var outer = new BpComponentDefinition { Id = "outer", Width = 150, Height = 48,
            Parameters = new Dictionary<string, BpComponentParameterDefinition> {
                ["caption"] = new BpComponentParameterDefinition { Required = true } },
            Elements = new List<BpElement> { Text("Name"), new BpGroupElement {
                ComponentId = "inner", StartCoordinate = new BpCoordinate { X = 0, Y = 24 },
                Parameters = new Dictionary<string, BpComponentParameterBinding> {
                    ["caption"] = new BpComponentParameterBinding { Path = "$param.caption" } } } } };
        var template = new BpTemplate { Version = 3, Width = 150, Height = 110,
            Components = new List<BpComponentDefinition> { inner, outer },
            Rows = new List<BpRow> { Row(new BpGroupElement { ComponentId = "outer", DataPath = "First",
                Parameters = new Dictionary<string, BpComponentParameterBinding> {
                    ["caption"] = new BpComponentParameterBinding { Path = "First.Label" } } }),
                Row(new BpGroupElement { ComponentId = "outer", DataPath = "Second",
                    Parameters = new Dictionary<string, BpComponentParameterBinding> {
                        ["caption"] = new BpComponentParameterBinding { Path = "Second.Label" } } }) } };
        var data = new { First = new { Name = "Tea", Label = "NEW" },
            Second = new { Name = "Rice", Label = "SALE" } };
        var loader = new TemplateLoader();
        var templateBefore = loader.SaveJson(template, Options);
        var dataBefore = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        var renderer = new TemplateRenderer();
        var result = renderer.Render(template, data, options: Options);
        foreach (var text in new[] { "Tea", "Rice", "NEW", "SALE" }) Assert.Contains(text, Output(result));
        Assert.Equal(templateBefore, loader.SaveJson(template, Options));
        Assert.Equal(dataBefore, Newtonsoft.Json.JsonConvert.SerializeObject(data));

        outer.Elements[1] = new BpGroupElement { ComponentId = "inner",
            StartCoordinate = new BpCoordinate { X = 0, Y = 24 } };
        var error = Assert.Throws<TemplateRenderException>(() => renderer.RenderWithLayout(template, data, options: Options));
        Assert.Equal("Rows[0].Cells[0].Elements[0].Elements[1]", error.SourcePath);
    }

    /// <summary>
    /// 为图片测试提供内存资源并统计读取次数。
    /// </summary>
    private sealed class ImageResources : ITemplateResourceProvider
    {
        /// <summary>
        /// 保存可重复读取的图片编码数据。
        /// </summary>
        private readonly byte[] bytes;
        /// <summary>
        /// 记录图片资源被打开的次数。
        /// </summary>
        internal int OpenCount;
        /// <summary>
        /// 初始化一个 <see cref="ImageResources"/> 类型的实例。
        /// </summary>
        /// <param name="bytes">要提供给渲染器的图片编码数据。</param>
        internal ImageResources(byte[] bytes) { this.bytes = bytes; }
        /// <inheritdoc />
        public Stream OpenRead(string resourceKey, System.Threading.CancellationToken cancellationToken)
        {
            if (resourceKey != "logo") return null;
            OpenCount++;
            return new MemoryStream(bytes, false);
        }
    }
}
