using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 GS1 Scan4Transport 应用标识的字段格式、关联关系和协议输出。</summary>
public sealed class Gs1Scan4TransportTests
{
    private const string Sscc = "123456789012345675";

    [Theory]
    [InlineData("4300", "ACME+LOGISTICS", true)]
    [InlineData("4301", "JANE+DOE", true)]
    [InlineData("4302", "8+NEXUS+COURT", true)]
    [InlineData("4304", "MULGRAVE", true)]
    [InlineData("4305", "MELBOURNE", true)]
    [InlineData("4306", "VICTORIA", true)]
    [InlineData("4307", "AU", false)]
    [InlineData("4308", "+61390000000", true)]
    [InlineData("4309", "12345678901234567890", false)]
    [InlineData("4310", "RETURNS+HQ", true)]
    [InlineData("4311", "RETURN+DESK", true)]
    [InlineData("4312", "42+TEST+ROAD", true)]
    [InlineData("4314", "SUBURB", true)]
    [InlineData("4315", "LOCALITY", true)]
    [InlineData("4316", "REGION", true)]
    [InlineData("4317", "US", false)]
    [InlineData("4318", "12345", true)]
    [InlineData("4319", "18005550199", true)]
    [InlineData("4320", "HANDLE+WITH+CARE", true)]
    [InlineData("4321", "1", false)]
    [InlineData("4322", "0", false)]
    [InlineData("4323", "1", false)]
    [InlineData("4324", "2601011200", false)]
    [InlineData("4325", "2601319999", false)]
    [InlineData("4326", "260201", false)]
    public void Scan4transport_ai_fields_normalize_with_their_defined_fnc1_boundary(
        string ai, string value, bool variable)
    {
        var input = $"(00){Sscc}({ai}){value}(17)260101";
        var expected = $"00{Sscc}{ai}{value}{(variable ? "\u001D" : string.Empty)}17260101";

        Assert.Equal(expected, Normalize(input));
    }

    [Fact]
    public void Address_line_two_fields_require_and_normalize_after_their_first_lines()
    {
        var input = $"(00){Sscc}(4302)STREET(4303)UNIT+2(4312)RETURN+ROAD(4313)LEVEL+2(17)260101";
        var expected = $"00{Sscc}4302STREET\u001D4303UNIT+2\u001D4312RETURN+ROAD\u001D4313LEVEL+2\u001D17260101";

        Assert.Equal(expected, Normalize(input));
    }

    [Theory]
    [InlineData("(4300)ACME", "AI 00")]
    [InlineData("(00)123456789012345675(4303)UNIT+2", "AI 4302")]
    [InlineData("(00)123456789012345675(4313)UNIT+2", "AI 4312")]
    public void Transport_fields_require_sscc_and_address_line_pairs(string input, string required)
    {
        var error = Assert.Throws<ArgumentException>(() => Normalize(input));

        Assert.Contains(required, error.Message);
    }

    [Theory]
    [InlineData("(00)123456789012345675(4307)uS")]
    [InlineData("(00)123456789012345675(4317)USA")]
    [InlineData("(00)123456789012345675(4321)2")]
    [InlineData("(00)123456789012345675(4309)1234567890123456789")]
    [InlineData("(00)123456789012345675(4324)260101120")]
    [InlineData("(00)123456789012345675(4302)01234567890123456789012345678901234567890123456789012345678901234567890")]
    public void Transport_fields_reject_values_outside_the_declared_shape(string input)
    {
        Assert.Throws<ArgumentException>(() => Normalize(input));
    }

    [Theory]
    [InlineData("(253)1234567890128ABCDEFGHIJKLMNOPQ(17)260101",
        "2531234567890128ABCDEFGHIJKLMNOPQ\u001D17260101")]
    [InlineData("(253)1234567890128(17)260101",
        "2531234567890128\u001D17260101")]
    [InlineData("(414)1234567890128(254)ABCDEFGHIJKLMNOPQRST",
        "4141234567890128254ABCDEFGHIJKLMNOPQRST")]
    [InlineData("(255)1234567890128123456789012(17)260101",
        "2551234567890128123456789012\u001D17260101")]
    [InlineData("(255)1234567890128(17)260101",
        "2551234567890128\u001D17260101")]
    public void Document_location_and_coupon_ai_fields_normalize_with_gs1_boundaries(
        string input, string expected)
    {
        Assert.Equal(expected, Normalize(input));
    }

