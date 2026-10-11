using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>保留未配置固定目录时的系统字体兼容契约；完整环境须有实际覆盖样本的字库。</summary>
[Trait("FontEnvironment", "System")]
public sealed class SystemFontEnvironmentTests
{
    [Theory]
    [InlineData("Raster text 中", false)]
    [InlineData("ABC", true)]
    public void Default_system_measurement_matches_the_resolved_family(string text, bool bold)
    {
        var family = EscPosRasterCanvas.ResolveFontFamilyName(text, null, bold);
        Assert.False(string.IsNullOrWhiteSpace(family));
        var width = EscPosRasterCanvas.MeasureTextWidth(text, 24, bold);
        Assert.True(width > 0);
        Assert.Equal(width, EscPosRasterCanvas.MeasureTextWidth(text, 24, bold, family));
        var canvas = new EscPosRasterCanvas(320, 80);
        canvas.DrawText(10, 10, text, 24, bold, fontFamily: family);
        Assert.Contains(canvas.ToBytes(), value => value != 0);
    }
}
