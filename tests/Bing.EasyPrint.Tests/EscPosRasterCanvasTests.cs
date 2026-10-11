using System;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosRasterCanvasTests
{
    [Fact]
    public void DrawLine_rejects_a_stroke_that_would_be_clipped()
    {
        var canvas = new EscPosRasterCanvas(2, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawLine(0, 0, 0, 0, int.MaxValue));
    }

    [Fact]
    public void DrawLine_preserves_the_existing_inclusive_brush_radius()
    {
        var canvas = new EscPosRasterCanvas(4, 4);

        canvas.DrawLine(1, 1, 1, 1, 3);

        Assert.Equal(new byte[]
        {
            0xE0, 0xE0, 0xE0, 0x00
        }, canvas.ToBytes());
    }

    [Fact]
    public void DrawLine_rejects_an_endpoint_outside_the_canvas()
    {
        var canvas = new EscPosRasterCanvas(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawLine(-1, 0, 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawLine(0, 0, 4, 0));
    }

    [Fact]
    public void DrawRectangle_rejects_filled_and_stroked_content_that_would_be_clipped()
    {
        var canvas = new EscPosRasterCanvas(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawRectangle(1, 1, 4, 2, filled: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawRectangle(0, 0, 4, 4, thickness: 3));
    }

    [Fact]
    public void DrawRaster_rejects_content_that_would_be_clipped()
    {
        var canvas = new EscPosRasterCanvas(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawRaster(3, 0, 2, 1, new byte[] { 0xC0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawRaster(0, 3, 1, 2, new byte[] { 0x80, 0x00 }));
    }

    [Fact]
    public void DrawText_rejects_ink_that_would_be_clipped()
    {
        var catalog = FixedTestFonts.Catalog();
        var familyName = EscPosRasterCanvas.ResolveFontFamilyName("A", null, false, catalog);
        var bounds = EscPosRasterCanvas.MeasureTextInkBounds("A", 24, false, false, familyName,
            long.MaxValue, catalog);
        var canvas = new EscPosRasterCanvas(1, 1, catalog);

        Assert.True(bounds.Width > 1 || bounds.Height > 1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            canvas.DrawText(0, 0, "A", 24, fontFamily: familyName));
    }

    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void DrawText_rotation_maps_the_same_fixed_font_ink(int rotation)
    {
        var catalog = FixedTestFonts.Catalog();
        const string familyName = "Primary";
        const string value = "AB";
        const int fontSize = 24;
        var source = new EscPosRasterCanvas(160, 160, catalog);
        source.DrawText(40, 40, value, fontSize, underline: true, fontFamily: familyName);
        var sourceBytes = source.ToBytes();
        var sourceBounds = Bounds(sourceBytes, source.RowBytes, source.Width, source.Height);
        Assert.True(sourceBounds.Width > 0 && sourceBounds.Height > 0);

        var outputWidth = rotation == 90 || rotation == 270 ? sourceBounds.Height : sourceBounds.Width;
        var outputHeight = rotation == 90 || rotation == 270 ? sourceBounds.Width : sourceBounds.Height;
        var rotated = new EscPosRasterCanvas(outputWidth, outputHeight, catalog);
        rotated.DrawText(0, 0, value, fontSize, underline: true, fontFamily: familyName, rotation: rotation);
        var rotatedBytes = rotated.ToBytes();
        var rotatedBounds = Bounds(rotatedBytes, rotated.RowBytes, rotated.Width, rotated.Height);

        Assert.Equal(outputWidth, rotatedBounds.Width);
        Assert.Equal(outputHeight, rotatedBounds.Height);
        Assert.Equal(BlackPixelCount(sourceBytes, source.RowBytes, sourceBounds),
            BlackPixelCount(rotatedBytes, rotated.RowBytes, rotatedBounds));

        for (var y = sourceBounds.Top; y < sourceBounds.Bottom; y++)
        for (var x = sourceBounds.Left; x < sourceBounds.Right; x++)
        {
            if (!IsBlack(sourceBytes, source.RowBytes, x, y)) continue;
            var sourceX = x - sourceBounds.Left;
            var sourceY = y - sourceBounds.Top;
            var targetX = rotation == 90 ? outputWidth - 1 - sourceY :
                rotation == 180 ? outputWidth - 1 - sourceX : rotation == 270 ? sourceY : sourceX;
            var targetY = rotation == 90 ? sourceX :
                rotation == 180 ? outputHeight - 1 - sourceY : rotation == 270 ? outputHeight - 1 - sourceX : sourceY;
            Assert.True(IsBlack(rotatedBytes, rotated.RowBytes, targetX, targetY),
                $"旋转 {rotation} 度后 ({targetX},{targetY}) 应保留文字墨迹。");
        }
    }

    [Fact]
    public void DrawText_rotation_validates_angle_and_rotated_bounds()
    {
        var catalog = FixedTestFonts.Catalog();
        var familyName = EscPosRasterCanvas.ResolveFontFamilyName("AB", null, false, catalog);
        var source = new EscPosRasterCanvas(160, 160, catalog);
        source.DrawText(40, 40, "AB", 24, fontFamily: familyName);
        var bounds = Bounds(source.ToBytes(), source.RowBytes, source.Width, source.Height);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.DrawText(0, 0, "AB", 24, fontFamily: familyName, rotation: 45));
        var clipped = new EscPosRasterCanvas(bounds.Height - 1, bounds.Width, catalog);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            clipped.DrawText(0, 0, "AB", 24, fontFamily: familyName, rotation: 90));
    }

    private static (int Left, int Top, int Width, int Height, int Right, int Bottom) Bounds(
        byte[] bytes, int rowBytes, int width, int height)
    {
        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (!IsBlack(bytes, rowBytes, x, y)) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }
        return right < left || bottom < top
            ? (0, 0, 0, 0, 0, 0)
            : (left, top, right - left + 1, bottom - top + 1, right + 1, bottom + 1);
    }

    private static int BlackPixelCount(byte[] bytes, int rowBytes,
        (int Left, int Top, int Width, int Height, int Right, int Bottom) bounds)
    {
        var count = 0;
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        for (var x = bounds.Left; x < bounds.Right; x++)
            if (IsBlack(bytes, rowBytes, x, y)) count++;
        return count;
    }

    private static bool IsBlack(byte[] bytes, int rowBytes, int x, int y)
        => (bytes[y * rowBytes + x / 8] & (0x80 >> (x & 7))) != 0;
}
