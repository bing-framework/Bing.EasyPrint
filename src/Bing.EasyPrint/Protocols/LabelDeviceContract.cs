using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bing.EasyPrint.Protocols;

// 两种标签协议共享不可变的限制快照，公开配置及数组不会逃逸到运行中的命令。
internal sealed class LabelDeviceContract
{
    internal string Name { get; private set; }
    internal string FirmwareVersion { get; private set; }
    internal int? Width { get; private set; }
    internal int? Height { get; private set; }
    internal int? ImageBytes { get; private set; }
    internal int? MaxGs1LinearPayloadCharacters { get; private set; }
    internal int? MaxGs1DataBarExpandedPayloadCharacters { get; private set; }
    internal bool Images { get; private set; }
    internal bool Graphics { get; private set; }
    internal bool Barcode { get; private set; }
    internal bool BarcodeHumanReadable { get; private set; }
    internal int? BarcodeHumanReadableReserveHeightDots { get; private set; }
    internal int? BarcodeHumanReadableReserveWidthDots { get; private set; }
    internal int? BarcodeHumanReadableLeftOverhangDots { get; private set; }
    internal int? BarcodeHumanReadableTopOverhangDots { get; private set; }
    internal int? BarcodeHumanReadableRightOverhangDots { get; private set; }
    internal int? BarcodeHumanReadableBottomOverhangDots { get; private set; }
    internal NativeBarcodeHumanReadableOverhang? BarcodeHumanReadableRotation90Overhang { get; private set; }
    internal NativeBarcodeHumanReadableOverhang? BarcodeHumanReadableRotation180Overhang { get; private set; }
    internal NativeBarcodeHumanReadableOverhang? BarcodeHumanReadableRotation270Overhang { get; private set; }
    internal IReadOnlyList<BarcodeType> BarcodeHumanReadableTypes { get; private set; }
    internal bool QrCode { get; private set; }
    internal IReadOnlyList<RotationAngle> QrCodeRotations { get; private set; }
    internal bool Rotation { get; private set; }
    internal bool Cut { get; private set; }
    internal IReadOnlyList<string> Fonts { get; private set; }
    internal IReadOnlyList<int> Encodings { get; private set; }
    internal IReadOnlyList<PrintCharacterRange> Characters { get; private set; }

