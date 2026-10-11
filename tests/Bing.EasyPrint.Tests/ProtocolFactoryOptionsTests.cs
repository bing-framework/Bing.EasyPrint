using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ProtocolFactoryOptionsTests
{
    [Fact]
    public void Factory_passes_escpos_options_to_command()
    {
        var options = new EscPosDeviceOptions
        {
            Dpi = 300,
            CodePageIndex = 17,
            PrintAreaWidthUnits = 576
        };

        var command = Assert.IsType<EscPosPrintCommand>(ProtocolCommandFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native, options));

        Assert.Equal(300, command.Capabilities.Dpi);
        command.Initialize();
        Assert.Equal(new byte[]
        {
            0x1B, 0x40, 0x1B, 0x74, 17, 0x1D, 0x57, 0x40, 0x02
        }, command.Build().GetBytes());
    }

    [Fact]
    public void Factory_passes_tspl_options_and_snapshots_them()
    {
        var options = new TsplMediaOptions
        {
            Dpi = 300,
            GapDistance = 2,
            GapOffset = 1,
            Direction = 1,
            ReferenceX = 3,
            ReferenceY = 4,
            Speed = 5,
            Density = 6
        };

        var command = Assert.IsType<TsplPrintCommand>(ProtocolCommandFactory.Create(
            PrintProtocol.Tspl, Encoding.ASCII, tsplOptions: options));

        options.GapDistance = 9;
        options.Dpi = 203;
        command.Size(60, 40);
        var output = Encoding.ASCII.GetString(command.Build().GetBytes());

        Assert.Equal(300, command.Capabilities.Dpi);
        Assert.Contains("GAP 2,1\r\n", output);
        Assert.Contains("DIRECTION 1\r\n", output);
        Assert.Contains("REFERENCE 3,4\r\n", output);
        Assert.Contains("SPEED 5\r\n", output);
        Assert.Contains("DENSITY 6\r\n", output);
        Assert.DoesNotContain("GAP 9,1", output);
    }

    [Fact]
    public void Factory_passes_zpl_options_and_snapshots_them()
    {
        var options = new ZplMediaOptions { Dpi = 300, PrintSpeed = 4 };
        var command = Assert.IsType<ZplPrintCommand>(ProtocolCommandFactory.Create(
            PrintProtocol.Zpl, Encoding.ASCII, zplOptions: options));

        options.Dpi = 203;
        options.PrintSpeed = 9;
        command.Start().Label(400, 300).End();
        var output = Encoding.ASCII.GetString(command.Build().GetBytes());

        Assert.Equal(300, command.Capabilities.Dpi);
        Assert.Contains("^PR4\r\n", output);
        Assert.DoesNotContain("^PR9", output);
    }

    [Fact]
    public void Factory_rejects_options_for_a_different_protocol()
    {
        Assert.Throws<ArgumentException>(() => ProtocolCommandFactory.Create(
            PrintProtocol.Tspl, escPosOptions: new EscPosDeviceOptions()));
        Assert.Throws<ArgumentException>(() => ProtocolCommandFactory.Create(
            PrintProtocol.Zpl, tsplOptions: new TsplMediaOptions()));
        Assert.Throws<ArgumentException>(() => ProtocolCommandFactory.Create(
            PrintProtocol.Cpcl, zplOptions: new ZplMediaOptions()));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Factories_reject_undefined_escpos_layout(PrintProtocol protocol)
    {
        var invalid = (EscPosLayoutMode)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => ProtocolCommandFactory.Create(
            protocol, Encoding.ASCII, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintProtocolProviderFactory.Create(
            protocol, Encoding.ASCII, invalid));
    }

    [Fact]
    public void Template_rejects_undefined_escpos_layout_before_processing_template()
    {
        var options = new TemplateRenderOptions { Protocol = PrintProtocol.EscPos,
            EscPosLayout = (EscPosLayoutMode)999, Encoding = Encoding.ASCII };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TemplateRenderer().Render(new BpTemplate(), null, options: options));
    }
}
