namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 边框样式
    /// </summary>
    public class BpBorder
    {
        /// <summary>
        /// 顶部线条类型。
        /// </summary>
        /// <remarks>1=实线；2=虚线</remarks>
        public int TopStyle { get; set; }

        /// <summary>
        /// 右部线条类型。
        /// </summary>
        /// <remarks>1=实线；2=虚线</remarks>
        public int RightStyle { get; set; }

        /// <summary>
        /// 左部线条类型。
        /// </summary>
        /// <remarks>1=实线；2=虚线</remarks>
        public int LeftStyle { get; set; }

        /// <summary>
        /// 下部线条线条类型。
        /// </summary>
        /// <remarks>1=实线；2=虚线</remarks>
        public int BottomStyle { get; set; }

        /// <summary>
        /// 背景颜色
        /// </summary>
        /// <remarks>1=白色；2=黑色；3=黄色</remarks>
        public int Color { get; set; }
    }
}
