using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ESC/POS Raster 固定字体资源的快照、别名解析和渲染接入。</summary>
public sealed class RasterFontCatalogTests
{
    [Fact]
    public void Resource_copies_input_bytes_and_returns_independent_getters()
    {
        var files = FindFontFiles();
        var regular = (byte[])files.Regular.Clone();
        var bold = (byte[])files.Bold.Clone();
        var resource = new RasterFontResource("Receipt", "test", regular, bold);
        var expectedRegular = resource.RegularData;
        var expectedBold = resource.BoldData;

        regular[0] ^= 0xFF;
        bold[0] ^= 0xFF;
        var regularGetter = resource.RegularData;
        var boldGetter = resource.BoldData;
        regularGetter[0] ^= 0xFF;
        boldGetter[0] ^= 0xFF;

        Assert.Equal(expectedRegular, resource.RegularData);
        Assert.Equal(expectedBold, resource.BoldData);
        Assert.True(resource.HasBold);
    }

    [Fact]
    public void Catalog_validates_alias_references_and_preserves_case_sensitivity()
    {
        var files = FindFontFiles();
        var resource = new RasterFontResource("Primary", "test", files.Regular);
        var catalog = new RasterFontCatalog(new[] { resource }, "Primary");

        Assert.Equal("Primary", catalog.DefaultFont);
        Assert.Single(catalog.Fonts);
        Assert.Empty(catalog.FallbackFonts);
        Assert.Throws<ArgumentException>(() =>
            new RasterFontCatalog(new[] { resource }, "primary"));
        Assert.Throws<ArgumentException>(() =>
            new RasterFontCatalog(new[] { resource, new RasterFontResource("Primary", "other", files.Regular) },
                "Primary"));
        Assert.Throws<ArgumentException>(() =>
            new RasterFontCatalog(new[] { resource }, "Primary", new[] { "Missing" }));
    }

    [Fact]
    public void Fixed_catalog_resolves_default_and_fallback_fonts_and_rejects_unknown_or_missing_style()
    {
        var files = FindFontFiles();
        var primary = new RasterFontResource("Primary", "test", files.Regular);
        var bold = new RasterFontResource("Bold", "test", files.Regular, files.Bold);
        var catalog = new RasterFontCatalog(new[] { primary, bold }, "Primary", new[] { "Bold" });

        Assert.Equal("Primary", EscPosRasterCanvas.ResolveFontFamilyName("A", null, false, catalog));
        Assert.Equal("Primary", EscPosRasterCanvas.ResolveFontFamilyName("A", "", false, catalog));
        Assert.Equal("Primary", EscPosRasterCanvas.ResolveFontFamilyName("A", "Primary", false, catalog));
        Assert.Equal("Bold", EscPosRasterCanvas.ResolveFontFamilyName("A", "Primary", true, catalog));
        Assert.Throws<NotSupportedException>(() =>
            EscPosRasterCanvas.ResolveFontFamilyName("A", "primary", false, catalog));
        Assert.Throws<NotSupportedException>(() =>
            EscPosRasterCanvas.ResolveFontFamilyName("A", "Primary", true,
                new RasterFontCatalog(new[] { primary }, "Primary")));
        Assert.Throws<NotSupportedException>(() =>
            EscPosRasterCanvas.ResolveFontFamilyName(char.ConvertFromUtf32(0x10FFFF), null, false,
                new RasterFontCatalog(new[] { primary }, "Primary")));
    }

    [Fact]
    public void Resource_rejects_invalid_bytes_and_a_regular_file_as_fake_bold()
    {
        var files = FindFontFiles();

        Assert.Throws<ArgumentException>(() =>
            new RasterFontResource("Invalid", "test", new byte[] { 0x00, 0x01, 0x02, 0x03 }));
        Assert.Throws<ArgumentException>(() =>
            new RasterFontResource("FakeBold", "test", files.Regular, files.Regular));
    }

