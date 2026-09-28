using System;
using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class TemplateBarcodeTests
{
    // 宽度含静区；CPCL width=2 的实际窄条宽3点，ratio=2 的宽条按8点预留。
    [Theory]
    [InlineData(BarcodeType.Ean13, "5901234123457", 339, 33, "EAN13")]
    [InlineData(BarcodeType.Ean8, "96385074", 243, 21, "EAN8")]
    [InlineData(BarcodeType.UpcA, "036000291452", 339, 27, "UPCA")]
    [InlineData(BarcodeType.UpcE, "0425261", 201, 27, "UPCE")]
    [InlineData(BarcodeType.Code39, "ABC", 282, 30, "39")]
    [InlineData(BarcodeType.Code93, "ABC", 252, 30, "93")]
    [InlineData(BarcodeType.Code93, "abc", 333, 30, "93")]
    [InlineData(BarcodeType.Code128, "abc", 264, 30, "128")]
    [InlineData(BarcodeType.Code128, "1234", 231, 30, "128")]
    [InlineData(BarcodeType.UccEan128, "0101234567890128", 462, 30, "UCCEAN128")]
    [InlineData(BarcodeType.Codabar, "A12B", 203, 30, "CODABAR")]
    [InlineData(BarcodeType.I2OF5, "1234", 186, 30, "I2OF5")]
    [InlineData(BarcodeType.I2OF5C, "123", 186, 30, "I2OF5C")]
    [InlineData(BarcodeType.I2OF5G, "12345678901", 386, 30, "I2OF5G")]
    [InlineData(BarcodeType.Msi, "123", 217, 30, "MSI")]
    [InlineData(BarcodeType.Postnet, "12345", 249, 30, "POSTNET")]
    [InlineData(BarcodeType.Fim, "A", 111, 30, "FIM")]
    public void Each_symbology_fits_its_reserved_width_and_rejects_one_dot_less(BarcodeType type, string data, int width, int left, string command)
    {
        var element = Barcode(type, data);
        var template = Template(element, width);
        var renderer = new TemplateRenderer();
        var result = renderer.Render(template, null, options: Options());
        Assert.Contains($"B {command} 2 2 30 {left} 0 {data}\r\n", Encoding.UTF8.GetString(result.Pages[0].Data));
        template.Width--;
        Assert.Throws<ArgumentException>(() => renderer.Render(template, null, options: Options()));
        template.Width++;
        var loader = new TemplateLoader();
        Assert.Equal(result.Pages[0].Data, renderer.Render(loader.LoadJson(loader.SaveJson(template)), null, options: Options()).Pages[0].Data);
    }

    [Theory]
    [InlineData(BarcodeType.Ean13, "590123412345", "5901234123457")]
    [InlineData(BarcodeType.Ean8, "9638507", "96385074")]
    [InlineData(BarcodeType.Ean8, "123456", "01234565")]
    [InlineData(BarcodeType.UpcA, "03600029145", "036000291452")]
    [InlineData(BarcodeType.UpcE, "425261", "0425261")]
    [InlineData(BarcodeType.I2OF5, "123", "0123")]
    [InlineData(BarcodeType.I2OF5C, "1234", "01234")]
    public void Retail_short_input_is_normalized_without_mutating_field(BarcodeType type, string input, string normalized)
    {
        var barcode = Barcode(type, input);
        var result = new TemplateRenderer().Render(Template(barcode, 576), null, options: Options());
        Assert.Contains(" " + normalized + "\r\n", Encoding.UTF8.GetString(result.Pages[0].Data));
        Assert.Equal(input, barcode.Field.Value);
    }

    [Theory]
    [InlineData(BarcodeType.Ean13, "5901234123450")]
    [InlineData(BarcodeType.Ean13, "abc")]
    [InlineData(BarcodeType.Ean8, "96385075")]
    [InlineData(BarcodeType.UpcA, "036000291453")]
    [InlineData(BarcodeType.UpcE, "2425261")]
    [InlineData(BarcodeType.Code39, "lowercase")]
    [InlineData(BarcodeType.Code39, "*ABC*")]
    [InlineData(BarcodeType.Code128, "")]
    [InlineData(BarcodeType.Code128, "中")]
    [InlineData(BarcodeType.Codabar, "123")]
    [InlineData(BarcodeType.Codabar, "AB12B")]
    [InlineData(BarcodeType.I2OF5, "12A")]
    [InlineData(BarcodeType.I2OF5C, "12B")]
    [InlineData(BarcodeType.UccEan128, "arbitrary text")]
    [InlineData(BarcodeType.UccEan128, "(01)01234567890128")]
    [InlineData(BarcodeType.I2OF5G, "1234")]
    [InlineData(BarcodeType.Msi, "12345678901234")]
    [InlineData(BarcodeType.Postnet, "---")]
    [InlineData(BarcodeType.Postnet, "1234")]
    [InlineData(BarcodeType.Fim, "D")]
    public void Invalid_barcode_data_fails_before_output(BarcodeType type, string data) =>
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(Template(Barcode(type, data), 2000), null, options: Options()));

    [Theory]
    [InlineData(0, 237)] // 窄3点，1.5倍宽条向上取整为5点。
    [InlineData(1, 252)]
    [InlineData(2, 282)]
    [InlineData(3, 297)]
    [InlineData(4, 327)]
    [InlineData(20, 252)]
    [InlineData(25, 282)]
    [InlineData(30, 297)]
    public void Cpcl_ratio_codes_are_not_literal_multipliers(int ratio, int width)
    {
        var barcode = Barcode(BarcodeType.Code39, "ABC"); barcode.Ratio = ratio;
        var template = Template(barcode, width);
        var renderer = new TemplateRenderer();
        Assert.Single(renderer.Render(template, null, options: Options()).Pages);
        template.Width--;
        Assert.Throws<ArgumentException>(() => renderer.Render(template, null, options: Options()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(19)]
    [InlineData(31)]
    public void Invalid_ratio_codes_fail(int ratio)
    {
        var barcode = Barcode(BarcodeType.Code128, "abc"); barcode.Ratio = ratio;
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(Template(barcode, 576), null, options: Options()));
    }

    [Fact]
    public void Barcode_moves_intact_to_next_page()
    {
        var barcode = Barcode(BarcodeType.Ean13, "5901234123457");
        var template = Template(barcode, 576);
        template.Height = 40;
        template.Rows.Insert(0, new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> {
            new BpFontElement { Field = new BgField { Value = "before" } }
        } } } });
        var result = new TemplateRenderer().Render(template, null, options: Options());
        Assert.Equal(2, result.PageCount);
        Assert.DoesNotContain("B EAN13", Encoding.UTF8.GetString(result.Pages[0].Data));
        Assert.Contains("B EAN13 2 2 30 33 0", Encoding.UTF8.GetString(result.Pages[1].Data));
    }

    private static BpBarcodeElement Barcode(BarcodeType type, string data) => new BpBarcodeElement {
        BarcodeType = type, Height = 30, Field = new BgField { Value = data }
    };
    private static TemplateRenderOptions Options() => new TemplateRenderOptions { Encoding = Encoding.UTF8 };
    private static BpTemplate Template(BpElement element, int width) => new BpTemplate {
        Width = width, Height = 200,
        Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> { element } } } } }
    };
}
