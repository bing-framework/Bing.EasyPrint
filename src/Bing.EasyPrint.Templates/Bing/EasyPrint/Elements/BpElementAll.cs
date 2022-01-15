namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 显示元素扩展。便于存储元素所有属性信息，只有其中一个不为空
    /// </summary>
    public class BpElementAll
    {
        /// <summary>
        /// 图片显示元素
        /// </summary>
        public BpImageElement ImageElement{ get; set; }

        /// <summary>
        /// 文字显示元素
        /// </summary>
        public BpFontElement FontElement { get; set; }

        /// <summary>
        /// 二维码显示元素
        /// </summary>
        public BpQrCodeElement QrCodeElement{ get; set; }

        /// <summary>
        /// 条码显示元素
        /// </summary>
        public BpBarcodeElement BarcodeElement{ get; set; }
    }
}
