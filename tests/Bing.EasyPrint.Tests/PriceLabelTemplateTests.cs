using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class PriceLabelTemplateTests
{
    private const string ErpProductName = "三全高端汤圆炫彩小汤圆混装口味黑芝麻、花生、紫薯、抹茶、麻薯、巧克力、豆沙";
    private const string ErpBarcode = "092836193615302";

    private static TemplateRenderOptions Options() => new TemplateRenderOptions { Encoding = Encoding.UTF8 };

    private static PriceLabelData Label(decimal originPrice = 10m, decimal salePrice = 10m, decimal? memberPrice = null)
        => new PriceLabelData
        {
            Name = "Green tea",
            Barcode = null,
            QrCode = null,
            Code = "GT-01",
            OriginPrice = originPrice,
            SalePrice = salePrice,
            MemberPrice = memberPrice,
            Unit = "box",
            Specification = "100 g",
            Origin = "Hangzhou"
        };

    private static PriceLabelData ErpLabel(decimal originPrice = 300m, decimal salePrice = 250m,
        decimal? memberPrice = 200.03m, string name = ErpProductName)
        => new PriceLabelData
        {
            Name = name,
            Barcode = ErpBarcode,
            QrCode = null,
            Code = null,
            OriginPrice = originPrice,
            SalePrice = salePrice,
            MemberPrice = memberPrice,
            Unit = string.Empty,
            Specification = "250ml*16",
            Origin = "中国"
        };

    private static string Output(TemplateRenderResult result)
        => string.Concat(result.Pages.Select(page => Encoding.UTF8.GetString(page.Data)));

    [Theory]
    [InlineData(PriceLabelPreset.Mm15)]
    [InlineData(PriceLabelPreset.Mm70)]
    [InlineData(PriceLabelPreset.Mm90)]
    public void Presets_render_at_template_size_and_skip_missing_qr(PriceLabelPreset preset)
    {
        var template = PriceLabelTemplateFactory.Create(preset);
        var result = new PriceLabelRenderer().Render(template, Label(), options: Options());

        Assert.NotEmpty(result.Pages);
        Assert.All(result.Pages, page =>
        {
            Assert.Equal(template.Width, page.Width);
            Assert.Equal(template.Height, page.Height);
        });
        Assert.DoesNotContain("B QR", Output(result));
    }

    [Theory]
    [InlineData(PriceLabelPreset.Mm15, 159, 159)]
    [InlineData(PriceLabelPreset.Mm70, 540, 300)]
    [InlineData(PriceLabelPreset.Mm90, 671, 320)]
    public void Erp_fixture_uses_preset_dimensions_and_renders_key_fields_without_qr(
        PriceLabelPreset preset, int expectedWidth, int expectedHeight)
    {
        // The full ERP title is covered by the fixed-region rejection test below.
        var label = ErpLabel(name: preset == PriceLabelPreset.Mm15 ? ErpProductName : "三全汤圆");
        var result = new PriceLabelRenderer().Render(
            PriceLabelTemplateFactory.Create(preset), label, options: Options());
        var output = Output(result);

        Assert.Equal(1, result.PageCount);
        Assert.Equal(expectedWidth, result.Pages[0].Width);
        Assert.Equal(expectedHeight, result.Pages[0].Height);
        Assert.DoesNotContain("B QR", output);

        if (preset == PriceLabelPreset.Mm15) return;

        var textCommands = TextCommands(output).ToList();
        var originX = preset == PriceLabelPreset.Mm70 ? 255 : 276;
        var originY = preset == PriceLabelPreset.Mm70 ? 194 : 220;
        var specificationY = preset == PriceLabelPreset.Mm70 ? 158 : 176;
        var barcodeTextY = preset == PriceLabelPreset.Mm70 ? 241 : 266;
        var barcodeY = preset == PriceLabelPreset.Mm70 ? 263 : 282;

        Assert.Contains(textCommands, command => command.Text == "三全汤圆");
        Assert.Contains(textCommands, command => command.X == originX && command.Y == originY && command.Text == "中国");
        Assert.Contains(textCommands, command => command.X == originX && command.Y == specificationY && command.Text == "250ml*16");
        Assert.Contains(textCommands, command => command.Y == barcodeTextY && command.Text == "商品条码：" + ErpBarcode);
        Assert.Contains(textCommands, command => command.Text == "250");
        Assert.Contains(textCommands, command => command.Text == ".00");
        Assert.Contains(textCommands, command => command.Text == "¥200.03");
        Assert.Contains($"B 128 1 2 32 32 {barcodeY} {ErpBarcode}", output);
    }

    [Theory]
    [InlineData(PriceLabelPreset.Mm70)]
    [InlineData(PriceLabelPreset.Mm90)]
    public void Large_presets_render_regulatory_phone_and_human_readable_barcode_text(PriceLabelPreset preset)
    {
        var label = Label();
        label.Barcode = "1208000780";
        label.Code = "1208000780";

        var output = Output(new PriceLabelRenderer().Render(
            PriceLabelTemplateFactory.Create(preset), label, options: Options()));
        var textCommands = TextCommands(output).ToList();
        var textX = preset == PriceLabelPreset.Mm70 ? 255 : 276;
        var phoneY = preset == PriceLabelPreset.Mm70 ? 222 : 250;
        var barcodeTextY = preset == PriceLabelPreset.Mm70 ? 241 : 266;
        var barcodeY = preset == PriceLabelPreset.Mm70 ? 263 : 282;

        Assert.Contains(textCommands, command => command.X == textX && command.Y == phoneY && command.Text == "监管电话：12358");
        Assert.Contains(textCommands, command => command.X == textX && command.Y == barcodeTextY && command.Text == "商品条码：1208000780");
        Assert.Contains($"B 128 1 2 32 32 {barcodeY} 1208000780", output);
    }

    [Fact]
    public void Promotion_and_member_price_are_conditionally_rendered()
    {
        var renderer = new PriceLabelRenderer();
        var template = PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm70);
        var regular = Output(renderer.Render(template, Label(12.5m, 12.5m, 0m), options: Options()));

        Assert.DoesNotContain("促销价", regular);
        Assert.DoesNotContain("会员价", regular);
        Assert.DoesNotContain("L 12 108 237 108 1", regular);

        var promotionalLabel = Label(12.5m, 9.75m, 8.5m);
        promotionalLabel.PromotionStart = new DateTime(2026, 9, 1);
        promotionalLabel.PromotionEnd = new DateTime(2026, 9, 30);
        var prepared = renderer.PrepareData(promotionalLabel);
        var promotional = Output(renderer.Render(template, promotionalLabel, options: Options()));

        Assert.True(prepared.IsPromotion);
        Assert.True(prepared.HasMemberPrice);
        Assert.Equal("12.50", prepared.OriginPriceText);
        Assert.Equal("9", prepared.SalePriceInteger);
        Assert.Equal(".75", prepared.SalePriceFraction);
        Assert.Equal("8", prepared.MemberPriceInteger);
        Assert.Equal(".50", prepared.MemberPriceFraction);
        Assert.Contains("12.50", promotional);
        Assert.Contains("促销价", promotional);
        Assert.Contains("会员价", promotional);
        Assert.Contains("L 12 108 237 108 1", promotional);
    }

    [Theory]
    [InlineData(PriceLabelPreset.Mm70, "L 12 108 237 108 1")]
    [InlineData(PriceLabelPreset.Mm90, "L 12 116 237 116 1")]
    public void Erp_price_branches_render_regular_promotion_member_only_and_combined_states(
        PriceLabelPreset preset, string promotionLine)
    {
        var renderer = new PriceLabelRenderer();
        var template = PriceLabelTemplateFactory.Create(preset);
        var cases = new[]
        {
            (Label: ErpLabel(300m, 300m, null, "三全汤圆"), Promotion: false, Member: false,
                OriginDisplay: null, SaleInteger: "300", SaleFraction: ".00", MemberDisplay: null),
            (Label: ErpLabel(300.02m, 250.01m, null, "三全汤圆"), Promotion: true, Member: false,
                OriginDisplay: "¥300.02", SaleInteger: "250", SaleFraction: ".01", MemberDisplay: null),
            (Label: ErpLabel(300.02m, 300.02m, 200.03m, "三全汤圆"), Promotion: false, Member: true,
                OriginDisplay: null, SaleInteger: "300", SaleFraction: ".02", MemberDisplay: "¥200.03"),
            (Label: ErpLabel(300.02m, 250.01m, 200.03m, "三全汤圆"), Promotion: true, Member: true,
                OriginDisplay: "¥300.02", SaleInteger: "250", SaleFraction: ".01", MemberDisplay: "¥200.03")
        };
        var originY = preset == PriceLabelPreset.Mm70 ? 80 : 90;
        var saleIntegerY = preset == PriceLabelPreset.Mm70 ? 132 : 154;
        var saleFractionY = saleIntegerY + 7;
        var saleCurrencyY = saleIntegerY + 20;
        var memberX = preset == PriceLabelPreset.Mm70 ? 390 : 520;
        var memberY = (preset == PriceLabelPreset.Mm70 ? 158 : 176) + 26;

        foreach (var testCase in cases)
        {
            var prepared = renderer.PrepareData(testCase.Label);
            var output = Output(renderer.Render(template, testCase.Label, options: Options()));
            var textCommands = TextCommands(output).ToList();
            var hasPromotionText = textCommands.Any(command => command.Text.Contains("促销价"));
            var hasRegularPriceText = textCommands.Any(command => command.Text.Contains("零售价"));
            var hasMemberText = textCommands.Any(command => command.Text.Contains("会员价"));
            var hasPromotionLine = output.Contains(promotionLine, StringComparison.Ordinal);

            Assert.Equal(testCase.Promotion, prepared.IsPromotion);
            Assert.Equal(testCase.Member, prepared.HasMemberPrice);
            Assert.Equal(testCase.SaleInteger, prepared.SalePriceInteger);
            Assert.Equal(testCase.SaleFraction, prepared.SalePriceFraction);
            Assert.Equal(testCase.Promotion, hasPromotionText);
            Assert.Equal(!testCase.Promotion, hasRegularPriceText);
            Assert.Equal(testCase.Member, hasMemberText);
            Assert.Equal(testCase.Promotion, hasPromotionLine);
            Assert.DoesNotContain("B QR", output);
            Assert.Contains(textCommands, command => command.Y == saleIntegerY && command.Text == testCase.SaleInteger);
            Assert.Contains(textCommands, command => command.Y == saleFractionY && command.Text == testCase.SaleFraction);
            Assert.Contains(textCommands, command => command.X == 12 && command.Y == saleCurrencyY && command.Text == "¥");
            Assert.Equal(testCase.OriginDisplay,
                textCommands.Where(command => command.X == 12 && command.Y == originY &&
                        command.Text.StartsWith("¥", StringComparison.Ordinal))
                    .Select(command => command.Text).SingleOrDefault());
            Assert.Equal(testCase.MemberDisplay,
                textCommands.Where(command => command.X == memberX && command.Y == memberY)
                    .Select(command => command.Text).SingleOrDefault());
        }
    }

    [Fact]
    public void Negative_origin_sale_and_member_prices_are_rejected()
    {
        var renderer = new PriceLabelRenderer();

        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.PrepareData(Label(-0.01m, 1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.PrepareData(Label(1m, -0.01m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.PrepareData(Label(1m, 1m, -0.01m)));
    }

    [Fact]
    public void Price_label_input_controls_are_rejected_by_the_matching_element()
    {
        var renderer = new PriceLabelRenderer();
        var template = PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm70);
        var textControl = Label();
        textControl.Name = "Tea\u001bPRINT";
        Assert.Throws<ArgumentException>(() => renderer.Render(template, textControl, options: Options()));

        var qrControl = Label();
        qrControl.QrCode = "sku-1\nPRINT";
        Assert.Throws<ArgumentException>(() => renderer.Render(template, qrControl, options: Options()));

        var barcodeControl = Label();
        barcodeControl.Barcode = "123\nPRINT";
        Assert.Throws<ArgumentException>(() => renderer.Render(template, barcodeControl, options: Options()));
    }

    [Fact]
    public void Price_components_round_money_to_two_places_away_from_zero()
    {
        var prepared = new PriceLabelRenderer().PrepareData(Label(1.005m, 1.005m));

        Assert.Equal("1.01", prepared.OriginPriceText);
        Assert.Equal("1", prepared.SalePriceInteger);
        Assert.Equal(".01", prepared.SalePriceFraction);
    }

    [Fact]
    public void Price_label_json_round_trip_preserves_rendered_bytes()
    {
        var template = PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm90);
        var data = Label(20m, 16.35m, 15m);
        data.Barcode = "6901234567890";
        data.QrCode = "https://example.test/item/GT-01";
        var loader = new TemplateLoader();
        var json = loader.SaveJson(template, Options());
        var restored = loader.LoadJson(json, Options());
        var renderer = new PriceLabelRenderer();

        var original = renderer.Render(template, data, options: Options());
        var roundTrip = renderer.Render(restored, data, options: Options());

        Assert.Equal(original.PageCount, roundTrip.PageCount);
        for (var index = 0; index < original.PageCount; index++)
            Assert.Equal(original.Pages[index].Data, roundTrip.Pages[index].Data);
    }

    [Theory]
    [InlineData(PriceLabelPreset.Mm70, 390, 158, 132)]
    [InlineData(PriceLabelPreset.Mm90, 520, 176, 135)]
    public void Member_price_components_use_reserved_region_outside_barcode(
        PriceLabelPreset preset, int expectedX, int expectedY, int expectedWidth)
    {
        var template = PriceLabelTemplateFactory.Create(preset);
        var elements = template.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Elements).ToList();
        var title = Assert.Single(elements.OfType<BpFontElement>()
            .Where(element => element.Field?.Value == "会员价 / Member"));
        var amount = Assert.Single(elements.OfType<BpFontElement>()
            .Where(element => element.Field?.Name == "MemberPriceDisplay"));
        var barcode = Assert.Single(elements.OfType<BpBarcodeElement>());

        Assert.Equal(expectedX, title.StartCoordinate.X);
        Assert.Equal(expectedY, title.StartCoordinate.Y);
        Assert.Equal(expectedWidth, title.EndCoordinate.X - title.StartCoordinate.X);
        Assert.True(title.EndCoordinate.Y > title.StartCoordinate.Y);
        Assert.Equal(expectedX, amount.StartCoordinate.X);
        Assert.Equal(expectedY + 26, amount.StartCoordinate.Y);
        Assert.Equal(expectedWidth, amount.EndCoordinate.X - amount.StartCoordinate.X);
        Assert.True(amount.EndCoordinate.Y > amount.StartCoordinate.Y);
        Assert.False(Overlaps(title, barcode));
        Assert.False(Overlaps(amount, barcode));
    }

    [Fact]
    public void Long_name_and_maximum_price_that_exceed_fixed_regions_are_rejected()
    {
        var renderer = new PriceLabelRenderer();
        var template = PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm70);
        var longName = Label();
        longName.Name = new string('W', 200);
        var oversizedPrice = Label(decimal.MaxValue, decimal.MaxValue);

        Assert.Throws<ArgumentException>(() => renderer.Render(template, longName, options: Options()));
        Assert.Throws<ArgumentException>(() => renderer.Render(template, oversizedPrice, options: Options()));
    }

    [Theory]
    [InlineData(PriceLabelPreset.Mm70)]
    [InlineData(PriceLabelPreset.Mm90)]
    public void Full_erp_product_name_is_rejected_when_it_exceeds_fixed_preset_title_region(PriceLabelPreset preset)
    {
        var template = PriceLabelTemplateFactory.Create(preset);
        var label = ErpLabel();

        Assert.Equal(ErpProductName, label.Name);
        Assert.Throws<ArgumentException>(() => new PriceLabelRenderer().Render(template, label, options: Options()));
    }

    [Fact]
    public void Pm0025_title_region_renders_the_full_erp_product_name_without_overflow()
    {
        const string fullName = "万代（BANDAI）MG 高达2 拼装模型 180CM（拆封无售后）红龙异端";
        var title = new BpFontElement
        {
            FontSize = 32,
            Bold = true,
            Field = new BgField { Name = "Name" },
            StartCoordinate = Point(12, 8),
            EndCoordinate = Point(502, 84)
        };
        var template = new BpTemplate
        {
            Width = 671,
            Height = 360,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 360,
                    Cells = new List<BpCell>
                    {
                        new BpCell { Width = 671, Elements = new List<BpElement> { title } }
                    }
                }
            }
        };
        var label = Label();
        label.Name = fullName;

        var result = new PriceLabelRenderer().Render(template, label, options: Options());
        var titleCommands = TextCommands(Output(result))
            .Where(command => command.X == title.StartCoordinate.X &&
                command.Y >= title.StartCoordinate.Y && command.Y < title.EndCoordinate.Y)
            .ToList();

        Assert.Equal(1, result.PageCount);
        Assert.Equal(671, result.Pages[0].Width);
        Assert.Equal(360, result.Pages[0].Height);
        Assert.NotEmpty(titleCommands);
        Assert.Equal(fullName, string.Concat(titleCommands.Select(command => command.Text)));
        Assert.All(titleCommands, command =>
            Assert.True(command.Y + title.FontSize <= title.EndCoordinate.Y));
    }

    [Fact]
    public void Line_element_round_trips_as_type_five_and_renders_its_coordinates()
    {
        var line = new BpLineElement
        {
            StartCoordinate = Point(3, 4),
            LineEndCoordinate = Point(30, 4),
            LineWidth = 2
        };
        var template = Template(line);
        var loader = new TemplateLoader();
        var json = loader.SaveJson(template, Options());
        var restored = loader.LoadJson(json, Options());
        var restoredLine = Assert.IsType<BpLineElement>(Assert.Single(restored.Rows[0].Cells[0].Elements));

        Assert.Contains("\"Type\": 5", json);
        Assert.Equal(line.StartCoordinate.X, restoredLine.StartCoordinate.X);
        Assert.Equal(line.StartCoordinate.Y, restoredLine.StartCoordinate.Y);
        Assert.Equal(line.LineEndCoordinate.X, restoredLine.LineEndCoordinate.X);
        Assert.Equal(line.LineEndCoordinate.Y, restoredLine.LineEndCoordinate.Y);
        Assert.Equal(line.LineWidth, restoredLine.LineWidth);
        Assert.Equal(
            new TemplateRenderer().Render(template, null, options: Options()).Pages[0].Data,
            new TemplateRenderer().Render(restored, null, options: Options()).Pages[0].Data);
        Assert.Contains("L 3 4 30 4 2", Output(new TemplateRenderer().Render(restored, null, options: Options())));
    }

    [Fact]
    public void Line_extending_past_cell_width_is_rejected()
    {
        var template = Template(new BpLineElement
        {
            StartCoordinate = Point(90, 4),
            LineEndCoordinate = Point(110, 4),
            LineWidth = 1
        });

        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(template, null, options: Options()));
    }

    [Fact]
    public void Absolute_line_crossing_page_boundary_is_emitted_whole_on_next_page()
    {
        var line = new BpLineElement
        {
            StartCoordinate = Point(10, 90),
            LineEndCoordinate = Point(10, 110),
            LineWidth = 1
        };
        var template = new BpTemplate
        {
            Width = 100,
            Height = 100,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 100,
                    Cells = new List<BpCell>
                    {
                        new BpCell { Width = 100, Elements = new List<BpElement> { line } }
                    }
                }
            }
        };

        var result = new TemplateRenderer().Render(template, null, options: Options());
        Assert.Equal(2, result.PageCount);
        var firstPage = Encoding.UTF8.GetString(result.Pages[0].Data);
        var secondPage = Encoding.UTF8.GetString(result.Pages[1].Data);

        Assert.DoesNotContain("L 10 ", firstPage);
        Assert.Equal(1, secondPage.Split(new[] { "L 10 0 10 20 1" }, StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Line_can_render_in_reverse_direction()
    {
        var line = new BpLineElement
        {
            StartCoordinate = Point(30, 30),
            LineEndCoordinate = Point(10, 10),
            LineWidth = 1
        };

        var output = Output(new TemplateRenderer().Render(Template(line), null, options: Options()));

        Assert.Contains("L 30 30 10 10 1", output);
    }

    [Fact]
    public void Text_alignment_changes_the_emitted_x_coordinate()
    {
        var template = new BpTemplate
        {
            Width = 100,
            Height = 60,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 50,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 100,
                            Elements = new List<BpElement>
                            {
                                Text("L", BpTextAlignment.Left),
                                Text("C", BpTextAlignment.Center),
                                Text("R", BpTextAlignment.Right)
                            }
                        }
                    }
                }
            }
        };

        var output = Output(new TemplateRenderer().Render(template, null, options: Options()));

        Assert.Contains("T 24 0 10 0 L", output);
        Assert.Contains("T 24 0 44 0 C", output);
        Assert.Contains("T 24 0 78 0 R", output);
    }

    private static BpTemplate Template(params BpElement[] elements)
        => new BpTemplate
        {
            Width = 100,
            Height = 50,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 50,
                    Cells = new List<BpCell>
                    {
                        new BpCell { Width = 100, Elements = elements.ToList() }
                    }
                }
            }
        };

    private static BpFontElement Text(string value, BpTextAlignment alignment)
        => new BpFontElement
        {
            FontSize = 24,
            TextAlignment = alignment,
            Field = new BgField { Value = value },
            StartCoordinate = Point(10, 0),
            EndCoordinate = Point(90, 40)
        };

    private static bool Overlaps(BpElement first, BpElement second)
    {
        Assert.NotNull(first.StartCoordinate);
        Assert.NotNull(first.EndCoordinate);
        Assert.NotNull(second.StartCoordinate);
        Assert.NotNull(second.EndCoordinate);

        return first.StartCoordinate.X < second.EndCoordinate.X &&
            second.StartCoordinate.X < first.EndCoordinate.X &&
            first.StartCoordinate.Y < second.EndCoordinate.Y &&
            second.StartCoordinate.Y < first.EndCoordinate.Y;
    }

    private static IEnumerable<(int X, int Y, string Text)> TextCommands(string output)
    {
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("T ", StringComparison.Ordinal)) continue;
            var fields = line.Split(new[] { ' ' }, 6, StringSplitOptions.None);
            if (fields.Length == 6 && int.TryParse(fields[3], out var x) && int.TryParse(fields[4], out var y))
                yield return (x, y, fields[5]);
        }
    }

    private static BpCoordinate Point(int x, int y) => new BpCoordinate { X = x, Y = y };
}
