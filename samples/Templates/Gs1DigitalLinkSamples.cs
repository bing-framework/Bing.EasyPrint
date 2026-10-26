using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;

internal static class Gs1DigitalLinkSamples
{
    internal static void WriteTo(string output, TemplateLoader loader, TemplateRenderer renderer)
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "gs1-digital-link-template.json");
        var fromJson = loader.LoadJson(File.ReadAllText(jsonPath));
        var fromCode = CreateTemplate();
        var data = new { Product = new { Gtin = "09520123456788" } };
        var jsonResult = renderer.Render(fromJson, data);
        var codeResult = renderer.Render(fromCode, data);
        if (jsonResult.PageCount != codeResult.PageCount ||
            !jsonResult.Pages[0].Data.SequenceEqual(codeResult.Pages[0].Data))
            throw new Exception("Digital Link C# 与 JSON 模板输出不一致。");

        File.WriteAllBytes(Path.Combine(output, "gs1-digital-link.cpcl"), jsonResult.Pages[0].Data);
        File.WriteAllText(Path.Combine(output, "gs1-digital-link-roundtrip.json"), loader.SaveJson(fromCode));
        Console.WriteLine("Digital Link C# 与 JSON 模板输出一致，保留 GTIN 绑定、扩展参数及 fragment。");
    }

    private static BpTemplate CreateTemplate() => new BpTemplate
    {
        Version = 2,
        Width = 320,
        Height = 240,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Elements = new List<BpElement>
                        {
                            new BpQrCodeElement
                            {
                                UnitWidth = 4,
                                ErrorLevel = "M",
                                Gs1 = false,
                                Field = new BgField
                                {
                                    Gs1Input = new BgGs1Input
                                    {
                                        Mode = BgGs1InputMode.DigitalLink,
                                        BaseUri = "https://id.gs1.org",
                                        Fragment = "product-label",
                                        Items = new List<BgGs1InputItem>
                                        {
                                            new BgGs1InputItem { Ai = "01", Name = "Product.Gtin" },
                                            new BgGs1InputItem
                                            {
                                                Ai = "10", Value = "LOT-A",
                                                Placement = BgGs1InputPlacement.DataAttribute
                                            }
                                        },
                                        QueryParameters = new List<BgGs1InputQueryParameter>
                                        {
                                            new BgGs1InputQueryParameter { Key = "source", Value = "print" },
                                            new BgGs1InputQueryParameter { Key = "source", Value = "batch-7" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    };
}
