using Bing.EasyPrint.CPCL;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint
{
    /// <summary>
    /// 简单打印(<see cref="IEasyPrint"/>) 扩展
    /// </summary>
    public static class EasyPrintExtensions
    {
        /// <summary>
        /// 创建CPCL打印命令
        /// </summary>
        /// <param name="print"></param>
        // ReSharper disable once InconsistentNaming
        public static CPCLPrintCommand CreateCPCLCommand(this IEasyPrint print)
        {
            var result = print.CreateCommand<CPCLPrintCommand>();
            return result;
        }
    }
}
