namespace Bing.EasyPrint.Templates.Labels
{
    /// <summary>
    /// 内容标签
    /// </summary>
    public class ContentLabel
    {
        /// <summary>
        /// 内容类型。1=文本 2=占位符
        /// </summary>
        public int ContentType { get; set; }

        /// <summary>
        /// 内容名称
        /// </summary>
        public string Content { get; set; }
    }
}
