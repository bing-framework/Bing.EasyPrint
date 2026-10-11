using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证资源分配后的填充失败、取消和跨异步作用域释放恢复。</summary>
public sealed class PrintResourceFailureRecoveryTests
{
    [Fact]
    public void Failed_fill_returns_the_budget_and_allows_a_retry_in_the_same_scope()
    {
        using (var scope = PrintResourceScope.Begin(32))
        using (PrintResourceScope.Reserve(4, "baseline"))
        {
            var before = scope.CurrentBytes;
            Assert.Throws<InvalidOperationException>(() => BudgetedBuffer.Create(
                8, "test.fill", _ => throw new InvalidOperationException("fill failed")));

            Assert.Equal(before, scope.CurrentBytes);

            var result = BudgetedBuffer.Create(8, "test.fill", bytes =>
            {
                for (var index = 0; index < bytes.Length; index++) bytes[index] = 0x5A;
            });

            Assert.Equal(before + 8, scope.CurrentBytes);
            Assert.All(result, value => Assert.Equal((byte)0x5A, value));
        }
    }

    [Fact]
    public void Cancelled_fill_returns_the_budget_before_the_same_scope_can_retry()
    {
        using (var scope = PrintResourceScope.Begin(32))
        using (var cancellation = new CancellationTokenSource())
        {
            var before = scope.CurrentBytes;
            Assert.Throws<OperationCanceledException>(() => BudgetedBuffer.Create(
                8, "test.cancel", _ =>
                {
                    cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                }));

            Assert.Equal(before, scope.CurrentBytes);
            var result = BudgetedBuffer.Create(8, "test.cancel", bytes => bytes[0] = 1);
            Assert.Equal(before + 8, scope.CurrentBytes);
            Assert.Equal((byte)1, result[0]);
        }
    }

    [Fact]
    public async Task Parent_scope_rejects_cross_context_disposal_until_child_finishes()
    {
        var parent = PrintResourceScope.Begin(16);
        using (var childReady = new ManualResetEventSlim())
        using (var releaseChild = new ManualResetEventSlim())
        {
            var childTask = Task.Run(() =>
            {
                using (var child = PrintResourceScope.Begin(16))
                using (PrintResourceScope.Reserve(7, "child"))
                {
                    childReady.Set();
                    releaseChild.Wait();
                }
            });

            try
            {
                Assert.True(childReady.Wait(TimeSpan.FromSeconds(5)));
                Assert.Equal(7, parent.CurrentBytes);

                Assert.Throws<InvalidOperationException>(() => parent.Dispose());
                Assert.Equal(7, parent.CurrentBytes);

                // The rejected disposal must leave the parent usable.
                using (PrintResourceScope.Reserve(3, "parent"))
                    Assert.Equal(10, parent.CurrentBytes);
                Assert.Equal(7, parent.CurrentBytes);

                releaseChild.Set();
                await childTask;
                Assert.Equal(0, parent.CurrentBytes);
                parent.Dispose();
            }
            finally
            {
                releaseChild.Set();
                await childTask;
                parent.Dispose();
            }
        }
    }

    [Fact]
    public void Tspl_invalid_bitmap_hex_releases_the_post_allocation_budget_and_can_retry()
    {
        var command = new TsplPrintCommand(Encoding.ASCII);
        using (var scope = PrintResourceScope.Begin(2048))
        {
            var before = scope.CurrentBytes;
            Assert.Throws<ArgumentException>(() => command.Bitmap(
                0, 0, 2, 1, 0, "80?0", CancellationToken.None));

            Assert.Equal(before, scope.CurrentBytes);

            var expected = new TsplPrintCommand(Encoding.ASCII)
                .Bitmap(0, 0, 2, 1, 0, "8000")
                .Build().GetBytes();
            var recovered = command.Bitmap(0, 0, 2, 1, 0, "8000")
                .Build().GetBytes();

            Assert.Equal(expected, recovered);
        }
    }

    [Fact]
    public void Commit_transfers_only_the_final_writer_reservation_to_the_parent()
    {
        using (var parent = PrintResourceScope.Begin(1024))
        {
            BufferWriter writer;
            using (var temporary = PrintResourceScope.BeginTemporary())
            {
                writer = new BufferWriter(Encoding.ASCII);
                temporary.CommitReservations();
            }
            Assert.Equal(256, parent.CurrentBytes);
            // 扩容发生在提交之后，旧租约必须从新归属的父级归还。
            writer.Write(new byte[300]);
            Assert.Equal(512, parent.CurrentBytes);
            Assert.Equal(768, parent.PeakBytes);
        }
    }

    [Fact]
    public void Failed_long_build_is_reclaimed_and_short_command_can_retry_in_root_scope()
    {
        var longCommand = new TsplPrintCommand(Encoding.ASCII)
            .WriteRaw(new string('A', 300));
        var shortCommand = new TsplPrintCommand(Encoding.ASCII)
            .WriteRaw("OK");

        using (var scope = PrintResourceScope.Begin(500))
        {
            Assert.Throws<PrintResourceLimitException>(() => longCommand.Build());
            Assert.Equal(0, scope.CurrentBytes);

            var bytes = shortCommand.Build().GetBytes();

            Assert.Equal(Encoding.ASCII.GetBytes("OK"), bytes);
            Assert.Equal(262, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Direct_escpos_copies_failure_is_reclaimed_and_one_copy_can_retry()
    {
        var document = new PrintDocument();
        document.AddPage(8, 8).Copies = 1;
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII);
        long oneCopyPeak;
        using (var calibration = PrintResourceScope.Begin(long.MaxValue))
        {
            var oneCopy = Assert.Single(provider.Render(document));
            Assert.NotEmpty(oneCopy);
            oneCopyPeak = calibration.PeakBytes;
        }

        document.Pages[0].Copies = 100;
        using (var scope = PrintResourceScope.Begin(oneCopyPeak))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() => provider.Render(document));
            Assert.Equal("EscPos.Copies", error.ResourceName);
            Assert.Equal(0, scope.CurrentBytes);

            document.Pages[0].Copies = 1;
            var output = Assert.Single(provider.Render(document));

            Assert.NotEmpty(output);
            Assert.True(scope.CurrentBytes > 0);
        }
    }
}
