using System;
using System.Collections.Generic;
using System.Threading;

namespace Bing.EasyPrint.Printing;

/// <summary>
/// 为受管缓冲区和调用方提供的资源估计建立一个可嵌套的预留作用域。
/// 该类型记录的是显式估计的字节数，不是进程实际内存使用量。
/// </summary>
public sealed class PrintResourceScope : IDisposable
{
    private static readonly AsyncLocal<PrintResourceScope> CurrentScope =
        new AsyncLocal<PrintResourceScope>();

    internal static bool HasCurrentScope => CurrentScope.Value != null;

    private readonly RootState _root;
    private readonly PrintResourceScope _parent;
    private readonly CancellationToken _cancellationToken;
    private readonly HashSet<Reservation> _reservations = new HashSet<Reservation>();
    private long _maxBytes;
    private long _currentBytes;
    private long _peakBytes;
    private long _retainedBytes;
    private int _activeChildren;
    private bool _disposed;

    private PrintResourceScope(PrintResourceScope parent, RootState root, long maxBytes, CancellationToken cancellationToken)
    {
        _parent = parent;
        _root = root;
        _maxBytes = maxBytes;
        _cancellationToken = cancellationToken;
    }

    /// <summary>当前作用域已预留的字节数，包括仍在活动的子作用域预留。</summary>
    public long CurrentBytes
    {
        get
        {
            lock (_root.Gate) return _currentBytes;
        }
    }

    /// <summary>当前作用域生命周期内达到的预留峰值。</summary>
    public long PeakBytes
    {
        get
        {
            lock (_root.Gate) return _peakBytes;
        }
    }

    /// <summary>当前作用域允许的最大预留字节数。</summary>
    public long MaxBytes
    {
        get
        {
            lock (_root.Gate) return _maxBytes;
        }
    }

    /// <summary>
    /// 收紧当前作用域的最大预留字节数。
    /// </summary>
    /// <param name="maxBytes">新的最大预留字节数；只能小于或等于当前限制。</param>
    /// <exception cref="ArgumentOutOfRangeException">新的限制为负数。</exception>
    /// <exception cref="ArgumentException">新的限制会放宽当前限制。</exception>
    /// <exception cref="PrintResourceLimitException">新的限制低于当前已预留字节数。</exception>
    /// <exception cref="ObjectDisposedException">作用域已经释放。</exception>
    /// <exception cref="OperationCanceledException">作用域的取消令牌已经取消。</exception>
    public void LimitTo(long maxBytes)
    {
        lock (_root.Gate)
        {
            EnsureActiveLocked();
            _cancellationToken.ThrowIfCancellationRequested();
            if (maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            if (maxBytes > _maxBytes)
                throw new ArgumentException("资源作用域限制只能收紧，不能放宽。", nameof(maxBytes));
            if (maxBytes < _currentBytes)
                throw new PrintResourceLimitException("PrintResourceScope.LimitTo",
                    _currentBytes, _currentBytes, maxBytes);

            _maxBytes = maxBytes;
        }
    }

    /// <summary>
    /// 在当前异步执行上下文建立一个资源预留作用域。
    /// 作用域结束时会恢复进入前的父作用域。
    /// </summary>
    /// <param name="maxBytes">此作用域允许的最大预留字节数，不能为负数。</param>
    /// <param name="cancellationToken">预留前检查的取消令牌。</param>
    public static PrintResourceScope Begin(long maxBytes, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));

