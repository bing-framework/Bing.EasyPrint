using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TsplFeedUnitTests
{
    [Fact]
    public void Provider_rejects_line_based_feed_because_tspl_requires_dot_distance()
    {
        var document = new PrintDocument();
        var page = document.AddPage(80, 40);
        page.FeedLines = 1;

        var provider = new TsplPrintProtocolProvider(Encoding.ASCII);

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(document));

        Assert.Contains("进纸行数", error.Message);
        Assert.Contains("打印点距离", error.Message);
    }

    [Fact]
    public void Zero_line_feed_remains_a_valid_no_feed_value()
    {
        var document = new PrintDocument();
        var page = document.AddPage(80, 40);
        page.FeedLines = 0;

        var output = Encoding.ASCII.GetString(
            new TsplPrintProtocolProvider(Encoding.ASCII).Render(document)[0]);

        Assert.DoesNotContain("FEED ", output);
    }
}
