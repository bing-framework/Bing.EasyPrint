using System;
using System.Collections.Generic;
using System.Threading;

namespace Bing.EasyPrint.Printing;

/// <summary>将协议无关打印文档编码为目标指令集的 Provider。</summary>
public interface IPrintProtocolProvider
{
    /// <summary>获取目标协议。</summary>
    PrintProtocol Protocol { get; }
    /// <summary>获取目标协议能力。</summary>
    PrintCapabilities Capabilities { get; }
    /// <summary>验证文档是否能由当前 Provider 表达。</summary>
    void Validate(PrintDocument document);
    /// <summary>生成逐页打印字节。</summary>
    IReadOnlyList<byte[]> Render(PrintDocument document, CancellationToken cancellationToken = default(CancellationToken));
}

/// <summary>可选的可取消预检契约；旧 Provider 不必增加接口成员。</summary>
public interface IPrintCancelableProtocolProvider : IPrintProtocolProvider
{
    /// <summary>校验文档并响应取消；取消不转换为能力或输入错误。</summary>
    void Validate(PrintDocument document, CancellationToken cancellationToken);
}

/// <summary>按协议创建 Provider 的工厂。</summary>
public interface IPrintProtocolProviderFactory
{
    /// <summary>创建指定协议的 Provider。</summary>
    IPrintProtocolProvider Create(PrintProtocol protocol);
}
