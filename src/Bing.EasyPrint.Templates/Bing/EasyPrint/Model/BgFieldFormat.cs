using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 字段格式类型。
    /// </summary>
    public enum BgFieldFormatKind
    {
        /// <summary>文本；只执行空值、补齐和前后缀处理。</summary>
        Text = 0,

        /// <summary>十进制数字；使用显式 .NET 数字格式字符串。</summary>
        Number = 1,

        /// <summary>货币；使用显式 .NET 货币格式字符串和可选文化。</summary>
        Currency = 2,

        /// <summary>日期或日期时间；保留输入中的时区/offset 语义。</summary>
        Date = 3,

        /// <summary>枚举映射；按原始值查找显式映射表。</summary>
        Enum = 4
    }

    /// <summary>
    /// 描述 <see cref="BgField"/> 的可持久化显示格式；配置只包含数据，不执行脚本或表达式。
    /// </summary>
    /// <remarks>
    /// 固定处理顺序为：取值、实际 null 处理、类型转换/枚举映射、格式化、补齐、前后缀。
    /// 未配置格式时完全沿用旧绑定输出。默认文化为 InvariantCulture，日期不会使用当前时间或隐式本地时区转换。
    /// </remarks>
    public sealed class BgFieldFormat
    {
        /// <summary>获取或设置格式类型。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public BgFieldFormatKind Kind { get; set; }

        /// <summary>
        /// 获取或设置 .NET 数字、货币或日期输出格式字符串。
        /// </summary>
        /// <remarks>Number、Currency、Date 类型必须显式提供；Text 和 Enum 不使用该字段。</remarks>
        public string Pattern { get; set; }

        /// <summary>
        /// 获取或设置日期输入格式；为空时只接受明确的 ISO 日期或带 offset 的 ISO 日期时间。
        /// </summary>
        public string InputFormat { get; set; }

        /// <summary>
        /// 获取或设置显式文化名称；为空时使用 InvariantCulture。
        /// </summary>
        public string Culture { get; set; }

        /// <summary>获取或设置实际 null 使用的替代文本；空字符串不等同于 null。</summary>
        public string NullText { get; set; }

        /// <summary>获取或设置补齐后的最小字符数；为空时不补齐。</summary>
        public int? PadLength { get; set; }

        /// <summary>获取或设置补齐字符；必须为单个字符，默认零。</summary>
        public string PadCharacter { get; set; } = "0";

        /// <summary>获取或设置格式化并补齐后的前缀。</summary>
        public string Prefix { get; set; }

        /// <summary>获取或设置格式化并补齐后的后缀。</summary>
        public string Suffix { get; set; }

        /// <summary>
        /// 获取或设置枚举原始值到显示文本的映射；Enum 类型必须提供。
        /// </summary>
        public Dictionary<string, string> Map { get; set; }
    }
}
