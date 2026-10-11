using System;
using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosRasterPlanetTests
{
    [Theory]
    [InlineData("123456789014", "123456789014", 246)]
    [InlineData("12345-6789014", "123456789014", 246)]
    [InlineData("12345678901239", "12345678901239", 286)]
    [InlineData("1234567-8901239", "12345678901239", 286)]
    public void Planet_normalizes_hyphens_and_keeps_the_valid_check_digit(
        string input, string normalized, int width)
    {
        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Planet, input));
        Assert.Equal(width, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Planet, input, 2));
    }

    [Fact]
    public void Planet_raster_uses_inverse_full_and_half_height_bars_with_a_stable_gap()
    {
        const int width = 246;
        const int height = 40;
        const int moduleWidth = 2;
        var canvas = new EscPosRasterCanvas(width, height);

        canvas.DrawBarcode(BarcodeType.Planet, 0, 0, "012345678905", height, moduleWidth);

        var bytes = canvas.ToBytes();
        Assert.Equal((0, 0, width, height), Bounds(bytes, canvas.RowBytes, width, height));
        // PLANET is the POSTNET inverse: 3 full and 2 short bars per digit.
        Assert.Equal(76, BlackPixelsInRow(bytes, canvas.RowBytes, width, 0));
        Assert.Equal(124, BlackPixelsInRow(bytes, canvas.RowBytes, width, height - 1));
        Assert.False(IsBlack(bytes, canvas.RowBytes, 4, 0));
        Assert.True(IsBlack(bytes, canvas.RowBytes, 4, height - 1));
        Assert.True(IsBlack(bytes, canvas.RowBytes, 0, 0));
        Assert.True(IsBlack(bytes, canvas.RowBytes, width - 1, 0));
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_provider_renders_planet_in_all_four_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10, BarcodeType.Planet,
            "123456789014", 40, narrowWidth: 2, humanReadable: false, rotation: rotation), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Planet));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Planet));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Theory]
    [InlineData("----")]
    [InlineData("12345678901")]
    [InlineData("1234567890123")]
    [InlineData("123456789013")]
    [InlineData("1234567890145")]
    [InlineData("12345678901A4")]
    public void Raster_planet_rejects_invalid_input_with_element_path(string data)
    {
        var document = new PrintDocument();
        document.AddPage(600, 140).Add(new PrintBarcodeElement(0, 0, BarcodeType.Planet,
            data, 40, narrowWidth: 2, humanReadable: false), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("PLANET", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Planet_normalization_rejects_null_or_empty_data(string data)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Planet, data));
    }

    [Fact]
    public void Planet_measurement_failure_releases_working_budget_for_retry()
    {
        using (var scope = PrintResourceScope.Begin(16))
        {
            Assert.Throws<PrintResourceLimitException>(() =>
                EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Planet, "123456789014", 2));
            Assert.Equal(0, scope.CurrentBytes);
        }

        Assert.Equal(246, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Planet, "123456789014", 2));
    }

    [Fact]
    public void Raster_template_outputs_planet_using_the_same_measured_width()
    {
        var template = new BpTemplate
        {
            Width = 600,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Planet,
                            LineWidth = 1,
                            Height = 40,
                            HumanReadable = false,
                            Field = new BgField { Value = "12345-6789014" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII
            });

        Assert.Single(result.Pages);
        Assert.Equal(40, result.Pages[0].Height);
        Assert.True(result.Pages[0].Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    private static (int Left, int Top, int Width, int Height) Bounds(
        byte[] bytes, int rowBytes, int width, int height)
    {
        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (!IsBlack(bytes, rowBytes, x, y)) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }
        return right < left || bottom < top
            ? (0, 0, 0, 0)
            : (left, top, right - left + 1, bottom - top + 1);
    }

    private static int BlackPixelsInRow(byte[] bytes, int rowBytes, int width, int row)
    {
        var count = 0;
        for (var x = 0; x < width; x++)
            if (IsBlack(bytes, rowBytes, x, row)) count++;
        return count;
    }

    private static bool IsBlack(byte[] bytes, int rowBytes, int x, int y)
        => (bytes[y * rowBytes + x / 8] & (0x80 >> (x & 7))) != 0;
}
