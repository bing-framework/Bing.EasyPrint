using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证真实分配入口的累计预留、分配前拒绝和失败恢复。</summary>
public sealed class PrintWorkingBudgetTests
{
    [Fact]
    public void Writer_growth_counts_old_and_new_buffers_before_allocating()
    {
        using var scope = PrintResourceScope.Begin(500);
        var writer = new BufferWriter(Encoding.ASCII);
        writer.Write("OK");
        var error = Assert.Throws<PrintResourceLimitException>(() => writer.Write(new byte[300]));
        Assert.Equal("Command.BufferGrowth", error.ResourceName);
        Assert.Equal(260, error.CurrentBytes);
        Assert.Equal(512, error.RequestedBytes);
        Assert.Equal(Encoding.ASCII.GetBytes("OK"), writer.GetBytes());
    }

    [Fact]
    public void Writer_output_snapshots_share_the_working_budget()
    {
        using var scope = PrintResourceScope.Begin(350);
        var writer = new BufferWriter(Encoding.ASCII);
        writer.Write(new string('A', 30));
        var first = writer.GetBytes();
        var error = Assert.Throws<PrintResourceLimitException>(() => writer.GetBytes());
        Assert.Equal("Command.Output", error.ResourceName);
        Assert.Equal(346, scope.CurrentBytes);
        Assert.Equal(new string('A', 30), Encoding.ASCII.GetString(first));
    }

    [Fact]
    public void Raster_rollback_reserves_a_second_canvas_before_drawing()
    {
        EscPosRasterCanvas canvas;
        using var cancellation = new CancellationTokenSource();
        using (var scope = PrintResourceScope.Begin(8))
        {
            canvas = new EscPosRasterCanvas(8, 8, null, cancellation.Token);
            var error = Assert.Throws<PrintResourceLimitException>(() =>
                canvas.DrawRectangle(0, 0, 8, 8, filled: true));
            Assert.Equal("Raster.Rollback", error.ResourceName);
            Assert.Equal(8, scope.CurrentBytes);
        }
        Assert.Equal(new byte[8], canvas.ToBytes());
    }

    [Fact]
    public void Cpcl_image_conversion_checks_rgba_memory_before_cloning()
    {
        using var image = new Image<Rgba32>(8, 8, new Rgba32(255, 255, 255));
        using (var scope = PrintResourceScope.Begin(255))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() =>
                PrintImageRasterizer.ToCpclMonochrome(image));
            Assert.Equal("Image.CpclRgba", error.ResourceName);
            Assert.Equal(0, scope.CurrentBytes);
        }
        Assert.Equal(new byte[8], PrintImageRasterizer.ToCpclMonochrome(image));
    }

    [Fact]
    public void Direct_escpos_provider_checks_copies_before_allocating_and_can_retry()
    {
        var document = new PrintDocument();
        document.AddPage(8, 8).Copies = 1;
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII);
        long oneCopyPeak;
        using (var calibration = PrintResourceScope.Begin(long.MaxValue))
        {
            Assert.NotEmpty(Assert.Single(provider.Render(document)));
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
        }
    }

    [Fact]
    public void Job_pages_use_one_cumulative_budget_and_failure_does_not_change_document()
    {
        var document = new PrintDocument();
        document.AddPage(8, 8);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII);
        var renderer = new PrintJobRenderer();
        var options = new PrintJobOptions { Copies = 1 };
        long oneCopyPeak;
        using (var calibration = PrintResourceScope.Begin(long.MaxValue))
        {
            Assert.Single(renderer.Render(provider, document, options).Pages);
            oneCopyPeak = calibration.PeakBytes;
        }

        options.Copies = 40;
        using (var scope = PrintResourceScope.Begin(oneCopyPeak))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() => renderer.Render(provider, document, options));
            Assert.True(error.ResourceName.StartsWith("Job.", StringComparison.Ordinal) ||
                        error.ResourceName.StartsWith("Output.", StringComparison.Ordinal),
                $"Unexpected cumulative output resource: {error.ResourceName}");
            Assert.Equal(0, scope.CurrentBytes);
            Assert.Equal(1, document.Pages[0].Copies);

            options.Copies = 1;
            Assert.Single(renderer.Render(provider, document, options).Pages);
        }
        Assert.Equal(1, document.Pages[0].Copies);
    }

    [Fact]
    public async Task Parallel_child_scopes_share_parent_budget_and_restore_it()
    {
        using var parent = PrintResourceScope.Begin(10);
        using var ready = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var task = Task.Run(() =>
        {
            using var child = PrintResourceScope.Begin(10);
            using var reservation = PrintResourceScope.Reserve(7, "worker");
            ready.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
        });
        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(7, parent.CurrentBytes);
            Assert.Throws<PrintResourceLimitException>(() => PrintResourceScope.Reserve(4, "main"));
        }
        finally
        {
            release.Set();
            await task;
        }
        Assert.Equal(0, parent.CurrentBytes);
        Assert.Equal(7, parent.PeakBytes);
    }
}
