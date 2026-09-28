using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.EasyPrint.Core;

/// <summary>
/// 累积 CPCL 命令的编码字节和文本片段。
/// </summary>
internal class BufferWriter : IBufferWriter
{
    /// <summary>
    /// 保存可复用的编码字节缓冲区。
    /// </summary>
    private byte[] _buffer;

    /// <summary>
    /// 保存缓冲区中已经写入的字节数。
    /// </summary>
    private int _length;

    /// <summary>
    /// 保存用于文本表示的原始命令片段。
    /// </summary>
    private readonly List<string> _sources;

    /// <summary>
    /// 保存文本命令写入时使用的字符编码。
    /// </summary>
    private readonly Encoding _encoding;

    /// <summary>
    /// 初始化一个 <see cref="BufferWriter"/> 类型的实例。
    /// </summary>
    /// <param name="encoding">将命令文本编码为字节的字符编码。</param>
    public BufferWriter(Encoding encoding)
    {
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        _buffer = new byte[256];
        _sources = new List<string>();
    }

    /// <inheritdoc />
    public IBufferWriter Write(byte[] value)
    {
        if (value == null)
            return this;
        EnsureCapacity(checked(_length + value.Length));
        Array.Copy(value, 0, _buffer, _length, value.Length);
        _length += value.Length;
        return this;
    }

    /// <inheritdoc />
    public IBufferWriter Write(string value)
    {
        if (string.IsNullOrEmpty(value))
            return this;
        var byteCount = _encoding.GetByteCount(value);
        var requiredCapacity = checked(_length + byteCount);
        EnsureCapacity(requiredCapacity);
        var bytesWritten = _encoding.GetBytes(value, 0, value.Length, _buffer, _length);
        _length += bytesWritten;
        _sources.Add(value);
        return this;
    }

    /// <inheritdoc />
    public IBufferWriter Clear()
    {
        _length = 0;
        _sources.Clear();
        return this;
    }

    /// <inheritdoc />
    public byte[] GetBytes()
    {
        var result = new byte[_length];
        Array.Copy(_buffer, 0, result, 0, _length);
        return result;
    }

    /// <summary>
    /// 按需扩展编码字节缓冲区。
    /// </summary>
    /// <param name="requiredCapacity">写入后所需的最小字节容量。</param>
    private void EnsureCapacity(int requiredCapacity)
    {
        if (_buffer.Length >= requiredCapacity)
            return;
        var doubledCapacity = _buffer.Length <= int.MaxValue / 2 ? _buffer.Length * 2 : int.MaxValue;
        Array.Resize(ref _buffer, Math.Max(requiredCapacity, doubledCapacity));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (_sources.Count == 0)
            return string.Empty;
        var result = new StringBuilder();
        foreach (var command in _sources)
            result.Append(command);
        return result.ToString();
    }
}
