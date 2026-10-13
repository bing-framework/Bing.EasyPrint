using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Protocols;

namespace Bing.EasyPrint.Printing;

/// <summary>按统一打印文档生成协议字节的内置 Provider 工厂。</summary>
public static class PrintProtocolProviderFactory
{
    /// <summary>创建指定协议的 Provider。</summary>
    public static IPrintProtocolProvider Create(PrintProtocol protocol, Encoding encoding = null,
        EscPosLayoutMode escPosLayoutMode = EscPosLayoutMode.Native, EscPosDeviceOptions escPosOptions = null,
        TsplMediaOptions tsplOptions = null, ZplMediaOptions zplOptions = null,
        bool requireDeclaredNativeBounds = false)
    {
        if (!Enum.IsDefined(typeof(EscPosLayoutMode), escPosLayoutMode))
            throw new ArgumentOutOfRangeException(nameof(escPosLayoutMode));
        if (protocol != PrintProtocol.EscPos && escPosOptions != null)
            throw new ArgumentException("ESC/POS 设备选项只能用于 ESC/POS 协议。", nameof(escPosOptions));
        if (protocol != PrintProtocol.Tspl && tsplOptions != null)
            throw new ArgumentException("TSPL 介质选项只能用于 TSPL 协议。", nameof(tsplOptions));
        if (protocol != PrintProtocol.Zpl && zplOptions != null)
            throw new ArgumentException("ZPL 介质选项只能用于 ZPL 协议。", nameof(zplOptions));
        if (requireDeclaredNativeBounds && protocol != PrintProtocol.Tspl && protocol != PrintProtocol.Zpl)
            throw new ArgumentException("设备原生占位严格校验只适用于 TSPL/ZPL。", nameof(requireDeclaredNativeBounds));
        switch (protocol)
        {
            case PrintProtocol.Cpcl: return new CpclPrintProtocolProvider(encoding);
            case PrintProtocol.EscPos: return new EscPosPrintProtocolProvider(encoding, escPosLayoutMode, escPosOptions);
            case PrintProtocol.Tspl: return new TsplPrintProtocolProvider(encoding, tsplOptions,
                requireDeclaredNativeBounds);
            case PrintProtocol.Zpl: return new ZplPrintProtocolProvider(encoding, zplOptions,
                requireDeclaredNativeBounds);
            default: throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "不支持的打印协议。");
        }
    }
}

public abstract class PrintProtocolProviderBase : IPrintCancelableProtocolProvider
{
    protected readonly Encoding Encoding;
    private readonly AsyncLocal<CancellationToken> _validationCancellation = new AsyncLocal<CancellationToken>();
    /// <summary>当前预检的取消令牌；按执行上下文隔离并在调用结束时恢复。</summary>
    protected CancellationToken ValidationCancellationToken => _validationCancellation.Value;
    protected PrintProtocolProviderBase(Encoding encoding) => Encoding = encoding ?? Encoding.UTF8;
    public abstract PrintProtocol Protocol { get; }
    public abstract PrintCapabilities Capabilities { get; }
    protected virtual bool RequireDeclaredNativeBounds => false;
    // 自动批次裁切必须在 PRINT 前配置；旧 ZPL ^CN 同样位于格式内部。
    internal virtual bool JobCutInPage => Protocol == PrintProtocol.Zpl;
    internal virtual void ValidateJobFinishing(PrintJobOptions options) { }

    internal virtual void ValidateCutOptions(PrintCutOptions options)
    {
        options.Validate();
        throw new NotSupportedException($"协议 {Protocol} 尚未声明显式裁切形状和补偿映射。");
    }
    internal virtual byte[] BuildJobCut(PrintCutOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCutOptions(options);
        throw new NotSupportedException("Provider 未实现显式裁切编码。");
    }
    internal virtual long JobCutLength(PrintCutOptions options)
    {
        ValidateCutOptions(options);
        throw new NotSupportedException("Provider 未实现显式裁切预算。");
    }

    internal virtual long JobFinishingLength(int amount, PrintFeedUnit unit, bool cut)
    {
        if (amount != 0 || cut) throw new NotSupportedException("Provider 不支持此任务收尾指令。");
        return 0;
    }
    internal virtual byte[] BuildJobFinishing(int amount, PrintFeedUnit unit, bool cut, CancellationToken cancellationToken)
    {
        JobFinishingLength(amount, unit, cut);
        return Array.Empty<byte>();
    }

