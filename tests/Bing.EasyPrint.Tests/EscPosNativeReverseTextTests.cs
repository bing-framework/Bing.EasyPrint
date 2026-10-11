using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ESC/POS Native 反白文字的能力、命令字节和统一文档输出契约。</summary>
public sealed class EscPosNativeReverseTextTests
{
    [Fact]
    public void Native_capabilities_advertise_reverse_text()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);

        Assert.True(command.Capabilities.SupportsReverseText);
        Assert.True(provider.Capabilities.SupportsReverseText);
    }

    [Fact]
    public void Native_command_emits_exact_gs_b_toggle_bytes()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native)
            .Initialize()
            .Reverse()
            .WriteText("A")
            .Reverse(false)
            .WriteText("B")
            .Build()
            .GetBytes();

        Assert.Equal(new byte[]
        {
            0x1B, 0x40,
            0x1D, 0x42, 0x01, (byte)'A',
            0x1D, 0x42, 0x00, (byte)'B'
        }, bytes);
    }

    [Fact]
    public void Native_provider_wraps_reversed_text_with_enable_and_disable_bytes()
    {
        var document = new PrintDocument();
        var page = document.AddPage(384, 72);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintTextElement(0, 0, "A", 24, reverse: true));
        page.Add(new PrintTextElement(0, 24, "B", 24));
        page.Add(new PrintTextElement(0, 48, "C", 24, reverse: true));

        var bytes = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Render(document)[0];

        Assert.Equal(new byte[]
        {
            0x1B, 0x40,
            0x1D, 0x42, 0x01, (byte)'A',
            0x1D, 0x42, 0x00, 0x0A,
            (byte)'B', 0x0A,
            0x1D, 0x42, 0x01, (byte)'C',
            0x1D, 0x42, 0x00, 0x0A
        }, bytes);
    }

    [Fact]
    public void Native_provider_preserves_reverse_state_when_text_styles_are_combined()
    {
        var document = new PrintDocument();
        var page = document.AddPage(384, 48);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintTextElement(0, 0, "A", 24, bold: true, reverse: true));
        page.Add(new PrintTextElement(0, 24, "B", 24, underline: true));

        var bytes = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Render(document)[0];

        Assert.Equal(new byte[]
        {
            0x1B, 0x40,
            0x1B, 0x45, 0x01,
            0x1D, 0x42, 0x01, (byte)'A',
            0x1D, 0x42, 0x00,
            0x1B, 0x45, 0x00, 0x0A,
            0x1B, 0x2D, 0x01, (byte)'B',
            0x1B, 0x2D, 0x00, 0x0A
        }, bytes);
    }

    [Fact]
    public void Native_template_reverse_passes_preflight_and_emits_gs_b_bytes()
    {
        var options = NativeTemplateOptions();
        var capabilities = TemplateProtocolCapabilities.Create(options);
        Assert.True(capabilities.SupportsReverseText);

        var result = new TemplateRenderer().RenderWithLayout(ReverseTemplate(), null, options: options);
        var page = Assert.Single(result.Pages);

        Assert.True(ContainsSequence(page.Data, new byte[]
        {
            0x1D, 0x42, 0x01, (byte)'A',
            0x1D, 0x42, 0x00, 0x0A
        }));
    }

    [Fact]
    public void Raster_template_reverse_is_rejected_during_preflight()
    {
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            ReverseTemplate(), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            }));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("反白", error.Message);
    }

    private static TemplateRenderOptions NativeTemplateOptions() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false
    };

    private static BpTemplate ReverseTemplate() => new BpTemplate
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
                                Reverse = true,
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
