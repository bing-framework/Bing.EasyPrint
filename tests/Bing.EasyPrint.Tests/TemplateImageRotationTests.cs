using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TemplateImageRotationTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Image_rotation_round_trips_and_matches_the_rotated_packed_pixels(PrintProtocol protocol)
    {
        foreach (var angle in new[] { 0, 90, 180, 270 })
        foreach (var compressed in new[] { false, true })
        {
            var picture = Picture((RotationAngle)angle, compressed);
            var template = Template(24, 12, picture);
            template.AutoHeight = true;
            var loader = new TemplateLoader();
            var loaded = loader.LoadJson(loader.SaveJson(template));
            var copy = Assert.IsType<BpImageElement>(loaded.Rows[0].Cells[0].Elements[0]);
            Assert.Equal(picture.Rotation, copy.Rotation);
            Assert.Equal((3, 2, compressed), (copy.Width, copy.Height, copy.IsCompress));
            var page = Assert.Single(new TemplateRenderer().RenderWithLayout(loaded, null,
                new Resources(), Options(protocol)).Pages);
            var quarterTurn = angle == 90 || angle == 270;
            var trace = Assert.Single(page.Elements.Where(element => element.Type == 2));
            Assert.Equal((2, 1, quarterTurn ? 2 : 3, quarterTurn ? 3 : 2),
                (trace.X, trace.Y, trace.Width, trace.Height));
            Assert.Equal((RotationAngle)angle, trace.Rotation);
            Assert.Equal("logo", trace.ResourceKey);
            Assert.Equal(quarterTurn ? 4 : 3, page.Height);
            byte[] raster = angle == 0 ? new byte[] { 0xA0, 0xC0 }
                : angle == 90 ? new byte[] { 0xC0, 0x80, 0x40 }
                : angle == 180 ? new byte[] { 0x60, 0xA0 } : new byte[] { 0x80, 0x40, 0xC0 };
            var expected = new PrintDocument();
            expected.AddPage(page.Width, page.Height).Add(new PrintImageElement(2, 1,
                quarterTurn ? 2 : 3, quarterTurn ? 3 : 2, raster, compressed));
            Assert.Equal(PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster)
                .Render(expected)[0], page.Data);
            Assert.Equal(page.Data, new TemplateRenderer().Render(template, null, new Resources(), Options(protocol)).Pages[0].Data);
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, 2, 2)]
    [InlineData(PrintProtocol.Cpcl, 4, 3)]
    [InlineData(PrintProtocol.Cpcl, 6, 5)]
    [InlineData(PrintProtocol.Tspl, 2, 2)]
    [InlineData(PrintProtocol.Tspl, 4, 3)]
    [InlineData(PrintProtocol.Tspl, 6, 5)]
    [InlineData(PrintProtocol.Zpl, 2, 2)]
    [InlineData(PrintProtocol.Zpl, 4, 3)]
    [InlineData(PrintProtocol.Zpl, 6, 5)]
    [InlineData(PrintProtocol.EscPos, 2, 2)]
    [InlineData(PrintProtocol.EscPos, 4, 3)]
    [InlineData(PrintProtocol.EscPos, 6, 5)]
    public void Explicit_image_size_is_scaled_before_protocol_encoding(PrintProtocol protocol,
        int targetWidth, int targetHeight)
    {
        foreach (var angle in new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate270 })
        {
            var picture = ScaledPicture(targetWidth, targetHeight, angle);
            var template = Template(20, 20, picture);
            template.AutoHeight = true;
            var page = Assert.Single(new TemplateRenderer().RenderWithLayout(template, null,
                new ScaledResources(), Options(protocol)).Pages);
            var quarterTurn = angle == RotationAngle.Rotate90 || angle == RotationAngle.Rotate270;
            var outputWidth = quarterTurn ? targetHeight : targetWidth;
            var outputHeight = quarterTurn ? targetWidth : targetHeight;
            var trace = Assert.Single(page.Elements.Where(element => element.Type == 2));

            Assert.Equal((20, 1 + outputHeight), (page.Width, page.Height));
            Assert.Equal((2, 1, outputWidth, outputHeight, angle, "logo"),
                (trace.X, trace.Y, trace.Width, trace.Height, trace.Rotation, trace.ResourceKey));

            AssertProtocolImageDimensions(protocol, page.Data, outputWidth, outputHeight,
                page.Width, page.Height);
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Image_crop_is_applied_before_aspect_ratio_scaling_and_json_round_trip(PrintProtocol protocol)
    {
        foreach (var angle in new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate270 })
        {
            var picture = CroppedPicture(0, 0, 2, 3, 4, 0);
            picture.Rotation = angle;
            var template = Template(20, 20, picture);
            template.AutoHeight = true;
            var loader = new TemplateLoader();
            var loaded = loader.LoadJson(loader.SaveJson(template));
            var copy = Assert.IsType<BpImageElement>(loaded.Rows[0].Cells[0].Elements[0]);
            Assert.Equal((0, 0, 2, 3), (copy.Crop.X, copy.Crop.Y, copy.Crop.Width, copy.Crop.Height));

            var page = Assert.Single(new TemplateRenderer().RenderWithLayout(loaded, null,
                new ScaledResources(), Options(protocol)).Pages);
            var trace = Assert.Single(page.Elements.Where(element => element.Type == 2));
            var quarterTurn = angle == RotationAngle.Rotate90 || angle == RotationAngle.Rotate270;
            var outputWidth = quarterTurn ? 6 : 4;
            var outputHeight = quarterTurn ? 4 : 6;

            // 裁切源图为 2×3；指定宽度 4、未指定高度时应按裁切后的宽高比得到 4×6。
            Assert.Equal((outputWidth, outputHeight), (trace.Width, trace.Height));
            Assert.Equal(1 + outputHeight, page.Height);
            AssertProtocolImageDimensions(protocol, page.Data, outputWidth, outputHeight, page.Width, page.Height);
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Different_source_crops_do_not_share_decoded_cache(PrintProtocol protocol)
    {
        var differentCrops = new TemplateRenderer().RenderWithLayout(
            CroppedRowsTemplate(0, 2), null, new ScaledResources(), Options(protocol));
        var sameCrop = new TemplateRenderer().RenderWithLayout(
            CroppedRowsTemplate(0, 0), null, new ScaledResources(), Options(protocol));

        Assert.False(differentCrops.Pages[0].Data.SequenceEqual(sameCrop.Pages[0].Data));
    }

    [Fact]
    public void Image_crop_outside_source_is_rejected_with_element_path()
    {
        var image = CroppedPicture(3, 2, 2, 2, 2, 2);
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Template(20, 20, image), null, new ScaledResources(), Options(PrintProtocol.Zpl)));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("裁切区域", error.Message);
    }

    [Theory]
    [InlineData(-1, 0, 2, 2)]
    [InlineData(0, -1, 2, 2)]
    [InlineData(0, 0, 0, 2)]
    [InlineData(0, 0, 2, 0)]
    public void Invalid_image_crop_geometry_is_rejected_before_opening_resource(
        int x, int y, int width, int height)
    {
        var image = CroppedPicture(x, y, width, height, 2, 2);
        var resources = new ScaledResources();
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Template(20, 20, image), null, resources, Options(PrintProtocol.Zpl)));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("裁切区域", error.Message);
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Default_cpcl_image_keeps_legacy_direction_and_bytes()
    {
        var renderer = new TemplateRenderer();
        var template = Template(24, 12, Picture(RotationAngle.None));
        var loader = new TemplateLoader();
        var oldJson = loader.SaveJson(template).Replace("\"Rotation\": 0,", "");
        var loaded = loader.LoadJson(oldJson);
        Assert.Equal(renderer.Render(template, null, new Resources(), Options(PrintProtocol.Cpcl)).Pages[0].Data,
            renderer.Render(loaded, null, new Resources(), Options(PrintProtocol.Cpcl)).Pages[0].Data);
        Assert.Contains("EG 1 2 2 1 A0C0", Encoding.ASCII.GetString(renderer.Render(template, null,
            new Resources(), Options(PrintProtocol.Cpcl)).Pages[0].Data));
    }

    [Theory]
    [InlineData(90)]
    [InlineData(270)]
    public void Cell_and_end_coordinate_checks_use_rotated_dimensions(int angle)
    {
        var image = Picture((RotationAngle)angle);
        image.StartCoordinate = new BpCoordinate();
        var template = Template(2, 10, image);
        new TemplateRenderer().Render(template, null, new Resources(), Options(PrintProtocol.Zpl));
        image.EndCoordinate = new BpCoordinate { X = 2, Y = 2 };
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, null, new Resources(), Options(PrintProtocol.Zpl)));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("结束坐标", error.Message);
        image.EndCoordinate = null;
        image.Height = 3;
        Assert.Contains("图片旋转", Assert.Throws<TemplateRenderException>(() => new TemplateRenderer()
            .RenderWithLayout(template, null, new Resources(), Options(PrintProtocol.Zpl))).Message);
    }

    [Fact]
    public void Rotated_image_moves_as_one_atom_to_the_next_page()
    {
        var picture = Picture(RotationAngle.Rotate90);
        picture.StartCoordinate = null;
        var template = Template(24, 4, picture);
        template.Rows.Insert(0, new BpRow { Height = 3, Cells = new List<BpCell> { new BpCell() } });
        var pages = new TemplateRenderer().RenderWithLayout(template, null, new Resources(), Options(PrintProtocol.Tspl)).Pages;
        Assert.Equal(2, pages.Count);
        var trace = Assert.Single(pages[1].Elements.Where(element => element.Type == 2));
        Assert.Equal((0, 0, 2, 3), (trace.X, trace.Y, trace.Width, trace.Height));
    }

    [Fact]
    public void Native_rejects_before_opening_an_image_resource()
    {
        var resources = new Resources();
        var options = Options(PrintProtocol.EscPos);
        options.EscPosLayout = EscPosLayoutMode.Native;
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Template(24, 12, Picture(RotationAngle.Rotate90)), null, resources, options));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Equal(0, resources.OpenCount);
        Assert.Equal(new[] { RotationAngle.None }, TemplateProtocolCapabilities.Create(options).ImageRotations);
    }

    [Theory]
    [InlineData("45")]
    [InlineData("90.0")]
    [InlineData("\"90\"")]
    [InlineData("null")]
    public void Invalid_json_image_rotation_is_rejected(string angle)
    {
        Assert.Throws<JsonSerializationException>(() => new TemplateLoader().LoadJson(
            "{\"Version\":1,\"Width\":24,\"Height\":12,\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":2,\"Rotation\":" + angle + "}]}]}]}"));
    }

    private static TemplateRenderOptions Options(PrintProtocol protocol) => new TemplateRenderOptions
    { Protocol = protocol, Encoding = Encoding.ASCII, EscPosLayout = EscPosLayoutMode.Raster,
        FeedLines = 0, CutAfterPrint = false };

    private static BpImageElement Picture(RotationAngle rotation, bool compressed = false) => new BpImageElement
    { Width = 3, Height = 2, Rotation = rotation, IsCompress = compressed,
        StartCoordinate = new BpCoordinate { X = 2, Y = 1 }, Field = new BgField { Value = "logo" } };

    private static BpImageElement ScaledPicture(int width, int height, RotationAngle rotation) => new BpImageElement
    { Width = width, Height = height, Rotation = rotation,
        StartCoordinate = new BpCoordinate { X = 2, Y = 1 }, Field = new BgField { Value = "logo" } };

    private static BpImageElement CroppedPicture(int cropX, int cropY, int cropWidth, int cropHeight,
        int targetWidth, int targetHeight, int startX = 2, int startY = 1) => new BpImageElement
    { Width = targetWidth, Height = targetHeight,
        Crop = new BpImageCrop { X = cropX, Y = cropY, Width = cropWidth, Height = cropHeight },
        StartCoordinate = new BpCoordinate { X = startX, Y = startY }, Field = new BgField { Value = "logo" } };

    private static BpTemplate CroppedRowsTemplate(int firstCropX, int secondCropX) => new BpTemplate
    { Width = 8, AutoHeight = true, Rows = new List<BpRow>
        {
            new BpRow { Height = 3, Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement>
                { CroppedPicture(firstCropX, 0, 2, 3, 2, 3, 0, 0) } } } },
            new BpRow { Height = 3, Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement>
                { CroppedPicture(secondCropX, 0, 2, 3, 2, 3, 0, 0) } } } }
        } };

    private static BpTemplate Template(int width, int height, BpImageElement image) => new BpTemplate
    { Width = width, Height = height, Rows = new List<BpRow>
        { new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> { image } } } } } };

    private static void AssertProtocolImageDimensions(PrintProtocol protocol, byte[] data,
        int width, int height, int pageWidth, int pageHeight)
    {
        var rowBytes = (width + 7) / 8;
        switch (protocol)
        {
            case PrintProtocol.Cpcl:
                Assert.Contains($"EG {rowBytes} {height} 2 1 ", Encoding.ASCII.GetString(data));
                break;
            case PrintProtocol.Tspl:
                Assert.Contains($"BITMAP 2,1,{rowBytes},{height},0,", Encoding.ASCII.GetString(data));
                break;
            case PrintProtocol.Zpl:
                var totalBytes = checked(rowBytes * height);
                Assert.Contains($"^FO2,1^GFA,{totalBytes},{totalBytes},{rowBytes},",
                    Encoding.ASCII.GetString(data));
                break;
            case PrintProtocol.EscPos:
                var marker = new byte[] { 0x1D, 0x76, 0x30, 0x00 };
                var offset = Find(data, marker);
                Assert.True(offset >= 0, "ESC/POS 输出应包含 GS v 0 光栅命令。");
                Assert.True(data.Length >= offset + 8, "ESC/POS 光栅头部不完整。");
                // Raster Provider 将图片合成整页；单图目标尺寸由上方布局轨迹断言验证。
                Assert.Equal((pageWidth + 7) / 8, data[offset + 4] | data[offset + 5] << 8);
                Assert.Equal(pageHeight, data[offset + 6] | data[offset + 7] << 8);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol));
        }
    }

    private static int Find(byte[] data, byte[] pattern)
    {
        for (var index = 0; index <= data.Length - pattern.Length; index++)
        {
            var matched = true;
            for (var offset = 0; offset < pattern.Length; offset++)
                if (data[index + offset] != pattern[offset]) { matched = false; break; }
            if (matched) return index;
        }
        return -1;
    }

    private sealed class Resources : ITemplateResourceProvider
    {
        internal int OpenCount;
        public Stream OpenRead(string key, CancellationToken cancellationToken)
        {
            OpenCount++;
            using var image = new Image<Rgba32>(3, 2, new Rgba32(255, 255, 255));
            foreach (var point in new[] { (0, 0), (2, 0), (0, 1), (1, 1) }) image[point.Item1, point.Item2] = new Rgba32(0, 0, 0);
            var stream = new MemoryStream();
            image.SaveAsPng(stream);
            stream.Position = 0;
            return stream;
        }
    }

    private sealed class ScaledResources : ITemplateResourceProvider
    {
        internal int OpenCount;

        public Stream OpenRead(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            using var image = new Image<Rgba32>(4, 3, new Rgba32(255, 255, 255));
            foreach (var point in new[] { (0, 0), (3, 0), (1, 1), (2, 2), (3, 2) })
                image[point.Item1, point.Item2] = new Rgba32(0, 0, 0);
            var stream = new MemoryStream();
            image.SaveAsPng(stream);
            stream.Position = 0;
            return stream;
        }
    }
}