    [Fact]
    public void Catalog_canvas_measurement_and_rendering_use_the_same_fixed_font()
    {
        var files = FindFontFiles();
        var catalog = new RasterFontCatalog(new[]
        {
            new RasterFontResource("Primary", "test", files.Regular, files.Bold)
        }, "Primary");

        var resolved = EscPosRasterCanvas.ResolveFontFamilyName("A", null, false, catalog);
        var width = EscPosRasterCanvas.MeasureTextWidth("A", 24, false, resolved, catalog);
        var ink = EscPosRasterCanvas.MeasureTextInkBounds("A", 24, false, false, resolved,
            long.MaxValue, catalog);
        Assert.Equal("Primary", resolved);
        Assert.True(width > 0);
        Assert.False(ink.IsEmpty);

        var first = new EscPosRasterCanvas(128, 64, catalog);
        first.DrawText(0, 0, "A", 24, fontFamily: resolved);
        var firstBytes = first.ToBytes();
        Assert.Contains(firstBytes, value => value != 0);

        var second = new EscPosRasterCanvas(128, 64, catalog);
        second.DrawText(0, 0, "A", 24, fontFamily: resolved);
        Assert.Equal(firstBytes, second.ToBytes());
    }

    [Fact]
    public void Raster_command_and_provider_snapshot_catalog_and_native_rejects_fixed_fonts()
    {
        var files = FindFontFiles();
        var catalog = new RasterFontCatalog(new[]
        {
            new RasterFontResource("Primary", "test", files.Regular, files.Bold)
        }, "Primary");
        var replacement = new RasterFontCatalog(new[]
        {
            new RasterFontResource("Other", "test", files.Regular)
        }, "Other");
        var options = new EscPosDeviceOptions { RasterFonts = catalog };
        var command = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster, options);
        options.RasterFonts = replacement;

        var bytes = command.RasterPage(128, 64, canvas =>
                canvas.DrawText(0, 0, "A", 24, fontFamily: "Primary"))
            .Build().GetBytes();
        Assert.Contains(bytes, value => value != 0);

