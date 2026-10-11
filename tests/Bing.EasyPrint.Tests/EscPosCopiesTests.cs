using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosCopiesTests
{
    [Theory]
    [InlineData(EscPosLayoutMode.Native)]
    [InlineData(EscPosLayoutMode.Raster)]
    public void Copies_repeat_the_complete_page_without_changing_logical_page_count(EscPosLayoutMode mode)
    {
        var one = new PrintDocument();
        one.AddPage(80, 40).CutAfterPrint = true;
        var two = new PrintDocument();
        two.AddPage(80, 40).CutAfterPrint = true;
        two.Pages[0].Copies = 2;
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, mode);

        var single = provider.Render(one)[0];
        var repeated = provider.Render(two);

        Assert.Single(repeated);
        Assert.Equal(single.Concat(single).ToArray(), repeated[0]);
    }
}
