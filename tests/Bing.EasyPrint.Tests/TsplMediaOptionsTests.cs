using System;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TsplMediaOptionsTests
{
    [Fact]
    public void Omitted_media_options_preserve_existing_bytes_and_capabilities()
    {
        var command = new TsplPrintCommand(Encoding.ASCII)
            .Size(60, 40, "dot")
            .Text(1, 2, "OK");

        Assert.Equal(203, command.Capabilities.Dpi);
        Assert.False(command.Capabilities.SupportsBoldText);
        Assert.False(command.Capabilities.SupportsUnderlineText);
        Assert.Equal(Encoding.ASCII.GetBytes(
            "SIZE 60 dot,40 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "TEXT 1,2,\"0\",0,1,1,\"OK\"\r\n"), command.Build().GetBytes());
    }

    [Fact]
    public void Constructor_media_options_are_emitted_after_size()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            GapDistance = 2,
            GapOffset = 1,
            Direction = 1,
            ReferenceX = 3,
            ReferenceY = 4,
            Speed = 5,
            Density = 8,
            Dpi = 300
        });

        var text = Encoding.ASCII.GetString(command.Size(60, 40, "dot").Text(1, 2, "OK").Build().GetBytes());

        Assert.Equal(300, command.Capabilities.Dpi);
        Assert.Equal("SIZE 60 dot,40 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "GAP 2,1\r\nDIRECTION 1\r\nREFERENCE 3,4\r\nSPEED 5\r\nDENSITY 8\r\n" +
            "TEXT 1,2,\"0\",0,1,1,\"OK\"\r\n", text);
    }

    [Fact]
    public void Apply_media_options_before_size_replaces_pending_settings()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions { GapDistance = 2 })
            .ApplyMediaOptions(new TsplMediaOptions { Direction = 1 })
            .Size(60, 40, "dot");

        var output = Encoding.ASCII.GetString(command.Build().GetBytes());

        Assert.DoesNotContain("GAP ", output);
        Assert.Contains("DIRECTION 1\r\n", output);
    }

    [Fact]
    public void Invalid_media_options_do_not_record_partial_commands()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Size(60, 40, "dot");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.ApplyMediaOptions(new TsplMediaOptions
        {
            GapDistance = 2,
            Density = 16
        }));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Partial_gap_and_reference_settings_use_zero_for_omitted_axis()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .Size(60, 40, "dot")
            .ApplyMediaOptions(new TsplMediaOptions { GapOffset = 3, ReferenceY = 7 })
            .Build().GetBytes());

        Assert.Contains("GAP 0,3\r\n", output);
        Assert.Contains("REFERENCE 0,7\r\n", output);
    }
}
