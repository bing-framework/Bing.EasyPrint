using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ESC/POS Raster 条码和二维码的四向旋转及 Provider 输出。</summary>
public sealed class EscPosRasterCodeRotationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void DrawBarcode_rotation_maps_every_black_pixel(int rotation)
    {
        const string data = "1234";
        const int moduleWidth = 2;
        const int height = 9;
        var sourceWidth = EscPosRasterCanvas.MeasureBarcodeWidth(data, moduleWidth);
        var source = new EscPosRasterCanvas(sourceWidth, height);
        source.DrawBarcode(0, 0, data, height, moduleWidth);

        var outputWidth = IsQuarterTurn(rotation) ? height : sourceWidth;
        var outputHeight = IsQuarterTurn(rotation) ? sourceWidth : height;
        var rotated = new EscPosRasterCanvas(outputWidth, outputHeight);
        rotated.DrawBarcode(0, 0, data, height, moduleWidth, humanReadable: false,
            humanReadableReserveHeight: 0, fontFamily: null, humanReadableFontSize: 12,
            rotation: rotation);

        AssertRotation(source.ToBytes(), source.RowBytes, source.Width, source.Height,
            rotated.ToBytes(), rotated.RowBytes, rotated.Width, rotated.Height, rotation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void DrawQrCode_rotation_maps_every_black_pixel(int rotation)
    {
        const string data = "QR rotation";
        const int moduleSize = 2;
        const QrCodeCorrectionLevel errorLevel = QrCodeCorrectionLevel.M;
        var sourceSize = EscPosRasterCanvas.MeasureQrCodeSize(data, moduleSize, errorLevel);
        var source = new EscPosRasterCanvas(sourceSize, sourceSize);
        source.DrawQrCode(0, 0, data, moduleSize, errorLevel);

        var rotated = new EscPosRasterCanvas(sourceSize, sourceSize);
        rotated.DrawQrCode(0, 0, data, moduleSize, errorLevel, rotation);

        AssertRotation(source.ToBytes(), source.RowBytes, source.Width, source.Height,
            rotated.ToBytes(), rotated.RowBytes, rotated.Width, rotated.Height, rotation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void DrawBarcode_rotation_rejects_content_that_would_be_clipped(int rotation)
    {
        const string data = "1234";
        const int moduleWidth = 2;
        const int height = 9;
        var sourceWidth = EscPosRasterCanvas.MeasureBarcodeWidth(data, moduleWidth);
        var outputWidth = IsQuarterTurn(rotation) ? height : sourceWidth;
        var outputHeight = IsQuarterTurn(rotation) ? sourceWidth : height;
        var canvas = new EscPosRasterCanvas(outputWidth - 1, outputHeight);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawBarcode(
            0, 0, data, height, moduleWidth, humanReadable: false,
            humanReadableReserveHeight: 0, fontFamily: null, humanReadableFontSize: 12,
            rotation: rotation));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void DrawQrCode_rotation_rejects_content_that_would_be_clipped(int rotation)
    {
        const string data = "QR rotation";
        const int moduleSize = 2;
        const QrCodeCorrectionLevel errorLevel = QrCodeCorrectionLevel.M;
        var sourceSize = EscPosRasterCanvas.MeasureQrCodeSize(data, moduleSize, errorLevel);
        var canvas = new EscPosRasterCanvas(sourceSize - 1, sourceSize);

        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawQrCode(
            0, 0, data, moduleSize, errorLevel, rotation));
    }

    [Fact]
    public void Raster_code_rotation_rejects_angles_outside_the_four_corners()
    {
        var barcode = new EscPosRasterCanvas(128, 32);
        Assert.Throws<ArgumentOutOfRangeException>(() => barcode.DrawBarcode(
            0, 0, "1234", 9, 2, humanReadable: false,
            humanReadableReserveHeight: 0, fontFamily: null, humanReadableFontSize: 12,
            rotation: 45));

        var qr = new EscPosRasterCanvas(128, 128);
        Assert.Throws<ArgumentOutOfRangeException>(() => qr.DrawQrCode(
            0, 0, "QR rotation", 2, QrCodeCorrectionLevel.M, 45));
    }

    [Fact]
    public void Raster_provider_advertises_code_rotations_and_writes_the_same_raster_payload()
    {
        const int pageWidth = 200;
        const int pageHeight = 200;
        const string barcodeData = "1234";
        const int barcodeX = 4;
        const int barcodeY = 5;
        const int barcodeHeight = 9;
        const int barcodeModuleWidth = 2;
        const RotationAngle barcodeRotation = RotationAngle.Rotate90;
        const string qrData = "QR rotation";
        const int qrX = 90;
        const int qrY = 80;
        const int qrModuleSize = 2;
        const RotationAngle qrRotation = RotationAngle.Rotate270;

        var document = new PrintDocument();
        document.AddPage(pageWidth, pageHeight)
            .Add(new PrintBarcodeElement(barcodeX, barcodeY, BarcodeType.Code128, barcodeData,
                barcodeHeight, narrowWidth: barcodeModuleWidth, wideWidth: barcodeModuleWidth,
                rotation: barcodeRotation, humanReadable: false), "Rows[0].Barcode")
            .Add(new PrintQrCodeElement(qrX, qrY, qrData, qrModuleSize,
                QrCodeCorrectionLevel.M, rotation: qrRotation), "Rows[0].Qr");

        var provider = PrintProtocolProviderFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsRasterLayout);
        Assert.True(provider.Capabilities.SupportsBarcodeRotation);
        Assert.True(provider.Capabilities.SupportsQrCodeRotation);
        provider.Validate(document);

        var expected = new EscPosRasterCanvas(pageWidth, pageHeight);
        expected.DrawBarcode(barcodeX, barcodeY, barcodeData, barcodeHeight,
            barcodeModuleWidth, humanReadable: false, humanReadableReserveHeight: 0,
            fontFamily: null, humanReadableFontSize: 12, rotation: (int)barcodeRotation);
        expected.DrawQrCode(qrX, qrY, qrData, qrModuleSize, QrCodeCorrectionLevel.M,
            (int)qrRotation);

        var outputPage = Assert.Single(provider.RenderOutput(document));
        var outputSegment = Assert.Single(outputPage.Segments);
        var payload = ReadRasterPayload(outputSegment.Data, out var rowBytes, out var height);

        Assert.Equal(expected.RowBytes, rowBytes);
        Assert.Equal(pageHeight, height);
        Assert.Equal(expected.ToBytes(), payload);
    }

    private static void AssertRotation(byte[] source, int sourceRowBytes, int sourceWidth,
        int sourceHeight, byte[] actual, int actualRowBytes, int actualWidth, int actualHeight,
        int rotation)
    {
        var expected = new bool[checked(actualWidth * actualHeight)];
        for (var sourceY = 0; sourceY < sourceHeight; sourceY++)
        for (var sourceX = 0; sourceX < sourceWidth; sourceX++)
        {
            if (!IsBlack(source, sourceRowBytes, sourceX, sourceY)) continue;
            Map(sourceX, sourceY, sourceWidth, sourceHeight, rotation,
                out var targetX, out var targetY);
            expected[targetY * actualWidth + targetX] = true;
        }

        for (var targetY = 0; targetY < actualHeight; targetY++)
        for (var targetX = 0; targetX < actualWidth; targetX++)
            Assert.Equal(expected[targetY * actualWidth + targetX],
                IsBlack(actual, actualRowBytes, targetX, targetY));
    }

    private static void Map(int sourceX, int sourceY, int sourceWidth, int sourceHeight,
        int rotation, out int targetX, out int targetY)
    {
        switch (rotation)
        {
            case 90:
                targetX = sourceHeight - 1 - sourceY;
                targetY = sourceX;
                break;
            case 180:
                targetX = sourceWidth - 1 - sourceX;
                targetY = sourceHeight - 1 - sourceY;
                break;
            case 270:
                targetX = sourceY;
                targetY = sourceWidth - 1 - sourceX;
                break;
            default:
                targetX = sourceX;
                targetY = sourceY;
                break;
        }
    }

    private static bool IsQuarterTurn(int rotation)
        => rotation == 90 || rotation == 270;

    private static bool IsBlack(byte[] bytes, int rowBytes, int x, int y)
        => (bytes[y * rowBytes + x / 8] & (0x80 >> (x & 7))) != 0;

    private static byte[] ReadRasterPayload(byte[] bytes, out int rowBytes, out int height)
    {
        var marker = new byte[] { 0x1D, 0x76, 0x30 };
        var index = Find(bytes, marker);
        Assert.True(index >= 0, "ESC/POS 输出应包含 GS v 0 光栅命令。");
        Assert.True(bytes.Length >= index + 8, "ESC/POS 光栅头部不完整。");
        Assert.Equal(0, bytes[index + 3]);

        rowBytes = bytes[index + 4] | bytes[index + 5] << 8;
        height = bytes[index + 6] | bytes[index + 7] << 8;
        var length = checked(rowBytes * height);
        Assert.True(bytes.Length >= index + 8 + length, "ESC/POS 光栅数据不完整。");
        var payload = new byte[length];
        Buffer.BlockCopy(bytes, index + 8, payload, 0, length);
        return payload;
    }

    private static int Find(byte[] bytes, byte[] pattern)
    {
        for (var index = 0; index <= bytes.Length - pattern.Length; index++)
        {
            var matched = true;
            for (var offset = 0; offset < pattern.Length; offset++)
                if (bytes[index + offset] != pattern[offset])
                {
                    matched = false;
                    break;
                }
            if (matched) return index;
        }
        return -1;
    }
}