        Assert.Throws<ArgumentException>(() =>
            new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native,
                new EscPosDeviceOptions { RasterFonts = catalog }));

        var providerOptions = new EscPosDeviceOptions { RasterFonts = catalog };
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, providerOptions);
        providerOptions.RasterFonts = replacement;
        var document = new PrintDocument();
        document.AddPage(128, 64).Add(
            new PrintTextElement(0, 0, "A", 24, fontFamily: "Primary"), "Receipt.FixedFont");

        var providerBytes = Assert.Single(provider.Render(document));
        Assert.Contains(providerBytes, value => value != 0);
    }

    [Fact]
    public void Fixed_catalog_capabilities_reject_unknown_host_alias()
    {
        var files = FindFontFiles();
        var catalog = new RasterFontCatalog(new[]
        {
            new RasterFontResource("Primary", "test", files.Regular)
        }, "Primary");
        var capabilities = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { RasterFonts = catalog }).Capabilities;
        var known = new PrintFont(PrintFontKind.Host, "Primary");
        var unknown = new PrintFont(PrintFontKind.Host, "Unknown");

        Assert.NotNull(capabilities.GetTextSizeRange(known));
        Assert.True(capabilities.SupportsFont(known, 24));
        Assert.Null(capabilities.GetTextSizeRange(unknown));
        Assert.False(capabilities.SupportsFont(unknown, 24));
    }

    [Fact]
    public void Template_rejects_unknown_fixed_alias_before_image_and_value_access()
    {
        var files = FindFontFiles();
        var catalog = new RasterFontCatalog(new[]
        {
            new RasterFontResource("Primary", "test", files.Regular)
        }, "Primary");
        var resources = new CountingResources(CreateImage());
        var valueCalled = false;

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            FixedFontPreflightTemplate("Unknown", false, () => valueCalled = true), null, resources,
            FixedFontTemplateOptions(catalog)));

        Assert.Equal("Rows[0].Cells[0].Elements[1]", error.SourcePath);
        Assert.Contains("Unknown", error.Message);
        Assert.Equal(0, resources.OpenCount);
        Assert.False(valueCalled);
    }

    [Fact]
    public void Template_rejects_missing_bold_before_image_and_value_access()
    {
        var files = FindFontFiles();
        var catalog = new RasterFontCatalog(new[]
        {
            new RasterFontResource("Primary", "test", files.Regular)
        }, "Primary");
        var resources = new CountingResources(CreateImage());
        var valueCalled = false;

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            FixedFontPreflightTemplate("Primary", true, () => valueCalled = true), null, resources,
            FixedFontTemplateOptions(catalog)));

        Assert.Equal("Rows[0].Cells[0].Elements[1]", error.SourcePath);
        Assert.Contains("Bold", error.Message);
        Assert.Equal(0, resources.OpenCount);
        Assert.False(valueCalled);
    }

    [Fact]
    public void Template_does_not_reject_a_bold_selection_with_a_configured_bold_fallback()
    {
        var files = FindFontFiles();
        var catalog = new RasterFontCatalog(new[]
        {
            new RasterFontResource("Primary", "test", files.Regular),
            new RasterFontResource("Bold", "test", files.Regular, files.Bold)
        }, "Primary", new[] { "Bold" });
        var resources = new CountingResources(CreateImage());
        var valueCalled = false;

        var result = new TemplateRenderer().RenderWithLayout(
            FixedFontPreflightTemplate("Primary", true, () => valueCalled = true), null, resources,
            FixedFontTemplateOptions(catalog));

        Assert.NotEmpty(result.Pages);
        Assert.True(valueCalled);
        Assert.True(resources.OpenCount > 0);
    }

    [Fact]
    public void Template_raster_metrics_match_fixed_catalog_measurement()
    {
        var files = FindFontFiles();
        var catalog = new RasterFontCatalog(new[]
        {
            new RasterFontResource("Primary", "test", files.Regular, files.Bold)
        }, "Primary");
        var text = "A";
        var advance = EscPosRasterCanvas.MeasureTextWidth(text, 24, false, "Primary", catalog);
        var ink = EscPosRasterCanvas.MeasureTextInkBounds(text, 24, false, false, "Primary",
            long.MaxValue, catalog);
        var expectedWidth = Math.Max(advance, ink.IsEmpty ? 0 : Math.Max(0, -ink.Left) + ink.Right);
        var template = new BpTemplate
        {
            Version = 3,
            Width = 128,
            Height = 64,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    FontFamily = "Primary",
                                    Field = new BgField { Value = text }
                                }
                            }
                        }
                    }
                }
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                EscPosLayout = EscPosLayoutMode.Raster,
                Encoding = Encoding.ASCII,
                EscPosDevice = new EscPosDeviceOptions { RasterFonts = catalog },
                FeedLines = 0,
                CutAfterPrint = false
            });

        var renderedText = Assert.Single(result.Pages[0].Elements.Where(element => element.Type == 1));
        Assert.Equal(expectedWidth, renderedText.Width);
    }

    [Fact]
    public void Missing_primary_glyph_uses_explicit_fallback_for_canvas_and_template_output()
    {
        var pair = FindCoveragePair();
        var catalog = new RasterFontCatalog(new[]
        {
            new RasterFontResource("Primary", "test", pair.PrimaryData),
            new RasterFontResource("Fallback", "test", pair.FallbackData)
        }, "Primary", new[] { "Fallback" });

        Assert.Equal("Fallback", EscPosRasterCanvas.ResolveFontFamilyName(pair.Text, null, false, catalog));
        Assert.Equal("Fallback", EscPosRasterCanvas.ResolveFontFamilyName(pair.Text, "Fallback", false, catalog));

        var implicitWidth = EscPosRasterCanvas.MeasureTextWidth(pair.Text, 24, false, null, catalog);
        var explicitWidth = EscPosRasterCanvas.MeasureTextWidth(pair.Text, 24, false, "Fallback", catalog);
        var implicitInk = EscPosRasterCanvas.MeasureTextInkBounds(pair.Text, 24, false, false, null,
            long.MaxValue, catalog);
        var explicitInk = EscPosRasterCanvas.MeasureTextInkBounds(pair.Text, 24, false, false, "Fallback",
            long.MaxValue, catalog);
        Assert.Equal(explicitWidth, implicitWidth);
        Assert.Equal(explicitInk.Left, implicitInk.Left);
        Assert.Equal(explicitInk.Top, implicitInk.Top);
        Assert.Equal(explicitInk.Width, implicitInk.Width);
        Assert.Equal(explicitInk.Height, implicitInk.Height);

        var implicitCanvas = new EscPosRasterCanvas(1024, 128, catalog);
        implicitCanvas.DrawText(0, 0, pair.Text, 24);
        var explicitCanvas = new EscPosRasterCanvas(1024, 128, catalog);
        explicitCanvas.DrawText(0, 0, pair.Text, 24, fontFamily: "Fallback");
        Assert.Equal(explicitCanvas.ToBytes(), implicitCanvas.ToBytes());
        Assert.Contains(implicitCanvas.ToBytes(), value => value != 0);

        var implicitResult = new TemplateRenderer().RenderWithLayout(
            CoverageTemplate(pair.Text, null), null, options: FixedFontTemplateOptions(catalog));
        var explicitResult = new TemplateRenderer().RenderWithLayout(
            CoverageTemplate(pair.Text, "Fallback"), null, options: FixedFontTemplateOptions(catalog));
        var implicitElement = Assert.Single(implicitResult.Pages[0].Elements.Where(element => element.Type == 1));
        var explicitElement = Assert.Single(explicitResult.Pages[0].Elements.Where(element => element.Type == 1));
        Assert.Equal(explicitElement.Width, implicitElement.Width);
        Assert.Equal(explicitResult.Pages[0].Data, implicitResult.Pages[0].Data);
    }

    private static FontFiles FindFontFiles()
        => new FontFiles(FixedTestFonts.Regular(), FixedTestFonts.Bold());

    private static FontCoveragePair FindCoveragePair()
    {
        var probeScalars = new[]
        {
            0x1F600, 0xF041, 0xF042, 0x4E2D, 0x3042, 0xAC00, 0x2605, 0x20AC
        };
        var probes = probeScalars.Select(scalar => "A" + char.ConvertFromUtf32(scalar)).ToArray();
        var primaryData = FixedTestFonts.Regular();
        var fallbackData = FixedTestFonts.Fallback();
        var primaryFamily = FixedTestFonts.LoadFamily(primaryData);
        var fallbackFamily = FixedTestFonts.LoadFamily(fallbackData);

        foreach (var text in probes)
        {
            var primaryCovers = EscPosRasterCanvas.HasGlyphCoverage(text, primaryFamily, false, out _);
            var fallbackCovers = EscPosRasterCanvas.HasGlyphCoverage(text, fallbackFamily, false, out _);
            if (!primaryCovers && fallbackCovers)
                return new FontCoveragePair(primaryData, fallbackData, text);
        }

        throw new Xunit.Sdk.XunitException(
            $"固定 DejaVu Mono/Sans 字体没有找到主字体缺字且 fallback 覆盖整段文字的稳定探测字符；探测字符数：{probes.Length}。");
    }

    private static BpTemplate CoverageTemplate(string text, string fontFamily)
        => new BpTemplate
        {
            Version = 3,
            Width = 1024,
            Height = 128,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Elements = new List<BpElement>
                            {
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    FontFamily = fontFamily,
                                    Field = new BgField { Value = text }
                                }
                            }
                        }
                    }
                }
            }
        };

    private static TemplateRenderOptions FixedFontTemplateOptions(RasterFontCatalog catalog)
        => new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            EscPosLayout = EscPosLayoutMode.Raster,
            Encoding = Encoding.UTF8,
            EscPosDevice = new EscPosDeviceOptions { RasterFonts = catalog },
            FeedLines = 0,
            CutAfterPrint = false
        };

    private static BpTemplate FixedFontPreflightTemplate(string fontFamily, bool bold, Action onValue)
        => new BpTemplate
        {
            Version = 3,
            Width = 200,
            Height = 100,
            Rows = new List<BpRow>
            {
                new BpRow
                {
                    Cells = new List<BpCell>
                    {
                        new BpCell
                        {
                            Elements = new List<BpElement>
                            {
                                new BpImageElement
                                {
                                    Width = 8,
                                    Height = 8,
                                    Field = new BgField { Value = "logo" }
                                },
                                new BpFontElement
                                {
                                    FontSize = 24,
                                    FontFamily = fontFamily,
                                    Bold = bold,
                                    Field = new BgField
                                    {
                                        ValueFunc = _ =>
                                        {
                                            onValue();
                                            return "A";
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

    private static byte[] CreateImage()
    {
        using (var image = new Image<Rgba32>(8, 8, new Rgba32(0, 0, 0)))
        using (var stream = new MemoryStream())
        {
            image.SaveAsPng(stream);
            return stream.ToArray();
        }
    }

    private sealed class FontFiles
    {
        public FontFiles(byte[] regular, byte[] bold)
        {
            Regular = regular;
            Bold = bold;
        }

        public byte[] Regular { get; }
        public byte[] Bold { get; }
    }

    private sealed class FontCoveragePair
    {
        public FontCoveragePair(byte[] primaryData, byte[] fallbackData, string text)
        {
            PrimaryData = primaryData;
            FallbackData = fallbackData;
            Text = text;
        }

        public byte[] PrimaryData { get; }
        public byte[] FallbackData { get; }
        public string Text { get; }
    }

    private sealed class CountingResources : ITemplateResourceProvider
    {
        private readonly byte[] _bytes;

        public CountingResources(byte[] bytes) => _bytes = bytes;

        public int OpenCount { get; private set; }

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            OpenCount++;
            return new MemoryStream(_bytes, writable: false);
        }
    }
}
