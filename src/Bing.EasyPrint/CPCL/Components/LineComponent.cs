// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 表示输出直线的 CPCL 打印组件。
/// </summary>
internal class LineComponent : ComponentMetadata
{
    /// <summary>
    /// 获取或设置线条宽度，单位为打印点。
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 获取或设置线条起点的横坐标。
    /// </summary>
    public int X0 { get; set; }

    /// <summary>
    /// 获取或设置线条终点的横坐标。
    /// </summary>
    public int X1 { get; set; }

    /// <summary>
    /// 获取或设置线条起点的纵坐标。
    /// </summary>
    public int Y0 { get; set; }

    /// <summary>
    /// 获取或设置线条终点的纵坐标。
    /// </summary>
    public int Y1 { get; set; }

    /// <inheritdoc />
    public override void Build(CPCLPrintCommand command) => command.Writer.WriteLine($"LINE {X0} {Y0} {X1} {Y1} {Width}");
}
