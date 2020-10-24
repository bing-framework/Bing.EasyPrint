// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 原始命令 - 元数据
    /// </summary>
    internal class RawMetadata : MetadataBase
    {
        /// <summary>
        /// 元数据类型
        /// </summary>
        public override MetadataType MetadataType => MetadataType.Raw;

        /// <summary>
        /// 原始命令
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// 是否换行
        /// </summary>
        public bool NewLine { get; }

        /// <summary>
        /// 初始化一个<see cref="RawMetadata"/>类型的实例
        /// </summary>
        /// <param name="raw">原始命令</param>
        public RawMetadata(string raw) : this(raw, false) { }

        /// <summary>
        /// 初始化一个<see cref="RawMetadata"/>类型的实例
        /// </summary>
        /// <param name="raw">原始命令</param>
        /// <param name="newLine">是否换行</param>
        public RawMetadata(string raw, bool newLine)
        {
            Raw = raw;
            NewLine = newLine;
        }

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="writer">缓冲区写入器</param>
        public override void Build(IBufferWriter writer)
        {
            if (NewLine)
                writer.WriteLine(Raw);
            else
                writer.Write(Raw);
        }
    }
}
