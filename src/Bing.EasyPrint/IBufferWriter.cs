namespace Bing.EasyPrint;

/// <summary>
/// 保存打印指令字节的缓冲区写入器。
/// </summary>
/// <typeparam name="T">写入操作返回的写入器类型。</typeparam>
public interface IBufferWriter<out T>
{
    /// <summary>
    /// 将字节追加到缓冲区。
    /// </summary>
    /// <param name="value">要追加的字节数组。</param>
    /// <returns>当前写入器实例。</returns>
    T Write(byte[] value);

    /// <summary>
    /// 将字符串按写入器编码追加到缓冲区。
    /// </summary>
    /// <param name="value">要编码并追加的字符串。</param>
    /// <returns>当前写入器实例。</returns>
    T Write(string value);

    /// <summary>
    /// 清空已写入的内容。
    /// </summary>
    /// <returns>当前写入器实例。</returns>
    T Clear();

    /// <summary>
    /// 获取已写入的字节。
    /// </summary>
    /// <returns>包含当前缓冲区内容的字节数组。</returns>
    byte[] GetBytes();
}

/// <summary>
/// 使用非泛型链式接口的打印指令缓冲区写入器。
/// </summary>
public interface IBufferWriter : IBufferWriter<IBufferWriter>
{
}
