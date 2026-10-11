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

public sealed class EscPosRasterGs1DataBarLimitedTests
{
    [Fact]
    public void Limited_pattern_matches_the_79_module_golden_symbol()
    {
        const string data = "0123456789012";
        const string expected =
            "0101000000111001010000101001010010101101001101000111001101010101111000010100000";

        var pattern = EscPosRasterCanvas.EncodeGs1DataBarLimitedPattern(data);

        Assert.Equal(79, pattern.Length);
        Assert.Equal(expected, pattern);
        Assert.Equal("(01)01234567890128",
            EscPosRasterCanvas.HumanReadableBarcodeData(BarcodeType.Gs1DataBarLimited, data));
        Assert.Equal(79, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarLimited, data, 1));
        Assert.Equal(158, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarLimited, data, 2));
    }

    [Theory]
    [InlineData("0000000000000")]
    [InlineData("1999999999999")]
    [InlineData("0123456789012")]
    public void Limited_payloads_keep_a_fixed_79_module_symbol(string data)
    {
        Assert.Equal(79, EscPosRasterCanvas.EncodeGs1DataBarLimitedPattern(data).Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2123456789012")]
    [InlineData("123456789012")]
    [InlineData("12345678901234")]
    [InlineData("012345678901A")]
    public void Limited_rejects_invalid_payloads_without_working_budget_pollution(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarLimited, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Raster_command_writes_limited_data_bar_through_the_shared_matrix()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(400, 120)
            .RasterBarcode(BarcodeType.Gs1DataBarLimited, 0, 0, "0123456789012", 40, 2)
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
    public void Raster_provider_renders_limited_data_bar_in_all_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.Gs1DataBarLimited, "0123456789012", 40,
            narrowWidth: 2, humanReadable: false, rotation: rotation), "DataBar");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarLimited));
        Assert.True(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Gs1DataBarLimited));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_uses_limited_data_bar_host_matrix_and_measured_width()
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
                            BarcodeType = BarcodeType.Gs1DataBarLimited,
                            NarrowModuleWidth = 2,
                            Height = 40,
                            HumanReadable = false,
                            Field = new BgField { Value = "0123456789012" }
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
