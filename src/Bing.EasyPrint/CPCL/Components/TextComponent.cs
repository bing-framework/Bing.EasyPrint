// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 表示输出文本及其样式的 CPCL 打印组件。
/// </summary>
internal class TextComponent : ComponentMetadata
{
    /// <summary>
    /// 初始化一个 <see cref="TextComponent"/> 类型的实例。
    /// </summary>
    /// <param name="text">要输出的文本内容。</param>
    public TextComponent(string text)
    {
        CPCLInputValidator.ValidateLine(text, nameof(text));
        Text = text;
    }

    /// <summary>
    /// 获取或设置是否以粗体输出文本。
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// 获取或设置用于选择输出字号的参数。
    /// </summary>
    public int FontSize { get; set; }

    /// <summary>
    /// 获取或设置文本的字体缩放值。
    /// </summary>
    public int FontZoom { get; set; }

    /// <summary>
    /// 获取或设置是否为文本添加反显线条。
    /// </summary>
    public bool Reverse { get; set; }

    /// <summary>
    /// 获取或设置文本的旋转角度。
    /// </summary>
    public int Rotate { get; set; }

    /// <summary>
    /// 获取要输出的文本内容。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 获取或设置文本起始位置的横坐标。
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// 获取或设置文本起始位置的纵坐标。
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 获取或设置是否为文本添加下划线。
    /// </summary>
    public bool Underline { get; set; }

    /// <inheritdoc />
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
    /// 根据状态向打印命令添加下划线开关指令。
    /// </summary>
    /// <param name="command">接收下划线指令的打印命令。</param>
    /// <param name="isOpen">为 <see langword="true"/> 时开启下划线，否则关闭下划线。</param>
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
    /// 根据状态向打印命令添加粗体开关指令。
    /// </summary>
    /// <param name="command">接收粗体指令的打印命令。</param>
    /// <param name="isOpen">为 <see langword="true"/> 时开启粗体，否则关闭粗体。</param>
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
    /// 根据旋转角度和字号向打印命令添加文本指令。
    /// </summary>
    /// <param name="command">接收文本指令的打印命令。</param>
    /// <param name="font">CPCL 字体标识。</param>
    /// <param name="size">CPCL 字号值。</param>
    private void AddText(CPCLPrintCommand command, int font, int size)
    {
        var cmd = Helper.GetTextRotateCommand(Rotate);
        command.Writer.WriteLine($"{cmd} {font} {size} {X} {Y} {Text}");
    }

    /// <summary>
    /// 在文本覆盖区域需要反显时添加线条指令。
    /// </summary>
    /// <param name="command">接收反显线条指令的打印命令。</param>
    /// <param name="size">计算反显线条高度和长度所用的字号值。</param>
    private void AddInverseLine(CPCLPrintCommand command, int size)
    {
        if (!Reverse)
            return;
        command.Writer.WriteLine($"IL {X} {Y} {X + size / 2 * Text.Length} {Y} {size}");
    }
}
