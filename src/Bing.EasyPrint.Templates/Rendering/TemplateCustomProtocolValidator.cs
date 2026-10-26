using System;
using System.Collections.Generic;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;

namespace Bing.EasyPrint.Templates;

/// <summary>
/// 在通用模板结构校验之后应用新协议适配器声明的元素、字体和页面范围。
/// </summary>
internal static class TemplateCustomProtocolValidator
{
    internal static void Validate(BpTemplate template, TemplateRenderOptions options,
        CancellationToken cancellationToken)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        var capabilities = options.CustomProtocol?.Capabilities ??
            throw new ArgumentException("新协议模板适配器不能为空。", nameof(options));
        if (template.Width <= 0 || capabilities.MaxPageWidthDots is int maxWidth && template.Width > maxWidth)
            throw new TemplateRenderException("Template", "模板宽度超过新协议声明上限。",
                new ArgumentOutOfRangeException(nameof(template.Width)));
        if (!template.AutoHeight && template.Height > 0 &&
            capabilities.MaxPageHeightDots is int maxHeight && template.Height > maxHeight)
            throw new TemplateRenderException("Template", "模板高度超过新协议声明上限。",
                new ArgumentOutOfRangeException(nameof(template.Height)));
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < (template.Components?.Count ?? 0); index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var component = template.Components[index];
            if (component == null || !visited.Add(component)) continue;
            WalkElements(component.Elements, "Components[" + index + "].Elements", capabilities, options,
                cancellationToken);
            WalkRows(component.Rows, "Components[" + index + "].", capabilities, options, visited,
                cancellationToken);
        }
        WalkRows(template.Rows, string.Empty, capabilities, options, visited, cancellationToken);
    }

    private static void WalkRows(IReadOnlyList<BpRow> rows, string prefix,
        TemplateCustomProtocolCapabilities capabilities, TemplateRenderOptions options,
        HashSet<object> visited, CancellationToken cancellationToken)
    {
        if (rows == null) return;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = rows[rowIndex];
            if (row == null) continue;
            var rowPath = prefix + "Rows[" + rowIndex + "]";
            if (row.Cells == null) continue;
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                var cell = row.Cells[cellIndex];
                if (cell == null) continue;
                var cellPath = rowPath + ".Cells[" + cellIndex + "]";
                WalkElements(cell.Elements, cellPath + ".Elements", capabilities, options, cancellationToken);
                WalkRows(cell.Rows, cellPath + ".", capabilities, options, visited, cancellationToken);
            }
        }
    }

    private static void WalkElements(IReadOnlyList<BpElement> elements, string path,
        TemplateCustomProtocolCapabilities capabilities, TemplateRenderOptions options,
        CancellationToken cancellationToken)
    {
        if (elements == null) return;
        for (var index = 0; index < elements.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var element = elements[index];
            if (element == null) continue;
            var elementPath = path + "[" + index + "]";
            if (element.Type == 6)
            {
                if (element is BpGroupElement group)
                {
                    WalkElements(group.Elements, elementPath + ".Elements", capabilities, options,
                        cancellationToken);
                    WalkRows(group.Rows, elementPath + ".", capabilities, options,
                        new HashSet<object>(ReferenceEqualityComparer.Instance), cancellationToken);
                }
                continue;
            }
            if (!capabilities.SupportsElementType(element.Type))
                throw new TemplateRenderException(elementPath,
                    $"新协议 {options.ProtocolId} 不支持元素类型 {element.Type}。",
                    new NotSupportedException("当前新协议没有声明该元素的真实布局和输出能力。"));
            if (!capabilities.SupportsCoordinates &&
                (element.StartCoordinate != null || element.EndCoordinate != null))
                throw new TemplateRenderException(elementPath, "新协议未声明坐标布局。",
                    new NotSupportedException("坐标布局未声明。"));
            switch (element)
            {
                case BpFontElement font:
                    ValidateFont(font, elementPath, capabilities, options);
                    break;
                case BpImageElement image:
                    if (image.Rotation != RotationAngle.None && !capabilities.SupportsImageRotation(image.Rotation))
                        throw new TemplateRenderException(elementPath, "新协议不支持该图片旋转角度。",
                            new NotSupportedException("图片旋转未声明。"));
                    if (!capabilities.SupportsImages)
                        throw new TemplateRenderException(elementPath, "新协议未声明图片支持。",
                            new NotSupportedException("图片未声明。"));
                    break;
                case BpBarcodeElement barcode:
                    if (barcode.QuietZone != null && !capabilities.SupportsExplicitQuietZone)
                        throw new TemplateRenderException(elementPath, "新协议未声明显式静区。", new NotSupportedException("显式静区未声明。"));
                    if (barcode.NarrowModuleWidth.HasValue && !capabilities.SupportsExplicitBarcodeModuleWidth)
                        throw new TemplateRenderException(elementPath, "新协议未声明显式窄模块宽度。", new NotSupportedException("显式窄模块宽度未声明。"));
                    if (barcode.WideModuleWidth.HasValue && !capabilities.SupportsWideModuleWidth)
                        throw new TemplateRenderException(elementPath, "新协议未声明显式宽模块宽度。", new NotSupportedException("显式宽模块宽度未声明。"));
                    if (barcode.HumanReadable == true && !capabilities.SupportsBarcodeHumanReadable)
                        throw new TemplateRenderException(elementPath, "新协议未声明条码可读文字。", new NotSupportedException("条码可读文字未声明。"));
                    if (!capabilities.SupportsBarcodes || !capabilities.SupportsBarcodeType(barcode.BarcodeType))
                        throw new TemplateRenderException(elementPath, "新协议不支持该一维码制式。",
                            new NotSupportedException("一维码制式未声明。"));
                    if (!capabilities.SupportsBarcodeRotation(barcode.Rotation))
                        throw new TemplateRenderException(elementPath, "新协议不支持该一维码旋转角度。",
                            new NotSupportedException("一维码旋转未声明。"));
                    break;
                case BpQrCodeElement qr:
                    if (qr.QuietZone != null && !capabilities.SupportsExplicitQuietZone)
                        throw new TemplateRenderException(elementPath, "新协议未声明显式静区。", new NotSupportedException("显式静区未声明。"));
                    if (qr.Gs1 && !capabilities.SupportsGs1QrCode)
                        throw new TemplateRenderException(elementPath, "新协议未声明 GS1 QR。", new NotSupportedException("GS1 QR 未声明。"));
                    if (!capabilities.SupportsQrCode)
                        throw new TemplateRenderException(elementPath, "新协议未声明二维码支持。",
                            new NotSupportedException("二维码未声明。"));
                    if (!capabilities.SupportsQrCodeRotation(qr.Rotation))
                        throw new TemplateRenderException(elementPath, "新协议不支持该二维码旋转角度。",
                            new NotSupportedException("二维码旋转未声明。"));
                    break;
                case BpGs1CompositeElement _:
                    if (!capabilities.SupportsComposite)
                        throw new TemplateRenderException(elementPath, "新协议未声明 GS1 Composite 支持。",
                            new NotSupportedException("Composite 未声明。"));
                    break;
            }
        }
    }

    private static void ValidateFont(BpFontElement font, string path,
        TemplateCustomProtocolCapabilities capabilities, TemplateRenderOptions options)
    {
        var size = font.FontSize == 0 ? 24 : font.FontSize;
        if (!capabilities.SupportsTextSize(size))
            throw new TemplateRenderException(path, $"新协议不支持字号 {size}。",
                new NotSupportedException("字号未声明。"));
        if (!capabilities.SupportsTextRotation(font.Rotation))
            throw new TemplateRenderException(path, "新协议不支持该文字旋转角度。",
                new NotSupportedException("文字旋转未声明。"));
        if (font.Bold && !capabilities.SupportsBoldText)
            throw new TemplateRenderException(path, "新协议未声明粗体文字。", new NotSupportedException("粗体文字未声明。"));
        if ((font.FontStyle & 4) != 0 && !capabilities.SupportsUnderlineText)
            throw new TemplateRenderException(path, "新协议未声明下划线文字。", new NotSupportedException("下划线文字未声明。"));
        if (font.Reverse && !capabilities.SupportsReverseText)
            throw new TemplateRenderException(path, "新协议未声明反白文字。", new NotSupportedException("反白文字未声明。"));
        try
        {
            _ = TemplateFontResolver.Resolve(font, options);
        }
        catch (Exception error) when (error is NotSupportedException || error is ArgumentException)
        {
            throw new TemplateRenderException(path, error.Message, error);
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
        public new bool Equals(object left, object right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}