    /// <summary>可取消预检，仍调用旧虚方法以保留派生 Provider 的校验行为。</summary>
    public virtual void Validate(PrintDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var previous = _validationCancellation.Value;
        _validationCancellation.Value = cancellationToken;
        try
        {
            Validate(document);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally { _validationCancellation.Value = previous; }
    }

    public virtual void Validate(PrintDocument document)
    {
        var cancellationToken = ValidationCancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        if (document == null) throw new ArgumentNullException(nameof(document));
        for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.Pages[pageIndex];
            var pagePath = $"Pages[{pageIndex}]";
            try { ValidatePage(page); }
            catch (NotSupportedException error) { throw new PrintCapabilityException(Protocol, pagePath, error, pageIndex, null); }
            catch (ArgumentException error) { throw new PrintElementValidationException(Protocol, pagePath, error, pageIndex, null); }
            for (var elementIndex = 0; elementIndex < page.Elements.Count; elementIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var validationResources = PrintResourceScope.BeginTemporary();
                var element = page.Elements[elementIndex];
                if (element == null) throw new ArgumentException("打印页面包含空元素。", nameof(document));
                // 已知机型禁用的元素必须先拒绝，避免尺寸预检先进入码图编码。
                try { ValidateProfileElementKind(element); }
                catch (NotSupportedException error)
                {
                    throw new PrintCapabilityException(Protocol,
                        element.SourcePath ?? $"Pages[{pageIndex}].Elements[{elementIndex}]", error, pageIndex, elementIndex);
                }
                if (Protocol != PrintProtocol.Cpcl || element is PrintImageElement rotatedImage && rotatedImage.Rotation != RotationAngle.None ||
                    element is PrintRectangleElement exactRectangle && exactRectangle.ExactFillBounds)
                {
                    try { ValidateElementBounds(page, element, Protocol, cancellationToken, Capabilities); }
                    catch (ArgumentException error)
                    {
                        throw new PrintElementValidationException(Protocol,
                            element.SourcePath ?? $"Pages[{pageIndex}].Elements[{elementIndex}]", error, pageIndex, elementIndex);
                    }
                }
                try { ValidateElement(element); }
                catch (NotSupportedException error)
                {
                    throw new PrintCapabilityException(Protocol,
                        element.SourcePath ?? $"Pages[{pageIndex}].Elements[{elementIndex}]", error, pageIndex, elementIndex);
                }
                catch (ArgumentException error)
                {
                    throw new PrintElementValidationException(Protocol,
                        element.SourcePath ?? $"Pages[{pageIndex}].Elements[{elementIndex}]", error, pageIndex, elementIndex);
                }
            }
            PrintQuietZoneValidator.Validate(page, pageIndex, Protocol, Capabilities.SupportsExplicitQuietZone,
                cancellationToken, Capabilities.RasterFonts);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateElementBounds(PrintPage page, PrintElement element, PrintProtocol protocol,
        CancellationToken cancellationToken, PrintCapabilities capabilities)
    {
        if (element.X < 0 || element.Y < 0)
            throw new ArgumentOutOfRangeException(nameof(element), "元素坐标不能为负数。");
        if (element.X >= page.Width || element.Y >= page.Height)
            throw new ArgumentOutOfRangeException(nameof(element), "元素坐标超出打印页范围。");

        switch (element)
        {
            case PrintLineElement line:
                if (line.X < 0 || line.X >= page.Width || line.Y < 0 || line.Y >= page.Height ||
                    line.EndX < 0 || line.EndX >= page.Width || line.EndY < 0 || line.EndY >= page.Height)
                    throw new ArgumentOutOfRangeException(nameof(element), "线段端点超出打印页范围。");
                if (line.X == line.EndX && (long)line.X + line.LineWidth > page.Width ||
                    line.Y == line.EndY && (long)line.Y + line.LineWidth > page.Height)
                    throw new ArgumentOutOfRangeException(nameof(element), "线段笔画超出打印页范围。");
                if (line.X != line.EndX && line.Y != line.EndY)
                {
                    var radius = line.LineWidth / 2;
                    if ((long)Math.Min(line.X, line.EndX) - radius < 0 ||
                        (long)Math.Min(line.Y, line.EndY) - radius < 0 ||
                        (long)Math.Max(line.X, line.EndX) + radius >= page.Width ||
                        (long)Math.Max(line.Y, line.EndY) + radius >= page.Height)
                        throw new ArgumentOutOfRangeException(nameof(element), "斜线笔画超出打印页范围。");
                }
                break;
            case PrintRectangleElement rectangle:
                if ((long)rectangle.X + rectangle.Width > page.Width ||
                    (long)rectangle.Y + rectangle.Height > page.Height)
                    throw new ArgumentOutOfRangeException(nameof(element), "矩形超出打印页范围。");
                break;
            case PrintImageElement image:
                if ((long)image.X + image.OutputWidth > page.Width ||
                    (long)image.Y + image.OutputHeight > page.Height)
                    throw new ArgumentOutOfRangeException(nameof(element), "图片超出打印页范围。");
                break;
            case PrintBarcodeElement barcode:
                var barcodeQuarterTurn = barcode.Rotation == RotationAngle.Rotate90 ||
                                         barcode.Rotation == RotationAngle.Rotate270;
                var reservedBarcodeWidth = MeasureBarcodeWidth(barcode, protocol, cancellationToken);
                var nativeHriEnabled = barcode.HumanReadable != false &&
                    (protocol == PrintProtocol.Tspl || protocol == PrintProtocol.Zpl);
                if (nativeHriEnabled && capabilities.NativeBarcodeHumanReadableReserveWidthDots.HasValue)
                    reservedBarcodeWidth = Math.Max(reservedBarcodeWidth,
                        capabilities.NativeBarcodeHumanReadableReserveWidthDots.Value);
                var reservedBarcodeHeight = MeasureBarcodeHeight(barcode, protocol);
                var hriOverhang = nativeHriEnabled
                    ? capabilities.ResolveNativeBarcodeHumanReadableOverhang(barcode.Rotation)
                    : default;
                var barcodeLeftOverhang = nativeHriEnabled
                    ? hriOverhang.LeftDots ?? 0 : 0;
                var barcodeTopOverhang = nativeHriEnabled
                    ? hriOverhang.TopDots ?? 0 : 0;
                var barcodeRightOverhang = nativeHriEnabled
                    ? hriOverhang.RightDots ?? 0 : 0;
                var barcodeBottomOverhang = nativeHriEnabled
                    ? hriOverhang.BottomDots ?? 0 : 0;
                var barcodeWidth = barcodeQuarterTurn ? reservedBarcodeHeight : reservedBarcodeWidth;
                var barcodeHeight = barcodeQuarterTurn ? reservedBarcodeWidth : reservedBarcodeHeight;
                var barcodeLeft = (long)barcode.X - barcodeLeftOverhang;
                var barcodeTop = (long)barcode.Y - barcodeTopOverhang;
                var barcodeRight = (long)barcode.X + barcodeWidth + barcodeRightOverhang;
                var barcodeBottom = (long)barcode.Y + barcodeHeight + barcodeBottomOverhang;
                if (barcodeWidth > 0 && (barcodeLeft < 0 || barcodeRight > page.Width))
                    throw new ArgumentOutOfRangeException(nameof(element), "条码超出打印页宽度。");
                if (barcodeHeight > 0 && (barcodeTop < 0 || barcodeBottom > page.Height))
                    throw new ArgumentOutOfRangeException(nameof(element), "条码超出打印页范围。");
                break;
            case PrintQrCodeElement qr:
                var qrSize = protocol == PrintProtocol.Tspl || protocol == PrintProtocol.Zpl
                    ? EscPosRasterCanvas.MeasureQrCodeSize(qr.Data, qr.ModuleSize, qr.ErrorLevel, cancellationToken) : 0;
                var qrReservedWidth = Math.Max(qr.Width, qrSize);
                var qrReservedHeight = Math.Max(qr.Height, qrSize);
                var qrQuarterTurn = qr.Rotation == RotationAngle.Rotate90 ||
                                    qr.Rotation == RotationAngle.Rotate270;
                var qrWidth = qrQuarterTurn ? qrReservedHeight : qrReservedWidth;
                var qrHeight = qrQuarterTurn ? qrReservedWidth : qrReservedHeight;
                if (qrWidth > 0 && (long)qr.X + qrWidth > page.Width ||
                    qrHeight > 0 && (long)qr.Y + qrHeight > page.Height)
                    throw new ArgumentOutOfRangeException(nameof(element), "二维码超出打印页范围。");
                break;
            case PrintTextElement text:
                var textQuarterTurn = text.Rotation == RotationAngle.Rotate90 ||
                                      text.Rotation == RotationAngle.Rotate270;
                var textExtent = MeasureTextExtent(text, protocol);
                var textWidth = textQuarterTurn ? textExtent.Height : textExtent.Width;
                var textHeight = textQuarterTurn ? textExtent.Width : textExtent.Height;
                if (textWidth > 0 && (long)text.X + textWidth > page.Width)
                    throw new ArgumentOutOfRangeException(nameof(element), "文字超出打印页宽度。");
                if (textHeight > 0 && (long)text.Y + textHeight > page.Height)
                    throw new ArgumentOutOfRangeException(nameof(element), "文字超出打印页高度。");
                break;
        }
    }

    private static bool IsPrintableAscii(string value)
    {
        foreach (var character in value)
            if (character < 32 || character > 126) return false;
        return true;
    }

    protected internal static long MeasureBarcodeWidth(PrintBarcodeElement barcode, PrintProtocol protocol)
        => MeasureBarcodeWidth(barcode, protocol, CancellationToken.None);

    protected internal static long MeasureBarcodeWidth(PrintBarcodeElement barcode, PrintProtocol protocol,
        CancellationToken cancellationToken)
        => Math.Max(barcode.Width,
            protocol == PrintProtocol.Tspl || protocol == PrintProtocol.Zpl
                ? NativeLinearBarcodeWidth(barcode, protocol, cancellationToken)
                : 0);

    internal static (long Width, long Height) EstimateNativeBarcodeExtent(PrintProtocol protocol,
        BarcodeType type, string data, int barHeight, int narrowWidth, int wideWidth,
        bool humanReadable, int rotation, int? humanReadableReserveHeightDots = null,
        int? humanReadableReserveWidthDots = null)
    {
        const int humanReadableHeightEstimate = 24;
        if (humanReadableReserveHeightDots.HasValue && humanReadableReserveHeightDots.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(humanReadableReserveHeightDots),
                "原生条码 HRI 预留高度必须为正数。");
        if (humanReadableReserveWidthDots.HasValue && humanReadableReserveWidthDots.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(humanReadableReserveWidthDots),
                "原生条码 HRI 预留宽度必须为正数。");
        var humanReadableHeight = humanReadable
            ? humanReadableReserveHeightDots ?? humanReadableHeightEstimate
            : 0;
        var barcode = new PrintBarcodeElement(0, 0, type, data, barHeight, narrowWidth, wideWidth,
            (RotationAngle)rotation, humanReadable: humanReadable,
            humanReadableReserveHeight: humanReadableHeight);
        var width = type == BarcodeType.Msi
            ? NativeMsiBarcodeWidth(barcode, protocol)
            : NativeLinearBarcodeWidth(barcode, protocol, CancellationToken.None);
        if (width <= 0 && EscPosRasterCanvas.SupportsBarcodeType(type))
            width = EscPosRasterCanvas.MeasureBarcodeWidth(type, data, narrowWidth);
        if (width <= 0)
            throw new NotSupportedException($"协议 {protocol} 无法估算 {type} 原生条码的页面宽度。");
        if (humanReadable && humanReadableReserveWidthDots.HasValue)
            width = Math.Max(width, humanReadableReserveWidthDots.Value);

        var height = MeasureBarcodeHeight(barcode, protocol);
        if (type == BarcodeType.Code49 && protocol != PrintProtocol.Zpl)
        {
            height = checked((long)barHeight * 8 + humanReadableHeight);
        }
        if (rotation == 90 || rotation == 270)
            return (height, width);
        return (width, height);
    }

    internal static int ZplCode49HeightMultiplierForDots(int rowHeightDots, int moduleWidth)
    {
        if (rowHeightDots <= 0) throw new ArgumentOutOfRangeException(nameof(rowHeightDots));
        if (moduleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(moduleWidth));
        return checked((rowHeightDots - 1) / moduleWidth + 1);
    }

    internal static int ZplCode49ActualRowHeightDots(int rowHeightDots, int moduleWidth)
        => checked(ZplCode49HeightMultiplierForDots(rowHeightDots, moduleWidth) * moduleWidth);

    private static long NativeLinearBarcodeWidth(PrintBarcodeElement barcode, PrintProtocol protocol,
        CancellationToken cancellationToken)
    {
        if (barcode.Type == BarcodeType.Code128 || barcode.Type == BarcodeType.UccEan128)
            return EscPosRasterCanvas.MeasureBarcodeWidth(barcode.Type, barcode.Data, barcode.NarrowWidth, cancellationToken);
        var modules = barcode.Type switch
        {
            BarcodeType.Ean8 => 67,
            BarcodeType.Ean13 => 95,
            BarcodeType.UpcA => 95,
            BarcodeType.UpcE => 51,
            _ => 0
        };
        if (modules != 0) return checked((long)modules * barcode.NarrowWidth);

        if (barcode.Type == BarcodeType.I2OF5)
        {
            var digits = checked(barcode.Data.Length + barcode.Data.Length % 2);
            var wide = protocol == PrintProtocol.Zpl
                ? checked(barcode.NarrowWidth * 3)
                : barcode.WideWidth;
            return checked((long)(3 * digits + 6) * barcode.NarrowWidth +
                           (long)(2 * digits + 1) * wide);
        }

        if (barcode.Type == BarcodeType.I2OF5G)
        {
            var digits = checked(barcode.Data.Length + barcode.Data.Length % 2);
            var wide = protocol == PrintProtocol.Zpl
                ? checked(barcode.NarrowWidth * 3)
                : barcode.WideWidth;
            return checked((long)(3 * digits + 6) * barcode.NarrowWidth +
                           (long)(2 * digits + 1) * wide);
        }

        if (protocol == PrintProtocol.Tspl && barcode.Type == BarcodeType.Postnet)
        {
            var digits = barcode.Data.Replace("-", string.Empty);
            var bars = checked((digits.Length + 1) * 5 + 2);
            // TSPL POST 使用 1:1 的窄条/间隔；设备负责追加最终校验位。
            return checked((long)bars * barcode.NarrowWidth +
                           (long)(bars - 1) * barcode.NarrowWidth);
        }

        if (protocol == PrintProtocol.Tspl && barcode.Type == BarcodeType.Planet)
        {
            var digits = barcode.Data.Replace("-", string.Empty);
            var bars = checked(digits.Length * 5 + 2);
            // TSPL PLANET 使用邮政码的 1:1 窄条/间隔布局；统一模型要求完整校验位。
            return checked((long)bars * barcode.NarrowWidth +
                           (long)(bars - 1) * barcode.NarrowWidth);
        }

        if (protocol == PrintProtocol.Tspl && barcode.Type == BarcodeType.Plessey)
        {
            var symbols = checked(4 * barcode.Data.Length + 17);
            var wide = checked(barcode.NarrowWidth * 2);
            // Plessey 的起止、数据和 CRC 固定占 4n+17 个二值单元；TSPL 要求 1:2。
            return checked((long)symbols * (barcode.NarrowWidth + wide));
        }

        if (protocol == PrintProtocol.Tspl && barcode.Type == BarcodeType.Itf14)
        {
            // TSPL ITF14 接受 13 位 payload，并由设备补成 14 位 ITF-14 符号。
            var encodedDigits = 14;
            return checked((long)(3 * encodedDigits + 6) * barcode.NarrowWidth +
                           (long)(2 * encodedDigits + 1) * barcode.WideWidth);
        }

        if (protocol == PrintProtocol.Tspl && barcode.Type == BarcodeType.Ean14)
        {
            // TSPL EAN14 接受 13 位 payload，并由设备生成带 AI 01 和校验位的
            // 固定 GS1-128 符号；手册只允许 1:1，完整符号占 134 个模块。
            return checked(134L * barcode.NarrowWidth);
        }

        if (protocol == PrintProtocol.Tspl && barcode.Type == BarcodeType.Code11)
        {
            var dataLength = barcode.Data.Length;
            // Code 11 在数据长度超过 10 时由设备追加第二个 Modulo-11 校验位。
            // 每个符号最多有两个宽元素，另加一个窄条字符间隔；这里按最大符号宽度
            // 预留，避免设备补校验位或遇到宽元素时页面边界检查低估。
            var symbolCount = checked(dataLength + (dataLength > 10 ? 2 : 1) + 2);
            var symbolWidth = checked(4L * barcode.NarrowWidth + 2L * barcode.WideWidth);
            return checked(symbolCount * symbolWidth);
        }

        if (protocol == PrintProtocol.Zpl && barcode.Type == BarcodeType.Code11)
        {
            var dataLength = barcode.Data.Length;
            // ^B1 emits one or two Modulo-11 check symbols; reserve two and use the
            // documented 3:1 upper ratio so rotation/page preflight never underestimates.
            var symbolCount = checked(dataLength + 2 + 2);
            var symbolWidth = checked(4L * barcode.NarrowWidth + 2L * barcode.NarrowWidth * 3);
            return checked(symbolCount * symbolWidth);
        }

        if (protocol == PrintProtocol.Zpl && barcode.Type == BarcodeType.Plessey)
        {
            // ^BP encodes eight elements per data character and uses the ^BY default
            // 3:1 ratio. Keep the same start/check/stop upper bound as TSPL, but map
            // each binary element to the ZPL narrow/wide pair so page preflight cannot
            // under-report a native field.
            var symbols = checked(4 * barcode.Data.Length + 17);
            var wide = checked(barcode.NarrowWidth * 3);
            return checked((long)symbols * (barcode.NarrowWidth + wide));
        }

        if (protocol == PrintProtocol.Zpl && barcode.Type == BarcodeType.Code49)
        {
            // ^B4 uses one module width and a fixed Code 49 row ratio.  A row
            // contains 70 modules; stacked row count affects height, not width.
            return checked((long)Code49Encoder.RowWidth * barcode.NarrowWidth);
        }

        if (protocol == PrintProtocol.Zpl)
        {
            // ^BR uses the magnification factor as the X dimension. Keep the
            // same conservative module bound as the native ESC/POS DataBar
            // path so page preflight never under-reports the field width.
            var moduleCount = barcode.Type switch
            {
                BarcodeType.Gs1DataBarOmnidirectional => 13L * 14 + 34,
                BarcodeType.Gs1DataBarTruncated => 13L * 14 + 34,
                BarcodeType.Gs1DataBarLimited => 13L * 14 + 34,
                BarcodeType.Gs1DataBarExpanded => checked((long)(barcode.Data.Length + 2) * 16 + 34),
                _ => 0
            };
            if (moduleCount != 0) return checked(moduleCount * barcode.NarrowWidth);
        }

        if (protocol == PrintProtocol.Tspl && barcode.Type == BarcodeType.ChinaPost)
        {
            // TSPL CPOST uses a fixed 1:1 ratio. Each China Post data symbol is
            // represented by ten equal modules in the conservative host bound;
            // the two additional symbol slots reserve the start/stop patterns.
            var symbolCount = checked(barcode.Data.Length + 2);
            return checked((long)symbolCount * 10 * barcode.NarrowWidth);
        }

        if (protocol == PrintProtocol.Tspl &&
            (barcode.Type == BarcodeType.Telepen || barcode.Type == BarcodeType.Telepenn))
        {
            // TELEPEN uses a fixed 16-element symbol pattern. Reserve the maximum
            // wide-element width for each element and two additional start/stop
            // symbols so page preflight cannot underestimate the device output.
            var symbolCount = checked(barcode.Data.Length + 2);
            var symbolWidth = checked(16L * Math.Max(barcode.NarrowWidth, barcode.WideWidth));
            return checked(symbolCount * symbolWidth);
        }

        if (protocol == PrintProtocol.Tspl && barcode.Type == BarcodeType.Code49)
        {
            // CODE49 is a stacked symbol whose row count is selected by the
            // device. Reserve 49 modules per content symbol plus start/stop
            // slots, using the maximum module width to keep page checks safe.
            var symbolCount = checked(barcode.Data.Length + 2);
            var symbolWidth = checked(49L * Math.Max(barcode.NarrowWidth, barcode.WideWidth));
            return checked(symbolCount * symbolWidth);
        }

        if (protocol == PrintProtocol.EscPos)
        {
            // Function B 75–78 use device-defined module widths. Reserve a
            // deliberately generous module count so native page preflight never
            // underestimates a valid GS1 DataBar field.
            var moduleCount = barcode.Type switch
            {
                BarcodeType.Gs1DataBarOmnidirectional => 13L * 14 + 34,
                BarcodeType.Gs1DataBarTruncated => 13L * 14 + 34,
                BarcodeType.Gs1DataBarLimited => 13L * 14 + 34,
                BarcodeType.Gs1DataBarExpanded => checked((long)(barcode.Data.Length + 2) * 16 + 34),
                _ => 0
            };
            if (moduleCount != 0) return checked(moduleCount * barcode.NarrowWidth);
        }

        if (barcode.Type == BarcodeType.Codabar && barcode.Data.Length >= 3)
        {
            var wide = protocol == PrintProtocol.Zpl
                ? checked(barcode.NarrowWidth * 3)
                : barcode.WideWidth;
            long width = barcode.Data.Length - 1;
            foreach (var character in barcode.Data)
            {
                var wideCount = "0123456789-$".IndexOf(character) >= 0 ? 2 : 3;
                width = checked(width + (long)wideCount * wide + (7 - wideCount) * barcode.NarrowWidth);
            }
            return width;
        }

        if (protocol == PrintProtocol.Zpl &&
            (barcode.Type == BarcodeType.Postnet || barcode.Type == BarcodeType.Planet ||
             barcode.Type == BarcodeType.IntelligentMail))
        {
            var digits = barcode.Data.Replace("-", string.Empty);
            var bars = barcode.Type == BarcodeType.Postnet
                ? checked((digits.Length + 1) * 5 + 2)
                : barcode.Type == BarcodeType.Planet
                    ? checked(digits.Length * 5 + 2)
                    : 65;
            // ^BZ uses a fixed postal ratio; reserve the upper documented 3:1 spacing
            // so page-bound checks never under-report the native field width.
            return checked((long)bars * barcode.NarrowWidth +
                           (long)(bars - 1) * barcode.NarrowWidth * 3);
        }

        return 0;
    }

    private static long NativeMsiBarcodeWidth(PrintBarcodeElement barcode, PrintProtocol protocol)
    {
        var wide = protocol == PrintProtocol.Zpl
            ? checked(barcode.NarrowWidth * 3)
            : barcode.WideWidth;
        return checked((long)barcode.Data.Length * 4 * (barcode.NarrowWidth + wide) +
                       3L * barcode.NarrowWidth + 2L * wide);
    }

    private static long MeasureBarcodeHeight(PrintBarcodeElement barcode, PrintProtocol protocol)
    {
        if (protocol == PrintProtocol.EscPos && barcode.Type == BarcodeType.Gs1DataBarStacked)
            return EscPosRasterCanvas.MeasureBarcodeHeight(barcode.Type, barcode.Data,
                barcode.BarHeight, barcode.NarrowWidth, CancellationToken.None);
        if (protocol == PrintProtocol.EscPos && barcode.Type == BarcodeType.Code49)
        {
            var symbolHeight = EscPosRasterCanvas.MeasureBarcodeHeight(barcode.Type, barcode.Data,
                barcode.BarHeight, barcode.NarrowWidth, CancellationToken.None);
            return checked((long)symbolHeight + barcode.HumanReadableReserveHeight);
        }
        if (protocol == PrintProtocol.Zpl && barcode.Type == BarcodeType.Code49)
        {
            var rowHeight = ZplCode49ActualRowHeightDots(barcode.BarHeight, barcode.NarrowWidth);
            var symbolHeight = EscPosRasterCanvas.MeasureCode49SymbolHeight(barcode.Data, rowHeight,
                barcode.NarrowWidth, CancellationToken.None);
            var humanReadableHeight = barcode.HumanReadable == false
                ? 0 : barcode.HumanReadableReserveHeight;
            return checked((long)symbolHeight + humanReadableHeight);
        }
        var height = (long)barcode.BarHeight + barcode.HumanReadableReserveHeight;
        if (protocol == PrintProtocol.Zpl)
        {
            var zplMinimum = barcode.Type switch
            {
                BarcodeType.Gs1DataBarOmnidirectional => 33L * barcode.NarrowWidth,
                BarcodeType.Gs1DataBarTruncated => 13L * barcode.NarrowWidth,
                BarcodeType.Gs1DataBarLimited => 10L * barcode.NarrowWidth,
                BarcodeType.Gs1DataBarExpanded => 34L * barcode.NarrowWidth,
                _ => 0
            };
            return Math.Max((long)barcode.BarHeight, zplMinimum) + barcode.HumanReadableReserveHeight;
        }
        if (protocol != PrintProtocol.EscPos) return height;
        var minimum = barcode.Type switch
        {
            BarcodeType.Gs1DataBarOmnidirectional => 33L * barcode.NarrowWidth,
            BarcodeType.Gs1DataBarTruncated => 13L * barcode.NarrowWidth,
            BarcodeType.Gs1DataBarLimited => 10L * barcode.NarrowWidth,
            BarcodeType.Gs1DataBarExpanded => 34L * barcode.NarrowWidth,
            _ => 0
        };
        return Math.Max((long)barcode.BarHeight, minimum) + barcode.HumanReadableReserveHeight;
    }

    protected internal static (long Width, long Height) MeasureTextExtent(PrintTextElement text, PrintProtocol protocol)
    {
        long width = text.Width, height = text.Height;
        if (protocol == PrintProtocol.Tspl && text.DeviceFontName is string font && font != "0" &&
            text.Text.Length > 0 && IsPrintableAscii(text.Text))
        {
            var cell = TsplFontSizing.FixedCell(font);
            var scale = TsplFontSizing.FixedScale(font, text.FontSize);
            width = Math.Max(width, (long)text.Text.Length * cell.Width * scale);
            height = Math.Max(height, (long)cell.Height * scale);
        }
        return (width, height);
    }

    protected static (int X, int Y) TsplRotationOrigin(int x, int y, long width, long height, RotationAngle rotation)
    {
        switch (rotation)
        {
            case RotationAngle.Rotate90: return (checked((int)(x + height)), y);
            case RotationAngle.Rotate180: return (checked((int)(x + width)), checked((int)(y + height)));
            case RotationAngle.Rotate270: return (x, checked((int)(y + width)));
            default: return (x, y);
        }
    }

    protected void ValidateDeclaredNativeBounds(PrintElement element)
    {
        if (!RequireDeclaredNativeBounds) return;
        if (element is PrintBarcodeElement barcode && barcode.Width == 0)
            throw new ArgumentException("严格设备边界校验要求声明条码的目标设备预留宽度。", nameof(element));
        if (element is PrintBarcodeElement readableBarcode && readableBarcode.HumanReadable != false &&
            readableBarcode.HumanReadableReserveHeight == 0)
            throw new ArgumentException("严格设备边界校验要求声明条码可读文字的目标设备预留高度；关闭可读文字可省略。", nameof(element));
        if (element is PrintTextElement text && text.Text.Length > 0 &&
            !(Protocol == PrintProtocol.Tspl && text.DeviceFontName is string font &&
              font != "0" && IsPrintableAscii(text.Text)) &&
            (text.Width == 0 || text.Height == 0))
            throw new ArgumentException("严格设备边界校验要求声明文字的目标设备预留宽高。", nameof(element));
    }

    protected virtual void ValidatePage(PrintPage page)
    {
        if (Capabilities.DeviceProfileName != null &&
            (Capabilities.MaxPrintableWidthDots is int width && page.Width > width ||
             Capabilities.MaxLabelHeightDots is int height && page.Height > height))
            throw new NotSupportedException($"机型 {Capabilities.DeviceProfileName} 的页面尺寸超过声明的宽高限制。");
        if (page.Copies <= 0) throw new ArgumentOutOfRangeException(nameof(page.Copies));
        if (page.FeedLines < 0) throw new ArgumentOutOfRangeException(nameof(page.FeedLines));
        if (page.CutAfterPrint && !Capabilities.SupportsCut)
            throw new NotSupportedException($"协议 {Protocol} 不支持切纸。");
        if (page.CutOptions != null && !page.CutAfterPrint)
            throw new ArgumentException("显式裁切参数必须启用页面切纸。", nameof(page.CutOptions));
        if (page.CutOptions != null) ValidateCutOptions(page.CutOptions);
        else if (page.CutAfterPrint && (Protocol == PrintProtocol.EscPos || Protocol == PrintProtocol.Tspl))
            ValidateCutOptions(new PrintCutOptions());
    }

    private void ValidateProfileElementKind(PrintElement element)
    {
        if (Capabilities.DeviceProfileName != null &&
            (element is PrintTextElement && !Capabilities.SupportsText ||
             element is PrintImageElement && !Capabilities.SupportsImages ||
             (element is PrintLineElement || element is PrintRectangleElement) && !Capabilities.SupportsGraphics ||
             element is PrintBarcodeElement && !Capabilities.SupportsBarcode ||
             element is PrintQrCodeElement && !Capabilities.SupportsQrCode))
            throw new NotSupportedException($"机型 {Capabilities.DeviceProfileName} 不支持此元素类型。");
    }

    protected virtual void ValidateElement(PrintElement element)
    {
        ValidateProfileElementKind(element);
        if (element is PrintBarcodeElement gs1Barcode)
        {
            if (gs1Barcode.Type == BarcodeType.UccEan128)
                Gs1BarcodeCapacity.Validate(gs1Barcode.Type, gs1Barcode.Data,
                    Capabilities.MaxGs1LinearPayloadCharacters, nameof(gs1Barcode.Data));
            else if (gs1Barcode.Type == BarcodeType.Gs1DataBarExpanded)
                Gs1BarcodeCapacity.Validate(gs1Barcode.Type, gs1Barcode.Data,
                    Capabilities.MaxGs1DataBarExpandedPayloadCharacters, nameof(gs1Barcode.Data));
        }
        if (!Enum.IsDefined(typeof(RotationAngle), element.Rotation))
            throw new ArgumentOutOfRangeException(nameof(element.Rotation));
        if (element.Rotation != RotationAngle.None && !(element is PrintImageElement) &&
            !(element is PrintTextElement && Capabilities.SupportsTextRotation) &&
            !(element is PrintBarcodeElement && Capabilities.SupportsBarcodeRotation) &&
            !(element is PrintQrCodeElement qrCode && Capabilities.SupportsQrCodeRotationAngle(qrCode.Rotation)) &&
            !Capabilities.SupportsRotation)
            throw new NotSupportedException($"协议 {Protocol} 不支持元素旋转。");
        if (element is PrintImageElement && element.Rotation != RotationAngle.None && !Capabilities.SupportsImageRotation)
            throw new NotSupportedException($"协议 {Protocol} 不支持此模式的图片旋转。");
        if (element is PrintTextElement && element.Rotation != RotationAngle.None &&
            !Capabilities.SupportsTextRotation)
            throw new NotSupportedException($"协议 {Protocol} 不支持文字旋转。");
        if (element is PrintBarcodeElement barcode && barcode.Rotation != RotationAngle.None &&
            !Capabilities.SupportsBarcodeRotation)
            throw new NotSupportedException($"协议 {Protocol} 不支持条码旋转。");
        if (Protocol == PrintProtocol.Tspl && element.Rotation != RotationAngle.None)
        {
            if (element is PrintTextElement rotatedText && rotatedText.Text.Length > 0)
            {
                var extent = MeasureTextExtent(rotatedText, Protocol);
                if (extent.Width == 0 || extent.Height == 0)
                    throw new ArgumentException("TSPL 旋转文字需要声明旋转前的预留宽高，或使用可推导占位的固定 ASCII 字体。");
            }
            if (element is PrintBarcodeElement rotatedBarcode && MeasureBarcodeWidth(rotatedBarcode, Protocol) == 0)
                throw new ArgumentException("TSPL 旋转条码需要声明旋转前的预留宽度。");
        }
        if (element is PrintQrCodeElement qr && qr.Rotation != RotationAngle.None &&
            !Capabilities.SupportsQrCodeRotationAngle(qr.Rotation))
            throw new NotSupportedException($"协议 {Protocol} 不支持二维码旋转。");
        if (element is PrintTextElement text && text.Reverse && !Capabilities.SupportsReverseText)
            throw new NotSupportedException($"协议 {Protocol} 不支持反白文字。");
        if (element is PrintTextElement fontText && !string.IsNullOrWhiteSpace(fontText.FontFamily) &&
            (!Capabilities.SupportsHostFontFamily ||
             (Protocol == PrintProtocol.Tspl || Protocol == PrintProtocol.Zpl) &&
             !fontText.Bold && !fontText.Underline && !fontText.Reverse))
            throw new NotSupportedException($"协议 {Protocol} 不支持系统字体族；请启用显式文字样式光栅化或使用 ESC/POS Raster。");
        if (element is PrintTextElement deviceText && deviceText.DeviceFontName != null)
        {
            var font = deviceText.DeviceFontName;
            var semanticCpclFont = Protocol == PrintProtocol.Cpcl && deviceText.Font?.Kind == PrintFontKind.Device;
            if (semanticCpclFont && (font != "24" && font != "55" ||
                !Enum.IsDefined(typeof(FontSize), deviceText.FontSize) ||
                font != (deviceText.FontSize % 24 == 0 ? "24" : "55")))
                throw new ArgumentException("CPCL 设备字体必须与内置字号对应（24 或 55）。");
            if (Protocol == PrintProtocol.Tspl &&
                (font.Length != 1 || font[0] < '0' || font[0] > '8') ||
                Protocol == PrintProtocol.Zpl &&
                (font.Length != 1 || !(font[0] >= '0' && font[0] <= '9' ||
                                       font[0] >= 'A' && font[0] <= 'Z')))
                throw new ArgumentException($"协议 {Protocol} 的设备字体标识无效。");
            if (Protocol != PrintProtocol.Tspl && Protocol != PrintProtocol.Zpl && !semanticCpclFont)
                throw new NotSupportedException($"协议 {Protocol} 不支持设备字体标识。");
        }
        if (element is PrintBarcodeElement barcodeOptions)
        {
            if (Protocol != PrintProtocol.Cpcl && barcodeOptions.CpclRatioCode.HasValue)
                throw new NotSupportedException(
                    $"协议 {Protocol} 不支持 CPCL Ratio 参数；请使用该协议支持的条码模块宽度选项。");
            if ((Protocol == PrintProtocol.Zpl || Protocol == PrintProtocol.EscPos) && barcodeOptions.WideWidth != 2)
                throw new NotSupportedException(
                    $"协议 {Protocol} 不支持独立宽条宽度；WideWidth 必须保持兼容默认值 2。");
        }
        if (element is PrintBarcodeElement humanReadableBarcode &&
            RequestsBarcodeHumanReadable(humanReadableBarcode) &&
            !Capabilities.SupportsBarcodeHumanReadableType(humanReadableBarcode.Type))
            throw new NotSupportedException($"协议 {Protocol} 当前不支持 {humanReadableBarcode.Type} 条码可读文字（HRI）。");
        if (element is PrintBarcodeElement reservedBarcode && reservedBarcode.HumanReadableReserveHeight > 0)
        {
            if (!Capabilities.SupportsBarcodeHumanReadableType(reservedBarcode.Type))
                throw new NotSupportedException($"协议 {Protocol} 不支持 {reservedBarcode.Type} 条码可读文字（HRI）区域。");
            if (reservedBarcode.HumanReadable == false ||
                reservedBarcode.HumanReadable == null && Protocol == PrintProtocol.EscPos)
                throw new ArgumentException("预留可读文字高度要求启用条码可读文字。");
        }
    }

    private bool RequestsBarcodeHumanReadable(PrintBarcodeElement barcode)
        => barcode.HumanReadable == true ||
           barcode.HumanReadable == null && (Protocol == PrintProtocol.Tspl || Protocol == PrintProtocol.Zpl);

    public abstract IReadOnlyList<byte[]> Render(PrintDocument document, CancellationToken cancellationToken = default(CancellationToken));

    protected static string TsplBarcodeType(BarcodeType type)
    {
        switch (type)
        {
            case BarcodeType.Code128: return "128";
            case BarcodeType.Code39: return "39";
            case BarcodeType.Code93: return "93";
            case BarcodeType.Ean8: return "EAN8";
            case BarcodeType.Ean13: return "EAN13";
            case BarcodeType.UpcA: return "UPCA";
            case BarcodeType.UpcE: return "UPCE";
            case BarcodeType.Codabar: return "CODABAR";
            case BarcodeType.I2OF5: return "25";
            case BarcodeType.I2OF5C: return "25C";
            // I2OF5G 通过 Add 中的 BarcodeI2OF5G 根据 11/13 位 payload 选择 DPI/DPL。
            case BarcodeType.I2OF5G: return "DPI";
            case BarcodeType.Msi: return "MSI";
            case BarcodeType.UccEan128: return "EAN128";
            case BarcodeType.Postnet: return "POST";
            case BarcodeType.Planet: return "PLANET";
            case BarcodeType.Plessey: return "PLESSEY";
            case BarcodeType.Itf14: return "ITF14";
            case BarcodeType.Ean14: return "EAN14";
            case BarcodeType.Code11: return "11";
            case BarcodeType.ChinaPost: return "CPOST";
            case BarcodeType.Telepen: return "TELEPEN";
            case BarcodeType.Telepenn: return "TELEPENN";
            case BarcodeType.Code49: return "CODE49";
            default: throw new NotSupportedException($"TSPL 不支持条码类型 {type}。");
        }
    }

    protected static byte[] RenderPage(Action action, IBufferWriter writer)
    {
        action();
        return writer.GetBytes();
    }
}

/// <summary>将统一打印文档编码为 CPCL。</summary>
public sealed class CpclPrintProtocolProvider : PrintProtocolProviderBase
{
    public CpclPrintProtocolProvider(Encoding encoding = null) : base(encoding ?? Encoding.GetEncoding("gbk")) { }
    public override PrintProtocol Protocol => PrintProtocol.Cpcl;
    public override PrintCapabilities Capabilities { get; } = new PrintCapabilities(PrintProtocol.Cpcl, 200, true, true, true, true, false,
        supportsReverseText: true);

    protected override void ValidatePage(PrintPage page)
    {
        base.ValidatePage(page);
        if (page.FeedLines != 0)
            throw new NotSupportedException("CPCL Provider 当前不支持页级额外进纸。");
    }

    protected override void ValidateElement(PrintElement element)
    {
        base.ValidateElement(element);
        switch (element)
        {
            case PrintTextElement _:
            case PrintLineElement _:
            case PrintRectangleElement _:
            case PrintBarcodeElement _:
            case PrintQrCodeElement _:
            case PrintImageElement _:
                break;
            default:
                throw new NotSupportedException("CPCL Provider 不支持此元素类型。");
        }
        if (element is PrintBarcodeElement barcode &&
            (barcode.Rotation == RotationAngle.Rotate90 || barcode.Rotation == RotationAngle.Rotate180) &&
            !barcode.IsCpclTemplateRotation)
            throw new NotSupportedException("CPCL 统一条码模型仅支持 0 或顺时针 270 度；90/180 度没有稳定的直连坐标语义。");
        if (element is PrintBarcodeElement unsupportedBarcode &&
            !Capabilities.SupportsBarcodeType(unsupportedBarcode.Type))
            throw new NotSupportedException($"CPCL Provider 不支持 {unsupportedBarcode.Type} 条码。");
        if (element is PrintImageElement) return;
        var candidate = new CPCLPrintCommand(Encoding).SetPage(1, 1);
        Add(candidate, element);
        candidate.Build(ValidationCancellationToken);
    }

    public override IReadOnlyList<byte[]> Render(PrintDocument document, CancellationToken cancellationToken = default(CancellationToken))
    {
        using var operation = PrintResourceScope.BeginTemporary();
        Validate(document, cancellationToken);
        var pages = new BudgetedList<byte[]>("Cpcl.Pages", itemBytes: 8);
        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = new CPCLPrintCommand(Encoding).SetPage(page.Width, page.Height);
            foreach (var element in page.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Add(command, element, cancellationToken);
            }
            command.InitCommand(0, page.Width, page.Height, page.Copies);
            pages.AddFactory(() => command.Build(cancellationToken).GetBytes());
        }
        var result = pages.AsReadOnly();
        operation?.CommitReservations();
        return result;
    }

    private static void Add(CPCLPrintCommand command, PrintElement element, CancellationToken cancellationToken = default(CancellationToken))
    {
        switch (element)
        {
            case PrintTextElement text:
                command.DrawText(text.X, text.Y, text.Text, text.FontSize, (int)text.Rotation, text.Bold, text.Reverse, text.Underline); break;
            case PrintLineElement line:
                command.Line(line.X, line.Y, line.EndX, line.EndY, line.LineWidth); break;
            case PrintRectangleElement rectangle:
                if (rectangle.Filled && rectangle.ExactFillBounds)
                    command.Line(rectangle.X, rectangle.Y, checked(rectangle.X + rectangle.Width - 1), rectangle.Y, rectangle.Height);
                else if (rectangle.Filled) command.DrawRectFill(rectangle.X, rectangle.Y, rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height);
                else command.DrawRect(rectangle.X, rectangle.Y, rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height, rectangle.LineWidth);
                break;
            case PrintBarcodeElement barcode:
                if (barcode.HumanReadable == true) command.BarcodeText("0", 24, 0);
                command.DrawBarcode1D(barcode.Type, barcode.X, barcode.Y, barcode.Data, barcode.NarrowWidth, barcode.BarHeight,
                    barcode.CpclRatioCode ?? barcode.WideWidth, barcode.Rotation);
                if (barcode.HumanReadable == true) command.BarcodeTextOff();
                break;
            case PrintQrCodeElement qr:
                command.DrawQrCode(qr.X, qr.Y, qr.Data, qr.ModuleSize, (int)qr.ErrorLevel, (int)qr.Rotation); break;
            case PrintImageElement image:
                using (var rotated = PrintImageRotation.Prepare(image, cancellationToken))
                    command.DrawRasterImage(image.X, image.Y, rotated.Width, rotated.Height, rotated.Raster, image.Compressed);
                break;
            default: throw new NotSupportedException("未知打印元素类型。");
        }
    }

}

/// <summary>将统一打印文档编码为 TSPL。</summary>
public sealed class TsplPrintProtocolProvider : PrintProtocolProviderBase
{
    internal override bool JobCutInPage => _mediaOptions?.Profile?.Cutter?.Strategy == TsplCutStrategy.Batch;
    internal override void ValidateCutOptions(PrintCutOptions options)
        => TsplCutEncoder.Validate(options, _mediaOptions?.Profile?.Cutter);
    internal override void ValidateJobFinishing(PrintJobOptions options)
    {
        if (JobCutInPage && options.CutScope != PrintFinishScope.None && options.FeedAmount > 0)
            throw new NotSupportedException("TSPL Batch 自动裁切不能保证任务额外进纸位于裁切之前；请使用已确认的 Immediate 策略或关闭额外进纸。");
        if (_mediaOptions?.Profile?.Cutter != null && options.FeedAmount > 9999)
            throw new ArgumentOutOfRangeException(nameof(options.FeedAmount), "TSPL 受控裁切任务的单次 FEED 不能超过 9999 点。");
    }
    internal override long JobCutLength(PrintCutOptions options)
    {
        ValidateCutOptions(options);
        var cutter = _mediaOptions.Profile.Cutter;
        return JobCutInPage
            ? Encoding.GetByteCount(TsplCutEncoder.Print(cutter, 1, 1, options)) - Encoding.GetByteCount(TsplCutEncoder.Print(cutter, 1, 1))
            : Encoding.GetByteCount(TsplCutEncoder.Cut(options, cutter));
    }
    internal override byte[] BuildJobCut(PrintCutOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCutOptions(options);
        if (JobCutInPage) throw new NotSupportedException("TSPL Batch 裁切必须嵌入 PRINT 批次。");
        return new TsplPrintCommand(Encoding, _mediaOptions).Cut(options).Build(cancellationToken).GetBytes();
    }
    internal override long JobFinishingLength(int amount, PrintFeedUnit unit, bool cut)
    {
        if (amount > 0 && unit != PrintFeedUnit.Dots)
            throw new NotSupportedException("TSPL 只支持按打印点进纸的任务收尾。");
        return (amount == 0 ? 0 : Encoding.GetByteCount($"FEED {amount}\r\n")) +
            (cut ? JobCutLength(new PrintCutOptions()) : 0);
    }
    internal override byte[] BuildJobFinishing(int amount, PrintFeedUnit unit, bool cut, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JobFinishingLength(amount, unit, cut);
        var command = new TsplPrintCommand(Encoding, _mediaOptions);
        if (amount > 0) command.Feed(amount);
        if (cut) command.Cut(new PrintCutOptions());
        return command.Build(cancellationToken).GetBytes();
    }
    private readonly TsplMediaOptions _mediaOptions;
    private readonly bool _requireDeclaredNativeBounds;
    public TsplPrintProtocolProvider(Encoding encoding = null, TsplMediaOptions options = null,
        bool requireDeclaredNativeBounds = false)
        : base(encoding ?? Encoding.GetEncoding("gbk"))
    {
        var configuration = new TsplPrintCommand(Encoding, options);
        _mediaOptions = options?.Clone();
        _requireDeclaredNativeBounds = requireDeclaredNativeBounds;
        Capabilities = configuration.Capabilities;
    }
    public override PrintProtocol Protocol => PrintProtocol.Tspl;
    public override PrintCapabilities Capabilities { get; }
    protected override bool RequireDeclaredNativeBounds => _requireDeclaredNativeBounds;

