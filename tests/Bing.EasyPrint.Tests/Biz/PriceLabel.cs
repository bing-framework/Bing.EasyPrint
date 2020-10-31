namespace Bing.EasyPrint.Tests.Biz
{
    /// <summary>
    /// 价格标签
    /// </summary>
    public class PriceLabel
    {
        /// <summary>
        /// 商品条码
        /// </summary>
        public string Barcode { get; set; }

        /// <summary>
        /// 二维码
        /// </summary>
        public string QrCode { get; set; }

        /// <summary>
        /// 商家自定义商品编码
        /// </summary>
        public string CustomProductCode { get; set; }

        /// <summary>
        /// 商家自定义sku编码
        /// </summary>
        public string CustomSkuCode { get; set; }

        /// <summary>
        /// 商品名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// SKU原价
        /// </summary>
        public decimal OriginPrice { get; set; }

        /// <summary>
        /// SKU价格
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 商品编码
        /// </summary>
        public string ProductCode { get; set; }

        /// <summary>
        /// sku编码
        /// </summary>
        public string SkuCode { get; set; }

        /// <summary>
        /// 店铺编码
        /// </summary>
        public string StoreCode { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 辅助属性
        /// </summary>
        public string AttributeName { get; set; }
    }
}
