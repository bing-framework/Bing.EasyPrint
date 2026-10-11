using System;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证模板加载器为静区结构错误保留精确 JSON 路径。</summary>
public sealed class TemplateQuietZonePathTests
{
    [Theory]
    [InlineData("{\"Left\":-1}", ".Left")]
    [InlineData("{\"Left\":1.5}", ".Left")]
    [InlineData("{\"Left\":\"1\"}", ".Left")]
    [InlineData("{\"Center\":1}", ".Center")]
    [InlineData("{\"Left\":2147483648}", ".Left")]
    [InlineData("[]", "")]
    public void LoadJsonWithPaths_reports_the_exact_quiet_zone_field_path(string quietZone, string suffix)
    {
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateLoader().LoadJsonWithPaths(
            Root(Element(4, quietZone))));

        Assert.Equal("Rows[0].Cells[0].Elements[0].QuietZone" + suffix, error.SourcePath);
        Assert.Null(error.PageIndex);
        Assert.Null(error.ElementIndex);
        Assert.Contains("QuietZone", error.Message);
    }

    [Fact]
    public void LoadJsonWithPaths_points_to_quiet_zone_when_the_element_type_is_not_a_code()
    {
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateLoader().LoadJsonWithPaths(
            Root(Element(1, "{}"))));

        Assert.Equal("Rows[0].Cells[0].Elements[0].QuietZone", error.SourcePath);
        Assert.Null(error.PageIndex);
        Assert.Null(error.ElementIndex);
    }

    [Fact]
    public void Regular_LoadJson_keeps_the_original_json_exception_type_for_invalid_quiet_zone()
    {
        var error = Assert.Throws<JsonSerializationException>(() => new TemplateLoader().LoadJson(
            Root(Element(4, "{\"Left\":-1}"))));

        Assert.DoesNotContain("Rows[0].Cells[0].Elements[0]", error.Message);
    }

    [Fact]
    public void Component_parameter_business_values_named_quiet_zone_are_not_treated_as_elements()
    {
        const string json = "{\"Version\":3,\"Width\":240,\"Height\":120,\"Components\":[{\"Id\":\"badge\",\"Width\":220,\"Height\":48,\"Parameters\":{\"payload\":{\"DefaultValue\":{\"Type\":3,\"QuietZone\":\"business\",\"Label\":\"definition\"}}},\"Elements\":[{\"Type\":1,\"Field\":{\"Name\":\"$param.payload\"}}]}],\"Rows\":[{\"Cells\":[{\"Elements\":[{\"Type\":6,\"ComponentId\":\"badge\",\"Parameters\":{\"payload\":{\"Value\":{\"Type\":3,\"QuietZone\":\"business\",\"Label\":\"instance\"}}}}]}]}]}";
        var loader = new TemplateLoader();

        var regular = loader.LoadJson(json);
        var withPaths = loader.LoadJsonWithPaths(json);

        Assert.Equal("business", regular.Components[0].Parameters["payload"].DefaultValue["QuietZone"].ToObject<string>());
        var regularGroup = Assert.IsType<Bing.EasyPrint.Elements.BpGroupElement>(regular.Rows[0].Cells[0].Elements[0]);
        Assert.Equal("business", regularGroup.Parameters["payload"].Value["QuietZone"].ToObject<string>());
        Assert.Equal("business", withPaths.Components[0].Parameters["payload"].DefaultValue["QuietZone"].ToObject<string>());
        var pathGroup = Assert.IsType<Bing.EasyPrint.Elements.BpGroupElement>(withPaths.Rows[0].Cells[0].Elements[0]);
        Assert.Equal("business", pathGroup.Parameters["payload"].Value["QuietZone"].ToObject<string>());
    }

    [Theory]
    [InlineData("null", false)]
    [InlineData("{}", true)]
    public void LoadJsonWithPaths_accepts_null_and_empty_quiet_zones(string quietZone, bool expectedObject)
    {
        var template = new TemplateLoader().LoadJsonWithPaths(Root(Element(4, quietZone)));
        var barcode = Assert.IsType<Bing.EasyPrint.Elements.BpBarcodeElement>(
            template.Rows[0].Cells[0].Elements[0]);

        if (!expectedObject)
        {
            Assert.Null(barcode.QuietZone);
            return;
        }

        Assert.NotNull(barcode.QuietZone);
        Assert.Equal((0, 0, 0, 0), (barcode.QuietZone.Left, barcode.QuietZone.Top,
            barcode.QuietZone.Right, barcode.QuietZone.Bottom));
    }

    [Theory]
    [InlineData(false, "Rows[0].Cells[0].Elements[0].Elements[0].QuietZone.Left")]
    [InlineData(true, "Components[0].Elements[0].Elements[0].QuietZone.Left")]
    public void LoadJsonWithPaths_preserves_nested_group_and_component_definition_paths(
        bool inComponent, string expectedPath)
    {
        var invalidBarcode = Element(4, "{\"Left\":-1}");
        var nestedGroup = "{\"Type\":6,\"Width\":220,\"Height\":48,\"Elements\":[" + invalidBarcode + "]}";
        var json = inComponent
            ? "{\"Version\":3,\"Width\":240,\"Height\":120,\"Components\":[{\"Id\":\"badge\",\"Width\":220,\"Height\":48,\"Elements\":[" + nestedGroup + "]}],\"Rows\":[]}"
            : Root(nestedGroup);

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateLoader().LoadJsonWithPaths(json));

        Assert.Equal(expectedPath, error.SourcePath);
        Assert.Null(error.PageIndex);
        Assert.Null(error.ElementIndex);
    }

    private static string Root(string element)
        => "{\"Version\":3,\"Width\":240,\"Height\":120,\"Rows\":[{\"Cells\":[{\"Elements\":[" + element + "]}]}]}";

    private static string Element(int type, string quietZone)
        => "{\"Type\":" + type + ",\"Width\":180,\"Height\":24,\"Field\":{\"Value\":\"1234\"},\"QuietZone\":" + quietZone + "}";
}