    protected override void ValidatePage(PrintPage page)
    {
        base.ValidatePage(page);
        if (page.FeedLines > 0)
            throw new NotSupportedException(
                "TSPL Provider 不支持按进纸行数表示的页级额外进纸；TSPL FEED 使用打印点距离，请使用独立 TsplPrintCommand.Feed(distance)。");
        new TsplPrintCommand(Encoding, _mediaOptions).Size(page.Width, page.Height, "dot");
    }

    protected override void ValidateElement(PrintElement element)
    {
        base.ValidateElement(element);
        switch (element)
        {
            case PrintTextElement text when (text.Bold || text.Underline || text.Reverse) &&
                                             (!Capabilities.SupportsBoldText && text.Bold ||
                                              !Capabilities.SupportsUnderlineText && text.Underline ||
                                              !Capabilities.SupportsReverseText && text.Reverse):
                throw new NotSupportedException("TSPL 内置字体当前不支持请求的文字样式。");
            case PrintBarcodeElement barcode:
                TsplBarcodeType(barcode.Type);
                break;
            case PrintQrCodeElement qr when qr.ModuleSize > 10:
                throw new NotSupportedException("TSPL 二维码模块尺寸不能超过 10。");
            case PrintTextElement _:
            case PrintLineElement _:
            case PrintRectangleElement _:
            case PrintQrCodeElement _:
            case PrintImageElement _:
                break;
            default:
                throw new NotSupportedException("TSPL Provider 不支持此元素类型。");
        }
        ValidateDeclaredNativeBounds(element);
        Add(new TsplPrintCommand(Encoding, _mediaOptions), element, Capabilities.Dpi, ValidationCancellationToken);
    }

