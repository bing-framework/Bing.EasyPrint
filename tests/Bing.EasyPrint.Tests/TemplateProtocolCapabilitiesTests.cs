using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证模板能力快照与实际模板渲染预检之间的契约。</summary>
public sealed class TemplateProtocolCapabilitiesTests
{
    static TemplateProtocolCapabilitiesTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void Default_cpcl_capabilities_preserve_legacy_rendering_and_semantic_fonts()
    {
        var capabilities = TemplateProtocolCapabilities.Create();

        Assert.Equal(PrintProtocol.Cpcl, capabilities.Protocol);
        Assert.Null(capabilities.EscPosLayoutMode);
        Assert.Equal(200, capabilities.Dpi);
        Assert.All(Enumerable.Range(1, 10), type => Assert.True(capabilities.SupportsElementType(type)));
        Assert.Equal(new[] { RotationAngle.None, RotationAngle.Rotate270 }, capabilities.BarcodeRotations);
        Assert.Equal(new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 }, capabilities.QrCodeRotations);
        Assert.Contains(BarcodeType.Code128, capabilities.BarcodeTypes);
        Assert.Contains(24, capabilities.TextSizes);
        Assert.True(capabilities.SupportsBarcodeHumanReadable);

        var font24 = new PrintFont(PrintFontKind.Device, "24");
        var font55 = new PrintFont(PrintFontKind.Device, "55");
        Assert.True(capabilities.SupportsFont(font24, 24));
        Assert.True(capabilities.SupportsFont(font24, 48));
        Assert.False(capabilities.SupportsFont(font24, 16));
        Assert.True(capabilities.SupportsFont(font55, 16));
        Assert.True(capabilities.SupportsFont(font55, 32));
        Assert.False(capabilities.SupportsFont(font55, 24));

        AssertRenderSucceeds(Template(Text("DEFAULT")), Options(PrintProtocol.Cpcl));
        AssertRenderSucceeds(Template(Text("DEVICE24", 24, font24)), Options(PrintProtocol.Cpcl));
        AssertRenderSucceeds(Template(Text("DEVICE55", 16, font55)), Options(PrintProtocol.Cpcl));
        AssertRenderFails(Template(Text("WRONG", 16, font24)), Options(PrintProtocol.Cpcl));

