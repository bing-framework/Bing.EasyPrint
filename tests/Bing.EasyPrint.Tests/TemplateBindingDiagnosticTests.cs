using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证模板绑定失败时的业务字段路径及组件归属诊断。
/// </summary>
public sealed class TemplateBindingDiagnosticTests
{
    private static TemplateRenderOptions Options => new TemplateRenderOptions { Encoding = Encoding.UTF8 };

    private static BpFontElement FieldText(string name) => new BpFontElement
    {
        FontSize = 24,
        Field = new BgField { Name = name }
    };

    private static BpFontElement LiteralText(string value) => new BpFontElement
    {
        FontSize = 24,
        Field = new BgField { Value = value }
    };

    private static BpRow Row(params BpElement[] elements) => new BpRow
    {
        Cells = new List<BpCell>
        {
            new BpCell { Elements = elements.ToList() }
        }
    };

    private static BpTemplate Template(params BpRow[] rows) => new BpTemplate
    {
        Width = 160,
        Height = 100,
        Rows = rows.ToList()
    };

    private static BpTemplate LoadWithPaths(BpTemplate template)
    {
        var loader = new TemplateLoader();
        return loader.LoadJsonWithPaths(loader.SaveJson(template, Options), Options);
    }

    /// <summary>
    /// 验证缺失的嵌套字段返回完整业务字段路径。
    /// </summary>
    [Fact]
    public void Missing_nested_field_exposes_full_field_path()
    {
        var template = LoadWithPaths(Template(Row(FieldText("Customer.Profile.Name"))));

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template,
            new { Customer = new { Profile = new { } } },
            options: Options));

        Assert.Equal("Customer.Profile.Name", error.FieldPath);
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    /// <summary>
    /// 验证绑定到对象的文字字段报告字段路径，而不是把对象转成文本。
    /// </summary>
    [Fact]
    public void Non_scalar_text_binding_exposes_field_path()
    {
        var template = LoadWithPaths(Template(Row(FieldText("Customer"))));

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template,
            new { Customer = new { Name = "Ada" } },
            options: Options));

        Assert.Equal("Customer", error.FieldPath);
        Assert.Contains("标量", error.Message);
    }

    /// <summary>
    /// 验证条件比较类型不兼容时返回比较字段路径。
    /// </summary>
    [Fact]
    public void Condition_type_mismatch_exposes_comparison_field_path()
    {
        var row = Row(LiteralText("visible"));
        row.Condition = new BpCondition { Path = "Quantity", Operator = "gt", Value = "20" };
        var template = LoadWithPaths(Template(row));

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template,
            new { Quantity = 21 },
            options: Options));

        Assert.Equal("Quantity", error.FieldPath);
        Assert.Contains("类型不兼容", error.Message);
    }

    /// <summary>
    /// 验证重复行的数据源缺失时返回重复源字段路径。
    /// </summary>
    [Fact]
    public void Missing_repeat_source_exposes_repeat_field_path()
    {
        var row = Row(LiteralText("item"));
        row.RepeatSource = "Items.Missing";
        var template = LoadWithPaths(Template(row));

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template,
            new { Items = new { } },
            options: Options));

        Assert.Equal("Items.Missing", error.FieldPath);
        Assert.Equal("Rows[0]", error.SourcePath);
    }

    /// <summary>
    /// 验证共享组件在重复数据项中失败时保留字段、定义、实例和项序号。
    /// </summary>
    [Fact]
    public void Repeated_shared_component_binding_preserves_all_diagnostic_ownership()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 160,
            Height = 100,
            Components = new List<BpComponentDefinition>
            {
                new BpComponentDefinition
                {
                    Id = "customer-label",
                    Width = 160,
                    Height = 32,
                    Elements = new List<BpElement> { FieldText("Customer.Name") }
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
                                new BpGroupElement { ComponentId = "customer-label" }
                            }
                        }
                    }
                }
            }
        };
        template = LoadWithPaths(template);

        var data = new
        {
            Items = new[]
            {
                new { Customer = (object)new { Name = "first" } },
                new { Customer = (object)new { } }
            }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template,
            data,
            options: Options));

        Assert.Equal("Customer.Name", error.FieldPath);
        Assert.Equal("Rows[0].Cells[0].Elements[0].Elements[0]", error.SourcePath);
        Assert.Equal("Components[0].Elements[0]", error.DefinitionPath);
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ComponentInstancePath);
        Assert.Equal(new[] { 1 }, error.InstanceIndices);
    }

    /// <summary>
    /// 验证旧的默认 CPCL Render 继续抛出精确的 ArgumentException 类型。
    /// </summary>
    [Fact]
    public void Default_cpcl_render_keeps_exact_argument_exception_type()
    {
        var template = LoadWithPaths(Template(Row(FieldText("Missing.Value"))));
        var error = Record.Exception(() => new TemplateRenderer().Render(
            template,
            new { Existing = "value" },
            options: Options));

        Assert.NotNull(error);
        Assert.IsType<ArgumentException>(error);
        Assert.IsNotType<TemplateRenderException>(error);
    }

    /// <summary>
    /// 验证与数据绑定无关的布局错误不伪造字段路径。
    /// </summary>
    [Fact]
    public void Non_binding_layout_error_has_no_field_path()
    {
        var text = LiteralText(new string('A', 64));
        text.Field.NeedFeedLine = false;
        var template = Template(Row(text));
        template.Width = 1;
        template = LoadWithPaths(template);

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template,
            null,
            options: Options));

        Assert.Null(error.FieldPath);
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }
}
