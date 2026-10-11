using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ESC/POS Native GS1-128（UCC/EAN-128）的原生命令、模板映射和协议边界。</summary>
public sealed class EscPosUccEan128ProviderTests
{
    [Fact]
    public void Capabilities_expose_ucc_ean128_for_all_coordinate_and_raster_providers()
    {
        var native = Provider(PrintProtocol.EscPos, EscPosLayoutMode.Native).Capabilities;
        var raster = Provider(PrintProtocol.EscPos, EscPosLayoutMode.Raster).Capabilities;
        var tspl = Provider(PrintProtocol.Tspl).Capabilities;
        var zpl = Provider(PrintProtocol.Zpl).Capabilities;

        Assert.True(native.SupportsBarcodeType(BarcodeType.UccEan128));
        Assert.True(native.SupportsCommandBarcodeType(BarcodeType.UccEan128));
        Assert.True(raster.SupportsBarcodeType(BarcodeType.UccEan128));
        Assert.True(raster.SupportsCommandBarcodeType(BarcodeType.UccEan128));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.UccEan128));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.UccEan128));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.UccEan128));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.UccEan128));
    }

    [Theory]
    [InlineData("0101234567890128")]
    [InlineData("(01)9501234567890*")]
    public void Direct_ucc_ean128_uses_function_b_74_without_rewriting_business_data(string data)
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Initialize()
            .BarcodeUccEan128(data, 40, 3, 2)
            .Build()
            .GetBytes();

        Assert.Equal(ExpectedNativeBarcode(data, 40, 3, 2, 74), bytes);
    }

    [Fact]
    public void Direct_ucc_ean128_preserves_ascii_fnc1_separator()
    {
        const string data = "01\u001d10ABC";
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Initialize()
            .BarcodeUccEan128(data)
            .Build()
            .GetBytes();

        Assert.Equal(ExpectedNativeBarcode(data, 80, 2, 0, 74), bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("中文")]
    public void Direct_ucc_ean128_rejects_invalid_length_or_non_ascii_without_pollution(string data)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeUccEan128(data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Direct_ucc_ean128_rejects_payload_over_255_bytes_without_pollution()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.BarcodeUccEan128(new string('A', 256)));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Native_provider_maps_ucc_ean128_to_function_b_74()
    {
        const string data = "0101234567890128";
        var bytes = Provider(PrintProtocol.EscPos, EscPosLayoutMode.Native)
            .Render(BarcodeDocument(data))[0];

        Assert.True(ContainsSequence(bytes, ExpectedNativeBarcode(data, 40, 2, 0, 74)));
    }

    [Fact]
    public void Native_template_maps_ucc_ean128_to_function_b_74()
    {
        const string data = "0101234567890128";
        var result = new TemplateRenderer().RenderWithLayout(
            BarcodeTemplate(data), null, options: NativeOptions());

        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.True(ContainsSequence(Assert.Single(result.Pages).Data,
            ExpectedNativeBarcode(data, 40, 2, 0, 74)));
    }

    [Fact]
    public void Native_template_renders_amount_ai_payload_after_width_preflight()
    {
        const string data = "39091\u001D39108401";
        var result = new TemplateRenderer().RenderWithLayout(
            BarcodeTemplate(data), null, options: NativeOptions());

        Assert.True(ContainsSequence(Assert.Single(result.Pages).Data,
            ExpectedNativeBarcode(data, 40, 2, 0, 74)));
    }

    private static IPrintProtocolProvider Provider(PrintProtocol protocol,
        EscPosLayoutMode layout = EscPosLayoutMode.Native)
        => PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout);

    private static PrintDocument BarcodeDocument(string data)
    {
        var document = new PrintDocument();
        var page = document.AddPage(600, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(0, 0, BarcodeType.UccEan128, data, 40, 2, 2,
            humanReadable: false), "Barcode");
        return document;
    }

    private static BpTemplate BarcodeTemplate(string data) => new BpTemplate
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
                                BarcodeType = BarcodeType.UccEan128,
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

    private static TemplateRenderOptions NativeOptions() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false
    };

    private static byte[] ExpectedNativeBarcode(string data, byte height, byte width,
        byte textPosition, byte symbology)
    {
        var payload = Encoding.ASCII.GetBytes(data);
        var bytes = new byte[15 + payload.Length];
        bytes[0] = 0x1B;
        bytes[1] = 0x40;
        bytes[2] = 0x1D;
        bytes[3] = 0x48;
        bytes[4] = textPosition;
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
                if (data[start + offset] != sequence[offset])
                {
                    matched = false;
                    break;
                }
            if (matched) return true;
        }
        return false;
    }
}
