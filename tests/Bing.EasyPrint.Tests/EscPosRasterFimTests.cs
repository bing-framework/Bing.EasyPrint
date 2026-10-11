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

public sealed class EscPosRasterFimTests
{
    [Theory]
    [InlineData("A", "110010011", 34)]
    [InlineData("B", "101101101", 34)]
    [InlineData("C", "110101011", 34)]
    [InlineData("D", "111010111", 34)]
    [InlineData("E", "101000101", 34)]
    public void Fim_normalizes_the_supported_usps_pattern_and_measures_nine_slots(
        string data, string pattern, int width)
    {
        Assert.Equal(data, EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Fim, data));
        Assert.Equal(width, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Fim, data, 2));

        var canvas = new EscPosRasterCanvas(width, 20);
        canvas.DrawBarcode(BarcodeType.Fim, 0, 0, data, 20, 2);
        var bytes = canvas.ToBytes();
        for (var index = 0; index < pattern.Length; index++)
        {
            var barX = index * 4 + 1;
            Assert.Equal(pattern[index] == '1', IsBlack(bytes, canvas.RowBytes, barX, 0));
            if (index + 1 < pattern.Length)
                Assert.False(IsBlack(bytes, canvas.RowBytes, barX + 2, 0));
        }
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    [InlineData("D")]
    [InlineData("E")]
    public void Raster_provider_renders_fim_and_exposes_host_only_capability(string data)
    {
        var document = new PrintDocument();
        document.AddPage(600, 140).Add(new PrintBarcodeElement(10, 10, BarcodeType.Fim,
            data, 40, narrowWidth: 2, humanReadable: false), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Fim));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Fim));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Theory]
    [InlineData("F")]
    [InlineData("a")]
    [InlineData("AB")]
    public void Raster_fim_rejects_unsupported_modes_with_element_path(string data)
    {
        var document = new PrintDocument();
        document.AddPage(600, 140).Add(new PrintBarcodeElement(0, 0, BarcodeType.Fim,
            data, 40, narrowWidth: 2, humanReadable: false), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("FIM", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Fim_normalization_rejects_null_or_empty_data(string data)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Fim, data));
    }

    [Fact]
    public void Fim_measurement_failure_releases_working_budget_for_retry()
    {
        using (var scope = PrintResourceScope.Begin(16))
        {
            Assert.Throws<PrintResourceLimitException>(() =>
                EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Fim, "A", 2));
            Assert.Equal(0, scope.CurrentBytes);
        }

        Assert.Equal(34, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Fim, "A", 2));
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_provider_renders_fim_in_all_four_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10, BarcodeType.Fim,
            "C", 40, narrowWidth: 2, humanReadable: false, rotation: rotation), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        provider.Validate(document);
        Assert.NotEmpty(provider.Render(document)[0]);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("D")]
    [InlineData("E")]
    public void Raster_template_outputs_fim_with_the_measured_quiet_zone(string data)
    {
        var template = new BpTemplate
        {
            Width = 74,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Fim,
                            LineWidth = 1,
                            Height = 30,
                            HumanReadable = false,
                            Field = new BgField { Value = data }
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

        var page = Assert.Single(result.Pages);
        Assert.Equal(30, page.Height);
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    private static bool IsBlack(byte[] bytes, int rowBytes, int x, int y)
        => (bytes[y * rowBytes + x / 8] & (0x80 >> (x & 7))) != 0;
}
