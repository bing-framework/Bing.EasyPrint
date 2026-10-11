using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Bing.EasyPrint.Printing;

/// <summary>独立发送文件的用途；文件边界不能按普通缓冲块合并。</summary>
public enum PrintOutputSegmentKind
{
    /// <summary>页面格式或受信任的原始命令。</summary>
    Commands = 0,
    /// <summary>须在前一文件之后独立发送的控制命令。</summary>
    Control = 1
}

/// <summary>一个独立发送的命令文件；发送方须保留顺序和文件边界。</summary>
public sealed class PrintOutputSegment
{
    /// <summary>文件用途。</summary>
    public PrintOutputSegmentKind Kind { get; }
    /// <summary>文件字节；与旧页面数据一致，发送期间不得修改。</summary>
    public byte[] Data { get; }
    /// <summary>文件字节数。</summary>
    public int ByteLength => Data.Length;

    /// <summary>复制输入字节，创建独立文件。</summary>
    public PrintOutputSegment(byte[] data, PrintOutputSegmentKind kind = PrintOutputSegmentKind.Commands)
        : this(data, kind, false) { }

    private PrintOutputSegment(byte[] data, PrintOutputSegmentKind kind, bool owned)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (!Enum.IsDefined(typeof(PrintOutputSegmentKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        using var operation = PrintResourceScope.BeginTemporary();
        PrintResourceScope.Retain(32, "Output.SegmentMetadata");
        Kind = kind;
        Data = owned ? data : Core.BudgetedBuffer.Copy(data, "Output.SegmentSnapshot", CancellationToken.None);
        operation?.CommitReservations();
    }

    internal static PrintOutputSegment FromOwned(byte[] data, PrintOutputSegmentKind kind = PrintOutputSegmentKind.Commands)
        => new PrintOutputSegment(data, kind, true);
}

/// <summary>一个逻辑或物理页面的有序文件集合；不把独立控制文件拼接到页面末尾。</summary>
public sealed class PrintPageOutput
{
    /// <summary>须逐个独立发送的文件，顺序即发送顺序。</summary>
    public IReadOnlyList<PrintOutputSegment> Segments { get; }
    /// <summary>全部文件的字节数，供输出预算使用。</summary>
    public long ByteLength { get; }

    /// <summary>创建有序输出；集合采用副本，文件数据由调用方保持发送期间不变。</summary>
    public PrintPageOutput(IEnumerable<PrintOutputSegment> segments)
    {
        if (segments == null) throw new ArgumentNullException(nameof(segments));
        using var operation = PrintResourceScope.BeginTemporary();
        PrintResourceScope.Retain(40, "Output.PageMetadata");
        var copy = new Core.BudgetedList<PrintOutputSegment>("Output.Segments");
        foreach (var segment in segments)
        {
            if (segment == null) throw new ArgumentException("页面输出必须包含至少一个非空文件。", nameof(segments));
            copy.Add(segment);
            ByteLength = checked(ByteLength + segment.ByteLength);
        }
        if (copy.Count == 0)
            throw new ArgumentException("页面输出必须包含至少一个非空文件。", nameof(segments));
        Segments = copy.AsReadOnly();
        operation?.CommitReservations();
    }

    /// <summary>获取旧单文件数据；多文件输出须使用 Segments，禁止静默合并。</summary>
    public byte[] Data => Segments.Count == 1 ? Segments[0].Data
        : throw new NotSupportedException("页面包含须独立发送的文件；请按 Segments 顺序分别发送，不能合并为 Data。");

    internal static PrintPageOutput FromOwned(byte[] data)
        => new PrintPageOutput(new[] { PrintOutputSegment.FromOwned(data) });
}

/// <summary>支持独立输出文件的可选 Provider 契约；不增加旧 Provider 的必实现成员。</summary>
public interface IPrintOutputProtocolProvider : IPrintProtocolProvider
{
    /// <summary>生成逐页有序文件；失败时不返回部分输出。</summary>
    IReadOnlyList<PrintPageOutput> RenderOutput(PrintDocument document,
        CancellationToken cancellationToken = default(CancellationToken));
}

/// <summary>为旧 Provider 适配单文件输出。</summary>
public static class PrintOutputProviderExtensions
{
    /// <summary>生成页面文件；旧 Provider 的每页字节保持为一个文件。</summary>
    public static IReadOnlyList<PrintPageOutput> RenderOutput(this IPrintProtocolProvider provider,
        PrintDocument document, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = PrintResourceScope.BeginTemporary();
        if (provider is IPrintOutputProtocolProvider output)
        {
            var direct = output.RenderOutput(document, cancellationToken);
            operation?.CommitReservations();
            return direct;
        }
        var pages = provider.Render(document, cancellationToken);
        var result = new Core.BudgetedList<PrintPageOutput>("Output.Pages", itemBytes: 8,
            capacity: pages.Count);
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.AddFactory(() => PrintPageOutput.FromOwned(page));
        }
        var outputPages = result.AsReadOnly();
        operation?.CommitReservations();
        return outputPages;
    }
}
