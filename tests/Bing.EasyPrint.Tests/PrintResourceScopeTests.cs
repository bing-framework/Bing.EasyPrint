using System;
using System.Threading;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证打印资源预算作用域的原子预留、嵌套限制和释放语义。</summary>
public sealed class PrintResourceScopeTests
{
    [Fact]
    public void Failed_reservation_does_not_pollute_current_or_peak_bytes()
    {
        using (var scope = PrintResourceScope.Begin(10))
        using (PrintResourceScope.Reserve(7, "image"))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() =>
                PrintResourceScope.Reserve(4, "raster"));

            Assert.Equal("raster", error.ResourceName);
            Assert.Equal(4, error.RequestedBytes);
            Assert.Equal(7, error.CurrentBytes);
            Assert.Equal(10, error.MaxBytes);
            Assert.Equal(7, scope.CurrentBytes);
            Assert.Equal(7, scope.PeakBytes);
        }
    }

    [Fact]
    public void Lease_release_is_idempotent_and_peak_is_retained()
    {
        using (var scope = PrintResourceScope.Begin(20))
        {
            var lease = PrintResourceScope.Reserve(12, "buffer");
            Assert.Equal(12, scope.CurrentBytes);
            Assert.Equal(12, scope.PeakBytes);

            lease.Dispose();
            lease.Dispose();

            Assert.Equal(0, scope.CurrentBytes);
            Assert.Equal(12, scope.PeakBytes);
            using (PrintResourceScope.Reserve(5, "buffer-2"))
            {
                Assert.Equal(5, scope.CurrentBytes);
                Assert.Equal(12, scope.PeakBytes);
            }
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Nested_reservation_counts_against_every_parent_scope()
    {
        using (var parent = PrintResourceScope.Begin(10))
        using (var child = PrintResourceScope.Begin(100))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() =>
                PrintResourceScope.Reserve(11, "child-image"));
            Assert.Equal(10, error.MaxBytes);
            Assert.Equal(0, parent.CurrentBytes);
            Assert.Equal(0, child.CurrentBytes);

            using (PrintResourceScope.Reserve(6, "child-image"))
            {
                Assert.Equal(6, parent.CurrentBytes);
                Assert.Equal(6, child.CurrentBytes);
            }

            Assert.Equal(0, parent.CurrentBytes);
            Assert.Equal(0, child.CurrentBytes);
        }
    }

    [Fact]
    public void Cancellation_is_checked_for_each_scope_without_changing_counts()
    {
        using (var parentCancellation = new CancellationTokenSource())
        using (var childCancellation = new CancellationTokenSource())
        using (var parent = PrintResourceScope.Begin(20, parentCancellation.Token))
        using (var child = PrintResourceScope.Begin(20, childCancellation.Token))
        {
            childCancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                PrintResourceScope.Reserve(1, "cancelled-by-child"));
            Assert.Equal(0, parent.CurrentBytes);
            Assert.Equal(0, child.CurrentBytes);

            parentCancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                PrintResourceScope.Reserve(1, "cancelled-by-parent"));
            Assert.Equal(0, parent.CurrentBytes);
            Assert.Equal(0, child.CurrentBytes);
        }
    }

    [Fact]
    public void Begin_rejects_pre_cancelled_tokens_without_installing_a_scope()
    {
        using (var parent = PrintResourceScope.Begin(10))
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                PrintResourceScope.Begin(100, cancellation.Token));

            // The failed child Begin must leave the parent as the current scope.
            using (PrintResourceScope.Reserve(10, "parent"))
                Assert.Equal(10, parent.CurrentBytes);
        }
    }

    [Fact]
    public void Begin_checks_cancelled_parent_token_without_replacing_the_parent_scope()
    {
        using (var cancellation = new CancellationTokenSource())
        using (var parent = PrintResourceScope.Begin(10, cancellation.Token))
        {
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => PrintResourceScope.Begin(100));
            Assert.Equal(0, parent.CurrentBytes);
        }
    }

    [Fact]
    public void Scope_disposal_restores_parent_and_late_lease_cannot_make_counts_negative()
    {
        var parent = PrintResourceScope.Begin(20);
        var parentLease = PrintResourceScope.Reserve(4, "parent");
        var child = PrintResourceScope.Begin(20);
        var childLease = PrintResourceScope.Reserve(6, "child");

        child.Dispose();
        Assert.Equal(4, parent.CurrentBytes);

        childLease.Dispose();
        Assert.Equal(4, parent.CurrentBytes);
        parentLease.Dispose();
        Assert.Equal(0, parent.CurrentBytes);

        parent.Dispose();
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public void Retain_is_released_at_scope_end_without_a_returned_lease()
    {
        var scope = PrintResourceScope.Begin(16);
        PrintResourceScope.Retain(9, "decoded-image");
        Assert.Equal(9, scope.CurrentBytes);
        Assert.Equal(9, scope.PeakBytes);

        scope.Dispose();
        Assert.Equal(0, scope.CurrentBytes);
        Assert.Equal(9, scope.PeakBytes);

        // No current scope: positive reservations are deliberately no-ops.
        using (PrintResourceScope.Reserve(100, "outside-scope"))
        {
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintResourceScope.Reserve(-1, "invalid"));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintResourceScope.Retain(-1, "invalid"));
    }

    [Fact]
    public void Parent_disposal_requires_lifo_order_and_child_scope_can_be_released_first()
    {
        var parent = PrintResourceScope.Begin(10);
        var child = PrintResourceScope.Begin(10);
        Assert.Throws<InvalidOperationException>(() => parent.Dispose());
        child.Dispose();
        child.Dispose();
        parent.Dispose();
        parent.Dispose();
    }
}
