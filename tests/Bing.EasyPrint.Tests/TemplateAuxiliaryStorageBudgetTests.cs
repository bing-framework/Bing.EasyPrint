using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TemplateAuxiliaryStorageBudgetTests
{
    [Theory]
    [InlineData(EscPosLayoutMode.Native)]
    [InlineData(EscPosLayoutMode.Raster)]
    public void Escpos_modes_release_failed_template_models_and_keep_successful_result_storage(EscPosLayoutMode mode)
    {
        var renderer = new TemplateRenderer();
        var template = ParametersTemplate(1);
        var options = new TemplateRenderOptions
        { Protocol = PrintProtocol.EscPos, EscPosLayout = mode, Encoding = Encoding.ASCII, CutAfterPrint = false };
        long peak;
        using (var measured = PrintResourceScope.Begin(long.MaxValue))
        {
            renderer.RenderWithLayout(template, null, options: options);
            peak = measured.PeakBytes;
        }
        using var parent = PrintResourceScope.Begin(long.MaxValue);
        options.MaxWorkingBytes = peak - 1;
        Assert.Throws<PrintResourceLimitException>(() => renderer.RenderWithLayout(template, null, options: options));
        Assert.Equal(0, parent.CurrentBytes);
        options.MaxWorkingBytes = null;
        var result = renderer.RenderWithLayout(template, null, options: options);
        Assert.NotEmpty(result.Pages);
        Assert.True(parent.CurrentBytes > 0);
        Assert.True(parent.CurrentBytes < parent.PeakBytes);
    }

    [Fact]
    public void Unused_component_parameters_still_consume_storage_and_failed_render_recovers()
    {
        var small = ParametersTemplate(1);
        var large = ParametersTemplate(128);
        var smallPeak = Peak(small, null);
        var largePeak = Peak(large, null);
        Assert.True(largePeak - smallPeak >= 127 * 256);
        using var parent = PrintResourceScope.Begin(long.MaxValue);
        Assert.Throws<PrintResourceLimitException>(() => new TemplateRenderer().Render(large, null,
            options: Options(smallPeak + 512)));
        Assert.Equal(0, parent.CurrentBytes);
        Assert.NotEmpty(new TemplateRenderer().Render(small, null, options: Options()).Pages);
    }

    [Fact]
    public void Unique_resource_index_failure_closes_all_streams_and_allows_a_small_render()
    {
        using var image = new Image<Rgba32>(1, 1);
        using var encoded = new MemoryStream();
        image.SaveAsPng(encoded);
        var resources = new Resources(encoded.ToArray());
        var large = ImagesTemplate(48);
        var peak = Peak(large, resources);
        resources.Streams.Clear();
        using var parent = PrintResourceScope.Begin(long.MaxValue);
        var error = Assert.Throws<PrintResourceLimitException>(() => new TemplateRenderer().Render(large, null,
            resources, Options(peak / 2)));
        Assert.False(string.IsNullOrWhiteSpace(error.ResourceName));
        Assert.NotEmpty(resources.Streams);
        Assert.All(resources.Streams, stream => Assert.True(stream.Disposed));
        Assert.Equal(0, parent.CurrentBytes);
        Assert.NotEmpty(new TemplateRenderer().Render(ImagesTemplate(1), null, resources, Options()).Pages);
        Assert.All(resources.Streams, stream => Assert.True(stream.Disposed));
    }

    private static long Peak(BpTemplate template, ITemplateResourceProvider resources)
    {
        using var scope = PrintResourceScope.Begin(long.MaxValue);
        new TemplateRenderer().Render(template, null, resources, Options());
        return scope.PeakBytes;
    }

    private static TemplateRenderOptions Options(long? limit = null) => new TemplateRenderOptions
    { Encoding = Encoding.ASCII, MaxWorkingBytes = limit };

    private static BpTemplate ParametersTemplate(int count) => Template(new BpGroupElement
    {
        Parameters = Enumerable.Range(0, count).ToDictionary(index => "p" + index,
            index => new BpComponentParameterBinding { Value = JValue.CreateString("unused") }),
        Elements = new List<BpElement> { new BpFontElement { FontSize = 24, Field = new BgField { Value = "OK" } } }
    });

    private static BpTemplate ImagesTemplate(int count) => Template(Enumerable.Range(0, count)
        .Select(index => (BpElement)new BpImageElement
        { Width = 1, Height = 1, Field = new BgField { Value = "image" + index } }).ToArray());

    private static BpTemplate Template(params BpElement[] elements) => new BpTemplate
    {
        Version = 3, Width = 240, Height = 200,
        Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell>
        { new BpCell { Elements = elements.ToList() } } } }
    };

    private sealed class Resources : ITemplateResourceProvider
    {
        private readonly byte[] data;
        internal readonly List<ResourceStream> Streams = new List<ResourceStream>();
        internal Resources(byte[] data) => this.data = data;
        public Stream OpenRead(string key, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var stream = new ResourceStream(data);
            Streams.Add(stream);
            return stream;
        }
    }

    private sealed class ResourceStream : MemoryStream
    {
        internal bool Disposed;
        internal ResourceStream(byte[] data) : base(data, false) { }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
