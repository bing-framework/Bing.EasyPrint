using System;
using System.Collections.Generic;
using System.Threading;
using Bing.EasyPrint.Core;

namespace Bing.EasyPrint.Printing;

/// <summary>新协议可选任务收尾的插入位置。</summary>
public enum PrintProtocolFinishingPlacement
{
    /// <summary>在当前任务最后一页之后插入。</summary>
    JobEnd = 0,
    /// <summary>在每一页之后插入。</summary>
    PageEnd = 1
}

/// <summary>
/// 描述新协议可选任务收尾的进纸、切纸及插入位置；未声明能力的后端必须早拒绝。
/// </summary>
public sealed class PrintProtocolFinishingRequest
{
    /// <summary>获取或设置进纸量；必须为非负数。</summary>
    public int FeedAmount { get; set; }

    /// <summary>获取或设置进纸单位。</summary>
    public PrintFeedUnit FeedUnit { get; set; } = PrintFeedUnit.Lines;

    /// <summary>获取或设置是否在收尾中切纸。</summary>
    public bool Cut { get; set; }

    /// <summary>获取或设置收尾文件插入位置。</summary>
    public PrintProtocolFinishingPlacement Placement { get; set; } = PrintProtocolFinishingPlacement.JobEnd;

    /// <summary>验证收尾请求的基础范围；协议相关范围由后端继续验证。</summary>
    /// <exception cref="ArgumentOutOfRangeException">进纸量为负或单位/位置未定义时抛出。</exception>
    public void Validate()
    {
        if (FeedAmount < 0) throw new ArgumentOutOfRangeException(nameof(FeedAmount), "收尾进纸量不能为负数。");
        if (!Enum.IsDefined(typeof(PrintFeedUnit), FeedUnit))
            throw new ArgumentOutOfRangeException(nameof(FeedUnit), "收尾进纸单位无效。");
        if (!Enum.IsDefined(typeof(PrintProtocolFinishingPlacement), Placement))
            throw new ArgumentOutOfRangeException(nameof(Placement), "收尾插入位置无效。");
    }
}

/// <summary>独立收尾控制文件；不属于任何物理打印页。</summary>
public sealed class PrintProtocolControlOutput
{
    /// <summary>获取独立控制文件输出。</summary>
    public PrintPageOutput Output { get; }
    /// <summary>获取插入位置。</summary>
    public PrintProtocolFinishingPlacement Placement { get; }
    /// <summary>获取页尾插入时的零基页序号；任务结束插入为 null。</summary>
    public int? PageIndex { get; }
    /// <summary>获取稳定文件序号。</summary>
    public int FileIndex { get; }

    internal PrintProtocolControlOutput(PrintPageOutput output, PrintProtocolFinishingPlacement placement,
        int? pageIndex, int fileIndex)
    {
        Output = output;
        Placement = placement;
        PageIndex = pageIndex;
        FileIndex = fileIndex;
    }
}

/// <summary>一个物理副本与源逻辑页的对应关系；源于共同的新协议页面规范化入口。</summary>
public sealed class PrintProtocolPageOrigin
{
    /// <summary>获取零基源逻辑页序号。</summary>
    public int SourcePageIndex { get; }
    /// <summary>获取零基物理副本序号；未声明多份时为零。</summary>
    public int CopyIndex { get; }

    internal PrintProtocolPageOrigin(int sourcePageIndex, int copyIndex)
    {
        SourcePageIndex = sourcePageIndex;
        CopyIndex = copyIndex;
    }
}

/// <summary>页面输出与独立收尾文件的组合结果；收尾文件不增加物理页数。</summary>
public sealed class PrintProtocolOutputResult
{
    /// <summary>获取按实际物理副本顺序排列的页面输出。</summary>
    public IReadOnlyList<PrintPageOutput> Pages { get; }
    /// <summary>获取按插入位置排列的独立收尾文件。</summary>
    public IReadOnlyList<PrintProtocolControlOutput> FinishingFiles { get; }
    /// <summary>获取与 <see cref="Pages"/> 一一对应的源页/副本归属；旧内置路径可为空集合。</summary>
    public IReadOnlyList<PrintProtocolPageOrigin> PageOrigins { get; }
    /// <summary>获取全部页面和收尾文件的字节数。</summary>
    public long ByteLength { get; }

    internal PrintProtocolOutputResult(IReadOnlyList<PrintPageOutput> pages,
        IReadOnlyList<PrintProtocolControlOutput> finishingFiles, long byteLength,
        IReadOnlyList<PrintProtocolPageOrigin> pageOrigins)
    {
        Pages = pages;
        FinishingFiles = finishingFiles;
        ByteLength = byteLength;
        PageOrigins = pageOrigins;
    }
}

