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

/// <summary>验证 ESC/POS Native 下划线的命令、统一模型和模板输出契约。</summary>
public sealed class EscPosNativeTextStyleTests
{
    [Fact]
    public void Native_capabilities_advertise_underline_text()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);

        Assert.True(command.Capabilities.SupportsUnderlineText);
        Assert.True(provider.Capabilities.SupportsUnderlineText);
    }

    [Fact]
    public void Native_command_emits_exact_esc_minus_toggle_bytes()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native)
            .Initialize()
            .Underline()
            .WriteText("A")
            .Underline(false)
            .WriteText("B")
            .Build()
            .GetBytes();

        Assert.Equal(new byte[]
        {
            0x1B, 0x40,
            0x1B, 0x2D, 0x01, (byte)'A',
            0x1B, 0x2D, 0x00, (byte)'B'
        }, bytes);
    }

    [Fact]
    public void Native_provider_wraps_underlined_text_with_enable_and_disable_bytes()
    {
        var document = new PrintDocument();
        var page = document.AddPage(384, 48);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintTextElement(0, 0, "A", 24, underline: true));
        page.Add(new PrintTextElement(0, 24, "B", 24));

        var bytes = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Render(document)[0];

        Assert.Equal(new byte[]
        {
            0x1B, 0x40,
            0x1B, 0x2D, 0x01, (byte)'A',
            0x1B, 0x2D, 0x00, 0x0A,
            (byte)'B', 0x0A
        }, bytes);
    }

    [Fact]
    public void Native_template_font_style_4_passes_preflight_and_emits_underline_bytes()
    {
        var options = NativeOptions();
        var capabilities = TemplateProtocolCapabilities.Create(options);
        Assert.True(capabilities.SupportsUnderlineText);

        var result = new TemplateRenderer().RenderWithLayout(UnderlinedTemplate(), null,
            options: options);
        var page = Assert.Single(result.Pages);

        Assert.True(ContainsSequence(page.Data, new byte[]
        {
            0x1B, 0x2D, 0x01, (byte)'A',
            0x1B, 0x2D, 0x00, 0x0A
        }));
    }

    private static TemplateRenderOptions NativeOptions() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false
    };

    private static BpTemplate UnderlinedTemplate() => new BpTemplate
    {
        Width = 384,
        Height = 64,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Elements = new List<BpElement>
                        {
                            new BpFontElement
                            {
                                FontSize = 24,
                                FontStyle = 4,
                                Field = new BgField { Value = "A" }
                            }
                        }
                    }
                }
            }
        }
    };

    private static bool ContainsSequence(byte[] value, byte[] sequence)
    {
        for (var start = 0; start <= value.Length - sequence.Length; start++)
        {
            var match = true;
            for (var index = 0; index < sequence.Length; index++)
                if (value[start + index] != sequence[index])
                {
                    match = false;
                    break;
                }
            if (match) return true;
        }
        return false;
    }
}
