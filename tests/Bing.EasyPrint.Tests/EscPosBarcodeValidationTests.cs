using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosBarcodeValidationTests
{
    [Fact]
    public void Constructor_rejects_an_undefined_layout_mode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EscPosPrintCommand(Encoding.ASCII, (EscPosLayoutMode)999));
    }

    [Theory]
    [InlineData(65, "12345678901")]
    [InlineData(66, "123456")]
    [InlineData(67, "123456789012")]
    [InlineData(68, "1234567")]
    [InlineData(69, "ABC-39")]
    [InlineData(70, "123456")]
    [InlineData(71, "A1234B")]
    [InlineData(72, "Code93")]
    [InlineData(73, "123")]
    public void Function_b_accepts_the_portable_baseline_families(byte symbology, string data)
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Barcode(data, symbology)
            .Build()
            .GetBytes();
        var barcode = bytes.Skip(9).ToArray();

        Assert.Equal(new byte[] { 0x1D, 0x6B, symbology, barcode[3] }, barcode.Take(4).ToArray());
        Assert.Equal(barcode.Length - 4, barcode[3]);
    }

    [Fact]
    public void Code128_plain_data_is_encoded_as_code_set_b()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Barcode("123", 73)
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x6B, 73, 5, (byte)'{', (byte)'B', (byte)'1', (byte)'2', (byte)'3' }, bytes.Skip(9).ToArray());
    }

    [Fact]
    public void Code128_preserves_valid_code_set_sequences_and_escapes_literal_braces()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Barcode("{C1234{B12{", 73)
            .Build()
            .GetBytes();

        Assert.Equal(new byte[]
        {
            0x1D, 0x6B, 73, 12,
            (byte)'{', (byte)'C', (byte)'1', (byte)'2', (byte)'3', (byte)'4',
            (byte)'{', (byte)'B', (byte)'1', (byte)'2', (byte)'{', (byte)'{'
        }, bytes.Skip(9).ToArray());
    }

    [Fact]
    public void Code128_keeps_an_already_escaped_literal_brace_idempotent()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Barcode("{{A", 73)
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x6B, 73, 5, (byte)'{', (byte)'B', (byte)'{', (byte)'{', (byte)'A' },
            bytes.Skip(9).ToArray());
    }

    [Theory]
    [InlineData(64)]
    [InlineData(255)]
    public void Unsupported_function_b_symbology_is_rejected_without_pollution(byte symbology)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Barcode("123", symbology));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(65, "1234567890")]
    [InlineData(66, "12345")]
    [InlineData(66, "1234567")]
    [InlineData(67, "12345678901")]
    [InlineData(68, "123456")]
    [InlineData(69, "abc")]
    [InlineData(70, "12345")]
    [InlineData(71, "1234")]
    [InlineData(71, "11234B")]
    public void Function_b_rejects_invalid_format_or_length_without_pollution(byte symbology, string data)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode(data, symbology));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Code128_requires_ascii_and_escaped_code_set_start_when_supplied()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode("中文", 73));
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Function_b_allows_7_bit_control_bytes_for_code93_and_code128()
    {
        var code93 = new EscPosPrintCommand(Encoding.ASCII)
            .Barcode("\0\u001B", 72)
            .Build().GetBytes();
        var code128 = new EscPosPrintCommand(Encoding.ASCII)
            .Barcode("{A\0", 73)
            .Build().GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x6B, 72, 2, 0x00, 0x1B }, code93.Skip(9).ToArray());
        Assert.Equal(new byte[] { 0x1D, 0x6B, 73, 3, (byte)'{', (byte)'A', 0x00 }, code128.Skip(9).ToArray());
    }

    [Theory]
    [InlineData("{C123")]
    [InlineData("{C12A4")]
    [InlineData("{C{S12")]
    [InlineData("{B\0")]
    public void Code128_rejects_data_that_does_not_match_the_selected_code_set(string data)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode(data, 73));

        Assert.Equal(before, command.Build().GetBytes());
    }
}
