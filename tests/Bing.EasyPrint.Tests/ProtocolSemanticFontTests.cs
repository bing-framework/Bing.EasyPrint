using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证统一字体模型与旧文本元素 API 及各协议 Provider 的兼容行为。
/// </summary>
public sealed class ProtocolSemanticFontTests
{
    /// <summary>
    /// 验证旧构造器继续保存旧字体属性，并且不生成统一字体选择。
    /// </summary>
    [Fact]
    public void Legacy_text_constructor_preserves_legacy_font_properties()
    {
        var text = new PrintTextElement(1, 2, "TEXT", 24, bold: true, underline: true,
            rotation: RotationAngle.Rotate90, width: 40, height: 24, fontFamily: "Host Font",
            reverse: true, deviceFontName: "1");

        Assert.Null(text.Font);
        Assert.Equal("Host Font", text.FontFamily);
        Assert.Equal("1", text.DeviceFontName);
        Assert.Equal("TEXT", text.Text);
        Assert.Equal(24, text.FontSize);
        Assert.True(text.Bold);
        Assert.True(text.Underline);
        Assert.True(text.Reverse);
        Assert.Equal(RotationAngle.Rotate90, text.Rotation);
        Assert.Equal(40, text.Width);
        Assert.Equal(24, text.Height);
    }

    /// <summary>
    /// 验证统一字体构造器保存不可变选择并映射到兼容旧字段。
    /// </summary>
    [Fact]
    public void Typed_text_constructor_maps_font_selection_to_legacy_properties()
    {
        var hostFont = new PrintFont(PrintFontKind.Host, "  Host Font  ");
        var host = new PrintTextElement(0, 0, "HOST", 24, hostFont);
        var deviceFont = new PrintFont(PrintFontKind.Device, "  55  ");
        var device = new PrintTextElement(0, 0, "DEVICE", 16, deviceFont);
        var defaultText = new PrintTextElement(0, 0, "DEFAULT", 24, PrintFont.Default);

        Assert.Same(hostFont, host.Font);
        Assert.Equal(hostFont.Name, host.FontFamily);
        Assert.Null(host.DeviceFontName);
        Assert.Same(deviceFont, device.Font);
        Assert.Null(device.FontFamily);
        Assert.Equal(deviceFont.Name, device.DeviceFontName);
        Assert.Same(PrintFont.Default, defaultText.Font);
        Assert.Null(defaultText.FontFamily);
        Assert.Null(defaultText.DeviceFontName);
    }

    /// <summary>
    /// 验证字体来源枚举和字体名称约束在构造阶段生效。
    /// </summary>
    [Fact]
    public void Print_font_rejects_invalid_kind_and_name_combinations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PrintFont((PrintFontKind)99));

        Assert.Throws<ArgumentException>(() =>
            new PrintFont(PrintFontKind.Default, ""));
        Assert.Throws<ArgumentException>(() =>
            new PrintFont(PrintFontKind.Default, "default"));

        Assert.Throws<ArgumentException>(() =>
            new PrintFont(PrintFontKind.Device, null));
        Assert.Throws<ArgumentException>(() =>
            new PrintFont(PrintFontKind.Device, " \t"));
        Assert.Throws<ArgumentException>(() =>
            new PrintFont(PrintFontKind.Host, "\r\n"));

        var preserved = new PrintFont(PrintFontKind.Device, "  55  ");
        Assert.Equal("  55  ", preserved.Name);
    }

    /// <summary>
    /// 验证统一字体构造器拒绝空字体描述。
    /// </summary>
    [Fact]
    public void Typed_text_constructor_rejects_null_font()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PrintTextElement(0, 0, "TEXT", 24, (PrintFont)null));
    }

    /// <summary>
    /// 验证 TSPL 和 ZPL 的设备字体新旧 API 生成完全相同的字节。
    /// </summary>
    [Theory]
    [InlineData(PrintProtocol.Tspl, "1")]
    [InlineData(PrintProtocol.Zpl, "A")]
    public void Typed_device_font_matches_legacy_protocol_bytes(PrintProtocol protocol, string font)
    {
        var legacy = Render(protocol,
            new PrintTextElement(1, 1, "TEST", 24, deviceFontName: font));
        var typed = Render(protocol,
            new PrintTextElement(1, 1, "TEST", 24,
                new PrintFont(PrintFontKind.Device, font)));

        Assert.Equal(legacy, typed);
    }

    /// <summary>
    /// 验证 CPCL 设备字体 55 与 16 点字号的统一模型输出保持旧字节一致。
    /// </summary>
    [Fact]
    public void Typed_cpcl_device_font_55_matches_default_font_at_size_16()
    {
        var legacy = Render(PrintProtocol.Cpcl,
            new PrintTextElement(1, 1, "TEST", 16));
        var typed = Render(PrintProtocol.Cpcl,
            new PrintTextElement(1, 1, "TEST", 16,
                new PrintFont(PrintFontKind.Device, "55")));

        Assert.Equal(legacy, typed);
    }

    /// <summary>
    /// 验证 CPCL 设备字体标识必须与内置字号匹配。
    /// </summary>
    [Fact]
    public void Typed_cpcl_device_font_rejects_mismatched_font_size()
    {
        var document = DocumentWith(new PrintTextElement(1, 1, "TEST", 24,
            new PrintFont(PrintFontKind.Device, "55")));
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Cpcl, Encoding.ASCII);

        var error = Assert.Throws<PrintElementValidationException>(() => provider.Validate(document));

        Assert.Equal(PrintProtocol.Cpcl, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
    }

    /// <summary>
    /// 验证主机字体只允许 ESC/POS Raster 布局。
    /// </summary>
    [Theory]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Native)]
    public void Host_font_is_rejected_outside_escpos_raster(PrintProtocol protocol, EscPosLayoutMode layout)
    {
        var document = DocumentWith(HostText());
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, layout);

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));

        Assert.Equal(protocol, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
    }

    /// <summary>
    /// 验证主机字体在 ESC/POS Raster 中可以完成校验和光栅输出。
    /// </summary>
    [Fact]
    public void Host_font_is_supported_by_escpos_raster()
    {
        var document = DocumentWith(HostText());
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() });

        provider.Validate(document);
        Assert.NotEmpty(Assert.Single(provider.Render(document)));
    }

    /// <summary>
    /// 验证显式设备字体在 ESC/POS Native 和 Raster 模式均严格拒绝。
    /// </summary>
    [Theory]
    [InlineData(EscPosLayoutMode.Native)]
    [InlineData(EscPosLayoutMode.Raster)]
    public void Device_font_is_rejected_by_all_escpos_layout_modes(EscPosLayoutMode layout)
    {
        var document = DocumentWith(new PrintTextElement(0, 0, "DEVICE", 24,
            new PrintFont(PrintFontKind.Device, "1")));
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII, layout,
            layout == EscPosLayoutMode.Raster
                ? new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() }
                : null);

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(document));

        Assert.Equal(PrintProtocol.EscPos, error.Protocol);
        Assert.Equal("Rows[0].Text", error.ElementPath);
    }

    private static PrintTextElement HostText()
    {
        return new PrintTextElement(0, 0, "HOST", 24,
            new PrintFont(PrintFontKind.Host, "Primary"));
    }

    private static PrintDocument DocumentWith(PrintTextElement text)
    {
        var document = new PrintDocument();
        document.AddPage(300, 80).Add(text, "Rows[0].Text");
        return document;
    }

    private static byte[] Render(PrintProtocol protocol, PrintTextElement text)
    {
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
        return Assert.Single(provider.Render(DocumentWith(text)));
    }
}
