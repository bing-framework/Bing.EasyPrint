using System;

namespace Bing.EasyPrint.Printing;

/// <summary>
/// 描述新协议后端在预检或渲染时的结构化错误；不依赖旧 <see cref="PrintProtocol"/> 枚举。
/// </summary>
public sealed class PrintProtocolBackendException : Exception
{
    /// <summary>获取失败协议标识。</summary>
    public string ProtocolId { get; }
    /// <summary>获取可选的模板节点或文档元素路径。</summary>
    public string ElementPath { get; }
    /// <summary>获取可选的零基页面序号。</summary>
    public int? PageIndex { get; }
    /// <summary>获取可选的页内零基元素序号。</summary>
    public int? ElementIndex { get; }
    /// <summary>获取是否属于能力不支持；否则视为输入/配置校验失败。</summary>
    public bool IsCapabilityFailure { get; }

    /// <summary>
    /// 初始化一个结构化新协议后端错误。
    /// </summary>
    /// <param name="protocolId">失败协议标识。</param>
    /// <param name="message">面向调用方的错误消息。</param>
    /// <param name="elementPath">可选节点或元素路径。</param>
    /// <param name="pageIndex">可选页面序号。</param>
    /// <param name="elementIndex">可选元素序号。</param>
    /// <param name="isCapabilityFailure">是否为能力不支持。</param>
    /// <param name="innerException">可选内部异常。</param>
    public PrintProtocolBackendException(string protocolId, string message, string elementPath = null,
        int? pageIndex = null, int? elementIndex = null, bool isCapabilityFailure = false,
        Exception innerException = null)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(protocolId))
            throw new ArgumentException("协议标识不能为空。", nameof(protocolId));
        ProtocolId = protocolId;
        ElementPath = elementPath;
        PageIndex = pageIndex;
        ElementIndex = elementIndex;
        IsCapabilityFailure = isCapabilityFailure;
    }
}
