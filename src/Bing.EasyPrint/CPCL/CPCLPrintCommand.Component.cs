using System;
using System.IO;
using SixLabors.ImageSharp;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// CPCL 打印命令 - 组件
/// </summary>
// ReSharper disable once InconsistentNaming
partial class CPCLPrintCommand
{
    /// <inheritdoc />
    public CPCLPrintCommand SetPage(int width, int height, PrintOrientation orientation = PrintOrientation.None)
    {
        Init(width, height);
        CommandInfo.PagerRotate = (int)orientation;
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand SetPage(int width, int height, int orientation)
    {
        Init(width, height);
        CommandInfo.PagerRotate = orientation;
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand SetQty(int qty)
    {
        CommandInfo.Qty = qty < 1 ? 1 : qty > 1024 ? 1024 : qty;
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand SetOffset(int offset)
    {
        CommandInfo.Offset = offset < 0 ? 0 : offset;
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawLine(int x0, int y0, int x1, int y1, int lineWidth)
    {
        Content.Add(new LineComponent { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Width = lineWidth });
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawLine(int x0, int y0, int x1, int y1, int lineWidth, LineStyle lineStyle)
    {
        switch (lineStyle)
        {
            case LineStyle.Full:
                DrawLine(x0, y0, x1, y1, lineWidth);
                break;
            case LineStyle.Dotted:
                DrawDashLine(x0, y0, x1, y1);
                break;
        }
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawDashLine(int x0, int y0, int x1, int y1)
    {
        Content.Add(new DashLineComponent
        {
            X0 = x0,
            Y0 = y0,
            X1 = x1,
            Y1 = y1
        });
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawDashLine(int x, int y, int length)
    {
        Content.Add(new DashLineComponent
        {
            X0 = x,
            Y0 = y,
            X1 = checked(x + length),
        });
        return this;
    }

    /// <summary>
    /// 画分割线
    /// </summary>
    /// <param name="x">线条起始x坐标</param>
    /// <param name="y">线条起始y坐标</param>
    /// <returns>当前打印命令实例。</returns>
    public CPCLPrintCommand DrawSplitLine(int x, int y)
    {
        Content.Add(new SplitLineComponent
        {
            X = x,
            Y = y
        });
        return this;
    }

    /// <summary>
    /// 画分割线
    /// </summary>
    /// <param name="x">线条起始x坐标</param>
    /// <param name="y">线条起始y坐标</param>
    /// <param name="symbol">分割符</param>
    /// <returns>当前打印命令实例。</returns>
    public CPCLPrintCommand DrawSplitLine(int x, int y,char symbol)
    {
        Content.Add(new SplitLineComponent
        {
            X = x,
            Y = y,
            Symbol = symbol
        });
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawRect(int x0, int y0, int x1, int y1, int lineWidth)
    {
        Content.Add(new BoxComponent { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Width = lineWidth });
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawRect(int x0, int y0, int x1, int y1, int lineWidth, LineStyle lineStyle)
    {
        switch (lineStyle)
        {
            case LineStyle.Full:
                DrawRectFill(x0, y0, x1, y1);
                break;
            case LineStyle.Dotted:
                DrawRect(x0, y0, x1, y1, lineWidth);
                break;
        }
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawRectFill(int x0, int y0, int x1, int y1) => InverseLine(x0, y0, x1, y1, y1 - y0);

    /// <inheritdoc />
    public CPCLPrintCommand DrawBarcode1D(string type, int x, int y, string text, int lineWidth, int height, int ratio, int rotation)
    {
        Content.Add(new Barcode1DComponent(type, text) { X = x, Y = y, LineWidth = lineWidth, Height = height, Rotate = rotation, Ratio = ratio });
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawBarcode1D(int type, int x, int y, string text, int lineWidth, int height, int ratio, int rotation)
    {
        if (!string.IsNullOrWhiteSpace(text))
            DrawBarcode1D(Helper.ConvertBarcodeType((BarcodeType) type), x, y, text, lineWidth, height, ratio, rotation);
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawBarcode1D(BarcodeType type, int x, int y, string text, int lineWidth, int height, int ratio, RotationAngle rotation)
    {
        if (!string.IsNullOrWhiteSpace(text))
            DrawBarcode1D(Helper.ConvertBarcodeType(type), x, y, text, lineWidth, height, ratio, (int) rotation);
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawQrCode(int x, int y, string text, int unitWidth, int errorLevel, int rotation) => DrawQrCode(x, y, text, unitWidth, Helper.ConvertErrorLevel(errorLevel), rotation);

    /// <inheritdoc />
    public CPCLPrintCommand DrawQrCode(int x, int y, string text, int unitWidth, string errorLevel, int rotation)
    {
        Content.Add(new QRCodeComponent(text, errorLevel) {X = x, Y = y, Size = unitWidth, Rotate = rotation});
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawQrCode(int x, int y, string text, QrCodeUnitSize unitWidth, QrCodeCorrectionLevel errorLevel, RotationAngle rotation)
    {
        if (!string.IsNullOrWhiteSpace(text))
            DrawQrCode(x, y, text, (int) unitWidth, (int) errorLevel, (int) rotation);
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawImage(int startX, int startY, Image bitmap)
    {
        return DrawImage(startX, startY, bitmap, new ImageProcessingOptions());
    }

    /// <summary>
    /// 在指定位置绘制受大小限制的图片。
    /// </summary>
    /// <param name="startX">水平打印点坐标。</param>
    /// <param name="startY">垂直打印点坐标。</param>
    /// <param name="bitmap">调用方持有且不会被修改的图片。</param>
    /// <param name="options">图片编码长度和像素数限制。</param>
    /// <returns>当前打印命令实例。</returns>
    public CPCLPrintCommand DrawImage(int startX, int startY, Image bitmap, ImageProcessingOptions options)
    {
        Content.Add(ImageComponent.CreateFromImage(bitmap, options).InitPosition(startX, startY));
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawImage(int startX, int startY, string imageUrl)
    {
        Content.Add(ImageComponent.CreateFromFile(imageUrl).InitPosition(startX, startY));
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawImage(int startX, int startY, byte[] bytes)
    {
        return DrawImage(startX, startY, bytes, new ImageProcessingOptions());
    }

    /// <summary>
    /// 在指定位置绘制受大小限制的编码图片。
    /// </summary>
    /// <param name="startX">水平打印点坐标。</param>
    /// <param name="startY">垂直打印点坐标。</param>
    /// <param name="bytes">调用方持有的编码后图片字节。</param>
    /// <param name="options">图片编码长度和像素数限制。</param>
    /// <returns>当前打印命令实例。</returns>
    public CPCLPrintCommand DrawImage(int startX, int startY, byte[] bytes, ImageProcessingOptions options)
    {
        Content.Add(ImageComponent.CreateFromBytes(bytes, options).InitPosition(startX, startY));
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawImage(int startX, int startY, Stream stream)
    {
        return DrawImage(startX, startY, stream, new ImageProcessingOptions());
    }

    /// <summary>
    /// 在指定位置绘制受大小限制的流图片。
    /// </summary>
    /// <param name="startX">水平打印点坐标。</param>
    /// <param name="startY">垂直打印点坐标。</param>
    /// <param name="stream">调用方持有的图片流。</param>
    /// <param name="options">图片编码长度和像素数限制。</param>
    /// <returns>当前打印命令实例。</returns>
    public CPCLPrintCommand DrawImage(int startX, int startY, Stream stream, ImageProcessingOptions options)
    {
        Content.Add(ImageComponent.CreateFromStream(stream, options).InitPosition(startX, startY));
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawText(int x, int y, string text, int fontSize, int rotation, bool bold, bool reverse, bool underline)
    {
        Content.Add(new TextComponent(text)
        {
            X = x, Y = y, FontSize = fontSize,
            Rotate = rotation, Bold = bold, Reverse = reverse, 
            Underline = underline
        });
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawText(int x, int y, string text, int fontSize, int rotation, int style)
    {
        var styleResult = Helper.ConvertStyle(style);
        return DrawText(x, y, text, fontSize, rotation, styleResult.bold, false, styleResult.underline);
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawText(int x, int y, string text, FontSize fontSize, RotationAngle rotation, TextStyle textStyle) => DrawText(x, y, text, (int) fontSize, (int) rotation, (int) textStyle);

    /// <inheritdoc />
    public CPCLPrintCommand DrawTextArea(int x, int y, int width, int height, string text, int fontSize, int rotation, int textStyle)
    {
        CPCLInputValidator.ValidateLine(text, nameof(text));
        if (string.IsNullOrWhiteSpace(text))
            return this;
        if (width == 0 || height == 0)
            throw new ArgumentException($"{nameof(width)} 或 {nameof(height)} 不能为0。");
        var widthTmp = 0;
        var startYTmp = y;
        var textTmp = "";
        foreach (var c in text.ToCharArray())
        {
            if (Helper.IsChinese(c))
                widthTmp = widthTmp + fontSize;
            else
                widthTmp = widthTmp + fontSize / 2;
            if (widthTmp >= width)
            {
                textTmp += c;
                DrawText(x, startYTmp, textTmp, fontSize, rotation, textStyle);
                widthTmp = 0;
                startYTmp += fontSize + 2;
                textTmp = "";
            }
            else
            {
                textTmp += c;
            }
        }

        if (!string.IsNullOrWhiteSpace(textTmp))
            DrawText(x, startYTmp, textTmp, fontSize, rotation, textStyle);
        return this;
    }

    /// <inheritdoc />
    public CPCLPrintCommand DrawTextArea(int x, int y, int width, int height, string text, FontSize fontSize, RotationAngle rotation, TextStyle textStyle) => DrawTextArea(x, y, width, height, text, (int) fontSize, (int) rotation, (int) textStyle);
}
