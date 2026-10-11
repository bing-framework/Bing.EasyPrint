using System;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 TSPL 已声明条码制式的数据校验。</summary>
public sealed class TsplBarcodeValidationTests
{
    [Theory]
    [InlineData("128", "ABC123", "ABC123")]
    [InlineData("93", "Code93-A", "Code93-A")]
    [InlineData("39", "ABC-123", "ABC-123")]
    [InlineData("25", "1234", "1234")]
    [InlineData("25", "123", "0123")]
    [InlineData("25C", "123", "123")]
    [InlineData("25C", "1234", "01234")]
    [InlineData("EAN13", "590123412345", "5901234123457")]
    [InlineData("EAN13", "5901234123457", "5901234123457")]
    [InlineData("EAN8", "963850", "09638501")]
    [InlineData("EAN8", "96385074", "96385074")]
    [InlineData("UPCA", "03600029145", "036000291452")]
    [InlineData("UPCA", "036000291452", "036000291452")]
    [InlineData("UPCE", "425261", "0425261")]
    [InlineData("UPCE", "0425261", "0425261")]
    [InlineData("CODABAR", "A12B", "A12B")]
    public void Supported_barcode_data_is_written_and_retail_variants_are_normalized(string type, string data, string expectedData)
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .Barcode(1, 2, type, 40, 1, 0, 2, 3, data)
            .Build()
            .GetBytes());

        Assert.Contains($"BARCODE 1,2,\"{type}\",40,1,0,2,3,\"{expectedData}\"\r\n", output);
    }

    [Theory]
    [InlineData("128", "")]
    [InlineData("128", "中文")]
    [InlineData("93", "")]
    [InlineData("93", "中文")]
    [InlineData("93", "A\u0001B")]
    [InlineData("39", "abc")]
    [InlineData("39", "*ABC*")]
    [InlineData("25", "12A4")]
    [InlineData("25C", "12A4")]
    [InlineData("EAN13", "ABC")]
    [InlineData("EAN13", "5901234123450")]
    [InlineData("EAN8", "96385075")]
    [InlineData("EAN8", "12345")]
    [InlineData("UPCA", "036000291453")]
    [InlineData("UPCE", "2425261")]
    [InlineData("CODABAR", "123")]
    [InlineData("CODABAR", "AB12B")]
    public void Invalid_supported_barcode_data_is_rejected(string type, string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(1, 2, "OK");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode(10, 20, type, 40, 1, 0, 2, 3, data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void A_failed_barcode_validation_does_not_apply_pending_media_options()
    {
        var command = new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { GapDistance = 2, GapOffset = 1 })
            .Size(60, 40, "dot");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode(1, 2, "EAN13", 40, 1, 0, 2, 3, "ABC"));

        Assert.Equal(before, command.Build().GetBytes());
    }
}
