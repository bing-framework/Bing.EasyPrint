using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证模板渲染中临时模型存储与返回结果存储的预算归属。</summary>
public sealed class TemplateStorageBudgetTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Long_text_is_rejected_before_output_and_a_small_render_recovers(PrintProtocol protocol)
    {
        var template = LongTextTemplate();
        var renderer = new TemplateRenderer();
        var lowBudget = MeasurePeak(renderer, template, protocol) - 1;
        Assert.True(lowBudget > 0);

        using (var parent = PrintResourceScope.Begin(long.MaxValue))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() => renderer.Render(
                template, null, options: Options(protocol, lowBudget)));

            AssertBudgetDiagnostic(error);
            Assert.Equal(0, parent.CurrentBytes);

            var recovered = renderer.Render(SmallTemplate(), null, options: Options(protocol));
            Assert.Single(recovered.Pages);
            Assert.Contains("OK", Encoding.ASCII.GetString(recovered.Pages[0].Data));
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Many_small_elements_are_rejected_without_partial_model_and_a_small_render_recovers(
        PrintProtocol protocol)
    {
        var template = RepeatedTextTemplate(128);
        var renderer = new TemplateRenderer();
        var lowBudget = MeasurePeak(renderer, template, protocol) - 1;
        Assert.True(lowBudget > 0);

        using (var parent = PrintResourceScope.Begin(long.MaxValue))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() => renderer.Render(
                template, null, options: Options(protocol, lowBudget)));

            AssertBudgetDiagnostic(error);
            Assert.Equal(0, parent.CurrentBytes);

            var recovered = renderer.Render(SmallTemplate(), null, options: Options(protocol));
            Assert.Single(recovered.Pages);
            Assert.Contains("OK", Encoding.ASCII.GetString(recovered.Pages[0].Data));
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Cancellation_releases_template_storage_and_the_parent_scope_can_render_again(
        PrintProtocol protocol)
    {
        var template = RepeatedRowTemplate();
        var renderer = new TemplateRenderer();
        using (var parent = PrintResourceScope.Begin(long.MaxValue))
        using (var cancellation = new CancellationTokenSource())
        {
            Assert.Throws<OperationCanceledException>(() => renderer.Render(
                template,
                new { Items = CancelAfter(cancellation, 24) },
                options: Options(protocol),
                cancellationToken: cancellation.Token));

            Assert.Equal(0, parent.CurrentBytes);

            var recovered = renderer.Render(SmallTemplate(), null, options: Options(protocol));
            Assert.Single(recovered.Pages);
            Assert.Contains("OK", Encoding.ASCII.GetString(recovered.Pages[0].Data));
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Render_and_layout_render_have_matching_output_and_result_storage_is_below_peak(
        PrintProtocol protocol)
    {
        var template = LongTextTemplate();
        var renderer = new TemplateRenderer();
        var plain = renderer.Render(template, null, options: Options(protocol));
        var withLayout = renderer.RenderWithLayout(template, null, options: Options(protocol));

        Assert.Equal(plain.PageCount, withLayout.PageCount);
        for (var index = 0; index < plain.PageCount; index++)
            Assert.Equal(plain.Pages[index].Data, withLayout.Pages[index].Data);
        Assert.All(plain.Pages, page => Assert.Empty(page.Elements));

        var layoutElements = withLayout.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Kind == "element").ToArray();
        Assert.NotEmpty(layoutElements);
        Assert.All(layoutElements, element =>
            Assert.StartsWith("Rows[0].Cells[0].Elements[0]", element.SourcePath));
        Assert.Contains(layoutElements, element => element.Text != null && element.Text.Length > 0);

        using (var parent = PrintResourceScope.Begin(long.MaxValue))
        {
            var retainedResult = renderer.RenderWithLayout(template, null, options: Options(protocol));
            var retained = parent.CurrentBytes;
            var peak = parent.PeakBytes;

            Assert.NotEmpty(retainedResult.Pages);
            Assert.True(retained > 0);
            Assert.True(retained < peak);
        }
    }

    private static long MeasurePeak(TemplateRenderer renderer, BpTemplate template, PrintProtocol protocol)
    {
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var result = renderer.Render(template, null, options: Options(protocol));
            Assert.NotEmpty(result.Pages);
            return scope.PeakBytes;
        }
    }

    private static void AssertBudgetDiagnostic(PrintResourceLimitException error)
    {
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.ResourceName));
        Assert.True(error.RequestedBytes > 0);
    }

    private static TemplateRenderOptions Options(PrintProtocol protocol, long? maxWorkingBytes = null)
    {
        var options = new TemplateRenderOptions
        {
            Protocol = protocol,
            Encoding = Encoding.ASCII,
            MaxWorkingBytes = maxWorkingBytes
        };
        if (protocol == PrintProtocol.Tspl || protocol == PrintProtocol.Zpl)
        {
            options.DeviceHalfCellWidthAt24 = 10;
            options.DeviceFullCellWidthAt24 = 20;
            options.DeviceLineHeightAt24 = 24;
        }
        return options;
    }

    private static BpTemplate LongTextTemplate()
        => new BpTemplate
        {
            Version = 3,
            Width = 240,
            Height = 200,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 240,
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    Field = new BgField { Value = new string('A', 4096) }
                                }
                            }
                        }
                    }
                }
            }
        };

    private static BpTemplate RepeatedTextTemplate(int count)
        => new BpTemplate
        {
            Version = 3,
            Width = 240,
            Height = 200,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 240,
                            Elements = Enumerable.Range(0, count)
                                .Select(_ => (BpElement)new BpFontElement
                                {
                                    FontSize = 24,
                                    Field = new BgField { Value = "x" }
                                }).ToList()
                        }
                    }
                }
            }
        };

    private static BpTemplate RepeatedRowTemplate()
        => new BpTemplate
        {
            Version = 3,
            Width = 240,
            Height = 200,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    RepeatSource = "Items",
                    Height = 24,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 240,
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    Field = new BgField { Value = "row" }
                                }
                            }
                        }
                    }
                }
            }
        };

    private static BpTemplate SmallTemplate()
        => new BpTemplate
        {
            Version = 3,
            Width = 240,
            Height = 80,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 240,
                            Elements = new List<BpElement>
                            {
                                new BpFontElement { FontSize = 24, Field = new BgField { Value = "OK" } }
                            }
                        }
                    }
                }
            }
        };

    private static IEnumerable<string> CancelAfter(CancellationTokenSource cancellation, int count)
    {
        for (var index = 0; index < count; index++) yield return "item";
        cancellation.Cancel();
        yield return "cancelled";
    }
}
