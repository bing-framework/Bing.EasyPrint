using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;

namespace Bing.EasyPrint.Templates
{
    /// <summary>
    /// 将共享字段的可选 GS1 结构化来源解析为 Elements 元素串或 Digital Link。
    /// </summary>
    internal static class Gs1StructuredInput
    {
        private const string FieldPath = "Field.Gs1Input";

        internal static string Resolve(BpElement element, Binding binding)
            => Resolve(element, element?.Field, binding);

        internal static string Resolve(BpElement element, BgField field, Binding binding)
        {
            if (field?.Gs1Input == null) return binding.Text(field);
            Validate(element, field, FieldPath);
            var input = field.Gs1Input;
            if (input.Mode == BgGs1InputMode.Elements)
                return ResolveElements(element, field, binding);
            if (input.Mode == BgGs1InputMode.DigitalLink)
                return ResolveDigitalLink(element, field, binding);
            throw Error("GS1 结构化来源 Mode 无效。", FieldPath + ".Mode");
        }

        internal static void Validate(BpElement element, BgField field, string fieldPath)
        {
            var input = field?.Gs1Input;
            if (input == null) return;
            if (!string.IsNullOrEmpty(field.Name) || field.Value != null || field.ValueFunc != null)
                throw Error("Gs1Input 不能与 Name、Value 或 ValueFunc 同时设置。", fieldPath);
            if (!Enum.IsDefined(typeof(BgGs1InputMode), input.Mode))
                throw Error("GS1 结构化来源 Mode 无效。", fieldPath + ".Mode");
            EnsureSupported(element, input.Mode, fieldPath);

            var items = input.Items;
            if (items == null || items.Count == 0)
                throw Error("GS1 结构化来源至少需要一个 AI 项。", fieldPath + ".Items");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var primaryCount = 0;
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var itemPath = fieldPath + ".Items[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (item == null) throw Error("GS1 AI 项不能为空。", itemPath);
                ValidateAi(item.Ai, itemPath + ".Ai", index);
                if (!seen.Add(item.Ai))
                    throw Error("GS1 结构化来源不能重复使用 AI " + item.Ai + "。", itemPath + ".Ai", item.Ai, index);
                var hasName = item.Name != null;
                var hasValue = item.Value != null;
                if (hasName == hasValue)
                    throw Error("GS1 AI 项的 Value 与 Name 必须恰选一个。", itemPath);
                if (hasName && item.Name.Length == 0)
                    throw Error("GS1 AI 项的字段路径不能为空。", itemPath + ".Name");
                if (!Enum.IsDefined(typeof(BgGs1InputPlacement), item.Placement))
                    throw Error("GS1 AI 项的 Placement 无效。", itemPath + ".Placement");

                if (item.Ai == "01")
                {
                    primaryCount++;
                    if (item.Placement != BgGs1InputPlacement.Auto)
                        throw Error("01 主键不能指定 Qualifier 或 DataAttribute 位置。", itemPath + ".Placement");
                }

                if (input.Mode == BgGs1InputMode.Elements)
                {
                    if (item.Placement != BgGs1InputPlacement.Auto)
                        throw Error("Elements 模式的 AI 项 Placement 必须为 Auto。", itemPath + ".Placement");
                    if (hasValue) ValidateStaticValue(item.Ai, item.Value, itemPath + ".Value", false, index);
                }
                else
                {
                    ValidateDigitalLinkPlacement(item, itemPath);
                    if (Gs1AiCatalog.TryGet(item.Ai, out var entry) &&
                        entry.DigitalLinkRole == Gs1AiDigitalLinkRole.Unsupported)
                        throw Error("GS1 AI " + item.Ai + " 不允许用于 GS1 Digital Link URI Syntax。",
                            itemPath + ".Ai", item.Ai, index);
                    if (hasValue) ValidateStaticValue(item.Ai, item.Value, itemPath + ".Value", true, index);
                }
            }

