using System;
using System.Threading;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证固定 Raster 字体选择的取消边界及旧入口兼容性。</summary>
public sealed class RasterFontCancellationTests
{
    [Fact]
    public void Resolve_honors_a_pre_cancelled_token_before_font_selection()
    {
        var catalog = CreateCatalog();
        using (var source = new CancellationTokenSource())
        {
            source.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                catalog.Resolve("A", "Primary", false, source.Token));
        }
    }

    [Fact]
    public void Resolve_with_none_preserves_legacy_selection_and_raster_output()
    {
        var catalog = CreateCatalog();
        var legacy = catalog.Resolve("A", "Primary", false);
        var explicitNone = catalog.Resolve("A", "Primary", false, CancellationToken.None);

        Assert.Equal(legacy.Name, explicitNone.Name);
        Assert.Equal(legacy.Family.Name, explicitNone.Family.Name);
        Assert.Equal("Primary", EscPosRasterCanvas.ResolveFontFamilyName(
            "A", "Primary", false, catalog, CancellationToken.None));

        var legacyCanvas = new EscPosRasterCanvas(128, 64, catalog);
        legacyCanvas.DrawText(4, 4, "A", 24, fontFamily: legacy.Name);
        var explicitNoneCanvas = new EscPosRasterCanvas(128, 64, catalog);
        explicitNoneCanvas.DrawText(4, 4, "A", 24, fontFamily: explicitNone.Name);

        Assert.Equal(legacyCanvas.ToBytes(), explicitNoneCanvas.ToBytes());
    }

    private static RasterFontCatalog CreateCatalog()
        => new RasterFontCatalog(new[]
        {
            new RasterFontResource("Primary", "test", FixedTestFonts.Regular())
        }, "Primary");
}
