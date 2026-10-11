using System;
using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ProtocolBarcodeModuleTests
{
    [Fact]
    public void Tspl_template_uses_explicit_module_widths()
    {
        var output = Render(PrintProtocol.Tspl, new BpBarcodeElement
        {
            Height = 30, NarrowModuleWidth = 2, WideModuleWidth = 4,
            Field = new BgField { Value = "1234" }
        });

        Assert.Contains(",30,1,0,2,4,\"1234\"", output);
    }

    [Fact]
    public void Zpl_template_uses_explicit_module_width()
    {
        var output = Render(PrintProtocol.Zpl, new BpBarcodeElement
        {
            Height = 30, NarrowModuleWidth = 2,
            Field = new BgField { Value = "1234" }
        });

        Assert.Contains("^BY2", output);
    }

    [Theory]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Legacy_ratio_uses_compatibility_wide_width_for_protocols_without_wide_parameter(
        PrintProtocol protocol)
    {
        var barcode = new BpBarcodeElement
        {
            Height = 30,
            Ratio = 4,
            Field = new BgField { Value = "1234" }
        };

        var options = new TemplateRenderOptions
        {
            Protocol = protocol,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII
        };

        var template = new BpTemplate
        {
            Width = 400,
            Height = 100,
            Rows = new List<BpRow> { new BpRow
            {
                Cells = new List<BpCell> { new BpCell
                {
                    Elements = new List<BpElement> { barcode }
                } }
            } }
        };

        var bytes = new TemplateRenderer().Render(template, null, options: options).Pages[0].Data;
        if (protocol == PrintProtocol.Zpl)
            Assert.Contains("^BY3", Encoding.ASCII.GetString(bytes));
        else
            Assert.NotEmpty(bytes);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Unsupported_module_width_setting_is_rejected(PrintProtocol protocol)
    {
        var barcode = new BpBarcodeElement
        {
            Height = 30, WideModuleWidth = 4,
            Field = new BgField { Value = "1234" }
        };

        var error = Assert.Throws<TemplateRenderException>(() => Render(protocol, barcode));
        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    private static string Render(PrintProtocol protocol, BpBarcodeElement barcode)
    {
        var template = new BpTemplate
        {
            Width = 400,
            Height = 100,
            Rows = new List<BpRow> { new BpRow
            {
                Cells = new List<BpCell> { new BpCell
                {
                    Elements = new List<BpElement> { barcode }
                } }
            } }
        };
        var bytes = new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = protocol,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII
        }).Pages[0].Data;
        return Encoding.ASCII.GetString(bytes);
    }
}
