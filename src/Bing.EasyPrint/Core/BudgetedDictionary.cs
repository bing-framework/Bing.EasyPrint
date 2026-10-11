using System;
using System.Collections;
using System.Collections.Generic;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Core;

/// <summary>
/// 为引擎索引预留容器和每次插入的保守存储估计，不计入已单独计费的值载荷。
/// 每项估计包含哈希槽、扩容余量和租约记录；移除不退还估计，直到 owner/作用域结束。
/// </summary>
internal sealed class BudgetedDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
{
    private readonly Dictionary<TKey, TValue> _items;
    private readonly string _name;
    private readonly PrintStorageBudget _owner;
    private readonly Func<TKey, TValue, long> _retainedSize;

    internal BudgetedDictionary(string name, PrintStorageBudget owner = null,
        IEqualityComparer<TKey> comparer = null, Func<TKey, TValue, long> retainedSize = null)
    {
        _name = name;
        _owner = owner;
        _retainedSize = retainedSize;
        using var operation = PrintResourceScope.BeginTemporary();
        PrintStorageBudget.Reserve(128, name + ".Container", owner);
        _items = new Dictionary<TKey, TValue>(comparer);
        operation?.CommitReservations();
    }

    public int Count => _items.Count;
    public TValue this[TKey key] => _items[key];
    public IEnumerable<TKey> Keys => _items.Keys;
    public IEnumerable<TValue> Values => _items.Values;
    public bool ContainsKey(TKey key) => _items.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => _items.TryGetValue(key, out value);

    internal void Add(TKey key, TValue value)
    {
        if (_items.ContainsKey(key)) throw new ArgumentException("索引中已存在相同键。", nameof(key));
        using var operation = PrintResourceScope.BeginTemporary();
        var bytes = checked(256L + (_retainedSize?.Invoke(key, value) ?? 0));
        PrintStorageBudget.Reserve(bytes, _name + ".Entry", _owner);
        _items.Add(key, value);
        operation?.CommitReservations();
    }

    internal bool TryAdd(TKey key, TValue value)
    {
        if (_items.ContainsKey(key)) return false;
        Add(key, value);
        return true;
    }

    internal bool Remove(TKey key) => _items.Remove(key);
    internal void Clear() => _items.Clear();
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
