using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TemplateImageWorkingBudgetTests
{
    [Theory]
    [InlineData(8, 8)]
    [InlineData(13, 9)]
    public void Small_png_decode_and_resize_preserve_legacy_output(int width, int height)
    {
        var encoded = CreatePng(8, 8);
        var template = ImageTemplate(width, height);
        var renderer = new TemplateRenderer();

        var legacy = renderer.Render(template, null, new TrackingResources(encoded), Options());
        var bounded = renderer.Render(template, null, new TrackingResources(encoded),
            OptionsWithBudget(1024 * 1024));

        Assert.Equal(legacy.Pages.SelectMany(page => page.Data),
            bounded.Pages.SelectMany(page => page.Data));
    }

    [Fact]
    public void Budget_rejects_before_decode_and_a_later_render_recovers()
    {
        var encoded = CreatePng(128, 128);
        var template = ImageTemplate(128, 128);
        var renderer = new TemplateRenderer();
        var resources = new TrackingResources(encoded);
        var options = Options();
        options.MaxWorkingBytes = 65535;

        var error = Assert.Throws<PrintResourceLimitException>(() => renderer.Render(
            template, null, resources, options));
        Assert.Equal("Template.ImageDecodeRgba", error.ResourceName);
        Assert.Equal(1, resources.OpenCount);
        Assert.True(resources.LastStreamDisposed);

        var result = renderer.Render(template, null, new TrackingResources(encoded),
            OptionsWithBudget(1024 * 1024));
        Assert.NotEmpty(result.Pages);
    }

    [Fact]
    public void Resize_reserves_destination_while_the_source_image_is_still_live()
    {
        var encoded = CreatePng(8, 8);
        var options = OptionsWithBudget(65535);
        var resources = new TrackingResources(encoded);
        var error = Assert.Throws<PrintResourceLimitException>(() => new TemplateRenderer().Render(
            ImageTemplate(128, 128), null, resources, options));
        Assert.Equal("Template.ImageResizeRgba", error.ResourceName);
        Assert.Equal(65536, error.RequestedBytes);
        Assert.True(error.CurrentBytes >= 256);
        Assert.True(resources.LastStreamDisposed);
    }

    [Fact]
    public void Nested_template_budget_keeps_results_in_parent_and_rolls_back_failed_render()
    {
        var encoded = CreatePng(8, 8);
        var template = ImageTemplate(8, 8);
        var renderer = new TemplateRenderer();
        using var parent = PrintResourceScope.Begin(1024 * 1024);
        var result = renderer.Render(template, null, new TrackingResources(encoded),
            OptionsWithBudget(1024 * 1024));
        var retained = parent.CurrentBytes;
        Assert.True(retained >= result.Pages.Sum(page => page.Data.Length));
        Assert.True(retained > 0);

        Assert.Throws<PrintResourceLimitException>(() => renderer.Render(template, null,
            new TrackingResources(encoded), OptionsWithBudget(100)));
        Assert.Equal(retained, parent.CurrentBytes);
        Assert.NotEmpty(renderer.Render(template, null, new TrackingResources(encoded),
            OptionsWithBudget(1024 * 1024)).Pages);
        Assert.True(parent.CurrentBytes > retained);
    }

    [Fact]
    public void Header_image_is_reused_across_pages_and_its_resource_stream_is_released()
    {
        var encoded = CreatePng(16, 16);
        var template = HeaderImageWithBodyRows(4, 16);
        var resources = new TrackingResources(encoded);

        var result = new TemplateRenderer().Render(template, null, resources,
            OptionsWithBudget(256 * 1024));

        Assert.True(result.PageCount > 1);
        Assert.Equal(1, resources.OpenCount);
        Assert.True(resources.AllStreamsDisposed);
    }

    [Fact]
    public void Body_images_are_released_between_pages_under_a_single_image_budget()
    {
        var encoded = CreatePng(128, 128);
        var template = RepeatedImageTemplate(4, 128);
        var resources = new TrackingResources(encoded);

        var result = new TemplateRenderer().Render(template, null, resources,
            OptionsWithBudget(300 * 1024));

        Assert.Equal(4, result.PageCount);
        Assert.Equal(4, resources.OpenCount);
        Assert.True(resources.AllStreamsDisposed);
    }

    [Fact]
    public void Cancellation_during_image_open_releases_the_stream_and_later_render_recovers()
    {
        var encoded = CreatePng(16, 16);
        var template = ImageTemplate(16, 16);
        using var cancellation = new CancellationTokenSource();
        var cancelledResources = new TrackingResources(encoded,
            _ => cancellation.Cancel());

        Assert.Throws<OperationCanceledException>(() => new TemplateRenderer().Render(
            template, null, cancelledResources, Options(), cancellation.Token));
        Assert.Equal(1, cancelledResources.OpenCount);
        Assert.True(cancelledResources.AllStreamsDisposed);

        var recovered = new TrackingResources(encoded);
        var result = new TemplateRenderer().Render(template, null, recovered,
            OptionsWithBudget(256 * 1024));
        Assert.NotEmpty(result.Pages);
        Assert.True(recovered.AllStreamsDisposed);
    }

    private static TemplateRenderOptions Options() => new TemplateRenderOptions
    {
        Encoding = Encoding.UTF8
    };

    private static TemplateRenderOptions OptionsWithBudget(long maxWorkingBytes)
    {
        var options = Options();
        options.MaxWorkingBytes = maxWorkingBytes;
        return options;
    }

    private static BpTemplate ImageTemplate(int width, int height) => new BpTemplate
    {
        Width = Math.Max(128, width),
        Height = Math.Max(128, height),
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Elements = new List<BpElement>
                        {
                            new BpImageElement
                            {
                                Width = width,
                                Height = height,
                                Field = new BgField { Value = "logo" }
                            }
                        }
                    }
                }
            }
        }
    };

    private static BpTemplate RepeatedImageTemplate(int rowCount, int imageSize)
    {
        var rows = new List<BpRow>();
        for (var index = 0; index < rowCount; index++)
            rows.Add(ImageRow("Body", imageSize));
        return new BpTemplate { Version = 2, Width = imageSize, Height = imageSize, Rows = rows };
    }

    private static BpTemplate HeaderImageWithBodyRows(int rowCount, int imageSize)
    {
        var rows = new List<BpRow> { ImageRow("PageHeader", imageSize) };
        for (var index = 0; index < rowCount; index++)
        {
            var row = new BpRow
            {
                Height = imageSize * 3,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Elements = new List<BpElement>
                        {
                            new BpFontElement
                            {
                                FontSize = 24,
                                Field = new BgField { Value = "body" }
                            }
                        }
                    }
                }
            };
            rows.Add(row);
        }
        return new BpTemplate { Version = 2, Width = imageSize, Height = imageSize * 4, Rows = rows };
    }

    private static BpRow ImageRow(string section, int imageSize) => new BpRow
    {
        Section = section,
        Height = imageSize,
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
                        Field = new BgField { Value = "logo" }
                    }
                }
            }
        }
    };

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image[x, y] = (x + y) % 2 == 0
                    ? new Rgba32(0, 0, 0, 255)
                    : new Rgba32(255, 255, 255, 255);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed class TrackingResources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;
        private readonly Action<CancellationToken> onOpen;
        private readonly List<TrackingStream> streams = new List<TrackingStream>();

        internal TrackingResources(byte[] bytes, Action<CancellationToken> onOpen = null)
        {
            this.bytes = bytes;
            this.onOpen = onOpen;
        }

        internal int OpenCount { get; private set; }
        internal bool LastStreamDisposed => streams.Count > 0 && streams[streams.Count - 1].IsDisposed;
        internal bool AllStreamsDisposed => streams.Count > 0 && streams.All(stream => stream.IsDisposed);

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            OpenCount++;
            onOpen?.Invoke(cancellationToken);
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
