using System;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class LabelValidationTests
{
    [Fact]
    public void Tspl_gap_uses_the_protocol_comma_separator()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .Gap(3, 4)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes("GAP 3,4\r\n"), bytes);
    }

    [Fact]
    public void Tspl_rejects_negative_coordinates_without_recording_commands()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(1, 1, "ok");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Reference(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Text(-1, 0, "ok"));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Line(-1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Barcode(-1, 0, "123"));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.QrCode(-1, 0, "qr"));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Bitmap(-1, 0, 1, 1, 0, new byte[] { 0x80 }));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_rejects_invalid_rectangles_and_empty_symbols_without_recording()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(1, 1, "ok");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Box(0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Box(2, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Box(0, 0, 1, 1, 0));
        Assert.Throws<ArgumentException>(() => command.Barcode(0, 0, string.Empty));
        Assert.Throws<ArgumentException>(() => command.QrCode(0, 0, string.Empty));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_rejects_bitmap_size_overflow_before_recording()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(1, 1, "ok");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Bitmap(0, 0, int.MaxValue, 2, 0, Array.Empty<byte>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Line(0, 0, int.MaxValue, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_rejects_negative_coordinates_without_recording_commands()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Text(1, 1, "ok");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Text(-1, 0, "ok"));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Line(-1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Box(-1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Barcode(-1, 0, "123"));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.QrCode(-1, 0, "qr"));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.GraphicField(-1, 0, 1, 1, new byte[] { 0x80 }));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_rejects_invalid_rectangles_and_empty_symbols_without_recording()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Text(1, 1, "ok");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Box(0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Box(2, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Box(0, 0, 1, 1, 0));
        Assert.Throws<ArgumentException>(() => command.Barcode(0, 0, string.Empty));
        Assert.Throws<ArgumentException>(() => command.QrCode(0, 0, string.Empty));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_rejects_raster_size_overflow_before_recording()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Text(1, 1, "ok");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.GraphicField(0, 0, int.MaxValue, 2, Array.Empty<byte>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Line(0, 0, int.MaxValue, 0, int.MaxValue));

        Assert.Equal(before, command.Build().GetBytes());
    }
}
