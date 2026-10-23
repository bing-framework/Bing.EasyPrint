using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 GS1 二维模板的严格关联、缓存复用与尺寸契约。</summary>
public sealed class Gs1TwoDimensionalTemplateTests
{
    [Fact]
    public void Data_matrix_strict_association_rejects_missing_required_ai()
    {
        var error = Assert.Throws<TemplateRenderException>(() =>
            Render(Template(DataMatrix("(02)12345678901231")), strict: true, width: 400, height: 300));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("02", error.Message);
        Assert.Contains("37", error.Message);

        Render(Template(DataMatrix("(02)12345678901231")), strict: false, width: 400, height: 300);
        Render(Template(DataMatrix("(00)123456789012345675(02)12345678901231(37)12")),
            strict: true, width: 400, height: 300);
    }

    [Fact]
    public void Qr_strict_association_rejects_missing_required_ai()
    {
        var error = Assert.Throws<TemplateRenderException>(() =>
            Render(Template(Qr("(02)12345678901231")), strict: true, width: 400, height: 300));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("02", error.Message);
        Assert.Contains("37", error.Message);
    }

    [Fact]
    public void Duplicate_gs1_qr_instances_reuse_the_cached_image()
    {
        var result = Render(Template(Qr("(01)12345678901231(10)LOT"), Qr("(01)12345678901231(10)LOT")),
            strict: false, width: 400, height: 500);
        Assert.Single(result.Pages);
        Assert.NotEmpty(result.Pages[0].Data);
    }

    [Fact]
    public void Same_gs1_qr_text_with_different_error_levels_is_not_merged()
    {
        var low = Render(Template(Qr("(01)12345678901231(10)LOT", "L"), Qr("(01)12345678901231(10)LOT", "L")),
            strict: false, width: 400, height: 500);
        var mixed = Render(Template(Qr("(01)12345678901231(10)LOT", "L"), Qr("(01)12345678901231(10)LOT", "H")),
            strict: false, width: 400, height: 500);
        Assert.NotEqual(low.Pages[0].Data, mixed.Pages[0].Data);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(33)]
    public void Gs1_qr_rejects_unit_width_outside_the_existing_contract(int unitWidth)
    {
        var qr = Qr("(01)12345678901231");
        qr.UnitWidth = unitWidth;

        Assert.Throws<TemplateRenderException>(() =>
            Render(Template(qr), strict: false, width: 1200, height: 1200));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    public void Gs1_qr_accepts_contract_boundaries_with_rotation_and_asymmetric_quiet_zone(int unitWidth)
    {
        var qr = Qr("(01)12345678901231", unitWidth: unitWidth);
        qr.Rotation = RotationAngle.Rotate90;
        qr.QuietZone = new PrintQuietZone(3, 5, 7, 11);

        var result = Render(Template(qr), strict: false, width: 1000, height: 1000);
        Assert.Single(result.Pages);
        Assert.NotEmpty(result.Pages[0].Data);
    }

    [Theory]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate270)]
    public void Gs1_qr_uses_transposed_occupancy_for_cell_and_neighbor_layout(RotationAngle rotation)
    {
        var qr = Qr("(01)12345678901231", unitWidth: 1);
        qr.Width = 450;
        qr.Height = 40;
        qr.Rotation = rotation;
        var template = new BpTemplate
        {
            Version = 2,
            Width = 400,
            Height = 500,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell { Elements = new List<BpElement> { qr } }
                    }
                },
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Elements = new List<BpElement>
                            {
                                new BpFontElement { Field = new BgField { Value = "NEIGHBOR" } }
                            }
                        }
                    }
                }
            }
        };
        var result = Render(template, strict: false, width: 400, height: 500);
        var qrLayout = Assert.Single(result.Pages[0].Elements.Where(value => value.Type == 3));
        var neighbor = Assert.Single(result.Pages[0].Elements.Where(value => value.Type == 1));
        Assert.Equal(40, qrLayout.Width);
        Assert.Equal(450, qrLayout.Height);
        Assert.Equal(450, neighbor.Y);
        Assert.Equal(0, neighbor.X);
    }

    [Fact]
    public void Gs1_qr_rotated_occupancy_over_cell_width_is_rejected_during_layout()
    {
        var qr = Qr("(01)12345678901231", unitWidth: 1);
        qr.Width = 40;
        qr.Height = 450;
        qr.Rotation = RotationAngle.Rotate90;

        var error = Assert.Throws<TemplateRenderException>(() =>
            Render(Template(qr), strict: false, width: 400, height: 500));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("GS1 QR", error.Message);
    }

    [Fact]
    public void Gs1_template_rejects_non_ai_payload_even_without_strict_association()
    {
        Assert.Throws<TemplateRenderException>(() =>
            Render(Template(Qr("NOT-GS1")), strict: false, width: 400, height: 300));
        Assert.Throws<TemplateRenderException>(() =>
            Render(Template(DataMatrix("NOT-GS1")), strict: false, width: 400, height: 300));
    }

    private static TemplateRenderResult Render(BpTemplate template, bool strict, int width, int height)
    {
        template.Width = width;
        template.Height = height;
        return new TemplateRenderer().RenderWithLayout(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII,
            ValidateGs1Associations = strict
        });
    }

    private static BpTemplate Template(params BpElement[] elements) => new BpTemplate
    {
        Version = 2,
        Width = 1200,
        Height = 1200,
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

    private static BpQrCodeElement Qr(string value, string errorLevel = "M", int unitWidth = 4)
        => new BpQrCodeElement
        {
            UnitWidth = unitWidth,
            ErrorLevel = errorLevel,
            Gs1 = true,
            Field = new BgField { Value = value }
        };

    private static BpMatrixCodeElement DataMatrix(string value)
        => new BpMatrixCodeElement
        {
            Format = "DataMatrix",
            Width = 96,
            Height = 96,
            Gs1 = true,
            Field = new BgField { Value = value }
        };
}
