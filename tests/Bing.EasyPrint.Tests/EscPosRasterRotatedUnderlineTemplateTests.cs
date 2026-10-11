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

/// <summary>验证 ESC/POS Raster 模板旋转下划线的能力、墨迹和占位边界。</summary>
public sealed class EscPosRasterRotatedUnderlineTemplateTests
{
    private const string TextValue = "A";
    private const int FontSize = 24;

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_template_accepts_underlined_text_at_all_angles_and_preserves_ink(
        RotationAngle rotation)
    {
        var fonts = FixedTestFonts.Catalog();
        var family = EscPosRasterCanvas.ResolveFontFamilyName(TextValue, null, false, fonts);
        var options = RasterOptions(fonts);
        var underlined = Render(Template(rotation, true, family), options);
        var plain = Render(Template(rotation, false, family), options);
        var underlinedPage = Assert.Single(underlined.Pages);
        var plainPage = Assert.Single(plain.Pages);
        var underlinedTrace = Assert.Single(underlinedPage.Elements.Where(element => element.Type == 1));
        var plainTrace = Assert.Single(plainPage.Elements.Where(element => element.Type == 1));

        Assert.Equal(rotation, underlinedTrace.Rotation);
        Assert.True(underlinedTrace.Width > 0 && underlinedTrace.Height > 0);
        Assert.True(underlinedTrace.Width >= plainTrace.Width || underlinedTrace.Height >= plainTrace.Height,
            "下划线应并入旋转后的布局占位，而不是被静默丢弃。");
        Assert.True(ContainsSequence(underlinedPage.Data, new byte[] { 0x1D, 0x76, 0x30 }));

        var underlinedPixels = CountBlackPixels(ReadRasterPayload(underlinedPage.Data));
        var plainPixels = CountBlackPixels(ReadRasterPayload(plainPage.Data));
        Assert.True(underlinedPixels > plainPixels,
            $"旋转 {rotation} 的下划线没有产生额外墨迹：underlined={underlinedPixels}, plain={plainPixels}。");
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_template_preflight_uses_the_rotated_underline_placeholder(
        RotationAngle rotation)
    {
        var fonts = FixedTestFonts.Catalog();
        var family = EscPosRasterCanvas.ResolveFontFamilyName(TextValue, null, false, fonts);
        var options = RasterOptions(fonts);
        var successful = Render(Template(rotation, true, family, 200, 200), options);
        var trace = Assert.Single(successful.Pages.Single().Elements.Where(element => element.Type == 1));

        Assert.True(trace.X + trace.Width <= successful.Pages.Single().Width);
        Assert.True(trace.Y + trace.Height <= successful.Pages.Single().Height);

        var tooNarrow = Template(rotation, true, family,
            Math.Max(1, trace.X + trace.Width - 1), successful.Pages.Single().Height);
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(tooNarrow, null, options: options));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("旋转", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    public void Non_raster_template_rotated_underline_remains_strictly_rejected(
        PrintProtocol protocol, EscPosLayoutMode layout)
    {
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(Template(RotationAngle.Rotate90, true), null,
                options: Options(protocol, layout)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains(protocol.ToString().ToUpperInvariant(), error.Message.ToUpperInvariant());
        Assert.True(error.Message.Contains("下划线") || error.Message.Contains("文字旋转"),
            $"应报告旋转下划线能力边界，实际错误：{error.Message}");
    }

    private static TemplateRenderOptions RasterOptions(RasterFontCatalog fonts)
        => new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            EscPosDevice = new EscPosDeviceOptions { RasterFonts = fonts },
            Encoding = Encoding.ASCII,
            FeedLines = 0,
            CutAfterPrint = false
        };

    private static TemplateRenderOptions Options(PrintProtocol protocol, EscPosLayoutMode layout)
        => new TemplateRenderOptions
        {
            Protocol = protocol,
            EscPosLayout = layout,
            Encoding = Encoding.ASCII,
            FeedLines = 0,
            CutAfterPrint = false
        };

    private static TemplateRenderResult Render(BpTemplate template, TemplateRenderOptions options)
        => new TemplateRenderer().RenderWithLayout(template, null, options: options);

    private static BpTemplate Template(RotationAngle rotation, bool underline,
        string family = null, int width = 200, int height = 200)
        => new BpTemplate
        {
            Version = 3,
            Width = width,
            Height = height,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = width,
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = FontSize,
                                    FontStyle = underline ? 4 : 0,
                                    Font = family == null ? null : new PrintFont(PrintFontKind.Host, family),
                                    Rotation = rotation,
                                    Width = 48,
                                    Field = new BgField { Value = TextValue }
                                }
                            }
                        }
                    }
                }
            }
        };

    private static byte[] ReadRasterPayload(byte[] data)
    {
        var marker = new byte[] { 0x1D, 0x76, 0x30 };
        var index = Find(data, marker);
        Assert.True(index >= 0, "ESC/POS Raster 输出应包含 GS v 0 光栅命令。");
        Assert.True(data.Length >= index + 8, "ESC/POS Raster 头部不完整。");
        var rowBytes = data[index + 4] | data[index + 5] << 8;
        var height = data[index + 6] | data[index + 7] << 8;
        var length = checked(rowBytes * height);
        Assert.True(data.Length >= index + 8 + length, "ESC/POS Raster 数据不完整。");
        var payload = new byte[length];
        Array.Copy(data, index + 8, payload, 0, length);
        return payload;
    }

    private static int CountBlackPixels(byte[] payload)
    {
        var total = 0;
        foreach (var value in payload)
        {
            var bits = value;
            while (bits != 0)
            {
                total += bits & 1;
                bits >>= 1;
            }
        }
        return total;
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

    private static bool ContainsSequence(byte[] data, byte[] pattern)
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
            if (matched) return true;
        }
        return false;
    }
}
