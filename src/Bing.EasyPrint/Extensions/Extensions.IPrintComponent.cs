﻿
// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint
{
    /// <summary>
    /// 打印组件(<see cref="IPrintComponent{T}"/>) 扩展
    /// </summary>
    public static class PrintComponentExtensions
    {
        /// <summary>
        /// 画文本
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="component">打印组件</param>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="text">内容</param>
        public static T DrawText<T>(this IPrintComponent<T> component, int x, int y, string text) where T : IPrintCommand<T> => component.DrawText(x, y, text, FontSize.Size16, RotationAngle.None, TextStyle.None);

        /// <summary>
        /// 画文本
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="component">打印组件</param>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="text">内容</param>
        /// <param name="fontSize">字体大小</param>
        public static T DrawText<T>(this IPrintComponent<T> component, int x, int y, string text, FontSize fontSize) where T : IPrintCommand<T> => component.DrawText(x, y, text, fontSize, RotationAngle.None, TextStyle.None);

        /// <summary>
        /// 画文本
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="component">打印组件</param>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="text">内容</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="textStyle">字体样式</param>
        public static T DrawText<T>(this IPrintComponent<T> component, int x, int y, string text, FontSize fontSize, TextStyle textStyle) where T : IPrintCommand<T> => component.DrawText(x, y, text, fontSize, RotationAngle.None, textStyle);

        /// <summary>
        /// 画文本区域
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="component">打印组件</param>
        /// <param name="x">文字起始x坐标</param>
        /// <param name="y">文字起始y坐标</param>
        /// <param name="width">文字绘制区域宽度(可以为0，不为0的时候文字需要根据宽度自动换行)</param>
        /// <param name="height">文字绘制区域高度(可以为0)</param>
        /// <param name="text">内容</param>
        public static T DrawTextArea<T>(this IPrintComponent<T> component, int x, int y, int width, int height, string text) where T : IPrintCommand<T> => component.DrawTextArea(x, y, width, height, text, FontSize.Size16, RotationAngle.None, TextStyle.None);
    }
}