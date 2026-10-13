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

public sealed class EscPosRasterReverseTextTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Reverse_raster_keeps_layout_background_glyph_and_underline_together(int rotation)
    {
        var fonts = FixedTestFonts.Catalog();
        const string family = "Primary";
        const string value = "Ag";
        const int size = 24;
        var source = EscPosRasterCanvas.RasterizeText(value, size, true, true, family, 0, true, fonts);
        var rotated = EscPosRasterCanvas.RasterizeText(value, size, true, true, family, rotation, true, fonts);

        var expectedWidth = rotation == 90 || rotation == 270 ? source.Height : source.Width;
        var expectedHeight = rotation == 90 || rotation == 270 ? source.Width : source.Height;
        Assert.Equal(expectedWidth, rotated.Width);
        Assert.Equal(expectedHeight, rotated.Height);
        AssertBitmapRotation(source, rotated, rotation);

        var black = CountBlack(rotated.Data, rotated.RowBytes, rotated.Width, rotated.Height);
        Assert.InRange(black, 1, rotated.Width * rotated.Height - 1);

        var canvas = new EscPosRasterCanvas(rotated.Width, rotated.Height, fonts);
        canvas.DrawText(0, 0, value, size, rotation, bold: true, reverse: true,
            underline: true, fontFamily: family);
        Assert.Equal(rotated.Data, canvas.ToBytes());
    }

    [Fact]
    public void Reverse_background_uses_layout_bounds_and_empty_text_draws_nothing()
    {
        var fonts = FixedTestFonts.Catalog();
        var bounds = EscPosRasterCanvas.MeasureReverseTextBounds("   ", 24, false, false,
            "Primary", long.MaxValue, fonts, default);
        Assert.True(bounds.Width > 0);
        Assert.Equal(24, bounds.Height);

        var canvas = new EscPosRasterCanvas(bounds.Width, bounds.Height, fonts);
        canvas.DrawText(0, 0, "   ", 24, bold: false, reverse: true, underline: false,
            fontFamily: "Primary");
        Assert.Equal(bounds.Width * bounds.Height, CountBlack(canvas.ToBytes(), canvas.RowBytes,
            canvas.Width, canvas.Height));

        var empty = new EscPosRasterCanvas(8, 8, fonts);
        empty.DrawText(0, 0, string.Empty, 24, bold: false, reverse: true, underline: false,
            fontFamily: "Primary");
        Assert.All(empty.ToBytes(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void Reverse_raster_checks_exact_bounds_and_cleans_up_after_failure()
    {
        var fonts = FixedTestFonts.Catalog();
        var raster = EscPosRasterCanvas.RasterizeText("A", 24, false, false, "Primary", 0, true, fonts);
        var clipped = new EscPosRasterCanvas(raster.Width - 1, raster.Height, fonts);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            clipped.DrawText(0, 0, "A", 24, 0, false, true, false, "Primary"));
        Assert.All(clipped.ToBytes(), value => Assert.Equal((byte)0, value));

        var exact = new EscPosRasterCanvas(raster.Width, raster.Height, fonts);
        exact.DrawText(0, 0, "A", 24, 0, false, true, false, "Primary");
        Assert.Equal(raster.Data, exact.ToBytes());
    }

    [Fact]
    public void Raster_template_rotated_reverse_text_moves_to_next_page_as_one_block()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 120,
            Height = 50,
            Rows = new List<BpRow>
            {
                Row(40, new BpFontElement { FontSize = 24, Field = new BgField { Value = "HEAD" } }),
                Row(0, new BpFontElement
                {
                    FontSize = 24,
                    FontStyle = 5,
                    Reverse = true,
                    Rotation = RotationAngle.Rotate90,
                    Width = 30,
                    Field = new BgField { Value = "AB" }
                })
            }
        };
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII,
            EscPosDevice = new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() },
            FeedLines = 0,
            CutAfterPrint = false
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null, options: options);

        Assert.Equal(2, result.PageCount);
        Assert.Equal("HEAD", Assert.Single(result.Pages[0].Elements.Where(element => element.Type == 1)).Text);
        var rotated = result.Pages[1].Elements.Where(element => element.Type == 1).ToArray();
        Assert.Equal(new[] { "A", "B" }, rotated.Select(element => element.Text));
        Assert.All(rotated, element => Assert.Equal(RotationAngle.Rotate90, element.Rotation));
        Assert.All(rotated, element => Assert.True(element.Width > 0 && element.Height > 0));
        Assert.True(Contains(result.Pages[1].Data, new byte[] { 0x1D, 0x76, 0x30 }));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_protocols_require_opt_in_and_advertise_reverse_text_only_when_enabled(PrintProtocol protocol)
    {
        var plain = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = protocol,
            Encoding = Encoding.ASCII
        });
        Assert.False(plain.SupportsReverseText);

        var options = new TemplateRenderOptions
        {
            Protocol = protocol,
            Encoding = Encoding.ASCII,
            TsplMedia = protocol == PrintProtocol.Tspl ? new TsplMediaOptions
                { AllowRasterizedTextStyles = true } : null,
            ZplMedia = protocol == PrintProtocol.Zpl ? new ZplMediaOptions
                { AllowRasterizedTextStyles = true } : null
        };
        var enabled = TemplateProtocolCapabilities.Create(options);
        Assert.True(enabled.SupportsReverseText);

        var document = new PrintDocument();
        document.AddPage(120, 80).Add(new PrintTextElement(5, 5, "A", 24, reverse: true,
            rotation: RotationAngle.Rotate90, width: 32, height: 24));
        var output = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            tsplOptions: options.TsplMedia, zplOptions: options.ZplMedia).Render(document);
        var bytes = Assert.Single(output);
        Assert.Contains(bytes, value => value != 0);
        Assert.True(Contains(bytes, protocol == PrintProtocol.Tspl
            ? Encoding.ASCII.GetBytes("BITMAP") : Encoding.ASCII.GetBytes("^GFA")));

        Assert.Throws<PrintCapabilityException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Render(document));
    }

    private static void AssertBitmapRotation((int Width, int Height, int RowBytes, byte[] Data) source,
        (int Width, int Height, int RowBytes, byte[] Data) rotated, int rotation)
    {
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            var targetX = rotation == 90 ? rotated.Width - 1 - y :
                rotation == 180 ? rotated.Width - 1 - x : rotation == 270 ? y : x;
            var targetY = rotation == 90 ? x :
                rotation == 180 ? rotated.Height - 1 - y : rotation == 270 ? rotated.Height - 1 - x : y;
            Assert.Equal(IsBlack(source.Data, source.RowBytes, x, y),
                IsBlack(rotated.Data, rotated.RowBytes, targetX, targetY));
        }
    }

    private static int CountBlack(byte[] data, int rowBytes, int width, int height)
        => Enumerable.Range(0, height).Sum(y => Enumerable.Range(0, width)
            .Count(x => IsBlack(data, rowBytes, x, y)));

    private static bool IsBlack(byte[] data, int rowBytes, int x, int y)
        => (data[y * rowBytes + x / 8] & (0x80 >> (x & 7))) != 0;

    private static bool Contains(byte[] source, byte[] value)
    {
        for (var start = 0; start <= source.Length - value.Length; start++)
            if (source.Skip(start).Take(value.Length).SequenceEqual(value)) return true;
        return false;
    }

    private static BpRow Row(int height, params BpElement[] elements) => new BpRow
    {
        Height = height,
        Cells = new List<BpCell> { new BpCell { Width = 120, Elements = elements.ToList() } }
    };
}
