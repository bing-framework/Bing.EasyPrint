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

public sealed class BarcodeHumanReadableTests
{
    [Theory]
    [InlineData(PrintProtocol.Tspl, "BARCODE 0,0,\"128\",40,0,")]
    [InlineData(PrintProtocol.Zpl, "^BCN,40,N,N,N")]
    public void Coordinate_providers_honor_explicitly_disabled_human_readable_text(
        PrintProtocol protocol, string expected)
    {
        var document = BarcodeDocument(false);
        var bytes = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Render(document)[0];
        Assert.Contains(expected, Encoding.ASCII.GetString(bytes));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "BARCODE 24,36,\"128\",40,1,")]
    [InlineData(PrintProtocol.Zpl, "^BCN,40,Y")]
    public void Coordinate_providers_render_hri_with_explicit_quiet_zone(
        PrintProtocol protocol, string expected)
    {
        var document = new PrintDocument();
        document.AddPage(500, 200).Add(new PrintBarcodeElement(24, 36, BarcodeType.Code128,
            "12345", 40, new PrintQuietZone(8, 4, 12, 6),
            humanReadable: true, humanReadableReserveHeight: 12, reservedWidth: 320), "Barcode");

        var bytes = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII).Render(document)[0];
        Assert.Contains(expected, Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void Native_escpos_can_enable_human_readable_text()
    {
        var bytes = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII)
            .Render(BarcodeDocument(true))[0];
        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x48, 0x02 }) >= 0);
    }

    [Fact]
    public void Cpcl_can_enable_human_readable_text_without_changing_disabled_output()
    {
        var enabled = Encoding.ASCII.GetString(PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII)
            .Render(BarcodeDocument(true))[0]);
        var disabled = Encoding.ASCII.GetString(PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII)
            .Render(BarcodeDocument(false))[0]);

        Assert.Contains("BT 0 24 0", enabled);
        Assert.Contains("BT OFF", enabled);
        Assert.DoesNotContain("BT 0 24 0", disabled);
        Assert.DoesNotContain("BT OFF", disabled);
    }

    [Fact]
    public void Raster_can_render_human_readable_text_and_keep_disabled_output_unchanged()
    {
        var readableDocument = new PrintDocument();
        readableDocument.AddPage(400, 100)
            .Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128, "12345", 40,
                humanReadable: true, humanReadableReserveHeight: 20), "Barcode");
        var enabled = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster)
            .Render(readableDocument)[0];
        var disabled = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Raster)
            .Render(BarcodeDocument(false))[0];

        Assert.NotEqual(disabled, enabled);
        Assert.True(enabled.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_requires_a_positive_human_readable_reservation()
    {
        var document = BarcodeDocument(true);
        var error = Assert.Throws<PrintCapabilityException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster)
                .Validate(document));
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Fact]
    public void Raster_human_readable_font_size_is_configurable_and_exposed()
    {
        var document = new PrintDocument();
        document.AddPage(400, 120).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "12345", 40, humanReadable: true, humanReadableReserveHeight: 30), "Barcode");

        var small = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { RasterBarcodeHumanReadableFontSize = 8 });
        var large = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { RasterBarcodeHumanReadableFontSize = 16 });

        Assert.Equal(8, small.Capabilities.RasterBarcodeHumanReadableFontSize);
        Assert.Equal(16, large.Capabilities.RasterBarcodeHumanReadableFontSize);
        Assert.NotEqual(small.Render(document)[0], large.Render(document)[0]);

        var templateCapabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII,
            EscPosDevice = new EscPosDeviceOptions { RasterBarcodeHumanReadableFontSize = 16 }
        });
        Assert.Equal(16, templateCapabilities.RasterBarcodeHumanReadableFontSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public void Raster_human_readable_font_size_rejects_values_outside_protocol_range(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EscPosPrintCommand(Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { RasterBarcodeHumanReadableFontSize = size }));
    }

    [Fact]
    public void Raster_human_readable_font_size_is_rejected_for_native_layout()
    {
        Assert.Throws<ArgumentException>(() => new EscPosPrintCommand(Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { RasterBarcodeHumanReadableFontSize = 16 }));
    }

    [Fact]
    public void Raster_template_rejects_missing_human_readable_reservation_before_rendering()
    {
        var template = new BpTemplate
        {
            Width = 400,
            AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpBarcodeElement { Field = new BgField { Value = "12345" },
                        Height = 40, HumanReadable = true }
                } }
            } } }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(template, null,
            options: new TemplateRenderOptions { Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster, Encoding = Encoding.ASCII }));
        Assert.Contains("HumanReadableReserveHeight", error.Message);
        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    [Fact]
    public void Reserved_human_readable_height_participates_in_page_bounds()
    {
        var document = new PrintDocument();
        document.AddPage(400, 100).Add(new PrintBarcodeElement(0, 50, BarcodeType.Code128,
            "12345", 40, humanReadableReserveHeight: 20), "Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Validate(document));
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Fact]
    public void Template_reserves_device_human_readable_height_in_layout()
    {
        var template = new BpTemplate
        {
            Width = 400,
            AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpBarcodeElement { Field = new BgField { Value = "12345" },
                        Height = 40, HumanReadableReserveHeight = 20 }
                } }
            } } }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions { Protocol = PrintProtocol.Tspl, Encoding = Encoding.ASCII });
        Assert.Equal(60, result.Pages[0].Height);
        Assert.Contains("BARCODE", Encoding.ASCII.GetString(result.Pages[0].Data));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "BARCODE")]
    [InlineData(PrintProtocol.Zpl, "^BC")]
    public void Template_coordinate_protocols_render_hri_and_quiet_zone(
        PrintProtocol protocol, string expected)
    {
        var template = new BpTemplate
        {
            Width = 500,
            AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpBarcodeElement { Field = new BgField { Value = "12345" },
                        Width = 320, Height = 40, HumanReadable = true,
                        HumanReadableReserveHeight = 12,
                        QuietZone = new PrintQuietZone(8, 4, 12, 6) }
                } }
            } } }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions { Protocol = protocol, Encoding = Encoding.ASCII });
        Assert.Equal(62, result.Pages[0].Height);
        Assert.Contains(expected, Encoding.ASCII.GetString(result.Pages[0].Data));
    }

    [Fact]
    public void Reserve_without_printed_human_readable_text_is_rejected()
    {
        var document = new PrintDocument();
        document.AddPage(400, 100).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "12345", 40, humanReadable: false, humanReadableReserveHeight: 10), "Barcode");

        Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Validate(document));
    }

    [Fact]
    public void Template_enables_cpcl_human_readable_text()
    {
        var template = new BpTemplate
        {
            Width = 400,
            AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpBarcodeElement { Field = new BgField { Value = "12345" },
                        Height = 40, HumanReadable = true, HumanReadableReserveHeight = 12 }
                } }
            } } }
        };

        var result = new TemplateRenderer().Render(template, null,
            options: new TemplateRenderOptions { Protocol = PrintProtocol.Cpcl, Encoding = Encoding.ASCII });
        Assert.Contains("BT 0 24 0", Encoding.ASCII.GetString(result.Pages[0].Data));
    }

    private static PrintDocument BarcodeDocument(bool? humanReadable)
    {
        var document = new PrintDocument();
        document.AddPage(400, 200).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "12345", 40, humanReadable: humanReadable), "Barcode");
        return document;
    }
}
