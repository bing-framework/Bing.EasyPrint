using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 GS1 设备 payload 上限的配置、快照和预检。</summary>
public sealed class Gs1BarcodeCapacityTests
{
    [Fact]
    public void Escpos_native_capacity_rejects_before_appending_barcode_bytes()
    {
        var profile = new EscPosPrinterProfile { MaxGs1LinearPayloadCharacters = 6 };
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = profile }).Text("before");
        var before = command.Build().GetBytes();
        profile.MaxGs1LinearPayloadCharacters = 32;

        var error = Assert.Throws<NotSupportedException>(() =>
            command.BarcodeUccEan128("(91)ABCDE", 40, 2));

        Assert.Contains("7", error.Message);
        Assert.Equal(6, command.Capabilities.MaxGs1LinearPayloadCharacters);
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Escpos_native_capacity_counts_fnc1_in_normalized_payload()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions
            {
                Profile = new EscPosPrinterProfile { MaxGs1LinearPayloadCharacters = 12 }
            });

        var error = Assert.Throws<NotSupportedException>(() =>
            command.BarcodeUccEan128("(91)AB(17)250101", 40, 2));

        Assert.Contains("13", error.Message);
        Assert.Empty(command.Build().GetBytes());
    }

    [Fact]
    public void Escpos_raster_capacity_uses_normalized_ai_payload_and_preserves_canvas_after_rejection()
    {
        var options = new EscPosDeviceOptions
        {
            Profile = new EscPosPrinterProfile { MaxGs1DataBarExpandedPayloadCharacters = 4 }
        };
        var rejectedThenValid = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster, options)
            .BeginRasterPage(256, 100);
        Assert.Throws<NotSupportedException>(() =>
            rejectedThenValid.RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0, "(10)ABC", 40, 2));
        var actual = rejectedThenValid
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0, "(10)AB", 40, 2)
            .EndRasterPage()
            .Build().GetBytes();

        var expected = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster, options)
            .BeginRasterPage(256, 100)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0, "(10)AB", 40, 2)
            .EndRasterPage()
            .Build().GetBytes();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Tspl_provider_reports_device_capacity_with_element_path_during_preflight()
    {
        var profile = new TsplPrinterProfile { MaxGs1LinearPayloadCharacters = 5 };
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: new TsplMediaOptions { Profile = profile });
        var document = BarcodeDocument(BarcodeType.UccEan128, "91ABCD");

        Assert.Equal(5, provider.Capabilities.MaxGs1LinearPayloadCharacters);
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));
        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("上限为 5", error.Message);
    }

    [Fact]
    public void Zpl_expanded_capacity_is_enforced_after_structured_input_normalization()
    {
        var command = new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { MaxGs1DataBarExpandedPayloadCharacters = 4 }
            }).Start();

        var error = Assert.Throws<NotSupportedException>(() =>
            command.BarcodeGs1DataBarExpanded(0, 0, "(10)ABC", 40, 2, 22));

        Assert.Contains("5", error.Message);
        Assert.Equal(4, command.Capabilities.MaxGs1DataBarExpandedPayloadCharacters);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Profiles_reject_non_positive_gs1_capacity_limits(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
                new EscPosDeviceOptions
                {
                    Profile = new EscPosPrinterProfile { MaxGs1LinearPayloadCharacters = limit }
                }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions
                {
                    Profile = new TsplPrinterProfile { MaxGs1LinearPayloadCharacters = limit }
                }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions
                {
                    Profile = new ZplPrinterProfile { MaxGs1DataBarExpandedPayloadCharacters = limit }
                }));
    }

    private static PrintDocument BarcodeDocument(BarcodeType type, string data)
    {
        var document = new PrintDocument();
        document.AddPage(1000, 200).Add(new PrintBarcodeElement(0, 0, type, data, 40), "Barcode");
        return document;
    }
}
