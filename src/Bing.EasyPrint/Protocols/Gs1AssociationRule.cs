using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.EasyPrint.Protocols;

internal sealed class Gs1AssociationRule
{
    internal Gs1AssociationRule(string kind, string raw, IReadOnlyList<IReadOnlyList<string>> alternatives)
    {
        Kind = kind;
        Raw = raw;
        Alternatives = alternatives;
    }

    internal string Kind { get; }
    internal string Raw { get; }
    internal IReadOnlyList<IReadOnlyList<string>> Alternatives { get; }

    internal bool Matches(IReadOnlyList<Gs1Element> elements, string excludedAi)
    {
        foreach (var alternative in Alternatives)
        {
            if (alternative.All(pattern => elements.Any(element =>
                    !string.Equals(element.Ai, excludedAi, StringComparison.Ordinal) &&
                    Gs1AssociationPattern.Matches(pattern, element.Ai))))
                return true;
        }
        return false;
    }

    internal IReadOnlyList<string> MissingPatterns(IReadOnlyList<Gs1Element> elements, string excludedAi)
    {
        var best = new List<string>();
        foreach (var alternative in Alternatives)
        {
            var missing = alternative.Where(pattern => !elements.Any(element =>
                    !string.Equals(element.Ai, excludedAi, StringComparison.Ordinal) &&
                    Gs1AssociationPattern.Matches(pattern, element.Ai))).ToList();
            if (missing.Count == 0) return Array.Empty<string>();
            if (best.Count == 0 || missing.Count < best.Count) best = missing;
        }
        return best;
    }
}

internal static class Gs1AssociationPattern
{
    internal static bool Matches(string pattern, string ai)
    {
        if (pattern == null || ai == null || pattern.Length != ai.Length) return false;
        for (var index = 0; index < pattern.Length; index++)
        {
            var expected = pattern[index];
            if (expected == 'n')
            {
                if (ai[index] < '0' || ai[index] > '9') return false;
                continue;
            }
            if (expected != ai[index]) return false;
        }
        return true;
    }
}

internal static class Gs1AssociationRuleParser
{
    internal static IReadOnlyList<Gs1AssociationRule> Parse(string attributes, string kind, string ai)
    {
        var result = new List<Gs1AssociationRule>();
        if (string.IsNullOrWhiteSpace(attributes)) return result;
        foreach (var token in attributes.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = token.IndexOf('=');
            var key = equals < 0 ? token : token.Substring(0, equals);
            if (!string.Equals(key, kind, StringComparison.Ordinal)) continue;
            if (equals < 0 || equals == token.Length - 1)
                throw new InvalidOperationException($"GS1 AI {ai} 的 {kind} 属性缺少值：{token}");
            var value = token.Substring(equals + 1);
            var alternatives = new List<IReadOnlyList<string>>();
            foreach (var alternative in value.Split(','))
            {
                if (alternative.Length == 0)
                    throw new InvalidOperationException($"GS1 AI {ai} 的 {kind} 属性包含空备选：{token}");
                var patterns = alternative.Split('+');
                if (patterns.Length == 0) throw new InvalidOperationException($"GS1 AI {ai} 的 {kind} 属性无效：{token}");
                foreach (var pattern in patterns)
                {
                    if (pattern.Length < 2 || pattern.Length > 4 ||
                        pattern.Any(character => character != 'n' && (character < '0' || character > '9')))
                        throw new InvalidOperationException($"GS1 AI {ai} 的 {kind} 模式无效：{pattern}");
                }
                alternatives.Add(patterns);
            }
            if (alternatives.Count == 0)
                throw new InvalidOperationException($"GS1 AI {ai} 的 {kind} 属性无效：{token}");
            result.Add(new Gs1AssociationRule(kind, value, alternatives));
        }
        return result;
    }
}

internal sealed class Gs1Element
{
    internal Gs1Element(string ai, string value)
    {
        Ai = ai;
        Value = value;
    }

    internal string Ai { get; }
    internal string Value { get; }
}
