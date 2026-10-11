using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 定义模板文字元素使用的字体与排版选项。
    /// </summary>
    public class BpFont
    {
        /// <summary>
        /// 获取或设置文字的字号值。
        /// </summary>
        public int FontSize{ get; set; }

        /// <summary>
        /// 获取或设置字体样式标识。
        /// </summary>
        public int FontStyle { get; set; }

        /// <summary>
        /// 获取或设置打印机字体编号。
        /// </summary>
        public int FontName { get; set; }

        /// <summary>
        /// 获取或设置文字是否加粗。
        /// </summary>
        public bool Bold { get; set; }

        /// <summary>
        /// 获取或设置文字是否反白。
        /// </summary>
        public bool Reverse { get; set; }

        /// <summary>
        /// 获取或设置文字的放大系数。
        /// </summary>
        public int Mag { get; set; }

        /// <summary>
        /// 获取或设置文字超过可用宽度时是否换行。
        /// </summary>
        public bool NeedFeedLine { get; set; }

        /// <summary>
        /// 获取或设置是否将文字缩放至可用宽度。
        /// </summary>
        public bool Fit { get; set; }
    }
}
