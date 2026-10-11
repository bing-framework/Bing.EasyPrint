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

public sealed class EscPosRasterPostnetTests
{
    [Theory]
    [InlineData("12345", "12345", 126)]
    [InlineData("1234-56789", "123456789", 206)]
    [InlineData("12345-67890-1", "12345678901", 246)]
    public void Postnet_normalizes_hyphens_and_appends_the_host_check_digit_for_encoding(
        string input, string normalized, int width)
    {
        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Postnet, input));
        Assert.Equal(width, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Postnet, input, 2));
    }

    [Fact]
    public void Postnet_raster_uses_full_and_half_height_bars_with_a_stable_gap()
    {
        const int width = 126;
        const int height = 40;
        const int moduleWidth = 2;
        var canvas = new EscPosRasterCanvas(width, height);

        canvas.DrawBarcode(BarcodeType.Postnet, 0, 0, "12345", height, moduleWidth);

        var bytes = canvas.ToBytes();
        Assert.Equal((0, 0, width, height), Bounds(bytes, canvas.RowBytes, width, height));
        Assert.Equal(28, BlackPixelsInRow(bytes, canvas.RowBytes, width, 0));
        Assert.Equal(64, BlackPixelsInRow(bytes, canvas.RowBytes, width, height - 1));
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
    public void Raster_provider_renders_postnet_in_all_four_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10, BarcodeType.Postnet,
            "1234-5", 40, narrowWidth: 2, humanReadable: false, rotation: rotation), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Postnet));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("1234A")]
    [InlineData("1234 5")]
    public void Raster_postnet_rejects_invalid_input_with_element_path(string data)
    {
        var document = new PrintDocument();
        document.AddPage(600, 140).Add(new PrintBarcodeElement(0, 0, BarcodeType.Postnet,
            data, 40, narrowWidth: 2, humanReadable: false), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("POSTNET", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Postnet_normalization_rejects_null_or_empty_data(string data)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Postnet, data));
    }

    [Fact]
    public void Postnet_measurement_failure_releases_working_budget_for_retry()
    {
        using (var scope = PrintResourceScope.Begin(16))
        {
            Assert.Throws<PrintResourceLimitException>(() =>
                EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Postnet, "12345", 2));
            Assert.Equal(0, scope.CurrentBytes);
        }

        Assert.Equal(126, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Postnet, "12345", 2));
    }

    [Fact]
    public void Raster_template_outputs_postnet_using_the_same_measured_width()
    {
        var template = new BpTemplate
        {
            Width = 240,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Postnet,
                            LineWidth = 1,
                            Height = 40,
                            HumanReadable = false,
                            Field = new BgField { Value = "1234-5" }
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
