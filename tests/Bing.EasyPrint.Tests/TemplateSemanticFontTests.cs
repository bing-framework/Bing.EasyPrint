using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证模板文字从旧版字段迁移到语义字体后的字节、布局和严格校验契约。
/// </summary>
public sealed class TemplateSemanticFontTests
{
    [Fact]
    public void Invalid_semantic_cpcl_font_has_protocol_and_path_without_requesting_layout()
    {
        var font = Text("INVALID", 24);
        font.Font = new PrintFont(PrintFontKind.Device, "55");
        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(font), null, options: new TemplateRenderOptions { Encoding = Encoding.ASCII }));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains(PrintProtocol.Cpcl.ToString(), error.Message);
    }
    [Theory]
    [InlineData(24, 24)]
    [InlineData(16, 55)]
    public void Cpcl_legacy_and_semantic_device_fonts_have_identical_bytes_and_layout(int size, int legacyName)
    {
        var legacy = Text("ABC", size);
        legacy.FontName = legacyName;
        var semantic = Text("ABC", size);
        semantic.Font = new PrintFont(PrintFontKind.Device, legacyName.ToString());

        AssertSameRender(legacy, semantic, Options(PrintProtocol.Cpcl));
    }

    [Theory]
    [InlineData(48, "24", 50)]
    [InlineData(32, "55", 32)]
    public void Cpcl_semantic_device_font_preserves_fit_bytes_and_layout(int size, string fontName, int width)
    {
        var legacy = Text("ABCD", size);
        legacy.FontName = int.Parse(fontName);
        legacy.Width = width;
        legacy.Field.Fit = true;

        var semantic = Text("ABCD", size);
        semantic.Font = new PrintFont(PrintFontKind.Device, fontName);
        semantic.Width = width;
        semantic.Field.Fit = true;

        AssertSameRender(legacy, semantic, Options(PrintProtocol.Cpcl));
    }

    [Fact]
    public void Cpcl_semantic_device_font_preserves_rotated_bytes_and_layout()
    {
        var legacy = Text("ABC", 24);
        legacy.FontName = 24;
        legacy.Rotation = RotationAngle.Rotate90;
        legacy.Width = 40;
        legacy.StartCoordinate = Point(8, 12);

        var semantic = Text("ABC", 24);
        semantic.Font = new PrintFont(PrintFontKind.Device, "24");
        semantic.Rotation = RotationAngle.Rotate90;
        semantic.Width = 40;
        semantic.StartCoordinate = Point(8, 12);

        AssertSameRender(legacy, semantic, Options(PrintProtocol.Cpcl));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "1")]
    [InlineData(PrintProtocol.Zpl, "A")]
    public void Label_device_font_selection_matches_legacy_bytes_and_layout(PrintProtocol protocol, string fontName)
    {
        var legacy = Text("AB", 24);
        legacy.DeviceFontName = fontName;
        var semantic = Text("AB", 24);
        semantic.Font = new PrintFont(PrintFontKind.Device, fontName);

        AssertSameRender(legacy, semantic, Options(protocol));
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "1")]
    [InlineData(PrintProtocol.Zpl, "A")]
    public void Label_semantic_device_font_uses_its_individual_calibration(PrintProtocol protocol, string fontName)
    {
        var options = Options(protocol);
        options.DeviceFontCalibrations = new Dictionary<string, DeviceFontCalibration>
        {
            [fontName] = new DeviceFontCalibration
            {
                HalfCellWidthAt24 = 20,
                FullCellWidthAt24 = 40,
                LineHeightAt24 = 36
            }
        };

        var font = Text("AB", 24);
        font.Font = new PrintFont(PrintFontKind.Device, fontName);
        var page = Assert.Single(new TemplateRenderer().RenderWithLayout(
            Template(font), null, options: options).Pages);
        var text = Assert.Single(page.Elements.Where(element => element.Type == 1));

        Assert.Equal(40, text.Width);
        Assert.Equal(36, text.Height);
    }

    [Fact]
    public void Esc_pos_raster_host_font_selection_matches_legacy_family_for_multiline_bytes_and_layout()
    {
        var family = EscPosRasterCanvas.ResolveFontFamilyName("A", null, false, FixedTestFonts.Catalog());
        var legacy = Text("A\nB", 24);
        legacy.FontFamily = family;
        var semantic = Text("A\nB", 24);
        semantic.Font = new PrintFont(PrintFontKind.Host, family);

        AssertSameRender(legacy, semantic, Options(PrintProtocol.EscPos, EscPosLayoutMode.Raster));
    }

    [Fact]
    public void Esc_pos_raster_explicit_default_matches_legacy_default_for_multiline_output()
    {
        var legacy = Text("A\nB", 24);
        var semantic = Text("A\nB", 24);
        semantic.Font = PrintFont.Default;

        AssertSameRender(legacy, semantic, Options(PrintProtocol.EscPos, EscPosLayoutMode.Raster));
    }

    [Fact]
    public void Semantic_font_round_trips_through_json_without_changing_rendered_bytes_or_layout()
    {
        var template = Template(Text("ROUND", 24));
        ((BpFontElement)template.Rows[0].Cells[0].Elements[0]).Font =
            new PrintFont(PrintFontKind.Device, "24");
        var options = Options(PrintProtocol.Cpcl);
        var loader = new TemplateLoader();
        var json = loader.SaveJson(template, options);
        var loaded = loader.LoadJson(json, options);
        var loadedFont = Assert.IsType<BpFontElement>(loaded.Rows[0].Cells[0].Elements[0]);

        Assert.NotNull(loadedFont.Font);
        Assert.Equal(PrintFontKind.Device, loadedFont.Font.Kind);
        Assert.Equal("24", loadedFont.Font.Name);
        AssertSameRender(template, loaded, options);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, 24)]
    [InlineData(PrintProtocol.Tspl, 55)]
    [InlineData(PrintProtocol.Zpl, 24)]
    [InlineData(PrintProtocol.Zpl, 55)]
    [InlineData(PrintProtocol.EscPos, 24)]
    [InlineData(PrintProtocol.EscPos, 55)]
    public void Non_cpcl_legacy_cpcl_font_fails_before_binding_and_resource_access(PrintProtocol protocol, int legacyName)
    {
        var data = new CountingData();
        var resources = new CountingResources();
        var font = Text(null, legacyName == 24 ? 24 : 16);
        font.FontName = legacyName;
        font.Field.Name = "Value";
        var image = new BpImageElement
        {
            Width = 8,
            Height = 8,
            Field = new BgField { Value = "logo" }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(font, image), data, resources, Options(protocol)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains(protocol.ToString(), error.Message);
        Assert.Equal(0, data.GetterCount);
        Assert.Equal(0, resources.OpenCount);
    }

    [Theory]
    [InlineData("FontName", "24")]
    [InlineData("FontFamily", "Arial")]
    [InlineData("DeviceFontName", "1")]
    public void Semantic_font_and_legacy_fields_are_rejected_on_save_load_and_render(string property, string value)
    {
        var template = Template(Text("CONFLICT", 24));
        var font = (BpFontElement)template.Rows[0].Cells[0].Elements[0];
        font.Font = new PrintFont(PrintFontKind.Device, "1");
        SetLegacyFontProperty(font, property, value);
        var options = Options(PrintProtocol.Tspl);
        var loader = new TemplateLoader();

        Assert.Throws<ArgumentException>(() => loader.SaveJson(template, options));
        Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(template, null, options: options));

        var clean = Template(Text("CONFLICT", 24));
        ((BpFontElement)clean.Rows[0].Cells[0].Elements[0]).Font =
            new PrintFont(PrintFontKind.Device, "1");
        var root = JObject.Parse(loader.SaveJson(clean, options));
        var serializedFont = (JObject)root["Rows"][0]["Cells"][0]["Elements"][0];
        serializedFont[property] = value;
        Assert.Throws<JsonSerializationException>(() => loader.LoadJson(root.ToString(Formatting.None), options));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Tspl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.Zpl, EscPosLayoutMode.Native)]
    [InlineData(PrintProtocol.EscPos, EscPosLayoutMode.Native)]
    public void Host_font_is_rejected_by_non_raster_protocol_modes(PrintProtocol protocol, EscPosLayoutMode layout)
    {
        var font = Text("HOST", 24);
        font.Font = new PrintFont(PrintFontKind.Host, "Primary");

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(font), null, options: Options(protocol, layout)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains(protocol.ToString(), error.Message);
    }

    [Theory]
    [InlineData(EscPosLayoutMode.Native)]
    [InlineData(EscPosLayoutMode.Raster)]
    public void Device_font_is_rejected_by_esc_pos_in_every_layout_mode(EscPosLayoutMode layout)
    {
        var font = Text("DEVICE", 24);
        font.Font = new PrintFont(PrintFontKind.Device, "1");

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(font), null, options: Options(PrintProtocol.EscPos, layout)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains(PrintProtocol.EscPos.ToString(), error.Message);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl, "9")]
    [InlineData(PrintProtocol.Zpl, "a")]
    public void Unknown_device_font_is_rejected_during_static_preflight(PrintProtocol protocol, string fontName)
    {
        var font = Text("UNKNOWN", 24);
        font.Font = new PrintFont(PrintFontKind.Device, fontName);

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template(font), null, options: Options(protocol)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains(protocol.ToString(), error.Message);
        Assert.Contains("设备字体", error.Message);
    }

    [Fact]
    public void Explicit_default_font_preserves_existing_cpcl_bytes()
    {
        var legacy = Text("DEFAULT", 24);
        var semantic = Text("DEFAULT", 24);
        semantic.Font = PrintFont.Default;

        AssertSameRender(legacy, semantic, Options(PrintProtocol.Cpcl));
    }

    [Theory]
    [InlineData("fractional-kind")]
    [InlineData("string-kind")]
    [InlineData("huge-kind")]
    [InlineData("unknown-property")]
    [InlineData("device-name-null")]
    [InlineData("default-name-null")]
    public void Json_semantic_font_rejects_lossy_or_ambiguous_shapes(string invalidShape)
    {
        var options = Options(PrintProtocol.Cpcl);
        var loader = new TemplateLoader();
        var template = Template(Text("JSON", 24));
        ((BpFontElement)template.Rows[0].Cells[0].Elements[0]).Font =
            new PrintFont(PrintFontKind.Device, "24");
        var root = JObject.Parse(loader.SaveJson(template, options));
        var font = (JObject)root["Rows"][0]["Cells"][0]["Elements"][0]["Font"];

        switch (invalidShape)
        {
            case "fractional-kind": font["Kind"] = 1.5; break;
            case "string-kind": font["Kind"] = "Host"; break;
            case "huge-kind": font["Kind"] = JToken.Parse("999999999999999999999999999999999"); break;
            case "unknown-property": font["Unexpected"] = true; break;
            case "device-name-null": font["Name"] = JValue.CreateNull(); break;
            case "default-name-null": font["Kind"] = 0; font["Name"] = JValue.CreateNull(); break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidShape));
        }

        Assert.ThrowsAny<JsonSerializationException>(() => loader.LoadJson(
            root.ToString(Formatting.None), options));
    }

    private static void AssertSameRender(BpFontElement expectedFont, BpFontElement actualFont,
        TemplateRenderOptions options)
    {
        AssertSameRender(Template(expectedFont), Template(actualFont), options);
    }

    private static void AssertSameRender(BpTemplate expectedTemplate, BpTemplate actualTemplate,
        TemplateRenderOptions options)
    {
        var renderer = new TemplateRenderer();
        var expected = renderer.RenderWithLayout(expectedTemplate, null, options: options);
        var actual = renderer.RenderWithLayout(actualTemplate, null, options: options);

        Assert.Equal(expected.Protocol, actual.Protocol);
        Assert.Equal(expected.PageCount, actual.PageCount);
        for (var index = 0; index < expected.Pages.Count; index++)
        {
            Assert.Equal(expected.Pages[index].Width, actual.Pages[index].Width);
            Assert.Equal(expected.Pages[index].Height, actual.Pages[index].Height);
            Assert.Equal(expected.Pages[index].Data, actual.Pages[index].Data);
            Assert.Equal(LayoutSignature(expected.Pages[index]), LayoutSignature(actual.Pages[index]));
        }
    }

    private static string LayoutSignature(TemplatePage page) => string.Join("|", page.Elements.Select(element =>
        string.Join(",", element.SourcePath, element.Type, element.X, element.Y, element.Width,
            element.Height, (int)element.Rotation, element.Text ?? string.Empty, element.FragmentIndex)));

    private static TemplateRenderOptions Options(PrintProtocol protocol,
        EscPosLayoutMode layout = EscPosLayoutMode.Native) => new TemplateRenderOptions
        {
            Protocol = protocol,
            EscPosLayout = layout,
            Encoding = Encoding.ASCII,
            EscPosDevice = protocol == PrintProtocol.EscPos && layout == EscPosLayoutMode.Raster
                ? new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() }
                : null,
            CutAfterPrint = protocol == PrintProtocol.EscPos ? (bool?)false : null,
            FeedLines = protocol == PrintProtocol.EscPos ? (int?)0 : null
        };

    private static BpTemplate Template(params BpElement[] elements) => new BpTemplate
    {
        Version = 3,
        Width = 160,
        Height = 100,
        Rows = new List<BpRow> { new BpRow
        {
            Cells = new List<BpCell> { new BpCell { Width = 160, Elements = elements.ToList() } }
        } }
    };

    private static BpFontElement Text(string value, int size) => new BpFontElement
    {
        FontSize = size,
        Field = new BgField { Value = value }
    };

    private static BpCoordinate Point(int x, int y) => new BpCoordinate { X = x, Y = y };

    private static void SetLegacyFontProperty(BpFontElement font, string property, string value)
    {
        switch (property)
        {
            case "FontName": font.FontName = int.Parse(value); break;
            case "FontFamily": font.FontFamily = value; break;
            case "DeviceFontName": font.DeviceFontName = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(property));
        }
    }

    private sealed class CountingData
    {
        public int GetterCount { get; private set; }

        public string Value
        {
            get
            {
                GetterCount++;
                return "bound";
            }
        }
    }

    private sealed class CountingResources : ITemplateResourceProvider
    {
        public int OpenCount { get; private set; }

        public Stream OpenRead(string resourceKey, System.Threading.CancellationToken cancellationToken)
        {
            OpenCount++;
            return new MemoryStream();
        }
    }
}
