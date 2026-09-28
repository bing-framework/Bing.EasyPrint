using System;
using System.Collections.Generic;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 校验模板结构及可渲染元素的边界。
/// </summary>
internal static class TemplateGraph
{
    /// <summary>
    /// 校验模板版本、结构、条件和元素约束。
    /// </summary>
    /// <param name="template">待校验模板。</param>
    /// <param name="options">资源及深度限制。</param>
    /// <param name="saving">是否检查 JSON 保存约束。</param>
    /// <param name="cancellation">取消令牌。</param>
    /// <param name="includePaths">是否在结构错误中附带节点路径。</param>
    internal static void Validate(BpTemplate template, TemplateRenderOptions options, bool saving,
        CancellationToken cancellation = default, bool includePaths = false)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (template.Version != 1 && template.Version != 2) throw new ArgumentException("不支持的模板版本。");
        if (template.Width <= 0 || template.Height < 0 || (!template.AutoHeight && template.Height == 0) || template.Height > options.MaxPageHeight)
            throw new ArgumentException("模板宽高无效或超过单页高度限制。");
        Border(template.Border);
        if (template.Color != 0 && template.Color != 1) throw new ArgumentException("模板仅支持白色背景。");
        var stack = new HashSet<object>();
        var count = 0;
        var components = new Dictionary<string, BpComponentDefinition>(StringComparer.Ordinal);
        if (template.Version == 1 && (template.Variables != null || template.Components != null))
            throw new ArgumentException("V1 模板不支持变量或组件。");
        if (template.Variables != null) foreach (var variable in template.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Key) || variable.Key.IndexOf('.') >= 0 || string.IsNullOrWhiteSpace(variable.Value))
                throw new ArgumentException("变量名或字段路径无效。");
        }
        if (template.Components != null) for (var index = 0; index < template.Components.Count; index++)
        {
            var component = template.Components[index];
            At("Components[" + index + "]", () =>
            {
                if (component == null || string.IsNullOrWhiteSpace(component.Id) || component.Width <= 0 || component.Height <= 0 ||
                    components.ContainsKey(component.Id)) throw new ArgumentException("组件标识或尺寸无效，或存在重复标识。");
                components.Add(component.Id, component);
            });
        }
        for (var componentIndex = 0; componentIndex < (template.Components?.Count ?? 0); componentIndex++)
        {
            var component = template.Components[componentIndex];
            var path = "Components[" + componentIndex + "]";
            At(path, () =>
            {
                Enter(component, 1);
                Elements(component.Elements, 2, new HashSet<string>(StringComparer.Ordinal) { component.Id }, path + ".Elements");
                stack.Remove(component);
            });
        }
        Rows(template.Rows, 1, true, "");

        void At(string path, Action action)
        {
            try { action(); }
            catch (ArgumentException error) when (includePaths && !(error is TemplateRenderException))
            { throw new TemplateRenderException(path, error.Message, error); }
        }

        void Enter(object value, int depth)
        {
            cancellation.ThrowIfCancellationRequested();
            if (value == null) throw new ArgumentException("模板节点不能为 null。");
            if (depth > options.MaxDepth) throw new ArgumentException("模板超过嵌套层数限制。");
            if (!stack.Add(value)) throw new ArgumentException("模板存在循环引用。");
        }
        void Condition(BpCondition condition, int depth)
        {
            if (condition == null) return;
            Enter(condition, depth);
            var op = condition.Operator;
            if (op == "and" || op == "or")
            {
                if (condition.Conditions == null || condition.Conditions.Count == 0) throw new ArgumentException("组合条件不能为空。");
                foreach (var child in condition.Conditions) { if (child == null) throw new ArgumentException("条件不能为空。"); Condition(child, depth + 1); }
            }
            else
            {
                if (op != "eq" && op != "ne" && op != "gt" && op != "ge" && op != "lt" && op != "le" && op != "isNull" && op != "notNull")
                    throw new ArgumentException("未知条件运算符。");
                if (string.IsNullOrWhiteSpace(condition.Path)) throw new ArgumentException("比较条件必须提供 Path。");
                if (condition.Conditions != null && condition.Conditions.Count > 0) throw new ArgumentException("比较条件不能包含子条件。");
                if (condition.Value != null && !(condition.Value is string) && !(condition.Value is bool) && !Binding.IsNumber(condition.Value))
                    throw new ArgumentException("条件常量必须为字符串、数字、布尔或 null。");
            }
            stack.Remove(condition);
        }
        void Rows(List<BpRow> rows, int depth, bool top, string prefix)
        {
            if (rows == null) return;
            var bodySeen = false;
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var rowPath = prefix + "Rows[" + rowIndex + "]";
                At(rowPath, () =>
                {
                var row = rows[rowIndex];
                Enter(row, depth);
                if (++count > options.MaxDetailRows) throw new ArgumentException("模板行数超过限制。");
                if (row.Height < 0) throw new ArgumentException("行高度不能为负数。");
                if (template.Version == 1 && row.Section != null) throw new ArgumentException("V1 模板不支持页面分区。");
                if (row.Section != null && (!top || row.IsHeader || (row.Section != "Body" && row.Section != "DocumentHeader" &&
                    row.Section != "PageHeader" && row.Section != "DocumentFooter" && row.Section != "PageFooter" && row.Section != "TableHeader")))
                    throw new ArgumentException("页面分区只能设置在 V2 顶层行。");
                if (row.Section == "TableHeader" && (!string.IsNullOrEmpty(row.RepeatSource) ||
                    rowIndex + 1 >= rows.Count || string.IsNullOrEmpty(rows[rowIndex + 1]?.RepeatSource) ||
                    (rows[rowIndex + 1].Section != null && rows[rowIndex + 1].Section != "Body")))
                    throw new ArgumentException("表格表头必须紧接一行正文重复明细。");
                if (row.IsHeader && (!top || bodySeen || !string.IsNullOrEmpty(row.RepeatSource))) throw new ArgumentException("表头必须是连续的顶层起始行，且不能重复数据源。");
                if ((row.Section == "PageHeader" || row.Section == "PageFooter") && !string.IsNullOrEmpty(row.RepeatSource))
                    throw new ArgumentException("页眉和页脚不能重复数据源。");
                if (!row.IsHeader) bodySeen = true;
                Border(row.Border);
                Condition(row.Condition, 1);
                if (row.Cells != null) for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                {
                    var cellPath = rowPath + ".Cells[" + cellIndex + "]";
                    At(cellPath, () =>
                    {
                    var cell = row.Cells[cellIndex];
                    Enter(cell, depth);
                    if (cell.Width < 0) throw new ArgumentException("单元格宽度不能为负数。");
                    Border(cell.Border);
                    Rows(cell.Rows, depth + 1, false, cellPath + ".");
                    if (cell.ElementAlls != null && cell.ElementAlls.Count > 0)
                        throw new ArgumentException("C# 模板使用 Elements；ElementAlls 仅由 JSON 加载器转换。");
                    Elements(cell.Elements, depth, new HashSet<string>(StringComparer.Ordinal), cellPath + ".Elements");
                    stack.Remove(cell);
                    });
                }
                stack.Remove(row);
                });
            }
        }
        void Elements(List<BpElement> elements, int depth, HashSet<string> references, string prefix)
        {
            if (elements == null) return;
            for (var elementIndex = 0; elementIndex < elements.Count; elementIndex++)
            {
                var elementPath = prefix + "[" + elementIndex + "]";
                At(elementPath, () =>
                {
                var element = elements[elementIndex];
                Enter(element, depth);
                var expected = element is BpFontElement ? 1 : element is BpImageElement ? 2 : element is BpQrCodeElement ? 3 :
                    element is BpBarcodeElement ? 4 : element is BpLineElement ? 5 : element is BpGroupElement ? 6 :
                    element is BpRectangleElement ? 7 : element is BpEllipseElement ? 8 : element is BpIconElement ? 9 :
                    element is BpMatrixCodeElement ? 10 : 0;
                if (expected == 0 || element.Type != expected || (template.Version == 1 && expected > 5))
                    throw new ArgumentException("元素类型与 Type 或模板版本不匹配。");
                if (saving && element.Field?.ValueFunc != null) throw new ArgumentException("含 ValueFunc 的模板不能保存为 JSON。");
                if (element.StartCoordinate?.X < 0 || element.StartCoordinate?.Y < 0) throw new ArgumentException("元素坐标不能为负数。");
                if (element is BpFontElement font && !Enum.IsDefined(typeof(BpTextAlignment), font.TextAlignment))
                    throw new ArgumentException("文字对齐方式无效。");
                if (element is BpLineElement line)
                {
                    var start = line.StartCoordinate;
                    var end = line.LineEndCoordinate;
                    if (start == null || end == null || (end.X == start.X && end.Y == start.Y) || end.X < 0 || end.Y < 0 ||
                        line.LineWidth < 1 || line.LineWidth > 16) throw new ArgumentException("线段坐标或线宽无效。");
                }
                if (element is BpGroupElement group)
                {
                    if (group.Width < 0 || group.Height < 0 || (group.Elements != null && group.ComponentId != null))
                        throw new ArgumentException("组合尺寸无效或同时指定内联元素与组件引用。");
                    if (group.ComponentId != null)
                    {
                        if (!components.TryGetValue(group.ComponentId, out var referenced)) throw new ArgumentException("组件不存在：" + group.ComponentId);
                        if (!references.Add(group.ComponentId)) throw new ArgumentException("组件存在循环引用。");
                        Elements(referenced.Elements, depth + 1, references, elementPath + ".Elements");
                        references.Remove(group.ComponentId);
                    }
                    else Elements(group.Elements, depth + 1, references, elementPath + ".Elements");
                }
                if (element is BpRectangleElement rectangle && (rectangle.Width <= 0 || rectangle.Height <= 0 || rectangle.LineWidth < 1 || rectangle.LineWidth > 16))
                    throw new ArgumentException("矩形尺寸或线宽无效。");
                if (element is BpEllipseElement ellipse && (ellipse.Width <= 0 || ellipse.Height <= 0 || ellipse.LineWidth < 1 || ellipse.LineWidth > 16))
                    throw new ArgumentException("椭圆尺寸或线宽无效。");
                if (element is BpIconElement icon && (icon.Size < 4 || icon.Size > 256 || icon.LineWidth < 1 || icon.LineWidth > 16 ||
                    (icon.Icon != "check" && icon.Icon != "cross" && icon.Icon != "info"))) throw new ArgumentException("图标参数无效。");
                if (element is BpMatrixCodeElement matrix && (matrix.Width < 8 || matrix.Height < 8 ||
                    (long)matrix.Width * matrix.Height > options.MaxImagePixels ||
                    (matrix.Format != "DataMatrix" && matrix.Format != "PDF417"))) throw new ArgumentException("二维点阵码参数无效。");
                var margin = element.Margin;
                if (margin != null && (margin.Top < 0 || margin.Left < 0 || margin.Right < 0 || margin.Bottom < 0)) throw new ArgumentException("元素间距不能为负数。");
                Condition(element.Condition, depth + 1);
                stack.Remove(element);
                });
            }
        }
    }

    /// <summary>
    /// 校验边框颜色和线型是否受打印协议支持。
    /// </summary>
    /// <param name="border">可为空的边框定义。</param>
    internal static void Border(BpBorder border)
    {
        if (border == null) return;
        if (border.Color != 0 && border.Color != 1 && border.Color != 2) throw new ArgumentException("不支持彩色边框。");
        foreach (var style in new[] { border.TopStyle, border.BottomStyle, border.LeftStyle, border.RightStyle })
            if (style < 0 || style > 2) throw new ArgumentException("不支持的边框样式。");
    }
}