    [Theory]
    [InlineData("(253)1234567890127(17)260101")]
    [InlineData("(253)1234567890128ABCDEFGHIJKLMNOPQR(17)260101")]
    [InlineData("(253)1234567890128ABC@DEF(17)260101")]
    [InlineData("(414)1234567890128(254)ABCDEFGHIJKLMNOPQRSTU")]
    [InlineData("(254)ROOM-1")]
    [InlineData("(254)ROOM-1(414)1234567890127")]
    [InlineData("(255)1234567890127(17)260101")]
    [InlineData("(255)1234567890128A(17)260101")]
    [InlineData("(255)1234567890128123456789012A(17)260101")]
    public void Document_location_and_coupon_ai_fields_reject_invalid_keys_or_shapes(string input)
    {
        Assert.Throws<ArgumentException>(() => Normalize(input));
    }

    [Fact]
    public void Single_extended_identifier_ai_uses_strict_validation_in_native_providers()
    {
        const string invalid = "(253)1234567890127";

        Assert.Throws<ArgumentException>(() => new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(invalid, 40, 2, 0));
        Assert.Throws<ArgumentException>(() => new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, invalid, 40, 0, 0, 2, 2));
        Assert.Throws<ArgumentException>(() => new ZplPrintCommand(Encoding.ASCII).Start()
            .BarcodeUccEan128(10, 20, invalid, 40, false, 2));
    }

    [Fact]
    public void Extended_identifier_ai_payload_is_shared_by_native_protocols()
    {
        const string input = "(253)1234567890128DOC-1(414)1234567890128(254)WH-01(255)1234567890128123";
        const string normalized = "2531234567890128DOC-1\u001D4141234567890128254WH-01\u001D2551234567890128123";

        var native = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(input, 40, 2, 0).Build().GetBytes();
        Assert.True(ContainsSequence(native, Encoding.ASCII.GetBytes(normalized)));

        var tspl = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, input, 40, 0, 0, 2, 2).Build().GetBytes());
        Assert.Contains("2531234567890128DOC-1!1024141234567890128254WH-01!1022551234567890128123", tspl);

        var zpl = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII).Start()
            .BarcodeUccEan128(10, 20, input, 40, false, 2).End().Build().GetBytes());
        Assert.Contains("^FD>;>82531234567890128DOC-1>84141234567890128254WH-01>82551234567890128123^FS", zpl);
    }

    [Fact]
    public void Scan4transport_gs1_128_is_normalized_by_native_raster_tspl_and_zpl()
    {
        const string input = "(00)123456789012345675(4300)ACME+WAREHOUSE(4307)US(4321)1";
        const string normalized = "001234567890123456754300ACME+WAREHOUSE\u001D4307US43211";

        var native = new EscPosPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(input, 40, 2, 0).Build().GetBytes();
        Assert.True(ContainsSequence(native, Encoding.ASCII.GetBytes(normalized)));

        var raster = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(1000, 100)
            .RasterBarcode(BarcodeType.UccEan128, 0, 0, input, 40, 2)
            .EndRasterPage().Build().GetBytes();
        var rasterFromNormalized = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(1000, 100)
            .RasterBarcode(BarcodeType.UccEan128, 0, 0, normalized, 40, 2)
            .EndRasterPage().Build().GetBytes();
        Assert.Equal(rasterFromNormalized, raster);

        var tspl = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, input, 40, 0, 0, 2, 2).Build().GetBytes());
        Assert.Contains("4300ACME+WAREHOUSE!1024307US43211", tspl);

        var zpl = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII).Start()
            .BarcodeUccEan128(10, 20, input, 40, false, 2).End().Build().GetBytes());
        Assert.Contains("^FD>;>8001234567890123456754300ACME+WAREHOUSE>84307US43211^FS", zpl);
    }

    private static string Normalize(string input)
        => Gs1ApplicationIdentifierParser.NormalizeForProtocol(input, nameof(input), 255);

    private static bool ContainsSequence(byte[] bytes, byte[] sequence)
    {
        for (var start = 0; start <= bytes.Length - sequence.Length; start++)
            if (bytes.Skip(start).Take(sequence.Length).SequenceEqual(sequence))
                return true;
        return false;
    }
}
