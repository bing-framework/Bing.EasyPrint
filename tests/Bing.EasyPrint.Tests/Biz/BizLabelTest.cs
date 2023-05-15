using System;
using System.Collections.Generic;
using System.Linq;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Tests.Biz.Label;
using SixLabors.ImageSharp;
using Xunit;
using Xunit.Abstractions;

namespace Bing.EasyPrint.Tests.Biz;

/// <summary>
/// 业务标签测试
/// </summary>
public class BizLabelTest : TestBase
{
    /// <summary>
    /// 简单打印
    /// </summary>
    protected IEasyPrint EasyPrint { get; set; }

    /// <summary>
    /// CPCL 打印命令
    /// </summary>
    protected CPCLPrintCommand Command { get; set; }

    /// <summary>
    /// 初始化一个<see cref="BizLabelTest"/>类型的实例
    /// </summary>
    /// <param name="output">输出</param>
    public BizLabelTest(ITestOutputHelper output) : base(output)
    {
        EasyPrint = new DefaultEasyPrint();
        Command = EasyPrint.CreateCPCLCommand();
    }

    /// <summary>
    /// 构建
    /// </summary>
    private void Build()
    {
        var result = Command.Build();
        Output.WriteLine("----------------------------- 调试命令 ---------------------------------------");
        Output.WriteLine(result.ToString());
        Output.WriteLine("----------------------------- 调试命令-十六进制 ---------------------------------------");
        Output.WriteLine(result.ToHex());
        Output.WriteLine("----------------------------- 执行命令 ---------------------------------------");
        Print(result.GetBytes());
    }

    /// <summary>
    /// 构建
    /// </summary>
    private void Build(IBufferWriter writer)
    {
        Output.WriteLine("----------------------------- 调试命令 ---------------------------------------");
        Output.WriteLine(writer.ToString());
        Output.WriteLine("----------------------------- 调试命令-十六进制 ---------------------------------------");
        Output.WriteLine(writer.ToHex());
        Output.WriteLine("----------------------------- 执行命令 ---------------------------------------");
        Print(writer.GetBytes());
    }

    /// <summary>
    /// 测试 - 商品标签
    /// </summary>
    [Fact]
    public void Test_GoodsLabel()
    {
        BuildGoodsLabel("美国阿拉巴利桑那州缓存地铁站进口毛豆仁 200g/盒",
            "200g",
            "瓶",
            "2019-11-04",
            3,
            "9999999991",
            "隔壁老王的二维码");
        Build();
    }

    /// <summary>
    /// 构建商品标签
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="specification">规格</param>
    /// <param name="unit">单位</param>
    /// <param name="packingDate">打包日期</param>
    /// <param name="shelfLife">保质期</param>
    /// <param name="barcode">条形码</param>
    /// <param name="qrCode">二维码</param>
    private void BuildGoodsLabel(string title, string specification, string unit, string packingDate, int shelfLife,
        string barcode, string qrCode)
    {
        var leftMargin = 0;
        Command.SetPage(560, 420)
            .DrawTextArea(leftMargin, 24, 550, 60, title, FontSize.Size32, RotationAngle.None, TextStyle.Bold)
            .DrawText(leftMargin, 130, $"规格：{specification}", FontSize.Size24)
            .DrawText(leftMargin, 170, $"计价单位：{unit}", FontSize.Size24)
            .DrawText(leftMargin, 210, $"包装日期：{packingDate}", FontSize.Size24)
            .DrawText(leftMargin, 250, $"保质期：{shelfLife}天", FontSize.Size24)
            .DrawText(leftMargin, 290, $"条码：{barcode}", FontSize.Size24)
            .DrawQrCode(358, 160, qrCode, QrCodeUnitSize.Size8, QrCodeCorrectionLevel.L, RotationAngle.None);
    }

    /// <summary>
    /// 测试 - 价格标签
    /// </summary>
    [Fact]
    public void Test_PriceLabel()
    {
        BuildPriceLabel("美国阿拉巴利桑那州缓存地铁站进口毛豆仁 200g/盒",
            null,
            800.00m,
            "200g",
            "盒",
            "9999999991",
            "4322214847");
        Build();
        BuildPriceLabel("非洲阿拉巴利桑那州缓存地铁站进口毛豆仁 200g/盒",
            12663.00m,
            80.00m,
            "200g",
            "盒",
            "9999999991",
            "4322214847隔壁老王的神兽");
        Build();
    }

