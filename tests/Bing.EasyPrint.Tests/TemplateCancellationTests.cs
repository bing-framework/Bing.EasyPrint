using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class TemplateCancellationTests
{
    [Fact]
    public void Cancellation_during_repeat_enumeration_stops_render()
    {
        using var cancellation = new CancellationTokenSource();
        var template = Template(new BpRow
        {
            RepeatSource = "Items",
            Cells = new List<BpCell>
            {
                new BpCell { Elements = new List<BpElement> { Text("$item") } }
            }
        });
        var renderer = new TemplateRenderer();

        Assert.Throws<OperationCanceledException>(() => renderer.Render(
            template,
            new { Items = CancelAfterFirst(cancellation) },
            options: Options(),
            cancellationToken: cancellation.Token));
        Assert.True(cancellation.IsCancellationRequested);

        var result = renderer.Render(template, new { Items = new[] { "first-order", "second-order" } }, options: Options());
        var output = Encoding.UTF8.GetString(result.Pages[0].Data);
        Assert.Contains("first-order", output);
        Assert.Contains("second-order", output);
    }

    [Fact]
    public void Cancellation_during_image_stream_read_disposes_resource_stream()
    {
        using var image = new Image<Rgba32>(8, 8, new Rgba32(0, 0, 0));
        using var encoded = new MemoryStream();
        image.SaveAsPng(encoded);
        using var cancellation = new CancellationTokenSource();
        var resources = new CancelOnReadResources(encoded.ToArray(), cancellation);
        var template = Template(new BpRow
        {
            Cells = new List<BpCell>
            {
                new BpCell
                {
                    Elements = new List<BpElement>
                    {
                        new BpImageElement { Field = new BgField { Value = "logo" } }
                    }
                }
            }
        });
        var renderer = new TemplateRenderer();

        Assert.Throws<OperationCanceledException>(() => renderer.Render(
            template,
            null,
            resources,
            Options(),
            cancellation.Token));
        Assert.True(resources.ReadStarted);
        Assert.True(resources.LastStreamDisposed);

        var retryResources = new TrackingResources(encoded.ToArray());
        var result = renderer.Render(template, null, retryResources, Options());
        Assert.Contains("EG 1 8 0 0", Encoding.UTF8.GetString(result.Pages[0].Data));
        Assert.True(retryResources.LastStreamDisposed);
    }

    [Fact]
    public void Renderer_can_render_again_after_a_failed_binding()
    {
        var template = Template(new BpRow
        {
            Cells = new List<BpCell>
            {
                new BpCell { Elements = new List<BpElement> { Text("Name") } }
            }
        });
        var renderer = new TemplateRenderer();

        Assert.Throws<ArgumentException>(() => renderer.Render(template, new { Other = "first" }, options: Options()));

        var result = renderer.Render(
            template,
            new Dictionary<string, object> { ["Name"] = "recovered" },
            options: Options());

        Assert.Contains("recovered", Encoding.UTF8.GetString(result.Pages[0].Data));
    }

    private static TemplateRenderOptions Options() => new TemplateRenderOptions { Encoding = Encoding.UTF8 };

    private static BpFontElement Text(string name) => new BpFontElement
    {
        FontSize = 24,
        Field = new BgField { Name = name }
    };

    private static BpTemplate Template(BpRow row) => new BpTemplate
    {
        Width = 576,
        Height = 200,
        Rows = new List<BpRow> { row }
    };

    private static IEnumerable<string> CancelAfterFirst(CancellationTokenSource cancellation)
    {
        yield return "first";
        cancellation.Cancel();
        yield return "second";
    }

    private sealed class CancelOnReadResources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;
        private readonly CancellationTokenSource cancellation;

        internal bool ReadStarted { get; private set; }
        internal bool LastStreamDisposed => stream?.IsDisposed == true;
        private CancelingReadStream stream;

        internal CancelOnReadResources(byte[] bytes, CancellationTokenSource cancellation)
        {
            this.bytes = bytes;
            this.cancellation = cancellation;
        }

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            Assert.Equal(cancellation.Token, cancellationToken);
            stream = new CancelingReadStream(bytes, cancellation, () => ReadStarted = true);
            return stream;
        }
    }

    private sealed class TrackingResources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;
        private TrackingMemoryStream stream;

        internal bool LastStreamDisposed => stream?.IsDisposed == true;

        internal TrackingResources(byte[] bytes) => this.bytes = bytes;

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken) =>
            stream = new TrackingMemoryStream(bytes);
    }

    private sealed class TrackingMemoryStream : MemoryStream
    {
        internal bool IsDisposed { get; private set; }

        internal TrackingMemoryStream(byte[] bytes) : base(bytes, false) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing) IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class CancelingReadStream : Stream
    {
        private readonly MemoryStream inner;
        private readonly CancellationTokenSource cancellation;
        private readonly Action onRead;
        private bool disposed;

        internal bool IsDisposed => disposed;
        public override bool CanRead => !disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        internal CancelingReadStream(byte[] bytes, CancellationTokenSource cancellation, Action onRead)
        {
            inner = new MemoryStream(bytes, false);
            this.cancellation = cancellation;
            this.onRead = onRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, Math.Min(count, 32));
            if (read > 0)
            {
                onRead();
                cancellation.Cancel();
            }
            return read;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
