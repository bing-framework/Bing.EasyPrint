using System;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp;

namespace Bing.EasyPrint.Core;

/// <summary>
/// 在图片解码前验证 TIFF 目录链的结构边界。
/// </summary>
/// <remarks>
/// 不解析像素或替代解码器。目录偏移递增及数量限制与现有 ImageSharp 解码器一致，
/// 以常量额外空间阻止截断 BigTIFF 条目数量引发的无进展循环。
/// </remarks>
internal static class TiffStructureValidator
{
    /// <summary>
    /// 检查从当前位置开始的编码图片；非 TIFF 数据交由原解码器处理。
    /// </summary>
    /// <param name="stream">可读、可定位的图片流；调用后恢复位置且不释放流。</param>
    /// <param name="cancellationToken">目录遍历及读取时观察的取消令牌。</param>
    /// <exception cref="InvalidImageContentException">TIFF 头部或目录链截断、越界或循环。</exception>
    /// <exception cref="OperationCanceledException">检查已取消。</exception>
    internal static void Validate(Stream stream, CancellationToken cancellationToken = default)
    {
        var origin = stream.Position;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = (ulong)(stream.Length - origin);
            if (length < 4) return;
            var first = stream.ReadByte();
            var second = stream.ReadByte();
            if (!((first == 0x49 && second == 0x49) || (first == 0x4d && second == 0x4d))) return;
            var littleEndian = first == 0x49;
            var magic = ReadUnsigned(stream, 2, littleEndian, cancellationToken);
            if (magic != 42 && magic != 43) return;

            var big = magic == 43;
            var headerSize = big ? 16UL : 8UL;
            if (length < headerSize) throw InvalidStructure();
            if (big && (ReadUnsigned(stream, 2, littleEndian, cancellationToken) != 8 ||
                        ReadUnsigned(stream, 2, littleEndian, cancellationToken) != 0))
                throw InvalidStructure();
            var offsetSize = big ? 8 : 4;
            var countSize = big ? 8UL : 2UL;
            var entrySize = big ? 20UL : 12UL;
            var offset = ReadUnsigned(stream, offsetSize, littleEndian, cancellationToken);
            var directories = 0;
            while (offset != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (offset < headerSize || offset > length || length - offset < countSize + (ulong)offsetSize)
                    throw InvalidStructure();
                stream.Position = origin + (long)offset;
                var count = ReadUnsigned(stream, (int)countSize, littleEndian, cancellationToken);
                // 先除后乘，避免攻击者控制的 64 位 count 溢出或驱动无界循环。
                var available = length - offset - countSize - (ulong)offsetSize;
                if (count > available / entrySize) throw InvalidStructure();
                stream.Position = origin + (long)(offset + countSize + count * entrySize);
                var next = ReadUnsigned(stream, offsetSize, littleEndian, cancellationToken);
                if (next != 0 && next <= offset) throw InvalidStructure();
                if (++directories >= 65534) throw InvalidStructure();
                offset = next;
            }
        }
        finally
        {
            stream.Position = origin;
        }
    }

    /// <summary>
    /// 按指定字节序读取无符号整数，拒绝短读。
    /// </summary>
    /// <param name="stream">当前图片流。</param>
    /// <param name="size">整数的字节数。</param>
    /// <param name="littleEndian">是否使用小端序。</param>
    /// <param name="cancellationToken">读取取消令牌。</param>
    /// <returns>读取的无符号整数。</returns>
    private static ulong ReadUnsigned(Stream stream, int size, bool littleEndian, CancellationToken cancellationToken)
    {
        ulong result = 0;
        for (var index = 0; index < size; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = stream.ReadByte();
            if (value < 0) throw InvalidStructure();
            result |= (ulong)value << (8 * (littleEndian ? index : size - index - 1));
        }
        return result;
    }

    /// <summary>
    /// 创建可由现有图片错误映射识别的结构异常。
    /// </summary>
    /// <returns>图片内容异常。</returns>
    private static InvalidImageContentException InvalidStructure()
        => new InvalidImageContentException("TIFF 图片头部或目录无效：存在截断、越界、循环或过多目录。");
}
