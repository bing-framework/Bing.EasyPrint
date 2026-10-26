using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bing.EasyPrint;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

internal static partial class DesignerRequestProcessor
{
    internal static DesignerProtocolRegistry ProtocolRegistry { get; set; } = DesignerProtocolRegistry.CreateDefault();
    internal const long MaxRequestBytes = 44L * 1024 * 1024;
    private const int MaxDataBytes = 1024 * 1024;
    private const int MaxResourceCount = 64;
    private const long MaxTotalResourceBytes = 32L * 1024 * 1024;
    private const int MaxRasterFontCount = RasterFontLimits.MaxResourceCount;
    private const long MaxTotalRasterFontBytes = RasterFontLimits.MaxTotalDataBytes;

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    internal static IResult Capabilities(DesignerRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.ProtocolId != null && string.IsNullOrWhiteSpace(request.ProtocolId))
                throw new DesignerOptionException("InvalidProtocolSelection", "protocolId 不能为空白。", "protocolId");
            var options = PrepareOptions(request, cancellationToken);
            if (request.ProtocolId != null)
                return CustomCapabilities(options, cancellationToken);
            var capabilities = TemplateProtocolCapabilities.Create(options);
            var nativeText = capabilities.NativeTextProfile;
            var rasterFonts = capabilities.RasterFonts;
            return new BudgetedJsonResult(new
            {
                contract = DesignerContract.Description,
                protocol = GetProtocolName(capabilities.Protocol),
                escPosLayoutMode = capabilities.EscPosLayoutMode?.ToString(),
                nativeFlow = capabilities.NativeFlow?.ToString(),
                labelProfile = GetLabelProfileCapabilities(capabilities, options),
                nativeText = nativeText is null ? null : new
                {
                    encodingCodePage = nativeText.EncodingCodePage,
                    codePageIndex = nativeText.CodePageIndex,
                    characterMode = nativeText.CharacterMode.ToString(),
                    characterRanges = nativeText.CharacterRanges.Select(range => new
                    {
                        first = range.First,
                        last = range.Last
                    }),
                    halfCellWidthDots = nativeText.HalfCellWidthDots,
                    fullCellWidthDots = nativeText.FullCellWidthDots,
                    lineHeightDots = nativeText.LineHeightDots
                },
                rasterFonts = rasterFonts is null ? null : new
                {
                    defaultFont = rasterFonts.DefaultFont,
                    fallbackFonts = rasterFonts.FallbackFonts,
                    fonts = rasterFonts.Fonts.Select(font => new
                    {
                        name = font.Name,
                        version = font.Version,
                        hasBold = font.HasBold
                    })
                },
                rasterBarcodeHumanReadableFontSize = capabilities.RasterBarcodeHumanReadableFontSize,
                supportedNativeBarcodeHumanReadableTypes = capabilities.SupportedNativeBarcodeHumanReadableTypes?.Select(type => type.ToString()),
                supportedNativeBarcodeHumanReadablePositions = capabilities.SupportedNativeBarcodeHumanReadablePositions?.Select(position => position.ToString()),
                jobFeedUnits = capabilities.JobFeedUnits.Select(unit => unit.ToString()),
                cutModes = capabilities.CutModes.Select(mode => mode.ToString()),
                cutFeedUnits = capabilities.CutFeedUnits.Select(unit => unit.ToString()),
                cutFeedMotionUnits = capabilities.CutFeedMotionUnits,
                cutFeedDots = capabilities.CutFeedDots,
                tsplCutter = GetTsplCutterCapabilities(capabilities.TsplCutter),
                zplCutter = GetZplCutterCapabilities(capabilities.ZplCutter),
                supportsIndependentOutputSegments = capabilities.SupportsIndependentOutputSegments,
                capabilities.Dpi,
                capabilities.SupportsCoordinates,
                capabilities.SupportsExplicitQuietZone,
                capabilities.ElementTypes,
                capabilities.SupportsGs1Composite,
                expandedStackedColumns = options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Raster
                    && capabilities.BarcodeTypes.Contains(BarcodeType.Gs1DataBarExpandedStacked)
                    ? new { minimum = 1, maximum = 11, @default = 2 } : null,
                expandedStackedRows = options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Raster
                    && capabilities.BarcodeTypes.Contains(BarcodeType.Gs1DataBarExpandedStacked)
                    ? new { minimum = 2, maximum = 11, @default = (int?)null } : null,
                gs1Composite = capabilities.SupportsGs1Composite ? new
                {
                    elementType = 11,
                    linearTypes = new[] { "Gs1DataBarOmnidirectional", "Gs1DataBarLimited", "UccEan128", "Gs1DataBarExpanded", "Gs1DataBarStacked", "Gs1DataBarExpandedStacked", "Ean8", "Ean13", "UpcA", "UpcE", "Gs1DataBarTruncated", "Gs1DataBarStackedOmnidirectional" },
                    modes = new[] { "Auto", "CcA", "CcB", "CcC" },
                    rotations = new[] { 0, 90, 180, 270 },
                    primaryFormats = new[] { "GTIN-13-data", "GTIN-14", "GS1-AI-01", "bracketed-gs1-ai", "ean-upc-digits", "ean-upc-digits-with-addon" },
                    secondaryFormat = "bracketed-gs1-ai",
                    includesHumanReadable = false,
                    dimensionsFromEncoding = true,
                    expandedStackedColumns = new { minimum = 2, maximum = 11, @default = 2 },
                    expandedStackedRows = new { minimum = 2, maximum = 11, @default = (int?)null }
                } : null,
                capabilities.FontKinds,
                capabilities.DeviceFonts,
                capabilities.TextSizes,
                capabilities.TextSize,
                capabilities.TextRotations,
                capabilities.BarcodeTypes,
                capabilities.BarcodeRotations,
                capabilities.QrCodeRotations,
                capabilities.ImageRotations,
                capabilities.QrModuleSize,
                capabilities.BarcodeModuleWidth,
                capabilities.BarcodeHeight,
                capabilities.SupportsExplicitBarcodeModuleWidth,
                capabilities.SupportsWideModuleWidth,
                capabilities.SupportsBoldText,
                capabilities.SupportsUnderlineText,
                capabilities.SupportsRotatedUnderlineText,
                capabilities.SupportsHostFontFamily,
                capabilities.SupportsLegacyDeviceFont,
                capabilities.SupportsBarcodeHumanReadable,
                capabilities.SupportsCut,
                capabilities.SupportsExtraFeed,
                capabilities.SupportsFilledRectangles,
                rasterPreview = GetRasterPreviewCapabilities(capabilities, options),
                gs1TwoDimensional = new
                {
                    qr = true,
                    dataMatrix = true,
                    native = false,
                    associationValidation = true
                }
            }, WebJson);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    internal static IResult CustomCapabilities(TemplateRenderOptions options, CancellationToken cancellationToken)
    {
        var adapter = options.CustomProtocol ?? throw new DesignerOptionException(
            "InvalidProtocolConfiguration", "新协议模板适配器不可用。", "protocolId");
        var capabilities = adapter.Capabilities;
        return new BudgetedJsonResult(new
        {
            contract = DesignerContract.Description,
            protocolId = options.ProtocolId,
            escPosLayoutMode = (string?)null,
            nativeFlow = (string?)null,
            labelProfile = (object?)null,
            nativeText = (object?)null,
            rasterFonts = (object?)null,
            rasterBarcodeHumanReadableFontSize = (int?)null,
            supportedNativeBarcodeHumanReadableTypes = (object?)null,
            supportedNativeBarcodeHumanReadablePositions = (object?)null,
            jobFeedUnits = Array.Empty<string>(),
            cutModes = Array.Empty<string>(),
            cutFeedUnits = Array.Empty<string>(),
            cutFeedMotionUnits = (object?)null,
            cutFeedDots = (object?)null,
            tsplCutter = (object?)null,
            zplCutter = (object?)null,
            supportsIndependentOutputSegments = false,
            dpi = capabilities.Dpi,
            supportsCoordinates = capabilities.SupportsCoordinates,
            supportsExplicitQuietZone = capabilities.SupportsExplicitQuietZone,
            elementTypes = capabilities.ElementTypes,
            supportsGs1Composite = capabilities.SupportsComposite,
            fontKinds = capabilities.FontKinds,
            deviceFonts = Array.Empty<string>(),
            textSizes = capabilities.TextSizes,
            textSize = new { minimum = 1, maximum = capabilities.MaxTextSize },
            textRotations = capabilities.TextRotations.Select(value => (int)value),
            barcodeTypes = capabilities.BarcodeTypes.Select(value => (int)value),
            barcodeRotations = capabilities.BarcodeRotations.Select(value => (int)value),
            qrCodeRotations = capabilities.QrCodeRotations.Select(value => (int)value),
            imageRotations = capabilities.ImageRotations.Select(value => (int)value),
            qrModuleSize = (object?)null,
            barcodeModuleWidth = (object?)null,
            barcodeHeight = (object?)null,
            supportsExplicitBarcodeModuleWidth = capabilities.SupportsExplicitBarcodeModuleWidth,
            supportsWideModuleWidth = capabilities.SupportsWideModuleWidth,
            supportsBoldText = capabilities.SupportsBoldText,
            supportsUnderlineText = capabilities.SupportsUnderlineText,
            supportsReverseText = capabilities.SupportsReverseText,
            supportsRotatedUnderlineText = false,
            supportsHostFontFamily = capabilities.SupportsHostFontFamily,
            supportsLegacyDeviceFont = capabilities.SupportsLegacyDeviceFont,
            supportsBarcodeHumanReadable = capabilities.SupportsBarcodeHumanReadable,
            supportsCut = false,
            supportsExtraFeed = capabilities.SupportsExtraFeed,
            supportsFinishing = capabilities.SupportsFinishing,
            supportsFilledRectangles = capabilities.SupportsFilledRectangles,
            rasterPreview = new
            {
                softwareBitmap = capabilities.SupportsSoftwareBitmap,
                elementPng = capabilities.SupportsSoftwareBitmap,
                elementTypes = capabilities.SoftwareBitmapElementTypes,
                pagePng = false,
                pagePngReason = capabilities.SupportsSoftwareBitmap
                    ? "新协议仅提供当前页元素软件码图。"
                    : capabilities.SoftwareBitmapReason ?? "新协议未声明软件位图能力。",
                dpiX = capabilities.Dpi,
                dpiY = capabilities.Dpi
            },
            gs1TwoDimensional = capabilities.SupportsGs1QrCode
                ? new { qr = true, dataMatrix = false, native = false, associationValidation = true }
                : null
        }, WebJson);
    }

    private static RenderResultView View(TemplateRenderResult result)
        => new()
        {
            ProtocolId = result.ProtocolId,
            Protocol = result.Protocol,
            Pages = result.Pages.Select(page => new RenderPageView
            {
                Width = page.Width,
                Height = page.Height,
                Output = page.Output,
                Elements = page.Elements,
                RenderElements = page.RenderElements,
                RenderElementOwnership = page.RenderElementOwnership,
                SourcePageIndex = page.SourcePageIndex,
                CopyIndex = page.CopyIndex,
                FeedAfterPrint = page.FeedAfterPrint,
                CutAfterPrint = page.CutAfterPrint,
            }).ToArray(),
            FinishingFiles = result.CustomFinishingFiles,
        };

    private static RenderResultView View(TemplateCustomRenderResult result)
        => new()
        {
            ProtocolId = result.ProtocolId,
            Pages = result.Pages.Select(page => new RenderPageView
            {
                Width = page.Width,
                Height = page.Height,
                Output = page.Output,
                Elements = page.Elements,
                RenderElements = page.RenderElements,
                RenderElementOwnership = page.RenderElementOwnership,
                SourcePageIndex = page.SourcePageIndex,
                CopyIndex = page.CopyIndex,
                FeedAfterPrint = page.FeedAfterPrint,
                CutAfterPrint = page.CutAfterPrint,
            }).ToArray(),
            FinishingFiles = result.FinishingFiles,
        };

