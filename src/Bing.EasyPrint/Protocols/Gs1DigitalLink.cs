using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bing.EasyPrint.Protocols
{
    /// <summary>
    /// GS1 Digital Link 的构建、解析与本地规范化入口。
    /// </summary>
    /// <remarks>
    /// 本类型只处理 GTIN 主键 01，不访问 resolver，也不执行压缩 URI 或重定向。
    /// </remarks>
    public static class Gs1DigitalLink
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 将 GTIN 输入补齐为 14 位并校验校验位。
        /// </summary>
        /// <param name="gtin">8、12、13 或 14 位 GTIN 字符串。</param>
        /// <returns>保留前导零的 14 位 GTIN。</returns>
        /// <exception cref="Gs1DigitalLinkException">输入位数、字符或校验位无效。</exception>
        public static string NormalizeGtin(string gtin)
        {
            if (gtin == null) throw Error("gtin-required", "GTIN 不能为空。", "gtin");
            if (gtin.Length == 0) throw Error("gtin-required", "GTIN 不能为空。", "gtin");
            for (var index = 0; index < gtin.Length; index++)
                if (gtin[index] < '0' || gtin[index] > '9')
                    throw Error("gtin-characters", "GTIN 只能包含 ASCII 数字。", "gtin");
            if (gtin.Length != 8 && gtin.Length != 12 && gtin.Length != 13 && gtin.Length != 14)
                throw Error("gtin-length", "GTIN 必须为 8、12、13 或 14 位数字。", "gtin");
            var normalized = gtin.PadLeft(14, '0');
            try
            {
                Gs1SyntaxDictionary.Validate("01", normalized, "gtin");
            }
            catch (ArgumentException error)
            {
                throw Error("gtin-check-digit", error.Message, "gtin", ai: "01", inner: error);
            }
            return normalized;
        }

        /// <summary>
        /// 构建 GS1 Digital Link。
        /// </summary>
        /// <param name="request">构建请求；基址必须为显式 HTTPS URI。</param>
        /// <returns>包含 14 位 GTIN、限定符和查询参数的 URI。</returns>
        /// <exception cref="Gs1DigitalLinkException">请求结构或 GS1 字段无效。</exception>
        public static string Build(Gs1DigitalLinkBuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var baseUri = ParseBaseUri(request.BaseUri);
            var gtin = NormalizeGtin(request.Gtin);
            IReadOnlyList<Gs1DigitalLinkAiValue> qualifiers = request.Qualifiers == null
                ? Array.Empty<Gs1DigitalLinkAiValue>()
                : new List<Gs1DigitalLinkAiValue>(request.Qualifiers);
            IReadOnlyList<Gs1DigitalLinkAiValue> dataAttributes = request.DataAttributes == null
                ? Array.Empty<Gs1DigitalLinkAiValue>()
                : new List<Gs1DigitalLinkAiValue>(request.DataAttributes);
            IReadOnlyList<Gs1DigitalLinkQueryParameter> extensions = request.ExtensionParameters == null
                ? Array.Empty<Gs1DigitalLinkQueryParameter>()
                : new List<Gs1DigitalLinkQueryParameter>(request.ExtensionParameters);

            var gs1Items = new List<Gs1DigitalLinkAiValue>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { "01" };
            for (var index = 0; index < qualifiers.Count; index++)
            {
                var item = qualifiers[index];
                var path = "qualifiers[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (item == null) throw Error("qualifier-required", "限定符项不能为空。", path);
                ValidateAi(item.Ai, path);
                if (item.Ai == "01")
                    throw Error("duplicate-ai", "01 已作为 Digital Link 主键，不能重复。", path + ".ai",
                        ai: item.Ai, index: index);
                if (!seen.Add(item.Ai))
                    throw Error("duplicate-ai", "Digital Link 不能重复使用 AI " + item.Ai + "。", path + ".ai",
                        ai: item.Ai, index: index);
                ValidateValue(item.Ai, item.Value, path + ".value", index);
                gs1Items.Add(item);
            }
            ValidateQualifierBranch(qualifiers, "qualifiers");

            for (var index = 0; index < dataAttributes.Count; index++)
            {
                var item = dataAttributes[index];
                var path = "dataAttributes[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (item == null) throw Error("data-attribute-required", "查询 AI 项不能为空。", path);
                ValidateAi(item.Ai, path);
                ValidateDigitalLinkSupport(item.Ai, path, index);
                if (!seen.Add(item.Ai))
                    throw Error("duplicate-ai", "Digital Link 不能重复使用 AI " + item.Ai + "。", path + ".ai",
                        ai: item.Ai, index: index);
                ValidateValue(item.Ai, item.Value, path + ".value", index);
                gs1Items.Add(item);
            }

            var normalizedExtensions = new List<Gs1DigitalLinkQueryParameter>();
            for (var index = 0; index < extensions.Count; index++)
            {
                var item = extensions[index];
                var path = "extensionParameters[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (item == null) throw Error("query-required", "非 GS1 查询参数不能为空。", path);
                ValidateExtensionKey(item.Key, path + ".key", index);
                if (IsAllDigits(item.Key))
                    throw Error("unknown-gs1-ai", "未知的数字查询参数不能作为非 GS1 参数放行。", path + ".key",
                        index: index);
                ValidateText(item.Value ?? string.Empty, path + ".value");
                normalizedExtensions.Add(item);
            }

            if (request.ValidateAssociations)
                ValidateAssociations(gtin, qualifiers, dataAttributes);

            var builder = new StringBuilder();
            builder.Append(baseUri.BaseUri);
            builder.Append("/01/").Append(gtin);
            foreach (var item in qualifiers)
                builder.Append('/').Append(item.Ai).Append('/').Append(Escape(item.Value));

            var query = new List<string>();
            foreach (var item in dataAttributes)
                query.Add(item.Ai + "=" + Escape(item.Value));
            foreach (var item in normalizedExtensions)
                query.Add(Escape(item.Key) + "=" + Escape(item.Value ?? string.Empty));
            AppendQuery(builder, query);

            if (request.Fragment != null)
            {
                ValidateFragment(request.Fragment);
                builder.Append('#').Append(request.Fragment);
            }
            return builder.ToString();
        }

        /// <summary>
        /// 解析 GS1 Digital Link；不会访问 resolver。
        /// </summary>
        /// <param name="uri">待解析的绝对 URI。</param>
        /// <returns>保留原始语义的解析结果。</returns>
        /// <exception cref="Gs1DigitalLinkException">URI 或 GS1 字段无效。</exception>
        public static Gs1DigitalLinkResult Parse(string uri) => Parse(uri, false);

        /// <summary>
        /// 解析 GS1 Digital Link；不会访问 resolver。
        /// </summary>
        /// <param name="uri">待解析的绝对 URI。</param>
        /// <param name="validateAssociations">是否执行显式 req/ex 关联校验。</param>
        /// <returns>保留原始语义的解析结果。</returns>
        /// <exception cref="Gs1DigitalLinkException">URI 或 GS1 字段无效。</exception>
        public static Gs1DigitalLinkResult Parse(string uri, bool validateAssociations)
        {
            if (uri == null) throw Error("uri-required", "URI 不能为空。", "uri");
            if (uri.Length == 0) throw Error("uri-required", "URI 不能为空。", "uri");
            for (var index = 0; index < uri.Length; index++)
            {
                if (uri[index] <= ' ' || uri[index] == '\u007f')
                    throw Error("uri-characters", "URI 不能包含未转义空白或控制字符。", "uri");
                if (uri[index] == '\\')
                    throw Error("invalid-uri-character", "URI 不能包含反斜线。", "uri");
            }

            var fragment = ParseFragment(uri, out var withoutFragment);
            var queryText = ParseQueryText(withoutFragment, out var withoutQuery);
            var parts = ParseAbsoluteUri(withoutQuery);

            var normalizedPath = parts.Path;
            if (normalizedPath.EndsWith("/", StringComparison.Ordinal))
                normalizedPath = normalizedPath.Substring(0, normalizedPath.Length - 1);
            var segments = normalizedPath.Split('/');
            var primaryIndex = -1;
            string gtin = null;
            Gs1DigitalLinkException primaryError = null;
            for (var index = segments.Length - 2; index >= 0; index--)
            {
                if (segments[index] != "01") continue;
                if (segments[index + 1].Length != 14)
                {
                    primaryError ??= Error("gtin-uri-length", "Digital Link URI 中的 GTIN 必须为 14 位数字。",
                        "uri.path.01", ai: "01");
                    continue;
                }
                try { gtin = NormalizeGtin(segments[index + 1]); }
                catch (Gs1DigitalLinkException error)
                {
                    primaryError ??= error;
                    continue;
                }
                primaryIndex = index;
                break;
            }
            if (primaryIndex < 0)
            {
                if (primaryError != null) throw primaryError;
                throw Error("primary-key-unsupported", "Digital Link 路径必须包含未压缩 GTIN 主键 /01/。", "uri.path",
                    ai: "01");
            }

            var pathPrefix = string.Join("/", segments.Take(primaryIndex));
            var qualifiers = new List<Gs1DigitalLinkAiValue>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { "01" };
            var qualifierIndex = primaryIndex + 2;
            while (qualifierIndex < segments.Length)
            {
                if (qualifierIndex + 1 >= segments.Length)
                    throw Error("qualifier-incomplete", "Digital Link 限定符必须为 AI/值成对出现。", "uri.path");
                var ai = segments[qualifierIndex];
                var valueRaw = segments[qualifierIndex + 1];
                var path = "qualifiers[" + qualifiers.Count.ToString(CultureInfo.InvariantCulture) + "]";
                ValidateAi(ai, "uri.path");
                ValidateDigitalLinkSupport(ai, path, qualifiers.Count);
                if (!seen.Add(ai))
                    throw Error("duplicate-ai", "Digital Link 不能重复使用 AI " + ai + "。", path + ".ai",
                        ai: ai, index: qualifiers.Count);
                var value = Decode(valueRaw, "uri.path." + ai);
                ValidateValue(ai, value, path + ".value", qualifiers.Count);
                qualifiers.Add(new Gs1DigitalLinkAiValue(ai, value));
                qualifierIndex += 2;
            }
            ValidateQualifierBranch(qualifiers, "uri.path");

            var queryItems = ParseQueryItems(queryText);
            var dataAttributes = new List<Gs1DigitalLinkAiValue>();
            var extensions = new List<Gs1DigitalLinkQueryParameter>();
            for (var index = 0; index < queryItems.Count; index++)
            {
                var item = queryItems[index];
                var path = "query[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (item.IsGs1)
                {
                    if (!seen.Add(item.Ai))
                        throw Error("duplicate-ai", "Digital Link 不能重复使用 AI " + item.Ai + "。", path + ".ai",
                            ai: item.Ai, index: index);
                    dataAttributes.Add(new Gs1DigitalLinkAiValue(item.Ai, item.Value));
                }
                else
                {
                    extensions.Add(new Gs1DigitalLinkQueryParameter(item.Key, item.Value));
                }
            }

            if (validateAssociations)
                ValidateAssociations(gtin, qualifiers, dataAttributes);

            var authority = parts.Scheme + "://" + parts.Authority;
            var baseUri = authority + pathPrefix;
            return new Gs1DigitalLinkResult(uri, parts.Scheme, parts.Authority, pathPrefix, baseUri, gtin,
                new ReadOnlyCollection<Gs1DigitalLinkAiValue>(qualifiers),
                new ReadOnlyCollection<Gs1DigitalLinkAiValue>(dataAttributes),
                new ReadOnlyCollection<Gs1DigitalLinkQueryParameter>(extensions),
                new ReadOnlyCollection<Gs1DigitalLinkQueryItem>(queryItems),
                fragment);
        }

        /// <summary>
        /// 尝试解析 GS1 Digital Link，并以结构化诊断返回失败原因。
        /// </summary>
        /// <param name="uri">待解析的绝对 URI。</param>
        /// <param name="result">解析成功时的结果；失败时为 <see langword="null"/>。</param>
        /// <param name="diagnostics">失败诊断；成功时为空集合。</param>
        /// <returns>解析成功返回 <see langword="true"/>。</returns>
        public static bool TryParse(string uri, out Gs1DigitalLinkResult result,
            out IReadOnlyList<Gs1DigitalLinkDiagnostic> diagnostics)
            => TryParse(uri, false, out result, out diagnostics);

        /// <summary>
        /// 尝试解析 GS1 Digital Link，并以结构化诊断返回失败原因。
        /// </summary>
        /// <param name="uri">待解析的绝对 URI。</param>
        /// <param name="validateAssociations">是否执行显式 req/ex 关联校验。</param>
        /// <param name="result">解析成功时的结果；失败时为 <see langword="null"/>。</param>
        /// <param name="diagnostics">失败诊断；成功时为空集合。</param>
        /// <returns>解析成功返回 <see langword="true"/>。</returns>
        public static bool TryParse(string uri, bool validateAssociations, out Gs1DigitalLinkResult result,
            out IReadOnlyList<Gs1DigitalLinkDiagnostic> diagnostics)
        {
            try
            {
                result = Parse(uri, validateAssociations);
                diagnostics = Array.Empty<Gs1DigitalLinkDiagnostic>();
                return true;
            }
            catch (Gs1DigitalLinkException error)
            {
                result = null;
                diagnostics = new ReadOnlyCollection<Gs1DigitalLinkDiagnostic>(
                    new[] { new Gs1DigitalLinkDiagnostic(error.Code, error.Message, error.FieldPath, error.Ai,
                        error.ItemIndex, error.RelatedAis) });
                return false;
            }
        }

        /// <summary>
        /// 保留调用者域名、路径前缀、非 GS1 参数和扩展顺序地规范化 URI。
        /// </summary>
        /// <param name="uri">待规范化的 GS1 Digital Link。</param>
        /// <param name="validateAssociations">是否执行显式 req/ex 关联校验。</param>
        /// <returns>不冒充 id.gs1.org canonical URI 的本地规范化结果。</returns>
        /// <exception cref="Gs1DigitalLinkException">URI 或 GS1 字段无效。</exception>
        public static string Normalize(string uri, bool validateAssociations = false)
        {
            var parsed = Parse(uri, validateAssociations);
            var builder = new StringBuilder(parsed.BaseUri);
            builder.Append("/01/").Append(parsed.Gtin);
            foreach (var item in parsed.Qualifiers)
                builder.Append('/').Append(item.Ai).Append('/').Append(Escape(item.Value));
            var query = new List<string>();
            foreach (var item in parsed.QueryItems)
                query.Add(item.IsGs1
                    ? item.Ai + "=" + Escape(item.Value)
                    : Escape(item.Key) + "=" + Escape(item.Value));
            AppendQuery(builder, query);
            if (parsed.Fragment != null) builder.Append('#').Append(parsed.Fragment);
            return builder.ToString();
        }

        private static void ValidateQualifierBranch(IReadOnlyList<Gs1DigitalLinkAiValue> qualifiers, string path)
        {
            var entry = Gs1AiCatalog.Entries.FirstOrDefault(item => item.Ai == "01");
            if (entry == null)
                throw Error("dictionary-primary-missing", "固定字典缺少 Digital Link 主键 AI 01。", path, ai: "01");
            var branches = entry.DigitalLinkBranches;
            if (branches.Count == 0)
            {
                if (qualifiers.Count == 0) return;
                throw Error("qualifier-not-supported", "AI 01 没有声明 Digital Link 限定符分支。", path, ai: "01");
            }
            if (qualifiers.Count == 0) return;

            var actual = qualifiers.Select(item => item.Ai).ToArray();
            foreach (var branch in branches)
            {
                if (actual.Any(ai => !branch.Contains(ai, StringComparer.Ordinal))) continue;
                var branchArray = branch.ToArray();
                var positions = actual.Select(ai => Array.IndexOf(branchArray, ai)).ToArray();
                var ordered = true;
                for (var index = 1; index < positions.Length; index++)
                    if (positions[index] <= positions[index - 1]) ordered = false;
                if (ordered) return;
                throw Error("qualifier-order", "Digital Link 限定符顺序必须遵循 dlpkey 分支声明："
                    + string.Join(",", branch) + "。", path, ai: actual.FirstOrDefault());
            }

            throw Error("qualifier-branch", "Digital Link 限定符只能来自一个 dlpkey 分支，235 不能与 22/10/21 混合。",
                path, ai: actual.FirstOrDefault());
        }

        private static void ValidateAssociations(string gtin, IReadOnlyList<Gs1DigitalLinkAiValue> qualifiers,
            IReadOnlyList<Gs1DigitalLinkAiValue> dataAttributes)
        {
            var elements = new List<Gs1Element> { new Gs1Element("01", gtin) };
            elements.AddRange(qualifiers.Select(item => new Gs1Element(item.Ai, item.Value)));
            elements.AddRange(dataAttributes.Select(item => new Gs1Element(item.Ai, item.Value)));
            foreach (var element in elements)
            {
                if (!Gs1SyntaxDictionary.TryGet(element.Ai, out var definition))
                    throw Error("unknown-ai", "GS1 AI " + element.Ai + " 不在固定字典中。", "association",
                        ai: element.Ai);
                foreach (var rule in definition.RequirementRules)
                {
                    if (rule.Matches(elements, null)) continue;
                    var missing = rule.MissingPatterns(elements, null);
                    throw Error("association-required",
                        "GS1 AI " + element.Ai + " 缺少必需关联 " + string.Join("+", missing) + "（req=" + rule.Raw + "）。",
                        "association", ai: element.Ai, related: missing);
                }
                foreach (var rule in definition.ExclusionRules)
                {
                    if (!rule.Matches(elements, element.Ai)) continue;
                    var conflict = elements.First(item =>
                        !string.Equals(item.Ai, element.Ai, StringComparison.Ordinal) &&
                        rule.Alternatives.Any(alternative => alternative.All(pattern =>
                            Gs1AssociationPattern.Matches(pattern, item.Ai))));
                    throw Error("association-excluded",
                        "GS1 AI " + element.Ai + " 与 AI " + conflict.Ai + " 互斥（ex=" + rule.Raw + "）。",
                        "association", ai: element.Ai, related: new[] { conflict.Ai });
                }
            }
        }

        private static void ValidateAi(string ai, string path, int? index = null)
        {
            if (string.IsNullOrEmpty(ai))
                throw Error("ai-required", "GS1 AI 不能为空。", path + ".ai", index: index);
            if (!IsAllDigits(ai) || ai.Length < 2 || ai.Length > 4)
                throw Error("ai-characters", "GS1 AI 必须为 2 到 4 位 ASCII 数字。", path + ".ai", ai: ai, index: index);
            if (!TryGetEntry(ai, out _))
                throw Error("unknown-ai", "GS1 AI " + ai + " 不在固定字典中。", path + ".ai", ai: ai, index: index);
        }

        private static void ValidateDigitalLinkSupport(string ai, string path, int? index = null)
        {
            if (!Gs1AiCatalog.TryGet(ai, out var entry) ||
                entry.DigitalLinkRole != Gs1AiDigitalLinkRole.Unsupported) return;
            throw Error("digital-link-ai-unsupported", "GS1 AI " + ai + " 不允许用于 GS1 Digital Link URI Syntax。",
                path + ".ai", ai: ai, index: index);
        }

        private static void ValidateValue(string ai, string value, string path, int? index = null)
        {
            if (value == null)
                throw Error("value-required", "GS1 AI " + ai + " 的值不能为空。", path, ai: ai, index: index);
            try
            {
                Gs1SyntaxDictionary.Validate(ai, value, path);
            }
            catch (ArgumentException error)
            {
                throw Error("value-invalid", error.Message, path, ai: ai, index: index, inner: error);
            }
        }

        private static void ValidateExtensionKey(string key, string path, int index)
        {
            if (string.IsNullOrEmpty(key))
                throw Error("query-key-required", "非 GS1 查询参数名不能为空。", path, index: index);
            ValidateText(key, path);
            if (string.Equals(key, "linkType", StringComparison.Ordinal) ||
                string.Equals(key, "context", StringComparison.Ordinal))
                throw Error("reserved-query-key", "非解析器扩展不能使用保留查询键 " + key + "。", path, index: index);
        }

        private static void ValidateText(string value, string path)
        {
            for (var index = 0; index < value.Length; index++)
                if (value[index] < ' ' || value[index] == '\u007f')
                    throw Error("query-characters", "查询参数不能包含控制字符。", path);
        }

        private static void ValidateFragment(string fragment)
        {
            if (fragment.IndexOf('#') >= 0)
                throw Error("fragment-characters", "fragment 不能包含第二个 #。", "fragment");
            ValidateText(fragment, "fragment");
            Decode(fragment, "fragment");
        }

        private static List<Gs1DigitalLinkQueryItem> ParseQueryItems(string query)
        {
            var result = new List<Gs1DigitalLinkQueryItem>();
            if (query == null) return result;
            if (query.Length == 0)
                throw Error("query-empty", "查询分隔符后必须至少包含一个 key=value 项。", "query");
            var segments = query.Split(new[] { '&', ';' }, StringSplitOptions.None);
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index].Length == 0)
                    throw Error("query-empty-item", "查询字符串不能包含空参数项。", "query[" + index + "]", index: index);
                var equals = segments[index].IndexOf('=');
                if (equals < 0)
                    throw Error("query-equals-required", "查询参数必须使用 key=value 形式。",
                        "query[" + index + "]", index: index);
                var rawKey = segments[index].Substring(0, equals);
                var rawValue = segments[index].Substring(equals + 1);
                var key = Decode(rawKey, "query[" + index + "].key");
                var value = Decode(rawValue, "query[" + index + "].value");
                if (key.Length == 0)
                    throw Error("query-key-required", "查询参数名不能为空。", "query[" + index + "].key", index: index);
                if (IsAllDigits(key))
                {
                    if (!TryGetEntry(key, out _))
                        throw Error("unknown-gs1-ai", "未知的数字查询参数不能作为非 GS1 参数放行。",
                            "query[" + index + "].key", ai: key, index: index);
                    ValidateDigitalLinkSupport(key, "query[" + index + "]", index);
                    ValidateValue(key, value, "query[" + index + "].value", index);
                    result.Add(new Gs1DigitalLinkQueryItem(key, value));
                }
                else
                {
                    ValidateExtensionKey(key, "query[" + index + "].key", index);
                    result.Add(new Gs1DigitalLinkQueryItem(null, value, key));
                }
            }
            return result;
        }

        private static bool TryGetEntry(string ai, out Gs1AiCatalogEntry entry)
            => Gs1AiCatalog.TryGet(ai, out entry);

        private static string ParseFragment(string uri, out string withoutFragment)
        {
            var hash = uri.IndexOf('#');
            if (hash < 0)
            {
                withoutFragment = uri;
                return null;
            }
            var fragment = uri.Substring(hash + 1);
            if (fragment.IndexOf('#') >= 0)
                throw Error("fragment-characters", "fragment 不能包含第二个 #。", "fragment");
            ValidateRawUriComponent(fragment, "uri.fragment", allowSlash: true, allowQuestion: true);
            Decode(fragment, "fragment");
            withoutFragment = uri.Substring(0, hash);
            return fragment;
        }

        private static string ParseQueryText(string uri, out string withoutQuery)
        {
            var question = uri.IndexOf('?');
            if (question < 0)
            {
                withoutQuery = uri;
                return null;
            }
            withoutQuery = uri.Substring(0, question);
            var query = uri.Substring(question + 1);
            ValidateRawUriComponent(query, "uri.query", allowSlash: true, allowQuestion: true);
            return query;
        }

        private static BaseParts ParseAbsoluteUri(string uri)
        {
            var schemeEnd = uri.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd <= 0)
                throw Error("base-uri", "Digital Link 必须是包含主机名的绝对 URI。", "uri");
            var scheme = uri.Substring(0, schemeEnd);
            if (!string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase))
                throw Error("base-uri-scheme", "Digital Link 只支持 HTTP 或 HTTPS 基址。", "uri.scheme");
            if (!IsAsciiLetter(scheme[0]) || scheme.Any(character =>
                    !(IsAsciiLetter(character) || character >= '0' && character <= '9' ||
                      character == '+' || character == '-' || character == '.')))
                throw Error("base-uri-scheme", "URI scheme 格式无效。", "uri.scheme");
            var afterScheme = uri.Substring(schemeEnd + 3);
            var slash = afterScheme.IndexOf('/');
            var authority = slash < 0 ? afterScheme : afterScheme.Substring(0, slash);
            var path = slash < 0 ? string.Empty : afterScheme.Substring(slash);
            ValidateAuthority(authority);
            ValidateRawUriComponent(path, "uri.path", allowSlash: true);
            if (path.Length > 0 && path[0] != '/')
                throw Error("base-uri-path", "Digital Link 路径必须以 / 开头。", "uri.path");
            return new BaseParts(scheme, authority, path);
        }

        private static void ValidateAuthority(string authority)
        {
            if (string.IsNullOrEmpty(authority) || authority.IndexOf('@') >= 0)
                throw Error("base-uri-host", "Digital Link 基址必须包含合法主机名。", "uri.authority");

            string host;
            string port = null;
            if (authority[0] == '[')
            {
                var close = authority.IndexOf(']');
                if (close < 0 || close == 1 || authority.IndexOf('[', 1) >= 0 ||
                    authority.IndexOf(']', close + 1) >= 0)
                    throw Error("base-uri-host", "IP 字面量格式无效。", "uri.authority");
                host = authority.Substring(1, close - 1);
                if (host.IndexOf('%') >= 0 || !IPAddress.TryParse(host, out var address) ||
                    address.AddressFamily != AddressFamily.InterNetworkV6)
                    throw Error("base-uri-host", "IPv6 地址格式无效。", "uri.authority");
                var remainder = authority.Substring(close + 1);
                if (remainder.Length > 0)
                {
                    if (remainder[0] != ':')
                        throw Error("base-uri-host", "IP 字面量后的 authority 格式无效。", "uri.authority");
                    port = remainder.Substring(1);
                }
            }
            else
            {
                if (authority.IndexOf('[') >= 0 || authority.IndexOf(']') >= 0)
                    throw Error("base-uri-host", "IP 字面量格式无效。", "uri.authority");
                var colon = authority.IndexOf(':');
                if (colon >= 0)
                {
                    if (authority.IndexOf(':', colon + 1) >= 0)
                        throw Error("base-uri-host", "IPv6 地址必须使用方括号。", "uri.authority");
                    host = authority.Substring(0, colon);
                    port = authority.Substring(colon + 1);
                }
                else host = authority;
                ValidateRegName(host);
            }

            if (port == null) return;
            if (port.Length == 0 || port.Any(character => character < '0' || character > '9') ||
                !int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var portNumber) ||
                portNumber > 65535)
                throw Error("base-uri-port", "URI port 必须为 0 到 65535 的十进制整数。", "uri.authority.port");
        }

        private static void ValidateRegName(string host)
        {
            if (host.Length == 0)
                throw Error("base-uri-host", "Digital Link 基址必须包含合法主机名。", "uri.authority");
            for (var index = 0; index < host.Length; index++)
            {
                var character = host[index];
                if (character == '%')
                {
                    if (index + 2 >= host.Length || !IsHex(host[index + 1]) || !IsHex(host[index + 2]))
                        throw Error("invalid-percent-encoding", "authority 包含无效的百分号转义。", "uri.authority");
                    index += 2;
                    continue;
                }
                if (character > '\u007f' || !(IsUnreserved(character) || IsSubDelimiter(character)))
                    throw Error("base-uri-host", "URI 主机名包含无效字符。", "uri.authority");
            }
        }

        private static void ValidateRawUriComponent(string value, string path, bool allowSlash,
            bool allowQuestion = false)
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character == '%')
                {
                    if (index + 2 >= value.Length || !IsHex(value[index + 1]) || !IsHex(value[index + 2]))
                        throw Error("invalid-percent-encoding", "URI 包含无效的百分号转义。", path);
                    index += 2;
                    continue;
                }
                if (character > '\u007f' || !(IsUnreserved(character) || IsSubDelimiter(character) ||
                    character == ':' || character == '@' || allowSlash && character == '/' ||
                    allowQuestion && character == '?'))
                    throw Error("invalid-uri-character", "URI 组件包含无效的原始字符。", path);
            }
        }

        private static bool IsAsciiLetter(char value)
            => value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';

        private static bool IsUnreserved(char value)
            => IsAsciiLetter(value) || value >= '0' && value <= '9' || value == '-' || value == '.' ||
               value == '_' || value == '~';

        private static bool IsSubDelimiter(char value)
            => value == '!' || value == '$' || value == '&' || value == '\'' || value == '(' ||
               value == ')' || value == '*' || value == '+' || value == ',' || value == ';' || value == '=';

        private static BaseParts ParseBaseUri(string baseUri)
        {
            if (string.IsNullOrEmpty(baseUri))
                throw Error("base-uri-required", "构建 Digital Link 必须提供显式 HTTPS 基址。", "baseUri");
            if (!baseUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw Error("base-uri-scheme", "构建 Digital Link 的基址必须使用 HTTPS。", "baseUri");
            if (baseUri.IndexOf('?') >= 0 || baseUri.IndexOf('#') >= 0)
                throw Error("base-uri-query", "基址不能包含查询参数或 fragment。", "baseUri");
            var parts = ParseAbsoluteUri(baseUri);
            var normalizedPath = parts.Path.TrimEnd('/');
            return new BaseParts(parts.Scheme, parts.Authority, normalizedPath);
        }

        private static void AppendQuery(StringBuilder builder, IReadOnlyList<string> query)
        {
            if (query.Count == 0) return;
            builder.Append('?');
            for (var index = 0; index < query.Count; index++)
            {
                if (index > 0) builder.Append('&');
                builder.Append(query[index]);
            }
        }

        private static string Decode(string raw, string path)
        {
            var bytes = new List<byte>();
            var builder = new StringBuilder(raw.Length);
            var index = 0;
            while (index < raw.Length)
            {
                var character = raw[index];
                if (character == '%')
                {
                    if (index + 2 >= raw.Length || !IsHex(raw[index + 1]) || !IsHex(raw[index + 2]))
                        throw Error("invalid-percent-encoding", "URI 包含无效的百分号转义。", path);
                    bytes.Add((byte)((HexValue(raw[index + 1]) << 4) | HexValue(raw[index + 2])));
                    index += 3;
                    continue;
                }
                if (character > '\u007f')
                    throw Error("invalid-uri-character", "未转义的非 ASCII 字符必须进行百分号编码。", path);
                if (bytes.Count > 0)
                {
                    AppendUtf8(builder, bytes, path);
                    bytes.Clear();
                }
                builder.Append(character);
                index++;
            }
            if (bytes.Count > 0) AppendUtf8(builder, bytes, path);
            return builder.ToString();
        }

        private static void AppendUtf8(StringBuilder builder, List<byte> bytes, string path)
        {
            try
            {
                builder.Append(StrictUtf8.GetString(bytes.ToArray()));
            }
            catch (DecoderFallbackException error)
            {
                throw Error("invalid-utf8", "百分号转义必须构成合法 UTF-8。", path, inner: error);
            }
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;
            var bytes = Encoding.UTF8.GetBytes(value);
            var builder = new StringBuilder(bytes.Length);
            foreach (var valueByte in bytes)
            {
                var character = (char)valueByte;
                if (character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9' || character == '-' || character == '_' ||
                    character == '.' || character == '~')
                    builder.Append(character);
                else
                    builder.Append('%').Append(valueByte.ToString("X2", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        private static bool IsHex(char value)
            => value >= '0' && value <= '9' || value >= 'A' && value <= 'F' || value >= 'a' && value <= 'f';

        private static int HexValue(char value)
            => value >= '0' && value <= '9' ? value - '0'
                : value >= 'A' && value <= 'F' ? value - 'A' + 10
                : value - 'a' + 10;

        private static bool IsAllDigits(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (var index = 0; index < value.Length; index++)
                if (value[index] < '0' || value[index] > '9') return false;
            return true;
        }

        private static Gs1DigitalLinkException Error(string code, string message, string fieldPath,
            string ai = null, int? index = null, IReadOnlyList<string> related = null, Exception inner = null)
            => new Gs1DigitalLinkException(code, message, fieldPath, ai, index, related, inner);

        private sealed class BaseParts
        {
            internal BaseParts(string scheme, string authority, string path)
            {
                Scheme = scheme;
                Authority = authority;
                Path = path;
            }

            internal string Scheme { get; }
            internal string Authority { get; }
            internal string Path { get; }
            internal string BaseUri => Scheme + "://" + Authority + Path.TrimEnd('/');
        }
    }

    /// <summary>
    /// GS1 Digital Link 构建请求。
    /// </summary>
    public sealed class Gs1DigitalLinkBuildRequest
    {
        /// <summary>获取或设置显式 HTTPS 基址，例如 <c>https://example.test/gtin</c>。</summary>
        public string BaseUri { get; set; }
        /// <summary>获取或设置 GTIN；支持 8、12、13 或 14 位并按左侧补零。</summary>
        public string Gtin { get; set; }
        /// <summary>获取或设置路径限定符，顺序必须符合 01 的 dlpkey 分支。</summary>
        public IReadOnlyList<Gs1DigitalLinkAiValue> Qualifiers { get; set; } =
            new List<Gs1DigitalLinkAiValue>();
        /// <summary>获取或设置 query 中的 GS1 数据属性。</summary>
        public IReadOnlyList<Gs1DigitalLinkAiValue> DataAttributes { get; set; } =
            new List<Gs1DigitalLinkAiValue>();
        /// <summary>获取或设置按序保留重复键的非 GS1 查询参数。</summary>
        public IReadOnlyList<Gs1DigitalLinkQueryParameter> ExtensionParameters { get; set; } =
            new List<Gs1DigitalLinkQueryParameter>();
        /// <summary>获取或设置原样保留的 fragment；null 表示没有 fragment。</summary>
        public string Fragment { get; set; }
        /// <summary>获取或设置是否执行显式 req/ex 关联校验。</summary>
        public bool ValidateAssociations { get; set; }
    }

    /// <summary>
    /// Digital Link 中的 AI 与原始值。
    /// </summary>
    public sealed class Gs1DigitalLinkAiValue
    {
        /// <summary>
        /// 初始化一个空的 AI 值项。
        /// </summary>
        public Gs1DigitalLinkAiValue() { }

        /// <summary>
        /// 初始化指定 AI 和值的项。
        /// </summary>
        /// <param name="ai">应用标识符。</param>
        /// <param name="value">原始字段值。</param>
        public Gs1DigitalLinkAiValue(string ai, string value)
        {
            Ai = ai;
            Value = value;
        }

        /// <summary>获取或设置应用标识符。</summary>
        public string Ai { get; set; }
        /// <summary>获取或设置原始字段值。</summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// Digital Link 中的非 GS1 查询参数。
    /// </summary>
    public sealed class Gs1DigitalLinkQueryParameter
    {
        /// <summary>
        /// 初始化一个空的查询参数项。
        /// </summary>
        public Gs1DigitalLinkQueryParameter() { }

        /// <summary>
        /// 初始化指定键值的查询参数项。
        /// </summary>
        /// <param name="key">非数字参数名。</param>
        /// <param name="value">参数字符串。</param>
        public Gs1DigitalLinkQueryParameter(string key, string value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>获取或设置参数名。</summary>
        public string Key { get; set; }
        /// <summary>获取或设置参数值。</summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// 解析后的 GS1 Digital Link。
    /// </summary>
    public sealed class Gs1DigitalLinkResult
    {
        private readonly IReadOnlyList<Gs1DigitalLinkAiValue> _qualifiers;
        private readonly IReadOnlyList<Gs1DigitalLinkAiValue> _dataAttributes;
        private readonly IReadOnlyList<Gs1DigitalLinkQueryParameter> _extensionParameters;

        internal Gs1DigitalLinkResult(string originalUri, string scheme, string authority, string pathPrefix,
            string baseUri, string gtin, IReadOnlyList<Gs1DigitalLinkAiValue> qualifiers,
            IReadOnlyList<Gs1DigitalLinkAiValue> dataAttributes,
            IReadOnlyList<Gs1DigitalLinkQueryParameter> extensionParameters,
            IReadOnlyList<Gs1DigitalLinkQueryItem> queryItems, string fragment)
        {
            OriginalUri = originalUri;
            Scheme = scheme;
            Authority = authority;
            PathPrefix = pathPrefix;
            BaseUri = baseUri;
            Gtin = gtin;
            _qualifiers = CloneAiValues(qualifiers);
            _dataAttributes = CloneAiValues(dataAttributes);
            _extensionParameters = CloneQueryParameters(extensionParameters);
            QueryItems = new ReadOnlyCollection<Gs1DigitalLinkQueryItem>(queryItems.ToList());
            Fragment = fragment;
        }

        /// <summary>获取解析前的原始 URI。</summary>
        public string OriginalUri { get; }
        /// <summary>获取 URI scheme。</summary>
        public string Scheme { get; }
        /// <summary>获取主机名及可选端口。</summary>
        public string Authority { get; }
        /// <summary>获取 /01 前的路径前缀；无前缀时为空字符串。</summary>
        public string PathPrefix { get; }
        /// <summary>获取保留域名和路径前缀的基址。</summary>
        public string BaseUri { get; }
        /// <summary>获取 14 位 GTIN。</summary>
        public string Gtin { get; }
        /// <summary>获取路径限定符的独立只读快照。</summary>
        public IReadOnlyList<Gs1DigitalLinkAiValue> Qualifiers => CloneAiValues(_qualifiers);
        /// <summary>获取 query 中 GS1 数据属性的独立只读快照。</summary>
        public IReadOnlyList<Gs1DigitalLinkAiValue> DataAttributes => CloneAiValues(_dataAttributes);
        /// <summary>获取保留顺序和重复键的非 GS1 查询参数独立只读快照。</summary>
        public IReadOnlyList<Gs1DigitalLinkQueryParameter> ExtensionParameters =>
            CloneQueryParameters(_extensionParameters);
        /// <summary>获取按原始 query 顺序排列的查询项。</summary>
        public IReadOnlyList<Gs1DigitalLinkQueryItem> QueryItems { get; }
        /// <summary>获取 fragment；不存在时为 <see langword="null"/>。</summary>
        public string Fragment { get; }

        private static IReadOnlyList<Gs1DigitalLinkAiValue> CloneAiValues(
            IReadOnlyList<Gs1DigitalLinkAiValue> values)
            => new ReadOnlyCollection<Gs1DigitalLinkAiValue>(values
                .Select(item => new Gs1DigitalLinkAiValue(item.Ai, item.Value)).ToList());

        private static IReadOnlyList<Gs1DigitalLinkQueryParameter> CloneQueryParameters(
            IReadOnlyList<Gs1DigitalLinkQueryParameter> values)
            => new ReadOnlyCollection<Gs1DigitalLinkQueryParameter>(values
                .Select(item => new Gs1DigitalLinkQueryParameter(item.Key, item.Value)).ToList());
    }

    /// <summary>
    /// 按 query 顺序保留的 GS1 或非 GS1 查询项。
    /// </summary>
    public sealed class Gs1DigitalLinkQueryItem
    {
        internal Gs1DigitalLinkQueryItem(string ai, string value, string key = null)
        {
            Ai = ai;
            Key = key;
            Value = value;
        }

        /// <summary>获取 GS1 AI；非 GS1 参数时为 <see langword="null"/>。</summary>
        public string Ai { get; }
        /// <summary>获取非 GS1 参数名；GS1 参数时为 <see langword="null"/>。</summary>
        public string Key { get; }
        /// <summary>获取解码后的值。</summary>
        public string Value { get; }
        /// <summary>获取是否为 GS1 参数。</summary>
        public bool IsGs1 => Ai != null;
    }

    /// <summary>
    /// GS1 Digital Link 的结构化诊断。
    /// </summary>
    public sealed class Gs1DigitalLinkDiagnostic
    {
        internal Gs1DigitalLinkDiagnostic(string code, string message, string fieldPath, string ai, int? itemIndex,
            IReadOnlyList<string> relatedAis)
        {
            Code = code;
            Message = message;
            FieldPath = fieldPath;
            Ai = ai;
            ItemIndex = itemIndex;
            RelatedAis = relatedAis ?? Array.AsReadOnly(Array.Empty<string>());
        }

        /// <summary>获取稳定的诊断代码。</summary>
        public string Code { get; }
        /// <summary>获取面向调用者的诊断消息。</summary>
        public string Message { get; }
        /// <summary>获取出错字段路径。</summary>
        public string FieldPath { get; }
        /// <summary>获取相关 AI；不适用时为 <see langword="null"/>。</summary>
        public string Ai { get; }
        /// <summary>获取相关项索引；不适用时为 <see langword="null"/>。</summary>
        public int? ItemIndex { get; }
        /// <summary>获取缺失或冲突的关联 AI。</summary>
        public IReadOnlyList<string> RelatedAis { get; }
    }

    /// <summary>
    /// Digital Link 校验或构建失败；同时保留旧 <see cref="ArgumentException"/> 捕获契约。
    /// </summary>
    public sealed class Gs1DigitalLinkException : ArgumentException
    {
        internal Gs1DigitalLinkException(string code, string message, string fieldPath, string ai, int? itemIndex,
            IReadOnlyList<string> relatedAis, Exception inner) : base(message, fieldPath, inner)
        {
            Code = code;
            FieldPath = fieldPath;
            Ai = ai;
            ItemIndex = itemIndex;
            RelatedAis = relatedAis ?? Array.AsReadOnly(Array.Empty<string>());
        }

        /// <summary>获取稳定的诊断代码。</summary>
        public string Code { get; }
        /// <summary>获取出错字段路径。</summary>
        public string FieldPath { get; }
        /// <summary>获取相关 AI；不适用时为 <see langword="null"/>。</summary>
        public string Ai { get; }
        /// <summary>获取相关项索引；不适用时为 <see langword="null"/>。</summary>
        public int? ItemIndex { get; }
        /// <summary>获取缺失或冲突的关联 AI。</summary>
        public IReadOnlyList<string> RelatedAis { get; }
    }
}
