using System;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证打印文档模型本身的存储估算、事务追加和临时归属释放。</summary>
public sealed class PrintDocumentStorageBudgetTests
{
    [Fact]
    public void Low_budget_rejects_repeated_empty_pages_without_changing_count()
    {
        var costs = MeasurePageAdds();
        var document = new PrintDocument();

        using (var scope = PrintResourceScope.Begin(checked(costs.First + costs.Second - 1)))
        {
            document.AddPage(80, 40);
            var error = Assert.Throws<PrintResourceLimitException>(() => document.AddPage(80, 40));

            Assert.NotNull(error);
            Assert.Single(document.Pages);
            Assert.Equal(costs.First, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Low_budget_rejects_repeated_empty_elements_without_changing_count()
    {
        var element = new PrintRectangleElement(0, 0, 10, 10);
        var costs = MeasureElementAdds(element, "Empty.Rectangle");
        var page = new PrintDocument().AddPage(80, 40);

        using (var scope = PrintResourceScope.Begin(checked(costs.First + costs.Second - 1)))
        {
            page.Add(element, "Empty.Rectangle");
            var error = Assert.Throws<PrintResourceLimitException>(() => page.Add(element, "Empty.Rectangle"));

            Assert.NotNull(error);
            Assert.Single(page.Elements);
            Assert.Equal(costs.First, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Text_is_reserved_before_rendering_and_rejected_without_a_partial_element()
    {
        var text = new PrintTextElement(0, 0, "budgeted text", 24);
        var textCost = MeasureElementAdds(text, "Text.BeforeRender").First;
        var page = new PrintDocument().AddPage(160, 50);

        using (var scope = PrintResourceScope.Begin(textCost - 1))
        {
            Assert.Throws<PrintResourceLimitException>(() => page.Add(text, "Text.BeforeRender"));
            Assert.Empty(page.Elements);
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Failed_large_element_releases_budget_and_a_small_element_can_retry()
    {
        var small = new PrintRectangleElement(0, 0, 10, 10);
        var smallCost = MeasureElementAdds(small, "Retry.Small").First;
        var large = new PrintTextElement(0, 0, new string('x', 512), 24);
        var page = new PrintDocument().AddPage(160, 50);

        using (var scope = PrintResourceScope.Begin(smallCost))
        {
            Assert.Throws<PrintResourceLimitException>(() => page.Add(large, "Retry.Large"));
            Assert.Empty(page.Elements);
            Assert.Equal(0, scope.CurrentBytes);

            page.Add(small, "Retry.Small");
            Assert.Single(page.Elements);
            Assert.Equal(smallCost, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Document_owner_releases_temporary_model_storage()
    {
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            var owner = new PrintStorageBudget();
            var document = new PrintDocument(owner);
            document.AddPage(80, 40).Add(new PrintTextElement(0, 0, "owned", 24), "Owned.Text");

            Assert.True(scope.CurrentBytes > before);
            owner.Dispose();
            Assert.Equal(before, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Reusing_an_image_does_not_duplicate_its_raster_budget()
    {
        var raster = new byte[4096];
        var image = new PrintImageElement(0, 0, 512, 64, raster);
        var page = new PrintDocument().AddPage(512, 64);

        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            page.Add(image, "Shared.Image");
            var first = scope.CurrentBytes - before;
            before = scope.CurrentBytes;
            page.Add(image, "Shared.Image");
            var second = scope.CurrentBytes - before;

            Assert.Equal(2, page.Elements.Count);
            Assert.True(first > 0);
            Assert.True(second > 0);
            Assert.True(second < raster.LongLength);
        }
    }

    [Fact]
    public void Barcode_data_is_rejected_as_retained_storage_and_a_short_value_can_retry()
    {
        AssertLargeRetainedValueIsRejectedAndSmallValueCanRetry(
            new PrintBarcodeElement(0, 0, BarcodeType.Code128, new string('B', 256), 20),
            new PrintBarcodeElement(0, 0, BarcodeType.Code128, "B", 20),
            "Barcode.Data");
    }

    [Fact]
    public void Qr_code_data_is_rejected_as_retained_storage_and_a_short_value_can_retry()
    {
        AssertLargeRetainedValueIsRejectedAndSmallValueCanRetry(
            new PrintQrCodeElement(0, 0, new string('Q', 256), 4, QrCodeCorrectionLevel.M),
            new PrintQrCodeElement(0, 0, "Q", 4, QrCodeCorrectionLevel.M),
            "QrCode.Data");
    }

    [Fact]
    public void Print_font_name_is_retained_for_budget_rejection_and_a_short_name_can_retry()
    {
        AssertLargeRetainedValueIsRejectedAndSmallValueCanRetry(
            new PrintTextElement(0, 0, "font", 24,
                new PrintFont(PrintFontKind.Device, new string('F', 256))),
            new PrintTextElement(0, 0, "font", 24,
                new PrintFont(PrintFontKind.Device, "1")),
            "Text.DeviceFont");
    }

    private static void AssertLargeRetainedValueIsRejectedAndSmallValueCanRetry(
        PrintElement large, PrintElement small, string sourcePath)
    {
        var smallCost = MeasureElementAdds(small, sourcePath).First;
        var page = new PrintDocument().AddPage(160, 50);

        using (var scope = PrintResourceScope.Begin(smallCost))
        {
            var before = scope.CurrentBytes;
            var error = Assert.Throws<PrintResourceLimitException>(() => page.Add(large, sourcePath));

            Assert.NotNull(error);
            Assert.Empty(page.Elements);
            Assert.Equal(before, scope.CurrentBytes);

            page.Add(small, sourcePath);
            Assert.Single(page.Elements);
            Assert.Equal(before + smallCost, scope.CurrentBytes);
        }
    }

    private static (long First, long Second) MeasurePageAdds()
    {
        var document = new PrintDocument();
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            document.AddPage(80, 40);
            var first = scope.CurrentBytes - before;
            before = scope.CurrentBytes;
            document.AddPage(80, 40);
            var second = scope.CurrentBytes - before;
            return (first, second);
        }
    }

    private static (long First, long Second) MeasureElementAdds(PrintElement element, string sourcePath)
    {
        var page = new PrintDocument().AddPage(80, 40);
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            page.Add(element, sourcePath);
            var first = scope.CurrentBytes - before;
            before = scope.CurrentBytes;
            page.Add(element, sourcePath);
            var second = scope.CurrentBytes - before;
            return (first, second);
        }
    }
}
