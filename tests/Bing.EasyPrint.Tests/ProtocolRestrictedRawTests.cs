using System;
using System.Text;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

public sealed class ProtocolRestrictedRawTests
{
    [Fact]
    public void Restricted_raw_tokenizer_budget_is_reserved_before_parsing_and_failure_is_atomic()
    {
        var cpcl = new CPCLPrintCommand(Encoding.ASCII).SetPage(200, 100);
        var escPos = new EscPosPrintCommand(Encoding.ASCII);
        var tspl = new TsplPrintCommand(Encoding.ASCII).Size(100, 80, "dot");
        var zpl = new ZplPrintCommand(Encoding.ASCII);
        var cpclBefore = cpcl.Build().GetBytes();
        var escPosBefore = escPos.Build().GetBytes();
        var tsplBefore = tspl.Build().GetBytes();
        var zplBefore = zpl.Build().GetBytes();

        using (var scope = PrintResourceScope.Begin(256))
        {
            AssertTokenizerBudgetRejected(scope, "CPCL.RestrictedRawTokenizer",
                () => cpcl.WriteRestrictedRaw("T 0 0 0 0 RAW\r\n"));
            AssertTokenizerBudgetRejected(scope, "EscPos.RestrictedRawTokenizer",
                () => escPos.WriteRestrictedRaw("RAW"));
            AssertTokenizerBudgetRejected(scope, "Tspl.RestrictedRawTokenizer",
                () => tspl.WriteRestrictedRaw("CLS\r\n"));
            AssertTokenizerBudgetRejected(scope, "Zpl.RestrictedRawTokenizer",
                () => zpl.WriteRestrictedRaw("^XA\r\n^XZ\r\n"));
        }

        Assert.Equal(cpclBefore, cpcl.Build().GetBytes());
        Assert.Equal(escPosBefore, escPos.Build().GetBytes());
        Assert.Equal(tsplBefore, tspl.Build().GetBytes());
        Assert.Equal(zplBefore, zpl.Build().GetBytes());
    }

    [Fact]
    public void Tspl_preserves_raw_bytes_and_shares_page_state_in_both_directions()
    {
        const string raw = "TEXT 2,3,\"0\",0,1,1,\"RAW\"\r\n";
        var command = new TsplPrintCommand(Encoding.ASCII).Size(100, 80, "dot");

        command.WriteRestrictedRaw(raw);
        command.Text(2, 20, "STRUCT");

        var expected = Encoding.ASCII.GetBytes(
            "SIZE 100 dot,80 dot\r\nCODEPAGE USA\r\nCLS\r\n" + raw +
            "TEXT 2,20,\"0\",0,1,1,\"STRUCT\"\r\n");
        Assert.Equal(expected, command.Build().GetBytes());

        var rawSize = "SIZE 100 dot,80 dot\r\nCODEPAGE USA\r\nCLS\r\n";
        var reverse = new TsplPrintCommand(Encoding.ASCII);
        reverse.WriteRestrictedRaw(rawSize);
        reverse.Text(2, 20, "STRUCT");
        Assert.Equal(Encoding.ASCII.GetBytes(rawSize + "TEXT 2,20,\"0\",0,1,1,\"STRUCT\"\r\n"),
            reverse.Build().GetBytes());
    }

    private static void AssertTokenizerBudgetRejected(PrintResourceScope scope, string resourceName,
        Action writeRestrictedRaw)
    {
        var before = scope.CurrentBytes;
        var error = Assert.Throws<PrintResourceLimitException>(writeRestrictedRaw);
        Assert.Equal(resourceName, error.ResourceName);
        Assert.Equal(before, scope.CurrentBytes);
    }

    [Fact]
    public void Tspl_restricted_text_preserves_quoted_whitespace()
    {
        const string raw = "TEXT 2,3,\"0\",0,1,1,\"  RAW  \"\r\n";
        var command = new TsplPrintCommand(Encoding.ASCII).Size(100, 80, "dot");

        command.WriteRestrictedRaw(raw);

        Assert.EndsWith(raw, command.Build().ToString());

        var boundary = new TsplPrintCommand(Encoding.ASCII).Size(30, 80, "dot");
        var before = boundary.Build().GetBytes();
        Assert.Throws<ArgumentException>(() => boundary.WriteRestrictedRaw(
            "TEXT 29,3,\"0\",0,1,1,\"  \"\r\n"));
        Assert.Equal(before, boundary.Build().GetBytes());
    }

