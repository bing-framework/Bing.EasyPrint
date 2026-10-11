using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 ESC/POS 机型配置驱动的图片编码、Provider 透传和严格能力边界。
/// </summary>
public sealed class EscPosPrinterProfileTests
{
    [Fact]
    public void Esc_star_8_dot_mode_writes_diagonal_columns_as_manual_golden_bytes()
    {
        var profile = EscPosPrinterProfile.EightDotImpact(8, 180, 180, 180);
        var raster = new byte[] { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
                new EscPosDeviceOptions { Profile = profile })
            .RasterImage(8, 8, raster)
            .Build().GetBytes();

        Assert.Equal(new byte[]
        {
            0x1B, 0x55, 0x01,
            0x1B, 0x33, 0x08,
            0x1B, 0x2A, 0x01, 0x08, 0x00,
            0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01,
            0x0A,
            0x1B, 0x32,
            0x1B, 0x55, 0x00
        }, bytes);
    }

    [Fact]
    public void Esc_star_24_dot_mode_writes_three_bytes_per_column_as_manual_golden_bytes()
    {
        var profile = new EscPosPrinterProfile
        {
            Name = "24-dot test",
            ImageCommand = EscPosImageCommand.BitImageEscStar,
            BitImageMode = EscPosBitImageMode.DoubleDensity24,
            HorizontalDpi = 203,
            VerticalDpi = 203,
            MotionUnitsPerInch = 203,
            PrintableWidthDots = 2
        };
        var raster = new byte[24];
        raster[0] = 0x80;
        raster[1] = 0x40;
        raster[8] = 0x80;
        raster[9] = 0x40;
        raster[16] = 0x80;
        raster[17] = 0x40;

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
                new EscPosDeviceOptions { Profile = profile })
            .RasterImage(2, 24, raster)
            .Build().GetBytes();