    /// <summary>
    /// 构建价格标签
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="originalPrice">原始价格</param>
    /// <param name="actualPrice">实际价格</param>
    /// <param name="specification">分隔符</param>
    /// <param name="unit">单位</param>
    /// <param name="barcode">条形码</param>
    /// <param name="qrCode">二维码</param>
    private void BuildPriceLabel(string title, decimal? originalPrice, decimal actualPrice, string specification,
        string unit, string barcode, string qrCode)
    {
        var xMargin = 30;
        var yMargin = 30;
        // 设置打印页
        Command.SetPage(540, 300);
        // 打印标题
        Command.DrawTextArea(30 - xMargin, 49 - yMargin, 480, 66, title, FontSize.Size24, RotationAngle.None,
            TextStyle.None);
        if (originalPrice == null)
        {
            // 打印正价
            Command.BilingualLabel(30 - xMargin, 116 - yMargin, "售价：", "Price", 6, zhCnFontSize: FontSize.Size16);
            Command.GoodsPriceLabel(78 - xMargin, 190 - yMargin, actualPrice, unit);
        }
        else
        {
            // 打印特价
            var originalPriceStr = $"原价：{originalPrice.Value:F}";
            var width = ((originalPriceStr.Length + 3) * 16) / 2;
            Command.DrawLine(30 - xMargin, 110 - yMargin + 8, 30 - xMargin + width, 110 - yMargin + 8);
            Command.BilingualLabel(30 - xMargin, 110 - yMargin, originalPriceStr, "Price", 6,
                zhCnFontSize: FontSize.Size16);
            Command.BilingualLabel(30 - xMargin, 154 - yMargin, $"优惠价：", "On sale", 6,
                zhCnFontSize: FontSize.Size16);
            Command.GoodsPriceLabel(98 - xMargin, 199 - yMargin, actualPrice, unit);
        }

        // 辅助属性
        Command.BilingualLabel(30 - xMargin, 200 - yMargin, $"规格：{specification}", "SPEC", 6);

        //条码
        Command.BilingualLabel(30 - xMargin, 252 - yMargin, $"条码：{barcode}", "Barcode", 6);

        // 二维码
        Command.DrawQrCode(390 - xMargin, 115 - yMargin, qrCode, QrCodeUnitSize.Size6, QrCodeCorrectionLevel.L,
            RotationAngle.None);

        // 监管电话
        Command.BilingualLabel(358 - xMargin, 251 - yMargin, $"监管电话：12358", "Complaints Hotline", 6);
    }

    [Fact]
    public void Test_PriceLabelBy15mm()
    {
        var result = PrintPriceLabelBase.Create(EasyPrint, PrintPageType.Page15mm).Build(GetPriceLabel());
        Build(result);
    }

    [Fact]
    public void Test_PriceLabelBy70mm()
    {
        var result = PrintPriceLabelBase.Create(EasyPrint, PrintPageType.Page70mm).Build(GetPriceLabel());
        Build(result);
    }

    [Fact]
    public void Test_PriceLabelBy90mm()
    {
        var result = PrintPriceLabelBase.Create(EasyPrint, PrintPageType.Page70mm).Build(GetPriceLabel());
        Build(result);
    }

    /// <summary>
    /// 获取价格标签
    /// </summary>
    /// <returns></returns>
    private PriceLabel GetPriceLabel()
    {
        return new PriceLabel
        {
            Name = "非洲阿拉巴利桑那州缓存地铁站进口毛豆仁 200g/盒",
            OriginPrice = 100.01m,
            Price = 100.01m,
            Barcode = "9999999991",
            QrCode = "4322214847隔壁老王的神兽",
            AttributeName = "200g",
            Unit = "盒",
        };
    }

