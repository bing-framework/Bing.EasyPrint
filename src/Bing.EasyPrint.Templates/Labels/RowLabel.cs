namespace Bing.EasyPrint.Templates.Labels
{
    /// <summary>
    /// 行标签
    /// </summary>
    public class RowLabel
    {
        /// <summary>
        /// 索引值
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 行类型。1=Line 2=Loop 3=Table
        /// </summary>
        public int RowType { get; set; }
    }
}
