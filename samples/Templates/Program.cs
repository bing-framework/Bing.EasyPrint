using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var renderer = new TemplateRenderer();
var loader = new TemplateLoader();
var label = new BpTemplate { Width = 576, Height = 320, Rows = new List<BpRow> {
    new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> {
        new BpFontElement { FontSize = 32, Bold = true, Field = new BgField { Value = "商品标签" } }
    } } } },
    new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> {
        new BpFontElement { Field = new BgField { Name = "Product.Name" } }
    } } } },
    new BpRow { Cells = new List<BpCell> { new BpCell { Elements = new List<BpElement> {
        new BpQrCodeElement { UnitWidth = 3, Field = new BgField { Name = "Product.Code" } }
    } } } }
} };
var product = new { Product = new { Name = "示例商品", Code = "SKU-001" } };
var fromCode = renderer.Render(label, product);
var fromJson = renderer.Render(loader.LoadJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "label.json"))), product);
if (!fromCode.Pages[0].Data.SequenceEqual(fromJson.Pages[0].Data)) throw new Exception("C# 与 JSON 输出不一致。");
var receipt = loader.LoadJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "receipt.json")));
var order = new { Title = "订单明细", Items = Enumerable.Range(1, 30).Select(i => new { Name = "商品 " + i, Quantity = i }).ToArray() };
var receiptCode = CreateReceiptTemplate();
var codePages = renderer.Render(receiptCode, order);
var jsonPages = renderer.Render(receipt, order);
AssertSamePages(codePages, jsonPages, "C# 模板与 receipt.json");
var receiptRoundTripJson = loader.SaveJson(receiptCode);
var receiptRoundTrip = loader.LoadJson(receiptRoundTripJson);
AssertSamePages(codePages, renderer.Render(receiptRoundTrip, order), "receipt.json 保存后重载");
var output = Path.GetFullPath(args.Length == 0 ? "artifacts/template-samples" : args[0]);
Directory.CreateDirectory(output);
File.WriteAllBytes(Path.Combine(output, "label.cpcl"), fromCode.Pages[0].Data);
for (var i = 0; i < jsonPages.PageCount; i++) File.WriteAllBytes(Path.Combine(output, $"receipt-{i + 1}.cpcl"), jsonPages.Pages[i].Data);
File.WriteAllText(Path.Combine(output, "label-roundtrip.json"), loader.SaveJson(label));
File.WriteAllText(Path.Combine(output, "receipt-roundtrip.json"), receiptRoundTripJson);
PriceLabelSamples.WriteTo(output, loader);
V3Samples.WriteTo(output, loader, renderer);
PrintJobSamples.WriteTo(output, renderer);
TsplCutterSamples.WriteTo(output, renderer);
ZplCutterSamples.WriteTo(output, renderer);
if (args.Length > 1) FixedFontSamples.WriteTo(output, renderer, args[1], args.Length > 2 ? args[2] : null);
var nativeTemplate = loader.LoadJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "native-mixed-receipt.json")));
var nativeReceipt = renderer.Render(nativeTemplate, new
{
    Title = "SHOP", Items = new[] { new { Description = "Apple  2.00" }, new { Description = "Tea    3.00" } },
    Code = "A-100", Footer = "Thank you"
}, options: new TemplateRenderOptions
{
    Protocol = PrintProtocol.EscPos, Encoding = Encoding.ASCII,
    EscPosDevice = new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Sequential }
});
for (var i = 0; i < nativeReceipt.PageCount; i++)
    File.WriteAllBytes(Path.Combine(output, $"native-mixed-{i + 1}.escpos"), nativeReceipt.Pages[i].Data);
Console.WriteLine($"已生成标签 1 页、小票 {jsonPages.PageCount} 页；C#、JSON 与往返输出一致：{output}");

static BpTemplate CreateReceiptTemplate() => new BpTemplate
{
    Version = 1,
    Width = 576,
    Height = 400,
    AutoHeight = true,
    Rows = new List<BpRow>
    {
        new BpRow
        {
            IsHeader = true,
            Cells = new List<BpCell>
            {
                new BpCell
                {
                    Elements = new List<BpElement>
                    {
                        new BpFontElement { Bold = true, Field = new BgField { Name = "Title" } }
                    }
                }
            }
        },
        new BpRow
        {
            RepeatSource = "Items",
            Condition = new BpCondition { Path = "Quantity", Operator = "gt", Value = 0 },
            Cells = new List<BpCell>
            {
                new BpCell
                {
                    Width = 400,
                    Elements = new List<BpElement>
                    {
                        new BpFontElement { Field = new BgField { Name = "Name" } }
                    }
                },
                new BpCell
                {
                    Width = 176,
                    Elements = new List<BpElement>
                    {
                        new BpFontElement { Field = new BgField { Name = "Quantity" } }
                    }
                }
            }
        }
    }
};

static void AssertSamePages(TemplateRenderResult expected, TemplateRenderResult actual, string source)
{
    if (expected.PageCount != actual.PageCount)
        throw new Exception($"{source}页数不一致：{expected.PageCount} != {actual.PageCount}。");
    for (var i = 0; i < expected.PageCount; i++)
    {
        var left = expected.Pages[i];
        var right = actual.Pages[i];
        if (left.Width != right.Width || left.Height != right.Height || !left.Data.SequenceEqual(right.Data))
            throw new Exception($"{source}第 {i + 1} 页的尺寸或 CPCL 字节不一致。");
    }
}
