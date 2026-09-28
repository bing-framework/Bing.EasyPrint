using System.IO;
using SixLabors.ImageSharp;

namespace Bing.EasyPrint;

/// <summary>
/// 定义打印页面及绘制元素的链式操作。
/// </summary>
/// <typeparam name="T">链式操作返回的打印命令类型。</typeparam>
public interface IPrintComponent<out T> where T : IPrintCommand<T>
{
    /// <summary>
    /// 设置打印页面。
    /// </summary>
    /// <param name="width">页面宽度，单位为打印点。</param>
    /// <param name="height">页面高度，单位为打印点。</param>
    /// <param name="orientation">页面旋转方向。</param>
    /// <returns>当前打印命令实例。</returns>
    T SetPage(int width, int height, PrintOrientation orientation = PrintOrientation.None);

    /// <summary>
    /// 设置打印页面。
    /// </summary>
    /// <param name="width">页面宽度，单位为打印点。</param>
    /// <param name="height">页面高度，单位为打印点。</param>
    /// <param name="orientation">页面旋转角度。</param>
    /// <returns>当前打印命令实例。</returns>
    T SetPage(int width, int height, int orientation = 0);

    /// <summary>
    /// 设置标签打印份数。
    /// </summary>
    /// <param name="qty">请求打印的份数。</param>
    /// <remarks>当前 CPCL 实现将份数限制在 1 至 1024 之间。</remarks>
    /// <returns>当前打印命令实例。</returns>
    T SetQty(int qty);

    /// <summary>
    /// 设置标签的横向偏移。
    /// </summary>
    /// <param name="offset">横向偏移量，单位为打印点。</param>
    /// <remarks>当前 CPCL 实现将负偏移量按零处理。</remarks>
    /// <returns>当前打印命令实例。</returns>
    T SetOffset(int offset);

    /// <summary>
    /// 画线
    /// </summary>
    /// <param name="x0">线条起始点x坐标</param>
    /// <param name="y0">线条起始点y坐标</param>
    /// <param name="x1">线条结束点x坐标</param>
    /// <param name="y1">线条结束点纵坐标。</param>
    /// <param name="lineWidth">线条宽度</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawLine(int x0, int y0, int x1, int y1, int lineWidth);

    /// <summary>
    /// 画线
    /// </summary>
    /// <param name="x0">线条起始点x坐标</param>
    /// <param name="y0">线条起始点y坐标</param>
    /// <param name="x1">线条结束点x坐标</param>
    /// <param name="y1">线条结束点纵坐标。</param>
    /// <param name="lineWidth">线条宽度</param>
    /// <param name="lineStyle">线条样式</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawLine(int x0, int y0, int x1, int y1, int lineWidth, LineStyle lineStyle);

    /// <summary>
    /// 画虚线
    /// </summary>
    /// <param name="x0">线条起始点x坐标</param>
    /// <param name="y0">线条起始点y坐标</param>
    /// <param name="x1">线条结束点x坐标</param>
    /// <param name="y1">线条结束点y坐标</param>
    /// <remarks>当前 CPCL 实现按起点纵坐标绘制水平虚线，不使用 <paramref name="y1"/>。</remarks>
    /// <returns>当前打印命令实例。</returns>
    T DrawDashLine(int x0, int y0, int x1, int y1);

    /// <summary>
    /// 画虚线
    /// </summary>
    /// <param name="x">线条点x坐标</param>
    /// <param name="y">线条点y坐标</param>
    /// <param name="length">线条长度</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawDashLine(int x, int y, int length);

    /// <summary>
    /// 画矩形
    /// </summary>
    /// <param name="x0">矩形框左上角x坐标</param>
    /// <param name="y0">矩形框左上角y坐标</param>
    /// <param name="x1">矩形框右下角x坐标</param>
    /// <param name="y1">矩形框右下角y坐标</param>
    /// <param name="lineWidth">线条宽度</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawRect(int x0, int y0, int x1, int y1, int lineWidth);

    /// <summary>
    /// 画矩形
    /// </summary>
    /// <param name="x0">矩形框左上角x坐标</param>
    /// <param name="y0">矩形框左上角y坐标</param>
    /// <param name="x1">矩形框右下角x坐标</param>
    /// <param name="y1">矩形框右下角y坐标</param>
    /// <param name="lineWidth">线条宽度</param>
    /// <param name="lineStyle">线条样式</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawRect(int x0, int y0, int x1, int y1, int lineWidth, LineStyle lineStyle);