/// <summary>
/// 组合页面输出与可选收尾控制段的通用执行器；收尾不支持、越界或长度超预算时整体失败，不返回部分输出。
/// </summary>
/// <remarks>
/// <see cref="Render(IPrintProtocolBackend, PrintDocument, PrintProtocolFinishingRequest, long?, CancellationToken)"/>
/// 是新协议执行入口：它先在编码前展开物理副本并保留源页/副本归属。旧内置 Provider 的原生 copies 路径使用
/// <see cref="RenderLegacy"/>，不会被无条件展开。
/// </remarks>
public static class PrintProtocolOutputComposer
{
    /// <summary>
    /// 渲染新协议文档并可选生成独立收尾控制文件；页面份数会在调用编码器前展开。
    /// </summary>
    /// <param name="backend">本次请求创建的新协议后端会话。</param>
    /// <param name="document">协议无关打印文档；页面的 <see cref="PrintPage.Copies"/> 会展开为实际副本。</param>
    /// <param name="finishing">可选收尾请求；为 null 时不生成收尾文件。</param>
    /// <param name="maxOutputBytes">可选的输出字节上限；达到上限时整体失败。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保留物理副本顺序、源页/副本归属与独立控制文件边界的组合结果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="backend"/> 或 <paramref name="document"/> 为 null 时抛出。</exception>
    /// <exception cref="NotSupportedException">后端未声明收尾能力但请求了收尾，或页面携带旧快捷收尾字段时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">页份数无效或输出超过字节上限时抛出。</exception>
    public static PrintProtocolOutputResult Render(IPrintProtocolBackend backend, PrintDocument document,
        PrintProtocolFinishingRequest finishing = null, long? maxOutputBytes = null,
        CancellationToken cancellationToken = default(CancellationToken))
        => RenderNormalized(backend, document, finishing, maxOutputBytes, int.MaxValue, cancellationToken);

    /// <summary>
    /// 渲染新协议文档，并在分配物理副本前执行页数上限检查。
    /// </summary>
    /// <param name="backend">本次请求创建的新协议后端会话。</param>
    /// <param name="document">协议无关打印文档；页面的 <see cref="PrintPage.Copies"/> 会展开为实际副本。</param>
    /// <param name="finishing">可选收尾请求；为 null 时不生成收尾文件。</param>
    /// <param name="maxOutputBytes">可选的输出字节上限；达到上限时整体失败。</param>
    /// <param name="maxPages">实际物理副本页数上限，必须为正数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保留物理副本顺序、源页/副本归属与独立控制文件边界的组合结果。</returns>
    public static PrintProtocolOutputResult Render(IPrintProtocolBackend backend, PrintDocument document,
        PrintProtocolFinishingRequest finishing, long? maxOutputBytes, int maxPages,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (maxPages < 1) throw new ArgumentOutOfRangeException(nameof(maxPages));
        return RenderNormalized(backend, document, finishing, maxOutputBytes, maxPages, cancellationToken);
    }

    /// <summary>
    /// 旧内置 Provider 适配路径：保留页面原生 copies 与旧收尾字段，不展开物理副本。
    /// </summary>
    internal static PrintProtocolOutputResult RenderLegacy(IPrintProtocolBackend backend, PrintDocument document,
        PrintProtocolFinishingRequest finishing = null, long? maxOutputBytes = null,
        CancellationToken cancellationToken = default(CancellationToken))
        => RenderCore(backend, document, finishing, maxOutputBytes, false, int.MaxValue, cancellationToken);