    [Fact]
    public void Tspl_consecutive_restricted_raw_calls_require_a_command_delimiter()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Size(100, 80, "dot");
        const string first = "TEXT 2,3,\"0\",0,1,1,\"FIRST\"";
        command.WriteRestrictedRaw(first);
        var before = command.Build().GetBytes();

        var error = Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "TEXT 2,20,\"0\",0,1,1,\"SECOND\"\r\n"));

        Assert.Contains("TSPL", error.Message);
        Assert.Contains("分隔符", error.Message);
        Assert.Equal(before, command.Build().GetBytes());

        const string second = "\r\nTEXT 2,20,\"0\",0,1,1,\"SECOND\"\r\n";
        command.WriteRestrictedRaw(second);
        Assert.Equal(Encoding.ASCII.GetBytes("SIZE 100 dot,80 dot\r\nCODEPAGE USA\r\nCLS\r\n" + first + second),
            command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_rejects_missing_size_and_late_geometry_failure_atomically()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        var missing = Assert.Throws<ArgumentException>(() =>
            command.WriteRestrictedRaw("TEXT 1,1,\"0\",0,1,1,\"X\"\r\n"));
        Assert.Contains("TSPL", missing.Message);
        Assert.Equal(before, command.Build().GetBytes());

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "SIZE 100 dot,80 dot\r\nCODEPAGE USA\r\nCLS\r\n" +
            "BAR 99,1,2,2\r\n"));
        Assert.Equal(before, command.Build().GetBytes());
        command.WriteRestrictedRaw("SIZE 100 dot,80 dot\r\nCODEPAGE USA\r\nCLS\r\n");
        Assert.StartsWith("beforeSIZE 100 dot", command.Build().ToString());
    }

    [Fact]
    public void Tspl_keeps_unemitted_media_options_and_requires_them_before_raw_drawing()
    {
        var options = new TsplMediaOptions
        {
            GapDistance = 4,
            GapOffset = 1,
            Direction = 1,
            ReferenceX = 2,
            ReferenceY = 3,
            Speed = 5,
            Density = 7
        };
        var command = new TsplPrintCommand(Encoding.ASCII, options);
        const string size = "SIZE 100 dot,80 dot\r\nCODEPAGE USA\r\nCLS\r\n";

        command.WriteRestrictedRaw(size);
        command.Text(5, 6, "0", 0, 1, 1, "NEXT");

        Assert.Equal(Encoding.ASCII.GetBytes(size +
            "GAP 4,1\r\nDIRECTION 1\r\nREFERENCE 2,3\r\nSPEED 5\r\nDENSITY 7\r\n" +
            "TEXT 5,6,\"0\",0,1,1,\"NEXT\"\r\n"), command.Build().GetBytes());

        var strict = new TsplPrintCommand(Encoding.ASCII, options).WriteRaw("before");
        var before = strict.Build().GetBytes();
        var error = Assert.Throws<ArgumentException>(() => strict.WriteRestrictedRaw(
            size + "GAP 4,1\r\nTEXT 5,6,\"0\",0,1,1,\"RAW\"\r\n"));
        Assert.Contains("DIRECTION", error.Message);
        Assert.Equal(before, strict.Build().GetBytes());

        const string configuredRaw = size +
            "GAP 4,1\r\nDIRECTION 1\r\nREFERENCE 2,3\r\nSPEED 5\r\nDENSITY 7\r\n" +
            "TEXT 5,6,\"0\",0,1,1,\"RAW\"\r\n";
        strict.WriteRestrictedRaw(configuredRaw);
        strict.Text(5, 20, "0", 0, 1, 1, "STRUCT");
        Assert.Equal(Encoding.ASCII.GetBytes("before" + configuredRaw +
            "TEXT 5,20,\"0\",0,1,1,\"STRUCT\"\r\n"), strict.Build().GetBytes());
    }

    [Fact]
    public void Zpl_preserves_field_text_and_shares_open_format_state()
    {
        const string raw = "^PW300\r\n^LL100\r\n^XA\r\n";
        var command = new ZplPrintCommand(Encoding.ASCII);

        command.WriteRestrictedRaw(raw);
        command.Text(1, 2, "RAW ", fontHeight: 10, fontWidth: 10);
        command.End();

        var expected = Encoding.ASCII.GetBytes(raw + "^FO1,2^A0N,10,10^FDRAW ^FS\r\n^XZ\r\n");
        Assert.Equal(expected, command.Build().GetBytes());

        const string complete = "^PW300\r\n^LL100\r\n^XA\r\n^FO1,2^A0N,10,10^FDR^FS\r\n^XZ\r\n";
        var completeCommand = new ZplPrintCommand(Encoding.ASCII);
        completeCommand.WriteRestrictedRaw(complete);
        Assert.Equal(Encoding.ASCII.GetBytes(complete), completeCommand.Build().GetBytes());
    }

    [Fact]
    public void Zpl_open_raw_format_must_close_before_build_and_can_recover()
    {
        const string raw = "^PW300\r\n^LL100\r\n^XA\r\n";
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRestrictedRaw(raw);
        command.Text(1, 2, "RAW", fontHeight: 10, fontWidth: 10);

        Assert.Throws<InvalidOperationException>(() => command.Build());
        Assert.Throws<InvalidOperationException>(() => command.BuildOutput());

        command.End();
        var expected = Encoding.ASCII.GetBytes(raw + "^FO1,2^A0N,10,10^FDRAW^FS\r\n^XZ\r\n");
        Assert.Equal(expected, command.Build().GetBytes());
        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_rejects_unmatched_start_and_end_without_changing_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII);

        Assert.Throws<InvalidOperationException>(() => command.End());
        command.Start();
        Assert.Throws<InvalidOperationException>(() => command.Start());
        command.End();
        Assert.Throws<InvalidOperationException>(() => command.End());

        Assert.Equal(Encoding.ASCII.GetBytes("^XA\r\n^XZ\r\n"), command.Build().GetBytes());
    }

    [Theory]
    [InlineData("^FO1,2^A0N,10,10^FDTEXT^FS")]
    [InlineData("^PQ1")]
    [InlineData("^CN123")]
    [InlineData("^PW100\r\n^LL80\r\n^XA\r\n^FO1,2^FD^CN1^FS^XZ")]
    [InlineData("^PW100\r\n^LL80\r\n^XA\r\n^FO1,2^GFA,1,1,1,GG^FS^XZ")]
    [InlineData("^PW100\r\n^LL80\r\n^XA\r\n^FO1,2^A0N,10,10^FDTEXT^FS^XZ^XX")]
    public void Zpl_rejects_missing_dimensions_malformed_fields_and_unknown_commands_atomically(string raw)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(raw));
        Assert.Equal(before, command.Build().GetBytes());
        command.WriteRestrictedRaw("^PW100\r\n^LL80\r\n^XA\r\n^XZ\r\n");
        Assert.EndsWith("^XZ\r\n", command.Build().ToString());
    }

    [Fact]
    public void Zpl_preserves_all_structured_barcode_families_in_restricted_raw()
    {
        Action<ZplPrintCommand>[] barcodes =
        {
            command => command.Barcode(10, 20, "ABC123", 60, true, 3),
            command => command.Barcode39(10, 20, "ABC123", 60, true, 3, 0),
            command => command.BarcodeCode11(10, 20, "12345", 60, true, 3, 90),
            command => command.BarcodePlessey(10, 20, "123456", 60, true, 3, 180),
            command => command.BarcodeCode49(10, 20, "ABC49", 60, true, 3, 'A', 270),
            command => command.BarcodeGs1DataBarOmnidirectional(10, 20, "1234567890123", 60, 3),
            command => command.BarcodeGs1DataBarTruncated(10, 20, "1234567890123", 60, 3),
            command => command.BarcodeGs1DataBarLimited(10, 20, "0123456789012", 60, 3),
            command => command.BarcodeGs1DataBarExpanded(10, 20, "12{", 60, 3, 18),
            command => command.Barcode93(10, 20, "ABC93", 60, true, 3, true, 0),
            command => command.BarcodeEan8(10, 20, "96385074", 60, true, 3, 90),
            command => command.BarcodeEan13(10, 20, "5901234123457", 60, true, 3, 180),
            command => command.BarcodeUpcA(10, 20, "036000291452", 60, true, 3, false, 270),
            command => command.BarcodeUpcE(10, 20, "425261", 60, true, 3, true, 0),
            command => command.BarcodeCodabar(10, 20, "A12B", 60, true, 3, 90),
            command => command.BarcodeI2OF5(10, 20, "1234", 60, true, 3, true, 180),
            command => command.BarcodeMsi(10, 20, "12345", 60, true, 3, 'D', true, 270),
            command => command.BarcodePostnet(10, 20, "12345", 60, true, 3, 0),
            command => command.BarcodePlanet(10, 20, "123456789014", 60, true, 3, 90),
            command => command.BarcodeIntelligentMail(10, 20, "01234567094987654321", 60, false, 3, 180),
            command => command.BarcodeUccEan128(10, 20, "0101234567890128", 60, true, 3, 270)
        };

        foreach (var draw in barcodes)
        {
            var structured = new ZplPrintCommand(Encoding.ASCII).Label(3000, 1000).Start();
            draw(structured);
            structured.End();
            var expected = structured.Build().GetBytes();

            var raw = new ZplPrintCommand(Encoding.ASCII);
            raw.WriteRestrictedRaw(Encoding.ASCII.GetString(expected));

            Assert.Equal(expected, raw.Build().GetBytes());
        }
    }

    [Fact]
    public void Zpl_cutter_boundary_budget_failure_does_not_commit_raw_or_state()
    {
        const string raw = "^PW100\r\n^LL80\r\n^XA\r\n^PQ1\r\n^XZ\r\n";
        var plain = new ZplPrintCommand(Encoding.ASCII);
        long rawActionBudget;
        using (var scope = PrintResourceScope.Begin(long.MaxValue))
        {
            var before = scope.CurrentBytes;
            plain.WriteRestrictedRaw(raw);
            rawActionBudget = scope.CurrentBytes - before;
        }

        var command = new ZplPrintCommand(Encoding.ASCII, new ZplMediaOptions
        {
            Profile = new ZplPrinterProfile
            {
                Cutter = new ZplCutterProfile
                {
                    Strategy = ZplCutStrategy.Delayed,
                    SupportedCutModes = new[] { PrintCutMode.Full },
                    SupportsTearOff = true
                }
            }
        });
        using (PrintResourceScope.Begin(rawActionBudget))
        {
            Assert.Throws<PrintResourceLimitException>(() => command.WriteRestrictedRaw(raw));
        }

        Assert.Empty(command.Build().GetBytes());
        command.WriteRestrictedRaw(raw);
        Assert.Equal(2, command.BuildOutput().Segments.Count);
    }

    [Fact]
    public void Escpos_native_raw_preserves_bytes_and_shares_print_area_and_text_state()
    {
        const string raw = "\u001BE\u0001RAW\r\n";
        var command = new EscPosPrintCommand(Encoding.ASCII).SetPrintAreaWidth(120);

        command.WriteRestrictedRaw(raw);
        command.WriteText("STRUCT");

        var expected = new byte[] { 0x1D, 0x57, 120, 0 }
            .ConcatBytes(Encoding.ASCII.GetBytes(raw + "STRUCT"));
        Assert.Equal(expected, command.Build().GetBytes());
        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void Escpos_raw_alignment_does_not_reset_structured_line_width()
    {
        const string raw = "\u001Ba\u0001C";
        var command = new EscPosPrintCommand(Encoding.ASCII).SetPrintAreaWidth(24).WriteText("AB");
        var before = command.Build().GetBytes();

        var exception = Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(raw));

        Assert.Contains("可打印宽度", exception.Message);
        Assert.Equal(before, command.Build().GetBytes());
        const string validRaw = "\r\nC";
        command.WriteRestrictedRaw(validRaw);
        Assert.Equal(before.ConcatBytes(Encoding.ASCII.GetBytes(validRaw)), command.Build().GetBytes());
    }

    [Fact]
    public void Escpos_raw_tab_overflow_is_rejected_before_a_following_newline()
    {
        const string invalidRaw = "AB\t\n";
        const string validRaw = "A\t\r\n";
        var command = new EscPosPrintCommand(Encoding.ASCII, options: new EscPosDeviceOptions
        {
            NativeTabWidthDots = 24
        }).SetPrintAreaWidth(24);
        var before = command.Build().GetBytes();

        var exception = Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(invalidRaw));

        Assert.Contains("制表符位置估算", exception.Message);
        Assert.Equal(before, command.Build().GetBytes());
        command.WriteRestrictedRaw(validRaw);
        Assert.Equal(before.ConcatBytes(Encoding.ASCII.GetBytes(validRaw)), command.Build().GetBytes());
    }

    [Fact]
    public void Escpos_rejects_widthless_text_and_unsupported_binary_control_atomically()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        var width = Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw("TEXT"));
        Assert.Contains("ESC/POS", width.Message);
        Assert.Contains("宽度", width.Message);
        Assert.Equal(before, command.Build().GetBytes());

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw("\u001D\u0076\u0030"));
        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw("\u001D\u0072\u0001"));
        Assert.Equal(before, command.Build().GetBytes());
        command.SetPrintAreaWidth(120).WriteRestrictedRaw("OK\r\n");
        Assert.EndsWith("OK\r\n", command.Build().ToString());
    }

    [Fact]
    public void Escpos_initialize_clears_raw_print_area_width_state()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII,
            options: new EscPosDeviceOptions { PrintAreaWidthUnits = 30 });
        command.WriteRestrictedRaw("OK");
        command.WriteRestrictedRaw("\u001B@");
        var before = command.Build().GetBytes();

        var exception = Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw("OK"));

        Assert.Contains("GS W", exception.Message);
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Escpos_accepts_complete_native_barcode_and_qr_packets_only()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).SetPrintAreaWidth(576);
        const string barcode = "\u001DH\u0000\u001DhP\u001Dw\u0002\u001DkI\u0004AB12";
        command.WriteRestrictedRaw(barcode);
        var widthCommand = new byte[] { 0x1D, 0x57, 64, 2 };
        Assert.Equal(widthCommand.ConcatBytes(Encoding.ASCII.GetBytes(barcode)), command.Build().GetBytes());

        var qrCommand = new EscPosPrintCommand(Encoding.ASCII).SetPrintAreaWidth(576);
        var structured = new EscPosPrintCommand(Encoding.ASCII).SetPrintAreaWidth(576).QrCode("QR").Build().GetBytes();
        qrCommand.WriteRestrictedRaw(Encoding.ASCII.GetString(structured));
        Assert.Equal(widthCommand.ConcatBytes(structured), qrCommand.Build().GetBytes());
    }

    [Fact]
    public void Escpos_accepts_native_state_text_feed_barcode_qr_and_cut_commands()
    {
        var profile = new EscPosPrinterProfile
        {
            Text = new EscPosTextProfile
            {
                EncodingCodePage = Encoding.ASCII.CodePage,
                CodePageIndex = 0,
                CharacterMode = EscPosCharacterMode.SingleByte,
                CharacterRanges = new[] { new PrintCharacterRange(32, 126) }
            },
            PrintableWidthDots = 576,
            SupportedCutModes = new[] { PrintCutMode.Full }
        };
        var options = new EscPosDeviceOptions
        {
            Profile = profile,
            CodePageIndex = 0,
            PrintAreaWidthUnits = 576,
            NativeTabWidthDots = 80
        };
        var structured = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native, options)
            .Initialize()
            .SelectCodePage(0)
            .SetCharacterMode(EscPosCharacterMode.SingleByte)
            .Align(0)
            .Bold()
            .Underline()
            .Reverse()
            .TextSize(2, 2)
            .WriteText("A")
            .Tab()
            .NewLine()
            .Feed(2)
            .FeedMotionUnits(3)
            .Barcode("1234", height: 30, width: 2)
            .QrCode("QR", moduleSize: 2)
            .Cut();
        var expected = structured.Build().GetBytes();
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native, options);

        command.WriteRestrictedRaw(Encoding.ASCII.GetString(expected));

        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_accepts_media_geometry_text_barcode_qr_and_job_commands()
    {
        const string raw = "SIZE 200 dot,160 dot\r\n" +
                           "CODEPAGE USA\r\n" +
                           "CLS\r\n" +
                           "GAP 2,0\r\n" +
                           "DIRECTION 0\r\n" +
                           "REFERENCE 0,0\r\n" +
                           "SPEED 3\r\n" +
                           "DENSITY 5\r\n" +
                           "TEXT 1,1,\"0\",0,1,1,\"RAW\"\r\n" +
                           "BAR 1,20,16,2\r\n" +
                           "BOX 1,25,20,40,2\r\n" +
                           "BARCODE 1,45,\"128\",25,1,0,2,2,\"12345\"\r\n" +
                           "QRCODE 50,1,M,2,A,0,\"AB\"\r\n" +
                           "FEED 1\r\n" +
                           "PRINT 1,1\r\n" +
                           "SELFTEST\r\n";
        var command = new TsplPrintCommand(Encoding.ASCII);

        command.WriteRestrictedRaw(raw);

        Assert.Equal(Encoding.ASCII.GetBytes(raw), command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_restricted_raw_rejects_ascii_hex_bitmap_and_structured_api_emits_binary()
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Size(200, 160, "dot");
        var before = command.Build().GetBytes();

        var exception = Assert.Throws<ArgumentException>(() =>
            command.WriteRestrictedRaw("BITMAP 120,1,1,1,0,FF\r\n"));

        Assert.Contains("TSPL", exception.Message);
        Assert.Contains("BITMAP", exception.Message);
        Assert.Contains("二进制", exception.Message);
        Assert.Equal(before, command.Build().GetBytes());

        command.Bitmap(120, 1, 1, 1, 0, "FF");
        var expected = before.ConcatBytes(
            Encoding.ASCII.GetBytes("BITMAP 120,1,1,1,0,").ConcatBytes(new byte[] { 0xFF, 0x0D, 0x0A }));
        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void Zpl_accepts_media_fields_geometry_graphic_quantity_and_complete_format()
    {
        var structured = new ZplPrintCommand(Encoding.ASCII)
            .Label(500, 200)
            .Start()
            .Text(1, 1, "RAW", 10, 10)
            .Line(1, 20, 15, 20, 1)
            .FillRectangle(20, 20, 10, 10)
            .Barcode(1, 40, "ABC123", 20, false, 2)
            .QrCode(40, 40, "AB", 1, 'L')
            .GraphicField(80, 1, 1, 1, "FF")
            .Print(2)
            .End();
        var expected = structured.Build().GetBytes();
        var command = new ZplPrintCommand(Encoding.ASCII);

        command.WriteRestrictedRaw(Encoding.ASCII.GetString(expected));

        Assert.Equal(expected, command.Build().GetBytes());
    }

    [Fact]
    public void Public_restricted_raw_contract_is_independent_of_legacy_raw_api()
    {
        var command = new TsplPrintCommand(Encoding.ASCII);
        var restricted = Assert.IsAssignableFrom<IRestrictedRawPrintCommand<TsplPrintCommand>>(command);
        restricted.WriteRestrictedRaw("SELFTEST\r\n");

        Assert.Equal(Encoding.ASCII.GetBytes("SELFTEST\r\n"), command.Build().GetBytes());
    }
}

internal static class RestrictedRawTestByteExtensions
{
    internal static byte[] ConcatBytes(this byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }
}
