using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bing.EasyPrint.Printing;

/// <summary>
/// 为 DesignerHost 的 JSON 和 ZIP 操作提供带资源预算的可扩展内存流。
/// </summary>
internal sealed class BudgetedHostStream : Stream
{
    private const int SnapshotChunkSize = 81920;
    private const int InitialCapacity = 256;

    private readonly MemoryStream _inner;
    private readonly string _resourceName;
    private readonly CancellationToken _cancellationToken;
    private IDisposable? _capacityReservation;
    private bool _disposed;

    public BudgetedHostStream(string resourceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new ArgumentException("资源名称不能为空。", nameof(resourceName));

        cancellationToken.ThrowIfCancellationRequested();
        _resourceName = resourceName;
        _cancellationToken = cancellationToken;
        _inner = new MemoryStream();
    }

    public override bool CanRead => !_disposed && _inner.CanRead;

    public override bool CanSeek => !_disposed && _inner.CanSeek;

    public override bool CanWrite => !_disposed && _inner.CanWrite;

    public override long Length
    {
        get
        {
            EnsureNotDisposed();
            return _inner.Length;
        }
    }

    public override long Position
    {
        get
        {
            EnsureNotDisposed();
            return _inner.Position;
        }
        set
        {
            EnsureNotDisposed();
            ThrowIfCancellationRequested();
            _inner.Position = value;
        }
    }

    public override void Flush()
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();
        _inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested(cancellationToken);
        return _inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();
        ValidateBuffer(buffer, offset, count);
        return _inner.Read(buffer, offset, count);
    }

    public override int Read(Span<byte> buffer)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();
        return _inner.Read(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested(cancellationToken);
        ValidateBuffer(buffer, offset, count);
        return _inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested(cancellationToken);
        return _inner.ReadAsync(buffer, cancellationToken);
    }

    public override int ReadByte()
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();
        return _inner.ReadByte();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();
        return _inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();
        ValidateLength(value);
        EnsureCapacity(value);
        _inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();
        ValidateBuffer(buffer, offset, count);
        EnsureCapacityForWrite(count);
        _inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();
        EnsureCapacityForWrite(buffer.Length);
        _inner.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested(cancellationToken);
        ValidateBuffer(buffer, offset, count);
        EnsureCapacityForWrite(count);
        return _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested(cancellationToken);
        EnsureCapacityForWrite(buffer.Length);
        return _inner.WriteAsync(buffer, cancellationToken);
    }

    public override void WriteByte(byte value)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();
        EnsureCapacityForWrite(1);
        _inner.WriteByte(value);
    }

    /// <summary>
    /// 创建当前位置不变的快照。成功后快照预留保留到当前资源作用域结束。
    /// </summary>
    internal byte[] ToArray(string resourceName)
    {
        EnsureNotDisposed();
        ThrowIfCancellationRequested();

        var length = _inner.Length;
        if (length > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(length), "内存流长度超过数组上限。");

        IDisposable? reservation = PrintResourceScope.Reserve(length, resourceName);
        try
        {
            ThrowIfCancellationRequested();
            var result = new byte[(int)length];
            if (length == 0)
            {
                reservation = null;
                return result;
            }

            if (!_inner.TryGetBuffer(out var segment))
                throw new InvalidOperationException("无法访问 DesignerHost 内存流缓冲区。");
            var source = segment.Array;
            if (source == null)
                throw new InvalidOperationException("无法访问 DesignerHost 内存流缓冲区。");

            var copied = 0;
            while (copied < result.Length)
            {
                ThrowIfCancellationRequested();
                var count = Math.Min(SnapshotChunkSize, result.Length - copied);
                Buffer.BlockCopy(source, checked(segment.Offset + copied), result, copied, count);
                copied += count;
            }

            ThrowIfCancellationRequested();
            reservation = null;
            return result;
        }
        catch
        {
            reservation?.Dispose();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            try
            {
                _capacityReservation?.Dispose();
            }
            finally
            {
                _capacityReservation = null;
                _inner.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void EnsureCapacityForWrite(int count)
    {
        var required = checked(_inner.Position + (long)count);
        ValidateLength(required);
        EnsureCapacity(required);
    }

    private void EnsureCapacity(long required)
    {
        if (required <= _inner.Capacity)
            return;

        ValidateLength(required);
        ThrowIfCancellationRequested();

        var currentCapacity = _inner.Capacity;
        var nextCapacity = currentCapacity == 0
            ? InitialCapacity
            : checked((long)currentCapacity * 2L);
        if (nextCapacity < required)
            nextCapacity = required;
        if (nextCapacity > int.MaxValue)
            nextCapacity = int.MaxValue;

        var nextReservation = PrintResourceScope.Reserve(nextCapacity, _resourceName);
        try
        {
            ThrowIfCancellationRequested();
            _inner.Capacity = (int)nextCapacity;
        }
        catch
        {
            nextReservation.Dispose();
            throw;
        }

        var previousReservation = _capacityReservation;
        _capacityReservation = nextReservation;
        previousReservation?.Dispose();
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BudgetedHostStream));
    }

    private void ThrowIfCancellationRequested()
        => _cancellationToken.ThrowIfCancellationRequested();

    private void ThrowIfCancellationRequested(CancellationToken cancellationToken)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateBuffer(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if ((uint)offset > (uint)buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if ((uint)count > (uint)(buffer.Length - offset))
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    private static void ValidateLength(long value)
    {
        if (value < 0 || value > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}
