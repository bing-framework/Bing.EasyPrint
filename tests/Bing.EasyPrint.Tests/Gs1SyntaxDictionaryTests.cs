using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证固定版本 GS1 Syntax Dictionary 的覆盖、来源与字段校验。</summary>
public sealed class Gs1SyntaxDictionaryTests
{
    private const string ResourceSuffix =
        ".gs1-syntax-dictionary-d1702ffb2fa5060f8460f7d581b01e377deaa1d6.txt";

    [Fact]
    public void Embedded_dictionary_matches_the_pinned_source_hash()
    {
        var assembly = typeof(Gs1SyntaxDictionary).GetTypeInfo().Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using var sha = SHA256.Create();
        var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();

        Assert.Equal(Gs1SyntaxDictionary.DictionarySha256, hash);
    }

    [Fact]
    public void Every_defined_ai_is_loaded_and_families_are_expanded()
    {
        Assert.Equal(536, Gs1SyntaxDictionary.Count);
        foreach (var ai in new[]
                 {
                     "00", "01", "10", "20", "30", "37", "90", "91", "99", "235", "242", "253", "255",
                     "3100", "3165", "3200", "3295", "3300", "3375", "3695", "3900", "3919", "3939",
                     "3940", "3943", "3950", "3955", "400", "403", "410", "421", "423", "427", "4300",
                     "4326", "4330", "7001", "7040", "7041", "710", "716", "7230", "7259", "8001",
                     "8009", "8019", "8026", "8030", "8112", "8200",
                 })
        {
            Assert.True(Gs1SyntaxDictionary.TryGet(ai, out _), $"missing AI {ai}");
        }

        Assert.False(Gs1SyntaxDictionary.TryGet("3960", out _));
        Assert.False(Gs1SyntaxDictionary.TryGet("311", out _));
        Assert.False(Gs1SyntaxDictionary.TryGet("9999", out _));
    }

    [Theory]
    [InlineData("10", "LOT-1")]
    [InlineData("240", "PART-1")]
    [InlineData("3100", "000123")]
    [InlineData("3910", "8401")]
    [InlineData("3943", "1234")]
    [InlineData("3955", "123456")]
    [InlineData("7001", "1234567890123")]
    [InlineData("8001", "00010000100110")]
    [InlineData("8030", "AQID")]
    [InlineData("8200", "https://example.test/product")]
    public void Representative_positive_values_normalize(string ai, string value)
    {
        var expected = ai + value;
        Assert.Equal(expected, Normalize($"({ai}){value}"));
    }

    [Theory]
    [InlineData("10", "A B")]
    [InlineData("3100", "00123")]
    [InlineData("3100", "00123A")]
    [InlineData("3910", "0001")]
    [InlineData("3943", "123")]
    [InlineData("3955", "1234567")]
    [InlineData("3960", "1")]
    [InlineData("7001", "123456789012A")]
    [InlineData("8001", "00000000100110")]
    [InlineData("8030", "A+B")]
    [InlineData("4307", "ZZ")]
    [InlineData("4326", "260230")]
    [InlineData("4325", "2601319999")]
    [InlineData("8008", "2601012460")]
    [InlineData("8008", "2601012360")]
    [InlineData("7041", "ZZZ")]
    [InlineData("8112", "2123456")]
    public void Representative_negative_values_are_rejected(string ai, string value)
    {
        var error = Assert.Throws<ArgumentException>(() => Normalize($"({ai}){value}"));
        Assert.Contains(ai, error.Message);
    }

    [Theory]
    [InlineData("(01)12345678901231(10)LOT-A(17)260101", "011234567890123110LOT-A\u001D17260101")]
    [InlineData("(01)12345678901231(3103)001234(17)260101",
        "0112345678901231310300123417260101")]
    [InlineData("(01)12345678901231(7030)840ABCDEFG(10)LOT",
        "01123456789012317030840ABCDEFG\u001D10LOT")]
    [InlineData("(00)123456789012345675(4302)STREET(4303)UNIT(4312)ROAD(4313)LEVEL",
        "001234567890123456754302STREET\u001D4303UNIT\u001D4312ROAD\u001D4313LEVEL")]
    public void Mixed_ai_values_use_dictionary_fnc1_boundaries(string input, string expected)
    {
        Assert.Equal(expected, Normalize(input));
    }

