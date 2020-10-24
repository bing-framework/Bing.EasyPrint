namespace Bing.EasyPrint
{
    /// <summary>
    /// 默认简单打印
    /// </summary>
    internal class DefaultEasyPrint : IEasyPrint
    {
        /// <summary>
        /// 创建打印命令
        /// </summary>
        /// <typeparam name="T">打印命令类型</typeparam>
        public T CreateCommand<T>() where T : IPrintCommand<T>, new() => new T();
    }
}
