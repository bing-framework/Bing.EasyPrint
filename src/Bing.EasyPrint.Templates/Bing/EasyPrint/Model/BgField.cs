using System;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 定义模板元素绑定的标题、静态值或业务数据字段。
    /// </summary>
    public class BgField
    {
        /// <summary>
        /// 获取或设置字段在模板中使用的说明文字。
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 获取或设置从绑定数据中读取值的字段路径。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置未使用字段路径或取值函数时显示的静态文本。
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 获取或设置文本超出可用宽度时是否换行，默认值为 <see langword="true" />。
        /// </summary>
        public bool NeedFeedLine { get; set; } = true;

        /// <summary>
        /// 获取或设置是否按可用宽度缩放文本，默认值为 <see langword="false" />。
        /// </summary>
        public bool Fit { get; set; } = false;

        /// <summary>
        /// 获取或设置从当前业务对象计算显示文本的函数。
        /// </summary>
        /// <remarks>
        /// 使用此函数的内存模板不能保存为 JSON。
        /// </remarks>
        public Func<object, string> ValueFunc { get; set; }
    }
}
