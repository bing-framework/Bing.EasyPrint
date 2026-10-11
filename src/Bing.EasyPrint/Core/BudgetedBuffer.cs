using System;
using System.Threading;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Core;

/// <summary>分配前预留受管缓冲区；返回数据的预留保留到当前资源作用域结束。</summary>
internal static class BudgetedBuffer
{
    internal static byte[] Allocate(int length, string resourceName)
    {
        var reservation = PrintResourceScope.Reserve(length, resourceName);
        try { return new byte[length]; }
        catch { reservation.Dispose(); throw; }
    }

    /// <summary>
    /// 预留并创建缓冲区，在填充失败或取消时立即归还预留。
    /// 成功返回后，预留由当前资源作用域统一释放，不追踪数组的 GC 生命周期。
    /// </summary>
    internal static byte[] Create(int length, string resourceName, Action<byte[]> fill)
    {
        if (fill == null) throw new ArgumentNullException(nameof(fill));
        var reservation = PrintResourceScope.Reserve(length, resourceName);
        try
        {
            var result = new byte[length];
            fill(result);
            return result;
        }
        catch
        {
            reservation.Dispose();
            throw;
        }
    }

    internal static byte[] Copy(byte[] source, string resourceName, CancellationToken cancellationToken)
        => Copy(source, 0, source.Length, resourceName, cancellationToken);

    internal static byte[] Copy(byte[] source, int sourceOffset, int count, string resourceName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reservation = PrintResourceScope.Reserve(count, resourceName);
        try
        {
            var result = new byte[count];
            BufferCopy.Copy(source, sourceOffset, result, 0, count, cancellationToken);
            return result;
        }
        catch { reservation.Dispose(); throw; }
    }
}
