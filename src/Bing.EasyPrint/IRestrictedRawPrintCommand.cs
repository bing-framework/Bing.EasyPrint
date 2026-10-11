namespace Bing.EasyPrint;

/// <summary>
/// 支持按协议语法校验可信原始文本命令的打印命令。
/// </summary>
/// <typeparam name="TCommand">具体命令类型，用于返回当前命令实例。</typeparam>
public interface IRestrictedRawPrintCommand<out TCommand>
{
    /// <summary>
    /// 校验并写入当前协议明确支持的原始文本命令。
    /// </summary>
    /// <param name="raw">单条或多条协议文本命令。</param>
    /// <returns>当前打印命令实例。</returns>
    TCommand WriteRestrictedRaw(string raw);
}
