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

/// <summary>验证 ESC/POS Raster 的 USPS Intelligent Mail 四状态主机编码。</summary>
public sealed class EscPosRasterIntelligentMailTests
{
    [Theory]
    [InlineData("01234567094987654321",
        "ATTFATTDTTADTAATTDTDTATTDAFDDFADFDFTFFFFFTATFAAAATDFFTDAADFTFDTDT")]
    [InlineData("0123456709498765432101234",
        "DTTAFADDTTFTDTFTFDTDDADADAFADFATDDFTAAAFDTTADFAAATDFDTDFADDDTDFFT")]
    [InlineData("01234567094987654321012345678",
        "ADFTTAFDTTTTFATTADTAAATFTFTATDAAAFDDADATATDTDTTDFDTDATADADTDFFTFA")]
    [InlineData("0123456709498765432101234567891",
        "AADTFFDFTDADTAADAATFDTDDAAADDTDTTDAFADADDDTFFFDDTTTADFAAADFTDAADA")]
    public void Encoder_matches_the_four_usps_b3200_reference_patterns(string data, string expected)
    {
        Assert.Equal(expected, EscPosRasterCanvas.EncodeIntelligentMailPattern(data));
        Assert.Equal(65, expected.Length);
    }

    [Theory]
    [InlineData("01234-56709-49876-54321", "01234567094987654321")]
    [InlineData("01234567094987654321-01234", "0123456709498765432101234")]
    public void Raster_normalizes_intelligent_mail_hyphens_and_measures_65_bars(
        string input, string normalized)
    {
        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.IntelligentMail, input));
        Assert.Equal(258, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.IntelligentMail, input, 2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890123456789")]
    [InlineData("123456789012345678901234567890")]
    [InlineData("12345678901234567890A")]
    public void Raster_rejects_invalid_intelligent_mail_without_consuming_budget(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.IntelligentMail, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Raster_draws_four_state_bars_with_pitch_and_hri()
    {
        const string data = "0123456709498765432101234567891";
        var canvas = new EscPosRasterCanvas(320, 90);

        canvas.DrawBarcode(BarcodeType.IntelligentMail, 0, 0, data, 40, 2,
            humanReadable: true, humanReadableReserveHeight: 40);

        var bytes = canvas.ToBytes();
        Assert.NotEmpty(bytes);
        Assert.Equal(258, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.IntelligentMail, data, 2));
        Assert.True(BlackPixelsInRow(bytes, canvas.RowBytes, canvas.Width, 0) > 0);
        Assert.True(BlackPixelsInRow(bytes, canvas.RowBytes, canvas.Width, 20) >
                    BlackPixelsInRow(bytes, canvas.RowBytes, canvas.Width, 0));
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_provider_renders_intelligent_mail_in_all_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.IntelligentMail, "0123456709498765432101234567891", 40,
            narrowWidth: 2, humanReadable: false, rotation: rotation), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos,
            Encoding.ASCII, EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.IntelligentMail));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_renders_intelligent_mail_with_normalized_layout_width()
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
                            BarcodeType = BarcodeType.IntelligentMail,
                            LineWidth = 1,
                            Height = 40,
                            HumanReadable = false,
                            Field = new BgField { Value = "01234567094987654321-01234" }
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

    [Fact]
    public void Raster_intelligent_mail_measurement_failure_releases_working_budget_for_retry()
    {
        using (var scope = PrintResourceScope.Begin(16))
        {
            Assert.Throws<PrintResourceLimitException>(() =>
                EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.IntelligentMail,
                    "01234567094987654321", 2));
            Assert.Equal(0, scope.CurrentBytes);
        }

        Assert.Equal(258, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.IntelligentMail, "01234567094987654321", 2));
    }

    private static int BlackPixelsInRow(byte[] bytes, int rowBytes, int width, int row)
    {
        var count = 0;
        for (var x = 0; x < width; x++)
            if ((bytes[row * rowBytes + x / 8] & (0x80 >> (x & 7))) != 0) count++;
        return count;
    }
}
