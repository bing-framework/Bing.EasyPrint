using System.Linq;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint
{
    /// <summary>
    /// 缓冲区写入器(<see cref="IBufferWriter"/>) 扩展
    /// </summary>
    public static class BufferWriterExtensions
    {
        /// <summary>
        /// 转换为16进制
        /// </summary>
        /// <param name="bufferWriter">缓冲区写入器</param>
        public static string ToHex(this IBufferWriter bufferWriter)
        {
            var bytes = bufferWriter.GetBytes();
            if (!bytes.Any())
                return string.Empty;
            var result = new StringBuilder();
            foreach (var b in bytes)
                result.AppendFormat("{0:x2}", b);
            return result.Replace("-", "").ToString();
        }

    }
}
