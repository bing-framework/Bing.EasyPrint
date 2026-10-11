using System;
using System.Text;
using Bing.EasyPrint.CPCL;
using Xunit;

namespace Bing.EasyPrint.Tests.CPCL;

/// <summary>
/// CPCL 受限原始文本命令测试。
/// </summary>
public sealed class CPCLRestrictedRawTests
{
    [Fact]
    public void Complete_program_preserves_original_text_and_bytes()
    {
        const string raw = "! 0 200 200 120 1\r\n" +
                           "PAGE-WIDTH 240\r\n" +
                           "T 0 0 10 10 HELLO PRINT\r\n" +
                           "L 0 40 200 40 1\r\n" +
                           "PRINT\r\n";
        var command = new CPCLPrintCommand(Encoding.ASCII);

        var restricted = Assert.IsAssignableFrom<IRestrictedRawPrintCommand<CPCLPrintCommand>>(command);
        restricted.WriteRestrictedRaw(raw);

        var writer = command.Build();
        Assert.Equal(raw, writer.ToString());
        Assert.Equal(Encoding.ASCII.GetBytes(raw), writer.GetBytes());
        Assert.Equal(writer.GetBytes(), command.Build().GetBytes());
    }

    [Fact]
    public void Supported_graphics_barcodes_and_blocks_are_accepted()
    {
        const string raw = "! 0 200 200 160 1\r\n" +
                           "PAGE-WIDTH 240\r\n" +
                           "T 0 0 0 0 TEXT\r\n" +
                           "BOX 0 10 100 30 1\r\n" +
                           "B 128 1 2 20 0 35 ABC123\r\n" +
                           "B QR 0 60 M 2 U 2\r\n" +
                           "MA,ABC123\r\n" +
                           "ENDQR\r\n" +
                           "B PDF-417 0 90 XD 2 YD 4 C 3 S 1\r\n" +
                           "PAYLOAD\r\n" +
                           "ENDPDF\r\n" +
                           "EG 1 1 0 100 FF\r\n" +
                           "CG 1 1 8 100 A\r\n" +
                           "PRINT\r\n";
        var command = new CPCLPrintCommand(Encoding.ASCII);

        command.WriteRestrictedRaw(raw);

        Assert.Equal(raw, command.Build().ToString());
    }

    [Fact]
    public void Body_only_raw_uses_existing_structured_page_and_preserves_body()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        command.SetPage(200, 100);

        command.WriteRestrictedRaw("T 0 0 10 10 BODY\r\n");

