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

/// <summary>验证 ESC/POS Raster 主机 China Post（Datalogic 2 of 5）编码。</summary>
public sealed class EscPosRasterChinaPostTests
{
    [Fact]
    public void China_post_pattern_matches_the_datalogic_start_stop_and_digit_table()
    {
        const string data = "123456";
        const string expected =
            "101011101011101000101110111000101010111011101110111010100011101011101";

        var pattern = EscPosRasterCanvas.EncodeChinaPostPattern(data);

        Assert.Equal(69, pattern.Length);
        Assert.Equal(expected, pattern);
        Assert.Equal(data, EscPosRasterCanvas.HumanReadableBarcodeData(BarcodeType.ChinaPost, data));
        Assert.Equal(69, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.ChinaPost, data, 1));
        Assert.Equal(138, EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.ChinaPost, data, 2));
    }

    [Theory]
    [InlineData("0", 19)]
    [InlineData("0123456789", 109)]
    [InlineData("99999999999999999999", 209)]
    public void China_post_width_is_deterministic_for_numeric_payloads(string data, int width)
    {
        Assert.Equal(width, EscPosRasterCanvas.EncodeChinaPostPattern(data).Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123A")]
    [InlineData("12 34")]
    public void China_post_rejects_invalid_payloads_without_budget_pollution(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.ChinaPost, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Raster_command_writes_china_post_through_the_shared_matrix()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(240, 120)
            .RasterBarcode(BarcodeType.ChinaPost, 0, 0, "123456", 40, 2)
            .EndRasterPage();

        var bytes = command.Build().GetBytes();

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_provider_renders_china_post_in_all_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.ChinaPost, "123456", 40, narrowWidth: 2,
            humanReadable: true, humanReadableReserveHeight: 20, rotation: rotation), "ChinaPost");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.ChinaPost));
        Assert.True(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.ChinaPost));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_uses_china_post_host_matrix_and_measured_width()
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
                            BarcodeType = BarcodeType.ChinaPost,
                            NarrowModuleWidth = 2,
                            Height = 40,
                            HumanReadable = false,
                            Field = new BgField { Value = "123456" }
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
