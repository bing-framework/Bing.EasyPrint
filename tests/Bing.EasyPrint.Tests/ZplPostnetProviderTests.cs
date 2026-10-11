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

/// <summary>验证 ZPL 原生 POSTNET、统一 Provider 和跨协议能力边界。</summary>
public sealed class ZplPostnetProviderTests
{
    [Theory]
    [InlineData(0, "N")]
    [InlineData(90, "R")]
    [InlineData(180, "I")]
    [InlineData(270, "B")]
    public void Zpl_command_writes_exact_postnet_bytes_for_each_rotation(
        int rotation, string orientation)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodePostnet(10, 20, "12345", 40, false, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^BZ{orientation},40,N,N^FD12345^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData("12345", "12345")]
    [InlineData("1234-56789", "123456789")]
    [InlineData("12345-67890-1", "12345678901")]
    [InlineData("1-2-3-4-5", "12345")]
    public void Zpl_command_normalizes_postnet_lengths_and_hyphens(
        string data, string normalized)
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodePostnet(10, 20, data, 40, true, 2, 0)
            .End()
            .Build()
            .GetBytes());

        Assert.Contains($"^FO10,20^BY2^BZN,40,Y,N^FD{normalized}^FS\r\n", output);
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
    [InlineData("123^45")]
    [InlineData("123~45")]
    public void Zpl_command_rejects_invalid_postnet_data_without_polluting_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodePostnet(
            1, 2, data, 40, false, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(32001, 2)]
    [InlineData(40, 0)]
    [InlineData(40, 11)]
    public void Zpl_command_rejects_invalid_postnet_dimensions_without_polluting_output(
        int height, int moduleWidth)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.BarcodePostnet(
            1, 2, "12345", height, false, moduleWidth, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_command_rejects_postnet_rotation_when_profile_disables_it_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodePostnet(
            1, 2, "12345", 40, false, 2, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_capabilities_expose_postnet_and_other_protocols_reject_it()
    {
        var zpl = ZplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.Postnet));
        Assert.True(cpcl.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Postnet));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.False(escPosNative.SupportsCommandBarcodeType(BarcodeType.Postnet));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.False(escPosRaster.SupportsCommandBarcodeType(BarcodeType.Postnet));
    }

    [Fact]
    public void Zpl_provider_renders_postnet_with_exact_page_envelope()
    {
        var bytes = ZplProvider().Render(BarcodeDocument("1234-5",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW320\r\n^LL120\r\n" +
            "^FO10,20^BY2^BZN,40,N,N^FD12345^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_provider_rejects_invalid_postnet_data_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument("123456", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("POSTNET", error.Message);
    }

    [Theory]
    [InlineData(RotationAngle.None, "N")]
    [InlineData(RotationAngle.Rotate90, "R")]
    [InlineData(RotationAngle.Rotate180, "I")]
    [InlineData(RotationAngle.Rotate270, "B")]
    public void Native_template_renders_postnet_using_each_zpl_orientation(
        RotationAngle rotation, string orientation)
    {
        var result = new TemplateRenderer().RenderWithLayout(PostnetTemplate("1234-5", rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains($"^BZ{orientation},40,N,N^FD12345^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void Escpos_native_provider_rejects_postnet_before_rendering()
    {
        const PrintProtocol protocol = PrintProtocol.EscPos;
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Native);

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Postnet));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345", barHeight: 40)));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.NotEmpty(error.Message);
    }

    [Fact]
    public void EscPos_raster_renders_postnet_with_host_encoding()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Postnet));
        provider.Validate(BarcodeDocument("12345", barHeight: 40));
        Assert.NotEmpty(provider.Render(BarcodeDocument("12345", barHeight: 40))[0]);
    }

    [Fact]
    public void Zpl_profile_can_disable_postnet_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Postnet));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Postnet));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    private static IPrintProtocolProvider ZplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, bool? humanReadable = null)
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Postnet, data, barHeight, narrowWidth, 2,
            humanReadable: humanReadable), "Barcode");
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
