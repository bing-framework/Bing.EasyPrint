using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证模板中的结构化 GS1 来源、绑定与 Digital Link 输出限制。</summary>
public sealed class Gs1StructuredTemplateTests
{
    private static readonly TemplateRenderOptions Options = new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Raster,
        Encoding = Encoding.ASCII
    };

    [Fact]
    public void Digital_link_qr_round_trips_and_matches_the_legacy_plain_uri_output()
    {
        const string uri = "https://id.gs1.org/01/09520123456788/22/2A/10/LOT-A";
        var template = Single(DigitalLinkQr());
        var loader = new TemplateLoader();
        var json = loader.SaveJson(template, Options);
        var restored = loader.LoadJson(json, Options);
        var renderer = new TemplateRenderer();
        var structured = renderer.RenderWithLayout(restored, null, options: Options);
        var legacy = renderer.RenderWithLayout(Single(new BpQrCodeElement
        {
            Field = new BgField { Value = uri }, Gs1 = false
        }), null, options: Options);

        Assert.Equal(BgGs1InputMode.DigitalLink, restored.Rows[0].Cells[0].Elements[0].Field.Gs1Input.Mode);
        Assert.Equal(new[] { "01", "22", "10" },
            restored.Rows[0].Cells[0].Elements[0].Field.Gs1Input.Items.Select(item => item.Ai));
        Assert.Equal(legacy.Pages[0].Data, structured.Pages[0].Data);
    }

    [Fact]
    public void Data_matrix_digital_link_uses_the_regular_matrix_path()
    {
        var matrix = new BpMatrixCodeElement
        {
            Format = "DataMatrix",
            Width = 120,
            Height = 120,
            Gs1 = false,
            Field = new BgField { Gs1Input = DigitalLinkInput() }
        };

        var result = new TemplateRenderer().RenderWithLayout(Single(matrix), null, options: Options);

        Assert.NotEmpty(result.Pages[0].Data);
    }

    [Fact]
    public void Structured_items_bind_original_values_and_require_missing_fields_at_render_time()
    {
        var qr = DigitalLinkQr();
        qr.Field!.Gs1Input!.Items[0].Name = "Product.Gtin";
        qr.Field.Gs1Input.Items[0].Value = null;
        qr.Field.Gs1Input.ValidateAssociations = true;
        var template = Single(qr);

        var result = new TemplateRenderer().RenderWithLayout(template,
            new { Product = new { Gtin = "09520123456788" } }, options: Options);
        Assert.NotEmpty(result.Pages[0].Data);

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, new { Product = new { } }, options: Options));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    [Fact]
    public void Structured_sources_reject_legacy_field_collisions_and_unsupported_elements()
    {
        var mixed = DigitalLinkQr();
        mixed.Field!.Name = "Product.Gtin";
        Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Single(mixed), null, options: Options));

        var pdf417 = new BpMatrixCodeElement
        {
            Format = "PDF417", Width = 160, Height = 80, Gs1 = false,
            Field = new BgField { Gs1Input = DigitalLinkInput() }
        };
        Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Single(pdf417), null, options: Options));

        var composite = new BpGs1CompositeElement
        {
            Field = new BgField { Gs1Input = DigitalLinkInput() },
            SecondaryField = new BgField { Value = "(91)ABC" }
        };
        Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Single(composite), null, options: Options));
    }

    [Fact]
    public void Digital_link_input_rejects_unsupported_ai_and_gs1_carrier_mode()
    {
        var unsupported = DigitalLinkInput();
        unsupported.Items.Add(new BgGs1InputItem { Ai = "8200", Value = "https://example.test/p" });
        var unsupportedQr = DigitalLinkQr();
        unsupportedQr.Field!.Gs1Input = unsupported;
        Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Single(unsupportedQr), null, options: Options));

        var gs1Qr = DigitalLinkQr();
        gs1Qr.Gs1 = true;
        Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            Single(gs1Qr), null, options: Options));
    }

    [Fact]
    public void Elements_mode_reuses_the_existing_gs1_qr_encoder()
    {
        var qr = new BpQrCodeElement
        {
            Gs1 = true,
            Field = new BgField
            {
                Gs1Input = new BgGs1Input
                {
                    Mode = BgGs1InputMode.Elements,
                    ValidateAssociations = true,
                    Items = new List<BgGs1InputItem>
                    {
                        new BgGs1InputItem { Ai = "01", Value = "09520123456788" },
                        new BgGs1InputItem { Ai = "10", Value = "LOT-A" }
                    }
                }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(Single(qr), null, options: Options);

        Assert.NotEmpty(result.Pages[0].Data);
    }

    private static BpQrCodeElement DigitalLinkQr()
        => new BpQrCodeElement
        {
            Gs1 = false,
            Field = new BgField { Gs1Input = DigitalLinkInput() }
        };

    private static BgGs1Input DigitalLinkInput()
        => new BgGs1Input
        {
            Mode = BgGs1InputMode.DigitalLink,
            BaseUri = "https://id.gs1.org",
            Items = new List<BgGs1InputItem>
            {
                new BgGs1InputItem { Ai = "01", Value = "09520123456788" },
                new BgGs1InputItem { Ai = "22", Value = "2A" },
                new BgGs1InputItem { Ai = "10", Value = "LOT-A" }
            }
        };

    private static BpTemplate Single(BpElement element)
        => new BpTemplate
        {
            Version = 2,
            Width = 400,
            Height = 200,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 200,
                    Cells = new List<BpCell>
                    {
                        new BpCell { Width = 400, Elements = new List<BpElement> { element } }
                    }
                }
            }
        };
}
