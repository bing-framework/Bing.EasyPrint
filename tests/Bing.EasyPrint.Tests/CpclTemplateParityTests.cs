using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 CPCL 模板绘制仍与等价的直接命令保持逐字节一致。
/// </summary>
public sealed class CpclTemplateParityTests
{
    private const int PageWidth = 400;
    private const int PageHeight = 300;

    [Fact]
    public void Template_vector_elements_match_equivalent_direct_command()
    {
        var template = new BpTemplate
        {
            Width = PageWidth,
            Height = PageHeight,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = PageHeight,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = PageWidth,
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    FontStyle = 4,
                                    Bold = true,
                                    Field = new BgField { Value = "TEXT" },
                                    StartCoordinate = new BpCoordinate { X = 10, Y = 10 }
                                },
                                new BpLineElement
                                {
                                    LineWidth = 2,
                                    StartCoordinate = new BpCoordinate { X = 10, Y = 80 },
                                    LineEndCoordinate = new BpCoordinate { X = 150, Y = 80 }
                                },
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Code128,
                                    LineWidth = 1,
                                    Ratio = 2,
                                    Height = 30,
                                    Field = new BgField { Value = "ABC" },
                                    StartCoordinate = new BpCoordinate { X = 180, Y = 40 }
                                },
                                new BpQrCodeElement
                                {
                                    UnitWidth = 2,
                                    ErrorLevel = "M",
                                    Field = new BgField { Value = "QR" },
                                    StartCoordinate = new BpCoordinate { X = 300, Y = 100 }
                                }
                            }
                        }
                    }
                }
            }
        };

        var actual = new TemplateRenderer().Render(template, null, options: Options()).Pages[0].Data;
        var expected = new CPCLPrintCommand(Encoding.ASCII)
            .SetPage(PageWidth, PageHeight)
            .DrawText(10, 10, "TEXT", 24, 0, true, false, true)
            .Line(10, 80, 150, 80, 2)
            .DrawBarcode1D(BarcodeType.Code128, 200, 40, "ABC", 1, 30, 2, 0)
            .DrawQrCode(308, 108, "QR", 2, "M", 0)
            .InitCommand(0, PageWidth, PageHeight, 1)
            .Build()
            .GetBytes();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Template_grayscale_and_compressed_images_match_equivalent_direct_commands()
    {
        using var image = CreateGrayscaleImage();
        using var encoded = new MemoryStream();
        image.SaveAsPng(encoded);
        var png = encoded.ToArray();
        var template = new BpTemplate
        {
            Width = 200,
            Height = 120,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 120,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 200,
                            Elements = new List<BpElement>
                            {
                                new BpImageElement
                                {
                                    Field = new BgField { Value = "gray" },
                                    StartCoordinate = new BpCoordinate { X = 3, Y = 4 }
                                },
                                new BpImageElement
                                {
                                    IsCompress = true,
                                    Field = new BgField { Value = "gray" },
                                    StartCoordinate = new BpCoordinate { X = 3, Y = 20 }
                                }
                            }
                        }
                    }
                }
            }
        };

        var actual = new TemplateRenderer().Render(template, null, new ImageResources(png), Options()).Pages[0].Data;
        var expected = new CPCLPrintCommand(Encoding.ASCII)
            .SetPage(200, 120)
            .DrawImage(3, 4, image)
            .DrawCompressedImage(3, 20, image)
            .InitCommand(0, 200, 120, 1)
            .Build()
            .GetBytes();

        Assert.Equal(expected, actual);
        var output = Encoding.ASCII.GetString(actual);
        Assert.Contains("EG 2 5 3 4 ", output);
        Assert.Contains("CG 2 5 3 20 ", output);
    }

    [Fact]
    public void Cpcl_provider_document_elements_match_direct_command_for_reverse_rotation_and_raster()
    {
        const int width = 240;
        const int height = 180;
        var raster = new byte[] { 0xAA, 0x80, 0x55, 0x40, 0xF0, 0x00, 0x18, 0xE7 };
        var document = new PrintDocument();
        document.AddPage(width, height)
            .Add(new PrintTextElement(5, 10, "REV", 24, bold: true, underline: true,
                reverse: true), "Rows[0].Text")
            .Add(new PrintBarcodeElement(60, 50, BarcodeType.Code128, "ABC", 30,
                narrowWidth: 1, wideWidth: 2, rotation: RotationAngle.Rotate270), "Rows[0].Barcode")
            .Add(new PrintQrCodeElement(130, 50, "QR", 2, QrCodeCorrectionLevel.Q,
                rotation: RotationAngle.Rotate180), "Rows[0].QrCode")
            .Add(new PrintImageElement(10, 100, 9, 4, raster, compressed: true), "Rows[0].Image");

        var actual = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII)
            .Render(document)[0];
        var expected = new CPCLPrintCommand(Encoding.ASCII)
            .SetPage(width, height)
            .DrawText(5, 10, "REV", 24, 0, true, true, true)
            .DrawBarcode1D(BarcodeType.Code128, 60, 50, "ABC", 1, 30, 2, RotationAngle.Rotate270)
            .DrawQrCode(130, 50, "QR", 2, (int)QrCodeCorrectionLevel.Q, (int)RotationAngle.Rotate180)
            .DrawRasterImage(10, 100, 9, 4, raster, compressed: true)
            .InitCommand(0, width, height, 1)
            .Build()
            .GetBytes();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Cpcl_provider_rejects_invalid_text_with_element_path()
    {
        var document = new PrintDocument();
        document.AddPage(200, 100)
            .Add(new PrintTextElement(0, 0, "bad\nPRINT", 24), "Rows[2].Cells[0].Elements[1]");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Validate(document));

        Assert.Equal(PrintProtocol.Cpcl, error.Protocol);
        Assert.Equal("Rows[2].Cells[0].Elements[1]", error.ElementPath);
        Assert.Contains("无效", error.Message);
    }

    private static TemplateRenderOptions Options() => new TemplateRenderOptions { Encoding = Encoding.ASCII };

    private static Image<Rgba32> CreateGrayscaleImage()
    {
        var image = new Image<Rgba32>(9, 5);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var value = (byte)((x * 31 + y * 17) % 256);
            image[x, y] = new Rgba32(value, value, value, 255);
        }
        return image;
    }

    private sealed class ImageResources : ITemplateResourceProvider
    {
        private readonly byte[] bytes;

        internal ImageResources(byte[] bytes) => this.bytes = bytes;

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
            => resourceKey == "gray" ? new MemoryStream(bytes, writable: false) : null;
    }
}
