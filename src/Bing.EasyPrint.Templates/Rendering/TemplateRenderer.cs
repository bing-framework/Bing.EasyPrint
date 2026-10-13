using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 将模板与业务数据渲染为目标协议页面。
/// </summary>
/// <remarks>
/// 渲染器不维护共享的可变状态；调用方不得在渲染期间修改传入的模板、数据或选项。
/// </remarks>
public sealed class TemplateRenderer
{
    /// <summary>
    /// 渲染模板并生成逐页打印数据。
    /// </summary>
    /// <param name="template">待渲染的模板。</param>
    /// <param name="data">用于字段绑定的根业务数据。</param>
    /// <param name="resources">按资源键提供图片内容的可选资源提供器。</param>
    /// <param name="options">本次渲染的可选资源限制及编码配置。</param>
    /// <param name="cancellationToken">取消渲染的令牌。</param>
    /// <returns>包含所有目标协议页面的渲染结果。</returns>
    public TemplateRenderResult Render(BpTemplate template, object data, ITemplateResourceProvider resources = null,
        TemplateRenderOptions options = null, CancellationToken cancellationToken = default)
        => RenderCore(template, data, resources, options, cancellationToken, false);

    /// <summary>
    /// 渲染模板并生成逐页打印数据与布局轨迹。
    /// </summary>
    /// <param name="template">待渲染的模板。</param>
    /// <param name="data">用于字段绑定的根业务数据。</param>
    /// <param name="resources">按资源键提供图片内容的可选资源提供器。</param>
    /// <param name="options">本次渲染的可选资源限制及编码配置。</param>
    /// <param name="cancellationToken">取消渲染的令牌。</param>
    /// <returns>包含目标协议页面及可定位布局节点的渲染结果。</returns>
    public TemplateRenderResult RenderWithLayout(BpTemplate template, object data, ITemplateResourceProvider resources = null,
        TemplateRenderOptions options = null, CancellationToken cancellationToken = default)
        => RenderCore(template, data, resources, options, cancellationToken, true);

    /// <summary>
    /// 按指定模式完成模板渲染。
    /// </summary>
    /// <param name="template">待渲染的模板。</param>
    /// <param name="data">字段绑定的根业务数据。</param>
    /// <param name="resources">可选的图片资源提供器。</param>
    /// <param name="options">本次渲染的可选配置。</param>
    /// <param name="cancellationToken">取消渲染的令牌。</param>
    /// <param name="captureLayout">是否同时记录布局轨迹。</param>
    /// <returns>完整的逐页渲染结果。</returns>
    private static TemplateRenderResult RenderCore(BpTemplate template, object data, ITemplateResourceProvider resources,
        TemplateRenderOptions options, CancellationToken cancellationToken, bool captureLayout)
    {
        options = options ?? new TemplateRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        using var resourceScope = options.MaxWorkingBytes.HasValue
            ? PrintResourceScope.Begin(options.MaxWorkingBytes.Value, cancellationToken) : PrintResourceScope.BeginTemporary();
        if (options.EscPosDevice?.RasterFonts != null)
            foreach (var font in options.EscPosDevice.RasterFonts.Fonts)
                PrintResourceScope.Retain(font.ByteLength, "Template.FontData");
        options.Validate();
        TemplateGraph.Validate(template, options, false, cancellationToken,
            captureLayout || options.Protocol != PrintProtocol.Cpcl, checkProtocol: true);
        var encoding = ResolveEncoding(options);
        var provider = PrintProtocolProviderFactory.Create(options.Protocol, encoding, options.EscPosLayout,
            options.EscPosDevice, options.TsplMedia, options.ZplMedia, options.RequireDeclaredNativeBounds);
        using var preflightStorage = new PrintStorageBudget();
        var preflight = new PrintDocument(preflightStorage);
        var preflightHeight = template.Height == 0 ? options.MaxPageHeight : template.Height;
        // AutoHeight 的实际页高只能由布局确定，不能把配置的布局上限当作设备所需页高。
        if (template.AutoHeight && provider.Capabilities.MaxLabelHeightDots.HasValue) preflightHeight = 1;
        var page = preflight.AddPage(template.Width, preflightHeight);
        page.Copies = options.Copies;
        page.FeedLines = options.Job == null ? options.FeedLines ?? (options.Protocol == PrintProtocol.EscPos ? 1 : 0) : 0;
        page.CutAfterPrint = options.Job == null && (options.CutAfterPrint ?? (options.Protocol == PrintProtocol.EscPos));
        page.CutOptions = options.CutOptions?.Snapshot();
        try
        {
            if (options.Job != null) PrintJobRenderer.ValidateSettings(provider, options.Job);
            provider.Validate(preflight, cancellationToken);
        }
        catch (PrintCapabilityException error)
        { throw new TemplateRenderException(options.Protocol == PrintProtocol.Cpcl ? "Template" : error.ElementPath,
            error.Message, error); }
        catch (PrintElementValidationException error)
        { throw new TemplateRenderException(options.Protocol == PrintProtocol.Cpcl ? "Template" : error.ElementPath,
            error.Message, error); }
        try
        {
            TemplateRenderResult result;
            using (var session = new Session(template, options, resources, cancellationToken, captureLayout))
                result = session.Render(session.BindRoot(data));
            resourceScope?.CommitReservations();
            return result;
        }
        catch (OverflowException error) { throw new ArgumentException("模板尺寸或资源计数溢出。", nameof(template), error); }
    }

    private static Encoding ResolveEncoding(TemplateRenderOptions options)
        => options.Encoding ?? (options.Protocol == PrintProtocol.Zpl ? Encoding.UTF8 : Encoding.GetEncoding("gbk"));

    /// <summary>
    /// 保存可直接绘制的单个布局元素。
    /// </summary>
    private sealed class TemplateDraft
    {
        internal IReadOnlyList<TemplateLayoutElement> Elements { get; }
        internal TemplateDraft(IReadOnlyList<TemplateLayoutElement> elements) => Elements = elements;
    }

    private sealed class Atom
    {
        /// <summary>
        /// 保存布局元素相对当前块的起点与尺寸，单位为打印点。
        /// </summary>
        internal int X, Y, Width, Height;
        /// <summary>
        /// 保存模板源路径、组件定义路径、实例路径、显示文字及图片资源键。
        /// </summary>
        internal string SourcePath, DefinitionPath, ComponentInstancePath, Text, ResourceKey;
        /// <summary>
        /// 保存元素的模板类型编号。
        /// </summary>
        internal int Type;
        internal RotationAngle Rotation;
        internal bool IsLayoutOnly;
        /// <summary>
        /// 保存各层重复数据的项序号。
        /// </summary>
        internal IReadOnlyList<int> InstanceIndices;
        /// <summary>
        /// 保存将元素写入 CPCL 页面的绘制操作。
        /// </summary>
        internal Action<IPrintCanvas, int, int> Draw;
    }
    /// <summary>
    /// 保存行、单元格或组合的布局边界。
    /// </summary>
    private sealed class Frame
    {
        /// <summary>
        /// 保存布局边界相对当前块的起点与尺寸，单位为打印点。
        /// </summary>
        internal int X, Y, Width, Height;
        /// <summary>
        /// 保存此边界绘制时使用的线条样式。
        /// </summary>
        internal BpBorder Border;
        /// <summary>
        /// 保存对应模板路径、组件关联路径与布局节点类别。
        /// </summary>
        internal string SourcePath, DefinitionPath, ComponentInstancePath, Kind;
        /// <summary>
        /// 保存各层重复数据的项序号。
        /// </summary>
        internal IReadOnlyList<int> InstanceIndices;
    }
    /// <summary>
    /// 汇总同一行或元素组的布局内容。
    /// </summary>
    private sealed class Block
    {
        private readonly PrintStorageBudget storage;

        internal Block(PrintStorageBudget storage)
        {
            this.storage = storage;
            PrintStorageBudget.Reserve(48, "Template.Block", storage);
            Atoms = new BudgetedList<Atom>("Template.Atoms", itemBytes: 192, owner: storage,
                retainedSize: atom => checked(PrintStorageBudget.TextBytes(atom.SourcePath) +
                    PrintStorageBudget.TextBytes(atom.DefinitionPath) + PrintStorageBudget.TextBytes(atom.ComponentInstancePath) +
                    PrintStorageBudget.TextBytes(atom.Text) + PrintStorageBudget.TextBytes(atom.ResourceKey) +
                    (atom.InstanceIndices?.Count ?? 0) * 4L));
            Frames = new BudgetedList<Frame>("Template.Frames", itemBytes: 96, owner: storage,
                retainedSize: frame => checked(PrintStorageBudget.TextBytes(frame.SourcePath) +
                    PrintStorageBudget.TextBytes(frame.DefinitionPath) + PrintStorageBudget.TextBytes(frame.ComponentInstancePath) +
                    PrintStorageBudget.TextBytes(frame.Kind) + (frame.InstanceIndices?.Count ?? 0) * 4L));
            TableScopes = new BudgetedList<TableScope>("Template.TableScopes", itemBytes: 64, owner: storage);
        }
        /// <summary>
        /// 保存当前布局块占用的打印点高度。
        /// </summary>
        internal int Height;
        /// <summary>
        /// 记录流式元素的实际结束位置。
        /// </summary>
        internal int FlowHeight;
        /// <summary>
        /// 保存当前布局块中的可绘制元素。
        /// </summary>
        internal readonly BudgetedList<Atom> Atoms;
        /// <summary>
        /// 保存当前布局块中的行、单元格和组合边界。
        /// </summary>
        internal readonly BudgetedList<Frame> Frames;
        /// <summary>
        /// 保存组合内部表格明细与所属表头的分页作用域。
        /// </summary>
        internal readonly BudgetedList<TableScope> TableScopes;
        /// <summary>
        /// 将子布局块放入指定偏移位置。
        /// </summary>
        /// <param name="child">待追加的子布局块。</param>
        /// <param name="x">水平打印点偏移。</param>
        /// <param name="y">垂直打印点偏移。</param>
        internal void Append(Block child, int x, int y)
        {
            foreach (var atom in child.Atoms) { atom.X = checked(atom.X + x); atom.Y = checked(atom.Y + y); Atoms.Add(atom); }
            foreach (var frame in child.Frames) { frame.X = checked(frame.X + x); frame.Y = checked(frame.Y + y); Frames.Add(frame); }
            foreach (var scope in child.TableScopes) { scope.Start = checked(scope.Start + y); scope.End = checked(scope.End + y); scope.HeaderX = checked(scope.HeaderX + x); TableScopes.Add(scope); }
            Height = Math.Max(Height, checked(y + child.Height));
        }

        /// <summary>
        /// 复制当前布局块，保留表头续页所需的相对坐标。
        /// </summary>
        /// <returns>包含独立布局集合的副本。</returns>
        internal Block Copy()
        {
            var copy = new Block(storage) { Height = Height, FlowHeight = FlowHeight };
            foreach (var atom in Atoms) copy.Atoms.Add(new Atom { X = atom.X, Y = atom.Y, Width = atom.Width,
                Height = atom.Height, SourcePath = atom.SourcePath, DefinitionPath = atom.DefinitionPath,
                ComponentInstancePath = atom.ComponentInstancePath, Text = atom.Text, ResourceKey = atom.ResourceKey,
                Type = atom.Type, Rotation = atom.Rotation, IsLayoutOnly = atom.IsLayoutOnly,
                InstanceIndices = atom.InstanceIndices, Draw = atom.Draw });
            foreach (var frame in Frames) copy.Frames.Add(new Frame { X = frame.X, Y = frame.Y, Width = frame.Width,
                Height = frame.Height, Border = frame.Border, SourcePath = frame.SourcePath,
                DefinitionPath = frame.DefinitionPath, ComponentInstancePath = frame.ComponentInstancePath, Kind = frame.Kind,
                InstanceIndices = frame.InstanceIndices });
            return copy;
        }
    }

    /// <summary>
    /// 保存跨页表格明细与可重复表头之间的布局作用域。
    /// </summary>
    private sealed class TableScope
    {
        /// <summary>
        /// 保存明细作用域的起止位置、表头横向位置和宽度，单位为打印点。
        /// </summary>
        internal int Start, End, HeaderX, Width;
        /// <summary>
        /// 保存明细作用域关联的表头布局块。
        /// </summary>
        internal Block Header;
    }

    /// <summary>
    /// 保存单次渲染的资源、预算和分页状态。
    /// </summary>
    private sealed class Session : IDisposable
    {
        /// <summary>
        /// 保存本次渲染使用的模板。
        /// </summary>
        private readonly BpTemplate template;
        /// <summary>
        /// 保存本次渲染的资源限制。
        /// </summary>
        private readonly TemplateRenderOptions options;
        /// <summary>
        /// 保存本次渲染使用的图片资源提供器。
        /// </summary>
        private readonly ITemplateResourceProvider resources;
        /// <summary>
        /// 保存本次渲染的取消令牌。
        /// </summary>
        private readonly CancellationToken cancellation;
        /// <summary>
        /// 指示是否记录可供设计器定位的布局轨迹。
        /// </summary>
        private readonly bool captureLayout;
        /// <summary>
        /// 保存用于输出文字和计算字节预算的编码。
        /// </summary>
        private readonly Encoding encoding;
        /// <summary>
        /// 保存本次渲染采用的单页打印点高度。
        /// </summary>
        private readonly int pageHeight;
        /// <summary>
        /// 缓存本次渲染已读取的资源键与编码图片字节。
        /// </summary>
        private readonly BudgetedDictionary<string, byte[]> resourceBytes;
        /// <summary>
        /// 缓存按资源键及尺寸识别的已解码图片，由会话释放。
        /// </summary>
        private readonly BudgetedDictionary<(string Key, int Width, int Height, int CropX, int CropY, int CropWidth, int CropHeight), Image> images;
        /// <summary>
        /// 记录编码图片缓存对应的资源租约；缓存清理时立即归还。
        /// </summary>
        private readonly BudgetedDictionary<string, IDisposable> encodedLeases;
        /// <summary>
        /// 记录解码图片对应的 RGBA 资源租约；图片清理时立即归还。
        /// </summary>
        private readonly BudgetedDictionary<(string Key, int Width, int Height, int CropX, int CropY, int CropWidth, int CropHeight), IDisposable> imageLeases;
        /// <summary>
        /// 记录本次渲染的字段字节数和当前图片像素数。
        /// </summary>
        private long payloadBytes, imagePixels;
        /// <summary>
        /// 记录当前已缓存图片的编码字节数。
        /// </summary>
        private long cachedEncodedBytes;
        /// <summary>
        /// 记录已遍历的明细行数，包含被条件过滤的行。
        /// </summary>
        private int rows;
        /// <summary>
        /// 保存正在生成的页面布局轨迹；未请求轨迹时为 null。
        /// </summary>
        private BudgetedList<TemplateLayoutElement> pageLayout;
        /// <summary>
        /// 保存供协议校验错误回溯的页面布局归属；不改变普通渲染的公开结果。
        /// </summary>
        private readonly PrintStorageBudget storage = new PrintStorageBudget();
        private readonly BudgetedList<IReadOnlyList<TemplateLayoutElement>> ownershipLayouts;
        /// <summary>
        /// 记录当前页面在输出中的零基序号。
        /// </summary>
        private int pageIndex;

