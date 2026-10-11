using System;
using System.Threading;

namespace Bing.EasyPrint.Core;

/// <summary>在可取消复制中按固定大小检查令牌，未启用取消时保留整块复制。</summary>
internal static class BufferCopy
{
    internal static void Copy(byte[] source, int sourceOffset, byte[] target, int targetOffset,
        int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled)
        {
            Buffer.BlockCopy(source, sourceOffset, target, targetOffset, count);
            return;
        }
        const int blockSize = 64 * 1024;
        for (var copied = 0; copied < count;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var block = Math.Min(blockSize, count - copied);
            Buffer.BlockCopy(source, sourceOffset + copied, target, targetOffset + copied, block);
            copied += block;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
