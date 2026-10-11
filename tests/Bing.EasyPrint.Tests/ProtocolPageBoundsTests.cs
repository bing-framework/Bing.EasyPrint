using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证坐标协议对页面边界和元素原点的严格校验。</summary>
public sealed class ProtocolPageBoundsTests
{
    [Fact]
    public void Tspl_fixed_font_checks_implicit_ascii_cell_extent()
    {
        var document = new PrintDocument();
        document.AddPage(100, 60)
            .Add(new PrintTextElement(80, 1, "AB", 24, deviceFontName: "1"), "Rows[0].Text");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Validate(document));

        Assert.Equal("Rows[0].Text", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_providers_check_encoded_code128_width_without_reservation(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(200, 80)
            .Add(new PrintBarcodeElement(160, 1, BarcodeType.Code128, "AB12", 30), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Validate(document));

        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_providers_measure_qr_when_reservation_is_unknown(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(100, 100)
            .Add(new PrintQrCodeElement(85, 10, "OK", 2,
                QrCodeCorrectionLevel.M), "Rows[0].Qr");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Validate(document));

        Assert.Equal("Rows[0].Qr", error.ElementPath);
        Assert.Contains("二维码", error.Message);
    }

    [Fact]
    public void Tspl_rotated_reserved_text_checks_transposed_height()
    {
        var document = new PrintDocument();
        document.AddPage(100, 100)
            .Add(new PrintTextElement(0, 60, "OK", 12,
                rotation: RotationAngle.Rotate90, width: 45, height: 10), "Rows[0].Text");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Validate(document));

        Assert.Equal("Rows[0].Text", error.ElementPath);
        Assert.Contains("高度", error.Message);
    }

    [Fact]
    public void Tspl_rotated_reserved_barcode_checks_transposed_height()
    {
        var document = new PrintDocument();
        document.AddPage(100, 100)
            .Add(new PrintBarcodeElement(0, 60, BarcodeType.Code128, "1234", 10,
                rotation: RotationAngle.Rotate90, reservedWidth: 45), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Validate(document));

        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("条码", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_providers_reject_diagonal_stroke_crossing_top_or_left(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(100, 50)
            .Add(new PrintLineElement(0, 1, 20, 20, 5), "Rows[0].Line");

        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            tsplOptions: protocol == PrintProtocol.Tspl ? new TsplMediaOptions { AllowRasterizedLines = true } : null,
            zplOptions: protocol == PrintProtocol.Zpl ? new ZplMediaOptions { AllowRasterizedLines = true } : null);
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));

        Assert.Equal("Rows[0].Line", error.ElementPath);
        Assert.Contains("超出", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 101, 10, 99, 10)]
    [InlineData(PrintProtocol.Tspl, 100, 10, 99, 10)]
    [InlineData(PrintProtocol.Tspl, 10, 51, 10, 49)]
    [InlineData(PrintProtocol.Zpl, 101, 10, 99, 10)]
    [InlineData(PrintProtocol.Zpl, 100, 10, 99, 10)]
    [InlineData(PrintProtocol.Zpl, 10, 51, 10, 49)]
    [InlineData(PrintProtocol.EscPos, 101, 10, 99, 10)]
    [InlineData(PrintProtocol.EscPos, 100, 10, 99, 10)]
    [InlineData(PrintProtocol.EscPos, 10, 51, 10, 49)]
    public void Coordinate_providers_reject_line_start_outside_page(
        PrintProtocol protocol, int x, int y, int endX, int endY)
    {
        var document = new PrintDocument();
        document.AddPage(100, 50)
            .Add(new PrintLineElement(x, y, endX, endY), "Rows[0].Line");

        var layout = protocol == PrintProtocol.EscPos ? EscPosLayoutMode.Raster : EscPosLayoutMode.Native;
        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout).Validate(document));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Rows[0].Line", error.ElementPath);
        Assert.Contains("范围", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Coordinate_providers_reject_line_end_at_page_width(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(100, 50)
            .Add(new PrintLineElement(99, 10, 100, 10), "Rows[0].Line");

        var layout = protocol == PrintProtocol.EscPos ? EscPosLayoutMode.Raster : EscPosLayoutMode.Native;
        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout).Validate(document));
        Assert.Equal("Rows[0].Line", error.ElementPath);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Coordinate_providers_accept_fixed_geometry_at_page_boundary(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(100, 50)
            .Add(new PrintRectangleElement(0, 0, 100, 50), "Rows[0].Box");

        var layout = protocol == PrintProtocol.EscPos ? EscPosLayoutMode.Raster : EscPosLayoutMode.Native;
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout);

        provider.Validate(document);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Coordinate_providers_reject_element_origin_beyond_page(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(100, 50)
            .Add(new PrintBarcodeElement(101, 0, BarcodeType.Code128, "1234", 20), "Rows[0].Barcode");

        var layout = protocol == PrintProtocol.EscPos ? EscPosLayoutMode.Raster : EscPosLayoutMode.Native;
        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout).Validate(document));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Rows[0].Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_providers_reject_axis_line_stroke_beyond_page(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(100, 50)
            .Add(new PrintLineElement(98, 2, 98, 20, 3), "Rows[0].Line");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Validate(document));
        Assert.Equal("Rows[0].Line", error.ElementPath);
        Assert.Contains("笔画", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_providers_reject_reserved_symbol_width_beyond_page(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(100, 50)
            .Add(new PrintBarcodeElement(80, 1, BarcodeType.Code128, "1234", 20,
                reservedWidth: 21), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Validate(document));
        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_providers_reject_reserved_qr_and_text_beyond_page(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(100, 50)
            .Add(new PrintQrCodeElement(80, 1, "hello", 2, QrCodeCorrectionLevel.M,
                width: 21, height: 20), "Rows[0].Qr");
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
        Assert.Equal("Rows[0].Qr",
            Assert.Throws<PrintElementValidationException>(() => provider.Validate(document)).ElementPath);

        var textDocument = new PrintDocument();
        textDocument.AddPage(100, 50)
            .Add(new PrintTextElement(80, 1, "test", 12, width: 21, height: 12), "Rows[0].Text");
        Assert.Equal("Rows[0].Text",
            Assert.Throws<PrintElementValidationException>(() => provider.Validate(textDocument)).ElementPath);
    }
}
