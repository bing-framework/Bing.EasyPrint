using Bing.EasyPrint.Printing;
using Xunit;

public sealed class HostAdmissionTests
{
    [Fact]
    public void Invalid_admission_configurations_are_rejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => new DesignerRequestAdmission(null, null, null));
        Assert.ThrowsAny<ArgumentException>(() => new DesignerRequestAdmission(null, 100, null));
        Assert.ThrowsAny<ArgumentException>(() => new DesignerRequestAdmission(100, null, null));
        Assert.ThrowsAny<ArgumentException>(() => new DesignerRequestAdmission(-1, null, 1));
        Assert.ThrowsAny<ArgumentException>(() => new DesignerRequestAdmission(1, -1, null));
        Assert.ThrowsAny<ArgumentException>(() => new DesignerRequestAdmission(1, null, 0));
        Assert.ThrowsAny<ArgumentException>(() => new DesignerRequestAdmission(1, null, 1, -1));
    }

    [Fact]
    public async Task Total_budget_reserves_the_full_request_budget_and_tracks_peak_parallelism()
    {
        var admission = new DesignerRequestAdmission(100, 200, null);
        Assert.Equal<long?>(100, admission.RequestMaxWorkingBytes);

        var first = await admission.EnterAsync(CancellationToken.None);
        AssertSnapshot(admission, active: 1, queued: 0, reserved: 100, parallelism: 1,
            maxQueued: 0, peak: 100);

        var second = await admission.EnterAsync(CancellationToken.None);
        AssertSnapshot(admission, active: 2, queued: 0, reserved: 200, parallelism: 2,
            maxQueued: 0, peak: 200);

        first.Dispose();
        Assert.Equal(100L, admission.Snapshot.ReservedWorkingBytes);
        second.Dispose();
        AssertSnapshot(admission, active: 0, queued: 0, reserved: 0, parallelism: 2,
            maxQueued: 0, peak: 200);
    }

    [Fact]
    public async Task Concurrency_only_admission_does_not_reserve_working_bytes()
    {
        var admission = new DesignerRequestAdmission(128, null, 1);
        var lease = await admission.EnterAsync(CancellationToken.None);

        AssertSnapshot(admission, active: 1, queued: 0, reserved: 0, parallelism: 1,
            maxQueued: 0, peak: 0);

        var rejected = await RejectAsync(admission, CancellationToken.None);
        Assert.Equal("QueueFull", rejected.Reason);
        Assert.Equal(1L, admission.Snapshot.RejectedRequests);

        lease.Dispose();
        Assert.Equal(0, admission.Snapshot.ActiveRequests);
        Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
    }

    [Fact]
    public async Task Total_and_concurrency_limits_reject_independently()
    {
        var totalLimited = new DesignerRequestAdmission(100, 150, null);
        var totalLease = await totalLimited.EnterAsync(CancellationToken.None);
        var totalRejected = await RejectAsync(totalLimited, CancellationToken.None);
        Assert.Equal("QueueFull", totalRejected.Reason);
        totalLease.Dispose();

        var concurrencyLimited = new DesignerRequestAdmission(100, 1000, 1);
        var concurrencyLease = await concurrencyLimited.EnterAsync(CancellationToken.None);
        var concurrencyRejected = await RejectAsync(concurrencyLimited, CancellationToken.None);
        Assert.Equal("QueueFull", concurrencyRejected.Reason);
        concurrencyLease.Dispose();

        Assert.Equal(0, totalLimited.Snapshot.ActiveRequests);
        Assert.Equal(0L, totalLimited.Snapshot.ReservedWorkingBytes);
        Assert.Equal(0, concurrencyLimited.Snapshot.ActiveRequests);
        Assert.Equal(0L, concurrencyLimited.Snapshot.ReservedWorkingBytes);
    }

    [Fact]
    public async Task Request_budget_exceeding_total_is_rejected_before_admission()
    {
        var admission = new DesignerRequestAdmission(101, 100, null);

        var rejected = await RejectAsync(admission, CancellationToken.None);

        Assert.Equal("RequestBudgetExceedsTotal", rejected.Reason);
        Assert.Equal(1L, admission.Snapshot.RejectedRequests);
        Assert.Equal(0, admission.Snapshot.ActiveRequests);
        Assert.Equal(0, admission.Snapshot.QueuedRequests);
        Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
    }

    [Fact]
    public async Task Zero_total_rejects_a_positive_request_budget_without_leaking()
    {
        var admission = new DesignerRequestAdmission(1, 0, null);

        var rejected = await RejectAsync(admission, CancellationToken.None);

        Assert.Equal("RequestBudgetExceedsTotal", rejected.Reason);
        Assert.Equal(0, admission.Snapshot.ActiveRequests);
        Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
    }

    [Fact]
    public async Task Queued_requests_are_admitted_in_fifo_order()
    {
        var admission = new DesignerRequestAdmission(100, null, 1, 2);
        var first = await admission.EnterAsync(CancellationToken.None);
        using var secondCancellation = new CancellationTokenSource();
        using var thirdCancellation = new CancellationTokenSource();
        var secondTask = admission.EnterAsync(secondCancellation.Token).AsTask();
        var thirdTask = admission.EnterAsync(thirdCancellation.Token).AsTask();

        try
        {
            Assert.Equal(2, admission.Snapshot.QueuedRequests);
            Assert.Equal(2, admission.Snapshot.MaxQueuedRequests);

            first.Dispose();
            var second = await secondTask;
            Assert.False(thirdTask.IsCompleted);
            Assert.Equal(1, admission.Snapshot.ActiveRequests);
            second.Dispose();

            var third = await thirdTask;
            third.Dispose();
            Assert.Equal(0, admission.Snapshot.ActiveRequests);
            Assert.Equal(0, admission.Snapshot.QueuedRequests);
        }
        finally
        {
            first.Dispose();
            await CancelAndDrainAsync(secondTask, secondCancellation);
            await CancelAndDrainAsync(thirdTask, thirdCancellation);
        }
    }

    [Fact]
    public async Task Queue_full_is_rejected_with_queue_full_reason()
    {
        var admission = new DesignerRequestAdmission(100, null, 1, 1);
        var active = await admission.EnterAsync(CancellationToken.None);
        using var queuedCancellation = new CancellationTokenSource();
        var queuedTask = admission.EnterAsync(queuedCancellation.Token).AsTask();

        try
        {
            Assert.Equal(1, admission.Snapshot.QueuedRequests);
            var rejected = await RejectAsync(admission, CancellationToken.None);
            Assert.Equal("QueueFull", rejected.Reason);
            Assert.Equal(1L, admission.Snapshot.RejectedRequests);
        }
        finally
        {
            active.Dispose();
            await CancelAndDrainAsync(queuedTask, queuedCancellation);
        }
    }

    [Fact]
    public async Task Cancelling_a_queued_request_removes_it_and_preserves_active_reservation()
    {
        var admission = new DesignerRequestAdmission(100, 100, 1, 1);
        var active = await admission.EnterAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var queued = admission.EnterAsync(cancellation.Token).AsTask();

        try
        {
            Assert.Equal(1, admission.Snapshot.QueuedRequests);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await queued);

            Assert.Equal(1, admission.Snapshot.ActiveRequests);
            Assert.Equal(0, admission.Snapshot.QueuedRequests);
            Assert.Equal(1L, admission.Snapshot.CancelledQueuedRequests);
            Assert.Equal(100L, admission.Snapshot.ReservedWorkingBytes);
        }
        finally
        {
            active.Dispose();
            await CancelAndDrainAsync(queued, cancellation);
        }

        Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
    }

    [Fact]
    public async Task Cancelling_the_queue_head_allows_the_next_request_to_proceed()
    {
        var admission = new DesignerRequestAdmission(100, 100, 1, 2);
        var active = await admission.EnterAsync(CancellationToken.None);
        using var headCancellation = new CancellationTokenSource();
        using var nextCancellation = new CancellationTokenSource();
        var head = admission.EnterAsync(headCancellation.Token).AsTask();
        var next = admission.EnterAsync(nextCancellation.Token).AsTask();

        try
        {
            headCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await head);
            Assert.Equal(1, admission.Snapshot.QueuedRequests);

            active.Dispose();
            var nextLease = await next;
            Assert.Equal(1, admission.Snapshot.ActiveRequests);
            nextLease.Dispose();
        }
        finally
        {
            active.Dispose();
            await CancelAndDrainAsync(head, headCancellation);
            await CancelAndDrainAsync(next, nextCancellation);
        }

        Assert.Equal(0, admission.Snapshot.ActiveRequests);
        Assert.Equal(0, admission.Snapshot.QueuedRequests);
        Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
    }

    [Fact]
    public async Task Pre_cancelled_tokens_are_transparent_and_do_not_enter_queue()
    {
        var admission = new DesignerRequestAdmission(100, 100, 1, 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await admission.EnterAsync(cancellation.Token));

        Assert.Equal(0, admission.Snapshot.ActiveRequests);
        Assert.Equal(0, admission.Snapshot.QueuedRequests);
        Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
    }

    [Fact]
    public async Task Lease_dispose_is_idempotent_and_releases_reservation_once()
    {
        var admission = new DesignerRequestAdmission(100, 100, 1);
        var lease = await admission.EnterAsync(CancellationToken.None);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(0, admission.Snapshot.ActiveRequests);
        Assert.Equal(0, admission.Snapshot.QueuedRequests);
        Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
        Assert.Equal(100L, admission.Snapshot.PeakReservedWorkingBytes);
    }

    [Fact]
    public async Task Concurrent_release_and_cancellation_leave_no_admission_leaks()
    {
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var admission = new DesignerRequestAdmission(100, 200, 2, 2);
            var first = await admission.EnterAsync(CancellationToken.None);
            var second = await admission.EnterAsync(CancellationToken.None);
            using var cancellation = new CancellationTokenSource();
            var cancelled = admission.EnterAsync(cancellation.Token).AsTask();
            var queued = admission.EnterAsync(CancellationToken.None).AsTask();

            await Task.WhenAll(
                Task.Run(() => cancellation.Cancel()),
                Task.Run(() => first.Dispose()),
                Task.Run(() => second.Dispose()));

            await DrainAsync(cancelled);
            await DrainAsync(queued);

            Assert.Equal(0, admission.Snapshot.ActiveRequests);
            Assert.Equal(0, admission.Snapshot.QueuedRequests);
            Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
            Assert.Equal(2, admission.Snapshot.MaxActualParallelism);
        }
    }

    private static async Task<DesignerAdmissionRejectedException> RejectAsync(
        DesignerRequestAdmission admission, CancellationToken cancellationToken)
        => await Assert.ThrowsAsync<DesignerAdmissionRejectedException>(async () =>
            await admission.EnterAsync(cancellationToken));

    private static async Task CancelAndDrainAsync(
        Task<IDisposable> pending, CancellationTokenSource cancellation)
    {
        if (!pending.IsCompleted)
            cancellation.Cancel();
        await DrainAsync(pending);
    }

    private static async Task DrainAsync(Task<IDisposable> pending)
    {
        try
        {
            var lease = await pending;
            lease.Dispose();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void AssertSnapshot(DesignerRequestAdmission admission,
        int active, int queued, long reserved, int parallelism, int maxQueued, long peak)
    {
        var snapshot = admission.Snapshot;
        Assert.Equal(active, snapshot.ActiveRequests);
        Assert.Equal(queued, snapshot.QueuedRequests);
        Assert.Equal(reserved, snapshot.ReservedWorkingBytes);
        Assert.Equal(parallelism, snapshot.MaxActualParallelism);
        Assert.Equal(maxQueued, snapshot.MaxQueuedRequests);
        Assert.Equal(peak, snapshot.PeakReservedWorkingBytes);
    }
}
