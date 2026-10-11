using System;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 RasterPage 在创建画布和调用用户回调之前完成输入检查，且失败不污染命令状态。
/// </summary>
public sealed class EscPosRasterPagePreflightTests
{
    [Fact]
    public void Native_mode_rejects_before_invoking_callback_and_preserves_output()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native).Initialize();
        var before = command.Build().GetBytes();
        var callbackCalled = false;

        Assert.Throws<InvalidOperationException>(() => command.RasterPage(8, 1, _ => callbackCalled = true));

        Assert.False(callbackCalled);
        Assert.Equal(before, command.Build().GetBytes());
        command.Text("after");
        Assert.Equal(
            new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native).Initialize().Text("after").Build().GetBytes(),
            command.Build().GetBytes());
    }

    [Fact]
    public void Active_raster_page_rejects_before_invoking_callback_and_can_still_finish()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(8, 1)
            .Line(0, 0, 7, 0);
        var expected = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(8, 1)
            .Line(0, 0, 7, 0)
            .EndRasterPage()
            .Build().GetBytes();
        var callbackCalled = false;

        Assert.Throws<InvalidOperationException>(() => command.RasterPage(8, 1, _ => callbackCalled = true));

        Assert.False(callbackCalled);
        Assert.Equal(expected, command.EndRasterPage().Build().GetBytes());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(524281, 1)]
    [InlineData(1, 65536)]
    [InlineData(int.MaxValue, 1)]
    public void Invalid_dimensions_reject_before_callback_without_allocating_or_polluting(int width, int height)
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Initialize();
        var before = command.Build().GetBytes();
        var callbackCalled = false;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.RasterPage(width, height, _ => callbackCalled = true));

        Assert.False(callbackCalled);
        Assert.Equal(before, command.Build().GetBytes());
        command.Feed();
        Assert.Equal(
            new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Initialize().Feed().Build().GetBytes(),
            command.Build().GetBytes());
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(5, true)]
    public void Profile_printable_width_including_double_width_rejects_before_callback(int width, bool doubleWidth)
    {
        var profile = EscPosPrinterProfile.EightDotImpact(8, 180, 180, 180);
        var command = CreateRasterCommand(profile).Initialize();
        var before = command.Build().GetBytes();
        var callbackCalled = false;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.RasterPage(width, 1, _ => callbackCalled = true, doubleWidth));

        Assert.False(callbackCalled);
        Assert.Equal(before, command.Build().GetBytes());
        command.Feed();
        Assert.Equal(
            CreateRasterCommand(profile).Initialize().Feed().Build().GetBytes(),
            command.Build().GetBytes());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Esc_star_double_flags_reject_before_callback(bool doubleWidth, bool doubleHeight)
    {
        var command = CreateRasterCommand(CreateEscStarProfile()).Initialize();
        var before = command.Build().GetBytes();
        var callbackCalled = false;

        Assert.Throws<NotSupportedException>(() =>
            command.RasterPage(8, 8, _ => callbackCalled = true, doubleWidth, doubleHeight));

        Assert.False(callbackCalled);
        Assert.Equal(before, command.Build().GetBytes());
        command.Feed();
        Assert.Equal(
            CreateRasterCommand(CreateEscStarProfile()).Initialize().Feed().Build().GetBytes(),
            command.Build().GetBytes());
    }

    [Fact]
    public void Esc_star_column_field_limit_rejects_before_callback()
    {
        var command = CreateRasterCommand(CreateEscStarProfile()).Initialize();
        var before = command.Build().GetBytes();
        var callbackCalled = false;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.RasterPage(ushort.MaxValue + 1, 1, _ => callbackCalled = true));

        Assert.False(callbackCalled);
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Gs_v_zero_block_budget_rejects_before_callback()
    {
        var profile = new EscPosPrinterProfile
        {
            Name = "GS v 0 budget test",
            ImageCommand = EscPosImageCommand.RasterGsV0,
            HorizontalDpi = 203,
            VerticalDpi = 203,
            MotionUnitsPerInch = 203,
            MaxImageBlockBytes = 1
        };
        var command = CreateRasterCommand(profile).Initialize();
        var before = command.Build().GetBytes();
        var callbackCalled = false;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.RasterPage(16, 1, _ => callbackCalled = true));

        Assert.False(callbackCalled);
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Extreme_esc_star_dpi_rejects_before_callback()
    {
        var profile = CreateEscStarProfile();
        profile.HorizontalDpi = 1;
        profile.VerticalDpi = int.MaxValue;
        var command = CreateRasterCommand(profile).Initialize();
        var before = command.Build().GetBytes();
        var callbackCalled = false;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.RasterPage(1, 2, _ => callbackCalled = true));

        Assert.False(callbackCalled);
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Callback_exception_does_not_pollute_command_and_allows_recovery()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Initialize();
        var before = command.Build().GetBytes();
        var callbackCalled = false;

        Assert.Throws<InvalidOperationException>(() => command.RasterPage(8, 1, canvas =>
        {
            callbackCalled = true;
            canvas.SetPixel(0, 0);
            throw new InvalidOperationException("draw failed");
        }));

        Assert.True(callbackCalled);
        Assert.Equal(before, command.Build().GetBytes());
        command.RasterImage(8, 1, new byte[] { 0x80 });
        Assert.Equal(
            new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster).Initialize()
                .RasterImage(8, 1, new byte[] { 0x80 }).Build().GetBytes(),
            command.Build().GetBytes());
    }

    [Fact]
    public void Valid_raster_page_matches_equivalent_raster_image_bytes()
    {
        var pageBytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .RasterPage(8, 2, canvas =>
            {
                canvas.SetPixel(0, 0);
                canvas.SetPixel(7, 1);
            })
            .Build().GetBytes();
        var imageBytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .RasterImage(8, 2, new byte[] { 0x80, 0x01 })
            .Build().GetBytes();

        Assert.Equal(imageBytes, pageBytes);
    }

    private static EscPosPrintCommand CreateRasterCommand(EscPosPrinterProfile profile)
        => new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { Profile = profile });

    private static EscPosPrinterProfile CreateEscStarProfile()
        => new EscPosPrinterProfile
        {
            Name = "ESC * preflight test",
            ImageCommand = EscPosImageCommand.BitImageEscStar,
            BitImageMode = EscPosBitImageMode.DoubleDensity8,
            HorizontalDpi = 180,
            VerticalDpi = 180,
            MotionUnitsPerInch = 180
        };
}