            if (input.Mode == BgGs1InputMode.Elements)
            {
                if (!string.IsNullOrEmpty(input.BaseUri) || input.Fragment != null ||
                    input.QueryParameters != null && input.QueryParameters.Count > 0)
                    throw Error("Elements 模式不能表达 BaseUri、Fragment 或非 GS1 QueryParameters。", fieldPath);
            }
            else
            {
                if (string.IsNullOrEmpty(input.BaseUri))
                    throw Error("DigitalLink 模式必须设置 BaseUri。", fieldPath + ".BaseUri");
                if (primaryCount != 1)
                    throw Error("DigitalLink 模式必须恰有一个 AI 01 主键项。", fieldPath + ".Items");
                try
                {
                    Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
                    {
                        BaseUri = input.BaseUri,
                        Gtin = "09520123456788",
                        ExtensionParameters = (input.QueryParameters ?? new List<BgGs1InputQueryParameter>())
                            .Select(query => query == null
                                ? null
                                : new Gs1DigitalLinkQueryParameter(query.Key, query.Value)).ToList(),
                        Fragment = input.Fragment
                    });
                }
                catch (Gs1DigitalLinkException error)
                {
                    var suffix = error.FieldPath.StartsWith("extensionParameters[", StringComparison.Ordinal)
                        ? ".QueryParameters[" + (error.ItemIndex ?? 0).ToString(CultureInfo.InvariantCulture) + "]"
                        : error.FieldPath.StartsWith("fragment", StringComparison.Ordinal)
                            ? ".Fragment" : ".BaseUri";
                    throw Error(error.Message, fieldPath + suffix, error.Ai, error.ItemIndex, error.RelatedAis, error);
                }
            }
        }

        private static string ResolveElements(BpElement element, BgField field, Binding binding)
        {
            var input = field.Gs1Input;
            var elements = new List<Gs1Element>();
            for (var index = 0; index < input.Items.Count; index++)
            {
                var item = input.Items[index];
                var value = ResolveItem(binding, item, index, "Field.Gs1Input.Items");
                ValidateValue(item.Ai, value, "Field.Gs1Input.Items[" + index + "].Value", index);
                elements.Add(new Gs1Element(item.Ai, value));
            }

            if (input.ValidateAssociations)
            {
                try
                {
                    Gs1AssociationValidator.ValidateElements(elements);
                }
                catch (ArgumentException error)
                {
                    throw Error(error.Message, FieldPath, inner: error);
                }
            }

            var result = new System.Text.StringBuilder();
            foreach (var item in elements)
            {
                var value = item.Value.Replace("\\", "\\\\").Replace("(", "\\(");
                result.Append('[').Append(item.Ai).Append(']').Append(value);
            }
            return result.ToString();
        }

        private static string ResolveDigitalLink(BpElement element, BgField field, Binding binding)
        {
            var input = field.Gs1Input;
            var values = new List<Gs1DigitalLinkAiValue>();
            for (var index = 0; index < input.Items.Count; index++)
            {
                var item = input.Items[index];
                var value = ResolveItem(binding, item, index, "Field.Gs1Input.Items");
                values.Add(new Gs1DigitalLinkAiValue(item.Ai, value));
            }

            var primary = values.First(item => item.Ai == "01");
            var qualifiers = new List<Gs1DigitalLinkAiValue>();
            var dataAttributes = new List<Gs1DigitalLinkAiValue>();
            for (var index = 0; index < input.Items.Count; index++)
            {
                var item = input.Items[index];
                if (item.Ai == "01") continue;
                var placement = item.Placement;
                if (placement == BgGs1InputPlacement.Auto)
                    placement = IsQualifier(item.Ai) ? BgGs1InputPlacement.Qualifier : BgGs1InputPlacement.DataAttribute;
                if (placement == BgGs1InputPlacement.Qualifier)
                    qualifiers.Add(values[index]);
                else
                    dataAttributes.Add(values[index]);
            }

            var query = new List<Gs1DigitalLinkQueryParameter>();
            if (input.QueryParameters != null)
                foreach (var parameter in input.QueryParameters)
                    query.Add(new Gs1DigitalLinkQueryParameter(parameter.Key, parameter.Value ?? string.Empty));

            try
            {
                return Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
                {
                    BaseUri = input.BaseUri,
                    Gtin = primary.Value,
                    Qualifiers = qualifiers,
                    DataAttributes = dataAttributes,
                    ExtensionParameters = query,
                    Fragment = input.Fragment,
                    ValidateAssociations = input.ValidateAssociations
                });
            }
            catch (Gs1DigitalLinkException error)
            {
                var sourceAi = error.Ai ?? (error.FieldPath == "gtin" ? "01" : null);
                var sourceIndex = sourceAi == null ? (int?)null :
                    input.Items.FindIndex(item => item != null && item.Ai == sourceAi);
                var queryIndex = error.FieldPath.StartsWith("extensionParameters[", StringComparison.Ordinal)
                    ? error.ItemIndex : null;
                var itemIndex = sourceIndex.HasValue && sourceIndex.Value >= 0 ? sourceIndex : error.ItemIndex;
                var suffix = queryIndex.HasValue
                    ? ".QueryParameters[" + queryIndex.Value.ToString(CultureInfo.InvariantCulture) + "]"
                    : itemIndex.HasValue
                        ? ".Items[" + itemIndex.Value.ToString(CultureInfo.InvariantCulture) + "]"
                        : string.Empty;
                throw Error(error.Message, FieldPath + suffix, sourceAi, itemIndex, error.RelatedAis, error);
            }
        }

        private static string ResolveItem(Binding binding, BgGs1InputItem item, int index, string path)
        {
            var temporary = new BgField
            {
                Name = item.Name,
                Value = item.Value,
                Format = item.Format
            };
            try
            {
                return binding.Text(temporary);
            }
            catch (BgFieldFormatException)
            {
                throw;
            }
            catch (ArgumentException error)
            {
                throw Error("AI " + item.Ai + " 的绑定值无效：" + error.Message, path + "[" + index + "]", item.Ai, index,
                    inner: error);
            }
        }

        private static void ValidateDigitalLinkPlacement(BgGs1InputItem item, string itemPath)
        {
            if (item.Placement == BgGs1InputPlacement.Qualifier && !IsQualifier(item.Ai))
                throw Error("AI " + item.Ai + " 不是 01 的 Digital Link 限定符。", itemPath + ".Placement", item.Ai);
            if (item.Placement != BgGs1InputPlacement.Auto && item.Placement != BgGs1InputPlacement.Qualifier &&
                item.Placement != BgGs1InputPlacement.DataAttribute)
                throw Error("GS1 AI 项的 Placement 无效。", itemPath + ".Placement", item.Ai);
        }

        private static bool IsQualifier(string ai)
        {
            return Gs1AiCatalog.TryGet(ai, out var entry) &&
                   entry.DigitalLinkRole == Gs1AiDigitalLinkRole.KeyQualifier;
        }

        private static void ValidateAi(string ai, string path, int index)
        {
            if (string.IsNullOrEmpty(ai) || ai.Length < 2 || ai.Length > 4 || ai.Any(character => character < '0' || character > '9'))
                throw Error("GS1 AI 必须为 2 到 4 位 ASCII 数字。", path, ai, index);
            if (!Gs1AiCatalog.TryGet(ai, out _))
                throw Error("GS1 AI " + ai + " 不在固定字典中。", path, ai, index);
        }

        private static void ValidateStaticValue(string ai, string value, string path, bool digitalLink, int index)
        {
            if (digitalLink && ai == "01")
            {
                try
                {
                    Gs1DigitalLink.NormalizeGtin(value);
                    return;
                }
                catch (Gs1DigitalLinkException error)
                {
                    throw Error(error.Message, path, ai, index, inner: error);
                }
            }
            ValidateValue(ai, value, path, index);
        }

        private static void ValidateValue(string ai, string value, string path, int index)
        {
            try
            {
                Gs1SyntaxDictionary.Validate(ai, value, path);
            }
            catch (ArgumentException error)
            {
                throw Error(error.Message, path, ai, index, inner: error);
            }
        }

        private static void EnsureSupported(BpElement element, BgGs1InputMode mode, string path)
        {
            if (element is BpQrCodeElement qr)
            {
                if (mode == BgGs1InputMode.Elements && !qr.Gs1)
                    throw Error("Elements 结构化 GS1 来源要求二维码启用 Gs1。", path);
                if (mode == BgGs1InputMode.DigitalLink && qr.Gs1)
                    throw Error("DigitalLink 必须使用普通二维码（Gs1=false）。", path);
                return;
            }
            if (element is BpMatrixCodeElement matrix)
            {
                if (!string.Equals(matrix.Format, "DataMatrix", StringComparison.Ordinal))
                    throw Error("GS1 结构化来源只支持 DataMatrix，PDF417 明确拒绝。", path);
                if (mode == BgGs1InputMode.Elements && !matrix.Gs1)
                    throw Error("Elements 结构化 GS1 来源要求 DataMatrix 启用 Gs1。", path);
                if (mode == BgGs1InputMode.DigitalLink && matrix.Gs1)
                    throw Error("DigitalLink 必须使用普通 DataMatrix（Gs1=false）。", path);
                return;
            }
            if (element is BpGs1CompositeElement)
            {
                if (mode == BgGs1InputMode.DigitalLink)
                    throw Error("Composite 不支持 DigitalLink，只能使用 Elements。", path);
                return;
            }
            if (element is BpBarcodeElement barcode)
            {
                if (mode == BgGs1InputMode.DigitalLink)
                    throw Error("一维条码不支持 DigitalLink。", path);
                if (!SupportsGs1Barcode(barcode.BarcodeType))
                    throw Error("此一维条码制式不支持 GS1 结构化来源。", path);
                return;
            }
            throw Error("此元素不支持 GS1 结构化来源。", path);
        }

        private static bool SupportsGs1Barcode(BarcodeType type)
        {
            switch (type)
            {
                case BarcodeType.UccEan128:
                case BarcodeType.Gs1DataBarOmnidirectional:
                case BarcodeType.Gs1DataBarTruncated:
                case BarcodeType.Gs1DataBarLimited:
                case BarcodeType.Gs1DataBarExpanded:
                case BarcodeType.Gs1DataBarStacked:
                case BarcodeType.Gs1DataBarExpandedStacked:
                case BarcodeType.Gs1DataBarStackedOmnidirectional:
                    return true;
                default:
                    return false;
            }
        }

        private static ArgumentException Error(string message, string fieldPath, string ai = null, int? index = null,
            IReadOnlyList<string> related = null, Exception inner = null)
            => new Gs1InputException(message, fieldPath, ai, index, related, inner);
    }

    /// <summary>
    /// 模板结构化 GS1 来源的领域错误；保留旧 ArgumentException 捕获契约。
    /// </summary>
    internal sealed class Gs1InputException : ArgumentException
    {
        internal Gs1InputException(string message, string fieldPath, string ai, int? itemIndex,
            IReadOnlyList<string> relatedAis, Exception inner) : base(message, fieldPath, inner)
        {
            FieldPath = fieldPath;
            Ai = ai;
            ItemIndex = itemIndex;
            RelatedAis = relatedAis ?? Array.Empty<string>();
        }

        internal string FieldPath { get; }
        internal string Ai { get; }
        internal int? ItemIndex { get; }
        internal IReadOnlyList<string> RelatedAis { get; }
    }
}
