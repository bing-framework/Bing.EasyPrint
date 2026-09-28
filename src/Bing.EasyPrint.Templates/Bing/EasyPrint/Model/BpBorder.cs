namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 模板区域的边框和填充样式。
    /// </summary>
    public class BpBorder
    {
        /// <summary>
        /// 获取或设置顶部边框的线型编码。
        /// </summary>
        /// <remarks>1=实线；2=虚线</remarks>
        public int TopStyle { get; set; }

        /// <summary>
        /// 获取或设置右侧边框的线型编码。
        /// </summary>
        /// <remarks>1=实线；2=虚线</remarks>
        public int RightStyle { get; set; }

        /// <summary>
        /// 获取或设置左侧边框的线型编码。
        /// </summary>
        /// <remarks>1=实线；2=虚线</remarks>
        public int LeftStyle { get; set; }

        /// <summary>
        /// 获取或设置底部边框的线型编码。
        /// </summary>
        /// <remarks>1=实线；2=虚线</remarks>
        public int BottomStyle { get; set; }

        /// <summary>
        /// 获取或设置模板区域的填充颜色编码。
        /// </summary>
        /// <remarks>1=白色；2=黑色；3=黄色</remarks>
        public int Color { get; set; }
    }
}
