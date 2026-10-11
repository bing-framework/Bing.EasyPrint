namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 表示图片源文件中的矩形裁切区域。
    /// </summary>
    /// <remarks>
    /// 坐标和尺寸使用源图片像素；渲染器会在按模板目标尺寸缩放前执行裁切。
    /// </remarks>
    public sealed class BpImageCrop
    {
        /// <summary>
        /// 获取或设置裁切区域左上角的水平像素坐标。
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 获取或设置裁切区域左上角的垂直像素坐标。
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 获取或设置裁切区域的像素宽度，必须大于零。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置裁切区域的像素高度，必须大于零。
        /// </summary>
        public int Height { get; set; }
    }
}
