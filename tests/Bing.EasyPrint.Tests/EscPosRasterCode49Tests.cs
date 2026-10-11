using System;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using ZXing.Common;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class EscPosRasterCode49Tests
{
    [Fact]
    public void Code49_encoder_matches_the_reference_two_row_grid()
    {
        var matrix = Code49Encoder.Encode("ABCD1234", CancellationToken.None);

        Assert.Equal(70, matrix.Width);
        Assert.Equal(2, matrix.Height);
        Assert.Equal(
            "1011111101101100101001111011000010111110100110011011101111100110101111",
            RowBits(matrix, 0));
        Assert.Equal(
            "1010111000010001001101100010100000111011101000111010001111011000101111",
            RowBits(matrix, 1));
    }

    [Fact]
    public void Code49_numeric_encodation_fits_the_standard_81_digit_limit()
    {
        var matrix = Code49Encoder.Encode(new string('1', 81), CancellationToken.None);

        Assert.Equal(70, matrix.Width);
        Assert.Equal(8, matrix.Height);
    }

    [Fact]
    public void Raster_code49_requires_the_minimum_row_height()
    {
        var canvas = new EscPosRasterCanvas(162, 38);

        Assert.Equal(162, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code49,
            "ABCD1234", 2));
        Assert.Equal(38, EscPosRasterCanvas.MeasureBarcodeHeight(BarcodeType.Code49,
            "ABCD1234", 16, 2, CancellationToken.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EscPosRasterCanvas.MeasureBarcodeHeight(BarcodeType.Code49,
                "ABCD1234", 15, 2, CancellationToken.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            canvas.DrawBarcode(BarcodeType.Code49, 0, 0, "ABCD1234", 15, 2));
    }

    [Fact]
    public void Raster_provider_renders_code49_and_uses_stacked_height_for_preflight()
    {
        var document = new PrintDocument();
        var page = document.AddPage(162, 46);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(0, 0, BarcodeType.Code49, "ABCD1234", 20,
            narrowWidth: 2, wideWidth: 2, humanReadable: false), "Code49");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code49));
        provider.Validate(document);
        var bytes = provider.Render(document)[0];

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Fact]
    public void Raster_provider_checks_the_complete_code49_footprint_at_page_edges()
    {
        var document = new PrintDocument();
        var page = document.AddPage(161, 38);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(0, 0, BarcodeType.Code49, "ABCD1234", 16,
            narrowWidth: 2, wideWidth: 2, humanReadable: false), "Code49");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
    }

    [Fact]
    public void Raster_code49_intrinsic_quiet_zones_reject_overlapping_elements()
    {
        var document = new PrintDocument();
        var page = document.AddPage(180, 50);
        page.Add(new PrintBarcodeElement(10, 5, BarcodeType.Code49, "ABCD1234", 16,
            narrowWidth: 2, wideWidth: 2, humanReadable: false), "Code49");
        page.Add(PrintRectangleElement.CreateFilled(12, 9, 1, 1), "InkInQuietZone");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
    }

    [Theory]
    [InlineData(RotationAngle.Rotate90, 40, 170, 3, 3)]
    [InlineData(RotationAngle.Rotate180, 170, 40, 145, 3)]
    [InlineData(RotationAngle.Rotate270, 40, 170, 3, 145)]
    public void Raster_code49_intrinsic_quiet_zones_rotate_with_the_symbol(RotationAngle rotation,
        int pageWidth, int pageHeight, int inkX, int inkY)
    {
        var document = new PrintDocument();
        var page = document.AddPage(pageWidth, pageHeight);
        page.Add(new PrintBarcodeElement(0, 0, BarcodeType.Code49, "ABCD1234", 16,
            narrowWidth: 2, wideWidth: 2, rotation: rotation, humanReadable: false), "Code49");
        page.Add(PrintRectangleElement.CreateFilled(inkX, inkY, 1, 1), "InkInRotatedQuietZone");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
    }

    [Fact]
    public void Raster_rotated_code49_page_preflight_includes_rotated_human_readable_reserve()
    {
        var document = new PrintDocument();
        var page = document.AddPage(53, 162);
        page.Add(new PrintBarcodeElement(0, 0, BarcodeType.Code49, "ABCD1234", 16,
            narrowWidth: 2, wideWidth: 2, rotation: RotationAngle.Rotate90,
            humanReadable: true, humanReadableReserveHeight: 16), "Code49");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));
    }

    [Fact]
    public void Raster_code49_emits_stable_golden_bytes()
    {
        var document = new PrintDocument();
        var page = document.AddPage(162, 38);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(0, 0, BarcodeType.Code49, "ABCD1234", 16,
            narrowWidth: 2, wideWidth: 2, humanReadable: false), "Code49");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.Equal(ExpectedRasterBytes(), provider.Render(document)[0]);
    }

    [Fact]
    public void Raster_template_renders_code49_and_reports_capability()
    {
        var template = new BpTemplate
        {
            Width = 980,
            Height = 42,
            Rows = new System.Collections.Generic.List<BpRow>
            {
                new BpRow
                {
                    Height = 42,
                    Cells = new System.Collections.Generic.List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 980,
                            Elements = new System.Collections.Generic.List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Code49,
                                    Height = 16,
                                    NarrowModuleWidth = 2,
                                    Field = new BgField { Value = "ABCD1234" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var result = new TemplateRenderer().Render(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            FeedLines = 0,
            CutAfterPrint = false,
            Encoding = Encoding.ASCII
        });

        Assert.Equal(PrintProtocol.EscPos, result.Protocol);
        Assert.Equal(980, result.Pages[0].Width);
        Assert.True(result.Pages[0].Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("中文")]
    [InlineData("A\u0080B")]
    public void Raster_code49_rejects_non_ascii_without_matrix_mutation(string data)
    {
        Assert.Throws<ArgumentException>(() =>
            EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code49, data, 2));
    }

    private static string RowBits(BitMatrix matrix, int row)
    {
        var result = new StringBuilder(matrix.Width);
        for (var column = 0; column < matrix.Width; column++)
            result.Append(matrix[column, row] ? '1' : '0');
        return result.ToString();
    }

    private static byte[] ExpectedRasterBytes()
    {
        const string firstRow = "1011111101101100101001111011000010111110100110011011101111100110101111";
        const string secondRow = "1010111000010001001101100010100000111011101000111010001111011000101111";
        const int width = 162;
        const int rowBytes = (width + 7) / 8;
        const int height = 38;
        var result = new byte[10 + rowBytes * height];
        new byte[] { 0x1B, 0x40, 0x1D, 0x76, 0x30, 0x00, rowBytes, 0x00, height, 0x00 }
            .CopyTo(result, 0);

        for (var y = 0; y < height; y++)
        {
            var boundaryRow = y < 2 || y >= 36;
            var separatorRow = y == 18 || y == 19;
            var source = y < 18 ? firstRow : secondRow;
            for (var x = 0; x < width; x++)
            {
                var patternColumn = x - 20;
                var inPattern = patternColumn >= 0 && patternColumn < 140;
                var black = boundaryRow ||
                    separatorRow && inPattern ||
                    !boundaryRow && !separatorRow && inPattern &&
                    source[patternColumn / 2] == '1';
                if (black)
                    result[10 + y * rowBytes + x / 8] |= (byte)(0x80 >> (x % 8));
            }
        }

        return result;
    }
}
