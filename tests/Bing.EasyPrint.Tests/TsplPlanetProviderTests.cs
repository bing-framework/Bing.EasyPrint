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

/// <summary>验证 TSPL 原生 PLANET、统一 Provider 和协议边界。</summary>
public sealed class TsplPlanetProviderTests
{
    [Fact]
    public void Tspl_command_writes_native_planet_payload()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .BarcodePlanet(10, 20, "123456789014", 40, 0, 90, 2, 2)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "BARCODE 10,20,\"PLANET\",40,0,90,2,2,\"123456789014\"\r\n"), bytes);
    }

    [Fact]
    public void Tspl_generic_barcode_normalizes_planet_hyphens_and_preserves_type()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .Barcode(1, 2, "planet", 30, 1, 0, 2, 2, "12345678901-4")
            .Build()
            .GetBytes());

        Assert.Equal(
            "BARCODE 1,2,\"planet\",30,1,0,2,2,\"123456789014\"\r\n",
            output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678901")]
    [InlineData("123456789013")]
    [InlineData("12345678901234")]
    [InlineData("12345678901A4")]
    public void Tspl_planet_validation_does_not_pollute_existing_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodePlanet(
            1, 2, data, 40, 0, 0, 2, 2));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_profile_can_disable_planet_without_polluting_existing_output()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodePlanet(
            1, 2, "123456789014", 40, 0, 0, 2, 2));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(RotationAngle.None, 10, 20)]
    [InlineData(RotationAngle.Rotate90, 50, 20)]
    [InlineData(RotationAngle.Rotate180, 256, 60)]
    [InlineData(RotationAngle.Rotate270, 10, 266)]
    public void Tspl_provider_renders_planet_with_rotation_origin(
        RotationAngle rotation, int originX, int originY)
    {
        var bytes = TsplProvider().Render(BarcodeDocument("123456789014", 10, 20,
            barHeight: 40, narrowWidth: 2, wideWidth: 2,
            humanReadable: false, rotation: rotation))[0];

        var expected = Encoding.ASCII.GetBytes(
            "SIZE 500 dot,300 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            $"BARCODE {originX},{originY},\"PLANET\",40,0,{(int)rotation},2,2,\"123456789014\"\r\n" +
            "PRINT 1,1\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Tspl_provider_rejects_planet_outside_page_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => TsplProvider().Validate(
            BarcodeDocument("123456789014", 10, 1, barHeight: 40, narrowWidth: 2,
                wideWidth: 2, humanReadable: false, rotation: RotationAngle.None,
                pageWidth: 255, pageHeight: 120)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Planet_capabilities_are_exposed_for_tspl_and_zpl_raster_only_elsewhere()
    {
        var tspl = TsplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Planet));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Planet));
        Assert.False(cpcl.SupportsBarcodeType(BarcodeType.Planet));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Planet));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Planet));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Planet));
    }

    [Fact]
    public void Tspl_template_renders_planet_and_exposes_capability()
    {
        var capabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Tspl,
            Encoding = Encoding.ASCII
        });
        Assert.Contains(BarcodeType.Planet, capabilities.BarcodeTypes);

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
            "\"PLANET\",40,0,0,2,2,\"123456789014\"\r\n",
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
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Planet, data, barHeight,
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
                                    BarcodeType = BarcodeType.Planet,
                                    Rotation = rotation,
                                    LineWidth = 1,
                                    Ratio = 1,
                                    NarrowModuleWidth = 2,
                                    WideModuleWidth = 2,
                                    Height = 40,
                                    HumanReadable = false,
                                    Field = new BgField { Value = "123456789014" }
                                }
                            }
                        }
                    }
                }
            }
        };
}