    public override IReadOnlyList<byte[]> Render(PrintDocument document, CancellationToken cancellationToken = default(CancellationToken))
    {
        using var operation = PrintResourceScope.BeginTemporary();
        Validate(document, cancellationToken);
        var pages = new BudgetedList<byte[]>("Tspl.Pages", itemBytes: 8);
        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = new TsplPrintCommand(Encoding, _mediaOptions)
                .DisablePageBoundsValidation().Size(page.Width, page.Height, "dot");
            foreach (var element in page.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Add(command, element, Capabilities.Dpi, cancellationToken);
            }
            if (page.CutAfterPrint) command.PrintWithCut(page.CutOptions ?? new PrintCutOptions(), 1, page.Copies);
            else command.Print(1, page.Copies);
            pages.AddFactory(() => command.Build(cancellationToken).GetBytes());
        }
        var result = pages.AsReadOnly();
        operation?.CommitReservations();
        return result;
    }

    private static void Add(TsplPrintCommand command, PrintElement element, int dpi, CancellationToken cancellationToken)
    {
        switch (element)
        {
            case PrintTextElement text:
                var font = text.DeviceFontName ?? "0";
                var pointSize = font == "0"
                    ? Math.Max(1, (int)Math.Round(text.FontSize * 72.0 / dpi,
                        MidpointRounding.AwayFromZero))
                    : TsplFontSizing.FixedScale(font, text.FontSize);
                if (text.Bold || text.Underline || text.Reverse)
                    command.TextRasterized(text.X, text.Y, text.Text, pointSize, text.Bold, text.Underline,
                        text.Reverse, (int)text.Rotation, text.FontFamily, cancellationToken);
                else
                {
                    var textExtent = MeasureTextExtent(text, PrintProtocol.Tspl);
                    var textOrigin = TsplRotationOrigin(text.X, text.Y, textExtent.Width, textExtent.Height, text.Rotation);
                    command.Text(textOrigin.X, textOrigin.Y, text.DeviceFontName ?? "0", (int)text.Rotation,
                        pointSize, pointSize, text.Text);
                }
                break;
            case PrintLineElement line:
                command.Line(line.X, line.Y, line.EndX, line.EndY, line.LineWidth, cancellationToken); break;
            case PrintRectangleElement rectangle:
                if (rectangle.Filled)
                    command.FillRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, cancellationToken);
                else command.Box(rectangle.X, rectangle.Y, rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height, rectangle.LineWidth);
                break;
            case PrintBarcodeElement barcode:
                var barcodeOrigin = TsplRotationOrigin(barcode.X, barcode.Y,
                    MeasureBarcodeWidth(barcode, PrintProtocol.Tspl, cancellationToken), barcode.Height, barcode.Rotation);
                var humanReadable = barcode.HumanReadable == false ? 0 : 1;
                if (barcode.Type == BarcodeType.I2OF5G)
                    command.BarcodeI2OF5G(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else if (barcode.Type == BarcodeType.Postnet)
                    command.BarcodePostnet(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else if (barcode.Type == BarcodeType.Planet)
                    command.BarcodePlanet(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else if (barcode.Type == BarcodeType.Plessey)
                    command.BarcodePlessey(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else if (barcode.Type == BarcodeType.Itf14)
                    command.BarcodeItf14(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else if (barcode.Type == BarcodeType.Ean14)
                    command.BarcodeEan14(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else if (barcode.Type == BarcodeType.ChinaPost)
                    command.BarcodeChinaPost(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else if (barcode.Type == BarcodeType.Telepen)
                    command.BarcodeTelepen(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else if (barcode.Type == BarcodeType.Telepenn)
                    command.BarcodeTelepenn(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else if (barcode.Type == BarcodeType.Code49)
                    command.BarcodeCode49(barcodeOrigin.X, barcodeOrigin.Y, barcode.Data, barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth);
                else
                    command.Barcode(barcodeOrigin.X, barcodeOrigin.Y, TsplBarcodeType(barcode.Type), barcode.BarHeight,
                        humanReadable, (int)barcode.Rotation, barcode.NarrowWidth, barcode.WideWidth, barcode.Data);
                break;
            case PrintQrCodeElement qr:
                command.QrCode(qr.X, qr.Y, (int)qr.ErrorLevel, qr.ModuleSize, "A", (int)qr.Rotation, qr.Data); break;
            case PrintImageElement image:
                using (var rotated = PrintImageRotation.Prepare(image, cancellationToken))
                    command.Bitmap(image.X, image.Y, rotated.RowBytes, rotated.Height, 0, rotated.Raster, cancellationToken);
                break;
            default: throw new NotSupportedException("未知打印元素类型。");
        }
    }
}

/// <summary>将统一打印文档编码为 ZPL。</summary>
public sealed class ZplPrintProtocolProvider : PrintProtocolProviderBase, IPrintOutputProtocolProvider
{
    internal override void ValidateCutOptions(PrintCutOptions options)
        => ZplCutEncoder.Validate(options, _mediaOptions?.Profile?.Cutter);
    internal override long JobCutLength(PrintCutOptions options)
    {
        var cutter = _mediaOptions?.Profile?.Cutter;
        ValidateCutOptions(options);
        return Encoding.GetByteCount(ZplCutEncoder.CutPrint(1, options, cutter)) -
            Encoding.GetByteCount(ZplCutEncoder.NeutralPrint(1, cutter)) +
            (cutter.Strategy == ZplCutStrategy.Delayed ? Encoding.GetByteCount(ZplCutEncoder.DelayedControl) : 0);
    }
    // ^CN 必须由本页 Render 放在 ^XZ 前，任务渲染器仅用此长度计算预算。
    internal override long JobFinishingLength(int amount, PrintFeedUnit unit, bool cut)
    {
        if (amount != 0) throw new NotSupportedException("ZPL 当前不支持任务额外进纸。");
        if (cut && !Capabilities.SupportsCut) throw new NotSupportedException("机型不支持切纸。");
        if (cut && _mediaOptions?.Profile?.Cutter != null) return JobCutLength(new PrintCutOptions());
        return cut ? Encoding.GetByteCount("^CN\r\n") : 0;
    }
    private readonly ZplMediaOptions _mediaOptions;
    private readonly bool _requireDeclaredNativeBounds;
    public ZplPrintProtocolProvider(Encoding encoding = null, ZplMediaOptions options = null,
        bool requireDeclaredNativeBounds = false)
        : base(encoding ?? Encoding.UTF8)
    {
        var configuration = new ZplPrintCommand(Encoding, options);
        _mediaOptions = options?.Clone();
        _requireDeclaredNativeBounds = requireDeclaredNativeBounds;
        Capabilities = configuration.Capabilities;
    }
    public override PrintProtocol Protocol => PrintProtocol.Zpl;
    public override PrintCapabilities Capabilities { get; }
    protected override bool RequireDeclaredNativeBounds => _requireDeclaredNativeBounds;

    protected override void ValidatePage(PrintPage page)
    {
        base.ValidatePage(page);
        if (page.FeedLines != 0)
            throw new NotSupportedException("ZPL Provider 当前不支持页级额外进纸。");
        new ZplPrintCommand(Encoding, _mediaOptions).Start().Label(page.Width, page.Height);
        ZplCutEncoder.ValidateQuantity(page.Copies);
        if (page.CutAfterPrint && _mediaOptions?.Profile?.Cutter != null)
            ValidateCutOptions(page.CutOptions ?? new PrintCutOptions());
    }

    protected override void ValidateElement(PrintElement element)
    {
        base.ValidateElement(element);
        switch (element)
        {
            case PrintTextElement text when (text.Bold || text.Underline || text.Reverse) &&
                                             (!Capabilities.SupportsBoldText && text.Bold ||
                                              !Capabilities.SupportsUnderlineText && text.Underline ||
                                              !Capabilities.SupportsReverseText && text.Reverse):
                throw new NotSupportedException("ZPL 内置字体当前不支持请求的文字样式。");
            case PrintBarcodeElement barcode when barcode.Type != BarcodeType.Code128 &&
                                                  barcode.Type != BarcodeType.Code39 &&
                                                  barcode.Type != BarcodeType.Code93 &&
                                                  barcode.Type != BarcodeType.Ean8 &&
                                                  barcode.Type != BarcodeType.Ean13 &&
                                                  barcode.Type != BarcodeType.UpcA &&
                                                  barcode.Type != BarcodeType.UpcE &&
                                                  barcode.Type != BarcodeType.Codabar &&
                                                   barcode.Type != BarcodeType.I2OF5 &&
                                                   barcode.Type != BarcodeType.I2OF5C &&
                                                   barcode.Type != BarcodeType.I2OF5G &&
                                                   barcode.Type != BarcodeType.Msi &&
                                                   barcode.Type != BarcodeType.UccEan128 &&
                                                   barcode.Type != BarcodeType.Postnet &&
                                                   barcode.Type != BarcodeType.Planet &&
                                                   barcode.Type != BarcodeType.IntelligentMail &&
                                                   barcode.Type != BarcodeType.Code11 &&
                                                   barcode.Type != BarcodeType.Plessey &&
                                                   barcode.Type != BarcodeType.Code49 &&
                                                   barcode.Type != BarcodeType.Gs1DataBarOmnidirectional &&
                                                   barcode.Type != BarcodeType.Gs1DataBarTruncated &&
                                                   barcode.Type != BarcodeType.Gs1DataBarLimited &&
                                                   barcode.Type != BarcodeType.Gs1DataBarExpanded:
                throw new NotSupportedException("ZPL Provider 当前支持 Code 128、Code 39、Code 93、Code 11、Plessey、Code 49、GS1 DataBar、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G、GS1-128、MSI、POSTNET、PLANET 和 Intelligent Mail 条码。");
            case PrintQrCodeElement qr when qr.ModuleSize > 10:
                throw new NotSupportedException("ZPL 二维码模块尺寸不能超过 10。");
            case PrintTextElement _:
            case PrintLineElement _:
            case PrintRectangleElement _:
            case PrintBarcodeElement _:
            case PrintQrCodeElement _:
            case PrintImageElement _:
                break;
            default:
                throw new NotSupportedException("ZPL Provider 不支持此元素类型。");
        }
        if (element is PrintBarcodeElement dataBar &&
            dataBar.Type >= BarcodeType.Gs1DataBarOmnidirectional &&
            dataBar.Type <= BarcodeType.Gs1DataBarExpanded && dataBar.HumanReadable == true)
            throw new NotSupportedException("ZPL ^BR 没有 HRI 参数；请将 HumanReadable 设为 false，或追加显式文字元素。");
        ValidateDeclaredNativeBounds(element);
        Add(new ZplPrintCommand(Encoding, _mediaOptions), element, ValidationCancellationToken);
    }

    public override IReadOnlyList<byte[]> Render(PrintDocument document, CancellationToken cancellationToken = default(CancellationToken))
    {
        using var operation = PrintResourceScope.BeginTemporary();
        cancellationToken.ThrowIfCancellationRequested();
        if (_mediaOptions?.Profile?.Cutter?.Strategy == ZplCutStrategy.Delayed && document != null)
            for (var index = 0; index < document.Pages.Count; index++)
                if (document.Pages[index].CutAfterPrint)
                    throw new PrintCapabilityException(Protocol, $"Pages[{index}]",
                        new NotSupportedException("Delayed 裁切须使用 RenderOutput，保留独立 ~JK 文件。"), index, null);
        var outputs = RenderOutput(document, cancellationToken);
        var pages = new BudgetedList<byte[]>("Zpl.Pages", itemBytes: 8, capacity: outputs.Count);
        foreach (var page in outputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pages.AddFactory(() => page.Data);
        }
        var result = pages.AsReadOnly();
        operation?.CommitReservations();
        return result;
    }

    /// <summary>生成页面及独立控制文件，保留 Delayed 裁切的文件边界。</summary>
    public IReadOnlyList<PrintPageOutput> RenderOutput(PrintDocument document, CancellationToken cancellationToken = default(CancellationToken))
    {
        using var operation = PrintResourceScope.BeginTemporary();
        cancellationToken.ThrowIfCancellationRequested();
        Validate(document, cancellationToken);
        var pages = new BudgetedList<PrintPageOutput>("Zpl.OutputPages", itemBytes: 8);
        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = new ZplPrintCommand(Encoding, _mediaOptions)
                .DisablePageBoundsValidation().Start().Label(page.Width, page.Height);
            foreach (var element in page.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Add(command, element, cancellationToken);
            }
            if (page.CutAfterPrint && _mediaOptions?.Profile?.Cutter != null)
                command.PrintWithCut(page.CutOptions ?? new PrintCutOptions(), page.Copies);
            else
            {
                command.Print(page.Copies);
                if (page.CutAfterPrint) command.Cut();
            }
            command.End();
            pages.AddFactory(() => command.BuildOutput(cancellationToken));
        }
        var result = pages.AsReadOnly();
        operation?.CommitReservations();
        return result;
    }

    private static void Add(ZplPrintCommand command, PrintElement element, CancellationToken cancellationToken)
    {
        switch (element)
        {
            case PrintTextElement text:
                if (text.Bold || text.Underline || text.Reverse)
                    command.TextRasterized(text.X, text.Y, text.Text, Math.Max(1, text.FontSize),
                        text.Bold, text.Underline, text.Reverse, (int)text.Rotation, text.FontFamily,
                        cancellationToken);
                else
                    command.Text(text.X, text.Y, text.Text, Math.Max(1, text.FontSize),
                        Math.Max(1, text.FontSize), text.DeviceFontName ?? "0", (int)text.Rotation);
                break;
            case PrintLineElement line:
                command.Line(line.X, line.Y, line.EndX, line.EndY, line.LineWidth, cancellationToken); break;
            case PrintRectangleElement rectangle:
                if (rectangle.Filled)
                    command.FillRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, cancellationToken);
                else command.Box(rectangle.X, rectangle.Y, rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height, rectangle.LineWidth);
                break;
            case PrintBarcodeElement barcode:
                if (barcode.Type == BarcodeType.Code39)
                    command.Barcode39(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Code93)
                    command.Barcode93(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, false, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Code11)
                    command.BarcodeCode11(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Plessey)
                    command.BarcodePlessey(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Code49)
                    command.BarcodeCode49(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Code128)
                    command.Barcode(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.UccEan128)
                    command.BarcodeUccEan128(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Ean8)
                    command.BarcodeEan8(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Ean13)
                    command.BarcodeEan13(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.UpcA)
                    command.BarcodeUpcA(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.UpcE)
                    command.BarcodeUpcE(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Codabar)
                    command.BarcodeCodabar(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.I2OF5)
                    command.BarcodeI2OF5(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, false, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.I2OF5C)
                    command.BarcodeI2OF5C(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.I2OF5G)
                    command.BarcodeI2OF5G(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Msi)
                    command.BarcodeMsi(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, 'A', false, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Postnet)
                    command.BarcodePostnet(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Planet)
                    command.BarcodePlanet(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.IntelligentMail)
                    command.BarcodeIntelligentMail(barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                        barcode.HumanReadable != false, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Gs1DataBarOmnidirectional)
                    command.BarcodeGs1DataBarOmnidirectional(barcode.X, barcode.Y, barcode.Data,
                        barcode.BarHeight, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Gs1DataBarTruncated)
                    command.BarcodeGs1DataBarTruncated(barcode.X, barcode.Y, barcode.Data,
                        barcode.BarHeight, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Gs1DataBarLimited)
                    command.BarcodeGs1DataBarLimited(barcode.X, barcode.Y, barcode.Data,
                        barcode.BarHeight, barcode.NarrowWidth, (int)barcode.Rotation);
                else if (barcode.Type == BarcodeType.Gs1DataBarExpanded)
                    command.BarcodeGs1DataBarExpanded(barcode.X, barcode.Y, barcode.Data,
                        barcode.BarHeight, barcode.NarrowWidth, 22, (int)barcode.Rotation);
                else throw new NotSupportedException("ZPL Provider 当前支持 Code 128、Code 39、Code 93、Code 11、Plessey、Code 49、GS1 DataBar、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5、I2OF5C、I2OF5G、GS1-128、MSI、POSTNET、PLANET 和 Intelligent Mail 条码。");
                break;
            case PrintQrCodeElement qr:
                command.QrCode(qr.X, qr.Y, qr.Data, qr.ModuleSize, "LMQH"[(int)qr.ErrorLevel], (int)qr.Rotation); break;
            case PrintImageElement image:
                using (var rotated = PrintImageRotation.Prepare(image, cancellationToken))
                    command.GraphicField(image.X, image.Y, rotated.RowBytes, rotated.Height, rotated.Raster, cancellationToken);
                break;
            default: throw new NotSupportedException("未知打印元素类型。");
        }
    }
}

/// <summary>将统一打印文档编码为 ESC/POS。</summary>
public sealed class EscPosPrintProtocolProvider : PrintProtocolProviderBase
{
    internal override void ValidateCutOptions(PrintCutOptions options)
        => EscPosCutEncoder.Encode(options, _deviceOptions?.Profile);
    internal override long JobCutLength(PrintCutOptions options)
        => EscPosCutEncoder.Encode(options, _deviceOptions?.Profile).Length;
    internal override byte[] BuildJobCut(PrintCutOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EscPosCutEncoder.Encode(options, _deviceOptions?.Profile);
    }
    private long JobFeedMotionAmount(int amount, PrintFeedUnit unit)
    {
        if (amount == 0) return 0;
        if (!System.Linq.Enumerable.Contains(Capabilities.JobFeedUnits, unit))
            throw new NotSupportedException("ESC/POS 的打印点进纸要求显式 Profile，其他进纸单位须与设备能力匹配。");
        if (unit != PrintFeedUnit.Dots) return amount;
        var profile = _deviceOptions.Profile;
        return checked(((long)amount * profile.MotionUnitsPerInch + profile.VerticalDpi - 1) / profile.VerticalDpi);
    }
    internal override long JobFinishingLength(int amount, PrintFeedUnit unit, bool cut)
    {
        if (cut) ValidateCutOptions(new PrintCutOptions());
        var units = JobFeedMotionAmount(amount, unit);
        return checked((units == 0 ? 0 : (units + 254) / 255 * 3) + (cut ? 3 : 0));
    }
    internal override byte[] BuildJobFinishing(int amount, PrintFeedUnit unit, bool cut, CancellationToken cancellationToken)
    {
        var length = JobFinishingLength(amount, unit, cut);
        if (length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(amount), "任务进纸指令超出可表示的字节数。");
        cancellationToken.ThrowIfCancellationRequested();
        return Bing.EasyPrint.Core.BudgetedBuffer.Create((int)length, "EscPos.JobFinishing", bytes =>
        {
            var units = JobFeedMotionAmount(amount, unit);
            var offset = 0;
            while (units > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = (byte)Math.Min(255L, units);
                bytes[offset++] = 0x1B;
                bytes[offset++] = unit == PrintFeedUnit.Lines ? (byte)0x64 : (byte)0x4A;
                bytes[offset++] = batch;
                units -= batch;
            }
            if (cut) { bytes[offset++] = 0x1D; bytes[offset++] = 0x56; bytes[offset] = 0; }
        });
    }
    // Keep direct Provider calls within the same temporary image budget used by
    // image processing and template rendering.
    private const long DefaultRasterTextMeasurementMaxPixels = 16_000_000;
    private const int RasterHumanReadableFontSize = EscPosRasterCanvas.DefaultHumanReadableFontSize;
    private readonly EscPosLayoutMode _layoutMode;
    private readonly EscPosDeviceOptions _deviceOptions;
    private readonly int _rasterHumanReadableFontSize;
    public EscPosPrintProtocolProvider(Encoding encoding = null, EscPosLayoutMode layoutMode = EscPosLayoutMode.Native,
        EscPosDeviceOptions options = null)
        : base(encoding ?? Encoding.GetEncoding("gb18030"))
    {
        _layoutMode = layoutMode;
        _deviceOptions = options == null ? null : new EscPosDeviceOptions
        {
            Dpi = options.Dpi,
            RasterFonts = options.RasterFonts,
            Profile = options.Profile?.Snapshot(),
            CodePageIndex = options.CodePageIndex,
            PrintAreaWidthUnits = options.PrintAreaWidthUnits,
            NativeTabWidthDots = options.NativeTabWidthDots,
            RasterBarcodeHumanReadableFontSize = options.RasterBarcodeHumanReadableFontSize,
            NativeFlow = options.NativeFlow
        };
        if (_deviceOptions?.NativeTabWidthDots is int tabWidth && (tabWidth < 8 || tabWidth % 8 != 0))
            throw new ArgumentOutOfRangeException(nameof(options.NativeTabWidthDots),
                "Native 制表位点距必须是至少 8 点的 8 的倍数。");
        var configuration = new EscPosPrintCommand(Encoding, layoutMode, _deviceOptions);
        Capabilities = configuration.Capabilities;
        _rasterHumanReadableFontSize = Capabilities.RasterBarcodeHumanReadableFontSize ?? RasterHumanReadableFontSize;
    }
    public override PrintProtocol Protocol => PrintProtocol.EscPos;
    public override PrintCapabilities Capabilities { get; }

    public override void Validate(PrintDocument document)
    {
        base.Validate(document);
        var cancellationToken = ValidationCancellationToken;
        if (_layoutMode == EscPosLayoutMode.Raster)
        {
            for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = document.Pages[pageIndex];
                for (var elementIndex = 0; elementIndex < page.Elements.Count; elementIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var element = page.Elements[elementIndex];
                    try { ValidateRasterBounds(page, element); }
                    catch (NotSupportedException error)
                    { throw new PrintCapabilityException(Protocol, element.SourcePath ?? $"Pages[{pageIndex}].Elements[{elementIndex}]", error, pageIndex, elementIndex); }
                    catch (ArgumentException error)
                    { throw new PrintElementValidationException(Protocol, element.SourcePath ?? $"Pages[{pageIndex}].Elements[{elementIndex}]", error, pageIndex, elementIndex); }
                }
            }
            return;
        }
        if (_layoutMode != EscPosLayoutMode.Native) return;
        for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
            WalkNativeRows(document.Pages[pageIndex], pageIndex, null, cancellationToken);
    }

    private void ValidateRasterBounds(PrintPage page, PrintElement element)
    {
        switch (element)
        {
            case PrintTextElement text:
                var ink = text.Reverse
                    ? EscPosRasterCanvas.MeasureReverseTextBounds(text.Text, text.FontSize, text.Bold,
                        text.Underline, text.FontFamily, DefaultRasterTextMeasurementMaxPixels,
                        _deviceOptions?.RasterFonts, ValidationCancellationToken)
                    : EscPosRasterCanvas.MeasureTextInkBounds(text.Text, text.FontSize,
                        text.Bold, text.Underline, text.FontFamily,
                        DefaultRasterTextMeasurementMaxPixels, _deviceOptions?.RasterFonts, ValidationCancellationToken);
                var quarterTurn = text.Rotation == RotationAngle.Rotate90 || text.Rotation == RotationAngle.Rotate270;
                var inkWidth = quarterTurn ? ink.Height : ink.Width;
                var inkHeight = quarterTurn ? ink.Width : ink.Height;
                if (!ink.IsEmpty)
                {
                    var left = text.Reverse || quarterTurn ? (long)text.X : (long)text.X + ink.Left;
                    var top = text.Reverse || quarterTurn ? (long)text.Y : (long)text.Y + ink.Top;
                    var right = text.Reverse || quarterTurn ? (long)text.X + inkWidth : (long)text.X + ink.Right;
                    var bottom = text.Reverse || quarterTurn ? (long)text.Y + inkHeight : (long)text.Y + ink.Bottom;
                    if (left < 0 || top < 0 || right > page.Width || bottom > page.Height)
                        throw new ArgumentOutOfRangeException(nameof(element),
                            $"文字{(text.Rotation == RotationAngle.None ? string.Empty : "旋转后")}墨迹范围 ({left},{top})-({right},{bottom}) 超出打印页 {page.Width}x{page.Height}。");
                }
                break;
            case PrintBarcodeElement barcode:
                var barcodeWidth = EscPosRasterCanvas.MeasureBarcodeWidth(barcode.Type, barcode.Data,
                    barcode.NarrowWidth, ValidationCancellationToken);
                var barcodeSymbolHeight = EscPosRasterCanvas.MeasureBarcodeHeight(barcode.Type, barcode.Data,
                    barcode.BarHeight, barcode.NarrowWidth, ValidationCancellationToken);
                var barcodeRasterHeight = checked(barcodeSymbolHeight +
                    (barcode.HumanReadable == true ? barcode.HumanReadableReserveHeight : 0));
                var barcodeQuarterTurn = barcode.Rotation == RotationAngle.Rotate90 ||
                                          barcode.Rotation == RotationAngle.Rotate270;
                var barcodeOutputWidth = barcodeQuarterTurn ? barcodeRasterHeight : barcodeWidth;
                var barcodeOutputHeight = barcodeQuarterTurn ? barcodeWidth : barcodeRasterHeight;
                if ((long)barcode.X + barcodeOutputWidth > page.Width ||
                    (long)barcode.Y + barcodeOutputHeight > page.Height)
                    throw new ArgumentOutOfRangeException(nameof(element), "条码输出区超出打印页范围。");
                if (barcode.HumanReadable == true)
                {
                    var hriData = EscPosRasterCanvas.HumanReadableBarcodeData(barcode.Type, barcode.Data);
                    if (barcode.Type == BarcodeType.UccEan128)
                        hriData = hriData.Replace("\u001D", string.Empty);
                    var hri = EscPosRasterCanvas.MeasureTextInkBounds(hriData, _rasterHumanReadableFontSize,
                        false, false, null, DefaultRasterTextMeasurementMaxPixels,
                        _deviceOptions?.RasterFonts, ValidationCancellationToken);
                    if (barcode.HumanReadableReserveHeight > 0 && !hri.IsEmpty &&
                        hri.Bottom > barcode.HumanReadableReserveHeight)
                        throw new ArgumentOutOfRangeException(nameof(element), "条码可读文字墨迹超出声明的预留高度。");
                    if (!hri.IsEmpty && !barcodeQuarterTurn)
                    {
                        var hriTextWidth = EscPosRasterCanvas.MeasureTextWidth(hriData,
                            _rasterHumanReadableFontSize, false, null, _deviceOptions?.RasterFonts,
                            ValidationCancellationToken);
                        var hriX = checked((long)barcode.X +
                            EscPosRasterCanvas.HumanReadableBarcodeTextOffset(barcode.Type,
                                barcode.NarrowWidth, barcodeWidth, hriTextWidth));
                        if (hriX + hri.Right > page.Width ||
                            (long)barcode.Y + barcodeSymbolHeight + hri.Bottom > page.Height)
                            throw new ArgumentOutOfRangeException(nameof(element), "条码可读文字墨迹超出打印页范围。");
                    }
                }
                break;
            case PrintQrCodeElement qr:
                var qrSize = EscPosRasterCanvas.MeasureQrCodeSize(qr.Data, qr.ModuleSize, qr.ErrorLevel, ValidationCancellationToken);
                var qrReservedWidth = Math.Max(qr.Width, qrSize);
                var qrReservedHeight = Math.Max(qr.Height, qrSize);
                var qrQuarterTurn = qr.Rotation == RotationAngle.Rotate90 || qr.Rotation == RotationAngle.Rotate270;
                var qrOutputWidth = qrQuarterTurn ? qrReservedHeight : qrReservedWidth;
                var qrOutputHeight = qrQuarterTurn ? qrReservedWidth : qrReservedHeight;
                if ((long)qr.X + qrOutputWidth > page.Width || (long)qr.Y + qrOutputHeight > page.Height)
                    throw new ArgumentOutOfRangeException(nameof(element), "二维码编码区超出打印页范围。");
                break;
            case PrintLineElement line:
                ValidateStrokeBounds(page, Math.Min(line.X, line.EndX), Math.Min(line.Y, line.EndY),
                    Math.Max(line.X, line.EndX), Math.Max(line.Y, line.EndY), line.LineWidth);
                break;
            case PrintRectangleElement rectangle when !rectangle.Filled:
                ValidateStrokeBounds(page, rectangle.X, rectangle.Y,
                    (long)rectangle.X + rectangle.Width - 1, (long)rectangle.Y + rectangle.Height - 1,
                    rectangle.LineWidth);
                break;
        }
    }

    private static void ValidateStrokeBounds(PrintPage page, long left, long top, long right, long bottom, int width)
    {
        var radius = width / 2;
        if (left - radius < 0 || top - radius < 0 || right + radius >= page.Width || bottom + radius >= page.Height)
            throw new ArgumentOutOfRangeException(nameof(width), "线条画笔超出打印页范围。");
    }

    protected override void ValidatePage(PrintPage page)
    {
        base.ValidatePage(page);
        if (page.FeedLines > 255)
            throw new ArgumentOutOfRangeException(nameof(page.FeedLines), "ESC/POS 进纸行数不能超过 255。");
        if (_deviceOptions?.Profile?.PrintableWidthDots is int maximum && page.Width > maximum)
            throw new ArgumentOutOfRangeException(nameof(page.Width),
                $"页面宽度超过设备 {_deviceOptions.Profile.Name} 的 {maximum} 点可打印区域。");
        if (_layoutMode == EscPosLayoutMode.Raster && ((long)page.Width + 7) / 8 > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page.Width), "ESC/POS 光栅页宽超出协议范围。");
        if (_layoutMode == EscPosLayoutMode.Raster && page.Height > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page.Height), "ESC/POS 光栅页高超出协议范围。");
        if (_layoutMode == EscPosLayoutMode.Raster && ((long)page.Width + 7) / 8 * page.Height > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page), "ESC/POS 光栅页缓冲区超出可表示范围。");
        if (_layoutMode == EscPosLayoutMode.Raster)
            EscPosImageEncoder.Validate(page.Width, page.Height, _deviceOptions?.Profile, false, false);
    }

    protected override void ValidateElement(PrintElement element)
    {
        base.ValidateElement(element);
        if (_layoutMode == EscPosLayoutMode.Native)
        {
            if (element is PrintTextElement fontText && !string.IsNullOrWhiteSpace(fontText.FontFamily))
                throw new NotSupportedException("ESC/POS Native 不支持系统字体族；请使用 Raster 模式。");
            if ((element.X != 0 && (!(element is PrintTextElement) || _deviceOptions?.NativeTabWidthDots == null)) ||
                (!(element is PrintTextElement) && element.Y != 0 &&
                 _deviceOptions?.NativeFlow != EscPosNativeFlowMode.Sequential))
                throw new NotSupportedException("ESC/POS Native Provider 不支持绝对坐标，请从原点按顺序布局。");
            switch (element)
            {
                case PrintTextElement text when text.FontSize == 24:
                    if (_deviceOptions?.NativeFlow == EscPosNativeFlowMode.Sequential &&
                        text.Height != 0 && text.Height != 24 && text.Height != 26)
                        throw new NotSupportedException("Native 混合收据文字占位高度只能为 24 或 26 点逻辑行；未知高度使用零。");
                    break;
                case PrintBarcodeElement barcode when
                    (barcode.Type == BarcodeType.Code128 || barcode.Type == BarcodeType.Code39 ||
                     barcode.Type == BarcodeType.Code93 || barcode.Type == BarcodeType.Ean8 ||
                     barcode.Type == BarcodeType.Ean13 || barcode.Type == BarcodeType.UpcA ||
                     barcode.Type == BarcodeType.UpcE || barcode.Type == BarcodeType.Codabar ||
                     barcode.Type == BarcodeType.I2OF5 || barcode.Type == BarcodeType.I2OF5C ||
                     barcode.Type == BarcodeType.I2OF5G || barcode.Type == BarcodeType.UccEan128 ||
                     barcode.Type == BarcodeType.Gs1DataBarOmnidirectional ||
                     barcode.Type == BarcodeType.Gs1DataBarTruncated ||
                     barcode.Type == BarcodeType.Gs1DataBarLimited ||
                     barcode.Type == BarcodeType.Gs1DataBarExpanded ||
                     barcode.Type == BarcodeType.Code128Auto) &&
                    barcode.NarrowWidth >= 2 && barcode.NarrowWidth <= 6 && barcode.BarHeight <= 255 &&
                    barcode.WideWidth > 0:
                    break;
                case PrintQrCodeElement qr when qr.ModuleSize <= 16:
                    break;
                default:
                    throw new NotSupportedException("ESC/POS Native Provider 只支持原点顺序文字、Code 128/Code 128 auto、GS1-128、GS1 DataBar、Code 39、Code 93、EAN8、EAN13、UPC-A、UPC-E、Codabar、I2OF5/I2OF5C/I2OF5G 和二维码及其原生参数范围。");
            }
            AddNative(new EscPosPrintCommand(Encoding, EscPosLayoutMode.Native, _deviceOptions), element, ValidationCancellationToken);
            return;
        }

        if (element is PrintBarcodeElement rasterBarcode &&
            !EscPosRasterCanvas.SupportsBarcodeType(rasterBarcode.Type))
            throw new NotSupportedException($"ESC/POS Raster Provider 当前不支持主机条码类型 {rasterBarcode.Type}。");
        if (element is PrintBarcodeElement rasterHri && rasterHri.HumanReadable == true &&
            rasterHri.HumanReadableReserveHeight <= 0)
            throw new NotSupportedException("ESC/POS Raster 开启条码可读文字时须声明正的 HumanReadableReserveHeight。");
        if (element is PrintQrCodeElement rasterQr && rasterQr.Data.Length == 0)
            throw new ArgumentException("二维码内容不能为空。", nameof(element));
        if (element is PrintBarcodeElement rasterCode && rasterCode.Data.Length == 0)
            throw new ArgumentException("条码内容不能为空。", nameof(element));
        if (element is PrintTextElement rasterText && !string.IsNullOrWhiteSpace(rasterText.FontFamily))
            EscPosRasterCanvas.ResolveFontFamilyName(rasterText.Text, rasterText.FontFamily, rasterText.Bold,
                _deviceOptions?.RasterFonts, ValidationCancellationToken);
        if (!(element is PrintTextElement) && !(element is PrintLineElement) &&
            !(element is PrintRectangleElement) && !(element is PrintImageElement) &&
            !(element is PrintBarcodeElement) && !(element is PrintQrCodeElement))
            throw new NotSupportedException("ESC/POS Raster Provider 不支持此元素类型。");
    }

    public override IReadOnlyList<byte[]> Render(PrintDocument document, CancellationToken cancellationToken = default(CancellationToken))
    {
        using var operation = PrintResourceScope.BeginTemporary();
        Validate(document, cancellationToken);
        var pages = new BudgetedList<byte[]>("EscPos.Pages", itemBytes: 8);
        for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
        {
            var page = document.Pages[pageIndex];
            cancellationToken.ThrowIfCancellationRequested();
            var command = new EscPosPrintCommand(Encoding, _layoutMode, _deviceOptions).Initialize();
            if (_layoutMode == EscPosLayoutMode.Native)
            {
                WalkNativeRows(page, pageIndex, command, cancellationToken);
                if (!page.FeedLinesSpecified || page.FeedLines > 0)
                    command.Feed(page.FeedLinesSpecified ? page.FeedLines : 1);
                if (page.CutAfterPrint) command.Cut(page.CutOptions ?? new PrintCutOptions());
                pages.AddFactory(() => RepeatCopies(command.Build(cancellationToken).GetBytes(), page.Copies, cancellationToken));
                continue;
            }
            command.RasterPage(page.Width, page.Height, canvas =>
            {
                for (var elementIndex = 0; elementIndex < page.Elements.Count; elementIndex++)
                {
                    var element = page.Elements[elementIndex];
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        switch (element)
                        {
                            case PrintTextElement text:
                                canvas.DrawText(text.X, text.Y, text.Text, text.FontSize, (int)text.Rotation,
                                    text.Bold, text.Reverse, text.Underline, text.FontFamily);
                                break;
                            case PrintLineElement line:
                                canvas.DrawLine(line.X, line.Y, line.EndX, line.EndY, line.LineWidth);
                                break;
                            case PrintRectangleElement rectangle:
                                canvas.DrawRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height,
                                    rectangle.LineWidth, rectangle.Filled);
                                break;
                            case PrintImageElement image:
                                using (var rotated = PrintImageRotation.Prepare(image, cancellationToken))
                                    canvas.DrawRaster(image.X, image.Y, rotated.Width, rotated.Height, rotated.Raster);
                                break;
                            case PrintBarcodeElement barcode:
                                // 公共模型按声明占位保护静区，整页预检已完成；Code 49 绘制完整符号外框。
                                canvas.DrawBarcode(barcode.Type, barcode.X, barcode.Y, barcode.Data, barcode.BarHeight,
                                    barcode.NarrowWidth, barcode.HumanReadable == true,
                                    barcode.HumanReadableReserveHeight, null, _rasterHumanReadableFontSize,
                                    (int)barcode.Rotation);
                                break;
                            case PrintQrCodeElement qr:
                                canvas.DrawQrCode(qr.X, qr.Y, qr.Data, qr.ModuleSize, qr.ErrorLevel,
                                    (int)qr.Rotation);
                                break;
                            default:
                                throw new NotSupportedException("ESC/POS Raster Provider 遇到未验证的元素类型。");
                        }
                    }
                    catch (NotSupportedException error)
                    { throw new PrintCapabilityException(Protocol, element.SourcePath ?? $"Pages[{pageIndex}].Elements[{elementIndex}]", error, pageIndex, elementIndex); }
                    catch (ArgumentException error)
                    { throw new PrintElementValidationException(Protocol, element.SourcePath ?? $"Pages[{pageIndex}].Elements[{elementIndex}]", error, pageIndex, elementIndex); }
                }
            }, cancellationToken);
            if (page.FeedLines > 0) command.Feed(page.FeedLines);
            if (page.CutAfterPrint) command.Cut(page.CutOptions ?? new PrintCutOptions());
            pages.AddFactory(() => RepeatCopies(command.Build(cancellationToken).GetBytes(), page.Copies, cancellationToken));
        }
        var result = pages.AsReadOnly();
        operation?.CommitReservations();
        return result;
    }

