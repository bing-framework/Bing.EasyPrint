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

/// <summary>验证 TSPL 原生 CODE49、统一 Provider 和协议边界。</summary>
public sealed class TsplCode49ProviderTests
{
    [Fact]
    public void Tspl_command_writes_native_code49_bytes()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .BarcodeCode49(10, 20, "abcd1234ABCD", 40, 0, 90, 2, 2)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "BARCODE 10,20,\"CODE49\",40,0,90,2,2,\"abcd1234ABCD\"\r\n"), bytes);
    }

    [Fact]
    public void Tspl_generic_barcode_preserves_code49_type()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .Barcode(1, 2, "code49", 30, 1, 0, 2, 2, "ABC123")
            .Build()
            .GetBytes());

        Assert.Equal(
            "BARCODE 1,2,\"code49\",30,1,0,2,2,\"ABC123\"\r\n",
            output);
    }

    [Fact]
    public void Tspl_code49_accepts_del_character_without_encoding_replacement()
    {
        var data = "A\u007fZ";
        var output = new TsplPrintCommand(Encoding.ASCII)
            .BarcodeCode49(1, 2, data, 30, 0, 0, 2, 2)
            .Build()
            .GetBytes();

        Assert.Equal((byte)0x7f, output[output.Length - 5]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789123")]
    [InlineData("A\u0001B")]
    [InlineData("A\rB")]
    [InlineData("A\nB")]
    [InlineData("A\"B")]
    [InlineData("A\u001bB")]
    [InlineData("中文")]
    public void Tspl_code49_validation_does_not_pollute_existing_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeCode49(
            1, 2, data, 40, 0, 0, 2, 2));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(2, 4)]
    [InlineData(2, 6)]
    [InlineData(2, 5)]
    [InlineData(3, 7)]
    public void Tspl_code49_accepts_documented_ratios(int narrow, int wide)
    {
        var command = new TsplPrintCommand(Encoding.ASCII)
            .BarcodeCode49(1, 2, "ABC", 40, 0, 0, narrow, wide);

        Assert.NotEmpty(command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_code49_rejects_unsupported_ratio_without_pollution()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.BarcodeCode49(
            1, 2, "ABC", 40, 0, 0, 2, 7));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_profile_can_disable_code49_without_polluting_existing_output()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodeCode49(
            1, 2, "ABC", 40, 0, 0, 2, 2));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(RotationAngle.None, 10, 20)]
    [InlineData(RotationAngle.Rotate90, 50, 20)]
    [InlineData(RotationAngle.Rotate180, 696, 60)]
    [InlineData(RotationAngle.Rotate270, 10, 706)]
    public void Tspl_provider_renders_code49_with_rotation_origin(
        RotationAngle rotation, int originX, int originY)
    {
        var bytes = TsplProvider().Render(BarcodeDocument("12345", 10, 20,
            barHeight: 40, narrowWidth: 2, wideWidth: 2,
            humanReadable: false, rotation: rotation, pageWidth: 1000, pageHeight: 1000))[0];

        var expected = Encoding.ASCII.GetBytes(
            "SIZE 1000 dot,1000 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            $"BARCODE {originX},{originY},\"CODE49\",40,0,{(int)rotation},2,2,\"12345\"\r\n" +
            "PRINT 1,1\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Tspl_provider_rejects_code49_outside_page_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => TsplProvider().Validate(
            BarcodeDocument("12345", 10, 1, barHeight: 40,
                narrowWidth: 2, wideWidth: 2, humanReadable: false,
                rotation: RotationAngle.None, pageWidth: 695, pageHeight: 120)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Code49_capabilities_are_exposed_by_tspl_and_zpl()
    {
        var tspl = TsplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Code49));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Code49));
        Assert.False(cpcl.SupportsBarcodeType(BarcodeType.Code49));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Code49));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.Code49));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Code49));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Code49));
    }

    [Fact]
    public void Tspl_template_renders_code49_and_exposes_capability()
    {
        var capabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Tspl,
            Encoding = Encoding.ASCII
        });
        Assert.Contains(BarcodeType.Code49, capabilities.BarcodeTypes);

        var result = new TemplateRenderer().RenderWithLayout(Template(), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Contains(
            "\"CODE49\",40,0,0,2,2,\"123-ABC\"\r\n",
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
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Code49, data, barHeight,
            narrowWidth, wideWidth, rotation, humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate Template()
        => new BpTemplate
        {
            Version = 1,
            Width = 1200,
            Height = 1200,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 1200,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 1200,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Code49,
                                    LineWidth = 1,
                                    Ratio = 1,
                                    NarrowModuleWidth = 2,
                                    WideModuleWidth = 2,
                                    Height = 40,
                                    HumanReadable = false,
                                    Field = new BgField { Value = "123-ABC" }
                                }
                            }
                        }
                    }
                }
            }
        };
}
