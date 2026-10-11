namespace Bing.EasyPrint.Protocols;

/// <summary>声明原生条码可读文字（HRI）超出估算条码矩形的页面边距。</summary>
/// <remarks>
/// 数值使用打印点。角度专用配置中，未指定的边继承 Profile 通用边距；显式零表示不增加该边距。
/// </remarks>
public readonly struct NativeBarcodeHumanReadableOverhang
{
    /// <summary>创建可选的页面左、上、右、下边距覆盖值。</summary>
    /// <param name="leftDots">左侧外伸；null 表示继承通用配置。</param>
    /// <param name="topDots">上侧外伸；null 表示继承通用配置。</param>
    /// <param name="rightDots">右侧外伸；null 表示继承通用配置。</param>
    /// <param name="bottomDots">下侧外伸；null 表示继承通用配置。</param>
    public NativeBarcodeHumanReadableOverhang(int? leftDots = null, int? topDots = null,
        int? rightDots = null, int? bottomDots = null)
    {
        if (leftDots < 0) throw new System.ArgumentOutOfRangeException(nameof(leftDots));
        if (topDots < 0) throw new System.ArgumentOutOfRangeException(nameof(topDots));
        if (rightDots < 0) throw new System.ArgumentOutOfRangeException(nameof(rightDots));
        if (bottomDots < 0) throw new System.ArgumentOutOfRangeException(nameof(bottomDots));
        LeftDots = leftDots;
        TopDots = topDots;
        RightDots = rightDots;
        BottomDots = bottomDots;
    }

    /// <summary>获取页面左侧外伸点数；null 表示继承 Profile 通用配置。</summary>
    public int? LeftDots { get; }
    /// <summary>获取页面上侧外伸点数；null 表示继承 Profile 通用配置。</summary>
    public int? TopDots { get; }
    /// <summary>获取页面右侧外伸点数；null 表示继承 Profile 通用配置。</summary>
    public int? RightDots { get; }
    /// <summary>获取页面下侧外伸点数；null 表示继承 Profile 通用配置。</summary>
    public int? BottomDots { get; }
}
