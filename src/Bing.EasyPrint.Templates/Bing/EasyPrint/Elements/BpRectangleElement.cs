namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 定义矩形轮廓或填充模板元素。
    /// </summary>
    public sealed class BpRectangleElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpRectangleElement"/> 类型的实例。
        /// </summary>
        public BpRectangleElement() { Type = 7; }

        /// <summary>
        /// 获取或设置矩形宽度，单位为打印点。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置矩形高度，单位为打印点。
        /// </summary>
        public int Height { get; set; }

        /// <summary>获取或设置是否绘制黑色填充；默认 false 保留轮廓输出。</summary>
        /// <remarks>填充占位为 Width × Height 个打印点，LineWidth 不影响填充面积。</remarks>
        public bool Filled { get; set; }

        /// <summary>
        /// 获取或设置矩形轮廓线宽，单位为打印点，默认值为 1。
        /// </summary>
        /// <remarks>
        /// 有效值为 1 至 16。
        /// </remarks>
        public int LineWidth { get; set; } = 1;
    }
}
