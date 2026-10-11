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
        /// <summary>获取或设置非 CPCL 协议的窄条模块宽度，单位为打印点；未指定时沿用旧参数映射。</summary>
        public int? NarrowModuleWidth { get; set; }
        /// <summary>获取或设置 TSPL 的宽条模块宽度，单位为打印点；其他协议不支持显式指定。</summary>
        public int? WideModuleWidth { get; set; }
        /// <summary>
        /// 获取或设置条码预留宽度，单位为打印点；零表示按条码内容和制式计算。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置条码高度，单位为打印点。
        /// </summary>
        public int Height { get; set; }
        /// <summary>获取或设置顺时针旋转角度；宽高表示旋转前占位，坐标表示旋转后左上角。</summary>
        public RotationAngle Rotation { get; set; }
        /// <summary>是否输出条码下方的可读文字；未指定时沿用目标协议默认行为。</summary>
        public bool? HumanReadable { get; set; }
        /// <summary>为设备条码可读文字额外预留的高度，单位为打印点；零表示不额外预留。</summary>
        public int HumanReadableReserveHeight { get; set; }
        /// <summary>获取或设置旋转前编码区四周的静区，单位为打印点；null 保留旧制式留白，显式零值不留白。</summary>
        /// <remarks>Width 包含左右静区；Height 仍为条高，布局高度另加上下静区及可读文字预留高度。</remarks>
        public PrintQuietZone QuietZone { get; set; }
    }
}
