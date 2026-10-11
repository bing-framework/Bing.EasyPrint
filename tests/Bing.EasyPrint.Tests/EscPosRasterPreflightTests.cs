using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosRasterPreflightTests
{
    [Fact]
    public void Empty_qr_is_rejected_with_element_path_during_validate()
    {
        var document = new PrintDocument();
        document.AddPage(32, 32)
            .Add(new PrintQrCodeElement(0, 0, "", 1, QrCodeCorrectionLevel.M), "Rows[0].Qr");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster).Validate(document));
        Assert.Equal("Rows[0].Qr", error.ElementPath);
    }

    [Fact]
    public void Raster_buffer_overflow_is_rejected_with_page_path_during_validate()
    {
        var document = new PrintDocument();
        document.AddPage(ushort.MaxValue * 8, ushort.MaxValue);

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster).Validate(document));
        Assert.Equal("Pages[0]", error.ElementPath);
    }

    [Fact]
    public void Barcode_encoded_width_is_checked_before_render()
    {
        var document = new PrintDocument();
        document.AddPage(80, 80).Add(new PrintBarcodeElement(1, 1, BarcodeType.Code128,
            "1234567890", 30), "Items[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster).Validate(document));
        Assert.Equal("Items[0].Barcode", error.ElementPath);
    }

    [Fact]
    public void Qr_encoded_height_is_checked_before_render()
    {
        var document = new PrintDocument();
        document.AddPage(100, 30).Add(new PrintQrCodeElement(1, 10, "hello", 2,
            QrCodeCorrectionLevel.M), "Items[0].Qr");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster).Validate(document));
        Assert.Equal("Items[0].Qr", error.ElementPath);
    }

    [Fact]
    public void Thick_line_brush_cannot_cross_page_edge()
    {
        var document = new PrintDocument();
        document.AddPage(80, 80).Add(new PrintLineElement(0, 10, 50, 10, 3), "Items[0].Line");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster).Validate(document));
        Assert.Equal("Items[0].Line", error.ElementPath);
    }

    [Fact]
    public void Text_advance_width_is_checked_before_render()
    {
        var document = new PrintDocument();
        document.AddPage(80, 80).Add(new PrintTextElement(75, 5, "MMMM", 24), "Items[0].Text");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster).Validate(document));
        Assert.Equal("Items[0].Text", error.ElementPath);
    }

    [Fact]
    public void Text_ink_below_page_is_rejected_before_render()
    {
        var document = new PrintDocument();
        document.AddPage(80, 80).Add(new PrintTextElement(5, 75, "M", 24), "Items[0].Text");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster).Validate(document));
        Assert.Equal("Items[0].Text", error.ElementPath);
    }
}
