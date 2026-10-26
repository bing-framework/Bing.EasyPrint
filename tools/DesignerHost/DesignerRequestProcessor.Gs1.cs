using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;

internal static partial class DesignerRequestProcessor
{
    /// <summary>
    /// 返回固定字典生成的 GS1 AI 只读目录。
    /// </summary>
    internal static IResult Gs1Catalog()
    {
        var entries = Gs1AiCatalog.Entries.Select(entry => new
        {
            ai = entry.Ai,
            title = entry.Title,
            specification = entry.Specification,
            attributes = entry.Attributes,
            predefinedLength = entry.PredefinedLength,
            requiresSeparator = entry.RequiresSeparator,
            minimumLength = entry.MinimumLength,
            maximumLength = entry.MaximumLength,
            role = entry.DigitalLinkRole.ToString(),
            branches = entry.DigitalLinkBranches,
            components = entry.Components.Select(component => new
            {
                type = component.Type.ToString(),
                minimumLength = component.MinimumLength,
                maximumLength = component.MaximumLength,
                optional = component.Optional,
                linters = component.Linters
            }),
            requirementRules = entry.RequirementRules.Select(rule => new
            {
                kind = rule.Kind,
                raw = rule.Raw,
                alternatives = rule.Alternatives
            }),
            exclusionRules = entry.ExclusionRules.Select(rule => new
            {
                kind = rule.Kind,
                raw = rule.Raw,
                alternatives = rule.Alternatives
            })
        }).ToArray();
        return Results.Json(new
        {
            contract = DesignerContract.Description,
            dictionaryCommit = Gs1AiCatalog.DictionaryCommit,
            entries
        }, WebJson);
    }

