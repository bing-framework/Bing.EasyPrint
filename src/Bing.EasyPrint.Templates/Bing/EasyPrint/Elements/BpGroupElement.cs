using System.Collections.Generic;
using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 定义可整体布局或跨页布局的元素组合。
    /// </summary>
    /// <remarks>
    /// 组合可以包含内联元素、行列内容或通过组件标识引用模板定义；内容来源不能同时使用。
    /// </remarks>
    public sealed class BpGroupElement : BpElement
    {
        /// <summary>
        /// 初始化一个 <see cref="BpGroupElement"/> 类型的实例。
        /// </summary>
        public BpGroupElement() { Type = 6; }

        /// <summary>
        /// 获取或设置此组合引用的模板组件标识。
        /// </summary>
        public string ComponentId { get; set; }

        /// <summary>
        /// 获取或设置组合布局宽度。
        /// </summary>
        /// <remarks>
        /// 单位为打印点；零表示使用所引用组件的宽度，未引用组件时使用可用宽度。
        /// </remarks>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置组合布局高度。
        /// </summary>
        /// <remarks>
        /// 单位为打印点；零表示使用所引用组件的高度，未引用组件时按子元素内容计算。
        /// </remarks>
        public int Height { get; set; }

        /// <summary>
        /// 获取或设置相对于组合原点布局的内联子元素。
        /// </summary>
        public List<BpElement> Elements { get; set; }

        /// <summary>
        /// 获取或设置相对于组合原点布局的行列内容。
        /// </summary>
        public List<BpRow> Rows { get; set; }

        /// <summary>
        /// 获取或设置相对于外层上下文的数据路径。
        /// </summary>
        public string DataPath { get; set; }

        /// <summary>
        /// 获取或设置组件实例的参数绑定。
        /// </summary>
        public Dictionary<string, BpComponentParameterBinding> Parameters { get; set; }

        /// <summary>
        /// 获取或设置组合的分页策略。
        /// </summary>
        public BpGroupPagination? Pagination { get; set; }
    }
}
