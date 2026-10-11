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

/// <summary>验证 ESC/POS Raster 的 Code 11 主机编码。</summary>
public sealed class EscPosRasterCode11Tests
{
    [Fact]
    public void Code11_short_payload_uses_one_c_check_digit_and_known_module_pattern()
    {
        const string data = "12345";
        const string encodedSymbols = "123452";

        Assert.Equal(data, EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Code11, data));
        Assert.Equal(ExpectedPattern(encodedSymbols), EscPosRasterCanvas.EncodeCode11Pattern(data));
        Assert.Equal(ExpectedPattern(encodedSymbols).Length,
            EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code11, data, 1));
        Assert.Equal(ExpectedPattern(encodedSymbols).Length * 2,
            EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code11, data, 2));
    }

    [Fact]
    public void Code11_long_payload_uses_c_and_k_check_digits()
    {
        const string data = "12345678901";
        const string encodedSymbols = "123456789014-";

        Assert.Equal(ExpectedPattern(encodedSymbols), EscPosRasterCanvas.EncodeCode11Pattern(data));
        Assert.Equal(ExpectedPattern(encodedSymbols).Length,
            EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code11, data, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123A")]
    [InlineData("12_34")]
    public void Code11_rejects_invalid_payload_without_consuming_working_budget(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Code11, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Code11_rejects_payload_longer_than_the_host_limit_and_can_retry()
    {
        var tooLong = new string('1', 256);

        Assert.Throws<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Code11, tooLong, 2));
        Assert.True(EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code11, "12345", 2) > 0);
    }

    [Fact]
    public void Raster_command_writes_code11_through_the_shared_host_matrix()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(320, 60)
            .RasterBarcode(BarcodeType.Code11, 0, 0, "12345", 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_provider_renders_code11_in_all_four_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10, BarcodeType.Code11,
            "12345", 40, narrowWidth: 2, humanReadable: false, rotation: rotation), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code11));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_uses_code11_host_matrix_and_measured_width()
    {
        var template = new BpTemplate
        {
            Version = 3,
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
                            BarcodeType = BarcodeType.Code11,
                            NarrowModuleWidth = 2,
                            Height = 40,
                            HumanReadable = false,
                            Field = new BgField { Value = "12345" }
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
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    private static string ExpectedPattern(string encodedSymbols)
    {
        var patterns = new Dictionary<char, string>
        {
            ['0'] = "101011", ['1'] = "1101011", ['2'] = "1001011", ['3'] = "1100101",
            ['4'] = "1011011", ['5'] = "1101101", ['6'] = "1001101", ['7'] = "1010011",
            ['8'] = "1101001", ['9'] = "110101", ['-'] = "101101", ['*'] = "1011001"
        };
        var output = new StringBuilder();
        output.Append(patterns['*']);
        output.Append('0');
        for (var index = 0; index < encodedSymbols.Length; index++)
        {
            if (index > 0) output.Append('0');
            output.Append(patterns[encodedSymbols[index]]);
        }
        output.Append('0');
        output.Append(patterns['*']);
        return output.ToString();
    }
}
