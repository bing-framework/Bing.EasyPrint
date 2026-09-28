using System.Collections.Generic;

namespace Bing.EasyPrint.Model;

/// <summary>
/// 定义用于控制模板行或元素显示的声明式条件。
/// </summary>
/// <remarks>
/// 条件按业务数据绑定求值；比较值为 JSON 标量且不会进行字符串与数字之间的隐式转换。
/// </remarks>
public sealed class BpCondition
{
    /// <summary>
    /// 获取或设置待比较字段的路径。
    /// </summary>
    /// <remarks>
    /// 路径可使用 <c>$root</c>、<c>$item</c> 或 <c>$index</c> 引用根数据、当前重复项或其序号。
    /// </remarks>
    public string Path { get; set; }
    /// <summary>
    /// 获取或设置条件运算符，默认值为 <c>eq</c>。
    /// </summary>
    /// <remarks>
    /// 支持 <c>eq</c>、<c>ne</c>、<c>gt</c>、<c>ge</c>、<c>lt</c>、<c>le</c>、<c>isNull</c>、<c>notNull</c>、<c>and</c> 和 <c>or</c>。
    /// </remarks>
    public string Operator { get; set; } = "eq";
    /// <summary>
    /// 获取或设置比较运算使用的标量值。
    /// </summary>
    /// <remarks>
    /// 比较时保留值的原始类型，不执行字符串到数字的隐式转换。
    /// </remarks>
    public object Value { get; set; }
    /// <summary>
    /// 获取或设置 <c>and</c> 或 <c>or</c> 运算使用的子条件。
    /// </summary>
    public List<BpCondition> Conditions { get; set; }
}
