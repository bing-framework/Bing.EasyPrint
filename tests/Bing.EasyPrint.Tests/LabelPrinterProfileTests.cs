using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 TSPL/ZPL 标签机型配置的能力边界、快照语义和静态预检。
/// </summary>
public sealed class LabelPrinterProfileTests
{
    [Fact]
    public void Omitted_profiles_preserve_legacy_command_bytes()
    {
        var tspl = new TsplPrintCommand(Encoding.ASCII)
            .Size(60, 40, "dot")
            .Text(1, 2, "OK")
            .Build()
            .GetBytes();
        var zpl = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .Label(61, 40)
            .Text(1, 2, "OK")
            .End()
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "SIZE 60 dot,40 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "TEXT 1,2,\"0\",0,1,1,\"OK\"\r\n"), tspl);
        Assert.Equal(Encoding.ASCII.GetBytes("^XA\r\n^PW61\r\n^LL40\r\n^FO1,2^A0N,30,30^FDOK^FS\r\n^XZ\r\n"), zpl);
    }

    [Fact]
    public void Configured_profile_is_exposed_by_command_and_provider_capabilities()
    {
        var ranges = new[] { new PrintCharacterRange(0x20, 0x7E) };
        var profile = new TsplPrinterProfile
        {
            Name = "TSC-203",
            FirmwareVersion = "1.7",
            PrintableWidthDots = 384,
            MaxLabelHeightDots = 1024,
            MaxImageDataBytes = 4096,
            SupportsImages = false,
            SupportsGraphics = false,
            SupportsNativeBarcode = false,
            SupportsNativeQrCode = false,
            SupportsRotation = false,
            DeviceFontNames = new[] { "0", "1" },
            EncodingCodePages = new[] { Encoding.ASCII.CodePage, Encoding.UTF8.CodePage },
            CharacterRanges = ranges
        };

        var command = new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { Profile = profile });
        var provider = TsplProvider(profile);

        AssertCapabilities(command.Capabilities, PrintProtocol.Tspl, profile);
        AssertCapabilities(provider.Capabilities, PrintProtocol.Tspl, profile);
        Assert.Equal(new[] { "0", "1" }, command.Capabilities.DeviceFontNames);
        Assert.Equal(new[] { 0x20, 0x7E }, new[] {
            command.Capabilities.DeviceCharacterRanges[0].First,
            command.Capabilities.DeviceCharacterRanges[0].Last });
        Assert.True(command.Capabilities.SupportsEncoding(Encoding.ASCII));
        Assert.True(command.Capabilities.SupportsEncoding(Encoding.UTF8));
        Assert.False(command.Capabilities.SupportsEncoding(Encoding.Unicode));
    }

    [Fact]
    public void Zpl_profile_restricts_capabilities_and_encoding_without_changing_old_defaults()
    {
        var profile = new ZplPrinterProfile
        {
            Name = "ZQ-320",
            FirmwareVersion = "3.2",
            PrintableWidthDots = 832,
            MaxLabelHeightDots = 1600,
            MaxImageDataBytes = 8192,
            SupportsImages = true,
            SupportsGraphics = true,
            SupportsNativeBarcode = false,
            SupportsNativeQrCode = false,
            SupportsRotation = true,
            SupportsCut = false,
            DeviceFontNames = new[] { "0", "A" },
            EncodingCodePages = new[] { Encoding.ASCII.CodePage },
            CharacterRanges = new[] { new PrintCharacterRange(0x20, 0x7E) }
        };

        var command = new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { Profile = profile });
        AssertCapabilities(command.Capabilities, PrintProtocol.Zpl, profile);
        Assert.False(command.Capabilities.SupportsEncoding(Encoding.UTF8));
        Assert.True(command.Capabilities.SupportsEncoding(Encoding.ASCII));
        Assert.Equal(new[] { "0", "A" }, command.Capabilities.DeviceFontNames);
        Assert.False(command.Capabilities.SupportsCut);
    }

    [Fact]
    public void Profile_and_nested_arrays_are_snapshotted_for_commands_and_providers()
    {
        var fonts = new[] { "0", "1" };
        var ranges = new[] { new PrintCharacterRange(0x20, 0x7E) };
        var profile = new TsplPrinterProfile
        {
            Name = "before",
            FirmwareVersion = "old",
            PrintableWidthDots = 100,
            MaxLabelHeightDots = 200,
            MaxImageDataBytes = 300,
            DeviceFontNames = fonts,
            EncodingCodePages = new[] { Encoding.ASCII.CodePage },
            CharacterRanges = ranges
        };
        var options = new TsplMediaOptions { Profile = profile };
        var command = new TsplPrintCommand(Encoding.ASCII, options);
        var provider = TsplProvider(profile);

        fonts[0] = "8";
        ranges[0] = new PrintCharacterRange(0x4E00, 0x9FFF);
        profile.Name = "after";
        profile.FirmwareVersion = "new";
        profile.PrintableWidthDots = 900;
        profile.MaxLabelHeightDots = 901;
        profile.MaxImageDataBytes = 902;
        profile.DeviceFontNames = new[] { "8" };
        profile.CharacterRanges = new[] { new PrintCharacterRange(0x4E00, 0x9FFF) };

        foreach (var capabilities in new[] { command.Capabilities, provider.Capabilities })
        {
            Assert.Equal("before", capabilities.DeviceProfileName);
            Assert.Equal("old", capabilities.DeviceFirmwareVersion);
            Assert.Equal(100, capabilities.MaxPrintableWidthDots);
            Assert.Equal(200, capabilities.MaxLabelHeightDots);
            Assert.Equal(300, capabilities.MaxImageDataBytes);
            Assert.Equal(new[] { "0", "1" }, capabilities.DeviceFontNames);
            Assert.Equal(0x20, capabilities.DeviceCharacterRanges[0].First);
        }
    }

    [Fact]
    public void Invalid_tspl_profile_scalars_are_rejected_before_commands_are_recorded()
    {
        var invalid = new[]
        {
            new TsplPrinterProfile { Name = "" },
            new TsplPrinterProfile { Name = "  " },
            new TsplPrinterProfile { PrintableWidthDots = -1 },
            new TsplPrinterProfile { MaxLabelHeightDots = -1 },
            new TsplPrinterProfile { MaxImageDataBytes = -1 }
        };

        foreach (var profile in invalid)
            Assert.ThrowsAny<ArgumentException>(() => new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions { Profile = profile }));
    }

    [Fact]
    public void Invalid_zpl_profile_scalars_are_rejected_before_commands_are_recorded()
    {
        var invalid = new[]
        {
            new ZplPrinterProfile { Name = "" },
            new ZplPrinterProfile { Name = "  " },
            new ZplPrinterProfile { PrintableWidthDots = -1 },
            new ZplPrinterProfile { MaxLabelHeightDots = -1 },
            new ZplPrinterProfile { MaxImageDataBytes = -1 }
        };

        foreach (var profile in invalid)
            Assert.ThrowsAny<ArgumentException>(() => new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { Profile = profile }));
    }

    [Fact]
    public void Invalid_tspl_font_encoding_and_range_lists_are_rejected()
    {
        var ascii = new[] { new PrintCharacterRange(0x20, 0x7E) };
        var invalid = new[]
        {
            new TsplPrinterProfile { DeviceFontNames = new[] { "0", "0" } },
            new TsplPrinterProfile { DeviceFontNames = new[] { "9" } },
            new TsplPrinterProfile { DeviceFontNames = new[] { "A" } },
            new TsplPrinterProfile { EncodingCodePages = new[] { Encoding.ASCII.CodePage, Encoding.ASCII.CodePage } },
            new TsplPrinterProfile { EncodingCodePages = new[] { 1252 } },
            new TsplPrinterProfile { CharacterRanges = new[] { ascii[0], ascii[0] } },
            new TsplPrinterProfile { CharacterRanges = new[] {
                new PrintCharacterRange(0x20, 0x7E), new PrintCharacterRange(0x7E, 0x80) } }
        };

        foreach (var profile in invalid)
            Assert.ThrowsAny<ArgumentException>(() => new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions { Profile = profile }));
    }

    [Fact]
    public void Invalid_zpl_font_encoding_and_range_lists_are_rejected()
    {
        var ascii = new[] { new PrintCharacterRange(0x20, 0x7E) };
        var invalid = new[]
        {
            new ZplPrinterProfile { DeviceFontNames = new[] { "0", "0" } },
            new ZplPrinterProfile { DeviceFontNames = new[] { "a" } },
            new ZplPrinterProfile { DeviceFontNames = new[] { "AA" } },
            new ZplPrinterProfile { EncodingCodePages = new[] { Encoding.UTF8.CodePage, Encoding.UTF8.CodePage } },
            new ZplPrinterProfile { EncodingCodePages = new[] { 936 } },
            new ZplPrinterProfile { CharacterRanges = new[] { ascii[0], ascii[0] } },
            new ZplPrinterProfile { CharacterRanges = new[] {
                new PrintCharacterRange(0x20, 0x7E), new PrintCharacterRange(0x7E, 0x80) } }
        };

        foreach (var profile in invalid)
            Assert.ThrowsAny<ArgumentException>(() => new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { Profile = profile }));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Empty_device_font_list_disables_text_and_default_font_range(PrintProtocol protocol)
    {
        var provider = protocol == PrintProtocol.Tspl
            ? TsplProvider(new TsplPrinterProfile { DeviceFontNames = Array.Empty<string>() })
            : ZplProvider(new ZplPrinterProfile { DeviceFontNames = Array.Empty<string>() });

        Assert.False(provider.Capabilities.SupportsText);
        Assert.Empty(provider.Capabilities.DeviceFontNames);
        Assert.Null(provider.Capabilities.GetTextSizeRange(PrintFont.Default));

        var document = new PrintDocument();
        document.AddPage(200, 80)
            .Add(new PrintTextElement(0, 0, "TEXT", 24), "Rows[0].Text");
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));
        Assert.Equal("Rows[0].Text", error.ElementPath);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_profile_without_default_font_exposes_only_declared_device_font(PrintProtocol protocol)
    {
        var provider = protocol == PrintProtocol.Tspl
            ? TsplProvider(new TsplPrinterProfile { DeviceFontNames = new[] { "1" } })
            : ZplProvider(new ZplPrinterProfile { DeviceFontNames = new[] { "1" } });
        var capabilities = provider.Capabilities;
        var deviceFont = new PrintFont(PrintFontKind.Device, "1");

        Assert.Equal(new[] { PrintFontKind.Device }, capabilities.FontKinds);
        Assert.Null(capabilities.GetTextSizeRange(PrintFont.Default));
        Assert.NotNull(capabilities.GetTextSizeRange(deviceFont));
        Assert.False(capabilities.SupportsFont(PrintFont.Default, 1));
        Assert.True(capabilities.SupportsFont(deviceFont, 1));
    }

    [Fact]
    public void Tspl_command_rejects_disabled_text_before_append_and_keeps_build_clean()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { DeviceFontNames = Array.Empty<string>() }
        });
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.Text(1, 2, "OK"));
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "8")]
    [InlineData(PrintProtocol.Zpl, "A")]
    public void Provider_rejects_a_font_missing_from_the_profile_with_element_path(
        PrintProtocol protocol, string font)
    {
        var provider = protocol == PrintProtocol.Tspl
            ? TsplProvider(new TsplPrinterProfile { DeviceFontNames = new[] { "0" } })
            : ZplProvider(new ZplPrinterProfile { DeviceFontNames = new[] { "0" } });
        var document = new PrintDocument();
        document.AddPage(200, 80)
            .Add(new PrintTextElement(0, 0, "TEXT", 24, deviceFontName: font), "Rows[0].Text");

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));
        Assert.Equal("Rows[0].Text", error.ElementPath);
        Assert.Equal(0, error.PageIndex);
        Assert.Equal(0, error.ElementIndex);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Provider_rejects_text_outside_declared_character_ranges_with_element_path(PrintProtocol protocol)
    {
        var profileRange = new[] { new PrintCharacterRange(0x20, 0x7E) };
        var provider = protocol == PrintProtocol.Tspl
            ? TsplProvider(new TsplPrinterProfile { CharacterRanges = profileRange }, Encoding.UTF8)
            : ZplProvider(new ZplPrinterProfile { CharacterRanges = profileRange }, Encoding.UTF8);
        var document = new PrintDocument();
        document.AddPage(200, 80)
            .Add(new PrintTextElement(0, 0, "A\u4E2D", 24), "Rows[0].Text");

        var error = Assert.ThrowsAny<Exception>(() => provider.Validate(document));
        AssertProviderPath(error, "Rows[0].Text");
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Provider_rejects_disabled_native_qr_before_measuring_unencodable_data(PrintProtocol protocol)
    {
        var provider = protocol == PrintProtocol.Tspl
            ? TsplProvider(new TsplPrinterProfile { SupportsNativeQrCode = false })
            : ZplProvider(new ZplPrinterProfile { SupportsNativeQrCode = false });
        var document = new PrintDocument();
        document.AddPage(200, 80)
            .Add(new PrintQrCodeElement(0, 0, new string('A', 5000), 4,
                QrCodeCorrectionLevel.M), "Rows[0].QrLong");

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));

        Assert.Equal("Rows[0].QrLong", error.ElementPath);
        Assert.Equal(0, error.PageIndex);
        Assert.Equal(0, error.ElementIndex);
    }

    [Fact]
    public void Tspl_size_checks_dot_and_mm_width_against_profile()
    {
        var options = new TsplMediaOptions
        {
            Dpi = 203,
            Profile = new TsplPrinterProfile { PrintableWidthDots = 100 }
        };
        Assert.Throws<NotSupportedException>(() => new TsplPrintCommand(Encoding.ASCII, options)
            .Size(101, 20, "dot"));
        Assert.Throws<NotSupportedException>(() => new TsplPrintCommand(Encoding.ASCII, options)
            .Size(26, 20, "mm"));

        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII, options)
            .Size(100, 20, "dot")
            .Build()
            .GetBytes());
        Assert.Contains("SIZE 100 dot,20 dot\r\n", output);
    }

    [Fact]
    public void Tspl_apply_profile_after_structured_output_is_rejected_but_legacy_apply_still_works()
    {
        var profile = new TsplPrinterProfile { Name = "late-profile" };
        var with_size = new TsplPrintCommand(Encoding.ASCII).Size(40, 20, "dot");
        Assert.Throws<InvalidOperationException>(() => with_size.ApplyMediaOptions(new TsplMediaOptions
        {
            Profile = profile
        }));

        var already_bound = new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { Profile = profile }).Size(40, 20, "dot");
        Assert.Throws<InvalidOperationException>(() => already_bound.ApplyMediaOptions(new TsplMediaOptions()));

        var legacy = new TsplPrintCommand(Encoding.ASCII).Size(40, 20, "dot");
        legacy.ApplyMediaOptions(new TsplMediaOptions { Density = 8 });
        Assert.Contains("DENSITY 8\r\n", Encoding.ASCII.GetString(legacy.Build().GetBytes()));
    }

    [Fact]
    public void Zpl_label_checks_width_and_height_against_profile()
    {
        var options = new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile
            {
                PrintableWidthDots = 100,
                MaxLabelHeightDots = 80
            }
        };
        Assert.Throws<NotSupportedException>(() => new ZplPrintCommand(Encoding.ASCII, options)
            .Label(101, 20));
        Assert.Throws<NotSupportedException>(() => new ZplPrintCommand(Encoding.ASCII, options)
            .Label(100, 81));

        new ZplPrintCommand(Encoding.ASCII, options).Label(100, 80);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Provider_preserves_page_path_for_profile_printable_width_and_height(PrintProtocol protocol)
    {
        var profile = protocol == PrintProtocol.Tspl
            ? (object)new TsplPrinterProfile { PrintableWidthDots = 100, MaxLabelHeightDots = 80 }
            : new ZplPrinterProfile { PrintableWidthDots = 100, MaxLabelHeightDots = 80 };
        var provider = protocol == PrintProtocol.Tspl
            ? TsplProvider((TsplPrinterProfile)profile)
            : ZplProvider((ZplPrinterProfile)profile);
        var document = new PrintDocument();
        document.AddPage(101, 81);

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));
        Assert.Equal("Pages[0]", error.ElementPath);
        Assert.Equal(0, error.PageIndex);
        Assert.Null(error.ElementIndex);
    }

    [Fact]
    public void Raster_image_budget_counts_padded_row_bytes_and_accepts_exact_boundary()
    {
        var rejecting = TsplProvider(new TsplPrinterProfile { MaxImageDataBytes = 1 });
        var document = new PrintDocument();
        document.AddPage(16, 8)
            .Add(new PrintImageElement(0, 0, 9, 1, new byte[] { 0x80, 0x00 }), "Rows[0].Image");
        var error = Assert.Throws<PrintCapabilityException>(() => rejecting.Validate(document));
        Assert.Equal("Rows[0].Image", error.ElementPath);

        var accepting = TsplProvider(new TsplPrinterProfile { MaxImageDataBytes = 2 });
        accepting.Validate(document);
        Assert.NotEmpty(Assert.Single(accepting.Render(document)));
    }

    [Fact]
    public void Rasterized_diagonal_checks_image_budget_before_bitmap_allocation()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            AllowRasterizedLines = true,
            Profile = new TsplPrinterProfile { MaxImageDataBytes = 1 }
        });
        Assert.Throws<NotSupportedException>(() => command.Line(0, 0, 8, 8));
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Failed_profile_image_append_does_not_pollute_build_and_build_is_idempotent()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { MaxImageDataBytes = 1 }
        }).Start().Label(20, 20);

        Assert.Throws<NotSupportedException>(() => command.Image(0, 0, 2, 1, new byte[] { 0x80, 0x00 }));

        var expected = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { MaxImageDataBytes = 1 }
        }).Start().Label(20, 20).Image(0, 0, 1, 1, new byte[] { 0x80 }).End().Build().GetBytes();
        command.Image(0, 0, 1, 1, new byte[] { 0x80 }).End();
        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_page_and_job_cut_are_rejected_when_profile_disables_cut()
    {
        var profile = new ZplPrinterProfile { SupportsCut = false };
        var command = new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { Profile = profile });
        Assert.Throws<NotSupportedException>(() => command.Cut());
        Assert.Empty(command.Build().GetBytes());

        var provider = ZplProvider(profile);
        var document = new PrintDocument();
        var page = document.AddPage(40, 20);
        page.CutAfterPrint = true;
        var pageError = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));
        Assert.Equal("Pages[0]", pageError.ElementPath);

        var jobError = Assert.Throws<PrintCapabilityException>(() => PrintJobRenderer.ValidateSettings(provider,
            new PrintJobOptions { CutScope = PrintFinishScope.JobEnd }));
        Assert.Equal("Job.CutScope", jobError.ElementPath);
    }

    [Fact]
    public void Disabled_command_features_reject_before_append_for_tspl()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            AllowRasterizedLines = true,
            Profile = new TsplPrinterProfile
            {
                SupportsImages = false,
                SupportsGraphics = false,
                SupportsNativeBarcode = false,
                SupportsNativeQrCode = false,
                SupportsRotation = false
            }
        });
        Assert.Throws<NotSupportedException>(() => command.Image(0, 0, 1, 1, 0, new byte[] { 0x80 }));
        Assert.Throws<NotSupportedException>(() => command.Line(0, 0, 1, 0));
        Assert.Throws<NotSupportedException>(() => command.Barcode(0, 0, "1234"));
        Assert.Throws<NotSupportedException>(() => command.QrCode(0, 0, "1234"));
        Assert.Throws<NotSupportedException>(() => command.Text(0, 0, "0", 90, 1, 1, "X"));
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Disabled_command_features_reject_before_append_for_zpl()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            AllowRasterizedLines = true,
            Profile = new ZplPrinterProfile
            {
                SupportsImages = false,
                SupportsGraphics = false,
                SupportsNativeBarcode = false,
                SupportsNativeQrCode = false,
                SupportsRotation = false
            }
        });
        Assert.Throws<NotSupportedException>(() => command.Image(0, 0, 1, 1, new byte[] { 0x80 }));
        Assert.Throws<NotSupportedException>(() => command.Line(0, 0, 1, 0));
        Assert.Throws<NotSupportedException>(() => command.Barcode(0, 0, "1234"));
        Assert.Throws<NotSupportedException>(() => command.QrCode(0, 0, "1234"));
        Assert.Throws<NotSupportedException>(() => command.Text(0, 0, "X", 30, 30, "0", 90));
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Template_profile_preflight_rejects_disabled_font_before_binding_or_resource_access()
    {
        var bound = 0;
        var resources = new CountingResources();
        var template = Template(
            new BpFontElement
            {
                FontSize = 24,
                Field = new BgField { ValueFunc = _ => { bound++; return "TEXT"; } }
            },
            new BpImageElement { Width = 8, Height = 8, Field = new BgField { Value = "image" } });
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Tspl,
            Encoding = Encoding.ASCII,
            TsplMedia = new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { DeviceFontNames = Array.Empty<string>() }
            }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            template, new object(), resources, options));
        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Equal(0, bound);
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Template_profile_preflight_rejects_disabled_image_before_binding_or_resource_access()
    {
        var bound = 0;
        var resources = new CountingResources();
        var template = Template(
            new BpImageElement { Width = 8, Height = 8, Field = new BgField { Value = "image" } },
            new BpFontElement
            {
                FontSize = 24,
                Field = new BgField { ValueFunc = _ => { bound++; return "TEXT"; } }
            });
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Zpl,
            Encoding = Encoding.ASCII,
            ZplMedia = new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsImages = false }
            }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            template, new object(), resources, options));
        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Equal(0, bound);
        Assert.Equal(0, resources.OpenCount);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Template_capabilities_keep_info_icon_when_images_are_disabled(PrintProtocol protocol)
    {
        var options = new TemplateRenderOptions
        {
            Protocol = protocol,
            Encoding = Encoding.ASCII
        };
        if (protocol == PrintProtocol.Tspl)
            options.TsplMedia = new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { SupportsImages = false, SupportsGraphics = true }
            };
        else
            options.ZplMedia = new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsImages = false, SupportsGraphics = true }
            };

        var capabilities = TemplateProtocolCapabilities.Create(options);

        Assert.False(capabilities.SupportsElementType(8));
        Assert.True(capabilities.SupportsElementType(9));
    }

    [Fact]
    public void Template_profile_preflight_rejects_page_width_before_binding_or_resource_access()
    {
        var bound = 0;
        var resources = new CountingResources();
        var template = Template(
            new BpFontElement
            {
                FontSize = 24,
                Field = new BgField { ValueFunc = _ => { bound++; return "TEXT"; } }
            });
        template.Width = 101;
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Zpl,
            Encoding = Encoding.ASCII,
            ZplMedia = new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { PrintableWidthDots = 100, MaxLabelHeightDots = 80 }
            }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            template, new object(), resources, options));
        Assert.Equal("Template", error.SourcePath);
        Assert.Equal(0, bound);
        Assert.Equal(0, resources.OpenCount);

        var tall = Template(
            new BpFontElement
            {
                FontSize = 24,
                Field = new BgField { ValueFunc = _ => { bound++; return "TEXT"; } }
            });
        tall.Width = 100;
        tall.Height = 81;
        var heightError = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            tall, new object(), resources, options));
        Assert.Equal("Template", heightError.SourcePath);
        Assert.Equal(0, bound);
        Assert.Equal(0, resources.OpenCount);
    }

    [Fact]
    public void Template_auto_height_uses_actual_short_page_for_profile_height()
    {
        var template = Template(new BpFontElement
        {
            FontSize = 24,
            Field = new BgField { Value = "OK" }
        });
        template.AutoHeight = true;
        template.Height = 0;
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Zpl,
            Encoding = Encoding.ASCII,
            ZplMedia = new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { MaxLabelHeightDots = 100 }
            }
        };

        var page = Assert.Single(new TemplateRenderer().Render(template, null, options: options).Pages);

        Assert.Equal(24, page.Height);
        Assert.NotEmpty(page.Data);
    }

    [Fact]
    public void Template_without_images_keeps_native_lines_and_info_but_rejects_bitmap_shapes_early()
    {
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Tspl,
            Encoding = Encoding.ASCII,
            TsplMedia = new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { SupportsImages = false, SupportsGraphics = true }
            }
        };

        var supported = new BpElement[]
        {
            Line(0, 0, 20, 0),
            Line(0, 0, 0, 20),
            new BpIconElement { Icon = "info", Size = 24 }
        };
        foreach (var element in supported)
        {
            var template = Template(element);
            template.Version = 3;
            Assert.NotEmpty(new TemplateRenderer().Render(template, null,
                options: options).Pages);
        }

        var unsupported = new BpElement[]
        {
            Line(0, 0, 20, 20),
            new BpIconElement { Icon = "cross", Size = 24 },
            new BpEllipseElement { Width = 24, Height = 24 }
        };
        foreach (var element in unsupported)
        {
            var bound = 0;
            var resources = new CountingResources();
            var template = Template(element,
                new BpFontElement
                {
                    FontSize = 24,
                    Field = new BgField { ValueFunc = _ => { bound++; return "TEXT"; } }
                },
                new BpImageElement { Width = 8, Height = 8, Field = new BgField { Value = "image" } });
            template.Version = 3;
            var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
                template, new object(), resources, options));
            Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
            Assert.Equal(0, bound);
            Assert.Equal(0, resources.OpenCount);
        }
    }

    private static void AssertCapabilities(PrintCapabilities capabilities, PrintProtocol protocol,
        TsplPrinterProfile profile)
    {
        Assert.Equal(protocol, capabilities.Protocol);
        Assert.Equal(profile.Name, capabilities.DeviceProfileName);
        Assert.Equal(profile.FirmwareVersion, capabilities.DeviceFirmwareVersion);
        Assert.Equal(profile.PrintableWidthDots, capabilities.MaxPrintableWidthDots);
        Assert.Equal(profile.MaxLabelHeightDots, capabilities.MaxLabelHeightDots);
        Assert.Equal(profile.MaxImageDataBytes, capabilities.MaxImageDataBytes);
        Assert.Equal(profile.SupportsImages, capabilities.SupportsImages);
        Assert.Equal(profile.SupportsGraphics, capabilities.SupportsGraphics);
        Assert.Equal(profile.SupportsNativeBarcode, capabilities.SupportsCommandBarcode);
        Assert.Equal(profile.SupportsNativeQrCode, capabilities.SupportsCommandQrCode);
        Assert.Equal(profile.SupportsRotation, capabilities.SupportsRotation);
    }

    private static void AssertCapabilities(PrintCapabilities capabilities, PrintProtocol protocol,
        ZplPrinterProfile profile)
    {
        Assert.Equal(protocol, capabilities.Protocol);
        Assert.Equal(profile.Name, capabilities.DeviceProfileName);
        Assert.Equal(profile.FirmwareVersion, capabilities.DeviceFirmwareVersion);
        Assert.Equal(profile.PrintableWidthDots, capabilities.MaxPrintableWidthDots);
        Assert.Equal(profile.MaxLabelHeightDots, capabilities.MaxLabelHeightDots);
        Assert.Equal(profile.MaxImageDataBytes, capabilities.MaxImageDataBytes);
        Assert.Equal(profile.SupportsImages, capabilities.SupportsImages);
        Assert.Equal(profile.SupportsGraphics, capabilities.SupportsGraphics);
        Assert.Equal(profile.SupportsNativeBarcode, capabilities.SupportsCommandBarcode);
        Assert.Equal(profile.SupportsNativeQrCode, capabilities.SupportsCommandQrCode);
        Assert.Equal(profile.SupportsRotation, capabilities.SupportsRotation);
        Assert.Equal(profile.SupportsCut, capabilities.SupportsCut);
    }

    private static IPrintProtocolProvider TsplProvider(TsplPrinterProfile profile, Encoding encoding = null)
        => PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, encoding ?? Encoding.ASCII,
            tsplOptions: new TsplMediaOptions { Profile = profile });

    private static IPrintProtocolProvider ZplProvider(ZplPrinterProfile profile, Encoding encoding = null)
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, encoding ?? Encoding.ASCII,
            zplOptions: new ZplMediaOptions { Profile = profile });

    private static void AssertProviderPath(Exception error, string path)
    {
        if (error is PrintCapabilityException capability)
        {
            Assert.Equal(path, capability.ElementPath);
            return;
        }
        if (error is PrintElementValidationException validation)
        {
            Assert.Equal(path, validation.ElementPath);
            return;
        }
        throw new Xunit.Sdk.XunitException($"Expected a path-bearing provider error, got {error.GetType().FullName}.");
    }

    private static BpTemplate Template(params BpElement[] elements)
        => new BpTemplate
        {
            Width = 200,
            Height = 100,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell { Elements = new List<BpElement>(elements) }
                    }
                }
            }
        };

    private static BpLineElement Line(int x, int y, int endX, int endY)
        => new BpLineElement
        {
            StartCoordinate = new BpCoordinate { X = x, Y = y },
            LineEndCoordinate = new BpCoordinate { X = endX, Y = endY }
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
