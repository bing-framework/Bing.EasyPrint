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
/// 验证版本 2 模板中的非 Code128 条码跨页重复时，四种目标协议都保留统一布局轨迹并生成对应输出。
/// </summary>
public sealed class TemplateCrossPageBarcodeTests
{
    private static readonly (BarcodeType Type, string Value, string SourcePath)[] BarcodeCases =
    {
        (BarcodeType.Code39, "ABC-12", "Rows[2].Cells[0].Elements[0]"),
        (BarcodeType.Code93, "ABC93", "Rows[2].Cells[0].Elements[1]"),
        (BarcodeType.Codabar, "A12B", "Rows[2].Cells[0].Elements[2]"),
        (BarcodeType.I2OF5C, "1234", "Rows[2].Cells[0].Elements[3]")
    };

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Version_two_cross_page_barcodes_keep_instances_and_protocol_output(PrintProtocol protocol)
    {
        var options = Options(protocol);
        var capabilities = TemplateProtocolCapabilities.Create(options);
        Assert.All(BarcodeCases, barcode => Assert.True(capabilities.BarcodeTypes.Contains(barcode.Type),
            $"协议 {protocol} 必须明确支持 {barcode.Type}，否则模板应在能力预检阶段严格拒绝。"));

        var result = new TemplateRenderer().RenderWithLayout(CrossPageTemplate(), Data(), options: options);

        Assert.Equal(protocol, result.Protocol);
        Assert.Equal(3, result.PageCount);
        Assert.All(result.Pages, page =>
        {
            Assert.Equal(protocol, page.Protocol);
            Assert.NotEmpty(page.Data);
            var pageBarcodes = page.Elements.Where(element => element.Type == 4).ToArray();
            Assert.NotEmpty(pageBarcodes);
            Assert.All(pageBarcodes, element => Assert.Equal(1, element.InstanceIndices.Count));
        });

        var barcodes = result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type == 4)
            .ToArray();
        Assert.Equal(BarcodeCases.Length * 3, barcodes.Length);
        foreach (var barcode in BarcodeCases)
        {
            var instances = barcodes.Where(element => element.SourcePath == barcode.SourcePath).ToArray();
            Assert.Equal(3, instances.Length);
            Assert.Equal(new[] { 0, 1, 2 }, instances.Select(element => element.InstanceIndices[0]));
            Assert.All(instances, element =>
            {
                Assert.True(element.Width > 0);
                Assert.True(element.Height > 0);
            });
        }

        AssertProtocolOutput(result, protocol);
    }

    private static void AssertProtocolOutput(TemplateRenderResult result, PrintProtocol protocol)
    {
        if (protocol == PrintProtocol.EscPos)
        {
            foreach (var page in result.Pages)
            {
                var raster = page.Data;
                var marker = new byte[] { 0x1D, 0x76, 0x30 };
                Assert.True(ContainsSequence(raster, marker), "ESC/POS Raster 输出必须包含 GS v 0 光栅命令。");
                var markerIndex = IndexOf(raster, marker);
                Assert.True(raster.Length >= markerIndex + 8, "ESC/POS Raster 头部必须完整。");
                var rowBytes = raster[markerIndex + 4] | raster[markerIndex + 5] << 8;
                var height = raster[markerIndex + 6] | raster[markerIndex + 7] << 8;
                Assert.True(rowBytes > 0 && height > 0, "ESC/POS Raster 尺寸必须为正数。");
                Assert.True(raster.Length >= markerIndex + 8 + rowBytes * height,
                    "ESC/POS Raster 必须包含非空光栅数据。");
            }
            return;
        }

        var output = string.Concat(result.Pages.Select(page => Encoding.ASCII.GetString(page.Data)));
        switch (protocol)
        {
            case PrintProtocol.Cpcl:
                Assert.Contains("B 39 ", output, StringComparison.Ordinal);
                Assert.Contains("B 93 ", output, StringComparison.Ordinal);
                Assert.Contains("B CODABAR ", output, StringComparison.Ordinal);
                Assert.Contains("B I2OF5C ", output, StringComparison.Ordinal);
                break;
            case PrintProtocol.Tspl:
                Assert.Contains("\"39\"", output, StringComparison.Ordinal);
                Assert.Contains("\"93\"", output, StringComparison.Ordinal);
                Assert.Contains("\"CODABAR\"", output, StringComparison.Ordinal);
                Assert.Contains("\"25C\"", output, StringComparison.Ordinal);
                break;
            case PrintProtocol.Zpl:
                Assert.Contains("^B3", output, StringComparison.Ordinal);
                Assert.Contains("^BA", output, StringComparison.Ordinal);
                Assert.Contains("^BK", output, StringComparison.Ordinal);
                Assert.Contains("^B2", output, StringComparison.Ordinal);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null);
        }
    }

    private static TemplateRenderOptions Options(PrintProtocol protocol) => new TemplateRenderOptions
    {
        Protocol = protocol,
        EscPosLayout = EscPosLayoutMode.Raster,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false
    };

    private static BpTemplate CrossPageTemplate() => new BpTemplate
    {
        Version = 2,
        Width = 600,
        Height = 250,
        Rows = new List<BpRow>
        {
            TextRow("PageHeader", "PAGE-HEADER", 24),
            TextRow("TableHeader", "TABLE-HEADER", 24),
            new BpRow
            {
                Section = "Body",
                RepeatSource = "Items",
                Height = 150,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 600,
                        Elements = new List<BpElement>
                        {
                            Barcode(BarcodeType.Code39, "ABC-12", 0),
                            Barcode(BarcodeType.Code93, "ABC93", 36),
                            Barcode(BarcodeType.Codabar, "A12B", 72),
                            Barcode(BarcodeType.I2OF5C, "1234", 108)
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
                Width = 600,
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
        Width = 520,
        Height = 28,
        HumanReadable = false,
        StartCoordinate = new BpCoordinate { X = 8, Y = y },
        Field = new BgField { Value = value }
    };

    private static object Data() => new
    {
        Items = new[] { new { Id = 1 }, new { Id = 2 }, new { Id = 3 } }
    };

    private static bool ContainsSequence(byte[] data, byte[] sequence) => IndexOf(data, sequence) >= 0;

    private static int IndexOf(byte[] data, byte[] sequence)
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
            if (matched) return start;
        }
        return -1;
    }
}
