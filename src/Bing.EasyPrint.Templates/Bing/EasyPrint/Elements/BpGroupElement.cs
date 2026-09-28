using System.Collections.Generic;

namespace Bing.EasyPrint.Elements
{
    /// <summary>
    /// 定义作为单一分页单位布局的元素组合。
    /// </summary>
    /// <remarks>
    /// 组合可以内联包含子元素，也可以通过组件标识引用模板定义；两种方式不能同时使用。
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
        /// 获取或设置组合布局宽度，单位为打印点；零表示使用所引用组件的宽度，未引用组件时使用可用宽度。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置组合布局高度，单位为打印点；零表示使用所引用组件的高度，未引用组件时按子元素内容计算。
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 获取或设置相对于组合原点布局的内联子元素。
        /// </summary>
        public List<BpElement> Elements { get; set; }
    }
}
