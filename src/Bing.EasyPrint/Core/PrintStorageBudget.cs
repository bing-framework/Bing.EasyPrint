using System;
using System.Collections.Generic;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Core;

/// <summary>管理引擎自有模型的显式存储估计；可在临时布局结束时提前归还租约。</summary>
internal sealed class PrintStorageBudget : IDisposable
{
    private readonly List<IDisposable> _leases = new List<IDisposable>();

    internal static long TextBytes(string text) => text == null ? 0 : checked(24L + text.Length * 2L);

    internal static IDisposable Reserve(long bytes, string name, PrintStorageBudget owner = null)
    {
        var lease = PrintResourceScope.Reserve(bytes, name);
        try
        {
            if (PrintResourceScope.HasCurrentScope) owner?._leases.Add(lease);
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    public void Dispose()
    {
        foreach (var lease in _leases) lease.Dispose();
        _leases.Clear();
    }
}
