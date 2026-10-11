using System.Globalization;

/// <summary>在读取请求前预留服务器工作额度，并限制并发及 FIFO 排队数量。</summary>
internal sealed class DesignerRequestAdmission
{
    private readonly object _gate = new();
    private readonly long? _maxTotalWorkingBytes;
    private readonly int _maxConcurrentRequests;
    private readonly int _maxQueuedRequests;
    private readonly long _requestReservation;
    private readonly LinkedList<Waiter> _waiters = new();
    private int _activeRequests;
    private long _reservedWorkingBytes;
    private int _peakActiveRequests;
    private int _peakQueuedRequests;
    private long _peakReservedWorkingBytes;
    private long _rejectedRequests;
    private long _cancelledQueuedRequests;

    internal DesignerRequestAdmission(long? requestMaxWorkingBytes, long? maxTotalWorkingBytes,
        int? maxConcurrentRequests, int maxQueuedRequests = 0)
    {
        if (requestMaxWorkingBytes < 0) throw new ArgumentOutOfRangeException(nameof(requestMaxWorkingBytes));
        if (maxTotalWorkingBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxTotalWorkingBytes));
        if (maxConcurrentRequests <= 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests));
        if (maxQueuedRequests < 0) throw new ArgumentOutOfRangeException(nameof(maxQueuedRequests));
        if (!maxTotalWorkingBytes.HasValue && !maxConcurrentRequests.HasValue)
            throw new ArgumentException("至少需要配置总工作额度或并发请求上限。");
        if (maxTotalWorkingBytes.HasValue && !requestMaxWorkingBytes.HasValue)
            throw new ArgumentException("总工作额度要求显式配置单请求工作额度。", nameof(requestMaxWorkingBytes));

        RequestMaxWorkingBytes = requestMaxWorkingBytes;
        _maxTotalWorkingBytes = maxTotalWorkingBytes;
        _maxConcurrentRequests = maxConcurrentRequests ?? int.MaxValue;
        _maxQueuedRequests = maxQueuedRequests;
        _requestReservation = maxTotalWorkingBytes.HasValue ? requestMaxWorkingBytes!.Value : 0;
    }

    internal long? RequestMaxWorkingBytes { get; }

    internal DesignerAdmissionSnapshot Snapshot
    {
        get
        {
            lock (_gate)
                return new(_activeRequests, _waiters.Count, _reservedWorkingBytes,
                    _peakActiveRequests, _peakQueuedRequests, _peakReservedWorkingBytes,
                    _rejectedRequests, _cancelledQueuedRequests);
        }
    }

    internal static DesignerRequestAdmission? FromEnvironment(long? requestMaxWorkingBytes)
    {
        var total = ReadNumber("EASYPRINT_DESIGNER_MAX_TOTAL_WORKING_BYTES", long.MaxValue, allowZero: true);
        var concurrent = ReadNumber("EASYPRINT_DESIGNER_MAX_CONCURRENT_REQUESTS", int.MaxValue, allowZero: false);
        var queued = ReadNumber("EASYPRINT_DESIGNER_MAX_QUEUED_REQUESTS", int.MaxValue, allowZero: true);
        if (!total.HasValue && !concurrent.HasValue)
        {
            if (queued.HasValue) throw new ArgumentException("排队上限需要启用总工作额度或并发请求上限。");
            return null;
        }
        return new(requestMaxWorkingBytes, total, (int?)concurrent, (int)(queued ?? 0));
    }

    internal ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Waiter waiter;
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_maxTotalWorkingBytes.HasValue && _requestReservation > _maxTotalWorkingBytes.Value)
                throw Reject("RequestBudgetExceedsTotal", "单请求工作额度超过服务器总工作额度，无法准入。");
            if (_waiters.Count == 0 && CanEnterLocked())
                return new(EnterLocked());
            if (_waiters.Count >= _maxQueuedRequests)
                throw Reject("QueueFull", "服务器工作额度或并发已满，等待队列无法接收更多请求。");

            waiter = new Waiter();
            waiter.Node = _waiters.AddLast(waiter);
            _peakQueuedRequests = Math.Max(_peakQueuedRequests, _waiters.Count);
        }
        return new(WaitAsync(waiter, cancellationToken));
    }

    private async Task<IDisposable> WaitAsync(Waiter waiter, CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => Cancel(waiter, cancellationToken));
        return await waiter.Completion.Task.ConfigureAwait(false);
    }

    private bool CanEnterLocked()
        => _activeRequests < _maxConcurrentRequests && (!_maxTotalWorkingBytes.HasValue ||
            _reservedWorkingBytes <= _maxTotalWorkingBytes.Value - _requestReservation);

    private IDisposable EnterLocked()
    {
        var lease = new Lease(this);
        _activeRequests++;
        _reservedWorkingBytes += _requestReservation;
        _peakActiveRequests = Math.Max(_peakActiveRequests, _activeRequests);
        _peakReservedWorkingBytes = Math.Max(_peakReservedWorkingBytes, _reservedWorkingBytes);
        return lease;
    }

    private DesignerAdmissionRejectedException Reject(string reason, string message)
    {
        _rejectedRequests++;
        return new(reason, message);
    }

    private void Cancel(Waiter waiter, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            // 已准入的请求由其处理路径观察取消，不能从回调释放正在使用的额度。
            if (waiter.Node == null) return;
            _waiters.Remove(waiter.Node);
            waiter.Node = null;
            _cancelledQueuedRequests++;
            waiter.Completion.TrySetCanceled(cancellationToken);
            DrainLocked();
        }
    }

    private void Release()
    {
        lock (_gate)
        {
            _activeRequests--;
            _reservedWorkingBytes -= _requestReservation;
            DrainLocked();
        }
    }

    private void DrainLocked()
    {
        while (_waiters.First != null && CanEnterLocked())
        {
            var waiter = _waiters.First.Value;
            _waiters.RemoveFirst();
            waiter.Node = null;
            waiter.Completion.SetResult(EnterLocked());
        }
    }

    private static long? ReadNumber(string name, long maximum, bool allowZero)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value)) return null;
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
            number < (allowZero ? 0 : 1) || number > maximum)
            throw new ArgumentException($"{name} 必须为{(allowZero ? "非负" : "正")}整数，且不超过 {maximum}。");
        return number;
    }

    private sealed class Waiter
    {
        internal LinkedListNode<Waiter>? Node;
        internal TaskCompletionSource<IDisposable> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Lease(DesignerRequestAdmission owner) : IDisposable
    {
        private DesignerRequestAdmission? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }
}

internal sealed record DesignerAdmissionSnapshot(int ActiveRequests, int QueuedRequests,
    long ReservedWorkingBytes, int MaxActualParallelism, int MaxQueuedRequests,
    long PeakReservedWorkingBytes, long RejectedRequests, long CancelledQueuedRequests);

internal sealed class DesignerAdmissionRejectedException(string reason, string message) : Exception(message)
{
    internal string Reason { get; } = reason;
}
