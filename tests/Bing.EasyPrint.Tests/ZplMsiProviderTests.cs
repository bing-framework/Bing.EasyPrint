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

/// <summary>验证 ZPL MSI 原生命令、统一 Provider 和跨协议能力边界。</summary>
public sealed class ZplMsiProviderTests
{
    [Theory]
    [InlineData(0, "N")]
    [InlineData(90, "R")]
    [InlineData(180, "I")]
    [InlineData(270, "B")]
    public void Zpl_command_writes_exact_msi_bytes_for_each_rotation(
        int rotation, string orientation)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeMsi(10, 20, "12345", 60, false, 3, 'A', false, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^BM{orientation},A,60,N,N,N^FD12345^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData('A', "^BMN,A,60,Y,N")]
    [InlineData('B', "^BMN,B,60,Y,N")]
    [InlineData('C', "^BMN,C,60,Y,N")]
    [InlineData('D', "^BMN,D,60,Y,N")]
    public void Zpl_command_writes_msi_check_digit_and_text_flags(
        char checkDigit, string command)
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeMsi(10, 20, "12345", 60, true, 3, checkDigit, false, 0)
            .End()
            .Build()
            .GetBytes());

        Assert.Contains($"^FO10,20^BY3{command},N^FD12345^FS\r\n", output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123A")]
    [InlineData("123^4")]
    [InlineData("123~4")]
    [InlineData("中文")]
    public void Zpl_command_rejects_non_numeric_msi_data_without_polluting_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeMsi(
            1, 2, data, 40, false, 2, 'A', false, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData('A', "12345678901234")]
    [InlineData('B', "123456789012345")]
    [InlineData('C', "123456789012345")]
    [InlineData('D', "123456789012345")]
    [InlineData('E', "12345")]
    public void Zpl_command_rejects_invalid_msi_length_or_check_mode_without_polluting_output(
        char checkDigit, string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeMsi(
            1, 2, data, 40, false, 2, checkDigit, false, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(32001, 2)]
    [InlineData(60, 0)]
    [InlineData(60, 11)]
    public void Zpl_command_rejects_invalid_msi_dimensions_without_polluting_output(
        int height, int moduleWidth)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.BarcodeMsi(
            1, 2, "12345", height, false, moduleWidth, 'A', false, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_command_rejects_rotation_when_profile_disables_it_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodeMsi(
            1, 2, "12345", 40, false, 2, 'A', false, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_and_tspl_capabilities_expose_msi_among_declared_native_protocols()
    {
        var zpl = ZplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Msi));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.Msi));
        Assert.True(cpcl.SupportsBarcodeType(BarcodeType.Msi));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Msi));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Msi));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Msi));
        Assert.False(escPosNative.SupportsCommandBarcodeType(BarcodeType.Msi));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Msi));
        Assert.True(escPosRaster.SupportsCommandBarcodeType(BarcodeType.Msi));
    }

    [Fact]
    public void Zpl_provider_renders_msi_with_exact_page_envelope()
    {
        var bytes = ZplProvider().Render(BarcodeDocument("12345",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW320\r\n^LL120\r\n" +
            "^FO10,20^BY2^BMN,A,40,N,N,N^FD12345^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_provider_rejects_invalid_msi_data_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument("12345678901234", barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("MSI", error.Message);
    }

    [Theory]
    [InlineData(RotationAngle.None, "N")]
    [InlineData(RotationAngle.Rotate90, "R")]
    [InlineData(RotationAngle.Rotate180, "I")]
    [InlineData(RotationAngle.Rotate270, "B")]
    public void Native_template_renders_msi_using_each_zpl_orientation(
        RotationAngle rotation, string orientation)
    {
        var result = new TemplateRenderer().RenderWithLayout(MsiTemplate("12345", rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains($"^BM{orientation},A,40,N,N,N^FD12345^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void EscPos_rejects_msi_before_rendering()
    {
        const PrintProtocol protocol = PrintProtocol.EscPos;
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Native);

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Msi));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345", barHeight: 40)));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.NotEmpty(error.Message);
    }

    [Fact]
    public void EscPos_raster_accepts_msi_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Msi));
        provider.Validate(BarcodeDocument("12345", barHeight: 40));
        Assert.NotEmpty(provider.Render(BarcodeDocument("12345", barHeight: 40))[0]);
    }

    [Fact]
    public void Zpl_profile_can_disable_msi_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Msi));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Msi));
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
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Msi, data, barHeight, narrowWidth, 2,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate MsiTemplate(string data, RotationAngle rotation)
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
                                    BarcodeType = BarcodeType.Msi,
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
