using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SixLabors.Fonts;

namespace Bing.EasyPrint.Protocols;

/// <summary>不可变的 Raster 字体配置；只使用所提供资源，按显式替代列表选择整段文字字体。</summary>
public sealed class RasterFontCatalog
{
    private readonly Dictionary<string, RasterFontResource> _fonts;
    /// <summary>固定字体资源的只读列表。</summary>
    public IReadOnlyList<RasterFontResource> Fonts { get; }
    /// <summary>未指定主机字体名称时使用的资源别名。</summary>
    public string DefaultFont { get; }
    /// <summary>主字体缺字或缺少样式时依次尝试的资源别名；空列表表示严格拒绝。</summary>
    public IReadOnlyList<string> FallbackFonts { get; }

    /// <summary>创建固定字体配置；名称按序号区分大小写，所有引用必须存在。</summary>
    public RasterFontCatalog(IEnumerable<RasterFontResource> fonts, string defaultFont,
        IEnumerable<string> fallbackFonts = null)
        : this(fonts, defaultFont, fallbackFonts, CancellationToken.None)
    {
    }

    /// <summary>使用取消令牌创建固定字体配置；目录数据由资源对象共享，不重复复制或计费。</summary>
    public RasterFontCatalog(IEnumerable<RasterFontResource> fonts, string defaultFont,
        IEnumerable<string> fallbackFonts, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (fonts == null) throw new ArgumentNullException(nameof(fonts));

        var resources = new List<RasterFontResource>();
        var totalDataBytes = 0L;
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var font in fonts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (resources.Count >= RasterFontLimits.MaxResourceCount)
                throw new ArgumentException($"字体资源数量不能超过 {RasterFontLimits.MaxResourceCount} 个。", nameof(fonts));
            if (font == null)
                throw new ArgumentException("字体配置必须包含非空资源。", nameof(fonts));

            cancellationToken.ThrowIfCancellationRequested();
            var byteLength = font.ByteLength;
            cancellationToken.ThrowIfCancellationRequested();
            totalDataBytes = checked(totalDataBytes + byteLength);
            if (totalDataBytes > RasterFontLimits.MaxTotalDataBytes)
                throw new ArgumentException($"字体目录数据不能超过 {RasterFontLimits.MaxTotalDataBytes} 字节。", nameof(fonts));

            resources.Add(font);
            cancellationToken.ThrowIfCancellationRequested();
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (resources.Count == 0)
            throw new ArgumentException("字体配置必须包含非空资源。", nameof(fonts));
        _fonts = new Dictionary<string, RasterFontResource>(StringComparer.Ordinal);
        foreach (var font in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_fonts.ContainsKey(font.Name)) throw new ArgumentException($"字体资源名称重复：{font.Name}。", nameof(fonts));
            _fonts.Add(font.Name, font);
            cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (defaultFont == null || !_fonts.ContainsKey(defaultFont))
            throw new ArgumentException("默认字体必须引用已声明的资源。", nameof(defaultFont));

        var fallback = new List<string>();
        if (fallbackFonts != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var name in fallbackFonts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (fallback.Count >= RasterFontLimits.MaxResourceCount)
                    throw new ArgumentException($"字体替代列表不能超过 {RasterFontLimits.MaxResourceCount} 项。", nameof(fallbackFonts));
                fallback.Add(name);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (fallback.Any(name => name == null || !_fonts.ContainsKey(name)) ||
            fallback.Distinct(StringComparer.Ordinal).Count() != fallback.Count)
            throw new ArgumentException("替代字体必须引用已声明且不重复的资源。", nameof(fallbackFonts));

        cancellationToken.ThrowIfCancellationRequested();
        Fonts = Array.AsReadOnly(resources.ToArray());
        DefaultFont = defaultFont;
        FallbackFonts = Array.AsReadOnly(fallback.ToArray());
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal (string Name, FontFamily Family) Resolve(string text, string name, bool bold)
        => Resolve(text, name, bold, CancellationToken.None);

    internal (string Name, FontFamily Family) Resolve(string text, string name, bool bold,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var primary = string.IsNullOrEmpty(name) ? DefaultFont : name;
        if (!_fonts.TryGetValue(primary, out var resource))
            throw new NotSupportedException($"固定字体配置未声明资源 {primary}；不会改用系统字体。");
        cancellationToken.ThrowIfCancellationRequested();
        if (resource.SupportsStyle(bold) && EscPosRasterCanvas.HasGlyphCoverage(text, resource.Family, bold,
                out _, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (primary, resource.Family);
        }
        foreach (var fallback in FallbackFonts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = _fonts[fallback];
            if (candidate.SupportsStyle(bold) && EscPosRasterCanvas.HasGlyphCoverage(text, candidate.Family, bold,
                    out _, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return (fallback, candidate.Family);
            }
        }
        if (!resource.SupportsStyle(bold))
            throw new NotSupportedException($"固定字体 {primary} ({resource.Version}) 未提供 Bold 样式，且显式替代列表不能满足要求。");
        cancellationToken.ThrowIfCancellationRequested();
        EscPosRasterCanvas.HasGlyphCoverage(text, resource.Family, bold, out var missing, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException($"固定字体 {primary} ({resource.Version}) 不包含 U+{missing:X4}，且显式替代列表不能覆盖整段文字。");
    }

    /// <summary>检查资源别名和样式及显式替代是否可表达；不代替实际文字的字形检查。</summary>
    public bool SupportsSelection(string name, bool bold = false)
    {
        var primary = string.IsNullOrEmpty(name) ? DefaultFont : name;
        return _fonts.TryGetValue(primary, out var resource) &&
            (resource.SupportsStyle(bold) || FallbackFonts.Any(fallback => _fonts[fallback].SupportsStyle(bold)));
    }
}
