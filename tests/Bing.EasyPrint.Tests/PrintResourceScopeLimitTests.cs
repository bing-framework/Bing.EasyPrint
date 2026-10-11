using System;
using System.Threading;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证打印资源作用域运行时收紧限制的原子性和边界行为。</summary>
public sealed class PrintResourceScopeLimitTests
{
    [Fact]
    public void Tightening_counts_existing_leases_and_rejects_new_reservations()
    {
        using (var scope = PrintResourceScope.Begin(20))
        using (PrintResourceScope.Reserve(6, "existing"))
        {
            scope.LimitTo(8);

            Assert.Equal(8, scope.MaxBytes);
            var error = Assert.Throws<PrintResourceLimitException>(() =>
                PrintResourceScope.Reserve(3, "after-limit"));

            Assert.Equal("after-limit", error.ResourceName);
            Assert.Equal(3, error.RequestedBytes);
            Assert.Equal(6, error.CurrentBytes);
            Assert.Equal(8, error.MaxBytes);
            Assert.Equal(6, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Failed_limit_changes_preserve_the_previous_limit()
    {
        using (var scope = PrintResourceScope.Begin(20))
        using (PrintResourceScope.Reserve(6, "existing"))
        {
            scope.LimitTo(10);

            Assert.Throws<ArgumentException>(() => scope.LimitTo(11));
            Assert.Equal(10, scope.MaxBytes);

            var error = Assert.Throws<PrintResourceLimitException>(() => scope.LimitTo(5));
            Assert.Equal("PrintResourceScope.LimitTo", error.ResourceName);
            Assert.Equal(6, error.RequestedBytes);
            Assert.Equal(6, error.CurrentBytes);
            Assert.Equal(5, error.MaxBytes);
            Assert.Equal(10, scope.MaxBytes);
        }
    }

    [Fact]
    public void Parent_and_child_scopes_apply_their_current_limits_independently()
    {
        using (var parent = PrintResourceScope.Begin(20))
        using (var child = PrintResourceScope.Begin(20))
        using (PrintResourceScope.Reserve(4, "child"))
        {
            parent.LimitTo(4);
            Assert.Equal(4, parent.CurrentBytes);
            Assert.Equal(4, child.CurrentBytes);
            Assert.Equal(20, child.MaxBytes);

            var error = Assert.Throws<PrintResourceLimitException>(() =>
                PrintResourceScope.Reserve(1, "child-after-parent-limit"));
            Assert.Equal(4, error.CurrentBytes);
            Assert.Equal(4, error.MaxBytes);

            child.LimitTo(4);
            Assert.Equal(4, child.MaxBytes);
            Assert.Equal(4, parent.MaxBytes);
        }
    }

    [Fact]
    public void Negative_limits_and_cancelled_or_disposed_scopes_do_not_change_state()
    {
        using (var cancellation = new CancellationTokenSource())
        using (var scope = PrintResourceScope.Begin(20, cancellation.Token))
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => scope.LimitTo(-1));
            Assert.Equal(20, scope.MaxBytes);

            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => scope.LimitTo(10));
            Assert.Equal(20, scope.MaxBytes);
        }

        var disposed = PrintResourceScope.Begin(20);
        disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => disposed.LimitTo(10));
        Assert.Equal(20, disposed.MaxBytes);
    }
}