    internal static IResult Preview(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Prepare(request, cancellationToken);
            var renderer = new TemplateRenderer();
            var view = input.Options.CustomProtocol != null
                ? View(renderer.RenderCustomProtocolWithLayout(input.Template, input.Data, input.Resources,
                    input.Options, cancellationToken))
                : View(renderer.RenderWithLayout(input.Template, input.Data, input.Resources,
                    input.Options, cancellationToken));
            return new BudgetedJsonResult(new { protocolId = view.ProtocolId, pages = CreatePreviewPages(view) }, WebJson);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    /// <summary>渲染一次模板并返回当前页布局清单、元素 PNG 与逐元素诊断 ZIP。</summary>
    internal static IResult PreviewBundle(DesignerRequest request, CancellationToken cancellationToken)
        => PreviewBundleCore(request, cancellationToken, null);

    /// <summary>供测试注入渲染委托的预览包实现；生产入口传入 null 使用模板库默认渲染器。</summary>
    internal static IResult PreviewBundleCore(DesignerRequest request, CancellationToken cancellationToken,
        Func<Bing.EasyPrint.Model.BpTemplate, object?, ITemplateResourceProvider, TemplateRenderOptions,
            TemplateRenderResult>? render, Func<int, Exception?>? elementEncodingFailure = null)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Prepare(request, cancellationToken);
            TemplateRenderResult? builtinResult = null;
            TemplateCustomRenderResult? customResult = null;
            RenderResultView view;
            if (input.Options.CustomProtocol != null)
            {
                customResult = new TemplateRenderer().RenderCustomProtocolWithLayoutAndPrimitives(
                    input.Template, input.Data, input.Resources, input.Options, cancellationToken);
                view = View(customResult);
            }
            else
            {
                builtinResult = render is null
                    ? RenderPreviewBundle(input.Template, input.Data, input.Resources, input.Options,
                        cancellationToken)
                    : render(input.Template, input.Data, input.Resources, input.Options) ??
                      throw new InvalidOperationException("预览包渲染委托未返回结果。");
                view = View(builtinResult);
            }
            try
            {
                return BuildPreviewBundle(request, input, view, cancellationToken, elementEncodingFailure);
            }
            finally
            {
                builtinResult?.ReleaseRenderElements();
                customResult?.ReleaseRenderElements();
            }
        }        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    /// <summary>执行一次带布局与 primitive 归属的模板渲染，供预览包和测试注入共用。</summary>
    internal static TemplateRenderResult RenderPreviewBundle(Bing.EasyPrint.Model.BpTemplate template,
        object? data, ITemplateResourceProvider resources, TemplateRenderOptions options,
        CancellationToken cancellationToken)
        => new TemplateRenderer().RenderWithLayoutAndPrimitives(template, data!, resources, options,
            cancellationToken);

