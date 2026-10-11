using System;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosQuietZoneCanvasTests
{
    [Fact]
    public void Barcode_quiet_zone_reserves_all_four_edges_and_rejects_black_ink()
    {
        const int x = 3;
        const int y = 3;
        const int height = 8;
        const int moduleWidth = 2;
        var symbolWidth = EscPosRasterCanvas.MeasureBarcodeWidth("12345", moduleWidth);
        var quiet = new PrintQuietZone(left: 1, top: 2, right: 2, bottom: 1);
        var canvas = new EscPosRasterCanvas(x + symbolWidth + quiet.Right + 4,
            y + height + quiet.Bottom + 4);

        canvas.DrawBarcode(x, y, "12345", height, quiet, moduleWidth);

        var bytes = canvas.ToBytes();
        Assert.True(HasBlackPixel(bytes, canvas.RowBytes, x, y, symbolWidth, height));
        for (var row = y - quiet.Top; row < y + height + quiet.Bottom; row++)
            for (var column = x - quiet.Left; column < x + symbolWidth + quiet.Right; column++)
                if (column < x || column >= x + symbolWidth || row < y || row >= y + height)
                    Assert.False(IsBlack(bytes, canvas.RowBytes, column, row));

        var beforeCollision = canvas.ToBytes();
        Assert.Throws<InvalidOperationException>(() => canvas.SetPixel(x - 1, y, true));
        Assert.Equal(beforeCollision, canvas.ToBytes());

        canvas.SetPixel(x - 1, y, false);
        Assert.Equal(beforeCollision, canvas.ToBytes());
    }

    [Fact]
    public void Quiet_zone_registration_rejects_existing_black_pixels_without_mutation()
    {
        const int x = 4;
        const int y = 4;
        const int height = 8;
        var symbolWidth = EscPosRasterCanvas.MeasureBarcodeWidth("123", 2);
        var quiet = new PrintQuietZone(left: 1);
        var canvas = new EscPosRasterCanvas(x + symbolWidth + 5, y + height + 5);
        canvas.SetPixel(x - 1, y, true);
        var before = canvas.ToBytes();

        Assert.Throws<InvalidOperationException>(() =>
            canvas.DrawBarcode(x, y, "123", height, quiet, 2));

        Assert.Equal(before, canvas.ToBytes());
    }

    [Fact]
    public void Drawing_into_a_registered_quiet_zone_rolls_back_partial_pixels()
    {
        const int x = 4;
        const int y = 4;
        const int height = 8;
        var symbolWidth = EscPosRasterCanvas.MeasureBarcodeWidth("123", 2);
        var canvas = new EscPosRasterCanvas(x + symbolWidth + 5, y + height + 5);
        canvas.DrawBarcode(x, y, "123", height, new PrintQuietZone(left: 1), 2);
        var before = canvas.ToBytes();

        Assert.Throws<InvalidOperationException>(() => canvas.DrawLine(x - 2, y, x + 1, y));

        Assert.Equal(before, canvas.ToBytes());
    }

    [Fact]
    public void Explicit_zero_quiet_zone_preserves_legacy_barcode_and_qr_bytes()
    {
        var barcodeCanvasWidth = EscPosRasterCanvas.MeasureBarcodeWidth("12345", 2) + 12;
        var legacyBarcode = new EscPosRasterCanvas(barcodeCanvasWidth, 80);
        var explicitZeroBarcode = new EscPosRasterCanvas(barcodeCanvasWidth, 80);
        legacyBarcode.DrawBarcode(4, 5, "12345", 24, 2);
        explicitZeroBarcode.DrawBarcode(4, 5, "12345", 24, PrintQuietZone.Uniform(0), 2);
        Assert.Equal(legacyBarcode.ToBytes(), explicitZeroBarcode.ToBytes());

        var legacyQr = new EscPosRasterCanvas(160, 160);
        var explicitZeroQr = new EscPosRasterCanvas(160, 160);
        legacyQr.DrawQrCode(5, 6, "quiet", 2, QrCodeCorrectionLevel.M);
        explicitZeroQr.DrawQrCode(5, 6, "quiet", 2, QrCodeCorrectionLevel.M,
            PrintQuietZone.Uniform(0));
        Assert.Equal(legacyQr.ToBytes(), explicitZeroQr.ToBytes());
    }

    [Fact]
    public void Quiet_zone_at_the_canvas_edge_is_rejected_before_registration()
    {
        var symbolWidth = EscPosRasterCanvas.MeasureBarcodeWidth("123", 2);
        var canvas = new EscPosRasterCanvas(symbolWidth + 2, 16);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            canvas.DrawBarcode(0, 0, "123", 8, new PrintQuietZone(left: 1, top: 1), 2));

        Assert.Equal(new byte[canvas.RowBytes * canvas.Height], canvas.ToBytes());
    }

    [Fact]
    public void Qr_quiet_zone_reserves_four_edges_when_the_envelope_touches_the_canvas()
    {
        const int moduleSize = 2;
        var symbolSize = EscPosRasterCanvas.MeasureQrCodeSize("quiet", moduleSize,
            QrCodeCorrectionLevel.M);
        var quiet = new PrintQuietZone(left: 1, top: 2, right: 2, bottom: 1);
        var canvas = new EscPosRasterCanvas(symbolSize + quiet.Left + quiet.Right,
            symbolSize + quiet.Top + quiet.Bottom);

        canvas.DrawQrCode(quiet.Left, quiet.Top, "quiet", moduleSize, QrCodeCorrectionLevel.M, quiet);

        var bytes = canvas.ToBytes();
        Assert.True(HasBlackPixel(bytes, canvas.RowBytes, quiet.Left, quiet.Top, symbolSize, symbolSize));
        AssertQuietZoneBlank(bytes, canvas.RowBytes, quiet.Left, quiet.Top, symbolSize, symbolSize, quiet);

        var rejected = new EscPosRasterCanvas(symbolSize + quiet.Right + 2,
            symbolSize + quiet.Top + quiet.Bottom + 2);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            rejected.DrawQrCode(0, quiet.Top, "quiet", moduleSize, QrCodeCorrectionLevel.M, quiet));
        Assert.Equal(new byte[rejected.RowBytes * rejected.Height], rejected.ToBytes());
    }

    [Fact]
    public void New_symbol_body_collision_rolls_back_its_new_quiet_zone()
    {
        const int firstY = 10;
        const int height = 12;
        const int moduleWidth = 2;
        var firstWidth = EscPosRasterCanvas.MeasureBarcodeWidth("111", moduleWidth);
        var secondWidth = EscPosRasterCanvas.MeasureBarcodeWidth("222", moduleWidth);
        var firstX = 4;
        var secondX = firstX + firstWidth;
        var canvas = new EscPosRasterCanvas(secondX + secondWidth + 8, 48);
        canvas.DrawBarcode(firstX, firstY, "111", height, new PrintQuietZone(right: 1), moduleWidth);
        var before = canvas.ToBytes();

        Assert.Throws<InvalidOperationException>(() =>
            canvas.DrawBarcode(secondX, firstY, "222", height,
                new PrintQuietZone(top: 1, right: 1, bottom: 1), moduleWidth));

        Assert.Equal(before, canvas.ToBytes());
        // This pixel was in the failed symbol's top quiet band. It must remain
        // writable after the failed registration is rolled back.
        canvas.SetPixel(secondX, firstY - 1, true);
        Assert.True(IsBlack(canvas.ToBytes(), canvas.RowBytes, secondX, firstY - 1));
    }

    [Theory]
    [InlineData("rectangle")]
    [InlineData("raster")]
    [InlineData("text")]
    public void Protected_drawings_restore_bytes_after_a_mid_operation_collision(string operation)
    {
        const int protectedY = 20;
        const int canvasHeight = 80;
        var catalog = FixedTestFonts.Catalog();
        var canvas = new EscPosRasterCanvas(180, canvasHeight, catalog);
        var operationX = 100;
        string textFamily = null;
        var textX = -1;
        if (operation == "text")
        {
            textFamily = EscPosRasterCanvas.ResolveFontFamilyName("A", null, false, catalog);
            var probe = new EscPosRasterCanvas(180, canvasHeight, catalog);
            probe.DrawText(80, protectedY, "A", 24, fontFamily: textFamily);
            textX = MaxBlackX(probe.ToBytes(), probe.RowBytes, 0, 0, probe.Width, probe.Height);
            operationX = textX;
        }
        var anchorWidth = EscPosRasterCanvas.MeasureBarcodeWidth("1", 2);
        var anchorX = operationX - anchorWidth;
        canvas.DrawBarcode(anchorX, 0, "1", canvasHeight,
            new PrintQuietZone(right: 1), 2);

        switch (operation)
        {
            case "rectangle":
                var rectangleBefore = canvas.ToBytes();
                Assert.Throws<InvalidOperationException>(() =>
                    canvas.DrawRectangle(operationX - 4, protectedY, 8, 4, 1, filled: false));
                Assert.Equal(rectangleBefore, canvas.ToBytes());
                break;
            case "raster":
                var rasterBefore = canvas.ToBytes();
                Assert.Throws<InvalidOperationException>(() =>
                    canvas.DrawRaster(operationX - 4, protectedY, 10, 1, new byte[] { 0xF8, 0x00 }));
                Assert.Equal(rasterBefore, canvas.ToBytes());
                break;
            case "text":
                var textBefore = canvas.ToBytes();
                Assert.True(textX > 80);
                Assert.Throws<InvalidOperationException>(() =>
                    canvas.DrawText(80, protectedY, "A", 24, fontFamily: textFamily));
                Assert.Equal(textBefore, canvas.ToBytes());
                Assert.False(IsBlack(canvas.ToBytes(), canvas.RowBytes, textX, protectedY));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    [Fact]
    public void Registering_a_quiet_zone_after_black_ink_is_strictly_rejected()
    {
        const int x = 5;
        const int y = 5;
        const int height = 10;
        var width = EscPosRasterCanvas.MeasureBarcodeWidth("123", 2);
        var canvas = new EscPosRasterCanvas(x + width + 5, 32);
        canvas.DrawBarcode(x, y, "123", height, 2);
        canvas.SetPixel(x - 1, y, true);
        var before = canvas.ToBytes();

        Assert.Throws<InvalidOperationException>(() =>
            canvas.DrawBarcode(x, y, "123", height, new PrintQuietZone(left: 1), 2));

        Assert.Equal(before, canvas.ToBytes());
    }

    [Fact]
    public void Barcode_and_qr_use_their_own_module_dimensions_in_output()
    {
        const int x = 3;
        const int y = 3;
        const int barcodeHeight = 10;
        const int barcodeModuleWidth = 3;
        const int qrModuleSize = 2;
        var barcodeWidth = EscPosRasterCanvas.MeasureBarcodeWidth("1234", barcodeModuleWidth);
        var qrSize = EscPosRasterCanvas.MeasureQrCodeSize("quiet", qrModuleSize, QrCodeCorrectionLevel.M);

        var barcode = new EscPosRasterCanvas(x + barcodeWidth + 4, y + barcodeHeight + 4);
        barcode.DrawBarcode(x, y, "1234", barcodeHeight, barcodeModuleWidth);
        Assert.Equal(x + barcodeWidth - 1, MaxBlackX(barcode.ToBytes(), barcode.RowBytes,
            x, y, barcodeWidth, barcodeHeight));

        var qr = new EscPosRasterCanvas(x + qrSize + 4, y + qrSize + 4);
        qr.DrawQrCode(x, y, "quiet", qrModuleSize, QrCodeCorrectionLevel.M);
        Assert.Equal(x + qrSize - 1, MaxBlackX(qr.ToBytes(), qr.RowBytes, x, y, qrSize, qrSize));
        Assert.NotEqual(barcodeWidth, qrSize);
    }

    private static bool HasBlackPixel(byte[] bytes, int rowBytes, int x, int y, int width, int height)
    {
        for (var row = y; row < y + height; row++)
            for (var column = x; column < x + width; column++)
                if (IsBlack(bytes, rowBytes, column, row)) return true;
        return false;
    }

    private static int MaxBlackX(byte[] bytes, int rowBytes, int x, int y, int width, int height)
    {
        var maximum = -1;
        for (var row = y; row < y + height; row++)
            for (var column = x; column < x + width; column++)
                if (IsBlack(bytes, rowBytes, column, row)) maximum = Math.Max(maximum, column);
        return maximum;
    }

    private static bool IsBlack(byte[] bytes, int rowBytes, int x, int y)
    {
        return (bytes[y * rowBytes + x / 8] & (0x80 >> (x & 7))) != 0;
    }

    private static void AssertQuietZoneBlank(byte[] bytes, int rowBytes, int x, int y,
        int width, int height, PrintQuietZone quiet)
    {
        for (var row = y - quiet.Top; row < y + height + quiet.Bottom; row++)
            for (var column = x - quiet.Left; column < x + width + quiet.Right; column++)
                if (column < x || column >= x + width || row < y || row >= y + height)
                    Assert.False(IsBlack(bytes, rowBytes, column, row));
    }
}
