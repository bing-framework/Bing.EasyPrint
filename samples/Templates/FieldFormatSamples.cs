using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;

internal static class FieldFormatSamples
{
    internal static void WriteTo(string output, TemplateLoader loader, TemplateRenderer renderer)
    {
        var template = new BpTemplate
        {
            Version = 2,
            Width = 420,
            Height = 160,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 40,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 420,
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    Field = new BgField
                                    {
                                        Name = "Amount",
                                        Format = new BgFieldFormat
                                        {
                                            Kind = BgFieldFormatKind.Currency,
                                            Pattern = "C2",
                                            Culture = "en-US",
                                            Prefix = "USD ",
                                            PadLength = 12,
                                            PadCharacter = " "
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                new BpRow
                {
                    Height = 40,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 420,
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    Field = new BgField
                                    {
                                        Name = "When",
                                        Format = new BgFieldFormat
                                        {
                                            Kind = BgFieldFormatKind.Date,
                                            Pattern = "yyyy-MM-dd HH:mm zzz",
                                            Culture = "en-US"
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                new BpRow
                {
                    Height = 40,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 420,
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    Field = new BgField
                                    {
                                        Name = "Status",
                                        Format = new BgFieldFormat
                                        {
                                            Kind = BgFieldFormatKind.Enum,
                                            Map = new Dictionary<string, string>
                                            {
                                                ["A"] = "Available",
                                                ["D"] = "Disabled"
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
        var data = new
        {
            Amount = "1234.5",
            When = "2024-01-02T03:04:05+08:00",
            Status = "A"
        };
        var rendered = renderer.Render(template, data);
        File.WriteAllBytes(Path.Combine(output, "field-format-sample.cpcl"), rendered.Pages[0].Data);
        File.WriteAllText(Path.Combine(output, "field-format-sample.json"), loader.SaveJson(template));
        CultureInfo.CurrentCulture = CultureInfo.CurrentCulture;
    }
}
