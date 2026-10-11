using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class HostPrintPipelineBudgetTests
{
    private static readonly JsonSerializerOptions RequestJson = new JsonSerializerOptions
    {
        PropertyNamingPolicy = null
    };

    static HostPrintPipelineBudgetTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Theory]
    [InlineData("cpcl", null)]
    [InlineData("tspl", null)]
    [InlineData("zpl", null)]
    [InlineData("escpos", "native")]
    [InlineData("escpos", "raster")]
    public async Task Preview_and_render_use_the_real_processor_pipeline_for_each_protocol(
        string protocol, string? layout)
    {
        using (var parent = PrintResourceScope.Begin(64L * 1024 * 1024))
        {
            foreach (var operation in new[] { "preview", "render" })
            {
                var context = CreateContext(BuildRequest(protocol, layout), observedScope: parent);
                var response = (ObservingResponseStream)context.Response.Body;
                var action = operation == "preview"
                    ? (Func<DesignerRequest, CancellationToken, IResult>)DesignerRequestProcessor.Preview
                    : DesignerRequestProcessor.Render;

                await DesignerRequestExecution.ExecuteAsync(context, action);

                Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
                Assert.True(response.MaxObservedBytes > 0,
                    $"{protocol}/{layout ?? "default"} {operation} 写响应时父 scope 没有活动预留。");
                if (operation == "preview")
                    AssertPreviewBody(context);
                else
                    AssertZipPageEntries(context);
                Assert.Equal(0, parent.CurrentBytes);
            }
        }
    }

    [Fact]
    public async Task Delayed_zpl_render_keeps_control_segment_and_manifest_order()
    {
        using (var parent = PrintResourceScope.Begin(64L * 1024 * 1024))
        {
            var context = CreateContext(BuildRequest("zpl",
                cutAfterPrint: true,
                cutOptions: new { Mode = "Full" },
                zplMedia: new
                {
                    Profile = new
                    {
                        SupportsCut = true,
                        Cutter = new
                        {
                            Strategy = "Delayed",
                            SupportedCutModes = new[] { "Full" },
                            SupportsTearOff = true
                        }
                    }
                }), observedScope: parent);
            var response = (ObservingResponseStream)context.Response.Body;

            await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.True(response.MaxObservedBytes > 0);
            using (var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(context), writable: false),
                ZipArchiveMode.Read))
            {
                var zipSegments = archive.Entries
                    .Where(entry => !string.Equals(entry.FullName, "job.json", StringComparison.OrdinalIgnoreCase))
                    .Select(entry => entry.FullName)
                    .ToArray();
                Assert.Equal(new[] { "page-001.zpl", "page-001-segment-002.zpl" }, zipSegments);

                var page = archive.GetEntry("page-001.zpl");
                Assert.NotNull(page);
                Assert.NotEmpty(ReadArchiveEntry(page!));

                var control = archive.GetEntry("page-001-segment-002.zpl");
                Assert.NotNull(control);
                Assert.Equal("~JK\r\n", Encoding.ASCII.GetString(ReadArchiveEntry(control!)));

                var manifest = archive.GetEntry("job.json");
                Assert.NotNull(manifest);
                using (var document = JsonDocument.Parse(ReadArchiveEntry(manifest!)))
                {
                    var root = document.RootElement;
                    Assert.Equal(2, root.GetProperty("outputVersion").GetInt32());
                    Assert.True(root.GetProperty("preserveFileBoundaries").GetBoolean());

                    var files = root.GetProperty("files").EnumerateArray().ToArray();
                    Assert.Equal(2, files.Length);
                    Assert.Equal(zipSegments, files.Select(file => file.GetProperty("file").GetString()).ToArray());
                    Assert.Equal("Commands", files[0].GetProperty("kind").GetString());
                    Assert.Equal("Control", files[1].GetProperty("kind").GetString());
                    Assert.Equal(0, files[0].GetProperty("pageIndex").GetInt32());
                    Assert.Equal(1, files[1].GetProperty("segmentIndex").GetInt32());

                    var pageSegments = Assert.Single(root.GetProperty("pages").EnumerateArray())
                        .GetProperty("segments").EnumerateArray().ToArray();
                    Assert.Equal(2, pageSegments.Length);
                    Assert.Equal("page-001.zpl", pageSegments[0].GetProperty("file").GetString());
                    Assert.Equal("page-001-segment-002.zpl", pageSegments[1].GetProperty("file").GetString());
                }
            }

            Assert.Equal(0, parent.CurrentBytes);
        }
    }

    [Fact]
    public async Task Over_budget_font_base64_releases_the_parent_and_a_following_render_succeeds()
    {
        var decodedLength = 16 * 1024;
        var regularData = Convert.ToBase64String(new byte[decodedLength]);
        var request = BuildRequest("escpos", "raster", RasterDevice(regularData));
        var serverLimit = CalculateFontFailureLimit(request, decodedLength);

        using (var parent = PrintResourceScope.Begin(serverLimit))
        {
            var failed = CreateContext(request);
            await DesignerRequestExecution.ExecuteAsync(failed, DesignerRequestProcessor.Render,
                serverMaxWorkingBytes: serverLimit);

            Assert.Equal(StatusCodes.Status413PayloadTooLarge, failed.Response.StatusCode);
            Assert.Contains("Host.FontBase64Regular", ReadResponseText(failed));
            Assert.Equal(0, parent.CurrentBytes);

            var recovered = CreateContext(BuildRequest("cpcl"));
            await DesignerRequestExecution.ExecuteAsync(recovered, DesignerRequestProcessor.Render);

            Assert.Equal(StatusCodes.Status200OK, recovered.Response.StatusCode);
            AssertZipPageEntries(recovered);
            Assert.Equal(0, parent.CurrentBytes);
        }
    }

    [Fact]
    public async Task Invalid_font_base64_releases_the_parent_and_a_following_preview_succeeds()
    {
        using (var parent = PrintResourceScope.Begin(8L * 1024 * 1024))
        {
            var failed = CreateContext(BuildRequest("escpos", "raster", RasterDevice("not-base64")));
            await DesignerRequestExecution.ExecuteAsync(failed, DesignerRequestProcessor.Preview);

            Assert.Equal(StatusCodes.Status400BadRequest, failed.Response.StatusCode);
            Assert.DoesNotContain("ResourceLimitExceeded", ReadResponseText(failed));
            Assert.Equal(0, parent.CurrentBytes);

            var recovered = CreateContext(BuildRequest("zpl"));
            await DesignerRequestExecution.ExecuteAsync(recovered, DesignerRequestProcessor.Preview);

            Assert.Equal(StatusCodes.Status200OK, recovered.Response.StatusCode);
            AssertPreviewBody(recovered);
            Assert.Equal(0, parent.CurrentBytes);
        }
    }

    [Fact]
    public async Task Response_write_cancellation_releases_the_request_scope_without_turning_into400()
    {
        using (var parent = PrintResourceScope.Begin(64L * 1024 * 1024))
        {
            var responseBody = new ThrowingWriteStream();
            var lifetime = new RecordingRequestLifetimeFeature();
            var context = CreateContext(BuildRequest("zpl"), responseBody: responseBody);
            context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);

            await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render);

            Assert.True(responseBody.WriteAttempted);
            Assert.True(lifetime.AbortCalled);
            Assert.NotEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            Assert.DoesNotContain("errors", ReadResponseText(context));
            Assert.Equal(0, parent.CurrentBytes);
        }
    }

    private static string BuildRequest(string protocol, string? layout = null, object? escPosDevice = null,
        bool? cutAfterPrint = null, object? cutOptions = null, object? zplMedia = null)
    {
        var request = new
        {
            Template = MinimalTemplate(),
            Data = new { },
            Protocol = protocol,
            EscPosLayoutMode = layout,
            Encoding = "ascii",
            EscPosDevice = escPosDevice,
            CutAfterPrint = cutAfterPrint,
            CutOptions = cutOptions,
            ZplMedia = zplMedia
        };
        return JsonSerializer.Serialize(request, RequestJson);
    }

    private static object RasterDevice(string regularData)
        => new
        {
            RasterFonts = new
            {
                DefaultFont = "Primary",
                Fonts = new[]
                {
                    new
                    {
                        Name = "Primary",
                        Version = "test",
                        RegularData = regularData
                    }
                }
            }
        };

    private static object MinimalTemplate()
        => new
        {
            Version = 1,
            Width = 576,
            Height = 320,
            Rows = new[]
            {
                new
                {
                    Cells = new[]
                    {
                        new
                        {
                            Elements = new[]
                            {
                                new
                                {
                                    Type = 4,
                                    LineWidth = 2,
                                    Height = 64,
                                    Field = new { Value = "123456" }
                                }
                            }
                        }
                    }
                }
            }
        };

    private static long CalculateFontFailureLimit(string request, int decodedLength)
    {
        var requestBytes = Encoding.UTF8.GetByteCount(request);
        // The request and preparation scopes retain 4*requestBytes before font
        // preparation; template/data model bytes are retained later.
        return checked(requestBytes * 4L + decodedLength - 1);
    }

    private static DefaultHttpContext CreateContext(string body, Stream? responseBody = null,
        CancellationToken cancellationToken = default, PrintResourceScope? observedScope = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var requestBody = new MemoryStream(Encoding.UTF8.GetBytes(body), writable: false);
        context.Request.Body = requestBody;
        context.Request.ContentLength = requestBody.Length;
        context.Request.ContentType = "application/json; charset=utf-8";
        context.RequestAborted = cancellationToken;
        context.Response.Body = responseBody ?? new ObservingResponseStream(observedScope);
        return context;
    }

    private static void AssertPreviewBody(HttpContext context)
    {
        using (var document = JsonDocument.Parse(ReadResponseBytes(context)))
        {
            var pages = document.RootElement.GetProperty("pages");
            Assert.NotEmpty(pages.EnumerateArray());
            Assert.NotEmpty(pages[0].GetProperty("elements").EnumerateArray());
        }
    }

    private static void AssertZipPageEntries(HttpContext context)
    {
        using (var archive = new ZipArchive(new MemoryStream(ReadResponseBytes(context), writable: false),
            ZipArchiveMode.Read))
        {
            var pages = archive.Entries.Where(entry => !string.Equals(entry.FullName, "job.json",
                StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.NotEmpty(pages);
            Assert.All(pages, entry => Assert.True(entry.Length > 0, entry.FullName));
        }
    }

    private static byte[] ReadArchiveEntry(ZipArchiveEntry entry)
    {
        using (var input = entry.Open())
        using (var output = new MemoryStream())
        {
            input.CopyTo(output);
            return output.ToArray();
        }
    }

    private static string ReadResponseText(HttpContext context)
        => Encoding.UTF8.GetString(ReadResponseBytes(context));

    private static byte[] ReadResponseBytes(HttpContext context)
        => ((MemoryStream)context.Response.Body).ToArray();

    private sealed class ObservingResponseStream : MemoryStream
    {
        private readonly PrintResourceScope? _scope;

        internal ObservingResponseStream(PrintResourceScope? scope)
        {
            _scope = scope;
        }

        internal long MaxObservedBytes { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Observe();
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Observe();
            base.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            Observe();
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Observe();
            return base.WriteAsync(buffer, cancellationToken);
        }

        private void Observe()
        {
            if (_scope != null) MaxObservedBytes = Math.Max(MaxObservedBytes, _scope.CurrentBytes);
        }
    }

    private sealed class ThrowingWriteStream : MemoryStream
    {
        internal bool WriteAttempted { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAttempted = true;
            throw new OperationCanceledException("response write cancelled");
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            WriteAttempted = true;
            throw new OperationCanceledException("response write cancelled");
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            WriteAttempted = true;
            throw new OperationCanceledException("response write cancelled");
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteAttempted = true;
            throw new OperationCanceledException("response write cancelled");
        }
    }

    private sealed class RecordingRequestLifetimeFeature : IHttpRequestLifetimeFeature
    {
        public CancellationToken RequestAborted { get; set; }

        internal bool AbortCalled { get; private set; }

        public void Abort()
        {
            AbortCalled = true;
        }
    }
}
