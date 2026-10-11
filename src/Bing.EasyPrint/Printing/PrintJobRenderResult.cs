using System.Collections.Generic;
using Bing.EasyPrint.Core;

namespace Bing.EasyPrint.Printing;

/// <summary>按实际输出顺序保存任务页，不把重复份数隐藏在单个页面字节内。</summary>
public sealed class PrintJobRenderResult
{
    /// <summary>每个实际输出页的字节及归属，须按此顺序发送。</summary>
    public IReadOnlyList<PrintJobPage> Pages { get; }
    internal PrintJobRenderResult(BudgetedList<PrintJobPage> pages)
    {
        if (pages == null) throw new System.ArgumentNullException(nameof(pages));
        using var operation = PrintResourceScope.BeginTemporary();
        PrintResourceScope.Retain(40, "Job.ResultMetadata");
        Pages = pages.AsReadOnly();
        operation?.CommitReservations();
    }
}

/// <summary>打印任务的一个实际输出页。</summary>
public sealed class PrintJobPage
{
    /// <summary>原文档中的零基页面序号。</summary>
    public int SourcePageIndex { get; }
    /// <summary>零基份数序号。</summary>
    public int CopyIndex { get; }
    /// <summary>单文件页的完整字节；包含独立控制文件时抛错，须使用 Segments。</summary>
    public byte[] Data => Output.Data;
    /// <summary>本页有序输出；独立文件必须分别发送。</summary>
    public PrintPageOutput Output { get; }
    /// <summary>须逐个独立发送的命令文件。</summary>
    public IReadOnlyList<PrintOutputSegment> Segments => Output.Segments;
    /// <summary>所有文件字节总数。</summary>
    public long ByteLength => Output.ByteLength;
    /// <summary>本页之后是否执行了额外进纸。</summary>
    public bool FeedAfterPrint { get; }
    /// <summary>本页之后是否请求切纸。</summary>
    public bool CutAfterPrint { get; }
    internal PrintJobPage(int sourcePageIndex, int copyIndex, byte[] data, bool feed, bool cut)
        : this(sourcePageIndex, copyIndex, PrintPageOutput.FromOwned(data), feed, cut) { }
    internal PrintJobPage(int sourcePageIndex, int copyIndex, PrintPageOutput output, bool feed, bool cut)
    {
        SourcePageIndex = sourcePageIndex; CopyIndex = copyIndex; Output = output;
        FeedAfterPrint = feed; CutAfterPrint = cut;
    }
}
