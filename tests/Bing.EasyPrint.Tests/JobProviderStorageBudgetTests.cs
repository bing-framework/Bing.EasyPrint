using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 Provider 和 Job 自有输出集合的预算、失败回收及同作用域重试。</summary>
public sealed class JobProviderStorageBudgetTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Provider_page_collection_rejection_restores_count_and_allows_small_retry(PrintProtocol protocol)
    {
        var provider = Provider(protocol);
        var onePage = Document("ONE");
        var twoPages = Document("ONE", "TWO");
        long onePagePeak;
        byte[] expected;

        using (var calibration = PrintResourceScope.Begin(long.MaxValue))
        {
            expected = Assert.Single(provider.Render(onePage));
            onePagePeak = calibration.PeakBytes;
        }

        using (var scope = PrintResourceScope.Begin(onePagePeak))
        {
            Assert.Throws<PrintResourceLimitException>(() => provider.Render(twoPages));
            Assert.Equal(0, scope.CurrentBytes);

            var retry = Assert.Single(provider.Render(onePage));
            Assert.Equal(expected, retry);
            Assert.True(scope.CurrentBytes > 0);
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Job_result_rejection_restores_count_and_small_retry_preserves_order(PrintProtocol protocol)
    {
        var provider = Provider(protocol);
        var document = Document("ONE");
        var renderer = new PrintJobRenderer();
        var options = new PrintJobOptions { Copies = 1 };
        long oneCopyPeak;
        (int SourcePageIndex, int CopyIndex, long ByteLength, string Data)[] expected;

        using (var calibration = PrintResourceScope.Begin(long.MaxValue))
        {
            var result = renderer.Render(provider, document, options);
            oneCopyPeak = calibration.PeakBytes;
            expected = result.Pages.Select(page => (page.SourcePageIndex, page.CopyIndex,
                page.ByteLength, Data: Convert.ToBase64String(page.Data))).ToArray();
        }

        using (var scope = PrintResourceScope.Begin(oneCopyPeak))
        {
            options.Copies = 2;
            Assert.Throws<PrintResourceLimitException>(() => renderer.Render(provider, document, options));
            Assert.Equal(0, scope.CurrentBytes);

            options.Copies = 1;
            var retry = renderer.Render(provider, document, options);
            var actual = retry.Pages.Select(page => (page.SourcePageIndex, page.CopyIndex,
                page.ByteLength, Data: Convert.ToBase64String(page.Data))).ToArray();
            Assert.Equal(expected, actual);
            Assert.True(scope.CurrentBytes > 0);
        }
    }

    [Fact]
    public void Job_preserves_delayed_zpl_file_boundaries_after_budgeted_segment_copy()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new Bing.EasyPrint.Protocols.ZplPrinterProfile
                {
                    Cutter = new Bing.EasyPrint.Protocols.ZplCutterProfile
                    {
                        Strategy = Bing.EasyPrint.Protocols.ZplCutStrategy.Delayed,
                        SupportedCutModes = new[] { PrintCutMode.Full },
                        SupportsTearOff = true
                    }
                }
            });
        var result = new PrintJobRenderer().Render(provider, Document("ONE"), new PrintJobOptions
        {
            CutScope = PrintFinishScope.JobEnd,
            CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full }
        });

        var page = Assert.Single(result.Pages);
        Assert.Equal(new[] { PrintOutputSegmentKind.Commands, PrintOutputSegmentKind.Control },
            page.Segments.Select(segment => segment.Kind));
        Assert.Equal("~JK\r\n", Encoding.ASCII.GetString(page.Segments[1].Data));
    }

    private static IPrintProtocolProvider Provider(PrintProtocol protocol)
        => PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);

    private static PrintDocument Document(params string[] values)
    {
        var document = new PrintDocument();
        foreach (var value in values)
            document.AddPage(320, 64).Add(new PrintTextElement(0, 0, value, 24));
        return document;
    }
}
