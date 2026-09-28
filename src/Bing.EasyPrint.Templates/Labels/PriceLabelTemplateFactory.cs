using System;
using System.Collections.Generic;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 创建可编辑的预置价签模板。
/// </summary>
public static class PriceLabelTemplateFactory
{
    /// <summary>
    /// 创建指定规格的价签模板。
    /// </summary>
    /// <param name="preset">15、70 或 90 毫米预置价签规格。</param>
    /// <returns>可继续编辑或保存为 JSON 的通用模板。</returns>
    public static BpTemplate Create(PriceLabelPreset preset)
    {
        switch (preset)
        {
            case PriceLabelPreset.Mm15: return Create15Mm();
            case PriceLabelPreset.Mm70: return Create70Mm();
            case PriceLabelPreset.Mm90: return Create90Mm();
            default: throw new ArgumentOutOfRangeException(nameof(preset), preset, "不支持的价签纸张规格。");
        }
    }

    /// <summary>
    /// 创建以二维码为主的 15 毫米价签。
    /// </summary>
    /// <returns>15 毫米预置模板。</returns>
    private static BpTemplate Create15Mm()
    {
        var elements = new List<BpElement>
        {
            Text(null, 16, 4, 127, 28, 24, "扫码购买"),
            new BpQrCodeElement
            {
                UnitWidth = 3,
                ErrorLevel = "L",
                Width = 115,
                Height = 115,
                Field = Field("QrCode"),
                StartCoordinate = Point(22, 36),
                EndCoordinate = Point(137, 151),
                Condition = NotNull("QrCode")
            }
        };
        return Template(159, 159, elements);
    }

    /// <summary>
    /// 创建包含促销和会员信息的 70 毫米价签。
    /// </summary>
    /// <returns>70 毫米预置模板。</returns>
    private static BpTemplate Create70Mm()
    {
        var elements = new List<BpElement>
        {
            Text("Name", 12, 12, 350, 66, 24, bold: true),
            Text("OriginPriceDisplay", 12, 80, 225, 30, 16, condition: IsPromotion()),
            new BpLineElement { StartCoordinate = Point(12, 108), LineEndCoordinate = Point(237, 108), LineWidth = 1, Condition = IsPromotion() }
        };
        AddBilingual(elements, 12, 80, "零售价：", "Price", NotPromotion());
        AddBilingual(elements, 12, 112, "促销价：", "Sale", IsPromotion());
        elements.AddRange(PriceGroup(12, 132, "SalePriceInteger", "SalePriceFraction", true));
        elements.AddRange(new BpElement[]
        {
            Text("Specification", 255, 158, 130, 34, 16),
            Text("Origin", 255, 194, 130, 26, 16),
            Text("PromotionPeriod", 12, 230, 230, 28, 16, condition: NotNull("PromotionPeriod")),
            Text(null, 255, 222, 267, 18, 16, "监管电话：12358"),
            Text("BarcodeDisplay", 255, 241, 267, 18, 16, condition: NotNull("BarcodeDisplay")),
            Barcode(12, 263, 460, 32),
            QrCode(390, 18, 132, 132, 3)
        });
        AddMemberPrice(elements, 390, 158, 132);
        return Template(540, 300, elements);
    }

    /// <summary>
    /// 创建包含促销和会员信息的 90 毫米价签。
    /// </summary>
    /// <returns>90 毫米预置模板。</returns>
    private static BpTemplate Create90Mm()
    {
        var elements = new List<BpElement>
        {
            Text("Name", 12, 12, 480, 76, 32, bold: true),
            Text("OriginPriceDisplay", 12, 90, 225, 28, 16, condition: IsPromotion()),
            new BpLineElement { StartCoordinate = Point(12, 116), LineEndCoordinate = Point(237, 116), LineWidth = 1, Condition = IsPromotion() }
        };
        AddBilingual(elements, 12, 92, "零售价", "Price", NotPromotion());
        AddBilingual(elements, 12, 120, "促销价", "Sale", IsPromotion());
        elements.AddRange(PriceGroup(12, 154, "SalePriceInteger", "SalePriceFraction"));
        elements.AddRange(new BpElement[]
        {
            Text("Specification", 276, 176, 225, 42, 24),
            Text("Origin", 276, 220, 225, 28, 16),
            Text("PromotionPeriod", 12, 250, 250, 26, 16, condition: NotNull("PromotionPeriod")),
            Text(null, 276, 250, 225, 16, 16, "监管电话：12358"),
            Text("BarcodeDisplay", 276, 266, 225, 16, 16, condition: NotNull("BarcodeDisplay")),
            Barcode(12, 282, 460, 32),
            Text("Code", 490, 286, 160, 26, 16),
            QrCode(520, 20, 135, 135, 4)
        });
        AddMemberPrice(elements, 520, 176, 135);
        return Template(671, 320, elements);
    }

