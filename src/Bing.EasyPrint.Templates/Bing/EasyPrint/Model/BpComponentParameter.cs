using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 指定组合的分页方式。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BpGroupPagination
    {
        /// <summary>
        /// 将整个组合放在同一页。
        /// </summary>
        KeepTogether,
        /// <summary>
        /// 允许组合内容跨页。
        /// </summary>
        Split
    }

    /// <summary>
    /// 定义可复用组件的参数。
    /// </summary>
    public sealed class BpComponentParameterDefinition
    {
        /// <summary>
        /// 获取或设置参数是否必须由实例提供。
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// 获取或设置参数默认值。
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public JToken DefaultValue
        {
            get => defaultValue;
            set => defaultValue = value ?? JValue.CreateNull();
        }

        /// <summary>
        /// 保存显式设置的参数默认值。
        /// </summary>
        private JToken defaultValue;

        /// <summary>
        /// 判断是否序列化参数默认值。
        /// </summary>
        /// <returns>已显式设置默认值时返回 true，否则返回 false。</returns>
        public bool ShouldSerializeDefaultValue() => defaultValue != null;
    }

    /// <summary>
    /// 指定组件实例的参数来源。
    /// </summary>
    public sealed class BpComponentParameterBinding
    {
        /// <summary>
        /// 获取或设置外层数据的字段路径。
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 获取或设置静态参数值。
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public JToken Value
        {
            get => value;
            set => this.value = value ?? JValue.CreateNull();
        }

        /// <summary>
        /// 保存显式设置的静态参数值。
        /// </summary>
        private JToken value;

        /// <summary>
        /// 判断是否序列化静态参数值。
        /// </summary>
        /// <returns>已显式设置静态值时返回 true，否则返回 false。</returns>
        public bool ShouldSerializeValue() => value != null;
    }
}
