using System;
using System.Linq;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证固定字典 req/ex 关联校验及多载体合并作用域。</summary>
public sealed class Gs1AssociationValidatorTests
{
    [Fact]
    public void Content_requires_count_and_accepts_count_on_secondary_carrier()
    {
        var error = Assert.Throws<ArgumentException>(() => Gs1AssociationValidator.Validate("(02)12345678901231"));
        Assert.Contains("GS1 AI 02", error.Message);
        Assert.Contains("37", error.Message);

        Gs1AssociationValidator.Validate("(00)123456789012345675(02)12345678901231(37)12");
        Gs1AssociationValidator.ValidateCarriers(new[] { "(00)123456789012345675(02)12345678901231", "(37)12" });
    }

    [Fact]
    public void Content_and_gtin_or_mto_are_mutually_exclusive()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            Gs1AssociationValidator.Validate("(02)12345678901231(37)12(01)12345678901231"));
        Assert.Contains("02", error.Message);
        Assert.Contains("01", error.Message);
        Assert.Contains("ex=01,03", error.Message);
    }

    [Fact]
    public void Repeated_req_attributes_are_each_required()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            Gs1AssociationValidator.Validate("(8006)123456789012310102(250)SECONDARY"));
        Assert.Contains("250", error.Message);
        Assert.Contains("21", error.Message);

        Gs1AssociationValidator.Validate("(8006)123456789012310102(250)SECONDARY(21)SERIAL");
    }

    [Fact]
    public void Comma_is_alternative_and_plus_is_a_required_group()
    {
        Gs1AssociationValidator.Validate("(01)12345678901231(3103)001234");
        Gs1AssociationValidator.Validate("(01)12345678901231(21)SERIAL");
        Gs1AssociationValidator.Validate("(01)12345678901231(21)SERIAL(8030)ABC");

        var error = Assert.Throws<ArgumentException>(() =>
            Gs1AssociationValidator.Validate("(01)12345678901231(8030)ABC"));
        Assert.Contains("8030", error.Message);
        Assert.Contains("01+21", error.Message);
    }

    [Fact]
    public void Digit_wildcards_match_only_numeric_ai_positions()
    {
        Gs1AssociationValidator.Validate("(01)12345678901231(3103)001234");
        var error = Assert.Throws<ArgumentException>(() =>
            Gs1AssociationValidator.Validate("(01)12345678901231(3103)001234(3104)001235"));
        Assert.Contains("3103", error.Message);
        Assert.Contains("3104", error.Message);
        Assert.Contains("ex=310n", error.Message);
    }

    [Fact]
    public void Ex_does_not_exclude_the_ai_itself()
    {
        Gs1AssociationValidator.Validate("(01)12345678901231(3103)001234(3103)001235");
    }

    [Fact]
    public void Variable_fixed_and_composite_secondary_data_are_merged_only_explicitly()
    {
        Assert.Throws<ArgumentException>(() => Gs1AssociationValidator.Validate("(20)12"));
        Gs1AssociationValidator.ValidateCarriers(new[] { "(01)12345678901231", "(20)12" });

        Gs1AssociationValidator.ValidateCarriers(new[] { "(01)12345678901231", "(10)LOT-A" });
        Gs1AssociationValidator.ValidateCarriers(new[] { "(01)12345678901231", "(17)260101" });
        Gs1AssociationValidator.ValidateCarriers(new[] { "(00)123456789012345675(02)12345678901231", "(37)12" });
    }

    [Fact]
    public void Composite_retail_and_databar_primary_identity_participates_in_merge()
    {
        Gs1AssociationValidator.ValidateComposite("12345670", "(20)12", BarcodeType.Ean8);
        Gs1AssociationValidator.ValidateComposite("1234567890123", "(20)12", BarcodeType.Gs1DataBarOmnidirectional);
        Assert.Throws<ArgumentException>(() =>
            Gs1AssociationValidator.ValidateComposite("12345670", "(37)12", BarcodeType.Ean8));
    }
    [Fact]
    public void Pinned_dictionary_association_attribute_inventory_is_complete()
    {
        var definitions = Gs1SyntaxDictionary.AllDefinitions;
        var sourceDefinitions = definitions.Select(definition => definition.Specification).Distinct(StringComparer.Ordinal);
        var req = sourceDefinitions.SelectMany(definition => definition
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            .Count(token => token.StartsWith("req=", StringComparison.Ordinal));
        var ex = sourceDefinitions.SelectMany(definition => definition
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            .Count(token => token.StartsWith("ex=", StringComparison.Ordinal));
        Assert.Equal(194, req);
        Assert.Equal(84, ex);
        Assert.True(definitions.Count(definition => definition.RequirementRules.Count > 1) >= 6);
        Assert.Contains(definitions, definition => definition.Ai == "7258" && definition.RequirementRules.Any(rule => rule.Raw.Contains("8018+7259")));
        Assert.Contains(definitions, definition => definition.Ai == "8030" && definition.RequirementRules.Any(rule => rule.Raw.Contains("01+21")));
    }
    [Fact]
    public void Bracketed_and_normalized_carriers_use_the_same_syntax_scope()
    {
        Gs1AssociationValidator.ValidateCarriers(new[] { "(00)123456789012345675", "(4300)ACME" });
        Gs1AssociationValidator.ValidateCarriers(new[] { "00123456789012345675", "4300ACME" });

        Gs1AssociationValidator.ValidateCarriers(new[] { "(414)1234567890128", "(254)EXT" });
        Gs1AssociationValidator.ValidateCarriers(new[] { "4141234567890128", "254EXT" });
    }

    [Fact]
    public void Fixed_length_non_predefined_ai_requires_a_separator_before_the_next_ai()
    {
        Assert.Throws<ArgumentException>(() =>
            Gs1AssociationValidator.Validate("4261240112345678901231"));
        Gs1AssociationValidator.Validate("426124\u001D0112345678901231");
    }

    [Fact]
    public void Composite_merges_split_primary_and_secondary_before_association_checks()
    {
        Gs1AssociationValidator.ValidateComposite("(00)123456789012345675", "(4300)ACME",
            BarcodeType.UccEan128);
        Gs1AssociationValidator.ValidateComposite("(414)1234567890128", "(254)EXT",
            BarcodeType.UccEan128);
        Assert.Throws<ArgumentException>(() =>
            Gs1AssociationValidator.ValidateComposite("(00)123456789012345675", "(02)12345678901231",
                BarcodeType.UccEan128));
    }

    [Fact]
    public void Unknown_or_malformed_association_rule_is_not_silently_ignored()
    {
        Assert.Throws<ArgumentException>(() => Gs1AssociationValidator.Validate("(9999)1"));
        Assert.Throws<ArgumentException>(() => Gs1AssociationValidator.Validate("NOT-GS1"));
    }
}
