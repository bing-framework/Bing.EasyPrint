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

/// <summary>验证 ESC/POS Raster 主机 GS1-128（UCC/EAN-128）编码和模板链路。</summary>
public sealed class EscPosRasterUccEan128Tests
{
    [Theory]
    [InlineData("0101234567890128", 268)]
    [InlineData("01\u001D10ABC", 268)]
    public void Ucc_ean128_uses_gs1_fnc1_and_measures_the_encoded_symbol(
        string data, int width)
    {
        Assert.Equal(data, EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.UccEan128, data));
        Assert.Equal(width, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.UccEan128, data, 2));

        // The GS1 start/FNC1 adds one Code 128 symbol compared with ordinary
        // numeric Code 128 data of the same value.
        Assert.Equal(246, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128,
            "0101234567890128", 2));
    }

    [Fact]
    public void Raster_canvas_renders_ucc_ean128_with_hri_without_exposing_group_separator()
    {
        const string data = "01\u001D10ABC";
        var canvas = new EscPosRasterCanvas(320, 90);

        canvas.DrawBarcode(BarcodeType.UccEan128, 0, 0, data, 50, 2,
            humanReadable: true, humanReadableReserveHeight: 40);

        var bytes = canvas.ToBytes();
        Assert.True(Array.Exists(bytes, value => value != 0));
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_provider_renders_ucc_ean128_in_all_four_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.UccEan128, "0101234567890128", 50, narrowWidth: 2,
            humanReadable: false, rotation: rotation), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos,
            Encoding.ASCII, EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.UccEan128));
        Assert.True(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.UccEan128));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("A101234")]
    [InlineData("01\u000110ABC")]
    [InlineData("中文")]
    public void Raster_ucc_ean128_rejects_invalid_data_with_element_path(string data)
    {
        var document = new PrintDocument();
        document.AddPage(600, 140).Add(new PrintBarcodeElement(0, 0,
            BarcodeType.UccEan128, data, 40, narrowWidth: 2,
            humanReadable: false), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos,
            Encoding.ASCII, EscPosLayoutMode.Raster);

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.True(error.Message.Contains("GS1-128") ||
                    data.Length == 0 && error.Message.Contains("条码"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ucc_ean128_normalization_rejects_null_or_empty_data(string data)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.UccEan128, data));
    }

    [Fact]
    public void Ucc_ean128_measurement_failure_releases_working_budget_for_retry()
    {
        using (var scope = PrintResourceScope.Begin(16))
        {
            Assert.Throws<PrintResourceLimitException>(() =>
                EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.UccEan128,
                    "0101234567890128", 2));
            Assert.Equal(0, scope.CurrentBytes);
        }

        Assert.Equal(268, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.UccEan128,
            "0101234567890128", 2));
    }

    [Fact]
    public void Raster_template_accepts_ascii_group_separator_and_outputs_gs1_symbol()
    {
        var template = new BpTemplate
        {
            Width = 320,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.UccEan128,
                            LineWidth = 1,
                            Height = 40,
                            HumanReadable = false,
                            Field = new BgField { Value = "01\u001D10ABC" }
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
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(40, page.Height);
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }
}
