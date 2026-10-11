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

/// <summary>验证 ZPL Code 11 的原生命令、统一 Provider 和模板能力边界。</summary>
public sealed class ZplCode11ProviderTests
{
    [Theory]
    [InlineData(0, "N", false, "N")]
    [InlineData(90, "R", false, "N")]
    [InlineData(180, "I", false, "N")]
    [InlineData(270, "B", false, "N")]
    [InlineData(0, "N", true, "Y")]
    public void Zpl_command_writes_exact_code11_bytes_for_each_rotation_and_text_flag(
        int rotation, string orientation, bool printText, string textFlag)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeCode11(10, 20, "123-45", 60, printText, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^B1{orientation},N,60,{textFlag},N^FD123-45^FS\r\n" +
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
    public void Zpl_command_rejects_invalid_code11_data_without_polluting_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeCode11(1, 2, data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(32001, 2)]
    [InlineData(60, 0)]
    [InlineData(60, 11)]
    public void Zpl_command_rejects_invalid_code11_dimensions_without_polluting_output(
        int height, int moduleWidth)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.BarcodeCode11(
            1, 2, "123-45", height, false, moduleWidth));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_command_rejects_code11_rotation_when_profile_disables_it_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodeCode11(
            1, 2, "123-45", 40, false, 2, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_capabilities_expose_code11_only_where_the_provider_can_encode_it()
    {
        var zpl = ZplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Code11));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.Code11));
        Assert.False(cpcl.SupportsBarcodeType(BarcodeType.Code11));
        Assert.False(cpcl.SupportsCommandBarcodeType(BarcodeType.Code11));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Code11));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Code11));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Code11));
        Assert.False(escPosNative.SupportsCommandBarcodeType(BarcodeType.Code11));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Code11));
        Assert.False(escPosRaster.SupportsCommandBarcodeType(BarcodeType.Code11));
    }

    [Fact]
    public void Zpl_provider_renders_code11_with_exact_page_envelope()
    {
        var bytes = ZplProvider().Render(BarcodeDocument("123-45",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW320\r\n^LL120\r\n" +
            "^FO10,20^BY2^B1N,N,40,N,N^FD123-45^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_provider_rejects_code11_outside_page_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument("12345", x: 10, y: 1, barHeight: 40, narrowWidth: 2,
                humanReadable: false, pageWidth: 180)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Zpl_profile_can_disable_code11_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code11));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("123-45", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(RotationAngle.None, "N")]
    [InlineData(RotationAngle.Rotate90, "R")]
    [InlineData(RotationAngle.Rotate180, "I")]
    [InlineData(RotationAngle.Rotate270, "B")]
    public void Native_template_renders_code11_using_each_zpl_orientation(
        RotationAngle rotation, string orientation)
    {
        var result = new TemplateRenderer().RenderWithLayout(Code11Template(rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains($"^B1{orientation},N,40,N,N^FD123-45^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void Zpl_template_capabilities_include_code11()
    {
        var capabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Zpl,
            Encoding = Encoding.ASCII
        });

        Assert.Contains(BarcodeType.Code11, capabilities.BarcodeTypes);
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
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Code11, data, barHeight, narrowWidth, 2,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate Code11Template(RotationAngle rotation)
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
                                    BarcodeType = BarcodeType.Code11,
                                    Rotation = rotation,
                                    LineWidth = 1,
                                    Ratio = 2,
                                    Height = 40,
                                    Width = 500,
                                    HumanReadable = false,
                                    Field = new BgField { Value = "123-45" }
                                }
                            }
                        }
                    }
                }
            }
        };
}
