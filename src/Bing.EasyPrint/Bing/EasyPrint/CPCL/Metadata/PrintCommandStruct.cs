using System.Collections.Generic;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 打印命令结构
    /// </summary>
    internal class PrintCommandStruct
    {
        /// <summary>
        /// 初始化一个<see cref="PrintCommandStruct"/>类型的实例
        /// </summary>
        public PrintCommandStruct()
        {
            Content = new List<MetadataBase>();
        }

        /// <summary>
        /// 开始命令
        /// </summary>
        public MetadataBase Begin { get; set; }

        /// <summary>
        /// 页宽命令
        /// </summary>
        public MetadataBase PageWidth { get; set; }

        /// <summary>
        /// 命令内容
        /// </summary>
        public List<MetadataBase> Content { get; set; }

        /// <summary>
        /// 结束命令
        /// </summary>
        public MetadataBase End { get; set; }

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="metadata">元数据</param>
        public void Add(MetadataBase metadata)
        {
            if (metadata.MetadataType == MetadataType.Raw)
            {
                Add(metadata as RawMetadata);
                return;
            }
            Content.Add(metadata);
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="rawMetadata">命令 - 元数据</param>
        public void Add(RawMetadata rawMetadata)
        {
            // 检查开始命令
            if (CheckBeginCommand(rawMetadata.Raw))
            {
                Begin = rawMetadata;
                return;
            }
            // 检查结束命令
            if (CheckEndCommand(rawMetadata.Raw))
            {
                End = rawMetadata;
                return;
            }
            // 检查页宽命令
            if (CheckPageWidthCommand(rawMetadata.Raw))
            {
                PageWidth = rawMetadata;
                return;
            }
            Content.Add(rawMetadata);
        }

        /// <summary>
        /// 添加命令
        /// </summary>
        /// <param name="raw">命令</param>
        public void AddRaw(string raw) => AddRaw(raw, false);

        /// <summary>
        /// 添加命令并换行
        /// </summary>
        /// <param name="raw">命令</param>
        public void AddRawLine(string raw) => AddRaw(raw, true);

        /// <summary>
        /// 添加命令
        /// </summary>
        /// <param name="raw">命令</param>
        /// <param name="newLine">是否换行</param>
        public void AddRaw(string raw, bool newLine) => Add(new RawMetadata(raw, newLine));

        /// <summary>
        /// 检查开始命令
        /// </summary>
        /// <param name="raw">原文</param>
        private bool CheckBeginCommand(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            if (Regex.IsMatch(raw, "^!\\s[0-9]\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s{0,1}$"))
                return true;
            return false;
        }

        /// <summary>
        /// 检查结束命令
        /// </summary>
        /// <param name="raw">原文</param>
        private bool CheckEndCommand(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            if (Regex.IsMatch(raw, "^PRINT\\s{0,1}$"))
                return true;
            return false;
        }

        /// <summary>
        /// 检查页宽命令
        /// </summary>
        /// <param name="raw">原文</param>
        private bool CheckPageWidthCommand(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            if (Regex.IsMatch(raw, "^(PAGE-WIDTH|PW)\\s[0-9]+\\s{0,1}$"))
                return true;
            return false;
        }

        /// <summary>
        /// 清空
        /// </summary>
        public void Clear()
        {
            Content.Clear();
            Begin = null;
            End = null;
            PageWidth = null;
        }

        /// <summary>
        /// 安全初始化
        /// </summary>
        /// <param name="info">CPCL命令信息</param>
        public void SafeInit(CPCLCommandInfo info)
        {
            InitBeginCommand(info.Offset, info.Height, info.Qty);
            InitPageWidth(info.Width);
            InitEndCommand();
        }

        /// <summary>
        /// 初始化开始命令
        /// </summary>
        /// <param name="offset">偏移量。整个标签的横向偏移值</param>
        /// <param name="height">高度。标签的最大高度。</param>
        /// <param name="qty">标签数量。打印标签数量</param>
        private void InitBeginCommand(int offset, int height, int qty)
        {
            if (Begin != null)
                return;
            AddRawLine($"! {offset} 200 200 {height} {qty}");
        }

        /// <summary>
        /// 初始化页宽命令
        /// </summary>
        /// <param name="width">宽度</param>
        private void InitPageWidth(int width)
        {
            if (PageWidth != null)
                return;
            AddRawLine($"PAGE-WIDTH {width}");
        }

        /// <summary>
        /// 初始化结束命令
        /// </summary>
        private void InitEndCommand()
        {
            if (End != null)
                return;
            AddRawLine("PRINT");
        }

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="command">CPCL打印命令</param>
        public void Build(CPCLPrintCommand command)
        {
            Begin.Build(command);
            PageWidth.Build(command);
            Content.ForEach(x => x.Build(command));
            End.Build(command);
        }
    }
}
