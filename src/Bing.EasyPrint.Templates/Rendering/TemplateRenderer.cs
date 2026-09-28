using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
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
/// 将模板与业务数据渲染为 CPCL 页面。
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
    /// <returns>包含所有 CPCL 页面的渲染结果。</returns>
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
    /// <returns>包含 CPCL 页面及可定位布局节点的渲染结果。</returns>
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
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        TemplateGraph.Validate(template, options, false, cancellationToken, captureLayout);
        try
        {
            using (var session = new Session(template, options, resources, cancellationToken, captureLayout))
                return session.Render(new Binding(data, template.Variables));
        }
        catch (OverflowException error) { throw new ArgumentException("模板尺寸或资源计数溢出。", nameof(template), error); }
    }

    /// <summary>
    /// 保存可直接绘制的单个布局元素。
    /// </summary>
    private sealed class Atom
    {
        /// <summary>
        /// 保存布局元素相对当前块的起点与尺寸，单位为打印点。
        /// </summary>
        internal int X, Y, Width, Height;
        /// <summary>
        /// 保存模板路径、可选显示文字及图片资源键。
        /// </summary>
        internal string SourcePath, Text, ResourceKey;
        /// <summary>
        /// 保存元素的模板类型编号。
        /// </summary>
        internal int Type;
        /// <summary>
        /// 保存各层重复数据的项序号。
        /// </summary>
        internal IReadOnlyList<int> InstanceIndices;
        /// <summary>
        /// 保存将元素写入 CPCL 页面的绘制操作。
        /// </summary>
        internal Action<CPCLPrintCommand, int, int> Draw;
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
        /// 保存对应模板路径与布局节点类别。
        /// </summary>
        internal string SourcePath, Kind;
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
        /// <summary>
        /// 保存当前布局块占用的打印点高度。
        /// </summary>
        internal int Height;
        /// <summary>
        /// 保存当前布局块中的可绘制元素。
        /// </summary>
        internal readonly List<Atom> Atoms = new List<Atom>();
        /// <summary>
        /// 保存当前布局块中的行、单元格和组合边界。
        /// </summary>
        internal readonly List<Frame> Frames = new List<Frame>();
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
            Height = Math.Max(Height, checked(y + child.Height));
        }
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
        private readonly Dictionary<string, byte[]> resourceBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        /// <summary>
        /// 缓存按资源键及尺寸识别的已解码图片，由会话释放。
        /// </summary>
        private readonly Dictionary<(string, int, int), Image> images = new Dictionary<(string, int, int), Image>();
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
        private List<TemplateLayoutElement> pageLayout;

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
            this.template = template; this.options = options; this.resources = resources; this.cancellation = cancellation;
            this.captureLayout = captureLayout;
            encoding = options.Encoding ?? Encoding.GetEncoding("gbk");
            pageHeight = template.Height == 0 ? options.MaxPageHeight : template.Height;
        }

        /// <summary>
        /// 展开模板行并生成分页结果。
        /// </summary>
        /// <param name="binding">根数据及变量绑定上下文。</param>
        /// <returns>包含所有打印页的渲染结果。</returns>
        internal TemplateRenderResult Render(Binding binding)
        {
            var header = new Block();
            var documentHeader = new Block();
            var footer = new Block();
            var body = new List<(BpRow Row, string Path)>();
            var documentFooter = new List<(BpRow Row, string Path)>();
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
            var headerImages = new HashSet<(string, int, int)>(images.Keys);
            var pages = new List<TemplatePage>();
            pageLayout = captureLayout ? new List<TemplateLayoutElement>() : null;
            var command = NewPage(true);
            var used = checked(header.Height + documentHeader.Height);
            long outputBytes = 0;
            var dirty = false;
            Block activeTableHeader = null;
            Block pendingTableHeader = null;
            foreach (var item in body)
            {
                if (item.Row.Section == "TableHeader")
                {
                    pendingTableHeader = new Block();
                    foreach (var block in Expand(item.Row, template.Width, binding, item.Path))
                        pendingTableHeader.Append(block, 0, pendingTableHeader.Height);
                    headerImages.UnionWith(images.Keys);
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
                            if (used == header.Height + (activeTableHeader?.Height ?? 0) + (pages.Count == 0 ? documentHeader.Height : 0))
                                throw LayoutError(block.Atoms.FirstOrDefault(atom => atom.Y <= offset && atom.Y + atom.Height > offset)?.SourcePath
                                    ?? item.Path, "单个元素或重叠元素组超过可打印页高。");
                            Finish();
                            continue;
                        }
                        DrawSlice(command, block, offset, end, used);
                        used = checked(used + end - offset);
                        dirty = true;
                        offset = end;
                        if (offset < block.Height) Finish();
                    }
                    // DrawImage 已转换为自有打印数据，正文图片无需在后续明细间保留。
                    foreach (var key in images.Keys.Where(key => !headerImages.Contains(key)).ToArray())
                    { images[key].Dispose(); images.Remove(key); }
                    resourceBytes.Clear(); cachedEncodedBytes = 0;
                    imagePixels = images.Values.Sum(image => (long)image.Width * image.Height);
                }
                activeTableHeader = null;
                pendingTableHeader = null;
            }
            if (dirty || pages.Count == 0) Finish(false);
            return new TemplateRenderResult(pages);

            ArgumentException LayoutError(string path, string message)
                => captureLayout ? new TemplateRenderException(path, message, new ArgumentException(message)) : new ArgumentException(message);

            CPCLPrintCommand NewPage(bool first)
            {
                var result = new CPCLPrintCommand(encoding).SetPage(template.Width, pageHeight);
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
                command.InitCommand(0, template.Width, height, 1);
                DrawBorder(command, new Frame { X = 0, Y = 0, Width = template.Width, Height = height, Border = template.Border }, 0, height, 0);
                var bytes = command.Build().GetBytes();
                outputBytes = checked(outputBytes + bytes.Length);
                if (outputBytes > options.MaxOutputBytes) throw new ArgumentException("输出超过字节上限。");
                pages.Add(new TemplatePage(template.Width, height, bytes, captureLayout ? pageLayout : null));
                dirty = false;
                if (startNext)
                {
                    pageLayout = captureLayout ? new List<TemplateLayoutElement>() : null;
                    command = NewPage(false);
                    used = header.Height;
                    if (activeTableHeader != null)
                    {
                        DrawSlice(command, activeTableHeader, 0, activeTableHeader.Height, used);
                        used = checked(used + activeTableHeader.Height);
                        dirty = true;
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
                    if (captureLayout) throw new TemplateRenderException(sourcePath, error.Message, error);
                    throw;
                }
                if (matches) { CountRow(); yield return Layout(row, width, binding, sourcePath); }
                yield break;
            }
            object source;
            try { source = binding.Resolve(row.RepeatSource); }
            catch (ArgumentException error)
            {
                if (captureLayout) throw new TemplateRenderException(sourcePath, error.Message, error);
                throw;
            }
            if (source == null) yield break;
            if (!(source is IEnumerable sequence) || source is string || source is IDictionary)
                throw new ArgumentException("RepeatSource 必须为集合。");
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
                    if (captureLayout) throw new TemplateRenderException(sourcePath, error.Message, error);
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
            var result = new Block { Height = row.Height };
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
                var content = new Block();
                var childRows = cell.Rows ?? new List<BpRow>();
                for (var childIndex = 0; childIndex < childRows.Count; childIndex++)
                    foreach (var block in Expand(childRows[childIndex], cellWidth, binding, ChildPath(cellPath, "Rows", childIndex))) content.Append(block, 0, content.Height);
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
                        if (captureLayout) throw new TemplateRenderException(elementPath, error.Message, error);
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
            var ey = checked((start?.Y ?? content.Height) + margin.Top);
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
            content.Height = checked(content.Height + margin.Bottom);
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
            if (element is BpRectangleElement rectangle) return Shape(rectangle.Width, rectangle.Height, rectangle.LineWidth, false, width, sourcePath, binding);
            if (element is BpEllipseElement ellipse) return Shape(ellipse.Width, ellipse.Height, ellipse.LineWidth, true, width, sourcePath, binding);
            if (element is BpIconElement icon) return Icon(icon, width, sourcePath, binding);
            var text = binding.Text(element.Field);
            payloadBytes = checked(payloadBytes + encoding.GetByteCount(text));
            if (payloadBytes > options.MaxOutputBytes) throw new ArgumentException("字段数据超过输出预算。");
            if (element is BpFontElement font) return Text(font, text, width, sourcePath, binding);
            if (text.IndexOfAny(new[] { '\r', '\n', '\0', '\u001b' }) >= 0) throw new ArgumentException("图片键或条码内容不能包含协议控制字符。");
            if (element is BpMatrixCodeElement matrix) return MatrixCode(matrix, text, width, sourcePath, binding);
            var result = new Block();
            if (element is BpImageElement picture)
            {
                var image = LoadImage(text, picture.Width, picture.Height);
                if (image.Width > width) throw new ArgumentException("图片宽度超出单元格。");
                Reserve(checked(((image.Width + 7L) / 8) * image.Height * (picture.IsCompress ? 1 : 2)));
                var imageOptions = new ImageProcessingOptions { MaxEncodedBytes = options.MaxImageBytes, MaxPixels = options.MaxImagePixels };
                result.Height = image.Height;
                result.Atoms.Add(new Atom { Width = image.Width, Height = image.Height, SourcePath = sourcePath, Type = element.Type, ResourceKey = text, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) => {
                    if (picture.IsCompress) cmd.DrawCompressedImage(x, y, image, imageOptions);
                    else cmd.DrawImage(x, y, image, imageOptions);
                } });
            }
            else if (element is BpQrCodeElement qr)
            {
                if (qr.UnitWidth < 1 || qr.UnitWidth > 32 || !new[] { "L", "M", "Q", "H" }.Contains(qr.ErrorLevel)) throw new ArgumentException("二维码参数无效。");
                var size = QrMetrics.Extent(text, encoding, qr.ErrorLevel, qr.UnitWidth);
                var w = qr.Width == 0 ? size : qr.Width;
                var h = qr.Height == 0 ? size : qr.Height;
                if (w < size || h < size || w > width) throw new ArgumentException("二维码预留区域不足或超过单元格。");
                result.Height = h;
                result.Atoms.Add(new Atom { Width = w, Height = h, SourcePath = sourcePath, Type = element.Type, Text = text, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) => cmd.DrawQrCode(x + 4 * qr.UnitWidth, y + 4 * qr.UnitWidth, text, qr.UnitWidth, qr.ErrorLevel, 0) });
            }
            else if (element is BpBarcodeElement barcode)
            {
                var layout = BarcodeMetrics.Measure(barcode, text);
                var w = barcode.Width == 0 ? layout.Width : barcode.Width;
                if (w < layout.Width || w > width) throw new ArgumentException("条码预留区域不足或超过单元格。");
                result.Height = barcode.Height;
                result.Atoms.Add(new Atom { Width = w, Height = barcode.Height, SourcePath = sourcePath, Type = element.Type, Text = text, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) => cmd.DrawBarcode1D(barcode.BarcodeType, checked(x + layout.Left), y, layout.Data, barcode.LineWidth, barcode.Height, barcode.Ratio, RotationAngle.None) });
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
            BitMatrix bits;
            try
            {
                bits = new MultiFormatWriter().encode(text, format, element.Width, element.Height,
                    new Dictionary<EncodeHintType, object> { [EncodeHintType.CHARACTER_SET] = "UTF-8", [EncodeHintType.MARGIN] = 2 });
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            { throw new ArgumentException("二维点阵码内容无法编码：" + error.Message, error); }
            if (bits.Width > element.Width || bits.Height > element.Height || (long)bits.Width * bits.Height > options.MaxImagePixels)
                throw new ArgumentException($"二维点阵码实际尺寸 {bits.Width}×{bits.Height} 超过预留尺寸 {element.Width}×{element.Height} 或像素限制。");
            Reserve(checked(((bits.Width + 7L) / 8) * bits.Height * 2));
            imagePixels = checked(imagePixels + (long)bits.Width * bits.Height);
            if (imagePixels > (long)options.MaxOutputBytes * 8) throw new ArgumentException("二维点阵码缓存超过渲染预算。");
            var image = new Image<Rgba32>(bits.Width, bits.Height, new Rgba32(255, 255, 255));
            try
            {
                for (var y = 0; y < bits.Height; y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (var x = 0; x < bits.Width; x++) if (bits[x, y]) image[x, y] = new Rgba32(0, 0, 0);
                }
                images.Add(("\0matrix" + images.Count, bits.Width, bits.Height), image);
            }
            catch { image.Dispose(); throw; }
            var imageOptions = new ImageProcessingOptions { MaxEncodedBytes = options.MaxImageBytes, MaxPixels = options.MaxImagePixels };
            var result = new Block { Height = bits.Height };
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
            var groupWidth = group.Width > 0 ? group.Width : definition?.Width ?? width;
            var groupHeight = group.Height > 0 ? group.Height : definition?.Height ?? 0;
            if (groupWidth > width) throw new ArgumentException("组合宽度超出单元格。");
            var result = new Block();
            var children = definition?.Elements ?? group.Elements;
            if (children != null) for (var index = 0; index < children.Count; index++)
            {
                var childPath = ChildPath(sourcePath, "Elements", index);
                try { Place(result, children[index], groupWidth, binding, childPath); }
                catch (ArgumentException error) { throw new TemplateRenderException(childPath, error.Message, error); }
            }
            if (groupHeight > 0 && result.Height > groupHeight) throw new ArgumentException("组合内容超出设定高度。");
            result.Height = Math.Max(result.Height, groupHeight);
            if (result.Height <= 0) throw new ArgumentException("组合高度必须大于零。");
            // 无绘制操作的原子边界使整组只在同一页出现。
            result.Atoms.Add(new Atom { Width = groupWidth, Height = result.Height, SourcePath = sourcePath,
                Type = group.Type, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) => { } });
            return result;
        }

        /// <summary>
        /// 生成矩形或椭圆轮廓的布局块。
        /// </summary>
        /// <param name="shapeWidth">图形打印点宽度。</param>
        /// <param name="shapeHeight">图形打印点高度。</param>
        /// <param name="stroke">轮廓线打印点宽度。</param>
        /// <param name="ellipse">为 true 时绘制椭圆，为 false 时绘制矩形。</param>
        /// <param name="available">可用打印点宽度。</param>
        /// <param name="sourcePath">模板元素的节点路径。</param>
        /// <param name="binding">当前重复数据的绑定上下文。</param>
        /// <returns>包含轮廓绘制操作的布局块。</returns>
        private Block Shape(int shapeWidth, int shapeHeight, int stroke, bool ellipse, int available, string sourcePath, Binding binding)
        {
            if (shapeWidth > available) throw new ArgumentException("图形宽度超出单元格。");
            Reserve(ellipse ? 48 * 32 : 4 * 32);
            var result = new Block { Height = shapeHeight };
            result.Atoms.Add(new Atom { Width = shapeWidth, Height = shapeHeight, SourcePath = sourcePath,
                Type = ellipse ? 8 : 7, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, y) =>
                {
                    if (!ellipse)
                    {
                        cmd.Line(x, y, x + shapeWidth - 1, y, stroke);
                        cmd.Line(x + shapeWidth - 1, y, x + shapeWidth - 1, y + shapeHeight - 1, stroke);
                        cmd.Line(x + shapeWidth - 1, y + shapeHeight - 1, x, y + shapeHeight - 1, stroke);
                        cmd.Line(x, y + shapeHeight - 1, x, y, stroke);
                        return;
                    }
                    var segments = 48;
                    for (var i = 0; i < segments; i++)
                    {
                        var a = 2 * Math.PI * i / segments;
                        var b = 2 * Math.PI * (i + 1) / segments;
                        cmd.Line(x + (int)Math.Round((1 + Math.Cos(a)) * (shapeWidth - 1) / 2),
                            y + (int)Math.Round((1 + Math.Sin(a)) * (shapeHeight - 1) / 2),
                            x + (int)Math.Round((1 + Math.Cos(b)) * (shapeWidth - 1) / 2),
                            y + (int)Math.Round((1 + Math.Sin(b)) * (shapeHeight - 1) / 2), stroke);
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
            var result = new Block { Height = size };
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
        private static Block Line(BpLineElement line, int width, BpMargin margin, string sourcePath, Binding binding)
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
            var result = new Block { Height = checked(maxY + margin.Top + line.LineWidth + margin.Bottom) };
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
            var size = font.FontSize == 0 ? 24 : font.FontSize;
            var metrics = new CpclFontMetrics(size, font.FontName, encoding);
            if ((font.FontStyle & ~5) != 0) throw new ArgumentException("模板文字只支持普通、粗体、下划线。");
            if (font.Field?.Fit == true)
            {
                foreach (var candidate in Enum.GetValues(typeof(FontSize)).Cast<int>().Where(n => n <= size && (font.FontName == 0 || (n % 24 == 0 ? 24 : 55) == font.FontName)).OrderByDescending(n => n))
                {
                    var candidateMetrics = new CpclFontMetrics(candidate, font.FontName, encoding);
                    if (candidateMetrics.Measure(text) <= width && text.IndexOfAny(new[] { '\r', '\n' }) < 0)
                    { size = candidate; metrics = candidateMetrics; break; }
                }
            }
            var lines = Wrap(text, metrics, width, font.Field?.NeedFeedLine ?? true);
            var result = new Block();
            foreach (var line in lines)
            {
                Reserve(32); // 每条文字指令的最小结构成本，限制空字段构造的布局规模。
                var content = line;
                var fontSize = size;
                var lineWidth = checked((int)metrics.Measure(line));
                var alignmentOffset = font.TextAlignment == BpTextAlignment.Right
                    ? width - lineWidth
                    : font.TextAlignment == BpTextAlignment.Center ? (width - lineWidth) / 2 : 0;
                var y = result.Height;
                result.Atoms.Add(new Atom { X = alignmentOffset, Y = y, Width = lineWidth, Height = size, SourcePath = sourcePath, Type = font.Type, Text = content, InstanceIndices = binding.InstanceIndices, Draw = (cmd, x, py) => cmd.DrawText(x, py, content, fontSize, 0, font.Bold || (font.FontStyle & 1) != 0, false, (font.FontStyle & 4) != 0) });
                result.Height = checked(result.Height + size + 2);
            }
            if (result.Height > 0) result.Height -= 2;
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
        private List<string> Wrap(string value, CpclFontMetrics metrics, int width, bool wrap)
        {
            var result = new List<string>();
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
                if (used + advance > width)
                {
                    if (!wrap) throw new ArgumentException("文字超出宽度且未启用换行。");
                    result.Add(line.ToString()); line.Clear(); used = 0;
                }
                line.Append(character); used += advance;
            }
            result.Add(line.ToString());
            return result;
        }

        /// <summary>
        /// 从资源提供器加载并缓存指定尺寸的图片。
        /// </summary>
        /// <param name="key">图片资源键。</param>
        /// <param name="width">要求的打印点宽度；为零时由原图或指定高度按比例计算。</param>
        /// <param name="height">要求的打印点高度；为零时按目标宽度等比例计算。</param>
        /// <returns>由当前会话管理生命周期的图片。</returns>
        private Image LoadImage(string key, int width, int height)
        {
            if (width < 0 || height < 0) throw new ArgumentException("图片尺寸不能为负数。");
            var cacheKey = (key, width, height);
            if (images.TryGetValue(cacheKey, out var cached)) return cached;
            if (resources == null) throw new ArgumentException("图片元素需要资源提供器。");
            if (!resourceBytes.TryGetValue(key, out var bytes))
            {
                using (var source = resources.OpenRead(key, cancellation) ?? throw new ArgumentException("图片资源不存在：" + key))
                using (var target = new MemoryStream())
                {
                    var buffer = new byte[8192];
                    int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (target.Length + read > options.MaxImageBytes) throw new ArgumentException("图片编码数据超过限制。");
                        target.Write(buffer, 0, read);
                    }
                    bytes = target.ToArray();
                }
                cachedEncodedBytes = checked(cachedEncodedBytes + bytes.Length);
                if (cachedEncodedBytes > options.MaxOutputBytes) throw new ArgumentException("图片编码缓存超过渲染预算。");
                resourceBytes.Add(key, bytes);
            }
            using (var stream = new MemoryStream(bytes, false))
            {
                var info = Image.Identify(stream) ?? throw new ArgumentException("图片格式无效。");
                if ((long)info.Width * info.Height > options.MaxImagePixels) throw new ArgumentException("图片像素数超过限制。");
                var targetWidth = width == 0 ? (height == 0 ? info.Width : checked((int)Math.Max(1, (long)info.Width * height / info.Height))) : width;
                var targetHeight = height == 0 ? checked((int)Math.Max(1, (long)info.Height * targetWidth / info.Width)) : height;
                var pixels = (long)targetWidth * targetHeight;
                if (pixels > options.MaxImagePixels) throw new ArgumentException("缩放后的图片像素数超过限制。");
                // 缓存总量同样受输出预算约束，避免许多资源绕过单图限制。
                imagePixels = checked(imagePixels + Math.Max((long)info.Width * info.Height, pixels));
                if (imagePixels > (long)options.MaxOutputBytes * 8) throw new ArgumentException("图片缓存超过本次渲染预算。");
                stream.Position = 0;
                var format = Image.DetectFormat(stream);
                if (format is WebpFormat && AnimatedWebp(bytes)) throw new ArgumentException("不支持多帧 WebP 图片。");
                stream.Position = 0;
                var image = format is GifFormat ? Image.Load(stream, new GifDecoder { MaxFrames = 1, DecodingMode = FrameDecodingMode.First, IgnoreMetadata = true }) :
                    format is TiffFormat ? Image.Load(stream, new TiffDecoder { DecodingMode = FrameDecodingMode.First, IgnoreMetadata = true }) : Image.Load(stream);
                try
                {
                    while (image.Frames.Count > 1) image.Frames.RemoveFrame(image.Frames.Count - 1);
                    if (image.Width != targetWidth || image.Height != targetHeight) image.Mutate(ctx => ctx.Resize(targetWidth, targetHeight));
                    images.Add(cacheKey, image);
                    return image;
                }
                catch { image.Dispose(); throw; }
            }
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
        private void DrawSlice(CPCLPrintCommand command, Block block, int start, int end, int destination)
        {
            foreach (var atom in block.Atoms)
            {
                cancellation.ThrowIfCancellationRequested();
                if (atom.Y >= start && atom.Y < end)
                {
                    var y = checked(atom.Y - start + destination);
                    if (captureLayout) pageLayout.Add(new TemplateLayoutElement(atom.SourcePath, "element", atom.Type,
                        atom.InstanceIndices, atom.X, y, atom.Width, atom.Height, atom.Text, atom.ResourceKey));
                    atom.Draw(command, atom.X, y);
                }
            }
            foreach (var frame in block.Frames)
            {
                DrawBorder(command, frame, start, end, destination);
                if (captureLayout && frame.Y < end && checked(frame.Y + frame.Height) > start)
                {
                    var top = Math.Max(frame.Y, start);
                    var bottom = Math.Min(checked(frame.Y + frame.Height), end);
                    pageLayout.Add(new TemplateLayoutElement(frame.SourcePath, frame.Kind, 0, frame.InstanceIndices,
                        frame.X, checked(top - start + destination), frame.Width, bottom - top, null, null));
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
        private void DrawBorder(CPCLPrintCommand command, Frame frame, int start, int end, int destination)
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
            => captureLayout
                ? (parent == null ? string.Empty : parent + ".") + collection + "[" + index.ToString(CultureInfo.InvariantCulture) + "]"
                : null;

        /// <inheritdoc />
        public void Dispose() { foreach (var image in images.Values) image.Dispose(); }
    }
}
