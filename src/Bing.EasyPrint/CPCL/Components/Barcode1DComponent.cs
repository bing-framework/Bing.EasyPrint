// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 表示可输出一维条码的 CPCL 打印组件。
/// </summary>
internal class Barcode1DComponent : ComponentMetadata
{
    /// <summary>
    /// 初始化一个 <see cref="Barcode1DComponent"/> 类型的实例。
    /// </summary>
    /// <param name="type">CPCL 条码制式标识。</param>
    /// <param name="text">要编码的条码内容。</param>
    public Barcode1DComponent(string type, string text)
    {
        CPCLInputValidator.ValidateToken(type, nameof(type));
        CPCLInputValidator.ValidateLine(text, nameof(text));
        Type = type;
        Text = text;
    }

    /// <summary>
    /// 获取或设置条码高度，单位为打印点。
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 获取或设置条码窄条的单位宽度。
    /// </summary>
    public int LineWidth { get; set; }

    /// <summary>
    /// 获取或设置宽条与窄条的宽度比，默认值为 1。
    /// </summary>
    public int Ratio { get; set; } = 1;

    /// <summary>
    /// 获取或设置条码的旋转角度。
    /// </summary>
    public int Rotate { get; set; }

    /// <summary>
    /// 获取要编码的条码内容。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 获取 CPCL 条码制式标识。
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// 获取或设置条码起始位置的横坐标。
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// 获取或设置条码起始位置的纵坐标。
    /// </summary>
    public int Y { get; set; }

    /// <inheritdoc />
    public override void Build(CPCLPrintCommand command)
    {
        var coordinate = Helper.GetBarcodeCoordinate(this);
        var cmd = Helper.GetBarcodeRotateCommand(Rotate);
        command.Writer.WriteLine($"{cmd} {Type} {LineWidth} {Ratio} {Height} {coordinate.x} {coordinate.y} {Text}");
    }
}
