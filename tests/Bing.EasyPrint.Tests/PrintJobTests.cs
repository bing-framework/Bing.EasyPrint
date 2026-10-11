using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证显式打印任务在四种协议上的展开顺序、收尾边界和严格能力校验。
/// </summary>
public sealed class PrintJobTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Job_expands_collated_pages_for_each_protocol(PrintProtocol protocol)
    {
        var document = TwoPageDocument();
        var result = Render(protocol, document, Job(copies: 2));

        Assert.Equal(4, result.Pages.Count);
        Assert.Equal(new[] { (0, 0), (1, 0), (0, 1), (1, 1) },
            result.Pages.Select(page => (page.SourcePageIndex, page.CopyIndex)));
        Assert.Equal(new[] { "A", "B", "A", "B" },
            result.Pages.Select(page => ContainsAscii(page.Data, "ALPHA") ? "A" : "B"));
        Assert.All(result.Pages, page =>
        {
            Assert.False(page.FeedAfterPrint);
            Assert.False(page.CutAfterPrint);
        });
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Job_expands_uncollated_pages_for_each_protocol(PrintProtocol protocol)
    {
        var result = Render(protocol, TwoPageDocument(), Job(copies: 2, order: PrintCopyOrder.Uncollated));

        Assert.Equal(new[] { (0, 0), (0, 1), (1, 0), (1, 1) },
            result.Pages.Select(page => (page.SourcePageIndex, page.CopyIndex)));
        Assert.Equal(new[] { "A", "A", "B", "B" },
            result.Pages.Select(page => ContainsAscii(page.Data, "ALPHA") ? "A" : "B"));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Single_copy_job_preserves_provider_page_bytes(PrintProtocol protocol)
    {
        var document = TwoPageDocument();
        var provider = Provider(protocol);
        var oldBytes = provider.Render(document);
        var result = Render(protocol, document, Job());

        Assert.Equal(oldBytes.Count, result.Pages.Count);
        for (var index = 0; index < oldBytes.Count; index++)
            Assert.Equal(oldBytes[index], result.Pages[index].Data);
        Assert.Equal(new[] { (0, 0), (1, 0) },
            result.Pages.Select(page => (page.SourcePageIndex, page.CopyIndex)));
    }

    [Theory]
    [InlineData(PrintFinishScope.EachPage, true, true, true, true)]
    [InlineData(PrintFinishScope.EachCopy, false, true, false, true)]
    [InlineData(PrintFinishScope.JobEnd, false, false, false, true)]
    public void EscPos_feed_scope_marks_the_expected_output_chunks(PrintFinishScope scope,
        bool first, bool second, bool third, bool fourth)
    {
        var result = Render(PrintProtocol.EscPos, TwoPageDocument(), Job(copies: 2,
            feedScope: scope, feedUnit: PrintFeedUnit.Lines, feedAmount: 1));

        Assert.Equal(new[] { first, second, third, fourth },
            result.Pages.Select(page => page.FeedAfterPrint));
        for (var index = 0; index < result.Pages.Count; index++)
            Assert.Equal(result.Pages[index].FeedAfterPrint,
                ContainsSequence(result.Pages[index].Data, 0x1B, 0x64, 0x01));
    }

    [Fact]
    public void EscPos_feed_is_encoded_before_cut_on_the_selected_chunk()
    {
        var result = Render(PrintProtocol.EscPos, TwoPageDocument(), Job(feedScope: PrintFinishScope.JobEnd,
            feedUnit: PrintFeedUnit.Lines, feedAmount: 1, cutScope: PrintFinishScope.JobEnd));
        var page = Assert.Single(result.Pages.Where(item => item.FeedAfterPrint && item.CutAfterPrint));
        var feed = IndexOf(page.Data, new byte[] { 0x1B, 0x64, 0x01 });
        var cut = IndexOf(page.Data, new byte[] { 0x1D, 0x56, 0x00 });

        Assert.True(feed >= 0);
        Assert.True(cut > feed);
        Assert.Equal(1, result.Pages.Count(item => item.FeedAfterPrint));
        Assert.Equal(1, result.Pages.Count(item => item.CutAfterPrint));
    }

    [Fact]
    public void EscPos_motion_units_command_has_golden_bytes()
    {
        var bytes = new Protocols.EscPosPrintCommand(Encoding.ASCII)
            .Initialize()
            .FeedMotionUnits(255)
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x4A, 0xFF }, bytes);
    }

    [Theory]
    [InlineData(PrintFeedUnit.Lines, 0x64, 0xFF, 0x2D)]
    [InlineData(PrintFeedUnit.MotionUnits, 0x4A, 0xFF, 0x2D)]
    public void EscPos_large_job_feed_is_split_into_protocol_chunks(PrintFeedUnit unit, byte command,
        byte firstAmount, byte secondAmount)
    {
        var result = Render(PrintProtocol.EscPos, OnePageDocument(), Job(feedScope: PrintFinishScope.JobEnd,
            feedUnit: unit, feedAmount: 300));

        var data = Assert.Single(result.Pages).Data;
        Assert.True(ContainsSequence(data, 0x1B, command, firstAmount));
        Assert.True(ContainsSequence(data, 0x1B, command, secondAmount));
    }

    [Fact]
    public void EscPos_dots_are_converted_upward_using_the_profile()
    {
        var profile = new EscPosPrinterProfile
        {
            Name = "144-motion-unit",
            ImageCommand = EscPosImageCommand.BitImageEscStar,
            HorizontalDpi = 180,
            VerticalDpi = 60,
            MotionUnitsPerInch = 144
        };
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = profile });
        var result = new PrintJobRenderer().Render(provider, OnePageDocument(), Job(
            feedScope: PrintFinishScope.JobEnd, feedUnit: PrintFeedUnit.Dots, feedAmount: 10));

        Assert.True(ContainsSequence(Assert.Single(result.Pages).Data, 0x1B, 0x4A, 24));
    }

    [Fact]
    public void EscPos_dots_are_rejected_without_a_profile()
    {
        var error = Assert.Throws<PrintCapabilityException>(() => Render(PrintProtocol.EscPos,
            OnePageDocument(), Job(feedScope: PrintFinishScope.JobEnd, feedUnit: PrintFeedUnit.Dots,
                feedAmount: 1)));

        Assert.Equal("Job.FeedUnit", error.ElementPath);
    }

    [Fact]
    public void Tspl_job_accepts_only_dot_feed_and_rejects_cut()
    {
        var result = Render(PrintProtocol.Tspl, OnePageDocument(), Job(feedScope: PrintFinishScope.JobEnd,
            feedUnit: PrintFeedUnit.Dots, feedAmount: 12));
        var data = Encoding.ASCII.GetString(Assert.Single(result.Pages).Data);
        Assert.Contains("FEED 12\r\n", data);

        var lineError = Assert.Throws<PrintCapabilityException>(() => Render(PrintProtocol.Tspl,
            OnePageDocument(), Job(feedScope: PrintFinishScope.JobEnd, feedUnit: PrintFeedUnit.Lines,
                feedAmount: 1)));
        Assert.Equal("Job.FeedUnit", lineError.ElementPath);

        var cutError = Assert.Throws<PrintCapabilityException>(() => Render(PrintProtocol.Tspl,
            OnePageDocument(), Job(cutScope: PrintFinishScope.JobEnd)));
        Assert.Equal("Job.CutScope", cutError.ElementPath);
    }

    [Fact]
    public void Cpcl_and_zpl_reject_positive_job_feed()
    {
        var cpclError = Assert.Throws<PrintCapabilityException>(() => Render(PrintProtocol.Cpcl,
            OnePageDocument(), Job(feedScope: PrintFinishScope.JobEnd, feedAmount: 1)));
        var zplError = Assert.Throws<PrintCapabilityException>(() => Render(PrintProtocol.Zpl,
            OnePageDocument(), Job(feedScope: PrintFinishScope.JobEnd, feedAmount: 1)));

        Assert.Equal("Job.FeedUnit", cpclError.ElementPath);
        Assert.Equal("Job.FeedUnit", zplError.ElementPath);
    }

    [Fact]
    public void Zpl_cut_is_inserted_before_the_page_end_marker()
    {
        var result = Render(PrintProtocol.Zpl, OnePageDocument(), Job(cutScope: PrintFinishScope.JobEnd));
        var data = Encoding.ASCII.GetString(Assert.Single(result.Pages).Data);
        var cut = data.IndexOf("^CN\r\n", StringComparison.Ordinal);
        var end = data.IndexOf("^XZ\r\n", StringComparison.Ordinal);

        Assert.True(cut >= 0);
        Assert.True(end > cut);
        Assert.Equal(1, Count(data, "^CN\r\n"));
    }

    [Fact]
    public void Uncollated_each_copy_is_rejected_for_multi_page_jobs()
    {
        var error = Assert.Throws<PrintCapabilityException>(() => Render(PrintProtocol.EscPos,
            TwoPageDocument(), Job(copies: 2, order: PrintCopyOrder.Uncollated,
                feedScope: PrintFinishScope.EachCopy, feedAmount: 1)));

        Assert.Equal("Job.CopyOrder", error.ElementPath);
    }

    [Fact]
    public void Uncollated_each_copy_is_allowed_for_a_single_page_job()
    {
        var result = Render(PrintProtocol.EscPos, OnePageDocument(), Job(copies: 3,
            order: PrintCopyOrder.Uncollated, feedScope: PrintFinishScope.EachCopy,
            feedUnit: PrintFeedUnit.Lines, feedAmount: 1, cutScope: PrintFinishScope.EachCopy));

        Assert.Equal(new[] { (0, 0), (0, 1), (0, 2) },
            result.Pages.Select(page => (page.SourcePageIndex, page.CopyIndex)));
        Assert.All(result.Pages, page =>
        {
            Assert.True(page.FeedAfterPrint);
            Assert.True(page.CutAfterPrint);
            var feed = IndexOf(page.Data, new byte[] { 0x1B, 0x64, 0x01 });
            var cut = IndexOf(page.Data, new byte[] { 0x1D, 0x56, 0x00 });
            Assert.True(feed >= 0);
            Assert.True(cut > feed);
        });
    }

    [Theory]
    [InlineData(PrintFinishScope.EachPage, PrintFinishScope.JobEnd, "1111", "0001")]
    [InlineData(PrintFinishScope.EachCopy, PrintFinishScope.EachPage, "0101", "1111")]
    [InlineData(PrintFinishScope.JobEnd, PrintFinishScope.EachCopy, "0001", "0101")]
    public void EscPos_feed_and_cut_scopes_can_cross_independently(PrintFinishScope feedScope,
        PrintFinishScope cutScope, string expectedFeed, string expectedCut)
    {
        var result = Render(PrintProtocol.EscPos, TwoPageDocument(), Job(copies: 2,
            feedScope: feedScope, feedUnit: PrintFeedUnit.Lines, feedAmount: 1,
            cutScope: cutScope));

        Assert.Equal(expectedFeed.Select(value => value == '1'),
            result.Pages.Select(page => page.FeedAfterPrint));
        Assert.Equal(expectedCut.Select(value => value == '1'),
            result.Pages.Select(page => page.CutAfterPrint));
        for (var index = 0; index < result.Pages.Count; index++)
        {
            var page = result.Pages[index];
            var feed = IndexOf(page.Data, new byte[] { 0x1B, 0x64, 0x01 });
            var cut = IndexOf(page.Data, new byte[] { 0x1D, 0x56, 0x00 });
            Assert.Equal(page.FeedAfterPrint, feed >= 0);
            Assert.Equal(page.CutAfterPrint, cut >= 0);
            if (page.FeedAfterPrint && page.CutAfterPrint) Assert.True(cut > feed);
        }
    }

    [Fact]
    public void Job_rejects_page_level_copies_feed_and_cut_with_precise_location()
    {
        var copies = OnePageDocument();
        copies.Pages[0].Copies = 2;
        var copiesError = Assert.Throws<PrintElementValidationException>(() => Render(PrintProtocol.EscPos,
            copies, Job()));
        Assert.Equal("Pages[0]", copiesError.ElementPath);
        Assert.Equal(0, copiesError.PageIndex);
        Assert.Null(copiesError.ElementIndex);

        var feed = OnePageDocument();
        feed.Pages[0].FeedLines = 1;
        var feedError = Assert.Throws<PrintElementValidationException>(() => Render(PrintProtocol.EscPos,
            feed, Job()));
        Assert.Equal("Pages[0]", feedError.ElementPath);

        var negativeFeed = OnePageDocument();
        negativeFeed.Pages[0].FeedLines = -1;
        var negativeFeedError = Assert.Throws<PrintElementValidationException>(() => Render(PrintProtocol.EscPos,
            negativeFeed, Job()));
        Assert.Equal("Pages[0]", negativeFeedError.ElementPath);

        var cut = OnePageDocument();
        cut.Pages[0].CutAfterPrint = true;
        var cutError = Assert.Throws<PrintElementValidationException>(() => Render(PrintProtocol.EscPos,
            cut, Job()));
        Assert.Equal("Pages[0]", cutError.ElementPath);
    }

    [Fact]
    public void Job_invalid_values_are_rejected_before_provider_rendering()
    {
        var provider = Provider(PrintProtocol.EscPos);
        var document = OnePageDocument();
        var renderer = new PrintJobRenderer();

        var copies = Assert.Throws<PrintElementValidationException>(() => renderer.Render(provider, document,
            new PrintJobOptions { Copies = 0 }));
        Assert.Equal("Job", copies.ElementPath);

        var order = Assert.Throws<PrintElementValidationException>(() => renderer.Render(provider, document,
            new PrintJobOptions { CopyOrder = (PrintCopyOrder)99 }));
        Assert.Equal("Job", order.ElementPath);

        var feedScope = Assert.Throws<PrintElementValidationException>(() => renderer.Render(provider, document,
            new PrintJobOptions { FeedScope = (PrintFinishScope)99 }));
        Assert.Equal("Job", feedScope.ElementPath);

        var feedUnit = Assert.Throws<PrintElementValidationException>(() => renderer.Render(provider, document,
            new PrintJobOptions { FeedUnit = (PrintFeedUnit)99 }));
        Assert.Equal("Job", feedUnit.ElementPath);

        var amount = Assert.Throws<PrintElementValidationException>(() => renderer.Render(provider, document,
            new PrintJobOptions { FeedAmount = -1 }));
        Assert.Equal("Job", amount.ElementPath);
    }

    [Fact]
    public void Job_checks_page_and_output_budgets_before_expansion()
    {
        var renderer = new PrintJobRenderer();
        var provider = Provider(PrintProtocol.EscPos);
        var document = TwoPageDocument();
        var options = Job(copies: 2);

        Assert.Throws<ArgumentException>(() => renderer.Render(provider, document, options,
            CancellationToken.None, maxPages: 3));
        Assert.Throws<ArgumentException>(() => renderer.Render(provider, document, options,
            CancellationToken.None, maxPages: int.MaxValue, maxOutputBytes: 1));
    }

    [Fact]
    public void Empty_document_allows_maximum_copies_without_physical_output()
    {
        var result = new PrintJobRenderer().Render(Provider(PrintProtocol.EscPos), new PrintDocument(),
            Job(copies: int.MaxValue), CancellationToken.None, maxPages: 1, maxOutputBytes: 1);

        Assert.Empty(result.Pages);
    }

    [Fact]
    public void EscPos_feed_and_cut_accept_exact_output_budget_and_reject_one_byte_less()
    {
        var document = OnePageDocument();
        var options = Job(feedScope: PrintFinishScope.JobEnd, feedUnit: PrintFeedUnit.Lines,
            feedAmount: 1, cutScope: PrintFinishScope.JobEnd);
        var expected = Render(PrintProtocol.EscPos, document, options);
        var expectedData = Assert.Single(expected.Pages).Data;

        var exact = new PrintJobRenderer().Render(Provider(PrintProtocol.EscPos), document, options,
            CancellationToken.None, maxPages: int.MaxValue, maxOutputBytes: expectedData.Length);
        Assert.Equal(expectedData, Assert.Single(exact.Pages).Data);

        Assert.Throws<ArgumentException>(() => new PrintJobRenderer().Render(Provider(PrintProtocol.EscPos),
            document, options, CancellationToken.None, maxPages: int.MaxValue,
            maxOutputBytes: expectedData.Length - 1));
    }

    [Fact]
    public void Zpl_cut_is_included_in_the_exact_output_budget()
    {
        var document = OnePageDocument();
        var withoutCut = Assert.Single(Render(PrintProtocol.Zpl, document, Job()).Pages).Data;
        var options = Job(cutScope: PrintFinishScope.JobEnd);
        var expected = Render(PrintProtocol.Zpl, document, options);
        var expectedData = Assert.Single(expected.Pages).Data;

        Assert.Equal(withoutCut.Length + Encoding.ASCII.GetByteCount("^CN\r\n"), expectedData.Length);
        var exact = new PrintJobRenderer().Render(Provider(PrintProtocol.Zpl), document, options,
            CancellationToken.None, maxPages: int.MaxValue, maxOutputBytes: expectedData.Length);
        Assert.Equal(expectedData, Assert.Single(exact.Pages).Data);

        Assert.Throws<ArgumentException>(() => new PrintJobRenderer().Render(Provider(PrintProtocol.Zpl),
            document, options, CancellationToken.None, maxPages: int.MaxValue,
            maxOutputBytes: expectedData.Length - 1));
    }

    [Fact]
    public void Job_honors_cancellation_before_rendering()
    {
        var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => new PrintJobRenderer().Render(
            Provider(PrintProtocol.EscPos), OnePageDocument(), Job(), source.Token));
    }

    [Fact]
    public void Template_job_maps_logical_pages_and_copies_to_physical_pages()
    {
        var result = new TemplateRenderer().RenderWithLayout(TwoPageTemplate(), null, options:
            new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                Job = Job(copies: 2, order: PrintCopyOrder.Uncollated)
            });

        Assert.Equal(4, result.PageCount);
        Assert.Equal(new[] { (0, 0), (0, 1), (1, 0), (1, 1) },
            result.Pages.Select(page => (page.SourcePageIndex!.Value, page.CopyIndex!.Value)));
        Assert.All(result.Pages, page =>
        {
            Assert.NotNull(page.FeedAfterPrint);
            Assert.NotNull(page.CutAfterPrint);
            Assert.NotEmpty(page.Elements);
        });
    }

    [Fact]
    public void Template_job_page_limit_rejects_on_second_page_before_third_row_resource()
    {
        var opened = 0;
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            ThreePageJobTemplate(), null, new CountingResources(() => opened++), new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                MaxPages = 3,
                Job = Job(copies: 2)
            }));

        Assert.Equal("Job", error.SourcePath);
        Assert.Contains("页数", error.Message);
        Assert.Equal(0, opened);
    }

    [Fact]
    public void Template_job_each_copy_rejects_on_second_page_before_third_row_resource()
    {
        var opened = 0;
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            ThreePageJobTemplate(), null, new CountingResources(() => opened++), new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                Job = Job(copies: 2, order: PrintCopyOrder.Uncollated,
                    feedScope: PrintFinishScope.EachCopy, feedUnit: PrintFeedUnit.Dots, feedAmount: 1)
            }));

        Assert.Equal("Job.CopyOrder", error.SourcePath);
        Assert.Contains("连续边界", error.Message);
        Assert.Equal(0, opened);
    }

    [Fact]
    public void Template_job_conflicts_are_rejected_before_opening_resources()
    {
        var opened = 0;
        var error = Assert.ThrowsAny<ArgumentException>(() => new TemplateRenderer().Render(
            TemplateWithImage(), null, new CountingResources(() => opened++), new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                Copies = 2,
                Job = Job(copies: 2)
            }));

        Assert.Equal(0, opened);
        Assert.Contains("策略", error.Message);
    }

    [Fact]
    public void Template_without_job_keeps_legacy_page_metadata_empty()
    {
        var result = new TemplateRenderer().RenderWithLayout(TwoPageTemplate(), null, options:
            new TemplateRenderOptions { Protocol = PrintProtocol.Tspl, Encoding = Encoding.ASCII });

        Assert.NotEmpty(result.Pages);
        Assert.All(result.Pages, page =>
        {
            Assert.Null(page.SourcePageIndex);
            Assert.Null(page.CopyIndex);
            Assert.Null(page.FeedAfterPrint);
            Assert.Null(page.CutAfterPrint);
        });
    }

    private static PrintJobRenderResult Render(PrintProtocol protocol, PrintDocument document, PrintJobOptions options)
        => new PrintJobRenderer().Render(Provider(protocol), document, options);

    private static IPrintProtocolProvider Provider(PrintProtocol protocol, EscPosDeviceOptions escPos = null)
        => PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Native, escPos);

    private static PrintJobOptions Job(int copies = 1, PrintCopyOrder order = PrintCopyOrder.Collated,
        PrintFinishScope feedScope = PrintFinishScope.None, PrintFeedUnit feedUnit = PrintFeedUnit.Lines,
        int feedAmount = 0, PrintFinishScope cutScope = PrintFinishScope.None)
        => new PrintJobOptions
        {
            Copies = copies,
            CopyOrder = order,
            FeedScope = feedScope,
            FeedUnit = feedUnit,
            FeedAmount = feedAmount,
            CutScope = cutScope
        };

    private static PrintDocument OnePageDocument()
    {
        var document = new PrintDocument();
        AddPage(document, "ALPHA");
        return document;
    }

    private static PrintDocument TwoPageDocument()
    {
        var document = new PrintDocument();
        AddPage(document, "ALPHA");
        AddPage(document, "BRAVO");
        return document;
    }

    private static void AddPage(PrintDocument document, string text)
    {
        var page = document.AddPage(320, 64);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintTextElement(0, 0, text, 24), $"Pages[{document.Pages.Count - 1}].Text");
    }

    private static BpTemplate TwoPageTemplate()
        => new BpTemplate
        {
            Version = 3,
            Width = 320,
            Height = 48,
            Rows = new List<BpRow>
            {
                TextRow("A"), TextRow("B"), TextRow("C")
            }
        };

    private static BpTemplate TemplateWithImage()
        => new BpTemplate
        {
            Version = 3,
            Width = 320,
            Height = 64,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 24,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 320,
                            Elements = new List<BpElement>
                            {
                                new BpImageElement
                                {
                                    Width = 8,
                                    Height = 1,
                                    Field = new BgField { Value = "missing-image" }
                                }
                            }
                        }
                    }
                }
            }
        };

    private static BpTemplate ThreePageJobTemplate()
        => new BpTemplate
        {
            Version = 3,
            Width = 320,
            Height = 24,
            Rows = new List<BpRow>
            {
                TextRow("FIRST"),
                TextRow("SECOND"),
                new BpRow
                {
                    Height = 24,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 320,
                            Elements = new List<BpElement>
                            {
                                new BpImageElement
                                {
                                    Width = 8,
                                    Height = 1,
                                    Field = new BgField { Value = "third-row-resource" }
                                }
                            }
                        }
                    }
                }
            }
        };

    private static BpRow TextRow(string value)
        => new BpRow
        {
            Height = 24,
            Cells = new List<BpCell>
            {
                new BpCell
                {
                    Width = 320,
                    Elements = new List<BpElement>
                    {
                        new BpFontElement { FontSize = 24, Field = new BgField { Value = value } }
                    }
                }
            }
        };

    private static bool ContainsAscii(byte[] data, string value)
        => Encoding.ASCII.GetString(data).Contains(value, StringComparison.Ordinal);

    private static bool ContainsSequence(byte[] data, params int[] sequence)
        => IndexOf(data, sequence.Select(value => (byte)value).ToArray()) >= 0;

    private static int IndexOf(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
            if (data.Skip(start).Take(sequence.Length).SequenceEqual(sequence)) return start;
        return -1;
    }

    private static int Count(string value, string needle)
    {
        var count = 0;
        for (var index = 0; (index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0; index += needle.Length)
            count++;
        return count;
    }

    private sealed class CountingResources : ITemplateResourceProvider
    {
        private readonly Action _onOpen;

        public CountingResources(Action onOpen) => _onOpen = onOpen;

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            _onOpen();
            return null;
        }
    }
}
