using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证直接打印文档中的字体族选项遵循各协议的能力边界。
/// </summary>
public sealed class ProtocolFontFamilyValidationTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Non_raster_providers_reject_font_family_with_element_path(PrintProtocol protocol)
    {
        var document = CreateDocument();
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            EscPosLayoutMode.Native);

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
    }

    [Fact]
    public void EscPos_raster_accepts_an_available_font_family()
    {
        var document = CreateDocument();
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() });

        provider.Validate(document);
        var bytes = Assert.Single(provider.Render(document));

        Assert.NotEmpty(bytes);
    }

    private static PrintDocument CreateDocument()
    {
        var document = new PrintDocument();
        document.AddPage(200, 64).Add(
            new PrintTextElement(1, 0, "A", 24, fontFamily: "Primary"),
            "Rows[0].Text");
        return document;
    }
}
