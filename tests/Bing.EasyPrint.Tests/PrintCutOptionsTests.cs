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
/// Covers explicit ESC/POS cut options and their page, job, and template boundaries.
/// </summary>
public sealed class PrintCutOptionsTests
{
    [Fact]
    public void Cut_options_have_stable_defaults_and_validate()
    {
        var options = new PrintCutOptions();

        Assert.Equal(PrintCutMode.Full, options.Mode);
        Assert.False(options.FeedToCutPosition);
        Assert.Equal(0, options.FeedBeforeCutAmount);
        Assert.Equal(PrintFeedUnit.MotionUnits, options.FeedBeforeCutUnit);
        options.Validate();
    }

    [Fact]
    public void Cut_options_reject_invalid_mode()
    {
        var options = new PrintCutOptions { Mode = (PrintCutMode)99 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Cut_options_reject_invalid_feed_unit()
    {
        var options = new PrintCutOptions { FeedBeforeCutUnit = (PrintFeedUnit)99 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Cut_options_reject_negative_amount()
    {
        var options = new PrintCutOptions { FeedBeforeCutAmount = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Cut_options_require_feed_to_cut_position_for_positive_amount()
    {
        var options = new PrintCutOptions { FeedBeforeCutAmount = 1 };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Full_cut_writes_the_legacy_golden_bytes()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Cut(new PrintCutOptions { Mode = PrintCutMode.Full })
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x56, 0x00 }, bytes);
    }

    [Fact]
    public void Partial_cut_writes_the_legacy_golden_bytes()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Cut(new PrintCutOptions { Mode = PrintCutMode.Partial })
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x56, 0x01 }, bytes);
    }

    [Fact]
    public void Function_b_zero_amount_still_writes_four_bytes()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
                new EscPosDeviceOptions { Profile = FeedAndCutProfile() })
            .Cut(new PrintCutOptions { FeedToCutPosition = true })
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x56, 0x41, 0x00 }, bytes);
    }

    [Fact]
    public void Function_b_encodes_motion_units_and_cut_mode()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
                new EscPosDeviceOptions { Profile = FeedAndCutProfile() })
            .Cut(new PrintCutOptions
            {
                Mode = PrintCutMode.Partial,
                FeedToCutPosition = true,
                FeedBeforeCutAmount = 7
            })
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x56, 0x42, 0x07 }, bytes);
    }

    [Fact]
    public void Function_b_rounds_dot_compensation_up_using_profile_units()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
                new EscPosDeviceOptions { Profile = FeedAndCutProfile(180, 60, 144) })
            .Cut(new PrintCutOptions
            {
                FeedToCutPosition = true,
                FeedBeforeCutAmount = 10,
                FeedBeforeCutUnit = PrintFeedUnit.Dots
            })
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1D, 0x56, 0x41, 0x18 }, bytes);
    }

    [Fact]
    public void Function_b_rejects_lines_even_when_amount_is_zero()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = FeedAndCutProfile() });

        Assert.Throws<NotSupportedException>(() => command.Cut(new PrintCutOptions
        {
            FeedToCutPosition = true,
            FeedBeforeCutUnit = PrintFeedUnit.Lines
        }));
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Function_b_requires_an_explicit_profile_capability()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII);

        Assert.Throws<NotSupportedException>(() => command.Cut(new PrintCutOptions
        {
            FeedToCutPosition = true
        }));
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Motion_compensation_over_255_is_rejected_without_polluting_build()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = FeedAndCutProfile() });

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Cut(new PrintCutOptions
        {
            FeedToCutPosition = true,
            FeedBeforeCutAmount = 256
        }));
        var bytes = command.Text("OK").Build().GetBytes();

        Assert.Equal(new byte[] { (byte)'O', (byte)'K', 0x0A }, bytes);
        Assert.Equal(bytes, command.Build().GetBytes());
    }

    [Fact]
    public void Dot_compensation_over_255_after_conversion_is_rejected_without_polluting_build()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = FeedAndCutProfile(203, 203, 203) });

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Cut(new PrintCutOptions
        {
            FeedToCutPosition = true,
            FeedBeforeCutAmount = 256,
            FeedBeforeCutUnit = PrintFeedUnit.Dots
        }));
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Null_supported_modes_expose_full_and_partial_capabilities()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII);

        Assert.Equal(new[] { PrintCutMode.Full, PrintCutMode.Partial }, command.Capabilities.CutModes);
        Assert.True(command.Capabilities.SupportsCut);
    }

    [Fact]
    public void Empty_supported_modes_disable_legacy_and_explicit_cutting()
    {
        var profile = FeedAndCutProfile();
        profile.SupportedCutModes = Array.Empty<PrintCutMode>();
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = profile });

        Assert.False(command.Capabilities.SupportsCut);
        Assert.Empty(command.Capabilities.CutModes);
        Assert.Throws<NotSupportedException>(() => command.Cut());
    }

    [Fact]
    public void Partial_only_profile_rejects_legacy_full_cut_without_auto_conversion()
    {
        var profile = FeedAndCutProfile();
        profile.SupportedCutModes = new[] { PrintCutMode.Partial };
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = profile });

        Assert.Throws<NotSupportedException>(() => command.Cut());
        Assert.Equal(new byte[] { 0x1D, 0x56, 0x01 }, command.Cut(true).Build().GetBytes());
    }

    [Fact]
    public void Profile_rejects_duplicate_or_unknown_supported_modes()
    {
        var duplicate = FeedAndCutProfile();
        duplicate.SupportedCutModes = new[] { PrintCutMode.Full, PrintCutMode.Full };
        Assert.Throws<ArgumentException>(() => new EscPosPrintCommand(Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = duplicate }));

        var unknown = FeedAndCutProfile();
        unknown.SupportedCutModes = new[] { (PrintCutMode)99 };
        Assert.Throws<ArgumentException>(() => new EscPosPrintCommand(Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = unknown }));
    }

    [Fact]
    public void Profile_snapshot_copies_modes_and_feed_capabilities()
    {
        var modes = new[] { PrintCutMode.Partial };
        var profile = FeedAndCutProfile();
        profile.SupportedCutModes = modes;
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = profile });

        modes[0] = PrintCutMode.Full;
        profile.SupportsFeedAndCut = false;

        Assert.Equal(new[] { PrintCutMode.Partial }, command.Capabilities.CutModes);
        Assert.Equal(new byte[] { 0x1D, 0x56, 0x42, 0x00 }, command.Cut(new PrintCutOptions
        {
            Mode = PrintCutMode.Partial,
            FeedToCutPosition = true
        }).Build().GetBytes());
    }

    [Fact]
    public void Cut_capabilities_expose_declared_units_and_motion_range()
    {
        var profile = FeedAndCutProfile();
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = profile });

        Assert.Equal(new[] { PrintFeedUnit.MotionUnits, PrintFeedUnit.Dots }, command.Capabilities.CutFeedUnits);
        Assert.True(command.Capabilities.CutFeedMotionUnits.Contains(0));
        Assert.True(command.Capabilities.CutFeedMotionUnits.Contains(255));
        Assert.False(command.Capabilities.CutFeedMotionUnits.Contains(256));

        var templateCapabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            Encoding = Encoding.ASCII,
            EscPosDevice = new EscPosDeviceOptions { Profile = profile }
        });
        Assert.Equal(command.Capabilities.CutModes, templateCapabilities.CutModes);
        Assert.Equal(command.Capabilities.CutFeedUnits, templateCapabilities.CutFeedUnits);
        Assert.Equal(command.Capabilities.CutFeedMotionUnits.Minimum,
            templateCapabilities.CutFeedMotionUnits.Minimum);
        Assert.Equal(command.Capabilities.CutFeedMotionUnits.Maximum,
            templateCapabilities.CutFeedMotionUnits.Maximum);

        var withoutFeed = new EscPosPrintCommand(Encoding.ASCII);
        Assert.Empty(withoutFeed.Capabilities.CutFeedUnits);
        Assert.Null(withoutFeed.Capabilities.CutFeedMotionUnits);
    }

    [Fact]
    public void Cut_options_snapshot_is_independent_of_source_mutation()
    {
        var source = new PrintCutOptions { Mode = PrintCutMode.Partial };
        var snapshot = source.Snapshot();
        source.Mode = PrintCutMode.Full;

        Assert.Equal(PrintCutMode.Partial, snapshot.Mode);
        Assert.Equal(new byte[] { 0x1D, 0x56, 0x01 },
            new EscPosPrintCommand(Encoding.ASCII).Cut(snapshot).Build().GetBytes());
    }

    [Fact]
    public void Provider_page_emits_explicit_function_b_cut()
    {
        var document = OnePageDocument();
        var page = document.Pages[0];
        page.CutAfterPrint = true;
        page.CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial, FeedToCutPosition = true };
        var provider = EscPosProvider(FeedAndCutProfile());

        var bytes = Assert.Single(provider.Render(document));

        Assert.Equal(new byte[] { 0x1D, 0x56, 0x42, 0x00 }, bytes.Skip(bytes.Length - 4).ToArray());
    }

    [Fact]
    public void Provider_rejects_page_cut_options_when_cut_is_disabled()
    {
        var document = OnePageDocument();
        document.Pages[0].CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial };

        var error = Assert.Throws<PrintElementValidationException>(() =>
            EscPosProvider(FeedAndCutProfile()).Validate(document));

        Assert.Equal("Pages[0]", error.ElementPath);
    }

    [Fact]
    public void Non_escpos_provider_rejects_explicit_cut_options()
    {
        var document = OnePageDocument();
        document.Pages[0].CutAfterPrint = true;
        document.Pages[0].CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial };

        var error = Assert.Throws<PrintCapabilityException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Validate(document));

        Assert.Equal("Pages[0]", error.ElementPath);
    }

    [Fact]
    public void Job_rejects_cut_options_without_a_cut_scope()
    {
        var job = new PrintJobOptions
        {
            CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial }
        };

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintJobRenderer.ValidateSettings(EscPosProvider(FeedAndCutProfile()), job));

        Assert.Equal("Job", error.ElementPath);
    }

    [Fact]
    public void Job_emits_partial_function_b_at_each_copy_boundary()
    {
        var result = new PrintJobRenderer().Render(EscPosProvider(FeedAndCutProfile()), TwoPageDocument(),
            new PrintJobOptions
            {
                Copies = 2,
                CutScope = PrintFinishScope.EachCopy,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial, FeedToCutPosition = true }
            });

        Assert.Equal(new[] { false, true, false, true }, result.Pages.Select(page => page.CutAfterPrint));
        Assert.Equal(2, result.Pages.Count(page => ContainsSequence(page.Data, 0x1D, 0x56, 0x42, 0x00)));
    }

    [Fact]
    public void Job_emits_explicit_cut_only_at_job_end()
    {
        var result = new PrintJobRenderer().Render(EscPosProvider(FeedAndCutProfile()), TwoPageDocument(),
            new PrintJobOptions
            {
                Copies = 2,
                CutScope = PrintFinishScope.JobEnd,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial, FeedToCutPosition = true }
            });

        Assert.Equal(new[] { false, false, false, true }, result.Pages.Select(page => page.CutAfterPrint));
        Assert.Single(result.Pages.Where(page => ContainsSequence(page.Data, 0x1D, 0x56, 0x42, 0x00)));
    }

    [Fact]
    public void Job_output_budget_counts_four_byte_function_b_cut()
    {
        var provider = EscPosProvider(FeedAndCutProfile());
        var document = OnePageDocument();
        var options = new PrintJobOptions
        {
            CutScope = PrintFinishScope.JobEnd,
            CutOptions = new PrintCutOptions { FeedToCutPosition = true }
        };
        var expected = new PrintJobRenderer().Render(provider, document, options);
        var bytes = Assert.Single(expected.Pages).Data;

        var exact = new PrintJobRenderer().Render(provider, document, options,
            maxOutputBytes: bytes.Length);
        Assert.Equal(bytes, Assert.Single(exact.Pages).Data);
        Assert.Throws<ArgumentException>(() => new PrintJobRenderer().Render(provider, document, options,
            maxOutputBytes: bytes.Length - 1));
    }

    [Fact]
    public void Template_default_escpos_cut_matches_explicit_full_cut()
    {
        var template = EmptyTemplate();
        var defaultResult = new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            Encoding = Encoding.ASCII,
            FeedLines = 0
        });
        var explicitResult = new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            Encoding = Encoding.ASCII,
            FeedLines = 0,
            CutAfterPrint = true,
            CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full }
        });

        Assert.Equal(Assert.Single(defaultResult.Pages).Data, Assert.Single(explicitResult.Pages).Data);
        Assert.Equal(new byte[] { 0x1D, 0x56, 0x00 },
            Assert.Single(explicitResult.Pages).Data.Skip(Assert.Single(explicitResult.Pages).Data.Length - 3).ToArray());
    }

    [Fact]
    public void Template_cut_options_inherit_escpos_default_cut_true()
    {
        var result = new TemplateRenderer().Render(EmptyTemplate(), null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            Encoding = Encoding.ASCII,
            FeedLines = 0,
            EscPosDevice = new EscPosDeviceOptions { Profile = FeedAndCutProfile() },
            CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial, FeedToCutPosition = true }
        });

        var bytes = Assert.Single(result.Pages).Data;
        Assert.Equal(new byte[] { 0x1D, 0x56, 0x42, 0x00 }, bytes.Skip(bytes.Length - 4).ToArray());
    }

    [Fact]
    public void Template_cut_options_conflict_with_cut_false()
    {
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(EmptyTemplate(), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                Encoding = Encoding.ASCII,
                CutAfterPrint = false,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial }
            }));
    }

    [Fact]
    public void Template_cut_options_conflict_with_job_options()
    {
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(EmptyTemplate(), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                Encoding = Encoding.ASCII,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial },
                Job = new PrintJobOptions { CutScope = PrintFinishScope.JobEnd }
            }));
    }

    [Fact]
    public void Template_job_cut_options_are_forwarded_to_job_renderer()
    {
        var result = new TemplateRenderer().Render(EmptyTemplate(), null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            Encoding = Encoding.ASCII,
            Job = new PrintJobOptions
            {
                CutScope = PrintFinishScope.JobEnd,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial, FeedToCutPosition = true }
            },
            EscPosDevice = new EscPosDeviceOptions { Profile = FeedAndCutProfile() }
        });

        var bytes = Assert.Single(result.Pages).Data;
        Assert.Equal(new byte[] { 0x1D, 0x56, 0x42, 0x00 }, bytes.Skip(bytes.Length - 4).ToArray());
    }

    [Fact]
    public void Template_zpl_cut_options_without_cutter_are_rejected_before_resource_access()
    {
        var resources = new CountingResources();
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(TemplateWithImage(), null, resources,
            new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                CutAfterPrint = true,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Full }
            }));

        Assert.Equal("Pages[0]", error.SourcePath);
        Assert.Contains("须声明 Cutter 契约", error.Message);
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Template_job_function_b_capability_is_rejected_before_resource_access()
    {
        var resources = new CountingResources();
        Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(TemplateWithImage(), null,
            resources, new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                Encoding = Encoding.ASCII,
                Job = new PrintJobOptions
                {
                    CutScope = PrintFinishScope.JobEnd,
                    CutOptions = new PrintCutOptions { FeedToCutPosition = true }
                }
            }));

        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Template_job_default_full_cut_is_rejected_before_kiosk_image_resource_access()
    {
        var resources = new CountingResources();
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            TemplateWithImage(), null, resources, new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                ZplMedia = ZplMedia(KioskCutter(PrintCutMode.Partial, 20)),
                Job = new PrintJobOptions { CutScope = PrintFinishScope.JobEnd, CutOptions = null }
            }));

        Assert.Equal("Job.CutOptions", error.SourcePath);
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Kiosk_partial_job_cut_options_pass_public_validation()
    {
        PrintJobRenderer.ValidateSettings(ZplProvider(KioskCutter(PrintCutMode.Partial, 20)),
            new PrintJobOptions
            {
                CutScope = PrintFinishScope.JobEnd,
                CutOptions = new PrintCutOptions { Mode = PrintCutMode.Partial }
            });
    }

    [Fact]
    public void Controlled_full_job_cut_uses_the_default_when_options_are_omitted()
    {
        PrintJobRenderer.ValidateSettings(ZplProvider(new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Cutter,
            SupportedCutModes = new[] { PrintCutMode.Full },
            SupportsTearOff = true
        }), new PrintJobOptions { CutScope = PrintFinishScope.JobEnd });
    }

    [Fact]
    public void Zpl_without_cutter_keeps_legacy_default_job_cut_bytes()
    {
        var result = new PrintJobRenderer().Render(ZplProvider(), OnePageDocument(),
            new PrintJobOptions { CutScope = PrintFinishScope.JobEnd });

        Assert.Equal(Encoding.ASCII.GetBytes(
            "^XA\r\n^PW320\r\n^LL64\r\n" +
            "^FO0,0^A0N,24,24^FDALPHA^FS\r\n" +
            "^PQ1\r\n^CN\r\n^XZ\r\n"), Assert.Single(result.Pages).Data);
    }

    private static IPrintProtocolProvider ZplProvider(ZplCutterProfile cutter = null)
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: cutter == null ? null : ZplMedia(cutter));

    private static ZplMediaOptions ZplMedia(ZplCutterProfile cutter)
        => new ZplMediaOptions { Profile = new ZplPrinterProfile { Cutter = cutter } };

    private static ZplCutterProfile KioskCutter(PrintCutMode mode, int partialUncut)
        => new ZplCutterProfile
        {
            Strategy = ZplCutStrategy.Kiosk,
            SupportedCutModes = new[] { mode },
            SupportsTearOff = true,
            KioskPartialUncutMillimeters = partialUncut,
            KioskCutMarginMillimeters = 4
        };

    private static EscPosPrinterProfile FeedAndCutProfile(int horizontalDpi = 203,
        int verticalDpi = 203, int motionUnitsPerInch = 203)
        => new EscPosPrinterProfile
        {
            Name = "feed-and-cut-test",
            ImageCommand = EscPosImageCommand.BitImageEscStar,
            BitImageMode = EscPosBitImageMode.SingleDensity8,
            HorizontalDpi = horizontalDpi,
            VerticalDpi = verticalDpi,
            MotionUnitsPerInch = motionUnitsPerInch,
            SupportsCut = true,
            SupportsFeedAndCut = true
        };

    private static IPrintProtocolProvider EscPosProvider(EscPosPrinterProfile profile)
        => PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = profile });

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

    private static bool ContainsSequence(byte[] data, params int[] values)
    {
        for (var start = 0; start <= data.Length - values.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < values.Length; offset++)
                if (data[start + offset] != (byte)values[offset])
                {
                    matched = false;
                    break;
                }
            if (matched) return true;
        }
        return false;
    }

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
