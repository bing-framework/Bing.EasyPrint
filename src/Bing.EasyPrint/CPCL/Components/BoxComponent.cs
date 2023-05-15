// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 矩形组件
/// </summary>
internal class BoxComponent : ComponentMetadata
{
    /// <summary>
    /// 线条宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 矩形框左上角x坐标
    /// </summary>
    public int X0 { get; set; }

    /// <summary>
    /// 矩形框右下角x坐标
    /// </summary>
    public int X1 { get; set; }

    /// <summary>
    /// 矩形框左上角y坐标
    /// </summary>
    public int Y0 { get; set; }

    /// <summary>
    /// 矩形框右下角y坐标
    /// </summary>
    public int Y1 { get; set; }

    /// <summary>
    /// 构建
    /// </summary>
    /// <param name="command">打印命令</param>
    public override void Build(CPCLPrintCommand command) => command.Writer.WriteLine($"BOX {X0} {Y0} {X1} {Y1} {Width}");
}