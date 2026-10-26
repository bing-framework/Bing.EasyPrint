using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("HostProtocolState")]
public sealed class BatchHostTests
{
    private const string Template =
        "{\"Version\":2,\"Width\":200,\"Height\":80,\"Rows\":[{\"Height\":80,\"Cells\":[{\"Width\":200,\"Elements\":[" +
        "{\"Type\":1,\"FontSize\":24,\"Field\":{\"Name\":\"Name\"}}]}]}]}";

    static BatchHostTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public async Task Batch_single_record_returns_stable_record_directory_and_manifest()
    {
        var context = await RenderBatch(Records(0, 1), ",\"totalRecords\":1");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = OpenArchive(context);
        var manifest = ReadManifest(archive);
        Assert.Equal("completed", manifest.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, manifest.RootElement.GetProperty("succeeded").GetInt32());
        Assert.Contains("records/000000/page-0001.cpcl", archive.Entries.Select(entry => entry.FullName));
        Assert.Contains("manifest.json", archive.Entries.Select(entry => entry.FullName));
        Assert.Contains("errors.csv", archive.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public async Task Batch_opt_in_pdf_and_svg_exports_are_bounded_manifested_and_copy_aware()
    {
        const string barcode = "{\"Version\":2,\"Width\":400,\"Height\":120,\"Rows\":[{\"Height\":120,\"Cells\":[{\"Width\":400,\"Elements\":[" +
            "{\"Type\":4,\"BarcodeType\":0,\"Height\":40,\"HumanReadable\":true,\"HumanReadableReserveHeight\":16,\"Field\":{\"Name\":\"Value\"}}]}]}]}";
        var context = CreateContext("{\"template\":" + barcode + ",\"data\":{},\"protocol\":\"EscPos\",\"escPosLayoutMode\":\"Raster\"," +
            "\"records\":[" + Record(0, "{\"Value\":\"F5-12345\"}") + "],\"totalRecords\":1,\"copies\":2,\"exportFormats\":[\"pdf\",\"svg\"]}");

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = OpenArchive(context);
        var manifest = ReadManifest(archive).RootElement;
        var record = manifest.GetProperty("records")[0];
        Assert.Equal("completed", record.GetProperty("status").GetString());
        var files = record.GetProperty("files").EnumerateArray().ToArray();
        var pdf = Assert.Single(files.Where(file => file.TryGetProperty("format", out var format) && format.GetString() == "pdf"));
        Assert.Equal(2, pdf.GetProperty("pageCount").GetInt32());
        Assert.Equal(2, pdf.GetProperty("copies").GetInt32());
        var svgs = files.Where(file => file.TryGetProperty("format", out var format) && format.GetString() == "svg").ToArray();
        Assert.Equal(2, svgs.Length);
        Assert.All(svgs, file => Assert.Equal(0, file.GetProperty("elementIndex").GetInt32()));
        Assert.Equal(new[] { 0, 1 }, svgs.Select(file => file.GetProperty("copyIndex").GetInt32()).Order().ToArray());
        Assert.Contains("records/000000/exports/pdf/record.pdf", archive.Entries.Select(entry => entry.FullName));
        var pdfBytes = ReadEntryBytes(archive, "records/000000/exports/pdf/record.pdf");
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdfBytes, 0, 5));
        foreach (var svg in svgs)
        {
            var path = svg.GetProperty("file").GetString()!;
            var content = ReadEntry(archive, path);
            Assert.Contains("<path", content);
            Assert.DoesNotContain("<image", content);
            Assert.DoesNotContain("F5-12345", content);
        }
        Assert.True(record.GetProperty("byteLength").GetInt64() >= pdfBytes.Length +
            svgs.Sum(file => archive.GetEntry(file.GetProperty("file").GetString()!)!.Length));
    }

    [Fact]
    public async Task Batch_chunk_boundaries_and_total_limit_are_enforced()
    {
        var chunk25 = await RenderBatch(Records(0, 25), ",\"totalRecords\":25,\"remainingPages\":1000");
        Assert.Equal(StatusCodes.Status200OK, chunk25.Response.StatusCode);
        var chunk26 = await RenderBatch(Records(0, 26), ",\"totalRecords\":26");
        Assert.Equal(StatusCodes.Status400BadRequest, chunk26.Response.StatusCode);

        var last = await RenderBatch(Records(975, 25), ",\"totalRecords\":1000,\"recordOffset\":975");
        Assert.Equal(StatusCodes.Status200OK, last.Response.StatusCode);
        var over = await RenderBatch(Records(976, 25), ",\"totalRecords\":1001,\"recordOffset\":976");
        Assert.Equal(StatusCodes.Status400BadRequest, over.Response.StatusCode);
    }

