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

/// <summary>验证 TSPL I2OF5/I2OF5C/I2OF5G 的原生命令、Provider、模板和协议边界。</summary>
public sealed class TsplI2of5ProviderTests
{
    [Theory]
    [InlineData("12345678901", "DPI", "12345678901")]
    [InlineData("123456789016", "DPI", "12345678901")]
    [InlineData("1234567890123", "DPL", "1234567890123")]
    [InlineData("12345678901236", "DPL", "1234567890123")]
    public void Tspl_command_writes_native_german_post_payload(
        string data, string type, string payload)
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .BarcodeI2OF5G(10, 20, data, 60, 0, 0, 2, 4)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            $"BARCODE 10,20,\"{type}\",60,0,0,2,4,\"{payload}\"\r\n"), bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890")]
    [InlineData("123456789012345")]
    [InlineData("123456789017")]
    [InlineData("12345678901237")]
    [InlineData("12345678901A")]
    public void Tspl_command_rejects_invalid_i2of5g_without_polluting_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(1, 2, "before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeI2OF5G(
            10, 20, data, 40, 0, 0, 2, 4));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_profile_disabled_i2of5g_keeps_command_output_unchanged()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodeI2OF5G(
            1, 2, "12345678901", 40, 0, 0, 2, 4));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData("25", "1234", "1234")]
    [InlineData("25C", "123", "123")]
    public void Tspl_command_writes_exact_i2of5_golden_bytes(
        string type, string data, string expectedData)
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .Barcode(10, 20, type, 60, 0, 0, 2, 4, data)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            $"BARCODE 10,20,\"{type}\",60,0,0,2,4,\"{expectedData}\"\r\n"), bytes);
    }

    [Theory]
    [InlineData("25", "123", "0123")]
    [InlineData("25", "1234", "1234")]
    [InlineData("25C", "123", "123")]
    [InlineData("25C", "1234", "01234")]
    public void Tspl_command_normalizes_i2of5_input_to_the_required_parity(
        string type, string data, string expectedData)
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .Barcode(1, 2, type, 40, 1, 0, 2, 3, data)
            .Build()
            .GetBytes());

        Assert.Contains($"BARCODE 1,2,\"{type}\",40,1,0,2,3,\"{expectedData}\"\r\n", output);
    }

    [Theory]
    [InlineData("25", "")]
    [InlineData("25C", "")]
    [InlineData("25", "12A4")]
    [InlineData("25C", "12A4")]
    [InlineData("25", "1\"2")]
    [InlineData("25C", "1\"2")]
    public void Tspl_command_rejects_invalid_i2of5_data_without_polluting_output(
        string type, string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(1, 2, "before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.Barcode(
            10, 20, type, 40, 0, 0, 2, 4, data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_failed_i2of5_validation_does_not_apply_pending_media_options()
    {
        var command = new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { GapDistance = 2, GapOffset = 1 })
            .Size(60, 40, "dot");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode(
            1, 2, "25C", 40, 1, 0, 2, 4, "12A4"));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_profile_disabled_i2of5_keeps_command_output_unchanged()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.Barcode(
            1, 2, "25", 40, 0, 0, 2, 4, "1234"));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(BarcodeType.I2OF5, "1234", "25", "1234")]
    [InlineData(BarcodeType.I2OF5C, "1234", "25C", "01234")]
    [InlineData(BarcodeType.I2OF5G, "12345678901", "DPI", "12345678901")]
    [InlineData(BarcodeType.I2OF5G, "1234567890123", "DPL", "1234567890123")]
    public void Tspl_provider_renders_i2of5_with_exact_page_envelope(
        BarcodeType type, string data, string commandType, string expectedData)
    {
        var bytes = TsplProvider().Render(BarcodeDocument(type, data,
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, wideWidth: 4,
            humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "SIZE 320 dot,120 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            $"BARCODE 10,20,\"{commandType}\",40,0,0,2,4,\"{expectedData}\"\r\n" +
            "PRINT 1,1\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(BarcodeType.I2OF5, "123")]
    [InlineData(BarcodeType.I2OF5C, "1234")]
    [InlineData(BarcodeType.I2OF5G, "12345678901")]
    [InlineData(BarcodeType.I2OF5G, "1234567890123")]
    public void Tspl_provider_normalizes_i2of5_data_without_mutating_document(
        BarcodeType type, string data)
    {
        var document = BarcodeDocument(type, data, barHeight: 40, narrowWidth: 2, wideWidth: 4,
            humanReadable: false);
        var output = Encoding.ASCII.GetString(TsplProvider().Render(document)[0]);
        var expectedData = type == BarcodeType.I2OF5 ? "0123" :
            type == BarcodeType.I2OF5C ? "01234" : data;
        var expectedType = type == BarcodeType.I2OF5 ? "25" :
            type == BarcodeType.I2OF5C ? "25C" :
            data.Length <= 12 ? "DPI" : "DPL";

        Assert.Contains($"BARCODE 0,0,\"{expectedType}\",40,0,0,2,4,\"{expectedData}\"\r\n", output);
        var barcode = Assert.IsType<PrintBarcodeElement>(document.Pages[0].Elements[0]);
        Assert.Equal(data, barcode.Data);
    }

    [Theory]
    [InlineData(BarcodeType.I2OF5, "12A4")]
    [InlineData(BarcodeType.I2OF5C, "12A4")]
    [InlineData(BarcodeType.I2OF5G, "123456789017")]
    public void Tspl_provider_rejects_invalid_i2of5_data_with_element_path(
        BarcodeType type, string data)
    {
        var error = Assert.Throws<PrintElementValidationException>(() => TsplProvider().Validate(
            BarcodeDocument(type, data, barHeight: 40)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        if (type == BarcodeType.I2OF5G)
            Assert.Contains("校验位", error.Message);
        else
            Assert.Contains("ASCII", error.Message);
    }

    [Fact]
    public void Tspl_capabilities_expose_both_i2of5_variants_as_unified_and_command_barcodes()
    {
        var capabilities = TsplProvider().Capabilities;

        foreach (var type in new[] { BarcodeType.I2OF5, BarcodeType.I2OF5C, BarcodeType.I2OF5G })
        {
            Assert.True(capabilities.SupportsBarcodeType(type));
            Assert.True(capabilities.SupportsCommandBarcodeType(type));
        }
    }

    [Theory]
    [InlineData(BarcodeType.I2OF5, "1234")]
    [InlineData(BarcodeType.I2OF5C, "1234")]
    [InlineData(BarcodeType.I2OF5G, "12345678901")]
    public void Tspl_profile_can_disable_all_i2of5_variants_before_rendering(
        BarcodeType type, string data)
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(type));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(type));

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument(type, data, barHeight: 40)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(BarcodeType.I2OF5, "123", "25", "0123", RotationAngle.None, 0)]
    [InlineData(BarcodeType.I2OF5, "123", "25", "0123", RotationAngle.Rotate90, 90)]
    [InlineData(BarcodeType.I2OF5, "123", "25", "0123", RotationAngle.Rotate180, 180)]
    [InlineData(BarcodeType.I2OF5, "123", "25", "0123", RotationAngle.Rotate270, 270)]
    [InlineData(BarcodeType.I2OF5C, "1234", "25C", "01234", RotationAngle.None, 0)]
    [InlineData(BarcodeType.I2OF5C, "1234", "25C", "01234", RotationAngle.Rotate90, 90)]
    [InlineData(BarcodeType.I2OF5C, "1234", "25C", "01234", RotationAngle.Rotate180, 180)]
    [InlineData(BarcodeType.I2OF5C, "1234", "25C", "01234", RotationAngle.Rotate270, 270)]
    [InlineData(BarcodeType.I2OF5G, "12345678901", "DPI", "12345678901", RotationAngle.None, 0)]
    [InlineData(BarcodeType.I2OF5G, "12345678901", "DPI", "12345678901", RotationAngle.Rotate90, 90)]
    [InlineData(BarcodeType.I2OF5G, "12345678901", "DPI", "12345678901", RotationAngle.Rotate180, 180)]
    [InlineData(BarcodeType.I2OF5G, "12345678901", "DPI", "12345678901", RotationAngle.Rotate270, 270)]
    [InlineData(BarcodeType.I2OF5G, "1234567890123", "DPL", "1234567890123", RotationAngle.None, 0)]
    [InlineData(BarcodeType.I2OF5G, "1234567890123", "DPL", "1234567890123", RotationAngle.Rotate90, 90)]
    [InlineData(BarcodeType.I2OF5G, "1234567890123", "DPL", "1234567890123", RotationAngle.Rotate180, 180)]
    [InlineData(BarcodeType.I2OF5G, "1234567890123", "DPL", "1234567890123", RotationAngle.Rotate270, 270)]
    public void Tspl_template_renders_all_i2of5_variants_at_each_native_rotation(
        BarcodeType type, string data, string commandType, string expectedData,
        RotationAngle rotation, int encodedRotation)
    {
        var result = new TemplateRenderer().RenderWithLayout(
            I2of5Template(type, data, rotation), null,
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
            $"\"{commandType}\",40,0,{encodedRotation},2,4,\"{expectedData}\"\r\n",
            Encoding.ASCII.GetString(page.Data));
        Assert.Contains(page.Elements, element => element.Type == 4 && element.Rotation == rotation);
    }

    [Fact]
    public void I2of5_capabilities_keep_the_protocol_boundaries_explicit()
    {
        var tspl = TsplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;

        foreach (var capabilities in new[] { tspl, cpcl, escPosNative })
            Assert.True(capabilities.SupportsBarcodeType(BarcodeType.I2OF5));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.I2OF5));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.I2OF5));

        Assert.True(tspl.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.I2OF5G));
        Assert.True(cpcl.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(escPosNative.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(escPosRaster.SupportsCommandBarcodeType(BarcodeType.I2OF5C));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.I2OF5C));

        Assert.True(escPosNative.SupportsCommandBarcodeType(BarcodeType.I2OF5));
        Assert.False(escPosNative.SupportsCommandBarcodeType(BarcodeType.I2OF5C));
    }

    [Fact]
    public void Tspl_i2of5g_is_native_and_preserves_protocol_element_path_on_invalid_data()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);
        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.I2OF5G));
        Assert.True(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.I2OF5G));
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(
            BarcodeDocument(BarcodeType.I2OF5G, "123456789017", barHeight: 40)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Fact]
    public void EscPos_raster_accepts_i2of5c_while_native_and_cpcl_accept_it()
    {
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);
        raster.Validate(BarcodeDocument(BarcodeType.I2OF5C, "1234", barHeight: 40));

        PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Validate(BarcodeDocument(BarcodeType.I2OF5C, "1234", barHeight: 40));
        PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII)
            .Validate(BarcodeDocument(BarcodeType.I2OF5C, "1234", barHeight: 40));
    }

    private static IPrintProtocolProvider TsplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(BarcodeType type, string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, int wideWidth = 2,
        RotationAngle rotation = RotationAngle.None, bool? humanReadable = null,
        int reservedWidth = 0, int pageWidth = 320, int pageHeight = 120)
    {
        var document = new PrintDocument();
        var page = document.AddPage(pageWidth, pageHeight);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, type, data, barHeight, narrowWidth, wideWidth,
            rotation: rotation, humanReadable: humanReadable, reservedWidth: reservedWidth), "Barcode");
        return document;
    }

    private static BpTemplate I2of5Template(BarcodeType type, string data, RotationAngle rotation)
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
                                    NarrowModuleWidth = 2,
                                    WideModuleWidth = 4,
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