    private static byte[] RepeatCopies(byte[] page, int copies, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (copies == 1) return page;
        var length = checked((long)page.Length * copies);
        if (length > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(copies), "ESC/POS 多份输出超出可表示的字节数。");
        return Bing.EasyPrint.Core.BudgetedBuffer.Create((int)length, "EscPos.Copies", output =>
        {
            for (var offset = 0; offset < output.Length; offset += page.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Bing.EasyPrint.Core.BufferCopy.Copy(page, 0, output, offset, page.Length, cancellationToken);
            }
        });
    }

    private void WalkNativeRows(PrintPage page, int pageIndex, EscPosPrintCommand command,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var nextTextRow = 0;
        var hasTextRow = false;
        int? openRow = null;
        var endX = 0;
        var rowIsAscii = true;
        var tabWidth = _deviceOptions?.NativeTabWidthDots;
        var sequential = _deviceOptions?.NativeFlow == EscPosNativeFlowMode.Sequential;

        void CloseRow()
        {
            if (!openRow.HasValue) return;
            command?.NewLine();
            nextTextRow = checked(openRow.Value + (_deviceOptions?.Profile?.Text?.LineHeightDots ?? 24));
            hasTextRow = true;
            openRow = null;
        }

        for (var index = 0; index < page.Elements.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var element = page.Elements[index];
            if (!(element is PrintTextElement text))
            {
                CloseRow();
                if (sequential)
                {
                    var codePath = element.SourcePath ?? $"Pages[{pageIndex}].Elements[{index}]";
                    var blankLines = NativeBlankLines(nextTextRow, element.Y, hasTextRow);
                    if (blankLines < 0)
                        throw new PrintCapabilityException(Protocol, codePath,
                            new NotSupportedException("Native 混合收据不能倒退或重叠；空白间隔须为整行。"), pageIndex, index);
                    if (element.Width <= 0 || element.Height <= 0 ||
                        element is PrintBarcodeElement hri && hri.HumanReadable == true && hri.HumanReadableReserveHeight <= 0)
                        throw new PrintElementValidationException(Protocol, codePath,
                            new ArgumentException("Native 混合收据码图须声明正数占位宽高；开启可读文字还须声明其预留高度。"), pageIndex, index);
                    while (blankLines > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var count = Math.Min(blankLines, 255);
                        command?.NewLine(count);
                        blankLines -= count;
                    }
                    // 原生码图指令在标准模式自行进纸并返回行首；不重复追加 LF。
                    nextTextRow = checked(element.Y + element.Height);
                    hasTextRow = false;
                }
                if (command != null) AddNative(command, element, cancellationToken);
                continue;
            }

            var path = text.SourcePath ?? $"Pages[{pageIndex}].Elements[{index}]";
            if (openRow != text.Y)
            {
                CloseRow();
                var blankLines = NativeBlankLines(nextTextRow, text.Y, hasTextRow);
                if (blankLines < 0 || (long)text.Y + text.FontSize > page.Height)
                    throw new PrintCapabilityException(Protocol, path,
                        new NotSupportedException("ESC/POS Native 文字只能按连续逻辑行或整行空白顺序排列。"), pageIndex, index);
                while (blankLines > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var count = Math.Min(blankLines, 255);
                    command?.NewLine(count);
                    blankLines -= count;
                }
                openRow = text.Y;
                endX = 0;
                rowIsAscii = true;
            }

            if (text.X != 0 || endX != 0)
            {
                if (!tabWidth.HasValue || text.X <= endX || text.X % tabWidth.Value != 0 ||
                    !rowIsAscii || !IsAsciiText(text.Text))
                    throw new PrintCapabilityException(Protocol, path,
                        new NotSupportedException("ESC/POS Native 同行列文字必须为不重叠的 ASCII 文本，并对齐已配置的制表位。"), pageIndex, index);
                var tabs = text.X / tabWidth.Value - endX / tabWidth.Value;
                for (var tab = 0; tab < tabs; tab++)
                { cancellationToken.ThrowIfCancellationRequested(); command?.Tab(); }
            }
            rowIsAscii &= IsAsciiText(text.Text);
            endX = checked(text.X + (_deviceOptions?.Profile?.Text?.MeasureTextWidth(text.Text, cancellationToken) ??
                text.Text.Length * (tabWidth.GetValueOrDefault(96) / 8)));
            if (_deviceOptions?.Profile?.Text != null && endX > page.Width)
                throw new PrintElementValidationException(Protocol, path,
                    new ArgumentOutOfRangeException(nameof(text), "文字超过设备声明的实测字宽布局范围。"), pageIndex, index);
            if (text.Bold) command?.Bold();
            if (text.Underline) command?.Underline();
            if (text.Reverse) command?.Reverse();
            command?.WriteText(text.Text, cancellationToken);
            if (text.Reverse) command?.Reverse(false);
            if (text.Underline) command?.Underline(false);
            if (text.Bold) command?.Bold(false);
        }
        CloseRow();
    }