    private static IResult BuildPreviewBundle(DesignerRequest request, PreparedRequest input,
        RenderResultView result, CancellationToken cancellationToken,
        Func<int, Exception?>? elementEncodingFailure)
    {
        var pageIndex = request.PageIndex ?? 0;
        if (pageIndex < 0 || pageIndex >= result.Pages.Count)
            throw new DesignerOptionException("InvalidPageIndex", "pageIndex 超出预览页范围。", "pageIndex");
        var imageTypes = ParseBundleImageTypes(request.ImageTypes);
        var metadataBytes = EstimatePreviewBundleMetadataBytes(result, imageTypes, cancellationToken);
        var scratchBytes = EstimatePreviewBundleScratchBytes(result, cancellationToken);
        using var metadataLease = PrintResourceScope.Reserve(
            checked(metadataBytes + Math.Max(4096L, metadataBytes) + scratchBytes),
            "Host.PreviewBundleMetadata");
        var page = result.Pages[pageIndex];
        var customCapabilities = input.Options.CustomProtocol?.Capabilities;
        var capabilities = customCapabilities == null ? TemplateProtocolCapabilities.Create(input.Options) : null;
        var dpiX = customCapabilities?.Dpi ?? RasterDpiX(input.Options, capabilities!);
        var dpiY = customCapabilities?.Dpi ?? RasterDpiY(input.Options, capabilities!);
        var humanReadableFontSize = input.Options.EscPosDevice?.RasterBarcodeHumanReadableFontSize ??
            capabilities?.RasterBarcodeHumanReadableFontSize ?? 12;
        var images = new List<object>();
        var diagnostics = new List<object>();
        using var archiveStream = new BudgetedHostStream("Host.PreviewBundleZipBuffer", cancellationToken);
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
        {
            var imageIndex = 0;
            for (var elementIndex = 0; elementIndex < page.Elements.Count; elementIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var layout = page.Elements[elementIndex];
                if (imageTypes is not null && !imageTypes.Contains(layout.Type)) continue;
                if (!ElementPngSupported(input.Options, layout.Type))
                {
                    diagnostics.Add(CreateBundleDiagnostic(pageIndex, elementIndex, layout,
                        "UnsupportedCapability", $"元素类型 {layout.Type} 在当前协议下没有准确的元素码图；请查看布局示意。"));
                    continue;
                }
                try
                {
                    var primitives = new List<PrintElement>();
                    for (var index = 0; index < page.RenderElements.Count; index++)
                    {
                        if (index < page.RenderElementOwnership.Count &&
                            ReferenceEquals(page.RenderElementOwnership[index], layout))
                            primitives.Add(page.RenderElements[index]);
                    }
                    using var preview = PrintRasterPreview.RenderElements(primitives, layout.X, layout.Y,
                        Math.Max(1, layout.Width), Math.Max(1, layout.Height),
                        input.Options.EscPosDevice?.RasterFonts, humanReadableFontSize, cancellationToken);
                    var injectedFailure = elementEncodingFailure?.Invoke(elementIndex);
                    if (injectedFailure is not null) throw injectedFailure;
                    var png = EncodePng(preview, dpiX, dpiY, cancellationToken);
                    var file = $"images/{++imageIndex:0000}.png";
                    var entry = archive.CreateEntry(file, CompressionLevel.Fastest);
                    using (var target = entry.Open())
                        WriteBytes(target, png, cancellationToken);
                    images.Add(new
                    {
                        key = CreatePreviewElementKey(pageIndex, layout),
                        file,
                        pageIndex,
                        elementIndex,
                        type = layout.Type,
                        sourcePath = layout.SourcePath,
                        width = Math.Max(1, layout.Width),
                        height = Math.Max(1, layout.Height)
                    });
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) when (DesignerError.Find<PrintResourceLimitException>(error) is null)
                {
                    // 单元素软件码图失败只记录诊断；有效布局和其他元素仍保留。
                    var description = DesignerError.Describe(error);
                    diagnostics.Add(CreateBundleDiagnostic(pageIndex, elementIndex, layout,
                        description.Code, description.Message));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            using var manifestBuffer = new BudgetedHostStream("Host.PreviewBundleManifestBuffer",
                cancellationToken);
            System.Text.Json.JsonSerializer.Serialize(manifestBuffer, new
            {
                contract = DesignerContract.Description,
                protocolId = result.ProtocolId,
                layout = new
                {
                    pageIndex,
                    pageCount = result.Pages.Count,
                    pages = CreatePreviewPages(result)
                },
                images,
                diagnostics
            }, WebJson);
            var manifestBytes = manifestBuffer.ToArray("Host.PreviewBundleManifestOutput");
            var manifest = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
            using (var manifestStream = manifest.Open())
                WriteBytes(manifestStream, manifestBytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Results.File(archiveStream.ToArray("Host.PreviewBundleOutput"), "application/zip",
            "easyprint-preview-bundle.zip");
    }

    private static object[] CreatePreviewPages(RenderResultView result)
        => result.Pages.Select(page => (object)new
        {
            width = page.Width,
            height = page.Height,
            sourcePageIndex = page.SourcePageIndex,
            copyIndex = page.CopyIndex,
            feedAfterPrint = page.FeedAfterPrint,
            cutAfterPrint = page.CutAfterPrint,
            elements = page.Elements.Select(element => new
            {
                sourcePath = element.SourcePath,
                definitionPath = element.DefinitionPath,
                componentInstancePath = element.ComponentInstancePath,
                fragmentIndex = element.FragmentIndex,
                kind = element.Kind,
                type = element.Type,
                instanceIndices = element.InstanceIndices,
                x = element.X,
                y = element.Y,
                width = element.Width,
                height = element.Height,
                rotation = element.Rotation,
                text = element.Text,
                displayText = element.Text,
                resourceKey = element.ResourceKey
            }).ToArray()
        }).ToArray();

    private static object CreateBundleDiagnostic(int pageIndex, int elementIndex, TemplateLayoutElement layout,
        string code, string message) => new
    {
        pageIndex,
        elementIndex,
        type = layout.Type,
        sourcePath = layout.SourcePath,
        code,
        phase = "PreviewImage",
        message
    };

    private static string CreatePreviewElementKey(int pageIndex, TemplateLayoutElement element)
        => $"{pageIndex}:{element.SourcePath}:{element.ComponentInstancePath ?? string.Empty}:" +
           $"{string.Join(".", element.InstanceIndices ?? Array.Empty<int>())}:{element.FragmentIndex}:{element.Type}";

    private static long EstimatePreviewBundleMetadataBytes(RenderResultView result, int[]? imageTypes,
        CancellationToken cancellationToken)
    {
        var filter = imageTypes is null ? null : new HashSet<int>(imageTypes);
        long total = 4096;
        foreach (var page in result.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total = checked(total + 256L);
            foreach (var element in page.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                total = checked(total + 192L +
                    MetadataTextBytes(element.SourcePath) +
                    MetadataTextBytes(element.DefinitionPath) +
                    MetadataTextBytes(element.ComponentInstancePath) +
                    MetadataTextBytes(element.Kind) +
                    MetadataTextBytes(element.Text) +
                    MetadataTextBytes(element.ResourceKey) +
                    (element.InstanceIndices?.Count ?? 0) * 4L);
                if (filter is null || filter.Contains(element.Type))
                    total = checked(total + 768L);
            }
        }
        return total;
    }

    private static long EstimatePreviewBundleScratchBytes(RenderResultView result,
        CancellationToken cancellationToken)
    {
        long total = 65536;
        foreach (var page in result.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total = checked(total + 1024L + page.Elements.Count * 1024L);
        }
        return total;
    }

    private static long MetadataTextBytes(string? value)
        => 24L + (value?.Length ?? 0) * 2L;

    private static int[]? ParseBundleImageTypes(int[]? values)
    {
        if (values is null) return null;
        if (values.Length > 12)
            throw new DesignerOptionException("InvalidImageTypes", "imageTypes 数量不能超过 12 个元素类型。",
                "imageTypes");
        var result = new int[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (value is < 0 or > 11)
                throw new DesignerOptionException("InvalidImageTypes", "imageTypes 只能包含 0 到 11 的元素类型。",
                    "imageTypes");
            if (Array.IndexOf(result, value, 0, index) >= 0)
                throw new DesignerOptionException("InvalidImageTypes", "imageTypes 不能包含重复元素类型。",
                    "imageTypes");
            result[index] = value;
        }
        return result;
    }

    internal static IResult Render(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Prepare(request, cancellationToken);
            var renderer = new TemplateRenderer();
            RenderResultView view;
            if (input.Options.CustomProtocol != null)
                view = View(renderer.RenderCustomProtocolWithLayout(input.Template, input.Data,
                    input.Resources, input.Options, cancellationToken));
            else
                view = View(renderer.RenderWithLayout(input.Template, input.Data, input.Resources,
                    input.Options, cancellationToken));

            var extension = view.ProtocolId == null
                ? GetProtocolFileExtension(view.Protocol!.Value)
                : GetCustomProtocolExtension(view.ProtocolId);
            var pages = view.Pages.Select((page, pageIndex) => new
            {
                Page = page,
                PageIndex = pageIndex,
                Segments = page.Segments.Select((segment, segmentIndex) => new
                {
                    Segment = segment,
                    SegmentIndex = segmentIndex,
                    File = GetSegmentFileName(pageIndex, segmentIndex, extension),
                    Kind = segment.Kind.ToString()
                }).ToArray()
            }).ToArray();
            var controls = view.FinishingFiles.SelectMany((control, controlIndex) =>
                control.Output.Segments.Select((segment, segmentIndex) => new
                {
                    Control = control,
                    Segment = segment,
                    File = control.Output.Segments.Count == 1
                        ? $"finish-{controlIndex + 1:000}.{extension}"
                        : $"finish-{controlIndex + 1:000}-{segmentIndex + 1:000}.{extension}",
                })).ToArray();
            var hasIndependentOutputSegments = pages.Any(page => page.Segments.Length > 1) || controls.Length > 0;
            var hasManifest = input.Options.Job != null || hasIndependentOutputSegments || controls.Length > 0;
            using var archiveStream = new BudgetedHostStream("Host.ZipBuffer", cancellationToken);
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
            {
                foreach (var page in pages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var segment in page.Segments)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var entry = archive.CreateEntry(segment.File, CompressionLevel.Fastest);
                        using var target = entry.Open();
                        WriteBytes(target, segment.Segment.Data, cancellationToken);
                    }
                }
                foreach (var control in controls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var controlEntry = archive.CreateEntry(control.File, CompressionLevel.Fastest);
                    using var target = controlEntry.Open();
                    WriteBytes(target, control.Segment.Data, cancellationToken);
                }
                if (hasManifest)
                {
                    var manifest = archive.CreateEntry("job.json", CompressionLevel.Fastest);
                    using var manifestStream = manifest.Open();
                    var orderedFiles = new List<object>();
                    for (var pageIndex = 0; pageIndex < pages.Length; pageIndex++)
                    {
                        var page = pages[pageIndex];
                        foreach (var segment in page.Segments)
                            orderedFiles.Add(new
                            {
                                file = segment.File,
                                pageIndex,
                                segmentIndex = segment.SegmentIndex,
                                kind = segment.Kind,
                                sourcePageIndex = page.Page.SourcePageIndex,
                                copyIndex = page.Page.CopyIndex,
                                placement = (string?)null,
                            });
                        foreach (var control in controls.Where(item => item.Control.PageIndex == pageIndex))
                            orderedFiles.Add(new
                            {
                                file = control.File,
                                pageIndex,
                                segmentIndex = 0,
                                kind = "Control",
                                sourcePageIndex = page.Page.SourcePageIndex,
                                copyIndex = page.Page.CopyIndex,
                                placement = control.Control.Placement.ToString(),
                            });
                    }
                    foreach (var control in controls.Where(item => item.Control.PageIndex == null))
                        orderedFiles.Add(new
                        {
                            file = control.File,
                            pageIndex = (int?)null,
                            segmentIndex = 0,
                            kind = "Control",
                            sourcePageIndex = (int?)null,
                            copyIndex = (int?)null,
                            placement = control.Control.Placement.ToString(),
                        });
                    object manifestModel = new
                    {
                        outputVersion = hasIndependentOutputSegments ? 2 : (int?)null,
                        protocol = view.ProtocolId ?? GetProtocolName(view.Protocol!.Value),
                        preserveFileBoundaries = hasIndependentOutputSegments,
                        files = orderedFiles,
                        pages = pages.Select(page => new
                        {
                            file = page.Segments[0].File,
                            sourcePageIndex = page.Page.SourcePageIndex,
                            copyIndex = page.Page.CopyIndex,
                            feedAfterPrint = page.Page.FeedAfterPrint,
                            cutAfterPrint = page.Page.CutAfterPrint,
                            segments = page.Segments.Select(segment => new
                            {
                                file = segment.File,
                                kind = segment.Kind
                            })
                        }),
                        finishing = controls.Select(control => new
                        {
                            file = control.File,
                            placement = control.Control.Placement.ToString(),
                            pageIndex = control.Control.PageIndex
                        })
                    };                    System.Text.Json.JsonSerializer.Serialize(manifestStream, manifestModel, WebJson);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Results.File(archiveStream.ToArray("Host.ZipOutput"), "application/zip", "easyprint-pages.zip");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }
    internal static IResult ElementPng(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Prepare(request, cancellationToken);
            TemplateRenderResult? builtinResult = null;
            TemplateCustomRenderResult? customResult = null;
            RenderResultView view;
            if (input.Options.CustomProtocol != null)
            {
                customResult = new TemplateRenderer().RenderCustomProtocolWithLayoutAndPrimitives(
                    input.Template, input.Data, input.Resources, input.Options, cancellationToken);
                view = View(customResult);
            }
            else
            {
                builtinResult = new TemplateRenderer().RenderWithLayoutAndPrimitives(input.Template, input.Data,
                    input.Resources, input.Options, cancellationToken);
                view = View(builtinResult);
            }
            try
            {
                var pageIndex = request.PageIndex ?? 0;
                if (pageIndex < 0 || pageIndex >= view.Pages.Count)
                    throw new DesignerOptionException("InvalidPageIndex", "pageIndex 超出预览页范围。", "pageIndex");
                var page = view.Pages[pageIndex];
                var elementIndex = request.ElementIndex ?? -1;
                if (elementIndex < 0 || elementIndex >= page.Elements.Count)
                    throw new DesignerOptionException("InvalidElementIndex", "elementIndex 超出当前页元素范围。",
                        "elementIndex");
                var layout = page.Elements[elementIndex];
                if (!string.IsNullOrEmpty(request.SourcePath) &&
                    !string.Equals(layout.SourcePath, request.SourcePath, StringComparison.Ordinal))
                    throw new DesignerOptionException("ElementSourcePathMismatch",
                        "sourcePath 与 elementIndex 指向的元素不一致。", "sourcePath");
                var capabilities = input.Options.CustomProtocol == null
                    ? TemplateProtocolCapabilities.Create(input.Options) : null;
                if (!ElementPngSupported(input.Options, layout.Type))
                {
                    if (input.Options.CustomProtocol != null)
                        throw new PrintProtocolBackendException(input.Options.ProtocolId!,
                            input.Options.CustomProtocol.Capabilities.SoftwareBitmapReason ??
                            $"元素类型 {layout.Type} 在当前协议下没有准确的元素码图；请查看布局示意。",
                            layout.SourcePath, null, elementIndex, true);
                    throw new NotSupportedException(
                        $"元素类型 {layout.Type} 在当前协议下没有准确的元素码图；请查看布局示意。");
                }
                var primitives = new List<PrintElement>();
                for (var index = 0; index < page.RenderElements.Count; index++)
                {
                    if (index < page.RenderElementOwnership.Count &&
                        ReferenceEquals(page.RenderElementOwnership[index], layout))
                        primitives.Add(page.RenderElements[index]);
                }
                using var preview = PrintRasterPreview.RenderElements(primitives, layout.X, layout.Y,
                    Math.Max(1, layout.Width), Math.Max(1, layout.Height),
                    input.Options.EscPosDevice?.RasterFonts,
                    input.Options.EscPosDevice?.RasterBarcodeHumanReadableFontSize ??
                    capabilities?.RasterBarcodeHumanReadableFontSize ?? 12, cancellationToken);
                var dpiX = input.Options.CustomProtocol?.Capabilities.Dpi ?? RasterDpiX(input.Options, capabilities!);
                var dpiY = input.Options.CustomProtocol?.Capabilities.Dpi ?? RasterDpiY(input.Options, capabilities!);
                var png = EncodePng(preview, dpiX, dpiY, cancellationToken);
                return Results.File(png, "image/png", $"easyprint-element-{pageIndex + 1}-{elementIndex + 1}.png");
            }
            finally
            {
                builtinResult?.ReleaseRenderElements();
                customResult?.ReleaseRenderElements();
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    internal static IResult PagePng(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Prepare(request, cancellationToken);
            if (input.Options.CustomProtocol != null)
                throw new NotSupportedException("新协议未声明完整页面 PNG；请使用元素码图。");
            if (input.Options.Protocol != PrintProtocol.EscPos ||
                input.Options.EscPosLayout != EscPosLayoutMode.Raster)
                throw new NotSupportedException(
                    "完整页面 PNG 仅支持 ESC/POS Raster；当前协议只提供可准确编码的元素码图。");
            var result = new TemplateRenderer().RenderWithLayoutAndPrimitives(input.Template, input.Data,
                input.Resources, input.Options, cancellationToken);
            try
            {
                var capabilities = TemplateProtocolCapabilities.Create(input.Options);
                var dpiX = RasterDpiX(input.Options, capabilities);
                var dpiY = RasterDpiY(input.Options, capabilities);
                var font = input.Options.EscPosDevice?.RasterBarcodeHumanReadableFontSize ??
                    capabilities.RasterBarcodeHumanReadableFontSize ?? 12;
                if (request.PageIndex is int pageIndex)
                {
                    if (pageIndex < 0 || pageIndex >= result.Pages.Count)
                        throw new DesignerOptionException("InvalidPageIndex", "pageIndex 超出预览页范围。", "pageIndex");
                    var page = result.Pages[pageIndex];
                    using var preview = PrintRasterPreview.RenderElements(page.RenderElements, page.Width,
                        page.Height, input.Options.EscPosDevice?.RasterFonts, font, cancellationToken);
                    var png = EncodePng(preview, dpiX, dpiY, cancellationToken);
                    return Results.File(png, "image/png", $"easyprint-page-{pageIndex + 1}.png");
                }
                if (result.Pages.Count == 1)
                {
                    using var preview = PrintRasterPreview.RenderElements(result.Pages[0].RenderElements,
                        result.Pages[0].Width, result.Pages[0].Height,
                        input.Options.EscPosDevice?.RasterFonts, font, cancellationToken);
                    var png = EncodePng(preview, dpiX, dpiY, cancellationToken);
                    return Results.File(png, "image/png", "easyprint-page-1.png");
                }
                using var archiveStream = new BudgetedHostStream("Host.PngZipBuffer", cancellationToken);
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
                {
                    for (var index = 0; index < result.Pages.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using var preview = PrintRasterPreview.RenderElements(result.Pages[index].RenderElements,
                            result.Pages[index].Width, result.Pages[index].Height,
                            input.Options.EscPosDevice?.RasterFonts, font, cancellationToken);
                        var png = EncodePng(preview, dpiX, dpiY, cancellationToken);
                        var entry = archive.CreateEntry($"page-{index + 1:000}.png", CompressionLevel.Fastest);
                        using var target = entry.Open();
                        WriteBytes(target, png, cancellationToken);
                    }
                }
                return Results.File(archiveStream.ToArray("Host.PngZipOutput"), "application/zip",
                    "easyprint-pages-png.zip");
            }
            finally
            {
                result.ReleaseRenderElements();
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    private static bool ElementPngSupported(TemplateRenderOptions options, int elementType)
    {
        if (options.CustomProtocol != null)
            return options.CustomProtocol.Capabilities.SupportsSoftwareBitmapElementType(elementType);
        if (elementType == 0) return true;
        if (elementType == 1 || elementType == 4)
            return options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Raster;
        return elementType is 2 or 3 or 5 or 7 or 8 or 9 or 10 or 11;
    }

    private static int RasterDpiX(TemplateRenderOptions options, TemplateProtocolCapabilities capabilities)
        => options.EscPosDevice?.Profile?.HorizontalDpi ?? capabilities.Dpi;

    private static int RasterDpiY(TemplateRenderOptions options, TemplateProtocolCapabilities capabilities)
        => options.EscPosDevice?.Profile?.VerticalDpi ?? capabilities.Dpi;

    private static byte[] EncodePng(PrintRasterPreviewResult preview, int dpiX, int dpiY,
        CancellationToken cancellationToken)
    {
        if (dpiX <= 0) dpiX = 203;
        if (dpiY <= 0) dpiY = 203;
        using var rgbaLease = PrintResourceScope.Reserve(
            checked((long)preview.Width * preview.Height * 4), "Host.PngRgba");
        using var stream = new BudgetedHostStream("Host.PngBuffer", cancellationToken);
        using (var image = new Image<Rgba32>(preview.Width, preview.Height, new Rgba32(255, 255, 255)))
        {
            for (var y = 0; y < preview.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < preview.Width; x++)
                    if ((preview.Raster[y * preview.RowBytes + x / 8] & (0x80 >> (x % 8))) != 0)
                        image[x, y] = new Rgba32(0, 0, 0);
            }
            image.Metadata.HorizontalResolution = dpiX;
            image.Metadata.VerticalResolution = dpiY;
            image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
            image.Save(stream, new PngEncoder());
        }
        return stream.ToArray("Host.PngOutput");
    }

    internal static IResult GetSamples()
    {
        try
        {
            var root = Path.Combine(AppContext.BaseDirectory, "Samples");
            var loader = new TemplateLoader();
            var labels = new PriceLabelRenderer();
            var defaultPrice = new PriceLabelData
            {
                Name = "三全高端汤圆炫彩小汤圆混装口味黑芝麻、花生、紫薯、抹茶",
                Barcode = "092836193615302",
                Code = "GT-001",
                QrCode = "https://go.example/p/0001",
                OriginPrice = 300m,
                SalePrice = 250m,
                MemberPrice = 200.03m,
                Unit = "袋",
                Specification = "250ml*16",
                Origin = "杭州"
            };
            var pm0025 = new PriceLabelData
            {
                Name = "万代（BANDAI）MG 高达2 拼装模型 180CM（拆封无售后）红龙异端",
                Barcode = "1208000780",
                Code = "1208000780",
                OriginPrice = 10.99m,
                SalePrice = 8.89m,
                Unit = "袋",
                Specification = "132g/袋",
                Origin = "广东广州"
            };
            var pm0034 = new PriceLabelData
            {
                Name = "三全高端汤圆炫彩小汤圆混装口味黑芝麻、花生、紫薯、抹茶、麻薯、巧克力、豆沙",
                Barcode = "092836193615302",
                QrCode = "编码-二维码",
                OriginPrice = 300.02m,
                SalePrice = 250.01m,
                MemberPrice = 200.03m,
                Specification = "250ml*16",
                Origin = "中国"
            };

            var samples = new List<DesignerSample>
            {
                FromFile("label", "固定商品标签", "label.json", new { Product = new { Name = "黑芝麻汤圆 250g", Code = "6901234567890" } }),
                FromFile("receipt", "分页小票", "receipt.json", new { Title = "零售小票", Items = new[] { new { Name = "黑芝麻汤圆", Quantity = 2 }, new { Name = "糯米粉", Quantity = 1 }, new { Name = "红糖", Quantity = 3 } } }),
                FromTemplate("price-label-15", "15mm 价签", PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm15), labels.PrepareData(defaultPrice)),
                FromTemplate("price-label-70", "70mm 价签", PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm70), labels.PrepareData(defaultPrice)),
                FromTemplate("price-label-90", "90mm 价签", PriceLabelTemplateFactory.Create(PriceLabelPreset.Mm90), labels.PrepareData(defaultPrice)),
                FromFile("pm0025", "PM0025 价签重制样例", "price-label-pm0025.json", labels.PrepareData(pm0025)),
                FromFile("pm0034", "PM0034 价签重制样例", "price-label-pm0034.json", labels.PrepareData(pm0034))
            };
            return Results.Json(new { samples }, WebJson);

            DesignerSample FromFile(string id, string name, string file, object data)
            {
                var json = File.ReadAllText(Path.Combine(root, file), Encoding.UTF8);
                return new DesignerSample(id, name, JsonNode.Parse(json)!, ToNode(data));
            }

            DesignerSample FromTemplate(string id, string name, Bing.EasyPrint.Model.BpTemplate template, object data)
                => new(id, name, JsonNode.Parse(loader.SaveJson(template))!, ToNode(data));
        }
        catch (Exception error) { return Error(error); }
    }

    private static PreparedRequest Prepare(DesignerRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.Template.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("template 必须为模板 JSON 对象。");
        if (request.Data.ValueKind == JsonValueKind.Undefined)
            throw new ArgumentException("data 必须包含 JSON 样例数据。");
        using var jsonLease = PrintResourceScope.Reserve(
            checked(request.EncodedRequestBytes * 2), "Host.TemplateJson");
        var templateJson = request.Template.GetRawText();
        var dataJson = request.Data.GetRawText();
        if (Encoding.UTF8.GetByteCount(dataJson) > MaxDataBytes)
            throw new ArgumentException("样例数据超过 1 MiB 上限。");

        cancellationToken.ThrowIfCancellationRequested();
        var options = PrepareOptions(request, cancellationToken);
        PrintResourceScope.Retain(checked((long)(templateJson.Length + dataJson.Length) * 2), "Host.TemplateModel");
        Bing.EasyPrint.Model.BpTemplate template;
        try { template = new TemplateLoader().LoadJsonWithPaths(templateJson, options); }
        catch (Exception error) when (error is ArgumentException or Newtonsoft.Json.JsonException)
        {
            throw new DesignerTemplateException(error);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var raw = BuildRawData(dataJson);
        var legacy = ParseDataToken(dataJson, DateParseHandling.DateTime, FloatParseHandling.Double).ToObject<object>();
        var data = new TemplateDataPair(legacy!, raw!);
        cancellationToken.ThrowIfCancellationRequested();
        var resources = DecodeResources(request.Resources, cancellationToken);
        return new PreparedRequest(template, data, new MemoryResources(resources), options);
    }

    private static object? BuildRawData(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ConvertRaw(document.RootElement);
    }

    private static object? ConvertRaw(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Object => RawObject(value),
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertRaw).ToList(),
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => new JsonRawNumber(value.GetRawText()),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

    private static Dictionary<string, object?> RawObject(JsonElement value)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject()) result[property.Name] = ConvertRaw(property.Value);
        return result;
    }

    private static JToken ParseDataToken(string json, DateParseHandling dateParseHandling = DateParseHandling.None,
        FloatParseHandling floatParseHandling = FloatParseHandling.Decimal)
    {
        using var reader = new JsonTextReader(new StringReader(json))
        {
            DateParseHandling = dateParseHandling,
            FloatParseHandling = floatParseHandling
        };
        var token = JToken.Load(reader);
        if (reader.Read()) throw new Newtonsoft.Json.JsonSerializationException("数据包含多个 JSON 值。");
        return token;
    }
    private static TemplateRenderOptions PrepareOptions(DesignerRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null) throw new ArgumentNullException(nameof(request));

        var protocolIdSpecified = request.ProtocolIdSpecified || request.ProtocolId != null;
        var protocolSpecified = request.ProtocolSpecified || request.Protocol != null;
        var protocolConfigSpecified = request.ProtocolConfigSpecified || request.ProtocolConfig.HasValue;
        var finishingSpecified = request.FinishingSpecified || request.Finishing != null;
        var feedSpecified = request.FeedLinesSpecified || request.FeedLines.HasValue;
        var cutSpecified = request.CutAfterPrintSpecified || request.CutAfterPrint.HasValue;
        if (protocolSpecified && request.Protocol == null)
            throw new DesignerOptionException("InvalidProtocolSelection", "protocol 不能为 null。", "protocol");
        if (protocolIdSpecified)
        {
            if (request.ProtocolIdIsNull || string.IsNullOrWhiteSpace(request.ProtocolId))
                throw new DesignerOptionException("InvalidProtocolSelection", "protocolId 不能为 null 或空白。", "protocolId");
            if (protocolSpecified)
                throw new DesignerOptionException("ProtocolSelectionConflict",
                    "protocol 和 protocolId 不能同时指定。", "protocolId");
            if (protocolConfigSpecified && !request.ProtocolConfig.HasValue)
                throw new DesignerOptionException("InvalidProtocolConfiguration", "protocolConfig 不能为 null。", "protocolConfig");
            if (request.EscPosDevice != null || request.TsplMedia != null || request.ZplMedia != null ||
                request.EscPosLayoutMode != null || request.Encoding != null ||
                request.DeviceHalfCellWidthAt24.HasValue || request.DeviceFullCellWidthAt24.HasValue ||
                request.DeviceLineHeightAt24.HasValue || request.DeviceFontCalibrations != null ||
                request.RequireDeclaredNativeBounds || request.CutOptions != null || request.Job != null)
                throw new DesignerOptionException("ProtocolSelectionConflict",
                    "protocolId 不能与内置协议专用字段同时指定。", "protocolId");
            if (finishingSpecified && request.Finishing == null)
                throw new DesignerOptionException("InvalidProtocolConfiguration", "finishing 不能为 null。", "finishing");
            if (finishingSpecified && (feedSpecified || cutSpecified))
                throw new DesignerOptionException("ProtocolSelectionConflict",
                    "finishing 不能与旧 feedLines/cutAfterPrint 同时指定。", "finishing");
            var configurationJson = request.ProtocolConfig.HasValue
                ? request.ProtocolConfig.Value.GetRawText() : string.Empty;
            if (!ProtocolRegistry.TryGetAdapter(request.ProtocolId!, configurationJson,
                    out var customAdapter, out var configurationError))
                throw new DesignerOptionException("InvalidProtocolConfiguration",
                    configurationError?.Message ?? "新协议配置或标识无效。", "protocolConfig");
            var finishing = request.Finishing != null
                ? ParseFinishing(request.Finishing)
                : LegacyCustomFinishing(request);
            return new TemplateRenderOptions
            {
                Copies = request.Copies ?? 1,
                ImageThreshold = request.ImageThreshold ?? 128,
                ValidateGs1Associations = request.ValidateGs1Associations,
                ProtocolId = request.ProtocolId,
                CustomProtocol = customAdapter,
                CustomProtocolConfigurationJson = configurationJson,
                ProtocolRegistry = ProtocolRegistry.CoreRegistry,
                CustomFinishing = finishing
            };
        }
        if (protocolConfigSpecified)
            throw new DesignerOptionException("ProtocolSelectionConflict",
                "protocolConfig 只能与 protocolId 同时指定。", "protocolConfig");
        if (finishingSpecified)
            throw new DesignerOptionException("ProtocolSelectionConflict",
                "finishing 只能与 protocolId 同时指定。", "finishing");
        var protocol = ParseProtocol(request.Protocol);
        if (request.EscPosDevice != null && protocol != PrintProtocol.EscPos)
            throw new ArgumentException("escPosDevice 只能用于 ESC/POS 协议。", nameof(request.EscPosDevice));
        if (request.TsplMedia != null && protocol != PrintProtocol.Tspl)
            throw new ArgumentException("tsplMedia 只能用于 TSPL 协议。", nameof(request.TsplMedia));
        if (request.ZplMedia != null && protocol != PrintProtocol.Zpl)
            throw new ArgumentException("zplMedia 只能用于 ZPL 协议。", nameof(request.ZplMedia));
        var escPosLayout = ParseEscPosLayoutMode(request.EscPosLayoutMode);
        var escPosDevice = protocol == PrintProtocol.EscPos && request.EscPosDevice != null
            ? ParseEscPosDevice(request.EscPosDevice, escPosLayout, cancellationToken)
            : null;
        var encoding = ParseEncoding(request.Encoding, protocol, escPosLayout, escPosDevice);
        ValidateEscPosTextEncoding(protocol, escPosLayout, encoding, escPosDevice);
        var options = new TemplateRenderOptions
        {
            Protocol = protocol,
            EscPosLayout = escPosLayout,
            Encoding = encoding,
            Copies = request.Copies ?? 1,
            FeedLines = request.FeedLines,
            CutAfterPrint = request.CutAfterPrint,
            CutOptions = request.CutOptions is null ? null : ParseCutOptions(request.CutOptions),
            Job = request.Job is null ? null : ParseJob(request.Job),
            ImageThreshold = request.ImageThreshold ?? 128,
            DeviceHalfCellWidthAt24 = request.DeviceHalfCellWidthAt24,
            DeviceFullCellWidthAt24 = request.DeviceFullCellWidthAt24,
            DeviceLineHeightAt24 = request.DeviceLineHeightAt24,
            DeviceFontCalibrations = request.DeviceFontCalibrations?.ToDictionary(
                pair => pair.Key,
                pair => new DeviceFontCalibration
                {
                    HalfCellWidthAt24 = pair.Value?.HalfCellWidthAt24 ?? 0,
                    FullCellWidthAt24 = pair.Value?.FullCellWidthAt24 ?? 0,
                    LineHeightAt24 = pair.Value?.LineHeightAt24 ?? 0,
                    TextElements = pair.Value?.TextElements?.ToDictionary(
                        element => element.Key,
                        element => new DeviceTextElementCalibration
                        {
                            AdvanceAt24 = element.Value?.AdvanceAt24 ?? double.NaN,
                            InkLeftAt24 = element.Value?.InkLeftAt24,
                            InkTopAt24 = element.Value?.InkTopAt24,
                            InkRightAt24 = element.Value?.InkRightAt24,
                            InkBottomAt24 = element.Value?.InkBottomAt24
                        },
                        StringComparer.Ordinal)
                },
                StringComparer.Ordinal),
            RequireDeclaredNativeBounds = request.RequireDeclaredNativeBounds,
            ValidateGs1Associations = request.ValidateGs1Associations,
            EscPosDevice = escPosDevice,
            TsplMedia = protocol == PrintProtocol.Tspl
                ? new TsplMediaOptions
                {
                    AllowRasterizedLines = request.AllowRasterizedLines,
                    AllowRasterizedTextStyles = request.TsplMedia?.AllowRasterizedTextStyles ?? false,
                    Dpi = request.TsplMedia?.Dpi ?? TsplMediaOptions.DefaultDpi,
                    GapDistance = request.TsplMedia?.GapDistance,
                    GapOffset = request.TsplMedia?.GapOffset,
                    Direction = request.TsplMedia?.Direction,
                    ReferenceX = request.TsplMedia?.ReferenceX,
                    ReferenceY = request.TsplMedia?.ReferenceY,
                    Speed = request.TsplMedia?.Speed,
                    Density = request.TsplMedia?.Density,
                    Profile = request.TsplMedia?.Profile is null ? null : ParseTsplPrinterProfile(request.TsplMedia.Profile)
                }
                : null,
            ZplMedia = protocol == PrintProtocol.Zpl
                ? new ZplMediaOptions
                {
                    AllowRasterizedLines = request.AllowRasterizedLines,
                    AllowRasterizedTextStyles = request.ZplMedia?.AllowRasterizedTextStyles ?? false,
                    Dpi = request.ZplMedia?.Dpi ?? ZplMediaOptions.DefaultDpi,
                    PrintSpeed = request.ZplMedia?.PrintSpeed,
                    Orientation = ParseZplOrientation(request.ZplMedia?.Orientation),
                    MediaTracking = ParseZplMediaTracking(request.ZplMedia?.MediaTracking),
                    Darkness = request.ZplMedia?.Darkness,
                    Profile = request.ZplMedia?.Profile is null ? null : ParseZplPrinterProfile(request.ZplMedia.Profile)
                }
                : null
        };
        return options;
    }

    private static object? GetTsplCutterCapabilities(TsplCutterProfile? cutter) => cutter is null ? null : new
    {
        strategy = cutter.Strategy.ToString(),
        supportedCutModes = cutter.SupportedCutModes.Select(mode => mode.ToString()),
        supportsEndOfJob = cutter.SupportsEndOfJob,
        cutPositionFeedDots = cutter.CutPositionFeedDots
    };

    private static object? GetZplCutterCapabilities(ZplCutterProfile? cutter) => cutter is null ? null : new
    {
        strategy = cutter.Strategy.ToString(),
        supportedCutModes = cutter.SupportedCutModes.Select(mode => mode.ToString()),
        supportsTearOff = cutter.SupportsTearOff,
        kioskPartialUncutMillimeters = cutter.KioskPartialUncutMillimeters,
        kioskCutMarginMillimeters = cutter.KioskCutMarginMillimeters
    };

    private static object GetRasterPreviewCapabilities(TemplateProtocolCapabilities capabilities,
        TemplateRenderOptions options)
    {
        var escPosRaster = options.Protocol == PrintProtocol.EscPos && options.EscPosLayout == EscPosLayoutMode.Raster;
        var elementTypes = escPosRaster
            ? new[] { 0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11 }
            : new[] { 0, 2, 3, 5, 7, 8, 9, 10, 11 };
        var dpiX = options.EscPosDevice?.Profile?.HorizontalDpi ?? capabilities.Dpi;
        var dpiY = options.EscPosDevice?.Profile?.VerticalDpi ?? capabilities.Dpi;
        return new
        {
            softwareBitmap = true,
            elementPng = true,
            elementTypes,
            pagePng = escPosRaster,
            pagePngReason = escPosRaster ? null :
                "完整页面 PNG 仅支持 ESC/POS Raster；当前协议只提供可准确编码的元素码图。",
            dpiX,
            dpiY
        };
    }

    private static object? GetLabelProfileCapabilities(TemplateProtocolCapabilities capabilities, TemplateRenderOptions options)
    {
        if (capabilities.DeviceProfileName is null) return null;
        var encodings = options.Protocol == PrintProtocol.Tspl
            ? options.TsplMedia?.Profile?.EncodingCodePages ?? new[] { 20127, 65001, 936 }
            : options.ZplMedia?.Profile?.EncodingCodePages ?? new[] { 20127, 65001 };
        return new
        {
            name = capabilities.DeviceProfileName,
            firmwareVersion = capabilities.DeviceFirmwareVersion,
            printableWidthDots = capabilities.MaxPrintableWidthDots,
            maxLabelHeightDots = capabilities.MaxLabelHeightDots,
            maxImageDataBytes = capabilities.MaxImageDataBytes,
            supportsNativeBarcodeHumanReadable = options.Protocol == PrintProtocol.Tspl
                ? options.TsplMedia?.Profile?.SupportsNativeBarcodeHumanReadable ?? true
                : options.ZplMedia?.Profile?.SupportsNativeBarcodeHumanReadable ?? true,
            supportedNativeBarcodeHumanReadableTypes = capabilities.SupportedNativeBarcodeHumanReadableTypes?.Select(type => type.ToString()),
            supportedNativeQrCodeRotations = capabilities.SupportedNativeQrCodeRotations?.Select(rotation => rotation.ToString()),
            encodingCodePages = encodings,
            characterRanges = capabilities.DeviceCharacterRanges?.Select(range => new { first = range.First, last = range.Last })
        };
    }

    private static PrintCharacterRange[]? ParseLabelCharacterRanges(DesignerPrintCharacterRangeRequest?[]? input)
        => input?.Select((range, index) =>
        {
            if (range is null || !range.First.HasValue || !range.Last.HasValue)
                throw new ArgumentException($"标签机型字库区间 {index} 必须同时声明 first 和 last。");
            return new PrintCharacterRange(range.First.Value, range.Last.Value);
        }).ToArray();

    private static TsplPrinterProfile ParseTsplPrinterProfile(DesignerTsplPrinterProfileRequest request)
        => new()
        {
            Name = request.Name ?? "Custom", FirmwareVersion = request.FirmwareVersion,
            PrintableWidthDots = request.PrintableWidthDots, MaxLabelHeightDots = request.MaxLabelHeightDots,
            MaxImageDataBytes = request.MaxImageDataBytes,
            SupportsImages = request.SupportsImages ?? true, SupportsGraphics = request.SupportsGraphics ?? true,
            SupportsNativeBarcode = request.SupportsNativeBarcode ?? true,
            SupportsNativeBarcodeHumanReadable = request.SupportsNativeBarcodeHumanReadable ?? true,
            SupportedNativeBarcodeHumanReadableTypes = ParseBarcodeHumanReadableTypes(
                request.SupportedNativeBarcodeHumanReadableTypes, nameof(request.SupportedNativeBarcodeHumanReadableTypes)),
            SupportsNativeQrCode = request.SupportsNativeQrCode ?? true,
            SupportedNativeQrCodeRotations = ParseNativeQrCodeRotations(
                request.SupportedNativeQrCodeRotations, nameof(request.SupportedNativeQrCodeRotations)),
            SupportsRotation = request.SupportsRotation ?? true,
            Cutter = request.Cutter is null ? null : new TsplCutterProfile
            {
                Strategy = ParseJobEnum(request.Cutter.Strategy, TsplCutStrategy.Batch),
                SupportedCutModes = request.Cutter.SupportedCutModes is null ? new[] { PrintCutMode.Full } :
                    request.Cutter.SupportedCutModes.Select(value => value is null
                        ? throw new ArgumentException("TSPL 切法不能为 null。") : ParseJobEnum(value, PrintCutMode.Full)).ToArray(),
                SupportsEndOfJob = request.Cutter.SupportsEndOfJob ?? false,
                CutPositionFeedDots = request.Cutter.CutPositionFeedDots
            },
            DeviceFontNames = request.DeviceFontNames, EncodingCodePages = request.EncodingCodePages,
            CharacterRanges = ParseLabelCharacterRanges(request.CharacterRanges)
        };

    private static ZplPrinterProfile ParseZplPrinterProfile(DesignerZplPrinterProfileRequest request)
        => new()
        {
            Name = request.Name ?? "Custom", FirmwareVersion = request.FirmwareVersion,
            PrintableWidthDots = request.PrintableWidthDots, MaxLabelHeightDots = request.MaxLabelHeightDots,
            MaxImageDataBytes = request.MaxImageDataBytes,
            SupportsImages = request.SupportsImages ?? true, SupportsGraphics = request.SupportsGraphics ?? true,
            SupportsNativeBarcode = request.SupportsNativeBarcode ?? true,
            SupportsNativeBarcodeHumanReadable = request.SupportsNativeBarcodeHumanReadable ?? true,
            SupportedNativeBarcodeHumanReadableTypes = ParseBarcodeHumanReadableTypes(
                request.SupportedNativeBarcodeHumanReadableTypes, nameof(request.SupportedNativeBarcodeHumanReadableTypes)),
            SupportsNativeQrCode = request.SupportsNativeQrCode ?? true,
            SupportedNativeQrCodeRotations = ParseNativeQrCodeRotations(
                request.SupportedNativeQrCodeRotations, nameof(request.SupportedNativeQrCodeRotations)),
            SupportsRotation = request.SupportsRotation ?? true,
            SupportsCut = request.SupportsCut ?? true,
            Cutter = request.Cutter is null ? null : new ZplCutterProfile
            {
                Strategy = ParseJobEnum(request.Cutter.Strategy, ZplCutStrategy.Cutter),
                SupportedCutModes = request.Cutter.SupportedCutModes is null ? new[] { PrintCutMode.Full } :
                    request.Cutter.SupportedCutModes.Select(value => value is null
                        ? throw new ArgumentException("ZPL 切法不能为 null。") : ParseJobEnum(value, PrintCutMode.Full)).ToArray(),
                SupportsTearOff = request.Cutter.SupportsTearOff ?? false,
                KioskPartialUncutMillimeters = request.Cutter.KioskPartialUncutMillimeters,
                KioskCutMarginMillimeters = request.Cutter.KioskCutMarginMillimeters
            },
            DeviceFontNames = request.DeviceFontNames, EncodingCodePages = request.EncodingCodePages,
            CharacterRanges = ParseLabelCharacterRanges(request.CharacterRanges)
        };

    private static BarcodeType[]? ParseBarcodeHumanReadableTypes(string?[]? values, string parameterName)
        => values?.Select((value, index) => value is null
            ? throw new ArgumentException($"{parameterName}[{index}] 不能为 null。", parameterName)
            : ParseJobEnum(value, BarcodeType.Code128)).ToArray();

    private static RotationAngle[]? ParseNativeQrCodeRotations(string?[]? values, string parameterName)
        => values?.Select((value, index) =>
        {
            if (value is null) throw new ArgumentException($"{parameterName}[{index}] 不能为 null。", parameterName);
            var rotation = ParseJobEnum(value, RotationAngle.Rotate90);
            if (rotation == RotationAngle.None)
                throw new ArgumentException($"{parameterName}[{index}] 不能为 None。", parameterName);
            return rotation;
        }).ToArray();

    private static EscPosBarcodeTextPosition[]? ParseEscPosBarcodeTextPositions(string?[]? values, string parameterName)
        => values?.Select((value, index) =>
        {
            if (value is null) throw new ArgumentException($"{parameterName}[{index}] 不能为 null。", parameterName);
            var position = ParseJobEnum(value, EscPosBarcodeTextPosition.Below);
            if (position == EscPosBarcodeTextPosition.None)
                throw new ArgumentException($"{parameterName}[{index}] 不能为 None。", parameterName);
            return position;
        }).ToArray();

    private static void ValidateEscPosTextEncoding(PrintProtocol protocol, EscPosLayoutMode layout,
        Encoding? encoding, EscPosDeviceOptions? device)
    {
        var text = device?.Profile?.Text;
        if (protocol != PrintProtocol.EscPos || layout != EscPosLayoutMode.Native || text is null) return;

        // TemplateRenderer 的既有默认编码是 GBK；未显式传 encoding 时只用它做契约比较，
        // 不把默认值写回 options，避免改变其他协议和旧调用方的行为。
        var effectiveEncoding = encoding ?? Encoding.GetEncoding("gbk");
        if (effectiveEncoding.CodePage != text.EncodingCodePage)
            throw new NotSupportedException(
                $"设备文字契约要求代码页 {text.EncodingCodePage}，主机编码为 {effectiveEncoding.CodePage} ({effectiveEncoding.WebName})。");
    }

    private static PrintProtocol ParseProtocol(string? value)
    {
        if (value is null) return PrintProtocol.Cpcl;

        return value.Trim().ToLowerInvariant() switch
        {
            "cpcl" => PrintProtocol.Cpcl,
            "escpos" => PrintProtocol.EscPos,
            "tspl" => PrintProtocol.Tspl,
            "zpl" => PrintProtocol.Zpl,
            _ => throw new DesignerOptionException("UnsupportedProtocol",
                "protocol 必须是 Cpcl、EscPos、Tspl 或 Zpl。", "protocol")
        };
    }

    private static EscPosLayoutMode ParseEscPosLayoutMode(string? value)
    {
        // 默认采用严格 Native；仅显式请求 Raster 时启用光栅化。
        if (value is null) return EscPosLayoutMode.Native;

        return value.Trim().ToLowerInvariant() switch
        {
            "native" => EscPosLayoutMode.Native,
            "raster" => EscPosLayoutMode.Raster,
            _ => throw new ArgumentException(
                "escPosLayoutMode 必须是 Native 或 Raster。", "escPosLayoutMode")
        };
    }

    private static Encoding? ParseEncoding(string? value, PrintProtocol protocol, EscPosLayoutMode layout,
        EscPosDeviceOptions? device)
    {
        if (value is null) return null;
        if (string.Equals(value.Trim(), "device", StringComparison.OrdinalIgnoreCase))
        {
            if (protocol != PrintProtocol.EscPos || layout != EscPosLayoutMode.Native || device?.Profile?.Text is null)
                throw new ArgumentException("encoding=Device 仅适用于声明了文字契约的 ESC/POS Native 配置。");
            return Encoding.GetEncoding(device.Profile.Text.EncodingCodePage);
        }
        var encoding = value.Trim().ToLowerInvariant() switch
        {
            "gbk" => Encoding.GetEncoding("gbk"),
            "utf8" => Encoding.UTF8,
            "ascii" => Encoding.ASCII,
            _ => throw new ArgumentException("encoding 必须是 Gbk、Utf8、Ascii 或 Device。")
        };
        if (protocol == PrintProtocol.Zpl && encoding.CodePage != Encoding.UTF8.CodePage &&
            encoding.CodePage != Encoding.ASCII.CodePage)
            throw new ArgumentException("ZPL 仅支持 Utf8 或 Ascii 编码。");
        return encoding;
    }

    private static EscPosDeviceOptions ParseEscPosDevice(DesignerEscPosDeviceRequest request, EscPosLayoutMode layout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.CodePageIndex is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.CodePageIndex), "设备代码页编号必须在 0–255 范围内。");
        if (request.RasterFonts != null && layout != EscPosLayoutMode.Raster)
            throw new ArgumentException("固定主机字体配置只能用于 ESC/POS Raster。", nameof(request.RasterFonts));
        if (request.RasterBarcodeHumanReadableFontSize != null && layout != EscPosLayoutMode.Raster)
            throw new ArgumentException("Raster 条码可读文字字号只能用于 ESC/POS Raster。",
                nameof(request.RasterBarcodeHumanReadableFontSize));
        if (request.RasterBarcodeHumanReadableFontSize is < 1 or > 255)
            throw new ArgumentOutOfRangeException(nameof(request.RasterBarcodeHumanReadableFontSize),
                "Raster 条码可读文字字号必须在 1–255 点范围内。");
        var profile = request.Profile is null ? null : ParseEscPosPrinterProfile(request.Profile);
        var rasterFonts = request.RasterFonts is null ? null : ParseEscPosRasterFonts(request.RasterFonts, cancellationToken);
        return new EscPosDeviceOptions
        {
            Dpi = request.Dpi ?? profile?.HorizontalDpi ?? 203,
            Profile = profile,
            RasterFonts = rasterFonts,
            RasterBarcodeHumanReadableFontSize = request.RasterBarcodeHumanReadableFontSize,
            CodePageIndex = request.CodePageIndex.HasValue ? (byte)request.CodePageIndex.Value : null,
            PrintAreaWidthUnits = request.PrintAreaWidthUnits,
            NativeTabWidthDots = request.NativeTabWidthDots,
            NativeFlow = ParseEscPosNativeFlow(request.NativeFlow)
        };
    }

    private static RasterFontCatalog ParseEscPosRasterFonts(DesignerRasterFontCatalogRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.DefaultFont))
            throw new ArgumentException("Raster 固定字体配置必须声明 defaultFont。", nameof(request.DefaultFont));
        if (request.Fonts is null || request.Fonts.Length == 0 || request.Fonts.Length > MaxRasterFontCount)
            throw new ArgumentException($"Raster 固定字体资源数量必须为 1–{MaxRasterFontCount} 个。", nameof(request.Fonts));
        if (request.FallbackFonts?.Any(string.IsNullOrWhiteSpace) == true)
            throw new ArgumentException("Raster 固定字体 fallbackFonts 不能包含空名称。", nameof(request.FallbackFonts));

