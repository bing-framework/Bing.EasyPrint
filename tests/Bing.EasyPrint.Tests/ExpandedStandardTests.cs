using System.Collections.Generic;
using Bing.EasyPrint.Protocols;
using Xunit;
namespace Bing.EasyPrint.Tests;

public sealed class ExpandedStandardTests
{
    [Theory]
    [InlineData("01937123456789043103-00123")]
    [InlineData("0193712345678904310A001234")]
    [InlineData("01937123456789043932ABC12345")]
    public void Legacy_raw_fields_do_not_enter_numeric_compression_with_non_numeric_values(string data)
    {
        // Raw compatibility accepts general-field text without claiming complete AI semantics.
        // Method 1 retains every character; compressed methods would silently replace its meaning.
        var normalized = EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Gs1DataBarExpanded, data);
        var method = typeof(EscPosRasterCanvas).GetMethod("EncodeGs1DataBarExpandedBinary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var binary = method.Invoke(null, new object[] { normalized, System.Threading.CancellationToken.None, false, false, 2, null });
        var bits = (string)binary.GetType().GetProperty("Bits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(binary);
        Assert.Equal('0', bits[0]); // Standalone linkage.
        Assert.Equal('1', bits[1]); // Method 1; no compressed weight/currency fields.
        Assert.NotEmpty(EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data));
    }

    // Zint 2.15.0 fixed commit 4896136bdbebb1a0908a5cc419513efa5bf4bf70,
    // backend/tests/test_rss.c test_examples cases 1, 10, 11, 15 (BSD-3-Clause).
    // Source copyright/license retained in Fixtures/Gs1CompositeStackedVectors.cs.
    public static IEnumerable<object[]> StandardVectors => new[]
    {
        new object[] { "[01]90614141000015[3202]000150", "0101100011001100001011111111000010100100010000111101110011100010100010111100000011100111010111111011010100000100000110001111110000101000000100011010010" },
        new object[] { "[01]98898765432106[3202]012345[15]991231", "01001000011000110110111111110000101110000110010100011010000001100010101111110000111010011100000010010100111110111001100011111100001011101100000100100100011110010110001011111111001110001101111010000101" },
        new object[] { "[01]90012345678908[3103]001750", "0101110010000010011011111111000010111000010011000101011110111001100010111100000011100101110001110111011110101111000110001111110000101011000010011111010" },
        new object[] { "[10]12A", "010100000110100000101111111100001010001000000010110101111100100111001011110000000010011101111111010101" },
    };

    [Theory]
    [MemberData(nameof(StandardVectors))]
    public void Standalone_matrix_matches_published_reference(string data, string expected)
        => Assert.Equal(expected, EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(data));

    [Theory]
    [InlineData("(01)12345678901231", "(01)12345678901231")]
    [InlineData("(01)09506000134352(10)LOT-A", "(01)09506000134352(10)LOT-A")]
    [InlineData("(10)123(21)456", "(10)123(21)456")]
    [InlineData("(20)12", "(20)12")]
    [InlineData("(01)93712345678904(3103)001234", "(01)93712345678904(3103)001234")]
    [InlineData("(01)93712345678904(3202)001234", "(01)93712345678904(3202)001234")]
    [InlineData("(01)93712345678904(3203)001234", "(01)93712345678904(3203)001234")]
    [InlineData("(01)93712345678904(3922)12345", "(01)93712345678904(3922)12345")]
    [InlineData("(01)93712345678904(3932)84012345", "(01)93712345678904(3932)84012345")]
    [InlineData("(01)93712345678904(3102)001234", "(01)93712345678904(3102)001234")]
    [InlineData("(01)93712345678904(3201)001234", "(01)93712345678904(3201)001234")]
    [InlineData("(01)93712345678904(3103)001234(11)270101", "(01)93712345678904(3103)001234(11)270101")]
    [InlineData("(01)93712345678904(3203)001234(11)270101", "(01)93712345678904(3203)001234(11)270101")]
    [InlineData("(01)93712345678904(3103)001234(13)270101", "(01)93712345678904(3103)001234(13)270101")]
    [InlineData("(01)93712345678904(3203)001234(13)270101", "(01)93712345678904(3203)001234(13)270101")]
    [InlineData("(01)93712345678904(3103)001234(15)270101", "(01)93712345678904(3103)001234(15)270101")]
    [InlineData("(01)93712345678904(3203)001234(15)270101", "(01)93712345678904(3203)001234(15)270101")]
    [InlineData("(01)93712345678904(3103)001234(17)270101", "(01)93712345678904(3103)001234(17)270101")]
    [InlineData("(01)93712345678904(3203)001234(17)270101", "(01)93712345678904(3203)001234(17)270101")]
    [InlineData("(01)12345678901231(3103)001234", "(01)12345678901231(3103)001234")]
    [InlineData("(01)93712345678904(3103)001234(10)A", "(01)93712345678904(3103)001234(10)A")]
    [InlineData("(01)93712345678904(3203)001234(17)270101(10)A", "(01)93712345678904(3203)001234(17)270101(10)A")]
    [InlineData("(01)12345678901231(3100)100000", "(01)12345678901231(3100)100000")]
    [InlineData("(01)12345678901231(3200)100000", "(01)12345678901231(3200)100000")]
    [InlineData("(01)93712345678904(3103)100000", "(01)93712345678904(3103)100000")]
    public void Standalone_decoder_recovers_primary_for_all_methods_and_fallbacks(string primary, string expected)
    {
        var pattern = EscPosRasterCanvas.EncodeGs1DataBarExpandedPattern(primary);
        var row = new ZXing.Common.BitArray(pattern.Length + 20);
        for (var x = 0; x < pattern.Length; x++) row[x + 10] = pattern[x] == '1';
        var result = new ZXing.OneD.RSS.Expanded.RSSExpandedReader().decodeRow(0, row, null);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Text);
        Assert.Equal(primary, EscPosRasterCanvas.HumanReadableBarcodeData(BarcodeType.Gs1DataBarExpanded, primary));
    }

}
