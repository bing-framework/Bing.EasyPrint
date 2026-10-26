using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Newtonsoft.Json.Linq;

namespace Bing.EasyPrint.Templates;

internal static class TemplateBindingTraversal
{
    internal static IEnumerable<Binding> ExpandRowBindings(BpRow row, Binding binding,
        CancellationToken cancellationToken, Action countRow)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(row.RepeatSource))
        {
            if (binding.Matches(row.Condition))
            {
                countRow();
                yield return binding;
            }
            yield break;
        }

        var source = binding.Resolve(row.RepeatSource);
        var rawSource = binding.ResolveRaw(row.RepeatSource);
        if (source == null) yield break;
        if (!(source is IEnumerable sequence) || source is string || source is IDictionary)
        {
            var error = new ArgumentException("RepeatSource 必须为集合。");
            TemplateBindingDiagnostic.Attach(error, row.RepeatSource);
            throw error;
        }

        var rawSequence = rawSource is IEnumerable rawItems && rawSource is not string && rawSource is not IDictionary
            ? rawItems : null;
        var rawEnumerator = rawSequence?.GetEnumerator();
        try
        {
            var index = 0;
            foreach (var item in sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                countRow();
                var rawItem = rawEnumerator != null && rawEnumerator.MoveNext() ? rawEnumerator.Current : item;
                var child = binding.Child(item, rawItem, index++);
                if (child.Matches(row.Condition)) yield return child;
            }
        }
        finally { (rawEnumerator as IDisposable)?.Dispose(); }
    }

    internal static BpComponentDefinition ComponentDefinition(BpTemplate template, BpGroupElement group)
        => group.ComponentId == null ? null : template.Components.FirstOrDefault(item => item.Id == group.ComponentId);

    internal static Binding BindComponent(BpTemplate template, BpGroupElement group, Binding binding,
        PrintStorageBudget storage)
    {
        var definition = ComponentDefinition(template, group);
        var parameters = new BudgetedDictionary<string, object>("Template.ComponentParameters", storage,
            StringComparer.Ordinal, (key, value) => checked(PrintStorageBudget.TextBytes(key) +
                (value is string text ? PrintStorageBudget.TextBytes(text) : 0)));
        var rawParameters = new Dictionary<string, object>(StringComparer.Ordinal);
        if (definition?.Parameters != null) foreach (var parameter in definition.Parameters)
        {
            if (group.Parameters != null && group.Parameters.TryGetValue(parameter.Key, out var supplied))
            {
                var parameterValue = supplied.Path != null ? binding.Resolve(supplied.Path) : ParameterValue(supplied.Value);
                var rawParameterValue = supplied.Path != null ? binding.ResolveRaw(supplied.Path) : parameterValue;
                parameters.Add(parameter.Key, parameterValue);
                rawParameters.Add(parameter.Key, rawParameterValue);
            }
            else if (parameter.Value.DefaultValue != null)
            {
                var parameterValue = ParameterValue(parameter.Value.DefaultValue);
                parameters.Add(parameter.Key, parameterValue);
                rawParameters.Add(parameter.Key, parameterValue);
            }
            else if (parameter.Value.Required) throw new ArgumentException("缺少组件参数：" + parameter.Key);
        }
        if (group.Parameters != null) foreach (var parameter in group.Parameters)
            if (definition != null && (definition.Parameters == null || !definition.Parameters.ContainsKey(parameter.Key)))
                throw new ArgumentException("组件未声明参数：" + parameter.Key);
        if (definition == null && group.Parameters != null) foreach (var parameter in group.Parameters)
        {
            var parameterValue = parameter.Value.Path != null
                ? binding.Resolve(parameter.Value.Path) : ParameterValue(parameter.Value.Value);
            parameters.Add(parameter.Key, parameterValue);
            rawParameters.Add(parameter.Key, parameter.Value.Path != null
                ? binding.ResolveRaw(parameter.Value.Path) : parameterValue);
        }
        var item = group.DataPath == null ? binding.Item : binding.Resolve(group.DataPath);
        var rawItem = group.DataPath == null ? binding.RawItem : binding.ResolveRaw(group.DataPath);
        return binding.Component(item, rawItem, parameters, rawParameters);
    }

    private static object ParameterValue(JToken token)
        => token is JValue scalar ? scalar.Value : (object)token;
}
