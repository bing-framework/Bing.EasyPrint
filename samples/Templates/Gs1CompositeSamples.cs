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
        WriteVariant(output, loader, renderer, BarcodeType.UccEan128, Gs1CompositeMode.CcA,
            "(01)12345678901231", "gs1-128-cc-a");
        WriteVariant(output, loader, renderer, BarcodeType.UccEan128, Gs1CompositeMode.CcB,
            "(01)12345678901231", "gs1-128-cc-b");
        WriteVariant(output, loader, renderer, BarcodeType.Gs1DataBarExpanded, Gs1CompositeMode.Auto,
            "(01)09506000134352(10)LOT-A", "expanded");
        WriteVariant(output, loader, renderer, BarcodeType.Gs1DataBarExpandedStacked, Gs1CompositeMode.Auto,
            "(01)09506000134352(10)LOT-A", "expanded-stacked");
        WriteVariant(output, loader, renderer, BarcodeType.Gs1DataBarExpandedStacked, Gs1CompositeMode.Auto,
            "[91]1234567890123456789012345678901234", "expanded-stacked-columns", expandedStackedColumns: 3);
        WriteVariant(output, loader, renderer, BarcodeType.Gs1DataBarExpandedStacked, Gs1CompositeMode.Auto,
            "[91]1234567890123456789012345678901234", "expanded-stacked-rows", expandedStackedRows: 2);
        WriteOrdinaryColumns(output, loader, renderer);
        WriteOrdinaryRows(output, loader, renderer);
        WriteVariant(output, loader, renderer, BarcodeType.Gs1DataBarStacked, Gs1CompositeMode.Auto,
            "(01)12345678901231", "stacked");
        WriteVariant(output, loader, renderer, BarcodeType.Ean8, Gs1CompositeMode.Auto,
            "12345670", "ean8");
        WriteVariant(output, loader, renderer, BarcodeType.Ean13, Gs1CompositeMode.Auto,
            "1234567890128", "ean13");
        WriteVariant(output, loader, renderer, BarcodeType.UpcA, Gs1CompositeMode.CcB,
            "012345678905", "upca");
        WriteVariant(output, loader, renderer, BarcodeType.UpcE, Gs1CompositeMode.Auto,
            "01234565", "upce");
        WriteVariant(output, loader, renderer, BarcodeType.Ean8, Gs1CompositeMode.CcB,
            "9876543+65", "ean8-addon");
        WriteVariant(output, loader, renderer, BarcodeType.Ean13, Gs1CompositeMode.CcB,
            "123456789012+54321", "ean13-addon");
        WriteVariant(output, loader, renderer, BarcodeType.UpcA, Gs1CompositeMode.CcB,
            "12345678901+12", "upca-addon");
        WriteVariant(output, loader, renderer, BarcodeType.UpcE, Gs1CompositeMode.CcB,
            "1876543+56789", "upce-addon");
        WriteVariant(output, loader, renderer, BarcodeType.Gs1DataBarTruncated, Gs1CompositeMode.Auto,
            "(01)12345678901231", "truncated");
        WriteVariant(output, loader, renderer, BarcodeType.Gs1DataBarStackedOmnidirectional, Gs1CompositeMode.Auto,
            "(01)12345678901231", "stacked-omnidirectional", 68);
    }

    private static void WriteVariant(string output, TemplateLoader loader, TemplateRenderer renderer,
        BarcodeType linearType, Gs1CompositeMode mode, string primary, string name, int linearHeight = 40,
        int? expandedStackedColumns = null, int? expandedStackedRows = null)
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
                    ModuleWidth = 2, LinearBarHeight = linearHeight, ExpandedStackedColumns = expandedStackedColumns,
                    ExpandedStackedRows = expandedStackedRows
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

    private static void WriteOrdinaryColumns(string output, TemplateLoader loader, TemplateRenderer renderer)
    {
        var template = new BpTemplate { Version = 3, Width = 200, Height = 500,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell {
                Elements = new List<BpElement> { new BpBarcodeElement {
                    BarcodeType = BarcodeType.Gs1DataBarExpandedStacked, ExpandedStackedColumns = 1,
                    Height = 35, NarrowModuleWidth = 2, HumanReadable = false, Field = new BgField { Name = "Primary" }
                } }
            } } } } };
        var data = new { Primary = "[91]1234567890123456789012345678901234" };
        var options = new TemplateRenderOptions { Protocol = PrintProtocol.EscPos, EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII, FeedLines = 0, CutAfterPrint = false };
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "expanded-stacked-columns.json"));
        var code = renderer.Render(template, data, options: options).Pages.Single().Data;
        var loaded = renderer.Render(loader.LoadJson(json, options), data, options: options).Pages.Single().Data;
        var restored = renderer.Render(loader.LoadJson(loader.SaveJson(template, options), options), data, options: options).Pages.Single().Data;
        if (!code.SequenceEqual(loaded) || !code.SequenceEqual(restored))
            throw new InvalidOperationException("普通 ExpandedStacked 单列 C#/JSON/往返输出不一致。");
        File.WriteAllBytes(Path.Combine(output, "expanded-stacked-columns.escpos"), code);
        Console.WriteLine("普通 ExpandedStacked 单列 Raster C#/JSON/往返输出一致。");
    }

    private static void WriteOrdinaryRows(string output, TemplateLoader loader, TemplateRenderer renderer)
    {
        var template = new BpTemplate { Version = 3, Width = 700, Height = 500,
            Rows = new List<BpRow> { new BpRow { Cells = new List<BpCell> { new BpCell {
                Elements = new List<BpElement> { new BpBarcodeElement {
                    BarcodeType = BarcodeType.Gs1DataBarExpandedStacked, ExpandedStackedRows = 2,
                    Height = 35, NarrowModuleWidth = 2, HumanReadable = false, Field = new BgField { Name = "Primary" }
                } }
            } } } } };
        var data = new { Primary = "[91]12345678901234567890123456789012345678901234567890123456789012345678" };
        var options = new TemplateRenderOptions { Protocol = PrintProtocol.EscPos, EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII, FeedLines = 0, CutAfterPrint = false };
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "expanded-stacked-rows.json"));
        var code = renderer.Render(template, data, options: options).Pages.Single().Data;
        var loaded = renderer.Render(loader.LoadJson(json, options), data, options: options).Pages.Single().Data;
        var restored = renderer.Render(loader.LoadJson(loader.SaveJson(template, options), options), data, options: options).Pages.Single().Data;
        if (!code.SequenceEqual(loaded) || !code.SequenceEqual(restored))
            throw new InvalidOperationException("普通 ExpandedStacked 最大行数 C#/JSON/往返输出不一致。");
        File.WriteAllBytes(Path.Combine(output, "expanded-stacked-rows.escpos"), code);
        Console.WriteLine("普通 ExpandedStacked 最大行数 Raster C#/JSON/往返输出一致。");
    }
}
