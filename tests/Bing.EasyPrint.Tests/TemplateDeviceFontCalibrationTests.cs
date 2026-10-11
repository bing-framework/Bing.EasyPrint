using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TemplateDeviceFontCalibrationTests
{
    [Fact]
    public void Zpl_uses_each_font_calibration_for_layout()
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["A"] = Calibration(10, 20, 24),
            ["B"] = Calibration(18, 36, 32)
        };

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            Template(("AB", "A"), ("AB", "B")), null, options: options).Pages);
        var text = page.Elements.Where(element => element.Type == 1).ToArray();

        Assert.Equal(2, text.Length);
        Assert.Equal((20, 24), (text[0].Width, text[0].Height));
        Assert.Equal((36, 32), (text[1].Width, text[1].Height));
    }

    [Fact]
    public void Default_font_calibration_measures_mixed_width_text()
    {
        var options = Options(PrintProtocol.Zpl);
        options.Encoding = Encoding.UTF8;
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["0"] = Calibration(10, 28, 30)
        };

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            Template(("中A", null)), null, options: options).Pages);
        var text = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal(38, text.Width);
        Assert.Equal(30, text.Height);
    }

    [Fact]
    public void Strict_mode_identifies_font_missing_from_calibration_table()
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["A"] = Calibration(10, 20, 24)
        };

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().Render(Template(("AB", "B")), null, options: options));

        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("字体 B", error.Message);
    }

    [Fact]
    public void Font_calibration_overrides_global_values_and_global_values_remain_fallback()
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceHalfCellWidthAt24 = 12;
        options.DeviceFullCellWidthAt24 = 24;
        options.DeviceLineHeightAt24 = 30;
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["A"] = Calibration(8, 16, 26)
        };

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            Template(("AB", "A"), ("AB", "B")), null, options: options).Pages);
        var text = page.Elements.Where(element => element.Type == 1).ToArray();

        Assert.Equal((16, 26), (text[0].Width, text[0].Height));
        Assert.Equal((24, 30), (text[1].Width, text[1].Height));
    }

    [Fact]
    public void Tspl_fixed_font_calibration_respects_native_cell_floor()
    {
        var options = Options(PrintProtocol.Tspl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["5"] = Calibration(12, 24, 24)
        };

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            Template(("AB", "5")), null, options: options).Pages);
        var text = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal(64, text.Width);
        Assert.Equal(48, text.Height);
    }

    [Fact]
    public void Tspl_fixed_font_calibration_scales_with_actual_integer_magnification()
    {
        var options = Options(PrintProtocol.Tspl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["1"] = Calibration(18, 36, 28)
        };
        var template = Template(("AB", "1"));
        ((BpFontElement)template.Rows[0].Cells[0].Elements[0]).FontSize = 36;

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            template, null, options: options).Pages);
        var text = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal(54, text.Width);
        Assert.Equal(42, text.Height);
    }

    [Fact]
    public void Text_element_advance_calibration_is_selected_by_device_font_and_used_for_wrapping()
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["A"] = Calibration(10, 20, 24),
            ["B"] = new DeviceFontCalibration {
                HalfCellWidthAt24 = 10,
                FullCellWidthAt24 = 20,
                LineHeightAt24 = 24,
                TextElements = new Dictionary<string, DeviceTextElementCalibration> {
                    ["A"] = new DeviceTextElementCalibration { AdvanceAt24 = 7 }
                }
            }
        };
        var template = Template(("AA", "B"));
        ((BpFontElement)template.Rows[0].Cells[0].Elements[0]).Width = 14;

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            template, null, options: options).Pages);
        var text = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal("AA", text.Text);
        Assert.Equal(14, text.Width);
        Assert.Equal(24, text.Height);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Combining_text_element_calibration_overrides_fallback_cell_advance(PrintProtocol protocol)
    {
        var options = Options(protocol);
        options.Encoding = Encoding.UTF8;
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["0"] = new DeviceFontCalibration {
                HalfCellWidthAt24 = 10,
                FullCellWidthAt24 = 20,
                LineHeightAt24 = 24,
                TextElements = new Dictionary<string, DeviceTextElementCalibration> {
                    ["e\u0301"] = new DeviceTextElementCalibration { AdvanceAt24 = 7 }
                }
            }
        };

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            Template(("e\u0301A", null)), null, options: options).Pages);
        var text = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal(17, text.Width);
        Assert.Equal("e\u0301A", text.Text);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "1")]
    [InlineData(PrintProtocol.Zpl, "A")]
    public void Text_element_advance_and_ink_bounds_scale_from_24_point_calibration(
        PrintProtocol protocol, string fontName)
    {
        var options = Options(protocol);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            [fontName] = new DeviceFontCalibration {
                HalfCellWidthAt24 = 10,
                FullCellWidthAt24 = 20,
                LineHeightAt24 = 24,
                TextElements = new Dictionary<string, DeviceTextElementCalibration> {
                    ["A"] = new DeviceTextElementCalibration {
                        AdvanceAt24 = 7,
                        InkLeftAt24 = -1,
                        InkTopAt24 = -1,
                        InkRightAt24 = 9,
                        InkBottomAt24 = 20
                    }
                }
            }
        };
        var template = Template(("A", fontName));
        ((BpFontElement)template.Rows[0].Cells[0].Elements[0]).FontSize = 48;

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            template, null, options: options).Pages);
        var text = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal((20, 50), (text.Width, text.Height));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "1", RotationAngle.None, 5, 5, 16, 31)]
    [InlineData(PrintProtocol.Tspl, "1", RotationAngle.Rotate90, 5, 5, 31, 16)]
    [InlineData(PrintProtocol.Tspl, "1", RotationAngle.Rotate180, 5, 5, 16, 31)]
    [InlineData(PrintProtocol.Tspl, "1", RotationAngle.Rotate270, 5, 5, 31, 16)]
    [InlineData(PrintProtocol.Zpl, "A", RotationAngle.None, 5, 5, 16, 31)]
    [InlineData(PrintProtocol.Zpl, "A", RotationAngle.Rotate90, 5, 5, 31, 16)]
    [InlineData(PrintProtocol.Zpl, "A", RotationAngle.Rotate180, 5, 5, 16, 31)]
    [InlineData(PrintProtocol.Zpl, "A", RotationAngle.Rotate270, 5, 5, 31, 16)]
    public void Calibrated_ink_bounds_are_reserved_for_each_text_rotation(
        PrintProtocol protocol, string fontName, RotationAngle rotation,
        int x, int y, int width, int height)
    {
        var options = Options(protocol);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            [fontName] = new DeviceFontCalibration {
                HalfCellWidthAt24 = 10,
                FullCellWidthAt24 = 20,
                LineHeightAt24 = 24,
                TextElements = new Dictionary<string, DeviceTextElementCalibration> {
                    ["A"] = new DeviceTextElementCalibration {
                        AdvanceAt24 = 10,
                        InkLeftAt24 = -2,
                        InkTopAt24 = -3,
                        InkRightAt24 = 14,
                        InkBottomAt24 = 28
                    }
                }
            }
        };
        var template = Template(("A", fontName));
        var element = (BpFontElement)template.Rows[0].Cells[0].Elements[0];
        element.Margin = new BpMargin { Left = 5, Top = 5 };
        element.Rotation = rotation;

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            template, null, options: options).Pages);
        var text = Assert.Single(page.Elements.Where(item => item.Type == 1));

        Assert.Equal((x, y, width, height), (text.X, text.Y, text.Width, text.Height));
    }

    [Fact]
    public void Text_element_calibration_rejects_partial_ink_bounds()
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["0"] = new DeviceFontCalibration {
                HalfCellWidthAt24 = 10,
                FullCellWidthAt24 = 20,
                LineHeightAt24 = 24,
                TextElements = new Dictionary<string, DeviceTextElementCalibration> {
                    ["A"] = new DeviceTextElementCalibration {
                        AdvanceAt24 = 10,
                        InkLeftAt24 = -1
                    }
                }
            }
        };

        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(
            Template(("A", null)), null, options: options));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Text_element_calibration_rejects_negative_or_non_finite_advance(double advance)
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["0"] = new DeviceFontCalibration {
                HalfCellWidthAt24 = 10,
                FullCellWidthAt24 = 20,
                LineHeightAt24 = 24,
                TextElements = new Dictionary<string, DeviceTextElementCalibration> {
                    ["A"] = new DeviceTextElementCalibration { AdvanceAt24 = advance }
                }
            }
        };

        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(
            Template(("A", null)), null, options: options));
    }

    [Fact]
    public void Zpl_rotated_text_keeps_the_calibrated_footprint_origin_in_field_origin()
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["A"] = new DeviceFontCalibration {
                HalfCellWidthAt24 = 10,
                FullCellWidthAt24 = 20,
                LineHeightAt24 = 24,
                TextElements = new Dictionary<string, DeviceTextElementCalibration> {
                    ["A"] = new DeviceTextElementCalibration {
                        AdvanceAt24 = 10,
                        InkLeftAt24 = -2,
                        InkTopAt24 = -3,
                        InkRightAt24 = 14,
                        InkBottomAt24 = 28
                    }
                }
            }
        };
        var font = (BpFontElement)Template(("A", "A")).Rows[0].Cells[0].Elements[0];
        font.StartCoordinate = new BpCoordinate { X = 5, Y = 5 };
        font.Rotation = RotationAngle.Rotate90;
        var template = new BpTemplate { Width = 80, Height = 40,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> {
                new BpCell { Width = 80, Elements = new List<BpElement> { font } }
            } } } };

        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            template, null, options: options).Pages);

        Assert.Contains("^FO5,5^AAR,24,24^FDA^FS", Encoding.ASCII.GetString(page.Data));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, "0")]
    [InlineData(PrintProtocol.Tspl, "A")]
    [InlineData(PrintProtocol.Zpl, "a")]
    public void Invalid_calibration_protocol_or_font_is_rejected(PrintProtocol protocol, string font)
    {
        var options = Options(protocol);
        options.RequireDeclaredNativeBounds = false;
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            [font] = Calibration(10, 20, 24)
        };

        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(
            Template(("AB", null)), null, options: options));
    }

    [Theory]
    [InlineData(double.NaN, 20, 24)]
    [InlineData(10, 0, 24)]
    [InlineData(10, 20, double.PositiveInfinity)]
    public void Calibration_requires_finite_positive_dimensions(double half, double full, double line)
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> {
            ["0"] = Calibration(half, full, line)
        };

        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(
            Template(("AB", null)), null, options: options));
    }

    [Fact]
    public void Calibration_rejects_null_font_entry_before_layout()
    {
        var options = Options(PrintProtocol.Zpl);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration> { ["0"] = null };

        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(
            Template(("AB", null)), null, options: options));
    }

    private static TemplateRenderOptions Options(PrintProtocol protocol) => new TemplateRenderOptions {
        Protocol = protocol, Encoding = Encoding.ASCII, RequireDeclaredNativeBounds = true
    };

    private static DeviceFontCalibration Calibration(double half, double full, double line) =>
        new DeviceFontCalibration { HalfCellWidthAt24 = half, FullCellWidthAt24 = full, LineHeightAt24 = line };

    private static BpTemplate Template(params (string Text, string Font)[] values)
    {
        var cells = new List<BpCell>();
        foreach (var value in values)
            cells.Add(new BpCell { Width = 200, Elements = new List<BpElement> {
                new BpFontElement { FontSize = 24, DeviceFontName = value.Font,
                    Field = new BgField { Value = value.Text } }
            } });
        return new BpTemplate { Width = 400, Height = 100,
            Rows = new List<BpRow> { new BpRow { Cells = cells } } };
    }
}