    private static bool IsAsciiText(string value)
    {
        foreach (var character in value)
            if (character < 32 || character > 126) return false;
        return true;
    }

    private static void AddNative(EscPosPrintCommand command, PrintElement element, CancellationToken cancellationToken)
    {
        switch (element)
        {
            case PrintTextElement text:
                if (text.Bold) command.Bold();
                if (text.Underline) command.Underline();
                if (text.Reverse) command.Reverse();
                command.Text(text.Text, cancellationToken);
                if (text.Reverse) command.Reverse(false);
                if (text.Underline) command.Underline(false);
                if (text.Bold) command.Bold(false);
                break;
            case PrintBarcodeElement barcode:
                if (barcode.Type == BarcodeType.Code128Auto)
                    command.BarcodeCode128Auto(barcode.Data, (byte)barcode.BarHeight, (byte)barcode.NarrowWidth,
                        barcode.HumanReadable == true ? (byte)2 : (byte)0);
                else
                    command.Barcode(NormalizeEscPosI2of5Data(barcode.Type, barcode.Data),
                        EscPosBarcodeSymbology(barcode.Type), (byte)barcode.BarHeight, (byte)barcode.NarrowWidth,
                        barcode.HumanReadable == true ? (byte)2 : (byte)0);
                break;
            case PrintQrCodeElement qr:
                command.QrCode(qr.Data, (byte)qr.ModuleSize, (byte)(48 + (int)qr.ErrorLevel));
                break;
            default:
                throw new NotSupportedException("ESC/POS Native Provider 不支持此元素类型。");
        }
    }

