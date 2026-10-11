using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Bing.EasyPrint.Model;
using Newtonsoft.Json.Linq;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 在根数据和当前明细项之间解析模板字段与条件。
/// </summary>
internal sealed class Binding
{
    /// <summary>
    /// 保存本次渲染的根数据。
    /// </summary>
    internal object Root;
    /// <summary>
    /// 保存当前重复行对应的数据项。
    /// </summary>
    internal object Item;
    /// <summary>
    /// 保存当前重复项的零基序号。
    /// </summary>
    /// <remarks>
    /// 非重复上下文使用 -1。
    /// </remarks>
    internal int Index;
    /// <summary>
    /// 保存嵌套重复行从外到内的零基序号。
    /// </summary>
    internal IReadOnlyList<int> InstanceIndices;
    /// <summary>
    /// 保存仅供本次渲染复用的公开属性查找缓存。
    /// </summary>
    internal readonly Bing.EasyPrint.Core.BudgetedDictionary<(Type, string), PropertyInfo> Properties;
    private readonly Bing.EasyPrint.Core.PrintStorageBudget storage;
    /// <summary>
    /// 保存变量名与根数据字段路径的映射。
    /// </summary>
    internal readonly IReadOnlyDictionary<string, string> Variables;
    /// <summary>
    /// 保存当前组件实例的参数值。
    /// </summary>
    internal readonly IReadOnlyDictionary<string, object> Parameters;
    /// <summary>
    /// 初始化一个 <see cref="Binding"/> 类型的实例。
    /// </summary>
    /// <param name="root">根业务数据。</param>
    /// <param name="variables">可选的模板变量定义。</param>
    /// <param name="storage">临时模型估计的归属；为空时保留到调用作用域结束。</param>
    internal Binding(object root, IReadOnlyDictionary<string, string> variables = null,
        Bing.EasyPrint.Core.PrintStorageBudget storage = null)
    {
        this.storage = storage;
        Bing.EasyPrint.Core.PrintStorageBudget.Reserve(64, "Template.Binding", storage);
        Root = Item = root; Index = -1; InstanceIndices = Array.AsReadOnly(Array.Empty<int>());
        Properties = new Bing.EasyPrint.Core.BudgetedDictionary<(Type, string), PropertyInfo>(
            "Template.PropertyIndex", storage,
            retainedSize: (key, _) => Bing.EasyPrint.Core.PrintStorageBudget.TextBytes(key.Item2));
        Variables = variables;
    }
    /// <summary>
    /// 为重复明细创建共享属性缓存的子绑定上下文。
    /// </summary>
    /// <param name="item">当前明细数据项。</param>
    /// <param name="index">当前明细的零基序号。</param>
    /// <returns>指向当前明细项的绑定上下文。</returns>
    internal Binding Child(object item, int index)
    {
        Bing.EasyPrint.Core.PrintStorageBudget.Reserve(checked(32L + (InstanceIndices.Count + 1L) * 4),
            "Template.BindingIndices", storage);
        var indices = new int[InstanceIndices.Count + 1];
        for (var current = 0; current < InstanceIndices.Count; current++) indices[current] = InstanceIndices[current];
        indices[indices.Length - 1] = index;
        return new Binding(Root, item, index, Array.AsReadOnly(indices), Properties, Variables, Parameters, storage);
    }
    /// <summary>
    /// 初始化一个 <see cref="Binding"/> 类型的实例。
    /// </summary>
    /// <param name="root">根业务数据。</param>
    /// <param name="item">当前明细数据项。</param>
    /// <param name="index">当前明细的零基序号。</param>
    /// <param name="instanceIndices">各层重复明细的序号。</param>
    /// <param name="properties">本次渲染共享的属性缓存。</param>
    /// <param name="variables">可选的模板变量定义。</param>
    /// <param name="parameters">当前组件实例的参数值。</param>
    /// <param name="storage">与父绑定共享的临时存储归属。</param>
    private Binding(object root, object item, int index, IReadOnlyList<int> instanceIndices,
        Bing.EasyPrint.Core.BudgetedDictionary<(Type, string), PropertyInfo> properties,
        IReadOnlyDictionary<string, string> variables, IReadOnlyDictionary<string, object> parameters = null,
        Bing.EasyPrint.Core.PrintStorageBudget storage = null)
    {
        this.storage = storage;
        Bing.EasyPrint.Core.PrintStorageBudget.Reserve(64, "Template.Binding", storage);
        Root = root; Item = item; Index = index; InstanceIndices = instanceIndices; Properties = properties; Variables = variables;
        Parameters = parameters;
    }

