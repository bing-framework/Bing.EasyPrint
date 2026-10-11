using System;

namespace Bing.EasyPrint;

/// <summary>描述主机侧指令参数的整数范围；不代表设备的物理或资源容量。</summary>
public sealed class PrintParameterRange
{
    /// <summary>获取包含在范围内的最小值。</summary>
    public int Minimum { get; }
    /// <summary>获取包含在范围内的最大值；null 表示未声明额外上限。</summary>
    public int? Maximum { get; }

    /// <summary>创建整数参数范围。</summary>
    public PrintParameterRange(int minimum, int? maximum = null)
    {
        if (maximum.HasValue && maximum.Value < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>判断参数是否位于声明的范围内。</summary>
    public bool Contains(int value) => value >= Minimum && (!Maximum.HasValue || value <= Maximum.Value);
}
