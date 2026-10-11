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

/// <summary>验证统一 Code93 条码在 ESC/POS Provider 中的能力边界和编码结果。</summary>
public sealed class Code93ProviderTests
{
    [Fact]
    public void EscPos_native_unified_code93_writes_fixed_gs_k_bytes()
    {
        var bytes = NativeProvider().Render(BarcodeDocument(BarcodeType.Code93, "Code93",
            barHeight: 40, narrowWidth: 2, wideWidth: 2, humanReadable: false))[0];

        Assert.Equal(new byte[]
        {
            0x1B, 0x40,
            0x1D, 0x48, 0x00,
            0x1D, 0x68, 0x28,
            0x1D, 0x77, 0x02,
            0x1D, 0x6B, 0x48, 0x06,
            (byte)'C', (byte)'o', (byte)'d', (byte)'e', (byte)'9', (byte)'3'
        }, bytes);
    }

    [Fact]
    public void EscPos_raster_renders_unified_code93_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        var bytes = provider.Render(BarcodeDocument(BarcodeType.Code93, "Code93", barHeight: 40))[0];

        Assert.NotEmpty(bytes);
        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code93));
    }

    [Fact]
    public void Capabilities_expose_code93_for_native_escpos_and_raster()
    {
        var native = NativeProvider().Capabilities;
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(native.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.Code93));
    }

    [Fact]
    public void EscPos_native_rejects_non_ascii_code93_data_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => NativeProvider().Validate(
            BarcodeDocument(BarcodeType.Code93, "中文", barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("7-bit ASCII", error.Message);
    }

    [Fact]
    public void Native_template_renders_code93_using_gs_k_72()
    {
        var result = new TemplateRenderer().RenderWithLayout(Code93Template(), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Native,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x6B, 0x48, 0x06, (byte)'C', (byte)'o',
            (byte)'d', (byte)'e', (byte)'9', (byte)'3' }));
    }

    [Fact]
    public void Raster_template_renders_code93_with_template_element_path()
    {
        var result = new TemplateRenderer().Render(
            Code93Template(), null, options: new TemplateRenderOptions
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

    private static IPrintProtocolProvider NativeProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);

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

    private static BpTemplate Code93Template() => new BpTemplate
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
                                BarcodeType = BarcodeType.Code93,
                                LineWidth = 1,
                                Ratio = 2,
                                Height = 40,
                                Width = 500,
                                Field = new BgField { Value = "Code93" }
                            }
                        }
                    }
                }
            }
        }
    };

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < sequence.Length; offset++)
                if (data[start + offset] != sequence[offset]) { matched = false; break; }
            if (matched) return true;
        }
        return false;
    }
}
