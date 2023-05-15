namespace Bing.EasyPrint.Tests.Biz.Label;

/// <summary>
/// 打印价格标签(90mm)
/// </summary>
// ReSharper disable once InconsistentNaming
public class PrintPriceLabelBy90mm : PrintPriceLabelBase
{
    /// <summary>
    /// 初始化一个<see cref="PrintPriceLabelBase"/>类型的实例
    /// </summary>
    public PrintPriceLabelBy90mm(IEasyPrint easyPrint) : base(easyPrint)
    {
    }

    /// <summary>
    /// 构建
    /// </summary>
    /// <param name="label">价格标签</param>
    public override IBufferWriter Build(PriceLabel label)
    {
        var printer = EasyPrint.CreateCPCLCommand();
        // 203 dpi
        // width: 90mm, height: 37mm
        // width: 719px, height: 296px
        #region 设置页面定位信息

        const int xStartMargin = 0;
        const int xStartMarginColumn2 = 230;
        const int xMargin = 30, yMargin = 30;
        const int pageWidth = 671, pageHeight = 320;
        // 设置打印页
        printer.SetPage(pageWidth, pageHeight);

        #endregion

        var name = label.Name.Length > 40 ? $"{label.Name.Substring(0, 38)}..." : label.Name;
        // 打印商品名称
        printer.DrawTextArea(xStartMargin, 49 - yMargin, 623, 92, name, FontSize.Size32, RotationAngle.None, TextStyle.Bold);

        // 打印价格
        if (label.OriginPrice == label.Price)
        {
            // 打印正价
            printer.DrawText(xStartMargin, 132 - yMargin, "零售价/Price", FontSize.Size24);
            printer.GoodsPriceLabelV2(xStartMargin, 280 - yMargin, label.Price, label.Unit);
        }
        else
        {
            // 打印促销价
            var originalPriceStr = $"零售价/Price：{label.OriginPrice:F2}";
            var width = ((originalPriceStr.Length + 3) * 24) / 2;
            printer.DrawLine(xStartMargin, 122 - yMargin + 8, 30 - xMargin + width, 122 - yMargin + 8);
            printer.DrawText(xStartMargin, 122 - yMargin, originalPriceStr, FontSize.Size24);
            printer.DrawText(xStartMargin, 150 - yMargin, "促销价/Sale", FontSize.Size24);
            printer.GoodsPriceLabelV2(xStartMargin, 280 - yMargin, label.Price, label.Unit);
        }

        // 监管电话 
        printer.DrawText(xStartMarginColumn2, 270 - yMargin, "监管电话：12358", FontSize.Size24);

        // 规格属性
        if (label.OriginPrice == label.Price)
        {
            printer.DrawText(xStartMarginColumn2, 132 - yMargin, "规格属性/SPEC", FontSize.Size24);
            printer.DrawTextArea(xStartMarginColumn2 + 5, 165 - yMargin, 270, 130, label.AttributeName, FontSize.Size24, RotationAngle.None, TextStyle.None);
        }
        else
        {
            printer.DrawText(xStartMarginColumn2, 146 - yMargin, "规格属性/SPEC", FontSize.Size24);
            printer.DrawTextArea(xStartMarginColumn2 + 5, 179 - yMargin, 270, 130, label.AttributeName, FontSize.Size24,RotationAngle.None, TextStyle.None);
        }

        // 商品条码
        printer.DrawText(xStartMarginColumn2, 235 - yMargin, $"商品条码：{label.Barcode}", FontSize.Size24);

        // 二维码
        printer.DrawQrCode(555 - xMargin, 135 - yMargin, label.QrCode, QrCodeUnitSize.Size5, QrCodeCorrectionLevel.L, RotationAngle.None);

        return printer.Build();
    }
}