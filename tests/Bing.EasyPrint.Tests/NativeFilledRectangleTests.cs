using System;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class NativeFilledRectangleTests
{
    [Theory]
    [InlineData(PrintProtocol.Tspl, 8, 3, "BAR 2,4,8,3\r\n")]
    [InlineData(PrintProtocol.Tspl, 3, 8, "BAR 2,4,3,8\r\n")]
    [InlineData(PrintProtocol.Tspl, 1, 1, "BAR 2,4,1,1\r\n")]
    [InlineData(PrintProtocol.Zpl, 8, 3, "^FO2,4^GB8,3,3,B,0^FS\r\n")]
    [InlineData(PrintProtocol.Zpl, 3, 8, "^FO2,4^GB3,8,3,B,0^FS\r\n")]
    [InlineData(PrintProtocol.Zpl, 1, 1, "^FO2,4^GB1,1,1,B,0^FS\r\n")]
    public void Commands_emit_exact_native_geometry_and_build_is_stable(PrintProtocol protocol, int width, int height, string expected)
    {
        var command = Command(protocol);
        Fill(command, 2, 4, width, height);
        var bytes = command.Build().GetBytes();
        Assert.Equal(Encoding.ASCII.GetBytes(expected), bytes);
        Assert.Equal(bytes, command.Build().GetBytes());
        Assert.True(command.Capabilities.SupportsFilledRectangles);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Invalid_geometry_and_cancellation_do_not_append_records(PrintProtocol protocol)
    {
        var command = Command(protocol);
        Fill(command, 0, 0, 1, 1);
        var before = command.Build().GetBytes();
        foreach (var geometry in new[] { (-1, 0, 1, 1), (0, -1, 1, 1), (0, 0, 0, 1),
            (0, 0, 1, 0), (0, 0, -1, 1), (0, 0, 1, -1), (int.MaxValue, 0, 1, 1), (0, int.MaxValue, 1, 1) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Fill(command, geometry.Item1, geometry.Item2, geometry.Item3, geometry.Item4));
            Assert.Equal(before, command.Build().GetBytes());
        }
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => Fill(command, 2, 4, 8, 3, cancellation.Token));
        }
        Assert.Equal(before, command.Build().GetBytes());
        Fill(command, 2, 4, 8, 3);
        Assert.True(command.Build().GetBytes().Length > before.Length);
    }

    [Fact]
    public void Zpl_new_fill_validates_the_documented_32000_dot_limit()
    {
        var command = new ZplPrintCommand(Encoding.ASCII);
        command.FillRectangle(0, 0, 32000, 32000);
        var before = command.Build().GetBytes();
        Assert.Equal("^FO0,0^GB32000,32000,32000,B,0^FS\r\n", Encoding.ASCII.GetString(before));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.FillRectangle(0, 0, 32001, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.FillRectangle(0, 0, 1, 32001));
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Device_graphics_and_physical_bounds_apply_without_requiring_images(PrintProtocol protocol)
    {
        var command = Command(protocol, images: false, width: 10, height: 7);
        Fill(command, 2, 4, 8, 3);
        var before = command.Build().GetBytes();
        Assert.Throws<NotSupportedException>(() => Fill(command, 3, 4, 8, 3));
        Assert.Throws<NotSupportedException>(() => Fill(command, 2, 5, 8, 3));
        Assert.Equal(before, command.Build().GetBytes());
        Assert.True(command.Capabilities.SupportsFilledRectangles);
        Assert.False(command.Capabilities.SupportsImages);
        var disabled = Command(protocol, graphics: false);
        Assert.False(disabled.Capabilities.SupportsFilledRectangles);
        Assert.Throws<NotSupportedException>(() => Fill(disabled, 0, 0, 1, 1));
        Assert.Empty(disabled.Build().GetBytes());
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Recording_and_build_resource_failure_can_retry_without_partial_output(PrintProtocol protocol)
    {
        var command = Command(protocol);
        using (var scope = PrintResourceScope.Begin(1))
        {
            Assert.Throws<PrintResourceLimitException>(() => Fill(command, 2, 4, 8, 3));
            Assert.Equal(0, scope.CurrentBytes);
        }
        Assert.Empty(command.Build().GetBytes());
        Fill(command, 2, 4, 8, 3);
        var expected = command.Build().GetBytes();
        using (var scope = PrintResourceScope.Begin(1))
        {
            Assert.Throws<PrintResourceLimitException>(() => command.Build().GetBytes());
            Assert.Equal(0, scope.CurrentBytes);
        }
        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "BAR 2,4,8,3\r\n")]
    [InlineData(PrintProtocol.Zpl, "^FO2,4^GB8,3,3,B,0^FS\r\n")]
    public void Provider_accepts_exact_page_edge_and_rejects_clipping_with_a_source_path(PrintProtocol protocol, string expected)
    {
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
        var valid = Document(2, 4, 8, 3);
        var output = provider.Render(valid)[0];
        Assert.Contains(expected, Encoding.ASCII.GetString(output));
        Assert.Equal(output, provider.Render(valid)[0]);
        Assert.True(provider.Capabilities.SupportsFilledRectangles);
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Render(Document(3, 4, 8, 3)));
        Assert.Equal("Fill", error.ElementPath);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Quiet_zone_checks_the_whole_fill_instead_of_only_its_outline(PrintProtocol protocol)
    {
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
        foreach (var filled in new[] { false, true })
        foreach (var rectangleFirst in new[] { false, true })
        {
            var document = new PrintDocument();
            var page = document.AddPage(120, 120);
            var rectangle = new PrintRectangleElement(10, 10, 100, 100, filled: filled);
            var qr = new PrintQrCodeElement(40, 40, "A", 1, QrCodeCorrectionLevel.M, PrintQuietZone.Uniform(4));
            if (rectangleFirst) page.Add(rectangle, "Fill").Add(qr, "Qr");
            else page.Add(qr, "Qr").Add(rectangle, "Fill");
            Assert.Equal(!filled, provider.Check(document).IsValid);
        }
    }

    private static PrintDocument Document(int x, int y, int width, int height)
    {
        var document = new PrintDocument();
        document.AddPage(10, 7).Add(new PrintRectangleElement(x, y, width, height, lineWidth: 16, filled: true), "Fill");
        return document;
    }

    private static IProtocolPrintCommand Command(PrintProtocol protocol, bool graphics = true, bool images = true, int? width = null, int? height = null)
        => protocol == PrintProtocol.Tspl
            ? (IProtocolPrintCommand)new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions { Profile = new TsplPrinterProfile {
                SupportsGraphics = graphics, SupportsImages = images, PrintableWidthDots = width, MaxLabelHeightDots = height } })
            : new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions { Profile = new ZplPrinterProfile {
                SupportsGraphics = graphics, SupportsImages = images, PrintableWidthDots = width, MaxLabelHeightDots = height } });

    private static void Fill(IProtocolPrintCommand command, int x, int y, int width, int height, CancellationToken token = default(CancellationToken))
    {
        if (command is TsplPrintCommand tspl) tspl.FillRectangle(x, y, width, height, token);
        else ((ZplPrintCommand)command).FillRectangle(x, y, width, height, token);
    }
}
