using System;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 模板元素绑定的标题、动态值或业务数据字段。
    /// </summary>
    public class BgField
    {
        /// <summary>
        /// 获取或设置该字段在模板设计器中的说明文字。
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 获取或设置从绑定数据中读取值的字段路径。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置未使用字段路径读取值时所显示的静态文本。
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 获取或设置文本按宽度换行时是否换行，默认值为 <see langword="true" />。
        /// </summary>
        public bool NeedFeedLine { get; set; } = true;

        /// <summary>
        /// 获取或设置文本是否按可用宽度自动适配字号，默认值为 <see langword="false" />。
        /// </summary>
        public bool Fit { get; set; } = false;

        /// <summary>
        /// 获取或设置可持久化的显示格式；为空时完全保留旧绑定输出。
        /// </summary>
        public BgFieldFormat Format { get; set; }

        /// <summary>
        /// 获取或设置可选的 GS1 结构化来源；为空时完全沿用旧字段绑定行为。
        /// </summary>
        /// <remarks>
        /// 非空时不能同时设置 <see cref="Name"/>、<see cref="Value"/> 或
        /// <see cref="ValueFunc"/>，且仅允许用于支持 GS1 数据的元素。
        /// </remarks>
        public BgGs1Input Gs1Input { get; set; }

        /// <summary>
        /// 获取或设置从当前业务数据生成显示文本的函数。
        /// </summary>
        /// <remarks>
        /// 使用此函数后模板不能保存为 JSON。
        /// </remarks>
        public Func<object, string> ValueFunc { get; set; }
    }
}
