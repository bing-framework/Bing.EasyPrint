using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证统一 Code39 条码在各协议 Provider 中的能力边界和编码结果。</summary>
public sealed class Code39ProviderTests
{
    [Fact]
    public void EscPos_native_unified_code39_writes_fixed_gs_k_bytes()
    {
        var bytes = NativeProvider().Render(BarcodeDocument(BarcodeType.Code39, "ABC-12",
            barHeight: 40, narrowWidth: 2, wideWidth: 2, humanReadable: false))[0];

        Assert.Equal(new byte[]
        {
            0x1B, 0x40,
            0x1D, 0x48, 0x00,
            0x1D, 0x68, 0x28,
            0x1D, 0x77, 0x02,
            0x1D, 0x6B, 0x45, 0x06,
            (byte)'A', (byte)'B', (byte)'C', (byte)'-', (byte)'1', (byte)'2'
        }, bytes);
    }

    [Fact]
    public void EscPos_raster_renders_unified_code39_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        var bytes = provider.Render(BarcodeDocument(BarcodeType.Code39, "ABC-12", barHeight: 40))[0];

        Assert.NotEmpty(bytes);
        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code39));
    }

    [Fact]
    public void Tspl_unified_code39_writes_the_native_39_barcode_command()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);
        var bytes = provider.Render(BarcodeDocument(BarcodeType.Code39, "ABC-12",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, wideWidth: 3,
            humanReadable: false))[0];
        var output = Encoding.ASCII.GetString(bytes);

        Assert.Contains("BARCODE 10,20,\"39\",40,0,0,2,3,\"ABC-12\"\r\n", output);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ABC_12")]
    public void EscPos_native_rejects_lowercase_or_illegal_code39_data(string data)
    {
        var error = Assert.Throws<PrintElementValidationException>(() => NativeProvider().Validate(
            BarcodeDocument(BarcodeType.Code39, data, barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("CODE39", error.Message);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ABC_12")]
    public void Tspl_rejects_lowercase_or_illegal_code39_data(string data)
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(
            BarcodeDocument(BarcodeType.Code39, data, barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("Code39", error.Message);
    }

    [Fact]
    public void Capabilities_expose_code39_for_native_escpos_tspl_and_raster()
    {
        var native = NativeProvider().Capabilities;
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;

        Assert.True(native.SupportsBarcodeType(BarcodeType.Code39));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.Code39));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Code39));
    }

    private static IPrintProtocolProvider NativeProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);

    private static PrintDocument BarcodeDocument(BarcodeType type, string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, int wideWidth = 2, bool? humanReadable = null)
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, type, data, barHeight, narrowWidth, wideWidth,
            humanReadable: humanReadable), "Barcode");
        return document;
    }
}
