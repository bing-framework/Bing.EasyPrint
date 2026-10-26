using System;
using System.Collections.Generic;
using System.Linq;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 GS1 Digital Link URI 和固定 AI 元数据目录。</summary>
public sealed class Gs1DigitalLinkTests
{
    private const string BaseUri = "https://id.gs1.org";
    private const string Gtin = "09520123456788";

    [Fact]
    public void Build_pads_supported_gtin_inputs_and_emits_fourteen_digit_uri_key()
    {
        Assert.Equal("09520123456788", Gs1DigitalLink.NormalizeGtin("9520123456788"));
        Assert.Equal(BaseUri + "/01/09520123456788", Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
        {
            BaseUri = BaseUri,
            Gtin = "9520123456788"
        }));
    }

    [Fact]
    public void Parse_requires_fourteen_digit_uri_gtin_and_normalizes_compatibility_trailing_slash()
    {
        var shortGtin = Assert.Throws<Gs1DigitalLinkException>(() =>
            Gs1DigitalLink.Parse(BaseUri + "/01/9520123456788"));
        Assert.Equal("gtin-uri-length", shortGtin.Code);

        var uri = BaseUri + "/01/" + Gtin + "/10/LOT-A/";
        var parsed = Gs1DigitalLink.Parse(uri);
        Assert.Equal("LOT-A", Assert.Single(parsed.Qualifiers).Value);
        Assert.Equal(BaseUri + "/01/" + Gtin + "/10/LOT-A", Gs1DigitalLink.Normalize(uri));
    }

    [Fact]
    public void Parse_uses_the_last_gtin_suffix_after_a_custom_path_prefix()
    {
        var parsed = Gs1DigitalLink.Parse("https://example.test/tenant/01/catalog/01/" + Gtin + "/22/2A");

        Assert.Equal("/tenant/01/catalog", parsed.PathPrefix);
        Assert.Equal("https://example.test/tenant/01/catalog", parsed.BaseUri);
        Assert.Equal("2A", Assert.Single(parsed.Qualifiers).Value);
    }

    [Theory]
    [InlineData("10", "01")]
    [InlineData("21", "01")]
    [InlineData("10", "09520123456788")]
    public void Parse_does_not_treat_a_qualifier_value_as_the_primary_key(string ai, string value)
    {
        var uri = BaseUri + "/01/" + Gtin + "/" + ai + "/" + value;

        var parsed = Gs1DigitalLink.Parse(uri);

        Assert.Equal(Gtin, parsed.Gtin);
        Assert.Equal(value, Assert.Single(parsed.Qualifiers).Value);
        Assert.Equal(uri, Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
        {
            BaseUri = parsed.BaseUri,
            Gtin = parsed.Gtin,
            Qualifiers = parsed.Qualifiers,
            DataAttributes = parsed.DataAttributes,
            ExtensionParameters = parsed.ExtensionParameters,
            Fragment = parsed.Fragment
        }));
        Assert.Equal(uri, Gs1DigitalLink.Normalize(uri));
    }

    [Fact]
    public void Parse_reports_an_incomplete_qualifier_suffix_after_a_valid_primary_key()
    {
        var error = Assert.Throws<Gs1DigitalLinkException>(() =>
            Gs1DigitalLink.Parse(BaseUri + "/01/" + Gtin + "/10/LOT-A/21"));

        Assert.Equal("qualifier-incomplete", error.Code);
    }

    [Fact]
    public void Parse_ignores_non_gtin_primary_candidates_in_custom_prefixes()
    {
        const string uri = "https://example.test/tenant/01/catalog/01/09520123456788/10/01";

        var parsed = Gs1DigitalLink.Parse(uri);

        Assert.Equal("/tenant/01/catalog", parsed.PathPrefix);
        Assert.Equal("09520123456788", parsed.Gtin);
        Assert.Equal("01", Assert.Single(parsed.Qualifiers).Value);
        Assert.Equal(uri, Gs1DigitalLink.Normalize(uri));
    }

    [Fact]
    public void Parse_rejects_an_ai_repeated_between_path_and_query()
    {
        var uri = BaseUri + "/01/" + Gtin + "/10/LOT?10=OTHER";

        var error = Assert.Throws<Gs1DigitalLinkException>(() => Gs1DigitalLink.Parse(uri));
        Assert.Equal("duplicate-ai", error.Code);
        Assert.False(Gs1DigitalLink.TryParse(uri, out _, out var diagnostics));
        Assert.Equal("duplicate-ai", Assert.Single(diagnostics).Code);
        Assert.Equal("duplicate-ai", Assert.Throws<Gs1DigitalLinkException>(() => Gs1DigitalLink.Normalize(uri)).Code);
    }

    [Theory]
    [InlineData("https://[bad/01/09520123456788")]
    [InlineData("https://example.test:abc/01/09520123456788")]
    [InlineData("https://example.test:65536/01/09520123456788")]
    [InlineData("https://example.test\\tenant/01/09520123456788")]
    [InlineData("https://example.test/tenant%GG/01/09520123456788")]
    public void Parse_rejects_invalid_authority_or_raw_path_structure(string uri)
    {
        Assert.Throws<Gs1DigitalLinkException>(() => Gs1DigitalLink.Parse(uri));
    }

    [Fact]
    public void Parse_preserves_ipv6_port_and_encoded_prefix_separators()
    {
        const string uri = "https://[2001:db8::1]:8443/tenant%2Fname/01/09520123456788/10/LOT";

        var parsed = Gs1DigitalLink.Parse(uri);

        Assert.Equal("[2001:db8::1]:8443", parsed.Authority);
        Assert.Equal("/tenant%2Fname", parsed.PathPrefix);
        Assert.Equal("https://[2001:db8::1]:8443/tenant%2Fname", parsed.BaseUri);
        Assert.Equal(uri, Gs1DigitalLink.Normalize(uri));
    }

    [Fact]
    public void Query_accepts_both_separators_and_preserves_extension_order_and_repeated_keys()
    {
        var parsed = Gs1DigitalLink.Parse(BaseUri + "/01/" + Gtin +
            "?17=260101;utm_source=label&utm_source=scan");

        Assert.Equal(new[] { "17=260101", "utm_source=label", "utm_source=scan" },
            parsed.QueryItems.Select(item => (item.Ai ?? item.Key) + "=" + item.Value));
        Assert.Equal(new[] { "label", "scan" }, parsed.ExtensionParameters.Select(item => item.Value));
        Assert.Equal(BaseUri + "/01/" + Gtin + "?17=260101&utm_source=label&utm_source=scan",
            Gs1DigitalLink.Normalize(parsed.OriginalUri));
    }

    [Theory]
    [InlineData("03=09520123456788", "03")]
    [InlineData("8014=ABC123", "8014")]
    [InlineData("8200=https%3A%2F%2Fexample.test%2Fp", "8200")]
    public void Query_rejects_application_identifiers_excluded_from_digital_link(string query, string ai)
    {
        var error = Assert.Throws<Gs1DigitalLinkException>(() =>
            Gs1DigitalLink.Parse(BaseUri + "/01/" + Gtin + "?" + query));

        Assert.Equal("digital-link-ai-unsupported", error.Code);
        Assert.Equal(ai, error.Ai);
    }

    [Theory]
    [InlineData("?x", "query-equals-required")]
    [InlineData("?x=1&&y=2", "query-empty-item")]
    [InlineData("?x=1;", "query-empty-item")]
    [InlineData("?9999=1", "unknown-gs1-ai")]
    [InlineData("?linkType=gs1%3Atraceability", "reserved-query-key")]
    [InlineData("?context=https%3A%2F%2Fexample.test", "reserved-query-key")]
    public void Query_rejects_invalid_parameter_syntax_and_reserved_keys(string suffix, string expectedCode)
    {
        var error = Assert.Throws<Gs1DigitalLinkException>(() =>
            Gs1DigitalLink.Parse(BaseUri + "/01/" + Gtin + suffix));

        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public void Query_decodes_once_and_keeps_plus_as_a_literal_plus()
    {
        var parsed = Gs1DigitalLink.Parse(BaseUri + "/01/" + Gtin +
            "?source=a+b&note=%252F");

        Assert.Equal("a+b", parsed.ExtensionParameters[0].Value);
        Assert.Equal("%2F", parsed.ExtensionParameters[1].Value);
        Assert.Equal(BaseUri + "/01/" + Gtin + "?source=a%2Bb&note=%252F",
            Gs1DigitalLink.Normalize(parsed.OriginalUri));
    }

    [Fact]
    public void Query_extension_key_and_value_can_contain_encoded_or_raw_equals()
    {
        var uri = BaseUri + "/01/" + Gtin + "?source%3Dchannel=lot=A=B";
        var parsed = Gs1DigitalLink.Parse(uri);

        Assert.Equal("source=channel", parsed.ExtensionParameters[0].Key);
        Assert.Equal("lot=A=B", parsed.ExtensionParameters[0].Value);
        Assert.Equal(BaseUri + "/01/" + Gtin + "?source%3Dchannel=lot%3DA%3DB",
            Gs1DigitalLink.Normalize(uri));
    }

    [Theory]
    [InlineData("?source=%")]
    [InlineData("#part%")]
    [InlineData("#未编码")]
    public void Parse_rejects_invalid_percent_encoding_and_unescaped_non_ascii(string suffix)
    {
        var error = Assert.Throws<Gs1DigitalLinkException>(() =>
            Gs1DigitalLink.Parse(BaseUri + "/01/" + Gtin + suffix));

        Assert.Contains(error.Code, new[] { "invalid-percent-encoding", "invalid-uri-character" });
    }

    [Fact]
    public void Qualifier_branch_accepts_declared_alternatives_and_rejects_mixed_or_misordered_values()
    {
        var numbered = Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
        {
            BaseUri = BaseUri,
            Gtin = Gtin,
            Qualifiers = new[]
            {
                new Gs1DigitalLinkAiValue("22", "2A"),
                new Gs1DigitalLinkAiValue("10", "LOT"),
                new Gs1DigitalLinkAiValue("21", "SERIAL")
            }
        });
        var alternative = Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
        {
            BaseUri = BaseUri,
            Gtin = Gtin,
            Qualifiers = new[] { new Gs1DigitalLinkAiValue("235", "TPX-1") }
        });

        Assert.Contains("/22/2A/10/LOT/21/SERIAL", numbered);
        Assert.Contains("/235/TPX-1", alternative);
        Assert.Equal("qualifier-order", Assert.Throws<Gs1DigitalLinkException>(() =>
            Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
            {
                BaseUri = BaseUri,
                Gtin = Gtin,
                Qualifiers = new[] { new Gs1DigitalLinkAiValue("10", "LOT"), new Gs1DigitalLinkAiValue("22", "2A") }
            })).Code);
        Assert.Equal("qualifier-branch", Assert.Throws<Gs1DigitalLinkException>(() =>
            Gs1DigitalLink.Build(new Gs1DigitalLinkBuildRequest
            {
                BaseUri = BaseUri,
                Gtin = Gtin,
                Qualifiers = new[] { new Gs1DigitalLinkAiValue("22", "2A"), new Gs1DigitalLinkAiValue("235", "TPX-1") }
            })).Code);
    }

    [Fact]
    public void Parse_result_lists_are_deeply_isolated_read_only_snapshots()
    {
        var parsed = Gs1DigitalLink.Parse(BaseUri + "/01/" + Gtin + "/22/2A?source=label");
        var qualifierSnapshot = parsed.Qualifiers;
        var extensionSnapshot = parsed.ExtensionParameters;

        qualifierSnapshot[0].Value = "MUTATED";
        extensionSnapshot[0].Value = "MUTATED";

        Assert.Equal("2A", parsed.Qualifiers[0].Value);
        Assert.Equal("label", parsed.ExtensionParameters[0].Value);
        Assert.Throws<NotSupportedException>(() => ((IList<Gs1DigitalLinkAiValue>)parsed.Qualifiers)
            .Add(new Gs1DigitalLinkAiValue("10", "LOT")));
    }

    [Fact]
    public void Catalog_covers_the_fixed_dictionary_and_marks_excluded_ais_unsupported()
    {
        Assert.Equal(536, Gs1AiCatalog.Entries.Count);
        Assert.Equal(536, Gs1AiCatalog.Entries.Select(entry => entry.Ai).Distinct().Count());
        Assert.Equal(Gs1AiDigitalLinkRole.Unsupported, Gs1AiCatalog.Entries.Single(entry => entry.Ai == "03").DigitalLinkRole);
        Assert.Equal(Gs1AiDigitalLinkRole.Unsupported, Gs1AiCatalog.Entries.Single(entry => entry.Ai == "8014").DigitalLinkRole);
        Assert.Equal(Gs1AiDigitalLinkRole.Unsupported, Gs1AiCatalog.Entries.Single(entry => entry.Ai == "8200").DigitalLinkRole);
        Assert.Throws<NotSupportedException>(() => ((IList<Gs1AiCatalogEntry>)Gs1AiCatalog.Entries)
            .Add(Gs1AiCatalog.Entries[0]));
        var entry = Gs1AiCatalog.Entries.First(item => item.Components.Count > 0);
        Assert.Throws<NotSupportedException>(() => ((IList<Gs1AiComponent>)entry.Components)
            .Add(entry.Components[0]));
    }
}
