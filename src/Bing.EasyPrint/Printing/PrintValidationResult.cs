using System;
using System.Threading;

namespace Bing.EasyPrint.Printing;

/// <summary>打印文档的首次协议校验结果。</summary>
public sealed class PrintValidationResult
{
    /// <summary>获取目标协议。</summary>
    public PrintProtocol Protocol { get; }
    /// <summary>获取文档是否通过校验。</summary>
    public bool IsValid => FirstIssue == null;
    /// <summary>获取首个校验问题；校验通过时为 null。</summary>
    public PrintValidationIssue FirstIssue { get; }

    internal PrintValidationResult(PrintProtocol protocol, PrintValidationIssue firstIssue)
    {
        Protocol = protocol;
        FirstIssue = firstIssue;
    }
}

/// <summary>带元素路径的协议能力或数据问题。</summary>
public sealed class PrintValidationIssue
{
    /// <summary>获取问题所在的页面或元素路径。</summary>
    public string ElementPath { get; }
    /// <summary>获取可显示的问题原因。</summary>
    public string Message { get; }
    /// <summary>获取是否为目标协议不支持的能力。</summary>
    public bool IsCapabilityIssue { get; }
    /// <summary>获取问题所在页面的零基序号；未提供位置时为 null。</summary>
    public int? PageIndex { get; }
    /// <summary>获取问题所在页内元素的零基序号；页级问题为 null。</summary>
    public int? ElementIndex { get; }

    internal PrintValidationIssue(string elementPath, string message, bool isCapabilityIssue)
        : this(elementPath, message, isCapabilityIssue, null, null)
    {
    }

    internal PrintValidationIssue(string elementPath, string message, bool isCapabilityIssue,
        int? pageIndex, int? elementIndex)
    {
        ElementPath = elementPath;
        Message = message;
        IsCapabilityIssue = isCapabilityIssue;
        PageIndex = pageIndex;
        ElementIndex = elementIndex;
    }
}

/// <summary>打印 Provider 的非抛错校验入口。</summary>
public static class PrintProtocolProviderValidationExtensions
{
    /// <summary>执行可取消预检；旧 Provider 在调用前后检查取消。</summary>
    public static void Validate(this IPrintProtocolProvider provider, PrintDocument document,
        CancellationToken cancellationToken)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        cancellationToken.ThrowIfCancellationRequested();
        if (provider is IPrintCancelableProtocolProvider cancelable)
            cancelable.Validate(document, cancellationToken);
        else provider.Validate(document);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>校验文档并返回第一个能力或数据问题；参数错误仍抛异常。</summary>
    public static PrintValidationResult Check(this IPrintProtocolProvider provider, PrintDocument document)
        => Check(provider, document, CancellationToken.None);

    /// <summary>执行可取消校验；取消仍抛出 OperationCanceledException，不作为校验问题返回。</summary>
    public static PrintValidationResult Check(this IPrintProtocolProvider provider, PrintDocument document,
        CancellationToken cancellationToken)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        try
        {
            provider.Validate(document, cancellationToken);
            return new PrintValidationResult(provider.Protocol, null);
        }
        catch (PrintCapabilityException error)
        {
            return new PrintValidationResult(provider.Protocol,
                new PrintValidationIssue(error.ElementPath, error.InnerException?.Message ?? error.Message, true,
                    error.PageIndex, error.ElementIndex));
        }
        catch (PrintElementValidationException error)
        {
            return new PrintValidationResult(provider.Protocol,
                new PrintValidationIssue(error.ElementPath, error.InnerException?.Message ?? error.Message, false,
                    error.PageIndex, error.ElementIndex));
        }
    }
}
