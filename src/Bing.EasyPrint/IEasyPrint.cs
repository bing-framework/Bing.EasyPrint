namespace Bing.EasyPrint;

/// <summary>
/// 创建打印命令的入口。
/// </summary>
public interface IEasyPrint
{
    /// <summary>
    /// 创建指定类型的打印命令。
    /// </summary>
    /// <typeparam name="T">具有公开无参构造函数的打印命令类型。</typeparam>
    /// <returns>新建的打印命令实例。</returns>
    T CreateCommand<T>() where T : IPrintCommand<T>, new();
}
