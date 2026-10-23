using System;
using System.Threading;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 Host 同源预览返回的 raster 快照在编码期间保留预算租约。</summary>
public sealed class PrintRasterPreviewTests
{
    [Fact]
    public void Returned_raster_holds_its_budget_until_disposed()
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        var result = PrintRasterPreview.RenderElements(Array.Empty<PrintElement>(), 100, 100, null, 12,
            CancellationToken.None);
        Assert.True(scope.CurrentBytes >= 1250, $"expected leased raster bytes, actual={scope.CurrentBytes}");
        result.Dispose();
        Assert.Equal(0, scope.CurrentBytes);
    }

    [Fact]
    public void Cancelled_preview_does_not_leave_a_lease()
    {
        using var scope = PrintResourceScope.Begin(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            PrintRasterPreview.RenderElements(Array.Empty<PrintElement>(), 100, 100, null, 12,
                cancellation.Token));
        Assert.Equal(0, scope.CurrentBytes);
    }
}
