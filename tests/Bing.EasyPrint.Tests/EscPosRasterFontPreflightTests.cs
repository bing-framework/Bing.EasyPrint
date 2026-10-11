using System;
using System.Text;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ESC/POS Raster 在渲染前校验显式系统字体。</summary>
public sealed class EscPosRasterFontPreflightTests
{
    [Fact]
    public void Provider_capabilities_reflect_the_selected_layout_mode()
    {
        var native = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);
        var raster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(native.Capabilities.SupportsNativeLayout);
        Assert.False(native.Capabilities.SupportsRasterLayout);
        Assert.False(native.Capabilities.SupportsHostFontFamily);
        Assert.False(raster.Capabilities.SupportsNativeLayout);
        Assert.True(raster.Capabilities.SupportsRasterLayout);
        Assert.True(raster.Capabilities.SupportsHostFontFamily);
        Assert.Equal(ushort.MaxValue, raster.Capabilities.MaxRasterRowBytes);
        Assert.Equal(ushort.MaxValue, raster.Capabilities.MaxRasterHeight);
        Assert.Equal(new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .Capabilities.SupportsHostFontFamily, raster.Capabilities.SupportsHostFontFamily);
    }

    [Fact]
    public void Validate_rejects_an_unavailable_explicit_font_with_element_path()
    {
        var document = new PrintDocument();
        document.AddPage(200, 64).Add(
            new PrintTextElement(0, 0, "A", 24,
                fontFamily: "__Bing_EasyPrint_Font_Does_Not_Exist__"),
            "Rows[0].Text");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.False(provider.Capabilities.SupportsNativeLayout);
        Assert.True(provider.Capabilities.SupportsRasterLayout);
        Assert.True(provider.Capabilities.SupportsHostFontFamily);

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));

        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
        Assert.Contains("字体", error.InnerException?.Message ?? error.Message, StringComparison.Ordinal);
    }
}
