// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 表示使用重复字符绘制分割线的 CPCL 打印组件。
/// </summary>
internal class SplitLineComponent : ComponentMetadata
{
    /// <summary>
    /// 获取或设置分割线起始位置的横坐标。
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// 获取或设置分割线起始位置的纵坐标。
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 获取或设置用于绘制分割线的字符，默认值为连字符。
    /// </summary>
    public char Symbol { get; set; } = '-';

    /// <inheritdoc />
    public override void Build(CPCLPrintCommand command)
    {
        CPCLInputValidator.ValidateCharacter(Symbol, nameof(Symbol));
        if (X < 0 || X > command.CommandInfo.Width) throw new System.ArgumentOutOfRangeException(nameof(X));
        var lineMaxLength = Helper.GetLineMaxLength(command.CommandInfo.Width - X, Symbol > 127 ? 24 : 12);
        var line = new string(Symbol, lineMaxLength);
        command.Writer.WriteLine("SETMAG 1 1");
        command.Writer.WriteLine($"T 24 0 {X} {Y} {line}");
        command.Writer.WriteLine("SETMAG 0 0");
    }
}
