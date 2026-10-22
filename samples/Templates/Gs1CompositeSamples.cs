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
        var template = new BpTemplate
        {
            Version = 3, Width = 600, Height = 300,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell
            {
                Elements = new List<BpElement> { new BpGs1CompositeElement
                {
                    LinearType = BarcodeType.UccEan128, Mode = Gs1CompositeMode.CcC,
                    Field = new BgField { Name = "Primary" }, SecondaryField = new BgField { Name = "Secondary" },
                    ModuleWidth = 2, LinearBarHeight = 40
                } }
            } } } }
        };
        var data = new { Primary = "(01)12345678901231", Secondary = "(17)270101(10)LOT123" };
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "gs1-composite-cc-c.json"));
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
                throw new InvalidOperationException($"CC-C {protocol} C#/JSON/往返输出不一致。");
            File.WriteAllBytes(Path.Combine(output, $"gs1-composite-cc-c.{protocol.ToString().ToLowerInvariant()}"), fromCode);
        }
        Console.WriteLine("CC-C 四协议 C#/JSON/往返输出一致。");
    }
}
