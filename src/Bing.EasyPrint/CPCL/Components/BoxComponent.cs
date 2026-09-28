// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 表示输出矩形边框的 CPCL 打印组件。
/// </summary>
internal class BoxComponent : ComponentMetadata
{
    /// <summary>
    /// 获取或设置矩形边框线宽，单位为打印点。
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 获取或设置矩形左上角的横坐标。
    /// </summary>
    public int X0 { get; set; }

    /// <summary>
    /// 获取或设置矩形右下角的横坐标。
    /// </summary>
    public int X1 { get; set; }

    /// <summary>
    /// 获取或设置矩形左上角的纵坐标。
    /// </summary>
    public int Y0 { get; set; }

    /// <summary>
    /// 获取或设置矩形右下角的纵坐标。
    /// </summary>
    public int Y1 { get; set; }

    /// <inheritdoc />
    public override void Build(CPCLPrintCommand command) => command.Writer.WriteLine($"BOX {X0} {Y0} {X1} {Y1} {Width}");
}
