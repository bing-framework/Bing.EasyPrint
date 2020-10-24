
// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 虚线组件
    /// </summary>
    internal class DashLineComponent : LineComponent
    {
        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="command">打印命令</param>
        public override void Build(CPCLPrintCommand command)
        {
            command.Writer.WriteLine("SETMAG 1 1");
            for (var i = 0; i < X1; i = ((i + 16) - 1) + 1) 
                command.Writer.WriteLine($"T 24 0 {X0 + i} {Y0 - 10} -");
            command.Writer.WriteLine("SETMAG 0 0");
        }
    }
}
