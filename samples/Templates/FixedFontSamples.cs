using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;

/// <summary>使用调用方提供的字体文件生成可复现的 Raster 小票，不安装或分发字体。</summary>
internal static class FixedFontSamples
{
    internal static void WriteTo(string output, TemplateRenderer renderer, string regularPath, string boldPath)
    {
        var font = new RasterFontResource("body", "sample-input",
            File.ReadAllBytes(regularPath), boldPath == null ? null : File.ReadAllBytes(boldPath));
        var options = new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos, EscPosLayout = EscPosLayoutMode.Raster, Encoding = Encoding.UTF8,
            FeedLines = 0, CutAfterPrint = false,
            EscPosDevice = new EscPosDeviceOptions { RasterFonts = new RasterFontCatalog(new[] { font }, "body") }
        };
        var template = new BpTemplate
        {
            Width = 384, Height = 120,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell
            {
                Elements = new List<BpElement> { new BpFontElement
                {
                    Font = new PrintFont(PrintFontKind.Host, "body"), FontSize = 24,
                    Bold = boldPath != null, Field = new BgField { Value = "Fixed font receipt 123" }
                } }
            } } } }
        };
        var first = renderer.RenderWithLayout(template, new { }, options: options);
        var second = renderer.RenderWithLayout(template, new { }, options: options);
        if (!first.Pages[0].Data.SequenceEqual(second.Pages[0].Data))
            throw new InvalidOperationException("同一固定字体资源的重复输出不一致。");
        File.WriteAllBytes(Path.Combine(output, "fixed-font-receipt.escpos"), first.Pages[0].Data);
    }
}
