using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// Verifies the ESC/POS device text contract independently from image and layout profiles.
/// </summary>
public sealed class EscPosTextProfileTests
{
    static EscPosTextProfileTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void Fs_kanji_profile_emits_code_page_and_character_mode_before_text()
    {
        var encoding = Encoding.GetEncoding(936);
        var profile = Profile(TextProfile(936, 15, EscPosCharacterMode.FsKanji,
            AsciiRange(), new PrintCharacterRange(0x4E00, 0x9FFF)));

        var bytes = new EscPosPrintCommand(encoding, EscPosLayoutMode.Native,
                new EscPosDeviceOptions { Profile = profile, PrintAreaWidthUnits = 300 })
            .Initialize()
            .WriteText("\u4E2D")
            .Build()
            .GetBytes();

        var prefix = new byte[]
        {
            0x1B, 0x40,
            0x1B, 0x74, 15,
            0x1C, 0x26,
            0x1D, 0x57, 0x2C, 0x01
        };
        AssertPrefix(prefix, bytes);
        Assert.Equal(encoding.GetBytes("\u4E2D"), bytes.Skip(prefix.Length).ToArray());
    }

    [Fact]
    public void Single_byte_profile_emits_single_byte_mode_without_changing_old_text_bytes()
    {
        var encoding = Encoding.GetEncoding(437);
        var profile = Profile(TextProfile(437, 0, EscPosCharacterMode.SingleByte, AsciiRange()));

        var bytes = new EscPosPrintCommand(encoding, EscPosLayoutMode.Native,
                new EscPosDeviceOptions { Profile = profile })
            .Initialize()
            .WriteText("A")
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 0, 0x1C, 0x2E, 0x41 }, bytes);
    }

    [Fact]
    public void Device_default_profile_emits_no_extra_character_mode_command()
    {
        var profile = Profile(TextProfile(65001, null, EscPosCharacterMode.DeviceDefault, AsciiRange()));

        var bytes = new EscPosPrintCommand(Encoding.UTF8, EscPosLayoutMode.Native,
                new EscPosDeviceOptions { Profile = profile })
            .Initialize()
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1B, 0x40 }, bytes);
    }

    [Fact]
    public void Profile_without_text_contract_preserves_manual_code_page_selection()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
                new EscPosDeviceOptions { Profile = new EscPosPrinterProfile { Text = null } })
            .Initialize()
            .SelectCodePage(17)
            .WriteText("A")
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 17, 0x41 }, bytes);
    }

    [Fact]
    public void Text_snapshot_rejects_a_profile_without_character_ranges()
    {
        var text = TextProfile(437, 0, EscPosCharacterMode.SingleByte, Array.Empty<PrintCharacterRange>());

        AssertProfileRejects(() => new EscPosPrintCommand(Encoding.GetEncoding(437),
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = Profile(text) }));
    }

    [Theory]
    [InlineData(0xD800, 0xD800)]
    [InlineData(0xDFFF, 0xDFFF)]
    [InlineData(0xD7FF, 0xE000)]
    [InlineData(0x100, 0xFF)]
    public void Character_range_rejects_surrogate_or_reversed_scalar_ranges(int first, int last)
    {
        Assert.ThrowsAny<ArgumentException>(() => new PrintCharacterRange(first, last));
    }

    [Fact]
    public void Text_snapshot_rejects_invalid_encoding_and_width_settings()
    {
        AssertProfileRejects(() => new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = Profile(TextProfile(-1, 0,
                EscPosCharacterMode.SingleByte, AsciiRange())) }));

        AssertProfileRejects(() => new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = Profile(TextProfile(437, 0,
                EscPosCharacterMode.SingleByte, AsciiRange(), halfCellWidthDots: 0)) }));

        AssertProfileRejects(() => new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = Profile(TextProfile(437, 0,
                EscPosCharacterMode.SingleByte, AsciiRange(), lineHeightDots: 25)) }));
    }

    [Fact]
    public void Fs_kanji_requires_a_supported_kanji_encoding()
    {
        var text = TextProfile(437, 15, EscPosCharacterMode.FsKanji,
            AsciiRange(), new PrintCharacterRange(0x4E00, 0x9FFF));

        AssertProfileRejects(() => new EscPosPrintCommand(Encoding.GetEncoding(437),
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = Profile(text) }));
    }

    [Fact]
    public void Single_byte_requires_an_encoding_with_single_byte_characters()
    {
        var text = TextProfile(65001, 0, EscPosCharacterMode.SingleByte, AsciiRange());

        AssertProfileRejects(() => new EscPosPrintCommand(Encoding.UTF8,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = Profile(text) }));
    }

    [Fact]
    public void Profile_encoding_must_match_the_host_encoding()
    {
        var text = TextProfile(936, 15, EscPosCharacterMode.FsKanji,
            AsciiRange(), new PrintCharacterRange(0x4E00, 0x9FFF));

        AssertProfileRejects(() => new EscPosPrintCommand(Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = Profile(text) }));
    }

    [Fact]
    public void Conflicting_option_code_page_is_rejected_when_profile_declares_one()
    {
        var profile = Profile(TextProfile(437, 15, EscPosCharacterMode.SingleByte, AsciiRange()));

        AssertProfileRejects(() => new EscPosPrintCommand(Encoding.GetEncoding(437),
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = profile, CodePageIndex = 16 }));
    }

    [Fact]
    public void Select_code_page_requires_the_declared_profile_index()
    {
        var declared = new EscPosPrintCommand(Encoding.GetEncoding(437), EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = Profile(TextProfile(437, 15,
                EscPosCharacterMode.SingleByte, AsciiRange())) });
        AssertProfileRejects(() => declared.SelectCodePage(16));

        var undeclared = new EscPosPrintCommand(Encoding.GetEncoding(437), EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = Profile(TextProfile(437, null,
                EscPosCharacterMode.SingleByte, AsciiRange())) });
        AssertProfileRejects(() => undeclared.SelectCodePage(15));
    }

    [Fact]
    public void Failed_text_validation_does_not_pollute_later_output_or_build_repeatability()
    {
        var command = new EscPosPrintCommand(Encoding.GetEncoding(437), EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = Profile(TextProfile(437, 0,
                EscPosCharacterMode.SingleByte, AsciiRange())) });

        command.Initialize();
        AssertProfileRejects(() => command.WriteText("\u4E2D"));

        var first = command.WriteText("OK").Build().GetBytes();
        var second = command.Build().GetBytes();

        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 0, 0x1C, 0x2E, 0x4F, 0x4B }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Failed_code_page_selection_does_not_pollute_later_output()
    {
        var command = new EscPosPrintCommand(Encoding.GetEncoding(437), EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = Profile(TextProfile(437, 15,
                EscPosCharacterMode.SingleByte, AsciiRange())) });

        command.Initialize();
        AssertProfileRejects(() => command.SelectCodePage(16));

        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 15, 0x1C, 0x2E, 0x4F, 0x4B },
            command.WriteText("OK").Build().GetBytes());
    }

    [Fact]
    public void Fs_kanji_accepts_supplementary_unicode_scalars_when_gb18030_is_declared()
    {
        var encoding = Encoding.GetEncoding(54936);
        var profile = Profile(TextProfile(54936, 15, EscPosCharacterMode.FsKanji,
            AsciiRange(), new PrintCharacterRange(0x20000, 0x2A6DF)));

        var bytes = new EscPosPrintCommand(encoding, EscPosLayoutMode.Native,
                new EscPosDeviceOptions { Profile = profile })
            .Initialize()
            .WriteText("\U00020000")
            .Build()
            .GetBytes();

        AssertSuffix(encoding.GetBytes("\U00020000"), bytes);
    }

    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public void Text_rejects_isolated_surrogates_before_appending(int scalar)
    {
        var command = new EscPosPrintCommand(Encoding.UTF8, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = Profile(TextProfile(65001, null,
                EscPosCharacterMode.DeviceDefault, new[]
                {
                    new PrintCharacterRange(0, 0xD7FF),
                    new PrintCharacterRange(0xE000, 0x10FFFF)
                })) });

        command.Initialize();
        var value = new string((char)scalar, 1);
        AssertProfileRejects(() => command.WriteText(value));
        Assert.Equal(new byte[] { 0x1B, 0x40, 0x4F, 0x4B },
            command.WriteText("OK").Build().GetBytes());
    }

    [Fact]
    public void Profile_and_provider_snapshot_character_range_arrays()
    {
        var ranges = AsciiRange();
        var profile = Profile(TextProfile(437, 0, EscPosCharacterMode.SingleByte, ranges));
        var command = new EscPosPrintCommand(Encoding.GetEncoding(437), EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = profile });
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.GetEncoding(437),
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = profile });

        ranges[0] = new PrintCharacterRange(0x4E00, 0x9FFF);

        Assert.Equal(new byte[] { 0x41 }, command.WriteText("A").Build().GetBytes());

        var document = new PrintDocument();
        document.AddPage(120, 24).Add(new PrintTextElement(0, 0, "A", 24), "Rows[0].Text");
        Assert.NotEmpty(provider.Render(document));
    }

    [Fact]
    public void Template_native_wrapping_uses_profile_half_full_width_and_line_height()
    {
        var encoding = Encoding.GetEncoding(936);
        var profile = Profile(TextProfile(936, 15, EscPosCharacterMode.FsKanji,
            AsciiRange(), new PrintCharacterRange(0x4E00, 0x9FFF),
            halfCellWidthDots: 12, fullCellWidthDots: 24, lineHeightDots: 26));
        var options = TemplateOptions(encoding, EscPosLayoutMode.Native, profile);

        var wrapped = new TemplateRenderer().RenderWithLayout(TextTemplate(24, "A\u4E2D"), null,
            options: options).Pages.Single();
        var wrappedText = wrapped.Elements.Where(element => element.Type == 1).ToArray();
        Assert.Equal(new[] { "A", "\u4E2D" }, wrappedText.Select(element => element.Text));
        Assert.Equal(new[] { 12, 24 }, wrappedText.Select(element => element.Width));
        Assert.Equal(new[] { 0, 26 }, wrappedText.Select(element => element.Y));
        Assert.All(wrappedText, element => Assert.Equal(24, element.Height));

        var unwrapped = new TemplateRenderer().RenderWithLayout(TextTemplate(36, "A\u4E2D"), null,
            options: options).Pages.Single();
        var unwrappedText = Assert.Single(unwrapped.Elements.Where(element => element.Type == 1));
        Assert.Equal("A\u4E2D", unwrappedText.Text);
        Assert.Equal(36, unwrappedText.Width);
    }

    [Fact]
    public void Template_native_character_failure_contains_the_full_element_path()
    {
        var profile = Profile(TextProfile(65001, null, EscPosCharacterMode.DeviceDefault, AsciiRange()));
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            TextTemplate(120, "\u4E2D"), null,
            options: TemplateOptions(Encoding.UTF8, EscPosLayoutMode.Native, profile)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    [Fact]
    public void Raster_host_text_is_not_limited_by_native_character_ranges_but_direct_text_is_checked()
    {
        var profile = Profile(TextProfile(65001, null, EscPosCharacterMode.DeviceDefault, AsciiRange()));
        var device = new EscPosDeviceOptions { Profile = profile };
        var command = new EscPosPrintCommand(Encoding.UTF8, EscPosLayoutMode.Raster, device);

        AssertProfileRejects(() => command.WriteText("\u4E2D"));

        var raster = new TemplateRenderer().RenderWithLayout(TextTemplate(120, "\u4E2D"), null,
            options: TemplateOptions(Encoding.UTF8, EscPosLayoutMode.Raster, profile));
        Assert.NotEmpty(raster.Pages);
        Assert.Contains(raster.Pages[0].Data, value => value != 0);

        var nativeError = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            TextTemplate(120, "\u4E2D"), null,
            options: TemplateOptions(Encoding.UTF8, EscPosLayoutMode.Native, profile)));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", nativeError.SourcePath);
    }

    [Fact]
    public void Measure_text_width_uses_declared_half_and_full_cells_for_gbk()
    {
        var profile = Profile(TextProfile(936, 15, EscPosCharacterMode.FsKanji,
            AsciiRange(), new PrintCharacterRange(0x4E00, 0x9FFF),
            halfCellWidthDots: 12, fullCellWidthDots: 24));

        Assert.Equal(12, profile.Text.MeasureTextWidth("A"));
        Assert.Equal(24, profile.Text.MeasureTextWidth("\u4E2D"));
        Assert.Equal(36, profile.Text.MeasureTextWidth("A\u4E2D"));
    }

    [Fact]
    public void Provider_enforces_declared_text_width_at_exact_boundaries_with_full_path()
    {
        var encoding = Encoding.GetEncoding(936);
        var profile = Profile(TextProfile(936, 15, EscPosCharacterMode.FsKanji,
            AsciiRange(), new PrintCharacterRange(0x4E00, 0x9FFF),
            halfCellWidthDots: 12, fullCellWidthDots: 24));
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, encoding,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = profile });

        Assert.NotEmpty(provider.Render(NativeTextDocument(12, "A", "Rows[0].Ascii")));
        Assert.NotEmpty(provider.Render(NativeTextDocument(24, "\u4E2D", "Rows[0].Cjk")));

        var error = Assert.Throws<PrintElementValidationException>(() =>
            provider.Render(NativeTextDocument(23, "\u4E2D", "Rows[0].Cells[0].Elements[0]")));
        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ElementPath);
    }

    [Fact]
    public void Native_text_profile_getter_is_a_deep_snapshot_for_capabilities_and_rendering()
    {
        var encoding = Encoding.GetEncoding(936);
        var profile = Profile(TextProfile(936, 15, EscPosCharacterMode.FsKanji,
            AsciiRange(), new PrintCharacterRange(0x4E00, 0x9FFF)));
        var device = new EscPosDeviceOptions { Profile = profile };
        var command = new EscPosPrintCommand(encoding, EscPosLayoutMode.Native, device);

        var first = command.Capabilities.NativeTextProfile;
        var second = command.Capabilities.NativeTextProfile;
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.NotSame(first.CharacterRanges, second.CharacterRanges);

        first.CharacterRanges[0] = new PrintCharacterRange(0x4E00, 0x9FFF);
        Assert.Equal(0x20, command.Capabilities.NativeTextProfile.CharacterRanges[0].First);
        Assert.True(command.Capabilities.SupportsEncoding(encoding));

        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, encoding,
            EscPosLayoutMode.Native, device);
        var exposed = provider.Capabilities.NativeTextProfile;
        exposed.CharacterRanges[0] = new PrintCharacterRange(0x4E00, 0x9FFF);
        Assert.True(provider.Capabilities.SupportsEncoding(encoding));
        Assert.NotEmpty(provider.Render(NativeTextDocument(12, "A", "Rows[0].Text")));
    }

    private static EscPosPrinterProfile Profile(EscPosTextProfile text)
        => new EscPosPrinterProfile { Text = text };

    private static EscPosTextProfile TextProfile(int encodingCodePage, byte? codePageIndex,
        EscPosCharacterMode mode, PrintCharacterRange[] ranges,
        int halfCellWidthDots = 12, int fullCellWidthDots = 24, int lineHeightDots = 24)
    {
        return new EscPosTextProfile
        {
            EncodingCodePage = encodingCodePage,
            CodePageIndex = codePageIndex,
            CharacterMode = mode,
            CharacterRanges = ranges,
            HalfCellWidthDots = halfCellWidthDots,
            FullCellWidthDots = fullCellWidthDots,
            LineHeightDots = lineHeightDots
        };
    }

    private static EscPosTextProfile TextProfile(int encodingCodePage, byte? codePageIndex,
        EscPosCharacterMode mode, PrintCharacterRange[] ranges, PrintCharacterRange additionalRange,
        int halfCellWidthDots = 12, int fullCellWidthDots = 24, int lineHeightDots = 24)
    {
        return TextProfile(encodingCodePage, codePageIndex, mode,
            ranges.Concat(new[] { additionalRange }).ToArray(),
            halfCellWidthDots, fullCellWidthDots, lineHeightDots);
    }

    private static PrintCharacterRange[] AsciiRange()
        => new[] { new PrintCharacterRange(0x20, 0x7E) };

    private static TemplateRenderOptions TemplateOptions(Encoding encoding,
        EscPosLayoutMode layout, EscPosPrinterProfile profile)
        => new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = layout,
            Encoding = encoding,
            EscPosDevice = new EscPosDeviceOptions { Profile = profile },
            FeedLines = 0,
            CutAfterPrint = false
        };

    private static BpTemplate TextTemplate(int width, string value)
        => new BpTemplate
        {
            Version = 3,
            Width = width,
            Height = 60,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    Field = new BgField { Value = value }
                                }
                            }
                        }
                    }
                }
            }
        };

    private static PrintDocument NativeTextDocument(int width, string value, string path)
    {
        var document = new PrintDocument();
        document.AddPage(width, 24)
            .Add(new PrintTextElement(0, 0, value, 24), path);
        return document;
    }

    private static void AssertProfileRejects(Action action)
    {
        var error = Assert.ThrowsAny<Exception>(action);
        Assert.True(error is ArgumentException || error is NotSupportedException,
            $"Expected profile validation failure, got {error.GetType().FullName}: {error.Message}");
    }

    private static void AssertPrefix(byte[] expected, byte[] actual)
    {
        Assert.True(actual.Length >= expected.Length,
            $"Output length {actual.Length} is shorter than prefix length {expected.Length}.");
        Assert.Equal(expected, actual.Take(expected.Length).ToArray());
    }

    private static void AssertSuffix(byte[] expected, byte[] actual)
    {
        Assert.True(actual.Length >= expected.Length,
            $"Output length {actual.Length} is shorter than suffix length {expected.Length}.");
        Assert.Equal(expected, actual.Skip(actual.Length - expected.Length).ToArray());
    }
}
