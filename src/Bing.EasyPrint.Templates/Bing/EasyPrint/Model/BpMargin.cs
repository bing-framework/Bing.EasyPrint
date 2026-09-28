namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 定义元素布局边界各方向的外边距。
    /// </summary>
    public class BpMargin
    {
        /// <summary>
        /// 获取或设置上边距，单位为打印点。
        /// </summary>
        public int Top{ get; set; }

        /// <summary>
        /// 获取或设置左边距，单位为打印点。
        /// </summary>
        public int Left { get; set; }

        /// <summary>
        /// 获取或设置右边距，单位为打印点。
        /// </summary>
        public int Right{ get; set; }

        /// <summary>
        /// 获取或设置下边距，单位为打印点。
        /// </summary>
        public int Bottom { get; set; }
    }
}
