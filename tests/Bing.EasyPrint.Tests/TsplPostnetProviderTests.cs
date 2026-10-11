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

/// <summary>验证 TSPL 原生 POSTNET、统一 Provider 和协议边界。</summary>
public sealed class TsplPostnetProviderTests
{
    [Theory]
    [InlineData("12345", "12345")]
    [InlineData("1234-56789", "123456789")]
    [InlineData("12345-67890-1", "12345678901")]
    public void Tspl_command_writes_native_postnet_bytes(string data, string normalized)
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .BarcodePostnet(10, 20, data, 40, 0, 0, 2, 4)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            $"BARCODE 10,20,\"POST\",40,0,0,2,4,\"{normalized}\"\r\n"), bytes);
    }

    [Theory]
    [InlineData("POST", "12345", "12345")]
    [InlineData("post", "1234-56789", "123456789")]
    public void Tspl_generic_barcode_maps_postnet_type_and_normalizes_data(
        string type, string data, string normalized)
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .Barcode(1, 2, type, 30, 1, 0, 2, 3, data)
            .Build()
            .GetBytes());

        Assert.Contains($"BARCODE 1,2,\"{type}\",30,1,0,2,3,\"{normalized}\"\r\n", output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("----")]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("1234A")]
    [InlineData("1234 5")]
    [InlineData("123\"45")]
    public void Tspl_postnet_validation_does_not_pollute_existing_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodePostnet(
            1, 2, data, 40, 0, 0, 2, 4));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_profile_can_disable_postnet_without_polluting_existing_output()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodePostnet(
            1, 2, "12345", 40, 0, 0, 2, 4));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(RotationAngle.None, 10, 20)]
    [InlineData(RotationAngle.Rotate90, 50, 20)]
    [InlineData(RotationAngle.Rotate180, 136, 60)]
    [InlineData(RotationAngle.Rotate270, 10, 146)]
    public void Tspl_provider_renders_postnet_with_rotation_origin(
        RotationAngle rotation, int originX, int originY)
    {
        var bytes = TsplProvider().Render(BarcodeDocument("1234-5", 10, 20,
            barHeight: 40, narrowWidth: 2, wideWidth: 4,
            humanReadable: false, rotation: rotation))[0];

        var expected = Encoding.ASCII.GetBytes(
            "SIZE 320 dot,220 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            $"BARCODE {originX},{originY},\"POST\",40,0,{(int)rotation},2,4,\"12345\"\r\n" +
            "PRINT 1,1\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Tspl_provider_rejects_postnet_outside_page_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => TsplProvider().Validate(
            BarcodeDocument("12345", 200, 1, barHeight: 40, narrowWidth: 2,
                wideWidth: 4, humanReadable: false, rotation: RotationAngle.None,
                pageWidth: 320, pageHeight: 120)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Tspl_capabilities_expose_postnet_and_other_native_protocols_keep_boundaries()
    {
        var tspl = TsplProvider().Capabilities;
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Postnet));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.True(cpcl.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.False(escPosNative.SupportsCommandBarcodeType(BarcodeType.Postnet));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.False(escPosRaster.SupportsCommandBarcodeType(BarcodeType.Postnet));
    }

    [Fact]
    public void Tspl_profile_can_disable_postnet_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Postnet));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345", 0, 0, barHeight: 40, narrowWidth: 2,
                wideWidth: 4, humanReadable: false, rotation: RotationAngle.None)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(RotationAngle.None, 0)]
    [InlineData(RotationAngle.Rotate90, 90)]
    [InlineData(RotationAngle.Rotate180, 180)]
    [InlineData(RotationAngle.Rotate270, 270)]
    public void Tspl_template_renders_postnet_for_each_native_rotation(
        RotationAngle rotation, int encodedRotation)
    {
        var result = new TemplateRenderer().RenderWithLayout(PostnetTemplate("1234-5", rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Tspl, result.Protocol);
        Assert.Contains(
            $"\"POST\",40,0,{encodedRotation},2,5,\"12345\"\r\n",
            Encoding.ASCII.GetString(page.Data));
        Assert.Contains(page.Elements, element => element.Type == 4 && element.Rotation == rotation);
    }

    private static IPrintProtocolProvider TsplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(string data, int x, int y,
        int barHeight, int narrowWidth, int wideWidth, bool humanReadable,
        RotationAngle rotation, int pageWidth = 320, int pageHeight = 220)
    {
        var document = new PrintDocument();
        var page = document.AddPage(pageWidth, pageHeight);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Postnet, data, barHeight,
            narrowWidth, wideWidth, rotation, humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate PostnetTemplate(string data, RotationAngle rotation)
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
                                    BarcodeType = BarcodeType.Postnet,
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
