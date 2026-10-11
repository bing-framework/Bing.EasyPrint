using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 保存自动生成及显式指定的打印任务命令。
/// </summary>
internal class PrintCommandStruct
{
    // 内容槽使用运行时指针宽度；记录估计包含元数据及租约列表的保守余量。
    private static readonly int ContentSlotBytes = IntPtr.Size;
    private const int MetadataObjectBytes = 128;

    /// <summary>记录内容项预留的租约；Clear 时释放仍属于当前作用域的记录估计。</summary>
    private readonly List<IDisposable> _contentRecordLeases = new List<IDisposable>();
    /// <summary>当前内容列表 backing array 的容量租约。</summary>
    private IDisposable _contentCapacityLease;
    /// <summary>边界元数据的存储租约。</summary>
    private IDisposable _beginLease;
    private IDisposable _pageWidthLease;
    private IDisposable _endLease;
    private readonly Action _beforeAdd;
    private MetadataBase _begin;
    private MetadataBase _pageWidth;
    private MetadataBase _end;

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
    public PrintCommandStruct(Action beforeAdd = null)
    {
        _beforeAdd = beforeAdd;
        using var operation = PrintResourceScope.BeginTemporary();
        PrintStorageBudget.Reserve(96, "Cpcl.Content.Container");
        Content = new List<MetadataBase>();
        operation?.CommitReservations();
    }

    /// <summary>
    /// 获取或设置打印任务的起始命令。
    /// </summary>
    public MetadataBase Begin
    {
        get => _begin;
        set => _begin = value;
    }

    /// <summary>
    /// 获取或设置页面宽度命令。
    /// </summary>
    public MetadataBase PageWidth
    {
        get => _pageWidth;
        set => _pageWidth = value;
    }

    /// <summary>
    /// 获取或设置起止命令之间的打印内容。
    /// </summary>
    public List<MetadataBase> Content { get; set; }

