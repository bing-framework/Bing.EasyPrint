namespace Bing.EasyPrint.CPCL;

/// <summary>
/// CPCL命令信息
/// </summary>
// ReSharper disable once InconsistentNaming
public class CPCLCommandInfo
{
    /// <summary>
    /// 是否已设置初始化页面信息
    /// </summary>
    public bool HasBegin{ get; internal set; }

    /// <summary>
    /// 是否设置结尾信息。如：PRINT
    /// </summary>
    public bool HasEnd { get; internal set; }

    /// <summary>
    /// 偏移量。整个标签的横向偏移值
    /// </summary>
    /// <remarks>此值可以使所有域以指定的单位数量进行横向偏移</remarks>
    public int Offset { get; internal set; } = 0;

    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; internal set; }

    /// <summary>
    /// 高度。标签的最大高度。
    /// </summary>
    /// <remarks>
    /// 标签最大高度的计算方法是，先测出从第 1 个黑条（或标签间隙）底部到下一个黑条（或标签间隙）顶部之间的距离。
    /// 然后从中减去 1/16 英寸（1.5 毫米），所得结果即大高度。（以点为单位时：对于 203 d.p.i 打印机，减去 12 点；对于 306 d.p.i. 打印机，减去 18 点）
    /// </remarks>
    public int Height { get; internal set; }

    /// <summary>
    /// 标签数量。打印标签数量
    /// </summary>
    /// <remarks>最大值 = 1024</remarks>
    public int Qty { get; internal set; } = 1;

    /// <summary>
    /// 纸张旋转角度
    /// </summary>
    public int PagerRotate { get; internal set; }
}