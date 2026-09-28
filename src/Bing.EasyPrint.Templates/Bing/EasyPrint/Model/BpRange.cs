namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 表示由起点和终点界定的矩形区域。
    /// </summary>
    public class BpRange
    {
        /// <summary>
        /// 获取或设置区域的左上角坐标，单位为打印点。
        /// </summary>
        public BpCoordinate StartCoordinate { get; set; }

        /// <summary>
        /// 获取或设置区域的右下角坐标，单位为打印点。
        /// </summary>
        public BpCoordinate EndCoordinate { get; set; }
    }
}
