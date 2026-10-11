using System;
using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 TSPL/ZPL 机型对原生条码可读文字（HRI）的显式能力契约。</summary>
public sealed class LabelBarcodeHumanReadableProfileTests
{
    [Fact]
    public void Tspl_profile_can_disable_default_hri_for_command_and_provider()
    {
        var media = new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile
            {
                Name = "TsplWithoutHri",
                SupportsNativeBarcodeHumanReadable = false
            }
        };
        var command = new TsplPrintCommand(Encoding.ASCII, media);

        Assert.False(command.Capabilities.SupportsNativeBarcodeHumanReadable);
        Assert.False(command.Capabilities.SupportsBarcodeHumanReadable);
        Assert.Throws<NotSupportedException>(() => command.Barcode(0, 0, "128", 40, 1, 0, 2, 2, "1234"));

        var noHri = command.Barcode(0, 0, "128", 40, 0, 0, 2, 2, "1234").Build().GetBytes();
        Assert.Contains("BARCODE 0,0,\"128\",40,0,0,2,2,\"1234\"", Encoding.ASCII.GetString(noHri));

        var document = BarcodeDocument(null);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: media);
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(document));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ElementPath);
    }

    [Fact]
    public void Zpl_profile_can_disable_default_hri_for_command_and_provider()
    {
        var media = new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile
            {
                Name = "ZplWithoutHri",
                SupportsNativeBarcodeHumanReadable = false
            }
        };
        var command = new ZplPrintCommand(Encoding.ASCII, media);

        Assert.False(command.Capabilities.SupportsNativeBarcodeHumanReadable);
        Assert.False(command.Capabilities.SupportsBarcodeHumanReadable);
        Assert.Throws<NotSupportedException>(() => command.Barcode(0, 0, "1234", 40, true, 2));

        var noHri = command.Barcode(0, 0, "1234", 40, false, 2).Build().GetBytes();
        Assert.Contains("^BCN,40,N,N,N", Encoding.ASCII.GetString(noHri));

        var document = BarcodeDocument(null);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: media);
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(document));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ElementPath);
    }

    [Fact]
    public void Explicit_hri_false_remains_valid_when_profile_disables_hri()
    {
        var template = new BpTemplate
        {
            Width = 1000,
            Height = 80,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = 80,
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Width = 1000,
                            Elements = new List<BpElement>
                            {
                                new BpBarcodeElement
                                {
                                    Height = 40,
                                    Width = 1000,
                                    HumanReadable = false,
                                    Field = new BgField { Value = "1234" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.Tspl,
            Encoding = Encoding.ASCII,
            TsplMedia = new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { SupportsNativeBarcodeHumanReadable = false }
            },
            FeedLines = 0,
            CutAfterPrint = false
        });

        Assert.NotEmpty(result.Pages);
    }

    [Fact]
    public void Tspl_profile_can_restrict_hri_by_barcode_type()
    {
        var allowed = new[] { BarcodeType.Code128 };
        var media = new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile
            {
                Name = "TsplCode128HriOnly",
                SupportedNativeBarcodeHumanReadableTypes = allowed
            }
        };
        var command = new TsplPrintCommand(Encoding.ASCII, media);
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: media);

        allowed[0] = BarcodeType.Code39;
        Assert.Contains(BarcodeType.Code128, command.Capabilities.SupportedNativeBarcodeHumanReadableTypes);
        Assert.True(command.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code128));
        Assert.False(command.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code39));
        command.Barcode(0, 0, "128", 40, 1, 0, 2, 2, "1234");
        Assert.Throws<NotSupportedException>(() => command.Barcode(0, 0, "39", 40, 1, 0, 2, 2, "ABC"));
        command.Barcode(0, 0, "39", 40, 0, 0, 2, 2, "ABC");

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(BarcodeDocument(
            BarcodeType.Code39, null, "ABC")));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ElementPath);
    }

    [Fact]
    public void Zpl_profile_can_restrict_hri_by_barcode_type()
    {
        var media = new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile
            {
                Name = "ZplCode128HriOnly",
                SupportedNativeBarcodeHumanReadableTypes = new[] { BarcodeType.Code128 }
            }
        };
        var command = new ZplPrintCommand(Encoding.ASCII, media);

        Assert.True(command.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code128));
        Assert.False(command.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code39));
        command.Barcode(0, 0, "1234", 40, true, 2);
        Assert.Throws<NotSupportedException>(() => command.Barcode39(0, 0, "ABC", 40, true, 2));
        command.Barcode39(0, 0, "ABC", 40, false, 2);

        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII,
            zplOptions: media);
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(BarcodeDocument(
            BarcodeType.Code39, null, "ABC")));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ElementPath);
    }

    [Fact]
    public void EscPos_native_profile_restricts_hri_but_raster_hri_is_host_rendered()
    {
        var profile = new EscPosPrinterProfile
        {
            Name = "EscPosCode128HriOnly",
            SupportedNativeBarcodeHumanReadableTypes = new[] { BarcodeType.Code128 }
        };
        var native = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
            new EscPosDeviceOptions { Profile = profile });

        Assert.True(native.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code128));
        Assert.False(native.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code39));
        native.Barcode("1234", 73, 40, 2, 1);
        Assert.Throws<NotSupportedException>(() => native.Barcode("ABC", 69, 40, 2, 1));
        native.Barcode("ABC", 69, 40, 2, 0);

        var raster = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { Profile = profile });
        Assert.True(raster.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code39));
    }

    [Fact]
    public void Empty_hri_type_list_disables_all_native_hri_without_disabling_barcodes()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
        {
            Profile = new TsplPrinterProfile { SupportedNativeBarcodeHumanReadableTypes = Array.Empty<BarcodeType>() }
        });

        Assert.True(command.Capabilities.SupportsCommandBarcodeType(BarcodeType.Code128));
        Assert.False(command.Capabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code128));
        Assert.Throws<NotSupportedException>(() => command.Barcode(0, 0, "128", 40, 1, 0, 2, 2, "1234"));
        command.Barcode(0, 0, "128", 40, 0, 0, 2, 2, "1234");
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 0)]
    [InlineData(PrintProtocol.Tspl, -1)]
    [InlineData(PrintProtocol.Zpl, 0)]
    [InlineData(PrintProtocol.Zpl, -1)]
    public void Native_hri_profile_reservation_must_be_positive(PrintProtocol protocol, int heightDots)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (protocol == PrintProtocol.Tspl)
                _ = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
                {
                    Profile = new TsplPrinterProfile {
                        NativeBarcodeHumanReadableReserveHeightDots = heightDots
                    }
                });
            else
                _ = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
                {
                    Profile = new ZplPrinterProfile {
                        NativeBarcodeHumanReadableReserveHeightDots = heightDots
                    }
                });
        });
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 0)]
    [InlineData(PrintProtocol.Tspl, -1)]
    [InlineData(PrintProtocol.Zpl, 0)]
    [InlineData(PrintProtocol.Zpl, -1)]
    public void Native_hri_width_profile_reservation_must_be_positive(PrintProtocol protocol, int widthDots)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (protocol == PrintProtocol.Tspl)
                _ = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
                {
                    Profile = new TsplPrinterProfile {
                        NativeBarcodeHumanReadableReserveWidthDots = widthDots
                    }
                });
            else
                _ = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
                {
                    Profile = new ZplPrinterProfile {
                        NativeBarcodeHumanReadableReserveWidthDots = widthDots
                    }
                });
        });
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, -1)]
    [InlineData(PrintProtocol.Zpl, -1)]
    public void Native_hri_left_overhang_profile_must_not_be_negative(PrintProtocol protocol, int overhangDots)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (protocol == PrintProtocol.Tspl)
                _ = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
                {
                    Profile = new TsplPrinterProfile
                    {
                        NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
                    }
                });
            else
                _ = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
                {
                    Profile = new ZplPrinterProfile
                    {
                        NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
                    }
                });
        });
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_hri_page_overhang_profile_values_must_not_be_negative(PrintProtocol protocol)
    {
        var invalidValues = new (int? Top, int? Right, int? Bottom)[]
        {
            (Top: -1, Right: null, Bottom: null),
            (Top: null, Right: -1, Bottom: null),
            (Top: null, Right: null, Bottom: -1)
        };

        foreach (var invalid in invalidValues)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                if (protocol == PrintProtocol.Tspl)
                    _ = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
                    {
                        Profile = new TsplPrinterProfile
                        {
                            NativeBarcodeHumanReadableTopOverhangDots = invalid.Top,
                            NativeBarcodeHumanReadableRightOverhangDots = invalid.Right,
                            NativeBarcodeHumanReadableBottomOverhangDots = invalid.Bottom
                        }
                    });
                else
                    _ = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
                    {
                        Profile = new ZplPrinterProfile
                        {
                            NativeBarcodeHumanReadableTopOverhangDots = invalid.Top,
                            NativeBarcodeHumanReadableRightOverhangDots = invalid.Right,
                            NativeBarcodeHumanReadableBottomOverhangDots = invalid.Bottom
                        }
                    });
            });
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_hri_page_bounds_profile_controls_all_sides_and_rotated_direct_bounds(PrintProtocol protocol)
    {
        const int left = 12;
        const int top = 8;
        const int right = 16;
        const int bottom = 6;
        const int barHeight = 40;
        const int hriHeight = 8;
        var symbolWidth = (int)EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128, "1234", 2);
        var reserveWidth = symbolWidth + 4;
        var width = left + reserveWidth + right;
        var height = top + barHeight + hriHeight + bottom;

        Assert.NotEmpty(BuildDirectBarcode(protocol, width, height, left, top, true, 0,
            left, top, right, bottom, reserveWidth, hriHeight));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildDirectBarcode(protocol, width, height,
            left - 1, top, true, 0, left, top, right, bottom, reserveWidth, hriHeight));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildDirectBarcode(protocol, width, height,
            left, top - 1, true, 0, left, top, right, bottom, reserveWidth, hriHeight));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildDirectBarcode(protocol, width - 1, height,
            left, top, true, 0, left, top, right, bottom, reserveWidth, hriHeight));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildDirectBarcode(protocol, width, height - 1,
            left, top, true, 0, left, top, right, bottom, reserveWidth, hriHeight));

        Assert.NotEmpty(BuildDirectBarcode(protocol, symbolWidth, barHeight, 0, 0, false, 0,
            left, top, right, bottom, reserveWidth, hriHeight));

        var rotatedWidth = left + barHeight + hriHeight + right;
        var rotatedHeight = top + reserveWidth + bottom;
        Assert.NotEmpty(BuildDirectBarcode(protocol, rotatedWidth, rotatedHeight, left, top, true, 90,
            left, top, right, bottom, reserveWidth, hriHeight));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildDirectBarcode(protocol,
            rotatedWidth, rotatedHeight - 1, left, top, true, 90,
            left, top, right, bottom, reserveWidth, hriHeight));
    }

    [Fact]
    public void Native_hri_rotation_specific_overhang_rejects_negative_edges_and_allows_zero()
    {
        var zero = new NativeBarcodeHumanReadableOverhang(leftDots: 0, topDots: 0,
            rightDots: 0, bottomDots: 0);

        Assert.Equal(0, zero.LeftDots);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NativeBarcodeHumanReadableOverhang(leftDots: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NativeBarcodeHumanReadableOverhang(topDots: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NativeBarcodeHumanReadableOverhang(rightDots: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NativeBarcodeHumanReadableOverhang(bottomDots: -1));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_hri_rotation_specific_overhang_controls_direct_bounds(PrintProtocol protocol)
    {
        const int barHeight = 40;
        const int hriHeight = 8;
        const int commonOverhang = 20;
        var symbolWidth = (int)EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128, "1234", 2);
        var overhang90 = new NativeBarcodeHumanReadableOverhang(1, 2, 3, 4);
        var overhang180 = new NativeBarcodeHumanReadableOverhang(5, 6, 7, 8);
        var overhang270 = new NativeBarcodeHumanReadableOverhang(9, 10, 11, 12);

        var configured90 = BuildDirectBarcode(protocol, 1 + barHeight + hriHeight + 3,
            2 + symbolWidth + 4, 1, 2, true, 90, commonOverhang, commonOverhang,
            commonOverhang, commonOverhang, symbolWidth, hriHeight,
            rotation90Overhang: overhang90);
        var withoutOverhang = BuildDirectBarcode(protocol, 1 + barHeight + hriHeight + 3,
            2 + symbolWidth + 4, 1, 2, true, 90, 0, 0, 0, 0, symbolWidth, hriHeight);
        Assert.NotEmpty(configured90);
        Assert.Equal(withoutOverhang, configured90);
        Assert.NotEmpty(BuildDirectBarcode(protocol, 5 + symbolWidth + 7,
            6 + barHeight + hriHeight + 8, 5, 6, true, 180, commonOverhang, commonOverhang,
            commonOverhang, commonOverhang, symbolWidth, hriHeight,
            rotation180Overhang: overhang180));
        Assert.NotEmpty(BuildDirectBarcode(protocol, 9 + barHeight + hriHeight + 11,
            10 + symbolWidth + 12, 9, 10, true, 270, commonOverhang, commonOverhang,
            commonOverhang, commonOverhang, symbolWidth, hriHeight,
            rotation270Overhang: overhang270));

        Assert.Throws<ArgumentOutOfRangeException>(() => BuildDirectBarcode(protocol,
            1 + barHeight + hriHeight + 3, 2 + symbolWidth + 4, 0, 2, true, 90,
            commonOverhang, commonOverhang, commonOverhang, commonOverhang,
            symbolWidth, hriHeight, rotation90Overhang: overhang90));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildDirectBarcode(protocol,
            1 + barHeight + hriHeight + 2, 2 + symbolWidth + 4, 1, 2, true, 90,
            commonOverhang, commonOverhang, commonOverhang, commonOverhang,
            symbolWidth, hriHeight, rotation90Overhang: overhang90));

        var partialOverride = new NativeBarcodeHumanReadableOverhang(leftDots: 1);
        Assert.NotEmpty(BuildDirectBarcode(protocol, 1 + barHeight + hriHeight + 3,
            2 + symbolWidth + 4, 1, 2, true, 90, commonOverhang, 2, 3, 4,
            symbolWidth, hriHeight, rotation90Overhang: partialOverride));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 90)]
    [InlineData(PrintProtocol.Tspl, 180)]
    [InlineData(PrintProtocol.Tspl, 270)]
    [InlineData(PrintProtocol.Zpl, 90)]
    [InlineData(PrintProtocol.Zpl, 180)]
    [InlineData(PrintProtocol.Zpl, 270)]
    public void Native_hri_rotation_specific_overhang_is_snapshotted_and_used_by_template_provider(
        PrintProtocol protocol, int rotation)
    {
        const int barHeight = 40;
        const int hriHeight = 8;
        const int commonOverhang = 20;
        var symbolWidth = (int)EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128, "1234", 2);
        var overhang90 = new NativeBarcodeHumanReadableOverhang(1, 2, 3, 4);
        var overhang180 = new NativeBarcodeHumanReadableOverhang(5, 6, 7, 8);
        var overhang270 = new NativeBarcodeHumanReadableOverhang(9, 10, 11, 12);
        var selectedOverhang = rotation == 90 ? overhang90 :
            rotation == 180 ? overhang180 : overhang270;
        PrintCapabilities capabilities;
        TsplPrintCommand tsplCommand = null;
        ZplPrintCommand zplCommand = null;
        TsplPrinterProfile tsplProfile = null;
        ZplPrinterProfile zplProfile = null;
        if (protocol == PrintProtocol.Tspl)
        {
            tsplProfile = new TsplPrinterProfile
            {
                NativeBarcodeHumanReadableReserveHeightDots = hriHeight,
                NativeBarcodeHumanReadableLeftOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableTopOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableRightOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableBottomOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableRotation90Overhang = overhang90,
                NativeBarcodeHumanReadableRotation180Overhang = overhang180,
                NativeBarcodeHumanReadableRotation270Overhang = overhang270
            };
            tsplCommand = new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions { Profile = tsplProfile });
            capabilities = tsplCommand.Capabilities;
        }
        else
        {
            zplProfile = new ZplPrinterProfile
            {
                NativeBarcodeHumanReadableReserveHeightDots = hriHeight,
                NativeBarcodeHumanReadableLeftOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableTopOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableRightOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableBottomOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableRotation90Overhang = overhang90,
                NativeBarcodeHumanReadableRotation180Overhang = overhang180,
                NativeBarcodeHumanReadableRotation270Overhang = overhang270
            };
            zplCommand = new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { Profile = zplProfile });
            capabilities = zplCommand.Capabilities;
        }

        if (tsplProfile != null)
        {
            tsplProfile.NativeBarcodeHumanReadableRotation90Overhang = new NativeBarcodeHumanReadableOverhang(
                commonOverhang, commonOverhang, commonOverhang, commonOverhang);
            tsplProfile.NativeBarcodeHumanReadableRotation180Overhang = tsplProfile.NativeBarcodeHumanReadableRotation90Overhang;
            tsplProfile.NativeBarcodeHumanReadableRotation270Overhang = tsplProfile.NativeBarcodeHumanReadableRotation90Overhang;
        }
        if (zplProfile != null)
        {
            zplProfile.NativeBarcodeHumanReadableRotation90Overhang = new NativeBarcodeHumanReadableOverhang(
                commonOverhang, commonOverhang, commonOverhang, commonOverhang);
            zplProfile.NativeBarcodeHumanReadableRotation180Overhang = zplProfile.NativeBarcodeHumanReadableRotation90Overhang;
            zplProfile.NativeBarcodeHumanReadableRotation270Overhang = zplProfile.NativeBarcodeHumanReadableRotation90Overhang;
        }
        var capabilityOverhang = rotation == 90
            ? capabilities.NativeBarcodeHumanReadableRotation90Overhang
            : rotation == 180
                ? capabilities.NativeBarcodeHumanReadableRotation180Overhang
                : capabilities.NativeBarcodeHumanReadableRotation270Overhang;
        Assert.Equal(selectedOverhang, capabilityOverhang.Value);

        var quarterTurn = rotation == 90 || rotation == 270;
        var selectedLeft = selectedOverhang.LeftDots.Value;
        var selectedTop = selectedOverhang.TopDots.Value;
        var selectedRight = selectedOverhang.RightDots.Value;
        var selectedBottom = selectedOverhang.BottomDots.Value;
        var snapshotPageWidth = selectedLeft + (quarterTurn ? barHeight + hriHeight : symbolWidth) + selectedRight;
        var snapshotPageHeight = selectedTop + (quarterTurn ? symbolWidth : barHeight + hriHeight) + selectedBottom;
        if (tsplCommand != null)
        {
            tsplCommand.Size(snapshotPageWidth, snapshotPageHeight, "dot")
                .Barcode(selectedLeft, selectedTop, "128", barHeight, 1, rotation, 2, 2, "1234");
            Assert.NotEmpty(tsplCommand.Build().GetBytes());
        }
        else
        {
            zplCommand.Label(snapshotPageWidth, snapshotPageHeight)
                .Barcode(selectedLeft, selectedTop, "1234", barHeight, true, 2, rotation);
            Assert.NotEmpty(zplCommand.Build().GetBytes());
        }

        TsplMediaOptions tsplOptions = null;
        ZplMediaOptions zplOptions = null;
        if (protocol == PrintProtocol.Tspl)
        {
            tsplOptions = new TsplMediaOptions { Profile = new TsplPrinterProfile
            {
                NativeBarcodeHumanReadableLeftOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableTopOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableRightOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableBottomOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableRotation90Overhang = overhang90,
                NativeBarcodeHumanReadableRotation180Overhang = overhang180,
                NativeBarcodeHumanReadableRotation270Overhang = overhang270
            }};
        }
        else
        {
            zplOptions = new ZplMediaOptions { Profile = new ZplPrinterProfile
            {
                NativeBarcodeHumanReadableLeftOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableTopOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableRightOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableBottomOverhangDots = commonOverhang,
                NativeBarcodeHumanReadableRotation90Overhang = overhang90,
                NativeBarcodeHumanReadableRotation180Overhang = overhang180,
                NativeBarcodeHumanReadableRotation270Overhang = overhang270
            }};
        }
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            tsplOptions: tsplOptions, zplOptions: zplOptions);
        var pageWidth = selectedLeft + (quarterTurn ? barHeight + hriHeight : symbolWidth) + selectedRight;
        var pageHeight = selectedTop + (quarterTurn ? symbolWidth : barHeight + hriHeight) + selectedBottom;
        var document = BarcodeDocument(BarcodeType.Code128, true, "1234", pageWidth: pageWidth,
            pageHeight: pageHeight, reservedWidth: 1, reserveHeight: hriHeight,
            x: selectedLeft, y: selectedTop, rotation: (RotationAngle)rotation);
        provider.Validate(document);

        var overflow = BarcodeDocument(BarcodeType.Code128, true, "1234", pageWidth: pageWidth - 1,
            pageHeight: pageHeight, reservedWidth: 1, reserveHeight: hriHeight,
            x: selectedLeft, y: selectedTop, rotation: (RotationAngle)rotation);
        Assert.Throws<PrintElementValidationException>(() => provider.Validate(overflow));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_hri_page_overhang_profile_values_are_snapshotted(PrintProtocol protocol)
    {
        const int left = 12;
        const int top = 8;
        const int right = 16;
        const int bottom = 6;
        PrintCapabilities capabilities;
        if (protocol == PrintProtocol.Tspl)
        {
            var profile = new TsplPrinterProfile
            {
                NativeBarcodeHumanReadableLeftOverhangDots = left,
                NativeBarcodeHumanReadableTopOverhangDots = top,
                NativeBarcodeHumanReadableRightOverhangDots = right,
                NativeBarcodeHumanReadableBottomOverhangDots = bottom
            };
            capabilities = new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions { Profile = profile }).Capabilities;
            profile.NativeBarcodeHumanReadableLeftOverhangDots = 0;
            profile.NativeBarcodeHumanReadableTopOverhangDots = 0;
            profile.NativeBarcodeHumanReadableRightOverhangDots = 0;
            profile.NativeBarcodeHumanReadableBottomOverhangDots = 0;
        }
        else
        {
            var profile = new ZplPrinterProfile
            {
                NativeBarcodeHumanReadableLeftOverhangDots = left,
                NativeBarcodeHumanReadableTopOverhangDots = top,
                NativeBarcodeHumanReadableRightOverhangDots = right,
                NativeBarcodeHumanReadableBottomOverhangDots = bottom
            };
            capabilities = new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { Profile = profile }).Capabilities;
            profile.NativeBarcodeHumanReadableLeftOverhangDots = 0;
            profile.NativeBarcodeHumanReadableTopOverhangDots = 0;
            profile.NativeBarcodeHumanReadableRightOverhangDots = 0;
            profile.NativeBarcodeHumanReadableBottomOverhangDots = 0;
        }

        Assert.Equal(left, capabilities.NativeBarcodeHumanReadableLeftOverhangDots);
        Assert.Equal(top, capabilities.NativeBarcodeHumanReadableTopOverhangDots);
        Assert.Equal(right, capabilities.NativeBarcodeHumanReadableRightOverhangDots);
        Assert.Equal(bottom, capabilities.NativeBarcodeHumanReadableBottomOverhangDots);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_hri_left_overhang_profile_controls_direct_bounds_and_is_snapshotted(PrintProtocol protocol)
    {
        const int overhangDots = 12;
        var symbolWidth = (int)EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128, "1234", 2);
        if (protocol == PrintProtocol.Tspl)
        {
            var profile = new TsplPrinterProfile
            {
                NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
            };
            var command = new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions { Profile = profile }).Size(symbolWidth + overhangDots, 100, "dot");
            profile.NativeBarcodeHumanReadableLeftOverhangDots = 1;
            Assert.Equal(overhangDots, command.Capabilities.NativeBarcodeHumanReadableLeftOverhangDots);
            command.Barcode(overhangDots, 0, "128", 40, 1, 0, 2, 2, "1234");
            var before = command.Build().GetBytes();

            var overflow = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile
                {
                    NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
                }
            }).Size(symbolWidth + overhangDots, 100, "dot");
            var overflowBefore = overflow.Build().GetBytes();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                overflow.Barcode(overhangDots - 1, 0, "128", 40, 1, 0, 2, 2, "1234"));

            new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile
                {
                    NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
                }
            }).Size(symbolWidth, 100, "dot").Barcode(0, 0, "128", 40, 0, 0, 2, 2, "1234");

            Assert.NotEmpty(before);
            Assert.Equal(overflowBefore, overflow.Build().GetBytes());
        }
        else
        {
            var profile = new ZplPrinterProfile
            {
                NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
            };
            var command = new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { Profile = profile }).Label(symbolWidth + overhangDots, 100);
            profile.NativeBarcodeHumanReadableLeftOverhangDots = 1;
            Assert.Equal(overhangDots, command.Capabilities.NativeBarcodeHumanReadableLeftOverhangDots);
            command.Barcode(overhangDots, 0, "1234", 40, true, 2);
            var before = command.Build().GetBytes();

            var overflow = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile
                {
                    NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
                }
            }).Label(symbolWidth + overhangDots, 100);
            var overflowBefore = overflow.Build().GetBytes();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                overflow.Barcode(overhangDots - 1, 0, "1234", 40, true, 2));

            new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile
                {
                    NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
                }
            }).Label(symbolWidth, 100).Barcode(0, 0, "1234", 40, false, 2);

            Assert.NotEmpty(before);
            Assert.Equal(overflowBefore, overflow.Build().GetBytes());
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_hri_width_profile_controls_direct_bounds_snapshot_and_hri_off(PrintProtocol protocol)
    {
        const int reserveWidth = 240;
        var symbolWidth = (int)EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128, "1234", 2);
        if (protocol == PrintProtocol.Tspl)
        {
            var profile = new TsplPrinterProfile { NativeBarcodeHumanReadableReserveWidthDots = reserveWidth };
            var command = new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions { Profile = profile }).Size(reserveWidth, 100, "dot");
            profile.NativeBarcodeHumanReadableReserveWidthDots = 1;
            Assert.Equal(reserveWidth, command.Capabilities.NativeBarcodeHumanReadableReserveWidthDots);
            command.Barcode(0, 0, "128", 40, 1, 0, 2, 2, "1234");
            var before = command.Build().GetBytes();

            var overflow = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { NativeBarcodeHumanReadableReserveWidthDots = reserveWidth }
            }).Size(reserveWidth - 1, 100, "dot");
            var overflowBefore = overflow.Build().GetBytes();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                overflow.Barcode(0, 0, "128", 40, 1, 0, 2, 2, "1234"));

            new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { NativeBarcodeHumanReadableReserveWidthDots = reserveWidth }
            }).Size(symbolWidth, 100, "dot").Barcode(0, 0, "128", 40, 0, 0, 2, 2, "1234");

            Assert.NotEmpty(before);
            Assert.Equal(overflowBefore, overflow.Build().GetBytes());
        }
        else
        {
            var profile = new ZplPrinterProfile { NativeBarcodeHumanReadableReserveWidthDots = reserveWidth };
            var command = new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { Profile = profile }).Label(reserveWidth, 100);
            profile.NativeBarcodeHumanReadableReserveWidthDots = 1;
            Assert.Equal(reserveWidth, command.Capabilities.NativeBarcodeHumanReadableReserveWidthDots);
            command.Barcode(0, 0, "1234", 40, true, 2);
            var before = command.Build().GetBytes();

            var overflow = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { NativeBarcodeHumanReadableReserveWidthDots = reserveWidth }
            }).Label(reserveWidth - 1, 100);
            var overflowBefore = overflow.Build().GetBytes();
            Assert.Throws<ArgumentOutOfRangeException>(() => overflow.Barcode(0, 0, "1234", 40, true, 2));

            new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { NativeBarcodeHumanReadableReserveWidthDots = reserveWidth }
            }).Label(symbolWidth, 100).Barcode(0, 0, "1234", 40, false, 2);

            Assert.NotEmpty(before);
            Assert.Equal(overflowBefore, overflow.Build().GetBytes());
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_hri_width_profile_controls_template_provider_bounds(PrintProtocol protocol)
    {
        const int reserveWidth = 240;
        var document = BarcodeDocument(BarcodeType.Code128, true, "1234", pageWidth: reserveWidth,
            reservedWidth: 1, reserveHeight: 24);
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            tsplOptions: protocol == PrintProtocol.Tspl
                ? new TsplMediaOptions { Profile = new TsplPrinterProfile {
                    NativeBarcodeHumanReadableReserveWidthDots = reserveWidth
                }} : null,
            zplOptions: protocol == PrintProtocol.Zpl
                ? new ZplMediaOptions { Profile = new ZplPrinterProfile {
                    NativeBarcodeHumanReadableReserveWidthDots = reserveWidth
                }} : null);
        provider.Validate(document);

        var overflow = BarcodeDocument(BarcodeType.Code128, true, "1234", pageWidth: reserveWidth - 1,
            reservedWidth: 1, reserveHeight: 24);
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(overflow));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ElementPath);
        Assert.Contains("宽度", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_hri_left_overhang_profile_controls_template_provider_bounds(PrintProtocol protocol)
    {
        const int overhangDots = 12;
        var symbolWidth = (int)EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128, "1234", 2);
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            tsplOptions: protocol == PrintProtocol.Tspl
                ? new TsplMediaOptions { Profile = new TsplPrinterProfile {
                    NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
                }} : null,
            zplOptions: protocol == PrintProtocol.Zpl
                ? new ZplMediaOptions { Profile = new ZplPrinterProfile {
                    NativeBarcodeHumanReadableLeftOverhangDots = overhangDots
                }} : null);

        provider.Validate(BarcodeDocument(BarcodeType.Code128, true, "1234",
            pageWidth: symbolWidth + overhangDots, reservedWidth: 1, reserveHeight: 24,
            x: overhangDots));

        var overflow = BarcodeDocument(BarcodeType.Code128, true, "1234",
            pageWidth: symbolWidth + overhangDots, reservedWidth: 1, reserveHeight: 24,
            x: overhangDots - 1);
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(overflow));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.ElementPath);
        Assert.Contains("宽度", error.Message);

        provider.Validate(BarcodeDocument(BarcodeType.Code128, false, "1234",
            pageWidth: symbolWidth, reservedWidth: 1, x: 0));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_hri_page_bounds_profile_controls_all_sides_and_rotated_template_bounds(PrintProtocol protocol)
    {
        const int left = 12;
        const int top = 8;
        const int right = 16;
        const int bottom = 6;
        const int barHeight = 40;
        const int hriHeight = 8;
        var symbolWidth = (int)EscPosRasterCanvas.MeasureBarcodeWidth(BarcodeType.Code128, "1234", 2);
        var reserveWidth = symbolWidth + 4;
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            tsplOptions: protocol == PrintProtocol.Tspl
                ? new TsplMediaOptions { Profile = new TsplPrinterProfile {
                    NativeBarcodeHumanReadableReserveWidthDots = reserveWidth,
                    NativeBarcodeHumanReadableLeftOverhangDots = left,
                    NativeBarcodeHumanReadableTopOverhangDots = top,
                    NativeBarcodeHumanReadableRightOverhangDots = right,
                    NativeBarcodeHumanReadableBottomOverhangDots = bottom
                }} : null,
            zplOptions: protocol == PrintProtocol.Zpl
                ? new ZplMediaOptions { Profile = new ZplPrinterProfile {
                    NativeBarcodeHumanReadableReserveWidthDots = reserveWidth,
                    NativeBarcodeHumanReadableLeftOverhangDots = left,
                    NativeBarcodeHumanReadableTopOverhangDots = top,
                    NativeBarcodeHumanReadableRightOverhangDots = right,
                    NativeBarcodeHumanReadableBottomOverhangDots = bottom
                }} : null);

        var width = left + reserveWidth + right;
        var height = top + barHeight + hriHeight + bottom;
        provider.Validate(BarcodeDocument(BarcodeType.Code128, true, "1234", pageWidth: width,
            pageHeight: height, reservedWidth: 1, reserveHeight: hriHeight, x: left, y: top));

        Assert.Throws<PrintElementValidationException>(() => provider.Validate(BarcodeDocument(
            BarcodeType.Code128, true, "1234", pageWidth: width, pageHeight: height,
            reservedWidth: 1, reserveHeight: hriHeight, x: left - 1, y: top)));
        Assert.Throws<PrintElementValidationException>(() => provider.Validate(BarcodeDocument(
            BarcodeType.Code128, true, "1234", pageWidth: width, pageHeight: height,
            reservedWidth: 1, reserveHeight: hriHeight, x: left, y: top - 1)));
        Assert.Throws<PrintElementValidationException>(() => provider.Validate(BarcodeDocument(
            BarcodeType.Code128, true, "1234", pageWidth: width - 1, pageHeight: height,
            reservedWidth: 1, reserveHeight: hriHeight, x: left, y: top)));
        Assert.Throws<PrintElementValidationException>(() => provider.Validate(BarcodeDocument(
            BarcodeType.Code128, true, "1234", pageWidth: width, pageHeight: height - 1,
            reservedWidth: 1, reserveHeight: hriHeight, x: left, y: top)));

        var rotatedWidth = left + barHeight + hriHeight + right;
        var rotatedHeight = top + reserveWidth + bottom;
        provider.Validate(BarcodeDocument(BarcodeType.Code128, true, "1234", pageWidth: rotatedWidth,
            pageHeight: rotatedHeight, reservedWidth: 1, reserveHeight: hriHeight,
            x: left, y: top, rotation: RotationAngle.Rotate90));
        Assert.Throws<PrintElementValidationException>(() => provider.Validate(BarcodeDocument(
            BarcodeType.Code128, true, "1234", pageWidth: left + barHeight + right,
            pageHeight: rotatedHeight, reservedWidth: 1, reserveHeight: hriHeight,
            x: left, y: top, rotation: RotationAngle.Rotate90)));

        provider.Validate(BarcodeDocument(BarcodeType.Code128, false, "1234",
            pageWidth: symbolWidth, pageHeight: barHeight, reservedWidth: 1, x: 0, y: 0));
    }

    private static byte[] BuildDirectBarcode(PrintProtocol protocol, int pageWidth, int pageHeight,
        int x, int y, bool humanReadable, int rotation, int left, int top, int right, int bottom,
        int reserveWidth, int reserveHeight,
        NativeBarcodeHumanReadableOverhang? rotation90Overhang = null,
        NativeBarcodeHumanReadableOverhang? rotation180Overhang = null,
        NativeBarcodeHumanReadableOverhang? rotation270Overhang = null)
    {
        if (protocol == PrintProtocol.Tspl)
        {
            var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile
                {
                    NativeBarcodeHumanReadableReserveWidthDots = reserveWidth,
                    NativeBarcodeHumanReadableReserveHeightDots = reserveHeight,
                    NativeBarcodeHumanReadableLeftOverhangDots = left,
                    NativeBarcodeHumanReadableTopOverhangDots = top,
                    NativeBarcodeHumanReadableRightOverhangDots = right,
                    NativeBarcodeHumanReadableBottomOverhangDots = bottom,
                    NativeBarcodeHumanReadableRotation90Overhang = rotation90Overhang,
                    NativeBarcodeHumanReadableRotation180Overhang = rotation180Overhang,
                    NativeBarcodeHumanReadableRotation270Overhang = rotation270Overhang
                }
            }).Size(pageWidth, pageHeight, "dot");
            command.Barcode(x, y, "128", 40, humanReadable ? 1 : 0, rotation, 2, 2, "1234");
            return command.Build().GetBytes();
        }

        var zpl = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile
            {
                NativeBarcodeHumanReadableReserveWidthDots = reserveWidth,
                NativeBarcodeHumanReadableReserveHeightDots = reserveHeight,
                NativeBarcodeHumanReadableLeftOverhangDots = left,
                NativeBarcodeHumanReadableTopOverhangDots = top,
                NativeBarcodeHumanReadableRightOverhangDots = right,
                NativeBarcodeHumanReadableBottomOverhangDots = bottom,
                NativeBarcodeHumanReadableRotation90Overhang = rotation90Overhang,
                NativeBarcodeHumanReadableRotation180Overhang = rotation180Overhang,
                NativeBarcodeHumanReadableRotation270Overhang = rotation270Overhang
            }
        }).Label(pageWidth, pageHeight);
        zpl.Barcode(x, y, "1234", 40, humanReadable, 2, rotation);
        return zpl.Build().GetBytes();
    }

    private static PrintDocument BarcodeDocument(bool? humanReadable)
        => BarcodeDocument(BarcodeType.Code128, humanReadable, "1234");

    private static PrintDocument BarcodeDocument(BarcodeType type, bool? humanReadable, string data,
        int pageWidth = 220, int pageHeight = 80, int reservedWidth = 180, int reserveHeight = 0,
        int x = 0, int y = 0, RotationAngle rotation = RotationAngle.None)
    {
        var document = new PrintDocument();
        document.AddPage(pageWidth, pageHeight).Add(new PrintBarcodeElement(x, y, type,
            data, 40, narrowWidth: 2, wideWidth: 2, rotation: rotation, humanReadable: humanReadable,
            humanReadableReserveHeight: reserveHeight, reservedWidth: reservedWidth),
            "Rows[0].Cells[0].Elements[0]");
        return document;
    }
}
