using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.EasyPrint.Tests.Biz
{
    /// <summary>
    /// 配送面单
    /// </summary>
    public class ShippingLabel
    {
        /// <summary>
        /// 单号
        /// </summary>
        public string Sheet { get; set; }

        /// <summary>
        /// 订单号
        /// </summary>
        public string SourceSheet { get; set; }

        /// <summary>
        /// 配送中心名称
        /// </summary>
        public string ShippingPointName { get; set; }

        /// <summary>
        /// 配送中心划区名称
        /// </summary>
        public string ShippingZoningName { get; set; }

        /// <summary>
        /// 配送起时间
        /// </summary>
        public DateTime DeliveryTimeBegin { get; set; }

        /// <summary>
        /// 配送止时间
        /// </summary>
        public DateTime DeliveryTimeEnd { get; set; }

        /// <summary>
        /// 收货人
        /// </summary>
        public string ConsigneeName { get; set; }

        /// <summary>
        /// 收货人电话
        /// </summary>
        public string ConsigneePhone { get; set; }

        /// <summary>
        /// 收货人地址
        /// </summary>
        public string ConsigneeStreet { get; set; }

        /// <summary>
        /// 包裹数
        /// </summary>
        public int BagQty { get; set; }

        /// <summary>
        /// 商品明细
        /// </summary>
        public List<Cargo> CargoList { get; set; }
    }


    /// <summary>
    /// 商品明细
    /// </summary>
    public class Cargo
    {
        /// <summary>
        /// 商品编码
        /// </summary>
        public string CargoCode { get; set; }
        /// <summary>
        /// 商品名称
        /// </summary>
        public string CargoName { get; set; }

        /// <summary>
        /// 商品单价
        /// </summary>
        public decimal CargoPrice { get; set; }

        /// <summary>
        /// 商品数量
        /// </summary>
        public int CargoQty { get; set; }
    }
}