        AssertRenderSucceeds(Template(Barcode(rotation: RotationAngle.None)), Options(PrintProtocol.Cpcl));
        AssertRenderSucceeds(Template(Barcode(rotation: RotationAngle.Rotate270)), Options(PrintProtocol.Cpcl));
        AssertRenderFails(Template(Barcode(rotation: RotationAngle.Rotate90)), Options(PrintProtocol.Cpcl));
    }

    [Fact]
    public void Cpcl_device_font_capabilities_expose_discrete_size_families()
    {
        var capabilities = TemplateProtocolCapabilities.Create(Options(PrintProtocol.Cpcl));
        var font24 = Assert.Single(capabilities.DeviceFonts.Where(font => font.Name == "24"));
        var font55 = Assert.Single(capabilities.DeviceFonts.Where(font => font.Name == "55"));

        Assert.NotNull(font24.Sizes);
        Assert.NotNull(font55.Sizes);
        Assert.Contains(24, font24.Sizes);
        Assert.Contains(48, font24.Sizes);
        Assert.DoesNotContain(16, font24.Sizes);
        Assert.Contains(16, font55.Sizes);
        Assert.Contains(32, font55.Sizes);
        Assert.DoesNotContain(24, font55.Sizes);
    }

    [Fact]
    public void Tspl_fixed_font_range_matches_integer_rounding_and_renderer_boundary()
    {
        var options = Options(PrintProtocol.Tspl);
        var capabilities = TemplateProtocolCapabilities.Create(options);
        var font = Assert.Single(capabilities.DeviceFonts.Where(item => item.Name == "1"));

        Assert.Null(capabilities.TextSizes);
        Assert.Equal(1, font.Size.Minimum);
        Assert.Equal(125, font.Size.Maximum);
        Assert.True(capabilities.SupportsFont(new PrintFont(PrintFontKind.Device, "1"), 125));
        Assert.False(capabilities.SupportsFont(new PrintFont(PrintFontKind.Device, "1"), 126));

        AssertRenderSucceeds(Template(Text("TSPL", 125,
            new PrintFont(PrintFontKind.Device, "1"), width: 500)), options);
        AssertRenderFails(Template(Text("TSPL", 126,
            new PrintFont(PrintFontKind.Device, "1"), width: 300)), options);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 10, 723)]
    [InlineData(PrintProtocol.Zpl, 10, null)]
    public void Label_protocols_use_unbounded_integer_text_for_font_zero_and_qr_limit(
        PrintProtocol protocol, int qrMaximum, int? textMaximum)
    {
        var capabilities = TemplateProtocolCapabilities.Create(Options(protocol));

        Assert.Null(capabilities.TextSizes);
        Assert.Equal(textMaximum, capabilities.TextSize.Maximum);
        Assert.Equal(1, capabilities.TextSize.Minimum);
        Assert.Equal(1, capabilities.QrModuleSize.Minimum);
        Assert.Equal(qrMaximum, capabilities.QrModuleSize.Maximum);
        Assert.Contains(new PrintFont(PrintFontKind.Device, protocol == PrintProtocol.Tspl ? "1" : "A").Name,
            capabilities.DeviceFonts.Select(font => font.Name));
        Assert.True(capabilities.SupportsFont(PrintFont.Default, 100));
        AssertRenderSucceeds(Template(Text(protocol.ToString(), 100)), Options(protocol));
    }

    [Fact]
    public void Escpos_native_capabilities_match_native_renderer_limits()
    {
        var options = Options(PrintProtocol.EscPos, EscPosLayoutMode.Native);
        var capabilities = TemplateProtocolCapabilities.Create(options);

        Assert.Equal(EscPosLayoutMode.Native, capabilities.EscPosLayoutMode);
        Assert.Equal(new[] { 1, 3, 4, 6 }, capabilities.ElementTypes);
        Assert.Equal(new[] { 24 }, capabilities.TextSizes);
        Assert.Equal(new[] { RotationAngle.None }, capabilities.TextRotations);
        Assert.Equal(new[] { RotationAngle.None }, capabilities.BarcodeRotations);
        Assert.Equal(new[] { RotationAngle.None }, capabilities.QrCodeRotations);
        Assert.Equal(new[] { BarcodeType.Code128, BarcodeType.Code39, BarcodeType.Code93,
            BarcodeType.Codabar, BarcodeType.Ean8, BarcodeType.Ean13, BarcodeType.UpcA,
            BarcodeType.UpcE, BarcodeType.I2OF5, BarcodeType.I2OF5C, BarcodeType.I2OF5G,
            BarcodeType.UccEan128, BarcodeType.Gs1DataBarOmnidirectional,
            BarcodeType.Gs1DataBarTruncated, BarcodeType.Gs1DataBarLimited,
            BarcodeType.Gs1DataBarExpanded, BarcodeType.Code128Auto }, capabilities.BarcodeTypes);
        Assert.Equal((1, (int?)16), (capabilities.QrModuleSize.Minimum, capabilities.QrModuleSize.Maximum));
        Assert.Equal((2, (int?)6), (capabilities.BarcodeModuleWidth.Minimum, capabilities.BarcodeModuleWidth.Maximum));
        Assert.False(capabilities.SupportsHostFontFamily);

        AssertRenderSucceeds(Template(Text("NATIVE", 24)), options);
        AssertRenderFails(Template(Text("NATIVE", 25)), options);
        AssertRenderFails(Template(Text("NATIVE", 24, rotation: RotationAngle.Rotate90)), options);
        AssertRenderSucceeds(Template(Barcode(BarcodeType.Code128)), options);
        AssertRenderSucceeds(Template(Barcode(BarcodeType.Code39)), options);
        AssertRenderSucceeds(Template(Barcode(BarcodeType.Code93)), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.Codabar, "A12B")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.Ean8, "96385074")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.Ean13, "5901234123457")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.UpcA, "036000291452")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.UpcE, "0425261")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.I2OF5, "1234")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.I2OF5C, "1234")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.I2OF5G, "12345678901")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.Gs1DataBarOmnidirectional, "1234567890123")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.Gs1DataBarTruncated, "1234567890123")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.Gs1DataBarLimited, "1234567890123")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.Gs1DataBarExpanded, "01123")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.Gs1DataBarExpanded, "12{")), options);
        AssertRenderSucceeds(Template(BarcodeData(BarcodeType.Code128Auto, "1234")), options);
        AssertRenderSucceeds(Template(Qr(16)), options);
        AssertRenderFails(Template(Qr(17)), options);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Coordinate_label_capabilities_and_templates_expose_qr_rotations(PrintProtocol protocol)
    {
        var capabilities = TemplateProtocolCapabilities.Create(Options(protocol));
        Assert.Equal(new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 },
            capabilities.QrCodeRotations);
        Assert.True(capabilities.SupportsQrCodeRotation(RotationAngle.Rotate90));

        var qr = Qr(2);
        qr.Rotation = RotationAngle.Rotate270;
        AssertRenderSucceeds(Template(qr), Options(protocol));
    }

    [Fact]
    public void Escpos_native_codes_render_origin_bytes_and_layout_but_reject_explicit_offsets()
    {
        var options = Options(PrintProtocol.EscPos, EscPosLayoutMode.Native);

        var barcodePage = RenderPage(Template(Barcode()), options);
        var barcode = Assert.Single(barcodePage.Elements.Where(element => element.Kind == "element" && element.Type == 4));
        Assert.Equal((0, 0, 500, 48), (barcode.X, barcode.Y, barcode.Width, barcode.Height));
        Assert.Equal(new byte[]
        {
            0x1B, 0x40,
            0x1D, 0x48, 0x00, 0x1D, 0x68, 48, 0x1D, 0x77, 2,
            0x1D, 0x6B, 73, 8, (byte)'{', (byte)'B', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6'
        }, barcodePage.Data);

        var qrPage = RenderPage(Template(Qr(4)), options);
        var qr = Assert.Single(qrPage.Elements.Where(element => element.Kind == "element" && element.Type == 3));
        Assert.Equal((0, 0, 116, 116), (qr.X, qr.Y, qr.Width, qr.Height));
        Assert.Equal(new byte[]
        {
            0x1B, 0x40,
            0x1D, 0x28, 0x6B, 4, 0, 0x31, 0x41, 0x32, 0,
            0x1D, 0x28, 0x6B, 3, 0, 0x31, 0x43, 4,
            0x1D, 0x28, 0x6B, 3, 0, 0x31, 0x45, (byte)'1',
            0x1D, 0x28, 0x6B, 4, 0, 0x31, 0x50, 0x30, (byte)'Q',
            0x1D, 0x28, 0x6B, 3, 0, 0x31, 0x51, (byte)'0'
        }, qrPage.Data);

        foreach (var coordinate in new[] { new BpCoordinate { X = 1 }, new BpCoordinate { Y = 1 } })
        {
            var offsetBarcode = Barcode();
            offsetBarcode.StartCoordinate = coordinate;
            AssertNativeRejectsExplicitCoordinate(offsetBarcode, options);

            var offsetQr = Qr(4);
            offsetQr.StartCoordinate = new BpCoordinate { X = coordinate.X, Y = coordinate.Y };
            AssertNativeRejectsExplicitCoordinate(offsetQr, options);
        }
    }

    [Fact]
    public void Escpos_command_ranges_match_native_command_validation_and_raster_model_ranges()
    {
        var native = ProtocolCommandFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native).Capabilities;
        var raster = ProtocolCommandFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() }).Capabilities;
        var disabledProfile = new EscPosPrinterProfile
        {
            SupportsNativeBarcode = false,
            SupportsNativeQrCode = false
        };
        var disabled = ProtocolCommandFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { Profile = disabledProfile }).Capabilities;

        Assert.Equal((1, (int?)16), (native.CommandQrModuleSize.Minimum, native.CommandQrModuleSize.Maximum));
        Assert.Equal((2, (int?)6), (native.CommandBarcodeModuleWidth.Minimum, native.CommandBarcodeModuleWidth.Maximum));
        Assert.Equal((1, (int?)255), (native.CommandBarcodeHeight.Minimum, native.CommandBarcodeHeight.Maximum));
        Assert.Equal((1, (int?)null), (raster.QrModuleSize.Minimum, raster.QrModuleSize.Maximum));
        Assert.Equal((1, (int?)null), (raster.BarcodeModuleWidth.Minimum, raster.BarcodeModuleWidth.Maximum));
        Assert.Equal((1, (int?)null), (raster.BarcodeHeight.Minimum, raster.BarcodeHeight.Maximum));
        Assert.Equal((1, (int?)16), (raster.CommandQrModuleSize.Minimum, raster.CommandQrModuleSize.Maximum));
        Assert.Equal((2, (int?)6), (raster.CommandBarcodeModuleWidth.Minimum, raster.CommandBarcodeModuleWidth.Maximum));
        Assert.Equal((1, (int?)255), (raster.CommandBarcodeHeight.Minimum, raster.CommandBarcodeHeight.Maximum));
        Assert.Null(disabled.CommandQrModuleSize);
        Assert.Null(disabled.CommandBarcodeModuleWidth);
        Assert.Null(disabled.CommandBarcodeHeight);

        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native);
        Assert.Same(command, command.QrCode("Q", moduleSize: 16));
        Assert.Same(command, command.Barcode("123456", height: 255, width: 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.QrCode("Q", moduleSize: 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Barcode("123456", height: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Barcode("123456", width: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => command.Barcode("123456", width: 7));
    }

    [Fact]
    public void Escpos_raster_allows_host_fonts_and_wider_qr_but_keeps_renderer_barcode_contract()
    {
        var options = Options(PrintProtocol.EscPos, EscPosLayoutMode.Raster);
        var capabilities = TemplateProtocolCapabilities.Create(options);
        var family = EscPosRasterCanvas.ResolveFontFamilyName("capabilities", null, false,
            FixedTestFonts.Catalog());

        Assert.Null(capabilities.TextSizes);
        Assert.True(capabilities.SupportsHostFontFamily);
        Assert.Equal(new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 },
            capabilities.TextRotations);
        Assert.True(capabilities.SupportsTextRotation(RotationAngle.Rotate90));
        Assert.Equal(new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 },
            capabilities.BarcodeRotations);
        Assert.Equal(new[] { RotationAngle.None, RotationAngle.Rotate90, RotationAngle.Rotate180, RotationAngle.Rotate270 },
            capabilities.QrCodeRotations);
        Assert.True(capabilities.SupportsBarcodeRotation(RotationAngle.Rotate90));
        Assert.True(capabilities.SupportsQrCodeRotation(RotationAngle.Rotate270));
        Assert.True(capabilities.SupportsBarcodeHumanReadable);
        Assert.Contains(PrintFontKind.Host, capabilities.FontKinds);
        Assert.True(capabilities.SupportsFont(new PrintFont(PrintFontKind.Host, family), 24));
        Assert.Equal(32, capabilities.QrModuleSize.Maximum);
        Assert.Contains(BarcodeType.Code128, capabilities.BarcodeTypes);
        Assert.Contains(BarcodeType.Code39, capabilities.BarcodeTypes);
        Assert.Contains(BarcodeType.Ean13, capabilities.BarcodeTypes);
        Assert.Contains(BarcodeType.Gs1DataBarExpanded, capabilities.BarcodeTypes);

        AssertRenderSucceeds(Template(Text("HOST", 24,
            new PrintFont(PrintFontKind.Host, family), width: 300)), options);
        AssertRenderSucceeds(Template(Text("ROTATED", 24,
            new PrintFont(PrintFontKind.Host, family), rotation: RotationAngle.Rotate90)), options);
        AssertRenderSucceeds(Template(Barcode(BarcodeType.Code128)), options);
        AssertRenderSucceeds(Template(Barcode(BarcodeType.Code39)), options);
        AssertRenderSucceeds(Template(Qr(32)), options);
        AssertRenderFails(Template(Qr(33)), options);
    }

    [Fact]
    public void Escpos_profile_disabling_native_codes_changes_native_and_raster_capabilities()
    {
        var profile = new EscPosPrinterProfile
        {
            SupportsNativeBarcode = false,
            SupportsNativeQrCode = false
        };
        var nativeOptions = Options(PrintProtocol.EscPos, EscPosLayoutMode.Native, profile);
        var rasterOptions = Options(PrintProtocol.EscPos, EscPosLayoutMode.Raster, profile);
        var native = TemplateProtocolCapabilities.Create(nativeOptions);
        var raster = TemplateProtocolCapabilities.Create(rasterOptions);

        Assert.DoesNotContain(4, native.ElementTypes);
        Assert.DoesNotContain(3, native.ElementTypes);
        Assert.Empty(native.BarcodeTypes);
        Assert.Null(native.QrModuleSize);
        Assert.Contains(4, raster.ElementTypes);
        Assert.Contains(3, raster.ElementTypes);
        Assert.Contains(BarcodeType.Code128, raster.BarcodeTypes);
        Assert.Equal(32, raster.QrModuleSize.Maximum);

        AssertRenderFails(Template(Barcode(BarcodeType.Code128)), nativeOptions);
        AssertRenderFails(Template(Qr(1)), nativeOptions);
        AssertRenderSucceeds(Template(Barcode(BarcodeType.Code128)), rasterOptions);
        AssertRenderSucceeds(Template(Qr(32)), rasterOptions);
    }

    [Fact]
    public void Escpos_native_hri_position_allowlist_controls_default_template_position()
    {
        var aboveOnly = new EscPosPrinterProfile
        {
            SupportedNativeBarcodeHumanReadablePositions = new[] { EscPosBarcodeTextPosition.Above }
        };
        var aboveOptions = Options(PrintProtocol.EscPos, EscPosLayoutMode.Native, aboveOnly);
        var aboveCapabilities = TemplateProtocolCapabilities.Create(aboveOptions);

        Assert.True(aboveCapabilities.SupportsNativeBarcodeHumanReadablePosition(EscPosBarcodeTextPosition.Above));
        Assert.False(aboveCapabilities.SupportsNativeBarcodeHumanReadablePosition(EscPosBarcodeTextPosition.Below));
        Assert.False(aboveCapabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code128));
        var aboveBarcode = Barcode(BarcodeType.Code128);
        aboveBarcode.HumanReadable = true;
        AssertRenderFails(Template(aboveBarcode), aboveOptions);

        var belowOnly = new EscPosPrinterProfile
        {
            SupportedNativeBarcodeHumanReadablePositions = new[] { EscPosBarcodeTextPosition.Below }
        };
        var belowOptions = Options(PrintProtocol.EscPos, EscPosLayoutMode.Native, belowOnly);
        var belowCapabilities = TemplateProtocolCapabilities.Create(belowOptions);
        Assert.True(belowCapabilities.SupportsBarcodeHumanReadableType(BarcodeType.Code128));
        var belowBarcode = Barcode(BarcodeType.Code128);
        belowBarcode.HumanReadable = true;
        AssertRenderSucceeds(Template(belowBarcode), belowOptions);
    }

    [Fact]
    public void Create_takes_a_configuration_snapshot()
    {
        var profile = new EscPosPrinterProfile();
        var options = Options(PrintProtocol.EscPos, EscPosLayoutMode.Native, profile);
        var snapshot = TemplateProtocolCapabilities.Create(options);

        profile.SupportsNativeBarcode = false;
        profile.SupportsNativeQrCode = false;
        options.EscPosLayout = EscPosLayoutMode.Raster;
        options.EscPosDevice.RasterFonts = FixedTestFonts.Catalog();

        Assert.Equal(EscPosLayoutMode.Native, snapshot.EscPosLayoutMode);
        Assert.Contains(4, snapshot.ElementTypes);
        Assert.Contains(3, snapshot.ElementTypes);
        Assert.Contains(BarcodeType.Code128, snapshot.BarcodeTypes);
        Assert.Equal(new[] { RotationAngle.None }, snapshot.TextRotations);

        var changed = TemplateProtocolCapabilities.Create(options);
        Assert.Equal(EscPosLayoutMode.Raster, changed.EscPosLayoutMode);
        Assert.NotEqual(snapshot.ElementTypes, changed.ElementTypes);
        Assert.Null(changed.TextSizes);
    }

    [Fact]
    public void Parameter_ranges_are_inclusive_and_unbounded_ranges_accept_any_larger_value()
    {
        var bounded = new PrintParameterRange(2, 6);
        var unbounded = new PrintParameterRange(1);

        Assert.False(bounded.Contains(1));
        Assert.True(bounded.Contains(2));
        Assert.True(bounded.Contains(6));
        Assert.False(bounded.Contains(7));
        Assert.True(unbounded.Contains(1));
        Assert.True(unbounded.Contains(int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintParameterRange(6, 2));
    }

    [Fact]
    public void Core_font_queries_reject_null_unknown_and_host_fonts_for_cpcl()
    {
        var provider = ProtocolCommandFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII).Capabilities;

        Assert.True(provider.SupportsFont(new PrintFont(PrintFontKind.Device, "24"), 24));
        Assert.False(provider.SupportsFont(null, 24));
        Assert.False(provider.SupportsFont(new PrintFont(PrintFontKind.Device, "unknown"), 24));
        Assert.False(provider.SupportsFont(new PrintFont(PrintFontKind.Host, "Primary"), 24));
    }

    [Fact]
    public void Tspl_default_font_range_scales_with_configured_dpi()
    {
        var options = Options(PrintProtocol.Tspl);
        options.TsplMedia = new TsplMediaOptions { Dpi = 300 };
        var capabilities = TemplateProtocolCapabilities.Create(options);

        Assert.Equal((513 * 300 - 1) / 144, capabilities.TextSize.Maximum);
    }

    private static TemplateRenderOptions Options(PrintProtocol protocol,
        EscPosLayoutMode layout = EscPosLayoutMode.Native, EscPosPrinterProfile profile = null)
        => new TemplateRenderOptions
        {
            Protocol = protocol,
            EscPosLayout = layout,
            Encoding = Encoding.ASCII,
            EscPosDevice = protocol == PrintProtocol.EscPos && layout == EscPosLayoutMode.Raster
                ? new EscPosDeviceOptions { Profile = profile, RasterFonts = FixedTestFonts.Catalog() }
                : profile == null ? null : new EscPosDeviceOptions { Profile = profile },
            FeedLines = protocol == PrintProtocol.EscPos ? (int?)0 : null,
            CutAfterPrint = protocol == PrintProtocol.EscPos ? (bool?)false : null
        };

    private static BpTemplate Template(params BpElement[] elements)
        => Template(1200, 1200, elements);

    private static BpTemplate Template(int width, int height, params BpElement[] elements)
        => new BpTemplate
        {
            Version = 1,
            Width = width,
            Height = height,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Height = height,
                    Cells = new List<BpCell>
                    {
                        new BpCell { Width = width, Elements = elements.ToList() }
                    }
                }
            }
        };

    private static BpFontElement Text(string value, int size = 24, PrintFont font = null,
        RotationAngle rotation = RotationAngle.None, int width = 0)
        => new BpFontElement
        {
            FontSize = size,
            Font = font,
            Rotation = rotation,
            Width = width,
            Field = new BgField { Value = value }
        };

    private static BpBarcodeElement Barcode(BarcodeType type = BarcodeType.Code128,
        RotationAngle rotation = RotationAngle.None)
        => new BpBarcodeElement
        {
            BarcodeType = type,
            Rotation = rotation,
            LineWidth = 1,
            Height = 48,
            Width = 500,
            Field = new BgField { Value = "123456" }
        };

    private static BpBarcodeElement BarcodeData(BarcodeType type, string data)
        => new BpBarcodeElement
        {
            BarcodeType = type,
            LineWidth = 1,
            Height = 48,
            Width = 500,
            Field = new BgField { Value = data }
        };

    private static BpQrCodeElement Qr(int unitWidth)
        => new BpQrCodeElement { UnitWidth = unitWidth, Field = new BgField { Value = "Q" } };

    private static void AssertRenderSucceeds(BpTemplate template, TemplateRenderOptions options)
    {
        var result = new TemplateRenderer().RenderWithLayout(template, null, options: options);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Pages);
    }

    private static TemplatePage RenderPage(BpTemplate template, TemplateRenderOptions options)
        => Assert.Single(new TemplateRenderer().RenderWithLayout(template, null, options: options).Pages);

    private static void AssertNativeRejectsExplicitCoordinate(BpElement element, TemplateRenderOptions options)
    {
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(Template(element), null, options: options));
        Assert.Contains("绝对坐标", error.Message);
    }

    private static void AssertRenderFails(BpTemplate template, TemplateRenderOptions options)
        => Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(template, null, options: options));
}