    /// <summary>
    /// 获取或设置打印任务的结束命令。
    /// </summary>
    public MetadataBase End
    {
        get => _end;
        set => _end = value;
    }

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
        _beforeAdd?.Invoke();
        AddContent(metadata);
    }

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="rawMetadata">命令 - 元数据</param>
    public void Add(RawMetadata rawMetadata)
    {
        _beforeAdd?.Invoke();
        // 检查开始命令
        if (CheckBeginCommand(rawMetadata.Raw))
        {
            ReplaceBoundary(rawMetadata, ref _begin, ref _beginLease, "Cpcl.Begin");
            _hasExplicitBegin = true;
            return;
        }
        // 检查结束命令
        if (CheckEndCommand(rawMetadata.Raw))
        {
            ReplaceBoundary(rawMetadata, ref _end, ref _endLease, "Cpcl.End");
            _hasExplicitEnd = true;
            return;
        }
        // 检查页宽命令
        if (CheckPageWidthCommand(rawMetadata.Raw))
        {
            ReplaceBoundary(rawMetadata, ref _pageWidth, ref _pageWidthLease, "Cpcl.PageWidth");
            _hasExplicitPageWidth = true;
            return;
        }
        AddContent(rawMetadata);
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
        ReleaseContentRecordLeases();
        Content.Clear();
        ReleaseBoundary(ref _begin, ref _beginLease);
        ReleaseBoundary(ref _pageWidth, ref _pageWidthLease);
        ReleaseBoundary(ref _end, ref _endLease);
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
        ReplaceBoundary(new RawMetadata($"! {offset} 200 200 {height} {qty}", true),
            ref _begin, ref _beginLease, "Cpcl.Begin");
    }

    /// <summary>
    /// 初始化页宽命令
    /// </summary>
    /// <param name="width">宽度</param>
    private void InitPageWidth(int width)
    {
        ReplaceBoundary(new RawMetadata($"PAGE-WIDTH {width}", true),
            ref _pageWidth, ref _pageWidthLease, "Cpcl.PageWidth");
    }

    /// <summary>
    /// 初始化结束命令
    /// </summary>
    private void InitEndCommand()
    {
        ReplaceBoundary(new RawMetadata("PRINT", true), ref _end, ref _endLease, "Cpcl.End");
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

    /// <summary>
    /// 在内容进入普通 List 前预留其记录和可能发生的 backing array 扩容。
    /// 临时作用域保证预算失败不会留下记录或租约。
    /// </summary>
    private void AddContent(MetadataBase metadata)
    {
        var count = Content.Count;
        var capacity = Content.Capacity;
        var required = checked(count + 1);
        var nextCapacity = required > capacity ? GetNextCapacity(capacity, required) : capacity;
        using var operation = PrintResourceScope.BeginTemporary();
        var recordLease = PrintStorageBudget.Reserve(GetMetadataStorageBytes(metadata), "Cpcl.Content.Record");
        IDisposable nextCapacityLease = null;
        if (nextCapacity > capacity)
        {
            // 旧数组仍存活时预留完整新数组，追加提交后才归还旧容量。
            nextCapacityLease = PrintStorageBudget.Reserve(
                checked((long)nextCapacity * ContentSlotBytes), "Cpcl.Content.Capacity");
        }

        try
        {
            Content.Add(metadata);
            operation?.CommitReservations();
        }
        catch
        {
            if (Content.Count > count && ReferenceEquals(Content[Content.Count - 1], metadata))
                Content.RemoveAt(Content.Count - 1);
            recordLease.Dispose();
            nextCapacityLease?.Dispose();
            throw;
        }

        if (operation != null)
            _contentRecordLeases.Add(recordLease);
        if (nextCapacityLease != null)
        {
            _contentCapacityLease?.Dispose();
            _contentCapacityLease = nextCapacityLease;
        }
    }

    /// <summary>先预留新边界，再提交替换，确保预算失败保留旧状态。</summary>
    private static void ReplaceBoundary(MetadataBase replacement, ref MetadataBase target,
        ref IDisposable targetLease, string resourceName)
    {
        using var operation = PrintResourceScope.BeginTemporary();
        var replacementLease = PrintStorageBudget.Reserve(
            GetMetadataStorageBytes(replacement), resourceName);
        var previous = target;
        var previousLease = targetLease;
        target = replacement;
        try
        {
            operation?.CommitReservations();
        }
        catch
        {
            target = previous;
            replacementLease.Dispose();
            throw;
        }

        targetLease = replacementLease;
        previousLease?.Dispose();
    }

    private static long GetMetadataStorageBytes(MetadataBase metadata)
    {
        var bytes = MetadataObjectBytes;
        switch (metadata)
        {
            case RawMetadata raw:
                return checked(bytes + PrintStorageBudget.TextBytes(raw.Raw));
            case TextComponent text:
                return checked(bytes + PrintStorageBudget.TextBytes(text.Text));
            case Barcode1DComponent barcode:
                return checked(bytes + PrintStorageBudget.TextBytes(barcode.Type) +
                    PrintStorageBudget.TextBytes(barcode.Text));
            case QRCodeComponent qrCode:
                return checked(bytes + PrintStorageBudget.TextBytes(qrCode.ErrorLevel) +
                    PrintStorageBudget.TextBytes(qrCode.Text));
            default:
                // 图片数组已在快照入口计费，此处仅计算元数据。
                return bytes;
        }
    }

    private static int GetNextCapacity(int currentCapacity, int required)
    {
        if (required <= currentCapacity)
            return currentCapacity;
        var doubled = currentCapacity <= int.MaxValue / 2 ? currentCapacity * 2 : int.MaxValue;
        return Math.Max(required, Math.Max(4, doubled));
    }

    private void ReleaseContentRecordLeases()
    {
        foreach (var lease in _contentRecordLeases)
            lease.Dispose();
        _contentRecordLeases.Clear();
    }

    private static void ReleaseBoundary(ref MetadataBase target, ref IDisposable lease)
    {
        lease?.Dispose();
        lease = null;
        target = null;
    }
}
