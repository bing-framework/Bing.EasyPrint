using Bing.EasyPrint.CPCL;

using Bing.EasyPrint.Protocols;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint;

/// <summary>
/// 简单打印(<see cref="IEasyPrint"/>) 扩展
/// </summary>
public static class EasyPrintExtensions
{
    /// <summary>
    /// 创建CPCL打印命令
    /// </summary>
    /// <param name="print">创建命令的打印服务。</param>
    /// <returns>新建的 CPCL 打印命令实例。</returns>
    // ReSharper disable once InconsistentNaming
    public static CPCLPrintCommand CreateCPCLCommand(this IEasyPrint print)
    {
        var result = print.CreateCommand<CPCLPrintCommand>();
        return result;
    }
    /// <summary>创建 ESC/POS 打印命令。</summary>
    public static EscPosPrintCommand CreateEscPosCommand(this IEasyPrint print)
        => print.CreateCommand<EscPosPrintCommand>();

    /// <summary>创建 TSPL 打印命令。</summary>
    public static TsplPrintCommand CreateTsplCommand(this IEasyPrint print)
        => print.CreateCommand<TsplPrintCommand>();

    /// <summary>创建 ZPL 打印命令。</summary>
    public static ZplPrintCommand CreateZplCommand(this IEasyPrint print)
        => print.CreateCommand<ZplPrintCommand>();
}
