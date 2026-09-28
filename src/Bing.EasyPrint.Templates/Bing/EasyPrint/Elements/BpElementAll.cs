namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 保存旧模板格式中按元素种类拆分的完整元素数据。
    /// </summary>
    /// <remarks>
    /// 仅用于旧格式的加载转换；一项通常只设置与其元素种类对应的属性，渲染时使用转换后的 <see cref="BpElement" />。
    /// </remarks>
    public class BpElementAll
    {
        /// <summary>
        /// 获取或设置旧格式中的图片元素数据。
        /// </summary>
        public BpImageElement ImageElement{ get; set; }

        /// <summary>
        /// 获取或设置旧格式中的文字元素数据。
        /// </summary>
        public BpFontElement FontElement { get; set; }

        /// <summary>
        /// 获取或设置旧格式中的二维码元素数据。
        /// </summary>
        public BpQrCodeElement QrCodeElement{ get; set; }

        /// <summary>
        /// 获取或设置旧格式中的一维条码元素数据。
        /// </summary>
        public BpBarcodeElement BarcodeElement{ get; set; }
    }
}
