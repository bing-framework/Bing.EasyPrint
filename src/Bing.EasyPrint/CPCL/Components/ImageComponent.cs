using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Core;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 将图片转换为 CPCL 单色位图的组件。
/// </summary>
internal class ImageComponent : ComponentMetadata
{
    /// <summary>
    /// 获取或设置图片的打印旋转角度。
    /// </summary>
    public int Rotate { get; set; }

    /// <summary>
    /// 获取或设置图片左上角的水平打印点坐标。
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// 获取或设置图片左上角的垂直打印点坐标。
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 获取按行打包的单色像素字节。
    /// </summary>
    public byte[] Data { get; private set; }

    /// <summary>
    /// 获取原图宽度，单位为像素。
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// 获取原图高度，单位为像素。
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// 初始化一个 <see cref="ImageComponent"/> 类型的实例。
    /// </summary>
    private ImageComponent() { }

    internal static ImageComponent CreateFromRaster(int width, int height, byte[] raster)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
        if (raster == null) throw new ArgumentNullException(nameof(raster));
        if (raster.Length != checked(((width + 7) / 8) * height))
            throw new ArgumentException("图片数据长度与尺寸不匹配。", nameof(raster));
        return new ImageComponent { Width = width, Height = height,
            Data = BudgetedBuffer.Copy(raster, "Cpcl.ImageSnapshot", CancellationToken.None) };
    }

    /// <inheritdoc />
    public override void Build(CPCLPrintCommand command)
    {
        var byteCountW = checked((int)(((long)Width + 7) / 8));
        var cancellationToken = (command.Writer as BufferWriter)?.BuildCancellationToken ?? CancellationToken.None;
        var data = ToHex(Data, cancellationToken);
        var cmd = Helper.GetImageRotateCommand(Rotate);
        var prefix = $"{cmd} {byteCountW} {Height} {X} {Y} ";
        PrintResourceScope.Retain(checked((long)(prefix.Length + data.Length) * 2), "Cpcl.ImageCommand");
        cancellationToken.ThrowIfCancellationRequested();
        command.Writer.WriteLine(prefix + data);
    }

    /// <summary>
    /// 设置图片在页面中的起始位置。
    /// </summary>
    /// <param name="x">水平打印点坐标。</param>
    /// <param name="y">垂直打印点坐标。</param>
    /// <returns>当前图片组件实例。</returns>
    public ImageComponent InitPosition(int x, int y)
    {
        X = x;
        Y = y;
        return this;
    }

    /// <summary>
    /// 从本地文件创建图片组件。
    /// </summary>
    /// <param name="fileUrl">调用方指定的本地文件路径。</param>
    /// <returns>包含单色位图数据的图片组件。</returns>
    public static ImageComponent CreateFromFile(string fileUrl)
    {
        return CreateFromFile(fileUrl, new ImageProcessingOptions());
    }

    /// <summary>
    /// 从本地文件创建受大小限制的图片组件。
    /// </summary>
    /// <param name="fileUrl">调用方指定的本地文件路径。</param>
    /// <param name="options">编码长度和像素数限制。</param>
    /// <returns>包含单色位图数据的图片组件。</returns>
    public static ImageComponent CreateFromFile(string fileUrl, ImageProcessingOptions options)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            throw new ArgumentException("图片路径不能为空。", nameof(fileUrl));
        using (var stream = File.OpenRead(fileUrl))
            return CreateFromStream(stream, options);
    }

    /// <summary>
    /// 从流创建图片组件。
    /// </summary>
    /// <param name="stream">调用方持有的图片流。</param>
    /// <returns>包含单色位图数据的图片组件。</returns>
    public static ImageComponent CreateFromStream(Stream stream)
    {
        return CreateFromStream(stream, new ImageProcessingOptions());
    }

    /// <summary>
    /// 从受大小限制的流创建图片组件。
    /// </summary>
    /// <param name="stream">调用方持有的图片流；可定位时会恢复原位置。</param>
    /// <param name="options">编码长度和像素数限制。</param>
    /// <returns>包含单色位图数据的图片组件。</returns>
    public static ImageComponent CreateFromStream(Stream stream, ImageProcessingOptions options)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        options.Validate();

        var originalPosition = stream.CanSeek ? (long?)stream.Position : null;
        OffsetReadStream offsetStream = null;
        MemoryStream bufferedStream = null;
        try
        {
            Stream source = stream;
            if (stream.CanSeek)
            {
                ValidateEncodedLength(stream.Length - originalPosition.Value, options.MaxEncodedBytes);
                offsetStream = new OffsetReadStream(stream, originalPosition.Value);
                source = offsetStream;
            }
            else
            {
                bufferedStream = CopyToBoundedStream(stream, options.MaxEncodedBytes);
                source = bufferedStream;
            }

            var sourcePosition = source.CanSeek ? source.Position : 0;
            var imageInfo = Image.Identify(Configuration.Default, source, out var format);
            if (imageInfo == null)
                throw new InvalidDataException("无法识别图片格式。");
            ValidateDimensions(imageInfo.Width, imageInfo.Height, options);
            if (format == WebpFormat.Instance && IsAnimatedWebp(source, sourcePosition))
                throw new ArgumentException("不支持多帧 WebP 图片。", nameof(stream));
            if (source.CanSeek)
                source.Position = sourcePosition;

            var decoder = GetSingleFrameDecoder(format);
            using (var decoderMemory = PrintResourceScope.Reserve(
                       checked((long)imageInfo.Width * imageInfo.Height * 4), "Cpcl.ImageDecodeRgba"))
            using (var image = Image.Load(Configuration.Default, source, decoder))
            {
                ValidateDimensions(image.Width, image.Height, options);
                return new ImageComponent
                {
                    Width = image.Width,
                    Height = image.Height,
                    Data = PrintImageRasterizer.ToCpclMonochrome(image)
                };
            }
        }
        finally
        {
            try
            {
                if (originalPosition.HasValue)
                    stream.Position = originalPosition.Value;
            }
            finally
            {
                offsetStream?.Dispose();
                bufferedStream?.Dispose();
            }
        }
    }

    /// <summary>
    /// 从编码后的图片字节创建图片组件。
    /// </summary>
    /// <param name="bytes">调用方持有的编码后图片字节。</param>
    /// <returns>包含单色位图数据的图片组件。</returns>
    public static ImageComponent CreateFromBytes(byte[] bytes)
    {
        return CreateFromBytes(bytes, new ImageProcessingOptions());
    }

    /// <summary>
    /// 从受大小限制的图片字节创建图片组件。
    /// </summary>
    /// <param name="bytes">调用方持有的编码后图片字节。</param>
    /// <param name="options">编码长度和像素数限制。</param>
    /// <returns>包含单色位图数据的图片组件。</returns>
    public static ImageComponent CreateFromBytes(byte[] bytes, ImageProcessingOptions options)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        options.Validate();
        ValidateEncodedLength(bytes.LongLength, options.MaxEncodedBytes);
        using (var stream = new MemoryStream(bytes, false))
            return CreateFromStream(stream, options);
    }

    /// <summary>
    /// 从现有图片创建图片组件。
    /// </summary>
    /// <param name="image">调用方持有且不会被修改的图片。</param>
    /// <returns>包含单色位图数据的图片组件。</returns>
    public static ImageComponent CreateFromImage(Image image)
    {
        return CreateFromImage(image, new ImageProcessingOptions());
    }

    /// <summary>
    /// 从现有图片的副本创建受像素数限制的图片组件。
    /// </summary>
    /// <param name="image">调用方持有且不会被修改的图片。</param>
    /// <param name="options">编码长度和像素数限制。</param>
    /// <returns>包含单色位图数据的图片组件。</returns>
    public static ImageComponent CreateFromImage(Image image, ImageProcessingOptions options)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        options.Validate();
        ValidateDimensions(image.Width, image.Height, options);
        if (image.Frames.Count != 1)
            throw new ArgumentException("不支持多帧图片。", nameof(image));

        return new ImageComponent
        {
            Width = image.Width,
            Height = image.Height,
            Data = PrintImageRasterizer.ToCpclMonochrome(image)
        };
    }

    /// <summary>
    /// 获取仅解码首帧的图片解码器。
    /// </summary>
    /// <param name="format">已识别的图片格式。</param>
    /// <returns>对应格式的图片解码器。</returns>
    private static IImageDecoder GetSingleFrameDecoder(IImageFormat format)
    {
        if (format == null)
            throw new InvalidDataException("无法识别图片格式。");
        if (format == GifFormat.Instance)
            return new GifDecoder { DecodingMode = FrameDecodingMode.First, MaxFrames = 1 };
        if (format == TiffFormat.Instance)
            return new TiffDecoder { DecodingMode = FrameDecodingMode.First };

        var decoder = Configuration.Default.ImageFormatsManager.FindDecoder(format);
        if (decoder == null)
            throw new InvalidDataException("不支持的图片格式。");
        return decoder;
    }

    /// <summary>
    /// 检查 WebP 扩展块是否声明动画。
    /// </summary>
    /// <param name="source">可定位的图片流。</param>
    /// <param name="startPosition">图片数据在流中的起始位置。</param>
    /// <returns>声明动画时返回 true，否则返回 false。</returns>
    private static bool IsAnimatedWebp(Stream source, long startPosition)
    {
        var originalPosition = source.Position;
        try
        {
            source.Position = startPosition;
            var header = new byte[12];
            if (!ReadExactly(source, header, header.Length) ||
                header[0] != (byte)'R' || header[1] != (byte)'I' || header[2] != (byte)'F' || header[3] != (byte)'F' ||
                header[8] != (byte)'W' || header[9] != (byte)'E' || header[10] != (byte)'B' || header[11] != (byte)'P')
                return false;

            while (source.Length - source.Position >= 8)
            {
                var chunkHeader = new byte[8];
                if (!ReadExactly(source, chunkHeader, chunkHeader.Length))
                    return false;
                var chunkLength = chunkHeader[4] | (long)chunkHeader[5] << 8 |
                                  (long)chunkHeader[6] << 16 | (long)chunkHeader[7] << 24;
                if (chunkLength > source.Length - source.Position)
                    return false;
                if (chunkHeader[0] == (byte)'V' && chunkHeader[1] == (byte)'P' &&
                    chunkHeader[2] == (byte)'8' && chunkHeader[3] == (byte)'X')
                {
                    var flags = source.ReadByte();
                    return flags >= 0 && (flags & 0x02) != 0;
                }

                var paddedChunkLength = chunkLength + (chunkLength & 1);
                if (paddedChunkLength > source.Length - source.Position)
                    return false;
                source.Position += paddedChunkLength;
            }

            return false;
        }
        finally
        {
            source.Position = originalPosition;
        }
    }

    /// <summary>
    /// 尝试从流读取指定数量的字节。
    /// </summary>
    /// <param name="stream">输入流。</param>
    /// <param name="buffer">目标缓冲区。</param>
    /// <param name="count">要求读取的字节数。</param>
    /// <returns>读取完整数据时返回 true；提前到达末尾时返回 false。</returns>
    private static bool ReadExactly(Stream stream, byte[] buffer, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = stream.Read(buffer, offset, count - offset);
            if (read == 0)
                return false;
            offset += read;
        }
        return true;
    }

    /// <summary>
    /// 将不可定位的输入流复制到受限缓冲区。
    /// </summary>
    /// <param name="source">调用方持有的图片输入流。</param>
    /// <param name="maxEncodedBytes">最大编码字节数。</param>
    /// <returns>定位至起始位置且由调用方释放的缓冲流。</returns>
    private static MemoryStream CopyToBoundedStream(Stream source, long maxEncodedBytes)
    {
        using (PrintResourceScope.Reserve(81920, "Cpcl.ImageReadBuffer"))
        {
            var copy = new BudgetedMemoryStream();
            var buffer = new byte[81920];
            try
            {
                while (true)
                {
                    var remaining = maxEncodedBytes - copy.Length;
                    var readLimit = remaining >= buffer.Length ? buffer.Length : (int)remaining + 1;
                    var read = source.Read(buffer, 0, readLimit);
                    if (read == 0)
                        break;
                    if (read > remaining)
                        throw new ArgumentException("图片编码数据超过配置的字节数限制。", nameof(source));

                    EnsureCopyStreamCapacity(copy, checked(copy.Length + read));
                    copy.Write(buffer, 0, read);
                }

                copy.Position = 0;
                return copy;
            }
            catch
            {
                copy.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// 将非定位图片缓冲流扩容到能够容纳下一次读取的数据，并在扩容成功后释放旧容量租约。
    /// </summary>
    /// <param name="target">受预算管理的目标缓冲流。</param>
    /// <param name="required">下一次写入完成后需要的容量。</param>
    private static void EnsureCopyStreamCapacity(BudgetedMemoryStream target, long required)
    {
        if (required <= target.Capacity)
            return;
        if (required > int.MaxValue)
            throw new ArgumentException("图片编码数据超过流容量限制。", nameof(required));

        var current = target.Capacity;
        var doubled = current == 0 ? 8192L : current > int.MaxValue / 2 ? int.MaxValue : (long)current * 2;
        var requested = Math.Min((long)int.MaxValue, Math.Max(required, doubled));
        var nextLease = PrintResourceScope.Reserve(requested, "Cpcl.ImageReadStream");
        try
        {
            // Capacity 的实际分配发生在旧租约仍然存活时，避免低估扩容峰值。
            target.Capacity = checked((int)requested);
            target.ReplaceCapacityLease(nextLease);
            nextLease = null;
        }
        finally
        {
            nextLease?.Dispose();
        }
    }

    /// <summary>
    /// 为非 seek 输入流持有当前 MemoryStream 容量租约的私有实现。
    /// </summary>
    private sealed class BudgetedMemoryStream : MemoryStream
    {
        private IDisposable capacityLease;

        internal void ReplaceCapacityLease(IDisposable nextLease)
        {
            if (nextLease == null)
                throw new ArgumentNullException(nameof(nextLease));

            var previousLease = capacityLease;
            capacityLease = nextLease;
            previousLease?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                base.Dispose(disposing);
                return;
            }

            var lease = capacityLease;
            capacityLease = null;
            try
            {
                base.Dispose(true);
            }
            finally
            {
                lease?.Dispose();
            }
        }
    }

    /// <summary>
    /// 将可定位调用方流的剩余区间映射为从零开始的只读图片流视图。
    /// </summary>
    private sealed class OffsetReadStream : Stream
    {
        private readonly Stream inner;
        private readonly long offset;
        private readonly long length;
        private bool disposed;

        internal OffsetReadStream(Stream inner, long offset)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (!inner.CanSeek)
                throw new ArgumentException("图片输入流必须支持定位。", nameof(inner));
            if (offset < 0 || offset > inner.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            this.offset = offset;
            length = inner.Length - offset;
            inner.Position = offset;
        }

        public override bool CanRead => !disposed && inner.CanRead;
        public override bool CanSeek => !disposed && inner.CanSeek;
        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                EnsureNotDisposed();
                return length;
            }
        }

        public override long Position
        {
            get
            {
                EnsureNotDisposed();
                return checked(inner.Position - offset);
            }
            set
            {
                EnsurePosition(value);
                inner.Position = checked(offset + value);
            }
        }

        public override int Read(byte[] buffer, int bufferOffset, int count)
        {
            EnsureNotDisposed();
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (bufferOffset < 0 || count < 0 || buffer.Length - bufferOffset < count)
                throw new ArgumentOutOfRangeException();
            if (count == 0)
                return 0;

            var remaining = length - Position;
            if (remaining <= 0)
                return 0;
            var readCount = (int)Math.Min((long)count, remaining);
            return inner.Read(buffer, bufferOffset, readCount);
        }

        public override int ReadByte()
        {
            EnsureNotDisposed();
            return Position >= length ? -1 : inner.ReadByte();
        }

        public override long Seek(long seekOffset, SeekOrigin origin)
        {
            EnsureNotDisposed();
            var basePosition = origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => Position,
                SeekOrigin.End => length,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            var target = checked(basePosition + seekOffset);
            EnsurePosition(target);
            inner.Position = checked(offset + target);
            return target;
        }

        public override void Flush()
        {
            EnsureNotDisposed();
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int bufferOffset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            disposed = true;
            // 视图不得关闭调用方持有的底层流。
            base.Dispose(disposing);
        }

        private void EnsureNotDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(OffsetReadStream));
        }

        private void EnsurePosition(long value)
        {
            EnsureNotDisposed();
            if (value < 0 || value > length)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    /// <summary>
    /// 校验编码后图片的字节数上限。
    /// </summary>
    /// <param name="length">编码后字节数。</param>
    /// <param name="maxEncodedBytes">允许的最大字节数。</param>
    private static void ValidateEncodedLength(long length, long maxEncodedBytes)
    {
        if (length < 0 || length > maxEncodedBytes)
            throw new ArgumentException("图片编码数据超过配置的字节数限制。", "bytes");
    }

    /// <summary>
    /// 校验像素数及打包结果的可分配长度。
    /// </summary>
    /// <param name="width">图片像素宽度。</param>
    /// <param name="height">图片像素高度。</param>
    /// <param name="options">像素数限制。</param>
    private static void ValidateDimensions(int width, int height, ImageProcessingOptions options)
    {
        if (width <= 0 || height <= 0 || (long)width * height > options.MaxPixels)
            throw new ArgumentOutOfRangeException(nameof(width), "图片尺寸超过配置的像素数限制。");
        var rowRealBytesCount = ((long)width + 7) / 8;
        if (rowRealBytesCount * height > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "图片转换结果超过可处理大小。");
    }

    /// <summary>
    /// 将图形字节转换为十六进制文本。
    /// </summary>
    /// <param name="data">待转换的图形字节。</param>
    /// <param name="cancellationToken">转换过程中检查的取消令牌。</param>
    /// <returns>每字节对应两个大写十六进制字符的文本。</returns>
    private static string ToHex(byte[] data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var memory = PrintResourceScope.Reserve(checked(data.LongLength * 4), "Cpcl.HexCharacters");
        const string digits = "0123456789ABCDEF";
        var chars = new char[checked(data.Length * 2)];
        for (var i = 0; i < data.Length; i++)
        {
            if ((i & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            chars[i * 2] = digits[data[i] >> 4];
            chars[i * 2 + 1] = digits[data[i] & 0x0F];
        }
        PrintResourceScope.Retain(checked(data.LongLength * 4), "Cpcl.HexString");
        var result = new string(chars);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
