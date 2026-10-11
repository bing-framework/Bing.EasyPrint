using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.EasyPrint.Core;

namespace Bing.EasyPrint.Printing;

/// <summary>用显式任务策略编排页面、份数及收尾操作，保留 Provider 原页面 API。</summary>
public sealed class PrintJobRenderer
{
    /// <summary>在资源加载前检查协议可判定的任务能力；实际页数未知时不猜测份边界。</summary>
    public static void ValidateSettings(IPrintProtocolProvider provider, PrintJobOptions options, int? logicalPageCount = null)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (options == null) throw new ArgumentNullException(nameof(options));
        try { options.Validate(); }
        catch (ArgumentException error) { throw new PrintElementValidationException(provider.Protocol, "Job", error); }
        try { (provider as PrintProtocolProviderBase)?.ValidateJobFinishing(options); }
        catch (NotSupportedException error) { throw new PrintCapabilityException(provider.Protocol, "Job", error); }
        catch (ArgumentException error) { throw new PrintElementValidationException(provider.Protocol, "Job", error); }
        if (options.CopyOrder == PrintCopyOrder.Uncollated && options.Copies > 1 && logicalPageCount > 1 &&
            (options.CutScope == PrintFinishScope.EachCopy || options.FeedScope == PrintFinishScope.EachCopy))
            throw new PrintCapabilityException(provider.Protocol, "Job.CopyOrder",
                new NotSupportedException("多页多份的同页连续输出不能形成完整份的连续边界，不能使用 EachCopy 收尾。"));
        if (options.FeedAmount > 0 && !provider.Capabilities.JobFeedUnits.Contains(options.FeedUnit))
            throw new PrintCapabilityException(provider.Protocol, "Job.FeedUnit",
                new NotSupportedException($"目标协议或机型不支持 {options.FeedUnit} 任务进纸单位。"));
        if (options.CutScope != PrintFinishScope.None && !provider.Capabilities.SupportsCut)
            throw new PrintCapabilityException(provider.Protocol, "Job.CutScope",
                new NotSupportedException("目标协议或机型不支持任务切纸。"));
        if ((options.FeedAmount > 0 || options.CutScope != PrintFinishScope.None) &&
            !(provider is PrintProtocolProviderBase))
            throw new PrintCapabilityException(provider.Protocol, "Job",
                new NotSupportedException("自定义 Provider 未实现内置任务收尾编码。"));
        if (options.CutOptions != null || options.CutScope != PrintFinishScope.None &&
            (provider.Protocol == PrintProtocol.EscPos || provider.Protocol == PrintProtocol.Tspl ||
             provider.Protocol == PrintProtocol.Zpl && provider.Capabilities.ZplCutter != null))
        {
            try { ((PrintProtocolProviderBase)provider).ValidateCutOptions(options.CutOptions ?? new PrintCutOptions()); }
            catch (NotSupportedException error)
            { throw new PrintCapabilityException(provider.Protocol, "Job.CutOptions", error); }
            catch (ArgumentException error)
            { throw new PrintElementValidationException(provider.Protocol, "Job.CutOptions", error); }
        }
    }

    /// <summary>生成逐个实际输出页，并在展开份数前检查页数与已知输出预算。</summary>
    public PrintJobRenderResult Render(IPrintProtocolProvider provider, PrintDocument document, PrintJobOptions options,
        CancellationToken cancellationToken = default(CancellationToken), int maxPages = int.MaxValue,
        long maxOutputBytes = int.MaxValue)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (maxPages < 1) throw new ArgumentOutOfRangeException(nameof(maxPages));
        if (maxOutputBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxOutputBytes));
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = PrintResourceScope.BeginTemporary();
        ValidateSettings(provider, options, document.Pages.Count);
        options = options.Snapshot();
        var count = checked((long)document.Pages.Count * options.Copies);
        if (count > maxPages) throw new ArgumentException("任务实际输出页数超过上限。", nameof(options));
        using var neutralStorage = new PrintStorageBudget();
        var neutral = new PrintDocument(neutralStorage);
        for (var index = 0; index < document.Pages.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.Pages[index];
            if (page.FeedLines < 0)
                throw new PrintElementValidationException(provider.Protocol, $"Pages[{index}]",
                    new ArgumentOutOfRangeException(nameof(page.FeedLines), "额外进纸行数不能为负数。"), index, null);
            if (page.Copies != 1 || page.FeedLines > 0 || page.CutAfterPrint || page.CutOptions != null)
                throw new PrintElementValidationException(provider.Protocol, $"Pages[{index}]",
                    new ArgumentException("任务策略与页面份数、额外进纸或切纸设置冲突；任务页面须为一份且不额外收尾。"), index, null);
            CopyPage(neutral, page, index, false, cancellationToken);
        }
        provider.Validate(neutral, cancellationToken);
        if (count == 0)
        {
            neutralStorage.Dispose();
            var emptyPages = new BudgetedList<PrintJobPage>("Job.Pages", itemBytes: 64);
            var emptyResult = new PrintJobRenderResult(emptyPages);
            operation?.CommitReservations();
            return emptyResult;
        }
        var internalProvider = provider as PrintProtocolProviderBase;
        var feedCount = EventCount(options.FeedScope, count, options.Copies);
        var cutCount = EventCount(options.CutScope, count, options.Copies);
        var feedLength = options.FeedAmount == 0 || feedCount == 0 ? 0 :
            internalProvider.JobFinishingLength(options.FeedAmount, options.FeedUnit, false);
        var cutLength = cutCount == 0 ? 0 : options.CutOptions == null
            ? internalProvider.JobFinishingLength(0, options.FeedUnit, true)
            : internalProvider.JobCutLength(options.CutOptions);
        if (feedCount > 0 && feedLength > maxOutputBytes / feedCount)
            throw new ArgumentException("任务收尾指令超过输出字节预算。");
        var predicted = feedLength * feedCount;
        if (cutCount > 0 && cutLength > (maxOutputBytes - predicted) / cutCount)
            throw new ArgumentException("任务收尾指令超过输出字节预算。");
        predicted += cutLength * cutCount;
        using var framesStorage = new PrintStorageBudget();
        var frames = new BudgetedList<PrintPageOutput>("Job.Frames", itemBytes: 8,
            owner: framesStorage, capacity: neutral.Pages.Count);
        using var cutFramesStorage = new PrintStorageBudget();
        var cutFrames = Array.Empty<PrintPageOutput>();
        if (cutCount > 0 && internalProvider != null && internalProvider.JobCutInPage)
        {
            var cutFrameBytes = checked(96L + (long)neutral.Pages.Count * 8L);
            var cutFrameReservation = PrintStorageBudget.Reserve(cutFrameBytes, "Job.CutFrames", cutFramesStorage);
            try { cutFrames = new PrintPageOutput[neutral.Pages.Count]; }
            catch { cutFrameReservation.Dispose(); throw; }
        }
        for (var index = 0; index < neutral.Pages.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = RenderPage(provider, neutral.Pages[index], index, false, cancellationToken);
            var contribution = frame.ByteLength * options.Copies;
            if (contribution > maxOutputBytes - predicted) throw new ArgumentException("任务输出超过字节预算。");
            predicted += contribution;
            frames.Add(frame);
        }
        var feedBytes = feedLength == 0 ? Array.Empty<byte>() :
            internalProvider.BuildJobFinishing(options.FeedAmount, options.FeedUnit, false, cancellationToken);
        var cutBytes = cutLength == 0 || internalProvider.JobCutInPage ? Array.Empty<byte>() :
            options.CutOptions == null
                ? internalProvider.BuildJobFinishing(0, options.FeedUnit, true, cancellationToken)
                : internalProvider.BuildJobCut(options.CutOptions, cancellationToken);
        var result = new BudgetedList<PrintJobPage>("Job.Pages", itemBytes: 64,
            capacity: checked((int)count));
        var ordinal = 0L;
        var actualBytes = 0L;
        void Emit(int pageIndex, int copyIndex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var copyEnd = pageIndex == neutral.Pages.Count - 1;
            var feed = options.FeedAmount > 0 && AtBoundary(options.FeedScope, copyEnd, ordinal == count - 1);
            var cut = AtBoundary(options.CutScope, copyEnd, ordinal == count - 1);
            var frame = frames[pageIndex];
            if (cut && internalProvider.JobCutInPage)
            {
                frame = cutFrames[pageIndex];
                if (frame == null)
                {
                    frame = RenderPage(provider, neutral.Pages[pageIndex], pageIndex, true, cancellationToken, options.CutOptions);
                    cutFrames[pageIndex] = frame;
                }
            }
            var finishingLength = (feed ? feedBytes.Length : 0) + (cut ? cutBytes.Length : 0);
            var pageBytes = checked(frame.ByteLength + finishingLength);
            if (pageBytes > maxOutputBytes - actualBytes) throw new ArgumentException("任务输出超过字节预算。");
            actualBytes += pageBytes;
            using (var segmentStorage = new PrintStorageBudget())
            {
                var segmentCount = checked(frame.Segments.Count +
                    (frame.Segments.Count > 1 && finishingLength > 0 ? 1 : 0));
                var segments = new BudgetedList<PrintOutputSegment>("Job.Segments", itemBytes: 8,
                    owner: segmentStorage, capacity: segmentCount);
                foreach (var segment in frame.Segments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var tail = frame.Segments.Count == 1 ? finishingLength : 0;
                    segments.AddFactory(() =>
                    {
                        var bytes = Core.BudgetedBuffer.Create(checked(segment.ByteLength + tail), "Job.OutputSegment",
                            output =>
                            {
                                Core.BufferCopy.Copy(segment.Data, 0, output, 0, segment.ByteLength, cancellationToken);
                                if (tail > 0) AppendFinishing(output, segment.ByteLength);
                            });
                        return PrintOutputSegment.FromOwned(bytes, segment.Kind);
                    });
                }
                if (frame.Segments.Count > 1 && finishingLength > 0)
                {
                    segments.AddFactory(() =>
                    {
                        var bytes = Core.BudgetedBuffer.Create(finishingLength, "Job.FinishingSegment",
                            output => AppendFinishing(output, 0));
                        return PrintOutputSegment.FromOwned(bytes, PrintOutputSegmentKind.Control);
                    });
                }
                result.AddFactory(() => new PrintJobPage(pageIndex, copyIndex,
                    new PrintPageOutput(segments), feed, cut));
            }
            ordinal++;

            void AppendFinishing(byte[] bytes, int offset)
            {
                if (feed) Core.BufferCopy.Copy(feedBytes, 0, bytes, offset, feedBytes.Length, cancellationToken);
                if (cut && cutBytes.Length > 0)
                    Core.BufferCopy.Copy(cutBytes, 0, bytes, offset + (feed ? feedBytes.Length : 0), cutBytes.Length, cancellationToken);
            }
        }
        if (options.CopyOrder == PrintCopyOrder.Collated)
            for (var copy = 0; copy < options.Copies; copy++)
                for (var page = 0; page < neutral.Pages.Count; page++) Emit(page, copy);
        else
            for (var page = 0; page < neutral.Pages.Count; page++)
                for (var copy = 0; copy < options.Copies; copy++) Emit(page, copy);
        cancellationToken.ThrowIfCancellationRequested();
        cutFramesStorage.Dispose();
        framesStorage.Dispose();
        neutralStorage.Dispose();
        var output = new PrintJobRenderResult(result);
        operation?.CommitReservations();
        return output;
    }

    private static long EventCount(PrintFinishScope scope, long pages, int copies)
        => pages == 0 || scope == PrintFinishScope.None ? 0 : scope == PrintFinishScope.EachPage ? pages :
            scope == PrintFinishScope.EachCopy ? copies : 1;
    private static bool AtBoundary(PrintFinishScope scope, bool copyEnd, bool jobEnd)
        => scope == PrintFinishScope.EachPage || scope == PrintFinishScope.EachCopy && copyEnd ||
            scope == PrintFinishScope.JobEnd && jobEnd;
    private static PrintPage CopyPage(PrintDocument target, PrintPage source, int sourceIndex, bool cut,
        CancellationToken cancellationToken)
    {
        var page = target.AddPage(source.Width, source.Height);
        page.FeedLines = 0; page.CutAfterPrint = cut;
        for (var index = 0; index < source.Elements.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            page.Add(source.Elements[index], source.Elements[index].SourcePath ?? $"Pages[{sourceIndex}].Elements[{index}]");
        }
        return page;
    }
    private static PrintPageOutput RenderPage(IPrintProtocolProvider provider, PrintPage source, int sourceIndex, bool cut,
        CancellationToken cancellationToken, PrintCutOptions cutOptions = null)
    {
        using var oneStorage = new PrintStorageBudget();
        var one = new PrintDocument(oneStorage);
        var page = CopyPage(one, source, sourceIndex, cut, cancellationToken);
        if (cut) page.CutOptions = cutOptions?.Snapshot();
        try { return provider.RenderOutput(one, cancellationToken)[0]; }
        catch (PrintCapabilityException error)
        { throw new PrintCapabilityException(provider.Protocol, error.ElementPath, error.InnerException ?? error, sourceIndex, error.ElementIndex); }
        catch (PrintElementValidationException error)
        { throw new PrintElementValidationException(provider.Protocol, error.ElementPath, error.InnerException ?? error, sourceIndex, error.ElementIndex); }
    }
}
