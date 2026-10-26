using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Bing.EasyPrint.Protocols
{
    /// <summary>
    /// 固定版本 GS1 Syntax Dictionary 的公共只读元数据目录。
    /// </summary>
    /// <remarks>
    /// 目录由仓库内嵌的固定字典生成，不包含网络请求或运行时下载。
    /// </remarks>
    public static class Gs1AiCatalog
    {
        /// <summary>
        /// 固定字典文件的版本标识（提交 SHA）。
        /// </summary>
        public const string DictionaryCommit = "d1702ffb2fa5060f8460f7d581b01e377deaa1d6";

        private static readonly Lazy<IReadOnlyList<Gs1AiCatalogEntry>> LazyEntries =
            new Lazy<IReadOnlyList<Gs1AiCatalogEntry>>(CreateEntries);

        private static readonly Lazy<IReadOnlyDictionary<string, Gs1AiCatalogEntry>> LazyIndex =
            new Lazy<IReadOnlyDictionary<string, Gs1AiCatalogEntry>>(CreateIndex);

        /// <summary>
        /// 获取按 AI 序数排序的全部只读定义。
        /// </summary>
        public static IReadOnlyList<Gs1AiCatalogEntry> Entries => LazyEntries.Value;

        /// <summary>
        /// 根据 AI 获取固定字典定义。
        /// </summary>
        /// <param name="ai">应用标识符。</param>
        /// <param name="entry">匹配的只读定义；未找到时为 <see langword="null"/>。</param>
        /// <returns>字典包含该 AI 时返回 <see langword="true"/>。</returns>
        public static bool TryGet(string ai, out Gs1AiCatalogEntry entry)
        {
            if (ai == null)
            {
                entry = null;
                return false;
            }

            return LazyIndex.Value.TryGetValue(ai, out entry);
        }

        private static IReadOnlyList<Gs1AiCatalogEntry> CreateEntries()
        {
            var primaries = new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>(StringComparer.Ordinal);
            foreach (var definition in Gs1SyntaxDictionary.AllDefinitions)
                if (TryReadDigitalLinkBranches(definition.Attributes, out var branches))
                    primaries[definition.Ai] = branches;

            var qualifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var branches in primaries.Values)
                foreach (var branch in branches)
                    foreach (var ai in branch)
                        qualifiers.Add(ai);

            var entries = new List<Gs1AiCatalogEntry>();
            foreach (var definition in Gs1SyntaxDictionary.AllDefinitions.OrderBy(entry => entry.Ai, StringComparer.Ordinal))
            {
                primaries.TryGetValue(definition.Ai, out var branches);
                var role = branches != null
                    ? Gs1AiDigitalLinkRole.PrimaryKey
                    : qualifiers.Contains(definition.Ai)
                        ? Gs1AiDigitalLinkRole.KeyQualifier
                        : IsDigitalLinkExcluded(definition.Ai)
                            ? Gs1AiDigitalLinkRole.Unsupported
                            : Gs1AiDigitalLinkRole.DataAttribute;
                entries.Add(new Gs1AiCatalogEntry(definition, role, branches));
            }

            return new ReadOnlyCollection<Gs1AiCatalogEntry>(entries);
        }

        private static bool IsDigitalLinkExcluded(string ai)
            => ai == "03" || ai == "8014" || ai == "8200";

        private static IReadOnlyDictionary<string, Gs1AiCatalogEntry> CreateIndex()
        {
            var result = new Dictionary<string, Gs1AiCatalogEntry>(StringComparer.Ordinal);
            foreach (var entry in Entries)
                result.Add(entry.Ai, entry);
            return new ReadOnlyDictionary<string, Gs1AiCatalogEntry>(result);
        }

        internal static bool TryReadDigitalLinkBranches(string attributes, out IReadOnlyList<IReadOnlyList<string>> branches)
        {
            branches = null;
            if (string.IsNullOrEmpty(attributes)) return false;
            foreach (var token in attributes.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                const string prefix = "dlpkey=";
                if (!token.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var value = token.Substring(prefix.Length);
                if (value.Length == 0) return false;
                var parsed = new List<IReadOnlyList<string>>();
                foreach (var branchText in value.Split('|'))
                {
                    if (branchText.Length == 0) return false;
                    var branch = branchText.Split(',');
                    if (branch.Length == 0 || branch.Any(ai => string.IsNullOrEmpty(ai))) return false;
                    parsed.Add(Array.AsReadOnly(branch));
                }
                if (parsed.Count == 0) return false;
                branches = new ReadOnlyCollection<IReadOnlyList<string>>(parsed);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// GS1 AI 在 Digital Link 中的角色。
    /// </summary>
    public enum Gs1AiDigitalLinkRole
    {
        /// <summary>可作为 Digital Link 路径主键（本产品仅接受 01）。</summary>
        PrimaryKey,
        /// <summary>可作为主键限定符出现在路径中。</summary>
        KeyQualifier,
        /// <summary>只能作为查询参数数据属性。</summary>
        DataAttribute,
        /// <summary>固定字典包含该 AI，但 GS1 Digital Link URI Syntax 排除它。</summary>
        Unsupported
    }

    /// <summary>
    /// 固定字典中的只读 AI 定义。
    /// </summary>
    public sealed class Gs1AiCatalogEntry
    {
        internal Gs1AiCatalogEntry(Gs1SyntaxAiDefinition definition, Gs1AiDigitalLinkRole role,
            IReadOnlyList<IReadOnlyList<string>> branches)
        {
            Ai = definition.Ai;
            Title = definition.Title;
            Specification = definition.Specification;
            Attributes = definition.Attributes;
            PredefinedLength = definition.PredefinedLength;
            RequiresSeparator = definition.RequiresSeparator;
            MinimumLength = definition.MinimumLength;
            MaximumLength = definition.MaximumLength;
            DigitalLinkRole = role;
            DigitalLinkBranches = branches ?? Array.AsReadOnly(Array.Empty<IReadOnlyList<string>>());
            Components = new ReadOnlyCollection<Gs1AiComponent>(
                definition.Components.Select(component => new Gs1AiComponent(component)).ToList());
            RequirementRules = ToPublicRules(definition.RequirementRules);
            ExclusionRules = ToPublicRules(definition.ExclusionRules);
        }

        /// <summary>获取应用标识符。</summary>
        public string Ai { get; }
        /// <summary>获取字典中的标题。</summary>
        public string Title { get; }
        /// <summary>获取字典原始规格。</summary>
        public string Specification { get; }
        /// <summary>获取字典属性原文，包括 req、ex 和 dlpkey。</summary>
        public string Attributes { get; }
        /// <summary>获取是否为预定义长度。</summary>
        public bool PredefinedLength { get; }
        /// <summary>获取后续元素是否必须使用 FNC1 分隔。</summary>
        public bool RequiresSeparator { get; }
        /// <summary>获取字段总最小长度。</summary>
        public int MinimumLength { get; }
        /// <summary>获取字段总最大长度。</summary>
        public int MaximumLength { get; }
        /// <summary>获取 Digital Link 角色。</summary>
        public Gs1AiDigitalLinkRole DigitalLinkRole { get; }
        /// <summary>获取主键可用的限定符分支；非主键时为空集合。</summary>
        public IReadOnlyList<IReadOnlyList<string>> DigitalLinkBranches { get; }
        /// <summary>获取按顺序排列的字段组件。</summary>
        public IReadOnlyList<Gs1AiComponent> Components { get; }
        /// <summary>获取 req 必需关联规则。</summary>
        public IReadOnlyList<Gs1AiAssociationRuleInfo> RequirementRules { get; }
        /// <summary>获取 ex 互斥关联规则。</summary>
        public IReadOnlyList<Gs1AiAssociationRuleInfo> ExclusionRules { get; }

        private static IReadOnlyList<Gs1AiAssociationRuleInfo> ToPublicRules(
            IReadOnlyList<Gs1AssociationRule> rules)
        {
            var result = new List<Gs1AiAssociationRuleInfo>();
            foreach (var rule in rules)
            {
                var alternatives = new List<IReadOnlyList<string>>();
                foreach (var alternative in rule.Alternatives)
                    alternatives.Add(Array.AsReadOnly(alternative.ToArray()));
                result.Add(new Gs1AiAssociationRuleInfo(rule.Kind, rule.Raw,
                    new ReadOnlyCollection<IReadOnlyList<string>>(alternatives)));
            }
            return new ReadOnlyCollection<Gs1AiAssociationRuleInfo>(result);
        }
    }

    /// <summary>
    /// GS1 字段的只读字符组件。
    /// </summary>
    public sealed class Gs1AiComponent
    {
        internal Gs1AiComponent(Gs1SyntaxComponent component)
        {
            Type = component.Type;
            MinimumLength = component.MinimumLength;
            MaximumLength = component.MaximumLength;
            Optional = component.Optional;
            Linters = Array.AsReadOnly(component.Linters.ToArray());
        }

        /// <summary>获取字段类型：N、X、Y 或 Z。</summary>
        public char Type { get; }
        /// <summary>获取最小长度。</summary>
        public int MinimumLength { get; }
        /// <summary>获取最大长度。</summary>
        public int MaximumLength { get; }
        /// <summary>获取是否为可选组件。</summary>
        public bool Optional { get; }
        /// <summary>获取字段 Linter 名称。</summary>
        public IReadOnlyList<string> Linters { get; }
    }

    /// <summary>
    /// GS1 req/ex 关联规则的只读投影。
    /// </summary>
    public sealed class Gs1AiAssociationRuleInfo
    {
        internal Gs1AiAssociationRuleInfo(string kind, string raw,
            IReadOnlyList<IReadOnlyList<string>> alternatives)
        {
            Kind = kind;
            Raw = raw;
            Alternatives = alternatives;
        }

        /// <summary>获取规则类别（req 或 ex）。</summary>
        public string Kind { get; }
        /// <summary>获取规则原文。</summary>
        public string Raw { get; }
        /// <summary>获取备选 AI 模式组。</summary>
        public IReadOnlyList<IReadOnlyList<string>> Alternatives { get; }
    }
}
