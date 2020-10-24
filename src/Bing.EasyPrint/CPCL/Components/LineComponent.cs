
// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 线条组件
    /// </summary>
    internal class LineComponent : ComponentMetadata
    {
        /// <summary>
        /// 线条宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 线条起始点x坐标
        /// </summary>
        public int X0 { get; set; }

        /// <summary>
        /// 线条结束点x坐标
        /// </summary>
        public int X1 { get; set; }

        /// <summary>
        /// 线条起始点y坐标
        /// </summary>
        public int Y0 { get; set; }

        /// <summary>
        /// 线条结束点y坐标
        /// </summary>
        public int Y1 { get; set; }

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="command">打印命令</param>
        public override void Build(CPCLPrintCommand command) => command.Line(X0, Y0, X1, Y1, Width);
    }
}