        var output = command.Build().ToString();
        Assert.Contains("! 0 200 200 100 1\r\n", output);
        Assert.Contains("PAGE-WIDTH 200\r\nT 0 0 10 10 BODY\r\nPRINT\r\n", output);
    }

    [Fact]
    public void Body_only_raw_requires_crlf_before_the_generated_print_command()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(200, 100);
        var before = command.Build().GetBytes();

        var error = Assert.Throws<ArgumentException>(() =>
            command.WriteRestrictedRaw("T 0 0 10 10 BODY"));

        Assert.Contains("CPCL", error.Message);
        Assert.Contains("CRLF", error.Message);
        Assert.Equal(before, command.Build().GetBytes());

        const string first = "T 0 0 10 10 FIRST\r\n";
        const string second = "T 0 0 10 20 SECOND\r\n";
        command.WriteRestrictedRaw(first);
        command.WriteRestrictedRaw(second);

        Assert.EndsWith("PAGE-WIDTH 200\r\n" + first + second + "PRINT\r\n",
            command.Build().ToString());
    }

    [Fact]
    public void Multiline_geometry_uses_glyph_height_and_rejects_inline_first_line_data()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        command.SetPage(200, 100);

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "ML 1\r\nT 55 0 0 90 INLINE\r\nSECOND\r\nENDML\r\n"));
        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "ML 1\r\nT 55 0 0 90\r\nFIRST\r\nSECOND\r\nENDML\r\n"));

        command.WriteRestrictedRaw(
            "ML 20\r\nT 55 0 0 20\r\nFIRST\r\nSECOND\r\nENDML\r\n");

        Assert.Contains("SECOND\r\nENDML\r\n", command.Build().ToString());
    }

    [Fact]
    public void Missing_dimensions_are_rejected_without_mutating_output()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        command.WriteRaw("before");
        var before = command.Build().GetBytes();

        var exception = Assert.Throws<ArgumentException>(() =>
            command.WriteRestrictedRaw("T 0 0 0 0 BODY\r\n"));

        Assert.Contains("CPCL", exception.Message);
        Assert.Contains("T", exception.Message);
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Encoding_error_reports_the_actual_command_and_line()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);

        var exception = Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "! 0 200 200 100 1\r\nT 0 0 0 0 中文\r\n"));

        Assert.Contains("T", exception.Message);
        Assert.Contains("第 2 行", exception.Message);
        Assert.Contains("CPCL", exception.Message);
    }

    [Fact]
    public void Invalid_block_graphics_and_unknown_commands_are_rejected_atomically()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        command.WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "! 0 200 200 100 1\r\nPAGE-WIDTH 200\r\n" +
            "B QR 0 0 M 2 U 2\r\nPRINT\r\n"));
        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "! 0 200 200 100 1\r\nPAGE-WIDTH 200\r\n" +
            "EG 1 1 0 0 GG\r\nPRINT\r\n"));
        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "! 0 200 200 100 1\r\nPAGE-WIDTH 200\r\nVENDOR-COMMAND 1\r\nPRINT\r\n"));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Full_program_cannot_be_mixed_with_existing_commands()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        command.WriteRaw("before");

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "! 0 200 200 100 1\r\nPAGE-WIDTH 200\r\nPRINT\r\n"));
    }

    [Fact]
    public void Supported_maxicode_and_aztec_blocks_use_conservative_page_extents()
    {
        const string maxicode = "! 0 200 200 300 1\r\n" +
                                "PAGE-WIDTH 300\r\n" +
                                "B MAXICODE 0 0\r\n" +
                                "POST 555555555\r\n" +
                                "CC 840\r\n" +
                                "ENDMAXICODE\r\n" +
                                "PRINT\r\n";
        const string aztec = "! 0 200 200 200 1\r\n" +
                             "PAGE-WIDTH 200\r\n" +
                             "VB AZTEC 0 0 XD 1 EC 0\r\n" +
                             "ABC123\r\n" +
                             "ENDQR\r\n" +
                             "PRINT\r\n";

        var maxicodeCommand = new CPCLPrintCommand(Encoding.ASCII);
        maxicodeCommand.WriteRestrictedRaw(maxicode);
        Assert.Equal(maxicode, maxicodeCommand.Build().ToString());

        var aztecCommand = new CPCLPrintCommand(Encoding.ASCII);
        aztecCommand.WriteRestrictedRaw(aztec);
        Assert.Equal(aztec, aztecCommand.Build().ToString());
    }

    [Fact]
    public void Maxicode_requires_postal_field_and_preserves_existing_output_on_failure()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(300, 300);
        command.WriteRestrictedRaw("T 55 0 0 0 BEFORE\r\n");
        var before = command.Build().GetBytes();

        var exception = Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "B MAXICODE 0 0\r\nCC 840\r\nENDMAXICODE\r\n"));

        Assert.Contains("POST", exception.Message);
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Barcode_text_qr_and_vector_geometry_overruns_are_rejected()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        command.SetPage(100, 100);
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw("T 55 0 70 0 HELLO\r\n"));
        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "B QR 0 0 M 1 U 2\r\n" +
            "LMA,ABCDEFGHIJKLMNOPQRSTUVWXYZ1234\r\nENDQR\r\n"));
        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw("B 128 1 2 20 95 0 ABC123\r\n"));

        Assert.Equal(before, command.Build().GetBytes());
        command.WriteRestrictedRaw("T 55 0 0 0 OK\r\n");
        Assert.Contains("T 55 0 0 0 OK\r\n", command.Build().ToString());
    }

    [Fact]
    public void Restricted_program_rejects_appends_atomically_and_page_reset_recovers()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        const string raw = "! 0 200 200 100 1\r\nPAGE-WIDTH 200\r\nPRINT\r\n";
        command.WriteRestrictedRaw(raw);
        var original = command.Build().GetBytes();

        Assert.Throws<InvalidOperationException>(() => command.SetQty(2));
        Assert.Throws<InvalidOperationException>(() =>
            command.DrawText(0, 0, "ignored", FontSize.Size16, RotationAngle.None, TextStyle.None));
        Assert.Throws<InvalidOperationException>(() => command.WriteRestrictedRaw(raw));

        Assert.Equal(original, command.Build().GetBytes());

        command.SetPage(200, 100)
            .DrawText(0, 0, "recovered", FontSize.Size16, RotationAngle.None, TextStyle.None);
        Assert.Contains("recovered", command.Build().ToString());
    }

    [Fact]
    public void Raw_orientation_is_committed_for_following_restricted_calls()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII);
        command.SetPage(200, 100);

        command.WriteRestrictedRaw("R 90\r\n");

        Assert.Equal((int)PrintOrientation.Left, command.CommandInfo.PagerRotate);
        command.WriteRestrictedRaw("T 55 0 0 0 ROTATED\r\n");
        Assert.Contains("R 90\r\nT 55 0 0 0 ROTATED\r\n", command.Build().ToString());
    }

    [Fact]
    public void Structured_text_state_is_shared_with_restricted_raw_geometry()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(40, 60);
        command.SetMag(2, 2).SetSp(2);

        var exception = Assert.Throws<ArgumentException>(() =>
            command.WriteRestrictedRaw("T 55 0 10 0 ABC\r\n"));

        Assert.Contains("T", exception.Message);
        command.SetMag(0, 0).SetSp(0);
        command.WriteRestrictedRaw("T 55 0 32 0 X\r\n");
        Assert.Contains("SETMAG 2 2\r\nSETSP 2\r\n", command.Build().ToString());
        Assert.Contains("SETMAG 0 0\r\nSETSP 0\r\nT 55 0 32 0 X\r\n", command.Build().ToString());
    }

    [Fact]
    public void Vertical_magnification_is_included_in_text_height()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(40, 30);
        command.SetMag(1, 2);

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "T 55 0 0 0 X\r\n"));

        command.SetMag(0, 0);
        command.WriteRestrictedRaw("T 55 0 0 10 X\r\n");
    }

    [Fact]
    public void Restricted_format_state_persists_and_structured_changes_apply_after_it()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(100, 100);
        command.WriteRestrictedRaw("BT 24 0 0\r\nSETMAG 2 2\r\nSETSP 2\r\nRIGHT 10\r\n");

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "T 55 0 0 0 XX\r\n"));

        command.Left().SetMag(0, 0).SetSp(0);
        command.WriteRestrictedRaw("T 55 0 90 0 X\r\n");
        Assert.Contains("RIGHT 10\r\n", command.Build().ToString());
        Assert.Contains("LEFT\r\nSETMAG 0 0\r\nSETSP 0\r\nT 55 0 90 0 X\r\n",
            command.Build().ToString());
    }

    [Fact]
    public void Barcode_text_format_persists_across_restricted_and_structured_calls()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(100, 100);
        command.WriteRestrictedRaw("BT 24 0 0\r\n");

        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "B EAN8 1 2 20 20 0 1234567\r\n"));

        command.BarcodeTextOff();
        command.WriteRestrictedRaw("B EAN8 1 2 20 20 0 1234567\r\n");
        Assert.Contains("BT 24 0 0\r\nBT OFF\r\nB EAN8 1 2 20 20 0 1234567\r\n",
            command.Build().ToString());
    }

    [Fact]
    public void Alignment_state_changes_text_footprint_and_opaque_raw_requires_reset()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(40, 60);
        command.Center(30);
        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "T 55 0 15 0 XX\r\n"));

        command.Left();
        command.WriteRestrictedRaw("T 55 0 15 0 XX\r\n");

        command.WriteRaw("opaque CPCL extension");
        Assert.Throws<ArgumentException>(() => command.WriteRestrictedRaw(
            "T 55 0 0 0 X\r\n"));
        command.Rotate(0).SetMag(0, 0).SetSp(0).Left();
        command.WriteRestrictedRaw("T 55 0 30 0 X\r\n");

        Assert.Contains("CENTER 30\r\nLEFT\r\nT 55 0 15 0 XX\r\n", command.Build().ToString());
    }

    [Fact]
    public void Structured_text_component_leaves_default_magnification_for_restricted_raw()
    {
        var command = new CPCLPrintCommand(Encoding.ASCII).SetPage(40, 60);
        command.SetMag(2, 2).DrawText(0, 0, "component", 16, 0, false, false, false);

        command.WriteRestrictedRaw("T 55 0 32 0 X\r\n");

        Assert.Contains("SETMAG 0 0\r\nT 55 0 32 0 X\r\n", command.Build().ToString());
    }

    [Fact]
    public void Complete_program_accepts_text_blocks_font_groups_and_barcode_text_controls()
    {
        const string raw = "! 0 200 200 300 1\r\n" +
                           "PAGE-WIDTH 300\r\n" +
                           "FG 0 55 0 24 0\r\n" +
                           "ML 20\r\n" +
                           "T 55 0 5 60\r\n" +
                           "FIRST ROW\r\n" +
                           "SECOND ROW\r\n" +
                           "ENDML\r\n" +
                           "CONCAT 5 120\r\n" +
                           "55 0 0 A\r\n" +
                           "55 0 0 B\r\n" +
                           "ENDCONCAT\r\n" +
                           "BT 55 0 2\r\n" +
                           "B 128 1 2 20 5 200 ABC123\r\n" +
                           "BT OFF\r\n" +
                           "PRINT\r\n";
        var command = new CPCLPrintCommand(Encoding.ASCII);

        command.WriteRestrictedRaw(raw);

        Assert.Equal(Encoding.ASCII.GetBytes(raw), command.Build().GetBytes());
    }
}