    /// <summary>
    /// 添加仅在会员价有效时显示的价格区域。
    /// </summary>
    /// <param name="elements">接收价签元素的集合。</param>
    /// <param name="x">区域水平打印点坐标。</param>
    /// <param name="y">区域垂直打印点坐标。</param>
    /// <param name="width">区域宽度，单位为打印点。</param>
    private static void AddMemberPrice(List<BpElement> elements, int x, int y, int width)
    {
        var condition = new BpCondition { Path = "HasMemberPrice", Operator = "eq", Value = true };
        elements.Add(Text(null, x, y, width, 26, 16, "会员价 / Member", condition));
        elements.Add(Text("MemberPriceDisplay", x, y + 26, width, 32, 24, bold: true, condition: condition));
    }

    /// <summary>
    /// 添加受同一条件控制的中英文双行标题。
    /// </summary>
    /// <param name="elements">接收文字元素的集合。</param>
    /// <param name="x">标题水平打印点坐标。</param>
    /// <param name="y">标题垂直打印点坐标。</param>
    /// <param name="chinese">中文标题。</param>
    /// <param name="english">英文标题。</param>
    /// <param name="condition">可选的显示条件。</param>
    private static void AddBilingual(List<BpElement> elements, int x, int y, string chinese, string english, BpCondition condition)
    {
        elements.Add(Text(null, x, y, 140, 26, 24, chinese, condition));
        elements.Add(Text(null, x, y + 23, 140, 24, 16, english, condition));
    }

    /// <summary>
    /// 创建币符、整数、小数及单位组成的价格区域。
    /// </summary>
    /// <param name="x">区域水平打印点坐标。</param>
    /// <param name="y">区域垂直打印点坐标。</param>
    /// <param name="integerPath">价格整数部分的字段路径。</param>
    /// <param name="fractionPath">价格小数部分的字段路径。</param>
    /// <param name="compact">是否使用紧凑排版。</param>
    /// <returns>按排版顺序排列的价格元素。</returns>
    private static BpElement[] PriceGroup(int x, int y, string integerPath, string fractionPath, bool compact = false)
    {
        var integerWidth = compact ? 140 : 175;
        var integerSize = compact ? 64 : 80;
        var fractionX = compact ? x + 180 : x + 219;
        var unitX = compact ? x + 184 : x + 223;
        return new BpElement[]
        {
            Text("CurrencySymbol", x, y + 20, 36, 44, 32),
            Text(integerPath, x + 38, y, integerWidth, integerSize + 8, integerSize, alignment: BpTextAlignment.Right),
            Text(fractionPath, fractionX, y + 7, 60, 44, 32),
            Text("Unit", unitX, y + 50, 58, 32, 16)
        };
    }

    /// <summary>
    /// 创建在有条码数据时显示的 Code 128 元素。
    /// </summary>
    /// <param name="x">水平打印点坐标。</param>
    /// <param name="y">垂直打印点坐标。</param>
    /// <param name="width">可用宽度，单位为打印点。</param>
    /// <param name="height">条码高度，单位为打印点。</param>
    /// <returns>条码元素。</returns>
    private static BpElement Barcode(int x, int y, int width, int height)
        => new BpBarcodeElement
        {
            BarcodeType = BarcodeType.Code128,
            LineWidth = 1,
            Ratio = 2,
            Height = height,
            Width = width,
            Field = Field("Barcode"),
            StartCoordinate = Point(x, y),
            EndCoordinate = Point(x + width, y + height),
            Condition = NotNull("Barcode")
        };

