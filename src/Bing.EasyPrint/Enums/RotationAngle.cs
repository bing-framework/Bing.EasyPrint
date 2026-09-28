// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint;

/// <summary>
/// 指定打印文本、条码或图片的旋转角度。
/// </summary>
public enum RotationAngle
{
    /// <summary>
    /// 不旋转输出内容。
    /// </summary>
    None = 0,
    /// <summary>
    /// 将输出内容旋转 90 度。
    /// </summary>
    Rotate90 = 90,
    /// <summary>
    /// 将输出内容旋转 180 度。
    /// </summary>
    Rotate180 = 180,
    /// <summary>
    /// 将输出内容旋转 270 度。
    /// </summary>
    Rotate270 = 270,
}
