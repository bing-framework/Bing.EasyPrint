using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 Provider 阶段按实际页面元素归属模板运行时错误。
/// </summary>
public sealed class TemplateProviderOwnershipTests
{
    [Fact]
    public void Render_maps_repeated_provider_failure_to_the_failing_instance()
    {
        var template = new BpTemplate
        {
            Version = 2,
            Width = 160,
            Height = 48,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    RepeatSource = "Items",
                    Height = 24,
                    Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement>
                    {
                        Text("Name")
                    } } }
                }
            }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            template,
            new
            {
                Items = new[]
                {
                    new { Name = "OK" },
                    new { Name = "BAD\"TEXT" }
                }
            },
            options: Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Null(error.DefinitionPath);
        Assert.Null(error.ComponentInstancePath);
        Assert.Single(error.InstanceIndices);
        Assert.Equal(1, error.InstanceIndices[0]);
        Assert.Equal(0, error.PageIndex);
        Assert.Equal(1, error.ElementIndex);
    }

    [Fact]
    public void RenderWithLayout_maps_last_cross_page_component_failure_to_definition_and_instance()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 160,
            Height = 48,
            Components = new List<BpComponentDefinition>
            {
                new BpComponentDefinition
                {
                    Id = "label",
                    Width = 160,
                    Elements = new List<BpElement> { Text("Name") }
                }
            },
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    RepeatSource = "Items",
                    Height = 24,
                    Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement>
                    {
                        new BpGroupElement { ComponentId = "label" }
                    } } }
                }
            }
        };
        var items = Enumerable.Range(0, 5)
            .Select(index => new { Name = index == 4 ? "BAD\"TEXT" : "OK" + index })
            .ToArray();

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, new { Items = items }, options: Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0].Elements[0]", error.SourcePath);
        Assert.Equal("Components[0].Elements[0]", error.DefinitionPath);
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ComponentInstancePath);
        Assert.Single(error.InstanceIndices);
        Assert.Equal(4, error.InstanceIndices[0]);
        Assert.Equal(2, error.PageIndex);
        Assert.Equal(0, error.ElementIndex);
    }

    [Fact]
    public void Static_component_capability_error_keeps_definition_without_instance()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 160,
            Height = 48,
            Components = new List<BpComponentDefinition>
            {
                new BpComponentDefinition
                {
                    Id = "label",
                    Width = 160,
                    Elements = new List<BpElement>
                    {
                        new BpFontElement
                        {
                            FontSize = 24,
                            Bold = true,
                            Field = new BgField { Value = "Bold" }
                        }
                    }
                }
            },
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 24,
                    Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement>
                    {
                        new BpGroupElement { ComponentId = "label" }
                    } } }
                }
            }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            template, null, options: Options()));

        Assert.Equal("Components[0].Elements[0]", error.SourcePath);
        Assert.Equal("Components[0].Elements[0]", error.DefinitionPath);
        Assert.Null(error.ComponentInstancePath);
        Assert.Null(error.PageIndex);
        Assert.Null(error.ElementIndex);
    }

    private static BpFontElement Text(string path) => new BpFontElement
    {
        FontSize = 24,
        Field = new BgField { Name = path }
    };

    private static TemplateRenderOptions Options() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.Tspl,
        Encoding = Encoding.ASCII
    };
}
