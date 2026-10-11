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

/// <summary>验证 ZPL Plessey 的原生命令、统一 Provider 和模板能力边界。</summary>
public sealed class ZplPlesseyProviderTests
{
    [Theory]
    [InlineData(0, "N", false, "N")]
    [InlineData(90, "R", false, "N")]
    [InlineData(180, "I", false, "N")]
    [InlineData(270, "B", false, "N")]
    [InlineData(0, "N", true, "Y")]
    public void Zpl_command_writes_exact_plessey_bytes_for_each_rotation_and_text_flag(
        int rotation, string orientation, bool printText, string textFlag)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodePlessey(10, 20, "12345", 60, printText, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^BP{orientation},N,60,{textFlag},N^FD12345^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123A")]
    [InlineData("12 34")]
    [InlineData("123^45")]
    [InlineData("123~45")]
    [InlineData("中文")]
    public void Zpl_command_rejects_invalid_plessey_data_without_polluting_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodePlessey(
            1, 2, data, 40, false, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(32001, 2)]
    [InlineData(60, 0)]
    [InlineData(60, 11)]
    public void Zpl_command_rejects_invalid_plessey_dimensions_without_polluting_output(
        int height, int moduleWidth)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.BarcodePlessey(
            1, 2, "12345", height, false, moduleWidth));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_command_rejects_plessey_rotation_when_profile_disables_it_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodePlessey(
            1, 2, "12345", 40, false, 2, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_capabilities_expose_plessey_on_zpl_and_tspl()
    {
        var zpl = ZplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Plessey));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.Plessey));
        Assert.False(cpcl.SupportsBarcodeType(BarcodeType.Plessey));
        Assert.False(cpcl.SupportsCommandBarcodeType(BarcodeType.Plessey));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Plessey));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Plessey));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Plessey));
        Assert.False(escPosNative.SupportsCommandBarcodeType(BarcodeType.Plessey));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Plessey));
        Assert.False(escPosRaster.SupportsCommandBarcodeType(BarcodeType.Plessey));
    }

    [Fact]
    public void Zpl_provider_renders_plessey_with_exact_page_envelope()
    {
        var bytes = ZplProvider().Render(BarcodeDocument("12345",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW320\r\n^LL120\r\n" +
            "^FO10,20^BY2^BPN,N,40,N,N^FD12345^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_provider_rejects_plessey_outside_page_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument("12345", x: 10, y: 1, barHeight: 40, narrowWidth: 2,
                humanReadable: false, pageWidth: 300)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Zpl_profile_can_disable_plessey_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Plessey));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(RotationAngle.None, "N")]
    [InlineData(RotationAngle.Rotate90, "R")]
    [InlineData(RotationAngle.Rotate180, "I")]
    [InlineData(RotationAngle.Rotate270, "B")]
    public void Native_template_renders_plessey_using_each_zpl_orientation(
        RotationAngle rotation, string orientation)
    {
        var result = new TemplateRenderer().RenderWithLayout(PlesseyTemplate(rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains($"^BP{orientation},N,40,N,N^FD12345^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void Zpl_template_capabilities_include_plessey()
    {
        var capabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Zpl,
            Encoding = Encoding.ASCII
        });

        Assert.Contains(BarcodeType.Plessey, capabilities.BarcodeTypes);
        Assert.Contains(RotationAngle.Rotate90, capabilities.BarcodeRotations);
    }

    private static IPrintProtocolProvider ZplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, bool? humanReadable = null,
        int pageWidth = 320)
    {
        var document = new PrintDocument();
        var page = document.AddPage(pageWidth, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Plessey, data, barHeight, narrowWidth, 2,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate PlesseyTemplate(RotationAngle rotation)
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
                                    BarcodeType = BarcodeType.Plessey,
                                    Rotation = rotation,
                                    LineWidth = 1,
                                    Ratio = 2,
                                    Height = 40,
                                    Width = 500,
                                    HumanReadable = false,
                                    Field = new BgField { Value = "12345" }
                                }
                            }
                        }
                    }
                }
            }
        };
}
