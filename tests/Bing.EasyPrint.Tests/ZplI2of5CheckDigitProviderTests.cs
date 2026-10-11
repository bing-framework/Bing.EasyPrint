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

/// <summary>Verifies ZPL I2OF5C command, provider, template, and protocol boundaries.</summary>
public sealed class ZplI2of5CheckDigitProviderTests
{
    [Theory]
    [InlineData(0, "N")]
    [InlineData(90, "R")]
    [InlineData(180, "I")]
    [InlineData(270, "B")]
    public void Zpl_command_writes_exact_i2of5c_bytes_for_each_rotation(
        int rotation, string orientation)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeI2OF5C(10, 20, "1234", 60, false, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^B2{orientation},60,N,N,Y^FD01234^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData("1234", "01234")]
    [InlineData("123", "123")]
    public void Zpl_command_normalizes_i2of5c_data_before_the_device_mod10_flag(
        string data, string normalized)
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeI2OF5C(10, 20, data, 60, true, 2, 0)
            .End()
            .Build()
            .GetBytes());

        Assert.Contains($"^FO10,20^BY2^B2N,60,Y,N,Y^FD{normalized}^FS\r\n", output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12A4")]
    [InlineData("1^2")]
    [InlineData("1~2")]
    public void Zpl_command_rejects_invalid_i2of5c_data_without_polluting_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeI2OF5C(
            1, 2, data, 40, false, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_command_rejects_i2of5c_rotation_when_profile_disables_it_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodeI2OF5C(
            1, 2, "1234", 40, false, 2, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_provider_renders_i2of5c_with_exact_page_envelope()
    {
        var bytes = ZplProvider().Render(BarcodeDocument("1234",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW320\r\n^LL120\r\n" +
            "^FO10,20^BY2^B2N,40,N,N,Y^FD01234^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData("1234", "01234")]
    [InlineData("123", "123")]
    public void Zpl_provider_normalizes_i2of5c_data_without_mutating_the_document(
        string data, string normalized)
    {
        var document = BarcodeDocument(data, barHeight: 40, humanReadable: false);
        var output = Encoding.ASCII.GetString(ZplProvider().Render(document)[0]);

        Assert.Contains($"^B2N,40,N,N,Y^FD{normalized}^FS\r\n", output);
        var barcode = Assert.IsType<PrintBarcodeElement>(document.Pages[0].Elements[0]);
        Assert.Equal(data, barcode.Data);
    }

    [Theory]
    [InlineData("12345678901", "123456789016", 0, "N")]
    [InlineData("12345678901", "123456789016", 90, "R")]
    [InlineData("12345678901", "123456789016", 180, "I")]
    [InlineData("12345678901", "123456789016", 270, "B")]
    [InlineData("123456789016", "123456789016", 0, "N")]
    public void Zpl_command_normalizes_i2of5g_using_the_german_post_4_9_check_digit(
        string data, string normalized, int rotation, string orientation)
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeI2OF5G(10, 20, data, 60, false, 3, rotation)
            .End()
            .Build()
            .GetBytes());

        Assert.Contains($"^FO10,20^BY3^B2{orientation},60,N,N,N^FD{normalized}^FS\r\n", output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890")]
    [InlineData("123456789017")]
    [InlineData("12345678901A")]
    public void Zpl_command_rejects_invalid_i2of5g_without_polluting_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeI2OF5G(
            1, 2, data, 40, false, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData("")]
    [InlineData("12A4")]
    [InlineData("1^2")]
    public void Zpl_provider_rejects_invalid_i2of5c_data_with_element_path(string data)
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument(data, barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("ASCII", error.Message);
    }

    [Fact]
    public void Zpl_profile_can_disable_i2of5c_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.I2OF5C));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("1234", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Fact]
    public void Zpl_profile_can_disable_i2of5g_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.I2OF5G));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.I2OF5G));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345678901", type: BarcodeType.I2OF5G)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Fact]
    public void I2of5c_capabilities_expose_zpl_tspl_and_escpos_raster()
    {
        var zpl = ZplProvider().Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(zpl.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.I2OF5C));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.I2OF5G));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.I2OF5G));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.I2OF5C));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(raster.SupportsCommandBarcodeType(BarcodeType.I2OF5C));
    }

    [Theory]
    [InlineData(PrintProtocol.EscPos)]
    public void Escpos_raster_accepts_i2of5c_during_preflight(PrintProtocol protocol)
    {
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.I2OF5C));
        provider.Validate(BarcodeDocument("1234", barHeight: 40));
    }

    [Theory]
    [InlineData(RotationAngle.None, "N")]
    [InlineData(RotationAngle.Rotate90, "R")]
    [InlineData(RotationAngle.Rotate180, "I")]
    [InlineData(RotationAngle.Rotate270, "B")]
    public void Native_template_renders_i2of5c_using_each_zpl_orientation(
        RotationAngle rotation, string orientation)
    {
        var result = new TemplateRenderer().RenderWithLayout(I2of5cTemplate("1234", rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains($"^B2{orientation},40,N,N,Y^FD01234^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    private static IPrintProtocolProvider ZplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, bool? humanReadable = null,
        BarcodeType type = BarcodeType.I2OF5C)
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, type, data, barHeight, narrowWidth, 2,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate I2of5cTemplate(string data, RotationAngle rotation)
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
                                    BarcodeType = BarcodeType.I2OF5C,
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
