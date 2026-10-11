using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证同一份混合模板在四种协议下保留统一布局轨迹并生成对应协议输出。
/// </summary>
public sealed class ProtocolMixedTemplateTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Long_text_keeps_content_across_pages(PrintProtocol protocol)
    {
        var content = string.Concat(Enumerable.Repeat("AB12", 20));
        var template = new BpTemplate
        {
            Width = 120, Height = 48,
            Rows = new List<BpRow> { new BpRow
            {
                Cells = new List<BpCell> { new BpCell
                {
                    Elements = new List<BpElement> { new BpFontElement
                    {
                        FontSize = 24, Field = new BgField { Value = content }
                    } }
                } }
            } }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = protocol, Encoding = Encoding.ASCII,
                EscPosLayout = EscPosLayoutMode.Raster,
                EscPosDevice = protocol == PrintProtocol.EscPos
                    ? new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() } : null
            });

        Assert.True(result.Pages.Count > 1);
        Assert.Equal(content, string.Concat(result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type == 1).Select(element => element.Text)));
        Assert.All(result.Pages, page => Assert.NotEmpty(page.Data));
    }

    [Fact]
    public void Same_mixed_template_preserves_layout_across_all_protocols()
    {
        var template = CreateTemplate();
        var imageBytes = CreateImage();
        var renderer = new TemplateRenderer();
        var results = new Dictionary<PrintProtocol, TemplateRenderResult>();

        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.Tspl, PrintProtocol.Zpl, PrintProtocol.EscPos })
        {
            results.Add(protocol, renderer.RenderWithLayout(template, null, new ImageResources(imageBytes),
                new TemplateRenderOptions
                {
                    Encoding = Encoding.ASCII,
                    Protocol = protocol,
                    EscPosLayout = EscPosLayoutMode.Raster,
                    EscPosDevice = protocol == PrintProtocol.EscPos
                        ? new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() } : null
                }));
        }

        var expectedPaths = new[]
        {
            "Rows[0].Cells[0].Elements[0].Elements[0]",
            "Rows[0].Cells[0].Elements[0].Elements[1]",
            "Rows[0].Cells[0].Elements[0].Elements[2]",
            "Rows[0].Cells[0].Elements[0].Elements[3]",
            "Rows[0].Cells[0].Elements[0].Elements[4]",
            "Rows[0].Cells[0].Elements[0].Elements[5]",
            "Rows[0].Cells[0].Elements[0]"
        };
        var expectedTypes = new[] { 1, 5, 7, 4, 3, 2, 6 };
        var expectedPositions = new[]
        {
            (X: 8, Y: 8),
            (X: 8, Y: 32),
            (X: 250, Y: 8),
            (X: 8, Y: 48),
            (X: 250, Y: 48),
            (X: 8, Y: 100),
            (X: 0, Y: 0)
        };

        foreach (var pair in results)
        {
            var result = pair.Value;
            var page = Assert.Single(result.Pages);
            var elements = page.Elements.Where(element => element.Kind == "element").ToArray();

            Assert.Equal(pair.Key, result.Protocol);
            Assert.Equal(pair.Key, page.Protocol);
            Assert.Equal(320, page.Width);
            Assert.Equal(180, page.Height);
            Assert.NotEmpty(page.Data);
            Assert.Equal(expectedPaths, elements.Select(element => element.SourcePath));
            Assert.Equal(expectedTypes, elements.Select(element => element.Type));
            Assert.Equal(expectedPositions, elements.Select(element => (element.X, element.Y)));
            Assert.All(elements, element => Assert.True(element.Width > 0 && element.Height > 0));
        }

        Assert.True(ContainsSequence(results[PrintProtocol.Cpcl].Pages[0].Data,
            Encoding.ASCII.GetBytes("! 0 200 200")), "CPCL 输出缺少页面开始标记。");
        Assert.True(ContainsSequence(results[PrintProtocol.Tspl].Pages[0].Data,
            Encoding.ASCII.GetBytes("SIZE 320 dot,180 dot")), "TSPL 输出缺少 SIZE 标记。");
        Assert.True(ContainsSequence(results[PrintProtocol.Tspl].Pages[0].Data,
            Encoding.ASCII.GetBytes(",28,1,0,2,5,\"1234\"")),
            "TSPL 条码应使用由 CPCL 比例码换算得到的实际窄条和宽条点数。");
        Assert.True(ContainsSequence(results[PrintProtocol.Zpl].Pages[0].Data,
            Encoding.ASCII.GetBytes("^XA")), "ZPL 输出缺少 ^XA 标记。");
        Assert.True(ContainsSequence(results[PrintProtocol.Zpl].Pages[0].Data,
            Encoding.ASCII.GetBytes("^BY2")), "ZPL 条码应使用实际模块宽度。");
        Assert.True(ContainsSequence(results[PrintProtocol.EscPos].Pages[0].Data,
            new byte[] { 0x1D, 0x76, 0x30 }), "ESC/POS 输出缺少光栅标记。");
    }

    private static BpTemplate CreateTemplate() => new BpTemplate
    {
        Version = 2,
        Width = 320,
        Height = 180,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Height = 160,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 320,
                        Elements = new List<BpElement>
                        {
                            new BpGroupElement
                            {
                                Width = 320,
                                Height = 160,
                                Elements = new List<BpElement>
                                {
                                    new BpFontElement
                                    {
                                        FontSize = 24,
                                        Field = new BgField { Value = "HELLO" },
                                        StartCoordinate = Point(8, 8)
                                    },
                                    new BpLineElement
                                    {
                                        LineWidth = 1,
                                        StartCoordinate = Point(8, 32),
                                        LineEndCoordinate = Point(100, 32)
                                    },
                                    new BpRectangleElement
                                    {
                                        Width = 60,
                                        Height = 28,
                                        LineWidth = 1,
                                        StartCoordinate = Point(250, 8)
                                    },
                                    new BpBarcodeElement
                                    {
                                        BarcodeType = BarcodeType.Code128,
                                        LineWidth = 1,
                                        Ratio = 2,
                                        Height = 28,
                                        Field = new BgField { Value = "1234" },
                                        StartCoordinate = Point(8, 48)
                                    },
                                    new BpQrCodeElement
                                    {
                                        UnitWidth = 1,
                                        ErrorLevel = "M",
                                        Field = new BgField { Value = "QR" },
                                        StartCoordinate = Point(250, 48)
                                    },
                                    new BpImageElement
                                    {
                                        Width = 8,
                                        Height = 8,
                                        Field = new BgField { Value = "logo" },
                                        StartCoordinate = Point(8, 100)
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    };

    private static BpCoordinate Point(int x, int y) => new BpCoordinate { X = x, Y = y };

    private static byte[] CreateImage()
    {
        using var image = new Image<Rgba32>(8, 8, new Rgba32(0, 0, 0));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed class ImageResources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;

        internal ImageResources(byte[] bytes) => this.bytes = bytes;

        public Stream OpenRead(string resourceKey, System.Threading.CancellationToken cancellationToken)
            => resourceKey == "logo" ? new MemoryStream(bytes, writable: false) : null;
    }

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
        {
            if (data.Skip(start).Take(sequence.Length).SequenceEqual(sequence)) return true;
        }
        return false;
    }
}