    /// <summary>
    /// 创建组件实例的数据及参数绑定上下文。
    /// </summary>
    /// <param name="item">组件实例使用的当前数据项。</param>
    /// <param name="parameters">组件实例解析后的参数值。</param>
    /// <returns>包含指定数据及参数上下文的绑定。</returns>
    internal Binding Component(object item, IReadOnlyDictionary<string, object> parameters)
        => new Binding(Root, item, Index, InstanceIndices, Properties, Variables, parameters, storage);

    /// <summary>
    /// 解析字段路径、变量或重复项内置引用。
    /// </summary>
    /// <param name="path">字段路径或以美元符号开头的内置引用。</param>
    /// <returns>字段值；存在但值为空时返回 null。</returns>
    internal object Resolve(string path)
    {
        try { return ResolveCore(path); }
        catch (ArgumentException error)
        {
            TemplateBindingDiagnostic.Attach(error, path);
            throw;
        }
    }

    private object ResolveCore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("字段路径不能为空。");
        using var pathStorage = Bing.EasyPrint.Printing.PrintResourceScope.Reserve(
            checked(64L + path.Length * 8L), "Template.BindingPathScratch");
        if (path.StartsWith("$var.", StringComparison.Ordinal))
        {
            var name = path.Substring(5);
            if (name.IndexOf('.') >= 0 || Variables == null || !Variables.TryGetValue(name, out var source))
                throw Missing(path);
            return ResolveRoot(source);
        }
        if (path == "$index") return Index;
        object value = Item;
        if (path.StartsWith("$param.", StringComparison.Ordinal))
        {
            var separator = path.IndexOf('.', 7);
            var name = separator < 0 ? path.Substring(7) : path.Substring(7, separator - 7);
            if (name.Length == 0 || Parameters == null || !Parameters.TryGetValue(name, out value)) throw Missing(path);
            if (separator < 0) return value;
            path = path.Substring(separator + 1);
        }
        else
        {
        if (path == "$root") return Root;
        if (path == "$item") return Item;
        if (path.StartsWith("$root.", StringComparison.Ordinal)) { value = Root; path = path.Substring(6); }
        else if (path.StartsWith("$item.", StringComparison.Ordinal)) path = path.Substring(6);
        }
        foreach (var name in path.Split('.'))
        {
            if (name.Length == 0) throw new ArgumentException("字段路径包含空段。");
            if (value == null) return null;
            if (value is JObject obj)
            {
                if (!obj.TryGetValue(name, StringComparison.Ordinal, out var token)) throw Missing(path);
                value = token;
            }
            else if (value is IDictionary<string, object> generic)
            {
                if (!generic.TryGetValue(name, out value)) throw Missing(path);
            }
            else if (value is IReadOnlyDictionary<string, object> readOnly)
            {
                if (!readOnly.TryGetValue(name, out value)) throw Missing(path);
            }
            else if (value is IDictionary dictionary)
            {
                if (!dictionary.Contains(name)) throw Missing(path);
                value = dictionary[name];
            }
            else
            {
                var key = (value.GetType(), name);
                if (!Properties.TryGetValue(key, out var property))
                {
                    property = key.Item1.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                    if (property == null || property.GetIndexParameters().Length != 0 || property.GetGetMethod() == null) throw Missing(path);
                    Properties.Add(key, property);
                }
                value = property.GetValue(value);
            }
            if (value is JValue scalar) value = scalar.Value;
        }
        return value;
    }
    /// <summary>
    /// 从根数据解析变量所引用的字段路径。
    /// </summary>
    /// <param name="path">不含内置引用的根数据字段路径。</param>
    /// <returns>根数据字段值；值为空时返回 null。</returns>
    private object ResolveRoot(string path)
    {
        if (path.StartsWith("$", StringComparison.Ordinal)) throw new ArgumentException("变量定义必须引用根数据字段。");
        var rootBinding = new Binding(Root, Root, -1, InstanceIndices, Properties, null, storage: storage);
        return rootBinding.Resolve(path);
    }
    /// <summary>
    /// 创建包含缺失字段路径的错误。
    /// </summary>
    /// <param name="path">未找到的字段路径。</param>
    /// <returns>字段缺失异常。</returns>
    private static ArgumentException Missing(string path) => new ArgumentException("字段不存在：" + path);
    /// <summary>
    /// 将模板字段取值转换为显示文本。
    /// </summary>
    /// <param name="field">字段定义；为空时显示空文本。</param>
    /// <returns>使用不随区域设置变化的格式表示的文本。</returns>
    internal string Text(BgField field)
    {
        try { return TextCore(field); }
        catch (ArgumentException error)
        {
            TemplateBindingDiagnostic.Attach(error, field?.Name ?? "Field.Value");
            throw;
        }
    }

    private string TextCore(BgField field)
    {
        if (field == null) return string.Empty;
        var value = field.ValueFunc != null ? field.ValueFunc(Item) : !string.IsNullOrEmpty(field.Name) ? Resolve(field.Name) : field.Value;
        if (value == null) return string.Empty;
        if (!(value is string) && !(value is ValueType)) throw new ArgumentException("显示字段必须为标量。");
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
    /// <summary>
    /// 计算字段比较或组合条件。
    /// </summary>
    /// <param name="condition">条件定义；为空时视为匹配。</param>
    /// <returns>条件成立返回 true，否则返回 false。</returns>
    internal bool Matches(BpCondition condition)
    {
        try { return MatchesCore(condition); }
        catch (ArgumentException error)
        {
            TemplateBindingDiagnostic.Attach(error, condition?.Path ?? "Condition");
            throw;
        }
    }

    private bool MatchesCore(BpCondition condition)
    {
        if (condition == null) return true;
        if (condition.Operator == "and") return condition.Conditions.All(Matches);
        if (condition.Operator == "or") return condition.Conditions.Any(Matches);
        var left = Resolve(condition.Path);
        var right = condition.Value;
        if (condition.Operator == "isNull") return left == null;
        if (condition.Operator == "notNull") return left != null;
        var same = left == null || right == null ? left == right :
            IsNumber(left) && IsNumber(right) ? Number(left) == Number(right) : left.GetType() == right.GetType() && Equals(left, right);
        if (condition.Operator == "eq") return same;
        if (condition.Operator == "ne") return !same;
        if (left == null || right == null) return false;
        int compare;
        if (IsNumber(left) && IsNumber(right)) compare = Number(left).CompareTo(Number(right));
        else if (left is string a && right is string b) compare = string.CompareOrdinal(a, b);
        else throw new ArgumentException("条件比较双方类型不兼容。");
        switch (condition.Operator) { case "gt": return compare > 0; case "ge": return compare >= 0; case "lt": return compare < 0; case "le": return compare <= 0; default: throw new ArgumentException("未知条件。"); }
    }
    /// <summary>
    /// 判断值是否属于模板条件支持的数字类型。
    /// </summary>
    /// <param name="value">待检查的值。</param>
    /// <returns>受支持的数字类型返回 true，否则返回 false。</returns>
    internal static bool IsNumber(object value) => value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal;
    /// <summary>
    /// 将受支持的数字转换为十进制数以供比较。
    /// </summary>
    /// <param name="value">已确认是受支持数字类型的值。</param>
    /// <returns>等值的十进制数。</returns>
    private static decimal Number(object value) => Convert.ToDecimal(value, CultureInfo.InvariantCulture);
}
