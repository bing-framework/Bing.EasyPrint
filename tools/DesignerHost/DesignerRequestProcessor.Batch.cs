using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static partial class DesignerRequestProcessor
{
    private const int BatchChunkLimit = 25;
    private const int BatchRecordLimit = 1000;
    private const int BatchPageLimit = 1000;
    private const long BatchByteLimit = 32L * 1024 * 1024;

    private static HashSet<string> NormalizeBatchExportFormats(string[]? formats)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        if (formats == null) return normalized;
        foreach (var format in formats)
        {
            var value = format?.Trim().ToLowerInvariant();
            if (value is not ("pdf" or "svg"))
                throw new DesignerOptionException("BatchExportFormat", "批量导出格式仅支持 pdf 与 svg。",
                    "exportFormats");
            if (!normalized.Add(value))
                throw new DesignerOptionException("BatchExportFormatDuplicate", "批量导出格式不能重复。",
                    "exportFormats");
        }
        return normalized;
    }

    internal static IResult BatchRender(DesignerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Records == null || request.Records.Length == 0)
                throw new DesignerOptionException("BatchEmpty", "批量请求必须包含至少一条记录。", "records");
            if (request.Records.Length > BatchChunkLimit)
                throw new DesignerOptionException("BatchChunkTooLarge", "单块最多 25 条记录。", "records");
            if (request.Template.ValueKind != JsonValueKind.Object)
                throw new DesignerOptionException("BatchInvalidTemplate", "template 必须为模板 JSON 对象。", "template");
            if (request.Job != null)
                throw new DesignerOptionException("BatchJobUnsupported",
                    "批量接口不接受旧 job 参数；请使用 copies 或 copiesField。", "job");
            var exportFormats = NormalizeBatchExportFormats(request.ExportFormats);
            var exportPdf = exportFormats.Contains("pdf");
            var exportSvg = exportFormats.Contains("svg");

            var offset = request.RecordOffset ?? 0;
            if (offset < 0 || offset > BatchRecordLimit)
                throw new DesignerOptionException("BatchRecordOffset", "recordOffset 必须在 0–1000 范围内。", "recordOffset");
            if (request.TotalRecords is int totalRecords &&
                (totalRecords < 1 || totalRecords > BatchRecordLimit || offset + request.Records.Length > totalRecords))
                throw new DesignerOptionException("BatchTotalRecords", "totalRecords 必须覆盖当前块且不超过 1000。",
                    "totalRecords");
            if (offset + request.Records.Length > BatchRecordLimit)
                throw new DesignerOptionException("BatchRecordLimit", "整批记录不能超过 1000 条。", "records");

            var remainingPages = request.RemainingPages ?? BatchPageLimit;
            if (remainingPages < 0 || remainingPages > BatchPageLimit)
                throw new DesignerOptionException("BatchPageBudget", "remainingPages 必须在 0–1000 范围内。",
                    "remainingPages");
            var remainingBytes = request.RemainingBytes ?? BatchByteLimit;
            if (remainingBytes < 0 || remainingBytes > BatchByteLimit)
                throw new DesignerOptionException("BatchByteBudget", "remainingBytes 必须在 0–32 MiB 范围内。",
                    "remainingBytes");
            var copiesField = string.IsNullOrWhiteSpace(request.CopiesField) ? null : request.CopiesField.Trim();
            if (copiesField != null && request.Copies.HasValue && request.Copies.Value != 1)
                throw new DesignerOptionException("BatchCopiesConflict",
                    "copiesField 不能与大于 1 的 copies 基础值同时指定。", "copiesField");

            var options = PrepareOptions(request, cancellationToken);
            if (exportPdf && (options.CustomProtocol != null || options.Protocol != Bing.EasyPrint.PrintProtocol.EscPos ||
                options.EscPosLayout != Bing.EasyPrint.EscPosLayoutMode.Raster))
                throw new NotSupportedException("批量 PDF 导出仅支持 ESC/POS Raster。");
            if (exportSvg && options.CustomProtocol != null)
                throw new NotSupportedException("新协议未声明共享码图 SVG 编码。");
            var templateJson = request.Template.GetRawText();
            if (Encoding.UTF8.GetByteCount(templateJson) > MaxDataBytes)
                throw new DesignerOptionException("BatchTemplateTooLarge", "模板 JSON 超过 1 MiB 上限。", "template");
            PrintResourceScope.Retain(checked(templateJson.Length * 2L), "Host.BatchTemplate");
            BpTemplate template;
            try { template = new TemplateLoader().LoadJsonWithPaths(templateJson, options); }
            catch (Exception error) when (error is ArgumentException || error is Newtonsoft.Json.JsonException)
            {
                throw new DesignerTemplateException(error);
            }
            var resources = DecodeResources(request.Resources, cancellationToken);
            var resourceProvider = new MemoryResources(resources);

            var indexes = new HashSet<int>();
            foreach (var record in request.Records)
            {
                if (record == null)
                    throw new DesignerOptionException("BatchInvalidRecord", "记录不能为 null。", "records");
                if (record.Index < 0 || record.Index >= BatchRecordLimit || !indexes.Add(record.Index))
                    throw new DesignerOptionException("BatchInvalidRecordIndex",
                        "记录序号必须在 0–999 内且不能重复。", "records");
                if (record.Data.ValueKind != JsonValueKind.Object)
                    throw new DesignerOptionException("BatchInvalidRecordData",
                        "每条记录 data 必须为对象。", "records");
            }

            var renderer = new TemplateRenderer();
            var baseCopies = options.Copies;
            var records = new List<BatchRecordResult>();
            var errors = new List<BatchErrorRow>();
            var totalPages = 0;
            var totalBytes = 0L;
            var logicalRemaining = remainingBytes;
            using var archiveStream = new BudgetedHostStream("Host.BatchZipBuffer", cancellationToken);
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
            {
                foreach (var record in request.Records)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int copies;
                    try { copies = ResolveCopies(record, request, copiesField, baseCopies); }
                    catch (DesignerOptionException error)
                    {
                        AddRecordError(errors, record.Index, error);
                        records.Add(BatchRecordResult.Failed(record.Index, 1));
                        continue;
                    }
                    var dataJson = record.Data.GetRawText();
                    if (Encoding.UTF8.GetByteCount(dataJson) > MaxDataBytes)
                        throw new DesignerOptionException("BatchRecordTooLarge",
                            $"记录 {record.Index} 的 data 超过 1 MiB 上限。", "records");
                    object data;
                    try
                    {
                        var raw = BuildRawData(dataJson)!;
                        var legacy = ParseDataToken(dataJson, Newtonsoft.Json.DateParseHandling.DateTime,
                            Newtonsoft.Json.FloatParseHandling.Double).ToObject<object>()!;
                        data = new TemplateDataPair(legacy, raw);
                    }
                    catch (Exception error) when (error is Newtonsoft.Json.JsonException || error is ArgumentException)
                    {
                        AddRecordError(errors, record.Index, error);
                        records.Add(BatchRecordResult.Failed(record.Index, copies));
                        continue;
                    }

                    options.Copies = copies;
                    TemplateRenderResult? printResult = null;
                    RenderResultView view = null!;
                    var exportEntries = new List<(string File, byte[] Data, BatchFileResult Result)>();
                    var exportedBytes = 0L;
                    var printBytes = 0L;
                    var physicalPages = 0;
                    try
                    {
                        if (options.CustomProtocol != null)
                            view = View(renderer.RenderCustomProtocolWithLayout(template, data, resourceProvider, options,
                                cancellationToken));
                        else if (exportPdf || exportSvg)
                        {
                            printResult = renderer.RenderWithLayoutAndPrimitives(template, data, resourceProvider,
                                options, cancellationToken);
                            view = View(printResult);
                        }
                        else view = View(renderer.RenderWithLayout(template, data, resourceProvider, options,
                            cancellationToken));

                        physicalPages = options.CustomProtocol != null
                            ? view.Pages.Count
                            : checked(view.Pages.Count * copies);
                        if ((long)totalPages + physicalPages > remainingPages)
                            throw new DesignerOptionException("BatchPageLimit",
                                $"批次实际物理页数超过剩余页数上限 {remainingPages}。", "records");
                        printBytes = checked(view.Pages.Sum(page => page.Output.ByteLength) +
                            view.FinishingFiles.Sum(control => control.Output.ByteLength));

                        if (exportPdf)
                        {
                            var available = remainingBytes - totalBytes - printBytes;
                            if (available < 0)
                                throw new DesignerOptionException("BatchByteBudget",
                                    $"批次累计输出超过剩余字节上限 {remainingBytes}。", "records");
                            var maxPdfBytes = checked((int)Math.Min(options.MaxOutputBytes, available));
                            var pdf = EncodePagePdf(printResult!.Pages, options, null, maxPdfBytes, cancellationToken);
                            var pdfPageCount = checked(view.Pages.Count * copies);
                            var file = $"records/{record.Index:000000}/exports/pdf/record.pdf";
                            exportEntries.Add((file, pdf, new BatchFileResult(file, null, 0, "Export", null,
                                null, null, "pdf", null, pdfPageCount, copies)));
                            exportedBytes = checked(exportedBytes + pdf.LongLength);
                        }

                        if (exportSvg)
                        {
                            var svgCapabilities = TemplateProtocolCapabilities.Create(options);
                            var dpiX = RasterDpiX(options, svgCapabilities);
                            var dpiY = RasterDpiY(options, svgCapabilities);
                            if (dpiX <= 0) dpiX = 203;
                            if (dpiY <= 0) dpiY = 203;
                            var symbolCount = 0;
                            foreach (var (page, pageIndex) in printResult!.Pages.Select((page, index) => (page, index)))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                for (var elementIndex = 0; elementIndex < page.Elements.Count; elementIndex++)
                                {
                                    var layout = page.Elements[elementIndex];
                                    if (layout.Type is not (3 or 4 or 10 or 11)) continue;
                                    var owned = new List<PrintElement>();
                                    for (var index = 0; index < page.RenderElements.Count; index++)
                                        if (index < page.RenderElementOwnership.Count &&
                                            ReferenceEquals(page.RenderElementOwnership[index], layout))
                                            owned.Add(page.RenderElements[index]);
                                    if (owned.Count == 0) continue;
                                    if (owned.Count != 1)
                                        throw new NotSupportedException(
                                            $"批量码图 {layout.SourcePath} 没有唯一的共享矢量符号。");
                                    var available = remainingBytes - totalBytes - printBytes - exportedBytes;
                                    if (available < 0)
                                        throw new DesignerOptionException("BatchByteBudget",
                                            $"批次累计输出超过剩余字节上限 {remainingBytes}。", "records");
                                    var maxSvgBytes = checked((int)Math.Min(options.MaxOutputBytes, available));
                                    var svg = EncodeBarcodeSvg(owned[0], dpiX, dpiY, maxSvgBytes, cancellationToken);
                                    var copyBytes = checked(svg.LongLength * copies);
                                    if (copyBytes > available)
                                        throw new DesignerOptionException("BatchByteBudget",
                                            $"批量 SVG 输出超过剩余字节上限 {available}。", "records");
                                    for (var copyIndex = 0; copyIndex < copies; copyIndex++)
                                    {
                                        var file = $"records/{record.Index:000000}/exports/svg/copy-{copyIndex + 1:0000}/" +
                                            $"page-{pageIndex + 1:0000}-element-{elementIndex + 1:0000}.svg";
                                        exportEntries.Add((file, svg, new BatchFileResult(file, pageIndex, 0, "Export",
                                            page.SourcePageIndex, copyIndex, null, "svg", elementIndex)));
                                    }
                                    exportedBytes = checked(exportedBytes + copyBytes);
                                    symbolCount++;
                                }
                            }
                            if (symbolCount == 0)
                                throw new NotSupportedException("模板没有可导出的软件码图元素。");
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception error) when (error is PrintResourceLimitException) { throw; }
                    catch (Exception error) when (IsRecordFailure(error))
                    {
                        AddRecordError(errors, record.Index, error);
                        records.Add(BatchRecordResult.Failed(record.Index, copies));
                        continue;
                    }
                    finally
                    {
                        printResult?.ReleaseRenderElements();
                    }

                    var recordBytes = checked(printBytes + exportedBytes);
                    if (recordBytes > remainingBytes - totalBytes)
                        throw new DesignerOptionException("BatchByteBudget",
                            $"批次累计输出超过剩余字节上限 {remainingBytes}。", "records");
                    if (recordBytes > 0) PrintResourceScope.Retain(recordBytes, "Host.BatchRawOutput");

                    var recordDir = $"records/{record.Index:000000}";
                    var extension = view.ProtocolId == null
                        ? GetProtocolFileExtension(view.Protocol!.Value)
                        : GetCustomProtocolExtension(view.ProtocolId);
                    var files = new List<BatchFileResult>();
                    var orderedEntries = new List<(string File, byte[] Data, BatchFileResult Result)>();
                    var controls = view.FinishingFiles.Select((control, index) => (control, index)).ToArray();
                    foreach (var (page, pageIndex) in view.Pages.Select((page, index) => (page, index)))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        foreach (var (segment, segmentIndex) in page.Segments.Select((segment, index) => (segment, index)))
                        {
                            var file = segmentIndex == 0
                                ? $"{recordDir}/page-{pageIndex + 1:0000}.{extension}"
                                : $"{recordDir}/page-{pageIndex + 1:0000}-{segmentIndex + 1:000}.{extension}";
                            orderedEntries.Add((file, segment.Data, new BatchFileResult(file, pageIndex, segmentIndex,
                                segment.Kind.ToString(), page.SourcePageIndex, page.CopyIndex, null)));
                        }
                        foreach (var (control, controlIndex) in controls.Where(item => item.control.PageIndex == pageIndex))
                            AddControlEntries(control, controlIndex);
                    }
                    foreach (var (control, controlIndex) in controls.Where(item => item.control.PageIndex == null))
                        AddControlEntries(control, controlIndex);
                    orderedEntries.AddRange(exportEntries);
                    foreach (var entry in orderedEntries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        WriteEntry(archive, entry.File, entry.Data, cancellationToken);
                        files.Add(entry.Result);
                    }

                    void AddControlEntries(PrintProtocolControlOutput control, int controlIndex)
                    {
                        foreach (var (segment, segmentIndex) in control.Output.Segments.Select((segment, index) => (segment, index)))
                        {
                            var file = control.Output.Segments.Count == 1
                                ? $"{recordDir}/finish-{controlIndex + 1:0000}.{extension}"
                                : $"{recordDir}/finish-{controlIndex + 1:0000}-{segmentIndex + 1:000}.{extension}";
                            orderedEntries.Add((file, segment.Data, new BatchFileResult(file, control.PageIndex,
                                segmentIndex, PrintOutputSegmentKind.Control.ToString(), null, null,
                                control.Placement.ToString())));
                        }
                    }

                    totalPages = checked(totalPages + physicalPages);
                    totalBytes = checked(totalBytes + recordBytes);
                    records.Add(BatchRecordResult.Succeeded(record.Index, copies, physicalPages, recordBytes,
                        options.CustomProtocol != null, files));
                }

                var failed = errors.Count;
                var succeeded = records.Count(record => record.Status == "completed");
                var status = failed == 0 ? "completed" : succeeded == 0 ? "all_failed" : "partial";
                var errorBytes = CountErrorsCsvBytes(errors);
                logicalRemaining = remainingBytes - totalBytes - errorBytes;
                BatchManifest manifest = null!;
                long manifestBytes = 0;
                for (var attempt = 0; attempt < 4; attempt++)
                {
                    manifest = CreateManifest(status, offset, request, totalPages, totalBytes, remainingPages,
                        logicalRemaining, records, errors);
                    manifestBytes = CountJsonBytes(manifest);
                    var next = remainingBytes - totalBytes - errorBytes - manifestBytes;
                    if (next == logicalRemaining) break;
                    logicalRemaining = next;
                }
                if (logicalRemaining < 0)
                    throw new DesignerOptionException("BatchByteBudget",
                        $"批量清单和错误表超过剩余字节上限 {remainingBytes}。", "records");
                using var metadataLease = PrintResourceScope.Reserve(checked(errorBytes + manifestBytes + 4096L),
                    "Host.BatchMetadata");
                WriteEntry(archive, "manifest.json",
                    System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(manifest, WebJson), cancellationToken);
                WriteEntry(archive, "errors.csv", BuildErrorsCsv(errors, errorBytes), cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (archiveStream.Length > remainingBytes)
                throw new DesignerOptionException("BatchByteBudget",
                    $"批次压缩结果超过剩余字节上限 {remainingBytes}。", "records");
            var bytes = archiveStream.ToArray("Host.BatchZipOutput");
            var response = Results.File(bytes, "application/zip", "easyprint-batch.zip");
            return new BatchFileResultWithHeaders(response, records.Count, totalPages, totalBytes,
                errors.Count, remainingPages - totalPages, logicalRemaining);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    private static int ResolveCopies(DesignerBatchRecord record, DesignerRequest request, string? copiesField,
        int fallback)
    {
        if (copiesField == null)
        {
            if (record.Copies.HasValue)
            {
                if (request.Copies.HasValue && request.Copies.Value != 1)
                    throw new DesignerOptionException("BatchCopiesConflict",
                        "记录 copies 不能与大于 1 的请求 copies 同时指定。", "copies");
                return ValidateCopies(record.Copies.Value, $"记录 {record.Index}");
            }
            return fallback;
        }
        if (record.Copies.HasValue)
            throw new DesignerOptionException("BatchCopiesConflict",
                "记录 copies 不能与 copiesField 同时指定。", "copies");
        try
        {
            var token = ParseDataToken(record.Data.GetRawText(), DateParseHandling.None, FloatParseHandling.Double)
                .SelectToken(copiesField, false);
            if (token == null || token.Type == JTokenType.Null)
                throw new ArgumentException($"记录 {record.Index} 的份数字段 {copiesField} 不存在。");
            long value;
            if (token.Type == JTokenType.Integer)
                value = token.Value<long>();
            else if (token.Type == JTokenType.String &&
                     long.TryParse(token.Value<string>(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                value = parsed;
            else
                throw new ArgumentException($"记录 {record.Index} 的份数字段必须是整数。");
            if (value < 1 || value > BatchPageLimit)
                throw new ArgumentException($"记录 {record.Index} 的份数必须在 1–1000 范围内。");
            return (int)value;
        }
        catch (Exception error) when (error is ArgumentException || error is FormatException ||
                                       error is OverflowException || error is Newtonsoft.Json.JsonException)
        {
            throw new DesignerOptionException("BatchInvalidCopies",
                $"记录 {record.Index} 的份数字段无效：{error.Message}", "copiesField");
        }
    }

    private static int ValidateCopies(int copies, string label)
        => copies is >= 1 and <= BatchPageLimit
            ? copies
            : throw new DesignerOptionException("BatchInvalidCopies", $"{label} 的份数必须在 1–1000 范围内。",
                "copies");

    private static bool IsRecordFailure(Exception error)
    {
        if (DesignerError.Find<OperationCanceledException>(error) != null) return false;
        if (DesignerError.Find<PrintResourceLimitException>(error) != null) return false;
        if (TemplateInputDiagnostic.IsMarked(error)) return true;
        if (error is DesignerOptionException) return false;
        if (error is TemplateRenderException render)
        {
            if (DesignerError.Find<InvalidOperationException>(error) != null) return false;
            if (render.FieldPath != null) return true;
            return DesignerError.Find<NotSupportedException>(error) != null ||
                DesignerError.Find<PrintCapabilityException>(error) != null ||
                DesignerError.Find<Newtonsoft.Json.JsonException>(error) != null ||
                DesignerError.Find<FormatException>(error) != null ||
                DesignerError.Find<OverflowException>(error) != null;
        }
        if (DesignerError.Find<InvalidOperationException>(error) != null) return false;
        return error is NotSupportedException || error is PrintCapabilityException ||
            error is Newtonsoft.Json.JsonException;
    }

    private static void AddRecordError(List<BatchErrorRow> errors, int index, Exception error)
    {
        var description = DesignerError.Describe(error);
        var render = DesignerError.Find<TemplateRenderException>(error);
        errors.Add(new BatchErrorRow
        {
            RecordIndex = index,
            Code = description.Code,
            Phase = description.Phase,
            FieldPath = render?.FieldPath ?? description.FieldPath,
            SourcePath = render?.SourcePath,
            Message = description.Message
        });
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        WriteBytes(stream, bytes, cancellationToken);
    }

    private static BatchManifest CreateManifest(string status, int offset, DesignerRequest request, int totalPages,
        long totalBytes, int remainingPages, long remainingBytes, IReadOnlyList<BatchRecordResult> records,
        IReadOnlyList<BatchErrorRow> errors)
        => new()
        {
            Status = status,
            RecordOffset = offset,
            TotalRecords = request.TotalRecords ?? checked(offset + (request.Records?.Length ?? 0)),
            Succeeded = records.Count(record => record.Status == "completed"),
            Failed = errors.Count,
            PhysicalPages = totalPages,
            ByteLength = totalBytes,
            RemainingPages = remainingPages - totalPages,
            RemainingBytes = remainingBytes,
            Records = records,
            Errors = errors
        };

    private static long CountJsonBytes<T>(T value)
    {
        using var counter = new CountingBufferWriter();
        using (var writer = new Utf8JsonWriter(counter, new JsonWriterOptions { Encoder = WebJson.Encoder }))
            System.Text.Json.JsonSerializer.Serialize(writer, value, WebJson);
        return counter.Written;
    }

    private static long CountErrorsCsvBytes(IReadOnlyList<BatchErrorRow> errors)
    {
        var total = 3L + Encoding.UTF8.GetByteCount("recordIndex,code,phase,fieldPath,sourcePath,message") +
            Encoding.UTF8.GetByteCount(Environment.NewLine);
        foreach (var error in errors)
        {
            total += IntegerBytes(error.RecordIndex) + 1;
            total += CsvBytes(error.Code) + 1;
            total += CsvBytes(error.Phase) + 1;
            total += CsvBytes(error.FieldPath) + 1;
            total += CsvBytes(error.SourcePath) + 1;
            total += CsvBytes(error.Message) + Encoding.UTF8.GetByteCount(Environment.NewLine);
        }
        return total;
    }

    private static long CsvBytes(string? value)
    {
        value ??= string.Empty;
        var total = 2L;
        if (value.Length > 0 && "=+-@\t\r".IndexOf(value[0]) >= 0) total++;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '"') { total += 2; continue; }
            if (character < 0x80) total++;
            else if (character < 0x800) total += 2;
            else if (char.IsHighSurrogate(character) && index + 1 < value.Length &&
                     char.IsLowSurrogate(value[index + 1]))
            {
                total += 4;
                index++;
            }
            else total += 3;
        }
        return total;
    }

    private static long IntegerBytes(int value)
    {
        var total = 1L;
        while (value >= 10) { value /= 10; total++; }
        return total;
    }

    private sealed class CountingBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[] _buffer;
        private IDisposable _lease;
        internal long Written { get; private set; }

        internal CountingBufferWriter()
        {
            _lease = PrintResourceScope.Reserve(4096, "Host.BatchMetadataScratch");
            try { _buffer = new byte[4096]; }
            catch { _lease.Dispose(); throw; }
        }

        public void Advance(int count) => Written += count;
        public Memory<byte> GetMemory(int sizeHint = 0) { Ensure(sizeHint); return _buffer; }
        public Span<byte> GetSpan(int sizeHint = 0) { Ensure(sizeHint); return _buffer; }

        private void Ensure(int sizeHint)
        {
            if (sizeHint <= _buffer.Length) return;
            var size = Math.Max(sizeHint, _buffer.Length * 2);
            var lease = PrintResourceScope.Reserve(size, "Host.BatchMetadataScratch");
            try { _buffer = new byte[size]; }
            catch { lease.Dispose(); throw; }
            _lease.Dispose();
            _lease = lease;
        }

        public void Dispose()
        {
            _lease.Dispose();
        }
    }

    private static byte[] BuildErrorsCsv(IReadOnlyList<BatchErrorRow> errors, long exactBytes)
    {
        var bytes = new byte[checked((int)exactBytes)];
        var offset = 0;
        WriteUtf8(bytes, ref offset, "\uFEFFrecordIndex,code,phase,fieldPath,sourcePath,message" + Environment.NewLine);
        foreach (var error in errors)
        {
            WriteUtf8(bytes, ref offset, error.RecordIndex.ToString(CultureInfo.InvariantCulture));
            WriteByte(bytes, ref offset, (byte)',');
            WriteCsvCell(bytes, ref offset, error.Code);
            WriteByte(bytes, ref offset, (byte)',');
            WriteCsvCell(bytes, ref offset, error.Phase);
            WriteByte(bytes, ref offset, (byte)',');
            WriteCsvCell(bytes, ref offset, error.FieldPath);
            WriteByte(bytes, ref offset, (byte)',');
            WriteCsvCell(bytes, ref offset, error.SourcePath);
            WriteByte(bytes, ref offset, (byte)',');
            WriteCsvCell(bytes, ref offset, error.Message);
            WriteUtf8(bytes, ref offset, Environment.NewLine);
        }
        if (offset != bytes.Length) throw new InvalidOperationException("CSV 预算计数与实际输出不一致。");
        return bytes;
    }

    private static void WriteCsvCell(byte[] destination, ref int offset, string? value)
    {
        value ??= string.Empty;
        WriteByte(destination, ref offset, (byte)'"');
        if (value.Length > 0 && "=+-@\t\r".IndexOf(value[0]) >= 0)
            WriteByte(destination, ref offset, (byte)'\'');
        var start = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '"') continue;
            if (index > start) WriteUtf8(destination, ref offset, value.AsSpan(start, index - start));
            WriteByte(destination, ref offset, (byte)'"');
            WriteByte(destination, ref offset, (byte)'"');
            start = index + 1;
        }
        if (start < value.Length) WriteUtf8(destination, ref offset, value.AsSpan(start));
        WriteByte(destination, ref offset, (byte)'"');
    }

    private static void WriteByte(byte[] destination, ref int offset, byte value)
        => destination[offset++] = value;

    private static void WriteUtf8(byte[] destination, ref int offset, string value)
        => WriteUtf8(destination, ref offset, value.AsSpan());

    private static void WriteUtf8(byte[] destination, ref int offset, ReadOnlySpan<char> value)
        => offset += Encoding.UTF8.GetBytes(value, destination.AsSpan(offset));


    private sealed class BatchManifest
    {
        public string Status { get; set; } = null!;
        public int RecordOffset { get; set; }
        public int TotalRecords { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public int PhysicalPages { get; set; }
        public long ByteLength { get; set; }
        public int RemainingPages { get; set; }
        public long RemainingBytes { get; set; }
        public IReadOnlyList<BatchRecordResult> Records { get; set; } = Array.Empty<BatchRecordResult>();
        public IReadOnlyList<BatchErrorRow> Errors { get; set; } = Array.Empty<BatchErrorRow>();
    }

    private sealed class BatchRecordResult
    {
        public int Index { get; set; }
        public string Status { get; set; } = null!;
        public int Copies { get; set; }
        public int PageCount { get; set; }
        public long ByteLength { get; set; }
        public bool CopiedByProtocol { get; set; }
        public IReadOnlyList<BatchFileResult> Files { get; set; } = Array.Empty<BatchFileResult>();

        internal static BatchRecordResult Failed(int index, int copies)
            => new() { Index = index, Status = "failed", Copies = copies };

        internal static BatchRecordResult Succeeded(int index, int copies, int pages, long bytes,
            bool copiedByProtocol, IReadOnlyList<BatchFileResult> files)
            => new()
            {
                Index = index, Status = "completed", Copies = copies, PageCount = pages,
                ByteLength = bytes, CopiedByProtocol = copiedByProtocol, Files = files
            };
    }

    private sealed class BatchFileResult
    {
        public BatchFileResult(string file, int? pageIndex, int segmentIndex, string kind, int? sourcePageIndex,
            int? copyIndex, string? placement, string? format = null, int? elementIndex = null,
            int? pageCount = null, int? copies = null)
        {
            File = file; PageIndex = pageIndex; SegmentIndex = segmentIndex; Kind = kind;
            SourcePageIndex = sourcePageIndex; CopyIndex = copyIndex; Placement = placement;
            Format = format; ElementIndex = elementIndex; PageCount = pageCount; Copies = copies;
        }

        public string File { get; }
        public int? PageIndex { get; }
        public int SegmentIndex { get; }
        public string Kind { get; }
        public int? SourcePageIndex { get; }
        public int? CopyIndex { get; }
        public string? Placement { get; }
        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Format { get; }
        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? ElementIndex { get; }
        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? PageCount { get; }
        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? Copies { get; }
    }

    private sealed class BatchErrorRow
    {
        public int RecordIndex { get; set; }
        public string? Code { get; set; }
        public string? Phase { get; set; }
        public string? FieldPath { get; set; }
        public string? SourcePath { get; set; }
        public string? Message { get; set; }
    }

    private sealed class BatchFileResultWithHeaders : IResult
    {
        private readonly IResult _inner;
        private readonly int _records;
        private readonly int _pages;
        private readonly long _bytes;
        private readonly int _failed;
        private readonly int _remainingPages;
        private readonly long _remainingBytes;

        internal BatchFileResultWithHeaders(IResult inner, int records, int pages, long bytes, int failed,
            int remainingPages, long remainingBytes)
        {
            _inner = inner; _records = records; _pages = pages; _bytes = bytes; _failed = failed;
            _remainingPages = remainingPages; _remainingBytes = remainingBytes;
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers["X-EasyPrint-Batch-Records"] = _records.ToString(System.Globalization.CultureInfo.InvariantCulture);
            httpContext.Response.Headers["X-EasyPrint-Batch-Pages"] = _pages.ToString(System.Globalization.CultureInfo.InvariantCulture);
            httpContext.Response.Headers["X-EasyPrint-Batch-Bytes"] = _bytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            httpContext.Response.Headers["X-EasyPrint-Batch-Failed"] = _failed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            httpContext.Response.Headers["X-EasyPrint-Batch-Remaining-Pages"] = _remainingPages.ToString(System.Globalization.CultureInfo.InvariantCulture);
            httpContext.Response.Headers["X-EasyPrint-Batch-Remaining-Bytes"] = _remainingBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return _inner.ExecuteAsync(httpContext);
        }
    }
}
