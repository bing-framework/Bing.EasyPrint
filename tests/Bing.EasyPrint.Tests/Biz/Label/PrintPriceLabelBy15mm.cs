namespace Bing.EasyPrint.Tests.Biz.Label;

/// <summary>
/// 打印价格标签(15mm)
/// </summary>
public class PrintPriceLabelBy15mm : PrintPriceLabelBase
{
    /// <summary>
    /// 初始化一个<see cref="PrintPriceLabelBase"/>类型的实例
    /// </summary>
    public PrintPriceLabelBy15mm(IEasyPrint easyPrint) : base(easyPrint)
    {
    }

    /// <summary>
    /// 构建
    /// </summary>
    /// <param name="label">价格标签</param>
    public override IBufferWriter Build(PriceLabel label)
    {
        //宽 8px=1mm
        var command = EasyPrint.CreateCPCLCommand();

        #region 设置页面定位信息

        const int xMargin = 0, yMargin = 0;
        const int pageWidth = 142, pageHeight = 142;
        // 设置打印页
        command.SetPage(pageWidth, pageHeight);

        // 抬头
        command.DrawTextArea(xMargin + 2, yMargin, 120, 24, "扫码购买", FontSize.Size24, RotationAngle.None, TextStyle.None);
        // 二维码
        var qrCodeUnitSize = label.QrCode.Length > 52 ? QrCodeUnitSize.Size3 : QrCodeUnitSize.Size4;
        command.DrawQrCode(xMargin, yMargin + 28, label.QrCode, qrCodeUnitSize, QrCodeCorrectionLevel.L, RotationAngle.None);

        #endregion

        return command.Build();
    }
}