    /// <summary>
    /// 画矩形（填充）
    /// </summary>
    /// <param name="x0">矩形框左上角x坐标</param>
    /// <param name="y0">矩形框左上角y坐标</param>
    /// <param name="x1">矩形框右下角x坐标</param>
    /// <param name="y1">矩形框右下角y坐标</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawRectFill(int x0, int y0, int x1, int y1);

    /// <summary>
    /// 画一维条码
    /// </summary>
    /// <param name="type">条码类型</param>
    /// <param name="x">条码起始x坐标</param>
    /// <param name="y">条码起始y坐标</param>
    /// <param name="text">条码内容</param>
    /// <param name="lineWidth">条码线条宽度</param>
    /// <param name="height">条码高度</param>
    /// <param name="ratio">宽条与窄条的比率</param>
    /// <param name="rotation">旋转角度</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawBarcode1D(string type, int x, int y, string text, int lineWidth, int height, int ratio, int rotation);

    /// <summary>
    /// 画一维条码
    /// </summary>
    /// <param name="type">条码类型</param>
    /// <param name="x">条码起始x坐标</param>
    /// <param name="y">条码起始y坐标</param>
    /// <param name="text">条码内容</param>
    /// <param name="lineWidth">条码线条宽度</param>
    /// <param name="height">条码高度</param>
    /// <param name="ratio">宽条与窄条的比率</param>
    /// <param name="rotation">旋转角度</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawBarcode1D(int type, int x, int y, string text, int lineWidth, int height, int ratio, int rotation);

    /// <summary>
    /// 画一维条码
    /// </summary>
    /// <param name="type">条码类型</param>
    /// <param name="x">条码起始x坐标</param>
    /// <param name="y">条码起始y坐标</param>
    /// <param name="text">条码内容</param>
    /// <param name="lineWidth">条码线条宽度</param>
    /// <param name="height">条码高度</param>
    /// <param name="ratio">宽条与窄条的比率</param>
    /// <param name="rotation">旋转角度</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawBarcode1D(BarcodeType type, int x, int y, string text, int lineWidth, int height, int ratio, RotationAngle rotation);

    /// <summary>
    /// 画二维码
    /// </summary>
    /// <param name="x">二维码起始x坐标</param>
    /// <param name="y">二维码起始y坐标</param>
    /// <param name="text">二维码内容</param>
    /// <param name="unitWidth">模块的单位宽度。(1-32)</param>
    /// <param name="errorLevel">二维码纠错级别</param>
    /// <param name="rotation">旋转角度</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawQrCode(int x, int y, string text, int unitWidth, int errorLevel, int rotation);

    /// <summary>
    /// 画二维码
    /// </summary>
    /// <param name="x">二维码起始x坐标</param>
    /// <param name="y">二维码起始y坐标</param>
    /// <param name="text">二维码内容</param>
    /// <param name="unitWidth">模块的单位宽度。(1-32)</param>
    /// <param name="errorLevel">二维码纠错级别</param>
    /// <param name="rotation">旋转角度</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawQrCode(int x, int y, string text, int unitWidth, string errorLevel, int rotation);

    /// <summary>
    /// 画二维码
    /// </summary>
    /// <param name="x">二维码起始x坐标</param>
    /// <param name="y">二维码起始y坐标</param>
    /// <param name="text">二维码内容</param>
    /// <param name="unitWidth">模块的单位宽度。(1-32)</param>
    /// <param name="errorLevel">二维码纠错级别</param>
    /// <param name="rotation">旋转角度</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawQrCode(int x, int y, string text, QrCodeUnitSize unitWidth, QrCodeCorrectionLevel errorLevel, RotationAngle rotation);

    /// <summary>
    /// 画图片
    /// </summary>
    /// <param name="startX">图片起始x坐标</param>
    /// <param name="startY">图片起始y坐标</param>
    /// <param name="bitmap">由调用方持有的图片对象。</param>
    /// <remarks>当前 CPCL 实现不修改或释放传入的图片。</remarks>
    /// <returns>当前打印命令实例。</returns>
    T DrawImage(int startX, int startY, Image bitmap);

