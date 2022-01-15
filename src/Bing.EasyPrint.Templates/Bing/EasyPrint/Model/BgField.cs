using System;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 字段
    /// </summary>
    public class BgField
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 字段属性名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 属性值
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 是否自动换行
        /// </summary>
        public bool NeedFeedLine { get; set; } = true;

        /// <summary>
        /// 是否自适应宽度
        /// </summary>
        public bool Fit { get; set; } = false;

        /// <summary>
        /// 属性取值函数
        /// </summary>
        public Func<object, string> ValueFunc { get; set; }
    }
}
