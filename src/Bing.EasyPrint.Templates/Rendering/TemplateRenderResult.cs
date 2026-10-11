using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Bing.EasyPrint.Core;

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

    /// <summary>获取绑定失败的业务字段路径；非绑定错误返回 null。</summary>
    /// <remarks>元数据沿内部异常链保留，不依赖错误文案解析。</remarks>
    public string FieldPath => TemplateBindingDiagnostic.GetFieldPath(this);

    /// <summary>
    /// 获取已知图片输入错误对应的资源键；其他错误返回空值。
    /// </summary>
    public string ResourceName => TemplateImageDiagnostic.GetResourceName(this);

    /// <summary>
    /// 获取已识别图片源图的像素宽度；资源缺失、损坏或尚未识别时返回空值。
    /// </summary>
    public int? SourcePixelWidth => TemplateImageDiagnostic.GetSourcePixelWidth(this);

    /// <summary>
    /// 获取已识别图片源图的像素高度；资源缺失、损坏或尚未识别时返回空值。
    /// </summary>
    public int? SourcePixelHeight => TemplateImageDiagnostic.GetSourcePixelHeight(this);

    /// <summary>
    /// 获取图片解码、裁切和缩放后实际输出的像素宽度；无法完成处理时返回空值。
    /// </summary>
    public int? RenderedPixelWidth => TemplateImageDiagnostic.GetRenderedPixelWidth(this);

    /// <summary>
    /// 获取图片解码、裁切和缩放后实际输出的像素高度；无法完成处理时返回空值。
    /// </summary>
    public int? RenderedPixelHeight => TemplateImageDiagnostic.GetRenderedPixelHeight(this);

    /// <summary>
    /// 获取请求裁切区域的源图像素 X 坐标；未请求裁切时返回空值。
    /// </summary>
    public int? CropX => TemplateImageDiagnostic.GetCropX(this);

    /// <summary>
    /// 获取请求裁切区域的源图像素 Y 坐标；未请求裁切时返回空值。
    /// </summary>
    public int? CropY => TemplateImageDiagnostic.GetCropY(this);

    /// <summary>
    /// 获取请求裁切区域的源图像素宽度；未请求裁切时返回空值。
    /// </summary>
    public int? CropWidth => TemplateImageDiagnostic.GetCropWidth(this);

    /// <summary>
    /// 获取请求裁切区域的源图像素高度；未请求裁切时返回空值。
    /// </summary>
    public int? CropHeight => TemplateImageDiagnostic.GetCropHeight(this);

    /// <summary>
    /// 获取导致错误的组件定义路径。
    /// </summary>
    /// <remarks>普通模板节点或内联组合没有组件定义路径时返回 null。</remarks>
    public string DefinitionPath { get; }

    /// <summary>
    /// 获取触发错误的组件实例路径。
    /// </summary>
    /// <remarks>错误发生在共享组件内部时返回引用该组件的模板节点路径。</remarks>
    public string ComponentInstancePath { get; }

    /// <summary>
    /// 获取错误所在重复数据源的各层项序号。
    /// </summary>
    public IReadOnlyList<int> InstanceIndices { get; }

    /// <summary>获取实际输出页面的零基序号；静态预检或尚未分页时为 null。</summary>
    public int? PageIndex { get; private set; }

    /// <summary>获取页面内实际打印元素的零基序号；页级错误或尚未输出时为 null。</summary>
    public int? ElementIndex { get; private set; }

    /// <summary>
    /// 初始化一个 <see cref="TemplateRenderException"/> 类型的实例。
    /// </summary>
    /// <param name="sourcePath">发生错误的模板节点路径。</param>
    /// <param name="message">供调用方显示的错误原因。</param>
    /// <param name="innerException">导致当前错误的异常。</param>
    internal TemplateRenderException(string sourcePath, string message, System.Exception innerException)
        : this(sourcePath, message, innerException, null, null, null)
    {
    }

    /// <summary>
    /// 使用可选的组件归属元数据初始化渲染错误。
    /// </summary>
    /// <param name="sourcePath">发生错误的模板节点路径。</param>
    /// <param name="message">供调用方显示的错误原因。</param>
    /// <param name="innerException">导致当前错误的异常。</param>
    /// <param name="definitionPath">组件定义节点路径。</param>
    /// <param name="componentInstancePath">组件实例节点路径。</param>
    /// <param name="instanceIndices">重复数据源的各层项序号。</param>
    internal TemplateRenderException(string sourcePath, string message, System.Exception innerException,
        string definitionPath, string componentInstancePath, IReadOnlyList<int> instanceIndices)
        : base(message, innerException)
    {
        SourcePath = sourcePath;
        DefinitionPath = definitionPath;
        ComponentInstancePath = componentInstancePath;
        if (instanceIndices != null)
        {
            var copy = new int[instanceIndices.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = instanceIndices[index];
            InstanceIndices = Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// 用外层组件上下文补充当前错误，但保留已经由内层组件设置的字段。
    /// </summary>
    /// <param name="definitionPath">可补充的组件定义路径。</param>
    /// <param name="componentInstancePath">可补充的组件实例路径。</param>
    /// <param name="instanceIndices">可补充的重复数据项序号。</param>
    /// <returns>包含最内层已知归属的错误实例。</returns>
    internal TemplateRenderException WithOwnership(string definitionPath, string componentInstancePath,
        IReadOnlyList<int> instanceIndices)
    {
        var mergedDefinitionPath = DefinitionPath ?? definitionPath;
        var mergedComponentInstancePath = ComponentInstancePath ?? componentInstancePath;
        var mergedInstanceIndices = InstanceIndices ?? instanceIndices;
        if (DefinitionPath == mergedDefinitionPath &&
            ComponentInstancePath == mergedComponentInstancePath &&
            ReferenceEquals(InstanceIndices, mergedInstanceIndices))
            return this;
        return new TemplateRenderException(SourcePath, Message, this,
            mergedDefinitionPath, mergedComponentInstancePath, mergedInstanceIndices)
            .WithLocation(PageIndex, ElementIndex);
    }

    /// <summary>为 Provider 阶段错误补充实际页面位置，保留原有模板归属。</summary>
    internal TemplateRenderException WithLocation(int? pageIndex, int? elementIndex)
    {
        if (PageIndex == pageIndex && ElementIndex == elementIndex) return this;
        return new TemplateRenderException(SourcePath, Message, this,
            DefinitionPath, ComponentInstancePath, InstanceIndices)
        {
            PageIndex = pageIndex,
            ElementIndex = elementIndex
        };
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
/// 保存单页的协议数据与布局结果。
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
    /// <summary>获取当前页使用的打印协议。</summary>
    public PrintProtocol Protocol { get; }
    /// <summary>
    /// 获取单文件页的目标协议字节；多文件页抛错，须使用 Segments。
    /// </summary>
    public byte[] Data => Output.Data;
    /// <summary>当前页有序输出；多文件页面不能通过 Data 合并发送。</summary>
    public Printing.PrintPageOutput Output { get; }
    /// <summary>须按顺序独立发送的命令文件。</summary>
    public IReadOnlyList<Printing.PrintOutputSegment> Segments => Output.Segments;
    /// <summary>全部命令文件字节数。</summary>
    public long ByteLength => Output.ByteLength;
    /// <summary>显式任务输出对应的原逻辑页序号；旧页面模式为 null。</summary>
    public int? SourcePageIndex { get; }
    /// <summary>显式任务输出对应的零基份数序号；旧页面模式为 null。</summary>
    public int? CopyIndex { get; }
    /// <summary>显式任务是否在本页后额外进纸；旧页面模式为 null。</summary>
    public bool? FeedAfterPrint { get; }
    /// <summary>显式任务是否在本页后切纸；旧页面模式为 null。</summary>
    public bool? CutAfterPrint { get; }
    /// <summary>
    /// 获取当前页的布局节点；普通渲染返回空集合。
    /// </summary>
    public IReadOnlyList<TemplateLayoutElement> Elements { get; }
    /// <summary>
    /// 初始化一个 <see cref="TemplatePage"/> 类型的实例。
    /// </summary>
    /// <param name="width">打印点页宽。</param>
    /// <param name="height">打印点页高。</param>
    /// <param name="data">当前页的协议字节。</param>
    /// <param name="elements">可选的布局节点集合。</param>
    /// <param name="protocol">当前页使用的打印协议。</param>
    /// <param name="sourcePageIndex">显式任务对应的原逻辑页序号。</param>
    /// <param name="copyIndex">显式任务对应的份数序号。</param>
    /// <param name="feedAfterPrint">显式任务是否在本页后额外进纸。</param>
    /// <param name="cutAfterPrint">显式任务是否在本页后切纸。</param>
    internal TemplatePage(int width, int height, byte[] data, IEnumerable<TemplateLayoutElement> elements = null,
        PrintProtocol protocol = PrintProtocol.Cpcl, int? sourcePageIndex = null, int? copyIndex = null,
        bool? feedAfterPrint = null, bool? cutAfterPrint = null)
        : this(width, height, new Printing.PrintPageOutput(new[] { new Printing.PrintOutputSegment(data) }),
            elements, protocol, sourcePageIndex, copyIndex, feedAfterPrint, cutAfterPrint) { }

    internal TemplatePage(int width, int height, Printing.PrintPageOutput output, IEnumerable<TemplateLayoutElement> elements = null,
        PrintProtocol protocol = PrintProtocol.Cpcl, int? sourcePageIndex = null, int? copyIndex = null,
        bool? feedAfterPrint = null, bool? cutAfterPrint = null)
    {
        Printing.PrintResourceScope.Retain(64, "Template.PageResult");
        Width = width;
        Height = height;
        Output = output;
        Protocol = protocol;
        SourcePageIndex = sourcePageIndex; CopyIndex = copyIndex;
        FeedAfterPrint = feedAfterPrint; CutAfterPrint = cutAfterPrint;
        if (elements == null) Elements = EmptyElements;
        else
        {
            var copy = new BudgetedList<TemplateLayoutElement>("Template.ResultLayout",
                retainedSize: element => element.StorageBytes);
            copy.AddRange(elements);
            Elements = copy.AsReadOnly();
        }
    }
}

/// <summary>
/// 记录模板节点在某页中的实际位置。
/// </summary>
public sealed class TemplateLayoutElement
{
    internal long StorageBytes => checked(112L + PrintStorageBudget.TextBytes(SourcePath) +
        PrintStorageBudget.TextBytes(DefinitionPath) + PrintStorageBudget.TextBytes(ComponentInstancePath) +
        PrintStorageBudget.TextBytes(Kind) + PrintStorageBudget.TextBytes(Text) + PrintStorageBudget.TextBytes(ResourceKey) +
        (InstanceIndices?.Count ?? 0) * 4L);
    /// <summary>
    /// 获取对应模板定义的节点路径。
    /// </summary>
    public string SourcePath { get; }
    /// <summary>
    /// 获取节点所属的组件定义路径。
    /// </summary>
    /// <remarks>
    /// 内联节点返回 null。
    /// </remarks>
    public string DefinitionPath { get; }
    /// <summary>
    /// 获取节点所属的组件实例路径。
    /// </summary>
    /// <remarks>
    /// 普通节点返回 null。
    /// </remarks>
    public string ComponentInstancePath { get; }
    /// <summary>
    /// 获取该节点所在的零基页面片段序号。
    /// </summary>
    public int FragmentIndex { get; }
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
    /// <summary>获取元素的逻辑顺时针旋转角度；宽高已经是旋转后的占位。</summary>
    public RotationAngle Rotation { get; }

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
    /// <param name="definitionPath">组件定义节点路径。</param>
    /// <param name="componentInstancePath">组件实例节点路径。</param>
    /// <param name="fragmentIndex">零基页面片段序号。</param>
    /// <param name="rotation">元素的逻辑顺时针旋转角度。</param>
    internal TemplateLayoutElement(string sourcePath, string kind, int type, IReadOnlyList<int> instanceIndices,
        int x, int y, int width, int height, string text, string resourceKey,
        string definitionPath = null, string componentInstancePath = null, int fragmentIndex = 0,
        RotationAngle rotation = RotationAngle.None)
    {
        SourcePath = sourcePath;
        DefinitionPath = definitionPath;
        ComponentInstancePath = componentInstancePath;
        FragmentIndex = fragmentIndex;
        Rotation = rotation;
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
    /// <summary>获取本次渲染使用的打印协议。</summary>
    public PrintProtocol Protocol { get; }
    /// <summary>
    /// 获取已生成的页数。
    /// </summary>
    public int PageCount => Pages.Count;
    /// <summary>
    /// 初始化一个 <see cref="TemplateRenderResult"/> 类型的实例。
    /// </summary>
    /// <param name="pages">已生成的完整页面集合。</param>
    /// <param name="protocol">本次渲染使用的打印协议。</param>
    internal TemplateRenderResult(IEnumerable<TemplatePage> pages, PrintProtocol protocol = PrintProtocol.Cpcl)
    {
        Printing.PrintResourceScope.Retain(32, "Template.Result");
        var copy = new BudgetedList<TemplatePage>("Template.ResultPages");
        copy.AddRange(pages);
        Pages = copy.AsReadOnly();
        Protocol = protocol;
    }
}
