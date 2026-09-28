namespace Bing.EasyPrint.Templates;

/// <summary>
/// 指定价签模板工厂提供的布局预设名称。
/// </summary>
/// <remarks>
/// 名称用于选择预设版式；实际打印尺寸由模板中的打印点宽度和高度决定。
/// </remarks>
public enum PriceLabelPreset
{
    /// <summary>
    /// 选择名为 15 mm 的方形二维码价签布局。
    /// </summary>
    Mm15 = 15,
    /// <summary>
    /// 选择名为 70 mm 的价签布局。
    /// </summary>
    Mm70 = 70,
    /// <summary>
    /// 选择名为 90 mm 的价签布局。
    /// </summary>
    Mm90 = 90
}
