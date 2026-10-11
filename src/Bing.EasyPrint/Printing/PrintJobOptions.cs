using System;

namespace Bing.EasyPrint.Printing;

/// <summary>显式打印任务策略；不设置任务策略时保留旧页面设置。</summary>
public sealed class PrintJobOptions
{
    /// <summary>整个文档的打印份数。</summary>
    public int Copies { get; set; } = 1;
    /// <summary>多页文档的份数顺序。</summary>
    public PrintCopyOrder CopyOrder { get; set; } = PrintCopyOrder.Collated;
    /// <summary>额外进纸的执行边界。</summary>
    public PrintFinishScope FeedScope { get; set; } = PrintFinishScope.None;
    /// <summary>进纸单位。</summary>
    public PrintFeedUnit FeedUnit { get; set; } = PrintFeedUnit.Lines;
    /// <summary>每次额外进纸的数量，零表示不进纸。</summary>
    public int FeedAmount { get; set; }
    /// <summary>切纸执行边界。</summary>
    public PrintFinishScope CutScope { get; set; } = PrintFinishScope.None;
    /// <summary>任务切纸边界使用的显式裁切参数；为空时保留原有输出。</summary>
    public PrintCutOptions CutOptions { get; set; }

    /// <summary>校验任务参数；协议和实际页数的限制由任务渲染器继续检查。</summary>
    public void Validate()
    {
        if (Copies < 1) throw new ArgumentOutOfRangeException(nameof(Copies));
        if (FeedAmount < 0) throw new ArgumentOutOfRangeException(nameof(FeedAmount));
        if (!Enum.IsDefined(typeof(PrintCopyOrder), CopyOrder)) throw new ArgumentOutOfRangeException(nameof(CopyOrder));
        if (!Enum.IsDefined(typeof(PrintFinishScope), FeedScope)) throw new ArgumentOutOfRangeException(nameof(FeedScope));
        if (!Enum.IsDefined(typeof(PrintFinishScope), CutScope)) throw new ArgumentOutOfRangeException(nameof(CutScope));
        if (!Enum.IsDefined(typeof(PrintFeedUnit), FeedUnit)) throw new ArgumentOutOfRangeException(nameof(FeedUnit));
        if (FeedAmount > 0 && FeedScope == PrintFinishScope.None)
            throw new ArgumentException("正数进纸数量必须指定执行边界。", nameof(FeedScope));
        if (CutOptions != null)
        {
            CutOptions.Validate();
            if (CutScope == PrintFinishScope.None)
                throw new ArgumentException("显式裁切参数必须指定切纸执行边界。", nameof(CutScope));
        }
    }

    internal PrintJobOptions Snapshot() => new PrintJobOptions
    {
        Copies = Copies, CopyOrder = CopyOrder, FeedScope = FeedScope,
        FeedUnit = FeedUnit, FeedAmount = FeedAmount, CutScope = CutScope,
        CutOptions = CutOptions?.Snapshot()
    };
}
