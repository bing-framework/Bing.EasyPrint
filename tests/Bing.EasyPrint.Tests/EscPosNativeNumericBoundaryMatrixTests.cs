using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 锁定 ESC/POS Native 原生条码的数值边界，避免能力声明与 GS k 实际输出脱节。
/// </summary>
public sealed class EscPosNativeNumericBoundaryMatrixTests
{
    private static readonly NativeBarcodeCase[] Cases =
    {
        new NativeBarcodeCase(BarcodeType.UpcA, 65, "12345678901"),
        new NativeBarcodeCase(BarcodeType.UpcE, 66, "425261"),
        new NativeBarcodeCase(BarcodeType.Ean13, 67, "4006381333931"),
        new NativeBarcodeCase(BarcodeType.Ean8, 68, "96385074"),
        new NativeBarcodeCase(BarcodeType.Code39, 69, "ABC-12"),
        new NativeBarcodeCase(BarcodeType.I2OF5, 70, "1234"),
        new NativeBarcodeCase(BarcodeType.I2OF5C, 70, "123456"),
        new NativeBarcodeCase(BarcodeType.I2OF5G, 70, "123456789012"),
        new NativeBarcodeCase(BarcodeType.Codabar, 71, "A12B"),
        new NativeBarcodeCase(BarcodeType.Code93, 72, "CODE93"),
        new NativeBarcodeCase(BarcodeType.Code128, 73, "1234"),
        new NativeBarcodeCase(BarcodeType.UccEan128, 74, "0101234567890128"),
        new NativeBarcodeCase(BarcodeType.Gs1DataBarOmnidirectional, 75, "1234567890123"),
        new NativeBarcodeCase(BarcodeType.Gs1DataBarTruncated, 76, "1234567890123"),
        new NativeBarcodeCase(BarcodeType.Gs1DataBarLimited, 77, "0123456789012"),
        new NativeBarcodeCase(BarcodeType.Gs1DataBarExpanded, 78, "(01)\u001D12345678901231"),
        new NativeBarcodeCase(BarcodeType.Code128Auto, 79, "ABC123")
    };

    [Fact]
    public void Every_native_function_b_family_emits_all_numeric_endpoint_combinations()
    {
        var combinations = new[]
        {
            (Height: (byte)1, Width: (byte)2, TextPosition: (byte)0),
            (Height: (byte)1, Width: (byte)6, TextPosition: (byte)1),
            (Height: (byte)255, Width: (byte)2, TextPosition: (byte)2),
            (Height: (byte)255, Width: (byte)6, TextPosition: (byte)3)
        };

        foreach (var item in Cases)
        foreach (var combination in combinations)
        {
            var bytes = new EscPosPrintCommand(Encoding.ASCII)
                .Barcode(item.Data, item.Symbology, combination.Height,
                    combination.Width, combination.TextPosition)
                .Build().GetBytes();

            Assert.True(bytes.Length >= 13, $"Function B {item.Symbology} 没有产生完整命令。");
            Assert.Equal(new byte[]
            {
                0x1D, 0x48, combination.TextPosition,
                0x1D, 0x68, combination.Height,
                0x1D, 0x77, combination.Width,
                0x1D, 0x6B, item.Symbology
            }, bytes.Take(12).ToArray());
            Assert.Equal(bytes.Length - 13, bytes[12]);
            Assert.NotEmpty(bytes.Skip(13).ToArray());

            var repeated = new EscPosPrintCommand(Encoding.ASCII)
                .Barcode(item.Data, item.Symbology, combination.Height,
                    combination.Width, combination.TextPosition)
                .Build().GetBytes();
            Assert.Equal(bytes, repeated);
        }
    }

    [Fact]
    public void Native_capabilities_expose_the_same_numeric_boundaries_as_the_command()
    {
        var capabilities = new EscPosPrintCommand(Encoding.ASCII).Capabilities;

        Assert.Equal(2, capabilities.CommandBarcodeModuleWidth.Minimum);
        Assert.Equal(6, capabilities.CommandBarcodeModuleWidth.Maximum);
        Assert.Equal(1, capabilities.CommandBarcodeHeight.Minimum);
        Assert.Equal(255, capabilities.CommandBarcodeHeight.Maximum);
        Assert.All(Cases, item => Assert.True(capabilities.SupportsBarcodeType(item.Type)));
    }

    [Fact]
    public void Invalid_numeric_boundaries_are_rejected_without_buffer_pollution_for_every_family()
    {
        foreach (var item in Cases)
        {
            AssertRejected(item, height: 0, width: 2, textPosition: 0);
            AssertRejected(item, height: 1, width: 1, textPosition: 0);
            AssertRejected(item, height: 1, width: 7, textPosition: 0);
            AssertRejected(item, height: 1, width: 2, textPosition: 4);
        }
    }

    private static void AssertRejected(NativeBarcodeCase item, byte height, byte width,
        byte textPosition)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.Barcode(
            item.Data, item.Symbology, height, width, textPosition));

        Assert.Equal(before, command.Build().GetBytes());
    }

    private sealed class NativeBarcodeCase
    {
        internal NativeBarcodeCase(BarcodeType type, byte symbology, string data)
        {
            Type = type;
            Symbology = symbology;
            Data = data;
        }

        internal BarcodeType Type { get; }
        internal byte Symbology { get; }
        internal string Data { get; }
    }
}
