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

/// <summary>验证 ESC/POS Native Codabar 与 I2OF5 的统一映射、校验和模板行为。</summary>
public sealed class CodabarI2of5ProviderTests
{
    [Theory]
    [InlineData(BarcodeType.Codabar, "A12B", 71)]
    [InlineData(BarcodeType.I2OF5, "1234", 70)]
    public void EscPos_native_barcode_writes_fixed_gs_k_bytes(
        BarcodeType type, string data, byte symbology)
    {
        var bytes = NativeProvider().Render(BarcodeDocument(type, data,
            barHeight: 40, narrowWidth: 2, wideWidth: 2, humanReadable: false))[0];

        Assert.Equal(ExpectedNativeBarcode(symbology, data, 40, 2), bytes);
    }

    [Theory]
    [InlineData(BarcodeType.Codabar)]
    [InlineData(BarcodeType.I2OF5)]
    public void Capabilities_expose_barcode_for_native_and_raster_escpos(BarcodeType type)
    {
        var native = NativeProvider().Capabilities;
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(native.SupportsBarcodeType(type));
        Assert.True(native.SupportsCommandBarcodeType(type));
        Assert.True(raster.SupportsBarcodeType(type));
        // Raster 使用主机编码器，Native 仍保留 GS k 命令能力。
        Assert.True(raster.SupportsCommandBarcodeType(type));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("A12")]
    [InlineData("12B")]
    public void EscPos_native_rejects_codabar_without_both_guard_characters(string data)
    {
        var error = Assert.Throws<PrintElementValidationException>(() => NativeProvider().Validate(
            BarcodeDocument(BarcodeType.Codabar, data, barHeight: 40)));

        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("起止", error.Message);
    }

    [Fact]
    public void EscPos_native_rejects_odd_i2of5_data_before_output()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => NativeProvider().Validate(
            BarcodeDocument(BarcodeType.I2OF5, "123", barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("偶数", error.Message);
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12中B")]
    [InlineData(BarcodeType.I2OF5, "12中34")]
    public void EscPos_native_rejects_non_ascii_barcode_data_with_element_path(
        BarcodeType type, string data)
    {
        var error = Assert.Throws<PrintElementValidationException>(() => NativeProvider().Validate(
            BarcodeDocument(type, data, barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("7-bit ASCII", error.Message);
    }

    [Fact]
    public void Native_template_renders_codabar_using_gs_k_71()
    {
        var result = new TemplateRenderer().RenderWithLayout(CodabarTemplate("A12B"), null,
            options: NativeOptions());

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.True(ContainsSequence(page.Data, ExpectedNativeBarcode(71, "A12B", 40, 2)));
    }

    [Fact]
    public void Native_template_normalizes_odd_i2of5_to_leading_zero()
    {
        var template = BarcodeTemplate(BarcodeType.I2OF5, "123");
        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: NativeOptions());

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.True(ContainsSequence(page.Data, ExpectedNativeBarcode(70, "0123", 40, 2)));
        Assert.Equal("123", template.Rows[0].Cells[0].Elements[0] is BpBarcodeElement barcode
            ? barcode.Field.Value
            : null);
    }

    [Theory]
    [InlineData(BarcodeType.Codabar, "A12B")]
    [InlineData(BarcodeType.I2OF5, "1234")]
    public void Raster_template_renders_barcode_with_template_element_path(
        BarcodeType type, string data)
    {
        var result = new TemplateRenderer().Render(
            BarcodeTemplate(type, data), null, options: RasterOptions());

        Assert.Single(result.Pages);
        Assert.NotEmpty(result.Pages[0].Data);
    }

    [Fact]
    public void EscPos_native_rejects_unsupported_codabar_character_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => NativeProvider().Validate(
            BarcodeDocument(BarcodeType.Codabar, "A12*B", barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("不支持", error.Message);
    }

    private static IPrintProtocolProvider NativeProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);

    private static TemplateRenderOptions NativeOptions() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false
    };

    private static TemplateRenderOptions RasterOptions() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Raster,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false
    };

    private static PrintDocument BarcodeDocument(BarcodeType type, string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, int wideWidth = 2, bool? humanReadable = null)
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, type, data, barHeight, narrowWidth, wideWidth,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate CodabarTemplate(string data) => BarcodeTemplate(BarcodeType.Codabar, data);

    private static BpTemplate BarcodeTemplate(BarcodeType type, string data) => new BpTemplate
    {
        Version = 1,
        Width = 600,
        Height = 120,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Height = 120,
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

    private static byte[] ExpectedNativeBarcode(byte symbology, string data, byte height, byte width)
    {
        var payload = Encoding.ASCII.GetBytes(data);
        var bytes = new byte[15 + payload.Length];
        bytes[0] = 0x1B;
        bytes[1] = 0x40;
        bytes[2] = 0x1D;
        bytes[3] = 0x48;
        bytes[4] = 0x00;
        bytes[5] = 0x1D;
        bytes[6] = 0x68;
        bytes[7] = height;
        bytes[8] = 0x1D;
        bytes[9] = 0x77;
        bytes[10] = width;
        bytes[11] = 0x1D;
        bytes[12] = 0x6B;
        bytes[13] = symbology;
        bytes[14] = (byte)payload.Length;
        Array.Copy(payload, 0, bytes, 15, payload.Length);
        return bytes;
    }

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < sequence.Length; offset++)
            {
                if (data[start + offset] == sequence[offset]) continue;
                matched = false;
                break;
            }
            if (matched) return true;
        }
        return false;
    }
}
