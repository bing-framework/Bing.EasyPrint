namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 定义从起点到终点绘制的 CPCL 线段。
    /// </summary>
    public sealed class BpLineElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpLineElement"/> 类型的实例。
        /// </summary>
        public BpLineElement() { Type = 5; }

        /// <summary>
        /// 获取或设置线段宽度，单位为打印点，默认值为 1。
        /// </summary>
        /// <remarks>
        /// 有效值为 1 至 16。
        /// </remarks>
        public int LineWidth { get; set; } = 1;

        /// <summary>
        /// 获取或设置线段终点相对于所在单元格或组合原点的坐标。
        /// </summary>
        public Bing.EasyPrint.Model.BpCoordinate LineEndCoordinate { get; set; }
    }
}
