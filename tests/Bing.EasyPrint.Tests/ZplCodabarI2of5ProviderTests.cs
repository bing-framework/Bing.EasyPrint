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

/// <summary>验证 ZPL Codabar 与 Interleaved 2 of 5 的原生命令、Provider 和模板能力边界。</summary>
public sealed class ZplCodabarI2of5ProviderTests
{
    [Theory]
    [InlineData(0, "N", false, "N")]
    [InlineData(90, "R", false, "N")]
    [InlineData(180, "I", false, "N")]
    [InlineData(270, "B", false, "N")]
    [InlineData(0, "N", true, "Y")]
    public void Zpl_command_writes_exact_codabar_bytes_for_each_rotation_and_text_flag(
        int rotation, string orientation, bool printText, string textFlag)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeCodabar(10, 20, "A12B", 60, printText, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^BK{orientation},N,60,{textFlag},N,A,B^FD12^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(0, "N", false, "N", false, "N")]
    [InlineData(90, "R", false, "N", false, "N")]
    [InlineData(180, "I", false, "N", false, "N")]
    [InlineData(270, "B", false, "N", false, "N")]
    [InlineData(0, "N", true, "Y", true, "Y")]
    public void Zpl_command_writes_exact_i2of5_bytes_for_each_rotation_and_flags(
        int rotation, string orientation, bool printText, string textFlag,
        bool printCheckDigit, string checkDigitFlag)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeI2OF5(10, 20, "1234", 60, printText, 3, printCheckDigit, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^B2{orientation},60,{textFlag},N,{checkDigitFlag}^FD1234^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "")]
    [InlineData(BarcodeType.Codabar, "123")]
    [InlineData(BarcodeType.Codabar, "A12")]
    [InlineData(BarcodeType.Codabar, "A12*B")]
    [InlineData(BarcodeType.I2OF5, "")]
    [InlineData(BarcodeType.I2OF5, "123")]
    [InlineData(BarcodeType.I2OF5, "12A4")]
    public void Zpl_command_rejects_invalid_data_without_polluting_output(
        BarcodeType type, string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => AddBarcode(command, type, data,
            1, 2, 40, false, 2, false, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12B")]
    [InlineData(BarcodeType.I2OF5, "1234")]
    public void Zpl_command_rejects_invalid_dimensions_without_polluting_output(
        BarcodeType type, string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => AddBarcode(command, type, data,
            1, 2, 0, false, 2, false, 0));
        Assert.Equal(before, command.Build().GetBytes());

        Assert.Throws<ArgumentOutOfRangeException>(() => AddBarcode(command, type, data,
            1, 2, 32001, false, 2, false, 0));
        Assert.Equal(before, command.Build().GetBytes());

        Assert.Throws<ArgumentOutOfRangeException>(() => AddBarcode(command, type, data,
            1, 2, 40, false, 0, false, 0));
        Assert.Equal(before, command.Build().GetBytes());

        Assert.Throws<ArgumentOutOfRangeException>(() => AddBarcode(command, type, data,
            1, 2, 40, false, 11, false, 0));
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12B")]
    [InlineData(BarcodeType.I2OF5, "1234")]
    public void Zpl_command_rejects_rotation_when_profile_disables_it_without_polluting_output(
        BarcodeType type, string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => AddBarcode(command, type, data,
            1, 2, 40, false, 2, false, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12B")]
    [InlineData(BarcodeType.I2OF5, "1234")]
    public void Zpl_command_rejects_barcode_when_profile_disables_it_without_polluting_output(
        BarcodeType type, string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => AddBarcode(command, type, data,
            1, 2, 40, false, 2, false, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_and_raster_capabilities_expose_codabar_and_i2of5()
    {
        var zpl = ZplProvider().Capabilities;
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;

        foreach (var type in new[] { BarcodeType.Codabar, BarcodeType.I2OF5 })
        {
            Assert.True(zpl.SupportsBarcodeType(type));
            Assert.True(zpl.SupportsCommandBarcodeType(type));
            Assert.True(raster.SupportsBarcodeType(type));
        }

        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Codabar));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.I2OF5));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.I2OF5G));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.I2OF5G));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.I2OF5G));
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12B", "^BKN,N,40,N,N,A,B^FD12^FS\r\n")]
    [InlineData(BarcodeType.I2OF5, "1234", "^B2N,40,N,N,N^FD1234^FS\r\n")]
    public void Zpl_provider_renders_barcode_with_exact_page_envelope(
        BarcodeType type, string data, string barcodeCommand)
    {
        var bytes = ZplProvider().Render(BarcodeDocument(type, data,
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW320\r\n^LL120\r\n" +
            $"^FO10,20^BY2{barcodeCommand}" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12*B", "Codabar")]
    [InlineData(BarcodeType.I2OF5, "123", "偶数")]
    public void Zpl_provider_rejects_invalid_data_with_element_path(
        BarcodeType type, string data, string messageFragment)
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument(type, data, barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains(messageFragment, error.Message);
    }

    [Theory]
    [InlineData(BarcodeType.Codabar)]
    [InlineData(BarcodeType.I2OF5)]
    public void Zpl_profile_can_disable_codabar_and_i2of5_before_rendering(BarcodeType type)
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(type));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(type));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument(type, type == BarcodeType.Codabar ? "A12B" : "1234", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12B", RotationAngle.None, "N", "^BKN,N,40,N,N,A,B^FD12^FS\r\n")]
    [InlineData(BarcodeType.Codabar, "A12B", RotationAngle.Rotate90, "R", "^BKR,N,40,N,N,A,B^FD12^FS\r\n")]
    [InlineData(BarcodeType.Codabar, "A12B", RotationAngle.Rotate180, "I", "^BKI,N,40,N,N,A,B^FD12^FS\r\n")]
    [InlineData(BarcodeType.Codabar, "A12B", RotationAngle.Rotate270, "B", "^BKB,N,40,N,N,A,B^FD12^FS\r\n")]
    [InlineData(BarcodeType.I2OF5, "123", RotationAngle.None, "N", "^B2N,40,N,N,N^FD0123^FS\r\n")]
    [InlineData(BarcodeType.I2OF5, "123", RotationAngle.Rotate90, "R", "^B2R,40,N,N,N^FD0123^FS\r\n")]
    [InlineData(BarcodeType.I2OF5, "123", RotationAngle.Rotate180, "I", "^B2I,40,N,N,N^FD0123^FS\r\n")]
    [InlineData(BarcodeType.I2OF5, "123", RotationAngle.Rotate270, "B", "^B2B,40,N,N,N^FD0123^FS\r\n")]
    public void Native_template_renders_each_barcode_using_each_zpl_orientation(
        BarcodeType type, string data, RotationAngle rotation, string orientation, string barcodeCommand)
    {
        var result = new TemplateRenderer().RenderWithLayout(BarcodeTemplate(type, data, rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains(barcodeCommand, Encoding.ASCII.GetString(page.Data));
        Assert.Contains(orientation, barcodeCommand);
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12B")]
    [InlineData(BarcodeType.I2OF5, "1234")]
    public void EscPos_raster_renders_zpl_native_barcode_with_element_path(
        BarcodeType type, string data)
    {
        var provider = PrintProtocolProviderFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster);
        var bytes = provider.Render(BarcodeDocument(type, data, barHeight: 40))[0];

        Assert.NotEmpty(bytes);
        Assert.True(provider.Capabilities.SupportsBarcodeType(type));
    }

    [Fact]
    public void Tspl_declares_i2of5_and_renders_native_barcode()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.I2OF5));
        var bytes = provider.Render(BarcodeDocument(BarcodeType.I2OF5, "1234", barHeight: 40))[0];

        Assert.Contains("BARCODE 0,0,\"25\",40,1,0,2,2,\"1234\"\r\n",
            Encoding.ASCII.GetString(bytes));
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12B")]
    [InlineData(BarcodeType.I2OF5, "1234")]
    public void Raster_template_renders_barcode_with_template_element_path(
        BarcodeType type, string data)
    {
        var result = new TemplateRenderer().Render(
            BarcodeTemplate(type, data), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        Assert.Single(result.Pages);
        Assert.NotEmpty(result.Pages[0].Data);
    }

    private static ZplPrintCommand AddBarcode(ZplPrintCommand command, BarcodeType type, string data,
        int x, int y, int height, bool printText, int moduleWidth, bool printCheckDigit, int rotation)
    {
        switch (type)
        {
            case BarcodeType.Codabar:
                return command.BarcodeCodabar(x, y, data, height, printText, moduleWidth, rotation);
            case BarcodeType.I2OF5:
                return command.BarcodeI2OF5(x, y, data, height, printText, moduleWidth,
                    printCheckDigit, rotation);
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
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
