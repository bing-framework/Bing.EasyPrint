using System.Collections.Generic;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 单元行
    /// </summary>
    public class BpRow
    {
        /// <summary>
        /// 高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 边框类型
        /// </summary>
        public BpBorder Border { get; set; }

        /// <summary>
        /// 单元格列表
        /// </summary>
        public List<BpCell> Cells { get; set; }
    }
}