        var estimates = new List<(DesignerRasterFontRequest Font, long Regular, long Bold)>();
        long estimatedTotal = 0;
        foreach (var font in request.Fonts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (font is null || string.IsNullOrWhiteSpace(font.Name) || string.IsNullOrWhiteSpace(font.Version))
                throw new ArgumentException("Raster 固定字体的 name、version 和 regularData 必须非空。", nameof(request.Fonts));
            var regular = EstimateBase64Bytes(font.RegularData, $"字体 {font.Name} 的 regularData", cancellationToken);
            var bold = font.BoldData is null ? 0 : EstimateBase64Bytes(font.BoldData, $"字体 {font.Name} 的 boldData", cancellationToken);
            estimatedTotal = checked(estimatedTotal + regular + bold);
            if (estimatedTotal > MaxTotalRasterFontBytes)
                throw new ArgumentException("Raster 固定字体字节总量不能超过 32 MiB。", nameof(request.Fonts));
            estimates.Add((font, regular, bold));
        }

        var resources = new List<RasterFontResource>(estimates.Count);
        long totalBytes = 0;
        foreach (var item in estimates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var regularLease = PrintResourceScope.Reserve(item.Regular, "Host.FontBase64Regular");
            using var boldLease = PrintResourceScope.Reserve(item.Bold, "Host.FontBase64Bold");
            byte[] regular;
            byte[]? bold = null;
            try
            {
                regular = Convert.FromBase64String(item.Font.RegularData!);
                if (item.Font.BoldData != null) bold = Convert.FromBase64String(item.Font.BoldData);
            }
            catch (FormatException error)
            {
                throw new ArgumentException($"Raster 固定字体 {item.Font.Name} 的字体数据必须是有效 Base64。", nameof(request.Fonts), error);
            }
            if (regular.Length == 0 || bold != null && bold.Length == 0)
                throw new ArgumentException($"Raster 固定字体 {item.Font.Name} 的字体数据不能为空。", nameof(request.Fonts));
            totalBytes = checked(totalBytes + regular.Length + (bold?.Length ?? 0));
            if (totalBytes > MaxTotalRasterFontBytes)
                throw new ArgumentException("Raster 固定字体字节总量不能超过 32 MiB。", nameof(request.Fonts));
            cancellationToken.ThrowIfCancellationRequested();
            resources.Add(new RasterFontResource(item.Font.Name, item.Font.Version, regular, bold, cancellationToken));
        }
        return new RasterFontCatalog(resources, request.DefaultFont, request.FallbackFonts, cancellationToken);
    }

    private static long EstimateBase64Bytes(string? value, string label, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} 不能为空。", nameof(value));
        if (value.Length % 4 != 0)
            throw new ArgumentException($"{label} 的 Base64 长度无效。", nameof(value));
        var padding = value.EndsWith("==", StringComparison.Ordinal) ? 2 :
            value.EndsWith("=", StringComparison.Ordinal) ? 1 : 0;
        var contentLength = value.Length - padding;
        for (var index = 0; index < contentLength; index++)
        {
            if ((index & 8191) == 0) cancellationToken.ThrowIfCancellationRequested();
            var character = value[index];
            if (!char.IsAsciiLetterOrDigit(character) && character != '+' && character != '/')
                throw new ArgumentException($"{label} 必须是无空白的 Base64 数据。", nameof(value));
        }
        for (var index = contentLength; index < value.Length; index++)
            if (value[index] != '=')
                throw new ArgumentException($"{label} 的 Base64 填充无效。", nameof(value));
        var decoded = checked((long)(value.Length / 4) * 3 - padding);
        if (decoded <= 0 || decoded > MaxTotalRasterFontBytes)
            throw new ArgumentException($"{label} 超过 32 MiB 或为空。", nameof(value));
        return decoded;
    }

    private static PrintJobOptions ParseJob(DesignerPrintJobRequest request) => new()
    {
        Copies = request.Copies ?? 1,
        CopyOrder = ParseJobEnum(request.CopyOrder, PrintCopyOrder.Collated),
        FeedScope = ParseJobEnum(request.FeedScope, PrintFinishScope.None),
        FeedUnit = ParseJobEnum(request.FeedUnit, PrintFeedUnit.Lines),
        FeedAmount = request.FeedAmount ?? 0,
        CutScope = ParseJobEnum(request.CutScope, PrintFinishScope.None),
        CutOptions = request.CutOptions is null ? null : ParseCutOptions(request.CutOptions)
    };

    private static PrintProtocolFinishingRequest ParseFinishing(DesignerProtocolFinishingRequest request)
    {
        var result = new PrintProtocolFinishingRequest
        {
            FeedAmount = request.FeedAmount ?? 0,
            FeedUnit = ParseJobEnum(request.FeedUnit, PrintFeedUnit.Lines),
            Cut = request.Cut ?? false,
            Placement = ParseJobEnum(request.Placement, PrintProtocolFinishingPlacement.JobEnd),
        };
        result.Validate();
        return result;
    }

    private static PrintProtocolFinishingRequest? LegacyCustomFinishing(DesignerRequest request)
    {
        if (!request.FeedLines.HasValue && !request.CutAfterPrint.HasValue) return null;
        var result = new PrintProtocolFinishingRequest
        {
            FeedAmount = request.FeedLines ?? 0,
            FeedUnit = PrintFeedUnit.Lines,
            Cut = request.CutAfterPrint ?? false,
            Placement = PrintProtocolFinishingPlacement.JobEnd,
        };
        result.Validate();
        return result;
    }

    private static PrintCutOptions ParseCutOptions(DesignerPrintCutRequest request) => new()
    {
        Mode = ParseJobEnum(request.Mode, PrintCutMode.Full),
        FeedToCutPosition = request.FeedToCutPosition ?? false,
        FeedBeforeCutAmount = request.FeedBeforeCutAmount ?? 0,
        FeedBeforeCutUnit = ParseJobEnum(request.FeedBeforeCutUnit, PrintFeedUnit.MotionUnits)
    };

    private static T ParseJobEnum<T>(string? value, T defaultValue) where T : struct, Enum
    {
        if (value is null) return defaultValue;
        if (Enum.TryParse<T>(value.Trim(), true, out var parsed) && Enum.IsDefined(parsed) &&
            string.Equals(parsed.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase)) return parsed;
        throw new ArgumentException($"不支持的任务 {typeof(T).Name}：{value}。");
    }

    private static EscPosNativeFlowMode ParseEscPosNativeFlow(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            null or "rows" => EscPosNativeFlowMode.Rows,
            "sequential" => EscPosNativeFlowMode.Sequential,
            _ => throw new ArgumentException("不支持的 ESC/POS Native 顺序布局方式。", nameof(value))
        };

    private static EscPosPrinterProfile ParseEscPosPrinterProfile(DesignerEscPosPrinterProfileRequest request)
    {
        var imageCommand = ParseEscPosImageCommand(request.ImageCommand);
        var bitImageMode = ParseEscPosBitImageMode(request.BitImageMode);
        var horizontalDpi = request.HorizontalDpi ?? 203;
        var verticalDpi = request.VerticalDpi ?? 203;
        var unidirectionalBitImage = request.UnidirectionalBitImage ?? false;
        if (horizontalDpi <= 0 || verticalDpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.HorizontalDpi), "机型横纵 DPI 必须为正数。");
        if (imageCommand == EscPosImageCommand.RasterGsV0 && horizontalDpi != verticalDpi)
            throw new ArgumentException("GS v 0 配置必须使用相同的横纵分辨率。", nameof(request));
        if (imageCommand == EscPosImageCommand.RasterGsV0 &&
            (bitImageMode != EscPosBitImageMode.DoubleDensity24 || unidirectionalBitImage))
            throw new ArgumentException("GS v 0 不接受 ESC * 点阵模式或单向打印设置。", nameof(request));
        if (request.MotionUnitsPerInch is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.MotionUnitsPerInch), "进纸单位必须为正数。");
        if (request.PrintableWidthDots is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.PrintableWidthDots), "可打印宽度必须为正数。");
        var minimumBlockBytes = imageCommand == EscPosImageCommand.BitImageEscStar &&
            (bitImageMode == EscPosBitImageMode.SingleDensity24 || bitImageMode == EscPosBitImageMode.DoubleDensity24) ? 3 : 1;
        if (request.MaxImageBlockBytes is int blockBytes && blockBytes < minimumBlockBytes)
            throw new ArgumentOutOfRangeException(nameof(request.MaxImageBlockBytes),
                $"图片分块预算至少为 {minimumBlockBytes} 字节。");
        return new EscPosPrinterProfile
        {
            Name = request.Name ?? "Custom",
            Text = request.Text is null ? null : ParseEscPosTextProfile(request.Text),
            ImageCommand = imageCommand,
            BitImageMode = bitImageMode,
            HorizontalDpi = horizontalDpi,
            VerticalDpi = verticalDpi,
            MotionUnitsPerInch = request.MotionUnitsPerInch ?? 203,
            PrintableWidthDots = request.PrintableWidthDots,
            MaxImageBlockBytes = request.MaxImageBlockBytes,
            SupportsCut = request.SupportsCut ?? true,
            SupportedCutModes = request.SupportedCutModes?.Select(value =>
                value is null ? throw new ArgumentException("切纸模式不能为 null。") : ParseJobEnum(value, PrintCutMode.Full)).ToArray(),
            SupportsFeedAndCut = request.SupportsFeedAndCut ?? false,
            SupportsNativeBarcode = request.SupportsNativeBarcode ?? true,
            SupportsNativeBarcodeHumanReadable = request.SupportsNativeBarcodeHumanReadable ?? true,
            SupportedNativeBarcodeHumanReadableTypes = ParseBarcodeHumanReadableTypes(
                request.SupportedNativeBarcodeHumanReadableTypes, nameof(request.SupportedNativeBarcodeHumanReadableTypes)),
            SupportedNativeBarcodeHumanReadablePositions = ParseEscPosBarcodeTextPositions(
                request.SupportedNativeBarcodeHumanReadablePositions, nameof(request.SupportedNativeBarcodeHumanReadablePositions)),
            SupportsNativeQrCode = request.SupportsNativeQrCode ?? true,
            SupportsPrintAreaWidth = request.SupportsPrintAreaWidth ?? true,
            SupportsTextSize = request.SupportsTextSize ?? true,
            UnidirectionalBitImage = unidirectionalBitImage
        };
    }

    private static EscPosTextProfile ParseEscPosTextProfile(DesignerEscPosTextProfileRequest request)
    {
        if (!request.EncodingCodePage.HasValue || request.EncodingCodePage.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.EncodingCodePage),
                "设备文字契约必须声明正数编码代码页。");
        if (request.CodePageIndex is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.CodePageIndex),
                "设备文字契约代码页编号必须在 0–255 范围内。");
        if (request.CharacterRanges is null || request.CharacterRanges.Length == 0)
            throw new ArgumentException("设备文字契约必须声明非空的字库覆盖区间。",
                nameof(request.CharacterRanges));

        try
        {
            _ = Encoding.GetEncoding(request.EncodingCodePage.Value);
        }
        catch (Exception error) when (error is ArgumentException || error is NotSupportedException)
        {
            throw new ArgumentException("设备文字契约的编码代码页不可用。",
                nameof(request.EncodingCodePage), error);
        }

        var ranges = request.CharacterRanges.Select((range, index) =>
        {
            if (range is null || !range.First.HasValue || !range.Last.HasValue)
                throw new ArgumentException($"设备文字契约的字库区间 {index} 必须同时声明 first 和 last。",
                    nameof(request.CharacterRanges));
            return new PrintCharacterRange(range.First.Value, range.Last.Value);
        }).ToArray();
        var halfCellWidthDots = request.HalfCellWidthDots ?? 12;
        var fullCellWidthDots = request.FullCellWidthDots ?? 24;
        var lineHeightDots = request.LineHeightDots ?? 24;
        if (halfCellWidthDots <= 0 || fullCellWidthDots <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.HalfCellWidthDots),
                "设备文字契约字宽必须为正数。");
        if (lineHeightDots != 24 && lineHeightDots != 26)
            throw new ArgumentOutOfRangeException(nameof(request.LineHeightDots),
                "设备文字契约行高必须为 24 或 26 点。");

        return new EscPosTextProfile
        {
            EncodingCodePage = request.EncodingCodePage.Value,
            CodePageIndex = request.CodePageIndex.HasValue ? (byte)request.CodePageIndex.Value : null,
            CharacterMode = ParseEscPosCharacterMode(request.CharacterMode),
            CharacterRanges = ranges,
            HalfCellWidthDots = halfCellWidthDots,
            FullCellWidthDots = fullCellWidthDots,
            LineHeightDots = lineHeightDots
        };
    }

    private static EscPosCharacterMode ParseEscPosCharacterMode(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            null or "devicedefault" => EscPosCharacterMode.DeviceDefault,
            "singlebyte" => EscPosCharacterMode.SingleByte,
            "fskanji" => EscPosCharacterMode.FsKanji,
            _ => throw new ArgumentException(
                "escPosDevice.profile.text.characterMode 必须是 DeviceDefault、SingleByte 或 FsKanji。")
        };

    private static EscPosImageCommand ParseEscPosImageCommand(string? value)
        => value switch
        {
            null => EscPosImageCommand.RasterGsV0,
            "RasterGsV0" => EscPosImageCommand.RasterGsV0,
            "BitImageEscStar" => EscPosImageCommand.BitImageEscStar,
            _ => throw new ArgumentException("escPosDevice.profile.imageCommand 必须是 RasterGsV0 或 BitImageEscStar。")
        };

    private static EscPosBitImageMode ParseEscPosBitImageMode(string? value)
        => value switch
        {
            null => EscPosBitImageMode.DoubleDensity24,
            "SingleDensity8" => EscPosBitImageMode.SingleDensity8,
            "DoubleDensity8" => EscPosBitImageMode.DoubleDensity8,
            "SingleDensity24" => EscPosBitImageMode.SingleDensity24,
            "DoubleDensity24" => EscPosBitImageMode.DoubleDensity24,
            _ => throw new ArgumentException(
                "escPosDevice.profile.bitImageMode 必须是 SingleDensity8、DoubleDensity8、SingleDensity24 或 DoubleDensity24。")
        };

    private static PrintOrientation? ParseZplOrientation(string? value)
    {
        if (value is null) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "none" => PrintOrientation.None,
            "down" => PrintOrientation.Down,
            "left" => PrintOrientation.Left,
            "right" => PrintOrientation.Right,
            _ => throw new ArgumentException("zplMedia.orientation 必须是 None、Down、Left 或 Right。")
        };
    }

    private static char? ParseZplMediaTracking(string? value)
    {
        if (value is null) return null;
        var code = value.Trim().ToUpperInvariant();
        if (code.Length != 1 || "NYWMAV".IndexOf(code[0]) < 0)
            throw new ArgumentException("zplMedia.mediaTracking 必须是 N、Y、W、M、A 或 V。");
        return code[0];
    }

    private static string GetProtocolName(PrintProtocol protocol) => protocol switch
    {
        PrintProtocol.Cpcl => "Cpcl", PrintProtocol.EscPos => "EscPos",
        PrintProtocol.Tspl => "Tspl", PrintProtocol.Zpl => "Zpl",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol))
    };

    private static string GetProtocolFileExtension(PrintProtocol protocol)
        => protocol switch
        {
            PrintProtocol.Cpcl => "cpcl",
            PrintProtocol.EscPos => "escpos",
            PrintProtocol.Tspl => "tspl",
            PrintProtocol.Zpl => "zpl",
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "不支持的打印协议。")
        };

    private static string GetCustomProtocolExtension(string protocolId)
    {
        var builder = new StringBuilder(protocolId.Length);
        foreach (var character in protocolId)
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        return builder.Length == 0 ? "bin" : builder.ToString();
    }

    private static string GetSegmentFileName(int pageIndex, int segmentIndex, string extension)
        => segmentIndex == 0
            ? $"page-{pageIndex + 1:000}.{extension}"
            : $"page-{pageIndex + 1:000}-segment-{segmentIndex + 1:000}.{extension}";

    private static Dictionary<string, byte[]> DecodeResources(Dictionary<string, string>? encodedResources,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var decoded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (encodedResources is null) return decoded;
        if (encodedResources.Count > MaxResourceCount)
            throw new ArgumentException($"资源数量不能超过 {MaxResourceCount} 个。");

        long totalBytes = 0;
        foreach (var pair in encodedResources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 256)
                throw new ArgumentException("资源键长度必须为 1–256 个字符。");
            var value = pair.Value ?? string.Empty;
            long estimated;
            try { estimated = ImageBase64Length(value, cancellationToken); }
            catch (FormatException error) { throw new ArgumentException("图片资源必须使用有效的 Base64 数据。", error); }
            if (estimated > 10 * 1024 * 1024)
                throw new ArgumentException($"资源“{pair.Key}”超过单图 10 MiB 上限。");
            totalBytes = checked(totalBytes + estimated);
            if (totalBytes > MaxTotalResourceBytes)
                throw new ArgumentException("本次请求的图片资源总量超过 32 MiB 上限。");
            var lease = PrintResourceScope.Reserve(estimated, "Host.ImageBase64");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytes = Convert.FromBase64String(value);
                cancellationToken.ThrowIfCancellationRequested();
                decoded.Add(pair.Key, bytes);
            }
            catch (FormatException error)
            {
                lease.Dispose();
                throw new ArgumentException("图片资源必须使用有效的 Base64 数据。", error);
            }
            catch { lease.Dispose(); throw; }
        }
        return decoded;
    }

    private static long ImageBase64Length(string value, CancellationToken cancellationToken)
    {
        long characters = 0;
        var previous = '\0';
        var last = '\0';
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 8191) == 0) cancellationToken.ThrowIfCancellationRequested();
            var character = value[index];
            // 保留 Convert.FromBase64String 对四种 ASCII 空白的兼容行为。
            if (character is ' ' or '\t' or '\r' or '\n') continue;
            characters++;
            previous = last;
            last = character;
        }
        if (characters % 4 != 0) throw new FormatException("Base64 长度无效。");
        return checked(characters / 4 * 3 - (last == '=' ? previous == '=' ? 2 : 1 : 0));
    }

    private static void WriteBytes(Stream target, byte[] bytes, CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < bytes.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(65536, bytes.Length - offset);
            target.Write(bytes, offset, count);
            offset += count;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static JsonNode ToNode(object value)
        => JsonNode.Parse(JsonConvert.SerializeObject(value, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.None
        }))!;

    internal static IResult Error(Exception error)
    {
        var renderError = DesignerError.Find<TemplateRenderException>(error);
        var admissionError = error as DesignerAdmissionRejectedException;
        var description = DesignerError.Describe(error);
        var resourceError = DesignerError.Find<PrintResourceLimitException>(error);
        var path = renderError?.SourcePath;
        return Results.Json(new
        {
            contract = DesignerContract.Description,
            errors = new[] { new
            {
                sourcePath = path,
                definitionPath = renderError?.DefinitionPath,
                componentInstancePath = renderError?.ComponentInstancePath,
                instanceIndices = renderError?.InstanceIndices,
                pageIndex = renderError?.PageIndex,
                elementIndex = renderError?.ElementIndex,
                code = description.Code,
                phase = description.Phase,
                fieldPath = description.FieldPath,
                reason = admissionError?.Reason,
                resourceName = resourceError?.ResourceName ?? renderError?.ResourceName,
                sourcePixelWidth = renderError?.SourcePixelWidth,
                sourcePixelHeight = renderError?.SourcePixelHeight,
                renderedPixelWidth = renderError?.RenderedPixelWidth,
                renderedPixelHeight = renderError?.RenderedPixelHeight,
                cropX = renderError?.CropX,
                cropY = renderError?.CropY,
                cropWidth = renderError?.CropWidth,
                cropHeight = renderError?.CropHeight,
                requestedBytes = resourceError?.RequestedBytes,
                currentBytes = resourceError?.CurrentBytes,
                maxBytes = resourceError?.MaxBytes,
                message = description.Message
            } }
        }, WebJson, statusCode: description.StatusCode);
    }

    private sealed record PreparedRequest(Bing.EasyPrint.Model.BpTemplate Template, object? Data,
        ITemplateResourceProvider Resources, TemplateRenderOptions Options);

    private sealed record DesignerSample(string Id, string Name, JsonNode Template, JsonNode Data);

    private sealed class MemoryResources(Dictionary<string, byte[]> resources) : ITemplateResourceProvider
    {
        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return resources.TryGetValue(resourceKey, out var bytes) ? new MemoryStream(bytes, false) : null!;
        }
    }
}

