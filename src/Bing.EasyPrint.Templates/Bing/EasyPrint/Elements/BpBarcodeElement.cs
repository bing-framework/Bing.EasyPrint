namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 用于输出一维条码的模板元素。
    /// </summary>
    public class BpBarcodeElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpBarcodeElement"/> 类型的实例。
        /// </summary>
        public BpBarcodeElement() { Type = 4; }

        /// <summary>
        /// 获取或设置条码制式，默认值为 <see cref="BarcodeType.Code128" />。
        /// </summary>
        public BarcodeType BarcodeType { get; set; } = BarcodeType.Code128;

        /// <summary>
        /// 获取或设置 CPCL 窄条宽度参数，默认值为 2。
        /// </summary>
        /// <remarks>
        /// Link-OS 设备的实际打印点宽为该参数值加一。
        /// </remarks>
        public int LineWidth { get; set; } = 2;

        /// <summary>
        /// 获取或设置 CPCL 条码宽窄比参数，默认值为 2。
        /// </summary>
        /// <remarks>
        /// 0 至 4 表示 1.5 至 3.5 的比值，20 至 30 表示 2.0 至 3.0 的比值；用于 POSTNET 时表示条间距参数。
        /// </remarks>
        public int Ratio { get; set; } = 2;
        /// <summary>
        /// 获取或设置条码预留宽度，单位为打印点；零表示按条码内容和制式计算。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置条码高度，单位为打印点。
        /// </summary>
        public int Height { get; set; }
    }
}
