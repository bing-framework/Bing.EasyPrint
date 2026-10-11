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

/// <summary>验证 TSPL 原生 TELEPEN/TELEPENN、统一 Provider 和协议边界。</summary>
public sealed class TsplTelepenProviderTests
{
    [Fact]
    public void Tspl_command_writes_native_telepen_bytes()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .BarcodeTelepen(10, 20, "abcd1234ABCD", 40, 0, 90, 2, 6)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "BARCODE 10,20,\"TELEPEN\",40,0,90,2,6,\"abcd1234ABCD\"\r\n"), bytes);
    }

    [Fact]
    public void Tspl_command_writes_native_telepenn_bytes()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .BarcodeTelepenn(10, 20, "1234567890", 40, 1, 0, 2, 6)
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "BARCODE 10,20,\"TELEPENN\",40,1,0,2,6,\"1234567890\"\r\n"), bytes);
    }

    [Theory]
    [InlineData("telepen", "abcd1234")]
    [InlineData("TELEPENN", "1234567890")]
    public void Tspl_generic_barcode_preserves_type_and_normalizes_data(string type, string data)
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .Barcode(1, 2, type, 30, 1, 0, 2, 4, data)
            .Build()
            .GetBytes());

        Assert.Equal($"BARCODE 1,2,\"{type}\",30,1,0,2,4,\"{data}\"\r\n", output);
    }

    [Fact]
    public void Tspl_telepen_accepts_del_character_without_encoding_replacement()
    {
        var data = "A\u007fZ";
        var output = new TsplPrintCommand(Encoding.ASCII)
            .BarcodeTelepen(1, 2, data, 30, 0, 0, 2, 4)
            .Build()
            .GetBytes();

        Assert.Equal((byte)0x7f, output[output.Length - 5]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("A\u0001B")]
    [InlineData("A\rB")]
    [InlineData("A\nB")]
    [InlineData("A\"B")]
    [InlineData("A\u001bB")]
    [InlineData("中文")]
    public void Tspl_telepen_validation_does_not_pollute_existing_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeTelepen(
            1, 2, data, 40, 0, 0, 2, 4));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123A")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901")]
    public void Tspl_telepenn_validation_does_not_pollute_existing_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeTelepenn(
            1, 2, data, 40, 0, 0, 2, 4));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(2, 4)]
    [InlineData(2, 6)]
    [InlineData(2, 5)]
    [InlineData(3, 7)]
    public void Tspl_telepen_accepts_documented_ratios(int narrow, int wide)
    {
        var command = new TsplPrintCommand(Encoding.ASCII)
            .BarcodeTelepen(1, 2, "ABC", 40, 0, 0, narrow, wide);

        Assert.NotEmpty(command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_telepen_rejects_unsupported_ratio_without_pollution()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.BarcodeTelepen(
            1, 2, "ABC", 40, 0, 0, 2, 7));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(BarcodeType.Telepen)]
    [InlineData(BarcodeType.Telepenn)]
    public void Tspl_profile_can_disable_telepen_without_polluting_existing_output(BarcodeType type)
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        var error = Assert.Throws<NotSupportedException>(() =>
            (type == BarcodeType.Telepen
                ? command.BarcodeTelepen(1, 2, "ABC", 40, 0, 0, 2, 4)
                : command.BarcodeTelepenn(1, 2, "123", 40, 0, 0, 2, 4)));

        Assert.Contains("原生条码", error.Message);
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(BarcodeType.Telepen, "12345")]
    [InlineData(BarcodeType.Telepenn, "12345")]
    public void Tspl_provider_renders_telepen_golden_output(BarcodeType type, string data)
    {
        var expectedType = type == BarcodeType.Telepen ? "TELEPEN" : "TELEPENN";
        var expected = Encoding.ASCII.GetBytes(
            "SIZE 500 dot,300 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            $"BARCODE 10,20,\"{expectedType}\",40,0,0,2,4,\"{data}\"\r\n" +
            "PRINT 1,1\r\n");

        var bytes = TsplProvider().Render(BarcodeDocument(type, data, 10, 20,
            barHeight: 40, narrowWidth: 2, wideWidth: 4,
            humanReadable: false, rotation: RotationAngle.None))[0];

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(RotationAngle.None, 10, 20)]
    [InlineData(RotationAngle.Rotate90, 50, 20)]
    [InlineData(RotationAngle.Rotate180, 458, 60)]
    [InlineData(RotationAngle.Rotate270, 10, 468)]
    public void Tspl_provider_renders_telepen_with_rotation_origin(
        RotationAngle rotation, int originX, int originY)
    {
        var bytes = TsplProvider().Render(BarcodeDocument(BarcodeType.Telepen, "12345", 10, 20,
            barHeight: 40, narrowWidth: 2, wideWidth: 4,
            humanReadable: false, rotation: rotation, pageWidth: 1000, pageHeight: 1000))[0];

        var expected = Encoding.ASCII.GetBytes(
            "SIZE 1000 dot,1000 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            $"BARCODE {originX},{originY},\"TELEPEN\",40,0,{(int)rotation},2,4,\"12345\"\r\n" +
            "PRINT 1,1\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(BarcodeType.Telepen)]
    [InlineData(BarcodeType.Telepenn)]
    public void Tspl_provider_rejects_telepen_outside_page_with_element_path(BarcodeType type)
    {
        var error = Assert.Throws<PrintElementValidationException>(() => TsplProvider().Validate(
            BarcodeDocument(type, "12345", 10, 1, barHeight: 40,
                narrowWidth: 2, wideWidth: 4, humanReadable: false,
                rotation: RotationAngle.None, pageWidth: 457, pageHeight: 120)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Telepen_capabilities_are_tspl_only()
    {
        var tspl = TsplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Telepen));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Telepenn));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Telepen));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Telepenn));
        Assert.False(cpcl.SupportsBarcodeType(BarcodeType.Telepen));
        Assert.False(cpcl.SupportsBarcodeType(BarcodeType.Telepenn));
        Assert.False(zpl.SupportsBarcodeType(BarcodeType.Telepen));
        Assert.False(zpl.SupportsBarcodeType(BarcodeType.Telepenn));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Telepen));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Telepenn));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Telepen));
        Assert.False(escPosRaster.SupportsCommandBarcodeType(BarcodeType.Telepen));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Telepenn));
        Assert.False(escPosRaster.SupportsCommandBarcodeType(BarcodeType.Telepenn));
    }

    [Theory]
    [InlineData(BarcodeType.Telepen, "ABC")]
    [InlineData(BarcodeType.Telepenn, "123")]
    public void Tspl_template_renders_telepen_and_exposes_capability(BarcodeType type, string data)
    {
        var capabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Tspl,
            Encoding = Encoding.ASCII
        });
        Assert.Contains(type, capabilities.BarcodeTypes);

        var result = new TemplateRenderer().RenderWithLayout(Template(type, data), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        var typeName = type == BarcodeType.Telepen ? "TELEPEN" : "TELEPENN";
        Assert.Contains(
            $"\"{typeName}\",40,0,0,2,4,\"{data}\"\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    private static IPrintProtocolProvider TsplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(BarcodeType type, string data, int x, int y,
        int barHeight, int narrowWidth, int wideWidth, bool humanReadable,
        RotationAngle rotation, int pageWidth = 500, int pageHeight = 300)
    {
        var document = new PrintDocument();
        var page = document.AddPage(pageWidth, pageHeight);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, type, data, barHeight,
            narrowWidth, wideWidth, rotation, humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate Template(BarcodeType type, string data)
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
                                    LineWidth = 1,
                                    Ratio = 1,
                                    NarrowModuleWidth = 2,
                                    WideModuleWidth = 4,
                                    Height = 40,
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
