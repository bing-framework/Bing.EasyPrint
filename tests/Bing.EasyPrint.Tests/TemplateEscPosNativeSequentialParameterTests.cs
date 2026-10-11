using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 ESC/POS Native Sequential 模式下，带 HRI 的重复条码明细能够跨页保持参数和布局轨迹。
/// </summary>
public sealed class TemplateEscPosNativeSequentialParameterTests
{
    private static readonly BarcodeCase[] BarcodeCases =
    {
        new BarcodeCase(BarcodeType.Code39, "ABC-12", 69,
            "Rows[2].Cells[0].Elements[0]"),
        new BarcodeCase(BarcodeType.I2OF5, "1234", 70,
            "Rows[2].Cells[0].Elements[1]"),
        new BarcodeCase(BarcodeType.UccEan128, "0101234567890128", 74,
            "Rows[2].Cells[0].Elements[2]")
    };

    [Fact]
    public void Sequential_template_preserves_hri_parameters_and_instances_across_pages()
    {
        var options = Options();
        var capabilities = TemplateProtocolCapabilities.Create(options);
        Assert.Equal(EscPosNativeFlowMode.Sequential, capabilities.NativeFlow);
        Assert.All(BarcodeCases, item => Assert.Contains(item.Type, capabilities.BarcodeTypes));

        var result = new TemplateRenderer().RenderWithLayout(
            Template(), Data(2), options: options);

        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.Equal(2, result.PageCount);
        Assert.All(result.Pages, page =>
        {
            Assert.NotEmpty(page.Data);
            var text = Encoding.ASCII.GetString(page.Data);
            Assert.Contains("PAGE-HEADER", text, StringComparison.Ordinal);
            Assert.Contains("TABLE-HEADER", text, StringComparison.Ordinal);
            Assert.Contains("PAGE-FOOTER", text, StringComparison.Ordinal);
            Assert.Equal(240, page.Height);
        });

        var barcodeElements = result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Kind == "element" && element.Type == 4)
            .ToArray();
        Assert.Equal(BarcodeCases.Length * 2, barcodeElements.Length);

        foreach (var item in BarcodeCases)
        {
            var instances = barcodeElements
                .Where(element => element.SourcePath == item.SourcePath)
                .OrderBy(element => element.InstanceIndices.Single())
                .ToArray();

            Assert.Equal(2, instances.Length);
            Assert.Equal(new[] { 0, 1 },
                instances.Select(element => element.InstanceIndices.Single()));
            Assert.All(instances, element =>
            {
                Assert.Equal(item.SourcePath, element.SourcePath);
                Assert.Equal(500, element.Width);
                Assert.Equal(56, element.Height);
                Assert.InRange(element.Y, 0, 240 - element.Height);
            });

            var expected = NativeBarcode(item.Symbology, item.Data, 40, 3, 2);
            var output = result.Pages.SelectMany(page => page.Data).ToArray();
            Assert.Equal(2, CountSequence(output, expected));
        }

        foreach (var page in result.Pages)
        {
            foreach (var item in BarcodeCases)
            {
                Assert.True(ContainsSequence(page.Data,
                    NativeBarcode(item.Symbology, item.Data, 40, 3, 2)),
                    $"页面缺少 {item.Type} 的 HRI、GS w=3 或 GS k={item.Symbology} 输出。");
            }
        }
    }

    [Fact]
    public void Sequential_template_rejects_nonzero_quiet_zone_before_opening_resource()
    {
        var opened = 0;
        var template = InvalidQuietZoneTemplate();

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(template, null,
                new CountingResources(() => opened++), Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("静区", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, opened);
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
        Width = 600,
        Height = 240,
        Rows = new List<BpRow>
        {
            TextRow("PageHeader", "PAGE-HEADER", 24),
            TextRow("TableHeader", "TABLE-HEADER", 24),
            new BpRow
            {
                Section = "Body",
                RepeatSource = "Items",
                Height = 168,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 600,
                        Elements = BarcodeCases.Select(item => Barcode(item.Type, item.Data))
                            .Cast<BpElement>().ToList()
                    }
                }
            },
            TextRow("PageFooter", "PAGE-FOOTER", 24)
        }
    };

    private static BpTemplate InvalidQuietZoneTemplate() => new BpTemplate
    {
        Version = 2,
        Width = 600,
        Height = 80,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Section = "Body",
                Height = 56,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 600,
                        Elements = new List<BpElement>
                        {
                            new BpBarcodeElement
                            {
                                BarcodeType = BarcodeType.UccEan128,
                                LineWidth = 1,
                                Ratio = 2,
                                NarrowModuleWidth = 3,
                                Width = 500,
                                Height = 40,
                                HumanReadable = false,
                                QuietZone = PrintQuietZone.Uniform(2),
                                Field = new BgField { Value = "0101234567890128" }
                            },
                            new BpImageElement
                            {
                                Width = 8,
                                Height = 1,
                                Field = new BgField { Value = "unused-resource" }
                            }
                        }
                    }
                }
            }
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
                Width = 600,
                Elements = new List<BpElement>
                {
                    new BpFontElement { FontSize = 24, Field = new BgField { Value = value } }
                }
            }
        }
    };

    private static BpBarcodeElement Barcode(BarcodeType type, string data) => new BpBarcodeElement
    {
        BarcodeType = type,
        LineWidth = 1,
        Ratio = 2,
        NarrowModuleWidth = 3,
        Width = 500,
        Height = 40,
        HumanReadable = true,
        HumanReadableReserveHeight = 16,
        Field = new BgField { Value = data }
    };

    private static object Data(int itemCount) => new
    {
        Items = Enumerable.Range(0, itemCount).Select(_ => new { }).ToArray()
    };

    private static byte[] NativeBarcode(byte symbology, string data, byte height,
        byte narrowWidth, byte textPosition)
    {
        var payload = Encoding.ASCII.GetBytes(data);
        return new byte[] { 0x1D, 0x48, textPosition, 0x1D, 0x68, height,
            0x1D, 0x77, narrowWidth, 0x1D, 0x6B, symbology, (byte)payload.Length }
            .Concat(payload)
            .ToArray();
    }

    private static bool ContainsSequence(byte[] data, byte[] sequence)
        => CountSequence(data, sequence) > 0;

    private static int CountSequence(byte[] data, byte[] sequence)
    {
        var count = 0;
        for (var start = 0; start <= data.Length - sequence.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < sequence.Length; offset++)
            {
                if (data[start + offset] == sequence[offset]) continue;
                matched = false;
                break;
            }
            if (matched) count++;
        }
        return count;
    }

    private sealed class BarcodeCase
    {
        internal BarcodeCase(BarcodeType type, string data, byte symbology, string sourcePath)
        {
            Type = type;
            Data = data;
            Symbology = symbology;
            SourcePath = sourcePath;
        }

        internal BarcodeType Type { get; }
        internal string Data { get; }
        internal byte Symbology { get; }
        internal string SourcePath { get; }
    }

    private sealed class CountingResources : ITemplateResourceProvider
    {
        private readonly Action _onOpen;

        internal CountingResources(Action onOpen) => _onOpen = onOpen;

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            _onOpen();
            return null;
        }
    }
}
