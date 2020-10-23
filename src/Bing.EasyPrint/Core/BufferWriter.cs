using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.EasyPrint.Core
{
    /// <summary>
    /// 缓冲区写入器
    /// </summary>
    internal class BufferWriter : IBufferWriter
    {
        /// <summary>
        /// 字节缓冲区
        /// </summary>
        private readonly List<byte> _buffer;

        /// <summary>
        /// 原始命令
        /// </summary>
        private readonly List<string> _sources;

        /// <summary>
        /// 字符编码
        /// </summary>
        private readonly Encoding _encoding;

        /// <summary>
        /// 初始化一个<see cref="BufferWriter"/>类型的实例
        /// </summary>
        /// <param name="encoding">字符编码</param>
        public BufferWriter(Encoding encoding)
        {
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _buffer = new List<byte>();
            _sources = new List<string>();
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="value">字节数组</param>
        public IBufferWriter Write(byte[] value)
        {
            if (value == null)
                return this;
            _buffer.AddRange(value);
            return this;
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="value">字符串</param>
        public IBufferWriter Write(string value)
        {
            if (string.IsNullOrEmpty(value))
                return this;
            var bytes = _encoding.GetBytes(value);
            _buffer.AddRange(bytes);
            _sources.Add(value);
            return this;
        }

        /// <summary>
        /// 清空内容
        /// </summary>
        public IBufferWriter Clear()
        {
            _buffer.Clear();
            _sources.Clear();
            return this;
        }

        /// <summary>
        /// 获取二进制数组
        /// </summary>
        public byte[] GetBytes() => _buffer.ToArray();

        /// <summary>
        /// 输出字符串
        /// </summary>
        public override string ToString()
        {
            if (_sources.Count == 0)
                return string.Empty;
            var result = new StringBuilder();
            foreach (var command in _sources)
                result.Append(command);
            return result.ToString();
        }
    }
}
