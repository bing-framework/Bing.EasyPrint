using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Core;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// CPCL 打印命令
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public partial class CPCLPrintCommand : IPrintCommand<CPCLPrintCommand>, IPrintComponent<CPCLPrintCommand>
    {
        /// <summary>
        /// 缓冲区写入器
        /// </summary>
        internal IBufferWriter Writer { get; private set; }

        /// <summary>
        /// 元数据列表
        /// </summary>
        internal List<MetadataBase> Items { get; set; }

        /// <summary>
        /// CPCL命令信息
        /// </summary>
        public CPCLCommandInfo CommandInfo { get; internal set; }

        /// <summary>
        /// 初始化一个<see cref="CPCLPrintCommand"/>类型的实例
        /// </summary>
        public CPCLPrintCommand() : this(System.Text.Encoding.GetEncoding("gbk")) { }

        /// <summary>
        /// 初始化一个<see cref="CPCLPrintCommand"/>类型的实例
        /// </summary>
        /// <param name="encoding">编码方式</param>
        public CPCLPrintCommand(Encoding encoding)
        {
            Writer = new BufferWriter(encoding);
            CommandInfo = new CPCLCommandInfo();
            Items = new List<MetadataBase>();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        internal CPCLPrintCommand Init(int width, int height)
        {
            CommandInfo = new CPCLCommandInfo {Width = width, Height = height};
            Items.Clear();
            Writer.Clear();
            return this;
        }

        /// <summary>
        /// 写入命令
        /// </summary>
        /// <param name="raw">命令</param>
        public CPCLPrintCommand WriteRaw(string raw)
        {
            Items.Add(new RawMetadata(raw));
            return this;
        }

        /// <summary>
        /// 写入命令并换行
        /// </summary>
        /// <param name="raw">命令</param>
        public CPCLPrintCommand WriteRawLine(string raw)
        {
            Items.Add(new RawMetadata(raw, true));
            return this;
        }

        /// <summary>
        /// 构建命令
        /// </summary>
        public IBufferWriter Build()
        {
            if (Items.All(x => x.MetadataType == MetadataType.Raw))
            {
                Items.ForEach(x => x.Build(this));
                return Writer;
            }

            Writer.WriteLine($"! {CommandInfo.Offset} 200 200 {CommandInfo.Height} {CommandInfo.Qty}");
            Writer.WriteLine($"PAGE-WIDTH {CommandInfo.Width}");
            Items.ForEach(x => x.Build(this));
            Writer.WriteLine("PRINT");
            return Writer;
        }
    }
}
