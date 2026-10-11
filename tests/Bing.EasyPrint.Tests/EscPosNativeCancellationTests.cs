using System;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 Native 文字取消入口不会追加部分命令，并保留旧字节和字宽。</summary>
public sealed class EscPosNativeCancellationTests
{
    [Fact]
    public void Pre_cancelled_text_does_not_append_and_command_can_be_reused()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Initialize();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => command.Text("discard", cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => command.WriteText("discard", cancellation.Token));
        command.WriteText("OK", CancellationToken.None);
        Assert.Equal(new byte[] { 0x1B, 0x40, 0x4F, 0x4B }, command.Build().GetBytes());
    }

    [Fact]
    public void None_text_overloads_keep_legacy_bytes()
    {
        var legacy = new EscPosPrintCommand(Encoding.UTF8).Initialize().Text("A中").WriteText("B");
        var current = new EscPosPrintCommand(Encoding.UTF8).Initialize()
            .Text("A中", CancellationToken.None).WriteText("B", CancellationToken.None);
        Assert.Equal(legacy.Build().GetBytes(), current.Build(CancellationToken.None).GetBytes());
    }

    [Fact]
    public void Device_width_measurement_honors_cancellation_and_keeps_legacy_width()
    {
        var profile = new EscPosTextProfile { EncodingCodePage = 65001 };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => profile.MeasureTextWidth("A中", cancellation.Token));
        Assert.Equal(36, profile.MeasureTextWidth("A中", CancellationToken.None));
        Assert.Equal(profile.MeasureTextWidth("A中"), profile.MeasureTextWidth("A中", CancellationToken.None));
    }
}
