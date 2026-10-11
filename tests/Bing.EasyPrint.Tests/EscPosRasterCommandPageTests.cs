using System;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosRasterCommandPageTests
{
    [Fact]
    public void Line_and_box_use_the_active_page_coordinates()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(8, 4)
            .Line(0, 0, 7, 0)
            .Box(1, 1, 6, 3)
            .EndRasterPage()
            .Build().GetBytes();

        Assert.Equal(new byte[]
        {
            0x1D, 0x76, 0x30, 0x00, 1, 0, 4, 0,
            0xFF, 0x7E, 0x42, 0x7E
        }, bytes);
    }

    [Fact]
    public void Unfinished_page_fails_build_and_can_be_completed()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(8, 2);

        Assert.Throws<InvalidOperationException>(() => command.Build());
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Line(0, 0, 8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Box(1, 0, 8, 1));

        command.Line(0, 0, 7, 0).EndRasterPage();
        var expected = new byte[] { 0x1D, 0x76, 0x30, 0, 1, 0, 2, 0, 0xFF, 0 };
        Assert.Equal(expected, command.Build().GetBytes());
        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void Native_commands_cannot_be_inserted_into_an_active_raster_page()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .Initialize()
            .BeginRasterPage(8, 1)
            .Line(0, 0, 7, 0);

        Assert.Throws<InvalidOperationException>(() => command.Cut());
        Assert.Throws<InvalidOperationException>(() => command.WriteRawBytes(new byte[] { 0x1B, 0x40 }));
        Assert.Throws<InvalidOperationException>(() => command.RasterImage(8, 1, new byte[] { 0x80 }));

        var bytes = command.EndRasterPage().Cut().Build().GetBytes();
        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1D, 0x76, 0x30, 0, 1, 0, 1, 0, 0xFF,
            0x1D, 0x56, 0 }, bytes);
        Assert.Equal(bytes, command.Build().GetBytes());
    }

    [Fact]
    public void Coordinate_text_supports_raster_rotation_and_style()
    {
        var fonts = FixedTestFonts.Catalog();
        var family = EscPosRasterCanvas.ResolveFontFamilyName("AB", null, false, fonts);
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
                new EscPosDeviceOptions { RasterFonts = fonts })
            .BeginRasterPage(96, 96)
            .Text(4, 4, "AB", 24, 90, underline: true, fontFamily: family)
            .EndRasterPage()
            .Build().GetBytes();

        Assert.True(Contains(bytes, new byte[] { 0x1D, 0x76, 0x30 }));
    }

    [Fact]
    public void Coordinate_codes_support_raster_rotation()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(160, 160)
            .RasterBarcode(4, 4, "1234", 24, moduleWidth: 2, rotation: 90)
            .RasterQrCode(90, 80, "QR", moduleSize: 2, rotation: 270)
            .EndRasterPage();

        Assert.True(Contains(command.Build().GetBytes(), new byte[] { 0x1D, 0x76, 0x30 }));
        Assert.Equal(command.Build().GetBytes(), command.Build().GetBytes());
    }

    [Fact]
    public void Layout_mode_and_protocol_limits_are_reported()
    {
        var native = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native);
        var raster = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster);

        Assert.True(native.Capabilities.SupportsNativeLayout);
        Assert.False(native.Capabilities.SupportsRasterLayout);
        Assert.False(native.Capabilities.SupportsHostFontFamily);
        Assert.Null(native.Capabilities.MaxRasterRowBytes);
        Assert.False(raster.Capabilities.SupportsNativeLayout);
        Assert.True(raster.Capabilities.SupportsRasterLayout);
        Assert.True(raster.Capabilities.SupportsHostFontFamily);
        Assert.Equal(ushort.MaxValue, raster.Capabilities.MaxRasterRowBytes);
        Assert.Equal(ushort.MaxValue, raster.Capabilities.MaxRasterHeight);

        Assert.Throws<NotSupportedException>(() => native.BeginRasterPage(8, 2));
        Assert.Throws<NotSupportedException>(() => native.Line(0, 0, 1, 0));
        Assert.Throws<InvalidOperationException>(() => raster.Box(0, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => raster.BeginRasterPage(ushort.MaxValue * 8 + 1, 1));
        Assert.Empty(raster.Build().GetBytes());
    }

    private static bool Contains(byte[] data, byte[] pattern)
    {
        for (var index = 0; index <= data.Length - pattern.Length; index++)
        {
            var matched = true;
            for (var offset = 0; offset < pattern.Length; offset++)
                if (data[index + offset] != pattern[offset]) { matched = false; break; }
            if (matched) return true;
        }
        return false;
    }
}
