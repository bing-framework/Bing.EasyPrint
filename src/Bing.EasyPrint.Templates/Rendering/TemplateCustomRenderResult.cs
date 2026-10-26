using System;
using System.Collections.Generic;
using System.Threading;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 新注册协议的单页结果；不携带任何内置 <see cref="PrintProtocol"/> 值。
/// </summary>
public sealed class TemplateCustomPage
{
    private readonly TemplatePage _source;

    internal TemplateCustomPage(TemplatePage source) => _source = source;

    /// <summary>获取以打印点表示的页宽。</summary>
    public int Width => _source.Width;
    /// <summary>获取以打印点表示的页高。</summary>
    public int Height => _source.Height;
    /// <summary>获取稳定新协议标识。</summary>
    public string ProtocolId => _source.ProtocolId;
    /// <summary>获取单文件页的目标协议字节；多文件页抛错。</summary>
    public byte[] Data => _source.Data;
    /// <summary>获取当前页有序输出。</summary>
    public PrintPageOutput Output => _source.Output;
    /// <summary>获取须按顺序独立发送的命令文件。</summary>
    public IReadOnlyList<PrintOutputSegment> Segments => _source.Segments;
    /// <summary>获取全部命令文件字节数。</summary>
    public long ByteLength => _source.ByteLength;
    /// <summary>获取当前页布局节点。</summary>
    public IReadOnlyList<TemplateLayoutElement> Elements => _source.Elements;
    /// <summary>获取显式任务输出对应的原逻辑页序号。</summary>
    public int? SourcePageIndex => _source.SourcePageIndex;
    /// <summary>获取显式任务输出对应的零基份数序号。</summary>
    public int? CopyIndex => _source.CopyIndex;
    /// <summary>获取显式任务是否在本页后额外进纸。</summary>
    public bool? FeedAfterPrint => _source.FeedAfterPrint;
    /// <summary>获取显式任务是否在本页后切纸。</summary>
    public bool? CutAfterPrint => _source.CutAfterPrint;

    internal IReadOnlyList<PrintElement> RenderElements => _source.RenderElements;
    internal IReadOnlyList<TemplateLayoutElement> RenderElementOwnership => _source.RenderElementOwnership;
    internal void ReleaseRenderElements() => _source.ReleaseRenderElements();
}

/// <summary>
/// 新注册协议的渲染结果；页面和收尾控制文件保持独立，不伪造内置协议。
/// </summary>
public sealed class TemplateCustomRenderResult
{
    /// <summary>获取稳定新协议标识。</summary>
    public string ProtocolId { get; }
    /// <summary>获取已生成的全部物理页面。</summary>
    public IReadOnlyList<TemplateCustomPage> Pages { get; }
    /// <summary>获取独立收尾控制文件；不属于物理页面。</summary>
    public IReadOnlyList<PrintProtocolControlOutput> FinishingFiles { get; }
    /// <summary>获取页面数。</summary>
    public int PageCount => Pages.Count;

    internal TemplateCustomRenderResult(string protocolId, IEnumerable<TemplateCustomPage> pages,
        IEnumerable<PrintProtocolControlOutput> finishingFiles = null)
    {
        if (string.IsNullOrWhiteSpace(protocolId)) throw new ArgumentException("协议标识不能为空。", nameof(protocolId));
        PrintResourceScope.Retain(32, "Template.CustomResult");
        ProtocolId = protocolId;
        var pageCopy = new List<TemplateCustomPage>(pages);
        Pages = pageCopy.AsReadOnly();
        var controlCopy = new List<PrintProtocolControlOutput>();
        if (finishingFiles != null) controlCopy.AddRange(finishingFiles);
        FinishingFiles = controlCopy.AsReadOnly();
    }

    internal void ReleaseRenderElements()
    {
        foreach (var page in Pages) page.ReleaseRenderElements();
    }
}
