using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ProtocolPrintCommandTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Factory_creates_the_requested_protocol(PrintProtocol protocol)
    {
        var command = ProtocolCommandFactory.Create(protocol, Encoding.ASCII);

        Assert.Equal(protocol, command.Protocol);
        Assert.NotNull(command.Capabilities);
        Assert.NotNull(command.Build());
    }

    [Fact]
    public void EscPos_capabilities_reflect_selected_layout_mode()
    {
        var native = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native);
        var raster = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster);
        var nativeProvider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native);
        var rasterProvider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster);

        Assert.False(native.Capabilities.SupportsCoordinates);
        Assert.False(native.Capabilities.SupportsGraphics);
        Assert.False(native.Capabilities.SupportsImages);
        Assert.True(raster.Capabilities.SupportsCoordinates);
        Assert.True(raster.Capabilities.SupportsGraphics);
        Assert.True(raster.Capabilities.SupportsImages);
        Assert.Equal(native.Capabilities.SupportsGraphics, nativeProvider.Capabilities.SupportsGraphics);
        Assert.Equal(raster.Capabilities.SupportsGraphics, rasterProvider.Capabilities.SupportsGraphics);
        Assert.True(native.Capabilities.SupportsUnderlineText);
        Assert.True(raster.Capabilities.SupportsUnderlineText);
        Assert.True(native.Capabilities.SupportsBarcodeType(BarcodeType.Code128));
        Assert.True(native.Capabilities.SupportsBarcodeType(BarcodeType.Code39));
        Assert.True(native.Capabilities.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(native.Capabilities.SupportsBarcodeType(BarcodeType.Codabar));
        Assert.True(native.Capabilities.SupportsBarcodeType(BarcodeType.Ean8));
        Assert.True(native.Capabilities.SupportsBarcodeType(BarcodeType.Ean13));
        Assert.True(native.Capabilities.SupportsBarcodeType(BarcodeType.UpcA));
        Assert.True(native.Capabilities.SupportsBarcodeType(BarcodeType.UpcE));
        Assert.True(native.Capabilities.SupportsBarcodeType(BarcodeType.I2OF5));
        Assert.True(raster.Capabilities.SupportsBarcodeType(BarcodeType.Code39));
        Assert.True(raster.Capabilities.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(raster.Capabilities.SupportsBarcodeType(BarcodeType.Codabar));
        Assert.True(raster.Capabilities.SupportsBarcodeType(BarcodeType.Ean8));
        Assert.True(raster.Capabilities.SupportsBarcodeType(BarcodeType.Ean13));
        Assert.True(raster.Capabilities.SupportsBarcodeType(BarcodeType.UpcA));
        Assert.True(raster.Capabilities.SupportsBarcodeType(BarcodeType.UpcE));
        Assert.True(raster.Capabilities.SupportsBarcodeType(BarcodeType.I2OF5));
        Assert.True(native.Capabilities.SupportsCommandBarcodeType(BarcodeType.Code39));
        Assert.True(native.Capabilities.SupportsCommandBarcodeType(BarcodeType.I2OF5));
        Assert.False(native.Capabilities.SupportsCommandBarcodeType(BarcodeType.Msi));
        Assert.False(PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII)
            .Capabilities.SupportsBoldText);
        Assert.False(native.Capabilities.SupportsTextRotation);
        Assert.True(raster.Capabilities.SupportsTextRotation);
        Assert.True(PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII)
            .Capabilities.SupportsTextRotation);
        Assert.True(ProtocolCommandFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII)
            .Capabilities.SupportsBarcodeRotation);
        Assert.True(PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII)
            .Capabilities.SupportsBarcodeRotation);
        Assert.True(PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII)
            .Capabilities.SupportsBarcodeRotation);
        Assert.True(raster.Capabilities.SupportsBarcodeRotation);
        Assert.True(raster.Capabilities.SupportsQrCodeRotation);
        Assert.False(native.Capabilities.SupportsQrCodeRotation);
        Assert.True(raster.Capabilities.SupportsFilledRectangles);
        Assert.False(native.Capabilities.SupportsFilledRectangles);
        Assert.True(native.Capabilities.SupportsPageCopies);
        Assert.True(native.Capabilities.SupportsExtraFeed);
        Assert.False(PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII)
            .Capabilities.SupportsExtraFeed);
    }

    [Fact]
    public void Provider_check_returns_typed_first_issue_without_throwing()
    {
        var unsupported = new PrintDocument();
        unsupported.AddPage(100, 100).Add(new PrintTextElement(1, 1, "A", 12, bold: true), "Rows[0].Cells[0]");
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

        var issue = tspl.Check(unsupported);
        Assert.False(issue.IsValid);
        Assert.Equal(PrintProtocol.Tspl, issue.Protocol);
        Assert.Equal("Rows[0].Cells[0]", issue.FirstIssue.ElementPath);
        Assert.True(issue.FirstIssue.IsCapabilityIssue);

        var invalid = new PrintDocument();
        invalid.AddPage(20, 20).Add(new PrintRectangleElement(15, 15, 10, 10));
        var dataIssue = tspl.Check(invalid);
        Assert.False(dataIssue.FirstIssue.IsCapabilityIssue);
        Assert.Equal("Pages[0].Elements[0]", dataIssue.FirstIssue.ElementPath);

        var valid = new PrintDocument();
        valid.AddPage(20, 20).Add(new PrintTextElement(0, 0, "OK", 12));
        Assert.True(tspl.Check(valid).IsValid);
    }

    [Fact]
    public void EscPos_device_options_declare_code_page_and_print_area_after_initialize()
    {
        var options = new EscPosDeviceOptions { Dpi = 300, CodePageIndex = 17, PrintAreaWidthUnits = 576 };
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native, options).Initialize();

        Assert.Equal(300, command.Capabilities.Dpi);
        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 17, 0x1D, 0x57, 0x40, 0x02 },
            command.Build().GetBytes());
    }

    [Fact]
    public void EscPos_provider_snapshots_device_options()
    {
        var options = new EscPosDeviceOptions { Dpi = 300, CodePageIndex = 17 };
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, options);
        options.Dpi = 203;
        options.CodePageIndex = 0;
        var document = new PrintDocument();
        document.AddPage(100, 40).Add(new PrintTextElement(0, 0, "OK", 24));

        Assert.Equal(300, provider.Capabilities.Dpi);
        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 17 }, provider.Render(document)[0].Take(5).ToArray());
    }

    [Fact]
    public void Tspl_provider_applies_media_options_after_page_setup()
    {
        var options = new TsplMediaOptions { GapDistance = 2, GapOffset = 1, Dpi = 300 };
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: options);
        options.GapDistance = 9;
        var document = new PrintDocument();
        document.AddPage(400, 200).Add(new PrintTextElement(0, 0, "OK", 24));

        var output = Encoding.ASCII.GetString(provider.Render(document)[0]);
        Assert.Equal(300, provider.Capabilities.Dpi);
        Assert.Contains("SIZE 400 dot,200 dot\r\nCODEPAGE USA\r\nCLS\r\nGAP 2,1\r\n", output);
        Assert.DoesNotContain("GAP 9,1", output);
    }

    [Theory]
    [InlineData(203, 9)]
    [InlineData(300, 6)]
    public void Tspl_provider_converts_text_dots_to_font_zero_points(int dpi, int expectedPoints)
    {
        var document = new PrintDocument();
        document.AddPage(400, 200).Add(new PrintTextElement(10, 20, "OK", 24));
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: new TsplMediaOptions { Dpi = dpi });

        var output = Encoding.ASCII.GetString(provider.Render(document)[0]);
        Assert.Contains($"TEXT 10,20,\"0\",0,{expectedPoints},{expectedPoints},\"OK\"", output);
    }

    [Fact]
    public void Zpl_provider_applies_media_speed_and_snapshots_dpi()
    {
        var options = new ZplMediaOptions { Dpi = 300, PrintSpeed = 4 };
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: options);
        options.Dpi = 203;
        options.PrintSpeed = 9;
        var document = new PrintDocument();
        document.AddPage(400, 200).Add(new PrintTextElement(0, 0, "OK", 24));

        var output = Encoding.ASCII.GetString(provider.Render(document)[0]);
        Assert.Equal(300, provider.Capabilities.Dpi);
        Assert.Contains("^PW400\r\n^LL200\r\n^PR4\r\n", output);
        Assert.DoesNotContain("^PR9", output);
    }

    [Fact]
    public void Print_document_preserves_element_order_and_copies_raster()
    {
        var raster = new byte[] { 0x80 };
        var document = new PrintDocument();
        var page = document.AddPage(8, 1)
            .Add(new PrintTextElement(0, 0, "A", 16))
            .Add(new PrintImageElement(0, 0, 8, 1, raster));
        raster[0] = 0;

        Assert.Equal(2, page.Elements.Count);
        Assert.Equal("A", ((PrintTextElement)page.Elements[0]).Text);
        Assert.Equal(0x80, ((PrintImageElement)page.Elements[1]).Raster[0]);
    }

    [Fact]
    public void Cpcl_provider_converts_msb_raster_to_black_white_image()
    {
        var document = new PrintDocument();
        document.AddPage(9, 2).Add(new PrintImageElement(10, 20, 9, 2,
            new byte[] { 0xAA, 0x80, 0x55, 0x00 }));

        var bytes = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII)
            .Render(document)[0];
        var output = Encoding.ASCII.GetString(bytes);

        Assert.NotEmpty(bytes);
        Assert.Contains("EG 2 2 10 20 AA805500\r\n", output);
    }

    [Fact]
    public void Tspl_provider_renders_document_elements()
    {
        var document = new PrintDocument();
        document.AddPage(60, 40).Add(new PrintTextElement(10, 10, "OK", 24));

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII)
            .Render(document)[0]);

        Assert.Contains("SIZE 60 dot,40 dot\r\n", output);
        Assert.Contains("TEXT 10,10", output);
        Assert.Contains("PRINT 1,1\r\n", output);
    }

    [Fact]
    public void Tspl_provider_preserves_barcode_rotation_from_document()
    {
        var document = new PrintDocument();
        document.AddPage(200, 200).Add(new PrintBarcodeElement(10, 20, BarcodeType.Code128,
            "1234", 40, 2, 4, RotationAngle.Rotate90));

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Tspl, Encoding.ASCII).Render(document)[0]);

        Assert.Contains("BARCODE 50,20,\"128\",40,1,90,2,4,\"1234\"", output);
    }

    [Theory]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Native)]
    public void Providers_reject_non_default_wide_width_with_element_path(
        PrintProtocol protocol, EscPosLayoutMode layoutMode)
    {
        var document = new PrintDocument();
        document.AddPage(400, 200).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.Code128, "1234", 40, wideWidth: 4), "Rows[3].Barcode");

        var error = Assert.Throws<PrintCapabilityException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layoutMode).Validate(document));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Rows[3].Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Native)]
    public void Providers_reject_cpcl_ratio_outside_cpcl_with_element_path(
        PrintProtocol protocol, EscPosLayoutMode layoutMode)
    {
        var document = new PrintDocument();
        document.AddPage(400, 200).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.Code128, "1234", 40, cpclRatioCode: 3), "Rows[4].Barcode");

        var error = Assert.Throws<PrintCapabilityException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layoutMode).Validate(document));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Rows[4].Barcode", error.ElementPath);
    }

    [Fact]
    public void Coordinate_providers_preserve_qr_error_correction()
    {
        var document = new PrintDocument();
        document.AddPage(400, 300).Add(new PrintQrCodeElement(10, 20, "OK", 3, QrCodeCorrectionLevel.H));

        var tspl = Encoding.ASCII.GetString(PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Render(document)[0]);
        var zpl = Encoding.ASCII.GetString(PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Render(document)[0]);

        Assert.Contains("QRCODE 10,20,H,3,A,0,\"OK\"", tspl);
        Assert.Contains("^FO10,20^BQN,2,3^FDHA,OK^FS", zpl);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Coordinate_providers_reject_unmapped_text_styles(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(400, 300).Add(new PrintTextElement(10, 20, "OK", 24, bold: true));

        var error = Assert.Throws<PrintCapabilityException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Render(document));
        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Pages[0].Elements[0]", error.ElementPath);
    }

    [Fact]
    public void Capabilities_expose_protocol_encoding_support()
    {
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;

        Assert.True(tspl.SupportsEncoding(Encoding.ASCII));
        Assert.True(tspl.SupportsEncoding(Encoding.UTF8));
        Assert.False(tspl.SupportsEncoding(Encoding.Unicode));
        Assert.True(zpl.SupportsEncoding(Encoding.ASCII));
        Assert.True(zpl.SupportsEncoding(Encoding.UTF8));
        Assert.False(zpl.SupportsEncoding(Encoding.Unicode));
        Assert.False(zpl.SupportsEncoding(null));
    }

    [Fact]
    public void Tspl_provider_rejects_unmapped_encoding_during_page_validation()
    {
        var document = new PrintDocument();
        document.AddPage(100, 50);

        var error = Assert.Throws<PrintCapabilityException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.Unicode).Validate(document));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Pages[0]", error.ElementPath);
    }

    [Fact]
    public void Zpl_provider_rejects_invalid_field_during_element_validation()
    {
        var document = new PrintDocument();
        document.AddPage(100, 50).Add(new PrintTextElement(0, 0, "^FD", 24), "Rows[2].Text");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Validate(document));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Rows[2].Text", error.ElementPath);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Coordinate_providers_reject_elements_outside_page_bounds(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(100, 50).Add(new PrintRectangleElement(90, 45, 20, 10), "Rows[4].Box");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
                protocol == PrintProtocol.EscPos ? EscPosLayoutMode.Raster : EscPosLayoutMode.Native)
                .Validate(document));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Rows[4].Box", error.ElementPath);
    }

    [Fact]
    public void EscPos_native_provider_rejects_oversized_barcode_before_rendering()
    {
        var document = new PrintDocument();
        document.AddPage(384, 100).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            new string('A', 256), 40), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Native).Validate(document));

        Assert.Equal("Rows[0].Barcode", error.ElementPath);
    }

    [Fact]
    public void EscPos_raster_provider_rejects_page_height_outside_protocol_range()
    {
        var document = new PrintDocument();
        document.AddPage(8, 65536);

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster).Validate(document));

        Assert.Equal("Pages[0]", error.ElementPath);
    }

    [Fact]
    public void EscPos_raster_provider_accepts_rotated_text_during_validation()
    {
        var document = new PrintDocument();
        document.AddPage(100, 100).Add(new PrintTextElement(0, 0, "A", 24,
            rotation: RotationAngle.Rotate90), "Rows[0].Text");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        provider.Validate(document);
        Assert.NotEmpty(provider.Render(document)[0]);
    }

    [Fact]
    public void EscPos_raster_drawing_failure_keeps_element_path()
    {
        var document = new PrintDocument();
        document.AddPage(32, 32).Add(new PrintQrCodeElement(16, 16, "OK", 4,
            QrCodeCorrectionLevel.M), "Rows[3].QrCode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster).Render(document));

        Assert.Equal("Rows[3].QrCode", error.ElementPath);
    }

    [Fact]
    public void Zpl_invalid_print_speed_does_not_record_page_commands()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Start();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Label(400, 300, 0));
        Assert.Equal(Encoding.ASCII.GetBytes("^XA\r\n^XZ\r\n"), command.End().Build().GetBytes());
    }

    [Fact]
    public void Zpl_provider_renders_document_elements()
    {
        var document = new PrintDocument();
        document.AddPage(400, 300).Add(new PrintTextElement(10, 20, "OK", 30));

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII)
            .Render(document)[0]);

        Assert.StartsWith("^XA\r\n", output);
        Assert.Contains("^FO10,20", output);
        Assert.EndsWith("^XZ\r\n", output);
    }

    [Fact]
    public void EscPos_native_provider_rejects_coordinate_layout()
    {
        var document = new PrintDocument();
        document.AddPage(384, 200).Add(new PrintTextElement(10, 10, "OK", 24));

        var error = Assert.Throws<PrintCapabilityException>(() => PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native).Render(document));
        Assert.Equal("Pages[0].Elements[0]", error.ElementPath);
    }

    [Fact]
    public void EscPos_native_provider_renders_origin_text()
    {
        var document = new PrintDocument();
        document.AddPage(384, 40).Add(new PrintTextElement(0, 0, "OK", 24));

        var bytes = PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native)
            .Render(document)[0];

        Assert.Equal(new byte[] { 0x1B, 0x40, (byte)'O', (byte)'K', 0x0A, 0x1B, 0x64, 0x01 }, bytes);
    }

    [Fact]
    public void EscPos_native_command_composes_tabbed_text_and_explicit_newlines()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .WriteText("A").Tab().WriteText("B").NewLine(2).Build().GetBytes();

        Assert.Equal(new byte[] { (byte)'A', 0x09, (byte)'B', 0x0A, 0x0A }, bytes);
    }

    [Fact]
    public void EscPos_native_provider_renders_sequential_text_rows()
    {
        var document = new PrintDocument();
        document.AddPage(384, 100)
            .Add(new PrintTextElement(0, 0, "A", 24))
            .Add(new PrintTextElement(0, 24, "B", 24))
            .Add(new PrintTextElement(0, 74, "C", 24));

        var bytes = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Render(document)[0];

        Assert.Equal(new byte[] { 0x1B, 0x40, (byte)'A', 0x0A, (byte)'B', 0x0A,
            0x0A, (byte)'C', 0x0A, 0x1B, 0x64, 0x01 }, bytes);
    }

    [Fact]
    public void EscPos_native_provider_uses_configured_tab_stops_for_same_row_text()
    {
        var document = new PrintDocument();
        document.AddPage(384, 48)
            .Add(new PrintTextElement(0, 0, "A", 24))
            .Add(new PrintTextElement(96, 0, "B", 24));
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { NativeTabWidthDots = 96 });

        Assert.Equal(new byte[] { 0x1B, 0x40, (byte)'A', 0x09, (byte)'B', 0x0A, 0x1B, 0x64, 0x01 },
            provider.Render(document)[0]);
    }

    [Fact]
    public void EscPos_native_provider_rejects_unconfigured_or_overlapping_tab_columns()
    {
        var document = new PrintDocument();
        document.AddPage(384, 48)
            .Add(new PrintTextElement(0, 0, "12345678", 24))
            .Add(new PrintTextElement(96, 0, "B", 24), "Rows[0].Cells[1]");

        var unconfigured = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);
        Assert.Equal("Rows[0].Cells[1]",
            Assert.Throws<PrintCapabilityException>(() => unconfigured.Validate(document)).ElementPath);

        var configured = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { NativeTabWidthDots = 96 });
        Assert.Equal("Rows[0].Cells[1]",
            Assert.Throws<PrintCapabilityException>(() => configured.Validate(document)).ElementPath);
    }

    [Fact]
    public void EscPos_native_provider_rejects_non_row_text_offset()
    {
        var document = new PrintDocument();
        document.AddPage(384, 100)
            .Add(new PrintTextElement(0, 0, "A", 24))
            .Add(new PrintTextElement(0, 31, "B", 24), "Rows[1].Cells[0].Elements[0]");

        var error = Assert.Throws<PrintCapabilityException>(() => PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native).Validate(document));

        Assert.Equal("Rows[1].Cells[0].Elements[0]", error.ElementPath);

        var firstOffset = new PrintDocument();
        firstOffset.AddPage(384, 100).Add(new PrintTextElement(0, 2, "A", 24));
        Assert.Throws<PrintCapabilityException>(() => PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native).Validate(firstOffset));
    }

    [Fact]
    public void EscPos_native_provider_keeps_text_barcode_and_qr_order()
    {
        var document = new PrintDocument();
        document.AddPage(384, 200)
            .Add(new PrintTextElement(0, 0, "OK", 24))
            .Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128, "1234", 40))
            .Add(new PrintQrCodeElement(0, 0, "QR", 4, QrCodeCorrectionLevel.H));

        var bytes = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Render(document)[0];

        var output = Encoding.ASCII.GetString(bytes);
        Assert.True(output.IndexOf("OK\n", StringComparison.Ordinal) < output.IndexOf("1234", StringComparison.Ordinal));
        Assert.True(output.IndexOf("1234", StringComparison.Ordinal) < output.IndexOf("QR", StringComparison.Ordinal));
        Assert.Contains(Enumerable.Range(0, bytes.Length - 3), index =>
            bytes.Skip(index).Take(4).SequenceEqual(new byte[] { 0x1D, 0x6B, 73, 6 }));
        Assert.Contains(Enumerable.Range(0, bytes.Length - 2), index =>
            bytes.Skip(index).Take(3).SequenceEqual(new byte[] { 0x31, 0x45, 51 }));
    }

    [Fact]
    public void EscPos_raster_provider_renders_graphics_document()
    {
        var document = new PrintDocument();
        document.AddPage(16, 8).Add(new PrintRectangleElement(1, 1, 14, 6));

        var bytes = PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster)
            .Render(document)[0];

        Assert.Contains((byte)0x1D, bytes);
        Assert.Contains((byte)0x76, bytes);
    }

    [Fact]
    public void EscPos_raster_provider_renders_text_pixels()
    {
        var document = new PrintDocument();
        document.AddPage(160, 40).Add(new PrintTextElement(2, 2, "OK", 24));

        var bytes = PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster)
            .Render(document)[0];

        Assert.Equal(new byte[] { 0x1D, 0x76, 0x30, 0x00, 20, 0, 40, 0 },
            bytes.Skip(2).Take(8).ToArray());
        Assert.Contains(bytes.Skip(10).Take(800), value => value != 0);
    }

    [Fact]
    public void EscPos_raster_provider_renders_code128_and_qr_pixels()
    {
        var document = new PrintDocument();
        document.AddPage(384, 160)
            .Add(new PrintBarcodeElement(8, 8, BarcodeType.Code128, "123456", 40))
            .Add(new PrintQrCodeElement(200, 8, "hello", 3, QrCodeCorrectionLevel.M));

        var bytes = PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster)
            .Render(document)[0];

        var raster = bytes.Skip(10).Take(48 * 160).ToArray();
        Assert.Contains(raster.Take(48 * 48), value => value != 0);
        Assert.Contains(raster.Skip(24), value => value != 0);
    }

    [Fact]
    public void EscPos_builds_native_text_and_cut()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Initialize()
            .Text("OK")
            .Cut()
            .Build().GetBytes();

        Assert.Equal(new byte[] { 0x1B, 0x40, (byte)'O', (byte)'K', 0x0A, 0x1D, 0x56, 0x00 }, bytes);
    }

    [Fact]
    public void EscPos_raster_rejects_wrong_payload_length()
    {
        Assert.Throws<ArgumentException>(() => new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .RasterImage(9, 1, new byte[] { 0 }));
    }

    [Fact]
    public void EscPos_invalid_qr_parameters_do_not_pollute_command()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Initialize();
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.QrCode("OK", 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.QrCode("OK", 4, 52));
        Assert.Throws<ArgumentException>(() => command.QrCode(string.Empty));
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void EscPos_raster_page_composes_lines_and_rectangles()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .Initialize()
            .RasterPage(16, 8, canvas => canvas.DrawRectangle(1, 1, 14, 6, 1))
            .Build().GetBytes();

        Assert.Contains((byte)0x1D, bytes);
        Assert.Contains((byte)0x76, bytes);
        Assert.True(bytes.Length > 10);
    }

    [Fact]
    public void Tspl_build_is_repeatable()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Size(60, 40).Text(10, 10, "OK").Print();
        var first = command.Build().GetBytes();
        var second = command.Build().GetBytes();

        Assert.Equal(first, second);
        Assert.Contains("SIZE 60 mm,40 mm\r\n", Encoding.ASCII.GetString(first));
        Assert.Contains("PRINT 1,1\r\n", Encoding.ASCII.GetString(first));
    }

    [Fact]
    public void Tspl_declares_the_code_page_matching_its_text_bytes()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("gbk");
        var gbkBytes = new TsplPrintCommand(gbk).Size(60, 40).Text(1, 2, "中文").Build().GetBytes();
        var utf8Bytes = new TsplPrintCommand(Encoding.UTF8).Size(60, 40).Text(1, 2, "中文").Build().GetBytes();

        Assert.Equal(gbk.GetBytes("SIZE 60 mm,40 mm\r\nCODEPAGE 936\r\nCLS\r\nTEXT 1,2,\"0\",0,1,1,\"中文\"\r\n"), gbkBytes);
        Assert.Equal(Encoding.UTF8.GetBytes("SIZE 60 mm,40 mm\r\nCODEPAGE UTF-8\r\nCLS\r\nTEXT 1,2,\"0\",0,1,1,\"中文\"\r\n"), utf8Bytes);
    }

    [Fact]
    public void Tspl_rejects_unmapped_encoding_before_recording_size()
    {
        var command = new TsplPrintCommand(Encoding.Unicode);

        Assert.Throws<NotSupportedException>(() => command.Size(60, 40));
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_bitmap_writes_raw_bytes_after_the_header()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .Bitmap(1, 2, 1, 2, 0, new byte[] { 0x80, 0x01 })
            .Build().GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes("BITMAP 1,2,1,2,0,")
            .Concat(new byte[] { 0x80, 0x01, 0x0D, 0x0A }), bytes);
    }

    [Fact]
    public void Zpl_builds_label_with_start_and_end()
    {
        var text = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start().Label(400, 300).Text(10, 20, "OK").End().Build().GetBytes());

        Assert.StartsWith("^XA\r\n", text);
        Assert.Contains("^FO10,20", text);
        Assert.EndsWith("^XZ\r\n", text);
    }

    [Fact]
    public void Zpl_line_encodes_a_diagonal_without_drawing_a_box()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { AllowRasterizedLines = true })
            .Start().Line(0, 0, 2, 2, 1).Box(4, 4, 8, 8, 1).End().Build().GetBytes());

        Assert.Contains("^FO0,0^GFA,3,3,1,804020^FS", output);
        Assert.Contains("^FO4,4^GB4,4,1^FS", output);
    }

    [Theory]
    [InlineData(0, 'N')]
    [InlineData(90, 'R')]
    [InlineData(180, 'I')]
    [InlineData(270, 'B')]
    public void Zpl_text_uses_protocol_orientation(int rotation, char orientation)
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Text(1, 2, "OK", 24, 24, "0", rotation).Build().GetBytes());

        Assert.Contains($"^FO1,2^A0{orientation},24,24^FDOK^FS", output);
    }

    [Fact]
    public void Zpl_utf8_label_declares_its_character_encoding()
    {
        var bytes = new ZplPrintCommand(Encoding.UTF8).Start().Text(1, 2, "中文").End().Build().GetBytes();
        var output = Encoding.UTF8.GetString(bytes);

        Assert.StartsWith("^XA\r\n^CI28\r\n", output);
        Assert.Contains("^FD中文^FS", output);
    }

    [Fact]
    public void Zpl_rejects_unmapped_encoding_before_recording_label_start()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var command = new ZplPrintCommand(Encoding.GetEncoding("gbk"));

        Assert.Throws<NotSupportedException>(() => command.Start());
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Raw_command_is_explicit_escape_hatch()
    {
        var text = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .WriteRaw("^XA\r\n^XZ\r\n").Build().GetBytes());

        Assert.Equal("^XA\r\n^XZ\r\n", text);
    }
}
