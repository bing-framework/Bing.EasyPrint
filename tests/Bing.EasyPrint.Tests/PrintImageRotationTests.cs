using System;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class PrintImageRotationTests
{
    // 非方形、非对称点阵：101 / 110，预期结果独立列出，避免对称图掩盖方向错误。
    [Theory]
    [InlineData(0, 3, 2, "A0C0")]
    [InlineData(90, 2, 3, "C08040")]
    [InlineData(180, 3, 2, "60A0")]
    [InlineData(270, 2, 3, "8040C0")]
    public void Packed_pixels_rotate_clockwise_and_keep_the_original_snapshot(int angle, int width, int height, string hex)
    {
        var input = new byte[] { 0xA0, 0xC0 };
        var image = new PrintImageElement(2, 1, 3, 2, input, (RotationAngle)angle);
        input[0] = 0;
        using var output = PrintImageRotation.Prepare(image, CancellationToken.None);
        Assert.Equal((width, height, 1), (output.Width, output.Height, output.RowBytes));
        Assert.Equal(Hex(hex), output.Raster);
        Assert.Equal((3, 2, (RotationAngle)angle), (image.Width, image.Height, image.Rotation));
        Assert.Equal(new byte[] { 0xA0, 0xC0 }, image.Raster);
    }

    [Theory]
    [InlineData(0, "A7C3")]
    [InlineData(90, "C08040")]
    [InlineData(180, "60A0")]
    [InlineData(270, "8040C0")]
    public void Legacy_padding_is_preserved_at_zero_and_is_not_rotated_into_visible_pixels(int angle, string expected)
    {
        var image = new PrintImageElement(0, 0, 3, 2, new byte[] { 0xA7, 0xC3 }, (RotationAngle)angle);
        using var output = PrintImageRotation.Prepare(image, CancellationToken.None);
        Assert.Equal(Hex(expected), output.Raster);
    }

    [Theory]
    [InlineData(90, "008080808080808080")]
    [InlineData(270, "808080808080808000")]
    public void Rotation_recomputes_byte_rows_across_an_eight_pixel_boundary(int angle, string expected)
    {
        var image = new PrintImageElement(0, 0, 9, 1, new byte[] { 0x7F, 0x80 }, (RotationAngle)angle);
        using var output = PrintImageRotation.Prepare(image, CancellationToken.None);
        Assert.Equal((1, 9, 1), (output.Width, output.Height, output.RowBytes));
        Assert.Equal(Hex(expected), output.Raster);
    }

    [Theory]
    [InlineData(0, 3, 2, "A0C0")]
    [InlineData(90, 2, 3, "C08040")]
    [InlineData(180, 3, 2, "60A0")]
    [InlineData(270, 2, 3, "8040C0")]
    public void All_coordinate_providers_match_independently_pre_rotated_golden_data(int angle, int width, int height, string hex)
    {
        foreach (var protocol in new[] { PrintProtocol.Cpcl, PrintProtocol.Tspl, PrintProtocol.Zpl, PrintProtocol.EscPos })
        foreach (var compressed in new[] { false, true })
        {
            var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster);
            var document = new PrintDocument();
            document.AddPage(24, 12).Add(new PrintImageElement(2, 1, 3, 2, new byte[] { 0xA0, 0xC0 },
                (RotationAngle)angle, compressed), "Logo");
            var golden = new PrintDocument();
            golden.AddPage(24, 12).Add(new PrintImageElement(2, 1, width, height, Hex(hex), compressed));
            var bytes = provider.Render(document)[0];
            Assert.Equal(provider.Render(golden)[0], bytes);
            Assert.Equal(bytes, provider.Render(document)[0]);
            if (protocol == PrintProtocol.Tspl)
                Assert.Contains("BITMAP 2,1,1," + height + ",0,", Encoding.ASCII.GetString(bytes));
            if (protocol == PrintProtocol.Zpl)
                Assert.Contains("^FO2,1^GFA," + height + "," + height + ",1," + hex, Encoding.ASCII.GetString(bytes));
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Rotated_image_page_bounds_use_the_swapped_extent(PrintProtocol protocol)
    {
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster);
        var document = new PrintDocument();
        var page = document.AddPage(3, 3);
        page.Add(new PrintImageElement(1, 0, 3, 2, new byte[] { 0xA0, 0xC0 }, RotationAngle.Rotate90), "Logo");
        provider.Validate(document);
        var invalid = new PrintDocument();
        invalid.AddPage(3, 3).Add(new PrintImageElement(1, 1, 3, 2,
            new byte[] { 0xA0, 0xC0 }, RotationAngle.Rotate90), "Logo");
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Render(invalid));
        Assert.Equal("Logo", error.ElementPath);
        Assert.Contains("图片", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Host_image_rotation_is_available_when_native_rotation_is_disabled(PrintProtocol protocol)
    {
        var provider = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
            tsplOptions: protocol == PrintProtocol.Tspl ? new TsplMediaOptions
            { Profile = new TsplPrinterProfile { SupportsRotation = false } } : null,
            zplOptions: protocol == PrintProtocol.Zpl ? new ZplMediaOptions
            { Profile = new ZplPrinterProfile { SupportsRotation = false } } : null);
        Assert.False(provider.Capabilities.SupportsTextRotation);
        Assert.True(provider.Capabilities.SupportsImageRotation);
        var document = new PrintDocument();
        document.AddPage(24, 12).Add(new PrintImageElement(0, 0, 3, 2,
            new byte[] { 0xA0, 0xC0 }, RotationAngle.Rotate90));
        Assert.NotEmpty(provider.Render(document)[0]);
    }

    [Fact]
    public void Native_rejects_images_with_the_source_path()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII);
        Assert.False(provider.Capabilities.SupportsImageRotation);
        var document = new PrintDocument();
        document.AddPage(24, 12).Add(new PrintImageElement(0, 0, 3, 2,
            new byte[] { 0xA0, 0xC0 }, RotationAngle.Rotate90), "Logo");
        Assert.Equal("Logo", Assert.Throws<PrintCapabilityException>(() => provider.Render(document)).ElementPath);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Quiet_zone_neighbor_check_uses_the_rotated_image_body(PrintProtocol protocol)
    {
        var document = new PrintDocument();
        document.AddPage(140, 100)
            .Add(new PrintImageElement(20, 20, 3, 10, new byte[10], RotationAngle.Rotate90), "Logo")
            .Add(new PrintQrCodeElement(30, 20, "Q", 2, QrCodeCorrectionLevel.M,
                new PrintQuietZone(5, 0, 0, 0), width: 50, height: 50), "Qr");
        var issue = PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII, EscPosLayoutMode.Raster)
            .Check(document).FirstIssue;
        Assert.NotNull(issue);
        Assert.Contains("静区", issue.Message);
    }

    [Fact]
    public void Rotation_reserves_before_allocating_and_releases_its_temporary_buffer()
    {
        var image = new PrintImageElement(0, 0, 9, 1, new byte[] { 0x7F, 0x80 }, RotationAngle.Rotate90);
        using (var budget = PrintResourceScope.Begin(8))
        {
            var error = Assert.Throws<PrintResourceLimitException>(() => PrintImageRotation.Prepare(image, CancellationToken.None));
            Assert.Equal("Image.RotatedMonochrome", error.ResourceName);
            Assert.Equal(0, budget.CurrentBytes);
        }
        using (var budget = PrintResourceScope.Begin(9))
        {
            using (var output = PrintImageRotation.Prepare(image, CancellationToken.None))
                Assert.Equal(9, budget.CurrentBytes);
            Assert.Equal(0, budget.CurrentBytes);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => PrintImageRotation.Prepare(image, cancelled.Token));
            Assert.Equal(0, budget.CurrentBytes);
        }
    }

    [Fact]
    public void Invalid_angles_fail_before_snapshot_and_the_old_constructor_remains_available()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintImageElement(0, 0, 3, 2,
            new byte[] { 0xA0, 0xC0 }, (RotationAngle)45));
        Assert.NotNull(typeof(PrintImageElement).GetConstructor(new[]
        { typeof(int), typeof(int), typeof(int), typeof(int), typeof(byte[]), typeof(bool) }));
    }

    private static byte[] Hex(string value) => Enumerable.Range(0, value.Length / 2)
        .Select(index => Convert.ToByte(value.Substring(index * 2, 2), 16)).ToArray();
}
