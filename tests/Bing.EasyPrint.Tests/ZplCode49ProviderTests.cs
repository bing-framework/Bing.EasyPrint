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

/// <summary>验证 ZPL Code 49 的原生命令、统一 Provider 和模板能力边界。</summary>
public sealed class ZplCode49ProviderTests
{
    [Theory]
    [InlineData(0, "N", false, "N")]
    [InlineData(90, "R", false, "N")]
    [InlineData(180, "I", false, "N")]
    [InlineData(270, "B", false, "N")]
    [InlineData(0, "N", true, "B")]
    public void Zpl_command_writes_exact_code49_bytes_for_each_rotation_and_text_flag(
        int rotation, string orientation, bool printText, string interpretation)
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeCode49(10, 20, "ABC123", 60, printText, 3, rotation)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^FO10,20^BY3^B4{orientation},20,{interpretation},A^FDABC123^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData('A')]
    [InlineData('0')]
    [InlineData('1')]
    [InlineData('2')]
    [InlineData('3')]
    [InlineData('4')]
    [InlineData('5')]
    public void Zpl_command_writes_explicit_code49_start_mode(char startMode)
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .BarcodeCode49(1, 2, "ABC", 40, false, 2, startMode, 0)
            .Build()
            .GetBytes());

        Assert.Contains($"^B4N,20,N,{startMode}^FDABC^FS\r\n", output);
    }

    [Theory]
    [InlineData(60, 3, 20)]
    [InlineData(61, 3, 21)]
    [InlineData(1, 3, 1)]
    public void Zpl_command_converts_requested_code49_row_dots_to_multiplier(
        int rowHeightDots, int moduleWidth, int expectedMultiplier)
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .BarcodeCode49(1, 2, "ABC", rowHeightDots, false, moduleWidth)
            .Build()
            .GetBytes());

        Assert.Contains($"^BY{moduleWidth}^B4N,{expectedMultiplier},N,A^FDABC^FS\r\n", output);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789123")]
    [InlineData("A\u0001B")]
    [InlineData("A\rB")]
    [InlineData("A\nB")]
    [InlineData("A\"B")]
    [InlineData("A^B")]
    [InlineData("A~B")]
    [InlineData("中文")]
    public void Zpl_command_rejects_invalid_code49_data_without_polluting_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeCode49(
            1, 2, data, 40, false, 2));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(32001, 2)]
    [InlineData(40, 0)]
    [InlineData(40, 11)]
    public void Zpl_command_rejects_invalid_code49_dimensions_without_polluting_output(
        int height, int moduleWidth)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.BarcodeCode49(
            1, 2, "ABC", height, false, moduleWidth));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_command_rejects_invalid_code49_start_mode_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.BarcodeCode49(
            1, 2, "ABC", 40, false, 2, '6', 0));

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

        Assert.Throws<NotSupportedException>(() => command.BarcodeCode49(
            1, 2, "ABC", 40, false, 2, 90));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_capabilities_expose_code49_on_zpl_and_tspl_only()
    {
        var zpl = ZplProvider().Capabilities;
        var cpcl = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;
        var escPosNative = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var escPosRaster = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities;

        Assert.True(zpl.SupportsBarcodeType(BarcodeType.Code49));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.Code49));
        Assert.False(cpcl.SupportsBarcodeType(BarcodeType.Code49));
        Assert.False(cpcl.SupportsCommandBarcodeType(BarcodeType.Code49));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.Code49));
        Assert.True(tspl.SupportsCommandBarcodeType(BarcodeType.Code49));
        Assert.False(escPosNative.SupportsBarcodeType(BarcodeType.Code49));
        Assert.False(escPosNative.SupportsCommandBarcodeType(BarcodeType.Code49));
        Assert.True(escPosRaster.SupportsBarcodeType(BarcodeType.Code49));
        Assert.False(escPosRaster.SupportsCommandBarcodeType(BarcodeType.Code49));
    }

    [Fact]
    public void Zpl_provider_renders_code49_with_exact_page_envelope()
    {
        const string data = "123-ABC";
        const int x = 10;
        const int y = 20;
        const int rowHeightDots = 40;
        const int moduleWidth = 2;
        var symbolHeight = EscPosRasterCanvas.MeasureBarcodeHeight(BarcodeType.Code49, data,
            rowHeightDots, moduleWidth, System.Threading.CancellationToken.None);
        var pageHeight = y + symbolHeight;
        var pageWidth = x + Code49Encoder.RowWidth * moduleWidth;
        var bytes = ZplProvider().Render(BarcodeDocument(data,
            x: x, y: y, barHeight: rowHeightDots, narrowWidth: moduleWidth, humanReadable: false,
            pageWidth: pageWidth, pageHeight: pageHeight))[0];

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            $"^PW{pageWidth}\r\n^LL{pageHeight}\r\n" +
            $"^FO{x},{y}^BY{moduleWidth}^B4N,20,N,A^FD{data}^FS\r\n" +
            "^PQ1\r\n^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_provider_rejects_code49_vertical_overflow_with_element_path()
    {
        const string data = "123-ABC";
        const int y = 20;
        const int rowHeightDots = 40;
        const int moduleWidth = 2;
        var symbolHeight = EscPosRasterCanvas.MeasureBarcodeHeight(BarcodeType.Code49, data,
            rowHeightDots, moduleWidth, System.Threading.CancellationToken.None);
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument(data, y: y, barHeight: rowHeightDots, narrowWidth: moduleWidth,
                humanReadable: false, pageWidth: 200, pageHeight: y + symbolHeight - 1)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("范围", error.Message);
    }

    [Fact]
    public void Zpl_provider_uses_full_code49_height_for_quarter_turn_page_width()
    {
        const string data = "123-ABC";
        const int rowHeightDots = 40;
        const int moduleWidth = 2;
        var symbolHeight = EscPosRasterCanvas.MeasureCode49SymbolHeight(data, rowHeightDots,
            moduleWidth, System.Threading.CancellationToken.None);

        ZplProvider().Validate(BarcodeDocument(data, barHeight: rowHeightDots,
            narrowWidth: moduleWidth, humanReadable: false, pageWidth: symbolHeight,
            pageHeight: Code49Encoder.RowWidth * moduleWidth, rotation: RotationAngle.Rotate90));

        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument(data, barHeight: rowHeightDots, narrowWidth: moduleWidth,
                humanReadable: false, pageWidth: symbolHeight - 1,
                pageHeight: Code49Encoder.RowWidth * moduleWidth, rotation: RotationAngle.Rotate90)));

        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Zpl_provider_rejects_code49_outside_page_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() => ZplProvider().Validate(
            BarcodeDocument("12345", x: 10, y: 1, barHeight: 40,
                narrowWidth: 2, humanReadable: false, pageWidth: 107)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Fact]
    public void Zpl_profile_can_disable_code49_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code49));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("12345", barHeight: 40)));

        Assert.Equal(PrintProtocol.Zpl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(RotationAngle.None, "N")]
    [InlineData(RotationAngle.Rotate90, "R")]
    [InlineData(RotationAngle.Rotate180, "I")]
    [InlineData(RotationAngle.Rotate270, "B")]
    public void Native_template_renders_code49_using_each_zpl_orientation(
        RotationAngle rotation, string orientation)
    {
        var result = new TemplateRenderer().RenderWithLayout(Code49Template(rotation), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(PrintProtocol.Zpl, result.Protocol);
        Assert.Contains($"^B4{orientation},20,N,A^FD123-ABC^FS\r\n",
            Encoding.ASCII.GetString(page.Data));
    }

    [Fact]
    public void Zpl_template_capabilities_include_code49()
    {
        var capabilities = TemplateProtocolCapabilities.Create(new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Zpl,
            Encoding = Encoding.ASCII
        });

        Assert.Contains(BarcodeType.Code49, capabilities.BarcodeTypes);
        Assert.Contains(RotationAngle.Rotate90, capabilities.BarcodeRotations);
    }

    private static IPrintProtocolProvider ZplProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII);

    private static PrintDocument BarcodeDocument(string data, int x = 0, int y = 0,
        int barHeight = 40, int narrowWidth = 2, bool? humanReadable = null,
        int pageWidth = 120, int pageHeight = 120, RotationAngle rotation = RotationAngle.None)
    {
        var document = new PrintDocument();
        var page = document.AddPage(pageWidth, pageHeight);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintBarcodeElement(x, y, BarcodeType.Code49, data, barHeight, narrowWidth, 2,
            rotation, humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate Code49Template(RotationAngle rotation)
        => new BpTemplate
        {
            Version = 1,
            Width = 2400,
            Height = 2400,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 2400,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 2400,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    BarcodeType = BarcodeType.Code49,
                                    Rotation = rotation,
                                    LineWidth = 1,
                                    Ratio = 1,
                                    NarrowModuleWidth = 2,
                                    Height = 40,
                                    Width = 2000,
                                    HumanReadable = false,
                                    Field = new BgField { Value = "123-ABC" }
                                }
                            }
                        }
                    }
                }
            }
        };
}
