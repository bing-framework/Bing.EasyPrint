using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

public sealed class HostAdmissionPipelineTests
{
    private static readonly JsonSerializerOptions RequestJson = new JsonSerializerOptions
    {
        PropertyNamingPolicy = null
    };

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);
    private readonly ITestOutputHelper _output;

    public HostAdmissionPipelineTests(ITestOutputHelper output)
    {
        _output = output;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public async Task Admission_queue_rejection_happens_before_body_read_and_cancelled_waiter_is_removed()
    {
        using var parent = PrintResourceScope.Begin(16L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(1L * 1024 * 1024,
            2L * 1024 * 1024, 1, maxQueuedRequests: 1);
        var firstBody = new BarrierReadStream(Encoding.UTF8.GetBytes(BuildRequest("cpcl")));
        var first = CreateContext(firstBody);
        var firstTask = DesignerRequestExecution.ExecuteAsync(first, DesignerRequestProcessor.Render,
            admission: admission);
        using var queuedCancellation = new CancellationTokenSource();
        var secondBody = new CountingReadStream(Encoding.UTF8.GetBytes(BuildRequest("cpcl")));
        var second = CreateContext(secondBody, cancellationToken: queuedCancellation.Token);
        Task secondTask = null!;

        try
        {
            await firstBody.ReadStarted.Task.WaitAsync(WaitTimeout);

            secondTask = DesignerRequestExecution.ExecuteAsync(second, DesignerRequestProcessor.Render,
                admission: admission);
            await WaitUntilAsync(() => admission.Snapshot.QueuedRequests == 1,
                "第二个请求未进入 admission 队列。");
            Assert.Equal(0, secondBody.ReadCount);

            var thirdBody = new CountingReadStream(Encoding.UTF8.GetBytes(BuildRequest("cpcl")));
            var third = CreateContext(thirdBody);
            await DesignerRequestExecution.ExecuteAsync(third, DesignerRequestProcessor.Render,
                admission: admission);

            Assert.Equal(StatusCodes.Status503ServiceUnavailable, third.Response.StatusCode);
            Assert.Equal(0, thirdBody.ReadCount);
            AssertAdmissionError(third, "QueueFull");

            queuedCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondTask);
            await WaitUntilAsync(() => admission.Snapshot.QueuedRequests == 0,
                "取消后的排队请求未移出 admission 队列。");

            firstBody.Release();
            await firstTask;
            Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
            AssertAdmissionEmpty(admission, parent);
        }
        finally
        {
            firstBody.Release();
            queuedCancellation.Cancel();
            await IgnoreAsync(firstTask);
            if (secondTask != null) await IgnoreAsync(secondTask);
        }
    }

    [Fact]
    public async Task Read_failure_releases_admission_and_print_scope_for_a_following_request()
    {
        using var parent = PrintResourceScope.Begin(16L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(4L * 1024 * 1024,
            8L * 1024 * 1024, 1);
        var failed = CreateContext(new ReadFailureStream());

        await DesignerRequestExecution.ExecuteAsync(failed, DesignerRequestProcessor.Render,
            admission: admission);

        Assert.Equal(StatusCodes.Status500InternalServerError, failed.Response.StatusCode);
        AssertAdmissionEmpty(admission, parent);

        var recovered = CreateContext(BuildRequest("cpcl"));
        await DesignerRequestExecution.ExecuteAsync(recovered, DesignerRequestProcessor.Render,
            admission: admission);
        Assert.Equal(StatusCodes.Status200OK, recovered.Response.StatusCode);
        AssertAdmissionEmpty(admission, parent);
    }

    [Fact]
    public async Task Result_execution_failure_releases_admission_and_print_scope()
    {
        using var parent = PrintResourceScope.Begin(16L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(4L * 1024 * 1024,
            8L * 1024 * 1024, 1);
        var context = CreateContext(BuildRequest("cpcl"));

        await DesignerRequestExecution.ExecuteAsync(context, (_, _) => new ThrowingResult(),
            admission: admission);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("InternalError", ReadError(context).Code);

        AssertAdmissionEmpty(admission, parent);
    }

    [Fact]
    public async Task Processing_delegate_failure_returns500_and_releases_admission_and_print_scope()
    {
        using var parent = PrintResourceScope.Begin(16L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(4L * 1024 * 1024,
            8L * 1024 * 1024, 1);
        var context = CreateContext(BuildRequest("cpcl"));

        await DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
        {
            throw new InvalidOperationException("processor failure");
        }, admission: admission);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        AssertAdmissionEmpty(admission, parent);
    }

    [Fact]
    public async Task Response_failure_releases_admission_and_print_scope()
    {
        using var parent = PrintResourceScope.Begin(16L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(4L * 1024 * 1024,
            8L * 1024 * 1024, 1);
        var context = CreateContext(BuildRequest("cpcl"),
            responseBody: new ThrowingResponseStream());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render,
                admission: admission));

        AssertAdmissionEmpty(admission, parent);
    }

    [Fact]
    public async Task Pre_cancelled_request_releases_admission_without_reading_body()
    {
        using var parent = PrintResourceScope.Begin(16L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(4L * 1024 * 1024,
            8L * 1024 * 1024, 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var body = new CountingReadStream(Encoding.UTF8.GetBytes(BuildRequest("cpcl")));
        var context = CreateContext(body, cancellationToken: cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render,
                admission: admission));

        Assert.Equal(0, body.ReadCount);
        AssertAdmissionEmpty(admission, parent);
    }

    [Fact]
    public async Task Admission_request_limit_cannot_be_widened_by_a_larger_server_limit()
    {
        using var parent = PrintResourceScope.Begin(16L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(1024, 8L * 1024 * 1024, 1);
        var body = new CountingReadStream(Encoding.UTF8.GetBytes(BuildRequest("cpcl")));
        var context = CreateContext(body);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render,
            serverMaxWorkingBytes: 64L * 1024 * 1024, admission: admission);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.Equal(0, body.ReadCount);
        var error = ReadError(context);
        Assert.Equal("ResourceLimitExceeded", error.Code);
        Assert.Equal(1024, error.MaxBytes);
        AssertAdmissionEmpty(admission, parent);
    }

    [Fact]
    public async Task Admission_rejects_a_request_budget_larger_than_total_before_body_read()
    {
        using var parent = PrintResourceScope.Begin(16L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(4L * 1024 * 1024,
            2L * 1024 * 1024, 1);
        var body = new CountingReadStream(Encoding.UTF8.GetBytes(BuildRequest("cpcl")));
        var context = CreateContext(body);

        await DesignerRequestExecution.ExecuteAsync(context, DesignerRequestProcessor.Render,
            admission: admission);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal(0, body.ReadCount);
        AssertAdmissionError(context, "RequestBudgetExceedsTotal");
        AssertAdmissionEmpty(admission, parent);
    }

    [Fact]
    public async Task Real_render_response_barrier_holds_admission_until_send_completes()
    {
        using var parent = PrintResourceScope.Begin(32L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(4L * 1024 * 1024,
            8L * 1024 * 1024, 1, maxQueuedRequests: 1);
        var firstResponse = new BarrierWriteStream();
        var first = CreateContext(BuildRequest("cpcl"), responseBody: firstResponse);
        var firstTask = DesignerRequestExecution.ExecuteAsync(first, DesignerRequestProcessor.Render,
            admission: admission);
        var secondBody = new CountingReadStream(Encoding.UTF8.GetBytes(BuildRequest("cpcl")));
        var second = CreateContext(secondBody);
        Task secondTask = null!;

        try
        {
            await firstResponse.WriteStarted.Task.WaitAsync(WaitTimeout);
            Assert.Equal(1, admission.Snapshot.ActiveRequests);
            Assert.False(firstTask.IsCompleted);

            secondTask = DesignerRequestExecution.ExecuteAsync(second, DesignerRequestProcessor.Render,
                admission: admission);
            await WaitUntilAsync(() => admission.Snapshot.QueuedRequests == 1,
                "响应发送期间第二个请求未保持排队。");
            Assert.Equal(0, secondBody.ReadCount);

            firstResponse.Release();
            await firstTask;
            await secondTask;
            Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
            Assert.Equal(StatusCodes.Status200OK, second.Response.StatusCode);
            AssertAdmissionEmpty(admission, parent);
        }
        finally
        {
            firstResponse.Release();
            await IgnoreAsync(firstTask);
            if (secondTask != null) await IgnoreAsync(secondTask);
        }
    }

    [Theory]
    [InlineData("cpcl", null)]
    [InlineData("escpos", "raster")]
    public async Task Real_render_admission_probe_observes_two_active_and_six_queued_requests(
        string protocol, string? layout)
    {
        const int requestedConcurrency = 8;
        const int maxQueuedRequests = 6;
        const long requestBytes = 4L * 1024 * 1024;
        const long totalBytes = 8L * 1024 * 1024;

        using var parent = PrintResourceScope.Begin(64L * 1024 * 1024);
        var admission = new DesignerRequestAdmission(requestBytes, totalBytes,
            3, maxQueuedRequests);
        var activeResponses = new[] { new BarrierWriteStream(), new BarrierWriteStream() };
        var allTasks = new List<Task>(requestedConcurrency);
        var allContexts = new List<DefaultHttpContext>(requestedConcurrency);
        var queuedBodies = new List<CountingReadStream>(maxQueuedRequests);
        var queuedCancellations = new CancellationTokenSource[maxQueuedRequests];

        try
        {
            for (var index = 0; index < activeResponses.Length; index++)
            {
                var context = CreateContext(BuildRequest(protocol, layout),
                    responseBody: activeResponses[index]);
                allContexts.Add(context);
                allTasks.Add(DesignerRequestExecution.ExecuteAsync(context,
                    DesignerRequestProcessor.Render, admission: admission));
            }

            await Task.WhenAll(activeResponses.Select(item => item.WriteStarted.Task.WaitAsync(WaitTimeout)));

            for (var index = 0; index < maxQueuedRequests; index++)
            {
                var cancellation = new CancellationTokenSource();
                queuedCancellations[index] = cancellation;
                var body = new CountingReadStream(Encoding.UTF8.GetBytes(BuildRequest(protocol, layout)));
                queuedBodies.Add(body);
                var context = CreateContext(body, cancellationToken: cancellation.Token);
                allContexts.Add(context);
                allTasks.Add(DesignerRequestExecution.ExecuteAsync(context,
                    DesignerRequestProcessor.Render, admission: admission));
            }

            await WaitUntilAsync(() => admission.Snapshot.ActiveRequests == 2 &&
                admission.Snapshot.QueuedRequests == maxQueuedRequests,
                "真实 Render 未达到 admission 的 2 active/6 queued 探针状态。");

            var probe = admission.Snapshot;
            Assert.Equal(2, probe.ActiveRequests);
            Assert.Equal(maxQueuedRequests, probe.QueuedRequests);
            Assert.Equal(2, probe.MaxActualParallelism);
            Assert.Equal(totalBytes, probe.ReservedWorkingBytes);
            Assert.Equal(totalBytes, probe.PeakReservedWorkingBytes);
            Assert.Equal(maxQueuedRequests, probe.MaxQueuedRequests);
            Assert.All(queuedBodies, body => Assert.Equal(0, body.ReadCount));

            queuedCancellations[0].Cancel();
            queuedCancellations[1].Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => allTasks[2]);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => allTasks[3]);

            foreach (var response in activeResponses) response.Release();
            for (var index = 0; index < activeResponses.Length; index++)
                await allTasks[index];
            for (var index = activeResponses.Length + 2; index < allTasks.Count; index++)
                await allTasks[index];

            var final = admission.Snapshot;
            Assert.Equal(0, final.ActiveRequests);
            Assert.Equal(0, final.QueuedRequests);
            Assert.Equal(0, final.ReservedWorkingBytes);
            Assert.Equal(2, final.MaxActualParallelism);
            Assert.Equal(totalBytes, final.PeakReservedWorkingBytes);
            Assert.Equal(2, final.CancelledQueuedRequests);
            Assert.Equal(0, parent.CurrentBytes);
            Assert.True(parent.PeakBytes <= totalBytes);
            Assert.All(allContexts.Where((_, index) => index < 2 || index >= 4),
                context => Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode));

            _output.WriteLine(JsonSerializer.Serialize(new
            {
                protocol,
                requestedConcurrency,
                actualParallelism = final.MaxActualParallelism,
                maxQueuedRequests = final.MaxQueuedRequests,
                peakReservedBytes = final.PeakReservedWorkingBytes,
                parentPeakBytes = parent.PeakBytes,
                finalActiveRequests = final.ActiveRequests,
                finalQueuedRequests = final.QueuedRequests,
                finalReservedWorkingBytes = final.ReservedWorkingBytes,
                cancelledQueuedRequests = final.CancelledQueuedRequests
            }));
        }
        finally
        {
            foreach (var response in activeResponses) response.Release();
            foreach (var cancellation in queuedCancellations)
                cancellation?.Cancel();
            foreach (var task in allTasks) await IgnoreAsync(task);
            foreach (var cancellation in queuedCancellations)
                cancellation?.Dispose();
        }
    }

    private static string BuildRequest(string protocol, string? layout = null)
    {
        var request = new
        {
            Template = MinimalCode128Template(),
            Data = new { },
            Protocol = protocol,
            EscPosLayoutMode = layout,
            Encoding = "ascii"
        };
        return JsonSerializer.Serialize(request, RequestJson);
    }

    private static object MinimalCode128Template()
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

    private static DefaultHttpContext CreateContext(string body, Stream? responseBody = null,
        CancellationToken cancellationToken = default)
        => CreateContext(new MemoryStream(Encoding.UTF8.GetBytes(body), writable: false),
            responseBody, cancellationToken);

    private static DefaultHttpContext CreateContext(Stream body, Stream? responseBody = null,
        CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = body;
        context.Request.ContentLength = body.CanSeek ? body.Length : null;
        context.Request.ContentType = "application/json; charset=utf-8";
        context.RequestAborted = cancellationToken;
        context.Response.Body = responseBody ?? new MemoryStream();
        return context;
    }

    private static void AssertAdmissionEmpty(DesignerRequestAdmission admission,
        PrintResourceScope parent)
    {
        var snapshot = admission.Snapshot;
        Assert.Equal(0, snapshot.ActiveRequests);
        Assert.Equal(0, snapshot.QueuedRequests);
        Assert.Equal(0, snapshot.ReservedWorkingBytes);
        Assert.Equal(0, parent.CurrentBytes);
    }

    private static void AssertAdmissionError(HttpContext context, string reason)
    {
        var error = ReadError(context);
        Assert.Equal("ResourceAdmissionRejected", error.Code);
        Assert.Equal(reason, error.Reason);
    }

    private static (string? Code, string? Reason, long? MaxBytes) ReadError(HttpContext context)
    {
        using var document = JsonDocument.Parse(ReadResponseBytes(context));
        var error = document.RootElement.GetProperty("errors")[0];
        var code = error.TryGetProperty("code", out var codeValue) ? codeValue.GetString() : null;
        var reason = error.TryGetProperty("reason", out var reasonValue) ? reasonValue.GetString() : null;
        var maxBytes = error.TryGetProperty("maxBytes", out var maxValue) &&
            maxValue.ValueKind != JsonValueKind.Null ? maxValue.GetInt64() : (long?)null;
        return (code, reason, maxBytes);
    }

    private static byte[] ReadResponseBytes(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var copy = new MemoryStream();
        context.Response.Body.CopyTo(copy);
        return copy.ToArray();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed >= WaitTimeout)
                throw new TimeoutException(description);
            await Task.Yield();
        }
    }

    private static async Task IgnoreAsync(Task task)
    {
        try { await task; }
        catch (Exception) { }
    }

    private sealed class CountingReadStream : MemoryStream
    {
        internal CountingReadStream(byte[] bytes) : base(bytes, writable: false) { }

        internal int ReadCount { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;
            return base.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            ReadCount++;
            return base.Read(buffer);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class BarrierReadStream : MemoryStream
    {
        private readonly TaskCompletionSource<bool> _release =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal BarrierReadStream(byte[] bytes) : base(bytes, writable: false) { }

        internal TaskCompletionSource<bool> ReadStarted { get; } =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Release() => _release.TrySetResult(true);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class BarrierWriteStream : MemoryStream
    {
        private readonly TaskCompletionSource<bool> _release =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<bool> WriteStarted { get; } =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Release() => _release.TrySetResult(true);

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteStarted.TrySetResult(true);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            WriteStarted.TrySetResult(true);
            base.Write(buffer);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            WriteStarted.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            await base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteStarted.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            await base.WriteAsync(buffer, cancellationToken);
        }
    }

    private sealed class ReadFailureStream : MemoryStream
    {
        internal ReadFailureStream() : base(Array.Empty<byte>(), writable: false) { }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("request read failed");

        public override int Read(Span<byte> buffer)
            => throw new InvalidOperationException("request read failed");

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("request read failed");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("request read failed");
    }

    private sealed class ThrowingResult : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
            => throw new InvalidOperationException("result execution failed");
    }

    private sealed class ThrowingResponseStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("response write failed");

        public override void Write(ReadOnlySpan<byte> buffer)
            => throw new InvalidOperationException("response write failed");

        public override Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("response write failed");

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("response write failed");
    }
}
