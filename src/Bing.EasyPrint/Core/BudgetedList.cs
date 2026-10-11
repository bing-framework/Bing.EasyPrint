using System;
using System.Collections;
using System.Collections.Generic;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Core;

/// <summary>在容量扩展和记录进入集合前预留显式估计；不追踪 GC 或调用方已有对象。</summary>
internal sealed class BudgetedList<T> : IReadOnlyList<T>
{
    private T[] _items;
    private int _count;
    private int _version;
    private readonly string _name;
    private readonly int _slotBytes;
    private readonly long _itemBytes;
    private readonly PrintStorageBudget _owner;
    private readonly Func<T, long> _retainedSize;
    private IDisposable _capacityLease;

    internal BudgetedList(string name, int slotBytes = 8, long itemBytes = 0,
        PrintStorageBudget owner = null, int capacity = 0, Func<T, long> retainedSize = null)
    {
        if (slotBytes <= 0 || itemBytes < 0 || capacity < 0) throw new ArgumentOutOfRangeException();
        _name = name;
        _slotBytes = slotBytes;
        _itemBytes = itemBytes;
        _owner = owner;
        _retainedSize = retainedSize;
        using var operation = PrintResourceScope.BeginTemporary();
        PrintStorageBudget.Reserve(96, name + ".Container", owner);
        _capacityLease = PrintStorageBudget.Reserve(checked((long)capacity * slotBytes), name + ".Capacity", owner);
        _items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        operation?.CommitReservations();
    }

    public int Count => _count;
    public T this[int index] => index >= 0 && index < _count ? _items[index] : throw new ArgumentOutOfRangeException(nameof(index));
    internal IReadOnlyList<T> AsReadOnly() => this;

    internal void Add(T item, long retainedBytes = 0) => AddFactory(() => item, retainedBytes);

    /// <summary>先预留记录成本，再创建对象；工厂或预算失败不会追加部分记录。</summary>
    internal void AddFactory(Func<T> create, long retainedBytes = 0, Action beforeCommit = null)
    {
        if (create == null) throw new ArgumentNullException(nameof(create));
        if (retainedBytes < 0) throw new ArgumentOutOfRangeException(nameof(retainedBytes));
        using var operation = PrintResourceScope.BeginTemporary();
        PrintStorageBudget.Reserve(checked(_itemBytes + retainedBytes), _name + ".Record", _owner);
        var required = checked(_count + 1);
        var target = _items;
        IDisposable nextLease = null;
        if (required > _items.Length)
        {
            var doubled = _items.Length <= int.MaxValue / 2 ? _items.Length * 2 : int.MaxValue;
            var next = Math.Max(required, Math.Max(4, doubled));
            nextLease = PrintStorageBudget.Reserve(checked((long)next * _slotBytes), _name + ".Capacity", _owner);
            target = new T[next];
            Array.Copy(_items, target, _count);
        }
        var item = create();
        if (_retainedSize != null)
            PrintStorageBudget.Reserve(_retainedSize(item), _name + ".Data", _owner);
        beforeCommit?.Invoke();
        target[_count] = item;
        if (nextLease != null)
        {
            _capacityLease.Dispose();
            _capacityLease = nextLease;
            _items = target;
        }
        _count = required;
        _version++;
        operation?.CommitReservations();
    }

    internal void AddRange(IEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        foreach (var item in items) Add(item);
    }

    internal void Clear()
    {
        Array.Clear(_items, 0, _count);
        _count = 0;
        _version++;
        // 记录估计仍保留至 owner/作用域结束；容量继续复用，不追踪 GC。
    }

    public IEnumerator<T> GetEnumerator()
    {
        var version = _version;
        for (var index = 0; index < _count; index++)
        {
            if (version != _version) throw new InvalidOperationException("枚举期间集合不能修改。");
            yield return _items[index];
        }
        if (version != _version) throw new InvalidOperationException("枚举期间集合不能修改。");
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