        /// <summary>
        /// 初始化一个 <see cref="Session"/> 类型的实例。
        /// </summary>
        /// <param name="template">本次渲染使用的模板。</param>
        /// <param name="options">本次渲染的资源限制及编码配置。</param>
        /// <param name="resources">可选的图片资源提供器。</param>
        /// <param name="cancellation">取消渲染的令牌。</param>
        /// <param name="captureLayout">是否记录布局轨迹。</param>
        internal Session(BpTemplate template, TemplateRenderOptions options, ITemplateResourceProvider resources, CancellationToken cancellation, bool captureLayout)
        {
            resourceBytes = new BudgetedDictionary<string, byte[]>("Template.ResourceIndex", storage,
                StringComparer.Ordinal, (key, _) => PrintStorageBudget.TextBytes(key));
            images = new BudgetedDictionary<(string Key, int Width, int Height, int CropX, int CropY, int CropWidth, int CropHeight), Image>("Template.ImageIndex", storage,
                retainedSize: (key, _) => PrintStorageBudget.TextBytes(key.Item1));
            encodedLeases = new BudgetedDictionary<string, IDisposable>("Template.EncodedLeaseIndex", storage,
                StringComparer.Ordinal, (key, _) => PrintStorageBudget.TextBytes(key));
            imageLeases = new BudgetedDictionary<(string Key, int Width, int Height, int CropX, int CropY, int CropWidth, int CropHeight), IDisposable>("Template.ImageLeaseIndex", storage,
                retainedSize: (key, _) => PrintStorageBudget.TextBytes(key.Item1));
            ownershipLayouts = new BudgetedList<IReadOnlyList<TemplateLayoutElement>>("Template.OwnershipPages", owner: storage);
            this.template = template; this.options = options; this.resources = resources; this.cancellation = cancellation;
            this.captureLayout = captureLayout;
            encoding = ResolveEncoding(options);
            pageHeight = template.Height == 0 ? options.MaxPageHeight : template.Height;
        }

        /// <summary>创建与布局会话一起释放的根绑定上下文。</summary>
        internal Binding BindRoot(object data) => new Binding(data, template.Variables, storage);

        /// <summary>
        /// 展开模板行并生成分页结果。
        /// </summary>
        /// <param name="binding">根数据及变量绑定上下文。</param>
        /// <returns>包含所有打印页的渲染结果。</returns>
        internal TemplateRenderResult Render(Binding binding)
        {
            var header = new Block(storage);
            var documentHeader = new Block(storage);
            var footer = new Block(storage);
            var body = new BudgetedList<(BpRow Row, string Path)>("Template.BodyRows", slotBytes: 16, owner: storage,
                retainedSize: item => PrintStorageBudget.TextBytes(item.Path));
            var documentFooter = new BudgetedList<(BpRow Row, string Path)>("Template.FooterRows", slotBytes: 16, owner: storage,
                retainedSize: item => PrintStorageBudget.TextBytes(item.Path));
            var templateRows = template.Rows ?? new List<BpRow>();
            for (var rowIndex = 0; rowIndex < templateRows.Count; rowIndex++)
            {
                var row = templateRows[rowIndex];
                var path = ChildPath(null, "Rows", rowIndex);
                if (row.IsHeader || row.Section == "PageHeader")
                {
                    foreach (var block in Expand(row, template.Width, binding, path)) header.Append(block, 0, header.Height);
                }
                else if (row.Section == "DocumentHeader")
                {
                    foreach (var block in Expand(row, template.Width, binding, path)) documentHeader.Append(block, 0, documentHeader.Height);
                }
                else if (row.Section == "PageFooter")
                {
                    foreach (var block in Expand(row, template.Width, binding, path)) footer.Append(block, 0, footer.Height);
                }
                else if (row.Section == "DocumentFooter") documentFooter.Add((row, path));
                else body.Add((row, path));
            }
            body.AddRange(documentFooter);
            if (header.Height + footer.Height >= pageHeight || header.Height + documentHeader.Height + footer.Height > pageHeight)
                throw new ArgumentException("页眉、首页内容与页脚必须为正文保留空间。");
            var headerImages = new BudgetedDictionary<(string Key, int Width, int Height, int CropX, int CropY, int CropWidth, int CropHeight), bool>("Template.HeaderImageIndex", storage);
            foreach (var key in images.Keys) headerImages.TryAdd(key, true);
            var pages = new BudgetedList<TemplateDraft>("Template.DraftPages", itemBytes: 40, owner: storage);
            var document = new PrintDocument(storage);
            // 即使调用方没有请求布局轨迹，也保留内部归属，以便 Provider 错误返回组件定义和实例信息。
            pageLayout = NewLayout();
            var command = NewPage(true);
            var used = checked(header.Height + documentHeader.Height);
            long outputBytes = 0;
            var dirty = false;
            Block activeTableHeader = null;
            Block pendingTableHeader = null;
            BudgetedList<TableScope> activeScopedHeaders = null;
            foreach (var item in body)
            {
                if (item.Row.Section == "TableHeader")
                {
                    pendingTableHeader = new Block(storage);
                    foreach (var block in Expand(item.Row, template.Width, binding, item.Path))
                        pendingTableHeader.Append(block, 0, pendingTableHeader.Height);
                    foreach (var key in images.Keys) headerImages.TryAdd(key, true);
                    continue;
                }
                foreach (var block in Expand(item.Row, template.Width, binding, item.Path))
                {
                    if (pendingTableHeader != null)
                    {
                        if (header.Height + footer.Height + pendingTableHeader.Height >= pageHeight ||
                            pages.Count == 0 && header.Height + documentHeader.Height + footer.Height + pendingTableHeader.Height >= pageHeight)
                            throw LayoutError(item.Path, "表格表头没有为明细保留空间。");
                        var firstAtomEnd = block.Atoms.Count == 0
                            ? block.Height
                            : block.Atoms.Min(atom => checked(atom.Y + atom.Height));
                        var firstSliceHeight = Math.Min(block.Height, Math.Max(1, firstAtomEnd));
                        bool extendsFirstSlice;
                        do
                        {
                            extendsFirstSlice = false;
                            foreach (var atom in block.Atoms)
                            {
                                var atomEnd = checked(atom.Y + atom.Height);
                                if (atom.Y < firstSliceHeight && atomEnd > firstSliceHeight)
                                { firstSliceHeight = atomEnd; extendsFirstSlice = true; }
                            }
                        } while (extendsFirstSlice);
                        if (used + pendingTableHeader.Height + firstSliceHeight > pageHeight - footer.Height)
                            Finish();
                        if (used + pendingTableHeader.Height + firstSliceHeight > pageHeight - footer.Height)
                            throw LayoutError(item.Path, "表格表头与首条明细无法放在同一页。");
                        DrawSlice(command, pendingTableHeader, 0, pendingTableHeader.Height, used);
                        used = checked(used + pendingTableHeader.Height);
                        dirty = true;
                        activeTableHeader = pendingTableHeader;
                        pendingTableHeader = null;
                    }
                    var offset = 0;
                    while (offset < block.Height)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var end = Math.Min(block.Height, checked(offset + pageHeight - footer.Height - used));
                        // 所有横向单元格共用切分线；文字按行切，图片和条码不可切。
                        bool changed;
                        do
                        {
                            changed = false;
                            foreach (var atom in block.Atoms)
                                if (atom.Y < end && checked(atom.Y + atom.Height) > end && atom.Y >= offset)
                                { end = atom.Y; changed = true; }
                        } while (changed);
                        if (end <= offset)
                        {
                            if (used == header.Height + (activeTableHeader?.Height ?? 0) + ScopedHeaderHeight() + (pages.Count == 0 ? documentHeader.Height : 0))
                                throw LayoutError(block.Atoms.FirstOrDefault(atom => atom.Y <= offset && atom.Y + atom.Height > offset)?.SourcePath
                                    ?? item.Path, "单个元素或重叠元素组超过可打印页高。");
                            activeScopedHeaders = SelectScopedHeaders(block, offset);
                            Finish();
                            continue;
                        }
                        DrawSlice(command, block, offset, end, used);
                        used = checked(used + end - offset);
                        dirty = true;
                        offset = end;
                        if (offset < block.Height)
                        {
                            activeScopedHeaders = SelectScopedHeaders(block, offset);
                            Finish();
                        }
                    }
                    // DrawImage 已转换为自有打印数据，正文图片无需在后续明细间保留。
                    using (var pruningStorage = new PrintStorageBudget())
                    {
                        var removed = new BudgetedList<(string Key, int Width, int Height, int CropX, int CropY, int CropWidth, int CropHeight)>("Template.PrunedImageKeys", slotBytes: 16,
                            owner: pruningStorage);
                        foreach (var key in images.Keys)
                            if (!headerImages.ContainsKey(key)) removed.Add(key);
                        foreach (var key in removed) RemoveImage(key);
                    }
                    ClearEncodedResources();
                    imagePixels = images.Values.Sum(image => (long)image.Width * image.Height);
                }
                activeTableHeader = null;
                pendingTableHeader = null;
                activeScopedHeaders = null;
            }
            if (dirty || pages.Count == 0) Finish(false);
            {
                var provider = PrintProtocolProviderFactory.Create(options.Protocol, encoding, options.EscPosLayout,
                    options.EscPosDevice, options.TsplMedia, options.ZplMedia, options.RequireDeclaredNativeBounds);
                var providerPageOffset = 0;
                try
                {
                    provider.Validate(document, cancellation);
                    if (options.Job != null)
                    {
                        var job = new PrintJobRenderer().Render(provider, document, options.Job, cancellation,
                            options.MaxPages, options.MaxOutputBytes - outputBytes);
                        var jobPages = new BudgetedList<TemplatePage>("Template.JobPages", owner: storage);
                        foreach (var output in job.Pages)
                        {
                            var source = document.Pages[output.SourcePageIndex];
                            outputBytes = checked(outputBytes + output.ByteLength);
                            jobPages.Add(new TemplatePage(source.Width, source.Height, output.Output,
                                captureLayout ? pages[output.SourcePageIndex].Elements : null, options.Protocol,
                                output.SourcePageIndex, output.CopyIndex, output.FeedAfterPrint, output.CutAfterPrint));
                        }
                        return new TemplateRenderResult(jobPages, options.Protocol);
                    }
                    var rendered = new BudgetedList<TemplatePage>("Template.RenderedPages", owner: storage, capacity: pages.Count);
                    for (var index = 0; index < document.Pages.Count; index++)
                    {
                        providerPageOffset = index;
                        cancellation.ThrowIfCancellationRequested();
                        var logicalPage = document.Pages[index];
                        var singlePage = new PrintDocument(storage);
                        var target = singlePage.AddPage(logicalPage.Width, logicalPage.Height);
                        target.Copies = logicalPage.Copies;
                        target.FeedLines = logicalPage.FeedLines;
                        target.CutAfterPrint = logicalPage.CutAfterPrint;
                        target.CutOptions = logicalPage.CutOptions?.Snapshot();
                        foreach (var element in logicalPage.Elements) target.Add(element, element.SourcePath);
                        var output = provider.RenderOutput(singlePage, cancellation)[0];
                        outputBytes = checked(outputBytes + output.ByteLength);
                        if (outputBytes > options.MaxOutputBytes) throw new ArgumentException("输出超过字节上限。");
                        rendered.Add(new TemplatePage(logicalPage.Width, logicalPage.Height, output,
                            captureLayout ? pages[index].Elements : null, options.Protocol));
                    }
                    return new TemplateRenderResult(rendered, options.Protocol);
                }
                catch (PrintCapabilityException error)
                {
                    throw ProviderError(error.ElementPath, error.Message, error, providerPageOffset);
                }
                catch (PrintElementValidationException error)
                {
                    throw ProviderError(error.ElementPath, error.Message, error, providerPageOffset);
                }
            }

            TemplateRenderException ProviderError(string path, string message, Exception error, int pageOffset)
            {
                var providerPage = error is PrintCapabilityException capability ? capability.PageIndex
                    : (error as PrintElementValidationException)?.PageIndex;
                var providerElement = error is PrintCapabilityException capabilityElement ? capabilityElement.ElementIndex
                    : (error as PrintElementValidationException)?.ElementIndex;
                var ownership = FindOwnership(path, providerPage.HasValue ? providerPage + pageOffset : null, providerElement);
                return new TemplateRenderException(path, message, error,
                    ownership?.DefinitionPath, ownership?.ComponentInstancePath, ownership?.InstanceIndices)
                    .WithLocation(providerPage.HasValue ? checked(providerPage.Value + pageOffset) : (int?)null,
                        providerElement);
            }

            TemplateLayoutElement FindOwnership(string path, int? owningPage, int? owningElement)
            {
                if (owningPage.HasValue)
                {
                    if (!owningElement.HasValue || owningPage < 0 || owningPage >= ownershipLayouts.Count) return null;
                    var elements = ownershipLayouts[owningPage.Value];
                    return owningElement >= 0 && owningElement < elements.Count ? elements[owningElement.Value] : null;
                }
                if (path == null) return null;
                foreach (var page in ownershipLayouts)
                {
                    var element = page.FirstOrDefault(item => item != null && item.Kind == "element" &&
                        string.Equals(item.SourcePath, path, StringComparison.Ordinal));
                    if (element != null) return element;
                }
                return null;
            }

            ArgumentException LayoutError(string path, string message)
                => captureLayout || options.Protocol != PrintProtocol.Cpcl
                    ? new TemplateRenderException(path, message, new ArgumentException(message)) : new ArgumentException(message);

            int ScopedHeaderHeight() => ScopedHeaderBands().Sum(band => band.Max(scope => scope.Header.Height));

            BudgetedList<TableScope> SelectScopedHeaders(Block block, int offset)
            {
                var scopes = new BudgetedList<TableScope>("Template.ActiveHeaders", owner: storage);
                scopes.AddRange(block.TableScopes.Where(scope => scope.Start <= offset && offset < scope.End));
                return scopes;
            }

