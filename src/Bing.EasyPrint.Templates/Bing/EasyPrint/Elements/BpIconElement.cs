namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 定义使用线段绘制的内置单色图标元素。
    /// </summary>
    public sealed class BpIconElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpIconElement"/> 类型的实例。
        /// </summary>
        public BpIconElement() { Type = 9; }

        /// <summary>
        /// 获取或设置图标标识，默认值为 <c>check</c>。
        /// </summary>
        /// <remarks>
        /// 支持 <c>check</c>、<c>cross</c> 和 <c>info</c>。
        /// </remarks>
        public string Icon { get; set; } = "check";

        /// <summary>
        /// 获取或设置图标边长，单位为打印点，默认值为 24。
        /// </summary>
        /// <remarks>
        /// 有效值为 4 至 256。
        /// </remarks>
        public int Size { get; set; } = 24;

        /// <summary>
        /// 获取或设置图标线宽，单位为打印点，默认值为 2。
        /// </summary>
        /// <remarks>
        /// 有效值为 1 至 16。
        /// </remarks>
        public int LineWidth { get; set; } = 2;
    }
}