    private static byte EscPosBarcodeSymbology(BarcodeType type)
    {
        switch (type)
        {
            case BarcodeType.Code128: return 73;
            case BarcodeType.Code39: return 69;
            case BarcodeType.Code93: return 72;
            case BarcodeType.UpcA: return 65;
            case BarcodeType.UpcE: return 66;
            case BarcodeType.Ean13: return 67;
            case BarcodeType.Ean8: return 68;
            case BarcodeType.I2OF5: return 70;
            case BarcodeType.I2OF5C: return 70;
            case BarcodeType.I2OF5G: return 70;
            case BarcodeType.UccEan128: return 74;
            case BarcodeType.Codabar: return 71;
            case BarcodeType.Gs1DataBarOmnidirectional: return 75;
            case BarcodeType.Gs1DataBarTruncated: return 76;
            case BarcodeType.Gs1DataBarLimited: return 77;
            case BarcodeType.Gs1DataBarExpanded: return 78;
            default: throw new NotSupportedException($"ESC/POS Native Provider 不支持条码类型 {type}。");
        }
    }

    /// <summary>
    /// 将 CPCL 语义的 I2OF5C/I2OF5G 校验规则转换为 ESC/POS 的基础 ITF 数据。
    /// </summary>
    private static string NormalizeEscPosI2of5Data(BarcodeType type, string data)
    {
        if (type != BarcodeType.I2OF5 && type != BarcodeType.I2OF5C && type != BarcodeType.I2OF5G)
            return data;
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0 || data.Any(character => character < '0' || character > '9'))
            throw new ArgumentException("I2OF5 系列条码仅接受非空 7-bit ASCII 数字。", nameof(data));

