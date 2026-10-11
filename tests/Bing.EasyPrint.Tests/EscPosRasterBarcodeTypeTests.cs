using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosRasterBarcodeTypeTests
{
    [Theory]
    [InlineData(BarcodeType.Code39, "ABC-123")]
    [InlineData(BarcodeType.Code93, "ABC123")]
    [InlineData(BarcodeType.Ean8, "12345670")]
    [InlineData(BarcodeType.Ean13, "1234567890128")]
    [InlineData(BarcodeType.UpcA, "036000291452")]
    [InlineData(BarcodeType.UpcE, "04252614")]
    [InlineData(BarcodeType.Codabar, "A12345B")]
    [InlineData(BarcodeType.I2OF5, "123456")]
    [InlineData(BarcodeType.I2OF5C, "1234")]
    [InlineData(BarcodeType.I2OF5G, "12345678901")]
    [InlineData(BarcodeType.Msi, "12345")]
    [InlineData(BarcodeType.Plessey, "12345")]
    [InlineData(BarcodeType.UccEan128, "0101234567890128")]
    [InlineData(BarcodeType.Fim, "A")]
    [InlineData(BarcodeType.Planet, "123456789014")]
    [InlineData(BarcodeType.IntelligentMail, "01234567094987654321")]
    [InlineData(BarcodeType.Itf14, "1234567890123")]
    [InlineData(BarcodeType.Code128Auto, "ABC123")]
    public void Raster_provider_renders_supported_barcode_types(BarcodeType type, string data)
    {
        var document = new PrintDocument();
        document.AddPage(2400, 140).Add(new PrintBarcodeElement(0, 0, type, data, 40,
            narrowWidth: 2, humanReadable: false), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(type));
        provider.Validate(document);
        var bytes = provider.Render(document)[0];

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.True(EscPosRasterCanvas.MeasureBarcodeWidth(type, data, 2) > 0);
    }

    [Fact]
    public void Raster_code128_auto_uses_the_same_host_matrix_as_code128()
    {
        var autoWidth = EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128Auto, "ABC123", 2);
        var code128Width = EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128, "ABC123", 2);

        Assert.Equal(code128Width, autoWidth);
        Assert.Equal(RenderBarcode(BarcodeType.Code128, "ABC123"),
            RenderBarcode(BarcodeType.Code128Auto, "ABC123"));
    }

    [Fact]
    public void Raster_plessey_uses_the_host_writer_and_scales_by_module_width()
    {
        var singleModuleWidth = EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Plessey, "12345", 1);
        var doubleModuleWidth = EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Plessey, "12345", 2);

        Assert.True(singleModuleWidth > 0);
        Assert.Equal(singleModuleWidth * 2, doubleModuleWidth);

        var output = RenderBarcode(BarcodeType.Plessey, "12345");
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(output, value => value != 0);
    }

    [Fact]
    public void Raster_command_and_template_share_the_plessey_host_path()
    {
        var commandBytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(2400, 40)
            .RasterBarcode(BarcodeType.Plessey, 0, 0, "12345", 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();

        Assert.True(commandBytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);

        var template = new BpTemplate
        {
            Version = 3,
            Width = 2400,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 2400,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Plessey,
                                    NarrowModuleWidth = 2,
                                    Height = 40,
                                    HumanReadable = false,
                                    Field = new BgField { Value = "12345" }
                                }
                            }
                        }
                    }
                }
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
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123A")]
    public void Raster_plessey_rejects_non_numeric_data_before_matrix_mutation(string data)
    {
        Assert.Throws<ArgumentException>(() =>
            EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Plessey, data, 2));
    }

    [Fact]
    public void Raster_itf14_generates_the_check_digit_and_reuses_itf_matrix()
    {
        const string payload = "1234567890123";
        const string normalized = "12345678901231";

        Assert.Equal(
            EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.I2OF5, normalized, 2),
            EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Itf14, payload, 2));
        Assert.Equal(RenderBarcode(BarcodeType.I2OF5, normalized),
            RenderBarcode(BarcodeType.Itf14, payload));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789012")]
    [InlineData("12345678901234")]
    [InlineData("123456789012A")]
    public void Raster_itf14_rejects_invalid_payload_without_throwing_a_partial_matrix(string data)
    {
        Assert.Throws<ArgumentException>(() =>
            EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Itf14, data, 2));
    }

    [Fact]
    public void Raster_template_renders_code128_auto_with_the_host_bitmap_path()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 600,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 600,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Code128Auto,
                                    LineWidth = 1,
                                    Height = 40,
                                    Field = new BgField { Value = "ABC123" }
                                }
                            }
                        }
                    }
                }
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
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_renders_itf14_with_the_host_bitmap_path()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 600,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 600,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Itf14,
                                    LineWidth = 1,
                                    Height = 40,
                                    Field = new BgField { Value = "1234567890123" }
                                }
                            }
                        }
                    }
                }
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
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A\u0080")]
    public void Raster_code128_auto_rejects_empty_or_non_ascii_data_without_pollution(string data)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(300, 100);
        var before = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(300, 100).EndRasterPage().Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.RasterBarcode(
            BarcodeType.Code128Auto, 0, 0, data, 40));

        Assert.Equal(before, command.EndRasterPage().Build().GetBytes());
    }

    [Theory]
    [InlineData(BarcodeType.I2OF5C, "1234", "012348")]
    [InlineData(BarcodeType.I2OF5G, "12345678901", "123456789016")]
    public void Raster_check_digit_variants_normalize_before_width_measurement_and_rendering(
        BarcodeType type, string input, string normalized)
    {
        var variantWidth = EscPosRasterCanvas.MeasureBarcodeWidth(type, input, 2);
        var normalizedWidth = EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.I2OF5, normalized, 2);

        Assert.Equal(normalizedWidth, variantWidth);
        Assert.Equal(
            RenderBarcode(type, input),
            RenderBarcode(BarcodeType.I2OF5, normalized));
    }

    [Theory]
    [InlineData(BarcodeType.I2OF5C, "12A4", "1234")]
    [InlineData(BarcodeType.I2OF5G, "123456789017", "12345678901")]
    public void Raster_check_digit_variants_reject_invalid_data_with_element_path_and_allow_retry(
        BarcodeType type, string invalidData, string validData)
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);
        var invalid = new PrintDocument();
        invalid.AddPage(600, 140).Add(new PrintBarcodeElement(0, 0, type, invalidData, 40,
            narrowWidth: 2, humanReadable: false), "Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Render(invalid));
        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);

        var retry = provider.Render(BarcodeDocument(type, validData))[0];
        Assert.NotEmpty(retry);
        Assert.True(retry.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Theory]
    [InlineData(BarcodeType.I2OF5C, "12A4", "1234")]
    [InlineData(BarcodeType.I2OF5G, "123456789017", "12345678901")]
    public void Raster_check_digit_measurement_failure_releases_working_budget_for_retry(
        BarcodeType type, string invalidData, string validData)
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);

        Assert.Throws<ArgumentException>(() =>
            EscPosRasterCanvas.MeasureBarcodeWidth(type, invalidData, 2));
        Assert.Equal(0, scope.CurrentBytes);

        Assert.True(EscPosRasterCanvas.MeasureBarcodeWidth(type, validData, 2) > 0);
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Fact]
    public void Raster_template_normalizes_retail_data_before_measuring_and_rendering()
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
                            BarcodeType = BarcodeType.Ean13,
                            LineWidth = 1,
                            Height = 40,
                            Field = new BgField { Value = "123456789012" }
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
        Assert.True(result.Pages[0].Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Equal(40, result.Pages[0].Height);
    }

    [Fact]
    public void Raster_host_barcode_keeps_rotation_and_hri_reservation()
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code39,
            "ABC-123", 40, narrowWidth: 2, humanReadable: true,
            humanReadableReserveHeight: 18, rotation: RotationAngle.Rotate90), "Barcode");

        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);
        provider.Validate(document);
        var bytes = provider.Render(document)[0];

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.NotEmpty(bytes);
    }

    [Theory]
    [InlineData("12A45")]
    [InlineData("12345678901234")]
    public void Raster_msi_rejects_non_digit_or_more_than_thirteen_digits_with_element_path(string data)
    {
        var document = new PrintDocument();
        document.AddPage(600, 140).Add(new PrintBarcodeElement(0, 0, BarcodeType.Msi,
            data, 40, narrowWidth: 2, humanReadable: false), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("MSI", error.Message);
    }

    [Fact]
    public void Raster_msi_validation_failure_releases_working_budget_for_retry()
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        Assert.Throws<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Msi, "12345678901234", 2));
        Assert.Equal(0, scope.CurrentBytes);
        Assert.True(EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Msi, "12345", 2) > 0);
        Assert.Equal(0, scope.CurrentBytes);
    }

    private static byte[] RenderBarcode(BarcodeType type, string data)
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);
        return provider.Render(BarcodeDocument(type, data))[0];
    }

    private static PrintDocument BarcodeDocument(BarcodeType type, string data)
    {
        var document = new PrintDocument();
        document.AddPage(2400, 140).Add(new PrintBarcodeElement(0, 0, type, data, 40,
            narrowWidth: 2, humanReadable: false), "Barcode");
        return document;
    }
}
