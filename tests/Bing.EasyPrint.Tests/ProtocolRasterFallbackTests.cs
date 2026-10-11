using System;
using System.Text;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ProtocolRasterFallbackTests
{
    [Fact]
    public void Tspl_diagonal_requires_explicit_raster_option()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(0, 0, "OK");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.Line(0, 0, 2, 2));
        Assert.Equal(before, command.Build().GetBytes());

        var enabled = new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { AllowRasterizedLines = true }).Line(0, 0, 2, 2, 1);
        Assert.Contains("BITMAP 0,0,1,3,1,", Encoding.ASCII.GetString(enabled.Build().GetBytes()));
    }

    [Fact]
    public void Zpl_uses_native_axis_lines_and_requires_opt_in_for_diagonals()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Line(1, 2, 3, 2, 1);
        var native = Encoding.ASCII.GetString(command.Build().GetBytes());
        Assert.Contains("^FO1,2^GB3,1,1^FS", native);
        Assert.DoesNotContain("^GFA", native);

        Assert.Throws<NotSupportedException>(() => command.Line(0, 0, 2, 2, 1));
        Assert.Equal(native, Encoding.ASCII.GetString(command.Build().GetBytes()));

        var enabled = new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { AllowRasterizedLines = true }).Line(0, 0, 2, 2, 1);
        Assert.Contains("^GFA,3,3,1,804020", Encoding.ASCII.GetString(enabled.Build().GetBytes()));
    }

    [Fact]
    public void EscPos_template_layout_defaults_to_native()
    {
        Assert.Equal(EscPosLayoutMode.Native, new TemplateRenderOptions().EscPosLayout);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Provider_preflight_honors_rasterized_line_option(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(16, 16).Add(new PrintLineElement(1, 1, 3, 3), "Root.Line");

        var strict = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
        var error = Assert.Throws<PrintCapabilityException>(() => strict.Validate(document));
        Assert.Equal("Root.Line", error.ElementPath);

        var enabled = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            tsplOptions: protocol == PrintProtocol.Tspl
                ? new TsplMediaOptions { AllowRasterizedLines = true } : null,
            zplOptions: protocol == PrintProtocol.Zpl
                ? new ZplMediaOptions { AllowRasterizedLines = true } : null);
        enabled.Validate(document);
        Assert.Single(enabled.Render(document));
    }
}
