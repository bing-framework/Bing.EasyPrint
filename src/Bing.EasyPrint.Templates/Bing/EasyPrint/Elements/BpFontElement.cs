
namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 用于输出单行或多行文本的模板元素。
    /// </summary>
    public class BpFontElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpFontElement"/> 类型的实例。
        /// </summary>
        public BpFontElement() { Type = 1; }
        /// <summary>
        /// 获取或设置打印字体的字号值。
        /// </summary>
        public int FontSize { get; set; }

        /// <summary>
        /// 获取或设置打印字体的样式编号。
        /// </summary>
        public int FontStyle { get; set; }

        /// <summary>
        /// 获取或设置打印机使用的字体编号。
        /// </summary>
        public int FontName { get; set; }

        /// <summary>
        /// 获取或设置是否以粗体输出文字。
        /// </summary>
        public bool Bold { get; set; }

        /// <summary>
        /// 获取或设置文字在可用区域内的水平对齐方式，默认值为左对齐。
        /// </summary>
        public BpTextAlignment TextAlignment { get; set; }
    }
}
