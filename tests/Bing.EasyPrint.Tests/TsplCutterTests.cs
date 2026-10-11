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
/// 验证 TSPL 结构化切纸在命令、页面、任务和模板边界上的输出契约。
/// </summary>
public sealed class TsplCutterTests
{
    [Fact]
    public void Omitted_cutter_preserves_legacy_print_bytes_and_disables_cut_capability()
    {
        var command = new TsplPrintCommand(Encoding.ASCII)
            .Size(60, 40, "dot")
            .Text(1, 2, "OK")
            .Print();

        Assert.False(command.Capabilities.SupportsCut);
        Assert.Empty(command.Capabilities.CutModes);
        Assert.Equal("SIZE 60 dot,40 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "TEXT 1,2,\"0\",0,1,1,\"OK\"\r\nPRINT 1,1\r\n",
            Encoding.ASCII.GetString(command.Build().GetBytes()));
    }

    [Fact]
    public void Batch_full_cut_writes_golden_sequence_and_restores_automatic_state()
    {
        var command = NewCommand(BatchCutter(PrintCutMode.Full))
            .Size(60, 40, "dot")
            .Text(1, 2, "OK")
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full });

        Assert.Equal("SIZE 60 dot,40 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "TEXT 1,2,\"0\",0,1,1,\"OK\"\r\n" +
            "EOJ\r\nSET CUTTER OFF\r\nSET CUTTER BATCH\r\n" +
            "PRINT 1,1\r\nEOJ\r\nSET CUTTER OFF\r\n",
            Ascii(command));
    }

    [Fact]
    public void Batch_partial_cut_uses_partial_setting_and_restores_all_cutter_settings()
    {
        var profile = BatchCutter(PrintCutMode.Full, PrintCutMode.Partial);
        var command = NewCommand(profile)
            .Size(60, 40, "dot")
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Partial });

        Assert.Equal("SIZE 60 dot,40 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "EOJ\r\nSET CUTTER OFF\r\nSET PARTIAL_CUTTER OFF\r\n" +
            "SET PARTIAL_CUTTER BATCH\r\nPRINT 1,1\r\nEOJ\r\n" +
            "SET CUTTER OFF\r\nSET PARTIAL_CUTTER OFF\r\n",
            Ascii(command));
    }

    [Fact]
    public void Immediate_full_cut_waits_then_cuts_after_print()
    {
        var command = NewCommand(ImmediateCutter(PrintCutMode.Full))
            .Size(60, 40, "dot")
            .Text(1, 2, "OK")
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full });

        Assert.Equal("SIZE 60 dot,40 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "TEXT 1,2,\"0\",0,1,1,\"OK\"\r\n" +
            "EOJ\r\nSET CUTTER OFF\r\nPRINT 1,1\r\nEOJ\r\nCUT\r\n",
            Ascii(command));
    }

    [Fact]
    public void Immediate_partial_cut_uses_declared_shape_and_dot_position_compensation()
    {
        var command = NewCommand(ImmediateCutter(PrintCutMode.Partial, 12))
            .Size(60, 40, "dot")
            .PrintWithCut(new PrintCutOptions
            {
                Mode = PrintCutMode.Partial,
                FeedToCutPosition = true,
                FeedBeforeCutAmount = 3,
                FeedBeforeCutUnit = PrintFeedUnit.Dots
            });

        Assert.Equal("SIZE 60 dot,40 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "EOJ\r\nSET CUTTER OFF\r\nSET PARTIAL_CUTTER OFF\r\n" +
            "PRINT 1,1\r\nEOJ\r\nFEED 15\r\nEOJ\r\nCUT\r\n",
            Ascii(command));
    }

    [Fact]
    public void Configured_print_without_cut_synchronizes_and_disables_automatic_state()
    {
        var command = NewCommand(BatchCutter(PrintCutMode.Full, PrintCutMode.Partial))
            .Size(60, 40, "dot")
            .Print();

        Assert.Equal("SIZE 60 dot,40 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "EOJ\r\nSET CUTTER OFF\r\nSET PARTIAL_CUTTER OFF\r\n" +
            "PRINT 1,1\r\nEOJ\r\n",
            Ascii(command));
        Assert.DoesNotContain("BATCH", Ascii(command));
    }

    [Fact]
    public void Profile_and_raw_byte_inputs_are_snapshotted()
    {
        var modes = new[] { PrintCutMode.Full };
        var cutter = new TsplCutterProfile
        {
            Strategy = TsplCutStrategy.Immediate,
            SupportedCutModes = modes,
            SupportsEndOfJob = true,
            CutPositionFeedDots = 4
        };
        var media = new TsplMediaOptions { Profile = new TsplPrinterProfile { Cutter = cutter } };
        var command = new TsplPrintCommand(Encoding.ASCII, media);
        var raw = Encoding.ASCII.GetBytes("RAW\r\n");
        command.WriteRawBytes(raw);

        modes[0] = PrintCutMode.Partial;
        cutter.SupportsEndOfJob = false;
        cutter.CutPositionFeedDots = 9999;
        raw[0] = (byte)'X';

        Assert.Equal("RAW\r\n", Ascii(command));
        Assert.Equal("RAW\r\nEOJ\r\nCUT\r\n", Ascii(command.Cut().Build().GetBytes()));
    }

    [Fact]
    public void Reported_cutter_capability_is_an_independent_snapshot()
    {
        var command = NewCommand(BatchCutter(PrintCutMode.Full, PrintCutMode.Partial));
        var reported = command.Capabilities.TsplCutter;
        reported.SupportedCutModes[0] = PrintCutMode.Partial;
        reported.SupportsEndOfJob = false;

        Assert.Equal(new[] { PrintCutMode.Full, PrintCutMode.Partial },
            command.Capabilities.TsplCutter.SupportedCutModes);
        command.PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full });
        Assert.Contains("SET CUTTER BATCH\r\n", Ascii(command));
    }

    [Fact]
    public void Provider_keeps_original_profile_and_modes_after_construction()
    {
        var modes = new[] { PrintCutMode.Partial };
        var cutter = new TsplCutterProfile
        {
            Strategy = TsplCutStrategy.Batch,
            SupportedCutModes = modes,
            SupportsEndOfJob = true
        };
        var media = new TsplMediaOptions { Profile = new TsplPrinterProfile { Cutter = cutter } };
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: media);

        modes[0] = PrintCutMode.Full;
        cutter.SupportsEndOfJob = false;
        cutter.SupportedCutModes = new[] { PrintCutMode.Full };

        var document = OnePageDocument();
        document.Pages[0].CutAfterPrint = true;
        document.Pages[0].CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial };
        var text = Ascii(Assert.Single(provider.Render(document)));

        Assert.Contains("SET PARTIAL_CUTTER BATCH\r\n", text);
        Assert.Equal(new[] { PrintCutMode.Partial }, provider.Capabilities.CutModes);
    }

    [Fact]
    public void Build_is_idempotent_for_controlled_output()
    {
        var command = NewCommand(BatchCutter(PrintCutMode.Full, PrintCutMode.Partial))
            .Size(60, 40, "dot")
            .Text(1, 2, "OK")
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Partial });

        var first = command.Build().GetBytes();
        var second = command.Build().GetBytes();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Failed_cut_validation_does_not_pollute_command_and_valid_call_can_recover()
    {
        var command = NewCommand(BatchCutter(PrintCutMode.Full))
            .Size(60, 40, "dot")
            .Text(1, 2, "OK");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.PrintWithCut(new PrintCutOptions
        {
            Mode = PrintCutMode.Partial
        }));
        Assert.Equal(before, command.Build().GetBytes());

        command.PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full });
        Assert.Contains("SET CUTTER BATCH\r\n", Ascii(command));
    }

    [Fact]
    public void Independent_cut_without_cutter_fails_without_recording_output()
    {
        var command = new TsplPrintCommand(Encoding.ASCII);

        Assert.Throws<NotSupportedException>(() => command.Cut());
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Batch_independent_cut_fails_and_print_with_cut_can_recover()
    {
        var command = NewCommand(BatchCutter(PrintCutMode.Full))
            .Size(60, 40, "dot");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.Cut());
        Assert.Equal(before, command.Build().GetBytes());

        command.PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full });
        Assert.Contains("SET CUTTER BATCH\r\nPRINT 1,1\r\n", Ascii(command));
    }

    [Fact]
    public void Cutter_profile_requires_explicit_end_of_job_support()
    {
        Assert.Throws<NotSupportedException>(() => new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { Profile = new TsplPrinterProfile
            {
                Cutter = new TsplCutterProfile { SupportedCutModes = new[] { PrintCutMode.Full } }
            }}));
    }

    [Fact]
    public void Immediate_cutter_requires_one_declared_shape_and_batch_rejects_position_offset()
    {
        Assert.Throws<ArgumentException>(() => new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { Profile = new TsplPrinterProfile
            {
                Cutter = new TsplCutterProfile
                {
                    Strategy = TsplCutStrategy.Immediate,
                    SupportedCutModes = new[] { PrintCutMode.Full, PrintCutMode.Partial },
                    SupportsEndOfJob = true
                }
            }}));

        Assert.Throws<NotSupportedException>(() => new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { Profile = new TsplPrinterProfile
            {
                Cutter = new TsplCutterProfile
                {
                    Strategy = TsplCutStrategy.Batch,
                    SupportsEndOfJob = true,
                    CutPositionFeedDots = 1
                }
            }}));
    }

    [Fact]
    public void Invalid_cutter_profile_values_are_rejected_during_construction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCommand(new TsplCutterProfile
        {
            Strategy = (TsplCutStrategy)99,
            SupportsEndOfJob = true
        }));
        Assert.Throws<ArgumentException>(() => NewCommand(new TsplCutterProfile
        {
            SupportedCutModes = Array.Empty<PrintCutMode>(),
            SupportsEndOfJob = true
        }));
        Assert.Throws<ArgumentException>(() => NewCommand(new TsplCutterProfile
        {
            SupportedCutModes = null,
            SupportsEndOfJob = true
        }));
        Assert.Throws<ArgumentException>(() => NewCommand(new TsplCutterProfile
        {
            SupportedCutModes = new[] { PrintCutMode.Full, PrintCutMode.Full },
            SupportsEndOfJob = true
        }));
        Assert.Throws<ArgumentException>(() => NewCommand(new TsplCutterProfile
        {
            SupportedCutModes = new[] { (PrintCutMode)99 },
            SupportsEndOfJob = true
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCommand(new TsplCutterProfile
        {
            SupportsEndOfJob = true,
            CutPositionFeedDots = -1
        }));
    }

    [Fact]
    public void Invalid_print_sets_or_copies_do_not_pollute_command_record()
    {
        var command = NewCommand(BatchCutter(PrintCutMode.Full)).Size(60, 40, "dot");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Print(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Print(1, 0));
        Assert.Equal(before, command.Build().GetBytes());

        command.Print(1, 2);
        Assert.Contains("PRINT 1,2\r\n", Ascii(command));
    }

    [Fact]
    public void Immediate_position_compensation_accepts_zero_and_9999_dot_boundaries()
    {
        var zero = Ascii(NewCommand(ImmediateCutter(PrintCutMode.Full, 0))
            .Cut(new PrintCutOptions
            {
                FeedToCutPosition = true,
                FeedBeforeCutUnit = PrintFeedUnit.Dots
            }).Build().GetBytes());
        Assert.Equal("EOJ\r\nCUT\r\n", zero);

        var maximum = Ascii(NewCommand(ImmediateCutter(PrintCutMode.Full, 9999))
            .Cut(new PrintCutOptions
            {
                FeedToCutPosition = true,
                FeedBeforeCutUnit = PrintFeedUnit.Dots
            }).Build().GetBytes());
        Assert.Equal("EOJ\r\nFEED 9999\r\nEOJ\r\nCUT\r\n", maximum);

        var exact = Ascii(NewCommand(ImmediateCutter(PrintCutMode.Full, 9998))
            .Cut(new PrintCutOptions
            {
                FeedToCutPosition = true,
                FeedBeforeCutAmount = 1,
                FeedBeforeCutUnit = PrintFeedUnit.Dots
            }).Build().GetBytes());
        Assert.Equal("EOJ\r\nFEED 9999\r\nEOJ\r\nCUT\r\n", exact);
    }

    [Fact]
    public void Immediate_position_compensation_rejects_unconfirmed_position_wrong_unit_and_overflow()
    {
        Assert.Throws<NotSupportedException>(() => NewCommand(ImmediateCutter(PrintCutMode.Full))
            .Cut(new PrintCutOptions
            {
                FeedToCutPosition = true,
                FeedBeforeCutUnit = PrintFeedUnit.Dots
            }));

        Assert.Throws<NotSupportedException>(() => NewCommand(ImmediateCutter(PrintCutMode.Full, 1))
            .Cut(new PrintCutOptions
            {
                FeedToCutPosition = true,
                FeedBeforeCutUnit = PrintFeedUnit.Lines
            }));

        var command = NewCommand(ImmediateCutter(PrintCutMode.Full, 9999));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Cut(new PrintCutOptions
        {
            FeedToCutPosition = true,
            FeedBeforeCutAmount = 1,
            FeedBeforeCutUnit = PrintFeedUnit.Dots
        }));
        Assert.Empty(command.Build().GetBytes());

        Assert.Throws<ArgumentOutOfRangeException>(() => NewCommand(ImmediateCutter(PrintCutMode.Full, 10000)));
    }

    [Fact]
    public void Page_render_maps_batch_partial_cut_inside_print_boundary()
    {
        var document = OnePageDocument();
        document.Pages[0].CutAfterPrint = true;
        document.Pages[0].CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial };

        var output = Assert.Single(Provider(BatchCutter(PrintCutMode.Full, PrintCutMode.Partial)).Render(document));
        var text = Encoding.ASCII.GetString(output);

        Assert.True(text.IndexOf("SET PARTIAL_CUTTER BATCH\r\n", StringComparison.Ordinal) >= 0);
        Assert.True(text.IndexOf("SET PARTIAL_CUTTER BATCH\r\n", StringComparison.Ordinal) <
            text.IndexOf("PRINT 1,1\r\n", StringComparison.Ordinal));
        Assert.EndsWith("SET CUTTER OFF\r\nSET PARTIAL_CUTTER OFF\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_render_maps_immediate_cut_after_position_feed()
    {
        var document = OnePageDocument();
        document.Pages[0].CutAfterPrint = true;
        document.Pages[0].CutOptions = new PrintCutOptions
        {
            FeedToCutPosition = true,
            FeedBeforeCutAmount = 2,
            FeedBeforeCutUnit = PrintFeedUnit.Dots
        };

        var output = Assert.Single(Provider(ImmediateCutter(PrintCutMode.Full, 5)).Render(document));
        var text = Encoding.ASCII.GetString(output);
        var feed = text.LastIndexOf("FEED 7\r\n", StringComparison.Ordinal);
        var cut = text.LastIndexOf("CUT\r\n", StringComparison.Ordinal);

        Assert.True(feed >= 0);
        Assert.True(cut > feed);
        Assert.DoesNotContain("SET CUTTER BATCH", text);
    }

    [Theory]
    [InlineData(PrintFinishScope.EachPage, "1111")]
    [InlineData(PrintFinishScope.EachCopy, "0101")]
    [InlineData(PrintFinishScope.JobEnd, "0001")]
    public void Batch_job_cut_scopes_render_cut_on_expected_boundary_pages(PrintFinishScope scope, string expected)
    {
        var result = RenderJob(BatchCutter(PrintCutMode.Full), scope, new PrintCutOptions());

        Assert.Equal(expected.Select(value => value == '1'), result.Pages.Select(page => page.CutAfterPrint));
        foreach (var page in result.Pages)
        {
            var text = Encoding.ASCII.GetString(page.Data);
            if (page.CutAfterPrint)
            {
                Assert.Contains("SET CUTTER BATCH\r\n", text);
                Assert.Contains("SET CUTTER OFF\r\n", text);
                Assert.True(text.IndexOf("SET CUTTER BATCH\r\n", StringComparison.Ordinal) <
                    text.IndexOf("PRINT 1,1\r\n", StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void Immediate_job_feed_is_emitted_before_cut()
    {
        var result = new PrintJobRenderer().Render(Provider(ImmediateCutter(PrintCutMode.Full)),
            TwoPageDocument(), new PrintJobOptions
            {
                FeedScope = PrintFinishScope.JobEnd,
                FeedAmount = 12,
                FeedUnit = PrintFeedUnit.Dots,
                CutScope = PrintFinishScope.JobEnd,
                CutOptions = new PrintCutOptions()
            });

        var page = Assert.Single(result.Pages.Where(value => value.CutAfterPrint));
        var text = Encoding.ASCII.GetString(page.Data);
        Assert.True(text.LastIndexOf("FEED 12\r\n", StringComparison.Ordinal) >= 0);
        Assert.True(text.LastIndexOf("CUT\r\n", StringComparison.Ordinal) >
            text.LastIndexOf("FEED 12\r\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Immediate_job_feed_at_9999_dots_is_allowed_but_10000_is_rejected()
    {
        var provider = Provider(ImmediateCutter(PrintCutMode.Full));
        PrintJobRenderer.ValidateSettings(provider, new PrintJobOptions
        {
            FeedScope = PrintFinishScope.JobEnd,
            FeedAmount = 9999,
            FeedUnit = PrintFeedUnit.Dots
        }, logicalPageCount: 1);

        var error = Assert.Throws<PrintElementValidationException>(() => PrintJobRenderer.ValidateSettings(provider,
            new PrintJobOptions
            {
                FeedScope = PrintFinishScope.JobEnd,
                FeedAmount = 10000,
                FeedUnit = PrintFeedUnit.Dots
            }, logicalPageCount: 1));

        Assert.Equal("Job", error.ElementPath);
        var cause = Assert.IsType<ArgumentOutOfRangeException>(error.InnerException);
        Assert.Equal("FeedAmount", cause.ParamName);
    }

    [Theory]
    [InlineData(PrintFinishScope.EachPage)]
    [InlineData(PrintFinishScope.EachCopy)]
    [InlineData(PrintFinishScope.JobEnd)]
    public void Batch_job_rejects_positive_feed_before_rendering_cut_scope(PrintFinishScope feedScope)
    {
        var error = Assert.Throws<PrintCapabilityException>(() =>
            PrintJobRenderer.ValidateSettings(Provider(BatchCutter(PrintCutMode.Full)), new PrintJobOptions
            {
                FeedScope = feedScope,
                FeedAmount = 1,
                FeedUnit = PrintFeedUnit.Dots,
                CutScope = PrintFinishScope.JobEnd
            }, logicalPageCount: 2));

        Assert.Equal("Job", error.ElementPath);
        Assert.Contains("Batch", error.Message);
    }

    [Fact]
    public void Batch_partial_job_cut_budget_accepts_exact_total_and_rejects_one_byte_less()
    {
        var provider = Provider(BatchCutter(PrintCutMode.Full, PrintCutMode.Partial));
        var options = new PrintJobOptions
        {
            Copies = 2,
            CutScope = PrintFinishScope.JobEnd,
            CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial }
        };
        var expected = new PrintJobRenderer().Render(provider, TwoPageDocument(), options);
        var total = expected.Pages.Sum(page => (long)page.Data.Length);

        var exact = new PrintJobRenderer().Render(provider, TwoPageDocument(), options,
            maxOutputBytes: checked((int)total));
        Assert.Equal(total, exact.Pages.Sum(page => (long)page.Data.Length));
        Assert.Throws<ArgumentException>(() => new PrintJobRenderer().Render(provider, TwoPageDocument(), options,
            maxOutputBytes: checked((int)total - 1)));
    }

    [Fact]
    public void Immediate_position_compensation_is_included_in_job_budget()
    {
        var provider = Provider(ImmediateCutter(PrintCutMode.Full, 5));
        var options = new PrintJobOptions
        {
            Copies = 2,
            CutScope = PrintFinishScope.JobEnd,
            CutOptions = new PrintCutOptions
            {
                FeedToCutPosition = true,
                FeedBeforeCutAmount = 2,
                FeedBeforeCutUnit = PrintFeedUnit.Dots
            }
        };
        var expected = new PrintJobRenderer().Render(provider, TwoPageDocument(), options);
        var total = expected.Pages.Sum(page => (long)page.Data.Length);

        var exact = new PrintJobRenderer().Render(provider, TwoPageDocument(), options,
            maxOutputBytes: checked((int)total));
        Assert.Equal(total, exact.Pages.Sum(page => (long)page.Data.Length));
        Assert.Throws<ArgumentException>(() => new PrintJobRenderer().Render(provider, TwoPageDocument(), options,
            maxOutputBytes: checked((int)total - 1)));
    }

    [Theory]
    [InlineData(TsplCutStrategy.Batch)]
    [InlineData(TsplCutStrategy.Immediate)]
    public void Job_output_budget_accepts_exact_total_and_rejects_one_byte_less(TsplCutStrategy strategy)
    {
        var cutter = strategy == TsplCutStrategy.Batch
            ? BatchCutter(PrintCutMode.Full)
            : ImmediateCutter(PrintCutMode.Full);
        var provider = Provider(cutter);
        var document = TwoPageDocument();
        var options = new PrintJobOptions
        {
            Copies = 2,
            CutScope = PrintFinishScope.JobEnd,
            CutOptions = new PrintCutOptions()
        };
        var expected = new PrintJobRenderer().Render(provider, document, options);
        var total = expected.Pages.Sum(page => (long)page.Data.Length);

        var exact = new PrintJobRenderer().Render(provider, document, options,
            maxOutputBytes: checked((int)total));
        Assert.Equal(total, exact.Pages.Sum(page => (long)page.Data.Length));
        Assert.Throws<ArgumentException>(() => new PrintJobRenderer().Render(provider, document, options,
            maxOutputBytes: checked((int)total - 1)));
    }

    [Fact]
    public void Template_rejects_undeclared_cutter_before_opening_image_resource()
    {
        var resources = new CountingResources();
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            TemplateWithImage(), null, resources, new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                CutAfterPrint = true,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full }
            }));

        Assert.Equal("Pages[0]", error.SourcePath);
        Assert.Contains("TSPL", error.Message.ToUpperInvariant());
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Template_accepts_code93_and_reports_the_later_resource_element_path()
    {
        var resources = new CountingResources();
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            TemplateWithUnsupportedBarcodeAndImage(), null, resources, new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII
            }));

        Assert.Equal("Rows[0].Cells[0].Elements[1]", error.SourcePath);
        Assert.Equal(1, resources.OpenCount);
    }

    [Fact]
    public void Template_maps_batch_page_cut_to_tspl_output()
    {
        var result = new TemplateRenderer().Render(EmptyTemplate(), null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Tspl,
            Encoding = Encoding.ASCII,
            TsplMedia = new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { Cutter = BatchCutter(PrintCutMode.Partial, PrintCutMode.Full) }
            },
            CutAfterPrint = true,
            CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial }
        });

        var text = Encoding.ASCII.GetString(Assert.Single(result.Pages).Data);
        Assert.Contains("SET PARTIAL_CUTTER BATCH\r\n", text);
        Assert.Contains("PRINT 1,1\r\n", text);
    }

    private static TsplPrintCommand NewCommand(TsplCutterProfile cutter)
        => new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { Cutter = cutter }
        });

    private static TsplCutterProfile BatchCutter(params PrintCutMode[] modes)
        => new TsplCutterProfile
        {
            Strategy = TsplCutStrategy.Batch,
            SupportedCutModes = modes,
            SupportsEndOfJob = true
        };

    private static TsplCutterProfile ImmediateCutter(PrintCutMode mode, int? position = null)
        => new TsplCutterProfile
        {
            Strategy = TsplCutStrategy.Immediate,
            SupportedCutModes = new[] { mode },
            SupportsEndOfJob = true,
            CutPositionFeedDots = position
        };

    private static string Ascii(TsplPrintCommand command)
        => Encoding.ASCII.GetString(command.Build().GetBytes());

    private static string Ascii(byte[] bytes) => Encoding.ASCII.GetString(bytes);

    private static IPrintProtocolProvider Provider(TsplCutterProfile cutter)
        => PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { Cutter = cutter }
            });

    private static PrintJobRenderResult RenderJob(TsplCutterProfile cutter, PrintFinishScope scope,
        PrintCutOptions cut)
        => new PrintJobRenderer().Render(Provider(cutter), TwoPageDocument(), new PrintJobOptions
        {
            Copies = 2,
            CutScope = scope,
            CutOptions = cut
        });

    private static PrintDocument OnePageDocument()
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 64);
        page.FeedLines = 0;
        page.Add(new PrintTextElement(0, 0, "ALPHA", 24));
        return document;
    }

    private static PrintDocument TwoPageDocument()
    {
        var document = OnePageDocument();
        var page = document.AddPage(320, 64);
        page.FeedLines = 0;
        page.Add(new PrintTextElement(0, 0, "BRAVO", 24));
        return document;
    }

    private static BpTemplate EmptyTemplate()
        => new BpTemplate { Version = 1, Width = 320, Height = 64, Rows = new List<BpRow>() };

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

    private static BpTemplate TemplateWithUnsupportedBarcodeAndImage()
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
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Code93,
                                    Height = 32,
                                    Field = new BgField { Value = "1234" }
                                },
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

    private sealed class CountingResources : ITemplateResourceProvider
    {
        public int OpenCount { get; private set; }

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            OpenCount++;
            return null;
        }
    }
}
