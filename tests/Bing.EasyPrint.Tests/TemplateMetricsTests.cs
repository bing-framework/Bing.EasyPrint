using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class TemplateMetricsTests
{
    [Theory]
    [InlineData("L", 17, 1)]
    [InlineData("L", 18, 2)]
    [InlineData("M", 213, 10)]
    [InlineData("M", 214, 11)]
    [InlineData("Q", 151, 10)]
    [InlineData("Q", 152, 11)]
    [InlineData("H", 119, 10)]
    [InlineData("H", 120, 11)]
    [InlineData("M", 666, 20)]
    [InlineData("M", 667, 21)]
    [InlineData("M", 1059, 26)]
    [InlineData("M", 1060, 27)]
    [InlineData("L", 2953, 40)]
    [InlineData("M", 2331, 40)]
    [InlineData("Q", 1663, 40)]
    [InlineData("H", 1273, 40)]
    public void Qr_byte_capacity_boundaries_choose_expected_version(string level, int count, int version)
    {
        var qr = new BpQrCodeElement { UnitWidth = 1, ErrorLevel = level, Field = Value(new string('a', count)) };
        var extent = 25 + version * 4;
        var page = Assert.Single(Render(qr, extent).Pages);
        Assert.Equal(extent, page.Height);
        Assert.Contains("B QR 4 4 M 2 U 1", Encoding.UTF8.GetString(page.Data));
        Assert.Throws<ArgumentException>(() => Render(qr, extent - 1));
    }

    [Theory]
    [InlineData('1', 41, 1)]
    [InlineData('1', 42, 2)]
    [InlineData('A', 25, 1)]
    [InlineData('A', 26, 2)]
    [InlineData('1', 7089, 40)]
    [InlineData('A', 4296, 40)]
    public void Qr_numeric_and_alphanumeric_capacity_is_not_limited_to_byte_mode(char value, int count, int version)
    {
        var qr = new BpQrCodeElement { UnitWidth = 1, ErrorLevel = "L", Field = Value(new string(value, count)) };
        Assert.Equal(25 + version * 4, Render(qr, 200).Pages[0].Height);
    }

    [Theory]
    [InlineData('a', 2954)]
    [InlineData('1', 7090)]
    [InlineData('A', 4297)]
    public void Qr_rejects_data_beyond_version_40(char value, int count) =>
        Assert.Throws<ArgumentException>(() => Render(new BpQrCodeElement { UnitWidth = 1, ErrorLevel = "L", Field = Value(new string(value, count)) }, 1000));

    [Fact]
    public void Qr_counts_encoded_bytes_and_fits_normal_label_after_version_10()
    {
        var qr = new BpQrCodeElement { UnitWidth = 4, ErrorLevel = "M", Field = Value(new string('a', 214)) };
        Assert.Equal(276, Render(qr, 576).Pages[0].Height);
        qr.UnitWidth = 1;
        qr.ErrorLevel = "L";
        qr.Field.Value = new string('中', 6); // UTF-8 18 字节，超出版本 1 的 17 字节。
        Assert.Equal(33, Render(qr, 576).Pages[0].Height);
    }

    public static IEnumerable<object[]> FontSizes() => Enum.GetValues(typeof(FontSize)).Cast<int>().Select(size => new object[] { size });

    [Theory]
    [MemberData(nameof(FontSizes))]
    public void Every_builtin_size_wraps_at_actual_scaled_cell_boundary(int size)
    {
        var font = new BpFontElement { FontSize = size, Field = Value("AB中C") };
        var page = Render(font, size * 2).Pages[0];
        var output = Encoding.UTF8.GetString(page.Data);
        var number = size % 24 == 0 ? 24 : 55;
        var scale = size / (number == 24 ? 24 : 16);
        Assert.Contains($"SETMAG {scale} {scale}", output);
        Assert.Contains($"T {number} 0 0 0 AB中\r\n", output);
        Assert.Contains($"T {number} 0 0 {size + 2} C\r\n", output);
        Assert.Equal(size * 2 + 2, page.Height);
    }

    [Theory]
    [InlineData("éｶ中", 48)]
    [InlineData("e\u0301中", 36)]
    [InlineData("😀中", 48)]
    [InlineData("Ａ中", 48)]
    public void Unicode_widths_preserve_complete_text_elements(string text, int width)
    {
        var font = new BpFontElement { FontSize = 24, Field = Value(text + "X") };
        var output = Encoding.UTF8.GetString(Render(font, width).Pages[0].Data);
        Assert.Contains("T 24 0 0 0 " + text + "\r\n", output);
        Assert.Contains("T 24 0 0 26 X\r\n", output);
    }

    [Fact]
    public void Codepage_glyph_width_uses_single_or_double_byte_font_cell()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var font = new BpFontElement { FontSize = 24, Field = Value("éA") };
        Assert.Equal(24, Render(font, 24, Encoding.GetEncoding(1252)).Pages[0].Height);
        Assert.Equal(50, Render(font, 24, Encoding.GetEncoding(936)).Pages[0].Height);
    }

    [Theory]
    [InlineData(20, 0)]
    [InlineData(24, 55)]
    [InlineData(16, 24)]
    [InlineData(24, 99)]
    public void Unsupported_font_profiles_fail_explicitly(int size, int number) =>
        Assert.Throws<ArgumentException>(() => Render(new BpFontElement { FontSize = size, FontName = number, Field = Value("x") }, 576));

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_protocols_accept_font_sizes_outside_cpcl_profiles(PrintProtocol protocol)
    {
        var template = new BpTemplate {
            Width = 120, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpFontElement { FontSize = 30, Field = Value("AB") }
                } }
            } } }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null, options: new TemplateRenderOptions {
            Encoding = Encoding.ASCII, Protocol = protocol
        });

        var text = Assert.Single(result.Pages[0].Elements.Where(element => element.Type == 1));
        Assert.Equal("AB", text.Text);
        Assert.Equal(30, text.Height);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_protocol_fit_selects_non_cpcl_font_size(PrintProtocol protocol)
    {
        var template = new BpTemplate {
            Width = 30, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpFontElement { FontSize = 30, Field = new BgField { Value = "AB", Fit = true } }
                } }
            } } }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null, options: new TemplateRenderOptions {
            Encoding = Encoding.ASCII, Protocol = protocol
        });

        var text = Assert.Single(result.Pages[0].Elements.Where(element => element.Type == 1));
        Assert.Equal("AB", text.Text);
        Assert.InRange(text.Height, 1, 29);
        Assert.InRange(text.Width, 1, 30);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_protocol_uses_configured_device_cell_width_for_wrapping(PrintProtocol protocol)
    {
        var template = new BpTemplate {
            Width = 24, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpFontElement { FontSize = 24, Field = Value("AB") }
                } }
            } } }
        };

        TemplateRenderResult RenderAt(double? cellWidth) => new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = protocol,
                DeviceHalfCellWidthAt24 = cellWidth });

        Assert.Equal(2, RenderAt(null).Pages[0].Elements.Count(element => element.Type == 1));
        var text = Assert.Single(RenderAt(12).Pages[0].Elements.Where(element => element.Type == 1));
        Assert.Equal("AB", text.Text);
        Assert.Equal(24, text.Width);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_protocol_can_calibrate_full_width_independently(PrintProtocol protocol)
    {
        var template = new BpTemplate {
            Width = 32, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpFontElement { FontSize = 24, Field = Value("中A") }
                } }
            } } }
        };

        TemplateRenderResult RenderAt(double? fullWidth) => new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions { Encoding = Encoding.UTF8, Protocol = protocol,
                DeviceHalfCellWidthAt24 = 12, DeviceFullCellWidthAt24 = fullWidth });

        Assert.Equal(2, RenderAt(null).Pages[0].Elements.Count(element => element.Type == 1));
        var text = Assert.Single(RenderAt(20).Pages[0].Elements.Where(element => element.Type == 1));
        Assert.Equal("中A", text.Text);
        Assert.Equal(32, text.Width);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 0)]
    [InlineData(PrintProtocol.Zpl, double.PositiveInfinity)]
    [InlineData(PrintProtocol.Cpcl, 12)]
    public void Device_cell_width_rejects_invalid_or_unrelated_configuration(PrintProtocol protocol, double cellWidth)
    {
        var template = new BpTemplate {
            Width = 100, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> { new BpFontElement { Field = Value("A") } } }
            } } }
        };

        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(template, null,
            options: new TemplateRenderOptions { Protocol = protocol,
                Encoding = Encoding.ASCII, DeviceHalfCellWidthAt24 = cellWidth }));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 0)]
    [InlineData(PrintProtocol.Zpl, double.NaN)]
    [InlineData(PrintProtocol.Zpl, double.PositiveInfinity)]
    [InlineData(PrintProtocol.Cpcl, 20)]
    [InlineData(PrintProtocol.EscPos, 20)]
    public void Device_full_cell_width_rejects_invalid_or_unrelated_configuration(PrintProtocol protocol,
        double cellWidth)
    {
        var template = new BpTemplate {
            Width = 100, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> { new BpFontElement { Field = Value("A") } } }
            } } }
        };

        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(template, null,
            options: new TemplateRenderOptions { Protocol = protocol,
                Encoding = Encoding.ASCII, DeviceFullCellWidthAt24 = cellWidth }));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native, 2)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native, 3)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native, 3)]
    public void Coordinate_protocols_wrap_text_with_their_declared_cell_metrics(
        PrintProtocol protocol, EscPosLayoutMode layout, int expectedLineCount)
    {
        var result = RenderTextWithLayout(protocol, layout, 24, 0, true, "ABC");
        var text = result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type == 1)
            .ToArray();

        Assert.Single(result.Pages);
        Assert.Equal(expectedLineCount, text.Length);
        Assert.Equal("ABC", string.Concat(text.Select(element => element.Text)));
        Assert.All(text, element => Assert.InRange(element.Width, 1, 24));
    }

    [Fact]
    public void EscPos_raster_wraps_text_using_the_same_font_measurement_as_the_canvas()
    {
        var catalog = FixedTestFonts.Catalog();
        var ink = EscPosRasterCanvas.MeasureTextInkBounds("A", 24, bold: false, underline: false,
            fontFamily: null, maxPixels: long.MaxValue, fonts: catalog);
        var singleCharacterWidth = Math.Max(EscPosRasterCanvas.MeasureTextWidth("A", 24,
                bold: false, fontFamily: null, fonts: catalog),
            Math.Max(0, -ink.Left) + ink.Right);
        Assert.InRange(singleCharacterWidth, 1, 25);

        var result = RenderTextWithLayout(PrintProtocol.EscPos, EscPosLayoutMode.Raster,
            singleCharacterWidth, 0, true, "AAAA");
        var text = result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type == 1)
            .ToArray();

        Assert.True(text.Length >= 2, "ESC/POS Raster 应按实际字体宽度换行。");
        Assert.Equal("AAAA", string.Concat(text.Select(element => element.Text)));
        Assert.All(text, element => Assert.InRange(element.Width, 1, singleCharacterWidth));
        Assert.Contains(result.Pages[0].Data, value => value != 0);
    }

    [Theory]
    [InlineData("AVATAR 12345", 90)]
    [InlineData("iiiiWWWW....", 100)]
    [InlineData("HELLO 世界 123", 100)]
    [InlineData("AB\n\nCD", 90)]
    public void EscPos_raster_wrap_matches_incremental_reference(string value, int width)
    {
        var expected = RasterReferenceLines(value, width);
        var result = RenderTextWithLayout(PrintProtocol.EscPos, EscPosLayoutMode.Raster,
            width, 0, true, value);
        var actual = result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type == 1)
            .Select(element => element.Text)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EscPos_raster_layout_handles_a_long_single_line()
    {
        var value = new string('A', 256);
        var result = RenderTextWithLayout(PrintProtocol.EscPos, EscPosLayoutMode.Raster,
            5000, 0, true, value);

        Assert.Equal(value, Assert.Single(result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type == 1)).Text);
    }

    private static string[] RasterReferenceLines(string value, int width)
    {
        var catalog = FixedCatalogFor(value);
        var fontFamily = EscPosRasterCanvas.ResolveFontFamilyName(value, null, false, catalog);
        var result = new List<string>();
        var line = new System.Text.StringBuilder();
        var iterator = System.Globalization.StringInfo.GetTextElementEnumerator(
            value.Replace("\r\n", "\n").Replace('\r', '\n'));
        while (iterator.MoveNext())
        {
            var character = iterator.GetTextElement();
            if (character == "\n") { result.Add(line.ToString()); line.Clear(); continue; }
            var candidate = line.ToString() + character;
            if (RasterReferenceWidth(candidate, fontFamily, catalog) > width)
            {
                result.Add(line.ToString()); line.Clear();
            }
            line.Append(character);
        }
        result.Add(line.ToString());
        return result.ToArray();
    }

    private static int RasterReferenceWidth(string value, string fontFamily, RasterFontCatalog catalog)
    {
        var advance = EscPosRasterCanvas.MeasureTextWidth(value, 24, bold: false,
            fontFamily: fontFamily, fonts: catalog);
        var ink = EscPosRasterCanvas.MeasureTextInkBounds(value, 24, bold: false, underline: false,
            fontFamily: fontFamily, maxPixels: long.MaxValue, fonts: catalog);
        return ink.IsEmpty ? advance : Math.Max(advance, Math.Max(0, -ink.Left) + ink.Right);
    }

    [Fact]
    [Trait("FontEnvironment", "System")]
    public void EscPos_raster_template_uses_explicit_system_font_family()
    {
        var family = EscPosRasterCanvas.ResolveFontFamilyName("HELLO");
        var template = new BpTemplate {
            Width = 120, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpFontElement { FontSize = 24, FontFamily = family, Field = Value("HELLO") }
                } }
            } } }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null, options: new TemplateRenderOptions {
            Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster
        });

        var text = Assert.Single(result.Pages[0].Elements.Where(element => element.Type == 1));
        var ink = EscPosRasterCanvas.MeasureTextInkBounds("HELLO", 24, fontFamily: family);
        Assert.Equal(Math.Max(EscPosRasterCanvas.MeasureTextWidth("HELLO", 24, fontFamily: family),
            Math.Max(0, -ink.Left) + ink.Right), text.Width);
    }

    [Fact]
    public void Template_font_family_errors_include_element_path()
    {
        var template = new BpTemplate {
            Width = 120, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Elements = new List<BpElement> {
                    new BpFontElement { FontSize = 24, FontFamily = "__missing_font__", Field = Value("A") }
                } }
            } } }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(template, null,
            options: new TemplateRenderOptions { Encoding = Encoding.ASCII, Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                EscPosDevice = new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() }}));

        Assert.Contains("Rows[0]", error.SourcePath);
        Assert.Contains(PrintProtocol.EscPos.ToString(), error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Raster)]
    public void Text_rows_are_paginated_consistently_for_each_protocol(
        PrintProtocol protocol, EscPosLayoutMode layout)
    {
        var result = RenderTextWithLayout(protocol, layout, 24, 26, false, "A", "B");
        var text = result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type == 1)
            .ToArray();

        Assert.Equal(2, result.PageCount);
        Assert.All(result.Pages, page => Assert.Equal(26, page.Height));
        Assert.Equal(new[] { "A", "B" }, text.Select(element => element.Text).ToArray());
        Assert.Equal(new[] { 0, 1 }, text.Select(element => element.FragmentIndex).ToArray());
    }

    private static BgField Value(string text) => new BgField { Value = text };
    private static TemplateRenderResult Render(BpElement element, int width, Encoding encoding = null) =>
        new TemplateRenderer().Render(new BpTemplate {
            Width = width, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> { element } } } } }
        }, null, options: new TemplateRenderOptions { Encoding = encoding ?? Encoding.UTF8 });

    private static TemplateRenderResult RenderTextWithLayout(PrintProtocol protocol, EscPosLayoutMode layout,
        int width, int height, bool autoHeight, params string[] values)
    {
        var template = new BpTemplate {
            Width = width,
            Height = height,
            AutoHeight = autoHeight,
            Rows = values.Select(value => new BpRow {
                Cells = new List<BpCell> { new BpCell {
                    Elements = new List<BpElement> { new BpFontElement { FontSize = 24, Field = Value(value) } }
                } }
            }).ToList()
        };
        return new TemplateRenderer().RenderWithLayout(template, null, options: new TemplateRenderOptions {
            Encoding = Encoding.ASCII,
            Protocol = protocol,
            EscPosLayout = layout,
            EscPosDevice = layout == EscPosLayoutMode.Raster
                ? new EscPosDeviceOptions { RasterFonts = FixedCatalogFor(values) }
                : null
        });
    }

    private static RasterFontCatalog FixedCatalogFor(params string[] values)
        => values.Any(value => value != null && value.Any(character =>
            character >= '\u4E00' && character <= '\u9FFF'))
            ? FixedTestFonts.ChineseCatalog()
            : FixedTestFonts.Catalog();
}
