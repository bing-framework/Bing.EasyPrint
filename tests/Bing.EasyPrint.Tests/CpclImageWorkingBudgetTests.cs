using System;
using System.IO;
using System.Text;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Printing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 CPCL 旧图片入口在解码和非定位流缓冲阶段的资源预算语义。</summary>
public sealed class CpclImageWorkingBudgetTests
{
    [Fact]
    public void Seekable_image_rejects_decode_budget_restores_position_and_can_retry()
    {
        var image = CreatePng(8, 8);
        var encoded = new byte[image.Length + 2];
        Buffer.BlockCopy(image, 0, encoded, 2, image.Length);
        using var stream = new MemoryStream(encoded);
        stream.Position = 2;
        var command = new CPCLPrintCommand(Encoding.ASCII);

        using (var scope = PrintResourceScope.Begin(255))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() =>
                command.DrawImage(0, 0, stream));

            Assert.Equal("Cpcl.ImageDecodeRgba", error.ResourceName);
            Assert.Equal(0, scope.CurrentBytes);
            Assert.Equal(2, stream.Position);
            Assert.True(stream.CanRead);
        }

        var output = new CPCLPrintCommand(Encoding.ASCII)
            .DrawImage(0, 0, stream)
            .Build()
            .GetBytes();

        Assert.Contains("EG 1 8 0 0", Encoding.ASCII.GetString(output));
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void Non_seekable_image_keeps_callers_stream_open()
    {
        var encoded = CreatePng(2, 2);
        var reference = new CPCLPrintCommand(Encoding.ASCII);
        long expectedRetained;
        using (var calibration = PrintResourceScope.Begin(long.MaxValue))
        using (var seekable = new MemoryStream(encoded))
        {
            reference.DrawImage(0, 0, seekable);
            expectedRetained = calibration.CurrentBytes;
        }
        using var stream = new TrackingNonSeekStream(encoded);
        var command = new CPCLPrintCommand(Encoding.ASCII);

        using (var scope = PrintResourceScope.Begin(200000))
        {
            command.DrawImage(0, 0, stream);
            // 非定位流只多用临时读取缓冲，结束后保留成本应与定位流完全相同。
            Assert.Equal(expectedRetained, scope.CurrentBytes);
            Assert.True(scope.PeakBytes > scope.CurrentBytes);
        }

        Assert.False(stream.IsDisposed);
        Assert.True(stream.ReadCount > 0);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Non_seekable_buffer_expansion_rejects_before_new_capacity_and_releases_all_leases()
    {
        using var stream = new TrackingNonSeekStream(new byte[81921]);
        var command = new CPCLPrintCommand(Encoding.ASCII);
        using var scope = PrintResourceScope.Begin(200000);

        var error = Assert.Throws<PrintResourceLimitException>(() =>
            command.DrawImage(0, 0, stream));

        Assert.Equal("Cpcl.ImageReadStream", error.ResourceName);
        Assert.Equal(163840, error.RequestedBytes);
        Assert.Equal(0, scope.CurrentBytes);
        Assert.False(stream.IsDisposed);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0, 255));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed class TrackingNonSeekStream : Stream
    {
        private readonly MemoryStream inner;

        internal TrackingNonSeekStream(byte[] bytes)
        {
            inner = new MemoryStream(bytes, writable: false);
        }

        internal bool IsDisposed { get; private set; }
        internal int ReadCount { get; private set; }

        public override bool CanRead => !IsDisposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;
            return inner.Read(buffer, offset, count);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                IsDisposed = true;
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
