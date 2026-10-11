using System;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 TSPL/ZPL 标签命令的可取消输入转换和失败恢复行为。</summary>
public sealed class LabelCommandCancellationTests
{
    [Fact]
    public void Tspl_bitmap_pre_cancellation_does_not_append_binary_input()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => command.Bitmap(
            0, 0, 2, 1, 0, new byte[] { 0x80, 0x01 }, cancellation.Token));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_bitmap_none_overload_matches_legacy_and_cancelled_command_recovers()
    {
        var raster = new byte[] { 0x80, 0x01 };
        var legacy = new TsplPrintCommand(Encoding.ASCII)
            .Bitmap(0, 0, 2, 1, 0, raster)
            .Build().GetBytes();
        var explicitNone = new TsplPrintCommand(Encoding.ASCII)
            .Bitmap(0, 0, 2, 1, 0, raster, CancellationToken.None)
            .Build().GetBytes();

        Assert.Equal(legacy, explicitNone);

        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => command.Bitmap(
            0, 0, 2, 1, 0, raster, cancellation.Token));

        var recovered = command.Bitmap(0, 0, 2, 1, 0, raster, CancellationToken.None)
            .Build().GetBytes();
        var expected = new TsplPrintCommand(Encoding.ASCII)
            .WriteRaw("before")
            .Bitmap(0, 0, 2, 1, 0, raster)
            .Build().GetBytes();
        Assert.Equal(expected, recovered);
    }

    [Fact]
    public void Tspl_diagonal_line_none_overload_matches_legacy()
    {
        var options = new TsplMediaOptions { AllowRasterizedLines = true };
        var legacy = new TsplPrintCommand(Encoding.ASCII, options)
            .Line(0, 0, 7, 7, 1)
            .Build().GetBytes();
        var explicitNone = new TsplPrintCommand(Encoding.ASCII, options)
            .Line(0, 0, 7, 7, 1, CancellationToken.None)
            .Build().GetBytes();

        Assert.Equal(legacy, explicitNone);
    }

    [Fact]
    public void Zpl_graphic_field_pre_cancellation_does_not_append_hex_input()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => command.GraphicField(
            0, 0, 1, 1, "80", cancellation.Token));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_graphic_field_none_overload_matches_legacy_and_cancelled_command_recovers()
    {
        var raster = new byte[] { 0x80 };
        var legacy = new ZplPrintCommand(Encoding.ASCII)
            .GraphicField(0, 0, 1, 1, raster)
            .Build().GetBytes();
        var explicitNone = new ZplPrintCommand(Encoding.ASCII)
            .GraphicField(0, 0, 1, 1, raster, CancellationToken.None)
            .Build().GetBytes();

        Assert.Equal(legacy, explicitNone);

        var legacyHex = new ZplPrintCommand(Encoding.ASCII)
            .GraphicField(0, 0, 1, 1, "80")
            .Build().GetBytes();
        var explicitNoneHex = new ZplPrintCommand(Encoding.ASCII)
            .GraphicField(0, 0, 1, 1, "80", CancellationToken.None)
            .Build().GetBytes();
        Assert.Equal(legacyHex, explicitNoneHex);

        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => command.GraphicField(
            0, 0, 1, 1, raster, cancellation.Token));

        var recovered = command.GraphicField(0, 0, 1, 1, raster, CancellationToken.None)
            .Build().GetBytes();
        var expected = new ZplPrintCommand(Encoding.ASCII)
            .WriteRaw("before")
            .GraphicField(0, 0, 1, 1, raster)
            .Build().GetBytes();
        Assert.Equal(expected, recovered);
    }

    [Fact]
    public void Zpl_graphic_field_rejects_budget_before_hex_string_allocation_without_appending()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        using (PrintResourceScope.Begin(5))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() => command.GraphicField(
                0, 0, 1, 1, new byte[] { 0x80 }, CancellationToken.None));

            Assert.Equal("Zpl.HexString", error.ResourceName);
        }

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_diagonal_line_none_overload_matches_legacy()
    {
        var options = new ZplMediaOptions { AllowRasterizedLines = true };
        var legacy = new ZplPrintCommand(Encoding.ASCII, options)
            .Line(2, 2, 9, 9, 2)
            .Build().GetBytes();
        var explicitNone = new ZplPrintCommand(Encoding.ASCII, options)
            .Line(2, 2, 9, 9, 2, CancellationToken.None)
            .Build().GetBytes();

        Assert.Equal(legacy, explicitNone);
    }
}
