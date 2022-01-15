namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 图片元素
    /// </summary>
    public class BpImageElement : BpElement
    {
        /// <summary>
        /// 宽度
        /// </summary>
        public int Width{ get; set; }

        /// <summary>
        /// 高度
        /// </summary>
        public int Height{ get; set; }

        /// <summary>
        /// 是否压缩
        /// </summary>
        public bool IsCompress { get; set; }
    }
}
