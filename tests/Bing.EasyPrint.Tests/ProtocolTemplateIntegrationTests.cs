using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证模板根边框经过统一打印模型后仍由各协议 Provider 正确封装输出。
/// </summary>
public sealed class ProtocolTemplateIntegrationTests
{
    [Fact]
    public void EscPos_native_template_uses_configured_tab_column()
    {
        var template = new BpTemplate
        {
            Width = 384,
            Height = 48,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 24,
                    Cells = new List<BpCell>
                    {
                        new BpCell { Width = 80, Elements = new List<BpElement>
                            { new BpFontElement { FontSize = 24, Field = new BgField { Value = "1234567" } } } },
                        new BpCell { Width = 304, Elements = new List<BpElement>
                            { new BpFontElement { FontSize = 24, Field = new BgField { Value = "B" } } } }
                    }
                }
            }
        };

        var data = new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Native,
            EscPosDevice = new EscPosDeviceOptions { NativeTabWidthDots = 80 },
            FeedLines = 0,
            CutAfterPrint = false,
            Encoding = Encoding.ASCII
        }).Pages.Single().Data;

        Assert.Equal(new byte[] { 0x1B, 0x40, (byte)'1', (byte)'2', (byte)'3', (byte)'4',
            (byte)'5', (byte)'6', (byte)'7', 0x09, (byte)'B', 0x0A }, data);
    }

    [Fact]
    public void Tspl_template_border_is_emitted_before_print()
    {
        var result = Render(PrintProtocol.Tspl, EscPosLayoutMode.Raster, 16, 8);
        var output = Encoding.ASCII.GetString(result.Pages.Single().Data);
        var lastBorder = output.LastIndexOf("BAR ", StringComparison.Ordinal);
        var print = output.IndexOf("PRINT ", StringComparison.Ordinal);

        Assert.Equal(PrintProtocol.Tspl, result.Protocol);
        Assert.True(print >= 0, "TSPL 输出应包含 PRINT 命令。");
        Assert.True(lastBorder >= 0 && lastBorder < print,
            "根边框的最后一条 BAR 命令必须位于 PRINT 之前。");
    }

    [Fact]
    public void Zpl_template_border_is_emitted_before_end_of_label()
    {
        var result = Render(PrintProtocol.Zpl, EscPosLayoutMode.Raster, 16, 8);
        var output = Encoding.ASCII.GetString(result.Pages.Single().Data);
        var lastBorder = output.LastIndexOf("^GB", StringComparison.Ordinal);
        var end = output.IndexOf("^XZ", StringComparison.Ordinal);

        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.True(lastBorder >= 0, "ZPL 输出应包含根边框的原生线框命令。");
        Assert.True(end > lastBorder, "根边框的图形命令必须位于 ^XZ 之前。");
    }

    [Fact]
    public void EscPos_raster_template_border_is_composed_in_the_full_page_bitmap()
    {
        var result = Render(PrintProtocol.EscPos, EscPosLayoutMode.Raster, 16, 8);
        var page = result.Pages.Single();
        var payload = ReadRasterPayload(page.Data, out var rowBytes, out var height);

        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.Equal(16, page.Width);
        Assert.Equal(8, page.Height);
        Assert.Equal(2, rowBytes);
        Assert.Equal(8, height);
        Assert.Equal(16, payload.Length);

        // 根边框占据页面四边：首尾行全黑，中间行仅左右两列为黑。
        Assert.Equal(new byte[] { 0xFF, 0xFF }, payload.Take(rowBytes).ToArray());
        Assert.Equal(new byte[] { 0x80, 0x01 }, payload.Skip(rowBytes).Take(rowBytes).ToArray());
        Assert.Equal(new byte[] { 0xFF, 0xFF }, payload.Skip((height - 1) * rowBytes).Take(rowBytes).ToArray());
    }

    private static TemplateRenderResult Render(PrintProtocol protocol, EscPosLayoutMode layout, int width, int height)
    {
        var template = new BpTemplate
        {
            Width = width,
            Height = height,
            Rows = new List<BpRow>(),
            Border = new BpBorder { TopStyle = 1, RightStyle = 1, BottomStyle = 1, LeftStyle = 1 }
        };

        return new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = protocol,
            EscPosLayout = layout,
            Encoding = Encoding.ASCII
        });
    }

    private static byte[] ReadRasterPayload(byte[] data, out int rowBytes, out int height)
    {
        var command = new byte[] { 0x1D, 0x76, 0x30 };
        var index = Find(data, command);
        Assert.True(index >= 0, "ESC/POS 输出应包含 GS v 0 光栅命令。");
        Assert.True(data.Length >= index + 8, "ESC/POS 光栅头部不完整。");
        Assert.Equal(0, data[index + 3]);

        rowBytes = data[index + 4] | data[index + 5] << 8;
        height = data[index + 6] | data[index + 7] << 8;
        var length = checked(rowBytes * height);
        Assert.True(data.Length >= index + 8 + length, "ESC/POS 光栅数据不完整。");
        var payload = new byte[length];
        Array.Copy(data, index + 8, payload, 0, length);
        return payload;
    }

    private static int Find(byte[] data, byte[] pattern)
    {
        for (var index = 0; index <= data.Length - pattern.Length; index++)
        {
            var matched = true;
            for (var offset = 0; offset < pattern.Length; offset++)
                if (data[index + offset] != pattern[offset]) { matched = false; break; }
            if (matched) return index;
        }
        return -1;
    }
}
