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

/// <summary>验证 ZPL ^BR GS1 DataBar 原生命令、统一 Provider 和严格能力边界。</summary>
public sealed class ZplDataBarProviderTests
{
    [Theory]
    [InlineData(0, "N")]
    [InlineData(90, "R")]
    [InlineData(180, "I")]
    [InlineData(270, "B")]
    public void Zpl_command_writes_omnidirectional_databar_for_each_rotation(
        int rotation, string orientation)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeGs1DataBarOmnidirectional(10, 20, "1234567890123", 60, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BR{orientation},1,3,1,60^FD1234567890123^FS\r\n" +
            "^XZ\r\n");
        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(1, "1234567890123", "^BRN,1,2,1,40")]
    [InlineData(2, "1234567890123", "^BRN,2,2,1,40")]
    [InlineData(5, "0123456789012", "^BRN,5,2,1,40")]
    [InlineData(6, "(01)1234567890123", "^BRN,6,2,1,40,18")]
    public void Zpl_command_writes_each_databar_type(
        int symbology, string data, string header)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Start();
        switch (symbology)
        {
            case 1: command.BarcodeGs1DataBarOmnidirectional(1, 2, data, 40, 2); break;
            case 2: command.BarcodeGs1DataBarTruncated(1, 2, data, 40, 2); break;
            case 5: command.BarcodeGs1DataBarLimited(1, 2, data, 40, 2); break;
            default: command.BarcodeGs1DataBarExpanded(1, 2, data, 40, 2, 18); break;
        }
        command.End();

        Assert.Contains($"^FO1,2{header}^FD{data}^FS\r\n",
            Encoding.ASCII.GetString(command.Build().GetBytes()));
    }

    [Fact]
    public void Zpl_expanded_databar_allows_explicit_segment_width_and_rotation()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeGs1DataBarExpanded(1, 2, "12{", 40, 4, 20, 270)
            .End()
            .Build()
            .GetBytes());

        Assert.Contains("^FO1,2^BRB,6,4,1,40,20^FD12{^FS\r\n", output);
    }

    [Fact]
    public void Zpl_expanded_databar_normalizes_bracketed_ai_sequence()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeGs1DataBarExpanded(1, 2,
                "(01)01234567890128(10)ABC(17)250101", 40, 2, 18)
            .End()
            .Build().GetBytes());

        Assert.Contains("^FO1,2^BRN,6,2,1,40,18^FD010123456789012810ABC\u001d17250101^FS\r\n",
            output);
    }

    [Fact]
    public void Zpl_expanded_databar_normalizes_supported_compressed_ai_sequence()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeGs1DataBarExpanded(1, 2,
                "(01)01234567890128(10)ABC(3103)000123", 40, 2, 18)
            .End()
            .Build().GetBytes());

        Assert.Contains("^FO1,2^BRN,6,2,1,40,18^FD010123456789012810ABC\u001d3103000123^FS\r\n",
            output);
    }

    [Theory]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional, "")]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional, "123456789012")]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional, "123456789012A")]
    [InlineData(BarcodeType.Gs1DataBarLimited, "2123456789012")]
    [InlineData(BarcodeType.Gs1DataBarExpanded, "A1")]
    [InlineData(BarcodeType.Gs1DataBarExpanded, "12\u0001")]
    [InlineData(BarcodeType.Gs1DataBarExpanded, "12^bad")]
    public void Zpl_command_rejects_invalid_databar_data_without_polluting_output(
        BarcodeType type, string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => Add(command, type, data));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 2, 22)]
    [InlineData(40, 0, 22)]
    [InlineData(40, 11, 22)]
    [InlineData(40, 2, 1)]
    [InlineData(40, 2, 23)]
    public void Zpl_command_rejects_invalid_databar_dimensions_without_polluting_output(
        int height, int moduleWidth, int segmentWidth)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.BarcodeGs1DataBarExpanded(
            1, 2, "12", height, moduleWidth, segmentWidth));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_databar_rejects_explicit_hri_in_unified_provider()
    {
        var error = Assert.Throws<PrintCapabilityException>(() => ZplProvider().Validate(
            BarcodeDocument(BarcodeType.Gs1DataBarLimited, "0123456789012", humanReadable: true)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("HRI", error.Message);
    }

    [Fact]
    public void Zpl_provider_renders_databar_with_exact_page_envelope()
    {
        var bytes = ZplProvider().Render(BarcodeDocument(
            BarcodeType.Gs1DataBarLimited, "0123456789012", x: 10, y: 20,
            barHeight: 40, narrowWidth: 2, humanReadable: false, pageWidth: 442))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW442\r\n^LL120\r\n" +
            "^FO10,20^BRN,5,2,1,40^FD0123456789012^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(BarcodeType.Gs1DataBarOmnidirectional, "1234567890123")]
    [InlineData(BarcodeType.Gs1DataBarTruncated, "1234567890123")]
    [InlineData(BarcodeType.Gs1DataBarLimited, "0123456789012")]
    [InlineData(BarcodeType.Gs1DataBarExpanded, "12{")]
    public void Zpl_template_capabilities_and_rendering_include_each_databar_type(
        BarcodeType type, string data)
    {
        var capabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Zpl,
            Encoding = Encoding.ASCII
        });

        Assert.Contains(type, capabilities.BarcodeTypes);
        Assert.Contains(RotationAngle.Rotate90, capabilities.BarcodeRotations);
        Assert.NotEmpty(ZplProvider().Render(BarcodeDocument(type, data, humanReadable: false)));
    }

    [Fact]
    public void Zpl_provider_renders_compressed_ai_databar_after_width_preflight()
    {
        var output = Encoding.ASCII.GetString(ZplProvider().Render(BarcodeDocument(
            BarcodeType.Gs1DataBarExpanded,
            "(01)01234567890128(10)ABC(3103)000123",
            humanReadable: false, pageWidth: 3000))[0]);

        Assert.Contains("^FO0,0^BRN,6,2,1,40,22^FD010123456789012810ABC\u001d3103000123^FS\r\n",
            output);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    public void Non_zpl_providers_reject_databar_before_rendering(PrintProtocol protocol)
    {
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarLimited));

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument(BarcodeType.Gs1DataBarLimited, "0123456789012", humanReadable: false)));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    private static void Add(ZplPrintCommand command, BarcodeType type, string data)
    {
        switch (type)
        {
            case BarcodeType.Gs1DataBarOmnidirectional:
                command.BarcodeGs1DataBarOmnidirectional(1, 2, data); break;
            case BarcodeType.Gs1DataBarTruncated:
                command.BarcodeGs1DataBarTruncated(1, 2, data); break;
            case BarcodeType.Gs1DataBarLimited:
                command.BarcodeGs1DataBarLimited(1, 2, data); break;
            default:
                command.BarcodeGs1DataBarExpanded(1, 2, data); break;
        }
    }

    private static IPrintProtocolProvider ZplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(BarcodeType type, string data,
        int x = 0, int y = 0, int barHeight = 40, int narrowWidth = 2,
        bool? humanReadable = null, int pageWidth = 1200)
    {
        var document = new PrintDocument();
        var page = document.AddPage(pageWidth, 120);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, type, data, barHeight, narrowWidth, 2,
            humanReadable: humanReadable), "Barcode");
        return document;
    }
}
