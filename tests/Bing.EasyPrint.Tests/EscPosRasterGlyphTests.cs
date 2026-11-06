using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using SixLabors.Fonts;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 Raster 在度量及绘制前拒绝缺字和非法 Unicode，失败保留画布。</summary>
public sealed class EscPosRasterGlyphTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_font_rejects_missing_supplementary_glyph(bool bold)
    {
        var catalog = FixedTestFonts.Catalog();
        var family = EscPosRasterCanvas.ResolveFontFamilyName("A", null, false, catalog);
        var error = Assert.Throws<NotSupportedException>(() =>
            EscPosRasterCanvas.MeasureTextWidth(char.ConvertFromUtf32(0x10FFFF), 24, bold, family, catalog));
        Assert.Contains("U+10FFFF", error.Message);
    }

    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public void Invalid_surrogate_is_rejected_before_measurement(int scalar)
    {
        var text = new string((char)scalar, 1);
        Assert.Throws<ArgumentException>(() =>
            EscPosRasterCanvas.MeasureTextWidth(text, 24, false, null, FixedTestFonts.Catalog()));
    }

    [Fact]
    public void Missing_glyph_leaves_previous_canvas_pixels_unchanged()
    {
        var catalog = FixedTestFonts.Catalog();
        var canvas = new EscPosRasterCanvas(120, 64, catalog);
        canvas.DrawLine(2, 2, 20, 2);
        var before = canvas.ToBytes();
        var family = EscPosRasterCanvas.ResolveFontFamilyName("A", null, false, catalog);
        Assert.Throws<NotSupportedException>(() => canvas.DrawText(8, 8,
            char.ConvertFromUtf32(0x10FFFF), 24, fontFamily: family));
        Assert.Equal(before, canvas.ToBytes());
    }

    [Fact]
    public void Provider_reports_missing_glyph_at_exact_element_location()
    {
        var catalog = FixedTestFonts.Catalog();
        var document = new PrintDocument();
        document.AddPage(200, 80).Add(new PrintTextElement(8, 8,
            char.ConvertFromUtf32(0x10FFFF), 24,
            fontFamily: EscPosRasterCanvas.ResolveFontFamilyName("A", null, false, catalog)), "Receipt.Missing");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.UTF8,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { RasterFonts = catalog });
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(document));
        Assert.Equal("Receipt.Missing", error.ElementPath);
        Assert.Equal(0, error.PageIndex);
        Assert.Equal(0, error.ElementIndex);
        Assert.Contains("U+10FFFF", error.InnerException.Message);
    }

    /// <summary>验证未配置 RasterFontCatalog 时，系统默认字体选择对无覆盖文本仍然拒绝。</summary>
    [Fact]
    [Trait("FontEnvironment", "System")]
    public void Default_font_selection_rejects_when_no_font_covers_text()
    {
        Assert.Throws<NotSupportedException>(() =>
            EscPosRasterCanvas.ResolveFontFamilyName(char.ConvertFromUtf32(0x10FFFF)));
    }

    /// <summary>模拟系统目录中元数据可读、字形表缺失的字体，仍应继续查找或报告无覆盖。</summary>
    [Theory]
    [InlineData("A", true)]
    [InlineData("\U0010FFFF", false)]
    public void Default_font_selection_skips_unreadable_font_metrics(string text, bool expectedCoverage)
    {
        var bytes = FixedTestFonts.Regular();
        var tableCount = bytes[4] * 256 + bytes[5];
        var locaOffset = Enumerable.Range(0, tableCount)
            .Select(index => 12 + index * 16)
            .Single(offset => Encoding.ASCII.GetString(bytes, offset, 4) == "loca");
        bytes[locaOffset + 3] = (byte)'x';
        var path = Path.Combine(Path.GetTempPath(), "easyprint-missing-loca-" + Guid.NewGuid().ToString("N") + ".ttf");
        File.WriteAllBytes(path, bytes);
        try
        {
            var unreadable = new FontCollection().Add(path);
            var readable = FixedTestFonts.LoadFamily(FixedTestFonts.Fallback());
            // 显式字形检查仍暴露字体错误；只有自动查找允许跳过该候选。
            Assert.Throws<MissingFontTableException>(() =>
                EscPosRasterCanvas.HasGlyphCoverage(text, unreadable, false, out _));
            var selected = EscPosRasterCanvas.FindFontFamily(text, false, new[] { unreadable, readable },
                CancellationToken.None);
            Assert.Equal(expectedCoverage ? readable : default(FontFamily), selected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>自动字体查找不得吞掉调用方取消。</summary>
    [Fact]
    public void Default_font_selection_preserves_cancellation()
    {
        var family = FixedTestFonts.LoadFamily(FixedTestFonts.Regular());
        using (var source = new CancellationTokenSource())
        {
            source.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                EscPosRasterCanvas.FindFontFamily("A", false, new[] { family }, source.Token));
        }
    }

    [Fact]
    public void Layout_line_controls_do_not_require_visible_font_glyphs()
    {
        var catalog = FixedTestFonts.Catalog();
        var family = EscPosRasterCanvas.ResolveFontFamilyName("A\r\nB\tC", null, false, catalog);
        Assert.False(string.IsNullOrWhiteSpace(family));
        Assert.True(EscPosRasterCanvas.MeasureTextWidth("A\u0301", 24, false, family, catalog) > 0);
    }
}
