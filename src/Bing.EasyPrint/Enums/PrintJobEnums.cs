namespace Bing.EasyPrint;

/// <summary>多页任务的份数输出顺序。</summary>
public enum PrintCopyOrder
{
    /// <summary>逐份输出完整页面序列，如 A、B、A、B。</summary>
    Collated = 0,
    /// <summary>逐页输出全部份数，如 A、A、B、B。</summary>
    Uncollated = 1
}

/// <summary>额外进纸或切纸的执行边界。</summary>
public enum PrintFinishScope
{
    /// <summary>不执行。</summary>
    None = 0,
    /// <summary>每个实际输出页后执行。</summary>
    EachPage = 1,
    /// <summary>每份完整文档结束后执行。</summary>
    EachCopy = 2,
    /// <summary>整个任务的最后一个输出页后执行。</summary>
    JobEnd = 3
}

/// <summary>明确的进纸数量单位，不把行数与点数互换。</summary>
public enum PrintFeedUnit
{
    /// <summary>设备当前行距下的行数，ESC/POS ESC d。</summary>
    Lines = 0,
    /// <summary>设备纵向打印点；ESC/POS 要求 Profile 声明纵向 DPI 和运动单位。</summary>
    Dots = 1,
    /// <summary>设备纵向运动单位，ESC/POS ESC J。</summary>
    MotionUnits = 2
}
