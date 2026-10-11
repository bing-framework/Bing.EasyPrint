using System;
using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ESC/POS Raster 的 GS1 DataBar Expanded Method 1/3/4 编码。</summary>
public sealed class EscPosRasterGs1DataBarExpandedTests
{
    [Fact]
    public void Expanded_gtin_only_pattern_matches_the_134_module_golden_symbol()
    {
        const string expected =
            "01000010100110000010111111110000101100001110111001011100011011000100101111000000001000111100001010110010100001111101100011111100001010";

        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231"));
        Assert.Equal(134, expected.Length);
        Assert.Equal("(01)12345678901231", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, "01" + "12345678901231"));
        Assert.Equal(134, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, "01" + "12345678901231", 1));
        Assert.Equal(268, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, "[01]12345678901231", 2));
    }

    [Fact]
    public void Expanded_accepts_canonical_ai_forms_with_identical_symbols()
    {
        var parenthesized = EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231");
        Assert.Equal(parenthesized, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231"));
        Assert.Equal(parenthesized, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "0112345678901231"));
    }

    [Fact]
    public void Expanded_numeric_general_field_pattern_matches_the_151_module_golden_symbol()
    {
        const string expected =
            "0101000011100100011011111111000010100100001110110001110001101100010010111100000011100011110000101011001010000111110110001111110000101110001011000001010";

        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)1234567890123112"));
        Assert.Equal(151, expected.Length);
        Assert.Equal("(01)1234567890123112", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, "(01)1234567890123112"));
        Assert.Equal(302, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, "(01)1234567890123112", 2));
    }

    [Fact]
    public void Expanded_numeric_general_field_accepts_canonical_ai_forms()
    {
        var parenthesized = EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)1234567890123112");
        Assert.Equal(parenthesized, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]1234567890123112"));
        Assert.Equal(parenthesized, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "011234567890123112"));
    }

    [Fact]
    public void Expanded_method3_metric_weight_matches_golden_symbol()
    {
        const string data = "(01)12345678901231(3103)000123";
        const string expected =
            "010100001110000110101111111100001011000010011000110100110000110001001011111100001110001100000011010100011100101100001000111111000010111000010101111001111001001000011010111111110011101";

        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data));
        Assert.Equal(183, expected.Length);
        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3103]000123"));
        Assert.Equal("(01)12345678901231(3103)000123", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data));
        Assert.Equal(366, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, data, 2));

        const string withGeneralField = "(01)12345678901231(3103)000123AB";
        const string withGeneralFieldExpected =
            "01000111011000010010111111110000101100001001100011010011000011000100101111110000111000110000001101010001110010110000100011111100001011100001010111100111100100000101101011111111001110011100011101110101";
        Assert.Equal(withGeneralFieldExpected,
            EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(withGeneralField));
        Assert.Equal("(01)12345678901231(3103)000123AB", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, withGeneralField));
    }

    [Fact]
    public void Expanded_method4_imperial_weight_matches_golden_symbols()
    {
        const string data3202 = "(01)12345678901231(3202)000123";
        const string expected3202 =
            "010010010000011011101111111100001011101100001000010100110000110001001011111100001110001100000011010100011100101100001000111111000010111000010101111001111001001000011010111111110011101";
        const string data3203 = "(01)12345678901231(3203)000123";
        const string expected3203 =
            "010110000011010001101111111100001011101100001000010100110000110001001011111100001110001100000011010100011100101100001000111111000010110011010011110001111001001000011010111111110011101";

        Assert.Equal(expected3202, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data3202));
        Assert.Equal(expected3203, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data3203));
        Assert.Equal(expected3202, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3202]000123"));
        Assert.Equal(expected3203, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3203]000123"));
        Assert.Equal("(01)12345678901231(3202)000123", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data3202));
        Assert.Equal("(01)12345678901231(3203)000123", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data3203));
        Assert.Equal(366, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, data3202, 2));
        Assert.Equal(366, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, data3203, 2));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231(3202)009999"));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231(3203)022767"));

        const string withGeneralField = "(01)12345678901231(3203)000123AB";
        const string withGeneralFieldExpected =
            "01001100010111000010111111110000101110110000100001010011000011000100101111110000111000110000001101010001110010110000100011111100001011001101001111000111100100000101101011111111001110011100011101110101";
        Assert.Equal(withGeneralFieldExpected,
            EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(withGeneralField));
        Assert.Equal("(01)12345678901231(3203)000123AB", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, withGeneralField));
    }

    [Fact]
    public void Expanded_method7_and_method8_weight_date_symbols_match_golden_symbols()
    {
        const string data3103 = "(01)12345678901231(3103)032768";
        const string expected3103 =
            "0100010000110011011011111111000010100010111100001001100111110101100010111110000001100111001001111011001101111001011110001111110000101111011101001000011101001101111000101111111100111000100110011100110101100010000000100011110000001010";
        const string data3202 = "(01)12345678901231(3202)010000";
        const string expected3202 =
            "0100011100001010011011111111000010110110001100001001100111110101100010111110000001100111001001111011001101111001011110001111110000101111010011011000011100011011110100101111111100111000100110011100110101100010000000100011110000001010";

        Assert.Equal(expected3103, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data3103));
        Assert.Equal(expected3202, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data3202));
        Assert.Equal(expected3103, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3103]032768"));
        Assert.Equal(expected3202, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3202]010000"));
        Assert.Equal("(01)12345678901231(3103)032768", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data3103));
        Assert.Equal("(01)12345678901231(3202)010000", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data3202));
        Assert.Equal(464, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, data3103, 2));
        Assert.Equal(464, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, data3202, 2));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231(3102)000123"));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231(3204)000123"));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231(3109)099999"));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231(3209)099999"));
    }

    [Fact]
    public void Expanded_method9_to_method14_date_symbols_match_golden_symbols()
    {
        var cases = new[]
        {
            ("(01)12345678901231(3103)032768(11)240101", "0100011000010011101011111111000010100010111100001001100111110101100010111110000001100111001001111011001101111001011110001111110000101111011101001000011101001101111000101111111100111010011111000111010001000110000010100011110000001010"),
            ("(01)12345678901231(3202)010000(11)240101", "0100010000100111101011111111000010110110001100001001100111110101100010111110000001100111001001111011001101111001011110001111110000101111010011011000011100011011110100101111111100111010011111000111010001000110000010100011110000001010"),
            ("(01)12345678901231(3103)000123(13)240229", "0100010000110100111011111111000010111001101000001001100111110101100010111110000001100111001001111011001101111001011110001111110000101011100101111000011101110001011100101111111100111001001111101110010111001111101001100011110000001010"),
            ("(01)12345678901231(3203)000123(13)240229", "0100001011101000011011111111000010100110100000111001100111110101100010111110000001100111001001111011001101111001011110001111110000101011100101111000011101110001011100101111111100111001001111101110010111001111101001100011110000001010"),
            ("(01)12345678901231(3103)000123(15)991231", "0100010011011100001011111111000010110100110000011001100111110101100010111110000001100111001001111011001101111001011110001111110000101011100101111000011101110001011100101111111100111000010110011100110011100110111110100011110000001010"),
            ("(01)12345678901231(3204)000123(15)991231", "0100010010110000111011111111000010100000101111010001100111110101100010111110000001100111001001111011001101111001011110001111110000101011011111001000011110010100110000101111111100111000010110011100110011100110111110100011110000001010"),
            ("(01)12345678901231(3102)000123(17)240101", "0100001010111100001011111111000010110000110011010001100111110101100010111110000001100111001001111011001101111001011110001111110000101110111001011000010011100000101000101111111100111010011111000111010001000110000010100011110000001010"),
            ("(01)12345678901231(3209)000123(17)240101", "0100001010011100011011111111000010111000110001010001100111110101100010111110000001100111001001111011001101111001011110001111110000101000101111101111010010000011011000101111111100111010011111000111010001000110000010100011110000001010")
        };

        foreach (var item in cases)
        {
            Assert.Equal(item.Item2, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(item.Item1));
            Assert.Equal(464, EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarExpanded, item.Item1, 2));
        }

        Assert.Equal(cases[0].Item2, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3103]032768[11]240101"));
        Assert.Equal("(01)12345678901231(3103)032768(11)240101",
            EscPosRasterCanvas.HumanReadableBarcodeData(BarcodeType.Gs1DataBarExpanded, cases[0].Item1));
        Assert.Equal("(01)12345678901231(3203)000123(13)240229",
            EscPosRasterCanvas.HumanReadableBarcodeData(BarcodeType.Gs1DataBarExpanded, cases[3].Item1));
    }

    [Fact]
    public void Expanded_method2_fnc1_data_uses_general_field_and_raster_path()
    {
        const string data = "10ABC\u001D17240101";
        var pattern = EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data);

        const string expected =
            "010010011010000011101111111100001010100010000011000101111110011110101011111100001110011100111110010100101110011111011000111111000010101100001000010001111010101111110010111111110011101";
        Assert.Equal(expected, pattern);
        Assert.Equal(data, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data));
        Assert.Equal(pattern.Length, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, data, 1));

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(500, 120)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0, data, 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();
        Assert.Contains(bytes, value => value != 0);
    }

    [Fact]
    public void Expanded_method2_bracketed_ai_forms_normalize_to_the_same_fnc1_symbol()
    {
        const string parenthesized = "(10)ABC(17)240101";
        const string bracketed = "[10]ABC[17]240101";
        const string normalized = "10ABC\u001D17240101";
        const string expected =
            "010010011010000011101111111100001010100010000011000101111110011110101011111100001110011100111110010100101110011111011000111111000010101100001000010001111010101111110010111111110011101";

        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.Gs1DataBarExpanded, parenthesized));
        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.Gs1DataBarExpanded, bracketed));
        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(parenthesized));
        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(bracketed));
        Assert.Equal(parenthesized, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, parenthesized));
        Assert.Equal(bracketed, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, bracketed));
        Assert.Equal(expected.Length, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, parenthesized, 1));
        Assert.Equal(expected.Length, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, bracketed, 1));

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(500, 120)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0, bracketed, 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();
        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Fact]
    public void Expanded_bracketed_gtin_with_document_ai_preserves_the_hri_input()
    {
        const string input = "(01)12345678901231(253)1234567890128DOC-A(17)260101";
        const string normalized = "01123456789012312531234567890128DOC-A\u001D17260101";

        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.Gs1DataBarExpanded, input));
        Assert.Equal(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(normalized),
            EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(input));
        Assert.Equal(input, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, input));
    }

    [Fact]
    public void Expanded_method2_bracketed_ai_value_decodes_literal_parenthesis_and_hri()
    {
        const string bracketed = "(10)AB\\(C)D(17)240101";
        const string normalized = "10AB(C)D\u001D17240101";

        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.Gs1DataBarExpanded, bracketed));
        Assert.Equal(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(normalized),
            EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(bracketed));
        Assert.Equal("(10)AB(C)D(17)240101", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, bracketed));
    }

    [Fact]
    public void Raster_gs1_128_decodes_literal_parenthesis_from_single_ai()
    {
        const string bracketed = "(91)AB\\(C)D";
        const string normalized = "91AB(C)D";
        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.UccEan128, bracketed));

        var escapedBytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(320, 100)
            .RasterBarcode(BarcodeType.UccEan128, 0, 0, bracketed, 40, 2)
            .EndRasterPage()
            .Build().GetBytes();
        var normalizedBytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(320, 100)
            .RasterBarcode(BarcodeType.UccEan128, 0, 0, normalized, 40, 2)
            .EndRasterPage()
            .Build().GetBytes();

        Assert.Equal(normalizedBytes, escapedBytes);
    }

    [Fact]
    public void Expanded_method2_bracketed_fixed_or_variable_last_ai_does_not_need_an_explicit_fnc1()
    {
        Assert.Equal("17240101", EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.Gs1DataBarExpanded, "(17)240101"));
        Assert.Equal("10ABC", EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.Gs1DataBarExpanded, "[10]ABC"));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern("(17)240101"));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern("[10]ABC"));
    }

    [Theory]
    [InlineData("(18)123")]
    [InlineData("[17]24010")]
    [InlineData("(11)24A101")]
    [InlineData("(10)ABC[17]240101")]
    [InlineData("(10)ABC[17)240101")]
    [InlineData("[10]ABC[17]240101\u001D")]
    public void Expanded_method2_bracketed_ai_validation_fails_before_matrix_budget(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarExpanded, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Expanded_date_methods_accept_a_general_field_after_the_date()
    {
        const string method7 = "(01)12345678901231(3103)032768(11)240101AB";
        const string method10 = "(01)12345678901231(3203)000123(13)240229AB";
        const string escapedDateTail = "(01)12345678901231(3103)032768(11)240101(91)AB\\(C)D";

        var method7Pattern = EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(method7);
        var method10Pattern = EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(method10);

        const string expectedMethod7 =
            "010101100011111000101111111100001010001011110000100110011111010110001011111000000110011100100111101100110111100101111000111111000010111101110100100001110100110111100010111111110011101001111100011101000100001000010110001111000000101100011100010001010";
        const string expectedMethod10 =
            "010000110011100010101111111100001010011010000011100110011111010110001011111000000110011100100111101100110111100101111000111111000010101110010111100001110111000101110010111111110011100100111110111001011110010111100110001111000000101100011100010001010";
        Assert.Equal(expectedMethod7, method7Pattern);
        Assert.Equal(expectedMethod10, method10Pattern);
        Assert.Equal(method7Pattern, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3103]032768[11]240101AB"));
        Assert.Equal(method10Pattern, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3203]000123[13]240229AB"));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(escapedDateTail));
        Assert.Equal("(01)12345678901231(3103)032768(11)240101(91)AB(C)D",
            EscPosRasterCanvas.HumanReadableBarcodeData(BarcodeType.Gs1DataBarExpanded, escapedDateTail));
        Assert.Equal(method7, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, method7));
        Assert.Equal(method10, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, method10));
        const string fnc1Tail = "(01)12345678901231(3203)000123(13)240229AB\u001D12";
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(fnc1Tail));
        Assert.Equal(fnc1Tail, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, fnc1Tail));
        Assert.Equal(method7Pattern.Length, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, method7, 1));
        Assert.Equal(method10Pattern.Length, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, method10, 1));

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(1200, 160)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0, method7, 40, 2)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 600, 0, method10, 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();
        Assert.Contains(bytes, value => value != 0);
    }

    [Fact]
    public void Expanded_date_methods_normalize_bracketed_ai_tails_to_the_same_fnc1_field()
    {
        const string parenthesized = "(01)12345678901231(3203)000123(13)240229(10)AB(20)12";
        const string bracketed = "[01]12345678901231[3203]000123[13]240229[10]AB[20]12";
        const string rawTail = "(01)12345678901231(3203)000123(13)24022910AB\u001D2012";

        var expected = EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(rawTail);
        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(parenthesized));
        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(bracketed));
        Assert.Equal(parenthesized, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, parenthesized));
        Assert.Equal(bracketed, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, bracketed));
        Assert.Equal(expected.Length, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, parenthesized, 1));
        Assert.Equal(expected.Length, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, bracketed, 1));

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(800, 160)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0, bracketed, 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();
        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Theory]
    [InlineData("(01)12345678901231(3203)000123(13)240229(17)240101")]
    [InlineData("(01)12345678901231(3203)000123(13)240229(18)123")]
    [InlineData("(01)12345678901231(3203)000123(13)240229(10)")]
    [InlineData("(01)12345678901231(3203)000123(13)240229(10)AB(20)1")]
    [InlineData("(01)12345678901231(3203)000123(13)240229(10)AB[20]12")]
    [InlineData("(01)12345678901231(3203)000123(13)240229[10]AB")]
    public void Expanded_date_methods_reject_unsupported_bracketed_ai_tails_before_matrix_budget(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarExpanded, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Expanded_method5_and_method6_price_fields_match_golden_symbols()
    {
        const string data3920 = "(01)12345678901231(3920)12345";
        const string expected3920 =
            "010100001110001100101111111100001011001000001101100110011111010110001011111100001110011100100111101100110111100101111000111111000010111001101011100001110001110100100010111111110011101";
        const string data3930 = "(01)12345678901231(3930)84012345";
        const string expected3930 =
            "01000110110001100010111111110000101101100000110010011001111101011000101111110000111001110010011110110011011110010111100011111100001011101100110110000110010011000011101011111111001110100000100110111101";

        Assert.Equal(expected3920, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data3920));
        Assert.Equal(expected3930, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data3930));
        Assert.Equal(expected3920, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3920]12345"));
        Assert.Equal(expected3930, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "[01]12345678901231[3930]84012345"));
        Assert.Equal("(01)12345678901231(3920)12345", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data3920));
        Assert.Equal("(01)12345678901231(3930)84012345", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data3930));
        Assert.Equal(366, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, data3920, 2));
        Assert.Equal(400, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, data3930, 2));

        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231(3923)12345"));
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231(3933)97812345"));
    }

    [Fact]
    public void Expanded_alphanumeric_general_field_matches_golden_symbol()
    {
        const string data = "(01)12345678901231AB12";
        const string expected =
            "01001100011100001010111111110000101001000011101100011100011011000100101111110000111000111100001010110010100001111101100011111100001010100001100000010111101011111001101011111111001110010011100000100101";

        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data));
        Assert.Equal("(01)12345678901231AB12", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data));
    }

    [Fact]
    public void Expanded_isoiec_and_fnc1_general_field_matches_golden_symbol()
    {
        const string data = "(01)12345678901231ABa!\u001D1234";
        const string expected =
            "010111011001110000101111111100001010010000111011000111000110110001001011111000000110001111000010101100101000011111011000111111000010101000011000000101111010110011111010111111110011100000110010101111011100001010011110001111000000101111100011000101010";

        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data));
        Assert.Equal("(01)12345678901231ABa!\u001D1234", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, data));
    }

    [Fact]
    public void Expanded_gtin_bracketed_ai_sequence_normalizes_to_the_same_method1_field()
    {
        const string parenthesized = "(01)12345678901231(10)AB(20)12";
        const string bracketed = "[01]12345678901231[10]AB[20]12";
        const string raw = "(01)1234567890123110AB\u001D2012";

        var expected = EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(raw);
        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(parenthesized));
        Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(bracketed));
        Assert.Equal(parenthesized, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, parenthesized));
        Assert.Equal(bracketed, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, bracketed));
        Assert.Equal(expected.Length, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, parenthesized, 1));

        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(700, 140)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0, bracketed, 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();
        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Fact]
    public void Expanded_gtin_ai_value_decodes_literal_parenthesis_and_hri()
    {
        const string bracketed = "(01)12345678901231(91)AB\\(CD";
        const string normalized = "011234567890123191AB(CD";

        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.Gs1DataBarExpanded, bracketed));
        Assert.Equal(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(normalized),
            EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(bracketed));
        Assert.Equal("(01)12345678901231(91)AB(CD", EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, bracketed));
    }

    [Fact]
    public void Expanded_gtin_amount_and_discount_ai_use_generic_field_with_fnc1_boundaries()
    {
        const string bracketed =
            "(01)12345678901231(3909)1(3910)8401(3947)1234(3959)123456";
        const string normalized =
            "011234567890123139091\u001D39108401\u001D394712343959123456";

        Assert.Equal(
            EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(normalized),
            EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(bracketed));
        Assert.Equal(bracketed, EscPosRasterCanvas.HumanReadableBarcodeData(
            BarcodeType.Gs1DataBarExpanded, bracketed));
    }

    [Fact]
    public void Expanded_gtin_company_internal_ai_uses_general_field_and_respects_raster_limit()
    {
        var value = new string('A', 10);
        var bracketed = "(01)12345678901231(91)" + value;
        var normalized = "011234567890123191" + value;

        Assert.Equal(
            EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(normalized),
            EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(bracketed));

        var nativeMaximumValue = new string('A', 90);
        Assert.Equal("91" + nativeMaximumValue,
            Gs1ApplicationIdentifierParser.NormalizeForProtocol(
                "(91)" + nativeMaximumValue, nameof(value), 255));

        var rasterOverflow = "(01)12345678901231(91)" + new string('A', 60);
        var exception = Assert.Throws<ArgumentException>(() => EscPosRasterCanvas.NormalizeBarcodeData(
            BarcodeType.Gs1DataBarExpanded, rasterOverflow));
        Assert.Contains("77", exception.Message);
    }

    [Theory]
    [InlineData("(01)12345678901231(18)123")]
    [InlineData("(01)12345678901231(20)1")]
    [InlineData("(01)12345678901231(10)AB[20]12")]
    [InlineData("(01)12345678901230(10)AB")]
    public void Expanded_gtin_bracketed_ai_sequence_rejects_invalid_contract_before_matrix_budget(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarExpanded, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Expanded_general_field_accepts_every_supported_character_class()
    {
        const string characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!\"%&'()*+,-./:;<=>?_ ";
        foreach (var character in characters)
        {
            Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
                "(01)12345678901231" + character));
        }

        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            "(01)12345678901231\u001D12"));
    }

    [Fact]
    public void Expanded_numeric_general_field_supports_variable_lengths_up_to_the_method_limit()
    {
        const string gtin = "0112345678901231";
        var observedWidths = new HashSet<int>();
        for (var length = 1; length <= 58; length++)
        {
            var data = gtin + new string((char)('0' + length % 10), length);
            var pattern = EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data);
            observedWidths.Add(pattern.Length);
            Assert.Equal(pattern.Length, EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarExpanded, data, 1));
            Assert.True(pattern.Length > 134);
        }

        Assert.True(observedWidths.Count >= 4);
        Assert.Throws<ArgumentException>(() => EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(
            gtin + new string('7', 59)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678901231")]
    [InlineData("(18)12345678901231")]
    [InlineData("(01)12345678901230")]
    [InlineData("(01)1234567890123A")]
    [InlineData("(01)12345678901231#")]
    [InlineData("(01)12345678901231$")]
    [InlineData("(01)12345678901231\u001D")]
    [InlineData("(01)12345678901231\u001D\u001D1")]
    [InlineData("(01)12345678901231(3103)12345A")]
    [InlineData("(01)12345678901231(3103)12")]
    [InlineData("(01)12345678901231(3100)100000")]
    [InlineData("(01)12345678901231(3202)12A345")]
    [InlineData("(01)12345678901231(3203)12")]
    [InlineData("(01)12345678901231(3200)100000")]
    [InlineData("(01)12345678901231(3103)000123(11)")]
    [InlineData("(01)12345678901231(3103)000123(12)240101")]
    [InlineData("(01)12345678901231(3103)000123(11)240001")]
    [InlineData("(01)12345678901231(3103)000123(11)241301")]
    [InlineData("(01)12345678901231(3103)000123(11)240132")]
    [InlineData("(01)12345678901231(3103)000123(11)240101[18]AB")]
    [InlineData("(01)12345678901231(3203)000123(13)240229\u001D")]
    [InlineData("(01)12345678901231(3920)")]
    [InlineData("(01)12345678901231(3920)12A45")]
    [InlineData("(01)12345678901231(3930)840")]
    [InlineData("(01)12345678901231(3930)84A12345")]
    [InlineData("(01)12345678901231(3940)12345")]
    [InlineData("(01)12345678901231(3934)84012345")]
    [InlineData("10ABC")]
    [InlineData("10ABC\u001D")]
    [InlineData("10ABC\u001D\u001D17")]
    [InlineData("\u001D10ABC")]
    [InlineData("10ABC\u001D17\u0001")]
    public void Expanded_rejects_non_gtin_inputs_without_working_budget_pollution(string data)
    {
        using (var scope = PrintResourceScope.Begin(1024 * 1024))
        {
            Assert.ThrowsAny<ArgumentException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarExpanded, data, 2));
            Assert.Equal(0, scope.CurrentBytes);
        }
    }

    [Fact]
    public void Expanded_budget_failure_does_not_poison_the_next_measurement()
    {
        using (var scope = PrintResourceScope.Begin(1))
        {
            Assert.Throws<PrintResourceLimitException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarExpanded, "(01)12345678901231", 2));
            Assert.Equal(0, scope.CurrentBytes);
        }

        Assert.Equal(268, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, "(01)12345678901231", 2));

        using (var scope = PrintResourceScope.Begin(1))
        {
            Assert.Throws<PrintResourceLimitException>(() => EscPosRasterCanvas.MeasureBarcodeWidth(
                BarcodeType.Gs1DataBarExpanded, "(01)1234567890123112", 2));
            Assert.Equal(0, scope.CurrentBytes);
        }

        Assert.Equal(302, EscPosRasterCanvas.MeasureBarcodeWidth(
            BarcodeType.Gs1DataBarExpanded, "(01)1234567890123112", 2));
    }

    [Fact]
    public void Raster_command_writes_expanded_data_bar_through_the_shared_matrix()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(400, 120)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0,
                "(01)1234567890123112", 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Fact]
    public void Raster_command_writes_method4_expanded_data_bar_through_the_shared_matrix()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(500, 120)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0,
                "(01)12345678901231(3203)000123", 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Fact]
    public void Raster_command_writes_method5_and_method6_expanded_data_bars_through_the_shared_matrix()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(700, 160)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0,
                "(01)12345678901231(3920)12345", 40, 2)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 200, 0,
                "(01)12345678901231(3930)84012345", 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Fact]
    public void Raster_command_writes_method7_and_method8_expanded_data_bars_through_the_shared_matrix()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(1000, 160)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0,
                "(01)12345678901231(3103)032768", 40, 2)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 500, 0,
                "(01)12345678901231(3202)010000", 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Fact]
    public void Raster_command_writes_method9_to_method14_expanded_data_bars_through_the_shared_matrix()
    {
        var bytes = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster)
            .BeginRasterPage(1000, 160)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 0, 0,
                "(01)12345678901231(3103)000123(13)240229", 40, 2)
            .RasterBarcode(BarcodeType.Gs1DataBarExpanded, 500, 0,
                "(01)12345678901231(3204)000123(15)991231", 40, 2)
            .EndRasterPage()
            .Build()
            .GetBytes();

        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
        Assert.Contains(bytes, value => value != 0);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Raster_provider_renders_expanded_data_bar_in_all_orientations(RotationAngle rotation)
    {
        var document = new PrintDocument();
        document.AddPage(600, 600).Add(new PrintBarcodeElement(10, 10,
            BarcodeType.Gs1DataBarExpanded, "(01)12345678901231", 40,
            narrowWidth: 2, humanReadable: true, humanReadableReserveHeight: 20,
            rotation: rotation), "Expanded");
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.Gs1DataBarExpanded));
        Assert.True(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.Gs1DataBarExpanded));
        provider.Validate(document);
        var output = provider.Render(document)[0];

        Assert.NotEmpty(output);
        Assert.True(output.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_uses_expanded_host_matrix_and_measured_width()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "(01)12345678901231" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_numeric_general_field_and_uses_dynamic_width()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "(01)1234567890123112" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_method3_metric_weight()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "(01)12345678901231(3103)000123" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_method4_imperial_weight()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "(01)12345678901231(3203)000123" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_method6_currency_and_price()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "(01)12345678901231(3930)84012345" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_method8_weight_with_dummy_date()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "(01)12345678901231(3202)010000" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_method14_weight_and_expiration_date()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "(01)12345678901231(3209)000123(17)240101" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_method2_fnc1_data()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "10ABC\u001D17240101" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_bracketed_method2_ai_data()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "[10]ABC[17]240101" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_bracketed_gtin_ai_sequence()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1000,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField
                            {
                                Value = "[01]12345678901231[10]AB[20]12"
                            }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_a_general_field_after_a_date_method()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1200,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField { Value = "(01)12345678901231(3203)000123(13)240229AB" }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }

    [Fact]
    public void Raster_template_accepts_bracketed_ai_tail_after_a_date_method()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 1200,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow { Cells = new List<BpCell>
                {
                    new BpCell { Elements = new List<BpElement>
                    {
                        new BpBarcodeElement
                        {
                            BarcodeType = BarcodeType.Gs1DataBarExpanded,
                            NarrowModuleWidth = 2,
                            Height = 80,
                            HumanReadable = false,
                            Field = new BgField
                            {
                                Value = "[01]12345678901231[3203]000123[13]240229[10]AB[20]12"
                            }
                        }
                    } }
                } }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var page = Assert.Single(result.Pages);
        Assert.Equal(80, page.Height);
        Assert.Contains(page.Elements, element => element.Type == 4 && element.SourcePath ==
            "Rows[0].Cells[0].Elements[0]");
        Assert.True(page.Data.AsSpan().IndexOf(new byte[] { 0x1D, 0x76, 0x30 }) >= 0);
    }
}