    /// <summary>
    /// 测试打印配送面单
    /// </summary>
    [Fact]
    public void Test_PriceShippingLabel()
    {
        var totalPage = 2;
        for (var i = 1; i <= totalPage; i++)
        {
            BuildShippingLabel(GetShippingLabel(), i, totalPage);
            Build();
        }
    }

    /// <summary>
    /// 测试打印配送面单
    /// </summary>
    [Fact]
    public void Test_PriceShippingLabel_1()
    {
        var totalPage = 2;
        BuildShippingLabel(GetShippingLabel(), 1, totalPage);
        Build();
    }

    /// <summary>
    /// 构建配送面单
    /// </summary>`
    /// <param name="data"></param>
    /// <param name="currentPage"></param>
    /// <param name="totalPage"></param>
    private void BuildShippingLabel(ShippingLabel data, int currentPage, int totalPage)
    {
        var xMargin = 15;
        var yMargin = 20;
        var y = 0;

        var pageWidth = 580;
        var pageHeight = 860;
        var fontSize1 = (int) FontSize.Size24;
        var fontSize2 = (int) FontSize.Size32;

        // 设置打印页
        Command.SetPage(pageWidth, pageHeight);

        // 打印长度超出限制500 无法使用多标签打印
        //Command.SetQty(totalPage);
        //y += yMargin;
        //Command.DrawText(xMargin, y, "1", fontSize2, 0, true, false, false);
        //Command.Count(1);
        //Command.DrawText(xMargin + fontSize2, y, $"/2", fontSize2, 0, true, false, false);

        //顶部页码
        Command.DrawText(xMargin, y, $"{currentPage}/{totalPage}", 48, 0, true, false, false);
        y = yMargin + fontSize2;

        //顶部logo
        var logo1 = Image.Load("./Biz/Images/logo.jpg");
        int logo1Width = logo1.Width;
        var logo1Text = "此处应有广告语！";
        int logo1TextWidth = logo1Text.Length * fontSize1 + xMargin;
        int logo1MarginLeft = (pageWidth - logo1Width - logo1TextWidth) / 2;
        Command.DrawImage(logo1MarginLeft, y, logo1);
        int logo1TextY = y + (logo1.Height - fontSize1) / 2;
        Command.DrawText(logo1MarginLeft + logo1Width + xMargin, logo1TextY, "此处应有广告语！", fontSize1, 0, true, false,
            false);
        y = y + yMargin + logo1.Height;

        //条形码
        Command.DrawBarcode1D("128", xMargin + 5, y, data.Sheet, 3, 100, 3, 1);
        y = y + yMargin + 100;
        //全英文数字宽度减半
        int sheetTextWidth = data.Sheet.Length * fontSize1 / 2;
        int sheetTextMarginLeft = (pageWidth - sheetTextWidth - xMargin) / 2;
        Command.DrawText(sheetTextMarginLeft, y, data.Sheet, fontSize1, 0, true, false, false);
        y += yMargin + fontSize1;

        //中部配送信息
        int x1 = xMargin;
        int x2 = x1 + (fontSize2 * 4) + xMargin;
        DrawItem(Command, ref y, yMargin, x1, x2, "订单号", data.SourceSheet, fontSize2);
        DrawItem(Command, ref y, yMargin, x1, x2, "配送中心", data.ShippingPointName, fontSize2, true);
        DrawItem(Command, ref y, yMargin, x1, x2, "配送片区", data.ShippingZoningName, fontSize2, false);
        DrawItem(Command, ref y, yMargin, x1, x2, "配送时段",
            $"{data.DeliveryTimeBegin:yyyy-MM-dd HH:mm} ~ {data.DeliveryTimeEnd:HH:mm}", fontSize2,
            true);


        //收件人信息
        Command.DrawTextArea(x1, y, pageWidth - xMargin * 2, fontSize2 * 2, data.ConsigneeStreet, fontSize2,
            0, 0);
        y += yMargin + fontSize2 * 2;
        Command.DrawText(x1, y, data.ConsigneePhone, fontSize2, 0, false, false, false);
        y += yMargin + fontSize2;
        Command.DrawText(x1, y, data.ConsigneeName, fontSize2, 0, false, false, false);
        y += yMargin + fontSize2;

        Command.DrawDashLine(xMargin, y, pageWidth - xMargin * 2);
        y += yMargin;

        //底部logo
        var logo2 = Image.Load("./Biz/Images/logo.jpg");
        int logo2Width = logo2.Width;
        int logo2MarginLeft = (pageWidth - logo2Width) / 2;
        Command.DrawImage(logo2MarginLeft, y, logo2);
    }


