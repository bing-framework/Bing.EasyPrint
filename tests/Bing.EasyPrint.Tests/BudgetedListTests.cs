using System;
using System.Collections.Generic;
using System.Linq;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证受预算管理集合的预留、失败回收、作用域归属和只读视图。</summary>
public sealed class BudgetedListTests
{
    [Fact]
    public void AddFactory_does_not_invoke_factory_when_record_budget_is_insufficient()
    {
        const long itemBytes = 11;
        const long retainedBytes = 7;
        var requestedBytes = itemBytes + retainedBytes;

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var list = new BudgetedList<int>("factory-admission", itemBytes: itemBytes);
            var before = scope.CurrentBytes;
            scope.LimitTo(before + requestedBytes - 1);
            var calls = 0;

            var error = Assert.Throws<PrintResourceLimitException>(() => list.AddFactory(
                () =>
                {
                    calls++;
                    return 1;
                }, retainedBytes));

            Assert.Equal("factory-admission.Record", error.ResourceName);
            Assert.Equal(requestedBytes, error.RequestedBytes);
            Assert.Equal(0, calls);
            Assert.Empty(list);
            Assert.Equal(before, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Factory_failure_during_growth_returns_the_new_reservation_and_keeps_old_state()
    {
        const int initialCapacity = 4;
        const int slotBytes = 13;

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var list = new BudgetedList<int>(
                "factory-failure", slotBytes: slotBytes, itemBytes: 0, capacity: initialCapacity);
            for (var index = 0; index < initialCapacity; index++) list.Add(index);

            var before = scope.CurrentBytes;
            var calls = 0;
            Func<int> factory = () =>
            {
                calls++;
                throw new InvalidOperationException("factory failed");
            };

            Assert.Throws<InvalidOperationException>(() => list.AddFactory(factory));

            Assert.Equal(1, calls);
            Assert.Equal(initialCapacity, list.Count);
            Assert.Equal(before, scope.CurrentBytes);

            // The failed growth must not poison the list or its capacity lease.
            list.Add(initialCapacity);
            Assert.Equal(initialCapacity + 1, list.Count);
        }
    }

    [Fact]
    public void Capacity_growth_from_four_to_eight_peaks_with_old_and_new_capacity()
    {
        const int initialCapacity = 4;
        const int nextCapacity = 8;
        const int slotBytes = 17;

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var list = new BudgetedList<int>(
                "capacity-peak", slotBytes: slotBytes, itemBytes: 0, capacity: initialCapacity);
            var beforeGrowth = scope.CurrentBytes;
            for (var index = 0; index < initialCapacity; index++) list.Add(index, retainedBytes: 0);

            list.Add(initialCapacity, retainedBytes: 0);

            Assert.Equal(initialCapacity + 1, list.Count);
            Assert.Equal(beforeGrowth + (long)nextCapacity * slotBytes, scope.PeakBytes);
            Assert.Equal(beforeGrowth + (long)(nextCapacity - initialCapacity) * slotBytes,
                scope.CurrentBytes);
        }
    }

    [Fact]
    public void Rejected_capacity_growth_keeps_state_and_retries_in_a_new_parent_scope()
    {
        const int initialCapacity = 4;
        const int nextCapacity = 8;
        const int slotBytes = 15;
        var requestedCapacityBytes = (long)nextCapacity * slotBytes;
        BudgetedList<int> list;

        using (var limitedScope = PrintResourceScope.Begin(long.MaxValue))
        {
            list = new BudgetedList<int>(
                "capacity-retry", slotBytes: slotBytes, itemBytes: 0, capacity: initialCapacity);
            for (var index = 0; index < initialCapacity; index++) list.Add(index);

            var before = limitedScope.CurrentBytes;
            limitedScope.LimitTo(before + requestedCapacityBytes - 1);

            var error = Assert.Throws<PrintResourceLimitException>(() => list.Add(initialCapacity));

            Assert.Equal(requestedCapacityBytes, error.RequestedBytes);
            Assert.Equal(initialCapacity, list.Count);
            Assert.Equal(before, limitedScope.CurrentBytes);
        }

        // The list survives its former parent; a fresh parent can fund the retry.
        using (var retryScope = PrintResourceScope.Begin(long.MaxValue))
        {
            list.Add(initialCapacity);

            Assert.Equal(initialCapacity + 1, list.Count);
            Assert.Equal(requestedCapacityBytes, retryScope.CurrentBytes);
        }
    }

    [Fact]
    public void Rejected_record_can_retry_in_a_remaining_capacity_slot_after_budget_is_released()
    {
        const long itemBytes = 12;
        const long occupiedBytes = 20;

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        using (var occupied = PrintResourceScope.Reserve(occupiedBytes, "test-occupied"))
        {
            var list = new BudgetedList<int>(
                "remaining-slot", slotBytes: 9, itemBytes: itemBytes, capacity: 4);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var before = scope.CurrentBytes;
            scope.LimitTo(before + itemBytes - 1);

            Assert.Throws<PrintResourceLimitException>(() => list.Add(4));
            Assert.Equal(3, list.Count);
            Assert.Equal(before, scope.CurrentBytes);

            occupied.Dispose();
            list.Add(4);

            Assert.Equal(4, list.Count);
            Assert.Equal(4, list[3]);
            Assert.Equal(before - occupiedBytes + itemBytes, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Owner_dispose_releases_all_list_leases_and_is_idempotent()
    {
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        using (PrintResourceScope.Reserve(19, "test-baseline"))
        {
            var before = scope.CurrentBytes;
            var owner = new PrintStorageBudget();
            var list = new BudgetedList<int>(
                "owner-release", slotBytes: 10, itemBytes: 6, owner: owner, capacity: 2);
            list.Add(1);
            list.Add(2);
            list.Add(3);
            Assert.True(scope.CurrentBytes > before);

            owner.Dispose();
            Assert.Equal(before, scope.CurrentBytes);

            owner.Dispose();
            Assert.Equal(before, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Child_scope_commit_moves_owner_leases_to_parent_for_later_release()
    {
        using (var parent = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = parent.CurrentBytes;
            var owner = new PrintStorageBudget();

            using (var child = PrintResourceScope.Begin(long.MaxValue))
            {
                var list = new BudgetedList<int>(
                    "child-owner", slotBytes: 11, itemBytes: 5, owner: owner, capacity: 1);
                list.Add(1);
                list.Add(2);
                Assert.True(child.CurrentBytes > 0);

                var held = parent.CurrentBytes;
                child.CommitReservations();

                Assert.Equal(0, child.CurrentBytes);
                Assert.Equal(held, parent.CurrentBytes);
            }

            Assert.True(parent.CurrentBytes > before);
            owner.Dispose();
            Assert.Equal(before, parent.CurrentBytes);

            owner.Dispose();
            Assert.Equal(before, parent.CurrentBytes);
        }
    }

    [Fact]
    public void Without_a_scope_the_view_stays_live_and_read_only()
    {
        var list = new BudgetedList<int>("view", capacity: 0);
        IReadOnlyList<int> view = list.AsReadOnly();

        Assert.Equal(0, view.Count);
        list.Add(1);
        Assert.Equal(new[] { 1 }, view.ToArray());

        list.Add(2);
        Assert.Equal(2, view.Count);
        Assert.Equal(2, view[1]);
        Assert.Equal(new[] { 1, 2 }, view.ToArray());
    }
}
