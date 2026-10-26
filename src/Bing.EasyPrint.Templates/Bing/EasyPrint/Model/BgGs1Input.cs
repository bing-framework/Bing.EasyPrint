using System;
using System.Collections.Generic;

namespace Bing.EasyPrint.Model
{
    /// <summary>
    /// 共享字段的可选结构化 GS1 数据来源。
    /// </summary>
    /// <remarks>
    /// 该属性为空时完全沿用旧 <see cref="BgField.Name"/>、<see cref="BgField.Value"/> 和
    /// <see cref="BgField.ValueFunc"/> 行为。
    /// </remarks>
    public sealed class BgGs1Input
    {
        /// <summary>获取或设置结构化来源输出模式。</summary>
        public BgGs1InputMode Mode { get; set; }
        /// <summary>获取或设置 Digital Link 的显式 HTTPS 基址。</summary>
        public string BaseUri { get; set; }
        /// <summary>获取或设置 Digital Link 原样保留的 fragment；null 表示没有。</summary>
        public string Fragment { get; set; }
        /// <summary>获取或设置是否执行显式 req/ex 关联校验。</summary>
        public bool ValidateAssociations { get; set; }
        /// <summary>获取或设置按顺序排列的 AI 项，每项恰选静态值或字段路径。</summary>
        public List<BgGs1InputItem> Items { get; set; } = new List<BgGs1InputItem>();
        /// <summary>获取或设置 Digital Link 中按序保留重复键的非 GS1 查询参数。</summary>
        public List<BgGs1InputQueryParameter> QueryParameters { get; set; } =
            new List<BgGs1InputQueryParameter>();
    }

    /// <summary>
    /// 结构化 GS1 来源的输出模式。
    /// </summary>
    public enum BgGs1InputMode
    {
        /// <summary>生成现有括号 AI 元素串，沿用 GS1 编码路径。</summary>
        Elements = 0,
        /// <summary>生成普通 QR 或 DataMatrix 可承载的 Digital Link URI。</summary>
        DigitalLink = 1
    }

    /// <summary>
    /// Digital Link 项的路径或查询位置。
    /// </summary>
    public enum BgGs1InputPlacement
    {
        /// <summary>按固定字典角色自动选择：限定符进入路径，其他进入 query。</summary>
        Auto = 0,
        /// <summary>强制作为路径限定符；必须属于 01 的 dlpkey 分支。</summary>
        Qualifier = 1,
        /// <summary>强制作为 query 中的 GS1 数据属性。</summary>
        DataAttribute = 2
    }

    /// <summary>
    /// 一个 AI 及其静态值或绑定字段路径。
    /// </summary>
    public sealed class BgGs1InputItem
    {
        /// <summary>获取或设置应用标识符。</summary>
        public string Ai { get; set; }
        /// <summary>获取或设置静态值；与 <see cref="Name"/> 恰选一个，允许空字符串。</summary>
        public string Value { get; set; }
        /// <summary>获取或设置绑定字段路径；与 <see cref="Value"/> 恰选一个。</summary>
        public string Name { get; set; }
        /// <summary>获取或设置可选的显示格式，供绑定值和静态值统一转换。</summary>
        public BgFieldFormat Format { get; set; }
        /// <summary>获取或设置 Digital Link 位置；Elements 模式必须为 Auto。</summary>
        public BgGs1InputPlacement Placement { get; set; }
    }

    /// <summary>
    /// Digital Link 中保留顺序和重复键的非 GS1 查询参数。
    /// </summary>
    public sealed class BgGs1InputQueryParameter
    {
        /// <summary>获取或设置非数字参数名。</summary>
        public string Key { get; set; }
        /// <summary>获取或设置参数值。</summary>
        public string Value { get; set; }
    }
}
