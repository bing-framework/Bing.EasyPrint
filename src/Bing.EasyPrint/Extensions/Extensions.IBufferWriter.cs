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
        /// <typeparam name="T">类型</typeparam>
        /// <param name="writer">缓冲区写入器</param>
        public static string ToHex<T>(this IBufferWriter<T> writer)
        {
            var bytes = writer.GetBytes();
            if (!bytes.Any())
                return string.Empty;
            var result = new StringBuilder();
            foreach (var b in bytes)
                result.AppendFormat("{0:x2}", b);
            return result.Replace("-", "").ToString();
        }

        /// <summary>
        /// 写入并换行
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="writer">缓冲区写入器</param>
        /// <param name="value">字符串</param>
        public static T WriteLine<T>(this IBufferWriter<T> writer, string value) => writer.Write($"{value}\r\n");
    }
}
