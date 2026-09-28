namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 描述模板中的一行换行文本及其占用高度。
    /// </summary>
    public class FeedLine
    {
        /// <summary>
        /// 获取或设置此行要输出的文本内容。
        /// </summary>
        public string Line { get; set; }

        /// <summary>
        /// 获取或设置此行占用的高度，单位为打印点。
        /// </summary>
        public int Height{ get; set; }
    }
}
