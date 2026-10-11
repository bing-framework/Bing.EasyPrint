using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证版本 2 模板在页眉、表头、重复明细和页脚同时包含图片时的长页资源边界。
/// </summary>
public sealed class TemplateImageLongPagePressureTests
{
    [Fact]
    public void Version_two_long_page_releases_body_crops_between_pages()
    {
        const int itemCount = 7;
        var resources = new TrackingResources(CreateSplitPng(128, 64));
        var result = new TemplateRenderer().RenderWithLayout(LongPageTemplate(),
            new { Items = Enumerable.Range(0, itemCount).Select(index => new { Id = index }).ToArray() },
            resources, Options(256 * 1024));

        Assert.Equal(4, result.PageCount);
        Assert.All(result.Pages, page => Assert.NotEmpty(page.Elements));
        Assert.Equal(1, resources.Count("page-header"));
        Assert.Equal(1, resources.Count("table-header"));
        Assert.Equal(1, resources.Count("page-footer"));
        Assert.Equal(itemCount, resources.Count("body"));
        Assert.Equal(itemCount, resources.Count("body-second-crop"));
        Assert.True(resources.AllStreamsDisposed);

        // 页面切分后，重复明细图片的流已被清理；所有页面的静态图片仍只解码一次。
        Assert.Equal(itemCount * 2 + 3, resources.OpenCount);
        Assert.Contains(result.Pages, page => page.Elements.Any(element =>
            element.Type == 2 && element.ResourceKey == "body" && element.InstanceIndices?.Count == 1));
    }

    [Fact]
    public void Different_body_crops_do_not_share_the_decoded_image_on_long_pages()
    {
        var data = new { Items = Enumerable.Range(0, 7).Select(index => new { Id = index }).ToArray() };
        var mixed = new TemplateRenderer().Render(LongPageTemplate(), data,
            new TrackingResources(CreateSplitPng(128, 64)), Options(256 * 1024));
        var same = new TemplateRenderer().Render(LongPageTemplate(useSecondCrop: false), data,
            new TrackingResources(CreateSplitPng(128, 64)), Options(256 * 1024));

        Assert.Equal(mixed.PageCount, same.PageCount);
        Assert.False(mixed.Pages.SelectMany(page => page.Data)
            .SequenceEqual(same.Pages.SelectMany(page => page.Data)));
    }

    [Fact]
    public void Long_page_budget_rejection_does_not_poison_a_later_render()
    {
        var data = new { Items = Enumerable.Range(0, 7).Select(index => new { Id = index }).ToArray() };
        var rejectedResources = new TrackingResources(CreateSplitPng(128, 64));
        var rejected = Assert.Throws<PrintResourceLimitException>(() => new TemplateRenderer().Render(
            LongPageTemplate(), data, rejectedResources, Options(1024)));

        Assert.Contains("Template.", rejected.ResourceName, StringComparison.Ordinal);
        Assert.True(rejectedResources.AllStreamsDisposed || rejectedResources.OpenCount == 0);

        var recoveredResources = new TrackingResources(CreateSplitPng(128, 64));
        var recovered = new TemplateRenderer().Render(LongPageTemplate(), data, recoveredResources,
            Options(256 * 1024));
        Assert.Equal(4, recovered.PageCount);
        Assert.True(recoveredResources.AllStreamsDisposed);
    }

    private static TemplateRenderOptions Options(long maxWorkingBytes) => new TemplateRenderOptions
    {
        Encoding = Encoding.UTF8,
        MaxWorkingBytes = maxWorkingBytes,
        FeedLines = 0,
        CutAfterPrint = false
    };

    private static BpTemplate LongPageTemplate(bool useSecondCrop = true)
    {
        return new BpTemplate
        {
            Version = 2,
            Width = 96,
            Height = 100,
            Rows = new List<BpRow>
            {
                ImageRow("PageHeader", "page-header", 14, 12),
                TableHeaderRow(),
                BodyRow(useSecondCrop),
                ImageRow("PageFooter", "page-footer", 14, 12)
            }
        };
    }

    private static BpRow TableHeaderRow() => new BpRow
    {
        Section = "TableHeader",
        Height = 18,
        Cells = new List<BpCell>
        {
            new BpCell
            {
                Elements = new List<BpElement>
                {
                    new BpImageElement
                    {
                        Width = 14,
                        Height = 14,
                        Field = new BgField { Value = "table-header" }
                    },
                    new BpFontElement
                    {
                        FontSize = 16,
                        StartCoordinate = new BpCoordinate { X = 20, Y = 0 },
                        Field = new BgField { Value = "ITEMS" }
                    }
                }
            }
        }
    };

    private static BpRow BodyRow(bool useSecondCrop)
    {
        var first = new BpImageElement
        {
            Width = 16,
            Height = 16,
            Field = new BgField { Value = "body" },
            Crop = new BpImageCrop { X = 0, Y = 0, Width = 64, Height = 64 }
        };
        var second = new BpImageElement
        {
            Width = 16,
            Height = 16,
            StartCoordinate = new BpCoordinate { X = 24, Y = 0 },
            Field = new BgField { Value = useSecondCrop ? "body-second-crop" : "body" },
            Crop = new BpImageCrop
            {
                X = useSecondCrop ? 64 : 0,
                Y = 0,
                Width = 64,
                Height = 64
            }
        };
        return new BpRow
        {
            Section = "Body",
            RepeatSource = "Items",
            Height = 20,
            Cells = new List<BpCell>
            {
                new BpCell { Elements = new List<BpElement> { first, second } }
            }
        };
    }

    private static BpRow ImageRow(string section, string key, int rowHeight, int imageSize) => new BpRow
    {
        Section = section,
        Height = rowHeight,
        Cells = new List<BpCell>
        {
            new BpCell
            {
                Elements = new List<BpElement>
                {
                    new BpImageElement
                    {
                        Width = imageSize,
                        Height = imageSize,
                        Field = new BgField { Value = key }
                    }
                }
            }
        }
    };

    private static byte[] CreateSplitPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image[x, y] = x < width / 2
                    ? new Rgba32(0, 0, 0, 255)
                    : new Rgba32(255, 255, 255, 255);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed class TrackingResources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;
        private readonly List<TrackingStream> streams = new List<TrackingStream>();
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        internal TrackingResources(byte[] bytes) => this.bytes = bytes;

        internal int OpenCount => streams.Count;
        internal bool AllStreamsDisposed => streams.Count > 0 && streams.All(stream => stream.IsDisposed);

        internal int Count(string key) => counts.TryGetValue(key, out var count) ? count : 0;

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            counts[resourceKey] = Count(resourceKey) + 1;
            var stream = new TrackingStream(bytes);
            streams.Add(stream);
            return stream;
        }
    }

    private sealed class TrackingStream : MemoryStream
    {
        internal bool IsDisposed { get; private set; }

        internal TrackingStream(byte[] bytes) : base(bytes, false) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing) IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
