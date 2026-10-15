namespace Bing.EasyPrint.Printing;

/// <summary>GS1 Composite 二维组件的编码模式。</summary>
public enum Gs1CompositeMode
{
    /// <summary>优先 CC-A；容量不足时改用 CC-B。</summary>
    Auto = 0,
    /// <summary>仅使用 CC-A，超过容量时失败。</summary>
    CcA = 1,
    /// <summary>仅使用 CC-B，超过容量时失败。</summary>
    CcB = 2
}