    /// <summary>
    /// 画标题内容项
    /// </summary>
    /// <param name="command"></param>
    /// <param name="y"></param>
    /// <param name="yMargin"></param>
    /// <param name="leftX"></param>
    /// <param name="rightX"></param>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <param name="fontSize"></param>
    /// <param name="rightBold"></param>
    private void DrawItem(CPCLPrintCommand command, ref int y, int yMargin, int leftX, int rightX, string left,
        string right, int fontSize, bool rightBold = false)
    {
        int x1 = leftX;
        int x2 = rightX;
        command.DrawText(x1, y, left, fontSize, 0, false, false, false);
        command.DrawText(x2, y, right, fontSize, 0, rightBold, false, false);
        y += yMargin + fontSize;
    }

    /// <summary>
    /// 测试打印配送详情面单
    /// </summary>
    [Fact]
    public void Test_PriceShippingDetailLabel()
    {
        BuildShippingDetailLabel(GetShippingLabel());
        Build();
    }

    /// <summary>
    /// 构建配送详情面单
    /// </summary>
    /// <param name="data"></param>
    private void BuildShippingDetailLabel(ShippingLabel data)
    {
        var xMargin = 15;
        var yMargin = 20;
        var y = 0;

        var pageWidth = 580;

        var fontSize1 = (int) FontSize.Size24;
        var fontSize2 = (int) FontSize.Size32;
        var pageHeight = 720 + data.CargoList.Count * (fontSize1 * 2 + yMargin * 2);

        // 设置打印页
        Command.SetPage(pageWidth, pageHeight);

        //顶部logo
        var logo1 = Image.Load("./Biz/Images/utopa.jpg");
        int logo1Width = logo1.Width;
        var logo1Text = "此处应有广告语！";
        int logo1TextWidth = logo1Text.Length * fontSize1 + xMargin;
        int logo1MarginLeft = (pageWidth - logo1Width - logo1TextWidth) / 2;
        Command.DrawImage(logo1MarginLeft, y, logo1);
        int logo1TextY = y + (logo1.Height - fontSize1) / 2;
        Command.DrawText(logo1MarginLeft + logo1Width + xMargin, logo1TextY, "此处应有广告语！", fontSize1, 0, true, false,
            false);
        y = y + yMargin + logo1.Height;

        //条形码
        Command.DrawBarcode1D("128", xMargin + 5, y, data.Sheet, 3, 100, 3, 1);
        y = y + yMargin + 100;
        //全英文数字宽度减半
        int sheetTextWidth = data.Sheet.Length * fontSize1 / 2;
        int sheetTextMarginLeft = (pageWidth - sheetTextWidth - xMargin) / 2;
        Command.DrawText(sheetTextMarginLeft, y, data.Sheet, fontSize1, 0, false, false, false);
        y += yMargin + fontSize1;

        //中部配送信息
        int x1 = xMargin;
        int x2 = x1 + (fontSize2 * 4) + xMargin;
        DrawItem(Command, ref y, yMargin, x1, x2, "订单号", data.SourceSheet, fontSize1);
        DrawItem(Command, ref y, yMargin, x1, x2, "收货人", data.ConsigneeName, fontSize1, false);
        DrawItem(Command, ref y, yMargin, x1, x2, "联系电话", data.ConsigneePhone, fontSize1, false);
        //DrawItem(Command, ref y, yMargin, x1, x2, "收货地址", lomWaybill.ConsigneeStreet, fontSize1, false);

        Command.DrawText(x1, y, "收货地址", fontSize1, 0, false, false, false);
        Command.DrawTextArea(x2, y, pageWidth - x2 - xMargin, fontSize1 * 2, data.ConsigneeStreet, fontSize1,
            0, 0);
        y += yMargin + fontSize2 * 2;

        Command.DrawDashLine(xMargin, y, pageWidth - xMargin * 2);
        y += yMargin;

        //商品列表
        int cx1 = xMargin;
        int cargoNameWidth = 200;
        int cx2 = cx1 + cargoNameWidth;
        int cx3 = cx2 + 150;
        int cx4 = cx3 + 150;



        Command.DrawText(cx1, y, "商品名称", fontSize1, 0, false, false, false);
        Command.DrawText(cx2, y, "数量", fontSize1, 0, false, false, false);
        Command.DrawText(cx3, y, "单价", fontSize1, 0, false, false, false);
        Command.DrawText(cx4, y, "合计", fontSize1, 0, false, false, false);

        y += fontSize1 + yMargin;
        data.CargoList.ForEach(t =>
        {
            var tmpY = y;
            Command.DrawText(cx1, tmpY, $"{t.CargoCode} {t.CargoName}", fontSize1, 0, false, false, false);
            tmpY += yMargin + fontSize1;
            Command.DrawText(cx2, tmpY, t.CargoQty.ToString("F"), fontSize1, 0, false, false, false);
            Command.DrawText(cx3, tmpY, t.CargoPrice.ToString("F"), fontSize1, 0, false, false, false);
            Command.DrawText(cx4, tmpY, $"{(t.CargoPrice * t.CargoQty):F}", fontSize1, 0, false, false, false);
            y = tmpY + fontSize1 + yMargin;
        });

        Command.DrawDashLine(xMargin, y, pageWidth - xMargin * 2);

        y += yMargin;

        Command.DrawText(cx1, y, "合计", fontSize1, 0, false, false, false);
        Command.DrawText(cx2, y, $"{data.CargoList.Sum(x => x.CargoQty)}", fontSize1, 0, false, false, false);
        Command.DrawText(cx4, y, $"{data.CargoList.Sum(x => x.CargoPrice * x.CargoQty):F}", fontSize1, 0,
            false, false, false);

        y += fontSize1 + yMargin;

        Command.DrawDashLine(xMargin, y, pageWidth - xMargin * 2);

        y += yMargin;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private ShippingLabel GetShippingLabel()
    {
        return new ShippingLabel
        {
            BagQty = 3,
            ConsigneeName = "张三三",
            ConsigneePhone = "13800138000",
            ConsigneeStreet = "广东 广州 黄埔 菜鸟驿站广州黄埔下沙大街14号店 菜鸟驿站",
            DeliveryTimeBegin = DateTime.Parse("2020-10-10 10:00"),
            DeliveryTimeEnd = DateTime.Parse("2020-10-10 12:00"),
            Sheet = "Y20201026000245",
            ShippingPointName = "广州黄埔店",
            ShippingZoningName = "下沙大街",
            SourceSheet = "30122020102600008408551340",
            CargoList = new List<Cargo>
            {
                new Cargo
                {
                    CargoCode = "13800138000", CargoName = "乐肴居烧肉米汉堡（蜜椒味）150g", CargoPrice = 3.42M, CargoQty = 1
                },
                new Cargo
                {
                    CargoCode = "13800138000", CargoName = "达利园瑞士卷(巧克力)240g", CargoPrice = 3.42M, CargoQty = 2
                },
                new Cargo
                {
                    CargoCode = "13800138000", CargoName = "怡泉+C柠檬味汽水400ml", CargoPrice = 4.37M, CargoQty = 3
                },
                new Cargo
                {
                    CargoCode = "13800138000", CargoName = "统一红茶1000ml", CargoPrice = 3.42M, CargoQty = 4
                },
                new Cargo
                {
                    CargoCode = "13800138000", CargoName = "喜之郎果肉果冻单杯装（什锦）200g", CargoPrice = 3.42M, CargoQty = 3
                },
                new Cargo
                {
                    CargoCode = "13800138000", CargoName = "大成巴黎塔脆骨盐酥鸡280g", CargoPrice = 27.93M, CargoQty = 1
                },
            }
        };
    }
}

/// <summary>
/// 扩展
/// </summary>
internal static class Extensions
{
    /// <summary>
    /// 双语标签
    /// </summary>
    /// <param name="protocol">协议</param>
    /// <param name="startX">标签起始x坐标</param>
    /// <param name="startY">标签起始y坐标</param>
    /// <param name="zhCnLabel">中文标签</param>
    /// <param name="enLabel">英文标签</param>
    /// <param name="lineSpacing">行距</param>
    /// <param name="zhCnFontSize">中文字体大小</param>
    /// <param name="enFontSize">英文字体大小</param>
    public static CPCLPrintCommand BilingualLabel(this CPCLPrintCommand protocol, int startX,
        int startY, string zhCnLabel, string enLabel, int lineSpacing = 5, FontSize zhCnFontSize = FontSize.Size24, FontSize enFontSize = FontSize.Size16)
    {
        protocol.DrawText(startX, startY, zhCnLabel, zhCnFontSize);
        protocol.DrawText(startX, startY + (int)zhCnFontSize + lineSpacing, enLabel, enFontSize);
        return protocol;
    }

