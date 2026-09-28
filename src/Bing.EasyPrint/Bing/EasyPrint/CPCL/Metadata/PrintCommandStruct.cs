using System.Collections.Generic;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 保存自动生成及显式指定的打印任务命令。
/// </summary>
internal class PrintCommandStruct
{
    /// <summary>
    /// 指示起始命令是否由调用方显式提供。
    /// </summary>
    private bool _hasExplicitBegin;
    /// <summary>
    /// 指示页宽命令是否由调用方显式提供。
    /// </summary>
    private bool _hasExplicitPageWidth;
    /// <summary>
    /// 指示结束命令是否由调用方显式提供。
    /// </summary>
    private bool _hasExplicitEnd;

    /// <summary>
    /// 初始化一个 <see cref="PrintCommandStruct"/> 类型的实例。
    /// </summary>
    public PrintCommandStruct()
    {
        Content = new List<MetadataBase>();
    }

    /// <summary>
    /// 获取或设置打印任务的起始命令。
    /// </summary>
    public MetadataBase Begin { get; set; }

    /// <summary>
    /// 获取或设置页面宽度命令。
    /// </summary>
    public MetadataBase PageWidth { get; set; }

    /// <summary>
    /// 获取或设置起止命令之间的打印内容。
    /// </summary>
    public List<MetadataBase> Content { get; set; }

    /// <summary>
    /// 获取或设置打印任务的结束命令。
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
            _hasExplicitBegin = true;
            return;
        }
        // 检查结束命令
        if (CheckEndCommand(rawMetadata.Raw))
        {
            End = rawMetadata;
            _hasExplicitEnd = true;
            return;
        }
        // 检查页宽命令
        if (CheckPageWidthCommand(rawMetadata.Raw))
        {
            PageWidth = rawMetadata;
            _hasExplicitPageWidth = true;
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
    /// <returns>匹配打印任务起始命令时返回 true，否则返回 false。</returns>
    private bool CheckBeginCommand(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        if (Regex.IsMatch(raw, "^!\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s[0-9]+\\s{0,1}$"))
            return true;
        return false;
    }

    /// <summary>
    /// 检查结束命令
    /// </summary>
    /// <param name="raw">原文</param>
    /// <returns>匹配打印任务结束命令时返回 true，否则返回 false。</returns>
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
    /// <returns>匹配页宽命令时返回 true，否则返回 false。</returns>
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
        _hasExplicitBegin = false;
        _hasExplicitPageWidth = false;
        _hasExplicitEnd = false;
    }

    /// <summary>
    /// 安全初始化
    /// </summary>
    /// <param name="info">CPCL命令信息</param>
    public void SafeInit(CPCLCommandInfo info)
    {
        if (!_hasExplicitBegin)
            InitBeginCommand(info.Offset, info.Height, info.Qty);
        if (!_hasExplicitPageWidth)
            InitPageWidth(info.Width);
        if (!_hasExplicitEnd)
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
        Begin = new RawMetadata($"! {offset} 200 200 {height} {qty}", true);
    }

    /// <summary>
    /// 初始化页宽命令
    /// </summary>
    /// <param name="width">宽度</param>
    private void InitPageWidth(int width)
    {
        PageWidth = new RawMetadata($"PAGE-WIDTH {width}", true);
    }

    /// <summary>
    /// 初始化结束命令
    /// </summary>
    private void InitEndCommand()
    {
        End = new RawMetadata("PRINT", true);
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
