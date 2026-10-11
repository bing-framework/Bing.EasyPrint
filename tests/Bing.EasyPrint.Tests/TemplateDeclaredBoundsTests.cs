using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TemplateDeclaredBoundsTests
{
    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Strict_template_requires_measured_text_metrics(PrintProtocol protocol)
    {
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().Render(TextTemplate("AB"), null,
                options: Options(protocol)));

        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("DeviceLineHeightAt24", error.Message);
        Assert.Contains(protocol.ToString(), error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Strict_template_uses_declared_text_metrics_for_layout(PrintProtocol protocol)
    {
        var options = Options(protocol);
        options.DeviceHalfCellWidthAt24 = 10;
        options.DeviceFullCellWidthAt24 = 20;
        options.DeviceLineHeightAt24 = 30;

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            TextTemplate("AB"), null, options: options).Pages);
        var text = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal(20, text.Width);
        Assert.Equal(30, text.Height);
    }

    [Fact]
    public void Strict_template_accepts_tspl_fixed_ascii_font_without_calibration()
    {
        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            TextTemplate("AB", "1"), null, options: Options(PrintProtocol.Tspl)).Pages);

        Assert.Single(page.Elements.Where(element => element.Type == 1));
    }

    [Fact]
    public void Tspl_fixed_font_calibration_cannot_shrink_printed_cell_height()
    {
        var options = Options(PrintProtocol.Tspl);
        options.DeviceLineHeightAt24 = 24;

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            TextTemplate("A\nB", "5"), null, options: options).Pages);
        var lines = page.Elements.Where(element => element.Type == 1).ToArray();

        Assert.Equal(2, lines.Length);
        Assert.Equal(48, lines[0].Height);
        Assert.Equal(48, lines[1].Height);
        Assert.True(lines[1].Y >= lines[0].Y + lines[0].Height);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Strict_template_requires_declared_barcode_width(PrintProtocol protocol)
    {
        var template = SingleElement(new BpBarcodeElement {
            BarcodeType = BarcodeType.Code128, Height = 30,
            Field = new BgField { Value = "AB12" }
        });

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().Render(template, null, options: Options(protocol)));

        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("Width", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Strict_template_accepts_declared_barcode_width(PrintProtocol protocol)
    {
        var template = SingleElement(new BpBarcodeElement {
            BarcodeType = BarcodeType.Code128, Height = 30, Width = 300,
            HumanReadableReserveHeight = 12,
            Field = new BgField { Value = "AB12" }
        });

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            template, null, options: Options(protocol)).Pages);

        Assert.Single(page.Elements.Where(element => element.Type == 4));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Strict_template_requires_human_readable_height_when_enabled(PrintProtocol protocol)
    {
        var template = SingleElement(new BpBarcodeElement {
            BarcodeType = BarcodeType.Code128, Height = 30, Width = 300,
            Field = new BgField { Value = "AB12" }
        });

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().Render(template, null, options: Options(protocol)));

        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("HumanReadableReserveHeight", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Strict_template_can_disable_human_readable_text_without_reservation(PrintProtocol protocol)
    {
        var template = SingleElement(new BpBarcodeElement {
            BarcodeType = BarcodeType.Code128, Height = 30, Width = 300,
            HumanReadable = false, Field = new BgField { Value = "AB12" }
        });

        Assert.Single(new TemplateRenderer().RenderWithLayout(
            template, null, options: Options(protocol)).Pages);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Strict_native_bounds_rejects_unrelated_protocols(PrintProtocol protocol)
    {
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(
            TextTemplate("AB"), null, options: Options(protocol)));
    }

    [Fact]
    public void Device_line_height_rejects_invalid_value()
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceLineHeightAt24 = double.NaN;

        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(
            TextTemplate("AB"), null, options: options));
    }

    private static TemplateRenderOptions Options(PrintProtocol protocol) => new TemplateRenderOptions {
        Protocol = protocol, Encoding = Encoding.ASCII, RequireDeclaredNativeBounds = true
    };

    private static BpTemplate TextTemplate(string text, string deviceFont = null) =>
        SingleElement(new BpFontElement {
            FontSize = 24, DeviceFontName = deviceFont,
            Field = new BgField { Value = text }
        });

    private static BpTemplate SingleElement(BpElement element) => new BpTemplate {
        Width = 400, Height = 100,
        Rows = new List<BpRow> { new BpRow {
            Cells = new List<BpCell> { new BpCell {
                Elements = new List<BpElement> { element }
            } }
        } }
    };
}
