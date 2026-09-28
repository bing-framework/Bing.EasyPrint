// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint;

/// <summary>
/// 指定 CPCL 文本输出使用的样式标志。
/// </summary>
public enum TextStyle
{
    /// <summary>
    /// 不启用额外文字样式。
    /// </summary>
    None = 0,
    /// <summary>
    /// 启用粗体输出。
    /// </summary>
    Bold = 1,
    /// <summary>
    /// 表示斜体样式；当前 CPCL 样式转换不生成斜体指令。
    /// </summary>
    Italic = 2,
    /// <summary>
    /// 启用下划线输出。
    /// </summary>
    Underline = 4
}
