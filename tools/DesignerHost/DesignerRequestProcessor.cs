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

internal static class DesignerRequestProcessor
{
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
            var options = PrepareOptions(request, cancellationToken);
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
                capabilities.SupportsFilledRectangles
            }, WebJson);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    internal static IResult Preview(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Prepare(request, cancellationToken);
            var result = new TemplateRenderer().RenderWithLayout(input.Template, input.Data, input.Resources,
                input.Options, cancellationToken);
            return new BudgetedJsonResult(new
            {
                pages = result.Pages.Select(page => new
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
                        resourceKey = element.ResourceKey
                    })
                })
            }, WebJson);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    internal static IResult Render(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Prepare(request, cancellationToken);
            var result = new TemplateRenderer().RenderWithLayout(input.Template, input.Data, input.Resources,
                input.Options, cancellationToken);
            var extension = GetProtocolFileExtension(result.Protocol);
            var pages = result.Pages.Select((page, pageIndex) => new
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
            var hasIndependentOutputSegments = pages.Any(page => page.Segments.Length > 1);
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
                if (input.Options.Job != null || hasIndependentOutputSegments)
                {
                    var manifest = archive.CreateEntry("job.json", CompressionLevel.Fastest);
                    using var manifestStream = manifest.Open();
                    object manifestModel;
                    if (hasIndependentOutputSegments)
                    {
                        manifestModel = new
                        {
                            outputVersion = 2,
                            preserveFileBoundaries = true,
                            protocol = GetProtocolName(result.Protocol),
                            files = pages.SelectMany(page => page.Segments.Select(segment => new
                            {
                                file = segment.File,
                                pageIndex = page.PageIndex,
                                segmentIndex = segment.SegmentIndex,
                                kind = segment.Kind
                            })),
                            pages = pages.Select(page => new
                            {
                                sourcePageIndex = page.Page.SourcePageIndex,
                                copyIndex = page.Page.CopyIndex,
                                feedAfterPrint = page.Page.FeedAfterPrint,
                                cutAfterPrint = page.Page.CutAfterPrint,
                                segments = page.Segments.Select(segment => new
                                {
                                    file = segment.File,
                                    kind = segment.Kind
                                })
                            })
                        };
                    }
                    else
                    {
                        manifestModel = new
                        {
                            protocol = GetProtocolName(result.Protocol),
                            pages = pages.Select(page => new
                            {
                                file = page.Segments[0].File,
                                sourcePageIndex = page.Page.SourcePageIndex,
                                copyIndex = page.Page.CopyIndex,
                                feedAfterPrint = page.Page.FeedAfterPrint,
                                cutAfterPrint = page.Page.CutAfterPrint
                            })
                        };
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    System.Text.Json.JsonSerializer.Serialize(manifestStream, manifestModel, WebJson);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Results.File(archiveStream.ToArray("Host.ZipOutput"), "application/zip", "easyprint-pages.zip");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
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
        var data = JToken.Parse(dataJson).ToObject<object>();
        cancellationToken.ThrowIfCancellationRequested();
        var resources = DecodeResources(request.Resources, cancellationToken);
        return new PreparedRequest(template, data, new MemoryResources(resources), options);
    }

    private static TemplateRenderOptions PrepareOptions(DesignerRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null) throw new ArgumentNullException(nameof(request));

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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DesignerRequest
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal long EncodedRequestBytes { get; set; }
    public long? MaxWorkingBytes { get; set; }
    public JsonElement Template { get; set; }
    public JsonElement Data { get; set; }
    public Dictionary<string, string>? Resources { get; set; }
    public string? Protocol { get; set; }
    public string? EscPosLayoutMode { get; set; }
    public string? Encoding { get; set; }
    public int? Copies { get; set; }
    public int? FeedLines { get; set; }
    public bool? CutAfterPrint { get; set; }
    public DesignerPrintCutRequest? CutOptions { get; set; }
    public DesignerPrintJobRequest? Job { get; set; }
    public bool AllowRasterizedLines { get; set; }
    public int? ImageThreshold { get; set; }
    public double? DeviceHalfCellWidthAt24 { get; set; }
    public double? DeviceFullCellWidthAt24 { get; set; }
    public double? DeviceLineHeightAt24 { get; set; }
    public Dictionary<string, DesignerDeviceFontCalibrationRequest>? DeviceFontCalibrations { get; set; }
    public bool RequireDeclaredNativeBounds { get; set; }
    public DesignerEscPosDeviceRequest? EscPosDevice { get; set; }
    public DesignerTsplMediaRequest? TsplMedia { get; set; }
    public DesignerZplMediaRequest? ZplMedia { get; set; }
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
