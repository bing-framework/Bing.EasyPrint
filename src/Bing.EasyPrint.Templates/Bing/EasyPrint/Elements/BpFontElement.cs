
namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 文字显示元素
    /// </summary>
    public class BpFontElement : BpElement
    {
        /// <summary>
        /// 字体大小
        /// </summary>
        public int FontSize { get; set; }

        /// <summary>
        /// 字体风格
        /// </summary>
        public int FontStyle { get; set; }

        /// <summary>
        /// 字体名称
        /// </summary>
        public int FontName { get; set; }

        /// <summary>
        /// 字体加粗
        /// </summary>
        public bool Bold { get; set; }
    }
}
