namespace Bing.EasyPrint.Tests.Biz.Label
{
    /// <summary>
    /// 打印价格标签基类
    /// </summary>
    public abstract class PrintPriceLabelBase
    {
        /// <summary>
        /// 简单打印
        /// </summary>
        protected IEasyPrint EasyPrint { get; set; }

        /// <summary>
        /// 初始化一个<see cref="PrintPriceLabelBase"/>类型的实例
        /// </summary>
        protected PrintPriceLabelBase(IEasyPrint easyPrint) => EasyPrint = easyPrint;

        /// <summary>
        /// 构建
        /// </summary>
        /// <param name="label">价格标签</param>
        public abstract IBufferWriter Build(PriceLabel label);

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="easyPrint">简单打印</param>
        /// <param name="pageType">打印纸类型</param>
        public static PrintPriceLabelBase Create(IEasyPrint easyPrint, PrintPageType pageType)
        {
            switch (pageType)
            {
                case PrintPageType.Page90mm:
                    return new PrintPriceLabelBy90mm(easyPrint);
                case PrintPageType.Page70mm:
                    return new PrintPriceLabelBy70mm(easyPrint);
                case PrintPageType.Page15mm:
                    return new PrintPriceLabelBy15mm(easyPrint);
                default:
                    return new PrintPriceLabelBy70mm(easyPrint);
            }
        }
    }
}
