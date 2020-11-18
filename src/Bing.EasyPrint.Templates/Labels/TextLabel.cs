namespace Bing.EasyPrint.Templates.Labels
{
    /// <summary>
    /// 文本标签
    /// </summary>
    public class TextLabel : ContentLabel
    {
        /// <summary>
        /// 距离上层元素的左边距
        /// </summary>
        public float Left { get; set; }

        /// <summary>
        /// 距离上层元素的上边距
        /// </summary>
        public float Top { get; set; }

        /// <summary>
        /// 打印的字体大小
        /// </summary>
        public float FontSize { get; set; }

        /// <summary>
        /// 字体名称
        /// </summary>
        public string FontName { get; set; }

        /// <summary>
        /// 显示位置
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// 结束位置
        /// </summary>
        public int End { get; set; }

        /// <summary>
        /// 字体样式
        /// </summary>
        public int FontStyle { get; set; }
    }
}
