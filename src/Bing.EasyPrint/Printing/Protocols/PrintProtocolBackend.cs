using System;
using System.Collections.Generic;
using System.Threading;

namespace Bing.EasyPrint.Printing;

/// <summary>
/// 按请求创建和释放的新协议后端会话契约；后端接收统一 <see cref="PrintDocument"/> 并返回有序 <see cref="PrintPageOutput"/>。
/// </summary>
/// <remarks>
/// 实现必须在取消、异常和预算失败时释放自身资源，不得返回部分输出。会话不跨请求复用。
/// </remarks>
public interface IPrintProtocolBackend : IDisposable
{
    /// <summary>获取会话对应的协议标识。</summary>
    string ProtocolId { get; }

    /// <summary>校验文档是否可由当前会话完整表达；失败时不得产生输出。</summary>
    /// <param name="document">协议无关打印文档。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    void Validate(PrintDocument document, CancellationToken cancellationToken);

    /// <summary>生成逐页有序文件；集合顺序即发送顺序。</summary>
    /// <param name="document">协议无关打印文档。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与文档页数一致的有序页面输出。</returns>
    IReadOnlyList<PrintPageOutput> Render(PrintDocument document, CancellationToken cancellationToken);
}

/// <summary>
/// 可声明任务收尾能力的新协议后端；未实现或未声明时不接受对应收尾请求。
/// </summary>
public interface IPrintProtocolFinishingBackend : IPrintProtocolBackend
{
    /// <summary>获取是否声明任务收尾能力。</summary>
    bool SupportsFinishing { get; }

    /// <summary>预检收尾请求；不支持或越界时明确失败。</summary>
    /// <param name="document">本次任务的协议无关打印文档。</param>
    /// <param name="request">收尾请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    void ValidateFinishing(PrintDocument document, PrintProtocolFinishingRequest request,
        CancellationToken cancellationToken);

    /// <summary>返回收尾文件准确字节长度；用于输出预算预检。</summary>
    /// <param name="request">收尾请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>收尾文件字节长度。</returns>
    long GetFinishingLength(PrintProtocolFinishingRequest request, CancellationToken cancellationToken);

    /// <summary>生成独立收尾控制文件；调用方须按位置单独发送，不得拼接进页面文件。</summary>
    /// <param name="request">收尾请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含一个或多个独立控制段的收尾输出。</returns>
    PrintPageOutput RenderFinishing(PrintProtocolFinishingRequest request, CancellationToken cancellationToken);
}