    [Theory]
    [InlineData("(01)12345678901230")]
    [InlineData("(414)1234567890127")]
    [InlineData("(8017)123456789012345678")]
    [InlineData("(8006)123456789012300102")]
    [InlineData("(253)1234567890127")]
    [InlineData("(255)1234567890128123A")]
    public void Required_check_digits_and_component_rules_are_enforced(string input)
    {
        Assert.Throws<ArgumentException>(() => Normalize(input));
    }

    [Fact]
    public void Raw_normalized_input_remains_compatible()
    {
        const string raw = "011234567890123110LOT-A\u001D17260101";
        Assert.Equal(raw, EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Gs1DataBarExpanded, raw));
        Assert.Equal(raw, Gs1ApplicationIdentifierParser.NormalizeForProtocol(
            "(01)12345678901231(10)LOT-A(17)260101", "data", 255));
    }

    [Theory]
    [InlineData("(01)12345678901231(7001)1234567890123(10)LOT",
        "011234567890123170011234567890123\u001D10LOT")]
    [InlineData("(01)12345678901231(402)12345678901234560(10)LOT",
        "011234567890123140212345678901234560\u001D10LOT")]
    [InlineData("(00)123456789012345675(4307)AU(10)LOT",
        "001234567890123456754307AU\u001D10LOT")]
    [InlineData("(00)123456789012345675(4325)2601312359(10)LOT",
        "0012345678901234567543252601312359\u001D10LOT")]
    [InlineData("(01)12345678901231(8111)0500(10)LOT",
        "011234567890123181110500\u001D10LOT")]
    [InlineData("(01)12345678901231(10)LOT", "011234567890123110LOT")]
    public void Fixed_length_ais_without_predefined_flag_require_fnc1_before_following_elements(
        string input, string expected)
    {
        Assert.Equal(expected, Normalize(input));
    }

    [Fact]
    public void Last_element_never_appends_a_trailing_fnc1()
    {
        Assert.Equal("011234567890123170011234567890123", Normalize("(01)12345678901231(7001)1234567890123"));
        Assert.Equal("011234567890123110LOT", Normalize("(01)12345678901231(10)LOT"));
    }

    public static IEnumerable<object[]> OfficialFnc1Flags() => Gs1OfficialFnc1Flags.All();

    [Theory]
    [MemberData(nameof(OfficialFnc1Flags))]
    public void Requires_separator_matches_official_syntax_engine_aitable(string ai, bool requiresSeparator)
    {
        Assert.True(Gs1SyntaxDictionary.TryGet(ai, out var definition), $"missing official AI {ai}");
        Assert.Equal(requiresSeparator, definition.RequiresSeparator);
    }

    [Fact]
    public void Official_fnc1_table_covers_every_loaded_ai()
    {
        Assert.Equal(Gs1SyntaxDictionary.Count, Gs1OfficialFnc1Flags.All().Count());
    }

    // Vectors selected from the official GS1 Syntax Dictionary linter unit tests at
    // d1702ffb2fa5060f8460f7d581b01e377deaa1d6 and the GS1 Syntax Engine JavaScript
    // tests at 9ae7fae0d43a731b6e22005d482f81482e092499. Expected accept/reject is
    // taken from the official sources, not from Bing.EasyPrint output.
    [Theory]
    [InlineData("(01)12345678901231", true)]
    [InlineData("(01)12345678901232", false)]
    [InlineData("(8013)1987654Ad4X4bL5ttr2310c2K", true)]
    [InlineData("(8013)1987654Ad4X4bL5ttr2310cXK", false)]
    [InlineData("(11)250606", true)]
    [InlineData("(11)251306", false)]
    [InlineData("(7006)250606", true)]
    [InlineData("(7006)250006", false)]
    [InlineData("(8008)2601012359", true)]
    [InlineData("(8008)2601012400", false)]
    [InlineData("(8008)2601012360", false)]
    [InlineData("(7003)2601012359", true)]
    [InlineData("(7003)2601012460", false)]
    [InlineData("(422)840", true)]
    [InlineData("(422)999", false)]
    [InlineData("(00)123456789012345675(4307)US", true)]
    [InlineData("(00)123456789012345675(4307)AA", false)]
    [InlineData("(3910)8401", true)]
    [InlineData("(3910)0001", false)]
    [InlineData("(7252)1", true)]
    [InlineData("(7252)3", false)]
    [InlineData("(8007)FR7630006000011234567890189", true)]
    [InlineData("(8007)FR7630006000011234567890188", false)]
    [InlineData("(8006)123456789012310102", true)]
    [InlineData("(8006)123456789012310002", false)]
    [InlineData("(7041)1A", true)]
    [InlineData("(7041)ZZZ", false)]
    [InlineData("(8110)012345612345611110123", true)]
    [InlineData("(8110)01234561234561111012a", false)]
    [InlineData("(8112)001234561234560123456", true)]
    [InlineData("(8112)201234561234560123456", false)]
    [InlineData("(00)123456789012345675(4300)A", true)]
    [InlineData("(00)123456789012345675(4300)%0G", false)]
    [InlineData("(253)1234567890128ABC", true)]
    [InlineData("(253)1234567890128ABC@", false)]
    [InlineData("(01)12345678901231(423)840", true)]
    [InlineData("(01)12345678901231(423)840999", false)]
    [InlineData("(9999)1", false)]
    public void Official_linter_and_engine_vectors_compare_acceptance(string input, bool valid)
    {
        if (valid)
        {
            Assert.False(string.IsNullOrEmpty(Normalize(input)));
            return;
        }
        var error = Assert.Throws<ArgumentException>(() => Normalize(input));
        Assert.Contains("GS1", error.Message);
    }

    [Theory]
    [InlineData("(01)12312312312319(99)TESTING123", "011231231231231999TESTING123")]
    [InlineData("(02)12312312312319", "0212312312312319")]
    public void Official_engine_normalized_element_strings_match(string input, string expected)
    {
        Assert.Equal(expected, Normalize(input));
    }

    [Fact]
    public void Official_fixed_non_predefined_fnc1_reaches_gs1_128_expanded_and_composite()
    {
        const string input = "(01)12345678901231(7001)1234567890123(10)LOT";
        const string normalized = "011234567890123170011234567890123\u001D10LOT";

        Assert.Equal(normalized, Normalize(input));
        Assert.Equal(normalized, EscPosRasterCanvas.NormalizeBarcodeData(BarcodeType.Gs1DataBarExpanded, input));

        var native = new EscPosPrintCommand(Encoding.ASCII).BarcodeUccEan128(input, 40, 2, 0).Build().GetBytes();
        Assert.True(ContainsSequence(native, Encoding.ASCII.GetBytes(normalized)));

        var tspl = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, input, 40, 0, 0, 2, 2).Build().GetBytes());
        Assert.Contains("011234567890123170011234567890123!10210LOT", tspl);

        var zpl = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII).Start()
            .BarcodeUccEan128(10, 20, input, 40, false, 2).End().Build().GetBytes());
        Assert.Contains("^FD>;>8011234567890123170011234567890123>810LOT^FS", zpl);

        var composite = new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarExpanded,
            input, "[20]12", 40, 2, Gs1CompositeMode.CcA);
        Assert.NotNull(Gs1CompositeEncoder.Encode(composite));
    }

    private static bool ContainsSequence(byte[] bytes, byte[] sequence)
    {
        for (var start = 0; start <= bytes.Length - sequence.Length; start++)
            if (bytes.Skip(start).Take(sequence.Length).SequenceEqual(sequence)) return true;
        return false;
    }

    private static string Normalize(string input)
        => Gs1ApplicationIdentifierParser.NormalizeForProtocol(input, "input", 255);
}
