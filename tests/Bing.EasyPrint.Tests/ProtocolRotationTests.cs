using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 TSPL/ZPL 原生文字和一维条码的四向旋转、占位和失败恢复。</summary>
public sealed class ProtocolRotationTests
{
    [Theory]
    [InlineData(RotationAngle.None, 10, 20)]
    [InlineData(RotationAngle.Rotate90, 22, 20)]
    [InlineData(RotationAngle.Rotate180, 40, 32)]
    [InlineData(RotationAngle.Rotate270, 10, 50)]
    public void Tspl_provider_writes_text_at_rotated_bbox_anchor(
        RotationAngle rotation, int expectedX, int expectedY)
    {
        var document = new PrintDocument();
        document.AddPage(80, 80).Add(new PrintTextElement(10, 20, "AB", 24,
            rotation: rotation, width: 30, height: 12), "Rows[0].Text");

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Tspl, Encoding.ASCII).Render(document)[0]);

        Assert.Contains($"TEXT {expectedX},{expectedY},\"0\",{(int)rotation},9,9,\"AB\"\r\n", output);
    }

    [Fact]
    public void Tspl_fixed_font_text_uses_the_maximum_of_declared_and_derived_extent()
    {
        var document = new PrintDocument();
        document.AddPage(100, 100).Add(new PrintTextElement(5, 5, "AB", 24,
            rotation: RotationAngle.Rotate180, width: 20, height: 30,
            deviceFontName: "1"), "Rows[0].Text");

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Tspl, Encoding.ASCII).Render(document)[0]);

        // Font 1 is 8x12 at size 24, so "AB" derives 32x24; the declared
        // height remains the larger value and the derived width wins.
        Assert.Contains("TEXT 37,35,\"1\",180,2,2,\"AB\"\r\n", output);
    }

    [Theory]
    [InlineData(RotationAngle.None, 'N')]
    [InlineData(RotationAngle.Rotate90, 'R')]
    [InlineData(RotationAngle.Rotate180, 'I')]
    [InlineData(RotationAngle.Rotate270, 'B')]
    public void Zpl_provider_writes_text_with_native_orientation(
        RotationAngle rotation, char orientation)
    {
        var document = new PrintDocument();
        document.AddPage(80, 80).Add(new PrintTextElement(10, 20, "AB", 24,
            rotation: rotation, width: 30, height: 12), "Rows[0].Text");

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Zpl, Encoding.ASCII).Render(document)[0]);

        Assert.Contains($"^FO10,20^A0{orientation},24,24^FDAB^FS\r\n", output);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Tspl_provider_writes_barcode_at_rotated_bbox_anchor(RotationAngle rotation)
    {
        var declaredWidth = 100;
        var effectiveWidth = Math.Max(declaredWidth,
            EscPosRasterCanvas.MeasureBarcodeWidth("1234", 2));
        var expectedX = rotation == RotationAngle.Rotate90
            ? 50
            : rotation == RotationAngle.Rotate180 ? 10 + effectiveWidth : 10;
        var expectedY = rotation == RotationAngle.Rotate180
            ? 60
            : rotation == RotationAngle.Rotate270 ? 20 + effectiveWidth : 20;
        var document = new PrintDocument();
        document.AddPage(200, 200).Add(new PrintBarcodeElement(10, 20,
            BarcodeType.Code128, "1234", 40, narrowWidth: 2, wideWidth: 4,
            rotation: rotation, humanReadable: false, reservedWidth: declaredWidth),
            "Rows[0].Barcode");

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Tspl, Encoding.ASCII).Render(document)[0]);

        Assert.Contains($"BARCODE {expectedX},{expectedY},\"128\",40,0,{(int)rotation},2,4,\"1234\"\r\n", output);
    }

    [Theory]
    [InlineData(RotationAngle.None, 'N')]
    [InlineData(RotationAngle.Rotate90, 'R')]
    [InlineData(RotationAngle.Rotate180, 'I')]
    [InlineData(RotationAngle.Rotate270, 'B')]
    public void Zpl_provider_writes_barcode_with_native_orientation(
        RotationAngle rotation, char orientation)
    {
        var document = new PrintDocument();
        document.AddPage(200, 200).Add(new PrintBarcodeElement(10, 20,
            BarcodeType.Code128, "1234", 40, narrowWidth: 2, wideWidth: 2,
            rotation: rotation, humanReadable: false, reservedWidth: 100),
            "Rows[0].Barcode");

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Zpl, Encoding.ASCII).Render(document)[0]);

        Assert.Contains($"^FO10,20^BY2^BC{orientation},40,N,N,N^FD1234^FS\r\n", output);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, RotationAngle.None)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate270)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.None)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate270)]
    public void Coordinate_protocols_write_qr_with_native_orientation(PrintProtocol protocol, RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(300, 300).Add(new PrintQrCodeElement(10, 20, "QR", 3,
            QrCodeCorrectionLevel.M, rotation: rotation), "Rows[0].Qr");

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(protocol, Encoding.ASCII).Render(document)[0]);

        if (protocol == PrintProtocol.Tspl)
            Assert.Contains($"QRCODE 10,20,M,3,A,{(int)rotation},\"QR\"\r\n", output);
        else
        {
            var orientation = rotation == RotationAngle.None ? 'N' : rotation == RotationAngle.Rotate90 ? 'R' :
                rotation == RotationAngle.Rotate180 ? 'I' : 'B';
            Assert.Contains($"^FO10,20^BQ{orientation},2,3^FDMA,QR^FS\r\n", output);
        }
    }

    [Fact]
    public void Tspl_code128_rotation_uses_encoded_width_when_declared_width_is_smaller()
    {
        var encodedWidth = EscPosRasterCanvas.MeasureBarcodeWidth("1234", 2);
        var document = new PrintDocument();
        document.AddPage(10 + encodedWidth + 1, 20 + 40 + 1).Add(new PrintBarcodeElement(10, 20,
            BarcodeType.Code128, "1234", 40, narrowWidth: 2, wideWidth: 4,
            rotation: RotationAngle.Rotate180, humanReadable: false, reservedWidth: encodedWidth - 1),
            "Rows[0].Barcode");

        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Tspl, Encoding.ASCII).Render(document)[0]);

        Assert.Contains($"BARCODE {10 + encodedWidth},{20 + 40},\"128\",40,0,180,2,4,\"1234\"\r\n", output);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, RotationAngle.None)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate270)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.None)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate270)]
    public void Rotated_text_accepts_exact_page_fit(PrintProtocol protocol, RotationAngle rotation)
    {
        const int x = 10;
        const int y = 20;
        const int width = 30;
        const int height = 12;
        var physicalWidth = IsQuarterTurn(rotation) ? height : width;
        var physicalHeight = IsQuarterTurn(rotation) ? width : height;
        var document = new PrintDocument();
        document.AddPage(x + physicalWidth, y + physicalHeight)
            .Add(new PrintTextElement(x, y, "AB", 24, rotation: rotation,
                width: width, height: height), "Rows[0].Text");

        PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Validate(document);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, RotationAngle.None)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate270)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.None)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate270)]
    public void Rotated_text_rejects_one_dot_beyond_page(PrintProtocol protocol, RotationAngle rotation)
    {
        const int x = 10;
        const int y = 20;
        const int width = 30;
        const int height = 12;
        var physicalWidth = IsQuarterTurn(rotation) ? height : width;
        var physicalHeight = IsQuarterTurn(rotation) ? width : height;
        var document = new PrintDocument();
        document.AddPage(x + physicalWidth - 1, y + physicalHeight)
            .Add(new PrintTextElement(x, y, "AB", 24, rotation: rotation,
                width: width, height: height), "Rows[0].Text");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Validate(document));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Escpos_raster_provider_renders_rotated_text(RotationAngle rotation)
    {
        var fonts = FixedTestFonts.Catalog();
        var family = EscPosRasterCanvas.ResolveFontFamilyName("AB", null, false, fonts);
        var ink = EscPosRasterCanvas.MeasureTextInkBounds("AB", 24, false, true, family,
            long.MaxValue, fonts);
        var width = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270
            ? ink.Height : ink.Width;
        var height = rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270
            ? ink.Width : ink.Height;
        var document = new PrintDocument();
        document.AddPage(width + 10, height + 10).Add(new PrintTextElement(5, 5, "AB", 24,
            underline: true, rotation: rotation, fontFamily: family), "Rows[0].Text");

        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { RasterFonts = fonts });
        Assert.True(provider.Capabilities.SupportsTextRotation);
        var output = provider.Render(document);
        Assert.Single(output);
        Assert.True(Contains(output[0], new byte[] { 0x1D, 0x76, 0x30 }));
    }

    [Theory]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    public void Cpcl_direct_barcode_model_rejects_unstable_angles(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(200, 200).Add(new PrintBarcodeElement(20, 20,
            BarcodeType.Code128, "1234", 40, rotation: rotation), "Rows[0].Barcode");

        var error = Assert.Throws<PrintCapabilityException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Validate(document));

        Assert.Equal(PrintProtocol.Cpcl, error.Protocol);
        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("90/180", error.Message);
    }

    [Fact]
    public void Cpcl_direct_barcode_model_keeps_vertical_angle()
    {
        var document = new PrintDocument();
        document.AddPage(200, 200).Add(new PrintBarcodeElement(20, 20,
            BarcodeType.Code128, "1234", 40, rotation: RotationAngle.Rotate270), "Rows[0].Barcode");

        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
        provider.Validate(document);
        var output = Encoding.ASCII.GetString(provider.Render(document)[0]);

        Assert.Contains("VB 128", output);
    }

    private static bool Contains(byte[] data, byte[] pattern)
    {
        for (var index = 0; index <= data.Length - pattern.Length; index++)
        {
            var matched = true;
            for (var offset = 0; offset < pattern.Length; offset++)
                if (data[index + offset] != pattern[offset]) { matched = false; break; }
            if (matched) return true;
        }
        return false;
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, RotationAngle.None)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate270)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.None)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate270)]
    public void Rotated_barcode_accepts_exact_page_fit(PrintProtocol protocol, RotationAngle rotation)
    {
        const int x = 10;
        const int y = 20;
        var declaredWidth = EscPosRasterCanvas.MeasureBarcodeWidth("1234", 2) + 20;
        const int height = 40;
        var physicalWidth = IsQuarterTurn(rotation) ? height : declaredWidth;
        var physicalHeight = IsQuarterTurn(rotation) ? declaredWidth : height;
        var document = new PrintDocument();
        document.AddPage(x + physicalWidth, y + physicalHeight)
            .Add(new PrintBarcodeElement(x, y, BarcodeType.Code128, "1234", height,
                narrowWidth: 2, wideWidth: protocol == PrintProtocol.Tspl ? 4 : 2, rotation: rotation, humanReadable: false,
                reservedWidth: declaredWidth), "Rows[0].Barcode");

        PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Validate(document);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, RotationAngle.None)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate270)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.None)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate270)]
    public void Rotated_barcode_rejects_one_dot_beyond_page(PrintProtocol protocol, RotationAngle rotation)
    {
        const int x = 10;
        const int y = 20;
        var declaredWidth = EscPosRasterCanvas.MeasureBarcodeWidth("1234", 2) + 20;
        const int height = 40;
        var physicalWidth = IsQuarterTurn(rotation) ? height : declaredWidth;
        var physicalHeight = IsQuarterTurn(rotation) ? declaredWidth : height;
        var document = new PrintDocument();
        document.AddPage(x + physicalWidth - 1, y + physicalHeight)
            .Add(new PrintBarcodeElement(x, y, BarcodeType.Code128, "1234", height,
                narrowWidth: 2, wideWidth: protocol == PrintProtocol.Tspl ? 4 : 2, rotation: rotation, humanReadable: false,
                reservedWidth: declaredWidth), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Validate(document));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Rows[0].Barcode", error.ElementPath);
    }

    [Fact]
    public void Tspl_rotated_font_zero_text_without_size_reports_data_validation_path()
    {
        var document = new PrintDocument();
        document.AddPage(100, 100)
            .Add(new PrintTextElement(10, 20, "AB", 24,
                rotation: RotationAngle.Rotate90), "Rows[0].Text");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Validate(document));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
        Assert.Contains("预留宽高", error.Message);
    }

    [Fact]
    public void Tspl_rotated_code39_without_reserved_width_reports_data_validation_path()
    {
        var document = new PrintDocument();
        document.AddPage(200, 100)
            .Add(new PrintBarcodeElement(10, 20, BarcodeType.Code39, "AB12", 40,
                narrowWidth: 2, wideWidth: 4, rotation: RotationAngle.Rotate90,
                humanReadable: false), "Rows[0].Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Validate(document));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Rows[0].Barcode", error.ElementPath);
        Assert.Contains("预留宽度", error.Message);
    }

    [Fact]
    public void Tspl_legacy_text_and_barcode_overloads_keep_their_bytes()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .Text(1, 2, "OK")
            .Barcode(10, 20, "1234")
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "TEXT 1,2,\"0\",0,1,1,\"OK\"\r\n" +
            "BARCODE 10,20,\"128\",80,1,0,2,2,\"1234\"\r\n"), bytes);
    }

    [Fact]
    public void Zpl_invalid_barcode_rotation_does_not_pollute_command_and_build_recovers()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Start();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.Barcode(1, 2, "1234", 40, false, 2, 45));

        var bytes = command
            .Text(1, 2, "OK")
            .Barcode(3, 4, "1234", 40, false, 2, 270)
            .End()
            .Build()
            .GetBytes();
        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^FO1,2^A0N,30,30^FDOK^FS\r\n" +
            "^FO3,4^BY2^BCB,40,N,N,N^FD1234^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
        Assert.Equal(expected, command.Build().GetBytes());
    }

    private static bool IsQuarterTurn(RotationAngle rotation)
        => rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270;
}
