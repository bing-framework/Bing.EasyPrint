using System.Text;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class PrintDocumentSnapshotTests
{
    [Fact]
    public void Reused_element_keeps_each_document_source_path_and_location()
    {
        var reusable = new PrintTextElement(0, 0, "BAD\"TEXT", 24);

        var firstDocument = new PrintDocument();
        firstDocument.AddPage(200, 80).Add(new PrintTextElement(0, 0, "OK", 24), "First.Header");
        var firstPage = firstDocument.AddPage(200, 80);
        firstPage.Add(new PrintTextElement(0, 0, "OK", 24), "First.Prefix");
        firstPage.Add(reusable, "First.Bad");

        var secondDocument = new PrintDocument();
        var secondPage = secondDocument.AddPage(200, 80);
        secondPage.Add(new PrintTextElement(0, 0, "OK", 24), "Second.Prefix");
        secondPage.Add(new PrintTextElement(0, 0, "OK", 24), "Second.Padding");
        secondPage.Add(reusable, "Second.Bad");

        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);
        var firstIssue = provider.Check(firstDocument).FirstIssue;
        var secondIssue = provider.Check(secondDocument).FirstIssue;

        Assert.Equal("First.Bad", firstIssue.ElementPath);
        Assert.Equal(1, firstIssue.PageIndex);
        Assert.Equal(1, firstIssue.ElementIndex);
        Assert.Equal("Second.Bad", secondIssue.ElementPath);
        Assert.Equal(0, secondIssue.PageIndex);
        Assert.Equal(2, secondIssue.ElementIndex);
        Assert.Null(reusable.SourcePath);
        Assert.NotSame(firstPage.Elements[1], secondPage.Elements[2]);
    }

    [Fact]
    public void Repeated_add_on_one_page_keeps_paths_independent_and_preserves_input()
    {
        var reusable = new PrintTextElement(0, 0, "BAD\"TEXT", 24);
        var document = new PrintDocument();
        var page = document.AddPage(200, 80);

        page.Add(reusable, "First.Bad");
        page.Add(reusable, "Second.Bad");

        Assert.Null(reusable.SourcePath);
        Assert.NotSame(page.Elements[0], page.Elements[1]);
        Assert.Equal("First.Bad", page.Elements[0].SourcePath);
        Assert.Equal("Second.Bad", page.Elements[1].SourcePath);

        var issue = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII)
            .Check(document).FirstIssue;
        Assert.Equal("First.Bad", issue.ElementPath);
        Assert.Equal(0, issue.PageIndex);
        Assert.Equal(0, issue.ElementIndex);
    }

    [Fact]
    public void Attaching_an_element_to_another_page_does_not_rewrite_the_first_path()
    {
        var reusable = new PrintTextElement(0, 0, "BAD\"TEXT", 24);
        var document = new PrintDocument();
        var firstPage = document.AddPage(200, 80);
        var secondPage = document.AddPage(200, 80);

        firstPage.Add(reusable, "First.Bad");
        secondPage.Add(firstPage.Elements[0], "Second.Bad");

        Assert.Null(reusable.SourcePath);
        Assert.Equal("First.Bad", firstPage.Elements[0].SourcePath);
        Assert.Equal("Second.Bad", secondPage.Elements[0].SourcePath);
        Assert.NotSame(firstPage.Elements[0], secondPage.Elements[0]);
    }

    [Fact]
    public void Image_raster_getter_returns_a_fresh_copy()
    {
        var image = new PrintImageElement(0, 0, 8, 1, new byte[] { 0x80 });
        var first = image.Raster;
        var second = image.Raster;

        Assert.NotSame(first, second);
        first[0] = 0;
        Assert.Equal(0x80, second[0]);
        Assert.Equal(0x80, image.Raster[0]);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Image_snapshot_keeps_render_bytes_stable_after_all_public_mutations(PrintProtocol protocol)
    {
        var input = new byte[] { 0x80 };
        var image = new PrintImageElement(0, 0, 8, 1, input);
        var document = new PrintDocument();
        var page = document.AddPage(8, 1);
        page.Add(image, "Image");
        var pageImage = (PrintImageElement)page.Elements[0];
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            protocol == PrintProtocol.EscPos ? EscPosLayoutMode.Raster : EscPosLayoutMode.Native);

        var before = provider.Render(document)[0];
        input[0] = 0;
        var constructorRaster = image.Raster;
        constructorRaster[0] = 0;
        var pageRaster = pageImage.Raster;
        pageRaster[0] = 0;

        var after = provider.Render(document)[0];

        Assert.Equal(before, after);
        Assert.Equal(0x80, image.Raster[0]);
        Assert.Equal(0x80, pageImage.Raster[0]);
    }
}
