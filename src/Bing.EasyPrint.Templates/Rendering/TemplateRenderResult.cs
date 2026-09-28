using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 表示带有模板节点路径的渲染错误。
/// </summary>
public sealed class TemplateRenderException : System.ArgumentException
{
    /// <summary>
    /// 获取导致错误的模板节点路径。
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// 初始化一个 <see cref="TemplateRenderException"/> 类型的实例。
    /// </summary>
    /// <param name="sourcePath">发生错误的模板节点路径。</param>
    /// <param name="message">供调用方显示的错误原因。</param>
    /// <param name="innerException">导致当前错误的异常。</param>
    internal TemplateRenderException(string sourcePath, string message, System.Exception innerException)
        : base(message, innerException)
    {
        SourcePath = sourcePath;
    }
}

/// <summary>
/// 按资源键提供模板图片内容。
/// </summary>
public interface ITemplateResourceProvider
{
    /// <summary>
    /// 打开指定图片资源的只读流。
    /// </summary>
    /// <param name="resourceKey">模板引用的资源键，不作为文件路径或 URL 解析。</param>
    /// <param name="cancellationToken">取消资源读取的令牌。</param>
    /// <returns>由渲染器负责释放的可读流；资源不存在时返回 null。</returns>
    Stream OpenRead(string resourceKey, CancellationToken cancellationToken);
}

/// <summary>
/// 保存单页的 CPCL 数据与布局结果。
/// </summary>
public sealed class TemplatePage
{
    /// <summary>
    /// 保存无布局轨迹时复用的空节点集合。
    /// </summary>
    private static readonly IReadOnlyList<TemplateLayoutElement> EmptyElements = Array.AsReadOnly(Array.Empty<TemplateLayoutElement>());

    /// <summary>
    /// 获取以打印点表示的页宽。
    /// </summary>
    public int Width { get; }
    /// <summary>
    /// 获取以打印点表示的页高。
    /// </summary>
    public int Height { get; }
    /// <summary>
    /// 获取当前页的 CPCL 指令字节。
    /// </summary>
    public byte[] Data { get; }
    /// <summary>
    /// 获取当前页的布局节点；普通渲染返回空集合。
    /// </summary>
    public IReadOnlyList<TemplateLayoutElement> Elements { get; }
    /// <summary>
    /// 初始化一个 <see cref="TemplatePage"/> 类型的实例。
    /// </summary>
    /// <param name="width">打印点页宽。</param>
    /// <param name="height">打印点页高。</param>
    /// <param name="data">当前页的 CPCL 字节。</param>
    /// <param name="elements">可选的布局节点集合。</param>
    internal TemplatePage(int width, int height, byte[] data, List<TemplateLayoutElement> elements = null)
    {
        Width = width;
        Height = height;
        Data = data;
        Elements = elements?.AsReadOnly() ?? EmptyElements;
    }
}

/// <summary>
/// 记录模板节点在某页中的实际位置。
/// </summary>
public sealed class TemplateLayoutElement
{
    /// <summary>
    /// 获取对应模板定义的节点路径。
    /// </summary>
    public string SourcePath { get; }
    /// <summary>
    /// 获取节点类别。
    /// </summary>
    public string Kind { get; }
    /// <summary>
    /// 获取元素类型编号；非元素节点使用默认值。
    /// </summary>
    public int Type { get; }
    /// <summary>
    /// 获取重复数据源中各层的项序号。
    /// </summary>
    public IReadOnlyList<int> InstanceIndices { get; }
    /// <summary>
    /// 获取节点左侧的打印点坐标。
    /// </summary>
    public int X { get; }
    /// <summary>
    /// 获取节点顶部的打印点坐标。
    /// </summary>
    public int Y { get; }
    /// <summary>
    /// 获取节点的打印点宽度。
    /// </summary>
    public int Width { get; }
    /// <summary>
    /// 获取节点的打印点高度。
    /// </summary>
    public int Height { get; }
    /// <summary>
    /// 获取文字或码图的绑定内容；其他节点返回 null。
    /// </summary>
    public string Text { get; }
    /// <summary>
    /// 获取图片资源键；其他节点返回 null。
    /// </summary>
    public string ResourceKey { get; }

    /// <summary>
    /// 初始化一个 <see cref="TemplateLayoutElement"/> 类型的实例。
    /// </summary>
    /// <param name="sourcePath">模板定义的节点路径。</param>
    /// <param name="kind">节点类别。</param>
    /// <param name="type">元素类型编号。</param>
    /// <param name="instanceIndices">重复明细的各层项序号。</param>
    /// <param name="x">节点左侧的打印点坐标。</param>
    /// <param name="y">节点顶部的打印点坐标。</param>
    /// <param name="width">打印点宽度。</param>
    /// <param name="height">打印点高度。</param>
    /// <param name="text">可选的文字或码图内容。</param>
    /// <param name="resourceKey">可选的图片资源键。</param>
    internal TemplateLayoutElement(string sourcePath, string kind, int type, IReadOnlyList<int> instanceIndices,
        int x, int y, int width, int height, string text, string resourceKey)
    {
        SourcePath = sourcePath;
        Kind = kind;
        Type = type;
        InstanceIndices = instanceIndices;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Text = text;
        ResourceKey = resourceKey;
    }
}

/// <summary>
/// 保存完整模板渲染后的页面。
/// </summary>
/// <remarks>
/// 渲染失败时不会返回部分页面。
/// </remarks>
public sealed class TemplateRenderResult
{
    /// <summary>
    /// 获取已生成的所有打印页。
    /// </summary>
    public IReadOnlyList<TemplatePage> Pages { get; }
    /// <summary>
    /// 获取已生成的页数。
    /// </summary>
    public int PageCount => Pages.Count;
    /// <summary>
    /// 初始化一个 <see cref="TemplateRenderResult"/> 类型的实例。
    /// </summary>
    /// <param name="pages">已生成的完整页面集合。</param>
    internal TemplateRenderResult(List<TemplatePage> pages) { Pages = pages.AsReadOnly(); }
}
