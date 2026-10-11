using System;
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
/// 验证 ESC/POS Native 模板能够把 Function B 扩展条码和 Code128 auto 传递到原生输出。
/// </summary>
public sealed class TemplateEscPosNativeExtendedBarcodeTests
{
    private static readonly (BarcodeType Type, string Value, string Payload, byte Symbology, int Width, string SourcePath)[] BarcodeCases =
    {
        (BarcodeType.Gs1DataBarOmnidirectional, "1234567890123", "1234567890123", 75, 472, "Rows[2].Cells[0].Elements[0]"),
        (BarcodeType.Gs1DataBarTruncated, "1234567890123", "1234567890123", 76, 472, "Rows[2].Cells[0].Elements[1]"),
        (BarcodeType.Gs1DataBarLimited, "0123456789012", "0123456789012", 77, 472, "Rows[2].Cells[0].Elements[2]"),
        (BarcodeType.Gs1DataBarExpanded, "(01)\u001D12345678901231", "{(01{){112345678901231", 78, 876, "Rows[2].Cells[0].Elements[3]"),
        (BarcodeType.Code128Auto, "ABC123", "ABC123", 79, 242, "Rows[2].Cells[0].Elements[4]")
    };

    [Fact]
    public void Sequential_template_exposes_extended_native_barcode_capabilities()
    {
        var options = Options();
        var capabilities = TemplateProtocolCapabilities.Create(options);

        Assert.Equal(EscPosNativeFlowMode.Sequential, capabilities.NativeFlow);
        Assert.All(BarcodeCases, barcode =>
            Assert.Contains(barcode.Type, capabilities.BarcodeTypes));
    }

    [Fact]
    public void Sequential_template_preserves_function_b_payloads_and_layout_trace_across_pages()
    {
        var result = new TemplateRenderer().RenderWithLayout(
            Template(),
            new { Items = Enumerable.Range(0, 2).Select(_ => new { }).ToArray() },
            options: Options());

        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.True(result.PageCount > 1);
        Assert.All(result.Pages, page =>
        {
            Assert.Equal(PrintProtocol.EscPos, page.Protocol);
            Assert.NotEmpty(page.Data);
            var text = Encoding.ASCII.GetString(page.Data);
            Assert.Contains("PAGE-HEADER", text, StringComparison.Ordinal);
            Assert.Contains("TABLE-HEADER", text, StringComparison.Ordinal);
            Assert.Contains("PAGE-FOOTER", text, StringComparison.Ordinal);
            Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x6B }),
                "Native 页面必须包含 GS k 条码指令。");
        });

        var barcodes = result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Kind == "element" && element.Type == 4)
            .ToArray();
        Assert.Equal(BarcodeCases.Length * 2, barcodes.Length);

        foreach (var barcode in BarcodeCases)
        {
            var instances = barcodes
                .Where(element => element.SourcePath == barcode.SourcePath)
                .OrderBy(element => element.InstanceIndices.Single())
                .ToArray();

            Assert.Equal(2, instances.Length);
            Assert.Equal(new[] { 0, 1 }, instances.Select(element => element.InstanceIndices.Single()));
            Assert.All(instances, element =>
            {
                Assert.Equal(barcode.SourcePath, element.SourcePath);
                Assert.Equal(barcode.Width, element.Width);
                Assert.Equal(48, element.Height);
                Assert.InRange(element.Y, 0, result.Pages.Max(page => page.Height) - element.Height);
            });

            var expected = BarcodeBytes(barcode);
            var output = result.Pages.SelectMany(page => page.Data).ToArray();
            Assert.Equal(2, CountSequence(output, expected));
        }
    }

    [Fact]
    public void Sequential_template_rejects_unsupported_msi_with_element_path()
    {
        var template = new BpTemplate
        {
            Version = 2,
            Width = 1200,
            Height = 96,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Section = "Body",
                    Height = 48,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 1200,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Msi,
                                    LineWidth = 1,
                                    Ratio = 2,
                                    Width = 240,
                                    Height = 48,
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
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
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
        Width = 1200,
        Height = 264,
        Rows = new List<BpRow>
        {
            TextRow("PageHeader", "PAGE-HEADER", 24),
            TextRow("TableHeader", "TABLE-HEADER", 24),
            new BpRow
            {
                Section = "Body",
                RepeatSource = "Items",
                Height = 240,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 1200,
                        Elements = BarcodeCases.Select(barcode => Barcode(
                            barcode.Type, barcode.Value, barcode.Width)).Cast<BpElement>().ToList()
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
                Width = 1200,
                Elements = new List<BpElement>
                {
                    new BpFontElement { FontSize = 24, Field = new BgField { Value = value } }
                }
            }
        }
    };

    private static BpBarcodeElement Barcode(BarcodeType type, string value, int width) => new BpBarcodeElement
    {
        BarcodeType = type,
        LineWidth = 1,
        Ratio = 2,
        Width = width,
        Height = 48,
        NarrowModuleWidth = 2,
        HumanReadable = false,
        Field = new BgField { Value = value }
    };

    private static byte[] BarcodeBytes((BarcodeType Type, string Value, string Payload, byte Symbology, int Width, string SourcePath) barcode)
        => new byte[] { 0x1D, 0x48, 0, 0x1D, 0x68, 48, 0x1D, 0x77, 2, 0x1D, 0x6B,
            barcode.Symbology, (byte)Encoding.ASCII.GetByteCount(barcode.Payload) }
            .Concat(Encoding.ASCII.GetBytes(barcode.Payload)).ToArray();

    private static bool ContainsSequence(byte[] data, byte[] sequence) => CountSequence(data, sequence) > 0;

    private static int CountSequence(byte[] data, byte[] sequence)
    {
        var count = 0;
        for (var start = 0; start <= data.Length - sequence.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < sequence.Length; offset++)
                if (data[start + offset] != sequence[offset])
                {
                    matched = false;
                    break;
                }
            if (matched) count++;
        }
        return count;
    }
}
