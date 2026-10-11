using System;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 TSPL/ZPL 直接命令按已设置标签尺寸执行页面边界预检。</summary>
public sealed class ProtocolCommandPageBoundsTests
{
    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Direct_filled_rectangles_accept_exact_edge_and_reject_overflow_atomically(PrintProtocol protocol)
    {
        if (protocol == PrintProtocol.Tspl)
        {
            var command = new TsplPrintCommand(Encoding.ASCII).Size(100, 50, "dot")
                .FillRectangle(0, 0, 10, 10)
                .FillRectangle(90, 40, 10, 10);
            var before = command.Build().GetBytes();

            Assert.Throws<ArgumentOutOfRangeException>(() => command.FillRectangle(90, 40, 11, 10));
            Assert.Equal(before, command.Build().GetBytes());
        }
        else
        {
            var command = new ZplPrintCommand(Encoding.ASCII).Label(100, 50)
                .FillRectangle(0, 0, 10, 10)
                .FillRectangle(90, 40, 10, 10);
            var before = command.Build().GetBytes();

            Assert.Throws<ArgumentOutOfRangeException>(() => command.FillRectangle(90, 40, 11, 10));
            Assert.Equal(before, command.Build().GetBytes());
        }
    }

    [Fact]
    public void Tspl_text_uses_fixed_font_extent_and_marks_estimate_in_error()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Size(100, 20, "dot")
            .Text(84, 0, "1", 0, 1, 1, "AB");

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.Text(85, 0, "1", 0, 1, 1, "AB"));

        Assert.Contains("估算", error.Message);
        Assert.Contains("文字", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Label_text_bounds_count_a_supplementary_unicode_scalar_once(PrintProtocol protocol)
    {
        var text = char.ConvertFromUtf32(0x1F600);
        byte[] before;
        if (protocol == PrintProtocol.Tspl)
        {
            var command = new TsplPrintCommand(Encoding.UTF8).Size(100, 20, "dot")
                .Text(84, 0, "1", 0, 1, 1, text);
            before = command.Build().GetBytes();

            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                command.Text(85, 0, "1", 0, 1, 1, text));

            Assert.Contains("估算", error.Message);
            Assert.Contains("文字", error.Message);
            Assert.Equal(before, command.Build().GetBytes());
        }
        else
        {
            var command = new ZplPrintCommand(Encoding.UTF8).Label(100, 20)
                .Text(84, 0, text, 10, 16);
            before = command.Build().GetBytes();

            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                command.Text(85, 0, text, 10, 16));

            Assert.Contains("估算", error.Message);
            Assert.Contains("文字", error.Message);
            Assert.Equal(before, command.Build().GetBytes());
        }
    }

    [Fact]
    public void Tspl_font_zero_converts_point_size_to_dots_at_configured_dpi()
    {
        var command = new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { Dpi = 203 }).Size(100, 40, "dot");
        command.Text(74, 0, "0", 0, 9, 9, "A");

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.Text(75, 0, "0", 0, 9, 9, "A"));

        Assert.Contains("估算", error.Message);
    }

    [Fact]
    public void Zpl_text_uses_requested_cell_extent_and_rotates_bounds()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Label(100, 100)
            .Text(80, 0, "AB", 10, 10);
        command.Text(0, 90, "A", 10, 10, rotation: 90);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            command.Text(81, 0, "AB", 10, 10));

        Assert.Contains("估算", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_barcode_estimate_checks_rotated_footprint(PrintProtocol protocol)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (protocol == PrintProtocol.Tspl)
                new TsplPrintCommand(Encoding.ASCII).Size(100, 100, "dot")
                    .Barcode(0, 95, "128", 10, 0, 90, 1, 1, "1");
            else
                new ZplPrintCommand(Encoding.ASCII).Label(100, 100)
                    .Barcode(0, 95, "1", 10, false, 1, 90);
        });

        Assert.Contains("估算", error.Message);
        Assert.Contains("条码", error.Message);
    }

    [Fact]
    public void Zpl_code49_uses_70_module_width_and_encoded_row_height_for_direct_bounds()
    {
        const string data = "123-ABC";
        const int rowHeightDots = 40;
        const int moduleWidth = 2;
        var symbolHeight = EscPosRasterCanvas.MeasureBarcodeHeight(BarcodeType.Code49, data,
            rowHeightDots, moduleWidth, System.Threading.CancellationToken.None);
        var command = new ZplPrintCommand(Encoding.ASCII).Label(140, symbolHeight)
            .BarcodeCode49(0, 0, data, rowHeightDots, false, moduleWidth);
        var before = command.Build().GetBytes();

        var widthOverflow = new ZplPrintCommand(Encoding.ASCII).Label(139, symbolHeight);
        var widthBefore = widthOverflow.Build().GetBytes();
        var widthError = Assert.Throws<ArgumentOutOfRangeException>(() =>
            widthOverflow.BarcodeCode49(0, 0, data, rowHeightDots, false, moduleWidth));

        var heightOverflow = new ZplPrintCommand(Encoding.ASCII).Label(140, symbolHeight - 1);
        var heightBefore = heightOverflow.Build().GetBytes();
        var heightError = Assert.Throws<ArgumentOutOfRangeException>(() =>
            heightOverflow.BarcodeCode49(0, 0, data, rowHeightDots, false, moduleWidth));

        Assert.Contains("^B4N,20,N,A", Encoding.ASCII.GetString(before));
        Assert.Contains("标签页范围", widthError.Message);
        Assert.Contains("估算", heightError.Message);
        Assert.Equal(widthBefore, widthOverflow.Build().GetBytes());
        Assert.Equal(heightBefore, heightOverflow.Build().GetBytes());
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_barcode_hri_profile_reservation_controls_bounds_and_is_snapshotted(PrintProtocol protocol)
    {
        if (protocol == PrintProtocol.Tspl)
        {
            var profile = new TsplPrinterProfile { NativeBarcodeHumanReadableReserveHeightDots = 8 };
            var command = new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions { Profile = profile }).Size(400, 48, "dot");
            profile.NativeBarcodeHumanReadableReserveHeightDots = 24;
            command.Barcode(0, 0, "128", 40, 1, 0, 2, 2, "1234");
            var before = command.Build().GetBytes();

            var overflow = new TsplPrintCommand(Encoding.ASCII,
                new TsplMediaOptions { Profile = new TsplPrinterProfile {
                    NativeBarcodeHumanReadableReserveHeightDots = 8
                }}).Size(400, 47, "dot");
            var overflowBefore = overflow.Build().GetBytes();
            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                overflow.Barcode(0, 0, "128", 40, 1, 0, 2, 2, "1234"));

            Assert.Contains("估算", error.Message);
            Assert.Equal(before, command.Build().GetBytes());
            Assert.Equal(overflowBefore, overflow.Build().GetBytes());
        }
        else
        {
            var profile = new ZplPrinterProfile { NativeBarcodeHumanReadableReserveHeightDots = 8 };
            var command = new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { Profile = profile }).Label(400, 48);
            profile.NativeBarcodeHumanReadableReserveHeightDots = 24;
            command.Barcode(0, 0, "1234", 40, true, 2);
            var before = command.Build().GetBytes();

            var overflow = new ZplPrintCommand(Encoding.ASCII,
                new ZplMediaOptions { Profile = new ZplPrinterProfile {
                    NativeBarcodeHumanReadableReserveHeightDots = 8
                }}).Label(400, 47);
            var overflowBefore = overflow.Build().GetBytes();
            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                overflow.Barcode(0, 0, "1234", 40, true, 2));

            Assert.Contains("估算", error.Message);
            Assert.Equal(before, command.Build().GetBytes());
            Assert.Equal(overflowBefore, overflow.Build().GetBytes());
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_barcode_hri_without_profile_retains_legacy_24_dot_estimate(PrintProtocol protocol)
    {
        if (protocol == PrintProtocol.Tspl)
        {
            var command = new TsplPrintCommand(Encoding.ASCII).Size(400, 64, "dot")
                .Barcode(0, 0, "128", 40, 1, 0, 2, 2, "1234");
            Assert.NotEmpty(command.Build().GetBytes());

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TsplPrintCommand(Encoding.ASCII).Size(400, 63, "dot")
                    .Barcode(0, 0, "128", 40, 1, 0, 2, 2, "1234"));
        }
        else
        {
            var command = new ZplPrintCommand(Encoding.ASCII).Label(400, 64)
                .Barcode(0, 0, "1234", 40, true, 2);
            Assert.NotEmpty(command.Build().GetBytes());

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ZplPrintCommand(Encoding.ASCII).Label(400, 63)
                    .Barcode(0, 0, "1234", 40, true, 2));
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_barcode_hri_profile_reservation_is_ignored_when_hri_is_disabled(PrintProtocol protocol)
    {
        if (protocol == PrintProtocol.Tspl)
        {
            var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { NativeBarcodeHumanReadableReserveHeightDots = 24 }
            }).Size(400, 40, "dot");

            command.Barcode(0, 0, "128", 40, 0, 0, 2, 2, "1234");
            Assert.NotEmpty(command.Build().GetBytes());
        }
        else
        {
            var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
            {
                Profile = new ZplPrinterProfile { NativeBarcodeHumanReadableReserveHeightDots = 24 }
            }).Label(400, 40);

            command.Barcode(0, 0, "1234", 40, false, 2);
            Assert.NotEmpty(command.Build().GetBytes());
        }
    }

    [Theory]
    [InlineData(BarcodeType.Code39)]
    [InlineData(BarcodeType.Code93)]
    public void Zpl_native_code39_and_code93_use_host_measured_width_as_estimate(BarcodeType type)
    {
        var width = EscPosRasterCanvas.MeasureBarcodeWidth(type, "A", 2);
        var command = new ZplPrintCommand(Encoding.ASCII).Label(width, 100);
        if (type == BarcodeType.Code39)
            command.Barcode39(0, 0, "A", 10, false, 2);
        else
            command.Barcode93(0, 0, "A", 10, false, 2);
        var before = command.Build().GetBytes();

        var overflow = new ZplPrintCommand(Encoding.ASCII).Label(width - 1, 100);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (type == BarcodeType.Code39)
                overflow.Barcode39(0, 0, "A", 10, false, 2);
            else
                overflow.Barcode93(0, 0, "A", 10, false, 2);
        });

        Assert.Contains("估算", error.Message);
        Assert.Equal(Encoding.ASCII.GetBytes($"^PW{width - 1}\r\n^LL100\r\n"),
            overflow.Build().GetBytes());
        Assert.NotEmpty(before);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_qr_estimate_rejects_right_edge_overflow(PrintProtocol protocol)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (protocol == PrintProtocol.Tspl)
                new TsplPrintCommand(Encoding.ASCII).Size(100, 100, "dot")
                    .QrCode(99, 0, 0, 1, "A", 90, "A");
            else
                new ZplPrintCommand(Encoding.ASCII).Label(100, 100)
                    .QrCode(99, 0, "A", 1, 'L', 90);
        });

        Assert.Contains("估算", error.Message);
        Assert.Contains("二维码", error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Native_lines_and_boxes_check_stroke_and_endpoint_bounds(PrintProtocol protocol)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (protocol == PrintProtocol.Tspl)
                new TsplPrintCommand(Encoding.ASCII).Size(100, 50, "dot")
                    .Line(99, 5, 99, 10, 2);
            else
                new ZplPrintCommand(Encoding.ASCII).Label(100, 50)
                    .Line(99, 5, 99, 10, 2);
        });

        Assert.Contains("超出", error.Message);
        if (protocol == PrintProtocol.Tspl)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TsplPrintCommand(Encoding.ASCII).Size(100, 50, "dot").Box(0, 0, 100, 49));
        else
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ZplPrintCommand(Encoding.ASCII).Label(100, 50).Box(0, 0, 101, 50));
    }

    [Fact]
    public void Raster_geometry_checks_actual_bitmap_and_graphic_field_dimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TsplPrintCommand(Encoding.ASCII).Size(100, 50, "dot")
                .Bitmap(93, 0, 1, 1, 0, "00"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZplPrintCommand(Encoding.ASCII).Label(100, 50)
                .GraphicField(93, 0, 1, 1, "00"));
    }

    [Fact]
    public void Tspl_millimeter_page_size_is_converted_using_current_dpi()
    {
        var command = new TsplPrintCommand(Encoding.ASCII, new TsplMediaOptions { Dpi = 203 })
            .Size(10, 10, "mm");

        Assert.Throws<ArgumentOutOfRangeException>(() => command.FillRectangle(72, 0, 8, 1));
    }

    [Fact]
    public void Tspl_reference_and_direction_are_applied_to_page_bounds()
    {
        var referenced = new TsplPrintCommand(Encoding.ASCII).Size(100, 50, "dot").Reference(10, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => referenced.FillRectangle(90, 0, 1, 1));

        var reversed = new TsplPrintCommand(Encoding.ASCII).Size(100, 50, "dot").Direction(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => reversed.FillRectangle(0, 0, 1, 51));
    }

    [Fact]
    public void Zpl_inverted_page_orientation_accepts_exact_edge_and_rejects_overflow()
    {
        var command = new ZplPrintCommand(Encoding.ASCII,
            new ZplMediaOptions { Orientation = PrintOrientation.Down }).Label(100, 60);

        command.FillRectangle(0, 0, 100, 60);
        Assert.Throws<ArgumentOutOfRangeException>(() => command.FillRectangle(0, 0, 101, 60));
    }

    [Fact]
    public void Direct_commands_without_size_keep_legacy_unbounded_behavior()
    {
        var tspl = new TsplPrintCommand(Encoding.ASCII).Text(1000, 1000, "outside");
        var zpl = new ZplPrintCommand(Encoding.ASCII).Text(1000, 1000, "outside");

        Assert.NotEmpty(tspl.Build().GetBytes());
        Assert.NotEmpty(zpl.Build().GetBytes());
    }
}
