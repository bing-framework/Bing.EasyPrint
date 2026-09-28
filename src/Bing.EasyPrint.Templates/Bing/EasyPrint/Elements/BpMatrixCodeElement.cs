namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 定义以黑白点阵图像输出的二维条码元素。
    /// </summary>
    public sealed class BpMatrixCodeElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpMatrixCodeElement"/> 类型的实例。
        /// </summary>
        public BpMatrixCodeElement() { Type = 10; }

        /// <summary>
        /// 获取或设置二维条码格式，默认值为 <c>DataMatrix</c>。
        /// </summary>
        /// <remarks>
        /// 支持 <c>DataMatrix</c> 和 <c>PDF417</c>。
        /// </remarks>
        public string Format { get; set; } = "DataMatrix";

        /// <summary>
        /// 获取或设置生成图像的宽度，单位为打印点，默认值为 96。
        /// </summary>
        /// <remarks>
        /// 宽度和高度均不得小于 8 点，且总像素数受渲染选项限制。
        /// </remarks>
        public int Width { get; set; } = 96;

        /// <summary>
        /// 获取或设置生成图像的高度，单位为打印点，默认值为 96。
        /// </summary>
        public int Height { get; set; } = 96;
    }
}