    /// <summary>
    /// 在分配副本前验证实际物理页数；供模板层在创建协议后端前复用同一计数规则。
    /// </summary>
    internal static int ValidatePageCount(PrintDocument document, int maxPages, CancellationToken cancellationToken)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (maxPages < 1) throw new ArgumentOutOfRangeException(nameof(maxPages));
        long total = 0;
        for (var index = 0; index < document.Pages.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.Pages[index] ?? throw new ArgumentException("打印文档包含空页面。", nameof(document));
            if (page.Copies < 1)
                throw new ArgumentOutOfRangeException(nameof(document), "打印页份数必须为正整数。");
            if (page.FeedLines != 0 || page.CutAfterPrint || page.CutOptions != null)
                throw new NotSupportedException("新协议页面必须使用 finishing 收尾，不能携带旧 FeedLines/CutAfterPrint/CutOptions。");
            total = checked(total + page.Copies);
            if (total > maxPages)
                throw new ArgumentOutOfRangeException(nameof(document), "实际物理页数超过页数上限。");
        }
        if (total > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(document), "实际物理页数超过可表示范围。");
        return (int)total;
    }

    private static PrintProtocolOutputResult RenderNormalized(IPrintProtocolBackend backend, PrintDocument document,
        PrintProtocolFinishingRequest finishing, long? maxOutputBytes, int maxPages,
        CancellationToken cancellationToken)
        => RenderCore(backend, document, finishing, maxOutputBytes, true, maxPages, cancellationToken);

    private static PrintProtocolOutputResult RenderCore(IPrintProtocolBackend backend, PrintDocument document,
        PrintProtocolFinishingRequest finishing, long? maxOutputBytes, bool normalizeCopies, int maxPages,
        CancellationToken cancellationToken)
    {
        if (backend == null) throw new ArgumentNullException(nameof(backend));
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (maxOutputBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxOutputBytes));
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = PrintResourceScope.BeginTemporary();
        using var plan = normalizeCopies ? CreatePhysicalPlan(document, maxPages, cancellationToken) : null;
        var targetDocument = plan?.Document ?? document;
        backend.Validate(targetDocument, cancellationToken);

        IPrintProtocolFinishingBackend finishingBackend = null;
        long finishingLength = 0;
        var finishingCopies = 0;
        if (finishing != null)
        {
            finishing.Validate();
            finishingBackend = backend as IPrintProtocolFinishingBackend;
            if (finishingBackend == null || !finishingBackend.SupportsFinishing)
                throw new NotSupportedException("协议后端未声明任务收尾能力。");
            finishingBackend.ValidateFinishing(targetDocument, finishing, cancellationToken);
            finishingLength = finishingBackend.GetFinishingLength(finishing, cancellationToken);
            if (finishingLength < 0) throw new InvalidOperationException("协议后端返回了负的收尾长度。");
            finishingCopies = finishing.Placement == PrintProtocolFinishingPlacement.PageEnd
                ? Math.Max(1, targetDocument.Pages.Count) : 1;
            var finishingBytesOnly = checked(finishingLength * finishingCopies);
            if (maxOutputBytes.HasValue && finishingBytesOnly > maxOutputBytes.Value)
                throw new ArgumentOutOfRangeException(nameof(maxOutputBytes), "输出超过字节上限。");
        }

        var pages = backend.Render(targetDocument, cancellationToken) ??
            throw new InvalidOperationException("协议后端未返回页面输出。");
        cancellationToken.ThrowIfCancellationRequested();
        if (pages.Count != targetDocument.Pages.Count)
            throw new InvalidOperationException("协议后端返回的页面数量与打印文档页数不一致。");
        var pageCopy = new List<PrintPageOutput>(pages.Count);
        long bytes = 0;
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (page == null) throw new InvalidOperationException("协议后端返回了空页面输出。");
            bytes = checked(bytes + page.ByteLength);
            pageCopy.Add(page);
        }
        if (maxOutputBytes.HasValue && bytes > maxOutputBytes.Value)
            throw new ArgumentOutOfRangeException(nameof(maxOutputBytes), "输出超过字节上限。");

        var controls = new List<PrintProtocolControlOutput>();
        if (finishing != null)
        {
            bytes = checked(bytes + checked(finishingLength * finishingCopies));
            if (maxOutputBytes.HasValue && bytes > maxOutputBytes.Value)
                throw new ArgumentOutOfRangeException(nameof(maxOutputBytes), "输出超过字节上限。");
            var output = finishingBackend.RenderFinishing(finishing, cancellationToken) ??
                throw new InvalidOperationException("协议后端未返回收尾输出。");
            cancellationToken.ThrowIfCancellationRequested();
            if (output.ByteLength != finishingLength)
                throw new InvalidOperationException("协议后端收尾长度与预检长度不一致。");
            if (finishing.Placement == PrintProtocolFinishingPlacement.PageEnd)
            {
                for (var index = 0; index < pageCopy.Count; index++)
                    controls.Add(new PrintProtocolControlOutput(output, finishing.Placement, index, index + 1));
            }
            else controls.Add(new PrintProtocolControlOutput(output, finishing.Placement, null, 1));
        }

        cancellationToken.ThrowIfCancellationRequested();
        operation?.CommitReservations();
        IReadOnlyList<PrintProtocolPageOrigin> origins = plan == null
            ? Array.Empty<PrintProtocolPageOrigin>()
            : plan.Origins.AsReadOnly();
        return new PrintProtocolOutputResult(pageCopy.AsReadOnly(), controls.AsReadOnly(), bytes, origins);
    }

    private static PhysicalPagePlan CreatePhysicalPlan(PrintDocument document, int maxPages,
        CancellationToken cancellationToken)
    {
        var count = ValidatePageCount(document, maxPages, cancellationToken);
        var storage = new PrintStorageBudget();
        try
        {
            var physical = new PrintDocument(storage);
            var origins = new List<PrintProtocolPageOrigin>(count);
            for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourcePage = document.Pages[pageIndex];
                for (var copyIndex = 0; copyIndex < sourcePage.Copies; copyIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = physical.AddPage(sourcePage.Width, sourcePage.Height);
                    page.Copies = 1;
                    page.FeedLines = 0;
                    page.CutAfterPrint = false;
                    for (var elementIndex = 0; elementIndex < sourcePage.Elements.Count; elementIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var element = sourcePage.Elements[elementIndex];
                        page.Add(element, element.SourcePath);
                    }
                    origins.Add(new PrintProtocolPageOrigin(pageIndex, copyIndex));
                }
            }
            return new PhysicalPagePlan(storage, physical, origins);
        }
        catch
        {
            storage.Dispose();
            throw;
        }
    }

    private sealed class PhysicalPagePlan : IDisposable
    {
        private readonly PrintStorageBudget _storage;

        internal PhysicalPagePlan(PrintStorageBudget storage, PrintDocument document,
            List<PrintProtocolPageOrigin> origins)
        {
            _storage = storage;
            Document = document;
            Origins = origins;
        }

        internal PrintDocument Document { get; }
        internal List<PrintProtocolPageOrigin> Origins { get; }

        public void Dispose() => _storage.Dispose();
    }
}