        var parent = CurrentScope.Value;
        var root = parent == null ? new RootState() : parent._root;
        lock (root.Gate)
        {
            // 子作用域注册和父作用域释放共用锁，避免安装指向已释放父级的作用域。
            cancellationToken.ThrowIfCancellationRequested();
            for (var ancestor = parent; ancestor != null; ancestor = ancestor._parent)
            {
                if (ancestor._disposed)
                    throw new ObjectDisposedException(nameof(PrintResourceScope));
                ancestor._cancellationToken.ThrowIfCancellationRequested();
            }

            var scope = new PrintResourceScope(parent, root, maxBytes, cancellationToken);
            if (parent != null) parent._activeChildren++;
            CurrentScope.Value = scope;
            return scope;
        }
    }

    // 临时操作在失败或预检结束时归还全部预留；需要返回结果时显式提交到父级。
    internal static PrintResourceScope BeginTemporary()
        => CurrentScope.Value == null ? null : Begin(long.MaxValue);

    // 成功结果离开内部操作时，将租约归属移到父级；父级总数本来就包含子级，无需重复计费。
    // 失败路径不提交，Dispose 会释放整个操作中的预留。
    internal void CommitReservations()
    {
        lock (_root.Gate)
        {
            EnsureActiveLocked();
            if (!ReferenceEquals(CurrentScope.Value, this) || _activeChildren != 0)
                throw new InvalidOperationException("只能提交没有活动子作用域的当前打印资源作用域。");
            if (_parent == null) return;
            var retained = checked(_parent._retainedBytes + _retainedBytes);
            try
            {
                foreach (var reservation in _reservations)
                    _parent._reservations.Add(reservation);
            }
            catch
            {
                foreach (var reservation in _reservations)
                    _parent._reservations.Remove(reservation);
                throw;
            }
            foreach (var reservation in _reservations)
                reservation.MoveTo(_parent);
            _reservations.Clear();
            _parent._retainedBytes = retained;
            _retainedBytes = 0;
            _currentBytes = 0;
        }
    }

    /// <summary>
    /// 在当前作用域预留指定字节数。无当前作用域时返回无操作 Lease。
    /// </summary>
    /// <param name="bytes">预留字节数，不能为负数。</param>
    /// <param name="resourceName">资源用途名称，用于超限诊断。</param>
    /// <returns>释放预留的幂等 Lease。</returns>
    public static IDisposable Reserve(long bytes, string resourceName)
    {
        ValidateRequest(bytes, resourceName);
        var scope = CurrentScope.Value;
        if (scope == null) return NoopLease.Instance;
        return scope.ReserveCore(bytes, resourceName);
    }

    /// <summary>
    /// 在当前作用域保留指定字节数直到作用域结束，不返回需要调用方保存的对象。
    /// 无当前作用域时不执行任何记账。
    /// </summary>
    /// <param name="bytes">预留字节数，不能为负数。</param>
    /// <param name="resourceName">资源用途名称，用于超限诊断。</param>
    public static void Retain(long bytes, string resourceName)
    {
        ValidateRequest(bytes, resourceName);
        var scope = CurrentScope.Value;
        if (scope == null) return;
        scope.RetainCore(bytes, resourceName);
    }

    /// <summary>释放当前作用域及其自身的所有预留，并恢复父作用域；活动子作用域必须先结束。</summary>
    public void Dispose()
    {
        lock (_root.Gate)
        {
            if (_disposed) return;
            if (!ReferenceEquals(CurrentScope.Value, this))
                throw new InvalidOperationException("打印资源作用域必须按 LIFO 顺序释放当前作用域。");
            if (_activeChildren != 0)
                throw new InvalidOperationException("打印资源作用域仍有活动子作用域，必须等待子作用域结束后释放。");

            // 先释放所有普通 Lease 和 Retain 记账，再标记作用域已释放。
            // 这样 ReleaseChain 能安全地遍历仍然有效的父作用域。
            while (_reservations.Count != 0)
            {
                Reservation reservation = null;
                foreach (var item in _reservations)
                {
                    reservation = item;
                    break;
                }
                reservation.ReleaseLocked();
            }

            if (_retainedBytes != 0)
            {
                ReleaseChainLocked(this, _retainedBytes);
                _retainedBytes = 0;
            }

            _disposed = true;
            if (_parent != null) _parent._activeChildren--;
            CurrentScope.Value = _parent;
        }
    }

    private IDisposable ReserveCore(long bytes, string resourceName)
    {
        lock (_root.Gate)
        {
            EnsureActiveLocked();
            ValidateChainLocked(bytes, resourceName);
            if (bytes == 0) return NoopLease.Instance;

            var reservation = new Reservation(this, bytes);
            _reservations.Add(reservation);
            try
            {
                AddChainLocked(this, bytes);
                return reservation;
            }
            catch
            {
                _reservations.Remove(reservation);
                reservation.MarkReleasedLocked();
                throw;
            }
        }
    }

    private void RetainCore(long bytes, string resourceName)
    {
        lock (_root.Gate)
        {
            EnsureActiveLocked();
            ValidateChainLocked(bytes, resourceName);
            if (bytes == 0) return;

            AddChainLocked(this, bytes);
            _retainedBytes = checked(_retainedBytes + bytes);
        }
    }

    private void EnsureActiveLocked()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PrintResourceScope));
    }

    private void ValidateChainLocked(long bytes, string resourceName)
    {
        for (var scope = this; scope != null; scope = scope._parent)
        {
            if (scope._disposed)
                throw new ObjectDisposedException(nameof(PrintResourceScope));
            scope._cancellationToken.ThrowIfCancellationRequested();
            if (bytes > scope._maxBytes - scope._currentBytes)
                throw new PrintResourceLimitException(resourceName, bytes, scope._currentBytes, scope._maxBytes);
        }
    }

    private static void AddChainLocked(PrintResourceScope scope, long bytes)
    {
        for (var current = scope; current != null; current = current._parent)
        {
            current._currentBytes = checked(current._currentBytes + bytes);
            if (current._currentBytes > current._peakBytes)
                current._peakBytes = current._currentBytes;
        }
    }

    private static void ReleaseChainLocked(PrintResourceScope scope, long bytes)
    {
        for (var current = scope; current != null; current = current._parent)
        {
            // A parent can only be disposed independently if a copied execution
            // context is released out of order. Never let a late Lease make the
            // already released scope negative.
            if (current._disposed) continue;
            current._currentBytes = current._currentBytes >= bytes
                ? current._currentBytes - bytes
                : 0;
        }
    }

    private static void ValidateRequest(long bytes, string resourceName)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new ArgumentException("资源名称不能为空。", nameof(resourceName));
    }

    private sealed class RootState
    {
        public readonly object Gate = new object();
    }

    private sealed class Reservation : IDisposable
    {
        private PrintResourceScope _scope;
        private readonly long _bytes;
        private bool _released;

        public Reservation(PrintResourceScope scope, long bytes)
        {
            _scope = scope;
            _bytes = bytes;
        }

        public void Dispose()
        {
            lock (_scope._root.Gate)
                ReleaseLocked();
        }

        public void ReleaseLocked()
        {
            if (_released) return;
            _released = true;
            _scope._reservations.Remove(this);
            ReleaseChainLocked(_scope, _bytes);
        }

        public void MarkReleasedLocked()
        {
            _released = true;
        }

        public void MoveTo(PrintResourceScope scope) => _scope = scope;
    }

    private sealed class NoopLease : IDisposable
    {
        public static readonly NoopLease Instance = new NoopLease();

        public void Dispose()
        {
        }
    }
}

/// <summary>表示受管打印资源预留超过作用域限制。</summary>
public sealed class PrintResourceLimitException : InvalidOperationException
{
    /// <summary>触发预留的资源名称。</summary>
    public string ResourceName { get; }
    /// <summary>本次请求的预留字节数。</summary>
    public long RequestedBytes { get; }
    /// <summary>拒绝前当前作用域已预留的字节数。</summary>
    public long CurrentBytes { get; }
    /// <summary>拒绝发生的作用域最大字节数。</summary>
    public long MaxBytes { get; }

    /// <summary>创建资源预算超限异常。</summary>
    public PrintResourceLimitException(string resourceName, long requestedBytes, long currentBytes, long maxBytes)
        : base($"打印资源预留超过限制：{resourceName} 请求 {requestedBytes} 字节，当前 {currentBytes} 字节，限制 {maxBytes} 字节。")
    {
        ResourceName = resourceName;
        RequestedBytes = requestedBytes;
        CurrentBytes = currentBytes;
        MaxBytes = maxBytes;
    }
}
