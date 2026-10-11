using System.Text;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证直接调用 ESC/POS Raster Provider 时的文字测量资源预算。</summary>
public sealed class EscPosRasterProviderBudgetTests
{
    [Fact]
    public void Raster_provider_rejects_text_measurement_above_the_default_pixel_budget()
    {
        var document = new PrintDocument();
        document.AddPage(2_000, 2_000)
            .Add(new PrintTextElement(0, 0, "A", 1024), "Rows[0].Text");

        var provider = PrintProtocolProviderFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster);

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));

        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
        Assert.Contains("像素预算", error.Message);
    }
}
