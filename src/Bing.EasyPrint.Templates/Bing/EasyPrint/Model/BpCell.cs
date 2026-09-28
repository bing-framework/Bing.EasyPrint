using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.EasyPrint.Elements;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 定义模板行中的一个列布局单元。
    /// </summary>
    public class BpCell
    {
        /// <summary>
        /// 获取或设置单元格宽度，单位为打印点。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置单元格的边框和填充样式。
        /// </summary>
        public BpBorder Border{ get; set; }

        /// <summary>
        /// 获取或设置单元格内按顺序布局的嵌套行。
        /// </summary>
        public List<BpRow> Rows { get; set; }

        /// <summary>
        /// 获取或设置单元格内参与模板布局和渲染的元素。
        /// </summary>
        public List<BpElement> Elements { get; set; }

        /// <summary>
        /// 获取或设置旧格式的完整元素数据，仅供加载阶段转换。
        /// </summary>
        /// <remarks>
        /// 渲染器以 <see cref="Elements" /> 为准；JSON 加载器会将此属性转换后清除。
        /// </remarks>
        public List<BpElementAll> ElementAlls { get; set; }
    }
}
