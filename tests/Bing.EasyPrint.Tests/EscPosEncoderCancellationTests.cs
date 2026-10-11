using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosEncoderCancellationTests
{
    [Fact]
    public void Canceled_at_entry_does_not_write()
    {
        var writer = new RecordingWriter();
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => EscPosImageEncoder.Write(writer, 8, 2,
            new byte[] { 0x80, 0x40 }, new EscPosPrinterProfile(), false, false, source.Token));
        Assert.Empty(writer.Writes);
    }

    [Fact]
    public void Canceled_after_first_raster_block_command_stops_following_writes()
    {
        using var source = new CancellationTokenSource();
        var writer = new RecordingWriter(value =>
        {
            if (value.SequenceEqual(new byte[] { 0x1D, 0x76, 0x30, 0, 1, 0, 1, 0 }))
                source.Cancel();
        });
        var profile = new EscPosPrinterProfile { ImageCommand = EscPosImageCommand.RasterGsV0, MaxImageBlockBytes = 1 };

        Assert.Throws<OperationCanceledException>(() => EscPosImageEncoder.Write(writer, 8, 2,
            new byte[] { 0x80, 0x40 }, profile, false, false, source.Token));
        Assert.Single(writer.Writes);
        Assert.Equal(new byte[] { 0x1D, 0x76, 0x30, 0, 1, 0, 1, 0 }, writer.Writes[0]);
    }

    [Fact]
    public void Canceled_after_first_column_command_stops_following_writes()
    {
        using var source = new CancellationTokenSource();
        var writer = new RecordingWriter(value =>
        {
            if (value.Length == 5 && value[0] == 0x1B && value[1] == 0x2A)
                source.Cancel();
        });
        var profile = new EscPosPrinterProfile
        {
            ImageCommand = EscPosImageCommand.BitImageEscStar,
            BitImageMode = EscPosBitImageMode.DoubleDensity24,
            HorizontalDpi = 203,
            VerticalDpi = 203,
            MotionUnitsPerInch = 203,
            MaxImageBlockBytes = 3
        };

        Assert.Throws<OperationCanceledException>(() => EscPosImageEncoder.Write(writer, 8, 24,
            new byte[24], profile, false, false, source.Token));
        Assert.Equal(2, writer.Writes.Count);
        Assert.Equal(new byte[] { 0x1B, 0x33, 24 }, writer.Writes[0]);
        Assert.Equal(new byte[] { 0x1B, 0x2A, 33, 1, 0 }, writer.Writes[1]);
    }

    [Theory]
    [InlineData(EscPosImageCommand.RasterGsV0)]
    [InlineData(EscPosImageCommand.BitImageEscStar)]
    public void None_overload_preserves_existing_bytes(EscPosImageCommand command)
    {
        var profile = new EscPosPrinterProfile
        {
            ImageCommand = command,
            MaxImageBlockBytes = command == EscPosImageCommand.RasterGsV0 ? 4 : 6,
            BitImageMode = EscPosBitImageMode.DoubleDensity24,
            HorizontalDpi = 203,
            VerticalDpi = 203,
            MotionUnitsPerInch = 203
        };
        var width = 16;
        var height = 26;
        var data = Enumerable.Range(0, ((width + 7) / 8) * height)
            .Select(index => (byte)(index * 17 + 3)).ToArray();
        var legacy = new RecordingWriter();
        var overload = new RecordingWriter();

        EscPosImageEncoder.Write(legacy, width, height, data, profile, false, false);
        EscPosImageEncoder.Write(overload, width, height, data, profile, false, false, CancellationToken.None);

        Assert.Equal(legacy.GetBytes(), overload.GetBytes());
    }

    private sealed class RecordingWriter : IBufferWriter
    {
        private readonly List<byte> _bytes = new List<byte>();
        private readonly Action<byte[]> _afterWrite;

        internal RecordingWriter(Action<byte[]> afterWrite = null) => _afterWrite = afterWrite;

        internal List<byte[]> Writes { get; } = new List<byte[]>();

        public IBufferWriter Write(byte[] value)
        {
            var copy = (byte[])value.Clone();
            Writes.Add(copy);
            _bytes.AddRange(copy);
            _afterWrite?.Invoke(copy);
            return this;
        }

        public IBufferWriter Write(string value) => Write(System.Text.Encoding.ASCII.GetBytes(value));

        public IBufferWriter Clear()
        {
            _bytes.Clear();
            Writes.Clear();
            return this;
        }

        public byte[] GetBytes() => _bytes.ToArray();
    }
}
