namespace Bing.EasyPrint.Tests.Biz.Label;

/// <summary>
/// 打印价格标签(70mm)
/// </summary>
// ReSharper disable once InconsistentNaming
public class PrintPriceLabelBy70mm : PrintPriceLabelBase
{
    /// <summary>
    /// 初始化一个<see cref="PrintPriceLabelBase"/>类型的实例
    /// </summary>
    public PrintPriceLabelBy70mm(IEasyPrint easyPrint) : base(easyPrint)
    {
    }

    /// <summary>
    /// 构建
    /// </summary>
    /// <param name="label">价格标签</param>
    public override IBufferWriter Build(PriceLabel label)
    {
        var printer = EasyPrint.CreateCPCLCommand();

        #region 设置页面定位信息

        const int xMargin = 30, yMargin = 30;
        const int pageWidth = 540, pageHeight = 300;
        // 设置打印页
        printer.SetPage(pageWidth, pageHeight);

        #endregion

        var name = label.Name.Length > 40 ? $"{label.Name.Substring(0, 38)}..." : label.Name;
        // 打印商品名称
        printer.DrawTextArea(30 - xMargin, 49 - yMargin, 480, 66, name, FontSize.Size24, RotationAngle.None, TextStyle.None);

        // 打印价格
        if (label.OriginPrice == label.Price)
        {
            // 打印正价
            printer.BilingualLabel(30 - xMargin, 112 - yMargin, "零售价：", "Price", 5, zhCnFontSize: FontSize.Size16);
            printer.GoodsPriceLabel(78 - xMargin, 186 - yMargin, label.Price, label.Unit);
        }
        else
        {
            // 打印促销价
            var originalPriceStr = $"零售价：{label.OriginPrice:F2}";
            var width = ((originalPriceStr.Length + 3) * 16) / 2;
            printer.DrawLine(30 - xMargin, 106 - yMargin + 8, 30 - xMargin + width, 106 - yMargin + 8);
            printer.BilingualLabel(30 - xMargin, 106 - yMargin, originalPriceStr, "Price", 5, zhCnFontSize: FontSize.Size16);
            printer.BilingualLabel(30 - xMargin, 150 - yMargin, $"促销价：", "Sale", 5, zhCnFontSize: FontSize.Size16);
            printer.GoodsPriceLabel(98 - xMargin, 195 - yMargin, label.Price, label.Unit);
        }

        // 辅助属性
        printer.BilingualLabel(30 - xMargin, 194 - yMargin, $"规格属性：{label.AttributeName}", "SPEC", 4);

        ////条码
        //Printer.BilingualLabel(30 - xMargin, 244 - yMargin, $"条码：{label.Barcode}", "Barcode", 4);

        // 监管电话
        printer.BilingualLabel(30 - xMargin, 244 - yMargin, $"商品条码：{label.Barcode}", "监管电话：12358", 4);

        // 二维码
        printer.DrawQrCode(390 - xMargin, 109 - yMargin, label.QrCode, QrCodeUnitSize.Size6, QrCodeCorrectionLevel.L, RotationAngle.None);

        //// 监管电话
        //Printer.BilingualLabel(358 - xMargin, 243 - yMargin, $"监管电话：12358", "Complaints Hotline", 4);

        return printer.Build();
    }
}