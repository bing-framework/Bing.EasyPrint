using System;
using System.Collections.Generic;
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
/// 验证 ZPL 结构化裁切在命令、页面、任务和模板边界上的输出契约。
/// </summary>
public sealed class ZplCutterTests
{
    [Fact]
    public void Omitted_cutter_preserves_legacy_zpl_bytes_and_uses_one_segment()
    {
        var command = new ZplPrintCommand(Encoding.ASCII)
            .Start().Label(61, 40).Text(1, 2, "OK").End();

        Assert.False(command.Capabilities.SupportsIndependentOutputSegments);
        Assert.Empty(command.Capabilities.CutModes);
        var expected = "^XA\r\n^PW61\r\n^LL40\r\n" +
            "^FO1,2^A0N,30,30^FDOK^FS\r\n^XZ\r\n";
        Assert.Equal(expected, Ascii(command.Build().GetBytes()));
        Assert.Equal(expected, Ascii(command.BuildOutput().Data));
    }

    [Fact]
    public void Cutter_full_cut_writes_the_golden_sequence()
    {
        var command = NewCommand(Cutter(PrintCutMode.Full))
            .Start().Label(61, 40).Text(1, 2, "OK")
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full })
            .End();

        var first = command.Build().GetBytes();
        var second = command.Build().GetBytes();
        Assert.Equal(first, second);
        Assert.Equal("^XA\r\n^PW61\r\n^LL40\r\n" +
            "^FO1,2^A0N,30,30^FDOK^FS\r\n" +
            "^MMC\r\n^PQ1,1,0,Y\r\n^XZ\r\n", Ascii(first));
        Assert.False(command.Capabilities.SupportsIndependentOutputSegments);
    }

    [Fact]
    public void Delayed_cut_returns_an_independent_control_segment_and_rejects_legacy_build()
    {
        var command = NewCommand(DelayedCutter())
            .Start().Label(61, 40).Text(1, 2, "OK")
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full })
            .End();

        Assert.Throws<NotSupportedException>(() => command.Build());
        var output = command.BuildOutput();

        Assert.True(command.Capabilities.SupportsIndependentOutputSegments);
        Assert.Equal(2, output.Segments.Count);
        Assert.Equal(PrintOutputSegmentKind.Commands, output.Segments[0].Kind);
        Assert.Equal(PrintOutputSegmentKind.Control, output.Segments[1].Kind);
        Assert.Equal("^XA\r\n^PW61\r\n^LL40\r\n" +
            "^FO1,2^A0N,30,30^FDOK^FS\r\n^MMD\r\n^PQ1\r\n^XZ\r\n",
            Ascii(output.Segments[0].Data));
        Assert.Equal("~JK\r\n", Ascii(output.Segments[1].Data));
        Assert.Throws<NotSupportedException>(() => ReadData(output));
    }

    [Fact]
    public void Kiosk_full_cut_writes_margin_and_full_cut_marker()
    {
        var output = NewCommand(KioskCutter(PrintCutMode.Full, 4))
            .Start().Label(61, 40).Text(1, 2, "OK")
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full })
            .End().BuildOutput();

        Assert.Equal("^XA\r\n^PW61\r\n^LL40\r\n" +
            "^FO1,2^A0N,30,30^FDOK^FS\r\n" +
            "^MMK\r\n^KV0,4\r\n^PQ1\r\n^CN1\r\n^XZ\r\n", Ascii(output.Data));
    }

    [Fact]
    public void Kiosk_partial_cut_writes_declared_uncut_distance()
    {
        var output = NewCommand(KioskCutter(PrintCutMode.Full, 4, 20))
            .Start().Label(60, 40)
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Partial })
            .End().BuildOutput();

        Assert.Equal("^XA\r\n^PW60\r\n^LL40\r\n" +
            "^MMK\r\n^KV20,4\r\n^PQ1\r\n^CN0\r\n^XZ\r\n", Ascii(output.Data));
    }

    [Theory]
    [InlineData("0,4", "1")]
    [InlineData("20,4", "0")]
    public void Restricted_raw_kiosk_cut_matches_full_or_partial_profile_mode(string kioskParameters,
        string cutMode)
    {
        var raw = "^PW60\r\n^LL40\r\n^XA\r\n^MMK\r\n^KV" + kioskParameters +
            "\r\n^PQ1\r\n^CN" + cutMode + "\r\n^XZ\r\n";
        var command = NewCommand(KioskCutter(PrintCutMode.Full, 4, 20));

        command.WriteRestrictedRaw(raw);

        var output = command.BuildOutput();
        Assert.Single(output.Segments);
        Assert.Equal(raw, Ascii(output.Data));
    }

    [Fact]
    public void Restricted_raw_kiosk_mismatch_is_atomic_and_valid_batch_recovers()
    {
        var command = NewCommand(KioskCutter(PrintCutMode.Full, 4, 20));
        const string invalid = "^PW60\r\n^LL40\r\n^XA\r\n^MMK\r\n^KV0,4\r\n" +
            "^PQ1\r\n^CN0\r\n^XZ\r\n";
        const string valid = "^PW60\r\n^LL40\r\n^XA\r\n^MMK\r\n^KV0,4\r\n" +
            "^PQ1\r\n^CN1\r\n^XZ\r\n";

        var exception = Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(invalid));

        Assert.Contains("^CN", exception.Message);
        Assert.Empty(command.Build().GetBytes());
        command.WriteRestrictedRaw(valid);
        Assert.Equal(valid, Ascii(command.BuildOutput().Data));
    }

    [Fact]
    public void Restricted_raw_cutter_requires_structured_quantity_shape()
    {
        var command = NewCommand(Cutter(PrintCutMode.Full));
        const string invalid = "^PW60\r\n^LL40\r\n^XA\r\n^MMC\r\n^PQ1\r\n^XZ\r\n";
        const string valid = "^PW60\r\n^LL40\r\n^XA\r\n^MMC\r\n^PQ2,2,0,Y\r\n^XZ\r\n";

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(invalid));
        Assert.Empty(command.Build().GetBytes());

        command.WriteRestrictedRaw(valid);

        Assert.Equal(valid, Ascii(command.BuildOutput().Data));
    }

    [Fact]
    public void Restricted_raw_delayed_mode_can_be_reset_to_tear_off()
    {
        var command = NewCommand(DelayedCutter());
        const string delayed = "^PW60\r\n^LL40\r\n^XA\r\n^MMD\r\n^PQ1\r\n^XZ\r\n";
        const string tearOff = "^PW60\r\n^LL40\r\n^XA\r\n^MMD\r\n^MMT\r\n^PQ1\r\n^XZ\r\n";

        command.WriteRestrictedRaw(delayed);
        command.WriteRestrictedRaw(tearOff);

        var output = command.BuildOutput();
        Assert.Equal(new[] { PrintOutputSegmentKind.Commands, PrintOutputSegmentKind.Control,
            PrintOutputSegmentKind.Commands }, output.Segments.Select(segment => segment.Kind));
        Assert.Equal("~JK\r\n", Ascii(output.Segments[1].Data));
        Assert.Equal(tearOff, Ascii(output.Segments[2].Data));
    }

    [Fact]
    public void Restricted_raw_kiosk_state_is_shared_with_structured_end()
    {
        var command = NewCommand(KioskCutter(PrintCutMode.Full, 4, 20));
        const string prefix = "^PW60\r\n^LL40\r\n^XA\r\n^MMK\r\n^KV0,4\r\n^PQ1\r\n^CN1\r\n";

        command.WriteRestrictedRaw(prefix).End();

        Assert.Equal(prefix + "^XZ\r\n", Ascii(command.BuildOutput().Data));
    }

    [Fact]
    public void Structured_delayed_state_is_shared_with_restricted_raw_close()
    {
        var command = NewCommand(DelayedCutter())
            .Start().Label(60, 40).PrintWithCut(new PrintCutOptions());

        command.WriteRestrictedRaw("^XZ\r\n");

        var output = command.BuildOutput();
        Assert.Equal(2, output.Segments.Count);
        Assert.Equal(PrintOutputSegmentKind.Control, output.Segments[1].Kind);
        Assert.Equal("~JK\r\n", Ascii(output.Segments[1].Data));
    }

    [Fact]
    public void Controlled_end_without_explicit_cut_uses_neutral_tear_off_mode()
    {
        var command = NewCommand(Cutter(PrintCutMode.Full))
            .Start().Label(61, 40).Text(1, 2, "OK").End();

        Assert.Equal("^XA\r\n^PW61\r\n^LL40\r\n" +
            "^FO1,2^A0N,30,30^FDOK^FS\r\n" +
            "^MMT\r\n^PQ1\r\n^XZ\r\n", Ascii(command.Build().GetBytes()));
    }

    [Fact]
    public void Cutter_configuration_and_capability_are_snapshotted()
    {
        var modes = new[] { PrintCutMode.Full };
        var cutter = new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Cutter,
            SupportedCutModes = modes,
            SupportsTearOff = true
        };
        var media = new ZplMediaOptions { Profile = new ZplPrinterProfile { Cutter = cutter } };
        var command = new ZplPrintCommand(Encoding.ASCII, media);

        modes[0] = PrintCutMode.Partial;
        cutter.SupportsTearOff = false;
        cutter.SupportedCutModes = new[] { PrintCutMode.Partial };
        media.Profile.Cutter = cutter;

        Assert.Equal(new[] { PrintCutMode.Full }, command.Capabilities.CutModes);
        command.Start().Label(60, 40)
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full }).End();
        Assert.Contains("^MMC\r\n", Ascii(command.Build().GetBytes()));

        var reported = command.Capabilities.ZplCutter;
        reported.SupportedCutModes[0] = PrintCutMode.Partial;
        reported.SupportsTearOff = false;
        Assert.Equal(new[] { PrintCutMode.Full }, command.Capabilities.ZplCutter.SupportedCutModes);
        Assert.True(command.Capabilities.ZplCutter.SupportsTearOff);
    }

    [Fact]
    public void Provider_snapshots_cutter_configuration_before_rendering()
    {
        var modes = new[] { PrintCutMode.Full, PrintCutMode.Partial };
        var cutter = new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Kiosk,
            SupportedCutModes = modes,
            SupportsTearOff = true,
            KioskCutMarginMillimeters = 4,
            KioskPartialUncutMillimeters = 20
        };
        var media = Media(cutter);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: media);

        modes[0] = PrintCutMode.Partial;
        modes[1] = PrintCutMode.Full;
        cutter.SupportsTearOff = false;
        cutter.SupportedCutModes = new[] { PrintCutMode.Full };

        var document = OnePageDocument();
        document.Pages[0].CutAfterPrint = true;
        document.Pages[0].CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial };
        var output = Assert.Single(provider.RenderOutput(document));

        Assert.Contains("^KV20,4\r\n^PQ1\r\n^CN0\r\n", Ascii(output.Data));
        Assert.Equal(new[] { PrintCutMode.Full, PrintCutMode.Partial }, provider.Capabilities.CutModes);
    }

    [Fact]
    public void Build_output_is_idempotent_across_multiple_delayed_formats()
    {
        var command = NewCommand(DelayedCutter())
            .Start().Label(60, 40).PrintWithCut(new PrintCutOptions()).End()
            .Start().Label(60, 40).PrintWithCut(new PrintCutOptions()).End();

        var first = command.BuildOutput();
        var second = command.BuildOutput();

        Assert.Equal(4, first.Segments.Count);
        Assert.Equal(first.Segments.Select(segment => segment.Kind),
            second.Segments.Select(segment => segment.Kind));
        Assert.Equal(first.Segments.Select(segment => Ascii(segment.Data)),
            second.Segments.Select(segment => Ascii(segment.Data)));
        Assert.Equal(new[] { PrintOutputSegmentKind.Commands, PrintOutputSegmentKind.Control,
            PrintOutputSegmentKind.Commands, PrintOutputSegmentKind.Control },
            first.Segments.Select(segment => segment.Kind));
        Assert.Equal(new[] { "~JK\r\n", "~JK\r\n" },
            first.Segments.Where(segment => segment.Kind == PrintOutputSegmentKind.Control)
                .Select(segment => Ascii(segment.Data)));
        Assert.Throws<NotSupportedException>(() => command.Build());
    }

    [Fact]
    public void Failed_cut_validation_does_not_pollute_the_format_and_valid_cut_recovers()
    {
        var command = NewCommand(Cutter(PrintCutMode.Full)).Start().Label(60, 40);

        Assert.Throws<NotSupportedException>(() => command.PrintWithCut(
            new PrintCutOptions { Mode = PrintCutMode.Partial }));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.PrintWithCut(
            new PrintCutOptions { Mode = PrintCutMode.Full }, 0));
        Assert.Throws<NotSupportedException>(() => command.PrintWithCut(
            new PrintCutOptions { Mode = PrintCutMode.Full, FeedToCutPosition = true }));

        command.PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full }).End();
        var text = Ascii(command.Build().GetBytes());
        Assert.Contains("^MMC\r\n^PQ1,1,0,Y\r\n", text);
        Assert.DoesNotContain("^MMD", text);
        Assert.DoesNotContain("^MMK", text);
    }

    [Fact]
    public void Invalid_cutter_profiles_are_rejected_at_construction()
    {
        Assert.Throws<NotSupportedException>(() => NewCommand(new ZplCutterProfile()));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCommand(new ZplCutterProfile
        {
            Strategy = (ZplCutStrategy)99,
            SupportsTearOff = true
        }));
        Assert.Throws<ArgumentException>(() => NewCommand(new ZplCutterProfile
        {
            SupportedCutModes = Array.Empty<PrintCutMode>(),
            SupportsTearOff = true
        }));
        Assert.Throws<ArgumentException>(() => NewCommand(new ZplCutterProfile
        {
            SupportedCutModes = null,
            SupportsTearOff = true
        }));
        Assert.Throws<ArgumentException>(() => NewCommand(new ZplCutterProfile
        {
            SupportedCutModes = new[] { PrintCutMode.Full, PrintCutMode.Full },
            SupportsTearOff = true
        }));
        Assert.Throws<NotSupportedException>(() => NewCommand(new ZplCutterProfile
        {
            SupportedCutModes = new[] { PrintCutMode.Full, PrintCutMode.Partial },
            SupportsTearOff = true
        }));
        Assert.Throws<NotSupportedException>(() => NewCommand(new ZplCutterProfile
        {
            KioskCutMarginMillimeters = 4,
            SupportsTearOff = true
        }));
        Assert.Throws<ArgumentException>(() => NewCommand(new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Kiosk,
            SupportedCutModes = new[] { PrintCutMode.Partial },
            SupportsTearOff = true,
            KioskCutMarginMillimeters = 4
        }));
        Assert.Throws<ArgumentException>(() => new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile
                {
                    SupportsCut = false,
                    Cutter = Cutter(PrintCutMode.Full)
                }
            }));
    }

    [Fact]
    public void Kiosk_profile_validates_margin_and_partial_distance_boundaries()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCommand(KioskCutter(PrintCutMode.Full, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCommand(KioskCutter(PrintCutMode.Full, 10)));
        Assert.Throws<ArgumentException>(() => NewCommand(new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Kiosk,
            SupportedCutModes = new[] { PrintCutMode.Full },
            SupportsTearOff = true,
            KioskCutMarginMillimeters = 4,
            KioskPartialUncutMillimeters = 20
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCommand(KioskCutter(
            PrintCutMode.Full, 4, 9)));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCommand(KioskCutter(
            PrintCutMode.Full, 4, 61)));
        Assert.Throws<ArgumentException>(() => NewCommand(new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Kiosk,
            SupportedCutModes = new[] { PrintCutMode.Partial },
            SupportsTearOff = true,
            KioskCutMarginMillimeters = 4
        }));

        NewCommand(KioskCutter(PrintCutMode.Full, 2, 10));
        NewCommand(KioskCutter(PrintCutMode.Full, 9, 60));
    }

    [Fact]
    public void Quantity_limits_accept_maximum_and_reject_overflow_without_recording()
    {
        var maximum = NewCommand(Cutter(PrintCutMode.Full)).Start().Label(60, 40)
            .PrintWithCut(new PrintCutOptions(), 99999999).End();
        Assert.Contains("^PQ99999999,99999999,0,Y\r\n", Ascii(maximum.Build().GetBytes()));

        var command = NewCommand(Cutter(PrintCutMode.Full)).Start().Label(60, 40);
        Assert.Throws<ArgumentOutOfRangeException>(() => command.PrintWithCut(
            new PrintCutOptions(), 100000000));
        command.End();
        Assert.Contains("^MMT\r\n^PQ1\r\n", Ascii(command.Build().GetBytes()));
    }

    [Fact]
    public void Page_render_keeps_legacy_cut_and_maps_controlled_cut()
    {
        var legacy = OnePageDocument();
        legacy.Pages[0].CutAfterPrint = true;
        var legacyText = Ascii(Assert.Single(Provider(null).Render(legacy)));
        Assert.Contains("^PQ1\r\n^CN\r\n^XZ\r\n", legacyText);

        var controlled = OnePageDocument();
        controlled.Pages[0].CutAfterPrint = true;
        controlled.Pages[0].CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full };
        var output = Assert.Single(Provider(Cutter(PrintCutMode.Full)).RenderOutput(controlled));
        Assert.Single(output.Segments);
        Assert.Contains("^MMC\r\n^PQ1,1,0,Y\r\n^XZ\r\n", Ascii(output.Data));
    }

    [Fact]
    public void Delayed_page_render_preserves_control_file_and_legacy_render_rejects_merge()
    {
        var document = OnePageDocument();
        document.Pages[0].CutAfterPrint = true;
        document.Pages[0].CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full };
        var provider = Provider(DelayedCutter());

        var output = Assert.Single(provider.RenderOutput(document));
        Assert.Equal(2, output.Segments.Count);
        Assert.Equal(PrintOutputSegmentKind.Control, output.Segments[1].Kind);
        Assert.Equal("~JK\r\n", Ascii(output.Segments[1].Data));
        Assert.Throws<NotSupportedException>(() => ReadData(output));

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(document));
        Assert.Equal("Pages[0]", error.ElementPath);
    }

    [Theory]
    [InlineData(PrintFinishScope.EachPage, "1111")]
    [InlineData(PrintFinishScope.EachCopy, "0101")]
    [InlineData(PrintFinishScope.JobEnd, "0001")]
    public void Cutter_job_cut_scopes_render_on_expected_boundary_pages(PrintFinishScope scope, string expected)
    {
        var result = RenderJob(Cutter(PrintCutMode.Full), scope, new PrintCutOptions());

        Assert.Equal(expected.Select(value => value == '1'),
            result.Pages.Select(page => page.CutAfterPrint));
        foreach (var page in result.Pages.Where(page => page.CutAfterPrint))
        {
            var text = Ascii(page.Segments.Single().Data);
            Assert.Contains("^MMC\r\n^PQ1,1,0,Y\r\n", text);
        }
    }

    [Fact]
    public void Delayed_job_keeps_control_segment_and_exact_budget_accounts_for_it()
    {
        var provider = Provider(DelayedCutter());
        var document = TwoPageDocument();
        var options = new PrintJobOptions
        {
            Copies = 2,
            CutScope = PrintFinishScope.JobEnd,
            CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full }
        };
        var expected = new PrintJobRenderer().Render(provider, document, options);
        var cutPage = Assert.Single(expected.Pages.Where(page => page.CutAfterPrint));
        Assert.Equal(2, cutPage.Segments.Count);
        Assert.Equal(PrintOutputSegmentKind.Control, cutPage.Segments[1].Kind);
        Assert.Throws<NotSupportedException>(() => ReadData(cutPage.Output));

        var total = expected.Pages.Sum(page => page.ByteLength);
        var exact = new PrintJobRenderer().Render(provider, TwoPageDocument(), options,
            CancellationToken.None, maxPages: int.MaxValue, maxOutputBytes: checked((int)total));
        Assert.Equal(total, exact.Pages.Sum(page => page.ByteLength));
        Assert.Throws<ArgumentException>(() => new PrintJobRenderer().Render(provider,
            TwoPageDocument(), options, CancellationToken.None, maxPages: int.MaxValue,
            maxOutputBytes: checked((int)total - 1)));
    }

    [Fact]
    public void Provider_job_and_template_render_honor_cancellation_entry_points()
    {
        var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => Provider(Cutter(PrintCutMode.Full))
            .RenderOutput(OnePageDocument(), source.Token));
        Assert.Throws<OperationCanceledException>(() => new PrintJobRenderer().Render(
            Provider(Cutter(PrintCutMode.Full)), OnePageDocument(), new PrintJobOptions(), source.Token));
        Assert.Throws<OperationCanceledException>(() => new TemplateRenderer().Render(
            EmptyTemplate(), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                ZplMedia = Media(DelayedCutter()),
                CutAfterPrint = true,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full }
            }, cancellationToken: source.Token));
    }

    [Fact]
    public void Template_page_preserves_delayed_segments_and_reports_byte_length()
    {
        var result = new TemplateRenderer().RenderWithLayout(EmptyTemplate(), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                ZplMedia = Media(DelayedCutter()),
                CutAfterPrint = true,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full }
            });
        var page = Assert.Single(result.Pages);

        Assert.Equal(2, page.Segments.Count);
        Assert.Equal(page.Segments.Sum(segment => (long)segment.ByteLength), page.ByteLength);
        Assert.Equal(PrintOutputSegmentKind.Control, page.Segments[1].Kind);
        Assert.Equal("~JK\r\n", Ascii(page.Segments[1].Data));
        Assert.Throws<NotSupportedException>(() => ReadData(page.Output));
    }

    [Fact]
    public void Template_job_carries_cutter_output_and_physical_page_metadata()
    {
        var result = new TemplateRenderer().RenderWithLayout(TwoPageTemplate(), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                ZplMedia = Media(DelayedCutter()),
                Job = new PrintJobOptions
                {
                    Copies = 2,
                    CutScope = PrintFinishScope.EachPage,
                    CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full }
                }
            });

        Assert.Equal(4, result.PageCount);
        Assert.All(result.Pages, page =>
        {
            Assert.Equal(2, page.Segments.Count);
            Assert.Equal(PrintOutputSegmentKind.Control, page.Segments[1].Kind);
            Assert.True(page.CutAfterPrint.GetValueOrDefault());
            Assert.NotNull(page.SourcePageIndex);
            Assert.NotNull(page.CopyIndex);
        });
    }

    [Fact]
    public void Legacy_provider_is_adapted_to_one_commands_segment_without_new_interface_members()
    {
        var expected = new byte[] { 0x5A, 0x50, 0x4C };
        var provider = new LegacyOutputProvider(expected);

        var output = Assert.Single(provider.RenderOutput(OnePageDocument()));

        Assert.Single(output.Segments);
        Assert.Equal(PrintOutputSegmentKind.Commands, output.Segments[0].Kind);
        Assert.Equal(expected, output.Data);
        Assert.NotSame(expected, output.Data);
        Assert.True(provider.RenderCalled);
    }

    [Fact]
    public void Custom_multi_segment_provider_keeps_segments_and_clones_each_job_copy()
    {
        var provider = new MultiSegmentProvider();
        var result = new PrintJobRenderer().Render(provider, OnePageDocument(),
            new PrintJobOptions { Copies = 2 });

        Assert.Equal(2, result.Pages.Count);
        Assert.Equal(1, provider.RenderOutputCalls);
        Assert.All(result.Pages, page =>
        {
            Assert.Equal(2, page.Segments.Count);
            Assert.Equal(PrintOutputSegmentKind.Commands, page.Segments[0].Kind);
            Assert.Equal(PrintOutputSegmentKind.Control, page.Segments[1].Kind);
        });
        var first = result.Pages[0].Segments[0].Data;
        var second = result.Pages[1].Segments[0].Data;
        Assert.NotSame(first, second);
        first[0] = 0x7F;
        Assert.Equal(0x41, second[0]);
    }

    [Fact]
    public void Canceled_legacy_provider_is_rejected_before_render_is_called()
    {
        var provider = new LegacyOutputProvider(new byte[] { 0x01 });
        var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => provider.RenderOutput(
            OnePageDocument(), source.Token));
        Assert.False(provider.RenderCalled);
    }

    [Fact]
    public void Output_segment_and_page_constructors_copy_and_validate_inputs()
    {
        var input = new byte[] { 0x10, 0x20 };
        var segment = new PrintOutputSegment(input, PrintOutputSegmentKind.Control);
        input[0] = 0x7F;
        segment.Data[1] = 0x30;

        Assert.Equal(new byte[] { 0x10, 0x30 }, segment.Data);
        Assert.Equal(new byte[] { 0x7F, 0x20 }, input);
        Assert.Throws<ArgumentNullException>(() => new PrintOutputSegment(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintOutputSegment(
            new byte[] { 0x01 }, (PrintOutputSegmentKind)99));
        Assert.Throws<ArgumentNullException>(() => new PrintPageOutput(null));
        Assert.Throws<ArgumentException>(() => new PrintPageOutput(
            Array.Empty<PrintOutputSegment>()));
        Assert.Throws<ArgumentException>(() => new PrintPageOutput(
            new PrintOutputSegment[] { null }));
    }

    [Fact]
    public void Controlled_command_rejects_invalid_state_without_snapshotting_open_format_and_recovers()
    {
        var command = NewCommand(Cutter(PrintCutMode.Full));

        Assert.Throws<InvalidOperationException>(() => command.Print());
        Assert.Throws<InvalidOperationException>(() => command.End());

        command.Start().Label(60, 40);
        Assert.Throws<InvalidOperationException>(() => command.Start());
        Assert.Throws<InvalidOperationException>(() => command.Build());
        Assert.Throws<InvalidOperationException>(() => command.BuildOutput());

        command.End();
        command.Start().Label(60, 40)
            .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full }).End();
        var text = Ascii(command.Build().GetBytes());
        Assert.Contains("^MMT\r\n^PQ1\r\n", text);
        Assert.Contains("^MMC\r\n^PQ1,1,0,Y\r\n", text);
    }

    private static ZplPrintCommand NewCommand(ZplCutterProfile cutter)
        => new ZplPrintCommand(Encoding.ASCII, Media(cutter));

    private static ZplMediaOptions Media(ZplCutterProfile cutter)
        => new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { Cutter = cutter }
        };

    private static ZplCutterProfile Cutter(params PrintCutMode[] modes)
        => new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Cutter,
            SupportedCutModes = modes,
            SupportsTearOff = true
        };

    private static ZplCutterProfile DelayedCutter()
        => new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Delayed,
            SupportedCutModes = new[] { PrintCutMode.Full },
            SupportsTearOff = true
        };

    private static ZplCutterProfile KioskCutter(PrintCutMode mode, int margin,
        int? partialUncut = null)
        => new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Kiosk,
            SupportedCutModes = partialUncut.HasValue
                ? new[] { PrintCutMode.Full, PrintCutMode.Partial }
                : new[] { mode },
            SupportsTearOff = true,
            KioskCutMarginMillimeters = margin,
            KioskPartialUncutMillimeters = partialUncut
        };

    private static string Ascii(ZplPrintCommand command)
        => Encoding.ASCII.GetString(command.Build().GetBytes());

    private static string Ascii(byte[] bytes)
        => Encoding.ASCII.GetString(bytes);

    private static byte[] ReadData(PrintPageOutput output)
        => output.Data;

    private static IPrintProtocolProvider Provider(ZplCutterProfile cutter)
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: Media(cutter));

    private static PrintJobRenderResult RenderJob(ZplCutterProfile cutter,
        PrintFinishScope scope, PrintCutOptions cut)
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
        page.CutAfterPrint = false;
        page.Add(new PrintTextElement(0, 0, "ALPHA", 24));
        return document;
    }

    private static PrintDocument TwoPageDocument()
    {
        var document = OnePageDocument();
        var page = document.AddPage(320, 64);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintTextElement(0, 0, "BRAVO", 24));
        return document;
    }

    private static BpTemplate EmptyTemplate()
        => new BpTemplate { Version = 1, Width = 320, Height = 64, Rows = new List<BpRow>() };

    private static BpTemplate TwoPageTemplate()
        => new BpTemplate
        {
            Version = 3,
            Width = 320,
            Height = 48,
            Rows = new List<BpRow> { TextRow("A"), TextRow("B"), TextRow("C") }
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

    private sealed class LegacyOutputProvider : IPrintProtocolProvider
    {
        private readonly byte[] _data;

        internal LegacyOutputProvider(byte[] data) => _data = data;

        public PrintProtocol Protocol => PrintProtocol.Cpcl;
        public PrintCapabilities Capabilities { get; } = new PrintCapabilities(
            PrintProtocol.Cpcl, 203, true, true, true, true, false);
        public bool RenderCalled { get; private set; }

        public void Validate(PrintDocument document) { }

        public IReadOnlyList<byte[]> Render(PrintDocument document,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RenderCalled = true;
            cancellationToken.ThrowIfCancellationRequested();
            return new[] { (byte[])_data.Clone() };
        }
    }

    private sealed class MultiSegmentProvider : IPrintOutputProtocolProvider
    {
        public PrintProtocol Protocol => PrintProtocol.Cpcl;
        public PrintCapabilities Capabilities { get; } = new PrintCapabilities(
            PrintProtocol.Cpcl, 203, true, true, true, true, false);
        public int RenderOutputCalls { get; private set; }

        public void Validate(PrintDocument document) { }

        public IReadOnlyList<byte[]> Render(PrintDocument document,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new[] { new byte[] { 0x41, 0x42 } };
        }

        public IReadOnlyList<PrintPageOutput> RenderOutput(PrintDocument document,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenderOutputCalls++;
            return new[]
            {
                new PrintPageOutput(new[]
                {
                    new PrintOutputSegment(new byte[] { 0x41 }),
                    new PrintOutputSegment(new byte[] { 0x42 }, PrintOutputSegmentKind.Control)
                })
            };
        }
    }
}
