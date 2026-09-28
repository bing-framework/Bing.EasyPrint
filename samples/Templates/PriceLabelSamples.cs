using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;

internal static class PriceLabelSamples
{
    internal static void WriteTo(string output, TemplateLoader loader)
    {
        var data = new PriceLabelData
        {
            Name = "三全高端汤圆炫彩小汤圆混装口味黑芝麻、花生、紫薯、抹茶",
            Barcode = "092836193615302",
            Code = "GT-001",
            QrCode = "https://go.example/p/0001",
            OriginPrice = 300m,
            SalePrice = 250m,
            MemberPrice = 200.03m,
            Unit = "袋",
            Specification = "规格：250ml*16",
            Origin = "产地：浙江杭州",
            PromotionStart = new DateTime(2026, 9, 1),
            PromotionEnd = new DateTime(2026, 9, 30)
        };
        var renderer = new PriceLabelRenderer();

        foreach (var preset in new[] { PriceLabelPreset.Mm15, PriceLabelPreset.Mm70, PriceLabelPreset.Mm90 })
            Save("price-label-" + (int)preset + "mm", PriceLabelTemplateFactory.Create(preset), data);

        var pm0025Data = new PriceLabelData
        {
            Name = "万代（BANDAI）MG 高达2 拼装模型 180CM（拆封无售后）红龙异端",
            Barcode = "1208000780",
            Code = "1208000780",
            QrCode = null,
            OriginPrice = 10.99m,
            SalePrice = 8.89m,
            Unit = "袋",
            Specification = "132g/袋",
            Origin = "广东广州",
            PromotionStart = new DateTime(2026, 9, 1),
            PromotionEnd = new DateTime(2026, 9, 30)
        };
        Save("price-label-pm0025", CreatePm0025(), pm0025Data);

        var pm0034Data = new PriceLabelData
        {
            Name = "三全高端汤圆炫彩小汤圆混装口味黑芝麻、花生、紫薯、抹茶、麻薯、巧克力、豆沙",
            Barcode = "092836193615302",
            QrCode = "编码-二维码",
            OriginPrice = 300.02m,
            SalePrice = 250.01m,
            MemberPrice = 200.03m,
            Specification = "250ml*16",
            Origin = "中国"
        };
        Save("price-label-pm0034", CreatePm0034(), pm0034Data);

        void Save(string name, BpTemplate template, PriceLabelData label)
        {
            var json = loader.SaveJson(template);
            var fromCode = renderer.Render(template, label);
            var restoredTemplate = loader.LoadJson(json);
            var fromJson = new TemplateRenderer().Render(restoredTemplate, renderer.PrepareData(label));
            AssertSame(fromCode, fromJson, name);
            if (name == "price-label-pm0025" || name == "price-label-pm0034")
            {
                var checkedInJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name + ".json"));
                var checkedInTemplate = loader.LoadJson(checkedInJson);
                AssertSame(fromCode,
                    new TemplateRenderer().Render(checkedInTemplate, renderer.PrepareData(label)), name + " 静态 JSON");
                AssertConditionalBranches(checkedInTemplate, label, name);
            }
            AssertLegacyLayout(name, template, fromCode);
            File.WriteAllText(Path.Combine(output, name + ".json"), json);
            File.WriteAllBytes(Path.Combine(output, name + ".cpcl"), fromCode.Pages[0].Data);
        }

        static void AssertSame(TemplateRenderResult expected, TemplateRenderResult actual, string name)
        {
            if (expected.PageCount != actual.PageCount) throw new Exception(name + " 的页数不一致。");
            for (var i = 0; i < expected.PageCount; i++)
            {
                var left = expected.Pages[i];
                var right = actual.Pages[i];
                if (left.Width != right.Width || left.Height != right.Height || !left.Data.SequenceEqual(right.Data))
                    throw new Exception(name + " 的 C# 与 JSON 输出不一致。");
            }
        }

        static void AssertLegacyLayout(string name, BpTemplate template, TemplateRenderResult result)
        {
            if (name == "price-label-pm0025" && (template.Width != 671 || template.Height != 360))
                throw new Exception("PM0025 应为 671x360 点。");
            if (name == "price-label-pm0034" && (template.Width != 600 || template.Height != 320))
                throw new Exception("PM0034 应为 600x320 点。");
            if (name != "price-label-pm0025" && name != "price-label-pm0034") return;
            if (result.PageCount != 1 || result.Pages[0].Width != template.Width || result.Pages[0].Height != template.Height)
                throw new Exception(name + " 的实际输出页尺寸或页数不符。");

            var cpcl = Encoding.GetEncoding("gbk").GetString(result.Pages[0].Data);
            if (!cpcl.Contains("B 128 ") || !cpcl.Contains("监督电话:12358") ||
                (name == "price-label-pm0025" && !cpcl.Contains("2026.09.01 - 2026.09.30")))
                throw new Exception(name + " 未保留一维条码内容。");
            var titleCommand = name == "price-label-pm0034" ? "T 24 0 12 8" : "T 55 0 12 8";
            if (!cpcl.Contains(titleCommand))
                throw new Exception(name + " 未保留商品标题或监管电话内容。");

            if (name == "price-label-pm0025")
            {
                if (!cpcl.Contains("132g/袋") || !cpcl.Contains("广东广州") || !cpcl.Contains("10.99") ||
                    !cpcl.Contains("T 55 0 282 174 8") || !cpcl.Contains("T 55 0 324 184 .89") ||
                    !cpcl.Contains("L 12 189 132 189 1"))
                    throw new Exception("PM0025 的规格、产地、促销价格或划线未按样例输出。");
                var renderedText = ExtractText(cpcl);
                if (!renderedText.Contains("万代（BANDAI）MG 高达2 拼装模型 180CM（拆封无售后）红龙异端"))
                    throw new Exception("PM0025 未完整输出 ERP 商品名称。");
                if (cpcl.Contains("B QR ") || cpcl.Contains("会员价"))
                    throw new Exception("PM0025 旧样例没有二维码或会员价，重制样例不应输出。");
            }
            else
            {
                const string erpName = "三全高端汤圆炫彩小汤圆混装口味黑芝麻、花生、紫薯、抹茶、麻薯、巧克力、豆沙";
                var renderedText = ExtractText(cpcl);
                if (!cpcl.Contains("B QR 16 108") || !cpcl.Contains("250ml*16") || !cpcl.Contains("中国") ||
                    !cpcl.Contains("300.02") || !cpcl.Contains("250") || !cpcl.Contains(".01") ||
                    !cpcl.Contains("200.03") || !cpcl.Contains("会员价 / Member") ||
                    !cpcl.Contains("L 180 135 390 135 1") || !renderedText.Contains(erpName))
                    throw new Exception("PM0034 的二维码、商品字段、促销/会员价格或划线未按样例输出。");
            }
        }

        static string ExtractText(string cpcl)
        {
            var result = new StringBuilder();
            foreach (var line in cpcl.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.StartsWith("T ", StringComparison.Ordinal)) continue;
                var position = 0;
                for (var token = 0; token < 5; token++)
                {
                    position = line.IndexOf(' ', position);
                    if (position < 0) break;
                    position++;
                }
                if (position < line.Length) result.Append(line.Substring(position));
            }
            return result.ToString();
        }
    }

    private static void AssertConditionalBranches(BpTemplate template, PriceLabelData source, string name)
    {
        var cases = name == "price-label-pm0034"
            ? new[]
            {
                (Origin: 300m, Sale: 300m, Member: (decimal?)null, Promotion: false, HasMember: false),
                (Origin: 300.02m, Sale: 250.01m, Member: (decimal?)null, Promotion: true, HasMember: false),
                (Origin: 300.02m, Sale: 300.02m, Member: (decimal?)200.03m, Promotion: false, HasMember: true),
                (Origin: 300.02m, Sale: 250.01m, Member: (decimal?)200.03m, Promotion: true, HasMember: true)
            }
            : new[]
        {
            (Origin: 12.50m, Sale: 12.50m, Member: (decimal?)0m, Promotion: false, HasMember: false),
            (Origin: 12.50m, Sale: 9.75m, Member: (decimal?)0m, Promotion: true, HasMember: false),
            (Origin: 12.50m, Sale: 12.50m, Member: (decimal?)8.50m, Promotion: false, HasMember: true),
            (Origin: 12.50m, Sale: 9.75m, Member: (decimal?)8.50m, Promotion: true, HasMember: true)
        };

        foreach (var item in cases.Where(item => name != "price-label-pm0025" || !item.HasMember))
        {
            var data = new PriceLabelData
            {
                Name = source.Name,
                Barcode = source.Barcode,
                QrCode = source.QrCode,
                Code = source.Code,
                OriginPrice = item.Origin,
                SalePrice = item.Sale,
                MemberPrice = item.Member,
                Unit = source.Unit,
                Specification = source.Specification,
                Origin = source.Origin,
                PromotionStart = source.PromotionStart,
                PromotionEnd = source.PromotionEnd
            };
            var result = new PriceLabelRenderer().Render(template, data);
            if (result.PageCount != 1) throw new Exception(name + " 的价格分支输出页数不为 1。");

            var cpcl = Encoding.GetEncoding("gbk").GetString(result.Pages[0].Data);
            var originPriceVisible = ContainsYenAmount(cpcl, item.Origin.ToString("F2", CultureInfo.InvariantCulture));
            var promotionVisible = cpcl.Contains("促销价 / Sale");
            var regularPriceVisible = cpcl.Contains("零售价 / Price");
            var memberVisible = cpcl.Contains("会员价 / Member") && item.Member.HasValue &&
                ContainsYenAmount(cpcl, item.Member.Value.ToString("F2", CultureInfo.InvariantCulture));
            var saleParts = item.Sale.ToString("F2", CultureInfo.InvariantCulture).Split('.');
            var strikeLine = name == "price-label-pm0025"
                ? "L 12 189 132 189 1"
                : "L 180 135 390 135 1";
            if (originPriceVisible != item.Promotion || promotionVisible != item.Promotion ||
                regularPriceVisible == item.Promotion || memberVisible != item.HasMember ||
                cpcl.Contains(strikeLine) != item.Promotion ||
                !HasTextCommand(cpcl, saleParts[0]) || !HasTextCommand(cpcl, "." + saleParts[1]))
                throw new Exception(name + " 的价格分支输出不符：promotion=" + item.Promotion +
                    ", member=" + item.HasMember + ", origin=" + originPriceVisible +
                    ", promotion label=" + promotionVisible + ", regular label=" + regularPriceVisible +
                    ", member price=" + memberVisible + ", strike=" + cpcl.Contains(strikeLine) + ".");
        }
    }

    private static bool ContainsYenAmount(string cpcl, string amount)
        => cpcl.Contains("¥" + amount) || cpcl.Contains("￥" + amount);

    private static bool HasTextCommand(string cpcl, string value)
        => cpcl.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.StartsWith("T ", StringComparison.Ordinal) &&
                line.EndsWith(" " + value, StringComparison.Ordinal));

    private static BpTemplate CreatePm0025()
    {
        var promotion = Condition("IsPromotion", true);
        var elements = new List<BpElement>
        {
            Text("Name", 12, 8, 490, 76, 32, bold: true),
            Text(null, 12, 86, 300, 22, 16, "监督电话:12358"),
            Text(null, 12, 112, 46, 24, 16, "规格:"),
            Text("Specification", 60, 112, 300, 24, 16),
            Text(null, 12, 138, 46, 24, 16, "产地:"),
            Text("Origin", 60, 138, 300, 24, 16),
            Text("OriginPriceDisplay", 12, 168, 120, 22, 16, condition: promotion),
            new BpLineElement { StartCoordinate = Point(12, 189), LineEndCoordinate = Point(132, 189), LineWidth = 1, Condition = promotion },
            Text(null, 12, 196, 120, 26, 16, "零售价 / Price", condition: Condition("IsPromotion", false)),
            Text(null, 12, 196, 120, 26, 16, "促销价 / Sale", condition: promotion),
            Text("CurrencySymbol", 138, 192, 32, 34, 24),
            Text("SalePriceInteger", 172, 174, 150, 88, 80, alignment: BpTextAlignment.Right),
            Text("SalePriceFraction", 324, 184, 82, 44, 32),
            Text("Unit", 324, 230, 82, 26, 16),
            Text("PromotionPeriod", 12, 274, 430, 24, 16, condition: NotNull("PromotionPeriod")),
            Barcode(12, 310, 280, 32),
            Text("Code", 308, 310, 210, 26, 16),
            QrCode(520, 20, 135, 135, 4)
        };
        return Template(671, 360, elements);
    }

    private static BpTemplate CreatePm0034()
    {
        var promotion = Condition("IsPromotion", true);
        var hasMemberPrice = Condition("HasMemberPrice", true);
        var elements = new List<BpElement>
        {
            Text("Name", 12, 8, 570, 76, 24, bold: true),
            Text(null, 12, 82, 300, 22, 16, "监督电话:12358"),
            QrCode(0, 92, 135, 135, 4),
            Text("OriginPriceDisplay", 180, 112, 210, 24, 16, condition: promotion),
            new BpLineElement { StartCoordinate = Point(180, 135), LineEndCoordinate = Point(390, 135), LineWidth = 1, Condition = promotion },
            Text(null, 180, 142, 130, 24, 16, "零售价 / Price", condition: Condition("IsPromotion", false)),
            Text(null, 180, 142, 130, 24, 16, "促销价 / Sale", condition: promotion),
            Text("CurrencySymbol", 180, 184, 30, 30, 24),
            Text("SalePriceInteger", 212, 154, 136, 72, 64, alignment: BpTextAlignment.Right),
            Text("SalePriceFraction", 350, 164, 60, 38, 24),
            Text("Unit", 350, 204, 60, 22, 16),
            Text(null, 420, 154, 168, 24, 16, "会员价 / Member", condition: hasMemberPrice),
            Text("MemberPriceDisplay", 420, 180, 168, 36, 24, bold: true, condition: hasMemberPrice),
            Text("Specification", 180, 232, 400, 22, 16),
            Text("Origin", 180, 256, 400, 22, 16),
            Barcode(12, 270, 460, 28),
            Text("Code", 500, 270, 88, 24, 16),
            Text("PromotionPeriod", 12, 300, 390, 18, 16, condition: NotNull("PromotionPeriod"))
        };
        return Template(600, 320, elements);
    }

    private static BpTemplate Template(int width, int height, List<BpElement> elements)
        => new BpTemplate
        {
            Version = 1,
            Width = width,
            Height = height,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = height,
                    Cells = new List<BpCell> { new BpCell { Width = width, Elements = elements } }
                }
            }
        };

    private static BpElement Text(string path, int x, int y, int width, int height, int fontSize,
        string value = null, BpCondition condition = null, bool bold = false,
        BpTextAlignment alignment = BpTextAlignment.Left)
        => new BpFontElement
        {
            FontSize = fontSize,
            Bold = bold,
            TextAlignment = alignment,
            Field = path == null ? new BgField { Value = value } : new BgField { Name = path },
            StartCoordinate = Point(x, y),
            EndCoordinate = Point(x + width, y + height),
            Condition = condition
        };

    private static BpElement Barcode(int x, int y, int width, int height)
        => new BpBarcodeElement
        {
            BarcodeType = BarcodeType.Code128,
            LineWidth = 1,
            Ratio = 2,
            Height = height,
            Width = width,
            Field = new BgField { Name = "Barcode" },
            StartCoordinate = Point(x, y),
            EndCoordinate = Point(x + width, y + height),
            Condition = NotNull("Barcode")
        };

    private static BpElement QrCode(int x, int y, int width, int height, int unitWidth)
        => new BpQrCodeElement
        {
            UnitWidth = unitWidth,
            ErrorLevel = "L",
            Width = width,
            Height = height,
            Field = new BgField { Name = "QrCode" },
            StartCoordinate = Point(x, y),
            EndCoordinate = Point(x + width, y + height),
            Condition = NotNull("QrCode")
        };

    private static BpCondition Condition(string path, bool value)
        => new BpCondition { Path = path, Operator = "eq", Value = value };

    private static BpCondition NotNull(string path)
        => new BpCondition { Path = path, Operator = "notNull" };

    private static BpCoordinate Point(int x, int y) => new BpCoordinate { X = x, Y = y };
}
