namespace Bing.EasyPrint
{
    /// <summary>
    /// 缓冲区写入器
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    public interface IBufferWriter<out T>
    {
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="value">字节数组</param>
        T Write(byte[] value);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="value">字符串</param>
        T Write(string value);

        /// <summary>
        /// 清空内容
        /// </summary>
        T Clear();

        /// <summary>
        /// 获取二进制数组
        /// </summary>
        byte[] GetBytes();
    }

    /// <summary>
    /// 缓冲区写入器
    /// </summary>
    public interface IBufferWriter : IBufferWriter<IBufferWriter>
    {
    }
}