        Assert.Equal(new byte[]
        {
            0x1B, 0x33, 0x18,
            0x1B, 0x2A, 0x21, 0x02, 0x00,
            0x80, 0x80, 0x80, 0x40, 0x40, 0x40,
            0x0A,
            0x1B, 0x32
        }, bytes);
    }

    [Fact]
    public void Esc_star_8_dot_mode_handles_a_short_tail_band_and_non_byte_aligned_width()
    {
        var profile = EscPosPrinterProfile.EightDotImpact(10, 180, 180, 180);
        var raster = new byte[]
        {
            0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0,
            0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0
        };

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
                new EscPosDeviceOptions { Profile = profile })
            .RasterImage(10, 10, raster)
            .Build().GetBytes();

        Assert.Equal(new byte[]
        {
            0x1B, 0x55, 0x01,
            0x1B, 0x33, 0x08,
            0x1B, 0x2A, 0x01, 0x0A, 0x00,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x0A,
            0x1B, 0x33, 0x02,
            0x1B, 0x2A, 0x01, 0x0A, 0x00,
            0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0,
            0x0A,
            0x1B, 0x32,
            0x1B, 0x55, 0x00
        }, bytes);
    }

    [Fact]
    public void Esc_star_multiple_horizontal_blocks_emit_one_lf_for_each_band()
    {
        var profile = EscPosPrinterProfile.EightDotImpact(10, 180, 180, 180);
        profile.UnidirectionalBitImage = false;
        profile.MaxImageBlockBytes = 4;
        var raster = new byte[]
        {
            0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0,
            0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC0
        };

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
                new EscPosDeviceOptions { Profile = profile })
            .RasterImage(10, 8, raster)
            .Build().GetBytes();

        Assert.Equal(new byte[]
        {
            0x1B, 0x33, 0x08,
            0x1B, 0x2A, 0x01, 0x04, 0x00, 0xFF, 0xFF, 0xFF, 0xFF,
            0x1B, 0x2A, 0x01, 0x04, 0x00, 0xFF, 0xFF, 0xFF, 0xFF,
            0x1B, 0x2A, 0x01, 0x02, 0x00, 0xFF, 0xFF,
            0x0A,
            0x1B, 0x32
        }, bytes);
        Assert.Equal(1, bytes.Count(value => value == 0x0A));
    }

    [Fact]
    public void Esc_star_non_square_dpi_resamples_and_uses_cumulative_feed_rounding()
    {
        var profile = EscPosPrinterProfile.EightDotImpact(1, 180, 60, 144);
        var raster = new byte[72];
        raster[4] = 0x80;

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
                new EscPosDeviceOptions { Profile = profile })
            .RasterImage(1, 72, raster)
            .Build().GetBytes();

        Assert.Equal(new byte[]
        {
            0x1B, 0x55, 0x01,
            0x1B, 0x33, 0x13,
            0x1B, 0x2A, 0x01, 0x01, 0x00, 0x40,
            0x0A,
            0x1B, 0x33, 0x13,
            0x1B, 0x2A, 0x01, 0x01, 0x00, 0x00,
            0x0A,
            0x1B, 0x33, 0x14,
            0x1B, 0x2A, 0x01, 0x01, 0x00, 0x00,
            0x0A,
            0x1B, 0x32,
            0x1B, 0x55, 0x00
        }, bytes);
    }

    [Fact]
    public void Gs_v_0_image_blocks_do_not_add_feed_or_cut_commands()
    {
        var profile = new EscPosPrinterProfile
        {
            Name = "GS v 0 block test",
            ImageCommand = EscPosImageCommand.RasterGsV0,
            BitImageMode = EscPosBitImageMode.DoubleDensity24,
            HorizontalDpi = 203,
            VerticalDpi = 203,
            MotionUnitsPerInch = 203,
            PrintableWidthDots = 16,
            MaxImageBlockBytes = 4
        };
        var raster = new byte[]
        {
            0x80, 0x01,
            0x40, 0x02,
            0x20, 0x04,
            0x10, 0x08,
            0x08, 0x10
        };

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
                new EscPosDeviceOptions { Profile = profile })
            .RasterImage(16, 5, raster)
            .Build().GetBytes();

        Assert.Equal(new byte[]
        {
            0x1D, 0x76, 0x30, 0x00, 0x02, 0x00, 0x02, 0x00,
            0x80, 0x01, 0x40, 0x02,
            0x1D, 0x76, 0x30, 0x00, 0x02, 0x00, 0x02, 0x00,
            0x20, 0x04, 0x10, 0x08,
            0x1D, 0x76, 0x30, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x08, 0x10
        }, bytes);
        AssertDoesNotContain(new byte[] { 0x1B, 0x64 }, bytes);
        AssertDoesNotContain(new byte[] { 0x1D, 0x56 }, bytes);
    }

    [Fact]
    public void Profile_and_input_are_snapshotted_and_build_is_idempotent()
    {
        var profile = new EscPosPrinterProfile
        {
            Name = "snapshot test",
            ImageCommand = EscPosImageCommand.RasterGsV0,
            HorizontalDpi = 203,
            VerticalDpi = 203,
            MotionUnitsPerInch = 203
        };
        var input = new byte[] { 0x80 };
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
                new EscPosDeviceOptions { Profile = profile })
            .RasterImage(8, 1, input);

        input[0] = 0x00;
        profile.ImageCommand = EscPosImageCommand.BitImageEscStar;
        profile.BitImageMode = EscPosBitImageMode.SingleDensity8;

        var expected = new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x01, 0x00, 0x01, 0x00, 0x80 };
        Assert.Equal(expected, command.Build().GetBytes());
        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void Failed_raster_image_does_not_pollute_a_profiled_command()
    {
        var profile = new EscPosPrinterProfile
        {
            ImageCommand = EscPosImageCommand.RasterGsV0,
            HorizontalDpi = 203,
            VerticalDpi = 203,
            MotionUnitsPerInch = 203
        };
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { Profile = profile });

        Assert.Throws<ArgumentException>(() => command.RasterImage(8, 2, new byte[] { 0x80 }));

        var expected = new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x01, 0x00, 0x01, 0x00, 0x80 };
        Assert.Equal(expected, command.RasterImage(8, 1, new byte[] { 0x80 }).Build().GetBytes());
    }

    [Fact]
    public void Extreme_esc_star_output_height_is_rejected_when_raster_image_is_added()
    {
        var profile = new EscPosPrinterProfile
        {
            ImageCommand = EscPosImageCommand.BitImageEscStar,
            BitImageMode = EscPosBitImageMode.SingleDensity8,
            HorizontalDpi = 1,
            VerticalDpi = int.MaxValue,
            MotionUnitsPerInch = 1,
            PrintableWidthDots = 1
        };
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { Profile = profile });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.RasterImage(1, 2, new byte[] { 0x80, 0x00 }));
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Provider_forwards_esc_star_profile_to_raster_output()
    {
        var profile = EscPosPrinterProfile.EightDotImpact(8, 180, 180, 180);
        var document = new PrintDocument();
        document.AddPage(8, 8).Add(new PrintImageElement(0, 0, 8, 8,
            new byte[] { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 }));

        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { Profile = profile });
        var bytes = provider.Render(document).Single();

        AssertContains(new byte[] { 0x1B, 0x2A, 0x01 }, bytes);
        AssertDoesNotContain(new byte[] { 0x1D, 0x76, 0x30 }, bytes);
    }

    [Fact]
    public void Native_provider_rejects_a_barcode_when_profile_disables_native_barcode()
    {
        var profile = EscPosPrinterProfile.EightDotImpact(200, 180, 60, 144);
        var document = new PrintDocument();
        document.AddPage(200, 100).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "12345", 40, 2, 2), "Rows[0].Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = profile });

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));
        Assert.Equal("Rows[0].Barcode", error.ElementPath);
    }

    [Fact]
    public void Provider_preflights_profile_printable_width()
    {
        var profile = EscPosPrinterProfile.EightDotImpact(8, 180, 180, 180);
        var document = new PrintDocument();
        document.AddPage(9, 8);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { Profile = profile });

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
        Assert.Equal("Pages[0]", error.ElementPath);
    }

    [Fact]
    public void Template_forwards_esc_star_profile_to_the_rendered_page()
    {
        var template = new BpTemplate { Width = 8, Height = 8, Rows = new List<BpRow>() };
        var result = new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            EscPosDevice = new EscPosDeviceOptions
            {
                Profile = EscPosPrinterProfile.EightDotImpact(8, 180, 180, 180)
            },
            FeedLines = 0,
            CutAfterPrint = false,
            Encoding = Encoding.ASCII
        });

        var bytes = result.Pages.Single().Data;
        AssertContains(new byte[] { 0x1B, 0x2A, 0x01 }, bytes);
        AssertDoesNotContain(new byte[] { 0x1D, 0x76, 0x30 }, bytes);
    }

    [Fact]
    public void Template_preflights_profile_printable_width_before_rendering()
    {
        var template = new BpTemplate { Width = 9, Height = 8, Rows = new List<BpRow>() };
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                EscPosDevice = new EscPosDeviceOptions
                {
                    Profile = EscPosPrinterProfile.EightDotImpact(8, 180, 180, 180)
                },
                FeedLines = 0,
                CutAfterPrint = false,
                Encoding = Encoding.ASCII
            }));

        Assert.Equal("Pages[0]", error.SourcePath);
    }

    private static void AssertContains(byte[] expected, byte[] actual)
    {
        for (var start = 0; start <= actual.Length - expected.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < expected.Length; offset++)
                if (actual[start + offset] != expected[offset])
                {
                    matched = false;
                    break;
                }
            if (matched) return;
        }

        throw new Xunit.Sdk.XunitException(
            $"Expected byte sequence [{BitConverter.ToString(expected)}] was not found in [{BitConverter.ToString(actual)}].");
    }

    private static void AssertDoesNotContain(byte[] unexpected, byte[] actual)
    {
        for (var start = 0; start <= actual.Length - unexpected.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < unexpected.Length; offset++)
                if (actual[start + offset] != unexpected[offset])
                {
                    matched = false;
                    break;
                }
            if (matched)
                throw new Xunit.Sdk.XunitException(
                    $"Unexpected byte sequence [{BitConverter.ToString(unexpected)}] was found in [{BitConverter.ToString(actual)}].");
        }
    }
}
