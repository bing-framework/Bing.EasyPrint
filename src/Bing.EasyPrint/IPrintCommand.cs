namespace Bing.EasyPrint;

/// <summary>
/// 可写入并构建打印指令的命令。
/// </summary>
/// <typeparam name="TCommand">具体命令类型，用于返回当前命令实例。</typeparam>
public interface IPrintCommand<out TCommand>
{
    /// <summary>
    /// 写入可信的原始指令内容。
    /// </summary>
    /// <param name="raw">由调用方保证符合打印协议的原始指令内容。</param>
    /// <returns>当前打印命令实例。</returns>
    TCommand WriteRaw(string raw);

    /// <summary>
    /// 写入可信的原始指令并追加换行。
    /// </summary>
    /// <param name="raw">由调用方保证符合打印协议的原始指令内容。</param>
    /// <returns>当前打印命令实例。</returns>
    TCommand WriteRawLine(string raw);

    /// <summary>
    /// 构建可读取的打印指令数据。
    /// </summary>
    /// <returns>包含完整指令的缓冲区写入器。</returns>
    IBufferWriter Build();
}
