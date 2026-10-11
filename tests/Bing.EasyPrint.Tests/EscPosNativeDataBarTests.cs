using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosNativeDataBarTests
{
    [Theory]
    [InlineData(75, "1234567890123")]
    [InlineData(76, "1234567890123")]
    [InlineData(77, "1234567890123")]
    public void Function_b_data_bar_families_emit_exact_payload(byte symbology, string data)
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Barcode(data, symbology, 80, 2, 2)
            .Build().GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x48, 2, 0x1D, 0x68, 80, 0x1D, 0x77, 2,
            0x1D, 0x6B, symbology, 13 }, bytes.Take(13).ToArray());
        Assert.Equal(Encoding.ASCII.GetBytes(data), bytes.Skip(13).ToArray());
    }

    [Fact]
    public void Expanded_escapes_parentheses_and_fnc1_before_length_is_written()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeGs1DataBarExpanded("(01)\u001D123", 40, 3, 1)
            .Build().GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x48, 1, 0x1D, 0x68, 40, 0x1D, 0x77, 3,
            0x1D, 0x6B, 78, 11 }, bytes.Take(13).ToArray());
        Assert.Equal(Encoding.ASCII.GetBytes("{(01{){1123"), bytes.Skip(13).ToArray());
    }

    [Fact]
    public void Expanded_bracketed_ai_sequence_uses_shared_fnc1_normalization()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeGs1DataBarExpanded("(01)01234567890128(10)ABC(17)250101", 40, 3, 1)
            .Build().GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x48, 1, 0x1D, 0x68, 40, 0x1D, 0x77, 3,
            0x1D, 0x6B, 78, 31 }, bytes.Take(13).ToArray());
        Assert.Equal(Encoding.ASCII.GetBytes("010123456789012810ABC{117250101"),
            bytes.Skip(13).ToArray());
    }

    [Fact]
    public void Native_gs1_128_accepts_supported_compressed_ai_and_preserves_fnc1()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128("(01)01234567890128(10)ABC(3103)000123", 40, 2, 0)
            .Build().GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x48, 0, 0x1D, 0x68, 40, 0x1D, 0x77, 2,
            0x1D, 0x6B, 74, 32 }, bytes.Take(13).ToArray());
        Assert.Equal(Encoding.ASCII.GetBytes("010123456789012810ABC\u001D3103000123"),
            bytes.Skip(13).ToArray());
    }

    [Fact]
    public void Native_gs1_128_normalizes_amount_and_discount_ai_with_specified_boundaries()
    {
        const string data = "(3909)123456789012345(3918)840123456789012345(3947)1234(3959)123456(17)250101";
        const string normalized =
            "3909123456789012345\u001D3918840123456789012345\u001D39471234395912345617250101";

        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(data, 40, 2, 0)
            .Build().GetBytes();

        Assert.Equal((byte)normalized.Length, bytes[12]);
        Assert.Equal(Encoding.ASCII.GetBytes(normalized), bytes.Skip(13).ToArray());
    }

    [Fact]
    public void Native_gs1_128_rejects_invalid_amount_and_discount_ai_without_pollution()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();
        var invalidData = new[]
        {
            "(01)01234567890128(3900)1234567890123456",
            "(01)01234567890128(3900)A",
            "(01)01234567890128(3910)840",
            "(01)01234567890128(3910)8401234567890123456",
            "(01)01234567890128(3940)123",
            "(01)01234567890128(3940)12345",
            "(01)01234567890128(3950)12345",
            "(01)01234567890128(3950)1234567",
            "(01)01234567890128(3960)1"
        };

        foreach (var data in invalidData)
            Assert.Throws<ArgumentException>(() => command.BarcodeUccEan128(data, 40, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData("90", 30)]
    [InlineData("91", 90)]
    [InlineData("99", 90)]
    public void Native_gs1_128_accepts_internal_ai_at_its_declared_limit(string ai, int valueLength)
    {
        var value = new string('A', valueLength);
        var data = $"({ai}){value}(17)250101";
        var normalized = $"{ai}{value}\u001D17250101";

        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(data, 40, 2, 0)
            .Build().GetBytes();

        Assert.Equal((byte)normalized.Length, bytes[12]);
        Assert.Equal(Encoding.ASCII.GetBytes(normalized), bytes.Skip(13).ToArray());
    }

    [Theory]
    [InlineData("90", 31)]
    [InlineData("91", 91)]
    [InlineData("99", 91)]
    public void Native_gs1_128_rejects_internal_ai_over_limit_without_pollution(string ai, int valueLength)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();
        var data = $"(01)01234567890128({ai}){new string('A', valueLength)}";

        Assert.Throws<ArgumentException>(() => command.BarcodeUccEan128(data, 40, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Native_gs1_128_rejects_internal_ai_characters_outside_cset82()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.BarcodeUccEan128(
            "(01)01234567890128(91)A#B", 40, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Native_gs1_128_decodes_literal_parentheses_before_encoding()
    {
        const string normalized = "91AB(C)D\u001D17250101";
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128("(91)AB\\(C)D(17)250101", 40, 2, 0)
            .Build().GetBytes();

        Assert.Equal((byte)normalized.Length, bytes[12]);
        Assert.Equal(Encoding.ASCII.GetBytes(normalized), bytes.Skip(13).ToArray());
    }

    [Fact]
    public void Native_gs1_128_rejects_invalid_single_element_escape_without_pollution()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.BarcodeUccEan128("(91)A\\)B", 40, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData("(91)A\\(B", "91A(B")]
    [InlineData("[91]A\\(B", "91A(B")]
    [InlineData("[91]A\\(B[17]250101", "91A(B\u001D17250101")]
    public void Shared_gs1_parser_decodes_literal_parentheses(string data, string normalized)
    {
        Assert.Equal(normalized, Gs1ApplicationIdentifierParser.NormalizeForProtocol(
            data, nameof(data), 255));
    }

    [Fact]
    public void Shared_gs1_parser_counts_decoded_parenthesis_toward_ai_length()
    {
        var maximum = "(90)" + new string('A', 29) + "\\(";
        Assert.Equal("90" + new string('A', 29) + "(",
            Gs1ApplicationIdentifierParser.NormalizeForProtocol(maximum, nameof(maximum), 255));

        var tooLong = "(90)" + new string('A', 30) + "\\(";
        Assert.Throws<ArgumentException>(() => Gs1ApplicationIdentifierParser.NormalizeForProtocol(
            tooLong, nameof(tooLong), 255));
    }

    [Theory]
    [InlineData("(91)A\\)B")]
    [InlineData("(91)A\\[B")]
    [InlineData("(91)A\\\\B")]
    [InlineData("(91)A\\")]
    public void Shared_gs1_parser_rejects_unknown_escape_sequences(string data)
    {
        Assert.Throws<ArgumentException>(() => Gs1ApplicationIdentifierParser.NormalizeForProtocol(
            data, nameof(data), 255));
    }

    [Fact]
    public void Shared_gs1_parser_accepts_cset82_and_rejects_space()
    {
        const string cset82 = "!\"%&'()*+,-./0123456789:;<=>?ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";
        Assert.Equal(82, cset82.Length);

        foreach (var character in cset82)
        {
            var inputValue = character == '(' ? "\\(" : character.ToString();
            Assert.Equal("91" + character, Gs1ApplicationIdentifierParser.NormalizeForProtocol(
                "(91)" + inputValue, nameof(character), 255));
        }

        Assert.Throws<ArgumentException>(() => Gs1ApplicationIdentifierParser.NormalizeForProtocol(
            "(91)A B", "data", 255));
    }

    [Fact]
    public void Expanded_preserves_official_literal_brace_data()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeGs1DataBarExpanded("12{", 40, 3, 1)
            .Build().GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x48, 1, 0x1D, 0x68, 40, 0x1D, 0x77, 3,
            0x1D, 0x6B, 78, 3 }, bytes.Take(13).ToArray());
        Assert.Equal(Encoding.ASCII.GetBytes("12{"), bytes.Skip(13).ToArray());
    }

    [Fact]
    public void Code128_auto_preserves_raw_8_bit_payload()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeCode128Auto(new byte[] { 0x00, 0xFF, 0x7F }, 60, 2, 0)
            .Build().GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x48, 0, 0x1D, 0x68, 60, 0x1D, 0x77, 2,
            0x1D, 0x6B, 79, 3, 0x00, 0xFF, 0x7F }, bytes);
    }

    [Theory]
    [InlineData(75, "123456789012")]
    [InlineData(76, "12345678901234")]
    [InlineData(77, "2234567890123")]
    [InlineData(78, "A1")]
    [InlineData(78, "(A1")]
    [InlineData(78, "A{")]
    [InlineData(78, "(01)01234567890128(1234)ABC")]
    public void Function_b_data_bar_families_reject_invalid_data_without_pollution(byte symbology, string data)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode(data, symbology));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Code128_auto_rejects_empty_or_oversized_binary_payload_without_pollution()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.BarcodeCode128Auto(Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => command.BarcodeCode128Auto(new byte[256]));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Capabilities_expose_native_data_bar_families_and_raster_code128_auto()
    {
        var native = new EscPosPrintCommand(Encoding.ASCII).Capabilities;
        Assert.True(native.SupportsBarcodeType(BarcodeType.Gs1DataBarOmnidirectional));
        Assert.True(native.SupportsBarcodeType(BarcodeType.Gs1DataBarTruncated));
        Assert.True(native.SupportsBarcodeType(BarcodeType.Gs1DataBarLimited));
        Assert.True(native.SupportsBarcodeType(BarcodeType.Gs1DataBarExpanded));
        Assert.True(native.SupportsBarcodeType(BarcodeType.Code128Auto));

        var raster = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Capabilities;
        Assert.True(raster.SupportsBarcodeType(BarcodeType.Gs1DataBarOmnidirectional));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.Gs1DataBarTruncated));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.Gs1DataBarLimited));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.Gs1DataBarExpanded));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.Code128Auto));
    }

    [Fact]
    public void Native_provider_renders_data_bar_and_code128_auto_through_gs_k()
    {
        var document = new PrintDocument();
        var page = document.AddPage(700, 120);
        page.Add(new PrintBarcodeElement(0, 0, BarcodeType.Gs1DataBarLimited, "1234567890123",
            40, narrowWidth: 2, wideWidth: 2));
        page.Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128Auto, "ABC",
            40, narrowWidth: 2, wideWidth: 2));

        var output = Assert.Single(PrintProtocolProviderFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native).Render(document));

        Assert.True(ContainsSequence(output, new byte[] { 0x1D, 0x6B, 77, 13 }));
        Assert.True(ContainsSequence(output,
            new byte[] { 0x1D, 0x6B, 79, 3, (byte)'A', (byte)'B', (byte)'C' }));
    }

    [Fact]
    public void Native_provider_renders_compressed_ai_after_template_preflight()
    {
        var document = new PrintDocument();
        document.AddPage(700, 120).Add(new PrintBarcodeElement(0, 0, BarcodeType.UccEan128,
            "(01)01234567890128(10)ABC(3103)000123", 40, 2, 2,
            humanReadable: false), "Barcode");

        var output = Assert.Single(PrintProtocolProviderFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native).Render(document));

        Assert.True(ContainsSequence(output,
            Encoding.ASCII.GetBytes("010123456789012810ABC\u001D3103000123")));
    }

    [Fact]
    public void Native_provider_renders_new_amount_and_discount_ai_after_preflight()
    {
        const string data = "(3909)1(3910)8401(3947)1234(3959)123456(17)250101";
        const string normalized = "39091\u001D39108401\u001D39471234395912345617250101";
        var document = new PrintDocument();
        document.AddPage(700, 120).Add(new PrintBarcodeElement(0, 0, BarcodeType.UccEan128,
            data, 40, 2, 2, humanReadable: false), "Barcode");

        var output = Assert.Single(PrintProtocolProviderFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native).Render(document));

        Assert.True(ContainsSequence(output, Encoding.ASCII.GetBytes(normalized)));
    }

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
            if (data.Skip(start).Take(sequence.Length).SequenceEqual(sequence)) return true;
        return false;
    }
}
