using System.Text;
using System.Text.Json;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;

/// <summary>展示同一个分页模板在四协议下按整份顺序输出，并保存实际页归属。</summary>
internal static class PrintJobSamples
{
    internal static void WriteTo(string output, TemplateRenderer renderer)
    {
        var template = new BpTemplate
        {
            Version = 3, Width = 320, Height = 48,
            Rows = new[] { "ALPHA", "BRAVO", "CHARLIE" }.Select(value => new BpRow
            {
                Height = 24, Cells = new List<BpCell> { new BpCell
                {
                    Width = 320, Elements = new List<BpElement>
                    { new BpFontElement { FontSize = 24, Field = new BgField { Value = value } } }
                } }
            }).ToList()
        };
        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.EscPos, PrintProtocol.Tspl, PrintProtocol.Zpl })
        {
            var hasFeed = protocol == PrintProtocol.EscPos || protocol == PrintProtocol.Tspl;
            var result = renderer.RenderWithLayout(template, null, options: new TemplateRenderOptions
            {
                Protocol = protocol, Encoding = Encoding.ASCII,
                Job = new PrintJobOptions
                {
                    Copies = 2, CopyOrder = PrintCopyOrder.Collated,
                    FeedScope = hasFeed ? PrintFinishScope.JobEnd : PrintFinishScope.None,
                    FeedUnit = protocol == PrintProtocol.Tspl ? PrintFeedUnit.Dots : PrintFeedUnit.Lines,
                    FeedAmount = protocol == PrintProtocol.Tspl ? 24 : protocol == PrintProtocol.EscPos ? 2 : 0,
                    CutScope = protocol == PrintProtocol.EscPos || protocol == PrintProtocol.Zpl
                        ? PrintFinishScope.JobEnd : PrintFinishScope.None
                }
            });
            if (result.PageCount != 4 || !result.Pages.Select(page => (page.SourcePageIndex, page.CopyIndex))
                .SequenceEqual(new[] { ((int?)0, (int?)0), ((int?)1, (int?)0), ((int?)0, (int?)1), ((int?)1, (int?)1) }))
                throw new Exception($"{protocol} 任务的页面顺序不正确。");
            var protocolName = protocol switch
            {
                PrintProtocol.Cpcl => "Cpcl", PrintProtocol.EscPos => "EscPos",
                PrintProtocol.Tspl => "Tspl", PrintProtocol.Zpl => "Zpl",
                _ => throw new ArgumentOutOfRangeException(nameof(protocol))
            };
            var extension = protocolName.ToLowerInvariant();
            var directory = Path.Combine(output, "print-job-" + extension);
            Directory.CreateDirectory(directory);
            for (var index = 0; index < result.PageCount; index++)
                File.WriteAllBytes(Path.Combine(directory, $"page-{index + 1:000}.{extension}"), result.Pages[index].Data);
            var manifest = new
            {
                protocol = protocolName,
                pages = result.Pages.Select((page, index) => new
                {
                    file = $"page-{index + 1:000}.{extension}",
                    sourcePageIndex = page.SourcePageIndex, copyIndex = page.CopyIndex,
                    feedAfterPrint = page.FeedAfterPrint, cutAfterPrint = page.CutAfterPrint
                })
            };
            File.WriteAllText(Path.Combine(directory, "job.json"), JsonSerializer.Serialize(manifest,
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
