using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;

internal static class Gs1CompositeSamples
{
    internal static void WriteTo(string output, TemplateLoader loader, TemplateRenderer renderer)
    {
        WriteVariant(output, loader, renderer, BarcodeType.UccEan128, Gs1CompositeMode.CcC,
            "(01)12345678901231", "cc-c");
        WriteVariant(output, loader, renderer, BarcodeType.Gs1DataBarExpanded, Gs1CompositeMode.Auto,
            "(01)09506000134352(10)LOT-A", "expanded");
    }

    private static void WriteVariant(string output, TemplateLoader loader, TemplateRenderer renderer,
        BarcodeType linearType, Gs1CompositeMode mode, string primary, string name)
    {
        var template = new BpTemplate
        {
            Version = 3, Width = 600, Height = 300,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell
            {
                Elements = new List<BpElement> { new BpGs1CompositeElement
                {
                    LinearType = linearType, Mode = mode,
                    Field = new BgField { Name = "Primary" }, SecondaryField = new BgField { Name = "Secondary" },
                    ModuleWidth = 2, LinearBarHeight = 40
                } }
            } } } }
        };
        var data = new { Primary = primary, Secondary = "(17)270101(10)LOT123" };
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, $"gs1-composite-{name}.json"));
        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.EscPos, PrintProtocol.Tspl, PrintProtocol.Zpl })
        {
            var options = new TemplateRenderOptions
            {
                Protocol = protocol, Encoding = Encoding.ASCII, EscPosLayout = EscPosLayoutMode.Raster,
                FeedLines = 0, CutAfterPrint = false
            };
            var fromCode = renderer.Render(template, data, options: options).Pages.Single().Data;
            var fromJson = renderer.Render(loader.LoadJson(json, options), data, options: options).Pages.Single().Data;
            var saved = loader.SaveJson(template, options);
            var fromRoundtrip = renderer.Render(loader.LoadJson(saved, options), data, options: options).Pages.Single().Data;
            if (!fromCode.SequenceEqual(fromJson) || !fromCode.SequenceEqual(fromRoundtrip))
                throw new InvalidOperationException($"Composite {name} {protocol} C#/JSON/往返输出不一致。");
            File.WriteAllBytes(Path.Combine(output, $"gs1-composite-{name}.{protocol.ToString().ToLowerInvariant()}"), fromCode);
        }
        Console.WriteLine($"Composite {name} 四协议 C#/JSON/往返输出一致。");
    }
}
