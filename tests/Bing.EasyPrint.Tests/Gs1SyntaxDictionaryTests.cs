using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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

    private static string Normalize(string input)
        => Gs1ApplicationIdentifierParser.NormalizeForProtocol(input, "input", 255);
}
