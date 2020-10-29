using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.EasyPrint.Tests.Biz
{
    public class LomWaybillLabel
    {
        /// <summary>
        /// 运单号
        /// </summary>
        public string Sheet { get; set; }

        /// <summary>
        /// 运单号
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
    }
}