            IEnumerable<BudgetedList<TableScope>> ScopedHeaderBands()
            {
                var bands = new BudgetedList<BudgetedList<TableScope>>("Template.HeaderBands", owner: storage);
                if (activeScopedHeaders == null) return bands;
                foreach (var scope in activeScopedHeaders.OrderBy(item => item.Start).ThenBy(item => item.HeaderX))
                {
                    var band = bands.FirstOrDefault(items => items.All(item =>
                        scope.HeaderX >= checked(item.HeaderX + item.Width) ||
                        item.HeaderX >= checked(scope.HeaderX + scope.Width)));
                    if (band == null) { band = new BudgetedList<TableScope>("Template.HeaderBand", owner: storage); bands.Add(band); }
                    band.Add(scope);
                }
                return bands;
            }

            IPrintCanvas NewPage(bool first)
            {
                if (options.Job != null)
                {
                    var logicalPageCount = checked(pages.Count + 1);
                    if ((long)logicalPageCount * options.Job.Copies > options.MaxPages)
                        throw new TemplateRenderException("Job", "任务实际输出页数超过上限。",
                            new ArgumentException("任务实际输出页数超过上限。"));
                    try
                    {
                        var provider = PrintProtocolProviderFactory.Create(options.Protocol, encoding, options.EscPosLayout,
                            options.EscPosDevice, options.TsplMedia, options.ZplMedia, options.RequireDeclaredNativeBounds);
                        PrintJobRenderer.ValidateSettings(provider, options.Job, logicalPageCount);
                    }
                    catch (PrintCapabilityException error)
                    { throw new TemplateRenderException(error.ElementPath, error.Message, error); }
                    catch (PrintElementValidationException error)
                    { throw new TemplateRenderException(error.ElementPath, error.Message, error); }
                }
                var result = PrintCanvasFactory.Create(options.Protocol, encoding, template.Width, pageHeight, options, cancellation, storage);
                DrawSlice(result, header, 0, header.Height, 0);
                if (first) DrawSlice(result, documentHeader, 0, documentHeader.Height, header.Height);
                return result;
            }
            void Finish(bool startNext = true)
            {
                cancellation.ThrowIfCancellationRequested();
                if (pages.Count >= options.MaxPages) throw new ArgumentException("输出超过页数上限。");
                var height = template.AutoHeight ? Math.Max(1, checked(used + footer.Height)) : pageHeight;
                if (footer.Height > 0) DrawSlice(command, footer, 0, footer.Height, height - footer.Height);
                command.InitCommand(0, template.Width, height, options.Copies);
                try
                {
                    command.SetSourcePath("Template");
                    DrawBorder(command, new Frame { X = 0, Y = 0, Width = template.Width, Height = height, Border = template.Border }, 0, height, 0);
                }
                catch (NotSupportedException error)
                {
                    throw new TemplateRenderException("Template", $"协议 {options.Protocol} 不支持模板边框：{error.Message}", error);
                }
                ((DocumentPrintCanvas)command).AppendPage(document);
                ownershipLayouts.Add(((DocumentPrintCanvas)command).ElementOwnership);
                pages.Add(new TemplateDraft(captureLayout ? pageLayout.AsReadOnly() : null));
                pageIndex++;
                dirty = false;
                if (startNext)
                {
                    pageLayout = NewLayout();
                    command = NewPage(false);
                    used = header.Height;
                    if (activeTableHeader != null)
                    {
                        DrawSlice(command, activeTableHeader, 0, activeTableHeader.Height, used);
                        used = checked(used + activeTableHeader.Height);
                        dirty = true;
                    }
                    if (activeScopedHeaders != null)
                    {
                        foreach (var band in ScopedHeaderBands())
                        {
                            foreach (var scope in band)
                                DrawSlice(command, scope.Header, 0, scope.Header.Height, used, scope.HeaderX);
                            used = checked(used + band.Max(scope => scope.Header.Height));
                            dirty = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 按重复数据源与显示条件展开模板行。
        /// </summary>
        /// <param name="row">待展开的模板行。</param>
        /// <param name="width">可用打印点宽度。</param>
        /// <param name="binding">当前数据绑定上下文。</param>
        /// <param name="sourcePath">模板行的节点路径。</param>
        /// <returns>符合条件的行布局块序列。</returns>
        private IEnumerable<Block> Expand(BpRow row, int width, Binding binding, string sourcePath)
        {
            cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(row.RepeatSource))
            {
                bool matches;
                try { matches = binding.Matches(row.Condition); }
                catch (ArgumentException error)
                {
                    if (captureLayout || options.Protocol != PrintProtocol.Cpcl) throw new TemplateRenderException(sourcePath, error.Message, error);
                    throw;
                }
                if (matches) { CountRow(); yield return Layout(row, width, binding, sourcePath); }
                yield break;
            }
            object source;
            try { source = binding.Resolve(row.RepeatSource); }
            catch (ArgumentException error)
            {
                if (captureLayout || options.Protocol != PrintProtocol.Cpcl) throw new TemplateRenderException(sourcePath, error.Message, error);
                throw;
            }
            if (source == null) yield break;
            if (!(source is IEnumerable sequence) || source is string || source is IDictionary)
            {
                var error = new ArgumentException("RepeatSource 必须为集合。");
                TemplateBindingDiagnostic.Attach(error, row.RepeatSource);
                if (captureLayout || options.Protocol != PrintProtocol.Cpcl)
                    throw new TemplateRenderException(sourcePath, error.Message, error);
                throw error;
            }
            var index = 0;
            foreach (var item in sequence)
            {
                cancellation.ThrowIfCancellationRequested();
                CountRow(); // 即使条件为 false 也计数，限制无限枚举。
                var child = binding.Child(item, index++);
                bool matches;
                try { matches = child.Matches(row.Condition); }
                catch (ArgumentException error)
                {
                    if (captureLayout || options.Protocol != PrintProtocol.Cpcl) throw new TemplateRenderException(sourcePath, error.Message, error);
                    throw;
                }
                if (matches) yield return Layout(row, width, child, sourcePath);
            }
        }
        /// <summary>
        /// 计入一条明细并检查行数限制。
        /// </summary>
        private void CountRow()
        {
            if (++rows > options.MaxDetailRows) throw new ArgumentException("明细行数超过上限。");
        }

        /// <summary>
        /// 计算模板行及其单元格的布局。
        /// </summary>
        /// <param name="row">待布局的模板行。</param>
        /// <param name="width">父级可用打印点宽度。</param>
        /// <param name="binding">当前数据绑定上下文。</param>
        /// <param name="sourcePath">模板行的节点路径。</param>
        /// <returns>包含行元素及边界的布局块。</returns>
        private Block Layout(BpRow row, int width, Binding binding, string sourcePath)
        {
            var result = new Block(storage) { Height = row.Height };
            var cells = row.Cells ?? new List<BpCell>();
            var explicitWidth = cells.Aggregate(0L, (sum, cell) => sum + cell.Width);
            if (explicitWidth > width) throw new ArgumentException("单元格总宽超过父容器。");
            var automatic = cells.Count(cell => cell.Width == 0);
            var remaining = width - (int)explicitWidth;
            var x = 0;
            for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                var cell = cells[cellIndex];
                var cellPath = ChildPath(sourcePath, "Cells", cellIndex);
                cancellation.ThrowIfCancellationRequested();
                var cellWidth = cell.Width;
                if (cellWidth == 0) { cellWidth = remaining / automatic; remaining -= cellWidth; automatic--; }
                if (cellWidth <= 0) throw new ArgumentException("单元格必须具有正宽度。");
                var content = new Block(storage);
                var childRows = cell.Rows ?? new List<BpRow>();
                if (template.Version == 3) AppendRows(content, childRows, cellWidth, binding, cellPath);
                else for (var childIndex = 0; childIndex < childRows.Count; childIndex++)
                    foreach (var block in Expand(childRows[childIndex], cellWidth, binding, ChildPath(cellPath, "Rows", childIndex))) content.Append(block, 0, content.Height);
                content.FlowHeight = content.Height;
                var elements = cell.Elements ?? new List<BpElement>();
                for (var elementIndex = 0; elementIndex < elements.Count; elementIndex++)
                {
                    var element = elements[elementIndex];
                    var elementPath = ChildPath(cellPath, "Elements", elementIndex);
                    cancellation.ThrowIfCancellationRequested();
                    try
                    {
                        Place(content, element, cellWidth, binding, elementPath);
                    }
                    catch (TemplateRenderException) { throw; }
                    catch (ArgumentException error)
                    {
                        if (captureLayout || options.Protocol != PrintProtocol.Cpcl) throw new TemplateRenderException(elementPath, error.Message, error);
                        throw;
                    }
                }
                result.Append(content, x, 0);
                result.Frames.Add(new Frame { X = x, Width = cellWidth, Border = cell.Border, SourcePath = cellPath, Kind = "cell", InstanceIndices = binding.InstanceIndices });
                x = checked(x + cellWidth);
            }
            foreach (var frame in result.Frames.Where(f => f.Height == 0)) frame.Height = result.Height;
            result.Frames.Add(new Frame { Width = width, Height = result.Height, Border = row.Border, SourcePath = sourcePath, Kind = "row", InstanceIndices = binding.InstanceIndices });
            return result;
        }

        /// <summary>
        /// 展开嵌套行并保留表格表头的分页作用域。
        /// </summary>
        /// <param name="content">接收展开内容的布局块。</param>
        /// <param name="childRows">待展开的嵌套行。</param>
        /// <param name="width">嵌套行可用的打印点宽度。</param>
        /// <param name="binding">当前数据绑定上下文。</param>
        /// <param name="parentPath">嵌套行父节点的模板路径。</param>
        private void AppendRows(Block content, List<BpRow> childRows, int width, Binding binding, string parentPath)
        {
            for (var index = 0; index < childRows.Count; index++)
            {
                var row = childRows[index];
                var path = ChildPath(parentPath, "Rows", index);
                if (row.Section == "TableHeader")
                {
                    var header = new Block(storage);
                    foreach (var part in Expand(row, width, binding, path)) header.Append(part, 0, header.Height);
                    var detail = childRows[++index];
                    var detailPath = ChildPath(parentPath, "Rows", index);
                    var first = true;
                    foreach (var part in Expand(detail, width, binding, detailPath))
                    {
                        var headerStart = content.Height;
                        if (first) { content.Append(header.Copy(), 0, content.Height); first = false; }
                        var start = content.Height;
                        content.Append(part, 0, start);
                        content.TableScopes.Add(new TableScope { Start = start, End = content.Height, Header = header, Width = width });
                        if (headerStart < start && part.Height > 0)
                        {
                            var firstEnd = part.Atoms.Count == 0 ? part.Height : part.Atoms.Min(atom => atom.Y + atom.Height) - start;
                            bool extended;
                            do
                            {
                                extended = false;
                                foreach (var atom in part.Atoms)
                                    if (atom.Y < start + firstEnd && atom.Y + atom.Height > start + firstEnd)
                                    { firstEnd = atom.Y + atom.Height - start; extended = true; }
                            } while (extended);
                            content.Atoms.Add(new Atom { Y = headerStart, Height = checked(start + firstEnd - headerStart),
                                Width = width, SourcePath = path, InstanceIndices = binding.InstanceIndices,
                                Draw = (command, x, y) => { } });
                        }
                    }
                }
                else foreach (var part in Expand(row, width, binding, path)) content.Append(part, 0, content.Height);
            }
            content.FlowHeight = content.Height;
        }

        /// <summary>
        /// 将条件可见的元素放入单元格布局块。
        /// </summary>
        /// <param name="content">接收元素的单元格布局块。</param>
        /// <param name="element">待放置的模板元素。</param>
        /// <param name="width">单元格可用打印点宽度。</param>
        /// <param name="binding">当前数据绑定上下文。</param>
        /// <param name="sourcePath">模板元素的节点路径。</param>
        private void Place(Block content, BpElement element, int width, Binding binding, string sourcePath)
        {
            if (!binding.Matches(element.Condition)) return;
            var margin = element.Margin ?? new BpMargin();
            var start = element.StartCoordinate;
            if (element is BpLineElement line)
            {
                content.Append(Line(line, width, margin, sourcePath, binding), 0, 0);
                return;
            }
            var ex = checked((start?.X ?? 0) + margin.Left);
            var ey = checked((start?.Y ?? (template.Version == 3 ? content.FlowHeight : content.Height)) + margin.Top);
            var available = checked(width - ex - margin.Right);
            if (element.EndCoordinate != null)
            {
                if (element.EndCoordinate.X <= (start?.X ?? 0) || element.EndCoordinate.Y <= (start?.Y ?? 0))
                    throw new ArgumentException("元素结束坐标必须大于起始坐标。");
                available = Math.Min(available, checked(element.EndCoordinate.X - ex - margin.Right));
            }
            if (available <= 0) throw new ArgumentException("元素没有可用宽度。");
            var block = Element(element, available, binding, sourcePath);
            if (element.EndCoordinate != null && checked(ey + block.Height + margin.Bottom) > element.EndCoordinate.Y)
                throw new ArgumentException($"元素 {element.Type} 在 y={ey} 的内容高度 {block.Height} 超过结束坐标 y={element.EndCoordinate.Y}。");
            content.Append(block, ex, ey);
            if (template.Version == 3)
            {
                if (start == null) content.FlowHeight = checked(ey + block.Height + margin.Bottom);
                content.Height = Math.Max(content.Height, content.FlowHeight);
            }
            else content.Height = checked(content.Height + margin.Bottom);
        }

        /// <summary>
        /// 将模板元素转换为可绘制的布局块。
        /// </summary>
        /// <param name="element">待转换的模板元素。</param>
        /// <param name="width">元素可用的打印点宽度。</param>
        /// <param name="binding">当前数据绑定上下文。</param>
        /// <param name="sourcePath">模板元素的节点路径。</param>
        /// <returns>包含实际尺寸与绘制操作的布局块。</returns>
        private Block Element(BpElement element, int width, Binding binding, string sourcePath)
        {
            if (element is BpGroupElement group) return Group(group, width, binding, sourcePath);
            if (element is BpRectangleElement rectangle) return Shape(rectangle.Width, rectangle.Height, rectangle.LineWidth, false, width, sourcePath, binding, rectangle.Filled);
            if (element is BpEllipseElement ellipse) return Shape(ellipse.Width, ellipse.Height, ellipse.LineWidth, true, width, sourcePath, binding);
            if (element is BpIconElement icon) return Icon(icon, width, sourcePath, binding);
            var text = binding.Text(element.Field);
            payloadBytes = checked(payloadBytes + encoding.GetByteCount(text));
            if (payloadBytes > options.MaxOutputBytes) throw new ArgumentException("字段数据超过输出预算。");
            if (element is BpFontElement font) return Text(font, text, width, sourcePath, binding);
            if (text.IndexOfAny(new[] { '\r', '\n', '\0', '\u001b' }) >= 0) throw new ArgumentException("图片键或条码内容不能包含协议控制字符。");
            if (element is BpMatrixCodeElement matrix) return MatrixCode(matrix, text, width, sourcePath, binding);
            var result = new Block(storage);
            if (element is BpImageElement picture)
            {
                Image image;
                try { image = LoadImage(text, picture.Width, picture.Height, picture.Crop); }
                catch (Exception error) when (error is ImageFormatException || error is ArgumentException)
                {
                    TemplateImageDiagnostic.Attach(error, text, picture.Crop, null, null, null, null);
                    if (captureLayout || options.Protocol != PrintProtocol.Cpcl)
                        throw new TemplateRenderException(sourcePath, error.Message, error);
                    throw;
                }
                var quarterTurn = picture.Rotation == RotationAngle.Rotate90 || picture.Rotation == RotationAngle.Rotate270;
                var occupiedWidth = quarterTurn ? image.Height : image.Width;
                var occupiedHeight = quarterTurn ? image.Width : image.Height;
                if (occupiedWidth > width) throw new ArgumentException("图片旋转后的宽度超出单元格。");
                Reserve(checked(((image.Width + 7L) / 8) * image.Height * (picture.IsCompress ? 1 : 2)));
                var imageOptions = new ImageProcessingOptions { MaxEncodedBytes = options.MaxImageBytes, MaxPixels = options.MaxImagePixels };
                result.Height = occupiedHeight;
                result.Atoms.Add(new Atom { Width = occupiedWidth, Height = occupiedHeight, Rotation = picture.Rotation,
                    SourcePath = sourcePath, Type = element.Type, ResourceKey = text, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) => {
                    if (picture.IsCompress) cmd.DrawCompressedImage(x, y, image, imageOptions, picture.Rotation);
                    else cmd.DrawImage(x, y, image, imageOptions, picture.Rotation);
                } });
            }
            else if (element is BpQrCodeElement qr)
            {
                if (qr.UnitWidth < 1 || qr.UnitWidth > 32 || !new[] { "L", "M", "Q", "H" }.Contains(qr.ErrorLevel)) throw new ArgumentException("二维码参数无效。");
                var size = options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Raster
                    ? QrMetrics.RasterExtent(text, qr.ErrorLevel, qr.UnitWidth, cancellation)
                    : QrMetrics.Extent(text, encoding, qr.ErrorLevel, qr.UnitWidth);
                var quietZone = qr.QuietZone;
                if (quietZone != null && (options.Protocol == PrintProtocol.Tspl || options.Protocol == PrintProtocol.Zpl))
                    size = Math.Max(size, QrMetrics.RasterExtent(text, qr.ErrorLevel, qr.UnitWidth, cancellation));
                var bodySize = checked(size - 8 * qr.UnitWidth);
                var requiredWidth = quietZone == null ? size : checked(bodySize + quietZone.Left + quietZone.Right);
                var requiredHeight = quietZone == null ? size : checked(bodySize + quietZone.Top + quietZone.Bottom);
                var w = qr.Width == 0 ? requiredWidth : qr.Width;
                var h = qr.Height == 0 ? requiredHeight : qr.Height;
                if (w < requiredWidth || h < requiredHeight || w > width) throw new ArgumentException("二维码预留区域不足或超过单元格。");
                result.Height = h;
                // Native 码图不接受绝对坐标；模板预留静区仍参与布局，但不转换为设备定位指令。
                var inset = options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Native
                    ? 0 : 4 * qr.UnitWidth;
                result.Atoms.Add(new Atom { Width = w, Height = h, Rotation = qr.Rotation, SourcePath = sourcePath, Type = element.Type, Text = text,
                    InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) => {
                        if (quietZone == null)
                            cmd.DrawQrCode(x + inset, y + inset, text, qr.UnitWidth, qr.ErrorLevel, (int)qr.Rotation, w - inset, h - inset);
                        else
                            cmd.DrawQrCode(checked(x + quietZone.Left), checked(y + quietZone.Top), text,
                                qr.UnitWidth, qr.ErrorLevel, (int)qr.Rotation, w - quietZone.Left - quietZone.Right,
                                h - quietZone.Top - quietZone.Bottom, quietZone);
                    } });
            }
            else if (element is BpBarcodeElement barcode)
            {
                if (options.RequireDeclaredNativeBounds && barcode.Width <= 0)
                    throw new TemplateRenderException(sourcePath,
                        $"协议 {options.Protocol} 的严格设备边界校验要求声明条码 Width 预留宽度。",
                        new ArgumentException("请按目标设备实测条码宽度设置 Width。"));
                if (options.RequireDeclaredNativeBounds && barcode.HumanReadable != false &&
                    barcode.HumanReadableReserveHeight == 0)
                    throw new TemplateRenderException(sourcePath,
                        $"协议 {options.Protocol} 的严格设备边界校验要求声明 HumanReadableReserveHeight 可读文字预留高度。",
                        new ArgumentException("请按目标设备实测字高设置预留高度，或关闭条码可读文字。"));
                if (barcode.HumanReadableReserveHeight < 0)
                    throw new ArgumentOutOfRangeException(nameof(barcode.HumanReadableReserveHeight));
                if (barcode.NarrowModuleWidth is int narrowModuleWidth && narrowModuleWidth <= 0 ||
                    barcode.WideModuleWidth is int wideModuleWidth && wideModuleWidth <= 0)
                    throw new ArgumentOutOfRangeException(nameof(barcode.NarrowModuleWidth), "条码模块宽度必须为正数。");
                if (options.Protocol == PrintProtocol.Cpcl &&
                    (barcode.NarrowModuleWidth.HasValue || barcode.WideModuleWidth.HasValue))
                    throw new TemplateRenderException(sourcePath,
                        "协议 Cpcl 不支持按打印点指定条码模块宽度。",
                        new NotSupportedException("CPCL 模板请使用 LineWidth 和 Ratio 设置条码宽度。"));
                if (options.Protocol != PrintProtocol.Tspl && barcode.WideModuleWidth.HasValue)
                    throw new TemplateRenderException(sourcePath,
                        $"协议 {options.Protocol} 不支持显式宽条模块宽度。",
                        new NotSupportedException("此协议没有独立的宽条宽度命令参数。"));
                var narrow = options.Protocol == PrintProtocol.Cpcl ? (int?)null : barcode.NarrowModuleWidth;
                var wide = options.Protocol == PrintProtocol.Tspl ? barcode.WideModuleWidth : null;
                var layout = BarcodeMetrics.Measure(barcode, text, narrow, wide);
                var quietZone = barcode.QuietZone;
                var requiredWidth = options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Raster
                        ? checked((quietZone == null ? layout.Left * 2 : quietZone.Left + quietZone.Right) + EscPosRasterCanvas.MeasureBarcodeWidth(
                            barcode.BarcodeType, layout.Data, narrow ?? checked(barcode.LineWidth + 1)))
                    : layout.Width;
                var w = barcode.Width == 0 ? requiredWidth : barcode.Width;
                var h = checked(barcode.Height + barcode.HumanReadableReserveHeight +
                    (quietZone == null ? 0 : checked(quietZone.Top + quietZone.Bottom)));
                var quarterTurn = barcode.Rotation == RotationAngle.Rotate90 || barcode.Rotation == RotationAngle.Rotate270;
                if (w < requiredWidth || (quarterTurn ? h : w) > width)
                    throw new ArgumentException("旋转后的条码预留区域不足或超过单元格。");
                result.Height = quarterTurn ? w : h;
                result.Atoms.Add(new Atom { Width = quarterTurn ? h : w, Height = result.Height,
                    Rotation = barcode.Rotation, SourcePath = sourcePath, Type = element.Type, Text = text,
                    InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) => {
                        if (quietZone != null)
                        {
                            var rotatedZone = quietZone.Rotate(barcode.Rotation);
                            cmd.DrawBarcode1D(barcode.BarcodeType, checked(x + rotatedZone.Left), checked(y + rotatedZone.Top),
                                layout.Data, barcode.LineWidth, barcode.Height, barcode.Ratio, barcode.Rotation,
                                barcode.HumanReadable, barcode.HumanReadableReserveHeight,
                                w - quietZone.Left - quietZone.Right, narrow, wide, quietZone);
                            return;
                        }
                        // 静区沿条码数据轴分配；旋转后的预留区域仍以左上角定位。
                        var dx = barcode.Rotation == RotationAngle.None &&
                            !(options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Native)
                            ? layout.Left : 0;
                        var dy = barcode.Rotation == RotationAngle.Rotate90 ? layout.Left : 0;
                        cmd.DrawBarcode1D(barcode.BarcodeType, checked(x + dx), checked(y + dy), layout.Data,
                            barcode.LineWidth, barcode.Height, barcode.Ratio, barcode.Rotation,
                            barcode.HumanReadable, barcode.HumanReadableReserveHeight,
                            options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Native
                                ? w : w - layout.Left, narrow, wide);
                    } });
            }
            return result;
        }

