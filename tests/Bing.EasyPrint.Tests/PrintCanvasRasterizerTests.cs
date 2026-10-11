using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bing.EasyPrint.Tests;

/// <summary>验证模板图片在 ESC/POS Raster 输出中的 Alpha 合成和位打包。</summary>
public sealed class PrintCanvasRasterizerTests
{
    [Fact]
    public void Transparent_black_and_semitransparent_gray_are_composited_on_white_paper()
    {
        var payload = RenderRaster(
            new Rgba32(0, 0, 0, 0),
            new Rgba32(64, 64, 64, 128),
            new Rgba32(255, 255, 255, 255),
            new Rgba32(0, 0, 0, 255),
            new Rgba32(255, 255, 255, 0));

        // 只有不透明黑色像素应打印为黑点；第 5 个像素验证奇数宽度的尾部位保持为 0。
        Assert.Equal(new byte[] { 0x10 }, payload);
    }

    [Fact]
    public void Opaque_black_and_white_keep_the_existing_threshold_and_bit_order()
    {
        var payload = RenderRaster(
            new Rgba32(0, 0, 0, 255),
            new Rgba32(127, 127, 127, 255),
            new Rgba32(128, 128, 128, 255),
            new Rgba32(255, 255, 255, 255),
            new Rgba32(0, 0, 0, 255),
            new Rgba32(255, 255, 255, 255),
            new Rgba32(127, 127, 127, 255),
            new Rgba32(128, 128, 128, 255));

        Assert.Equal(new byte[] { 0xCA }, payload);
    }

    [Fact]
    public void Explicit_threshold_changes_non_cpcl_image_output()
    {
        var pixels = new[] { new Rgba32(128, 128, 128, 255) };
        Assert.Equal(new byte[] { 0x00 }, RenderRaster(pixels));
        Assert.Equal(new byte[] { 0x80 }, RenderRaster(129, pixels));
    }

    private static byte[] RenderRaster(params Rgba32[] pixels)
        => RenderRaster(128, pixels);

    private static byte[] RenderRaster(int threshold, params Rgba32[] pixels)
    {
        using var image = new Image<Rgba32>(pixels.Length, 1);
        for (var x = 0; x < pixels.Length; x++) image[x, 0] = pixels[x];

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);

        var template = new BpTemplate
        {
            Width = pixels.Length,
            Height = 1,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 1,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = pixels.Length,
                            Elements = new List<BpElement>
                            {
                                new BpImageElement
                                {
                                    Field = new BgField { Value = "image" },
                                    Width = pixels.Length,
                                    Height = 1
                                }
                            }
                        }
                    }
                }
            }
        };

        var result = new TemplateRenderer().Render(template, null,
            new ImageResources(stream.ToArray()), new TemplateRenderOptions
            {
                Encoding = Encoding.ASCII,
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                ImageThreshold = threshold,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var data = result.Pages.Single().Data;
        var marker = new byte[] { 0x1D, 0x76, 0x30, 0x00 };
        var offset = Find(data, marker);
        Assert.True(offset >= 0, "ESC/POS 输出应包含 GS v 0 光栅命令。");
        Assert.True(data.Length >= offset + 8, "ESC/POS 光栅头部不完整。");

        var rowBytes = data[offset + 4] | data[offset + 5] << 8;
        var height = data[offset + 6] | data[offset + 7] << 8;
        Assert.Equal((pixels.Length + 7) / 8, rowBytes);
        Assert.Equal(1, height);

        var payload = data.Skip(offset + 8).Take(rowBytes * height).ToArray();
        Assert.Equal(rowBytes * height, payload.Length);
        return payload;
    }

    private static int Find(byte[] data, byte[] pattern)
    {
        for (var index = 0; index <= data.Length - pattern.Length; index++)
        {
            var matched = true;
            for (var offset = 0; offset < pattern.Length; offset++)
                if (data[index + offset] != pattern[offset])
                {
                    matched = false;
                    break;
                }

            if (matched) return index;
        }

        return -1;
    }

    private sealed class ImageResources : ITemplateResourceProvider
    {
        private readonly byte[] _bytes;

        internal ImageResources(byte[] bytes) => _bytes = bytes;

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
            => new MemoryStream(_bytes, writable: false);
    }
}
