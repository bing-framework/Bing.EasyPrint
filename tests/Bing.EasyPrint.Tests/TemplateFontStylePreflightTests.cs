using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证模板文字样式位在布局前完成校验，并保持 CPCL 旧调用行为。
/// </summary>
public sealed class TemplateFontStylePreflightTests
{
    [Fact]
    public void Unknown_font_style_is_rejected_before_resource_and_data_access()
    {
        var data = new CountingData();
        var resources = new CountingResources();
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            TemplateWithRepeat(new BpFontElement
            {
                FontStyle = 8,
                Field = new BgField { Name = "Name" }
            }, Image()), data, resources, new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII
            }));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("TSPL", error.Message.ToUpperInvariant());
        Assert.Contains("未知文字样式位", error.Message);
        Assert.Equal(0, resources.OpenCount);
        Assert.Equal(0, data.EnumerationCount);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 1)]
    [InlineData(PrintProtocol.Zpl, 4)]
    public void Tspl_and_zpl_native_styles_remain_strictly_rejected(PrintProtocol protocol, int style)
    {
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(new BpFontElement
            {
                FontStyle = style,
                Field = new BgField { Value = "OK" }
            }), null, options: new TemplateRenderOptions
            {
                Protocol = protocol,
                Encoding = Encoding.ASCII
            }));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains(protocol.ToString().ToUpperInvariant(), error.Message.ToUpperInvariant());
        Assert.Contains("设备内置字体", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Explicit_rasterized_text_style_option_enables_template_output(PrintProtocol protocol)
    {
        var options = new TemplateRenderOptions
        {
            Protocol = protocol,
            Encoding = Encoding.ASCII,
            CutAfterPrint = false,
            FeedLines = 0,
            TsplMedia = protocol == PrintProtocol.Tspl
                ? new TsplMediaOptions { AllowRasterizedTextStyles = true } : null,
            ZplMedia = protocol == PrintProtocol.Zpl
                ? new ZplMediaOptions { AllowRasterizedTextStyles = true } : null
        };
        var capabilities = TemplateProtocolCapabilities.Create(options);
        Assert.True(capabilities.SupportsBoldText);
        Assert.True(capabilities.SupportsUnderlineText);
        Assert.True(capabilities.SupportsRotatedUnderlineText);

        var output = new TemplateRenderer().Render(Template(new BpFontElement
        {
            FontStyle = 5,
            Rotation = RotationAngle.Rotate90,
            Field = new BgField { Value = "OK" }
        }), null, options: options);
        var bytes = output.Pages[0].Data;
        Assert.True(ContainsSequence(bytes, protocol == PrintProtocol.Tspl
            ? Encoding.ASCII.GetBytes("BITMAP") : Encoding.ASCII.GetBytes("^GFA")));
    }

    [Fact]
    public void Cpcl_plain_render_keeps_unknown_style_as_legacy_argument_exception()
    {
        var error = Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(
            Template(new BpFontElement
            {
                FontStyle = 8,
                Field = new BgField { Value = "BAD" }
            }), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Cpcl,
                Encoding = Encoding.ASCII
            }));

        Assert.IsNotType<TemplateRenderException>(error);
        Assert.Contains("模板文字只支持", error.Message);
    }

    [Fact]
    public void Cpcl_render_with_layout_reports_unknown_style_path()
    {
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Template(new BpFontElement
            {
                FontStyle = 8,
                Field = new BgField { Value = "BAD" }
            }), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Cpcl,
                Encoding = Encoding.ASCII
            }));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains($"协议 {PrintProtocol.Cpcl}", error.Message);
        Assert.Contains("未知文字样式位", error.Message);
    }

    private static BpTemplate Template(params BpElement[] elements) => CreateTemplate(false, elements);

    private static BpTemplate TemplateWithRepeat(params BpElement[] elements) => CreateTemplate(true, elements);

    private static BpTemplate CreateTemplate(bool repeat, params BpElement[] elements) => new BpTemplate
    {
        Width = 200,
        Height = 100,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                RepeatSource = repeat ? "Items" : null,
                Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>(elements) }
                }
            }
        }
    };

    private static BpImageElement Image() => new BpImageElement
    {
        Width = 8,
        Height = 8,
        Field = new BgField { Value = "logo" }
    };

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
        {
            var match = true;
            for (var index = 0; index < sequence.Length; index++)
                if (data[start + index] != sequence[index]) { match = false; break; }
            if (match) return true;
        }
        return false;
    }

    private sealed class CountingData
    {
        public int EnumerationCount { get; private set; }
        public IEnumerable Items => new CountingEnumerable(() => EnumerationCount++);
        public string Name => "unused";
    }

    private sealed class CountingEnumerable : IEnumerable
    {
        private readonly Action onEnumerate;

        internal CountingEnumerable(Action onEnumerate) => this.onEnumerate = onEnumerate;

        public IEnumerator GetEnumerator()
        {
            onEnumerate();
            return Array.Empty<object>().GetEnumerator();
        }
    }

    private sealed class CountingResources : ITemplateResourceProvider
    {
        public int OpenCount { get; private set; }

        public Stream OpenRead(string resourceKey, System.Threading.CancellationToken cancellationToken)
        {
            OpenCount++;
            return new MemoryStream();
        }
    }
}
