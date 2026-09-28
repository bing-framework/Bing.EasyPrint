namespace Bing.EasyPrint;

/// <summary>
/// 默认简单打印
/// </summary>
public class DefaultEasyPrint : IEasyPrint
{
    /// <inheritdoc />
    public T CreateCommand<T>() where T : IPrintCommand<T>, new() => new T();
}
