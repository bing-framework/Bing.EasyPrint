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

public sealed class EscPosRasterGs1DataBarTests
{
    [Fact]
    public void Stacked_pattern_matches_the_two_row_golden_symbol()
    {
        var rows = EscPosRasterCanvas.EncodeGs1DataBarStackedPattern("1234567890123");

        Assert.Equal(new[]
        {
            "01011101001000000100111000000001010000101111101010",
            "00001010100110111010000101011110101101010000110000",
            "10110100011001100101111111110001011011000111000101"
        }, rows);
    }

    [Fact]
    public void Stacked_requires_a_13_module_total_height_and_uses_a_50_module_width()
    {
        Assert.Equal(50, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarStacked, "1234567890123", 1));
        Assert.Equal(26, EscPosRasterCanvas.MeasureBarcodeHeight(
            BarcodeType.Gs1DataBarStacked, "1234567890123", 26, 2, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => EscPosRasterCanvas.MeasureBarcodeHeight(
            BarcodeType.Gs1DataBarStacked, "1234567890123", 25, 2, default));
    }

    [Fact]
    public void Stacked_rejects_invalid_payloads_before_retaining_matrix_budget()
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);

        Assert.Throws<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarStacked, "123456789012", 2));
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Fact]
    public void Stacked_raster_command_emits_the_raster_image_and_is_raster_only()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(50, 13)
            .RasterBarcode(BarcodeType.Gs1DataBarStacked, 0, 0, "1234567890123", 13, 1)
            .EndRasterPage();

        var bytes = command.Build().GetBytes();
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);
        var native = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.True(raster.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarStacked));
        Assert.True(raster.Capabilities.SupportsCommandBarcodeType(BarcodeType.Gs1DataBarStacked));
        Assert.False(native.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarStacked));
        Assert.False(tspl.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarStacked));
        Assert.False(zpl.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarStacked));
        Assert.False(cpcl.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarStacked));
    }

    [Fact]
    public void Omnidirectional_pattern_matches_the_96_module_golden_symbol()
    {
        const string data = "1234567890123";
        const string expected =
            "0101110100100000010011100000000101000010111110101101000110011001" +
            "01111111110001011011000111000101";

        var pattern = EscPosRasterCanvas.EncodeGs1DataBarPattern(data);

        Assert.Equal(96, pattern.Length);
        Assert.Equal(expected, pattern);
        Assert.Equal("(01)12345678901231",
            EscPosRasterCanvas.HumanReadableBarcodeData(BarcodeType.Gs1DataBarOmnidirectional, data));
        Assert.Equal(96, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarOmnidirectional, data, 1));
        Assert.Equal(192, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarTruncated, data, 2));
    }

    [Theory]
    [InlineData("0000000000000")]
    [InlineData("9999999999999")]
    [InlineData("2718281828459")]
    public void Both_linear_variants_keep_a_fixed_96_module_symbol(string data)
    {
        Assert.Equal(96, EscPosRasterCanvas.EncodeGs1DataBarPattern(data).Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456789012")]
    [InlineData("12345678901234")]
    [InlineData("123456789012A")]
    public void Data_bar_rejects_non_payloads_without_working_budget_pollution(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarOmnidirectional, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Raster_command_writes_both_data_bar_variants_through_the_shared_matrix()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(500, 120)
            .RasterBarcode(BarcodeType.Gs1DataBarOmnidirectional, 0, 0, "1234567890123", 40, 2)
            .RasterBarcode(BarcodeType.Gs1DataBarTruncated, 0, 50, "1234567890123", 40, 2)
            .EndRasterPage();

        var bytes = command.Build().GetBytes();

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_provider_renders_omnidirectional_data_bar_in_all_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.Gs1DataBarOmnidirectional, "1234567890123", 40,
            narrowWidth: 2, humanReadable: false, rotation: rotation), "DataBar");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarOmnidirectional));
        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarTruncated));
        Assert.True(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Gs1DataBarOmnidirectional));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_uses_data_bar_host_matrix_and_measured_width()
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
                            BarcodeType = BarcodeType.Gs1DataBarTruncated,
                            NarrowModuleWidth = 2,
                            Height = 40,
                            HumanReadable = false,
                            Field = new BgField { Value = "1234567890123" }
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

    [Fact]
    public void Raster_template_renders_stacked_data_bar_with_its_two_row_height()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 220,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarStacked,
                            NarrowModuleWidth = 2,
                            Height = 26,
                            HumanReadable = false,
                            Field = new BgField { Value = "1234567890123" }
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
        Assert.Equal(26, page.Height);
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }
}