    /// <summary>
    /// 使用模板中的静态配置和可选真实数据验证结构化 GS1 来源。
    /// </summary>
    internal static IResult Gs1Validate(DesignerRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Template.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("template 必须为模板 JSON 对象。");
            var options = PrepareOptions(request, cancellationToken);
            var templateJson = request.Template.GetRawText();
            PrintResourceScope.Retain(checked((long)templateJson.Length * 2), "Host.Gs1TemplateModel");
            BpTemplate template;
            try { template = new TemplateLoader().LoadJsonWithPaths(templateJson, options); }
            catch (Exception error) when (error is ArgumentException or Newtonsoft.Json.JsonException)
            {
                throw new DesignerTemplateException(error);
            }

            var hasData = request.Data.ValueKind != JsonValueKind.Undefined &&
                          request.Data.ValueKind != JsonValueKind.Null;
            using var bindingStorage = new PrintStorageBudget();
            Binding binding;
            if (hasData)
            {
                var dataJson = request.Data.GetRawText();
                if (Encoding.UTF8.GetByteCount(dataJson) > MaxDataBytes)
                    throw new ArgumentException("样例数据超过 1 MiB 上限。");
                var raw = BuildRawData(dataJson);
                var legacy = ParseDataToken(dataJson, Newtonsoft.Json.DateParseHandling.DateTime,
                    Newtonsoft.Json.FloatParseHandling.Double).ToObject<object>();
                binding = new Binding(new TemplateDataPair(legacy!, raw!), template.Variables, bindingStorage);
            }
            else binding = new Binding(new object(), template.Variables, bindingStorage);

            var items = new List<object>();
            var errors = new List<object>();
            var pending = false;
            var rowCount = 0;
            void CountRow()
            {
                if (++rowCount > options.MaxDetailRows) throw new ArgumentException("明细行数超过上限。");
            }

            void Field(BpElement element, BgField? field, string path, Binding currentBinding,
                bool requiresInstance = false)
            {
                if (field?.Gs1Input == null) return;
                var input = field.Gs1Input;
                try
                {
                    Gs1StructuredInput.Validate(element, field, path);
                }
                catch (ArgumentException error)
                {
                    errors.Add(Diagnostic(error, path, currentBinding));
                    return;
                }

                var dynamicItem = input.Items.Any(item => item.Name != null);
                if (!hasData && (dynamicItem || requiresInstance))
                {
                    if (!dynamicItem)
                    {
                        try { _ = Gs1StructuredInput.Resolve(element, field, currentBinding); }
                        catch (ArgumentException error)
                        {
                            errors.Add(Diagnostic(error, path, currentBinding));
                            return;
                        }
                    }
                    pending = true;
                    items.Add(new
                    {
                        path,
                        mode = input.Mode.ToString(),
                        status = "pending-data",
                        dynamic = true,
                        instanceIndices = currentBinding.InstanceIndices
                    });
                    return;
                }

                try
                {
                    var output = Gs1StructuredInput.Resolve(element, field, currentBinding);
                    items.Add(new
                    {
                        path,
                        mode = input.Mode.ToString(),
                        status = "valid",
                        output,
                        dynamic = dynamicItem,
                        instanceIndices = currentBinding.InstanceIndices
                    });
                }
                catch (ArgumentException error)
                {
                    errors.Add(Diagnostic(error, path, currentBinding));
                }
            }

            void ValidateConditionConfiguration(BpCondition? condition, string path, Binding currentBinding)
            {
                if (condition == null) return;
                if (condition.Operator is "and" or "or")
                {
                    if (condition.Conditions == null || condition.Conditions.Count == 0)
                    {
                        errors.Add(Diagnostic(new ArgumentException("组合条件必须至少包含一个子条件。"), path, currentBinding));
                        return;
                    }
                    for (var index = 0; index < condition.Conditions.Count; index++)
                        ValidateConditionConfiguration(condition.Conditions[index],
                            path + ".Conditions[" + index + "]", currentBinding);
                    return;
                }

                if (condition.Operator is not ("eq" or "ne" or "gt" or "ge" or "lt" or "le" or "isNull" or "notNull"))
                    errors.Add(Diagnostic(new ArgumentException("未知条件。"), path, currentBinding));
                if (string.IsNullOrWhiteSpace(condition.Path))
                    errors.Add(Diagnostic(new ArgumentException("字段路径不能为空。"), path, currentBinding));
            }

            void StaticElements(IReadOnlyList<BpElement>? elements, string prefix, Binding currentBinding,
                bool requiresInstance)
            {
                if (elements == null) return;
                for (var index = 0; index < elements.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var element = elements[index];
                    if (element == null) continue;
                    var path = prefix + "[" + index + "]";
                    ValidateConditionConfiguration(element.Condition, path + ".Condition", currentBinding);
                    var elementRequiresInstance = requiresInstance || element.Condition != null;
                    Field(element, element.Field, path + ".Field.Gs1Input", currentBinding, elementRequiresInstance);
                    if (element is BpGs1CompositeElement composite)
                        Field(element, composite.SecondaryField, path + ".SecondaryField.Gs1Input",
                            currentBinding, elementRequiresInstance);
                    if (element is not BpGroupElement group) continue;

                    var definition = TemplateBindingTraversal.ComponentDefinition(template, group);
                    if (definition?.Parameters != null)
                    {
                        foreach (var parameter in definition.Parameters)
                        {
                            var supplied = group.Parameters != null && group.Parameters.ContainsKey(parameter.Key);
                            if (!supplied && parameter.Value.DefaultValue == null && parameter.Value.Required)
                                errors.Add(Diagnostic(new ArgumentException("缺少组件参数：" + parameter.Key), path, currentBinding));
                        }
                        if (group.Parameters != null)
                            foreach (var parameter in group.Parameters)
                                if (!definition.Parameters.ContainsKey(parameter.Key))
                                    errors.Add(Diagnostic(new ArgumentException("组件未声明参数：" + parameter.Key), path, currentBinding));
                    }

                    var groupRequiresInstance = elementRequiresInstance || group.DataPath != null ||
                        group.Parameters?.Values.Any(parameter => parameter?.Path != null) == true;
                    StaticElements(definition?.Elements ?? group.Elements, path + ".Elements", currentBinding,
                        groupRequiresInstance);
                    StaticRows(definition?.Rows ?? group.Rows, path + ".", currentBinding, groupRequiresInstance);
                }
            }

            void StaticRows(IReadOnlyList<BpRow>? rows, string prefix, Binding currentBinding,
                bool requiresInstance)
            {
                if (rows == null) return;
                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var row = rows[rowIndex];
                    if (row?.Cells == null) continue;
                    var rowPath = prefix + "Rows[" + rowIndex + "]";
                    try { CountRow(); }
                    catch (ArgumentException error)
                    {
                        errors.Add(Diagnostic(error, rowPath, currentBinding));
                        return;
                    }
                    ValidateConditionConfiguration(row.Condition, rowPath + ".Condition", currentBinding);
                    var rowRequiresInstance = requiresInstance || !string.IsNullOrEmpty(row.RepeatSource) ||
                        row.Condition != null;
                    if (row.RepeatSource != null && string.IsNullOrWhiteSpace(row.RepeatSource))
                        errors.Add(Diagnostic(new ArgumentException("字段路径不能为空。"), rowPath, currentBinding));
                    for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                    {
                        var cell = row.Cells[cellIndex];
                        if (cell == null) continue;
                        var cellPath = rowPath + ".Cells[" + cellIndex + "]";
                        StaticElements(cell.Elements, cellPath + ".Elements", currentBinding, rowRequiresInstance);
                        StaticRows(cell.Rows, cellPath + ".", currentBinding, rowRequiresInstance);
                    }
                }
            }

