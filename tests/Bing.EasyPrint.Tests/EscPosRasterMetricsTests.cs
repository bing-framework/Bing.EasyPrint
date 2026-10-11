using System;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosRasterMetricsTests
{
    [Fact]
    public void Ink_measurement_rejects_temporary_bitmap_above_pixel_budget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EscPosRasterCanvas.MeasureTextInkBounds("ABC", 24, false, false, null, 1,
                FixedTestFonts.Catalog()));
    }
    [Fact]
    public void ResolveFontFamilyName_returns_a_font_used_by_default_measurement()
    {
        var catalog = FixedTestFonts.ChineseCatalog();
        var familyName = EscPosRasterCanvas.ResolveFontFamilyName("Raster text 中", null, false, catalog);

        Assert.False(string.IsNullOrWhiteSpace(familyName));
        Assert.True(EscPosRasterCanvas.MeasureTextWidth("Raster text 中", 24, false, null, catalog) > 0);
        Assert.Equal(
            EscPosRasterCanvas.MeasureTextWidth("Raster text 中", 24, false, null, catalog),
            EscPosRasterCanvas.MeasureTextWidth("Raster text 中", 24, false, familyName, catalog));
    }

    [Fact]
    public void MeasureTextWidth_uses_the_requested_font_and_bold_style()
    {
        var catalog = FixedTestFonts.Catalog();
        var familyName = EscPosRasterCanvas.ResolveFontFamilyName("ABC", null, false, catalog);

        Assert.True(EscPosRasterCanvas.MeasureTextWidth("ABC", 24, false, familyName, catalog) > 0);
        Assert.True(EscPosRasterCanvas.MeasureTextWidth("ABC", 24, true, familyName, catalog) > 0);
    }

    [Fact]
    public void MeasureTextInkBounds_matches_drawn_pixels_for_the_requested_font()
    {
        var catalog = FixedTestFonts.Catalog();
        var familyName = EscPosRasterCanvas.ResolveFontFamilyName("Ag", null, false, catalog);
        const int x = 23;
        const int y = 17;
        const int fontSize = 28;
        var canvas = new EscPosRasterCanvas(320, 160, catalog);

        canvas.DrawText(x, y, "Ag", fontSize, bold: true, fontFamily: familyName);
        var bounds = EscPosRasterCanvas.MeasureTextInkBounds("Ag", fontSize, bold: true, underline: false,
            fontFamily: familyName, maxPixels: long.MaxValue, fonts: catalog);
        var actual = FindBlackPixels(canvas);

        Assert.False(bounds.IsEmpty);
        Assert.Equal(x + bounds.Left, actual.Left);
        Assert.Equal(y + bounds.Top, actual.Top);
        Assert.Equal(x + bounds.Right, actual.Right);
        Assert.Equal(y + bounds.Bottom, actual.Bottom);
    }

    [Fact]
    public void MeasureTextInkBounds_includes_the_existing_underline_stroke()
    {
        var catalog = FixedTestFonts.Catalog();
        var familyName = EscPosRasterCanvas.ResolveFontFamilyName("A", null, false, catalog);
        const int x = 19;
        const int y = 11;
        const int fontSize = 24;
        var canvas = new EscPosRasterCanvas(240, 120, catalog);

        canvas.DrawText(x, y, "A", fontSize, underline: true, fontFamily: familyName);
        var bounds = EscPosRasterCanvas.MeasureTextInkBounds("A", fontSize, bold: false, underline: true,
            fontFamily: familyName, maxPixels: long.MaxValue, fonts: catalog);
        var actual = FindBlackPixels(canvas);

        Assert.False(bounds.IsEmpty);
        Assert.Equal(x + bounds.Left, actual.Left);
        Assert.Equal(y + bounds.Top, actual.Top);
        Assert.Equal(x + bounds.Right, actual.Right);
        Assert.Equal(y + bounds.Bottom, actual.Bottom);
        Assert.True(bounds.Bottom >= fontSize + 1);
        Assert.True(bounds.Width >= (int)Math.Ceiling(fontSize * 0.7));
    }

    [Fact]
    public void Font_metrics_validate_arguments_and_missing_fonts()
    {
        var catalog = FixedTestFonts.Catalog();
        Assert.Throws<ArgumentNullException>(() =>
            EscPosRasterCanvas.ResolveFontFamilyName(null, null, false, catalog));
        Assert.Throws<ArgumentNullException>(() =>
            EscPosRasterCanvas.MeasureTextWidth(null, 24, false, null, catalog));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EscPosRasterCanvas.MeasureTextWidth("ABC", 0, false, null, catalog));
        Assert.Throws<NotSupportedException>(() =>
            EscPosRasterCanvas.MeasureTextWidth("ABC", 24, false,
                "__Bing_EasyPrint_Font_Does_Not_Exist__", catalog));
        Assert.Throws<ArgumentNullException>(() =>
            EscPosRasterCanvas.MeasureTextInkBounds(null, 24, false, false, null, long.MaxValue, catalog));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EscPosRasterCanvas.MeasureTextInkBounds("ABC", 0, false, false, null, long.MaxValue, catalog));
        Assert.Throws<NotSupportedException>(() =>
            EscPosRasterCanvas.MeasureTextInkBounds("ABC", 24, false, false,
                "__Bing_EasyPrint_Font_Does_Not_Exist__", long.MaxValue, catalog));
    }

    private static PixelBounds FindBlackPixels(EscPosRasterCanvas canvas)
    {
        var bytes = canvas.ToBytes();
        var left = canvas.Width;
        var top = canvas.Height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < canvas.Height; y++)
            for (var x = 0; x < canvas.Width; x++)
            {
                var black = (bytes[y * canvas.RowBytes + x / 8] & (0x80 >> (x & 7))) != 0;
                if (!black) continue;
                if (x < left) left = x;
                if (y < top) top = y;
                if (x > right) right = x;
                if (y > bottom) bottom = y;
            }

        return right < left
            ? new PixelBounds(0, 0, 0, 0)
            : new PixelBounds(left, top, right + 1, bottom + 1);
    }

    private readonly struct PixelBounds
    {
        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }

        public PixelBounds(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }
}
