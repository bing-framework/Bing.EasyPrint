using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace Bing.EasyPrint.Protocols;

/// <summary>显式声明目标机型的字符处理模式；不会自动推断厂商或固件。</summary>
public enum EscPosCharacterMode
{
    /// <summary>使用初始化后的设备默认模式，不追加字符模式指令。</summary>
    DeviceDefault = 0,
    /// <summary>使用 FS . 取消双字节模式；须声明单字节编码。</summary>
    SingleByte = 1,
    /// <summary>使用 FS &amp; 选择汉字模式；须由设备手册确认编码和字库。</summary>
    FsKanji = 2
}

/// <summary>
/// 目标 ESC/POS 设备当前原生字库的编码、模式、覆盖范围及实测布局。
/// 不设置此契约时保留旧代码页行为；声明后拒绝编码冲突和缺字。
/// </summary>
public sealed class EscPosTextProfile
{
    /// <summary>与设备原生字符集对应的 .NET 编码代码页，必须显式设置。</summary>
    public int EncodingCodePage { get; set; }
    /// <summary>初始化时使用 ESC t 选择的设备代码页；为空表示不追加该指令。</summary>
    public byte? CodePageIndex { get; set; }
    /// <summary>设备实际支持的字符模式；默认不追加模式指令。</summary>
    public EscPosCharacterMode CharacterMode { get; set; }
    /// <summary>当前默认字库已确认支持的字符区间；声明契约时必须非空。</summary>
    public PrintCharacterRange[] CharacterRanges { get; set; }
    /// <summary>24 点逻辑字号下实测的半角字宽，单位为布局点。</summary>
    public int HalfCellWidthDots { get; set; } = 12;
    /// <summary>24 点逻辑字号下实测的全角字宽，单位为布局点。</summary>
    public int FullCellWidthDots { get; set; } = 24;
    /// <summary>Native 文字的逻辑行高；当前布局接受 24 或 26 点。</summary>
    public int LineHeightDots { get; set; } = 24;

    internal EscPosTextProfile Snapshot()
    {
        if (EncodingCodePage <= 0) throw new ArgumentOutOfRangeException(nameof(EncodingCodePage));
        if (!Enum.IsDefined(typeof(EscPosCharacterMode), CharacterMode))
            throw new ArgumentOutOfRangeException(nameof(CharacterMode));
        var encoding = Encoding.GetEncoding(EncodingCodePage);
        if (!PrintCapabilities.IsEscPosEncodingCompatible(encoding))
            throw new NotSupportedException("设备文字契约必须使用 ASCII 兼容编码。");
        if (CharacterMode == EscPosCharacterMode.SingleByte && !encoding.IsSingleByte)
            throw new NotSupportedException("FS . 单字节模式不能声明多字节文字编码。");
        if (CharacterMode == EscPosCharacterMode.FsKanji &&
            EncodingCodePage != 936 && EncodingCodePage != 54936 && EncodingCodePage != 950 && EncodingCodePage != 949)
            throw new NotSupportedException("FS & 当前只接受显式声明的 GBK、GB18030、Big5 或韩文代码页；UTF-8 和日文代码系统需要独立指令契约。");
        if (CharacterRanges == null || CharacterRanges.Length == 0 || CharacterRanges.Any(range => range == null))
            throw new ArgumentException("设备文字契约必须声明非空的字库覆盖区间。", nameof(CharacterRanges));
        if (HalfCellWidthDots <= 0 || FullCellWidthDots <= 0)
            throw new ArgumentOutOfRangeException(nameof(HalfCellWidthDots), "实测字宽必须为正数。");
        if (LineHeightDots != 24 && LineHeightDots != 26)
            throw new NotSupportedException("Native 文字契约当前接受 24 或 26 点逻辑行高。");
        return Clone();
    }

    internal EscPosTextProfile Clone()
    {
        var copy = (EscPosTextProfile)MemberwiseClone();
        copy.CharacterRanges = (PrintCharacterRange[])CharacterRanges.Clone();
        return copy;
    }

    internal void ValidateEncoding(Encoding encoding)
    {
        if (encoding.CodePage != EncodingCodePage)
            throw new NotSupportedException($"设备文字契约要求代码页 {EncodingCodePage}，主机编码为 {encoding.CodePage} ({encoding.WebName})。");
    }

    internal void ValidateText(string text)
        => ValidateText(text, CancellationToken.None);

    internal void ValidateText(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var scalar in PrintUnicodeScalars.Enumerate(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var covered = false;
            foreach (var range in CharacterRanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (range.Contains(scalar)) { covered = true; break; }
            }
            if (!covered)
                throw new NotSupportedException($"设备声明的原生字库不包含 U+{scalar:X4}；请确认字库覆盖或显式使用 Raster。");
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>按已声明的半角/全角单元测量文字宽度；不代替字库和编码校验。</summary>
    public int MeasureTextWidth(string text)
        => MeasureTextWidth(text, CancellationToken.None);

    /// <summary>按已声明字宽进行可取消测量。</summary>
    public int MeasureTextWidth(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var encoding = Encoding.GetEncoding(EncodingCodePage);
        var width = 0;
        var iterator = StringInfo.GetTextElementEnumerator(text);
        while (iterator.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var element = iterator.GetTextElement();
            var elementWidth = 0;
            var joined = element.IndexOf('\u200D') >= 0;
            foreach (var scalar in PrintUnicodeScalars.Enumerate(element))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = char.ConvertFromUtf32(scalar);
                var category = CharUnicodeInfo.GetUnicodeCategory(value, 0);
                if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.EnclosingMark ||
                    category == UnicodeCategory.Format || scalar >= 0x1F3FB && scalar <= 0x1F3FF) continue;
                var wide = encoding.CodePage == 65001 ? IsWide(scalar) : encoding.GetByteCount(value) > 1;
                var advance = wide ? FullCellWidthDots : HalfCellWidthDots;
                elementWidth = joined ? Math.Max(elementWidth, advance) : checked(elementWidth + advance);
            }
            width = checked(width + elementWidth);
        }
        return width;
    }

    private static bool IsWide(int value) => value >= 0x1100 && (
        value <= 0x115F || value == 0x2329 || value == 0x232A ||
        value >= 0x2E80 && value <= 0xA4CF && value != 0x303F ||
        value >= 0xAC00 && value <= 0xD7A3 || value >= 0xF900 && value <= 0xFAFF ||
        value >= 0xFE10 && value <= 0xFE19 || value >= 0xFE30 && value <= 0xFE6F ||
        value >= 0xFF01 && value <= 0xFF60 || value >= 0xFFE0 && value <= 0xFFE6 ||
        value >= 0x1F000 && value <= 0x1FAFF || value >= 0x20000 && value <= 0x3FFFD);
}
