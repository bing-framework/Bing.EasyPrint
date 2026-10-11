namespace Bing.EasyPrint;

/// <summary>ESC/POS Native 页面的顺序布局方式。</summary>
public enum EscPosNativeFlowMode
{
    /// <summary>保留既有文字行和原点码图行为。</summary>
    Rows = 0,
    /// <summary>按声明占位推进逻辑游标，支持文字与原生码图顺序混排。</summary>
    /// <remarks>坐标用于顺序、边界和分页校验；实际纸张位置由设备原生行距及码图进纸决定。</remarks>
    Sequential = 1
}
