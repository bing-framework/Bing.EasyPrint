namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 定义椭圆轮廓模板元素。
    /// </summary>
    public sealed class BpEllipseElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpEllipseElement"/> 类型的实例。
        /// </summary>
        public BpEllipseElement() { Type = 8; }

        /// <summary>
        /// 获取或设置椭圆宽度，单位为打印点。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置椭圆高度，单位为打印点。
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 获取或设置椭圆轮廓线宽，单位为打印点，默认值为 1。
        /// </summary>
        /// <remarks>
        /// 有效值为 1 至 16。
        /// </remarks>
        public int LineWidth { get; set; } = 1;
    }
}
