using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;

/// <summary>展示已确认机型的 TSPL 批次裁切和立即裁切；参数须替换为目标设备实测值。</summary>
internal static class TsplCutterSamples
{
    internal static void WriteTo(string output, TemplateRenderer renderer)
    {
        var batch = new TsplMediaOptions { Profile = new TsplPrinterProfile { Name = "Verified batch example",
            Cutter = new TsplCutterProfile { SupportsEndOfJob = true,
                SupportedCutModes = new[] { PrintCutMode.Full, PrintCutMode.Partial } } } };
        var command = new TsplPrintCommand(Encoding.ASCII, batch).Size(320, 80, "dot")
            .Text(8, 8, "Batch label").PrintWithCut(new PrintCutOptions { Mode = PrintCutMode.Partial });
        var bytes = command.Build().GetBytes();
        if (!bytes.SequenceEqual(command.Build().GetBytes())) throw new Exception("TSPL 重复构建字节不一致。");
        File.WriteAllBytes(Path.Combine(output, "tspl-batch-partial.tspl"), bytes);

        var immediate = new TsplMediaOptions { Profile = new TsplPrinterProfile { Name = "Verified immediate example",
            Cutter = new TsplCutterProfile { Strategy = TsplCutStrategy.Immediate, SupportsEndOfJob = true,
                CutPositionFeedDots = 12 } } };
        var template = new BpTemplate { Width = 320, Height = 80, Rows = new List<BpRow> {
            new BpRow { Height = 24, Cells = new List<BpCell> { new BpCell { Width = 320,
                Elements = new List<BpElement> { new BpFontElement { FontSize = 24,
                    Field = new BgField { Value = "Immediate label" } } } } } } } };
        var result = renderer.Render(template, null, options: new TemplateRenderOptions {
            Protocol = PrintProtocol.Tspl, Encoding = Encoding.ASCII, TsplMedia = immediate,
            Job = new PrintJobOptions { Copies = 2, CutScope = PrintFinishScope.JobEnd,
                CutOptions = new PrintCutOptions { FeedToCutPosition = true,
                    FeedBeforeCutAmount = 4, FeedBeforeCutUnit = PrintFeedUnit.Dots } } });
        if (result.PageCount != 2 || result.Pages[0].CutAfterPrint == true || result.Pages[1].CutAfterPrint != true)
            throw new Exception("TSPL 任务裁切边界不正确。");
        for (var index = 0; index < result.PageCount; index++)
            File.WriteAllBytes(Path.Combine(output, $"tspl-immediate-{index + 1}.tspl"), result.Pages[index].Data);
    }
}