    [Fact]
    public async Task Batch_26_records_cross_two_real_chunks_and_1000_records_cross_forty_chunks()
    {
        var first = await RenderBatch(Records(0, 25), ",\"totalRecords\":26,\"remainingPages\":1000,\"remainingBytes\":33554432");
        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        using (var archive = OpenArchive(first))
            Assert.Equal(25, ReadManifest(archive).RootElement.GetProperty("physicalPages").GetInt32());
        var second = await RenderBatch(Records(25, 1),
            ",\"totalRecords\":26,\"recordOffset\":25,\"remainingPages\":975,\"remainingBytes\":33550000");
        Assert.Equal(StatusCodes.Status200OK, second.Response.StatusCode);
        using (var archive = OpenArchive(second))
        {
            var manifest = ReadManifest(archive).RootElement;
            Assert.Equal(1, manifest.GetProperty("physicalPages").GetInt32());
            Assert.Equal(25, manifest.GetProperty("recordOffset").GetInt32());
        }

        var pagesRemaining = 1000;
        long bytesRemaining = 32L * 1024 * 1024;
        for (var offset = 0; offset < 1000; offset += 25)
        {
            var context = await RenderBatch(Records(offset, 25),
                ",\"totalRecords\":1000,\"recordOffset\":" + offset + ",\"remainingPages\":" + pagesRemaining +
                ",\"remainingBytes\":" + bytesRemaining);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            using var archive = OpenArchive(context);
            var manifest = ReadManifest(archive).RootElement;
            pagesRemaining -= manifest.GetProperty("physicalPages").GetInt32();
            bytesRemaining = manifest.GetProperty("remainingBytes").GetInt64();
            Assert.Equal(bytesRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture),
                context.Response.Headers["X-EasyPrint-Batch-Remaining-Bytes"].ToString());
        }
        Assert.Equal(0, pagesRemaining);
        Assert.True(bytesRemaining < 32L * 1024 * 1024);
    }

    [Fact]
    public async Task Batch_partial_and_all_failed_are_distinct_and_never_claim_success()
    {
        var partial = await RenderBatch(
            "[" + Record(0, "{\"Name\":\"ok\"}") + "," + Record(1, "{}") + "]",
            ",\"totalRecords\":2");
        Assert.Equal(StatusCodes.Status200OK, partial.Response.StatusCode);
        using var partialArchive = OpenArchive(partial);
        var partialManifest = ReadManifest(partialArchive).RootElement;
        Assert.Equal("partial", partialManifest.GetProperty("status").GetString());
        Assert.Equal(1, partialManifest.GetProperty("succeeded").GetInt32());
        Assert.Equal(1, partialManifest.GetProperty("failed").GetInt32());
        Assert.DoesNotContain("records/000001", partialArchive.Entries.Select(entry => entry.FullName));
        Assert.Contains("Name", ReadEntry(partialArchive, "errors.csv"));

        var failed = await RenderBatch(
            "[" + Record(0, "{}") + "," + Record(1, "{}") + "]",
            ",\"totalRecords\":2");
        Assert.Equal(StatusCodes.Status200OK, failed.Response.StatusCode);
        using var failedArchive = OpenArchive(failed);
        var failedManifest = ReadManifest(failedArchive).RootElement;
        Assert.Equal("all_failed", failedManifest.GetProperty("status").GetString());
        Assert.Equal(0, failedManifest.GetProperty("succeeded").GetInt32());
        Assert.DoesNotContain(failedArchive.Entries.Select(entry => entry.FullName),
            name => name.StartsWith("records/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Batch_uses_remaining_page_and_byte_budgets()
    {
        var pageLimited = await RenderBatch(Records(0, 11), ",\"totalRecords\":11,\"remainingPages\":10");
        Assert.Equal(StatusCodes.Status400BadRequest, pageLimited.Response.StatusCode);
        Assert.Contains("BatchPageLimit", await ResponseText(pageLimited));

        var byteLimited = await RenderBatch(Records(0, 1), ",\"totalRecords\":1,\"remainingBytes\":10");
        Assert.Equal(StatusCodes.Status400BadRequest, byteLimited.Response.StatusCode);
        Assert.Contains("BatchByteBudget", await ResponseText(byteLimited));
    }

    [Fact]
    public async Task Batch_record_copies_do_not_leak_to_the_next_record()
    {
        var context = await RenderBatch(
            "[{\"index\":0,\"data\":{\"Name\":\"A\"},\"copies\":3},{\"index\":1,\"data\":{\"Name\":\"B\"}}]",
            ",\"totalRecords\":2,\"remainingPages\":1000");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = OpenArchive(context);
        var manifest = ReadManifest(archive).RootElement;
        Assert.Equal(4, manifest.GetProperty("physicalPages").GetInt32());
        Assert.Equal(3, manifest.GetProperty("records")[0].GetProperty("copies").GetInt32());
        Assert.Equal(1, manifest.GetProperty("records")[1].GetProperty("copies").GetInt32());
    }

    [Fact]
    public async Task Batch_fractional_copies_are_rejected_without_rounding()
    {
        var context = await RenderBatch(
            "[" + Record(0, "{\"Name\":\"A\",\"Count\":1.6}") + "]",
            ",\"totalRecords\":1,\"copiesField\":\"Count\"");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = OpenArchive(context);
        var manifest = ReadManifest(archive).RootElement;
        Assert.Equal("all_failed", manifest.GetProperty("status").GetString());
        Assert.Equal(0, manifest.GetProperty("physicalPages").GetInt32());
        Assert.Contains("BatchInvalidCopies", ReadEntry(archive, "errors.csv"));
    }

    [Fact]
    public async Task Batch_custom_protocol_keeps_page_and_finishing_segments()
    {
        var customTemplate =
            "{\"Version\":2,\"Width\":100,\"Height\":100,\"Rows\":[{\"Height\":100,\"Cells\":[{\"Width\":100,\"Elements\":[" +
            "{\"Type\":1,\"FontSize\":24,\"Field\":{\"Value\":\"DEMO\"}}]}]}]}";
        var context = CreateContext("{\"template\":" + customTemplate + ",\"data\":{},\"records\":[" +
            Record(0, "{}") + "],\"totalRecords\":1,\"protocolId\":\"demo.protocol\"," +
            "\"protocolConfig\":{\"characterWidth\":6,\"lineHeight\":24,\"maxPageHeight\":512}," +
            "\"copies\":2,\"finishing\":{\"feedAmount\":1,\"feedUnit\":\"Lines\",\"cut\":true," +
            "\"placement\":\"PageEnd\"},\"remainingPages\":1000,\"remainingBytes\":33554432}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = OpenArchive(context);
        var manifest = ReadManifest(archive).RootElement;
        Assert.Equal(2, manifest.GetProperty("physicalPages").GetInt32());
        Assert.Contains("records/000000/page-0001.demo-protocol", archive.Entries.Select(entry => entry.FullName));
        Assert.Contains("records/000000/page-0002.demo-protocol", archive.Entries.Select(entry => entry.FullName));
        Assert.Contains("records/000000/finish-0001.demo-protocol", archive.Entries.Select(entry => entry.FullName));
        Assert.Contains("records/000000/finish-0002.demo-protocol", archive.Entries.Select(entry => entry.FullName));
        var files = manifest.GetProperty("records")[0].GetProperty("files").EnumerateArray()
            .Select(item => item.GetProperty("file").GetString()).ToArray();
        Assert.Equal(new[]
        {
            "records/000000/page-0001.demo-protocol",
            "records/000000/finish-0001.demo-protocol",
            "records/000000/page-0002.demo-protocol",
            "records/000000/finish-0002.demo-protocol",
        }, files);
    }

    [Fact]
    public async Task Batch_cross_chunk_page_budget_and_error_metadata_are_limited()
    {
        var first = await RenderBatch(Records(0, 25), ",\"totalRecords\":35,\"remainingPages\":30");
        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        using (var archive = OpenArchive(first))
            Assert.Equal(5, ReadManifest(archive).RootElement.GetProperty("remainingPages").GetInt32());
        var second = await RenderBatch(Records(25, 10),
            ",\"totalRecords\":35,\"recordOffset\":25,\"remainingPages\":5");
        Assert.Equal(StatusCodes.Status400BadRequest, second.Response.StatusCode);
        Assert.Contains("BatchPageLimit", await ResponseText(second));

        var metadata = await RenderBatch(
            "[" + Record(0, "{}") + "," + Record(1, "{}") + "]",
            ",\"totalRecords\":2,\"remainingBytes\":100");
        Assert.Equal(StatusCodes.Status400BadRequest, metadata.Response.StatusCode);
        Assert.Contains("BatchByteBudget", await ResponseText(metadata));
    }

    [Fact]
    public async Task Batch_copies_field_controls_actual_physical_pages()
    {
        var context = await RenderBatch(
            "[" + Record(0, "{\"Name\":\"ok\",\"Copies\":3}") + "]",
            ",\"totalRecords\":1,\"copiesField\":\"Copies\",\"remainingPages\":3");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = OpenArchive(context);
        var manifest = ReadManifest(archive).RootElement;
        Assert.Equal(3, manifest.GetProperty("physicalPages").GetInt32());
        Assert.Equal(3, manifest.GetProperty("records")[0].GetProperty("pageCount").GetInt32());
    }

    [Fact]
    public async Task Batch_cancellation_does_not_write_a_result_archive()
    {
        var context = CreateContext("{\"template\":" + Template + ",\"data\":{},\"records\":[" + Record(0, "{\"Name\":\"x\"}") +
            "],\"totalRecords\":1}");
        using var source = new CancellationTokenSource();
        source.Cancel();
        context.RequestAborted = source.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender));
        Assert.Equal(0, ((MemoryStream)context.Response.Body).Length);
    }
    [Fact]
    public void Batch_error_classification_terminates_resource_internal_and_cancel_chains()
    {
        var method = typeof(DesignerRequestProcessor).GetMethod("IsRecordFailure",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var renderCtor = typeof(Bing.EasyPrint.Templates.TemplateRenderException)
            .GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Single(ctor => ctor.GetParameters().Length == 3);
        bool Continue(Exception error) => (bool)method.Invoke(null, new object[] { error })!;
        Exception Wrapped(Exception inner) => (Exception)renderCtor.Invoke(new object[] { "Template", "wrapped", inner })!;

        Assert.False(Continue(Wrapped(new PrintResourceLimitException("test", 2, 0, 1))));
        Assert.False(Continue(Wrapped(new InvalidOperationException("internal"))));
        Assert.False(Continue(Wrapped(new OperationCanceledException())));
        var withFieldPath = Wrapped(new InvalidOperationException("internal"));
        var attach = typeof(TemplateRenderException).Assembly.GetType("Bing.EasyPrint.Templates.TemplateBindingDiagnostic")!
            .GetMethod("Attach", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        attach.Invoke(null, new object[] { withFieldPath, "Value" });
        Assert.Equal("Value", ((TemplateRenderException)withFieldPath).FieldPath);
        Assert.False(Continue(withFieldPath));
    }
    [Fact]
    public async Task Batch_resource_and_internal_failures_terminate_instead_of_becoming_record_errors()
    {
        var capabilities = new Bing.EasyPrint.Printing.PrintProtocolCapabilities(203, new[] { 1 },
            fontKinds: new[] { 0 }, textRotations: new[] { Bing.EasyPrint.RotationAngle.None });
        var registration = new Bing.EasyPrint.Printing.PrintProtocolRegistration("fail.protocol", "Failing",
            _ => new FailingBackend(), "{}", "{}", null, capabilities: capabilities);
        var registry = DesignerProtocolRegistry.CreateForTests(registration, _ => new FailingAdapter(capabilities));
        var previous = DesignerRequestProcessor.ProtocolRegistry;
        try
        {
            DesignerRequestProcessor.ProtocolRegistry = registry;
            var templateFor = "{\"Version\":2,\"Width\":200,\"Height\":80,\"Rows\":[{\"Height\":80,\"Cells\":[{\"Width\":200,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Name\":\"Value\"}}]}]}]}";
            var resource = CreateContext("{\"template\":" + templateFor + ",\"data\":{},\"protocolId\":\"fail.protocol\",\"records\":[{\"index\":0,\"data\":{\"Value\":\"OK\"}},{\"index\":1,\"data\":{\"Value\":\"FAIL\"}}],\"totalRecords\":2}");
            await DesignerRequestExecution.ExecuteAsync(resource, DesignerRequestProcessor.BatchRender);
            Assert.Equal(StatusCodes.Status413PayloadTooLarge, resource.Response.StatusCode);
            using (var json = ReadJson(resource))
                Assert.Equal("ResourceLimitExceeded", json.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());

            var internalError = CreateContext("{\"template\":" + templateFor + ",\"data\":{},\"protocolId\":\"fail.protocol\",\"records\":[{\"index\":0,\"data\":{\"Value\":\"INTERNAL\"}}],\"totalRecords\":1}");
            await DesignerRequestExecution.ExecuteAsync(internalError, DesignerRequestProcessor.BatchRender);
            Assert.Equal(StatusCodes.Status500InternalServerError, internalError.Response.StatusCode);
        }
        finally { DesignerRequestProcessor.ProtocolRegistry = previous; }
    }
    private sealed class FailingAdapter : ITemplateProtocolAdapter
    {
        internal FailingAdapter(PrintProtocolCapabilities capabilities)
            => Capabilities = TemplateCustomProtocolCapabilities.FromCore(capabilities);

        public string ProtocolId => "fail.protocol";
        public TemplateCustomProtocolCapabilities Capabilities { get; }
        public ITemplateTextMetrics CreateTextMetrics(TemplateTextMeasurementRequest request) => new Metrics();
        public TemplateBarcodeMeasurement MeasureBarcode(TemplateBarcodeMeasurementRequest request) => throw new NotSupportedException();
        public PrintElement ConvertBarcode(TemplateBarcodeConversionRequest request) => throw new NotSupportedException();
        public TemplateQrCodeMeasurement MeasureQrCode(TemplateQrCodeMeasurementRequest request) => throw new NotSupportedException();
        public PrintElement ConvertQrCode(TemplateQrCodeConversionRequest request) => throw new NotSupportedException();
    }

    private sealed class Metrics : ITemplateTextMetrics
    {
        public int LineHeight => 24;
        public string FontFamily => string.Empty;
        public bool HasDeviceTextElementCalibrations => false;
        public bool NeedsFullMeasure => false;
        public int Advance(string textElement) => (textElement ?? string.Empty).Length * 6;
        public long Measure(string text) => (text ?? string.Empty).Length * 6L;
        public TemplateTextBounds MeasureBounds(string text) => new(0, 0, Advance(text), 24);
    }

    private sealed class FailingBackend : IPrintProtocolBackend
    {
        public string ProtocolId => "fail.protocol";
        public void Validate(PrintDocument document, CancellationToken cancellationToken) { }
        public IReadOnlyList<PrintPageOutput> Render(PrintDocument document, CancellationToken cancellationToken)
        {
            foreach (var page in document.Pages)
            foreach (var element in page.Elements)
            {
                if (element is not PrintTextElement text) continue;
                if (text.Text == "FAIL") throw new PrintResourceLimitException("test", 2, 0, 1);
                if (text.Text == "INTERNAL") throw new InvalidOperationException("internal failure");
                if (text.Text == "CANCEL") throw new OperationCanceledException();
            }
            return document.Pages.Select(_ => new PrintPageOutput(new[]
            {
                new PrintOutputSegment(Encoding.UTF8.GetBytes("ok")),
            })).ToArray();
        }
        public void Dispose() { }
    }
    [Fact]
    public async Task Batch_backend_cancellation_terminates_without_a_result_archive()
    {
        var capabilities = new PrintProtocolCapabilities(203, new[] { 1 }, fontKinds: new[] { 0 },
            textRotations: new[] { RotationAngle.None });
        var registration = new PrintProtocolRegistration("fail.protocol", "Failing",
            _ => new FailingBackend(), "{}", "{}", null, capabilities: capabilities);
        var registry = DesignerProtocolRegistry.CreateForTests(registration, _ => new FailingAdapter(capabilities));
        var previous = DesignerRequestProcessor.ProtocolRegistry;
        try
        {
            DesignerRequestProcessor.ProtocolRegistry = registry;
            var template = "{\"Version\":2,\"Width\":200,\"Height\":80,\"Rows\":[{\"Height\":80,\"Cells\":[{\"Width\":200,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Name\":\"Value\"}}]}]}]}";
            var context = CreateContext("{\"template\":" + template + ",\"data\":{},\"protocolId\":\"fail.protocol\",\"records\":[{\"index\":0,\"data\":{\"Value\":\"OK\"}},{\"index\":1,\"data\":{\"Value\":\"CANCEL\"}}],\"totalRecords\":2}");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender));
            Assert.Equal(0, ((MemoryStream)context.Response.Body).Length);
        }
        finally { DesignerRequestProcessor.ProtocolRegistry = previous; }
    }
    [Fact]
    public async Task Batch_preserves_high_precision_decimal_and_date_offset()
    {
        var formatted = "{\"Version\":2,\"Width\":600,\"Height\":80,\"Rows\":[{\"Height\":80,\"Cells\":[{\"Width\":600,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Name\":\"Value\",\"Format\":{\"Kind\":\"Number\",\"Pattern\":\"0.0000\"}}}]}]}]}";
        var context = CreateContext("{\"template\":" + formatted + ",\"data\":{},\"records\":[{\"index\":0,\"data\":{\"Value\":123456789012345.6789}}],\"totalRecords\":1}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using (var archive = OpenArchive(context))
            Assert.Contains("123456789012345.6789", await new StreamReader(archive.GetEntry("records/000000/page-0001.cpcl")!.Open(), Encoding.UTF8).ReadToEndAsync());

        var dateTemplate = formatted.Replace("{\"Kind\":\"Number\",\"Pattern\":\"0.0000\"}",
            "{\"Kind\":\"Date\",\"Pattern\":\"yyyy-MM-dd HH:mm zzz\"}");
        var dateContext = CreateContext("{\"template\":" + dateTemplate + ",\"data\":{},\"records\":[{\"index\":0,\"data\":{\"Value\":\"2024-01-02T03:04:05-05:00\"}}],\"totalRecords\":1}");
        await DesignerRequestExecution.ExecuteAsync(dateContext, DesignerRequestProcessor.BatchRender);
        Assert.Equal(StatusCodes.Status200OK, dateContext.Response.StatusCode);
        using var dateArchive = OpenArchive(dateContext);
        Assert.Contains("2024-01-02 03:04 -05:00", await new StreamReader(dateArchive.GetEntry("records/000000/page-0001.cpcl")!.Open(), Encoding.UTF8).ReadToEndAsync());
    }
    [Fact]
    public async Task Batch_unused_large_numbers_keep_legacy_path_and_formatted_overflow_is_record_scoped()
    {
        var formatted = "{\"Version\":2,\"Width\":600,\"Height\":80,\"Rows\":[{\"Height\":80,\"Cells\":[{\"Width\":600,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Name\":\"Value\",\"Format\":{\"Kind\":\"Number\",\"Pattern\":\"0.00\"}}}]}]}]}";
        var context = CreateContext("{\"template\":" + formatted + ",\"data\":{},\"records\":[" +
            Record(0, "{\"Value\":\"1.2\",\"Unused\":1e40}") + "," + Record(1, "{\"Value\":1e40}") +
            "],\"totalRecords\":2}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = OpenArchive(context);
        var manifest = ReadManifest(archive).RootElement;
        Assert.Equal("partial", manifest.GetProperty("status").GetString());
        Assert.Equal(1, manifest.GetProperty("succeeded").GetInt32());
        Assert.Equal(1, manifest.GetProperty("failed").GetInt32());
        Assert.Contains("records/000000/page-0001.cpcl", archive.Entries.Select(entry => entry.FullName));
        Assert.Contains("Value", ReadEntry(archive, "errors.csv"));
    }

    [Fact]
    public async Task Batch_invalid_barcode_record_is_partial_and_keeps_good_record()
    {
        var barcode = "{\"Version\":2,\"Width\":400,\"Height\":100,\"Rows\":[{\"Height\":100,\"Cells\":[{\"Width\":400,\"Elements\":[{\"Type\":4,\"BarcodeType\":5,\"Height\":40,\"Field\":{\"Name\":\"Value\"}}]}]}]}";
        var context = CreateContext("{\"template\":" + barcode + ",\"data\":{},\"records\":[" +
            Record(0, "{\"Value\":\"6901234567892\"}") + "," + Record(1, "{\"Value\":\"abc\"}") +
            "],\"totalRecords\":2}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = OpenArchive(context);
        var manifest = ReadManifest(archive).RootElement;
        Assert.Equal("partial", manifest.GetProperty("status").GetString());
        Assert.Equal(1, manifest.GetProperty("succeeded").GetInt32());
        Assert.Equal(1, manifest.GetProperty("failed").GetInt32());
        Assert.Contains("records/000000/page-0001.cpcl", archive.Entries.Select(entry => entry.FullName));
        Assert.Contains("ASCII", ReadEntry(archive, "errors.csv"));
    }

    [Fact]
    public async Task Batch_invalid_qr_record_is_partial_and_keeps_good_record()
    {
        var qr = "{\"Version\":2,\"Width\":400,\"Height\":400,\"Rows\":[{\"Height\":400,\"Cells\":[{\"Width\":400,\"Elements\":[{\"Type\":3,\"UnitWidth\":4,\"ErrorLevel\":\"M\",\"Field\":{\"Name\":\"Value\"}}]}]}]}";
        var context = CreateContext("{\"template\":" + qr + ",\"data\":{},\"records\":[" +
            Record(0, "{\"Value\":\"hello\"}") + "," + Record(1, "{\"Value\":\"" + new string('x', 3000) + "\"}") +
            "],\"totalRecords\":2}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var archive = OpenArchive(context);
        var manifest = ReadManifest(archive).RootElement;
        Assert.Equal("partial", manifest.GetProperty("status").GetString());
        Assert.Equal(1, manifest.GetProperty("succeeded").GetInt32());
        Assert.Contains("records/000000/page-0001.cpcl", archive.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public async Task Batch_metadata_over_budget_is_rejected_before_large_metadata_allocation()
    {
        var name = new string('x', 12000);
        var template = "{\"Version\":2,\"Width\":600,\"Height\":80,\"Rows\":[{\"Height\":80,\"Cells\":[{\"Width\":600,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Name\":\"" + name + "\"}}]}]}]}";
        var context = CreateContext("{\"template\":" + template + ",\"data\":{},\"records\":[" +
            Record(0, "{}") + "],\"totalRecords\":1,\"remainingBytes\":10000}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("BatchByteBudget", await ResponseText(context));
    }

    [Fact]
    public async Task Batch_internal_failure_after_success_returns_no_archive()
    {
        var capabilities = new PrintProtocolCapabilities(203, new[] { 1 }, fontKinds: new[] { 0 },
            textRotations: new[] { RotationAngle.None });
        var registration = new PrintProtocolRegistration("fail.protocol", "Failing",
            _ => new FailingBackend(), "{}", "{}", null, capabilities: capabilities);
        var registry = DesignerProtocolRegistry.CreateForTests(registration, _ => new FailingAdapter(capabilities));
        var previous = DesignerRequestProcessor.ProtocolRegistry;
        try
        {
            DesignerRequestProcessor.ProtocolRegistry = registry;
            var template = "{\"Version\":2,\"Width\":200,\"Height\":80,\"Rows\":[{\"Height\":80,\"Cells\":[{\"Width\":200,\"Elements\":[{\"Type\":1,\"FontSize\":24,\"Field\":{\"Name\":\"Value\"}}]}]}]}";
            var context = CreateContext("{\"template\":" + template + ",\"data\":{},\"protocolId\":\"fail.protocol\",\"records\":[" +
                Record(0, "{\"Value\":\"OK\"}") + "," + Record(1, "{\"Value\":\"INTERNAL\"}") +
                "],\"totalRecords\":2}");
            await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender);
            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            Assert.NotEqual("application/zip", context.Response.ContentType);
        }
        finally { DesignerRequestProcessor.ProtocolRegistry = previous; }
    }

    private static string Records(int start, int count)
    {
        var values = Enumerable.Range(start, count)
            .Select(index => Record(index, "{\"Name\":\"value-" + index + "\"}"));
        return "[" + string.Join(",", values) + "]";
    }

    private static string Record(int index, string data)
        => "{\"index\":" + index + ",\"data\":" + data + "}";

    private static async Task<DefaultHttpContext> RenderBatch(string records, string extra)
    {
        var context = CreateContext("{\"template\":" + Template + ",\"data\":{},\"records\":" + records + extra + "}");
        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.BatchRender);
        return context;
    }

    private static DefaultHttpContext CreateContext(string json)
    {
        var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = "application/json; charset=utf-8";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ZipArchive OpenArchive(DefaultHttpContext context)
    {
        var stream = new MemoryStream(((MemoryStream)context.Response.Body).ToArray());
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    private static JsonDocument ReadManifest(ZipArchive archive)
        => JsonDocument.Parse(ReadEntry(archive, "manifest.json"));

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException("ZIP entry missing: " + name);
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadEntryBytes(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException("ZIP entry missing: " + name);
        using var output = new MemoryStream();
        using var input = entry.Open();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static JsonDocument ReadJson(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body);
    }
    private static async Task<string> ResponseText(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