            void Elements(IReadOnlyList<BpElement>? elements, string prefix, Binding currentBinding)
            {
                if (elements == null) return;
                for (var index = 0; index < elements.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var element = elements[index];
                    if (element == null) continue;
                    var path = prefix + "[" + index + "]";
                    bool matches;
                    try { matches = currentBinding.Matches(element.Condition); }
                    catch (ArgumentException error)
                    {
                        errors.Add(Diagnostic(error, path, currentBinding));
                        continue;
                    }
                    if (!matches) continue;
                    Field(element, element.Field, path + ".Field.Gs1Input", currentBinding);
                    if (element is BpGs1CompositeElement composite)
                        Field(element, composite.SecondaryField, path + ".SecondaryField.Gs1Input", currentBinding);
                    if (element is BpGroupElement group)
                    {
                        var definition = TemplateBindingTraversal.ComponentDefinition(template, group);
                        var groupBinding = currentBinding;
                        if (template.Version == 3)
                        {
                            try { groupBinding = TemplateBindingTraversal.BindComponent(template, group, currentBinding, bindingStorage); }
                            catch (ArgumentException error)
                            {
                                errors.Add(Diagnostic(error, path, currentBinding));
                                continue;
                            }
                        }
                        Elements(definition?.Elements ?? group.Elements, path + ".Elements", groupBinding);
                        Rows(definition?.Rows ?? group.Rows, path + ".", groupBinding);
                    }
                }
            }

