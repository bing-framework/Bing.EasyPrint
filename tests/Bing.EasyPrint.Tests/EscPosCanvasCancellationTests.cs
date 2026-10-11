using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosCanvasCancellationTests
{
    [Fact]
    public void Cancelled_constructor_rejects_the_token()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                new EscPosRasterCanvas(16, 16, null, cancellation.Token));
        }
    }

    [Fact]
    public void Cancellation_during_fill_restores_the_previous_bitmap()
    {
        const int width = 4096;
        const int height = 2048;
        using (var cancellation = new CancellationTokenSource())
        {
            var canvas = new EscPosRasterCanvas(width, height, null, cancellation.Token);
            var before = canvas.ToBytes();
            var backingBytes = GetBackingBytes(canvas);
            Exception failure = null;

            var draw = Task.Run(() =>
            {
                try
                {
                    canvas.DrawRectangle(0, 0, width, height, filled: true);
                }
                catch (Exception error)
                {
                    failure = error;
                }
            });

            try
            {
                var observedWrite = false;
                var observed = SpinWait.SpinUntil(() =>
                {
                    if (Volatile.Read(ref backingBytes[0]) != 0)
                    {
                        observedWrite = true;
                        return true;
                    }

                    // If the task finishes without a visible write, return so the
                    // joined task can produce a useful assertion failure below.
                    return draw.IsCompleted;
                }, TimeSpan.FromSeconds(5));

                cancellation.Cancel();
                draw.GetAwaiter().GetResult();

                Assert.True(observed && observedWrite,
                    "绘制未在任务结束前进入可观察的像素写入阶段。");
                Assert.IsType<OperationCanceledException>(failure);
                Assert.Equal(before, canvas.ToBytes());
            }
            finally
            {
                // Assertions above are intentionally made only after the worker
                // has joined. This fallback also cleans up when observation fails.
                cancellation.Cancel();
                if (!draw.IsCompleted) draw.GetAwaiter().GetResult();
            }
        }
    }

    [Fact]
    public void Cancellation_none_preserves_legacy_output()
    {
        var legacy = new EscPosRasterCanvas(64, 40);
        var explicitNone = new EscPosRasterCanvas(64, 40, null, CancellationToken.None);

        DrawLegacyShape(legacy);
        DrawLegacyShape(explicitNone);

        Assert.Equal(legacy.ToBytes(), explicitNone.ToBytes());
    }

    [Fact]
    public void ToBytes_with_a_cancelled_call_token_is_rejected()
    {
        using (var cancellation = new CancellationTokenSource())
        {
            var canvas = new EscPosRasterCanvas(8, 8);
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => canvas.ToBytes(cancellation.Token));
        }
    }

    private static void DrawLegacyShape(EscPosRasterCanvas canvas)
    {
        canvas.DrawLine(2, 2, 48, 20, 2);
        canvas.DrawRectangle(8, 8, 20, 12, filled: true);
        canvas.DrawRaster(36, 4, 12, 4, new byte[] { 0xF0, 0x00, 0x90, 0x00, 0xF0, 0x00, 0x90, 0x00 });
    }

    private static byte[] GetBackingBytes(EscPosRasterCanvas canvas)
    {
        var field = typeof(EscPosRasterCanvas).GetField("_bytes",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (byte[])field.GetValue(canvas);
    }
}
