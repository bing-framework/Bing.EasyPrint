using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ESC/POS Native HRI 位置的机型契约、快照和严格失败语义。</summary>
public sealed class EscPosBarcodeTextPositionProfileTests
{
    [Fact]
    public void Null_position_allowlist_preserves_all_native_hri_positions()
    {
        var command = Native(new EscPosPrinterProfile());

        Assert.Null(command.Capabilities.SupportedNativeBarcodeHumanReadablePositions);
        Assert.True(command.Capabilities.SupportsNativeBarcodeHumanReadablePosition(EscPosBarcodeTextPosition.Above));
        Assert.True(command.Capabilities.SupportsNativeBarcodeHumanReadablePosition(EscPosBarcodeTextPosition.Below));
        Assert.True(command.Capabilities.SupportsNativeBarcodeHumanReadablePosition(EscPosBarcodeTextPosition.Both));
        Assert.True(command.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code128));
    }

    [Fact]
    public void Position_allowlist_is_enforced_by_direct_native_barcode_output()
    {
        var command = Native(new EscPosPrinterProfile
        {
            SupportedNativeBarcodeHumanReadablePositions = new[] { EscPosBarcodeTextPosition.Below }
        });

        Assert.Throws<NotSupportedException>(() => command.Barcode("ABC", 73, 80, 2, 1));
        var bytes = command.Barcode("ABC", 73, 80, 2, 2).Build().GetBytes();
        Assert.True(ContainsSequence(bytes, new byte[] { 0x1D, 0x48, 0x02 }));
    }

    [Fact]
    public void Empty_position_allowlist_disables_hri_but_keeps_barcode_and_none_position()
    {
        var command = Native(new EscPosPrinterProfile
        {
            SupportedNativeBarcodeHumanReadablePositions = Array.Empty<EscPosBarcodeTextPosition>()
        });

        Assert.False(command.Capabilities.SupportsNativeBarcodeHumanReadablePosition(EscPosBarcodeTextPosition.Below));
        Assert.False(command.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code128));
        command.Barcode("ABC", 73, 80, 2, 0);
        Assert.Throws<NotSupportedException>(() => command.Barcode("ABC", 73, 80, 2, 3));
    }

    [Fact]
    public void Position_allowlist_is_snapshotted_and_rejects_none_or_duplicates()
    {
        var positions = new[] { EscPosBarcodeTextPosition.Below };
        var command = Native(new EscPosPrinterProfile { SupportedNativeBarcodeHumanReadablePositions = positions });
        positions[0] = EscPosBarcodeTextPosition.Above;

        command.Barcode("ABC", 73, 80, 2, 2);
        Assert.Throws<NotSupportedException>(() => command.Barcode("ABC", 73, 80, 2, 1));
        Assert.Throws<ArgumentException>(() => Native(new EscPosPrinterProfile
        {
            SupportedNativeBarcodeHumanReadablePositions = new[] { EscPosBarcodeTextPosition.None }
        }));
        Assert.Throws<ArgumentException>(() => Native(new EscPosPrinterProfile
        {
            SupportedNativeBarcodeHumanReadablePositions = new[]
            {
                EscPosBarcodeTextPosition.Above, EscPosBarcodeTextPosition.Above
            }
        }));
    }

    private static EscPosPrintCommand Native(EscPosPrinterProfile profile)
        => new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = profile });

    private static bool ContainsSequence(byte[] source, byte[] expected)
    {
        return Enumerable.Range(0, source.Length - expected.Length + 1)
            .Any(index => source.Skip(index).Take(expected.Length).SequenceEqual(expected));
    }
}
