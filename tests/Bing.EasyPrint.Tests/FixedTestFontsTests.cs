using System;
using System.IO;
using Bing.EasyPrint.Protocols;
using SixLabors.Fonts;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证固定测试字体资源可解析且保持预期覆盖差异。</summary>
public sealed class FixedTestFontsTests
{
    [Fact]
    public void Regular_and_bold_fixed_files_parse_as_the_same_font_family()
    {
        var regular = FixedTestFonts.LoadFamily(FixedTestFonts.Regular());
        var bold = FixedTestFonts.LoadFamily(FixedTestFonts.Bold());

        Assert.Equal(regular.Name, bold.Name);
        Assert.Contains(FontStyle.Regular, regular.GetAvailableStyles());
        Assert.Contains(FontStyle.Bold, bold.GetAvailableStyles());
    }

    [Fact]
    public void Fixed_mono_and_sans_have_a_stable_emoji_coverage_difference()
    {
        const string text = "A\U0001F600";
        var mono = FixedTestFonts.LoadFamily(FixedTestFonts.Regular());
        var sans = FixedTestFonts.LoadFamily(FixedTestFonts.Fallback());

        Assert.False(EscPosRasterCanvas.HasGlyphCoverage(text, mono, false, out _));
        Assert.True(EscPosRasterCanvas.HasGlyphCoverage(text, sans, false, out _));
    }

    [Fact]
    public void Missing_fixed_font_file_fails_fast()
    {
        Assert.Throws<FileNotFoundException>(() =>
            FixedTestFonts.ReadRequired("__missing_fixed_test_font__.ttf"));
    }

    [Fact]
    public void Fixed_chinese_font_measures_and_draws_mixed_text_without_system_fonts()
    {
        const string text = "Raster text 中";
        var catalog = FixedTestFonts.ChineseCatalog();
        Assert.Equal("Chinese", EscPosRasterCanvas.ResolveFontFamilyName(text, null, false, catalog));
        Assert.True(EscPosRasterCanvas.MeasureTextWidth(text, 24, false, null, catalog) > 0);
        var canvas = new EscPosRasterCanvas(320, 80, catalog);
        canvas.DrawText(10, 10, text, 24);
        Assert.Contains(canvas.ToBytes(), value => value != 0);
        Assert.Throws<NotSupportedException>(() => catalog.Resolve(text, null, true));
    }
}
