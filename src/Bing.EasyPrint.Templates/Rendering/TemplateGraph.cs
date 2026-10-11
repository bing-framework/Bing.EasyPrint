using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;

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
    /// <param name="checkProtocol">是否同时检查无需绑定数据即可确定的协议能力。</param>
    internal static void Validate(BpTemplate template, TemplateRenderOptions options, bool saving,
        CancellationToken cancellation = default, bool includePaths = false, bool checkProtocol = false)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (template.Version != 1 && template.Version != 2 && template.Version != 3) throw new ArgumentException("不支持的模板版本。");
        if (template.Width <= 0 || template.Height < 0 || (!template.AutoHeight && template.Height == 0) || template.Height > options.MaxPageHeight)
            throw new ArgumentException("模板宽高无效或超过单页高度限制。");
        var capabilities = checkProtocol ? TemplateProtocolCapabilities.Create(options) : null;
        if (checkProtocol && capabilities.DeviceProfileName != null &&
            (capabilities.MaxPrintableWidthDots is int maxWidth && template.Width > maxWidth ||
             !template.AutoHeight && capabilities.MaxLabelHeightDots is int maxHeight && template.Height > maxHeight))
            Capability("Template", "目标机型的页面尺寸上限。");
        ValidateBorder(template.Border, "Template");
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
                if (component == null || string.IsNullOrWhiteSpace(component.Id) || component.Width < 0 || component.Height < 0 ||
                    (template.Version < 3 && (component.Width == 0 || component.Height == 0 || component.Rows != null || component.Parameters != null)) ||
                    (component.Rows != null && component.Elements != null) ||
                    components.ContainsKey(component.Id)) throw new ArgumentException("组件标识或尺寸无效，或存在重复标识。");
                if (component.Parameters != null) foreach (var parameter in component.Parameters)
                    if (string.IsNullOrWhiteSpace(parameter.Key) || parameter.Key.IndexOf('.') >= 0 || parameter.Value == null)
                        throw new ArgumentException("组件参数定义无效。");
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
                Rows(component.Rows, 2, false, path + ".", new HashSet<string>(StringComparer.Ordinal) { component.Id });
                stack.Remove(component);
            });
        }
        Rows(template.Rows, 1, true, "");

        void At(string path, Action action)
        {
            try { action(); }
            catch (TemplateRenderException error) when (includePaths &&
                error.SourcePath?.StartsWith("Components[", StringComparison.Ordinal) == true)
            { throw error.WithOwnership(error.SourcePath, null, null); }
            catch (ArgumentException error) when (includePaths && !(error is TemplateRenderException))
            { throw new TemplateRenderException(path, error.Message, error,
                path?.StartsWith("Components[", StringComparison.Ordinal) == true ? path : null, null, null); }
        }
        void Capability(string path, string reason)
        {
            var message = $"协议 {options.Protocol} 不支持 {reason}";
            throw new TemplateRenderException(path, message, new NotSupportedException(message));
        }
        void ValidateBorder(BpBorder border, string path)
        {
            Border(border);
            if (checkProtocol && capabilities.DeviceProfileName != null && !capabilities.SupportsElementType(5) &&
                border != null && (border.TopStyle != 0 || border.BottomStyle != 0 || border.LeftStyle != 0 || border.RightStyle != 0))
                Capability(path, "目标机型的边框绘图。");
            if (checkProtocol && options.Protocol == PrintProtocol.EscPos &&
                options.EscPosLayout == EscPosLayoutMode.Native && border != null &&
                (border.TopStyle != 0 || border.BottomStyle != 0 ||
                 border.LeftStyle != 0 || border.RightStyle != 0))
                Capability(path, "Native 模式的边框。");
        }
        void ValidateProtocolElement(BpElement element, string path)
        {
            if (!checkProtocol) return;
            if (capabilities.DeviceProfileName != null && !capabilities.SupportsElementType(element.Type))
                Capability(path, "目标机型的元素类型。");
            if (capabilities.DeviceProfileName != null && !capabilities.SupportsElementType(2) &&
                (element is BpEllipseElement || element is BpIconElement bitmapIcon && bitmapIcon.Icon != "info" ||
                 element is BpLineElement diagonal && diagonal.StartCoordinate != null && diagonal.LineEndCoordinate != null &&
                 diagonal.StartCoordinate.X != diagonal.LineEndCoordinate.X && diagonal.StartCoordinate.Y != diagonal.LineEndCoordinate.Y))
                Capability(path, "目标机型的位图绘图；水平/垂直线和 info 图标仍可使用原生命令。");
            var native = options.Protocol == PrintProtocol.EscPos &&
                         options.EscPosLayout == EscPosLayoutMode.Native;
            if (element is BpRectangleElement filledRectangle && filledRectangle.Filled &&
                !capabilities.SupportsFilledRectangles)
                Capability(path, "此模式的填充矩形。");
            if (element is BpRectangleElement zplFill && zplFill.Filled && options.Protocol == PrintProtocol.Zpl &&
                (zplFill.Width > 32000 || zplFill.Height > 32000))
                throw new TemplateRenderException(path, "协议 Zpl 的填充矩形宽高不能超过 32000 打印点。",
                    new ArgumentOutOfRangeException(nameof(zplFill), "填充矩形宽高超出原生命令范围。"));
            var quietZone = element is BpQrCodeElement quietQr ? quietQr.QuietZone :
                element is BpBarcodeElement quietBarcode ? quietBarcode.QuietZone : null;
            if (quietZone != null && !quietZone.IsEmpty() && !capabilities.SupportsExplicitQuietZone)
                Capability(path, "Native 模式的非零静区；请使用 Raster 模式。");
            if (native && !capabilities.SupportsElementType(element.Type) && element.Type != 3 && element.Type != 4)
                Capability(path, "Native 模式的坐标图形或图片；请使用 Raster 模式。");
            if (element is BpImageElement rotatedImage && rotatedImage.Rotation != RotationAngle.None &&
                !capabilities.SupportsImageRotation(rotatedImage.Rotation))
                Capability(path, "此模式的图片旋转。");
            if (element is BpFontElement font)
            {
                ResolvedTemplateFont resolvedFont;
                try { resolvedFont = TemplateFontResolver.Resolve(font, options.Protocol, options.EscPosLayout); }
                catch (NotSupportedException error) { Capability(path, error.Message); return; }
                catch (ArgumentException error) when (font.Font != null)
                { throw new TemplateRenderException(path, $"协议 {options.Protocol} 的语义字体配置无效：{error.Message}", error); }
                if (capabilities.DeviceProfileName != null && !capabilities.DeviceFonts.Any(deviceFont =>
                    deviceFont.Name == (resolvedFont.DeviceName ?? "0")))
                    Capability(path, $"目标机型的设备字体 {resolvedFont.DeviceName ?? "0"}。");
                if ((font.FontStyle & ~5) != 0 && includePaths)
                    throw new TemplateRenderException(path,
                        $"协议 {options.Protocol} 不支持未知文字样式位。",
                        new ArgumentException("模板文字只支持普通、粗体、下划线。"));
                if (font.Rotation != RotationAngle.None && (options.Protocol == PrintProtocol.EscPos || capabilities.DeviceProfileName != null) &&
                    !capabilities.SupportsTextRotation(font.Rotation))
                    Capability(path, "此模式的文字旋转。");
                if (font.Rotation != RotationAngle.None && !capabilities.SupportsRotatedUnderlineText &&
                    capabilities.SupportsUnderlineText && (font.FontStyle & 4) != 0)
                    Capability(path, "旋转文字的下划线。");
                if (resolvedFont.DeviceName != null)
                {
                    var name = resolvedFont.DeviceName;
                    if (options.Protocol == PrintProtocol.Tspl)
                    {
                        if (name.Length != 1 || name[0] < '0' || name[0] > '8')
                            throw new ArgumentException($"协议 {options.Protocol} 的设备字体标识无效。");
                        if (name != "0") TsplFontSizing.FixedScale(name, font.FontSize == 0 ? 24 : font.FontSize);
                    }
                    else if (options.Protocol == PrintProtocol.Zpl)
                    {
                        if (name.Length != 1 || !(name[0] >= '0' && name[0] <= '9' ||
                            name[0] >= 'A' && name[0] <= 'Z'))
                            throw new ArgumentException($"协议 {options.Protocol} 的设备字体标识无效。");
                    }
                    else Capability(path, "此模式的设备字体标识。");
                }
                var rasterizedTextStyles =
                    (options.Protocol == PrintProtocol.Tspl && options.TsplMedia?.AllowRasterizedTextStyles == true ||
                     options.Protocol == PrintProtocol.Zpl && options.ZplMedia?.AllowRasterizedTextStyles == true) &&
                    (font.Bold || (font.FontStyle & 5) != 0);
                if (!string.IsNullOrWhiteSpace(resolvedFont.HostFamily) &&
                    (options.Protocol != PrintProtocol.EscPos && !rasterizedTextStyles ||
                     options.Protocol == PrintProtocol.EscPos && options.EscPosLayout != EscPosLayoutMode.Raster))
                    Capability(path, "此模式的系统字体族。");
                if (options.EscPosDevice?.RasterFonts != null &&
                    !options.EscPosDevice.RasterFonts.SupportsSelection(resolvedFont.HostFamily,
                        font.Bold || (font.FontStyle & 1) != 0))
                    Capability(path, $"固定字体 {resolvedFont.HostFamily ?? options.EscPosDevice.RasterFonts.DefaultFont} 的 " +
                        $"{(font.Bold || (font.FontStyle & 1) != 0 ? "Bold" : "Regular")} 样式；请检查资源别名及显式替代策略。");
                if ((!capabilities.SupportsBoldText && (font.Bold || (font.FontStyle & 1) != 0)) ||
                    !capabilities.SupportsUnderlineText && (font.FontStyle & 4) != 0 && !native)
                    Capability(path, "设备内置字体的粗体或下划线。");
                if (font.Reverse && !capabilities.SupportsReverseText)
                    Capability(path, "反白文字。");
                if (native && font.FontSize != 0 && font.FontSize != 24)
                    Capability(path, "Native 模式的非 24 点文字。");
            }
            if (element is BpBarcodeElement barcode)
            {
                if (native && options.EscPosDevice?.NativeFlow == EscPosNativeFlowMode.Sequential &&
                    barcode.HumanReadable == true && barcode.HumanReadableReserveHeight <= 0)
                    throw new ArgumentException("Native 混合收据开启条码可读文字时须声明 HumanReadableReserveHeight。");
                var defaultHri = options.Protocol == PrintProtocol.Tspl || options.Protocol == PrintProtocol.Zpl;
                var requestsHri = barcode.HumanReadable == true ||
                    barcode.HumanReadable == null && defaultHri;
                if (barcode.QuietZone != null && !barcode.QuietZone.IsEmpty() && defaultHri &&
                    requestsHri && barcode.HumanReadableReserveHeight <= 0)
                    throw new ArgumentException($"协议 {options.Protocol} 的显式静区要求声明 HumanReadableReserveHeight 条码可读文字高度，或关闭 HumanReadable。");
                if (native && options.EscPosDevice?.Profile?.SupportsNativeBarcode == false)
                    Capability(path, "目标设备的原生条码；请显式使用 Raster 模式。");
                if (barcode.Rotation != RotationAngle.None && !capabilities.SupportsBarcodeRotation(barcode.Rotation) &&
                    (options.Protocol == PrintProtocol.EscPos || capabilities.DeviceProfileName != null))
                    Capability(path, "此模式的条码旋转。");
                if (options.Protocol == PrintProtocol.Cpcl && !capabilities.SupportsBarcodeRotation(barcode.Rotation))
                    Capability(path, "此角度的条码旋转；CPCL 模板仅支持 0 或顺时针 270 度。");
                if (barcode.NarrowModuleWidth is int narrowWidth && narrowWidth <= 0 ||
                    barcode.WideModuleWidth is int wideWidth && wideWidth <= 0)
                    throw new ArgumentException($"协议 {options.Protocol} 的条码模块宽度必须为正数。");
                if (!capabilities.SupportsExplicitBarcodeModuleWidth && options.Protocol == PrintProtocol.Cpcl &&
                    (barcode.NarrowModuleWidth.HasValue || barcode.WideModuleWidth.HasValue))
                    Capability(path, "CPCL 的显式条码模块宽度。");
                if (!capabilities.SupportsWideModuleWidth && barcode.WideModuleWidth.HasValue)
                    Capability(path, "此模式的独立宽条点宽。");
                if (barcode.HumanReadableReserveHeight < 0)
                    throw new ArgumentOutOfRangeException(nameof(barcode.HumanReadableReserveHeight));
                var supportsHri = capabilities.SupportsBarcodeHumanReadableType(barcode.BarcodeType);
                if (requestsHri && !supportsHri)
                    Capability(path, "此模式的条码可读文字。");
                if (barcode.HumanReadable == true && options.Protocol == PrintProtocol.EscPos &&
                    options.EscPosLayout == EscPosLayoutMode.Raster && barcode.HumanReadableReserveHeight <= 0)
                    Capability(path, "ESC/POS Raster 开启条码可读文字时须声明 HumanReadableReserveHeight。");
                if (barcode.HumanReadableReserveHeight > 0)
                {
                    if (!supportsHri) Capability(path, "此模式的条码可读文字预留区域。");
                    if (barcode.HumanReadable == false || native && barcode.HumanReadable != true)
                        throw new ArgumentException("预留可读文字高度要求启用条码可读文字。");
                }
                if (!capabilities.BarcodeTypes.Contains(barcode.BarcodeType))
                    Capability(path, $"{barcode.BarcodeType} 条码。");
            }
            if (element is BpQrCodeElement && native &&
                options.EscPosDevice?.Profile?.SupportsNativeQrCode == false)
                Capability(path, "目标设备的原生二维码；请显式使用 Raster 模式。");
            if (element is BpQrCodeElement qr && capabilities.QrModuleSize != null &&
                qr.UnitWidth > 0 && !capabilities.QrModuleSize.Contains(qr.UnitWidth) &&
                (options.Protocol != PrintProtocol.Cpcl || includePaths))
                Capability(path, "此模式的二维码模块尺寸。");
            if (element is BpQrCodeElement rotatedQr && rotatedQr.Rotation != RotationAngle.None &&
                !capabilities.SupportsQrCodeRotation(rotatedQr.Rotation))
                Capability(path, "此模式的二维码旋转。");
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
        void Rows(List<BpRow> rows, int depth, bool top, string prefix, HashSet<string> references = null)
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
                if (row.Section != null && ((!top && !(template.Version == 3 && (row.Section == "TableHeader" || row.Section == "Body"))) || row.IsHeader || (row.Section != "Body" && row.Section != "DocumentHeader" &&
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
                ValidateBorder(row.Border, rowPath);
                Condition(row.Condition, 1);
                if (row.Cells != null) for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                {
                    var cellPath = rowPath + ".Cells[" + cellIndex + "]";
                    At(cellPath, () =>
                    {
                    var cell = row.Cells[cellIndex];
                    Enter(cell, depth);
                    if (cell.Width < 0) throw new ArgumentException("单元格宽度不能为负数。");
                    ValidateBorder(cell.Border, cellPath);
                Rows(cell.Rows, depth + 1, false, cellPath + ".", references);
                    if (cell.ElementAlls != null && cell.ElementAlls.Count > 0)
                        throw new ArgumentException("C# 模板使用 Elements；ElementAlls 仅由 JSON 加载器转换。");
                    Elements(cell.Elements, depth, references ?? new HashSet<string>(StringComparer.Ordinal), cellPath + ".Elements");
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
                ValidateProtocolElement(element, elementPath);
                if (saving && element.Field?.ValueFunc != null) throw new ArgumentException("含 ValueFunc 的模板不能保存为 JSON。");
                if (element.StartCoordinate?.X < 0 || element.StartCoordinate?.Y < 0) throw new ArgumentException("元素坐标不能为负数。");
                if (element is BpFontElement font && !Enum.IsDefined(typeof(BpTextAlignment), font.TextAlignment))
                    throw new ArgumentException("文字对齐方式无效。");
                if (element is BpFontElement fontSelection)
                    TemplateFontResolver.ValidateStructure(fontSelection);
                if (element is BpFontElement rotatedFont &&
                    (rotatedFont.Width < 0 || !Enum.IsDefined(typeof(RotationAngle), rotatedFont.Rotation)))
                    throw new ArgumentException("文字排版宽度或旋转角度无效。");
                if (element is BpBarcodeElement rotatedBarcode &&
                    !Enum.IsDefined(typeof(RotationAngle), rotatedBarcode.Rotation))
                    throw new ArgumentException("条码旋转角度无效。");
                if (element is BpImageElement image && (image.Width < 0 || image.Height < 0 ||
                    !Enum.IsDefined(typeof(RotationAngle), image.Rotation)))
                    throw new ArgumentException("图片尺寸或旋转角度无效。");
                if (element is BpImageElement croppedImage && croppedImage.Crop != null &&
                    (croppedImage.Crop.X < 0 || croppedImage.Crop.Y < 0 ||
                     croppedImage.Crop.Width <= 0 || croppedImage.Crop.Height <= 0))
                    throw new ArgumentException("图片裁切区域必须使用非负坐标及正数宽高。");
                if (element is BpLineElement line)
                {
                    var start = line.StartCoordinate;
                    var end = line.LineEndCoordinate;
                    if (start == null || end == null || (end.X == start.X && end.Y == start.Y) || end.X < 0 || end.Y < 0 ||
                        line.LineWidth < 1 || line.LineWidth > 16) throw new ArgumentException("线段坐标或线宽无效。");
                }
                if (element is BpGroupElement group)
                {
                    if (group.Width < 0 || group.Height < 0 ||
                        (group.ComponentId != null && (group.Elements != null || group.Rows != null)) ||
                        (group.Elements != null && group.Rows != null))
                        throw new ArgumentException("组合尺寸无效或同时指定内联元素与组件引用。");
                    if (template.Version < 3 && (group.Rows != null || group.DataPath != null || group.Parameters != null || group.Pagination != null))
                        throw new ArgumentException("此模板版本不支持 V3 组合属性。");
                    if (group.Pagination != null && !Enum.IsDefined(typeof(BpGroupPagination), group.Pagination.Value))
                        throw new ArgumentException("组合分页策略无效。");
                    if (group.DataPath != null && string.IsNullOrWhiteSpace(group.DataPath)) throw new ArgumentException("组合数据路径不能为空。");
                    if (group.Parameters != null) foreach (var parameter in group.Parameters)
                        if (string.IsNullOrWhiteSpace(parameter.Key) || parameter.Value == null ||
                            (parameter.Value.Path != null && parameter.Value.Value != null) ||
                            (parameter.Value.Path == null && parameter.Value.Value == null))
                            throw new ArgumentException("组合参数绑定无效。");
                    if (group.ComponentId != null)
                    {
                        if (!components.TryGetValue(group.ComponentId, out var referenced)) throw new ArgumentException("组件不存在：" + group.ComponentId);
                        if (!references.Add(group.ComponentId)) throw new ArgumentException("组件存在循环引用。");
                        Elements(referenced.Elements, depth + 1, references, elementPath + ".Elements");
                        Rows(referenced.Rows, depth + 1, false, elementPath + ".", references);
                        references.Remove(group.ComponentId);
                    }
                    else
                    {
                        Elements(group.Elements, depth + 1, references, elementPath + ".Elements");
                        Rows(group.Rows, depth + 1, false, elementPath + ".", references);
                    }
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
