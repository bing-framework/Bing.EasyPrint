using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;

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

    /// <inheritdoc />
    public override void Build(CPCLPrintCommand command)
    {
        var byteCountW = checked((int)(((long)Width + 7) / 8));
        var data = ToHex(Data);
        var cmd = Helper.GetImageRotateCommand(Rotate);
        command.Writer.WriteLine($"{cmd} {byteCountW} {Height} {X} {Y} {data}");
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
        MemoryStream bufferedStream = null;
        try
        {
            Stream source = stream;
            if (stream.CanSeek)
            {
                ValidateEncodedLength(stream.Length - originalPosition.Value, options.MaxEncodedBytes);
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
            using (var image = Image.Load(Configuration.Default, source, decoder))
            {
                ValidateDimensions(image.Width, image.Height, options);
                return new ImageComponent
                {
                    Width = image.Width,
                    Height = image.Height,
                    Data = GetImageData(image)
                };
            }
        }
        finally
        {
            if (originalPosition.HasValue)
                stream.Position = originalPosition.Value;
            bufferedStream?.Dispose();
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

        using (var copy = image.CloneAs<Rgba32>())
        {
            return new ImageComponent
            {
                Width = copy.Width,
                Height = copy.Height,
                Data = GetImageData(copy)
            };
        }
    }

    /// <summary>
    /// 将独立图片副本转换为按行打包的单色像素。
    /// </summary>
    /// <param name="image">允许原地转换的内部图片副本。</param>
    /// <returns>每行按八像素对齐的单色字节。</returns>
    private static byte[] GetImageData(Image image)
    {
        image.Mutate(context =>
        {
            context.Grayscale(GrayscaleMode.Bt601);
            context.BinaryDither(ErrorDither.FloydSteinberg);
        });
        // 直接打包二值像素，避免 BMP 往返及编码器对不足八像素行的越界问题。
        if (image is Image<Rgba32> rgba)
            return PackPixels(rgba);
        using (var copy = image.CloneAs<Rgba32>())
            return PackPixels(copy);
    }

    /// <summary>
    /// 将黑白像素按每字节八位写入行缓冲区。
    /// </summary>
    /// <param name="image">已完成二值化的图片。</param>
    /// <returns>每行按八像素对齐的单色字节。</returns>
    private static byte[] PackPixels(Image<Rgba32> image)
    {
        var rowBytes = checked((int)(((long)image.Width + 7) / 8));
        var result = new byte[checked(rowBytes * image.Height)];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var pixels = accessor.GetRowSpan(y);
                var offset = y * rowBytes;
                for (var x = 0; x < pixels.Length; x++)
                    if (pixels[x].R < 128)
                        result[offset + x / 8] |= (byte)(0x80 >> (x % 8));
            }
        });
        // 数组初始为零，末字节未使用的低位保持白色。
        return result;
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
        var copy = new MemoryStream();
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
    /// <returns>每字节对应两个大写十六进制字符的文本。</returns>
    private static string ToHex(byte[] data)
    {
        const string digits = "0123456789ABCDEF";
        var chars = new char[checked(data.Length * 2)];
        for (var i = 0; i < data.Length; i++)
        {
            chars[i * 2] = digits[data[i] >> 4];
            chars[i * 2 + 1] = digits[data[i] & 0x0F];
        }
        return new string(chars);
    }
}
