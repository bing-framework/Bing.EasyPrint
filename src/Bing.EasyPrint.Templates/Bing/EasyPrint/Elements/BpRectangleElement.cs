namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 定义矩形轮廓模板元素。
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

        /// <summary>
        /// 获取或设置矩形轮廓线宽，单位为打印点，默认值为 1。
        /// </summary>
        /// <remarks>
        /// 有效值为 1 至 16。
        /// </remarks>
        public int LineWidth { get; set; } = 1;
    }
}
