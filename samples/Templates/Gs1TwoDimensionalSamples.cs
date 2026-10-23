using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;

/// <summary>GS1 QR Code 与 GS1 DataMatrix 的模板和协议输出示例。</summary>
public static class Gs1TwoDimensionalSamples
{
    public static void WriteTo(string output, TemplateLoader loader, TemplateRenderer renderer)
    {
        var template = new BpTemplate
        {
            Version = 2,
            Width = 400,
            Height = 400,
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
                                    Gs1 = true,
                                    Field = new BgField { Value = "(01)12345678901231(10)LOT%20A" }
                                },
                                new BpMatrixCodeElement
                                {
                                    Format = "DataMatrix",
                                    Width = 96,
                                    Height = 96,
                                    Gs1 = true,
                                    Field = new BgField { Value = "(01)12345678901231(10)LOT%20A" }
                                }
                            }
                        }
                    }
                }
            }
        };

        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.Tspl, PrintProtocol.Zpl })
        {
            var result = renderer.Render(template, null, options: new TemplateRenderOptions
            {
                Protocol = protocol,
                Encoding = protocol == PrintProtocol.Zpl ? Encoding.UTF8 : Encoding.ASCII
            });
            File.WriteAllBytes(Path.Combine(output, $"gs1-two-dimensional-{protocol}.bin"), result.Pages[0].Data);
        }

        var raster = renderer.Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII
        });
        File.WriteAllBytes(Path.Combine(output, "gs1-two-dimensional-EscPosRaster.bin"), raster.Pages[0].Data);

        var strict = renderer.Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Cpcl,
            Encoding = Encoding.ASCII,
            ValidateGs1Associations = true
        });
        File.WriteAllBytes(Path.Combine(output, "gs1-two-dimensional-strict.cpcl"), strict.Pages[0].Data);

        var invalid = new BpTemplate
        {
            Version = 2,
            Width = 400,
            Height = 300,
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
                                new BpMatrixCodeElement
                                {
                                    Format = "DataMatrix",
                                    Width = 96,
                                    Height = 96,
                                    Gs1 = true,
                                    Field = new BgField { Value = "(02)12345678901231" }
                                }
                            }
                        }
                    }
                }
            }
        };
        try
        {
            renderer.Render(invalid, null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Cpcl,
                Encoding = Encoding.ASCII,
                ValidateGs1Associations = true
            });
            throw new Exception("严格 GS1 关联示例未拒绝缺少 (37) 的 DataMatrix。");
        }
        catch (TemplateRenderException error) when (error.Message.Contains("37", StringComparison.Ordinal))
        {
        }

        var json = loader.SaveJson(template);
        var restored = loader.LoadJson(json);
        var restoredResult = renderer.Render(restored, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.ASCII
        });
        if (!raster.Pages[0].Data.SequenceEqual(restoredResult.Pages[0].Data))
            throw new Exception("GS1 二维模板 C#/JSON 往返输出不一致。");

        try
        {
            renderer.Render(template, null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Native,
                Encoding = Encoding.ASCII
            });
            throw new Exception("GS1 二维模板不应通过 ESC/POS Native 输出。");
        }
        catch (TemplateRenderException)
        {
            // 预期的 Native 拒绝。
        }

        File.WriteAllText(Path.Combine(output, "gs1-two-dimensional-roundtrip.json"), json);
        Console.WriteLine("GS1 QR/DataMatrix 四协议图片输出与 JSON 往返一致。");
    }
}
