// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL;

/// <summary>
/// 表示输出二维码的 CPCL 打印组件。
/// </summary>
// ReSharper disable once InconsistentNaming
internal class QRCodeComponent : ComponentMetadata
{
    /// <summary>
    /// 初始化一个 <see cref="QRCodeComponent"/> 类型的实例。
    /// </summary>
    /// <param name="text">要编码的二维码内容。</param>
    /// <param name="errorLevel">二维码纠错级别标识。</param>
    public QRCodeComponent(string text, string errorLevel)
    {
        CPCLInputValidator.ValidateToken(errorLevel, nameof(errorLevel));
        CPCLInputValidator.ValidateLine(text, nameof(text));
        Text = text;
        ErrorLevel = errorLevel;
    }

    /// <summary>
    /// 获取二维码纠错级别标识。
    /// </summary>
    public string ErrorLevel { get; }

    /// <summary>
    /// 获取或设置二维码的旋转角度。
    /// </summary>
    public int Rotate { get; set; }

    /// <summary>
    /// 获取或设置二维码单个模块的尺寸，单位为打印点。
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// 获取或设置二维码起始位置的横坐标。
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// 获取或设置二维码起始位置的纵坐标。
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 获取要编码的二维码内容。
    /// </summary>
    public string Text { get; }

    /// <inheritdoc />
    public override void Build(CPCLPrintCommand command)
    {
        var coordinate = Helper.GetBarcodeCoordinate(this);
        var cmd = Helper.GetBarcodeRotateCommand(Rotate);
        command.Writer.WriteLine($"{cmd} QR {coordinate.x} {coordinate.y} M 2 U {Size}");
        command.Writer.WriteLine($"{ErrorLevel}A,{Text}");
        command.Writer.WriteLine("ENDQR");
    }
}
