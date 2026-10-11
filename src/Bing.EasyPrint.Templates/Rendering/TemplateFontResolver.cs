using System;
using Bing.EasyPrint.Elements;

namespace Bing.EasyPrint.Templates;

/// <summary>将语义字体和旧模板字段转换为协议明确支持的字体选择。</summary>
internal static class TemplateFontResolver
{
    internal static void ValidateStructure(BpFontElement element)
    {
        if (element.Font != null && (element.FontName != 0 ||
            !string.IsNullOrEmpty(element.FontFamily) || element.DeviceFontName != null))
            throw new ArgumentException("Font 不能与旧版 FontName、FontFamily 或 DeviceFontName 同时指定。");
    }

    internal static ResolvedTemplateFont Resolve(BpFontElement element, PrintProtocol protocol,
        EscPosLayoutMode layout)
    {
        ValidateStructure(element);
        var font = element.Font;
        if (font == null)
        {
            if (protocol != PrintProtocol.Cpcl && element.FontName != 0)
                throw new NotSupportedException($"协议 {protocol} 无法映射 CPCL FontName={element.FontName}；请改用 Font 选择目标协议字体，或将 FontName 设置为 0 使用协议默认字体。");
            return new ResolvedTemplateFont(element.FontName, element.FontFamily, element.DeviceFontName, null);
        }

        if (font.Kind == PrintFontKind.Default)
            return new ResolvedTemplateFont(0, null, null, font);
        if (font.Kind == PrintFontKind.Host)
        {
            if (protocol != PrintProtocol.EscPos || layout != EscPosLayoutMode.Raster)
                throw new NotSupportedException($"协议 {protocol} 的当前布局不支持主机字体；请显式使用 ESC/POS Raster。");
            return new ResolvedTemplateFont(0, font.Name, null, font);
        }

        if (protocol == PrintProtocol.Cpcl)
        {
            if (font.Name != "24" && font.Name != "55")
                throw new ArgumentException("CPCL 设备字体仅支持 24 或 55。");
            var size = element.FontSize == 0 ? 24 : element.FontSize;
            if (!Enum.IsDefined(typeof(FontSize), size) || font.Name != (size % 24 == 0 ? "24" : "55"))
                throw new ArgumentException("CPCL 设备字体必须与内置字号对应（24 或 55）。");
            return new ResolvedTemplateFont(int.Parse(font.Name), null, null, font);
        }
        if (protocol != PrintProtocol.Tspl && protocol != PrintProtocol.Zpl)
            throw new NotSupportedException($"协议 {protocol} 不支持显式设备字体；请使用协议默认字体。");
        return new ResolvedTemplateFont(0, null, font.Name, font);
    }
}

internal readonly struct ResolvedTemplateFont
{
    internal int CpclFontName { get; }
    internal string HostFamily { get; }
    internal string DeviceName { get; }
    internal PrintFont Selection { get; }

    internal ResolvedTemplateFont(int cpclFontName, string hostFamily, string deviceName, PrintFont selection)
    {
        CpclFontName = cpclFontName;
        HostFamily = hostFamily;
        DeviceName = deviceName;
        Selection = selection;
    }
}