    /// <summary>
    /// 商品价格
    /// </summary>
    /// <param name="protocol"></param>
    /// <param name="startX"></param>
    /// <param name="startY"></param>
    /// <param name="price"></param>
    /// <param name="unit"></param>
    public static CPCLPrintCommand GoodsPriceLabel(this CPCLPrintCommand protocol, int startX,
        int startY, decimal price, string unit)
    {
        // 获取比例
        var symbolScale = GetScale("symbol");
        var priceScale = GetScale("price");
        var unitScale = GetScale("unit");
        var decimalScale = GetScale("decimal");

        // 获取分离后的价格
        var priceResult = SplitPrice(price);

        // 获取比例结果
        var symbolScaleResult = ComputeScale(symbolScale.widthScale, symbolScale.heightScale, symbolScale.fontSize);
        var priceScaleResult = ComputeScale(priceScale.widthScale, priceScale.heightScale, priceScale.fontSize);
        var unitScaleResult = ComputeScale(unitScale.widthScale, unitScale.heightScale, unitScale.fontSize);
        var decimalScaleResult = ComputeScale(decimalScale.widthScale, decimalScale.heightScale, decimalScale.fontSize);

        // 获取整数价格宽度
        var integerPriceWidth = ComputeWidth(priceScale.widthScale, priceScale.fontSize, priceResult.integerPrice);

        // 设置金钱符号
        protocol.WriteRawLine($"SETMAG {symbolScale.widthScale} {symbolScale.heightScale}");
        protocol.WriteRawLine($"T 03 0 {startX} {startY - symbolScaleResult.height} ¥");

        // 设置整数价格
        protocol.WriteRawLine($"SETMAG {priceScale.widthScale} {priceScale.heightScale}");
        protocol.WriteRawLine($"T 03 0 {startX + symbolScaleResult.width} {startY - priceScaleResult.height} {priceResult.integerPrice}");

        // 设置小数价格
        protocol.WriteRawLine($"SETMAG {decimalScale.widthScale} {decimalScale.heightScale}");
        protocol.WriteRawLine(
            $"T 03 0 {startX + symbolScaleResult.width + integerPriceWidth} {startY - decimalScaleResult.height - unitScaleResult.height - 16} .{priceResult.decimalPrice}");

        // 设置单位
        protocol.WriteRawLine($"SETMAG {unitScale.widthScale} {unitScale.heightScale}");
        protocol.WriteRawLine(
            $"T 03 0 {startX + symbolScaleResult.width + integerPriceWidth} {startY - symbolScaleResult.height + 16} /{unit}");
        return protocol;
    }