internal sealed class RenderPageView
{
    public int Width { get; init; }
    public int Height { get; init; }
    public PrintPageOutput Output { get; init; } = null!;
    public IReadOnlyList<PrintOutputSegment> Segments => Output.Segments;
    public IReadOnlyList<TemplateLayoutElement> Elements { get; init; } = Array.Empty<TemplateLayoutElement>();
    public IReadOnlyList<PrintElement> RenderElements { get; init; } = Array.Empty<PrintElement>();
    public IReadOnlyList<TemplateLayoutElement> RenderElementOwnership { get; init; } = Array.Empty<TemplateLayoutElement>();
    public int? SourcePageIndex { get; init; }
    public int? CopyIndex { get; init; }
    public bool? FeedAfterPrint { get; init; }
    public bool? CutAfterPrint { get; init; }
}

internal sealed class RenderResultView
{
    public string? ProtocolId { get; init; }
    public PrintProtocol? Protocol { get; init; }
    public IReadOnlyList<RenderPageView> Pages { get; init; } = Array.Empty<RenderPageView>();
    public IReadOnlyList<PrintProtocolControlOutput> FinishingFiles { get; init; } = Array.Empty<PrintProtocolControlOutput>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerBatchRecord
{
    public int Index { get; set; }
    public JsonElement Data { get; set; }
    public int? Copies { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerRequest
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal long EncodedRequestBytes { get; set; }
    public long? MaxWorkingBytes { get; set; }
    public JsonElement Template { get; set; }
    public JsonElement Data { get; set; }
    public DesignerBatchRecord[]? Records { get; set; }
    public int? RecordOffset { get; set; }
    public int? TotalRecords { get; set; }
    public string? CopiesField { get; set; }
    public int? RemainingPages { get; set; }
    public long? RemainingBytes { get; set; }
    public Dictionary<string, string>? Resources { get; set; }
    public string? Protocol { get; set; }
    public string? ProtocolId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] internal bool ProtocolIdSpecified { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] internal bool ProtocolIdIsNull { get; set; }
    public JsonElement? ProtocolConfig { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] internal bool ProtocolConfigSpecified { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] internal bool ProtocolSpecified { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] internal bool FeedLinesSpecified { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] internal bool CutAfterPrintSpecified { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] internal bool FinishingSpecified { get; set; }
    public string? EscPosLayoutMode { get; set; }
    public string? Encoding { get; set; }
    public int? Copies { get; set; }
    public int? FeedLines { get; set; }
    public bool? CutAfterPrint { get; set; }
    public DesignerProtocolFinishingRequest? Finishing { get; set; }
    public DesignerPrintCutRequest? CutOptions { get; set; }
    public DesignerPrintJobRequest? Job { get; set; }
    public bool AllowRasterizedLines { get; set; }
    public int? ImageThreshold { get; set; }
    public double? DeviceHalfCellWidthAt24 { get; set; }
    public double? DeviceFullCellWidthAt24 { get; set; }
    public double? DeviceLineHeightAt24 { get; set; }
    public Dictionary<string, DesignerDeviceFontCalibrationRequest>? DeviceFontCalibrations { get; set; }
    public bool RequireDeclaredNativeBounds { get; set; }
    public bool ValidateGs1Associations { get; set; }
    public DesignerGs1ConversionRequest? Gs1Conversion { get; set; }
    public int? PageIndex { get; set; }
    public int? ElementIndex { get; set; }
    public string[]? ExportFormats { get; set; }
    public int[]? ImageTypes { get; set; }
    public string? SourcePath { get; set; }
    public DesignerEscPosDeviceRequest? EscPosDevice { get; set; }
    public DesignerTsplMediaRequest? TsplMedia { get; set; }
    public DesignerZplMediaRequest? ZplMedia { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerGs1ConversionRequest
{
    public string? Input { get; set; }
    public string? TargetFormat { get; set; }
    public string? BaseUri { get; set; }
    public string? Fragment { get; set; }
    public bool ValidateAssociations { get; set; }
    public DesignerGs1QueryParameter[]? QueryParameters { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerGs1QueryParameter
{
    public string? Key { get; set; }
    public string? Value { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerPrintJobRequest
{
    public int? Copies { get; set; }
    public string? CopyOrder { get; set; }
    public string? FeedScope { get; set; }
    public string? FeedUnit { get; set; }
    public int? FeedAmount { get; set; }
    public string? CutScope { get; set; }
    public DesignerPrintCutRequest? CutOptions { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerPrintCutRequest
{
    public string? Mode { get; set; }
    public bool? FeedToCutPosition { get; set; }
    public int? FeedBeforeCutAmount { get; set; }
    public string? FeedBeforeCutUnit { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerProtocolFinishingRequest
{
    public int? FeedAmount { get; set; }
    public string? FeedUnit { get; set; }
    public bool? Cut { get; set; }
    public string? Placement { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerDeviceFontCalibrationRequest
{
    public double? HalfCellWidthAt24 { get; set; }
    public double? FullCellWidthAt24 { get; set; }
    public double? LineHeightAt24 { get; set; }
    public Dictionary<string, DesignerDeviceTextElementCalibrationRequest>? TextElements { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerDeviceTextElementCalibrationRequest
{
    public double? AdvanceAt24 { get; set; }
    public double? InkLeftAt24 { get; set; }
    public double? InkTopAt24 { get; set; }
    public double? InkRightAt24 { get; set; }
    public double? InkBottomAt24 { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerEscPosDeviceRequest
{
    public string? NativeFlow { get; set; }
    public int? Dpi { get; set; }
    public int? CodePageIndex { get; set; }
    public int? PrintAreaWidthUnits { get; set; }
    public int? NativeTabWidthDots { get; set; }
    public int? RasterBarcodeHumanReadableFontSize { get; set; }
    public DesignerEscPosPrinterProfileRequest? Profile { get; set; }
    public DesignerRasterFontCatalogRequest? RasterFonts { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerEscPosPrinterProfileRequest
{
    public string? Name { get; set; }
    public DesignerEscPosTextProfileRequest? Text { get; set; }
    public string? ImageCommand { get; set; }
    public string? BitImageMode { get; set; }
    public int? HorizontalDpi { get; set; }
    public int? VerticalDpi { get; set; }
    public int? MotionUnitsPerInch { get; set; }
    public int? PrintableWidthDots { get; set; }
    public int? MaxImageBlockBytes { get; set; }
    public bool? SupportsCut { get; set; }
    public string?[]? SupportedCutModes { get; set; }
    public bool? SupportsFeedAndCut { get; set; }
    public bool? SupportsNativeBarcode { get; set; }
    public bool? SupportsNativeBarcodeHumanReadable { get; set; }
    public string?[]? SupportedNativeBarcodeHumanReadableTypes { get; set; }
    public string?[]? SupportedNativeBarcodeHumanReadablePositions { get; set; }
    public bool? SupportsNativeQrCode { get; set; }
    public bool? SupportsPrintAreaWidth { get; set; }
    public bool? SupportsTextSize { get; set; }
    public bool? UnidirectionalBitImage { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerEscPosTextProfileRequest
{
    public int? EncodingCodePage { get; set; }
    public int? CodePageIndex { get; set; }
    public string? CharacterMode { get; set; }
    public DesignerPrintCharacterRangeRequest?[]? CharacterRanges { get; set; }
    public int? HalfCellWidthDots { get; set; }
    public int? FullCellWidthDots { get; set; }
    public int? LineHeightDots { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerPrintCharacterRangeRequest
{
    public int? First { get; set; }
    public int? Last { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerRasterFontCatalogRequest
{
    public string? DefaultFont { get; set; }
    public string[]? FallbackFonts { get; set; }
    public DesignerRasterFontRequest?[]? Fonts { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerRasterFontRequest
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? RegularData { get; set; }
    public string? BoldData { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerTsplMediaRequest
{
    public DesignerTsplPrinterProfileRequest? Profile { get; set; }
    public bool? AllowRasterizedTextStyles { get; set; }
    public int? Dpi { get; set; }
    public int? GapDistance { get; set; }
    public int? GapOffset { get; set; }
    public int? Direction { get; set; }
    public int? ReferenceX { get; set; }
    public int? ReferenceY { get; set; }
    public int? Speed { get; set; }
    public int? Density { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerZplMediaRequest
{
    public DesignerZplPrinterProfileRequest? Profile { get; set; }
    public bool? AllowRasterizedTextStyles { get; set; }
    public int? Dpi { get; set; }
    public int? PrintSpeed { get; set; }
    public string? Orientation { get; set; }
    public string? MediaTracking { get; set; }
    public int? Darkness { get; set; }
}

internal abstract class DesignerLabelPrinterProfileRequest
{
    public string? Name { get; set; }
    public string? FirmwareVersion { get; set; }
    public int? PrintableWidthDots { get; set; }
    public int? MaxLabelHeightDots { get; set; }
    public int? MaxImageDataBytes { get; set; }
    public bool? SupportsImages { get; set; }
    public bool? SupportsGraphics { get; set; }
    public bool? SupportsNativeBarcode { get; set; }
    public bool? SupportsNativeBarcodeHumanReadable { get; set; }
    public string?[]? SupportedNativeBarcodeHumanReadableTypes { get; set; }
    public bool? SupportsNativeQrCode { get; set; }
    public string?[]? SupportedNativeQrCodeRotations { get; set; }
    public bool? SupportsRotation { get; set; }
    public string[]? DeviceFontNames { get; set; }
    public int[]? EncodingCodePages { get; set; }
    public DesignerPrintCharacterRangeRequest?[]? CharacterRanges { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerTsplPrinterProfileRequest : DesignerLabelPrinterProfileRequest
{
    public DesignerTsplCutterRequest? Cutter { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerTsplCutterRequest
{
    public string? Strategy { get; set; }
    public string?[]? SupportedCutModes { get; set; }
    public bool? SupportsEndOfJob { get; set; }
    public int? CutPositionFeedDots { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerZplPrinterProfileRequest : DesignerLabelPrinterProfileRequest
{
    public bool? SupportsCut { get; set; }
    public DesignerZplCutterRequest? Cutter { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerZplCutterRequest
{
    public string? Strategy { get; set; }
    public string?[]? SupportedCutModes { get; set; }
    public bool? SupportsTearOff { get; set; }
    public int? KioskPartialUncutMillimeters { get; set; }
    public int? KioskCutMarginMillimeters { get; set; }
}
