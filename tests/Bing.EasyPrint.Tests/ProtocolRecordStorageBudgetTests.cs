using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证新协议动作记录、原始数据保留和独立输出边界的预算回收。</summary>
public sealed class ProtocolRecordStorageBudgetTests
{
    [Theory]
    [InlineData("EscPos")]
    [InlineData("Tspl")]
    [InlineData("Zpl")]
    public void Many_empty_raw_records_are_rejected_before_build_and_existing_output_recovers(
        string protocol)
    {
        var command = Create(protocol);
        command.Raw("keep");
        var beforeBytes = command.Build();

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            // Fill several capacity generations, then measure two stable records.
            for (var index = 0; index < 16; index++) command.Raw(string.Empty);
            command.Raw(string.Empty);
            var beforeProbe = scope.CurrentBytes;
            command.Raw(string.Empty);
            var firstStableDelta = scope.CurrentBytes - beforeProbe;
            var beforeStableProbe = scope.CurrentBytes;
            command.Raw(string.Empty);
            var stableDelta = scope.CurrentBytes - beforeStableProbe;

            Assert.True(firstStableDelta > 0);
            Assert.Equal(firstStableDelta, stableDelta);

            var beforeReject = scope.CurrentBytes;
            scope.LimitTo(beforeReject + stableDelta - 1);
            var error = Assert.Throws<PrintResourceLimitException>(() => command.Raw(string.Empty));

            Assert.Equal("Command.Actions.Record", error.ResourceName);
            Assert.Equal(beforeReject, scope.CurrentBytes);
        }