            void Rows(IReadOnlyList<BpRow>? rows, string prefix, Binding currentBinding)
            {
                if (rows == null) return;
                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var row = rows[rowIndex];
                    if (row?.Cells == null) continue;
                    var rowPath = prefix + "Rows[" + rowIndex + "]";
                    try
                    {
                        foreach (var rowBinding in TemplateBindingTraversal.ExpandRowBindings(
                                     row, currentBinding, cancellationToken, CountRow))
                        {
                            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                            {
                                var cell = row.Cells[cellIndex];
                                if (cell == null) continue;
                                var cellPath = rowPath + ".Cells[" + cellIndex + "]";
                                Elements(cell.Elements, cellPath + ".Elements", rowBinding);
                                Rows(cell.Rows, cellPath + ".", rowBinding);
                            }
                        }
                    }
                    catch (ArgumentException error)
                    {
                        errors.Add(Diagnostic(error, rowPath, currentBinding));
                    }
                }
            }

            if (hasData) Rows(template.Rows, string.Empty, binding);
            else StaticRows(template.Rows, string.Empty, binding, requiresInstance: false);

            var status = errors.Count > 0 ? "invalid" : pending ? "pending" : "valid";
            return Results.Json(new
            {
                contract = DesignerContract.Description,
                dictionaryCommit = Gs1AiCatalog.DictionaryCommit,
                status,
                dataProvided = hasData,
                items,
                errors
            }, WebJson);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Error(error); }
    }

    /// <summary>
    /// 在 Elements、Digital Link 和保留的非 GS1 查询参数之间转换。
    /// </summary>
    internal static IResult Gs1Convert(DesignerRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var conversion = request.Gs1Conversion;
        var inputText = conversion?.Input;
        try
        {
            if (conversion == null)
                throw new ArgumentException("缺少 gs1Conversion 请求。");
            if (string.IsNullOrWhiteSpace(inputText))
                throw new ArgumentException("gs1Conversion.input 不能为空。");
            var trimmed = inputText!.Trim();
            var target = ParseTarget(conversion.TargetFormat);
            var sourceElements = trimmed[0] == '[' || trimmed[0] == '(';
            string output;
            object structuredInput;
            if (sourceElements)
            {
                var elements = Gs1ApplicationIdentifierParser.ParseElements(trimmed, "gs1Conversion.input");
                output = target == BgGs1InputMode.Elements
                    ? FormatElements(elements)
                    : BuildDigitalLinkFromElements(elements, conversion, cancellationToken);
                structuredInput = CreateStructuredInput(target, elements,
                    target == BgGs1InputMode.DigitalLink ? conversion.BaseUri : null,
                    target == BgGs1InputMode.DigitalLink ? conversion.Fragment : null,
                    target == BgGs1InputMode.DigitalLink ? conversion.QueryParameters : null,
                    conversion.ValidateAssociations);
            }
            else
            {
                var parsed = Gs1DigitalLink.Parse(trimmed, conversion.ValidateAssociations);
                output = target == BgGs1InputMode.Elements
                    ? FormatElementsFromDigitalLink(parsed)
                    : Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
                    {
                        BaseUri = string.IsNullOrWhiteSpace(conversion.BaseUri)
                            ? parsed.BaseUri : conversion.BaseUri!,
                        Gtin = parsed.Gtin,
                        Qualifiers = parsed.Qualifiers,
                        DataAttributes = parsed.DataAttributes,
                        ExtensionParameters = conversion.QueryParameters == null
                            ? parsed.ExtensionParameters
                            : ToQueryParameters(conversion.QueryParameters),
                        Fragment = conversion.Fragment ?? parsed.Fragment,
                        ValidateAssociations = conversion.ValidateAssociations
                    });
                structuredInput = target == BgGs1InputMode.Elements
                    ? CreateStructuredInput(BgGs1InputMode.Elements, ToElements(parsed), null, null, null,
                        conversion.ValidateAssociations)
                    : CreateStructuredInput(BgGs1InputMode.DigitalLink, ToElements(parsed),
                        string.IsNullOrWhiteSpace(conversion.BaseUri) ? parsed.BaseUri : conversion.BaseUri,
                        conversion.Fragment ?? parsed.Fragment,
                        conversion.QueryParameters ?? parsed.ExtensionParameters.Select(item =>
                            new DesignerGs1QueryParameter { Key = item.Key, Value = item.Value }).ToArray(),
                        conversion.ValidateAssociations, GetPlacements(parsed));
            }

            return Results.Json(new
            {
                contract = DesignerContract.Description,
                status = "valid",
                sourceFormat = sourceElements ? "elements" : "digital-link",
                targetFormat = target == BgGs1InputMode.Elements ? "elements" : "digital-link",
                output,
                structuredInput
            }, WebJson);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            return Results.Json(new
            {
                contract = DesignerContract.Description,
                status = "invalid",
                input = inputText,
                errors = new[] { Diagnostic(error, "gs1Conversion.input") }
            }, WebJson, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static BgGs1InputMode ParseTarget(string? target)
    {
        if (string.Equals(target, "elements", StringComparison.OrdinalIgnoreCase)) return BgGs1InputMode.Elements;
        if (string.Equals(target, "digital-link", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, "digitallink", StringComparison.OrdinalIgnoreCase))
            return BgGs1InputMode.DigitalLink;
        throw new ArgumentException("gs1Conversion.targetFormat 必须为 elements 或 digital-link。");
    }

    private static string BuildDigitalLinkFromElements(IReadOnlyList<Gs1Element> elements,
        DesignerGs1ConversionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (elements.Count == 0 || elements[0].Ai != "01")
            throw new ArgumentException("Elements 转换 Digital Link 时 01 必须为第一个元素。");
        if (string.IsNullOrWhiteSpace(request.BaseUri))
            throw new ArgumentException("Elements 转换 Digital Link 时必须提供 HTTPS baseUri。");
        var qualifiers = new List<Gs1DigitalLinkAiValue>();
        var dataAttributes = new List<Gs1DigitalLinkAiValue>();
        for (var index = 1; index < elements.Count; index++)
        {
            var element = elements[index];
            var value = new Gs1DigitalLinkAiValue(element.Ai, element.Value);
            if (IsQualifier(element.Ai)) qualifiers.Add(value);
            else dataAttributes.Add(value);
        }
        return Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
        {
            BaseUri = request.BaseUri!,
            Gtin = elements[0].Value,
            Qualifiers = qualifiers,
            DataAttributes = dataAttributes,
            ExtensionParameters = ToQueryParameters(request.QueryParameters),
            Fragment = request.Fragment,
            ValidateAssociations = request.ValidateAssociations
        });
    }

    private static object CreateStructuredInput(BgGs1InputMode mode, IReadOnlyList<Gs1Element> elements,
        string? baseUri, string? fragment, IReadOnlyList<DesignerGs1QueryParameter>? queryParameters,
        bool validateAssociations,
        IReadOnlyDictionary<string, BgGs1InputPlacement>? placements = null)
        => new
        {
            mode = (int)mode,
            baseUri,
            fragment,
            validateAssociations,
            items = elements.Select(item => new
            {
                ai = item.Ai,
                value = item.Value,
                placement = (int)(placements != null && placements.TryGetValue(item.Ai, out var placement)
                    ? placement : BgGs1InputPlacement.Auto)
            }).ToArray(),
            queryParameters = (queryParameters ?? Array.Empty<DesignerGs1QueryParameter>())
                .Select(item => new { key = item.Key, value = item.Value }).ToArray()
        };

    private static IReadOnlyDictionary<string, BgGs1InputPlacement> GetPlacements(Gs1DigitalLinkResult parsed)
    {
        var placements = new Dictionary<string, BgGs1InputPlacement>(StringComparer.Ordinal);
        foreach (var item in parsed.Qualifiers)
            placements.Add(item.Ai, BgGs1InputPlacement.Qualifier);
        foreach (var item in parsed.DataAttributes)
            placements.Add(item.Ai, BgGs1InputPlacement.DataAttribute);
        return placements;
    }

    private static IReadOnlyList<Gs1Element> ToElements(Gs1DigitalLinkResult parsed)
    {
        var elements = new List<Gs1Element> { new("01", parsed.Gtin) };
        elements.AddRange(parsed.Qualifiers.Select(item => new Gs1Element(item.Ai, item.Value)));
        elements.AddRange(parsed.DataAttributes.Select(item => new Gs1Element(item.Ai, item.Value)));
        return elements;
    }

    private static string FormatElementsFromDigitalLink(Gs1DigitalLinkResult parsed)
    {
        if (parsed.ExtensionParameters.Count > 0)
            throw new ArgumentException("Digital Link 的非 GS1 查询参数无法用 Elements 表达，拒绝静默删除。");
        if (parsed.Fragment != null)
            throw new ArgumentException("Digital Link 的 fragment 无法用 Elements 表达，拒绝静默删除。");
        var elements = new List<Gs1Element> { new Gs1Element("01", parsed.Gtin) };
        elements.AddRange(parsed.Qualifiers.Select(item => new Gs1Element(item.Ai, item.Value)));
        elements.AddRange(parsed.DataAttributes.Select(item => new Gs1Element(item.Ai, item.Value)));
        return FormatElements(elements);
    }

    private static string FormatElements(IReadOnlyList<Gs1Element> elements)
    {
        var builder = new StringBuilder();
        foreach (var element in elements)
            builder.Append('[').Append(element.Ai).Append(']')
                .Append(element.Value.Replace("\\", "\\\\").Replace("(", "\\("));
        return builder.ToString();
    }

    private static IReadOnlyList<Gs1DigitalLinkQueryParameter> ToQueryParameters(DesignerGs1QueryParameter[]? query)
    {
        if (query == null) return Array.Empty<Gs1DigitalLinkQueryParameter>();
        var result = new List<Gs1DigitalLinkQueryParameter>();
        foreach (var item in query)
            result.Add(new Gs1DigitalLinkQueryParameter(item?.Key ?? string.Empty, item?.Value ?? string.Empty));
        return result;
    }

    private static bool IsQualifier(string ai)
        => Gs1AiCatalog.TryGet(ai, out var entry) &&
           entry.DigitalLinkRole == Gs1AiDigitalLinkRole.KeyQualifier;

    private static object Diagnostic(Exception error, string path, Binding? binding = null)
    {
        var gs1 = error as Gs1InputException ?? error.InnerException as Gs1InputException;
        var digital = error as Gs1DigitalLinkException ?? error.InnerException as Gs1DigitalLinkException;
        return new
        {
            sourcePath = path,
            code = digital?.Code ?? "Gs1InputInvalid",
            fieldPath = gs1?.FieldPath ?? digital?.FieldPath ?? TemplateBindingDiagnostic.GetFieldPath(error),
            ai = gs1?.Ai ?? digital?.Ai,
            itemIndex = gs1?.ItemIndex ?? digital?.ItemIndex,
            relatedAis = gs1?.RelatedAis ?? digital?.RelatedAis,
            instanceIndices = binding?.InstanceIndices,
            message = error.Message
        };
    }
}
