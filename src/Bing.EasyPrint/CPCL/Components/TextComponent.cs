
// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 文本组件
/// </summary>
internal class TextComponent : ComponentMetadata
{
    /// <summary>
    /// 是否加粗
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// 字体大小
    /// </summary>
    public int FontSize { get; set; }

    /// <summary>
    /// 字体缩放
    /// </summary>
    public int FontZoom { get; set; }

    /// <summary>
    /// 是否反显
    /// </summary>
    public bool Reverse { get; set; }

    /// <summary>
    /// 旋转角度
    /// </summary>
    public int Rotate { get; set; }

    /// <summary>
    /// 内容
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// 文字起始x坐标
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// 文字起始y坐标
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 是否下划线
    /// </summary>
    public bool Underline { get; set; }

    /// <summary>
    /// 构建
    /// </summary>
    /// <param name="command">打印命令</param>
    public override void Build(CPCLPrintCommand command)
    {
        AddUnderline(command, true);
        AddBold(command, true);

        var computeFontSizeResult = Helper.ComputeFontSize(FontSize);
        command.Writer.WriteLine($"SETMAG {computeFontSizeResult.scale} {computeFontSizeResult.scale}");

        AddText(command, computeFontSizeResult.font, computeFontSizeResult.size);
        AddInverseLine(command, computeFontSizeResult.size);
        AddBold(command, false);
        AddUnderline(command, false);
        command.Writer.WriteLine($"SETMAG 0 0");
    }

    /// <summary>
    /// 添加下划线
    /// </summary>
    /// <param name="command">打印命令</param>
    /// <param name="isOpen">是否开标签</param>
    private void AddUnderline(CPCLPrintCommand command, bool isOpen)
    {
        if (!Underline)
            return;
        if (isOpen)
        {
            command.Writer.WriteLine("UNDERLINE ON");
            return;
        }
        command.Writer.WriteLine("UNDERLINE OFF");
    }

    /// <summary>
    /// 添加粗体
    /// </summary>
    /// <param name="command">打印命令</param>
    /// <param name="isOpen">是否开标签</param>
    private void AddBold(CPCLPrintCommand command, bool isOpen)
    {
        if (!Bold)
            return;
        if (isOpen)
        {
            command.Writer.WriteLine("SETBOLD 1");
            return;
        }
        command.Writer.WriteLine("SETBOLD 0");
    }

    /// <summary>
    /// 添加文本
    /// </summary>
    /// <param name="command">打印命令</param>
    /// <param name="font">字体</param>
    /// <param name="size">字体大小</param>
    private void AddText(CPCLPrintCommand command, int font, int size)
    {
        var cmd = Helper.GetTextRotateCommand(Rotate);
        command.Writer.WriteLine($"{cmd} {font} {size} {X} {Y} {Text}");
    }

    /// <summary>
    /// 添加黑白反显
    /// </summary>
    /// <param name="command">打印命令</param>
    /// <param name="size">大小</param>
    private void AddInverseLine(CPCLPrintCommand command, int size)
    {
        if (!Reverse)
            return;
        command.Writer.WriteLine($"IL {X} {Y} {X + size / 2 * Text.Length} {Y} {size}");
    }
}