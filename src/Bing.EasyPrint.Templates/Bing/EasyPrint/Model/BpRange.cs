namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 范围
    /// </summary>
    public class BpRange
    {
        /// <summary>
        /// 左上角起始坐标
        /// </summary>
        public BpCoordinate StartCoordinate { get; set; }

        /// <summary>
        /// 右下角终止坐标
        /// </summary>
        public BpCoordinate EndCoordinate { get; set; }
    }
}
