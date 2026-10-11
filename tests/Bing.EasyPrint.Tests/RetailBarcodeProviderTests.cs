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

/// <summary>验证 ESC/POS Native 零售条码的统一映射、能力边界和模板规范化。</summary>
public sealed class RetailBarcodeProviderTests
{
    [Theory]
    [InlineData(BarcodeType.Ean8, "96385074", 68)]
    [InlineData(BarcodeType.Ean13, "5901234123457", 67)]
    [InlineData(BarcodeType.UpcA, "036000291452", 65)]
    [InlineData(BarcodeType.UpcE, "0425261", 66)]
    public void EscPos_native_retail_barcode_writes_fixed_gs_k_bytes(
        BarcodeType type, string data, byte symbology)
    {
        var bytes = NativeProvider().Render(BarcodeDocument(type, data,
            barHeight: 40, narrowWidth: 2, wideWidth: 2, humanReadable: false))[0];

        Assert.Equal(ExpectedNativeBarcode(symbology, data, 40, 2), bytes);
    }

    [Theory]
    [InlineData(BarcodeType.Ean8)]
    [InlineData(BarcodeType.Ean13)]
    [InlineData(BarcodeType.UpcA)]
    [InlineData(BarcodeType.UpcE)]
    public void Capabilities_expose_retail_barcode_for_native_and_raster_escpos(BarcodeType type)
    {
        var native = NativeProvider().Capabilities;
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(native.SupportsBarcodeType(type));
        Assert.True(native.SupportsCommandBarcodeType(type));
        Assert.True(raster.SupportsBarcodeType(type));
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "9638507A")]
    [InlineData(BarcodeType.Ean13, "59012341234")]
    [InlineData(BarcodeType.UpcA, "0360002914")]
    [InlineData(BarcodeType.UpcE, "1425261")]
    public void EscPos_native_rejects_invalid_retail_data_with_element_path(BarcodeType type, string data)
    {
        var error = Assert.Throws<PrintElementValidationException>(() => NativeProvider().Validate(
            BarcodeDocument(type, data, barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.NotEmpty(error.Message);
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "9638507", "96385074", 68)]
    [InlineData(BarcodeType.Ean13, "590123412345", "5901234123457", 67)]
    [InlineData(BarcodeType.UpcA, "03600029145", "036000291452", 65)]
    [InlineData(BarcodeType.UpcE, "425261", "0425261", 66)]
    public void Native_template_normalizes_retail_data_before_gs_k_output(
        BarcodeType type, string input, string normalized, byte symbology)
    {
        var result = new TemplateRenderer().RenderWithLayout(RetailTemplate(type, input), null,
            options: NativeOptions());

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.True(ContainsSequence(page.Data, ExpectedNativeBarcode(symbology, normalized, 40, 2)));
    }

    [Theory]
    [InlineData(BarcodeType.Ean8, "96385074")]
    [InlineData(BarcodeType.Ean13, "5901234123457")]
    [InlineData(BarcodeType.UpcA, "036000291452")]
    [InlineData(BarcodeType.UpcE, "0425261")]
    public void Raster_template_renders_retail_barcode_with_template_element_path(
        BarcodeType type, string data)
    {
        var result = new TemplateRenderer().Render(
            RetailTemplate(type, data), null, options: new TemplateRenderOptions
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

    [Fact]
    public void Native_template_rejects_wrong_retail_check_digit_before_output()
    {
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            RetailTemplate(BarcodeType.Ean8, "96385075"), null, options: NativeOptions()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("校验", error.Message);
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

    private static BpTemplate RetailTemplate(BarcodeType type, string data) => new BpTemplate
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
