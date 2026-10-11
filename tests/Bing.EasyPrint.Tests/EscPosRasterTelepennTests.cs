using System;
using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ESC/POS Raster 的 Telepen Numeric 主机编码。</summary>
public sealed class EscPosRasterTelepennTests
{
    [Fact]
    public void Telepenn_packs_odd_digits_with_a_leading_zero_and_known_check_symbol()
    {
        const string data = "12345";
        const string expected =
            "1010101010111000" + // start '_'
            "1110101010111010" + // 01 -> glyph 28
            "1110001000100010" + // 23 -> glyph 50
            "1110111000111000" + // 45 -> glyph 72
            "1110111000101010" + // check 104
            "1110001010101010";  // stop 'z'

        Assert.Equal(data, EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Telepenn, data));
        Assert.Equal(expected, EscPosRasterCanvas.EncodeTelepennPattern(data));
        Assert.Equal(96, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Telepenn, data, 1));
        Assert.Equal(192, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Telepenn, data, 2));
    }

    [Fact]
    public void Telepenn_has_a_complete_two_digit_glyph_range()
    {
        for (var value = 0; value <= 99; value++)
        {
            var pattern = EscPosRasterCanvas.EncodeTelepennPattern(value.ToString("00"));
            Assert.Equal(64, pattern.Length);
        }
    }

    [Fact]
    public void Telepenn_reserves_sixteen_modules_per_packed_symbol_at_host_limit()
    {
        var data = new string('9', 60);

        Assert.Equal(528, EscPosRasterCanvas.EncodeTelepennPattern(data).Length);
        Assert.Equal(528, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Telepenn, data, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123A")]
    [InlineData("123X")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901")]
    public void Telepenn_rejects_invalid_payload_without_consuming_working_budget(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Telepenn, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Raster_command_writes_telepenn_through_the_shared_host_matrix()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(400, 80)
            .RasterBarcode(BarcodeType.Telepenn, 0, 0, "12345", 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_provider_renders_telepenn_in_all_four_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10, BarcodeType.Telepenn,
            "12345", 40, narrowWidth: 2, humanReadable: false, rotation: rotation), "Barcode");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Telepenn));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Telepenn));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_uses_telepenn_host_matrix_and_measured_width()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 320,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Telepenn,
                            NarrowModuleWidth = 2,
                            Height = 40,
                            HumanReadable = false,
                            Field = new BgField { Value = "12345" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(40, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }
}
