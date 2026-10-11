using System.Collections.Generic;
using Bing.EasyPrint.Elements;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 定义可在模板中按标识复用的元素组合。
    /// </summary>
    /// <remarks>
    /// 组件内元素坐标相对于组件原点，尺寸和坐标均使用打印点。
    /// </remarks>
    public sealed class BpComponentDefinition
    {
        /// <summary>
        /// 获取或设置模板内用于引用此组件的唯一标识。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置组件的布局宽度，单位为打印点。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置组件的布局高度，单位为打印点。
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 获取或设置相对于组件原点布局的子元素。
        /// </summary>
        public List<BpElement> Elements { get; set; }

        /// <summary>
        /// 获取或设置相对于组件原点布局的行列内容。
        /// </summary>
        public List<BpRow> Rows { get; set; }

        /// <summary>
        /// 获取或设置组件的参数定义。
        /// </summary>
        public Dictionary<string, BpComponentParameterDefinition> Parameters { get; set; }
    }
}
