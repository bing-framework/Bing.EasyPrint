namespace Bing.EasyPrint;

/// <summary>
/// 打印命令
/// </summary>
public interface IPrintCommand<out TCommand>
{
    /// <summary>
    /// 写入命令
    /// </summary>
    /// <param name="raw">命令</param>
    TCommand WriteRaw(string raw);

    /// <summary>
    /// 写入命令并换行
    /// </summary>
    /// <param name="raw">命令</param>
    TCommand WriteRawLine(string raw);

    /// <summary>
    /// 构建命令
    /// </summary>
    IBufferWriter Build();
}