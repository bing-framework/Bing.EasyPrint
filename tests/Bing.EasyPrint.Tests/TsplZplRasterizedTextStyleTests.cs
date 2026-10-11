using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TsplZplRasterizedTextStyleTests
{
    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Styled_text_remains_strictly_rejected_without_explicit_opt_in(PrintProtocol protocol)
    {
        var command = protocol == PrintProtocol.Tspl
            ? (IProtocolPrintCommand)new TsplPrintCommand(Encoding.ASCII)
            : new ZplPrintCommand(Encoding.ASCII);

        Assert.False(command.Capabilities.SupportsBoldText);
        Assert.False(command.Capabilities.SupportsUnderlineText);
        if (protocol == PrintProtocol.Tspl)
            Assert.Throws<NotSupportedException>(() =>
                new TsplPrintCommand(Encoding.ASCII).TextRasterized(0, 0, "A", 24, bold: true));
        else
            Assert.Throws<NotSupportedException>(() =>
                new ZplPrintCommand(Encoding.ASCII).TextRasterized(0, 0, "A", 24, underline: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Tspl_opt_in_emits_rotated_styled_text_as_bitmap(int rotation)
    {
        var command = new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions { AllowRasterizedTextStyles = true })
            .Size(200, 200, "dot")
            .TextRasterized(10, 10, "A", 24, bold: true, underline: true, rotation: rotation)
            .Print();

        var bytes = command.Build().GetBytes();
        Assert.True(command.Capabilities.SupportsBoldText);
        Assert.True(command.Capabilities.SupportsUnderlineText);
        Assert.True(ContainsSequence(bytes, Encoding.ASCII.GetBytes("BITMAP 10,10,")));
        Assert.Contains(bytes, value => value != 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Zpl_opt_in_emits_rotated_styled_text_as_graphic_field(int rotation)
    {
        var command = new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { AllowRasterizedTextStyles = true })
            .Start()
            .Label(200, 200)
            .TextRasterized(10, 10, "A", 24, bold: true, underline: true, rotation: rotation)
            .Print()
            .End();

        var bytes = command.Build().GetBytes();
        Assert.True(command.Capabilities.SupportsBoldText);
        Assert.True(command.Capabilities.SupportsUnderlineText);
        Assert.True(ContainsSequence(bytes, Encoding.ASCII.GetBytes("^GFA,")));
        Assert.True(ContainsSequence(bytes, Encoding.ASCII.GetBytes("^XA")));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Provider_renders_opt_in_styled_text_and_keeps_default_rejection(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        var page = document.AddPage(200, 200);
        page.CutAfterPrint = false;
        page.FeedLines = 0;
        page.Add(new PrintTextElement(10, 10, "A", 24, bold: true, underline: true,
            rotation: RotationAngle.Rotate90, width: 40, height: 40));

        var output = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            tsplOptions: protocol == PrintProtocol.Tspl
                ? new TsplMediaOptions { AllowRasterizedTextStyles = true } : null,
            zplOptions: protocol == PrintProtocol.Zpl
                ? new ZplMediaOptions { AllowRasterizedTextStyles = true } : null).Render(document);
        var bytes = Assert.Single(output);
        Assert.True(ContainsSequence(bytes, protocol == PrintProtocol.Tspl
            ? Encoding.ASCII.GetBytes("BITMAP") : Encoding.ASCII.GetBytes("^GFA")));

        var strict = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
        Assert.Throws<PrintCapabilityException>(() => strict.Render(document));
    }

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
            if (data.Skip(start).Take(sequence.Length).SequenceEqual(sequence)) return true;
        return false;
    }
}
