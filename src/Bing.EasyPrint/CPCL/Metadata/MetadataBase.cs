// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 元数据基类
    /// </summary>
    internal abstract class MetadataBase
    {
        /// <summary>
        /// 元数据类型
        /// </summary>
        public abstract MetadataType MetadataType { get; }

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="command">打印命令</param>
        public abstract void Build(CPCLPrintCommand command);
    }
}
