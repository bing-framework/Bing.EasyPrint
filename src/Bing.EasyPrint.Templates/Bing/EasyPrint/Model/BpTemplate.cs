using System.Collections.Generic;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 模板
    /// </summary>
    public class BpTemplate
    {
        /// <summary>
        /// 纸张类型
        /// </summary>
        /// <remarks>1=A4</remarks>
        public int PaperType { get; set; }

        /// <summary>
        /// 宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 高度
        /// </summary>
        public int Height{ get; set; }

        /// <summary>
        /// 背景颜色
        /// </summary>
        /// <remarks>1=白色；2=黑色；3=黄色</remarks>
        public int Color{ get; set; }

        /// <summary>
        /// 边框颜色
        /// </summary>
        public BpBorder Border { get; set; }

        /// <summary>
        /// 单元行列表
        /// </summary>
        public List<BpRow> Rows { get; set; }
    }
}
