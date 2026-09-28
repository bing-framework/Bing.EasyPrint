namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 定义传统文本模板使用的文字样式参数。
    /// </summary>
    public class BpText
    {
        /// <summary>
        /// 获取或设置文字的字号值。
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// 获取或设置打印指令使用的字体类型标识。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 获取或设置文字的样式标识。
        /// </summary>
        public string Style { get; set; }
    }
}
