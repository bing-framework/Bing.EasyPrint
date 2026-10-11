using System;

namespace Bing.EasyPrint.Printing;

/// <summary>描述带有页面或元素路径的打印数据校验错误。</summary>
public sealed class PrintElementValidationException : ArgumentException
{
    /// <summary>获取目标协议。</summary>
    public PrintProtocol Protocol { get; }
    /// <summary>获取文档页面或元素路径。</summary>
    public string ElementPath { get; }
    /// <summary>获取失败页面的零基序号；旧调用未提供时为 null。</summary>
    public int? PageIndex { get; }
    /// <summary>获取失败元素在页面中的零基序号；页级错误为 null。</summary>
    public int? ElementIndex { get; }

    internal PrintElementValidationException(PrintProtocol protocol, string elementPath, Exception cause)
        : this(protocol, elementPath, cause, null, null)
    {
    }

    internal PrintElementValidationException(PrintProtocol protocol, string elementPath, Exception cause,
        int? pageIndex, int? elementIndex)
        : base($"协议 {protocol} 的 {elementPath} 无效：{cause.Message}", cause)
    {
        Protocol = protocol;
        ElementPath = elementPath;
        PageIndex = pageIndex;
        ElementIndex = elementIndex;
    }
}
