using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;

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
    /// 保存每个原始文本片段对应的 UTF-16 预算租约。
    /// </summary>
    private readonly List<IDisposable> _sourceReservations;

    /// <summary>
    /// 保存文本命令写入时使用的字符编码。
    /// </summary>
    private readonly Encoding _encoding;
    private readonly CancellationToken _cancellationToken;
    internal CancellationToken BuildCancellationToken => _cancellationToken;
    private IDisposable _bufferReservation;

    /// <summary>
    /// 初始化一个 <see cref="BufferWriter"/> 类型的实例。
    /// </summary>
    /// <param name="encoding">将命令文本编码为字节的字符编码。</param>
    public BufferWriter(Encoding encoding)
        : this(encoding, CancellationToken.None) { }

    internal BufferWriter(Encoding encoding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        _cancellationToken = cancellationToken;
        _bufferReservation = PrintResourceScope.Reserve(256, "Command.Buffer");
        try
        {
            _buffer = new byte[256];
            _sources = new List<string>();
            _sourceReservations = new List<IDisposable>();
        }
        catch { _bufferReservation.Dispose(); throw; }
    }

    /// <inheritdoc />
    public IBufferWriter Write(byte[] value)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (value == null)
            return this;
        EnsureCapacity(checked(_length + value.Length));
        BufferCopy.Copy(value, 0, _buffer, _length, value.Length, _cancellationToken);
        _length += value.Length;
        return this;
    }

    /// <inheritdoc />
    public IBufferWriter Write(string value)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(value))
            return this;
        var sourceReservation = PrintResourceScope.Reserve(
            checked((long)value.Length * 2), "Command.SourceText");
        var originalLength = _length;
        var sourceAdded = false;
        var reservationAdded = false;
        try
        {
            var byteCount = _encoding.GetByteCount(value);
            var requiredCapacity = checked(_length + byteCount);
            EnsureCapacity(requiredCapacity);
            var bytesWritten = _encoding.GetBytes(value, 0, value.Length, _buffer, _length);
            _cancellationToken.ThrowIfCancellationRequested();

            _sources.Add(value);
            sourceAdded = true;
            _sourceReservations.Add(sourceReservation);
            reservationAdded = true;
            sourceReservation = null;
            _length = checked(_length + bytesWritten);
            _cancellationToken.ThrowIfCancellationRequested();
            return this;
        }
        catch
        {
            _length = originalLength;
            if (reservationAdded)
            {
                var last = _sourceReservations.Count - 1;
                var reservation = _sourceReservations[last];
                _sourceReservations.RemoveAt(last);
                reservation.Dispose();
            }
            else sourceReservation?.Dispose();
            if (sourceAdded) _sources.RemoveAt(_sources.Count - 1);
            throw;
        }
    }

    /// <inheritdoc />
    public IBufferWriter Clear()
    {
        _length = 0;
        _sources.Clear();
        foreach (var reservation in _sourceReservations) reservation.Dispose();
        _sourceReservations.Clear();
        return this;
    }

    /// <inheritdoc />
    public byte[] GetBytes()
    {
        return BudgetedBuffer.Copy(_buffer, 0, _length, "Command.Output", _cancellationToken);
    }

    /// <summary>
    /// 按需扩展编码字节缓冲区。
    /// </summary>
    /// <param name="requiredCapacity">写入后所需的最小字节容量。</param>
    private void EnsureCapacity(int requiredCapacity)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_buffer.Length >= requiredCapacity)
            return;
        var doubledCapacity = _buffer.Length <= int.MaxValue / 2 ? _buffer.Length * 2 : int.MaxValue;
        var capacity = Math.Max(requiredCapacity, doubledCapacity);
        // 扩容期间旧、新数组同时存活，先预留整个新数组，而不是只计算容量差额。
        var reservation = PrintResourceScope.Reserve(capacity, "Command.BufferGrowth");
        try { Array.Resize(ref _buffer, capacity); }
        catch { reservation.Dispose(); throw; }
        _bufferReservation.Dispose();
        _bufferReservation = reservation;
        _cancellationToken.ThrowIfCancellationRequested();
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
