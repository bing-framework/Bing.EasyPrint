using System;
using System.Drawing;
using System.IO;

namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// CPCL 打印命令 - 组件
    /// </summary>
    // ReSharper disable once InconsistentNaming
    partial class CPCLPrintCommand
    {
        /// <summary>
        /// 设置纸张
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="orientation">旋转角度</param>
        public CPCLPrintCommand SetPage(int width, int height, PrintOrientation orientation = PrintOrientation.None)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 设置纸张
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="orientation">旋转角度</param>
        public CPCLPrintCommand SetPage(int width, int height, int orientation = 0)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 设置打印标签数
        /// </summary>
        /// <param name="qty">打印标签数量</param>
        public CPCLPrintCommand SetQty(int qty)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 设置标签横向偏移量
        /// </summary>
        /// <param name="offset">偏移量</param>
        public CPCLPrintCommand SetOffset(int offset)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画线
        /// </summary>
        /// <param name="x0">线条起始点x坐标</param>
        /// <param name="y0">线条起始点y坐标</param>
        /// <param name="x1">线条结束点x坐标</param>
        /// <param name="y1">线条结束点y坐标</param>
        /// <param name="lineWidth">线条宽度</param>
        public CPCLPrintCommand DrawLine(int x0, int y0, int x1, int y1, int lineWidth)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画线
        /// </summary>
        /// <param name="x0">线条起始点x坐标</param>
        /// <param name="y0">线条起始点y坐标</param>
        /// <param name="x1">线条结束点x坐标</param>
        /// <param name="y1">线条结束点y坐标</param>
        /// <param name="lineWidth">线条宽度</param>
        /// <param name="lineStyle">线条样式</param>
        public CPCLPrintCommand DrawLine(int x0, int y0, int x1, int y1, int lineWidth, LineStyle lineStyle)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画虚线
        /// </summary>
        /// <param name="x0">线条起始点x坐标</param>
        /// <param name="y0">线条起始点y坐标</param>
        /// <param name="x1">线条结束点x坐标</param>
        /// <param name="y1">线条结束点y坐标</param>
        public CPCLPrintCommand DrawDashLine(int x0, int y0, int x1, int y1)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画虚线
        /// </summary>
        /// <param name="x">线条点x坐标</param>
        /// <param name="y">线条点y坐标</param>
        /// <param name="length">线条长度</param>
        public CPCLPrintCommand DrawDashLine(int x, int y, int length)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画矩形
        /// </summary>
        /// <param name="x0">矩形框左上角x坐标</param>
        /// <param name="y0">矩形框左上角y坐标</param>
        /// <param name="x1">矩形框右下角x坐标</param>
        /// <param name="y1">矩形框右下角y坐标</param>
        /// <param name="lineWidth">线条宽度</param>
        public CPCLPrintCommand DrawRect(int x0, int y0, int x1, int y1, int lineWidth)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画矩形
        /// </summary>
        /// <param name="x0">矩形框左上角x坐标</param>
        /// <param name="y0">矩形框左上角y坐标</param>
        /// <param name="x1">矩形框右下角x坐标</param>
        /// <param name="y1">矩形框右下角y坐标</param>
        /// <param name="lineWidth">线条宽度</param>
        /// <param name="lineStyle">线条样式</param>
        public CPCLPrintCommand DrawRect(int x0, int y0, int x1, int y1, int lineWidth, LineStyle lineStyle)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画矩形（填充）
        /// </summary>
        /// <param name="x0">矩形框左上角x坐标</param>
        /// <param name="y0">矩形框左上角y坐标</param>
        /// <param name="x1">矩形框右下角x坐标</param>
        /// <param name="y1">矩形框右下角y坐标</param>
        public CPCLPrintCommand DrawRectFill(int x0, int y0, int x1, int y1)
        {
            throw new NotImplementedException();
        }

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
        public CPCLPrintCommand DrawBarcode1D(string type, int x, int y, string text, int lineWidth, int height, int ratio,
            int rotation)
        {
            throw new NotImplementedException();
        }

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
        public CPCLPrintCommand DrawBarcode1D(int type, int x, int y, string text, int lineWidth, int height, int ratio, int rotation)
        {
            throw new NotImplementedException();
        }

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
        public CPCLPrintCommand DrawBarcode1D(BarcodeType type, int x, int y, string text, int lineWidth, int height, int ratio,
            RotationAngle rotation)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画二维码
        /// </summary>
        /// <param name="x">二维码起始x坐标</param>
        /// <param name="y">二维码起始y坐标</param>
        /// <param name="text">二维码内容</param>
        /// <param name="unitWidth">模块的单位宽度。(1-32)</param>
        /// <param name="errorLevel">二维码纠错级别</param>
        /// <param name="rotation">旋转角度</param>
        public CPCLPrintCommand DrawQrCode(int x, int y, string text, int unitWidth, int errorLevel, int rotation)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画二维码
        /// </summary>
        /// <param name="x">二维码起始x坐标</param>
        /// <param name="y">二维码起始y坐标</param>
        /// <param name="text">二维码内容</param>
        /// <param name="unitWidth">模块的单位宽度。(1-32)</param>
        /// <param name="errorLevel">二维码纠错级别</param>
        /// <param name="rotation">旋转角度</param>
        public CPCLPrintCommand DrawQrCode(int x, int y, string text, int unitWidth, string errorLevel, int rotation)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画二维码
        /// </summary>
        /// <param name="x">二维码起始x坐标</param>
        /// <param name="y">二维码起始y坐标</param>
        /// <param name="text">二维码内容</param>
        /// <param name="unitWidth">模块的单位宽度。(1-32)</param>
        /// <param name="errorLevel">二维码纠错级别</param>
        /// <param name="rotation">旋转角度</param>
        public CPCLPrintCommand DrawQrCode(int x, int y, string text, QrCodeUnitSize unitWidth, QrCodeCorrectionLevel errorLevel,
            RotationAngle rotation)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画图片
        /// </summary>
        /// <param name="startX">图片起始x坐标</param>
        /// <param name="startY">图片起始y坐标</param>
        /// <param name="bitmap">图片数据</param>
        public CPCLPrintCommand DrawImage(int startX, int startY, Bitmap bitmap)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画图片
        /// </summary>
        /// <param name="startX">图片起始x坐标</param>
        /// <param name="startY">图片起始y坐标</param>
        /// <param name="imageUrl">图片地址</param>
        public CPCLPrintCommand DrawImage(int startX, int startY, string imageUrl)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画图片
        /// </summary>
        /// <param name="startX">图片起始x坐标</param>
        /// <param name="startY">图片起始y坐标</param>
        /// <param name="bytes">图片数据</param>
        public CPCLPrintCommand DrawImage(int startX, int startY, byte[] bytes)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画图片
        /// </summary>
        /// <param name="startX">图片起始x坐标</param>
        /// <param name="startY">图片起始y坐标</param>
        /// <param name="stream">图片流</param>
        public CPCLPrintCommand DrawImage(int startX, int startY, Stream stream)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画文字
        /// </summary>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="text">内容</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="rotation">旋转角度</param>
        /// <param name="bold">是否加粗</param>
        /// <param name="reverse">是否颠倒</param>
        /// <param name="underline">是否下划线</param>
        public CPCLPrintCommand DrawText(int x, int y, string text, int fontSize, int rotation, bool bold, bool reverse,
            bool underline)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画文字
        /// </summary>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="text">内容</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="rotation">旋转角度</param>
        /// <param name="bold">是否加粗</param>
        /// <param name="style">样式</param>
        public CPCLPrintCommand DrawText(int x, int y, string text, int fontSize, int rotation, bool bold, int style)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画文本
        /// </summary>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="text">内容</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="rotation">旋转角度</param>
        /// <param name="textStyle">字体样式</param>
        /// <param name="color">文字颜色</param>
        public CPCLPrintCommand DrawText(int x, int y, string text, int fontSize, int rotation, int textStyle, int color)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画文本区域
        /// </summary>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="text">内容</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="rotation">旋转角度</param>
        /// <param name="textStyle">字体样式</param>
        /// <param name="color">文字颜色</param>
        public CPCLPrintCommand DrawText(int x, int y, string text, FontSize fontSize, RotationAngle rotation, TextStyle textStyle,
            PrintColor color)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画文本区域
        /// </summary>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="width">文字绘制区域宽度(可以为0，不为0的时候文字需要根据宽度自动换行)</param>
        /// <param name="height">文字绘制区域高度(可以为0)</param>
        /// <param name="text">内容</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="rotation">旋转角度</param>
        /// <param name="textStyle">字体样式</param>
        /// <param name="color">文字颜色</param>
        public CPCLPrintCommand DrawTextArea(int x, int y, int width, int height, string text, int fontSize, int rotation,
            int textStyle, int color)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 画文本区域
        /// </summary>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="width">文字绘制区域宽度(可以为0，不为0的时候文字需要根据宽度自动换行)</param>
        /// <param name="height">文字绘制区域高度(可以为0)</param>
        /// <param name="text">内容</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="rotation">旋转角度</param>
        /// <param name="textStyle">字体样式</param>
        /// <param name="color">文字颜色</param>
        public CPCLPrintCommand DrawTextArea(int x, int y, int width, int height, string text, FontSize fontSize,
            RotationAngle rotation, TextStyle textStyle, PrintColor color)
        {
            throw new NotImplementedException();
        }
    }
}
