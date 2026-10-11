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

/// <summary>验证 ESC/POS I2OF5C/I2OF5G 的校验位归一化及协议边界。</summary>
public sealed class EscPosI2of5CheckDigitTests
{
    [Fact]
    public void Capabilities_expose_i2of5c_and_i2of5g_for_escpos_native_and_raster_layouts()
    {
        var native = Provider(PrintProtocol.EscPos, EscPosLayoutMode.Native).Capabilities;
        var raster = Provider(PrintProtocol.EscPos, EscPosLayoutMode.Raster).Capabilities;
        var tspl = Provider(PrintProtocol.Tspl).Capabilities;
        var zpl = Provider(PrintProtocol.Zpl).Capabilities;

        Assert.True(native.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(native.SupportsBarcodeType(BarcodeType.I2OF5G));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.I2OF5G));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.I2OF5G));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.I2OF5C));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.I2OF5G));
    }

    [Theory]
    [InlineData("123", "1236")]
    [InlineData("1234", "012348")]
    public void Native_i2of5c_normalizes_payload_with_mod10_3_1_and_writes_gs_k_70(
        string input, string normalized)
    {
        var bytes = Provider(PrintProtocol.EscPos, EscPosLayoutMode.Native)
            .Render(BarcodeDocument(BarcodeType.I2OF5C, input))[0];

        Assert.Equal(ExpectedNativeBarcode(normalized), bytes);
    }

    [Theory]
    [InlineData("12345678901", "123456789016")]
    [InlineData("123456789016", "123456789016")]
    [InlineData("1234567890123", "12345678901236")]
    [InlineData("12345678901236", "12345678901236")]
    public void Native_i2of5g_accepts_11_to_14_digits_and_applies_mod10_4_9(
        string input, string normalized)
    {
        var bytes = Provider(PrintProtocol.EscPos, EscPosLayoutMode.Native)
            .Render(BarcodeDocument(BarcodeType.I2OF5G, input))[0];

        Assert.Equal(ExpectedNativeBarcode(normalized), bytes);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012345")]
    [InlineData("123456789017")]
    [InlineData("12345678901237")]
    public void Native_i2of5g_rejects_out_of_range_or_invalid_check_digit_without_output(string data)
    {
        var provider = Provider(PrintProtocol.EscPos, EscPosLayoutMode.Native);
        var error = Assert.Throws<PrintElementValidationException>(() =>
            provider.Validate(BarcodeDocument(BarcodeType.I2OF5G, data)));

        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("I2OF5G", error.Message);
    }

    [Theory]
    [InlineData(BarcodeType.I2OF5C, "1234", "012348")]
    [InlineData(BarcodeType.I2OF5G, "12345678901", "123456789016")]
    public void Native_template_writes_normalized_i2of5_data_as_gs_k_70(
        BarcodeType type, string input, string normalized)
    {
        var result = new TemplateRenderer().RenderWithLayout(
            BarcodeTemplate(type, input), null, options: NativeOptions());

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.True(ContainsSequence(page.Data, ExpectedNativeBarcode(normalized)));
    }

    [Theory]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    public void Zpl_i2of5g_is_rendered_during_template_preflight(
        PrintProtocol protocol, EscPosLayoutMode layout)
    {
        var result = new TemplateRenderer().RenderWithLayout(
            BarcodeTemplate(BarcodeType.I2OF5G, "12345678901"), null,
            options: new TemplateRenderOptions
            {
                Protocol = protocol,
                EscPosLayout = layout,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Contains("^B2N,40,N,N,N^FD123456789016^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    private static IPrintProtocolProvider Provider(PrintProtocol protocol,
        EscPosLayoutMode layout = EscPosLayoutMode.Native)
        => PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout);

    private static TemplateRenderOptions NativeOptions() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false
    };

    private static PrintDocument BarcodeDocument(BarcodeType type, string data)
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(0, 0, type, data, 40, 2, 2,
            humanReadable: false), "Barcode");
        return document;
    }

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

    private static byte[] ExpectedNativeBarcode(string data)
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
        bytes[7] = 40;
        bytes[8] = 0x1D;
        bytes[9] = 0x77;
        bytes[10] = 2;
        bytes[11] = 0x1D;
        bytes[12] = 0x6B;
        bytes[13] = 70;
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
