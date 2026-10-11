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

/// <summary>验证 ZPL 原生 Intelligent Mail、统一 Provider 和跨协议能力边界。</summary>
public sealed class ZplIntelligentMailProviderTests
{
    [Theory]
    [InlineData("12345678901234567890", 0, "N")]
    [InlineData("12345678901234567890", 90, "R")]
    [InlineData("12345678901234567890", 180, "I")]
    [InlineData("12345678901234567890", 270, "B")]
    [InlineData("1234567890123456789012345", 0, "N")]
    [InlineData("1234567890123456789012345", 90, "R")]
    [InlineData("1234567890123456789012345", 180, "I")]
    [InlineData("1234567890123456789012345", 270, "B")]
    [InlineData("12345678901234567890123456789", 0, "N")]
    [InlineData("12345678901234567890123456789", 90, "R")]
    [InlineData("12345678901234567890123456789", 180, "I")]
    [InlineData("12345678901234567890123456789", 270, "B")]
    [InlineData("1234567890123456789012345678901", 0, "N")]
    [InlineData("1234567890123456789012345678901", 90, "R")]
    [InlineData("1234567890123456789012345678901", 180, "I")]
    [InlineData("1234567890123456789012345678901", 270, "B")]
    public void Zpl_command_writes_exact_intelligent_mail_bytes_for_each_length_and_rotation(
        string data, int rotation, string orientation)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeIntelligentMail(10, 20, data, 60, false, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^BZ{orientation},60,N,N,3^FD{data}^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData("12345-67890-12345-67890", "12345678901234567890")]
    [InlineData("12345-67890-12345-67890-12345", "1234567890123456789012345")]
    [InlineData("12345-67890-12345-67890-123456789", "12345678901234567890123456789")]
    [InlineData("12345-67890-12345-67890-12345-678901", "1234567890123456789012345678901")]
    public void Zpl_command_normalizes_intelligent_mail_hyphens(
        string data, string normalized)
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeIntelligentMail(10, 20, data, 40, true, 2, 0)
            .End()
            .Build()
            .GetBytes());

        Assert.Contains($"^FO10,20^BY2^BZN,40,Y,N,3^FD{normalized}^FS\r\n", output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("--------------------")]
    [InlineData("1234567890123456789")]
    [InlineData("123456789012345678901")]
    [InlineData("123456789012345678901234")]
    [InlineData("12345678901234567890123456")]
    [InlineData("1234567890123456789012345678")]
    [InlineData("123456789012345678901234567890")]
    [InlineData("12345678901234567890123456789012")]
    [InlineData("1234567890123456789A")]
    [InlineData("1234567890123456789 0")]
    [InlineData("1234567890123456789^0")]
    [InlineData("1234567890123456789~0")]
    [InlineData("中文")]
    public void Zpl_command_rejects_invalid_intelligent_mail_data_without_polluting_output(
        string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeIntelligentMail(
            1, 2, data, 40, false, 2, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(32001, 2)]
    [InlineData(60, 0)]
    [InlineData(60, 11)]
    public void Zpl_command_rejects_invalid_intelligent_mail_dimensions_without_polluting_output(
        int height, int moduleWidth)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.BarcodeIntelligentMail(
            1, 2, "12345678901234567890", height, false, moduleWidth, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_command_rejects_intelligent_mail_rotation_when_profile_disables_it_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile { SupportsRotation = false }
        }).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<NotSupportedException>(() => command.BarcodeIntelligentMail(
            1, 2, "12345678901234567890", 40, false, 2, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_capabilities_expose_intelligent_mail_only_on_zpl()
    {
        var zpl = ZplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(zpl.SupportsBarcodeType(BarcodeType.IntelligentMail));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(cpcl.SupportsBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(cpcl.SupportsCommandBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(tspl.SupportsBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(tspl.SupportsCommandBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(escPosNative.SupportsCommandBarcodeType(BarcodeType.IntelligentMail));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(escPosRaster.SupportsCommandBarcodeType(BarcodeType.IntelligentMail));
    }

    [Fact]
    public void Zpl_provider_renders_intelligent_mail_with_exact_page_envelope()
    {
        var bytes = ZplProvider().Render(BarcodeDocument("12345-67890-12345-67890",
            x: 10, y: 20, barHeight: 40, narrowWidth: 2, humanReadable: false))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW1200\r\n^LL120\r\n" +
            "^FO10,20^BY2^BZN,40,N,N,3^FD12345678901234567890^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_provider_rejects_invalid_intelligent_mail_length_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument("123456789012345678901", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("Intelligent Mail", error.Message);
    }

    [Theory]
    [InlineData(RotationAngle.None, "N")]
    [InlineData(RotationAngle.Rotate90, "R")]
    [InlineData(RotationAngle.Rotate180, "I")]
    [InlineData(RotationAngle.Rotate270, "B")]
    public void Native_template_renders_intelligent_mail_using_each_zpl_orientation(
        RotationAngle rotation, string orientation)
    {
        var result = new TemplateRenderer().RenderWithLayout(
            IntelligentMailTemplate("12345678901234567890", rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains($"^BZ{orientation},40,N,N,3^FD12345678901234567890^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Cpcl)]
    public void Non_zpl_providers_reject_intelligent_mail_before_rendering(PrintProtocol protocol)
    {
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.IntelligentMail));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345678901234567890", barHeight: 40)));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.NotEmpty(error.Message);
    }

    [Theory]
    [InlineData(EscPosLayoutMode.Native)]
    public void EscPos_providers_reject_intelligent_mail_before_rendering(EscPosLayoutMode layout)
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, layout);

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.IntelligentMail));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345678901234567890", barHeight: 40)));

        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.NotEmpty(error.Message);
    }

    [Fact]
    public void Zpl_profile_can_disable_intelligent_mail_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.IntelligentMail));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.IntelligentMail));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345678901234567890", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    private static IPrintProtocolProvider ZplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, bool? humanReadable = null)
    {
        var document = new PrintDocument();
        var page = document.AddPage(1200, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.IntelligentMail, data, barHeight, narrowWidth, 2,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate IntelligentMailTemplate(string data, RotationAngle rotation)
        => new BpTemplate
        {
            Version = 1,
            Width = 1200,
            Height = 1200,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 1200,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 1200,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.IntelligentMail,
                                    Rotation = rotation,
                                    LineWidth = 1,
                                    Ratio = 2,
                                    Height = 40,
                                    Width = 1100,
                                    HumanReadable = false,
                                    Field = new BgField { Value = data }
                                }
                            }
                        }
                    }
                }
            }
        };
}
