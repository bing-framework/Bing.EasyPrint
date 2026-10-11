using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 ESC/POS Native Rows 模式下，条码可读文字、预留高度和模块宽度组合能够保持统一布局语义。
/// </summary>
public sealed class TemplateEscPosNativeParameterCombinationTests
{
    [Theory]
    [InlineData(BarcodeType.Code39, "ABC-12", 69)]
    [InlineData(BarcodeType.Code93, "CODE93", 72)]
    [InlineData(BarcodeType.Codabar, "A12B", 71)]
    [InlineData(BarcodeType.I2OF5, "1234", 70)]
    [InlineData(BarcodeType.UccEan128, "0101234567890128", 74)]
    public void Rows_template_maps_hri_reservation_and_native_symbology(
        BarcodeType type, string data, byte symbology)
    {
        const int narrowWidth = 3;
        const int barHeight = 40;
        const int hriReserve = 16;

        var result = new TemplateRenderer().RenderWithLayout(
            BarcodeTemplate(type, data, narrowWidth, humanReadable: true, hriReserve),
            null,
            options: Options());

        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        var page = Assert.Single(result.Pages);
        var element = Assert.Single(page.Elements
            .Where(item => item.Kind == "element" && item.Type == 4));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", element.SourcePath);
        Assert.Equal(4, element.Type);
        Assert.True(element.Width > 0);
        Assert.Equal(barHeight + hriReserve, element.Height);
        Assert.Equal(barHeight + hriReserve, page.Height);
        Assert.True(ContainsSequence(page.Data, NativeBarcode(symbology, data,
            barHeight, narrowWidth, textPosition: 2)),
            $"Native Rows 输出缺少 {type} 的 HRI、模块宽度或 GS k {symbology} payload。");
    }

    [Fact]
    public void Rows_template_explicit_module_width_changes_layout_width_and_gs_w_output()
    {
        var defaultWidth = RenderCode39(narrowWidth: 2, humanReadable: false,
            hriReserve: 0);
        var widerWidth = RenderCode39(narrowWidth: 3, humanReadable: false,
            hriReserve: 0);

        var defaultPage = Assert.Single(defaultWidth.Pages);
        var widerPage = Assert.Single(widerWidth.Pages);
        var defaultElement = Assert.Single(defaultPage.Elements
            .Where(item => item.Kind == "element" && item.Type == 4));
        var widerElement = Assert.Single(widerPage.Elements
            .Where(item => item.Kind == "element" && item.Type == 4));

        Assert.Equal(40, defaultPage.Height);
        Assert.Equal(40, widerPage.Height);
        Assert.True(widerElement.Width > defaultElement.Width);
        Assert.True(ContainsSequence(defaultPage.Data,
            NativeBarcode(69, "ABC-12", 40, 2, textPosition: 0)));
        Assert.True(ContainsSequence(widerPage.Data,
            NativeBarcode(69, "ABC-12", 40, 3, textPosition: 0)));
    }

    [Fact]
    public void Rows_template_rejects_native_module_width_below_device_range_with_element_path()
    {
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(
                BarcodeTemplate(BarcodeType.Code93, "CODE93", narrowWidth: 1,
                    humanReadable: false, hriReserve: 0),
                null,
                options: Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("ESC/POS Native", error.Message, StringComparison.Ordinal);
    }

    private static TemplateRenderResult RenderCode39(int narrowWidth, bool humanReadable, int hriReserve)
        => new TemplateRenderer().RenderWithLayout(
            BarcodeTemplate(BarcodeType.Code39, "ABC-12", narrowWidth,
                humanReadable, hriReserve),
            null,
            options: Options());

    private static TemplateRenderOptions Options() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false,
        EscPosDevice = new EscPosDeviceOptions
        {
            NativeFlow = EscPosNativeFlowMode.Rows
        }
    };

    private static BpTemplate BarcodeTemplate(BarcodeType type, string data,
        int narrowWidth, bool humanReadable, int hriReserve) => new BpTemplate
    {
        Version = 2,
        Width = 600,
        Height = 40 + hriReserve,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Section = "Body",
                Height = 40 + hriReserve,
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
                                NarrowModuleWidth = narrowWidth,
                                Width = 0,
                                Height = 40,
                                HumanReadable = humanReadable,
                                HumanReadableReserveHeight = hriReserve,
                                Field = new BgField { Value = data }
                            }
                        }
                    }
                }
            }
        }
    };

    private static byte[] NativeBarcode(byte symbology, string data, byte height,
        byte narrowWidth, byte textPosition)
    {
        var payload = Encoding.ASCII.GetBytes(data);
        return new byte[] { 0x1D, 0x48, textPosition, 0x1D, 0x68, height,
            0x1D, 0x77, narrowWidth, 0x1D, 0x6B, symbology, (byte)payload.Length }
            .Concat(payload)
            .ToArray();
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
