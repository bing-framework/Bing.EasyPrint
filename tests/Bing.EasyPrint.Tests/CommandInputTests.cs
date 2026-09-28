using System;
using System.Text;
using Bing.EasyPrint.CPCL;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class CommandInputTests
{
    private static CPCLPrintCommand Command() => new CPCLPrintCommand(Encoding.UTF8).SetPage(576, 200);

    [Theory]
    [InlineData("hello\r\nPRINT")]
    [InlineData("hello\nCUT")]
    [InlineData("hello\0")]
    [InlineData("hello\u001b")]
    public void Single_line_data_rejects_protocol_controls(string value)
    {
        Assert.Throws<ArgumentException>(() => Command().Text(24, 0, 0, 0, value));
        Assert.Throws<ArgumentException>(() => Command().Barcode("128", 1, 1, 20, 0, 0, value));
        Assert.Throws<ArgumentException>(() => Command().QRCode(0, 0, 2, 4, "M", null, value));
        Assert.Throws<ArgumentException>(() => Command().VQRCode(0, 0, 2, 4, "M", null, value));
        Assert.Throws<ArgumentException>(() => Command().QRCodeManual(0, 0, 2, 4, "M", null, ("A", value)));
    }

    [Fact]
    public void Block_terminators_are_rejected_before_mutating_command()
    {
        var command = Command().Text(24, 0, 0, 0, "before");
        var before = command.Build().GetBytes();
        Assert.Throws<ArgumentException>(() => command.Pdf417(0, 0, 2, 3, 4, 1, "good", "ENDPDF", "CUT"));
        Assert.Equal(before, command.Build().GetBytes());
        Assert.Throws<ArgumentException>(() => command.Aztec(0, 0, 2, 0, "ENDAZTEC"));
        Assert.Throws<ArgumentException>(() => command.Concat(0, 0, "ENDCONCAT"));
        Assert.Throws<ArgumentException>(() => command.Concat(0, 0, ("ENDCONCAT", 0, 0, "text")));
        Assert.Throws<ArgumentException>(() => command.MultiLine(24, "TEXT", "24", 0, 0, 0, "ENDML"));
        Assert.Throws<ArgumentException>(() => command.MultiLine(24, "ENDML", "24", 0, 0, 0, "text"));
        Assert.Throws<ArgumentException>(() => command.Pdf417(0, 0, 2, 3, 4, 1, "PRINT"));
        Assert.Throws<ArgumentException>(() => command.Pdf417(0, 0, 2, 3, 4, 1, "PAGE-WIDTH 1"));
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Protocol_tokens_cannot_inject_additional_fields_or_commands()
    {
        Assert.Throws<ArgumentException>(() => Command().Text("24\nPRINT", 0, 0, 0, "text"));
        Assert.Throws<ArgumentException>(() => Command().Barcode("128 2", 1, 1, 10, 0, 0, "text"));
        Assert.Throws<ArgumentException>(() => Command().Encoding("GBK\nPRINT"));
        Assert.Throws<ArgumentException>(() => Command().OnFeed("PRINT\nCUT"));
        Assert.Throws<ArgumentException>(() => Command().UseFormat("FORM", "\nCUT"));
    }

    [Fact]
    public void Raw_escape_hatches_remain_explicitly_available()
    {
        var output = Command().WriteRawLine("T 24 0 0 0 raw\r\nCUT").Build().ToString();
        Assert.Contains("\r\nCUT\r\n", output);
        Assert.Contains("ENDQR", Command().QRCodeRaw(0, 0, 2, 4, "MA,raw").Build().ToString());
    }

    [Fact]
    public void Graphics_payload_length_cannot_escape_declared_dimensions()
    {
        Assert.Throws<ArgumentException>(() => Command().ExpandedGraphics(1, 1, 0, 0, "FF\nPRINT"));
        Assert.Throws<ArgumentException>(() => Command().ExpandedGraphics(1, 1, 0, 0, "GG"));
        Assert.Throws<ArgumentException>(() => Command().CompressedGraphics(1, 1, 0, 0, "x\r\nPRINT"));
        Assert.Contains("EG 1 1 0 0 FF", Command().ExpandedGraphics(1, 1, 0, 0, "FF").Build().ToString());
        // 二进制图形允许控制字节，只要声明的长度精确覆盖它们。
        Assert.Contains("CG 1 1 0 0 \n", Command().CompressedGraphics(1, 1, 0, 0, "\n").Build().ToString());
    }
}
