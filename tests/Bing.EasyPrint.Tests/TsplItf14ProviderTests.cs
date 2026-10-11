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

/// <summary>验证 TSPL 原生 ITF14、统一 Provider 和协议边界。</summary>
public sealed class TsplItf14ProviderTests
{
    [Fact]
    public void Tspl_command_writes_native_itf14_payload()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .BarcodeItf14(10, 20, "1234567890123", 40, 0, 90, 2, 4)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "BARCODE 10,20,\"ITF14\",40,0,90,2,4,\"1234567890123\"\r\n"), bytes);
    }

    [Fact]
    public void Tspl_generic_barcode_normalizes_itf14_payload_without_host_check_digit()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .Barcode(1, 2, "itf14", 30, 1, 0, 2, 4, "9876543210123")
            .Build()
            .GetBytes());

        Assert.Equal(
            "BARCODE 1,2,\"itf14\",30,1,0,2,4,\"9876543210123\"\r\n",
            output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456789012")]
    [InlineData("12345678901234")]
    [InlineData("123456789012A")]
    public void Tspl_itf14_validation_does_not_pollute_existing_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeItf14(
            1, 2, data, 40, 0, 0, 2, 4));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_profile_can_disable_itf14_without_polluting_existing_output()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodeItf14(
            1, 2, "1234567890123", 40, 0, 0, 2, 4));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(RotationAngle.None, 10, 20)]
    [InlineData(RotationAngle.Rotate90, 50, 20)]
    [InlineData(RotationAngle.Rotate180, 222, 60)]
    [InlineData(RotationAngle.Rotate270, 10, 232)]
    public void Tspl_provider_renders_itf14_with_rotation_origin(
        RotationAngle rotation, int originX, int originY)
    {
        var bytes = TsplProvider().Render(BarcodeDocument("1234567890123", 10, 20,
            barHeight: 40, narrowWidth: 2, wideWidth: 4,
            humanReadable: false, rotation: rotation))[0];

        var expected = Encoding.ASCII.GetBytes(
            "SIZE 500 dot,300 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            $"BARCODE {originX},{originY},\"ITF14\",40,0,{(int)rotation},2,4,\"1234567890123\"\r\n" +
            "PRINT 1,1\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Tspl_provider_rejects_itf14_outside_page_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => TsplProvider().Validate(
            BarcodeDocument("1234567890123", 10, 1, barHeight: 40, narrowWidth: 2,
                wideWidth: 4, humanReadable: false, rotation: RotationAngle.None,
                pageWidth: 221, pageHeight: 120)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Itf14_capabilities_are_tspl_native_and_esc_pos_raster_only()
    {
        var tspl = TsplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Itf14));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Itf14));
        Assert.False(cpcl.SupportsBarcodeType(BarcodeType.Itf14));
        Assert.False(zpl.SupportsBarcodeType(BarcodeType.Itf14));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Itf14));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Itf14));
    }

    [Fact]
    public void Tspl_template_renders_itf14_and_exposes_capability()
    {
        var capabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Tspl,
            Encoding = Encoding.ASCII
        });
        Assert.Contains(BarcodeType.Itf14, capabilities.BarcodeTypes);

        var result = new TemplateRenderer().RenderWithLayout(Template(RotationAngle.None), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Contains(
            "\"ITF14\",40,0,0,2,4,\"1234567890123\"\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    private static IPrintProtocolProvider TsplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(string data, int x, int y,
        int barHeight, int narrowWidth, int wideWidth, bool humanReadable,
        RotationAngle rotation, int pageWidth = 500, int pageHeight = 300)
    {
        var document = new PrintDocument();
        var page = document.AddPage(pageWidth, pageHeight);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Itf14, data, barHeight,
            narrowWidth, wideWidth, rotation, humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate Template(RotationAngle rotation)
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
                                    BarcodeType = BarcodeType.Itf14,
                                    Rotation = rotation,
                                    LineWidth = 1,
                                    Ratio = 1,
                                    NarrowModuleWidth = 2,
                                    WideModuleWidth = 4,
                                    Height = 40,
                                    HumanReadable = false,
                                    Field = new BgField { Value = "1234567890123" }
                                }
                            }
                        }
                    }
                }
            }
        };
}