    /// <summary>
    /// 商品价格
    /// </summary>
    /// <param name="protocol"></param>
    /// <param name="startX"></param>
    /// <param name="startY"></param>
    /// <param name="price"></param>
    /// <param name="unit"></param>
    public static CPCLPrintCommand GoodsPriceLabelV2(this CPCLPrintCommand protocol, int startX,
        int startY, decimal price, string unit)
    {
        // 获取比例
        var symbolScale = GetScaleV2("symbol");
        var priceScale = GetScaleV2("price");
        var unitScale = GetScaleV2("unit");
        var decimalScale = GetScaleV2("decimal");

        // 获取分离后的价格
        var priceResult = SplitPrice(price);

        // 获取比例结果
        var symbolScaleResult = ComputeScale(symbolScale.widthScale, symbolScale.heightScale, symbolScale.fontSize);
        var priceScaleResult = ComputeScale(priceScale.widthScale, priceScale.heightScale, priceScale.fontSize);
        var unitScaleResult = ComputeScale(unitScale.widthScale, unitScale.heightScale, unitScale.fontSize);
        var decimalScaleResult = ComputeScale(decimalScale.widthScale, decimalScale.heightScale, decimalScale.fontSize);

        // 获取整数价格宽度
        var integerPriceWidth = ComputeWidth(priceScale.widthScale, priceScale.fontSize, priceResult.integerPrice);

        // 设置金钱符号
        protocol.WriteRawLine($"SETMAG {symbolScale.widthScale} {symbolScale.heightScale}");
        protocol.WriteRawLine($"T 03 0 {startX} {startY - symbolScaleResult.height} ¥");

        // 设置整数价格
        protocol.WriteRawLine("SETBOLD 1");// 进行加粗
        protocol.WriteRawLine($"SETMAG {priceScale.widthScale} {priceScale.heightScale}");
        protocol.WriteRawLine($"T 03 0 {startX + symbolScaleResult.width} {startY - priceScaleResult.height} {priceResult.integerPrice}");

        // 设置小数价格
        protocol.WriteRawLine($"SETMAG {decimalScale.widthScale} {decimalScale.heightScale}");
        protocol.WriteRawLine(
            $"T 03 0 {startX + symbolScaleResult.width + integerPriceWidth} {startY - decimalScaleResult.height - unitScaleResult.height - 16} .{priceResult.decimalPrice}");
        protocol.WriteRawLine("SETBOLD 0");
        // 设置单位
        protocol.WriteRawLine($"SETMAG {unitScale.widthScale} {unitScale.heightScale}");
        protocol.WriteRawLine(
            $"T 03 0 {startX + symbolScaleResult.width + integerPriceWidth} {startY - symbolScaleResult.height + 16} /{unit}");
        return protocol;
    }

