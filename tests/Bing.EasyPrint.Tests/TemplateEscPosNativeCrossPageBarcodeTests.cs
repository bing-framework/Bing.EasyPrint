using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 ESC/POS Native Sequential 模式下，带固定页眉、表头和页脚的重复条码明细能够跨页输出。
/// </summary>
public sealed class TemplateEscPosNativeCrossPageBarcodeTests
{
    private static readonly (BarcodeType Type, string SourcePath, byte Symbology)[] BarcodeCases =
    {
        (BarcodeType.Code128, "Rows[2].Cells[0].Elements[0]", 73),
        (BarcodeType.Code39, "Rows[2].Cells[0].Elements[1]", 69),
        (BarcodeType.Ean13, "Rows[2].Cells[0].Elements[2]", 67)
    };

    [Fact]
    public void Sequential_template_preserves_native_barcodes_and_trace_across_pages()
    {
        var options = Options();
        var capabilities = TemplateProtocolCapabilities.Create(options);

        Assert.Equal(EscPosNativeFlowMode.Sequential, capabilities.NativeFlow);
        Assert.All(BarcodeCases, barcode =>
            Assert.Contains(barcode.Type, capabilities.BarcodeTypes));

        const int itemCount = 5;
        var result = new TemplateRenderer().RenderWithLayout(Template(), Data(itemCount), options: options);

        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.True(result.PageCount > 1);
        Assert.All(result.Pages, page =>
        {
            Assert.NotEmpty(page.Data);
            Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x6B }),
                "每个包含重复明细的 Native 页面都应包含 GS k 原生条码指令。");
            Assert.Contains("PAGE-HEADER", Encoding.ASCII.GetString(page.Data));
            Assert.Contains("TABLE-HEADER", Encoding.ASCII.GetString(page.Data));
            Assert.Contains("PAGE-FOOTER", Encoding.ASCII.GetString(page.Data));
        });

        var barcodeElements = result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Kind == "element" && element.Type == 4)
            .ToArray();
        Assert.Equal(itemCount * BarcodeCases.Length, barcodeElements.Length);

        foreach (var barcode in BarcodeCases)
        {
            var instances = barcodeElements
                .Where(element => element.SourcePath == barcode.SourcePath)
                .OrderBy(element => element.InstanceIndices.Single())
                .ToArray();

            Assert.Equal(itemCount, instances.Length);
            Assert.Equal(Enumerable.Range(0, itemCount),
                instances.Select(element => element.InstanceIndices.Single()));
            Assert.All(instances, element =>
            {
                Assert.True(element.Width > 0);
                Assert.Equal(24, element.Height);
                Assert.InRange(element.Y, 0, result.Pages.Max(page => page.Height) - element.Height);
            });

            var marker = new byte[] { 0x1D, 0x6B, barcode.Symbology };
            Assert.True(ContainsSequence(result.Pages.SelectMany(page => page.Data).ToArray(), marker),
                $"Native 输出缺少 {barcode.Type} 对应的 GS k {barcode.Symbology} 指令。");
        }
    }

    [Fact]
    public void Sequential_template_rejects_unsupported_msi_with_element_path()
    {
        var template = new BpTemplate
        {
            Version = 2,
            Width = 280,
            Height = 80,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Section = "Body",
                    Height = 24,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 280,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Msi,
                                    LineWidth = 1,
                                    Ratio = 2,
                                    Width = 220,
                                    Height = 24,
                                    HumanReadable = false,
                                    Field = new BgField { Value = "1234" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(template, null, options: Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("Msi", error.Message, StringComparison.Ordinal);
    }

    private static TemplateRenderOptions Options() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        Encoding = Encoding.ASCII,
        EscPosLayout = EscPosLayoutMode.Native,
        FeedLines = 0,
        CutAfterPrint = false,
        EscPosDevice = new EscPosDeviceOptions
        {
            NativeFlow = EscPosNativeFlowMode.Sequential
        }
    };

    private static BpTemplate Template() => new BpTemplate
    {
        Version = 2,
        Width = 280,
        Height = 168,
        Rows = new List<BpRow>
        {
            TextRow("PageHeader", "PAGE-HEADER", 24),
            TextRow("TableHeader", "TABLE-HEADER", 24),
            new BpRow
            {
                Section = "Body",
                RepeatSource = "Items",
                Height = 72,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 280,
                        Elements = new List<BpElement>
                        {
                            Barcode(BarcodeType.Code128, "1234", 0),
                            Barcode(BarcodeType.Code39, "ABC-12", 24),
                            Barcode(BarcodeType.Ean13, "4006381333931", 48)
                        }
                    }
                }
            },
            TextRow("PageFooter", "PAGE-FOOTER", 24)
        }
    };

    private static BpRow TextRow(string section, string value, int height) => new BpRow
    {
        Section = section,
        Height = height,
        Cells = new List<BpCell>
        {
            new BpCell
            {
                Width = 280,
                Elements = new List<BpElement>
                {
                    new BpFontElement { FontSize = 24, Field = new BgField { Value = value } }
                }
            }
        }
    };

    private static BpBarcodeElement Barcode(BarcodeType type, string value, int y) => new BpBarcodeElement
    {
        BarcodeType = type,
        LineWidth = 1,
        Ratio = 2,
        Width = 280,
        Height = 24,
        HumanReadable = false,
        StartCoordinate = new BpCoordinate { X = 0, Y = y },
        Field = new BgField { Value = value }
    };

    private static object Data(int itemCount) => new
    {
        Items = Enumerable.Range(0, itemCount).Select(_ => new { }).ToArray()
    };

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < sequence.Length; offset++)
                if (data[start + offset] != sequence[offset])
                {
                    matched = false;
                    break;
                }
            if (matched) return true;
        }
        return false;
    }
}
