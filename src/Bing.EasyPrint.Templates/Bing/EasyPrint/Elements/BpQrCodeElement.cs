namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 用于输出 CPCL 二维码的模板元素。
    /// </summary>
    public class BpQrCodeElement : BpElement
    {
        /// <summary>
    /// 初始化一个 <see cref="BpQrCodeElement"/> 类型的实例。
        /// </summary>
        public BpQrCodeElement() { Type = 3; }

        /// <summary>
        /// 获取或设置二维码单个模块的宽度，单位为打印点，默认值为 4。
        /// </summary>
        /// <remarks>
        /// 有效值为 1 至 32；实际符号尺寸由编码内容、纠错等级和模块宽度共同决定。
        /// </remarks>
        public int UnitWidth { get; set; } = 4;

        /// <summary>
        /// 获取或设置二维码纠错级别，默认值为 <c>M</c>。
        /// </summary>
        /// <remarks>
        /// 支持 <c>L</c>、<c>M</c>、<c>Q</c> 和 <c>H</c>。
        /// </remarks>
        public string ErrorLevel { get; set; } = "M";
        /// <summary>
        /// 获取或设置二维码预留区域宽度，单位为打印点；零表示按编码结果计算。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置二维码预留区域高度，单位为打印点；零表示按编码结果计算。
        /// </summary>
        public int Height { get; set; }
    }
}
