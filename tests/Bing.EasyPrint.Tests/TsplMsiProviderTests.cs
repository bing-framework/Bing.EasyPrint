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

/// <summary>验证 TSPL MSI 原生命令、统一 Provider、模板旋转和能力边界。</summary>
public sealed class TsplMsiProviderTests
{
    [Fact]
    public void Tspl_command_writes_exact_msi_barcode_bytes()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .Barcode(10, 20, "MSI", 60, 0, 0, 2, 4, "12345")
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "BARCODE 10,20,\"MSI\",60,0,0,2,4,\"12345\"\r\n"), bytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123A")]
    [InlineData("123^4")]
    [InlineData("中文")]
    [InlineData("12345678901234")]
    public void Tspl_command_rejects_invalid_msi_data_without_polluting_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(1, 2, "before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.Barcode(
            10, 20, "MSI", 40, 0, 0, 2, 4, data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_failed_msi_validation_does_not_apply_pending_media_options()
    {
        var command = new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { GapDistance = 2, GapOffset = 1 })
            .Size(60, 40, "dot");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.Barcode(
            1, 2, "MSI", 40, 0, 0, 2, 4, "12345678901234"));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(RotationAngle.None, 10, 20)]
    [InlineData(RotationAngle.Rotate90, 50, 20)]
    [InlineData(RotationAngle.Rotate180, 110, 60)]
    [InlineData(RotationAngle.Rotate270, 10, 120)]
    public void Tspl_provider_writes_msi_at_rotated_bbox_anchor(
        RotationAngle rotation, int expectedX, int expectedY)
    {
        var document = new PrintDocument();
        document.AddPage(220, 220).Add(new PrintBarcodeElement(10, 20,
            BarcodeType.Msi, "12345", 40, narrowWidth: 2, wideWidth: 4,
            rotation: rotation, humanReadable: false, reservedWidth: 100),
            "Rows[0].Barcode");

        var output = Encoding.ASCII.GetString(TsplProvider().Render(document)[0]);

        Assert.Contains($"BARCODE {expectedX},{expectedY},\"MSI\",40,0,{(int)rotation},2,4,\"12345\"\r\n",
            output);
    }

    [Fact]
    public void Tspl_provider_renders_msi_with_exact_page_envelope()
    {
        var document = BarcodeDocument("12345", x: 10, y: 20,
            barHeight: 40, narrowWidth: 2, wideWidth: 4);

        var output = Encoding.ASCII.GetString(TsplProvider().Render(document)[0]);

        Assert.Equal(
            "SIZE 320 dot,120 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            "BARCODE 10,20,\"MSI\",40,0,0,2,4,\"12345\"\r\n" +
            "PRINT 1,1\r\n",
            output);
    }

    [Fact]
    public void Tspl_provider_rejects_invalid_msi_data_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => TsplProvider().Validate(
            BarcodeDocument("12345678901234", barHeight: 40)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("MSI", error.Message);
    }

    [Fact]
    public void Tspl_capabilities_expose_msi_while_other_non_cpcl_providers_keep_their_boundaries()
    {
        var tspl = TsplProvider().Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;

        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Msi));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Msi));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Msi));
        Assert.False(escPosNative.SupportsCommandBarcodeType(BarcodeType.Msi));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Msi));
        Assert.True(escPosRaster.SupportsCommandBarcodeType(BarcodeType.Msi));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Msi));
        Assert.True(cpcl.SupportsBarcodeType(BarcodeType.Msi));
    }

    [Fact]
    public void EscPos_native_rejects_msi_before_rendering()
    {
        var error = Assert.Throws<PrintCapabilityException>(() => PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native)
            .Validate(BarcodeDocument("12345", barHeight: 40)));

        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("不支持", error.Message);
    }

    [Fact]
    public void EscPos_raster_accepts_msi_before_rendering()
    {
        var provider = PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Raster);
        provider.Validate(BarcodeDocument("12345", barHeight: 40));
        Assert.NotEmpty(provider.Render(BarcodeDocument("12345", barHeight: 40))[0]);
    }

    [Theory]
    [InlineData(RotationAngle.None, 0)]
    [InlineData(RotationAngle.Rotate90, 90)]
    [InlineData(RotationAngle.Rotate180, 180)]
    [InlineData(RotationAngle.Rotate270, 270)]
    public void Tspl_template_renders_msi_using_each_native_rotation(
        RotationAngle rotation, int encodedRotation)
    {
        var result = new TemplateRenderer().RenderWithLayout(MsiTemplate(rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Tspl, result.Protocol);
        Assert.Contains($"\"MSI\",40,0,{encodedRotation},2,4,\"12345\"\r\n",
            Encoding.ASCII.GetString(page.Data));
        Assert.Contains(page.Elements, element => element.Type == 4 && element.Rotation == rotation);
    }

    private static IPrintProtocolProvider TsplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, int wideWidth = 2)
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Msi, data, barHeight,
            narrowWidth, wideWidth, humanReadable: false), "Barcode");
        return document;
    }

    private static BpTemplate MsiTemplate(RotationAngle rotation) => new BpTemplate
    {
        Version = 1,
        Width = 600,
        Height = 600,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Height = 600,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 600,
                        Elements = new List<BpElement>
                        {
                            new BpBarcodeElement
                            {
                                BarcodeType = BarcodeType.Msi,
                                Rotation = rotation,
                                LineWidth = 1,
                                Ratio = 2,
                                NarrowModuleWidth = 2,
                                WideModuleWidth = 4,
                                Height = 40,
                                Width = 500,
                                HumanReadable = false,
                                Field = new BgField { Value = "12345" }
                            }
                        }
                    }
                }
            }
        }
    };
}
