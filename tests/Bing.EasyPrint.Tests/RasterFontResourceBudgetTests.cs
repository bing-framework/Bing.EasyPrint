using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证固定字体资源和目录的容量、取消及失败恢复边界。</summary>
public sealed class RasterFontResourceBudgetTests
{
    [Fact]
    public void Failed_font_parse_releases_copies_for_a_same_scope_retry()
    {
        var regular = FixedTestFonts.Regular();
        using (var scope = PrintResourceScope.Begin(regular.LongLength + 1024))
        {
            Assert.Throws<ArgumentException>(() => new RasterFontResource(
                "Broken", "test", new byte[] { 0x00, 0x01, 0x02, 0x03 }));
            Assert.Equal(0, scope.CurrentBytes);

            var recovered = new RasterFontResource("Recovered", "test", regular);
            Assert.Equal(regular.LongLength, recovered.ByteLength);
            Assert.Equal(regular.LongLength, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Resource_budget_rejection_preserves_type_and_allows_a_same_scope_retry()
    {
        var small = FixedTestFonts.Regular();
        var larger = PadValidFont(small, checked(small.LongLength + 1));

        using (var scope = PrintResourceScope.Begin(small.LongLength))
        {
            Assert.Throws<PrintResourceLimitException>(() => new RasterFontResource(
                "OverBudget", "test", larger));
            Assert.Equal(0, scope.CurrentBytes);

            var recovered = new RasterFontResource("Recovered", "test", small);
            Assert.Equal(small.LongLength, recovered.ByteLength);
            Assert.Equal(small.LongLength, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Pre_cancelled_resource_and_catalog_do_not_start_work_or_reserve_bytes()
    {
        var regular = FixedTestFonts.Regular();
        var resource = new RasterFontResource("Primary", "test", regular);
        var enumerated = false;
        using (var scope = PrintResourceScope.Begin(regular.LongLength + 1024))
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => new RasterFontResource(
                "Cancelled", "test", regular, null, cancellation.Token));
            Assert.Throws<OperationCanceledException>(() => new RasterFontCatalog(
                EnumerateOnce(resource, () => enumerated = true), "Primary", null,
                cancellation.Token));

            Assert.False(enumerated);
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Legacy_and_explicit_none_font_entry_points_have_the_same_result()
    {
        var regular = FixedTestFonts.Regular();
        var legacyResource = new RasterFontResource("Primary", "test", regular);
        var explicitNoneResource = new RasterFontResource(
            "Primary", "test", regular, null, CancellationToken.None);

        Assert.Equal(legacyResource.ByteLength, explicitNoneResource.ByteLength);
        Assert.Equal(legacyResource.RegularData, explicitNoneResource.RegularData);

        var legacyCatalog = new RasterFontCatalog(new[] { legacyResource }, "Primary");
        var explicitNoneCatalog = new RasterFontCatalog(new[] { explicitNoneResource },
            "Primary", null, CancellationToken.None);

        Assert.Equal(legacyCatalog.DefaultFont, explicitNoneCatalog.DefaultFont);
        Assert.Equal(legacyCatalog.FallbackFonts, explicitNoneCatalog.FallbackFonts);
        Assert.Equal(legacyCatalog.Fonts.Single().ByteLength,
            explicitNoneCatalog.Fonts.Single().ByteLength);
    }

    [Fact]
    public void A_single_resource_over_the_data_limit_is_rejected_before_copying()
    {
        var regular = new byte[(int)RasterFontLimits.MaxTotalDataBytes];
        var bold = new byte[1];
        using (var scope = PrintResourceScope.Begin(RasterFontLimits.MaxTotalDataBytes + 1))
        {
            Assert.Throws<ArgumentException>(() => new RasterFontResource(
                "TooLarge", "test", regular, bold, CancellationToken.None));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Catalog_stops_an_overlong_resource_sequence_at_the_count_limit()
    {
        var resource = new RasterFontResource("Primary", "test", FixedTestFonts.Regular());
        var perResourceLimit = RasterFontLimits.MaxTotalDataBytes / RasterFontLimits.MaxResourceCount;
        Assert.True(resource.ByteLength < perResourceLimit,
            $"测试字体实际长度 {resource.ByteLength} 必须小于目录容量均摊上限 {perResourceLimit}，否则无法区分数量拒绝和容量拒绝。");
        var yielded = 0;

        var error = Assert.Throws<ArgumentException>(() => new RasterFontCatalog(
            RepeatForever(resource, () => yielded++), "Primary"));
        Assert.Equal(RasterFontLimits.MaxResourceCount + 1, yielded);
        Assert.Equal("fonts", error.ParamName);
        Assert.Contains("数量", error.Message);
        Assert.DoesNotContain("目录数据不能超过", error.Message);
    }

    [Fact]
    public void Catalog_rejects_total_data_over_the_limit_without_copying_existing_resources()
    {
        var data = PadValidFont(FixedTestFonts.Regular(),
            RasterFontLimits.MaxTotalDataBytes / 2 + 1);
        var first = new RasterFontResource("First", "test", data);
        var second = new RasterFontResource("Second", "test", data);

        using (var scope = PrintResourceScope.Begin(RasterFontLimits.MaxTotalDataBytes * 2))
        {
            Assert.Throws<ArgumentException>(() => new RasterFontCatalog(
                new[] { first, second }, "First"));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Catalog_checks_cancellation_during_resource_and_fallback_enumeration()
    {
        var resource = new RasterFontResource("Primary", "test", FixedTestFonts.Regular());
        using (var cancellation = new CancellationTokenSource())
        {
            Assert.Throws<OperationCanceledException>(() => new RasterFontCatalog(
                EnumerateAndCancel(resource, cancellation), "Primary", null, cancellation.Token));
        }

        using (var cancellation = new CancellationTokenSource())
        {
            Assert.Throws<OperationCanceledException>(() => new RasterFontCatalog(
                new[] { resource }, "Primary",
                EnumerateFallbackAndCancel(cancellation), cancellation.Token));
        }
    }

    [Fact]
    public void Catalog_rejects_a_finite_fallback_list_at_its_bound()
    {
        var resource = new RasterFontResource("Primary", "test", FixedTestFonts.Regular());
        var fallback = Enumerable.Repeat("Primary", RasterFontLimits.MaxResourceCount + 1);

        Assert.Throws<ArgumentException>(() => new RasterFontCatalog(
            new[] { resource }, "Primary", fallback));
    }

    private static IEnumerable<RasterFontResource> EnumerateOnce(
        RasterFontResource resource, Action onEnumeration)
    {
        onEnumeration();
        yield return resource;
    }

    private static IEnumerable<RasterFontResource> RepeatForever(
        RasterFontResource resource, Action onEnumeration)
    {
        while (true)
        {
            onEnumeration();
            yield return resource;
        }
    }

    private static IEnumerable<RasterFontResource> EnumerateAndCancel(
        RasterFontResource resource, CancellationTokenSource cancellation)
    {
        yield return resource;
        cancellation.Cancel();
        yield return resource;
    }

    private static IEnumerable<string> EnumerateFallbackAndCancel(
        CancellationTokenSource cancellation)
    {
        yield return "Primary";
        cancellation.Cancel();
        yield return "Primary";
    }

    private static byte[] PadValidFont(byte[] source, long targetLength)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (targetLength < source.LongLength || targetLength > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(targetLength));

        var padded = new byte[(int)targetLength];
        Buffer.BlockCopy(source, 0, padded, 0, source.Length);
        _ = new RasterFontResource("PaddedProbe", "test", padded);
        return padded;
    }

}
