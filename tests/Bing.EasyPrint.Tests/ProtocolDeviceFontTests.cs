using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ProtocolDeviceFontTests
{
    [Theory]
    [InlineData(PrintProtocol.Tspl, "1", "\"1\",0,")]
    [InlineData(PrintProtocol.Zpl, "A", "^AAN,")]
    public void Template_emits_selected_device_font(PrintProtocol protocol, string font, string expected)
    {
        var template = new BpTemplate
        {
            Width = 300, Height = 80,
            Rows = new List<BpRow> { new BpRow
            {
                Cells = new List<BpCell> { new BpCell
                {
                    Elements = new List<BpElement> { new BpFontElement
                    {
                        FontSize = 24, DeviceFontName = font,
                        Field = new BgField { Value = "TEST" }
                    } }
                } }
            } }
        };

        var output = new TemplateRenderer().Render(template, null, options:
            new TemplateRenderOptions { Protocol = protocol, Encoding = Encoding.ASCII }).Pages[0].Data;
        Assert.Contains(expected, Encoding.ASCII.GetString(output));
    }

    [Theory]
    [InlineData("1", 16, 24, ",2,2,")]
    [InlineData("5", 32, 48, ",1,1,")]
    public void Tspl_fixed_font_layout_uses_emitted_cell_size(string font, int cellWidth,
        int cellHeight, string scale)
    {
        var template = new BpTemplate
        {
            Width = 300, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow
            {
                Cells = new List<BpCell> { new BpCell
                {
                    Elements = new List<BpElement> { new BpFontElement
                    {
                        FontSize = 24, DeviceFontName = font,
                        Field = new BgField { Value = "AB" }
                    } }
                } }
            } }
        };

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions { Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII }).Pages);
        var text = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal(2 * cellWidth, text.Width);
        Assert.Equal(cellHeight, text.Height);
        Assert.Contains(scale, Encoding.ASCII.GetString(page.Data));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "A")]
    [InlineData(PrintProtocol.Zpl, "bad")]
    [InlineData(PrintProtocol.EscPos, "1")]
    public void Unsupported_device_font_has_element_path(PrintProtocol protocol, string font)
    {
        var document = new PrintDocument();
        document.AddPage(300, 80)
            .Add(new PrintTextElement(0, 0, "TEST", 24, deviceFontName: font), "Rows[0].Text");
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
        var error = Assert.ThrowsAny<Exception>(() => provider.Validate(document));
        if (error is PrintElementValidationException validation)
            Assert.Equal("Rows[0].Text", validation.ElementPath);
        else
            Assert.Equal("Rows[0].Text", Assert.IsType<PrintCapabilityException>(error).ElementPath);
    }
}