    /// <summary>
    /// 分隔价格
    /// </summary>
    /// <param name="price">价格</param>
    private static (string integerPrice, string decimalPrice) SplitPrice(decimal price)
    {
        var priceStr = price.ToString("F");
        var prices = priceStr.Split('.');
        return (prices[0], prices[1]);
    }

    private static int ComputeWidth(int scale, int fontSize, string content) => ComputeWidth(scale * fontSize, content);

    /// <summary>
    /// 计算宽度
    /// </summary>
    /// <param name="fontSize">字体尺寸</param>
    /// <param name="content">内容</param>
    private static int ComputeWidth(int fontSize, string content) => fontSize * content.Length / 2;

    /// <summary>
    /// 计算比例
    /// </summary>
    /// <param name="widthScale">宽比例</param>
    /// <param name="heightScale">高比例</param>
    /// <param name="fontSize">字体大小</param>
    private static (int width, int height) ComputeScale(int widthScale, int heightScale, int fontSize) => (widthScale * fontSize, heightScale * fontSize);

    /// <summary>
    /// 获取比例
    /// </summary>
    /// <param name="type">类型</param>
    private static (int widthScale, int heightScale, int fontSize) GetScale(string type)
    {
        switch (type)
        {
            case "symbol":
                return (2, 2, 24);
            case "price":
                return (2, 3, 24);
            case "unit":
                return (1, 1, 24);
            case "decimal":
                return (1, 1, 24);
            default:
                throw new NotImplementedException($"尚未实现该[{type}]类型的比例");
        }
    }

    /// <summary>
    /// 获取比例
    /// </summary>
    /// <param name="type">类型</param>
    private static (int widthScale, int heightScale, int fontSize) GetScaleV2(string type)
    {
        switch (type)
        {
            case "symbol":
                return (2, 2, 24);
            case "price":
                return (3, 4, 24);
            case "unit":
                return (1, 1, 24);
            case "decimal":
                return (1, 1, 24);
            default:
                throw new NotImplementedException($"尚未实现该[{type}]类型的比例");
        }
    }
}