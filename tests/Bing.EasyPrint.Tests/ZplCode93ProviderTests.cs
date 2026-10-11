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

/// <summary>验证 ZPL Code 93 的原生命令、统一 Provider 和模板能力边界。</summary>
public sealed class ZplCode93ProviderTests
{
    [Theory]
    [InlineData(0, "N", false, "N")]
    [InlineData(90, "R", false, "N")]
    [InlineData(180, "I", false, "N")]
    [InlineData(270, "B", false, "N")]
    [InlineData(0, "N", true, "Y")]
    public void Zpl_command_writes_exact_code93_bytes_for_each_rotation_and_text_flag(
        int rotation, string orientation, bool printText, string textFlag)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .Barcode93(10, 20, "Code93-A", 60, printText, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^BA{orientation},60,{textFlag},N,N^FDCode93-A^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_command_accepts_lowercase_and_extended_printable_ascii_data()
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .Barcode93(1, 2, "a-z$+%", 40, false, 2)
            .End()
            .Build()
            .GetBytes();

        Assert.Contains("^FDa-z$+%^FS", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void Zpl_command_can_show_code93_check_digits_in_the_interpretation_line()
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .Barcode93(10, 20, "Code93", 60, true, 3, true, 0)
            .End()
            .Build()
            .GetBytes();

        Assert.Contains("^FO10,20^BY3^BAN,60,Y,N,Y^FDCode93^FS\r\n",
            Encoding.ASCII.GetString(bytes));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("A^B")]
    [InlineData("A~B")]
    [InlineData("A\u0001B")]
    [InlineData("A\u007fB")]
    [InlineData("中文")]
    public void Zpl_command_rejects_invalid_code93_data_without_polluting_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.Barcode93(1, 2, data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(32001, 2)]
    [InlineData(60, 0)]
    [InlineData(60, 11)]
    public void Zpl_command_rejects_invalid_code93_dimensions(int height, int moduleWidth)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Barcode93(
            1, 2, "Code93", height, false, moduleWidth));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_command_rejects_rotation_when_profile_disables_it_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.Barcode93(
            1, 2, "Code93", 40, false, 2, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_and_raster_capabilities_expose_code93_and_code128()
    {
        var capabilities = ZplProvider().Capabilities;

        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(capabilities.SupportsCommandBarcodeType(BarcodeType.Code93));
        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.Code128));
        Assert.True(capabilities.SupportsBarcodeType(BarcodeType.Code39));
        Assert.True(PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities.SupportsBarcodeType(BarcodeType.Code93));
        Assert.True(PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities.SupportsBarcodeType(BarcodeType.Code93));
    }

    [Fact]
    public void Zpl_provider_renders_code93_with_exact_page_envelope()
    {
        var bytes = ZplProvider().Render(BarcodeDocument(BarcodeType.Code93, "Code93",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW320\r\n^LL120\r\n" +
            "^FO10,20^BY2^BAN,40,N,N,N^FDCode93^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_provider_keeps_code128_and_code39_output_while_adding_code93()
    {
        var code128 = Encoding.ASCII.GetString(ZplProvider().Render(
            BarcodeDocument(BarcodeType.Code128, "12345", barHeight: 40,
                narrowWidth: 3, humanReadable: false))[0]);
        var code39 = Encoding.ASCII.GetString(ZplProvider().Render(
            BarcodeDocument(BarcodeType.Code39, "ABC-12", barHeight: 40,
                narrowWidth: 3, humanReadable: false))[0]);

        Assert.Contains("^FO0,0^BY3^BCN,40,N,N,N^FD12345^FS\r\n", code128);
        Assert.Contains("^FO0,0^BY3^B3N,N,40,N,N^FDABC-12^FS\r\n", code39);
    }

    [Fact]
    public void Zpl_provider_rejects_invalid_code93_data_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument(BarcodeType.Code93, "中文", barHeight: 40)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("ASCII", error.Message);
    }

    [Fact]
    public void Zpl_profile_can_disable_code93_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code93));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Code93));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument(BarcodeType.Code93, "Code93", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(RotationAngle.None, "N")]
    [InlineData(RotationAngle.Rotate90, "R")]
    [InlineData(RotationAngle.Rotate180, "I")]
    [InlineData(RotationAngle.Rotate270, "B")]
    public void Native_template_renders_code93_using_each_zpl_orientation(
        RotationAngle rotation, string orientation)
    {
        var result = new TemplateRenderer().RenderWithLayout(Code93Template(rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains($"^BA{orientation},40,N,N,N^FDCode93^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void EscPos_raster_renders_code93_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.NotEmpty(provider.Render(BarcodeDocument(BarcodeType.Code93, "Code93", barHeight: 40))[0]);
    }

    [Fact]
    public void Tspl_declares_and_renders_code93_through_the_native_barcode_command()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code93));
        var output = Encoding.ASCII.GetString(provider.Render(
            BarcodeDocument(BarcodeType.Code93, "Code93", barHeight: 40))[0]);

        Assert.Contains("BARCODE 0,0,\"93\",40,1,0,2,2,\"Code93\"\r\n", output);
    }

    [Fact]
    public void Cpcl_keeps_code93_capability_for_legacy_provider()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code93));
        Assert.NotEmpty(provider.Render(BarcodeDocument(BarcodeType.Code93, "Code93", barHeight: 40)));
    }

    [Fact]
    public void Raster_template_renders_code93_with_template_element_path()
    {
        var result = new TemplateRenderer().Render(
            Code93Template(RotationAngle.None), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        Assert.Single(result.Pages);
        Assert.NotEmpty(result.Pages[0].Data);
    }

    private static IPrintProtocolProvider ZplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(BarcodeType type, string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, bool? humanReadable = null)
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, type, data, barHeight, narrowWidth, 2,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate Code93Template(RotationAngle rotation)
        => new BpTemplate
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
