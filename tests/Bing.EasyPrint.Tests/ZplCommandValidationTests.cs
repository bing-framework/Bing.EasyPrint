using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ZPL 结构化字段的字符集和失败恢复行为。</summary>
public sealed class ZplCommandValidationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("A")]
    [InlineData("Z")]
    public void Text_accepts_single_ascii_digit_or_uppercase_font(string font)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Text(1, 2, "OK", font: font)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes($"^FO1,2^A{font}N,30,30^FDOK^FS\r\n"), bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("a")]
    [InlineData("中")]
    [InlineData("^")]
    [InlineData("~")]
    public void Text_rejects_invalid_font_without_polluting_recorded_output(string font)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Text(1, 2, "OK", font: font));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Text_rejects_null_font_without_polluting_recorded_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentNullException>(() => command.Text(1, 2, "OK", font: null));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData("")]
    [InlineData("A\u0000B")]
    [InlineData("A\u001FB")]
    [InlineData("A\u007FB")]
    [InlineData("A\rB")]
    [InlineData("A\nB")]
    [InlineData("中文")]
    [InlineData("A^B")]
    [InlineData("A~B")]
    [InlineData("A>B")]
    public void Code128_rejects_non_printable_non_ascii_or_zpl_delimiters_without_pollution(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode(1, 2, data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Code128_accepts_printable_ascii_data_supported_by_the_template_baseline()
    {
        var data = "A-100 / {B}";
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Barcode(1, 2, data, 60, false, 3)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "^FO1,2^BY3^BCN,60,N,N,N^FDA-100 / {B}^FS\r\n"), bytes);
    }

    [Fact]
    public void Failed_barcode_can_be_followed_by_valid_commands_and_build_is_idempotent()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Start();

        Assert.Throws<ArgumentException>(() => command.Barcode(1, 2, "bad\u0001data"));

        var bytes = command
            .Text(1, 2, "OK")
            .Barcode(3, 4, "A-1")
            .End()
            .Build()
            .GetBytes();
        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^FO1,2^A0N,30,30^FDOK^FS\r\n" +
            "^FO3,4^BY2^BCN,80,Y,N,N^FDA-1^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void Provider_reports_zpl_invocation_character_at_element_path()
    {
        var document = new PrintDocument();
        document.AddPage(400, 100).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.Code128, "A>B", 30), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Validate(document));

        Assert.Equal("Rows[0].Barcode", error.ElementPath);
    }
}
