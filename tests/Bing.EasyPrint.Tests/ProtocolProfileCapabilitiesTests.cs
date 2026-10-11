using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证能力元数据与实际页面、图片指令的一致性。</summary>
public sealed class ProtocolProfileCapabilitiesTests
{
    [Fact]
    public void Cpcl_dpi_matches_existing_page_header()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);
        var document = new PrintDocument();
        document.AddPage(100, 50);
        Assert.Equal(200, provider.Capabilities.Dpi);
        Assert.Equal(200, ProtocolCommandFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities.Dpi);
        Assert.Contains("! 0 200 200 50 1", Encoding.ASCII.GetString(provider.Render(document)[0]));
    }

    [Fact]
    public void Impact_capabilities_report_snapshot_and_image_format_limits()
    {
        var profile = EscPosPrinterProfile.EightDotImpact(360);
        profile.MaxImageBlockBytes = 128;
        var provider = new EscPosPrintProtocolProvider(Encoding.ASCII, EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { Profile = profile });
        profile.HorizontalDpi = 300;
        profile.VerticalDpi = 300;
        profile.PrintableWidthDots = 100;
        profile.MaxImageBlockBytes = 1;
        Assert.Equal(180, provider.Capabilities.Dpi);
        Assert.Equal(60, provider.Capabilities.RasterVerticalDpi);
        Assert.Equal(360, provider.Capabilities.MaxPrintableWidthDots);
        Assert.Equal(128, provider.Capabilities.MaxImageBlockBytes);
        Assert.Equal(EscPosImageCommand.BitImageEscStar, provider.Capabilities.RasterImageCommand);
        Assert.Equal(8192, provider.Capabilities.MaxRasterRowBytes);
        Assert.True(provider.Capabilities.SupportsBarcode);
        Assert.True(provider.Capabilities.SupportsQrCode);
        Assert.False(provider.Capabilities.SupportsCut);
    }

    [Fact]
    public void Gs_v_zero_rejects_unused_esc_star_settings()
    {
        var profile = new EscPosPrinterProfile { UnidirectionalBitImage = true };
        Assert.Throws<ArgumentException>(() => new EscPosPrintCommand(Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { Profile = profile }));
        profile.UnidirectionalBitImage = false;
        profile.BitImageMode = EscPosBitImageMode.SingleDensity8;
        Assert.Throws<ArgumentException>(() => new EscPosPrintCommand(Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { Profile = profile }));
    }
}
