using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Core;

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
            InitCommand(CommandInfo.Offset, CommandInfo.Width, CommandInfo.Height, CommandInfo.Qty);
            Items.ForEach(x => x.Build(this));
            Print();
            return Writer;
        }
    }
}
