using System;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class BudgetedDictionaryTests
{
    [Fact]
    public void Rejected_entry_preserves_index_and_budget_and_can_retry_after_release()
    {
        using var scope = PrintResourceScope.Begin(long.MaxValue);
        using var owner = new PrintStorageBudget();
        var index = new BudgetedDictionary<string, object>("test-index", owner, StringComparer.Ordinal,
            (key, _) => PrintStorageBudget.TextBytes(key));
        index.Add("old", new object());
        var before = scope.CurrentBytes;
        using var occupied = PrintResourceScope.Reserve(1000, "test-occupied");
        scope.LimitTo(scope.CurrentBytes);
        var error = Assert.Throws<PrintResourceLimitException>(() => index.Add("new", new object()));
        Assert.Equal("test-index.Entry", error.ResourceName);
        Assert.Single(index);
        Assert.False(index.ContainsKey("new"));
        Assert.Equal(before + 1000, scope.CurrentBytes);
        occupied.Dispose();
        index.Add("new", new object());
        Assert.True(index.TryGetValue("new", out _));
        owner.Dispose();
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Fact]
    public void Duplicate_keys_do_not_reserve_and_cleared_capacity_remains_until_owner_ends()
    {
        using var scope = PrintResourceScope.Begin(long.MaxValue);
        using var owner = new PrintStorageBudget();
        var index = new BudgetedDictionary<string, int>("test-index", owner, StringComparer.OrdinalIgnoreCase);
        index.Add("KEY", 1);
        var retained = scope.CurrentBytes;
        Assert.False(index.TryAdd("key", 2));
        Assert.Throws<ArgumentException>(() => index.Add("key", 2));
        Assert.Equal(1, index["key"]);
        Assert.Equal(retained, scope.CurrentBytes);
        index.Clear();
        Assert.Empty(index);
        Assert.Equal(retained, scope.CurrentBytes);
        owner.Dispose();
        Assert.Equal(0, scope.CurrentBytes);
    }
}
