using System;
using System.Text;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 CPCL 元数据追加前的存储估算、回滚和边界替换恢复。</summary>
public sealed class CpclRecordStorageBudgetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Many_small_raw_or_text_records_reject_without_partial_append_and_retry_in_same_scope(
        bool text)
    {
        const string seed = "seed";
        const string value = "OK";
        var command = new CPCLPrintCommand(Encoding.ASCII);

        var calibrationCount = 0;
        long stableCost = 0;
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            AppendRawOrText(command, text, seed);

            var sameCostCount = 0;
            for (; calibrationCount < 256 &&
                   (calibrationCount < 16 || sameCostCount < 3); calibrationCount++)
            {
                var before = scope.CurrentBytes;
                AppendRawOrText(command, text, value);
                var cost = scope.CurrentBytes - before;
                Assert.True(cost > 1);

                if (cost == stableCost)
                    sameCostCount++;
                else
                {
                    stableCost = cost;
                    sameCostCount = 1;
                }
            }

            Assert.True(sameCostCount >= 3, "未能取得不跨容量扩展的稳定记录成本。");
            var countBeforeFailure = command.Content.Content.Count;
            var bytesBeforeFailure = scope.CurrentBytes;

            // Leave exactly one byte available for the next stable record.
            scope.LimitTo(checked(bytesBeforeFailure + stableCost));
            var placeholder = PrintResourceScope.Reserve(stableCost - 1, "Cpcl.TestPlaceholder");
            try
            {
                var error = Assert.Throws<PrintResourceLimitException>(() =>
                    AppendRawOrText(command, text, value));

                Assert.Equal(countBeforeFailure, command.Content.Content.Count);
                Assert.Equal(bytesBeforeFailure + stableCost - 1, scope.CurrentBytes);
                Assert.Equal(scope.CurrentBytes, error.CurrentBytes);
            }
            finally
            {
                placeholder.Dispose();
            }

            Assert.Equal(bytesBeforeFailure, scope.CurrentBytes);
            AppendRawOrText(command, text, value);
            Assert.Equal(countBeforeFailure + 1, command.Content.Content.Count);
            Assert.Equal(bytesBeforeFailure + stableCost, scope.CurrentBytes);
        }

        var expected = new CPCLPrintCommand(Encoding.ASCII);
        AppendRawOrText(expected, text, seed);
        for (var index = 0; index < calibrationCount + 1; index++)
            AppendRawOrText(expected, text, value);

        Assert.Equal(expected.Build().GetBytes(), command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Long_text_fields_are_budgeted_before_cpcl_metadata_is_appended(int metadataKind)
    {
        var value = new string('X', 512);
        var probe = new CPCLPrintCommand(Encoding.ASCII);
        long cost;

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            AppendMetadata(probe, metadataKind, value);
            cost = scope.CurrentBytes - before;
        }

        Assert.True(cost > 1);
        var command = new CPCLPrintCommand(Encoding.ASCII);
        using (var scope = PrintResourceScope.Begin(cost - 1))
        {
            var before = scope.CurrentBytes;
            Assert.Throws<PrintResourceLimitException>(() =>
                AppendMetadata(command, metadataKind, value));

            Assert.Empty(command.Content.Content);
            Assert.Equal(before, scope.CurrentBytes);
        }
    }

    [Theory]
    [InlineData("Begin")]
    [InlineData("PageWidth")]
    [InlineData("End")]
    public void Boundary_replacement_budget_failure_preserves_old_reference_state_and_bytes(string boundary)
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        var oldValue = OldBoundaryValue(boundary);
        var replacementValue = ReplacementBoundaryValue(boundary);
        SetBoundary(command, boundary, oldValue);

        var oldBoundary = GetBoundary(command, boundary);
        var oldBytes = command.Build().GetBytes();
        var replacementCost = MeasureBoundaryCost(boundary, replacementValue);
        Assert.True(replacementCost > 1);

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            scope.LimitTo(checked(before + replacementCost));
            var placeholder = PrintResourceScope.Reserve(replacementCost - 1, "Cpcl.TestPlaceholder");
            try
            {
                Assert.Throws<PrintResourceLimitException>(() =>
                    SetBoundary(command, boundary, replacementValue));

                Assert.Same(oldBoundary, GetBoundary(command, boundary));
                Assert.Empty(command.Content.Content);
                Assert.Equal(before + replacementCost - 1, scope.CurrentBytes);
            }
            finally
            {
                placeholder.Dispose();
            }
        }

        Assert.Same(oldBoundary, GetBoundary(command, boundary));
        Assert.Equal(oldBytes, command.Build().GetBytes());
    }

    [Fact]
    public void Clear_releases_record_estimates_but_retains_the_existing_content_capacity()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            command.WriteRaw("A");
            command.WriteRaw("B");
            var capacity = command.Content.Content.Capacity;
            var withRecords = scope.CurrentBytes;

            command.Content.Clear();

            Assert.Empty(command.Content.Content);
            Assert.Equal(capacity, command.Content.Content.Capacity);
            Assert.True(scope.CurrentBytes < withRecords);
            Assert.True(scope.CurrentBytes > 0);

            var afterClear = scope.CurrentBytes;
            command.WriteRaw("C");
            Assert.Equal(capacity, command.Content.Content.Capacity);
            Assert.True(scope.CurrentBytes > afterClear);
        }
    }

    [Fact]
    public void Repeated_safe_init_replaces_boundaries_without_accumulating_budget()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        var info = new CPCLCommandInfo
        {
            Offset = 2,
            Width = 200,
            Height = 100,
            Qty = 1
        };

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            command.Content.SafeInit(info);
            var first = scope.CurrentBytes;
            Assert.True(first > 0);

            command.Content.SafeInit(info);
            Assert.Equal(first, scope.CurrentBytes);
        }
    }

    private static void AppendRawOrText(CPCLPrintCommand command, bool text, string value)
    {
        if (text)
        {
            command.DrawText(0, 0, value, 24, 0, false, false, false);
            return;
        }

        command.WriteRaw(value);
    }

    private static void AppendMetadata(CPCLPrintCommand command, int metadataKind, string value)
    {
        switch (metadataKind)
        {
            case 0:
                command.DrawText(0, 0, value, 24, 0, false, false, false);
                break;
            case 1:
                command.DrawBarcode1D("128", 0, 0, value, 1, 20, 2, 0);
                break;
            case 2:
                command.DrawQrCode(0, 0, value, 2, "M", 0);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(metadataKind), metadataKind, null);
        }
    }

    private static string OldBoundaryValue(string boundary)
    {
        switch (boundary)
        {
            case "Begin": return "! 0 200 200 100 1";
            case "PageWidth": return "PAGE-WIDTH 200";
            case "End": return "PRINT";
            default: throw new ArgumentOutOfRangeException(nameof(boundary), boundary, null);
        }
    }

    private static string ReplacementBoundaryValue(string boundary)
    {
        switch (boundary)
        {
            case "Begin": return "! 2 200 200 300 1";
            case "PageWidth": return "PAGE-WIDTH 400";
            case "End": return "PRINT ";
            default: throw new ArgumentOutOfRangeException(nameof(boundary), boundary, null);
        }
    }

    private static void SetBoundary(CPCLPrintCommand command, string boundary, string value)
    {
        switch (boundary)
        {
            case "Begin":
            case "PageWidth":
            case "End":
                command.WriteRaw(value);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(boundary), boundary, null);
        }
    }

    private static MetadataBase GetBoundary(CPCLPrintCommand command, string boundary)
    {
        switch (boundary)
        {
            case "Begin": return command.Content.Begin;
            case "PageWidth": return command.Content.PageWidth;
            case "End": return command.Content.End;
            default: throw new ArgumentOutOfRangeException(nameof(boundary), boundary, null);
        }
    }

    private static long MeasureBoundaryCost(string boundary, string value)
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            SetBoundary(command, boundary, value);
            return scope.CurrentBytes - before;
        }
    }
}