    /// <summary>
    /// 画图片
    /// </summary>
    /// <param name="startX">图片起始x坐标</param>
    /// <param name="startY">图片起始y坐标</param>
    /// <param name="imageUrl">图片位置。</param>
    /// <remarks>CPCL 实现将图片位置作为本地文件路径读取。</remarks>
    /// <returns>当前打印命令实例。</returns>
    T DrawImage(int startX, int startY, string imageUrl);

    /// <summary>
    /// 画图片
    /// </summary>
    /// <param name="startX">图片起始x坐标</param>
    /// <param name="startY">图片起始y坐标</param>
    /// <param name="bytes">编码后的图片字节。</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawImage(int startX, int startY, byte[] bytes);

    /// <summary>
    /// 画图片
    /// </summary>
    /// <param name="startX">图片起始x坐标</param>
    /// <param name="startY">图片起始y坐标</param>
    /// <param name="stream">图片流。</param>
    /// <remarks>调用方保留图片流的所有权，并负责释放；当前 CPCL 实现会恢复可定位流的原始位置。</remarks>
    /// <returns>当前打印命令实例。</returns>
    T DrawImage(int startX, int startY, Stream stream);

    /// <summary>
    /// 画文字
    /// </summary>
    /// <param name="x">文字起始x坐标</param>
    /// <param name="y">文字起始y坐标</param>
    /// <param name="text">内容</param>
    /// <param name="fontSize">字体大小</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="bold">是否加粗</param>
    /// <param name="reverse">是否反显文字。</param>
    /// <param name="underline">是否下划线</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawText(int x, int y, string text, int fontSize, int rotation, bool bold, bool reverse, bool underline);

    /// <summary>
    /// 画文字
    /// </summary>
    /// <param name="x">文字起始x坐标</param>
    /// <param name="y">文字起始y坐标</param>
    /// <param name="text">内容</param>
    /// <param name="fontSize">字体大小</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="style">样式</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawText(int x, int y, string text, int fontSize, int rotation, int style);

    /// <summary>
    /// 画文字
    /// </summary>
    /// <param name="x">文字起始x坐标</param>
    /// <param name="y">文字起始y坐标</param>
    /// <param name="text">内容</param>
    /// <param name="fontSize">字体大小</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="textStyle">字体样式</param>
    /// <returns>当前打印命令实例。</returns>
    T DrawText(int x, int y,string text, FontSize fontSize, RotationAngle rotation, TextStyle textStyle);

    /// <summary>
    /// 画文本区域
    /// </summary>
    /// <param name="x">文字起始x坐标</param>
    /// <param name="y">文字起始y坐标</param>
    /// <param name="width">文字区域宽度，非空文本时不能为零。</param>
    /// <param name="height">文字区域高度，非空文本时不能为零。</param>
    /// <param name="text">内容</param>
    /// <param name="fontSize">字体大小</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="textStyle">字体样式</param>
    /// <remarks>非空文本按区域宽度换行；当前 CPCL 实现不根据高度截断内容。</remarks>
    /// <exception cref="System.ArgumentException">非空文本的区域宽度或高度为零。</exception>
    /// <returns>当前打印命令实例。</returns>
    T DrawTextArea(int x, int y, int width, int height, string text, int fontSize, int rotation, int textStyle);

    /// <summary>
    /// 画文本区域
    /// </summary>
    /// <param name="x">文字起始x坐标</param>
    /// <param name="y">文字起始y坐标</param>
    /// <param name="width">文字区域宽度，非空文本时不能为零。</param>
    /// <param name="height">文字区域高度，非空文本时不能为零。</param>
    /// <param name="text">内容</param>
    /// <param name="fontSize">字体大小</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="textStyle">字体样式</param>
    /// <remarks>非空文本按区域宽度换行；当前 CPCL 实现不根据高度截断内容。</remarks>
    /// <exception cref="System.ArgumentException">非空文本的区域宽度或高度为零。</exception>
    /// <returns>当前打印命令实例。</returns>
    T DrawTextArea(int x, int y, int width, int height, string text, FontSize fontSize, RotationAngle rotation, TextStyle textStyle);
}
