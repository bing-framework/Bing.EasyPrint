namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 表示打印页面或元素布局中的二维坐标。
    /// </summary>
    public class BpCoordinate
    {
        /// <summary>
        /// 获取或设置水平方向的坐标值，单位为打印点。
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 获取或设置垂直方向的坐标值，单位为打印点。
        /// </summary>
        public int Y { get; set; }
    }
}
