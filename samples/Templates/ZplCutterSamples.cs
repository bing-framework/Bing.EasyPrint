using System.Text;
using System.Text.Json;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;

/// <summary>演示三种已确认设备的 ZPL 切刀契约和独立文件；示例值须按目标设备替换。</summary>
internal static class ZplCutterSamples
{
    internal static void WriteTo(string output, TemplateRenderer renderer)
    {
        foreach (var strategy in new[] { ZplCutStrategy.Cutter, ZplCutStrategy.Delayed, ZplCutStrategy.Kiosk })
        {
            var cutter = new ZplCutterProfile { Strategy = strategy, SupportsTearOff = true };
            if (strategy == ZplCutStrategy.Kiosk)
            {
                cutter.SupportedCutModes = new[] { PrintCutMode.Full, PrintCutMode.Partial };
                cutter.KioskPartialUncutMillimeters = 20;
                cutter.KioskCutMarginMillimeters = 9;
            }
            var media = new ZplMediaOptions { Profile = new ZplPrinterProfile { Name = "Confirmed example", Cutter = cutter } };
            var mode = strategy == ZplCutStrategy.Kiosk ? PrintCutMode.Partial : PrintCutMode.Full;
            var command = new ZplPrintCommand(Encoding.ASCII, media).Start().Label(320, 80)
                .Text(8, 8, "ZPL cutter", 24, 24).PrintWithCut(new PrintCutOptions { Mode = mode }).End();
            var first = command.BuildOutput();
            var second = command.BuildOutput();
            if (first.Segments.Count != second.Segments.Count || first.Segments.Where((segment, index) =>
                !segment.Data.SequenceEqual(second.Segments[index].Data)).Any()) throw new Exception("ZPL 重复构建文件不一致。");
            var directory = Path.Combine(output, $"zpl-cutter-{strategy.ToString().ToLowerInvariant()}");
            Directory.CreateDirectory(directory);
            for (var index = 0; index < first.Segments.Count; index++)
                File.WriteAllBytes(Path.Combine(directory, $"segment-{index + 1:000}.zpl"), first.Segments[index].Data);
            if (strategy != ZplCutStrategy.Delayed) continue;

            var template = new BpTemplate { Width = 320, Height = 80, Rows = new List<BpRow> {
                new BpRow { Height = 24, Cells = new List<BpCell> { new BpCell { Width = 320,
                    Elements = new List<BpElement> { new BpFontElement { FontSize = 24,
                        Field = new BgField { Value = "Delayed job" } } } } } } } };
            var result = renderer.Render(template, null, options: new TemplateRenderOptions {
                Protocol = PrintProtocol.Zpl, Encoding = Encoding.ASCII, ZplMedia = media,
                Job = new PrintJobOptions { Copies = 2, CutScope = PrintFinishScope.EachCopy } });
            var files = new List<object>();
            for (var page = 0; page < result.PageCount; page++)
                for (var segment = 0; segment < result.Pages[page].Segments.Count; segment++)
                {
                    var file = $"page-{page + 1:000}-segment-{segment + 1:000}.zpl";
                    var entry = result.Pages[page].Segments[segment];
                    File.WriteAllBytes(Path.Combine(directory, file), entry.Data);
                    files.Add(new { file, pageIndex = page, segmentIndex = segment, kind = entry.Kind.ToString() });
                }
            File.WriteAllText(Path.Combine(directory, "job.json"), JsonSerializer.Serialize(new {
                outputVersion = 2, preserveFileBoundaries = true, protocol = "Zpl", files
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
