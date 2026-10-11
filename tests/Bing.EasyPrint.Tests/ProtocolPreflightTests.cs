using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证模板协议能力预检先于资源读取执行。
/// </summary>
public sealed class ProtocolPreflightTests
{
    [Fact]
    public void EscPos_native_rejects_line_before_loading_later_image()
    {
        var template = Template(
            new BpLineElement
            {
                StartCoordinate = Point(0, 0),
                LineEndCoordinate = Point(40, 0)
            },
            Image());
        var resources = new CountingResources(CreateImage());

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(template, null,
            resources, new TemplateRenderOptions
            {
                Encoding = Encoding.ASCII,
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Native
            }));

        AssertProtocolPath(error, PrintProtocol.EscPos, "Rows[0].Cells[0].Elements[0]");
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Zpl_rejects_unsupported_fim_before_loading_later_image()
    {
        var template = Template(
            new BpBarcodeElement
            {
                BarcodeType = BarcodeType.Fim,
                Height = 32,
                Field = Value("A")
            },
            Image());
        var resources = new CountingResources(CreateImage());

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(template, null,
            resources, new TemplateRenderOptions
            {
                Encoding = Encoding.ASCII,
                Protocol = PrintProtocol.Zpl
            }));

        AssertProtocolPath(error, PrintProtocol.Zpl, "Rows[0].Cells[0].Elements[0]");
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Zpl_rejects_extra_feed_before_loading_image_resource()
    {
        var resources = new CountingResources(CreateImage());

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(Image()), null, resources, new TemplateRenderOptions
            {
                Encoding = Encoding.ASCII,
                Protocol = PrintProtocol.Zpl,
                FeedLines = 1
            }));

        AssertProtocolPath(error, PrintProtocol.Zpl, "Pages[0]");
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Tspl_rejects_invalid_fixed_font_before_loading_image_resource()
    {
        var resources = new CountingResources(CreateImage());
        var template = Template(new BpFontElement
        {
            FontSize = 24, DeviceFontName = "9", Field = Value("OK")
        }, Image());

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            template, null, resources, new TemplateRenderOptions
            { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Tspl }));

        AssertProtocolPath(error, PrintProtocol.Tspl, "Rows[0].Cells[0].Elements[0]");
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Zpl_rejects_invalid_barcode_module_width_before_loading_image_resource()
    {
        var resources = new CountingResources(CreateImage());
        var template = Template(new BpBarcodeElement
        {
            BarcodeType = BarcodeType.Code128, Height = 32,
            NarrowModuleWidth = 0, Field = Value("1234")
        }, Image());

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            template, null, resources, new TemplateRenderOptions
            { Encoding = Encoding.ASCII, Protocol = PrintProtocol.Zpl }));

        AssertProtocolPath(error, PrintProtocol.Zpl, "Rows[0].Cells[0].Elements[0]");
        Assert.Equal(0, resources.OpenCount);
    }

    private static BpTemplate Template(params BpElement[] elements) => new BpTemplate
    {
        Width = 200,
        Height = 100,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>(elements) }
                }
            }
        }
    };

    private static BpImageElement Image() => new BpImageElement
    {
        Width = 8,
        Height = 8,
        Field = Value("logo")
    };

    private static BgField Value(string value) => new BgField { Value = value };

    private static BpCoordinate Point(int x, int y) => new BpCoordinate { X = x, Y = y };

    private static void AssertProtocolPath(TemplateRenderException error, PrintProtocol protocol, string path)
    {
        Assert.Equal(path, error.SourcePath);
        Assert.Contains("协议", error.Message);
        var message = error.Message.ToUpperInvariant();
        Assert.Contains(protocol == PrintProtocol.EscPos ? "ESC" : protocol.ToString().ToUpperInvariant(), message);
    }

    private static byte[] CreateImage()
    {
        using var image = new Image<Rgba32>(8, 8, new Rgba32(0, 0, 0));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed class CountingResources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;

        internal CountingResources(byte[] bytes) => this.bytes = bytes;

        internal int OpenCount { get; private set; }

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            OpenCount++;
            return new MemoryStream(bytes, writable: false);
        }
    }
}
