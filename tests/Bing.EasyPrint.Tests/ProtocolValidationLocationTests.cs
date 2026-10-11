using System.Text;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ProtocolValidationLocationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Check_identifies_second_page_element_with_reused_source_path(bool unsupported)
    {
        var document = new PrintDocument();
        document.AddPage(40, 40).Add(new PrintRectangleElement(0, 0, 10, 10), "Shared.Element");
        var second = document.AddPage(40, 40);
        second.Add(new PrintRectangleElement(0, 0, 10, 10), "Shared.Element");
        second.Add(unsupported
            ? (PrintElement)new PrintTextElement(10, 10, "A", 12, bold: true)
            : new PrintRectangleElement(35, 35, 10, 10), "Shared.Element");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

        var issue = provider.Check(document).FirstIssue;

        Assert.NotNull(issue);
        Assert.Equal("Shared.Element", issue.ElementPath);
        Assert.Equal(unsupported, issue.IsCapabilityIssue);
        Assert.Equal(1, issue.PageIndex);
        Assert.Equal(1, issue.ElementIndex);
    }

    [Fact]
    public void Page_issue_has_page_index_without_element_index()
    {
        var document = new PrintDocument();
        document.AddPage(40, 40);
        document.AddPage(40, 40).FeedLines = 1;
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

        var issue = provider.Check(document).FirstIssue;

        Assert.True(issue.IsCapabilityIssue);
        Assert.Equal("Pages[1]", issue.ElementPath);
        Assert.Equal(1, issue.PageIndex);
        Assert.Null(issue.ElementIndex);
    }
}
