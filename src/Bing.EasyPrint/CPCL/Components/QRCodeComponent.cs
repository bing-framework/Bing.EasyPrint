
// ReSharper disable once CheckNamespace
namespace Bing.EasyPrint.CPCL
{
    /// <summary>
    /// 二维码组件
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal class QRCodeComponent : ComponentMetadata
    {
        /// <summary>
        /// 二维码纠错级别
        /// </summary>
        public string ErrorLevel { get; set; }

        /// <summary>
        /// 旋转角度
        /// </summary>
        public int Rotate { get; set; }

        /// <summary>
        /// 尺寸
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// 二维码起始x坐标
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 二维码起始y坐标
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 二维码内容
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="command">打印命令</param>
        public override void Build(CPCLPrintCommand command)
        {
            var coordinate = Helper.GetBarcodeCoordinate(this);
            switch (Rotate)
            {
                case 0:
                case 180:
                    command.QRCode(coordinate.x, coordinate.y, 2, Size, ErrorLevel, null, Text);
                    break;
                case 90:
                case 270:
                    command.VQRCode(coordinate.x, coordinate.y, 2, Size, ErrorLevel, null, Text);
                    break;
            }
        }
    }
}
