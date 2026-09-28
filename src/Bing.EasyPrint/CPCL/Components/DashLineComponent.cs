
// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 将线段按固定间隔输出为虚线的组件。
/// </summary>
internal class DashLineComponent : LineComponent
{
    /// <inheritdoc />
    public override void Build(CPCLPrintCommand command)
    {
        command.Writer.WriteLine("SETMAG 1 1");
        for (long x = X0; x < X1; x += 16)
            command.Writer.WriteLine($"T 24 0 {x} {Y0 - 10} -");
        command.Writer.WriteLine("SETMAG 0 0");
    }
}
