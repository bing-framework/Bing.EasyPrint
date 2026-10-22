using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class TiffStructureValidatorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Valid_tiff_endianness_and_size_decode_through_both_public_paths(bool big, bool little)
    {
        var bytes = ValidTiff(big, little);
        using var stream = new MemoryStream(bytes);
        TiffStructureValidator.Validate(stream);
        Assert.Equal(0, stream.Position);
        Assert.NotEmpty(new CPCLPrintCommand(Encoding.ASCII).DrawImage(0, 0, stream).Build().GetBytes());
        Assert.NotEmpty(new TemplateRenderer().Render(Template(), null, new Resources(bytes), Options()).Pages);
        using var decoded = Image.Load<Rgba32>(bytes);
        Assert.Equal(1, decoded.Width);
        Assert.Equal(1, decoded.Height);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Huge_bigtiff_count_rejects_before_decoder_and_restores_callers_stream(bool little)
    {
        var bytes = Header(true, little, 24);
        Write(bytes, 16, 5000000000UL, 8, little);
        using var stream = new MemoryStream();
        stream.Write(new byte[] { 9, 8, 7 }, 0, 3);
        stream.Write(bytes, 0, bytes.Length);
        stream.Position = 3;
        var command = new CPCLPrintCommand(Encoding.ASCII);
        var original = command.Build().GetBytes();
        using var budget = PrintResourceScope.Begin(8 * 1024 * 1024);
        Assert.Throws<InvalidImageContentException>(() => command.DrawImage(0, 0, stream));
        Assert.Equal(3, stream.Position);
        Assert.True(stream.CanRead);
        Assert.Equal(0, budget.CurrentBytes);
        Assert.Equal(original, command.Build().GetBytes());
        Assert.NotEmpty(command.DrawImage(0, 0, ValidTiff(false, true)).Build().GetBytes());
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Invalid_tiff_template_keeps_resource_diagnostics_and_releases_budget(PrintProtocol protocol)
    {
        var bytes = Header(true, true, 24);
        Write(bytes, 16, ulong.MaxValue, 8, true);
        var resources = new Resources(bytes);
        var options = Options();
        options.Protocol = protocol;
        using var budget = PrintResourceScope.Begin(8 * 1024 * 1024);
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(Template(), null, resources, options));
        Assert.IsType<InvalidImageContentException>(error.InnerException);
        Assert.Equal("image", error.ResourceName);
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.False(resources.LastStream.CanRead);
        Assert.Equal(0, budget.CurrentBytes);
        Assert.NotEmpty(new TemplateRenderer().Render(Template(), null, new Resources(ValidTiff(false, true)), options).Pages);
    }

    [Theory]
    [MemberData(nameof(MalformedTiffs))]
    public void Malformed_headers_counts_and_directory_chains_are_rejected(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        Assert.Throws<InvalidImageContentException>(() => TiffStructureValidator.Validate(stream));
        Assert.Equal(0, stream.Position);
    }

    public static IEnumerable<object[]> MalformedTiffs()
    {
        foreach (var big in new[] { false, true })
        foreach (var little in new[] { false, true })
        {
            var header = big ? 16 : 8;
            var size = big ? 8 : 4;
            var countSize = big ? 8 : 2;
            var bytes = Header(big, little, header + countSize + size);
            Write(bytes, header, big ? ulong.MaxValue : ushort.MaxValue, countSize, little);
            yield return new object[] { bytes };
            bytes = Header(big, little, header + countSize + size);
            Write(bytes, header + countSize, (ulong)header, size, little); // 自循环。
            yield return new object[] { bytes };
            bytes = Header(big, little, header + countSize + size);
            Write(bytes, big ? 8 : 4, ulong.MaxValue, size, little);
            yield return new object[] { bytes };
            bytes = Header(big, little, header + 2 * (countSize + size));
            Write(bytes, header + countSize, (ulong)(header + countSize + size), size, little);
            Write(bytes, header + 2 * countSize + size, (ulong)header, size, little); // 双目录循环。
            yield return new object[] { bytes };
            bytes = Header(big, little, header);
            Array.Resize(ref bytes, header - 1);
            yield return new object[] { bytes };
            bytes = Header(big, little, header + countSize); // 缺少 next IFD 指针。
            yield return new object[] { bytes };
        }
        var badOffsetSize = Header(true, true, 32);
        badOffsetSize[4] = 4;
        yield return new object[] { badOffsetSize };
    }

    [Fact]
    public void Cancellation_during_directory_read_restores_stream_and_propagates()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancelOnReadStream(ValidTiff(true, true), cancellation);
        var error = Assert.Throws<OperationCanceledException>(() =>
            TiffStructureValidator.Validate(stream, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Non_tiff_and_multiframe_tiff_preserve_existing_output_and_first_frame_behavior()
    {
        using var image = new Image<Rgba32>(2, 2, new Rgba32(0, 0, 0, 255));
        using var white = new Image<Rgba32>(2, 2, new Rgba32(255, 255, 255, 255));
        foreach (var format in new[] { "png", "jpeg", "gif", "tiff" })
        {
            using var encoded = new MemoryStream();
            if (format == "png") image.SaveAsPng(encoded);
            else if (format == "jpeg") image.SaveAsJpeg(encoded);
            else if (format == "gif") image.SaveAsGif(encoded);
            else image.SaveAsTiff(encoded);
            encoded.Position = 0;
            TiffStructureValidator.Validate(encoded);
            Assert.Equal(0, encoded.Position);
            Assert.NotEmpty(new CPCLPrintCommand(Encoding.ASCII).DrawImage(0, 0, encoded).Build().GetBytes());
            Assert.NotEmpty(new TemplateRenderer().Render(Template(), null, new Resources(encoded.ToArray()), Options()).Pages);
        }
        using var single = new MemoryStream();
        image.SaveAsTiff(single);
        image.Frames.AddFrame(white.Frames.RootFrame);
        using var multi = new MemoryStream();
        image.SaveAsTiff(multi);
        byte[] Render(byte[] bytes) => new CPCLPrintCommand(Encoding.ASCII).DrawImage(0, 0, bytes).Build().GetBytes();
        Assert.Equal(Render(single.ToArray()), Render(multi.ToArray()));
    }

    private static byte[] Header(bool big, bool little, int length)
    {
        var bytes = new byte[length];
        bytes[0] = bytes[1] = (byte)(little ? 0x49 : 0x4d);
        Write(bytes, 2, big ? 43UL : 42UL, 2, little);
        if (big) Write(bytes, 4, 8, 2, little);
        Write(bytes, big ? 8 : 4, big ? 16UL : 8UL, big ? 8 : 4, little);
        return bytes;
    }

    private static byte[] ValidTiff(bool big, bool little)
    {
        var header = big ? 16 : 8;
        var countSize = big ? 8 : 2;
        var entrySize = big ? 20 : 12;
        var dataOffset = header + countSize + 9 * entrySize + (big ? 8 : 4);
        var bytes = Header(big, little, dataOffset + 1);
        Write(bytes, header, 9, countSize, little);
        var tags = new[] { 256, 257, 258, 259, 262, 273, 277, 278, 279 };
        for (var index = 0; index < tags.Length; index++)
        {
            var offset = header + countSize + index * entrySize;
            var shortValue = tags[index] == 258 || tags[index] == 259 || tags[index] == 262 || tags[index] == 277;
            Write(bytes, offset, (ulong)tags[index], 2, little);
            Write(bytes, offset + 2, shortValue ? 3UL : 4UL, 2, little);
            Write(bytes, offset + 4, 1, big ? 8 : 4, little);
            Write(bytes, offset + (big ? 12 : 8), tags[index] == 273 ? (ulong)dataOffset : tags[index] == 258 ? 8UL : 1UL,
                shortValue ? 2 : 4, little);
        }
        return bytes;
    }

    private static void Write(byte[] bytes, int offset, ulong value, int size, bool little)
    {
        for (var index = 0; index < size; index++)
            bytes[offset + index] = (byte)(value >> (8 * (little ? index : size - index - 1)));
    }

    private static BpTemplate Template() => new BpTemplate
    {
        Width = 80, Height = 40, Rows = new List<BpRow> { new BpRow
        {
            Cells = new List<BpCell> { new BpCell
            {
                Elements = new List<BpElement> { new BpImageElement
                    { Width = 8, Height = 8, Field = new BgField { Value = "image" } } }
            } }
        } }
    };

    private static TemplateRenderOptions Options() => new TemplateRenderOptions
        { Encoding = Encoding.ASCII, EscPosLayout = EscPosLayoutMode.Raster, MaxWorkingBytes = 8 * 1024 * 1024 };

    private sealed class Resources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;
        internal MemoryStream LastStream;
        internal Resources(byte[] bytes) => this.bytes = bytes;
        public Stream OpenRead(string name, CancellationToken cancellationToken)
            => LastStream = new MemoryStream(bytes, false);
    }

    private sealed class CancelOnReadStream : MemoryStream
    {
        private readonly CancellationTokenSource cancellation;
        internal CancelOnReadStream(byte[] bytes, CancellationTokenSource cancellation) : base(bytes)
            => this.cancellation = cancellation;
        public override int ReadByte()
        {
            var value = base.ReadByte();
            if (Position == 18) cancellation.Cancel();
            return value;
        }
    }
}
