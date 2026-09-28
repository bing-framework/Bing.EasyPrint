// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 定义可写入 CPCL 命令的元数据。
/// </summary>
internal abstract class MetadataBase
{
    /// <summary>
    /// 获取元数据的类型。
    /// </summary>
    public abstract MetadataType MetadataType { get; }

    /// <summary>
    /// 将元数据写入指定打印命令。
    /// </summary>
    /// <param name="command">接收元数据指令的打印命令。</param>
    public abstract void Build(CPCLPrintCommand command);
}
