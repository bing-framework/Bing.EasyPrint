using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证独立协议命令的稳定字节布局。
/// </summary>
public sealed class ProtocolGoldenBytesTests
{
    [Fact]
    public void Tspl_writes_page_encoding_bitmap_copies_and_feed_in_order()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .Size(60, 40, "dot")
            .Gap(2, 1)
            .Text(10, 12, "OK")
            .Bitmap(1, 20, 1, 1, 0, new byte[] { 0xA5 })
            .Print(1, 2)
            .Feed(3)
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "SIZE 60 dot,40 dot\r\n" +
            "CODEPAGE USA\r\n" +
            "CLS\r\n" +
            "GAP 2,1\r\n" +
            "TEXT 10,12,\"0\",0,1,1,\"OK\"\r\n" +
            "BITMAP 1,20,1,1,0,")
            .Concat(new byte[] { 0xA5, 0x0D, 0x0A })
            .Concat(Encoding.ASCII.GetBytes("PRINT 1,2\r\nFEED 3\r\n"))
            .ToArray();

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Tspl_failed_bitmap_does_not_pollute_the_command()
    {
        var command = new TsplPrintCommand(Encoding.ASCII)
            .Size(40, 30, "dot");

        Assert.Throws<ArgumentException>(() => command.Bitmap(0, 0, 1, 2, 0, new byte[] { 0x80 }));

        var bytes = command.Text(1, 1, "OK").Build().GetBytes();
        Assert.Equal(Encoding.ASCII.GetBytes(
            "SIZE 40 dot,30 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "TEXT 1,1,\"0\",0,1,1,\"OK\"\r\n"), bytes);
        Assert.Equal(bytes, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_writes_barcode_and_qr_commands_with_stable_fields()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .Barcode(10, 20, "128", 60, 1, 0, 2, 3, "12345")
            .QrCode(30, 40, 2, 5, "A", "HELLO")
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "BARCODE 10,20,\"128\",60,1,0,2,3,\"12345\"\r\n" +
            "QRCODE 30,40,Q,5,A,0,\"HELLO\"\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Tspl_writes_rotated_qr_command_with_explicit_orientation()
    {
        var bytes = new TsplPrintCommand(Encoding.ASCII)
            .QrCode(30, 40, 2, 5, "A", 270, "HELLO")
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes("QRCODE 30,40,Q,5,A,270,\"HELLO\"\r\n"), bytes);
    }

    [Fact]
    public void Coordinate_protocols_write_line_and_box_commands()
    {
        var tspl = new TsplPrintCommand(Encoding.ASCII)
            .Line(2, 3, 12, 3, 2)
            .Box(4, 5, 20, 15, 1)
            .Build().GetBytes();
        Assert.Equal(Encoding.ASCII.GetBytes("BAR 2,3,11,2\r\nBOX 4,5,20,15,1\r\n"), tspl);

        var zpl = new ZplPrintCommand(Encoding.ASCII)
            .Box(4, 5, 20, 15, 1)
            .Build().GetBytes();
        Assert.Equal(Encoding.ASCII.GetBytes("^FO4,5^GB16,10,1^FS\r\n"), zpl);
    }

    [Fact]
    public void Zpl_writes_page_text_qr_image_copies_cut_and_end_in_order()
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .Label(300, 200)
            .Text(10, 20, "OK", 30, 40, "0", 90)
            .QrCode(50, 60, "Q", 3, 'H')
            .GraphicField(100, 120, 1, 1, new byte[] { 0x80 })
            .Print(2)
            .Cut()
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^PW300\r\n" +
            "^LL200\r\n" +
            "^FO10,20^A0R,30,40^FDOK^FS\r\n" +
            "^FO50,60^BQN,2,3^FDHA,Q^FS\r\n" +
            "^FO100,120^GFA,1,1,1,80^FS\r\n" +
            "^PQ2\r\n" +
            "^CN\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Zpl_writes_rotated_qr_command_with_native_orientation()
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .QrCode(50, 60, "Q", 3, 'H', 180)
            .End()
            .Build()
            .GetBytes();

        Assert.Equal(Encoding.ASCII.GetBytes(
            "^XA\r\n^FO50,60^BQI,2,3^FDHA,Q^FS\r\n^XZ\r\n"), bytes);
    }

    [Fact]
    public void Zpl_failed_image_does_not_pollute_the_command()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Start();

        Assert.Throws<ArgumentException>(() => command.GraphicField(0, 0, 1, 1, Array.Empty<byte>()));

        var bytes = command.Text(1, 2, "OK").End().Build().GetBytes();
        Assert.Equal(Encoding.ASCII.GetBytes(
            "^XA\r\n^FO1,2^A0N,30,30^FDOK^FS\r\n^XZ\r\n"), bytes);
        Assert.Equal(bytes, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_writes_barcode_with_stable_parameters()
    {
        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .Barcode(10, 20, "12345", 60, false, 3)
            .End()
            .Build()
            .GetBytes();

        var expected = Encoding.ASCII.GetBytes(
            "^XA\r\n" +
            "^FO10,20^BY3^BCN,60,N,N,N^FD12345^FS\r\n" +
            "^XZ\r\n");

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void EscPos_native_writes_text_barcode_qr_feed_and_partial_cut()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII)
            .Initialize()
            .Align(1)
            .Bold()
            .Text("Hi")
            .Bold(false)
            .TextSize(2, 3)
            .Barcode("123", 73, 40, 2, 2)
            .QrCode("Q", 4, 51)
            .Feed(2)
            .Cut(true)
            .Build()
            .GetBytes();

        var expected = new byte[]
        {
            0x1B, 0x40,
            0x1B, 0x61, 0x01,
            0x1B, 0x45, 0x01,
            (byte)'H', (byte)'i', 0x0A,
            0x1B, 0x45, 0x00,
            0x1D, 0x21, 0x12,
            0x1D, 0x48, 0x02,
            0x1D, 0x68, 0x28,
            0x1D, 0x77, 0x02,
            0x1D, 0x6B, 0x49, 0x05,
            (byte)'{', (byte)'B', (byte)'1', (byte)'2', (byte)'3',
            0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00,
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x04,
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x33,
            0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x50, 0x30, (byte)'Q',
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30,
            0x1B, 0x64, 0x02,
            0x1D, 0x56, 0x01
        };

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void EscPos_raster_writes_image_header_payload_feed_and_cut()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .Initialize()
            .RasterImage(9, 2, new byte[] { 0x80, 0x01, 0xAA, 0x55 }, true, true)
            .Feed(2)
            .Cut()
            .Build()
            .GetBytes();

        var expected = new byte[]
        {
            0x1B, 0x40,
            0x1D, 0x76, 0x30, 0x03, 0x02, 0x00, 0x02, 0x00,
            0x80, 0x01, 0xAA, 0x55,
            0x1B, 0x64, 0x02,
            0x1D, 0x56, 0x00
        };

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void EscPos_failed_raster_image_does_not_pollute_the_command()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .Initialize();

        Assert.Throws<ArgumentException>(() => command.RasterImage(9, 1, new byte[] { 0x80 }));

        var bytes = command.Text("OK").Build().GetBytes();
        Assert.Equal(new byte[] { 0x1B, 0x40, (byte)'O', (byte)'K', 0x0A }, bytes);
        Assert.Equal(bytes, command.Build().GetBytes());
    }

    [Fact]
    public void New_protocol_commands_preserve_raw_bytes_and_repeat_build_snapshot()
    {
        var tsplRaw = new byte[] { 0x00, 0xFF };
        var tspl = new TsplPrintCommand(Encoding.ASCII)
            .WriteRaw("TS")
            .WriteRawBytes(tsplRaw);
        tsplRaw[0] = 0x7F;
        var tsplExpected = new byte[] { (byte)'T', (byte)'S', 0x00, 0xFF };
        Assert.Equal(tsplExpected, tspl.Build().GetBytes());
        Assert.Equal(tsplExpected, tspl.Build().GetBytes());

        var zplRaw = new byte[] { 0x10, 0x20 };
        var zpl = new ZplPrintCommand(Encoding.ASCII)
            .WriteRaw("Z")
            .WriteRawBytes(zplRaw);
        zplRaw[1] = 0x7F;
        var zplExpected = new byte[] { (byte)'Z', 0x10, 0x20 };
        Assert.Equal(zplExpected, zpl.Build().GetBytes());
        Assert.Equal(zplExpected, zpl.Build().GetBytes());

        var escPosRaw = new byte[] { 0x1B, 0x40 };
        var escPos = new EscPosPrintCommand(Encoding.ASCII)
            .WriteRaw("E")
            .WriteRawBytes(escPosRaw);
        escPosRaw[0] = 0x7F;
        var escPosExpected = new byte[] { (byte)'E', 0x1B, 0x40 };
        Assert.Equal(escPosExpected, escPos.Build().GetBytes());
        Assert.Equal(escPosExpected, escPos.Build().GetBytes());
    }
}
