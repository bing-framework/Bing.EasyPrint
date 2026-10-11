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

/// <summary>验证 TSPL Code 93 的原生命令、统一 Provider、模板和协议能力边界。</summary>
public sealed class TsplCode93ProviderTests
{
    [Fact]
    public void Tspl_command_writes_exact_code93_barcode_bytes()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .Barcode(10, 20, "93", 60, 0, 0, 2, 4, "Code93-A")
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "BARCODE 10,20,\"93\",60,0,0,2,4,\"Code93-A\"\r\n"), bytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A\u0001B")]
    [InlineData("A\u007fB")]
    [InlineData("A\"B")]
    [InlineData("中文")]
    public void Tspl_command_rejects_invalid_code93_data_without_polluting_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(1, 2, "before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.Barcode(
            10, 20, "93", 40, 0, 0, 2, 4, data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_command_rejects_code93_rotation_when_profile_disables_it_without_polluting_output()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.Barcode(
            1, 2, "93", 40, 0, 90, 2, 4, "Code93"));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(RotationAngle.None, 10, 20)]
    [InlineData(RotationAngle.Rotate90, 50, 20)]
    [InlineData(RotationAngle.Rotate180, 510, 60)]
    [InlineData(RotationAngle.Rotate270, 10, 520)]
    public void Tspl_provider_writes_code93_at_rotated_bbox_anchor(
        RotationAngle rotation, int expectedX, int expectedY)
    {
        var output = Encoding.ASCII.GetString(TsplProvider().Render(
            BarcodeDocument(BarcodeType.Code93, "Code93", x: 10, y: 20,
                barHeight: 40, narrowWidth: 2, wideWidth: 4,
                rotation: rotation, humanReadable: false, reservedWidth: 500,
                pageWidth: 600, pageHeight: 600))[0]);

        Assert.Contains($"BARCODE {expectedX},{expectedY},\"93\",40,0,{(int)rotation},2,4,\"Code93\"\r\n",
            output);
    }

    [Fact]
    public void Tspl_capabilities_expose_code93_for_unified_and_native_commands()
    {
        var capabilities = TsplProvider().Capabilities;

        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(capabilities.SupportsCommandBarcodeType(BarcodeType.Code93));
    }

    [Fact]
    public void Tspl_provider_renders_code93_with_exact_page_envelope()
    {
        var bytes = TsplProvider().Render(BarcodeDocument(BarcodeType.Code93, "Code93",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, wideWidth: 4,
            humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "SIZE 320 dot,120 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            "BARCODE 10,20,\"93\",40,0,0,2,4,\"Code93\"\r\n" +
            "PRINT 1,1\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Tspl_provider_rejects_invalid_code93_data_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => TsplProvider().Validate(
            BarcodeDocument(BarcodeType.Code93, "中文", barHeight: 40)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("Code93", error.Message);
    }

    [Fact]
    public void Tspl_profile_can_disable_code93_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code93));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Code93));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument(BarcodeType.Code93, "Code93", barHeight: 40)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(RotationAngle.None, 0)]
    [InlineData(RotationAngle.Rotate90, 90)]
    [InlineData(RotationAngle.Rotate180, 180)]
    [InlineData(RotationAngle.Rotate270, 270)]
    public void Tspl_template_renders_code93_using_each_native_rotation(
        RotationAngle rotation, int encodedRotation)
    {
        var result = new TemplateRenderer().RenderWithLayout(Code93Template(rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Tspl, result.Protocol);
        Assert.Contains($"\"93\",40,0,{encodedRotation},2,4,\"Code93\"\r\n",
            Encoding.ASCII.GetString(page.Data));
        Assert.Contains(page.Elements, element => element.Type == 4 && element.Rotation == rotation);
    }

    [Fact]
    public void EscPos_and_zpl_keep_their_code93_capability_boundaries()
    {
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;

        Assert.True(escPosNative.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(escPosNative.SupportsCommandBarcodeType(BarcodeType.Code93));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.Code93));
    }

    [Fact]
    public void EscPos_native_rejects_rotated_code93_while_zpl_accepts_it()
    {
        var document = BarcodeDocument(BarcodeType.Code93, "Code93", x: 10, y: 20,
            barHeight: 40, narrowWidth: 2, wideWidth: 2,
            rotation: RotationAngle.Rotate90, reservedWidth: 500,
            pageWidth: 600, pageHeight: 600);

        var error = Assert.Throws<PrintCapabilityException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
                EscPosLayoutMode.Native).Validate(document));
        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);

        PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Validate(document);
    }

    private static IPrintProtocolProvider TsplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(BarcodeType type, string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, int wideWidth = 2,
        RotationAngle rotation = RotationAngle.None, bool? humanReadable = null,
        int reservedWidth = 0, int pageWidth = 320, int pageHeight = 120)
    {
        var document = new PrintDocument();
        var page = document.AddPage(pageWidth, pageHeight);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, type, data, barHeight, narrowWidth, wideWidth,
            rotation: rotation, humanReadable: humanReadable, reservedWidth: reservedWidth), "Barcode");
        return document;
    }

    private static BpTemplate Code93Template(RotationAngle rotation) => new BpTemplate
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
                                BarcodeType = BarcodeType.Code93,
                                Rotation = rotation,
                                LineWidth = 1,
                                Ratio = 2,
                                NarrowModuleWidth = 2,
                                WideModuleWidth = 4,
                                Height = 40,
                                Width = 500,
                                HumanReadable = false,
                                Field = new BgField { Value = "Code93" }
                            }
                        }
                    }
                }
            }
        }
    };
}
