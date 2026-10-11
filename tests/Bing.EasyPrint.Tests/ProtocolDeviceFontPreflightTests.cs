using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 TSPL/ZPL 设备字体在 Provider 预检阶段的边界和路径信息。</summary>
public sealed class ProtocolDeviceFontPreflightTests
{
    [Theory]
    [InlineData(29, true)]
    [InlineData(126, false)]
    public void Tspl_fixed_font_scales_are_limited_before_render(int fontSize, bool valid)
    {
        var document = new PrintDocument();
        document.AddPage(200, 100)
            .Add(new PrintTextElement(1, 1, "OK", fontSize, deviceFontName: "1"), "Rows[0].Text");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

        if (valid)
        {
            var output = Encoding.ASCII.GetString(provider.Render(document)[0]);
            Assert.Contains("TEXT 1,1,\"1\",0,2,2,\"OK\"", output);
            return;
        }

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("A")]
    [InlineData("Z")]
    public void Zpl_accepts_single_uppercase_or_digit_font_identifiers(string font)
    {
        var document = new PrintDocument();
        document.AddPage(200, 100)
            .Add(new PrintTextElement(1, 1, "OK", 24, deviceFontName: font), "Rows[0].Text");

        PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Validate(document);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("AA")]
    [InlineData("_")]
    public void Zpl_rejects_non_standard_font_identifiers_with_element_path(string font)
    {
        var document = new PrintDocument();
        document.AddPage(200, 100)
            .Add(new PrintTextElement(1, 1, "OK", 24, deviceFontName: font), "Rows[0].Text");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Validate(document));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
    }
}
