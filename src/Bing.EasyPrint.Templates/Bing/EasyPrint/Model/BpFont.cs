using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 字体
    /// </summary>
    public class BpFont
    {
        /// <summary>
        /// 字体大小
        /// </summary>
        public int FontSize{ get; set; }

        /// <summary>
        /// 字体风格
        /// </summary>
        public int FontStyle { get; set; }

        /// <summary>
        /// 字体名称
        /// </summary>
        public int FontName { get; set; }

        /// <summary>
        /// 字体加粗
        /// </summary>
        public bool Bold { get; set; }

        /// <summary>
        /// 字体放大
        /// </summary>
        public int Mag { get; set; }

        /// <summary>
        /// 是否自动换行
        /// </summary>
        public bool NeedFeedLine { get; set; }

        /// <summary>
        /// 是否自适应宽度
        /// </summary>
        public bool Fit { get; set; }
    }
}
