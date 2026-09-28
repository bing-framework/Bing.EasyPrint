namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 保存 CPCL 打印任务的页面与份数设置。
/// </summary>
// ReSharper disable once InconsistentNaming
public class CPCLCommandInfo
{
    /// <summary>
    /// 获取是否已设置页面起始信息。
    /// </summary>
    public bool HasBegin{ get; internal set; }

    /// <summary>
    /// 获取是否已设置打印任务结束信息。
    /// </summary>
    public bool HasEnd { get; internal set; }

    /// <summary>
    /// 获取标签整体的水平偏移量，单位为打印点。
    /// </summary>
    /// <remarks>此值可以使所有域以指定的单位数量进行横向偏移</remarks>
    public int Offset { get; internal set; } = 0;

    /// <summary>
    /// 获取页面宽度，单位为打印点。
    /// </summary>
    public int Width { get; internal set; }

    /// <summary>
    /// 获取页面最大高度，单位为打印点。
    /// </summary>
    public int Height { get; internal set; }

    /// <summary>
    /// 获取打印份数，最大值为 1024。
    /// </summary>
    /// <remarks>最大值 = 1024</remarks>
    public int Qty { get; internal set; } = 1;

    /// <summary>
    /// 获取页面旋转角度。
    /// </summary>
    public int PagerRotate { get; internal set; }
}
