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

/// <summary>验证 ZPL Code 39 的原生命令、统一 Provider 和模板能力边界。</summary>
public sealed class ZplCode39ProviderTests
{
    [Theory]
    [InlineData(0, "N", false, "N")]
    [InlineData(90, "R", false, "N")]
    [InlineData(180, "I", false, "N")]
    [InlineData(270, "B", false, "N")]
    [InlineData(0, "N", true, "Y")]
    public void Zpl_command_writes_exact_code39_bytes_for_each_rotation_and_text_flag(
        int rotation, string orientation, bool printText, string textFlag)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .Barcode39(10, 20, "ABC-12", 60, printText, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^B3{orientation},N,60,{textFlag},N^FDABC-12^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("A^B")]
    [InlineData("A~B")]
    [InlineData("A:B")]
    [InlineData("A\u4E2D B")]
    public void Zpl_command_rejects_invalid_code39_data_without_polluting_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode39(1, 2, data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(32001, 2)]
    [InlineData(60, 0)]
    [InlineData(60, 11)]
    public void Zpl_command_rejects_invalid_code39_dimensions(int height, int moduleWidth)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Barcode39(
            1, 2, "ABC-12", height, false, moduleWidth));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_capabilities_expose_code39_code93_retail_and_basic_numeric_barcodes()
    {
        var capabilities = ZplProvider().Capabilities;

        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.Code39));
        Assert.True(capabilities.SupportsCommandBarcodeType(BarcodeType.Code39));
        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(capabilities.SupportsCommandBarcodeType(BarcodeType.Code93));
        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.Code128));
        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.Ean13));
        Assert.True(capabilities.SupportsCommandBarcodeType(BarcodeType.Ean13));
        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.Codabar));
        Assert.True(capabilities.SupportsCommandBarcodeType(BarcodeType.Codabar));
        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.I2OF5));
        Assert.True(capabilities.SupportsCommandBarcodeType(BarcodeType.I2OF5));
    }

    [Fact]
    public void Zpl_provider_renders_code39_with_exact_page_envelope()
    {
        var bytes = ZplProvider().Render(BarcodeDocument(BarcodeType.Code39, "ABC-12",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW320\r\n^LL120\r\n" +
            "^FO10,20^BY2^B3N,N,40,N,N^FDABC-12^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_provider_keeps_code128_output_while_adding_code39()
    {
        var output = Encoding.ASCII.GetString(ZplProvider().Render(
            BarcodeDocument(BarcodeType.Code128, "12345", barHeight: 40,
                narrowWidth: 3, humanReadable: false))[0]);

        Assert.Contains("^FO0,0^BY3^BCN,40,N,N,N^FD12345^FS\r\n", output);
    }

    [Fact]
    public void Zpl_provider_renders_i2of5g_with_host_german_check_digit()
    {
        var output = Encoding.ASCII.GetString(ZplProvider().Render(
            BarcodeDocument(BarcodeType.I2OF5G, "12345678901", barHeight: 40,
                humanReadable: false))[0]);

        Assert.Contains("^B2N,40,N,N,N^FD123456789016^FS\r\n", output);
    }

    [Fact]
    public void Zpl_profile_can_disable_code39_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code39));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument(BarcodeType.Code39, "ABC-12", barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
    }

    [Fact]
    public void Native_template_renders_rotated_code39()
    {
        var result = new TemplateRenderer().RenderWithLayout(Code39Template(RotationAngle.Rotate90), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains("^B3R,N,40,N,N^FDABC-12^FS\r\n", Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void Template_renders_i2of5g_with_host_german_check_digit()
    {
        var result = new TemplateRenderer().RenderWithLayout(
            BarcodeTemplate(BarcodeType.I2OF5G, "12345678901"), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Contains("^B2N,40,N,N,N^FD123456789016^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void EscPos_raster_includes_code39_in_the_unified_raster_capability()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code39));
        Assert.NotEmpty(provider.Render(BarcodeDocument(BarcodeType.Code39, "ABC-12", barHeight: 40))[0]);
    }

    [Fact]
    public void Other_coordinate_protocols_keep_their_declared_code39_capability()
    {
        Assert.True(PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII)
            .Capabilities.SupportsBarcodeType(BarcodeType.Code39));
        Assert.True(PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII)
            .Capabilities.SupportsBarcodeType(BarcodeType.Code39));
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

    private static BpTemplate Code39Template(RotationAngle rotation)
        => BarcodeTemplate(BarcodeType.Code39, "ABC-12", rotation);

    private static BpTemplate BarcodeTemplate(BarcodeType type, string data,
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
