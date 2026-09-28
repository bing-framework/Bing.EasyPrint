// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint;

/// <summary>
/// 指定打印页面的方向或旋转方式。
/// </summary>
public enum PrintOrientation
{
    /// <summary>
    /// 使用默认页面方向。
    /// </summary>
    None = 0,
    /// <summary>
    /// 按打印机约定向下旋转页面。
    /// </summary>
    Down = 1,
    /// <summary>
    /// 向左旋转页面。
    /// </summary>
    Left = 2,
    /// <summary>
    /// 向右旋转页面。
    /// </summary>
    Right = 3
}
