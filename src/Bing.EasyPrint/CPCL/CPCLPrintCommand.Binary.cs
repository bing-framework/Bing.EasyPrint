using SixLabors.ImageSharp;

namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 提供二进制图片打印命令。
/// </summary>
partial class CPCLPrintCommand
{
    /// <summary>
    /// 将图片转换为 CPCL 二进制图形命令。
    /// </summary>
    /// <param name="x">图片左上角的水平打印点坐标。</param>
    /// <param name="y">图片左上角的垂直打印点坐标。</param>
    /// <param name="bitmap">调用方持有的图片实例。</param>
    /// <param name="options">可选的图片处理限制。</param>
    /// <returns>当前打印命令实例。</returns>
    /// <remarks>
    /// 不修改调用方图片；二进制结果需通过 Build().GetBytes() 传输。
    /// </remarks>
    public CPCLPrintCommand DrawCompressedImage(int x, int y, Image bitmap, ImageProcessingOptions options = null)
    {
        var image = ImageComponent.CreateFromImage(bitmap, options ?? new ImageProcessingOptions());
        Content.Add(new BinaryImageMetadata { X = x, Y = y, Image = image });
        return this;
    }

    /// <summary>
    /// 保存待写入的二进制图片及打印位置。
    /// </summary>
    private sealed class BinaryImageMetadata : ComponentMetadata
    {
        /// <summary>
        /// 获取图片左上角的水平和垂直打印点坐标。
        /// </summary>
        internal int X, Y;
        /// <summary>
        /// 获取待编码的图片数据。
        /// </summary>
        internal ImageComponent Image;
        /// <inheritdoc />
        public override void Build(CPCLPrintCommand command)
        {
            command.Writer.Write($"CG {(Image.Width + 7L) / 8} {Image.Height} {X} {Y} ");
            command.Writer.Write(Image.Data);
            command.Writer.Write("\r\n");
        }
    }
}
