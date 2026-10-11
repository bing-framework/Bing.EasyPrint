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

/// <summary>验证 ZPL 原生 EAN/UPC 零售条码、统一 Provider 和模板能力边界。</summary>
public sealed class ZplRetailBarcodeProviderTests
{
    [Theory]
    [InlineData(BarcodeType.Ean8, "96385074", "9638507", "^B8", 0, "N")]
    [InlineData(BarcodeType.Ean8, "96385074", "9638507", "^B8", 90, "R")]
    [InlineData(BarcodeType.Ean8, "96385074", "9638507", "^B8", 180, "I")]
    [InlineData(BarcodeType.Ean8, "96385074", "9638507", "^B8", 270, "B")]
    [InlineData(BarcodeType.Ean13, "5901234123457", "590123412345", "^BE", 0, "N")]
    [InlineData(BarcodeType.Ean13, "5901234123457", "590123412345", "^BE", 90, "R")]
    [InlineData(BarcodeType.Ean13, "5901234123457", "590123412345", "^BE", 180, "I")]
    [InlineData(BarcodeType.Ean13, "5901234123457", "590123412345", "^BE", 270, "B")]
    [InlineData(BarcodeType.UpcA, "036000291452", "03600029145", "^BU", 0, "N")]
    [InlineData(BarcodeType.UpcA, "036000291452", "03600029145", "^BU", 90, "R")]
    [InlineData(BarcodeType.UpcA, "036000291452", "03600029145", "^BU", 180, "I")]
    [InlineData(BarcodeType.UpcA, "036000291452", "03600029145", "^BU", 270, "B")]
    [InlineData(BarcodeType.UpcE, "425261", "4210000526", "^B9", 0, "N")]
    [InlineData(BarcodeType.UpcE, "425261", "4210000526", "^B9", 90, "R")]
    [InlineData(BarcodeType.UpcE, "425261", "4210000526", "^B9", 180, "I")]
    [InlineData(BarcodeType.UpcE, "425261", "4210000526", "^B9", 270, "B")]
    public void Zpl_command_writes_exact_retail_bytes_for_each_rotation(
        BarcodeType type, string data, string zplData, string command, int rotation, string orientation)
    {
        var bytes = AddRetail(new ZplPrintCommand(Encoding.ASCII).Start(), type, data,
                10, 20, 60, false, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var checkDigit = type == BarcodeType.UpcA || type == BarcodeType.UpcE ? ",Y" : string.Empty;
        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3{command}{orientation},60,N,N{checkDigit}^FD{zplData}^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "96385074", "^B8N,60,Y,N")]
    [InlineData(BarcodeType.Ean13, "5901234123457", "^BEN,60,Y,N")]
    [InlineData(BarcodeType.UpcA, "036000291452", "^BUN,60,Y,N,Y")]
    [InlineData(BarcodeType.UpcE, "425261", "^B9N,60,Y,N,Y")]
    public void Zpl_command_writes_interpretation_line_flags(
        BarcodeType type, string data, string prefix)
    {
        var output = Encoding.ASCII.GetString(AddRetail(new ZplPrintCommand(Encoding.ASCII).Start(), type, data,
                10, 20, 60, true, 3, 0)
            .End()
            .Build()
            .GetBytes());

        var zplData = type == BarcodeType.Ean8 ? "9638507" :
            type == BarcodeType.Ean13 ? "590123412345" :
            type == BarcodeType.UpcA ? "03600029145" : "4210000526";
        Assert.Contains($"^FO10,20^BY3{prefix}^FD{zplData}^FS\r\n", output);
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "")]
    [InlineData(BarcodeType.Ean8, "96385075")]
    [InlineData(BarcodeType.Ean8, "9638507A")]
    [InlineData(BarcodeType.Ean13, "5901234123450")]
    [InlineData(BarcodeType.Ean13, "59012341234A")]
    [InlineData(BarcodeType.UpcA, "036000291453")]
    [InlineData(BarcodeType.UpcA, "03600029145A")]
    [InlineData(BarcodeType.UpcE, "42A261")]
    [InlineData(BarcodeType.UpcE, "04252610")]
    public void Zpl_command_rejects_invalid_retail_data_without_polluting_output(
        BarcodeType type, string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => AddRetail(command, type, data,
            1, 2, 40, false, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "96385074")]
    [InlineData(BarcodeType.Ean13, "5901234123457")]
    [InlineData(BarcodeType.UpcA, "036000291452")]
    [InlineData(BarcodeType.UpcE, "425261")]
    public void Zpl_command_rejects_invalid_dimensions_without_polluting_output(
        BarcodeType type, string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => AddRetail(command, type, data,
            1, 2, 0, false, 2, 0));
        Assert.Equal(before, command.Build().GetBytes());

        Assert.Throws<ArgumentOutOfRangeException>(() => AddRetail(command, type, data,
            1, 2, 40, false, 0, 0));
        Assert.Equal(before, command.Build().GetBytes());

        Assert.Throws<ArgumentOutOfRangeException>(() => AddRetail(command, type, data,
            1, 2, 40, false, 11, 0));
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_command_rejects_retail_rotation_when_profile_disables_it_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => AddRetail(command, BarcodeType.Ean13,
            "5901234123457", 1, 2, 40, false, 2, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_and_raster_capabilities_expose_all_retail_barcodes()
    {
        var zpl = ZplProvider().Capabilities;
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        foreach (var type in RetailTypes())
        {
            Assert.True(zpl.SupportsBarcodeType(type));
            Assert.True(zpl.SupportsCommandBarcodeType(type));
            Assert.True(raster.SupportsBarcodeType(type));
        }
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "96385074", "9638507", "^B8N,40,N,N")]
    [InlineData(BarcodeType.Ean13, "5901234123457", "590123412345", "^BEN,40,N,N")]
    [InlineData(BarcodeType.UpcA, "036000291452", "03600029145", "^BUN,40,N,N,Y")]
    [InlineData(BarcodeType.UpcE, "425261", "4210000526", "^B9N,40,N,N,Y")]
    public void Zpl_provider_renders_retail_barcode_with_exact_page_envelope(
        BarcodeType type, string data, string zplData, string barcodeCommand)
    {
        var bytes = ZplProvider().Render(BarcodeDocument(type, data,
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW320\r\n^LL120\r\n" +
            $"^FO10,20^BY2{barcodeCommand}^FD{zplData}^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_provider_rejects_invalid_retail_data_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument(BarcodeType.Ean13, "5901234123450", barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("校验", error.Message);
    }

    [Fact]
    public void Zpl_profile_can_disable_retail_barcodes_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        foreach (var type in RetailTypes())
        {
            Assert.False(provider.Capabilities.SupportsBarcodeType(type));
            Assert.False(provider.Capabilities.SupportsCommandBarcodeType(type));
        }

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument(BarcodeType.Ean13, "5901234123457", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "9638507", "^B8R,40,N,N", "9638507")]
    [InlineData(BarcodeType.Ean13, "590123412345", "^BER,40,N,N", "590123412345")]
    [InlineData(BarcodeType.UpcA, "03600029145", "^BUR,40,N,N,Y", "03600029145")]
    [InlineData(BarcodeType.UpcE, "425261", "^B9R,40,N,N,Y", "4210000526")]
    public void Native_template_renders_retail_barcode_with_zpl_orientation(
        BarcodeType type, string data, string barcodeCommand, string zplData)
    {
        var result = new TemplateRenderer().RenderWithLayout(RetailTemplate(type, data,
            RotationAngle.Rotate90), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains(barcodeCommand + $"^FD{zplData}^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void Zpl_provider_renders_i2of5g_with_element_path_preserved()
    {
        var document = BarcodeDocument(BarcodeType.I2OF5G, "12345678901", barHeight: 40,
            humanReadable: false);
        ZplProvider().Validate(document);
        var output = Encoding.ASCII.GetString(ZplProvider().Render(document)[0]);

        Assert.Contains("^B2N,40,N,N,N^FD123456789016^FS\r\n", output);
    }

    [Fact]
    public void Raster_template_renders_retail_barcode_with_template_element_path()
    {
        var result = new TemplateRenderer().Render(
            RetailTemplate(BarcodeType.Ean13, "5901234123457"), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        Assert.Single(result.Pages);
        Assert.NotEmpty(result.Pages[0].Data);
    }

    private static ZplPrintCommand AddRetail(ZplPrintCommand command, BarcodeType type, string data,
        int x, int y, int height, bool printText, int moduleWidth, int rotation)
    {
        switch (type)
        {
            case BarcodeType.Ean8:
                return command.BarcodeEan8(x, y, data, height, printText, moduleWidth, rotation);
            case BarcodeType.Ean13:
                return command.BarcodeEan13(x, y, data, height, printText, moduleWidth, rotation);
            case BarcodeType.UpcA:
                return command.BarcodeUpcA(x, y, data, height, printText, moduleWidth, rotation);
            case BarcodeType.UpcE:
                return command.BarcodeUpcE(x, y, data, height, printText, moduleWidth, rotation);
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private static IEnumerable<BarcodeType> RetailTypes()
    {
        yield return BarcodeType.Ean8;
        yield return BarcodeType.Ean13;
        yield return BarcodeType.UpcA;
        yield return BarcodeType.UpcE;
    }

    private static IPrintProtocolProvider ZplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(BarcodeType type, string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, bool? humanReadable = null)
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, type, data, barHeight, narrowWidth, 2,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate RetailTemplate(BarcodeType type, string data,
        RotationAngle rotation = RotationAngle.None)
        => new BpTemplate
        {
            Version = 1,
            Width = 600,
            Height = 600,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 600,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 600,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = type,
                                    Rotation = rotation,
                                    LineWidth = 1,
                                    Ratio = 2,
                                    Height = 40,
                                    Width = 500,
                                    HumanReadable = false,
                                    Field = new BgField { Value = data }
                                }
                            }
                        }
                    }
                }
            }
        };
}