        Assert.Equal(beforeBytes, command.Build());
        command.Raw("tail");
        Assert.Equal(Encoding.ASCII.GetBytes("keeptail"), command.Build());
    }

    [Theory]
    [InlineData("EscPos")]
    [InlineData("Tspl")]
    [InlineData("Zpl")]
    public void Large_text_or_raw_append_failure_preserves_output_and_allows_short_retry(
        string protocol)
    {
        const string initial = "keep";
        const string shortValue = "OK";
        var command = Create(protocol);
        AppendTextOrRaw(command, protocol, initial);

        var shortCost = ProbeTextOrRawCost(protocol, shortValue);
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        using (PrintResourceScope.Reserve(7, "test-baseline"))
        {
            var before = scope.CurrentBytes;
            scope.LimitTo(before + shortCost);

            var error = Assert.Throws<PrintResourceLimitException>(() =>
                AppendTextOrRaw(command, protocol, new string('X', 4096)));

            Assert.Equal("Command.RecordData", error.ResourceName);
            Assert.Equal(before, error.CurrentBytes);
            Assert.Equal(before, scope.CurrentBytes);

            AppendTextOrRaw(command, protocol, shortValue);
            Assert.True(scope.CurrentBytes > before);
        }

        var expected = Create(protocol);
        AppendTextOrRaw(expected, protocol, initial);
        AppendTextOrRaw(expected, protocol, shortValue);
        Assert.Equal(expected.Build(), command.Build());
    }

    [Theory]
    [InlineData("EscPos")]
    [InlineData("Tspl")]
    [InlineData("Zpl")]
    public void RawBytes_snapshot_is_released_when_action_record_budget_is_rejected(string protocol)
    {
        var command = Create(protocol);
        command.RawBytes(new byte[] { 0x10, 0x20 });
        var payload = new byte[512];
        for (var index = 0; index < payload.Length; index++) payload[index] = 0x5A;

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        using (PrintResourceScope.Reserve(7, "test-baseline"))
        {
            var before = scope.CurrentBytes;
            scope.LimitTo(before + payload.LongLength);

            var error = Assert.Throws<PrintResourceLimitException>(() => command.RawBytes(payload));

            Assert.Equal("Command.Actions.Record", error.ResourceName);
            Assert.Equal(before, scope.CurrentBytes);

            command.RawBytes(new byte[] { 0x7E });
            Assert.True(scope.CurrentBytes > before);
        }

        Assert.Equal(new byte[] { 0x10, 0x20, 0x7E }, command.Build());
    }

    [Fact]
    public void Tspl_print_with_pending_media_budget_failure_preserves_bytes_and_retries_once()
    {
        var options = PendingTsplMedia();
        var printCost = ProbeTsplControlledCost(options, command => command.Print());
        var command = new TsplPrintCommand(Encoding.ASCII, options);
        var beforeBytes = command.Build().GetBytes();

        using (var scope = PrintResourceScope.Begin(checked(printCost - 1)))
        {
            var before = scope.CurrentBytes;
            var error = Assert.Throws<PrintResourceLimitException>(() => command.Print());

            Assert.NotNull(error);
            Assert.Equal(before, scope.CurrentBytes);
        }

        Assert.Equal(beforeBytes, command.Build().GetBytes());

        using (var retryScope = PrintResourceScope.Begin(long.MaxValue))
        {
            command.Print();
            Assert.True(retryScope.CurrentBytes > 0);
        }

        var output = Encoding.ASCII.GetString(command.Build().GetBytes());
        Assert.Equal(1, CountOccurrences(output, "GAP 2,1\r\n"));
        Assert.Equal(1, CountOccurrences(output, "DIRECTION 1\r\n"));
        Assert.Equal(1, CountOccurrences(output, "PRINT 1,1\r\n"));
    }

    [Fact]
    public void Tspl_cut_with_pending_media_budget_failure_preserves_bytes_and_retries_once()
    {
        var options = PendingTsplMedia(new TsplCutterProfile
        {
            Strategy = TsplCutStrategy.Immediate,
            SupportedCutModes = new[] { PrintCutMode.Full },
            SupportsEndOfJob = true
        });
        var cut = new PrintCutOptions { Mode = PrintCutMode.Full };
        var cutCost = ProbeTsplControlledCost(options, command => command.Cut(cut));
        var command = new TsplPrintCommand(Encoding.ASCII, options);
        var beforeBytes = command.Build().GetBytes();

        using (var scope = PrintResourceScope.Begin(checked(cutCost - 1)))
        {
            var before = scope.CurrentBytes;
            var error = Assert.Throws<PrintResourceLimitException>(() => command.Cut(cut));

            Assert.NotNull(error);
            Assert.Equal(before, scope.CurrentBytes);
        }

        Assert.Equal(beforeBytes, command.Build().GetBytes());

        using (var retryScope = PrintResourceScope.Begin(long.MaxValue))
        {
            command.Cut(cut);
            Assert.True(retryScope.CurrentBytes > 0);
        }

        var output = Encoding.ASCII.GetString(command.Build().GetBytes());
        Assert.Equal(1, CountOccurrences(output, "GAP 2,1\r\n"));
        Assert.Equal(1, CountOccurrences(output, "DIRECTION 1\r\n"));
        Assert.Equal(1, CountOccurrences(output, "CUT\r\n"));
    }

    [Fact]
    public void Delayed_end_boundary_failure_recovers_in_a_new_scope_and_preserves_control_order()
    {
        var command = NewDelayedCommand();

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            command.Start()
                .Label(60, 40)
                .Text(1, 2, "A")
                .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full });
            var before = scope.CurrentBytes;
            scope.LimitTo(before);

            var error = Assert.Throws<PrintResourceLimitException>(() => command.End());

            Assert.Equal("Zpl.OutputBoundaries.Capacity", error.ResourceName);
            Assert.Equal(before, error.CurrentBytes);
            Assert.Equal(before, scope.CurrentBytes);
        }

        using (var retryScope = PrintResourceScope.Begin(long.MaxValue))
        {
            command.End();
            command.Start()
                .Label(60, 40)
                .Text(3, 4, "B")
                .PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Full })
                .End();

            var output = command.BuildOutput();
            Assert.Equal(4, output.Segments.Count);
            Assert.Equal(
                new[]
                {
                    PrintOutputSegmentKind.Commands,
                    PrintOutputSegmentKind.Control,
                    PrintOutputSegmentKind.Commands,
                    PrintOutputSegmentKind.Control
                }, output.Segments.Select(segment => segment.Kind));
            Assert.Equal(
                new[]
                {
                    "^XA\r\n^PW60\r\n^LL40\r\n" +
                    "^FO1,2^A0N,30,30^FDA^FS\r\n" +
                    "^MMD\r\n^PQ1\r\n^XZ\r\n",
                    "~JK\r\n",
                    "^XA\r\n^PW60\r\n^LL40\r\n" +
                    "^FO3,4^A0N,30,30^FDB^FS\r\n" +
                    "^MMD\r\n^PQ1\r\n^XZ\r\n",
                    "~JK\r\n"
                }, output.Segments.Select(segment => Encoding.ASCII.GetString(segment.Data)));
            Assert.Equal(output.Segments.Sum(segment => (long)segment.ByteLength), output.ByteLength);
        }
    }

    private static ProtocolCommandCase Create(string protocol)
    {
        switch (protocol)
        {
            case "EscPos":
            {
                var command = new EscPosPrintCommand(Encoding.ASCII);
                return new ProtocolCommandCase(
                    raw => command.WriteRaw(raw),
                    text => command.Text(text),
                    bytes => command.WriteRawBytes(bytes),
                    () => command.Build().GetBytes());
            }
            case "Tspl":
            {
                var command = new TsplPrintCommand(Encoding.ASCII);
                return new ProtocolCommandCase(
                    raw => command.WriteRaw(raw),
                    text => command.Text(0, 0, text),
                    bytes => command.WriteRawBytes(bytes),
                    () => command.Build().GetBytes());
            }
            case "Zpl":
            {
                var command = new ZplPrintCommand(Encoding.ASCII);
                return new ProtocolCommandCase(
                    raw => command.WriteRaw(raw),
                    text => command.Text(0, 0, text),
                    bytes => command.WriteRawBytes(bytes),
                    () => command.Build().GetBytes());
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null);
        }
    }

    private static long ProbeTextOrRawCost(string protocol, string value)
    {
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var command = Create(protocol);
            var before = scope.CurrentBytes;
            AppendTextOrRaw(command, protocol, value);
            return scope.CurrentBytes - before;
        }
    }

    private static long ProbeTsplControlledCost(TsplMediaOptions options, Action<TsplPrintCommand> action)
    {
        var command = new TsplPrintCommand(Encoding.ASCII, options);
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            action(command);
            return scope.CurrentBytes - before;
        }
    }

    private static TsplMediaOptions PendingTsplMedia(TsplCutterProfile cutter = null)
        => new TsplMediaOptions
        {
            GapDistance = 2,
            GapOffset = 1,
            Direction = 1,
            Profile = cutter == null ? null : new TsplPrinterProfile { Cutter = cutter }
        };

    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        for (var index = 0; ; index += expected.Length)
        {
            index = value.IndexOf(expected, index, StringComparison.Ordinal);
            if (index < 0) return count;
            count++;
        }
    }

    private static void AppendTextOrRaw(ProtocolCommandCase command, string protocol, string value)
    {
        if (protocol == "EscPos") command.Text(value);
        else command.Raw(value);
    }

    private static ZplPrintCommand NewDelayedCommand()
        => new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile
            {
                Cutter = new ZplCutterProfile
                {
                    Strategy = ZplCutStrategy.Delayed,
                    SupportedCutModes = new[] { PrintCutMode.Full },
                    SupportsTearOff = true
                }
            }
        });

    private sealed class ProtocolCommandCase
    {
        internal readonly Action<string> Raw;
        internal readonly Action<string> Text;
        internal readonly Action<byte[]> RawBytes;
        internal readonly Func<byte[]> Build;

        internal ProtocolCommandCase(Action<string> raw, Action<string> text,
            Action<byte[]> rawBytes, Func<byte[]> build)
        {
            Raw = raw;
            Text = text;
            RawBytes = rawBytes;
            Build = build;
        }
    }
}
