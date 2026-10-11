using System;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ZplMediaOptionsTests
{
    [Fact]
    public void Omitted_media_options_preserve_existing_bytes_and_capabilities()
    {
        var command = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .Label(300, 200)
            .End();

        Assert.Equal(ZplMediaOptions.DefaultDpi, command.Capabilities.Dpi);
        Assert.Equal(Encoding.ASCII.GetBytes("^XA\r\n^PW300\r\n^LL200\r\n^XZ\r\n"),
            command.Build().GetBytes());
    }

    [Fact]
    public void Options_update_dpi_metadata_and_default_label_speed()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Dpi = 300,
            PrintSpeed = 6
        });

        var output = Encoding.ASCII.GetString(command.Start().Label(300, 200).End().Build().GetBytes());

        Assert.Equal("^XA\r\n^PW300\r\n^LL200\r\n^PR6\r\n^XZ\r\n", output);
        Assert.Equal(300, command.Capabilities.Dpi);
    }

    [Fact]
    public void Explicit_normal_orientation_emits_the_standard_normal_direction()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Orientation = PrintOrientation.None
        }).Start().Label(300, 200).End().Build().GetBytes());

        Assert.Contains("^PON\r\n", output);
    }

    [Fact]
    public void Explicit_label_speed_overrides_the_option_snapshot()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions { PrintSpeed = 6 });

        var output = Encoding.ASCII.GetString(command.Start().Label(300, 200, 8).End().Build().GetBytes());

        Assert.Contains("^PR8\r\n", output);
        Assert.DoesNotContain("^PR6\r\n", output);
    }

    [Fact]
    public void Media_options_emit_supported_direction_tracking_and_darkness_commands()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Orientation = PrintOrientation.Down,
            MediaTracking = 'M',
            Darkness = -4
        }).Start().Label(300, 200).End().Build().GetBytes());

        Assert.Contains("^POI\r\n", output);
        Assert.Contains("^MNM\r\n", output);
        Assert.Contains("^MD-4\r\n", output);
    }

    [Theory]
    [InlineData(PrintOrientation.Left)]
    [InlineData(PrintOrientation.Right)]
    public void Unsupported_page_orientations_are_rejected_before_output(PrintOrientation orientation)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { Orientation = orientation }));

        Assert.Equal("Orientation", exception.ParamName);
        Assert.Contains("ZPL ^PO 仅支持 None（N）或 Down（I）", exception.Message);
    }

    [Fact]
    public void Options_are_snapshotted_at_construction()
    {
        var options = new ZplMediaOptions { Dpi = 300, PrintSpeed = 6 };
        var command = new ZplPrintCommand(Encoding.ASCII, options);
        options.Dpi = 600;
        options.PrintSpeed = 8;

        var output = Encoding.ASCII.GetString(command.Start().Label(300, 200).End().Build().GetBytes());

        Assert.Equal(300, command.Capabilities.Dpi);
        Assert.Contains("^PR6\r\n", output);
        Assert.DoesNotContain("^PR8\r\n", output);
    }

    [Fact]
    public void Invalid_options_are_rejected_before_commands_are_recorded()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { Dpi = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { PrintSpeed = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { MediaTracking = 'X' }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { Darkness = 31 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { Orientation = (PrintOrientation)99 }));

        var command = new ZplPrintCommand(Encoding.ASCII).Start().End();
        var before = command.Build().GetBytes();
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Label(300, 200, 0));
        Assert.Equal(before, command.Build().GetBytes());
    }
}