        if (type == BarcodeType.I2OF5)
            return data;

        if (type == BarcodeType.I2OF5C)
        {
            // CPCL I2OF5C 要求校验计算前的数据以奇数位开始；偶数输入补前导 0。
            if (data.Length % 2 == 0) data = "0" + data;
            return data + CalculateI2of5CheckDigit(data, 3, 1);
        }

        if (data.Length < 11 || data.Length > 14)
            throw new ArgumentException("I2OF5G 需要 11、12、13 或 14 位数字。", nameof(data));
        if (data.Length % 2 == 1)
            return data + CalculateI2of5CheckDigit(data, 4, 9);

        var expected = CalculateI2of5CheckDigit(data.Substring(0, data.Length - 1), 4, 9);
        if (data[data.Length - 1] != expected)
            throw new ArgumentException("I2OF5G 校验位错误。", nameof(data));
        return data;
    }

    private static char CalculateI2of5CheckDigit(string data, int firstWeight, int secondWeight)
    {
        var sum = 0;
        for (var index = 0; index < data.Length; index++)
            sum += (data[index] - '0') * (index % 2 == 0 ? firstWeight : secondWeight);
        return (char)('0' + (10 - sum % 10) % 10);
    }

    private int NativeBlankLines(int nextY, int y, bool hasTextRow)
    {
        var gap = (long)y - nextY;
        if (_deviceOptions?.Profile?.Text is EscPosTextProfile text)
            return gap >= 0 && gap % text.LineHeightDots == 0 ? checked((int)(gap / text.LineHeightDots)) : -1;
        if (gap == 0 || hasTextRow && gap == 2) return 0;
        if (gap > 0 && gap % 26 == 0) return checked((int)(gap / 26));
        if (gap > 0 && gap % 24 == 0) return checked((int)(gap / 24));
        return -1;
    }
}
