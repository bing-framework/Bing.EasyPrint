using System.Text;
using System.Text.Json;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class HostRequestBudgetTests
{
    private const string ValidRequest =
        "{\"template\":{\"Version\":1,\"Width\":80,\"Height\":40,\"Rows\":[]},\"data\":{}}";

    [Fact]
    public async Task Normal_action_and_result_execute_inside_the_request_scope_then_release_parent_bytes()
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        var context = CreateContext(ValidRequest);
        long actionBytes = -1;
        long resultBytes = -1;

        await DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
            {
                actionBytes = parent.CurrentBytes;
                return new ProbeResult(() => resultBytes = parent.CurrentBytes);
            });

        Assert.True(actionBytes > 0);
        Assert.True(resultBytes > 0);
        Assert.Equal(0, parent.CurrentBytes);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("ok", await ReadResponseText(context));
    }

    [Fact]
    public async Task Json_output_budget_failure_returns_413_without_writing_success_content()
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        var context = CreateContext(ValidRequest);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var value = new { payload = new string('x', 2 * 1024 * 1024) };

        await DesignerRequestExecution.ExecuteAsync(context,
            (_, _) => new BudgetedJsonResult(value, options), serverMaxWorkingBytes: 1024 * 1024);

        AssertResourceLimitResponse(context);
        Assert.DoesNotContain("payload", await ReadResponseText(context));
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Server_budget_zero_is_rejected_before_reading_or_invoking_action()
    {
        var body = new TrackingReadStream(Encoding.UTF8.GetBytes(ValidRequest));
        var context = CreateContext(body);
        var invoked = false;

        await DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
        {
            invoked = true;
            return Results.Text("unexpected");
        }, serverMaxWorkingBytes: 0);

        Assert.False(invoked);
        Assert.Equal(0, body.ReadCount);
        AssertResourceLimitResponse(context);
    }

    [Fact]
    public async Task Request_budget_zero_is_rejected_after_request_json_is_read_without_action()
    {
        var body = new TrackingReadStream(Encoding.UTF8.GetBytes(
            "{\"maxWorkingBytes\":0,\"template\":{\"Version\":1,\"Width\":80,\"Height\":40,\"Rows\":[]},\"data\":{}}"));
        var context = CreateContext(body);
        var invoked = false;

        await DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
        {
            invoked = true;
            return Results.Text("unexpected");
        }, serverMaxWorkingBytes: 1024 * 1024);

        Assert.False(invoked);
        Assert.True(body.ReadCount > 0);
        AssertResourceLimitResponse(context);
    }

    [Fact]
    public async Task Invalid_json_releases_the_parent_scope_and_a_following_request_succeeds()
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        var invalid = CreateContext("{\"template\":");
        var invalidInvoked = false;

        await DesignerRequestExecution.ExecuteAsync(invalid, (_, _) =>
        {
            invalidInvoked = true;
            return Results.Text("unexpected");
        });

        Assert.False(invalidInvoked);
        Assert.Equal(400, invalid.Response.StatusCode);
        Assert.Equal(0, parent.CurrentBytes);

        var valid = CreateContext(ValidRequest);
        await DesignerRequestExecution.ExecuteAsync(valid, (_, _) => Results.Text("ok"));

        Assert.Equal(200, valid.Response.StatusCode);
        Assert.Equal("ok", await ReadResponseText(valid));
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Cancellation_during_request_read_releases_the_parent_scope()
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        var body = new CancellationReadStream(cancellation.Token);
        var context = CreateContext(body, cancellation.Token);
        var invoked = false;
        var execution = DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
        {
            invoked = true;
            return Results.Text("unexpected");
        });

        try
        {
            await body.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        }
        finally
        {
            cancellation.Cancel();
            if (!execution.IsCompleted)
                await execution;
        }

        Assert.False(invoked);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public async Task Cancellation_thrown_by_action_is_transparent_and_not_a_validation_error()
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        var context = CreateContext(ValidRequest);
        var invoked = false;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            DesignerRequestExecution.ExecuteAsync(context, (_, token) =>
            {
                invoked = true;
                throw new OperationCanceledException(token);
            }));

        Assert.True(invoked);
        Assert.NotEqual(400, context.Response.StatusCode);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("render")]
    public async Task Pre_cancelled_preview_and_render_propagate_cancellation(string operation)
    {
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var body = new TrackingReadStream(Encoding.UTF8.GetBytes(ValidRequest));
        var context = CreateContext(body, cancellation.Token);
        Func<DesignerRequest, CancellationToken, IResult> action = operation == "preview"
            ? DesignerRequestProcessor.Preview
            : DesignerRequestProcessor.Render;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            DesignerRequestExecution.ExecuteAsync(context, action));

        Assert.Equal(0, body.ReadCount);
        Assert.NotEqual(400, context.Response.StatusCode);
        Assert.Equal(0, parent.CurrentBytes);
    }

    [Fact]
    public void Budgeted_host_stream_counts_new_capacity_before_releasing_old_and_recovers_after_failed_growth()
    {
        using var scope = PrintResourceScope.Begin(768);
        using var stream = new BudgetedHostStream("Host.TestBuffer", CancellationToken.None);

        stream.Write(new byte[256]);
        Assert.Equal(256, scope.CurrentBytes);

        stream.WriteByte(1);
        Assert.Equal(512, scope.CurrentBytes);

        stream.Write(new byte[255]);
        Assert.Equal(512, scope.CurrentBytes);
        Assert.Throws<PrintResourceLimitException>(() => stream.WriteByte(2));
        Assert.Equal(512, scope.CurrentBytes);

        stream.Position = 0;
        stream.WriteByte(3);
        Assert.Equal(512, scope.CurrentBytes);
        stream.Dispose();
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Fact]
    public void Budgeted_host_stream_snapshot_budget_failure_does_not_leak_a_snapshot_lease()
    {
        using var scope = PrintResourceScope.Begin(300);
        using var stream = new BudgetedHostStream("Host.TestBuffer", CancellationToken.None);
        stream.Write(new byte[256]);

        Assert.Throws<PrintResourceLimitException>(() => stream.ToArray("Host.TestSnapshot"));
        Assert.Equal(256, scope.CurrentBytes);

        stream.Position = 0;
        stream.WriteByte(4);
        Assert.Equal(256, scope.CurrentBytes);
        stream.Dispose();
        Assert.Equal(0, scope.CurrentBytes);
    }

    private static DefaultHttpContext CreateContext(string body, CancellationToken cancellationToken = default)
        => CreateContext(new MemoryStream(Encoding.UTF8.GetBytes(body), writable: false), cancellationToken);

    private static DefaultHttpContext CreateContext(Stream body, CancellationToken cancellationToken = default)
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
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseText(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, false, 1024, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static void AssertResourceLimitResponse(HttpContext context)
    {
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(context.Response.Body);
        var error = document.RootElement.GetProperty("errors")[0];
        Assert.Equal("ResourceLimitExceeded", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("resourceName").GetString()));
    }

    private sealed class ProbeResult : IResult
    {
        private readonly Action _probe;

        internal ProbeResult(Action probe) => _probe = probe;

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            _probe();
            await httpContext.Response.WriteAsync("ok");
        }
    }

    private sealed class TrackingReadStream : MemoryStream
    {
        internal TrackingReadStream(byte[] bytes) : base(bytes, writable: false) { }

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

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ReadCount++;
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class CancellationReadStream : Stream
    {
        private readonly CancellationToken _cancellationToken;

        internal CancellationReadStream(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        internal TaskCompletionSource<bool> ReadStarted { get; } =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadStarted.TrySetResult(true);
            _cancellationToken.WaitHandle.WaitOne();
            throw new OperationCanceledException(_cancellationToken);
        }

        public override int Read(Span<byte> buffer)
        {
            ReadStarted.TrySetResult(true);
            _cancellationToken.WaitHandle.WaitOne();
            throw new OperationCanceledException(_cancellationToken);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult(true);
            await WaitForCancellation(cancellationToken);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult(true);
            await WaitForCancellation(cancellationToken);
            return 0;
        }

        private async Task WaitForCancellation(CancellationToken readToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, readToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, linked.Token);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
