using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.EasyPrint.Elements;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 单元格
    /// </summary>
    public class BpCell
    {
        /// <summary>
        /// 宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 边框样式
        /// </summary>
        public BpBorder Border{ get; set; }

        /// <summary>
        /// 单元行列表。列拆行
        /// </summary>
        public List<BpRow> Rows { get; set; }

        /// <summary>
        /// 显示的元素列表
        /// </summary>
        public List<BpElement> Elements { get; set; }

        /// <summary>
        /// 显示的元素，便于存储显示元素全量信息（只用于前端和存储，不参与运算逻辑）
        /// </summary>
        public List<BpElementAll> ElementAlls { get; set; }
    }
}