    /// <summary>
    /// 创建在有二维码数据时显示的元素。
    /// </summary>
    /// <param name="x">水平打印点坐标。</param>
    /// <param name="y">垂直打印点坐标。</param>
    /// <param name="width">可用宽度，单位为打印点。</param>
    /// <param name="height">可用高度，单位为打印点。</param>
    /// <param name="unitWidth">每个二维码模块的打印点宽度。</param>
    /// <returns>二维码元素。</returns>
    private static BpElement QrCode(int x, int y, int width, int height, int unitWidth)
        => new BpQrCodeElement
        {
            UnitWidth = unitWidth,
            ErrorLevel = "L",
            Width = width,
            Height = height,
            Field = Field("QrCode"),
            StartCoordinate = Point(x, y),
            EndCoordinate = Point(x + width, y + height),
            Condition = NotNull("QrCode")
        };

    /// <summary>
    /// 创建绑定字段或静态值的文字元素。
    /// </summary>
    /// <param name="path">字段路径；为 null 时使用静态值。</param>
    /// <param name="x">水平打印点坐标。</param>
    /// <param name="y">垂直打印点坐标。</param>
    /// <param name="width">文字区域宽度，单位为打印点。</param>
    /// <param name="height">文字区域高度，单位为打印点。</param>
    /// <param name="fontSize">内置字体大小。</param>
    /// <param name="value">可选的静态文字。</param>
    /// <param name="condition">可选的显示条件。</param>
    /// <param name="bold">是否加粗。</param>
    /// <param name="alignment">水平对齐方式。</param>
    /// <returns>文字元素。</returns>
    private static BpElement Text(string path, int x, int y, int width, int height, int fontSize,
        string value = null, BpCondition condition = null, bool bold = false,
        BpTextAlignment alignment = BpTextAlignment.Left)
        => new BpFontElement
        {
            FontSize = fontSize,
            Bold = bold,
            TextAlignment = alignment,
            Field = path == null ? new BgField { Value = value } : Field(path),
            StartCoordinate = Point(x, y),
            EndCoordinate = Point(x + width, y + height),
            Condition = condition
        };

    /// <summary>
    /// 将价签元素放入单行单元格模板。
    /// </summary>
    /// <param name="width">标签宽度，单位为打印点。</param>
    /// <param name="height">标签高度，单位为打印点。</param>
    /// <param name="elements">标签内容元素。</param>
    /// <returns>版本 1 的通用模板。</returns>
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
                    Cells = new List<BpCell> { new BpCell { Elements = elements } }
                }
            }
        };

    /// <summary>
    /// 创建引用价签显示字段的绑定。
    /// </summary>
    /// <param name="path">价签显示字段路径。</param>
    /// <returns>字段绑定定义。</returns>
    private static BgField Field(string path) => new BgField { Name = path };
    /// <summary>
    /// 创建以打印点表示的二维坐标。
    /// </summary>
    /// <param name="x">水平坐标。</param>
    /// <param name="y">垂直坐标。</param>
    /// <returns>打印点坐标。</returns>
    private static BpCoordinate Point(int x, int y) => new BpCoordinate { X = x, Y = y };
    /// <summary>
    /// 创建字段值不为空的显示条件。
    /// </summary>
    /// <param name="path">待判断字段路径。</param>
    /// <returns>非空条件。</returns>
    private static BpCondition NotNull(string path) => new BpCondition { Path = path, Operator = "notNull" };
    /// <summary>
    /// 创建促销状态成立的显示条件。
    /// </summary>
    /// <returns>促销条件。</returns>
    private static BpCondition IsPromotion() => new BpCondition { Path = "IsPromotion", Operator = "eq", Value = true };
    /// <summary>
    /// 创建非促销状态的显示条件。
    /// </summary>
    /// <returns>非促销条件。</returns>
    private static BpCondition NotPromotion() => new BpCondition { Path = "IsPromotion", Operator = "eq", Value = false };
}
