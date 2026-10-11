using System;

namespace Bing.EasyPrint.Printing;

/// <summary>描述打印元素与目标协议不兼容的错误。</summary>
public sealed class PrintCapabilityException : NotSupportedException
{
    /// <summary>获取目标协议。</summary>
    public PrintProtocol Protocol { get; }
    /// <summary>获取文档元素或模板节点路径。</summary>
    public string ElementPath { get; }
    /// <summary>获取失败页面的零基序号；旧调用未提供时为 null。</summary>
    public int? PageIndex { get; }
    /// <summary>获取失败元素在页面中的零基序号；页级错误为 null。</summary>
    public int? ElementIndex { get; }

    internal PrintCapabilityException(PrintProtocol protocol, string elementPath, Exception cause)
        : this(protocol, elementPath, cause, null, null)
    {
    }

    internal PrintCapabilityException(PrintProtocol protocol, string elementPath, Exception cause,
        int? pageIndex, int? elementIndex)
        : base($"协议 {protocol} 不支持元素 {elementPath}：{cause.Message}", cause)
    {
        Protocol = protocol;
        ElementPath = elementPath;
        PageIndex = pageIndex;
        ElementIndex = elementIndex;
    }
}
