namespace Bing.EasyPrint
{
    public interface IEasyPrint
    {
        /// <summary>
        /// 创建打印命令
        /// </summary>
        /// <typeparam name="T">打印命令类型</typeparam>
        T CreateCommand<T>() where T : IPrintCommand<T>;
    }
}
