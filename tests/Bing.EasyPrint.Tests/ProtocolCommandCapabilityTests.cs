using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证统一光栅能力与独立协议原生命令能力的边界。</summary>
public sealed class ProtocolCommandCapabilityTests
{
    [Fact]
    public void EscPos_raster_keeps_unified_barcode_and_qr_capabilities_when_native_commands_are_disabled()
    {
        var profile = new EscPosPrinterProfile
        {
            SupportsNativeBarcode = false,
            SupportsNativeQrCode = false
        };
        var options = new EscPosDeviceOptions { Profile = profile };
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster, options);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, options);

        Assert.True(command.Capabilities.SupportsBarcode);
        Assert.True(command.Capabilities.SupportsQrCode);
        Assert.False(command.Capabilities.SupportsCommandBarcode);
        Assert.False(command.Capabilities.SupportsCommandQrCode);
        Assert.True(command.Capabilities.SupportsBarcodeType(BarcodeType.Code128));
        Assert.False(command.Capabilities.SupportsCommandBarcodeType(BarcodeType.Code128));

        Assert.True(provider.Capabilities.SupportsBarcode);
        Assert.True(provider.Capabilities.SupportsQrCode);
        Assert.False(provider.Capabilities.SupportsCommandBarcode);
        Assert.False(provider.Capabilities.SupportsCommandQrCode);
        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Code128));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Code128));

        var document = new PrintDocument();
        document.AddPage(384, 200)
            .Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128, "123456", 40))
            .Add(new PrintQrCodeElement(160, 0, "payload", 4, QrCodeCorrectionLevel.M));
        Assert.NotEmpty(provider.Render(document)[0]);

        Assert.Throws<NotSupportedException>(() => command.Barcode("123456"));
        Assert.Throws<NotSupportedException>(() => command.QrCode("payload"));
    }

    [Fact]
    public void EscPos_command_capabilities_snapshot_profile_native_flags()
    {
        var profile = new EscPosPrinterProfile
        {
            SupportsNativeBarcode = false,
            SupportsNativeQrCode = false
        };
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { Profile = profile });

        profile.SupportsNativeBarcode = true;
        profile.SupportsNativeQrCode = true;

        Assert.False(command.Capabilities.SupportsCommandBarcode);
        Assert.False(command.Capabilities.SupportsCommandQrCode);
        Assert.Throws<NotSupportedException>(() => command.Barcode("123456"));
        Assert.Throws<NotSupportedException>(() => command.QrCode("payload"));
    }

    [Fact]
    public void Default_escpos_command_enables_native_capabilities()
    {
        var capabilities = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Capabilities;

        Assert.True(capabilities.SupportsBarcode);
        Assert.True(capabilities.SupportsQrCode);
        Assert.True(capabilities.SupportsCommandBarcode);
        Assert.True(capabilities.SupportsCommandQrCode);
        Assert.True(capabilities.SupportsCommandBarcodeType(BarcodeType.Code128));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Non_escpos_protocols_retain_command_capability_semantics(PrintProtocol protocol)
    {
        var capabilities = ProtocolCommandFactory.Create(protocol, Encoding.ASCII).Capabilities;

        Assert.Equal(capabilities.SupportsBarcode, capabilities.SupportsCommandBarcode);
        Assert.Equal(capabilities.SupportsQrCode, capabilities.SupportsCommandQrCode);
        Assert.Equal(capabilities.SupportsBarcodeType(BarcodeType.Code128),
            capabilities.SupportsCommandBarcodeType(BarcodeType.Code128));
    }
}
