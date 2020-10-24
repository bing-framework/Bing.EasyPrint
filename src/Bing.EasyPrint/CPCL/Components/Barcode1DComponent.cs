
// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 一维条码组件
    /// </summary>
    internal class Barcode1DComponent : ComponentMetadata
    {
        /// <summary>
        /// 高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 窄条的单位宽度
        /// </summary>
        public int LineWidth { get; set; }

        /// <summary>
        /// 宽条与窄条的比率
        /// </summary>
        public int Ratio { get; set; } = 1;

        /// <summary>
        /// 旋转角度
        /// </summary>
        public int Rotate { get; set; }

        /// <summary>
        /// 条码内容
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 条码类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 条码起始x坐标
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 条码起始y坐标
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="command">打印命令</param>
        public override void Build(CPCLPrintCommand command)
        {
            var coordinate = Helper.GetBarcodeCoordinate(this);
            var cmd = Helper.GetBarcodeRotateCommand(Rotate);
            command.Writer.WriteLine($"{cmd} {Type} {LineWidth} {Ratio} {Height} {coordinate.x} {coordinate.y} {Text}");
        }
    }
}