        /// <summary>
        /// 生成二维点阵码的黑白图片布局。
        /// </summary>
        /// <param name="element">点阵码元素及预留尺寸。</param>
        /// <param name="text">待编码的内容。</param>
        /// <param name="available">元素可用的打印点宽度。</param>
        /// <param name="sourcePath">模板元素的节点路径。</param>
        /// <param name="binding">当前重复数据的绑定上下文。</param>
        /// <returns>包含点阵码位图的布局块。</returns>
        private Block MatrixCode(BpMatrixCodeElement element, string text, int available, string sourcePath, Binding binding)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("二维点阵码内容不能为空。");
            if (element.Width > available) throw new ArgumentException("二维点阵码宽度超出单元格。");
            var format = element.Format == "PDF417" ? BarcodeFormat.PDF_417 : BarcodeFormat.DATA_MATRIX;
            cancellation.ThrowIfCancellationRequested();
            using var matrixLease = PrintResourceScope.Reserve(
                PrintMatrixBudget.MatrixCodeBytes(element.Width, element.Height, format == BarcodeFormat.PDF_417),
                "Template.MatrixEncoding");
            BitMatrix bits;
            try
            {
                bits = new MultiFormatWriter().encode(text, format, element.Width, element.Height,
                    new Dictionary<EncodeHintType, object> { [EncodeHintType.CHARACTER_SET] = "UTF-8", [EncodeHintType.MARGIN] = 2 });
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            { throw new ArgumentException("二维点阵码内容无法编码：" + error.Message, error); }
            cancellation.ThrowIfCancellationRequested();
            if (bits.Width > element.Width || bits.Height > element.Height || (long)bits.Width * bits.Height > options.MaxImagePixels)
                throw new ArgumentException($"二维点阵码实际尺寸 {bits.Width}×{bits.Height} 超过预留尺寸 {element.Width}×{element.Height} 或像素限制。");
            Reserve(checked(((bits.Width + 7L) / 8) * bits.Height * 2));
            imagePixels = checked(imagePixels + (long)bits.Width * bits.Height);
            if (imagePixels > (long)options.MaxOutputBytes * 8) throw new ArgumentException("二维点阵码缓存超过渲染预算。");
            var cacheKey = ("\0matrix" + images.Count, bits.Width, bits.Height, 0, 0, 0, 0);
            var imageLease = PrintResourceScope.Reserve(
                checked((long)bits.Width * bits.Height * 4), "Template.MatrixRgba");
            Image<Rgba32> image = null;
            try
            {
                image = new Image<Rgba32>(bits.Width, bits.Height, new Rgba32(255, 255, 255));
                for (var y = 0; y < bits.Height; y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (var x = 0; x < bits.Width; x++) if (bits[x, y]) image[x, y] = new Rgba32(0, 0, 0);
                }
                images.Add(cacheKey, image);
                try
                {
                    imageLeases.Add(cacheKey, imageLease);
                    imageLease = null;
                }
                catch
                {
                    images.Remove(cacheKey);
                    throw;
                }
            }
            catch
            {
                image?.Dispose();
                imageLease?.Dispose();
                throw;
            }
            var imageOptions = new ImageProcessingOptions { MaxEncodedBytes = options.MaxImageBytes, MaxPixels = options.MaxImagePixels };
            var result = new Block(storage) { Height = bits.Height };
            result.Atoms.Add(new Atom { Width = bits.Width, Height = bits.Height, SourcePath = sourcePath, Type = element.Type,
                Text = text, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) => cmd.DrawImage(x, y, image, imageOptions) });
            return result;
        }

        /// <summary>
        /// 将内联或引用组件展开为整体布局块。
        /// </summary>
        /// <param name="group">待展开的组合元素。</param>
        /// <param name="width">可用打印点宽度。</param>
        /// <param name="binding">当前数据绑定上下文。</param>
        /// <param name="sourcePath">组合元素的节点路径。</param>
        /// <returns>包含全部子元素的组合布局块。</returns>
        private Block Group(BpGroupElement group, int width, Binding binding, string sourcePath)
        {
            var definition = group.ComponentId == null ? null : template.Components.First(item => item.Id == group.ComponentId);
            var definitionPath = definition == null ? null : "Components[" + template.Components.IndexOf(definition) + "]";
            var componentInstancePath = definition == null ? null : sourcePath;
            var groupWidth = group.Width > 0 ? group.Width : definition?.Width > 0 ? definition.Width : width;
            var groupHeight = group.Height > 0 ? group.Height : definition?.Height ?? 0;
            if (groupWidth > width) throw new ArgumentException("组合宽度超出单元格。");
            if (template.Version == 3)
            {
                try
                {
                    var parameters = new BudgetedDictionary<string, object>("Template.ComponentParameters", storage,
                        StringComparer.Ordinal, (key, value) => checked(PrintStorageBudget.TextBytes(key) +
                            (value is string text ? PrintStorageBudget.TextBytes(text) : 0)));
                    if (definition?.Parameters != null) foreach (var parameter in definition.Parameters)
                    {
                        if (group.Parameters != null && group.Parameters.TryGetValue(parameter.Key, out var supplied))
                            parameters.Add(parameter.Key, supplied.Path != null ? binding.Resolve(supplied.Path) : ParameterValue(supplied.Value));
                        else if (parameter.Value.DefaultValue != null)
                            parameters.Add(parameter.Key, ParameterValue(parameter.Value.DefaultValue));
                        else if (parameter.Value.Required) throw new ArgumentException("缺少组件参数：" + parameter.Key);
                    }
                    if (group.Parameters != null) foreach (var parameter in group.Parameters)
                        if (definition != null && (definition.Parameters == null || !definition.Parameters.ContainsKey(parameter.Key)))
                            throw new ArgumentException("组件未声明参数：" + parameter.Key);
                    if (definition == null && group.Parameters != null) foreach (var parameter in group.Parameters)
                        parameters.Add(parameter.Key, parameter.Value.Path != null ? binding.Resolve(parameter.Value.Path) : ParameterValue(parameter.Value.Value));
                    var item = group.DataPath == null ? binding.Item : binding.Resolve(group.DataPath);
                    binding = binding.Component(item, parameters);
                }
                catch (TemplateRenderException error)
                {
                    throw error.WithOwnership(definitionPath, componentInstancePath, binding.InstanceIndices);
                }
                catch (ArgumentException error)
                {
                    throw ComponentError(sourcePath, error, definitionPath, componentInstancePath, binding.InstanceIndices);
                }
            }
            var result = new Block(storage);
            var children = definition?.Elements ?? group.Elements;
            if (children != null) for (var index = 0; index < children.Count; index++)
            {
                var childPath = ChildPath(sourcePath, "Elements", index);
                var childDefinitionPath = definitionPath == null ? null : ChildPath(definitionPath, "Elements", index);
                var atomStart = result.Atoms.Count;
                var frameStart = result.Frames.Count;
                try { Place(result, children[index], groupWidth, binding, childPath); }
                catch (TemplateRenderException error)
                {
                    throw error.WithOwnership(MapDefinitionPath(error.SourcePath, childPath, childDefinitionPath),
                        componentInstancePath, binding.InstanceIndices);
                }
                catch (ArgumentException error)
                {
                    throw ComponentError(childPath, error, childDefinitionPath, componentInstancePath,
                        binding.InstanceIndices);
                }
                Mark(result, atomStart, frameStart, sourcePath, childPath,
                    childDefinitionPath);
            }
            var childRows = definition?.Rows ?? group.Rows;
            if (childRows != null)
            {
                var atomStart = result.Atoms.Count;
                var frameStart = result.Frames.Count;
                var rowsPath = sourcePath + ".Rows";
                var definitionRowsPath = definitionPath == null ? null : definitionPath + ".Rows";
                try { AppendRows(result, childRows, groupWidth, binding, sourcePath); }
                catch (TemplateRenderException error)
                {
                    throw error.WithOwnership(MapDefinitionPath(error.SourcePath, rowsPath, definitionRowsPath),
                        componentInstancePath, binding.InstanceIndices);
                }
                catch (ArgumentException error)
                {
                    throw ComponentError(rowsPath, error, definitionRowsPath, componentInstancePath,
                        binding.InstanceIndices);
                }
                Mark(result, atomStart, frameStart, sourcePath, sourcePath + ".Rows",
                    definitionRowsPath);
                foreach (var scope in result.TableScopes)
                    Mark(scope.Header, 0, 0, sourcePath, sourcePath + ".Rows",
                        definitionRowsPath);
            }
            if (groupHeight > 0 && result.Height > groupHeight)
                throw ComponentError(sourcePath, new ArgumentException("组合内容超出设定高度。"), definitionPath,
                    componentInstancePath, binding.InstanceIndices);
            if (template.Version < 3) result.Height = Math.Max(result.Height, groupHeight);
            if (result.Height <= 0 && !(template.Version == 3 && childRows != null))
                throw ComponentError(sourcePath, new ArgumentException("组合高度必须大于零。"), definitionPath,
                    componentInstancePath, binding.InstanceIndices);
            // 无绘制操作的原子边界使整组只在同一页出现。
            var pagination = group.Pagination ?? (template.Version == 3 && childRows != null ? BpGroupPagination.Split : BpGroupPagination.KeepTogether);
            if (pagination == BpGroupPagination.KeepTogether)
                result.Atoms.Add(new Atom { Width = groupWidth, Height = result.Height, SourcePath = sourcePath,
                    DefinitionPath = definitionPath, ComponentInstancePath = sourcePath,
                    Type = group.Type, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) => { } });
            result.Frames.Add(new Frame { Width = groupWidth, Height = result.Height, SourcePath = sourcePath,
                DefinitionPath = definitionPath, ComponentInstancePath = sourcePath,
                Kind = definition == null ? "group" : "component", InstanceIndices = binding.InstanceIndices });
            return result;
        }

        /// <summary>
        /// 创建带组件归属的错误；CPCL 普通渲染继续保留旧的原始异常行为。
        /// </summary>
        private Exception ComponentError(string sourcePath, ArgumentException error, string definitionPath,
            string componentInstancePath, IReadOnlyList<int> instanceIndices)
            => captureLayout || options.Protocol != PrintProtocol.Cpcl
                ? new TemplateRenderException(sourcePath, error.Message, error, definitionPath,
                    componentInstancePath, instanceIndices)
                : error;

        /// <summary>
        /// 将组件实例错误路径映射为对应的定义路径。
        /// </summary>
        private static string MapDefinitionPath(string sourcePath, string instancePrefix, string definitionPrefix)
        {
            if (definitionPrefix == null) return null;
            if (sourcePath == null || instancePrefix == null ||
                !sourcePath.StartsWith(instancePrefix, StringComparison.Ordinal)) return definitionPrefix;
            return definitionPrefix + sourcePath.Substring(instancePrefix.Length);
        }

        /// <summary>
        /// 关联组合子节点的实例路径和定义路径。
        /// </summary>
        /// <param name="block">包含待关联布局节点的块。</param>
        /// <param name="atomStart">需要处理的元素节点起始索引。</param>
        /// <param name="frameStart">需要处理的边界节点起始索引。</param>
        /// <param name="instancePath">组合实例的模板路径。</param>
        /// <param name="childPath">组合子节点的模板路径前缀。</param>
        /// <param name="definitionPath">组合定义的模板路径前缀；没有定义时为 null。</param>
        private static void Mark(Block block, int atomStart, int frameStart, string instancePath,
            string childPath, string definitionPath)
        {
            for (var index = atomStart; index < block.Atoms.Count; index++)
            {
                var atom = block.Atoms[index];
                atom.ComponentInstancePath = atom.ComponentInstancePath ?? instancePath;
                if (atom.DefinitionPath == null && definitionPath != null && atom.SourcePath?.StartsWith(childPath, StringComparison.Ordinal) == true)
                    atom.DefinitionPath = definitionPath + atom.SourcePath.Substring(childPath.Length);
            }
            for (var index = frameStart; index < block.Frames.Count; index++)
            {
                var frame = block.Frames[index];
                frame.ComponentInstancePath = frame.ComponentInstancePath ?? instancePath;
                if (frame.DefinitionPath == null && definitionPath != null && frame.SourcePath?.StartsWith(childPath, StringComparison.Ordinal) == true)
                    frame.DefinitionPath = definitionPath + frame.SourcePath.Substring(childPath.Length);
            }
        }

        /// <summary>
        /// 将 JSON 参数值转换为绑定时使用的标量或结构。
        /// </summary>
        /// <param name="token">待转换的 JSON 值。</param>
        /// <returns>转换后的标量值或 JSON 结构。</returns>
        private static object ParameterValue(Newtonsoft.Json.Linq.JToken token)
            => token is Newtonsoft.Json.Linq.JValue scalar ? scalar.Value : (object)token;

        /// <summary>
        /// 生成矩形或椭圆轮廓的布局块。
        /// </summary>
        /// <param name="shapeWidth">图形打印点宽度。</param>
        /// <param name="shapeHeight">图形打印点高度。</param>
        /// <param name="stroke">轮廓线打印点宽度。</param>
        /// <param name="ellipse">为 true 时绘制椭圆，为 false 时绘制矩形。</param>
        /// <param name="filled">为 true 时绘制占位内的黑色填充；轮廓线宽不影响填充尺寸。</param>
        /// <param name="available">可用打印点宽度。</param>
        /// <param name="sourcePath">模板元素的节点路径。</param>
        /// <param name="binding">当前重复数据的绑定上下文。</param>
        /// <returns>包含轮廓绘制操作的布局块。</returns>
        private Block Shape(int shapeWidth, int shapeHeight, int stroke, bool ellipse, int available, string sourcePath, Binding binding, bool filled = false)
        {
            if (shapeWidth > available) throw new ArgumentException("图形宽度超出单元格。");
            Reserve(ellipse ? 48 * 32 : filled ? 32 : 4 * 32);
            var result = new Block(storage) { Height = shapeHeight };
            result.Atoms.Add(new Atom { Width = shapeWidth, Height = shapeHeight, SourcePath = sourcePath,
                Type = ellipse ? 8 : 7, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) =>
                {
                    if (filled)
                    {
                        cmd.FillRectangle(x, y, shapeWidth, shapeHeight);
                        return;
                    }
                    if (!ellipse)
                    {
                        cmd.Line(x, y, x + shapeWidth - 1, y, stroke);
                        cmd.Line(x + shapeWidth - 1, y, x + shapeWidth - 1, y + shapeHeight - 1, stroke);
                        cmd.Line(x + shapeWidth - 1, y + shapeHeight - 1, x, y + shapeHeight - 1, stroke);
                        cmd.Line(x, y + shapeHeight - 1, x, y, stroke);
                        return;
                    }
                    var segments = 48;
                    var inset = options.Protocol == PrintProtocol.Cpcl ? 0 : stroke / 2;
                    var ellipseWidth = Math.Max(0, shapeWidth - 1 - 2 * inset);
                    var ellipseHeight = Math.Max(0, shapeHeight - 1 - 2 * inset);
                    for (var i = 0; i < segments; i++)
                    {
                        var a = 2 * Math.PI * i / segments;
                        var b = 2 * Math.PI * (i + 1) / segments;
                        cmd.Line(x + inset + (int)Math.Round((1 + Math.Cos(a)) * ellipseWidth / 2),
                            y + inset + (int)Math.Round((1 + Math.Sin(a)) * ellipseHeight / 2),
                            x + inset + (int)Math.Round((1 + Math.Cos(b)) * ellipseWidth / 2),
                            y + inset + (int)Math.Round((1 + Math.Sin(b)) * ellipseHeight / 2), stroke);
                    }
                } });
            return result;
        }

        /// <summary>
        /// 生成单色图标的布局块。
        /// </summary>
        /// <param name="icon">图标类型与尺寸配置。</param>
        /// <param name="available">可用打印点宽度。</param>
        /// <param name="sourcePath">模板元素的节点路径。</param>
        /// <param name="binding">当前重复数据的绑定上下文。</param>
        /// <returns>包含图标绘制操作的布局块。</returns>
        private Block Icon(BpIconElement icon, int available, string sourcePath, Binding binding)
        {
            if (icon.Size > available) throw new ArgumentException("图标宽度超出单元格。");
            Reserve(160);
            var size = icon.Size;
            var result = new Block(storage) { Height = size };
            result.Atoms.Add(new Atom { Width = size, Height = size, SourcePath = sourcePath, Type = icon.Type,
                InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) =>
                {
                    if (icon.Icon == "check")
                    {
                        cmd.Line(x + size / 8, y + size / 2, x + size * 2 / 5, y + size * 4 / 5, icon.LineWidth);
                        cmd.Line(x + size * 2 / 5, y + size * 4 / 5, x + size * 7 / 8, y + size / 8, icon.LineWidth);
                    }
                    else if (icon.Icon == "cross")
                    {
                        cmd.Line(x + size / 8, y + size / 8, x + size * 7 / 8, y + size * 7 / 8, icon.LineWidth);
                        cmd.Line(x + size * 7 / 8, y + size / 8, x + size / 8, y + size * 7 / 8, icon.LineWidth);
                    }
                    else
                    {
                        cmd.Line(x + size / 2, y + size / 3, x + size / 2, y + size * 3 / 4, icon.LineWidth);
                        cmd.Line(x + size / 2, y + size / 8, x + size / 2, y + size / 6, icon.LineWidth);
                    }
                } });
            return result;
        }

        /// <summary>
        /// 生成独立线段的布局块。
        /// </summary>
        /// <param name="line">线段端点与线宽配置。</param>
        /// <param name="width">单元格可用打印点宽度。</param>
        /// <param name="margin">线段的额外边距。</param>
        /// <param name="sourcePath">模板元素的节点路径。</param>
        /// <param name="binding">当前重复数据的绑定上下文。</param>
        /// <returns>包含线段绘制操作的布局块。</returns>
        private Block Line(BpLineElement line, int width, BpMargin margin, string sourcePath, Binding binding)
        {
            var start = line.StartCoordinate;
            var end = line.LineEndCoordinate;
            if (start == null || end == null)
                throw new ArgumentException("线段缺少起止坐标。");
            var minX = Math.Min(start.X, end.X);
            var minY = Math.Min(start.Y, end.Y);
            var maxX = Math.Max(start.X, end.X);
            var maxY = Math.Max(start.Y, end.Y);
            var deltaX = checked(end.X - start.X);
            var deltaY = checked(end.Y - start.Y);
            if (deltaX == 0 && deltaY == 0)
                throw new ArgumentException("线段长度必须大于零。");
            if (checked(maxX + margin.Left + line.LineWidth + margin.Right) > width)
                throw new ArgumentException("线段终点超出单元格宽度。");
            var result = new Block(storage) { Height = checked(maxY + margin.Top + line.LineWidth + margin.Bottom) };
            result.Atoms.Add(new Atom
            {
                X = checked(minX + margin.Left),
                Y = checked(minY + margin.Top),
                Width = checked(Math.Abs(deltaX) + line.LineWidth),
                Height = checked(Math.Abs(deltaY) + line.LineWidth),
                SourcePath = sourcePath,
                Type = line.Type,
                InstanceIndices = binding.InstanceIndices,
                Text = null,
                Draw = (command, x, y) => command.Line(
                    checked(x + (start.X - minX)), checked(y + (start.Y - minY)),
                    checked(x + (end.X - minX)), checked(y + (end.Y - minY)), line.LineWidth)
            });
            return result;
        }

        /// <summary>
        /// 按内置字体度量文字并生成可分页行。
        /// </summary>
        /// <param name="font">文字字号、样式及对齐配置。</param>
        /// <param name="text">字段绑定后的文字内容。</param>
        /// <param name="width">可用打印点宽度。</param>
        /// <param name="sourcePath">模板元素的节点路径。</param>
        /// <param name="binding">当前重复数据的绑定上下文。</param>
        /// <returns>由文字行组成的布局块。</returns>
        private Block Text(BpFontElement font, string text, int width, string sourcePath, Binding binding)
        {
            var resolvedFont = TemplateFontResolver.Resolve(font, options.Protocol, options.EscPosLayout);
            var availableWidth = width;
            var quarterTurn = font.Rotation == RotationAngle.Rotate90 || font.Rotation == RotationAngle.Rotate270;
            var availableHeight = font.EndCoordinate == null ? pageHeight : checked(font.EndCoordinate.Y -
                (font.StartCoordinate?.Y ?? 0) - (font.Margin?.Top ?? 0) - (font.Margin?.Bottom ?? 0));
            width = font.Width > 0 ? font.Width : quarterTurn ? availableHeight : width;
            if (width <= 0 || !quarterTurn && width > availableWidth)
                throw new ArgumentException("文字旋转前的排版宽度超过可用区域。");
            var size = font.FontSize == 0 ? 24 : font.FontSize;
            var bold = font.Bold || (font.FontStyle & 1) != 0;
            var underline = (font.FontStyle & 4) != 0;
            var rasterizeTextStyles =
                (options.Protocol == PrintProtocol.Tspl && options.TsplMedia?.AllowRasterizedTextStyles == true ||
                 options.Protocol == PrintProtocol.Zpl && options.ZplMedia?.AllowRasterizedTextStyles == true) &&
                (bold || underline || font.Reverse);
            var fixedAscii = options.Protocol == PrintProtocol.Tspl &&
                !string.IsNullOrEmpty(resolvedFont.DeviceName) && resolvedFont.DeviceName != "0" &&
                text.All(character => character >= 32 && character <= 126 ||
                    character == '\r' || character == '\n');
            DeviceFontCalibration fontCalibration = null;
            if (options.DeviceFontCalibrations != null)
                options.DeviceFontCalibrations.TryGetValue(resolvedFont.DeviceName ?? "0", out fontCalibration);
            if (options.RequireDeclaredNativeBounds && text.Length > 0 && !fixedAscii && fontCalibration == null &&
                (!options.DeviceHalfCellWidthAt24.HasValue || !options.DeviceFullCellWidthAt24.HasValue ||
                 !options.DeviceLineHeightAt24.HasValue))
                throw new TemplateRenderException(sourcePath,
                    $"协议 {options.Protocol} 的字体 {resolvedFont.DeviceName ?? "0"} 严格设备边界校验要求声明 DeviceFontCalibrations，或全局 DeviceHalfCellWidthAt24、DeviceFullCellWidthAt24 和 DeviceLineHeightAt24。",
                    new ArgumentException("请按目标设备字体实测字宽和行高设置模板选项。"));
            if (!string.IsNullOrWhiteSpace(resolvedFont.HostFamily) &&
                (options.Protocol != PrintProtocol.EscPos || options.EscPosLayout != EscPosLayoutMode.Raster))
                throw new TemplateRenderException(sourcePath,
                    $"协议 {options.Protocol} 不支持模板系统字体族。", new NotSupportedException("请使用 ESC/POS Raster。"));
            if (resolvedFont.DeviceName != null &&
                options.Protocol != PrintProtocol.Tspl && options.Protocol != PrintProtocol.Zpl)
                throw new TemplateRenderException(sourcePath,
                    $"协议 {options.Protocol} 不支持设备字体标识。",
                    new NotSupportedException("设备字体标识仅用于 TSPL/ZPL。"));
            ProtocolTextMetrics metrics;
            try
            {
                metrics = new ProtocolTextMetrics(options.Protocol, options.EscPosLayout, size,
                    resolvedFont.CpclFontName, encoding, text, bold, options.DeviceHalfCellWidthAt24, resolvedFont.HostFamily,
                    options.EscPosDevice?.NativeTabWidthDots, options.DeviceFullCellWidthAt24,
                    (font.FontStyle & 4) != 0, options.MaxImagePixels, resolvedFont.DeviceName,
                    options.DeviceLineHeightAt24, fontCalibration, options.EscPosDevice?.Profile?.Text,
                    options.EscPosDevice?.RasterFonts, cancellation, rasterizeTextStyles, font.Reverse);
            }
            catch (NotSupportedException error)
            {
                throw new TemplateRenderException(sourcePath, $"协议 {options.Protocol} 无法使用字体：{error.Message}", error);
            }
            if ((font.FontStyle & ~5) != 0) throw new ArgumentException("模板文字只支持普通、粗体、下划线。");
            if (font.Field?.Fit == true)
            {
                if (options.Protocol == PrintProtocol.Cpcl)
                {
                    foreach (var candidate in Enum.GetValues(typeof(FontSize)).Cast<int>().Where(n => n <= size && (resolvedFont.CpclFontName == 0 || (n % 24 == 0 ? 24 : 55) == resolvedFont.CpclFontName)).OrderByDescending(n => n))
                    {
                        var candidateMetrics = new ProtocolTextMetrics(options.Protocol, options.EscPosLayout,
                            candidate, resolvedFont.CpclFontName, encoding, text, bold, options.DeviceHalfCellWidthAt24,
                            resolvedFont.HostFamily, options.EscPosDevice?.NativeTabWidthDots,
                            options.DeviceFullCellWidthAt24, (font.FontStyle & 4) != 0,
                            options.MaxImagePixels, resolvedFont.DeviceName, options.DeviceLineHeightAt24,
                            fontCalibration, options.EscPosDevice?.Profile?.Text, options.EscPosDevice?.RasterFonts, cancellation,
                            rasterizeTextStyles, font.Reverse);
                        if (candidateMetrics.Measure(text) <= width && (!quarterTurn || candidateMetrics.LineHeight <= availableWidth) && text.IndexOfAny(new[] { '\r', '\n' }) < 0)
                        { size = candidate; metrics = candidateMetrics; break; }
                    }
                }
                else if (options.Protocol != PrintProtocol.EscPos || options.EscPosLayout != EscPosLayoutMode.Native)
                {
                    if (text.IndexOfAny(new[] { '\r', '\n' }) < 0)
                    {
                        var low = 1;
                        var high = size;
                        while (low <= high)
                        {
                            var candidate = low + (high - low) / 2;
                            var candidateMetrics = new ProtocolTextMetrics(options.Protocol, options.EscPosLayout,
                                candidate, resolvedFont.CpclFontName, encoding, text, bold, options.DeviceHalfCellWidthAt24,
                                resolvedFont.HostFamily, options.EscPosDevice?.NativeTabWidthDots,
                                options.DeviceFullCellWidthAt24, (font.FontStyle & 4) != 0,
                                options.MaxImagePixels, resolvedFont.DeviceName, options.DeviceLineHeightAt24,
                                fontCalibration, options.EscPosDevice?.Profile?.Text, options.EscPosDevice?.RasterFonts, cancellation,
                                rasterizeTextStyles, font.Reverse);
                            if (candidateMetrics.Measure(text) <= width && (!quarterTurn || candidateMetrics.LineHeight <= availableWidth))
                            { size = candidate; metrics = candidateMetrics; low = candidate + 1; }
                            else high = candidate - 1;
                        }
                    }
                }
            }
            var lines = Wrap(text, metrics, width, font.Field?.NeedFeedLine ?? true);
            if (font.Rotation != RotationAngle.None)
            {
                if (metrics.HasDeviceTextElementCalibrations)
                    return RotatedTextWithDeviceBounds(font, resolvedFont, lines, metrics, size, bold,
                        width, availableWidth, sourcePath, binding);
                return RotatedText(font, resolvedFont, lines, metrics, size, bold, width, availableWidth, sourcePath, binding);
            }
            var result = new Block(storage);
            foreach (var line in lines)
            {
                Reserve(32); // 每条文字指令的最小结构成本，限制空字段构造的布局规模。
                var content = line;
                var fontSize = size;
                var fontFamily = metrics.FontFamily;
                var lineWidth = checked((int)metrics.Measure(line));
                var calibratedBounds = metrics.HasDeviceTextElementCalibrations
                    ? metrics.MeasureBounds(line)
                    : new DeviceTextBounds(0, 0, lineWidth, metrics.LineHeight);
                var rasterInk = options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Raster
                    ? EscPosRasterCanvas.MeasureTextInkBounds(content, fontSize, bold,
                        (font.FontStyle & 4) != 0, fontFamily, options.MaxImagePixels, options.EscPosDevice?.RasterFonts, cancellation)
                    : default(EscPosRasterTextBounds);
                var rasterReverseBounds = font.Reverse && options.Protocol == PrintProtocol.EscPos &&
                    options.EscPosLayout == EscPosLayoutMode.Raster
                    ? EscPosRasterCanvas.MeasureReverseTextBounds(content, fontSize, bold,
                        (font.FontStyle & 4) != 0, fontFamily, options.MaxImagePixels,
                        options.EscPosDevice?.RasterFonts, cancellation)
                    : default(EscPosRasterTextBounds);
                var rasterLeftPadding = font.Reverse ? 0 : rasterInk.IsEmpty ? 0 : Math.Max(0, -rasterInk.Left);
                var rasterTopPadding = font.Reverse ? 0 : rasterInk.IsEmpty ? 0 : Math.Max(0, -rasterInk.Top);
                var lineHeight = font.Reverse && !rasterReverseBounds.IsEmpty
                    ? Math.Max(metrics.LineHeight, rasterReverseBounds.Height)
                    : rasterInk.IsEmpty ? calibratedBounds.Height : Math.Max(metrics.LineHeight, checked(rasterTopPadding + rasterInk.Bottom));
                var alignmentOffset = font.TextAlignment == BpTextAlignment.Right
                    ? width - lineWidth
                    : font.TextAlignment == BpTextAlignment.Center ? (width - lineWidth) / 2 : 0;
                var y = result.Height;
                var useDeviceBounds = metrics.HasDeviceTextElementCalibrations;
                result.Atoms.Add(new Atom {
                    X = alignmentOffset,
                    Y = y,
                    Width = useDeviceBounds ? calibratedBounds.Width : lineWidth,
                    Height = lineHeight,
                    SourcePath = sourcePath, Type = font.Type, Text = content,
                    InstanceIndices = binding.InstanceIndices,
                    Draw = (cmd, x, py) => cmd.DrawText(
                        // Keep the measured footprint at the template anchor and compensate the native origin for its bearing.
                        useDeviceBounds ? checked(x - calibratedBounds.Left) : checked(x + rasterLeftPadding),
                        useDeviceBounds ? checked(py - calibratedBounds.Top) : checked(py + rasterTopPadding),
                        content, fontSize, 0, bold, font.Reverse, (font.FontStyle & 4) != 0,
                        fontFamily,
                        options.Protocol == PrintProtocol.Tspl || options.Protocol == PrintProtocol.Zpl
                            ? Math.Max(content.Length == 0 ? 0 : 1, useDeviceBounds ? calibratedBounds.Right : lineWidth)
                            : 0,
                        options.Protocol == PrintProtocol.Tspl || options.Protocol == PrintProtocol.Zpl
                            ? Math.Max(content.Length == 0 ? 0 : 1, useDeviceBounds ? calibratedBounds.Bottom : lineHeight)
                            : 0,
                        resolvedFont.DeviceName, resolvedFont.Selection)
                });
                var gap = options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Native &&
                    options.EscPosDevice?.Profile?.Text != null ? options.EscPosDevice.Profile.Text.LineHeightDots - 24 : 2;
                result.Height = checked(result.Height + lineHeight + gap);
            }
            if (result.Height > 0)
                result.Height -= options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Native &&
                    options.EscPosDevice?.Profile?.Text != null ? options.EscPosDevice.Profile.Text.LineHeightDots - 24 : 2;
            return result;
        }

        private Block RotatedText(BpFontElement font, ResolvedTemplateFont resolvedFont, IReadOnlyList<string> lines, ProtocolTextMetrics metrics,
            int size, bool bold, int lineWidthLimit, int availableWidth, string sourcePath, Binding binding)
        {
            var lineHeights = new int[lines.Count];
            var raster = options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Raster;
            for (var index = 0; index < lines.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                var lineHeight = metrics.LineHeight;
                if (raster)
                {
                    if (font.Reverse)
                    {
                        var bounds = EscPosRasterCanvas.MeasureReverseTextBounds(lines[index], size, bold,
                            (font.FontStyle & 4) != 0, metrics.FontFamily, options.MaxImagePixels,
                            options.EscPosDevice?.RasterFonts, cancellation);
                        lineHeight = Math.Max(lineHeight, bounds.Height);
                    }
                    else
                    {
                        var ink = EscPosRasterCanvas.MeasureTextInkBounds(lines[index], size, bold,
                            (font.FontStyle & 4) != 0, metrics.FontFamily, options.MaxImagePixels,
                            options.EscPosDevice?.RasterFonts, cancellation);
                        // Raster rotation draws a tight ink box. Include an underline or
                        // descender that extends beyond the logical device line height.
                        if (!ink.IsEmpty) lineHeight = Math.Max(lineHeight, ink.Height);
                    }
                }
                lineHeights[index] = lineHeight;
            }
            var totalHeight = 0;
            var topOffsets = new int[lines.Count];
            for (var index = 0; index < lines.Count; index++)
            {
                topOffsets[index] = totalHeight;
                totalHeight = checked(totalHeight + lineHeights[index] + 2);
            }
            if (lines.Count > 0) totalHeight -= 2;
            using var widthsStorage = PrintResourceScope.Reserve(checked(lines.Count * 12L), "Template.TextWidths");
            var widths = lines.Select(line => checked((int)metrics.Measure(line))).ToArray();
            // 对齐在旋转前的行内进行；旋转后整体占位包含各行的对齐留白。
            var totalWidth = font.TextAlignment == BpTextAlignment.Left ? widths.Max() : lineWidthLimit;
            var quarterTurn = font.Rotation == RotationAngle.Rotate90 || font.Rotation == RotationAngle.Rotate270;
            if ((quarterTurn ? totalHeight : totalWidth) > availableWidth)
                throw new ArgumentException("旋转后的文字宽度超过单元格。");
            var result = new Block(storage) { Height = quarterTurn ? totalWidth : totalHeight };
            for (var index = 0; index < lines.Count; index++)
            {
                Reserve(32);
                var content = lines[index];
                var w = widths[index];
                var lineHeight = lineHeights[index];
                var left = font.TextAlignment == BpTextAlignment.Right ? lineWidthLimit - w :
                    font.TextAlignment == BpTextAlignment.Center ? (lineWidthLimit - w) / 2 : 0;
                var top = topOffsets[index];
                int x, y;
                switch (font.Rotation)
                {
                    case RotationAngle.Rotate90: x = totalHeight - top - lineHeight; y = left; break;
                    case RotationAngle.Rotate180: x = totalWidth - left - w; y = totalHeight - top - lineHeight; break;
                    case RotationAngle.Rotate270: x = top; y = totalWidth - left - w; break;
                    default: throw new ArgumentOutOfRangeException(nameof(font.Rotation));
                }
                result.Atoms.Add(new Atom { X = x, Y = y, Width = quarterTurn ? lineHeight : w,
                    Height = quarterTurn ? w : lineHeight, Rotation = font.Rotation, SourcePath = sourcePath,
                    Type = font.Type, Text = content, InstanceIndices = binding.InstanceIndices,
                    Draw = (cmd, px, py) => {
                        // 空行保留布局间隔，但没有可旋转的字形或设备边界。
                        if (content.Length != 0)
                            cmd.DrawText(px, py, content, size, (int)font.Rotation,
                                bold, font.Reverse, (font.FontStyle & 4) != 0, metrics.FontFamily, w, lineHeight, resolvedFont.DeviceName, resolvedFont.Selection);
                    } });
            }
            // 旋转文字的整个矩形不可拆页，否则会改变列次序或使短行脱离原始位置。
            result.Atoms.Add(new Atom { Width = quarterTurn ? totalHeight : totalWidth, Height = result.Height,
                SourcePath = sourcePath, IsLayoutOnly = true, InstanceIndices = binding.InstanceIndices,
                Draw = (cmd, x, y) => { } });
            return result;
        }

        private Block RotatedTextWithDeviceBounds(BpFontElement font, ResolvedTemplateFont resolvedFont,
            IReadOnlyList<string> lines, ProtocolTextMetrics metrics, int size, bool bold,
            int lineWidthLimit, int availableWidth, string sourcePath, Binding binding)
        {
            using var widthsStorage = PrintResourceScope.Reserve(checked(lines.Count * 20L), "Template.TextWidths");
            var widths = new int[lines.Count];
            var bounds = new DeviceTextBounds[lines.Count];
            var lineOriginsY = new int[lines.Count];
            var leftOffsets = new int[lines.Count];
            var totalHeight = 0;
            var minX = 0;
            var alignedWidth = font.TextAlignment == BpTextAlignment.Left ? 0 : lineWidthLimit;
            var maxX = alignedWidth;
            for (var index = 0; index < lines.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                widths[index] = checked((int)metrics.Measure(lines[index]));
                bounds[index] = metrics.MeasureBounds(lines[index]);
                var width = widths[index];
                var left = font.TextAlignment == BpTextAlignment.Right ? lineWidthLimit - width :
                    font.TextAlignment == BpTextAlignment.Center ? (lineWidthLimit - width) / 2 : 0;
                leftOffsets[index] = left;
                minX = Math.Min(minX, checked(left + bounds[index].Left));
                maxX = Math.Max(maxX, checked(left + bounds[index].Right));
                alignedWidth = Math.Max(alignedWidth, width);
                lineOriginsY[index] = checked(totalHeight - bounds[index].Top);
                totalHeight = checked(totalHeight + bounds[index].Height + 2);
            }
            if (lines.Count > 0) totalHeight -= 2;
            maxX = Math.Max(maxX, alignedWidth);
            var horizontalShift = minX < 0 ? checked(-minX) : 0;
            var totalWidth = checked(maxX - minX);
            var quarterTurn = font.Rotation == RotationAngle.Rotate90 || font.Rotation == RotationAngle.Rotate270;
            if (totalWidth > availableWidth)
                throw new ArgumentException("旋转后的文字宽度超过单元格。");

            var result = new Block(storage) { Height = quarterTurn ? totalWidth : totalHeight };
            for (var index = 0; index < lines.Count; index++)
            {
                Reserve(32);
                var content = lines[index];
                var measured = bounds[index];
                var textOriginX = checked(leftOffsets[index] + horizontalShift);
                var textOriginY = lineOriginsY[index];
                int originX, originY, atomX, atomY, atomWidth, atomHeight, elementX, elementY;
                switch (font.Rotation)
                {
                    case RotationAngle.Rotate90:
                        originX = checked(totalHeight - textOriginY);
                        originY = textOriginX;
                        atomX = checked(originX - measured.Bottom);
                        atomY = checked(originY + measured.Left);
                        atomWidth = measured.Height;
                        atomHeight = measured.Width;
                        elementX = checked(originX - measured.Height);
                        elementY = originY;
                        break;
                    case RotationAngle.Rotate180:
                        originX = checked(totalWidth - textOriginX);
                        originY = checked(totalHeight - textOriginY);
                        atomX = checked(originX - measured.Right);
                        atomY = checked(originY - measured.Bottom);
                        atomWidth = measured.Width;
                        atomHeight = measured.Height;
                        elementX = checked(originX - measured.Width);
                        elementY = checked(originY - measured.Height);
                        break;
                    case RotationAngle.Rotate270:
                        originX = textOriginY;
                        originY = checked(totalWidth - textOriginX);
                        atomX = checked(originX + measured.Top);
                        atomY = checked(originY - measured.Right);
                        atomWidth = measured.Height;
                        atomHeight = measured.Width;
                        elementX = originX;
                        elementY = checked(originY - measured.Width);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(font.Rotation));
                }
                var relativeElementX = options.Protocol == PrintProtocol.Tspl
                    ? checked(elementX - atomX) : 0;
                var relativeElementY = options.Protocol == PrintProtocol.Tspl
                    ? checked(elementY - atomY) : 0;
                result.Atoms.Add(new Atom {
                    X = atomX, Y = atomY, Width = atomWidth, Height = atomHeight,
                    Rotation = font.Rotation, SourcePath = sourcePath, Type = font.Type,
                    Text = content, InstanceIndices = binding.InstanceIndices,
                    Draw = (cmd, px, py) => {
                        if (content.Length != 0)
                            cmd.DrawText(checked(px + relativeElementX), checked(py + relativeElementY),
                                content, size, (int)font.Rotation, bold, font.Reverse,
                                (font.FontStyle & 4) != 0, metrics.FontFamily,
                                measured.Width, measured.Height, resolvedFont.DeviceName, resolvedFont.Selection);
                    }
                });
            }
            result.Atoms.Add(new Atom {
                Width = quarterTurn ? totalHeight : totalWidth, Height = result.Height,
                SourcePath = sourcePath, IsLayoutOnly = true, InstanceIndices = binding.InstanceIndices,
                Draw = (cmd, x, y) => { }
            });
            return result;
        }
        /// <summary>
        /// 按字体字距与可用宽度拆分文字行。
        /// </summary>
        /// <param name="value">待布局的文字。</param>
        /// <param name="metrics">当前字体的字距度量器。</param>
        /// <param name="width">单行可用打印点宽度。</param>
        /// <param name="wrap">是否允许按宽度换行。</param>
        /// <returns>保持 Unicode 字符边界的文字行。</returns>
        private BudgetedList<string> Wrap(string value, ProtocolTextMetrics metrics, int width, bool wrap)
        {
            if (metrics.NeedsFullMeasure) return WrapRaster(value, metrics, width, wrap);
            using var scratch = PrintResourceScope.Reserve(checked(value.Length * 8L + 128), "Template.WrapScratch");
            var result = new BudgetedList<string>("Template.TextLines", owner: storage, retainedSize: PrintStorageBudget.TextBytes);
            var line = new StringBuilder();
            var used = 0;
            var iterator = StringInfo.GetTextElementEnumerator(value.Replace("\r\n", "\n").Replace('\r', '\n'));
            while (iterator.MoveNext())
            {
                cancellation.ThrowIfCancellationRequested();
                var character = iterator.GetTextElement();
                if (character == "\n") { result.Add(line.ToString()); line.Clear(); used = 0; continue; }
                if (character.Any(c => char.IsControl(c))) throw new ArgumentException("文字包含不支持的控制字符。");
                var advance = metrics.Advance(character);
                if (advance > width) throw new ArgumentException($"字符“{character}”的宽度 {advance} 超出可用宽度 {width}。");
                var nextWidth = checked(used + advance);
                if (nextWidth > width)
                {
                    if (!wrap) throw new ArgumentException("文字超出宽度且未启用换行。");
                    result.Add(line.ToString()); line.Clear(); used = 0;
                    nextWidth = advance;
                }
                line.Append(character); used = nextWidth;
            }
            result.Add(line.ToString());
            return result;
        }

        private BudgetedList<string> WrapRaster(string value, ProtocolTextMetrics metrics, int width, bool wrap)
        {
            using var scratch = PrintResourceScope.Reserve(checked(value.Length * 8L + 128), "Template.WrapScratch");
            var result = new BudgetedList<string>("Template.TextLines", owner: storage, retainedSize: PrintStorageBudget.TextBytes);
            var segment = new StringBuilder();
            var offsets = new BudgetedList<int>("Template.TextOffsets", slotBytes: 4, owner: storage);
            offsets.Add(0);
            var iterator = StringInfo.GetTextElementEnumerator(value.Replace("\r\n", "\n").Replace('\r', '\n'));
            while (iterator.MoveNext())
            {
                cancellation.ThrowIfCancellationRequested();
                var character = iterator.GetTextElement();
                if (character == "\n")
                {
                    AppendRasterLines(segment.ToString(), offsets, metrics, width, wrap, result);
                    segment.Clear(); offsets.Clear(); offsets.Add(0);
                    continue;
                }
                if (character.Any(c => char.IsControl(c))) throw new ArgumentException("文字包含不支持的控制字符。");
                var advance = metrics.Advance(character);
                if (advance > width) throw new ArgumentException($"字符“{character}”的宽度 {advance} 超出可用宽度 {width}。");
                segment.Append(character);
                offsets.Add(segment.Length);
            }
            AppendRasterLines(segment.ToString(), offsets, metrics, width, wrap, result);
            return result;
        }

        private void AppendRasterLines(string segment, IReadOnlyList<int> offsets,
            ProtocolTextMetrics metrics, int width, bool wrap, BudgetedList<string> result)
        {
            var count = offsets.Count - 1;
            if (count == 0) { result.Add(string.Empty); return; }
            if (!wrap)
            {
                if (metrics.Measure(segment) > width) throw new ArgumentException("文字超出宽度且未启用换行。");
                result.Add(segment);
                return;
            }

            for (var start = 0; start < count;)
            {
                cancellation.ThrowIfCancellationRequested();
                var remaining = count - start;
                var fit = 1;
                var probe = Math.Min(2, remaining);
                while (probe > fit)
                {
                    using var candidateStorage = PrintResourceScope.Reserve(checked(24L + (offsets[start + probe] - offsets[start]) * 2L), "Template.MeasureCandidate");
                    var candidate = segment.Substring(offsets[start], offsets[start + probe] - offsets[start]);
                    if (metrics.Measure(candidate) > width) break;
                    fit = probe;
                    probe = probe > remaining / 2 ? remaining : probe * 2;
                }
                var lower = fit + 1;
                var upper = probe - 1;
                while (lower <= upper)
                {
                    var middle = lower + (upper - lower) / 2;
                    using var candidateStorage = PrintResourceScope.Reserve(checked(24L + (offsets[start + middle] - offsets[start]) * 2L), "Template.MeasureCandidate");
                    var candidate = segment.Substring(offsets[start], offsets[start + middle] - offsets[start]);
                    if (metrics.Measure(candidate) <= width) { fit = middle; lower = middle + 1; }
                    else upper = middle - 1;
                }
                var lineStart = offsets[start];
                var lineLength = offsets[start + fit] - lineStart;
                result.AddFactory(() => segment.Substring(lineStart, lineLength));
                start += fit;
            }
        }

        /// <summary>
        /// 从资源提供器加载并缓存指定尺寸的图片。
        /// </summary>
        /// <param name="key">图片资源键。</param>
        /// <param name="width">要求的打印点宽度；为零时由原图或指定高度按比例计算。</param>
        /// <param name="height">要求的打印点高度；为零时按目标宽度等比例计算。</param>
        /// <param name="crop">可选的源图片像素裁切区域。</param>
        /// <returns>由当前会话管理生命周期的图片。</returns>
        private Image LoadImage(string key, int width, int height, BpImageCrop crop)
        {
            if (width < 0 || height < 0) throw new ArgumentException("图片尺寸不能为负数。");
            var cropX = crop?.X ?? 0;
            var cropY = crop?.Y ?? 0;
            var cropWidth = crop?.Width ?? 0;
            var cropHeight = crop?.Height ?? 0;
            var cacheKey = (key, width, height, cropX, cropY, cropWidth, cropHeight);
            if (images.TryGetValue(cacheKey, out var cached)) return cached;
            if (resources == null) throw new ArgumentException("图片元素需要资源提供器。");
            if (!resourceBytes.TryGetValue(key, out var bytes))
            {
                using (var source = resources.OpenRead(key, cancellation) ?? throw new ArgumentException("图片资源不存在：" + key))
                using (var target = new MemoryStream())
                {
                    IDisposable capacityLease = null;
                    try
                    {
                        using (PrintResourceScope.Reserve(8192, "Template.ImageReadBuffer"))
                        {
                            var buffer = new byte[8192];
                            while (true)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                var read = source.Read(buffer, 0, buffer.Length);
                                cancellation.ThrowIfCancellationRequested();
                                if (read == 0) break;
                                var required = checked(target.Length + read);
                                if (required > options.MaxImageBytes)
                                    throw new ArgumentException("图片编码数据超过限制。");
                                EnsureImageStreamCapacity(target, ref capacityLease, required);
                                target.Write(buffer, 0, read);
                            }
                        }

                        IDisposable encodedLease = null;
                        try
                        {
                            encodedLease = PrintResourceScope.Reserve(target.Length, "Template.ImageEncoded");
                            bytes = target.ToArray();
                            cachedEncodedBytes = checked(cachedEncodedBytes + bytes.Length);
                            if (cachedEncodedBytes > options.MaxOutputBytes)
                                throw new ArgumentException("图片编码缓存超过渲染预算。");
                            encodedLeases.Add(key, encodedLease);
                            try
                            {
                                resourceBytes.Add(key, bytes);
                                encodedLease = null;
                            }
                            catch
                            {
                                encodedLeases.Remove(key);
                                throw;
                            }
                        }
                        finally { encodedLease?.Dispose(); }
                    }
                    finally { capacityLease?.Dispose(); }
                }
            }
            using (var stream = new MemoryStream(bytes, false))
            {
                cancellation.ThrowIfCancellationRequested();
                var info = Image.Identify(stream) ?? throw new ArgumentException("图片格式无效。");
                cancellation.ThrowIfCancellationRequested();
                if ((long)info.Width * info.Height > options.MaxImagePixels)
                {
                    var error = new ArgumentException("图片像素数超过限制。");
                    TemplateImageDiagnostic.Attach(error, key, crop, info.Width, info.Height, null, null);
                    throw error;
                }
                if (crop != null && (checked((long)crop.X + crop.Width) > info.Width ||
                    checked((long)crop.Y + crop.Height) > info.Height))
                {
                    var error = new ArgumentException($"图片裁切区域 {crop.X},{crop.Y},{crop.Width}×{crop.Height} 超出源图片 {info.Width}×{info.Height}。", nameof(crop));
                    TemplateImageDiagnostic.Attach(error, key, crop, info.Width, info.Height, null, null);
                    throw error;
                }
                var sourceWidth = crop?.Width ?? info.Width;
                var sourceHeight = crop?.Height ?? info.Height;
                var targetWidth = width == 0 ? (height == 0 ? sourceWidth : checked((int)Math.Max(1, (long)sourceWidth * height / sourceHeight))) : width;
                var targetHeight = height == 0 ? checked((int)Math.Max(1, (long)sourceHeight * targetWidth / sourceWidth)) : height;
                var pixels = (long)targetWidth * targetHeight;
                if (pixels > options.MaxImagePixels)
                {
                    var error = new ArgumentException("缩放后的图片像素数超过限制。");
                    TemplateImageDiagnostic.Attach(error, key, crop, info.Width, info.Height, targetWidth, targetHeight);
                    throw error;
                }
                // 缓存总量同样受输出预算约束，避免许多资源绕过单图限制。
                imagePixels = checked(imagePixels + Math.Max((long)info.Width * info.Height, pixels));
                if (imagePixels > (long)options.MaxOutputBytes * 8)
                {
                    var error = new ArgumentException("图片缓存超过本次渲染预算。");
                    TemplateImageDiagnostic.Attach(error, key, crop, info.Width, info.Height, targetWidth, targetHeight);
                    throw error;
                }
                stream.Position = 0;
                var format = Image.DetectFormat(stream);
                if (format is WebpFormat && AnimatedWebp(bytes))
                {
                    var error = new ArgumentException("不支持多帧 WebP 图片。");
                    TemplateImageDiagnostic.Attach(error, key, crop, info.Width, info.Height, targetWidth, targetHeight);
                    throw error;
                }
                stream.Position = 0;
                cancellation.ThrowIfCancellationRequested();
                var decodedLease = PrintResourceScope.Reserve(
                    checked((long)info.Width * info.Height * 4), "Template.ImageDecodeRgba");
                Image<Rgba32> image = null;
                var imageAdded = false;
                try
                {
                    image = format is GifFormat ? Image.Load<Rgba32>(stream,
                        new GifDecoder { MaxFrames = 1, DecodingMode = FrameDecodingMode.First, IgnoreMetadata = true }) :
                        format is TiffFormat ? Image.Load<Rgba32>(stream,
                            new TiffDecoder { DecodingMode = FrameDecodingMode.First, IgnoreMetadata = true }) :
                            Image.Load<Rgba32>(stream);
                    cancellation.ThrowIfCancellationRequested();
                    while (image.Frames.Count > 1)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        image.Frames.RemoveFrame(image.Frames.Count - 1);
                    }
                    if (crop != null)
                    {
                        var croppedPixels = checked((long)crop.Width * crop.Height);
                        var croppedLease = PrintResourceScope.Reserve(
                            checked(croppedPixels * 4), "Template.ImageCropRgba");
                        try
                        {
                            image.Mutate(ctx => ctx.Crop(new Rectangle(crop.X, crop.Y, crop.Width, crop.Height)));
                            var previousLease = decodedLease;
                            decodedLease = croppedLease;
                            croppedLease = null;
                            previousLease.Dispose();
                        }
                        finally { croppedLease?.Dispose(); }
                        cancellation.ThrowIfCancellationRequested();
                    }
                    if (image.Width != targetWidth || image.Height != targetHeight)
                    {
                        var resizedLease = PrintResourceScope.Reserve(
                            checked(pixels * 4), "Template.ImageResizeRgba");
                        try
                        {
                            image.Mutate(ctx => ctx.Resize(targetWidth, targetHeight));
                            var previousLease = decodedLease;
                            decodedLease = resizedLease;
                            resizedLease = null;
                            previousLease.Dispose();
                        }
                        finally { resizedLease?.Dispose(); }
                    }
                    cancellation.ThrowIfCancellationRequested();
                    images.Add(cacheKey, image);
                    imageAdded = true;
                    try
                    {
                        imageLeases.Add(cacheKey, decodedLease);
                        decodedLease = null;
                    }
                    catch
                    {
                        images.Remove(cacheKey);
                        imageAdded = false;
                        throw;
                    }
                    return image;
                }
                catch (Exception error)
                {
                    TemplateImageDiagnostic.Attach(error, key, crop, info.Width, info.Height, targetWidth, targetHeight);
                    if (imageAdded) images.Remove(cacheKey);
                    image?.Dispose();
                    decodedLease?.Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// 将图片输入流扩容到足够容纳下一次读取的数据，并在扩容成功后替换旧租约。
        /// </summary>
        private static void EnsureImageStreamCapacity(MemoryStream target, ref IDisposable capacityLease, long required)
        {
            if (required <= target.Capacity) return;
            if (required > int.MaxValue) throw new ArgumentException("图片编码数据超过流容量限制。");
            var current = target.Capacity;
            var requested = Math.Min((long)int.MaxValue,
                Math.Max(required, current == 0 ? 8192L : checked(current * 2L)));
            var nextLease = PrintResourceScope.Reserve(requested, "Template.ImageReadStream");
            try
            {
                target.Capacity = checked((int)requested);
                var previousLease = capacityLease;
                capacityLease = nextLease;
                nextLease = null;
                previousLease?.Dispose();
            }
            finally { nextLease?.Dispose(); }
        }

        /// <summary>清理编码图片缓存并归还其资源租约。</summary>
        private void ClearEncodedResources()
        {
            foreach (var lease in encodedLeases.Values) lease.Dispose();
            encodedLeases.Clear();
            resourceBytes.Clear();
            cachedEncodedBytes = 0;
        }

        /// <summary>清理指定解码图片并归还其资源租约。</summary>
        private void RemoveImage((string Key, int Width, int Height, int CropX, int CropY, int CropWidth, int CropHeight) key)
        {
            images.TryGetValue(key, out var image);
            imageLeases.TryGetValue(key, out var lease);
            images.Remove(key);
            imageLeases.Remove(key);
            try
            {
                image?.Dispose();
            }
            finally { lease?.Dispose(); }
        }

        /// <summary>
        /// 为预计的输出字节数预留预算。
        /// </summary>
        /// <param name="bytes">预计新增的指令字节数。</param>
        private void Reserve(long bytes)
        {
            payloadBytes = checked(payloadBytes + bytes);
            if (payloadBytes > options.MaxOutputBytes) throw new ArgumentException("布局数据超过输出预算。");
        }
        /// <summary>
        /// 检查 WebP 数据是否包含多个帧。
        /// </summary>
        /// <param name="bytes">待检查的 WebP 编码字节。</param>
        /// <returns>包含多帧时为 true，否则为 false。</returns>
        private static bool AnimatedWebp(byte[] bytes)
        {
            for (long position = 12; position + 8 <= bytes.Length;)
            {
                var p = (int)position;
                var length = (uint)bytes[p + 4] | (uint)bytes[p + 5] << 8 | (uint)bytes[p + 6] << 16 | (uint)bytes[p + 7] << 24;
                if (length > bytes.Length - position - 8) throw new ArgumentException("WebP 数据块长度无效。");
                if (bytes[p] == 'A' && bytes[p + 1] == 'N' && bytes[p + 2] == 'I' && bytes[p + 3] == 'M') return true;
                if (bytes[p] == 'A' && bytes[p + 1] == 'N' && bytes[p + 2] == 'M' && bytes[p + 3] == 'F') return true;
                if (bytes[p] == 'V' && bytes[p + 1] == 'P' && bytes[p + 2] == '8' && bytes[p + 3] == 'X' && length > 0 && (bytes[p + 8] & 2) != 0) return true;
                position += 8L + length + (length & 1);
            }
            return false;
        }

        /// <summary>
        /// 将布局块的指定垂直片段绘制到当前页。
        /// </summary>
        /// <param name="command">当前页的打印命令。</param>
        /// <param name="block">待绘制的布局块。</param>
        /// <param name="start">片段在布局块中的起始打印点。</param>
        /// <param name="end">片段在布局块中的结束打印点。</param>
        /// <param name="destination">片段在当前页中的起始打印点。</param>
        /// <param name="horizontal">片段的水平打印点偏移。</param>
        private void DrawSlice(IPrintCanvas command, Block block, int start, int end, int destination, int horizontal = 0)
        {
            foreach (var atom in block.Atoms)
            {
                cancellation.ThrowIfCancellationRequested();
                if (atom.Y >= start && atom.Y < end)
                {
                    var y = checked(atom.Y - start + destination);
                    var ownership = new TemplateLayoutElement(atom.SourcePath, "element", atom.Type,
                        atom.InstanceIndices, checked(atom.X + horizontal), y, atom.Width, atom.Height, atom.Text, atom.ResourceKey,
                        atom.DefinitionPath, atom.ComponentInstancePath, pageIndex, atom.Rotation);
                    if (!atom.IsLayoutOnly) pageLayout.Add(ownership);
                    try
                    {
                        command.SetSourceOwnership(ownership);
                        atom.Draw(command, checked(atom.X + horizontal), y);
                    }
                    catch (TemplateRenderException error)
                    {
                        throw error.WithOwnership(atom.DefinitionPath, atom.ComponentInstancePath, atom.InstanceIndices);
                    }
                    catch (NotSupportedException error)
                    {
                        throw new TemplateRenderException(atom.SourcePath,
                            $"协议 {options.Protocol} 不支持此模板元素：{error.Message}", error,
                            atom.DefinitionPath, atom.ComponentInstancePath, atom.InstanceIndices);
                    }
                }
            }
            foreach (var frameSource in block.Frames)
            {
                var frame = frameSource;
                if (horizontal != 0) frame = new Frame { X = checked(frame.X + horizontal), Y = frame.Y,
                    Width = frame.Width, Height = frame.Height, Border = frame.Border, SourcePath = frame.SourcePath,
                    Kind = frame.Kind, InstanceIndices = frame.InstanceIndices,
                    DefinitionPath = frame.DefinitionPath, ComponentInstancePath = frame.ComponentInstancePath };
                try
                {
                    command.SetSourceOwnership(new TemplateLayoutElement(frame.SourcePath, frame.Kind, 0,
                        frame.InstanceIndices, frame.X, checked(Math.Max(frame.Y, start) - start + destination),
                        frame.Width, Math.Max(0, Math.Min(checked(frame.Y + frame.Height), end) - Math.Max(frame.Y, start)),
                        null, null, frame.DefinitionPath, frame.ComponentInstancePath, pageIndex));
                    DrawBorder(command, frame, start, end, destination);
                }
                catch (NotSupportedException error)
                {
                    throw new TemplateRenderException(frame.SourcePath,
                        $"协议 {options.Protocol} 不支持此模板边框：{error.Message}", error,
                        frame.DefinitionPath, frame.ComponentInstancePath, frame.InstanceIndices);
                }
                if (captureLayout && frame.Y < end && checked(frame.Y + frame.Height) > start)
                {
                    var top = Math.Max(frame.Y, start);
                    var bottom = Math.Min(checked(frame.Y + frame.Height), end);
                    pageLayout.Add(new TemplateLayoutElement(frame.SourcePath, frame.Kind, 0, frame.InstanceIndices,
                        frame.X, checked(top - start + destination), frame.Width, bottom - top, null, null,
                        frame.DefinitionPath, frame.ComponentInstancePath, pageIndex));
                }
            }
        }
        /// <summary>
        /// 为跨页布局片段绘制对应的边框部分。
        /// </summary>
        /// <param name="command">当前页的打印命令。</param>
        /// <param name="frame">行、单元格或组合的边框范围。</param>
        /// <param name="start">片段在原布局块中的起始打印点。</param>
        /// <param name="end">片段在原布局块中的结束打印点。</param>
        /// <param name="destination">片段在当前页中的起始打印点。</param>
        private void DrawBorder(IPrintCanvas command, Frame frame, int start, int end, int destination)
        {
            if (frame.Border == null || frame.Height == 0 || frame.Y >= end || checked(frame.Y + frame.Height) <= start) return;
            var top = checked(Math.Max(frame.Y, start) - start + destination);
            var bottom = checked(Math.Min(frame.Y + frame.Height, end) - start + destination - 1);
            var right = checked(frame.X + frame.Width - 1);
            Line(frame.X, top, right, top, frame.Border.TopStyle);
            Line(frame.X, bottom, right, bottom, frame.Border.BottomStyle);
            Line(frame.X, top, frame.X, bottom, frame.Border.LeftStyle);
            Line(right, top, right, bottom, frame.Border.RightStyle);
            void Line(int x0, int y0, int x1, int y1, int style)
            {
                if (style == 0) return;
                if (style == 1) { command.Line(x0, y0, x1, y1, 1); return; }
                var length = Math.Max(x1 - x0, y1 - y0);
                if ((long)length / 8 * 40 > options.MaxOutputBytes) throw new ArgumentException("虚线超出输出预算。");
                for (long offset = 0; offset <= length; offset += 8)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var finish = (int)Math.Min(length, offset + 3);
                    command.Line(x0 + (x1 == x0 ? 0 : (int)offset), y0 + (y1 == y0 ? 0 : (int)offset), x0 + (x1 == x0 ? 0 : finish), y0 + (y1 == y0 ? 0 : finish), 1);
                }
            }
        }

        /// <summary>
        /// 构造可定位子元素的模板节点路径。
        /// </summary>
        /// <param name="parent">父节点路径；根节点时为 null。</param>
        /// <param name="collection">子节点所在集合名称。</param>
        /// <param name="index">子节点在集合中的序号。</param>
        /// <returns>可映射回模板结构的节点路径。</returns>
        private string ChildPath(string parent, string collection, int index)
            => (parent == null ? string.Empty : parent + ".") + collection + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

        /// <summary>创建由布局会话持有的临时归属集合。</summary>
        private BudgetedList<TemplateLayoutElement> NewLayout()
            => new BudgetedList<TemplateLayoutElement>("Template.Layout", owner: storage,
                retainedSize: element => element.StorageBytes);

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                // 清理时不创建键快照，不让预算耗尽掩盖原始错误。
                foreach (var image in images.Values) image.Dispose();
                foreach (var lease in imageLeases.Values) lease.Dispose();
                images.Clear();
                imageLeases.Clear();
                ClearEncodedResources();
            }
            finally { storage.Dispose(); }
        }
    }
}
