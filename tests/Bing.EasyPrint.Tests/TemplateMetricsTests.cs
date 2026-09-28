using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
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

    private static BgField Value(string text) => new BgField { Value = text };
    private static TemplateRenderResult Render(BpElement element, int width, Encoding encoding = null) =>
        new TemplateRenderer().Render(new BpTemplate {
            Width = width, AutoHeight = true,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> { element } } } } }
        }, null, options: new TemplateRenderOptions { Encoding = encoding ?? Encoding.UTF8 });
}