    internal static LabelDeviceContract Create(PrintProtocol protocol, string name, string firmware,
        int? width, int? height, int? imageBytes, int? maxGs1LinearPayloadCharacters,
        int? maxGs1DataBarExpandedPayloadCharacters, bool images, bool graphics, bool barcode,
        bool barcodeHumanReadable, int? barcodeHumanReadableReserveHeightDots,
        int? barcodeHumanReadableReserveWidthDots, int? barcodeHumanReadableLeftOverhangDots,
        int? barcodeHumanReadableTopOverhangDots, int? barcodeHumanReadableRightOverhangDots,
        int? barcodeHumanReadableBottomOverhangDots,
        NativeBarcodeHumanReadableOverhang? barcodeHumanReadableRotation90Overhang,
        NativeBarcodeHumanReadableOverhang? barcodeHumanReadableRotation180Overhang,
        NativeBarcodeHumanReadableOverhang? barcodeHumanReadableRotation270Overhang,
        BarcodeType[] barcodeHumanReadableTypes,
        bool qrCode, RotationAngle[] qrCodeRotations,
        bool rotation, bool cut, string[] fonts,
        int[] encodings, PrintCharacterRange[] characters)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("机型配置名称不能为空。", nameof(name));
        if (width <= 0 || height <= 0 || imageBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "机型纸宽、标签高度和图片预算必须为正数。");
        Gs1BarcodeCapacity.ValidateLimit(maxGs1LinearPayloadCharacters, nameof(maxGs1LinearPayloadCharacters));
        Gs1BarcodeCapacity.ValidateLimit(maxGs1DataBarExpandedPayloadCharacters,
            nameof(maxGs1DataBarExpandedPayloadCharacters));
        if (barcodeHumanReadableReserveHeightDots.HasValue && barcodeHumanReadableReserveHeightDots.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(barcodeHumanReadableReserveHeightDots),
                "原生条码 HRI 预留高度必须为正数。");
        if (barcodeHumanReadableReserveWidthDots.HasValue && barcodeHumanReadableReserveWidthDots.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(barcodeHumanReadableReserveWidthDots),
                "原生条码 HRI 预留宽度必须为正数。");
        if (barcodeHumanReadableLeftOverhangDots.HasValue && barcodeHumanReadableLeftOverhangDots.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(barcodeHumanReadableLeftOverhangDots),
                "原生条码 HRI 左侧外伸不能为负数。");
        if (barcodeHumanReadableTopOverhangDots.HasValue && barcodeHumanReadableTopOverhangDots.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(barcodeHumanReadableTopOverhangDots),
                "原生条码 HRI 上侧外伸不能为负数。");
        if (barcodeHumanReadableRightOverhangDots.HasValue && barcodeHumanReadableRightOverhangDots.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(barcodeHumanReadableRightOverhangDots),
                "原生条码 HRI 右侧外伸不能为负数。");
        if (barcodeHumanReadableBottomOverhangDots.HasValue && barcodeHumanReadableBottomOverhangDots.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(barcodeHumanReadableBottomOverhangDots),
                "原生条码 HRI 下侧外伸不能为负数。");
        if (protocol == PrintProtocol.Tspl && maxGs1DataBarExpandedPayloadCharacters.HasValue)
            throw new ArgumentException("TSPL Profile 当前没有 GS1 DataBar Expanded Provider。", nameof(maxGs1DataBarExpandedPayloadCharacters));
        if (fonts != null && (fonts.Any(font => !ValidFont(protocol, font)) || fonts.Distinct(StringComparer.Ordinal).Count() != fonts.Length))
            throw new ArgumentException("机型字体必须是不重复的协议内置字体标识。", nameof(fonts));
        if (encodings != null && (encodings.Length == 0 || encodings.Distinct().Count() != encodings.Length ||
            encodings.Any(code => code != 20127 && code != 65001 && !(protocol == PrintProtocol.Tspl && code == 936))))
            throw new ArgumentException("机型代码页只能收窄当前协议已映射的编码，且不得为空或重复。", nameof(encodings));
        if (characters != null && (characters.Length == 0 || characters.Any(range => range == null)))
            throw new ArgumentException("机型字符区间必须非空且不包含 null。", nameof(characters));
        if (barcodeHumanReadableTypes != null && barcodeHumanReadableTypes.Any(type =>
                !Enum.IsDefined(typeof(BarcodeType), type) ||
                barcodeHumanReadableTypes.Count(candidate => candidate == type) > 1))
            throw new ArgumentException("原生 HRI 条码制式必须有效且不能重复。", nameof(barcodeHumanReadableTypes));
        if (qrCodeRotations != null && qrCodeRotations.Any(rotation =>
                rotation == RotationAngle.None || !Enum.IsDefined(typeof(RotationAngle), rotation) ||
                qrCodeRotations.Count(candidate => candidate == rotation) > 1))
            throw new ArgumentException("原生二维码旋转角度必须是非零有效角度且不能重复。", nameof(qrCodeRotations));
        if (characters != null)
        {
            var ordered = characters.OrderBy(range => range.First).ToArray();
            for (var index = 1; index < ordered.Length; index++)
                if (ordered[index].First <= ordered[index - 1].Last)
                    throw new ArgumentException("机型字符区间不能重复或重叠。", nameof(characters));
        }
        return new LabelDeviceContract {
            Name = name, FirmwareVersion = firmware, Width = width, Height = height, ImageBytes = imageBytes,
            MaxGs1LinearPayloadCharacters = maxGs1LinearPayloadCharacters,
            MaxGs1DataBarExpandedPayloadCharacters = maxGs1DataBarExpandedPayloadCharacters,
            Images = images, Graphics = graphics, Barcode = barcode,
            BarcodeHumanReadable = barcodeHumanReadable,
            BarcodeHumanReadableReserveHeightDots = barcodeHumanReadableReserveHeightDots,
            BarcodeHumanReadableReserveWidthDots = barcodeHumanReadableReserveWidthDots,
            BarcodeHumanReadableLeftOverhangDots = barcodeHumanReadableLeftOverhangDots,
            BarcodeHumanReadableTopOverhangDots = barcodeHumanReadableTopOverhangDots,
            BarcodeHumanReadableRightOverhangDots = barcodeHumanReadableRightOverhangDots,
            BarcodeHumanReadableBottomOverhangDots = barcodeHumanReadableBottomOverhangDots,
            BarcodeHumanReadableRotation90Overhang = barcodeHumanReadableRotation90Overhang,
            BarcodeHumanReadableRotation180Overhang = barcodeHumanReadableRotation180Overhang,
            BarcodeHumanReadableRotation270Overhang = barcodeHumanReadableRotation270Overhang,
            BarcodeHumanReadableTypes = barcodeHumanReadableTypes == null ? null :
                Array.AsReadOnly((BarcodeType[])barcodeHumanReadableTypes.Clone()),
            QrCode = qrCode,
            QrCodeRotations = qrCodeRotations == null ? null :
                Array.AsReadOnly((RotationAngle[])qrCodeRotations.Clone()),
            Rotation = rotation, Cut = cut,
            Fonts = fonts == null ? null : Array.AsReadOnly((string[])fonts.Clone()),
            Encodings = encodings == null ? null : Array.AsReadOnly((int[])encodings.Clone()),
            Characters = characters == null ? null : Array.AsReadOnly((PrintCharacterRange[])characters.Clone())
        };
    }

    private static bool ValidFont(PrintProtocol protocol, string font) => font != null && font.Length == 1 &&
        (protocol == PrintProtocol.Tspl ? font[0] >= '0' && font[0] <= '8' :
        font[0] >= '0' && font[0] <= '9' || font[0] >= 'A' && font[0] <= 'Z');

    internal void ValidateEncoding(Encoding encoding)
    {
        if (Encodings != null && !Encodings.Contains(encoding.CodePage))
            throw new NotSupportedException($"机型 {Name} 未声明支持代码页 {encoding.CodePage}。");
    }

    internal void ValidateText(string font, string text)
    {
        if (Fonts != null && !Fonts.Contains(font))
            throw new NotSupportedException($"机型 {Name} 未声明设备字体 {font}。");
        if (Characters == null) return;
        foreach (var scalar in PrintUnicodeScalars.Enumerate(text))
            if (!Characters.Any(range => range.Contains(scalar)))
                throw new NotSupportedException($"机型 {Name} 的声明字库不包含 U+{scalar:X4}。");
    }

    internal void ValidatePage(double width, double height)
    {
        if (Width.HasValue && width > Width.Value)
            throw new NotSupportedException($"机型 {Name} 的可打印宽度上限为 {Width} 点。");
        if (Height.HasValue && height > Height.Value)
            throw new NotSupportedException($"机型 {Name} 的标签高度上限为 {Height} 点。");
    }

    internal void Require(bool supported, string feature)
    {
        if (!supported) throw new NotSupportedException($"机型 {Name} 不支持 {feature}。");
    }

    internal bool SupportsBarcodeHumanReadable(BarcodeType type)
        => BarcodeHumanReadable && (BarcodeHumanReadableTypes == null || BarcodeHumanReadableTypes.Contains(type));

    internal NativeBarcodeHumanReadableOverhang ResolveBarcodeHumanReadableOverhang(int rotation)
    {
        var rotationOverhang = rotation switch
        {
            90 => BarcodeHumanReadableRotation90Overhang,
            180 => BarcodeHumanReadableRotation180Overhang,
            270 => BarcodeHumanReadableRotation270Overhang,
            _ => null
        };
        return new NativeBarcodeHumanReadableOverhang(
            rotationOverhang?.LeftDots ?? BarcodeHumanReadableLeftOverhangDots,
            rotationOverhang?.TopDots ?? BarcodeHumanReadableTopOverhangDots,
            rotationOverhang?.RightDots ?? BarcodeHumanReadableRightOverhangDots,
            rotationOverhang?.BottomDots ?? BarcodeHumanReadableBottomOverhangDots);
    }

    internal bool SupportsQrCodeRotation(RotationAngle rotation)
    {
        if (!Enum.IsDefined(typeof(RotationAngle), rotation)) return false;
        if (rotation == RotationAngle.None) return QrCode;
        return QrCode && Rotation && (QrCodeRotations == null || QrCodeRotations.Contains(rotation));
    }

    internal void ValidateImage(int x, int y, long rowBytes, long height)
    {
        Require(Images, "图片");
        if (ImageBytes.HasValue && rowBytes * height > ImageBytes.Value)
            throw new NotSupportedException($"机型 {Name} 的单条图片数据上限为 {ImageBytes} 字节。");
        ValidatePage(x + rowBytes * 8L, y + height);
    }
}
