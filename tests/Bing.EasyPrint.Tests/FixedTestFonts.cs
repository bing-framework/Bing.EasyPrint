using System;
using System.IO;
using SixLabors.Fonts;
using Bing.EasyPrint.Protocols;

namespace Bing.EasyPrint.Tests;

/// <summary>读取随测试程序集输出的固定跨平台字体资源。</summary>
internal static class FixedTestFonts
{
    internal const string RegularFileName = "DejaVuSansMono.ttf";
    internal const string BoldFileName = "DejaVuSansMono-Bold.ttf";
    internal const string FallbackFileName = "DejaVuSans.ttf";
    internal const string ChineseFileName = "NotoSansCJKsc-Regular.otf";

    /// <summary>读取固定 Mono Regular 字体。</summary>
    internal static byte[] Regular() => ReadRequired(RegularFileName);

    /// <summary>读取固定 Mono Bold 字体。</summary>
    internal static byte[] Bold() => ReadRequired(BoldFileName);

    /// <summary>读取固定 Sans fallback 字体。</summary>
    internal static byte[] Fallback() => ReadRequired(FallbackFileName);

    /// <summary>读取固定中文常规字体，保留中文绘字测试。</summary>
    internal static byte[] Chinese() => ReadRequired(ChineseFileName);

    /// <summary>创建带真实常规/粗体的固定 Mono 字体目录；使用别名 Primary。</summary>
    internal static RasterFontCatalog Catalog() => new RasterFontCatalog(new[]
    {
        new RasterFontResource("Primary", "dejavu-2.37", Regular(), Bold())
    }, "Primary");

    /// <summary>创建固定中文常规字体目录；使用别名 Chinese，不隐式合成粗体。</summary>
    internal static RasterFontCatalog ChineseCatalog() => new RasterFontCatalog(new[]
    {
        new RasterFontResource("Chinese", "noto-cjk-fixture-2026-10", Chinese())
    }, "Chinese");

    /// <summary>从测试程序集输出目录读取字体；文件缺失时立即失败。</summary>
    internal static byte[] ReadRequired(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("字体文件名不能为空。", nameof(fileName));

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fonts", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("固定测试字体文件不存在。", path);

        return File.ReadAllBytes(path);
    }

    /// <summary>从内存字节加载字体族，供测试字形覆盖而不依赖系统字体目录。</summary>
    internal static FontFamily LoadFamily(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("字体数据不能为空。", nameof(data));

        var collection = new FontCollection();
        using (var stream = new MemoryStream(data, writable: false))
        {
            return collection.Add(stream);
        }
    }
}
