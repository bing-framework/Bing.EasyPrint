using System.Text;
using Bing.EasyPrint.Printing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[CollectionDefinition("HostAdmissionEnvironment", DisableParallelization = true)]
public sealed class HostAdmissionEnvironmentCollection
{
}

[Collection("HostAdmissionEnvironment")]
public sealed class HostAdmissionConfigurationTests
{
    private const string TotalVariable = "EASYPRINT_DESIGNER_MAX_TOTAL_WORKING_BYTES";
    private const string ConcurrentVariable = "EASYPRINT_DESIGNER_MAX_CONCURRENT_REQUESTS";
    private const string QueuedVariable = "EASYPRINT_DESIGNER_MAX_QUEUED_REQUESTS";
    private const string ValidRequest =
        "{\"template\":{\"Version\":1,\"Width\":80,\"Height\":40,\"Rows\":[]},\"data\":{}}";

    private static readonly string[] AdmissionVariables =
    {
        TotalVariable,
        ConcurrentVariable,
        QueuedVariable
    };

    [Fact]
    public void All_admission_environment_variables_unset_returns_null()
    {
        RunWithEnvironment(() =>
        {
            Assert.Null(DesignerRequestAdmission.FromEnvironment(1024));
        });
    }

    [Fact]
    public void Queue_without_total_or_concurrency_is_rejected()
    {
        RunWithEnvironment(() =>
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                DesignerRequestAdmission.FromEnvironment(null));
        }, (QueuedVariable, "1"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("invalid")]
    public void Invalid_concurrent_environment_values_are_rejected(string value)
    {
        RunWithEnvironment(() =>
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                DesignerRequestAdmission.FromEnvironment(null));
        }, (ConcurrentVariable, value));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("9223372036854775808")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("invalid")]
    public void Invalid_total_environment_values_are_rejected(string value)
    {
        RunWithEnvironment(() =>
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                DesignerRequestAdmission.FromEnvironment(1));
        }, (TotalVariable, value));
    }

    [Fact]
    public void Total_requires_an_explicit_request_limit()
    {
        RunWithEnvironment(() =>
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                DesignerRequestAdmission.FromEnvironment(null));
        }, (TotalVariable, "1"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("9223372036854775807")]
    public void Nonnegative_int64_total_with_request_limit_is_accepted(string value)
    {
        RunWithEnvironment(() =>
        {
            var admission = DesignerRequestAdmission.FromEnvironment(1);
            Assert.NotNull(admission);
            Assert.Equal<long?>(1, admission!.RequestMaxWorkingBytes);
        }, (TotalVariable, value));
    }

    [Fact]
    public async Task Queue_defaults_to_zero_and_full_concurrency_rejects_immediately()
    {
        await RunWithEnvironmentAsync(async () =>
        {
            var admission = DesignerRequestAdmission.FromEnvironment(null);
            Assert.NotNull(admission);
            Assert.Equal(0, admission!.Snapshot.MaxQueuedRequests);

            var active = await admission.EnterAsync(CancellationToken.None);
            try
            {
                var rejected = await Assert.ThrowsAsync<DesignerAdmissionRejectedException>(async () =>
                    await admission.EnterAsync(CancellationToken.None));
                Assert.Equal("QueueFull", rejected.Reason);
                Assert.Equal(0, admission.Snapshot.QueuedRequests);
            }
            finally
            {
                active.Dispose();
            }

            Assert.Equal(0, admission.Snapshot.ActiveRequests);
        }, (ConcurrentVariable, "1"));
    }

    [Fact]
    public async Task Valid_environment_fields_construct_and_reserve_one_request_budget()
    {
        await RunWithEnvironmentAsync(async () =>
        {
            var admission = DesignerRequestAdmission.FromEnvironment(100);
            Assert.NotNull(admission);
            Assert.Equal<long?>(100, admission!.RequestMaxWorkingBytes);

            var first = await admission.EnterAsync(CancellationToken.None);
            var second = await admission.EnterAsync(CancellationToken.None);
            using var firstQueuedCancellation = new CancellationTokenSource();
            using var secondQueuedCancellation = new CancellationTokenSource();
            using var thirdQueuedCancellation = new CancellationTokenSource();
            var firstQueued = admission.EnterAsync(firstQueuedCancellation.Token).AsTask();
            var secondQueued = admission.EnterAsync(secondQueuedCancellation.Token).AsTask();
            var thirdQueued = admission.EnterAsync(thirdQueuedCancellation.Token).AsTask();
            try
            {
                Assert.Equal(2, admission.Snapshot.ActiveRequests);
                Assert.Equal(200L, admission.Snapshot.ReservedWorkingBytes);
                Assert.Equal(2, admission.Snapshot.MaxActualParallelism);
                Assert.Equal(3, admission.Snapshot.QueuedRequests);
                Assert.Equal(3, admission.Snapshot.MaxQueuedRequests);

                var rejected = await Assert.ThrowsAsync<DesignerAdmissionRejectedException>(async () =>
                    await admission.EnterAsync(CancellationToken.None));
                Assert.Equal("QueueFull", rejected.Reason);

                first.Dispose();
                second.Dispose();
                var firstQueuedLease = await firstQueued;
                var secondQueuedLease = await secondQueued;
                firstQueuedLease.Dispose();
                secondQueuedLease.Dispose();
                var thirdQueuedLease = await thirdQueued;
                thirdQueuedLease.Dispose();
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                await CancelAndDrainAsync(firstQueued, firstQueuedCancellation);
                await CancelAndDrainAsync(secondQueued, secondQueuedCancellation);
                await CancelAndDrainAsync(thirdQueued, thirdQueuedCancellation);
            }

            Assert.Equal(0, admission.Snapshot.ActiveRequests);
            Assert.Equal(0, admission.Snapshot.QueuedRequests);
            Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
        },
        (TotalVariable, "200"),
        (ConcurrentVariable, "2"),
        (QueuedVariable, "3"));
    }

    [Fact]
    public async Task Zero_request_and_total_budget_enters_admission_then_fails_request_scope_with_413()
    {
        await RunWithEnvironmentAsync(async () =>
        {
            var admission = DesignerRequestAdmission.FromEnvironment(0);
            Assert.NotNull(admission);

            var context = CreateContext();
            var invoked = false;
            await DesignerRequestExecution.ExecuteAsync(context, (_, _) =>
            {
                invoked = true;
                return Results.Text("unexpected");
            }, admission: admission);

            Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
            Assert.False(invoked);
            Assert.Equal(0, admission!.Snapshot.ActiveRequests);
            Assert.Equal(0, admission.Snapshot.QueuedRequests);
            Assert.Equal(0L, admission.Snapshot.ReservedWorkingBytes);
        }, (TotalVariable, "0"));
    }

    private static void RunWithEnvironment(Action action,
        params (string Name, string? Value)[] overrides)
    {
        var previous = CaptureEnvironment();
        try
        {
            ClearEnvironment();
            ApplyEnvironment(overrides);
            action();
        }
        finally
        {
            RestoreEnvironment(previous);
        }
    }

    private static async Task RunWithEnvironmentAsync(Func<Task> action,
        params (string Name, string? Value)[] overrides)
    {
        var previous = CaptureEnvironment();
        try
        {
            ClearEnvironment();
            ApplyEnvironment(overrides);
            await action();
        }
        finally
        {
            RestoreEnvironment(previous);
        }
    }

    private static Dictionary<string, string?> CaptureEnvironment()
    {
        var previous = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var name in AdmissionVariables)
            previous[name] = Environment.GetEnvironmentVariable(name);
        return previous;
    }

    private static void ClearEnvironment()
    {
        foreach (var name in AdmissionVariables)
            Environment.SetEnvironmentVariable(name, null);
    }

    private static void ApplyEnvironment((string Name, string? Value)[] overrides)
    {
        foreach (var item in overrides)
            Environment.SetEnvironmentVariable(item.Name, item.Value);
    }

    private static void RestoreEnvironment(Dictionary<string, string?> previous)
    {
        foreach (var name in AdmissionVariables)
            Environment.SetEnvironmentVariable(name, previous[name]);
    }

    private static async Task CancelAndDrainAsync(
        Task<IDisposable> pending, CancellationTokenSource cancellation)
    {
        if (!pending.IsCompleted)
            cancellation.Cancel();
        try
        {
            var lease = await pending;
            lease.Dispose();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static DefaultHttpContext CreateContext()
    {
        var body = new MemoryStream(Encoding.UTF8.GetBytes(ValidRequest), writable: false);
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = "application/json; charset=utf-8";
        context.Response.Body = new MemoryStream();
        return context;
    }
}
