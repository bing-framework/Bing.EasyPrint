using System;

namespace Bing.EasyPrint;

/// <summary>
/// 图片解码与打印转换限制。
/// </summary>
public sealed class ImageProcessingOptions
{
    /// <summary>
    /// 获取或设置图片编码数据允许的最大字节数，默认值为 10 MiB。
    /// </summary>
    public long MaxEncodedBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>
    /// 获取或设置图片允许的最大像素数，默认值为 1600 万像素。
    /// </summary>
    public long MaxPixels { get; set; } = 16_000_000;

    /// <summary>
    /// 校验图片大小和像素数上限均为正数。
    /// </summary>
    internal void Validate()
    {
        if (MaxEncodedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxEncodedBytes), "图片编码数据限制必须大于 0。");
        if (MaxPixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxPixels), "图片像素限制必须大于 0。");
    }
}
