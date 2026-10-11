using System;
using System.Linq;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证独立输出 segment 和页面结果的存储预算事务与结果契约。</summary>
public sealed class PrintOutputStorageBudgetTests
{
    [Fact]
    public void Invalid_segment_and_page_inputs_do_not_leak_budget()
    {
        var segment = new PrintOutputSegment(new byte[] { 0x41 }, PrintOutputSegmentKind.Commands);

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;

            Assert.Throws<ArgumentNullException>(() => new PrintOutputSegment(null));
            Assert.Equal(before, scope.CurrentBytes);

            Assert.Throws<ArgumentOutOfRangeException>(() => new PrintOutputSegment(
                new byte[] { 0x42 }, (PrintOutputSegmentKind)99));
            Assert.Equal(before, scope.CurrentBytes);

            Assert.Throws<ArgumentNullException>(() => new PrintPageOutput(null));
            Assert.Equal(before, scope.CurrentBytes);

            Assert.Throws<ArgumentException>(() => new PrintPageOutput(
                Array.Empty<PrintOutputSegment>()));
            Assert.Equal(before, scope.CurrentBytes);

            Assert.Throws<ArgumentException>(() => new PrintPageOutput(
                new PrintOutputSegment[] { segment, null }));
            Assert.Equal(before, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Segment_budget_failure_rolls_back_snapshot_and_a_short_segment_can_retry()
    {
        var small = new byte[] { 0x10, 0x20 };
        var smallCost = MeasureSegmentCost(small, PrintOutputSegmentKind.Control);
        var large = new byte[512];

        using (var scope = PrintResourceScope.Begin(smallCost))
        {
            var before = scope.CurrentBytes;
            var error = Assert.Throws<PrintResourceLimitException>(() => new PrintOutputSegment(
                large, PrintOutputSegmentKind.Control));

            Assert.NotNull(error);
            Assert.Equal(before, scope.CurrentBytes);

            var result = new PrintOutputSegment(small, PrintOutputSegmentKind.Control);
            Assert.Equal(PrintOutputSegmentKind.Control, result.Kind);
            Assert.Equal(small, result.Data);
            Assert.Equal(before + smallCost, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Page_output_budget_failure_rolls_back_metadata_and_segment_collection()
    {
        var segments = new[]
        {
            new PrintOutputSegment(new byte[] { 0x41, 0x42 }, PrintOutputSegmentKind.Commands),
            new PrintOutputSegment(new byte[] { 0x43 }, PrintOutputSegmentKind.Control),
            new PrintOutputSegment(new byte[] { 0x44, 0x45, 0x46 }, PrintOutputSegmentKind.Commands),
            new PrintOutputSegment(new byte[] { 0x47 }, PrintOutputSegmentKind.Control),
            new PrintOutputSegment(new byte[] { 0x48 }, PrintOutputSegmentKind.Commands)
        };
        var outputCost = MeasurePageOutputCost(segments);

        using (var scope = PrintResourceScope.Begin(checked(outputCost - 1)))
        {
            var before = scope.CurrentBytes;
            var error = Assert.Throws<PrintResourceLimitException>(() => new PrintPageOutput(segments));

            Assert.NotNull(error);
            Assert.Equal(before, scope.CurrentBytes);
        }

        using (var retryScope = PrintResourceScope.Begin(long.MaxValue))
        {
            var output = new PrintPageOutput(segments);

            Assert.Equal(segments.Length, output.Segments.Count);
            Assert.Equal(segments.Sum(segment => (long)segment.ByteLength), output.ByteLength);
            Assert.Equal(
                new[]
                {
                    PrintOutputSegmentKind.Commands,
                    PrintOutputSegmentKind.Control,
                    PrintOutputSegmentKind.Commands,
                    PrintOutputSegmentKind.Control,
                    PrintOutputSegmentKind.Commands
                }, output.Segments.Select(segment => segment.Kind));
            Assert.Equal(new byte[] { 0x41, 0x42 }, output.Segments[0].Data);
            Assert.Throws<NotSupportedException>(() => output.Data);
            Assert.True(retryScope.CurrentBytes > 0);
        }
    }

    [Fact]
    public void Single_segment_page_output_preserves_metadata_and_legacy_data_view()
    {
        var input = new byte[] { 0x01, 0x02, 0x03 };
        var segment = new PrintOutputSegment(input, PrintOutputSegmentKind.Control);
        var output = new PrintPageOutput(new[] { segment });

        Assert.Single(output.Segments);
        Assert.Equal(PrintOutputSegmentKind.Control, output.Segments[0].Kind);
        Assert.Equal(input, output.Data);
        Assert.Equal(input.LongLength, output.ByteLength);
        Assert.Equal(input, output.Segments[0].Data);
    }

    private static long MeasureSegmentCost(byte[] data, PrintOutputSegmentKind kind)
    {
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            new PrintOutputSegment(data, kind);
            return scope.CurrentBytes - before;
        }
    }

    private static long MeasurePageOutputCost(PrintOutputSegment[] segments)
    {
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            new PrintPageOutput(segments);
            return scope.CurrentBytes - before;
        }
    }
}
