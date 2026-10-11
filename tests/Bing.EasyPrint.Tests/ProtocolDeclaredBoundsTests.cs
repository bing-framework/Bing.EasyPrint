using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证设备原生元素的显式占位预检。</summary>
public sealed class ProtocolDeclaredBoundsTests
{
    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Strict_mode_rejects_native_barcode_without_declared_width(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(400, 100)
            .Add(new PrintBarcodeElement(10, 10, BarcodeType.Code128, "AB12", 30), "Rows[0].Barcode");

        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            requireDeclaredNativeBounds: true);
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Render(document));

        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("预留宽度", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Strict_mode_requires_human_readable_height_when_enabled(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(400, 100).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.Code128, "AB12", 30, reservedWidth: 150), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
                requireDeclaredNativeBounds: true).Validate(document));

        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("预留高度", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Strict_mode_accepts_declared_human_readable_height_or_disabled_text(PrintProtocol protocol)
    {
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            requireDeclaredNativeBounds: true);
        var declared = new PrintDocument();
        declared.AddPage(400, 100).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.Code128, "AB12", 30, humanReadableReserveHeight: 12,
            reservedWidth: 150), "Rows[0].Barcode");
        var disabled = new PrintDocument();
        disabled.AddPage(400, 100).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.Code128, "AB12", 30, humanReadable: false,
            reservedWidth: 150), "Rows[0].Barcode");

        provider.Validate(declared);
        provider.Validate(disabled);
    }

    [Fact]
    public void Strict_mode_rejects_tspl_code39_without_declared_width()
    {
        var document = new PrintDocument();
        document.AddPage(400, 100)
            .Add(new PrintBarcodeElement(10, 10, BarcodeType.Code39, "AB12", 30), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
                requireDeclaredNativeBounds: true).Validate(document));

        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("预留宽度", error.Message);
    }

    [Fact]
    public void Supported_zpl_i2of5g_reports_missing_bounds_in_strict_mode()
    {
        var document = new PrintDocument();
        document.AddPage(400, 100)
            .Add(new PrintBarcodeElement(10, 10, BarcodeType.I2OF5G, "12345678901", 30), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
                requireDeclaredNativeBounds: true).Validate(document));

        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("预留宽度", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "0")]
    [InlineData(PrintProtocol.Zpl, "0")]
    [InlineData(PrintProtocol.Zpl, "A")]
    public void Strict_mode_rejects_unknown_device_font_bounds(PrintProtocol protocol, string font)
    {
        var document = new PrintDocument();
        document.AddPage(400, 100)
            .Add(new PrintTextElement(10, 10, "AB", 24, deviceFontName: font), "Rows[0].Text");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
                requireDeclaredNativeBounds: true).Validate(document));

        Assert.Equal("Rows[0].Text", error.ElementPath);
        Assert.Contains("预留宽高", error.Message);
    }

    [Fact]
    public void Strict_mode_accepts_declared_bounds_and_checks_page_edge()
    {
        var document = new PrintDocument();
        document.AddPage(100, 60)
            .Add(new PrintTextElement(10, 10, "AB", 24, width: 30, height: 24,
                deviceFontName: "A"), "Rows[0].Text");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            requireDeclaredNativeBounds: true);
        provider.Validate(document);

        var outside = new PrintDocument();
        outside.AddPage(100, 60)
            .Add(new PrintTextElement(80, 10, "AB", 24, width: 30, height: 24,
                deviceFontName: "A"), "Rows[0].Text");
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(outside));

        Assert.Equal("Rows[0].Text", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Strict_mode_checks_declared_tspl_code39_width_at_page_edge()
    {
        var document = new PrintDocument();
        document.AddPage(100, 80)
            .Add(new PrintBarcodeElement(70, 5, BarcodeType.Code39, "AB12", 30,
                reservedWidth: 31), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
                requireDeclaredNativeBounds: true).Validate(document));

        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Strict_mode_keeps_tspl_fixed_ascii_cell_measurement()
    {
        var document = new PrintDocument();
        document.AddPage(100, 60)
            .Add(new PrintTextElement(0, 0, "AB", 24, deviceFontName: "1"), "Rows[0].Text");

        PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            requireDeclaredNativeBounds: true).Validate(document);
    }

    [Fact]
    public void Strict_native_bounds_option_rejects_non_label_protocols()
    {
        Assert.Throws<ArgumentException>(() => PrintProtocolProviderFactory.Create(
            PrintProtocol.Cpcl, Encoding.ASCII, requireDeclaredNativeBounds: true));
    }
}
