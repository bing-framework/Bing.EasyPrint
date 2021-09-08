// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 分割线组件
    /// </summary>
    internal class SplitLineComponent : ComponentMetadata
    {
        /// <summary>
        /// 线条起始x坐标
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 线条起始y坐标
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 分割符
        /// </summary>
        public char Symbol { get; set; } = '-';

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="command">打印命令</param>
        public override void Build(CPCLPrintCommand command)
        {
            var lineMaxLength = Helper.GetLineMaxLength(command.CommandInfo.Width - X, Symbol > 127 ? 24 : 12);
            var line = new string(Symbol, lineMaxLength);
            command.Writer.WriteLine("SETMAG 1 1");
            command.Writer.WriteLine($"T 24 0 {X} {Y} {line}");
            command.Writer.WriteLine("SETMAG 0 0");
        }
    }
}
