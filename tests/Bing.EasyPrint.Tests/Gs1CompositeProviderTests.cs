using System;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 GS1 Composite 通过四种图片协议输出时的 Provider 契约。</summary>
public sealed class Gs1CompositeProviderTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Extended_composites_match_explicit_raster_with_bounds_and_failure_recovery(PrintProtocol protocol)
    {
        foreach (var element in new[] { Gs1CompositeCcCTests.Element(), Gs1CompositeExpandedTests.Element(), Gs1CompositeStackedTests.Element(),
            Gs1CompositeGs1128AbTests.Element(Gs1CompositeMode.CcA), Gs1CompositeGs1128AbTests.Element(Gs1CompositeMode.CcB),
            ExpandedStackedTests.Element(Gs1CompositeMode.CcA), ExpandedStackedTests.Element(Gs1CompositeMode.CcB), DataBarCompletionTests.Element(),
            DataBarCompletionTests.Element(Gs1CompositeMode.CcB),
            new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarTruncated, "12345678901231", "(91)123", 26),
            new PrintGs1CompositeElement(0, 0, BarcodeType.Gs1DataBarTruncated, "12345678901231", "(91)123", 26, mode: Gs1CompositeMode.CcB) })
        {
            var provider = Provider(protocol);
            var image = Gs1CompositeEncoder.Rasterize(element);
            var expected = Assert.Single(provider.Render(Document(image, "Raster")));
            Assert.Equal(expected, Assert.Single(provider.Render(Document(element, "Composite"))));
            Assert.Throws<PrintElementValidationException>(() => provider.Render(Document(element, "Composite", width: 10)));
            Assert.Equal(expected, Assert.Single(provider.Render(Document(element, "Composite"))));
        }
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl, RotationAngle.None)]
    [InlineData(PrintProtocol.Cpcl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Cpcl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Cpcl, RotationAngle.Rotate270)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.None)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Tspl, RotationAngle.Rotate270)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.None)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.Zpl, RotationAngle.Rotate270)]
    [InlineData(PrintProtocol.EscPos, RotationAngle.None)]
    [InlineData(PrintProtocol.EscPos, RotationAngle.Rotate90)]
    [InlineData(PrintProtocol.EscPos, RotationAngle.Rotate180)]
    [InlineData(PrintProtocol.EscPos, RotationAngle.Rotate270)]
    public void Composite_render_matches_the_same_explicit_raster_for_each_protocol_and_rotation(
        PrintProtocol protocol, RotationAngle rotation)
    {
        var composite = Composite(40, 40, rotation, new PrintQuietZone(3, 5, 7, 11));
        var provider = Provider(protocol);

        var compositeOutput = Assert.Single(provider.Render(Document(composite, "Composite")));
        var raster = Gs1CompositeEncoder.Rasterize(composite);
        var rasterOutput = Assert.Single(provider.Render(Document(raster, "Raster")));

        Assert.Equal(rasterOutput, compositeOutput);
    }

    [Theory]
    [InlineData("not-a-gtin", "(03)1")]
    [InlineData("123456789012", "(91)123456")]
    public void Escpos_native_rejects_composite_capability_before_invalid_payload_validation(
        string primary, string secondary)
    {
        var provider = PrintProtocolProviderFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native);
        Assert.False(provider.Capabilities.SupportsGs1Composite);

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(
            Document(Composite(0, 0, RotationAngle.None, null, primary, secondary), "Composite")));

        Assert.Equal("Composite", error.ElementPath);
        Assert.IsType<NotSupportedException>(error.InnerException);
    }

    [Theory]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Disabled_image_profiles_reject_composite_before_invalid_payload_validation(PrintProtocol protocol)
    {
        var provider = ProviderWithoutImages(protocol);
        Assert.False(provider.Capabilities.SupportsImages);
        Assert.False(provider.Capabilities.SupportsGs1Composite);

        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(
            Document(Composite(0, 0, RotationAngle.None, null, "not-a-gtin", "(03)1"), "Composite")));

        Assert.Equal("Composite", error.ElementPath);
        Assert.IsType<NotSupportedException>(error.InnerException);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Composite_accepts_exact_page_edge_and_rejects_one_dot_clipping(PrintProtocol protocol)
    {
        var composite = Composite(0, 0, RotationAngle.Rotate90, new PrintQuietZone(2, 3, 4, 5));
        var raster = Gs1CompositeEncoder.Rasterize(composite);
        Assert.True(raster.OutputWidth > 1);
        Assert.True(raster.OutputHeight > 1);

        var provider = Provider(protocol);
        provider.Validate(Document(composite, "Composite", raster.OutputWidth, raster.OutputHeight));

        var clipped = Assert.Throws<PrintElementValidationException>(() => provider.Validate(
            Document(composite, "Composite", raster.OutputWidth - 1, raster.OutputHeight)));
        Assert.Equal("Composite", clipped.ElementPath);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Composite_quiet_zone_rejects_an_intersecting_rectangle(PrintProtocol protocol)
    {
        var composite = Composite(40, 40, RotationAngle.None, new PrintQuietZone(8, 8, 8, 8));
        var document = Document(composite, "Composite");
        document.Pages[0].Add(new PrintRectangleElement(40, 40, 8, 8, filled: true), "Occupied");

        var error = Assert.Throws<PrintElementValidationException>(() => Provider(protocol).Validate(document));

        Assert.Equal("Composite", error.ElementPath);
        Assert.Contains("静区", error.InnerException?.Message ?? error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Escpos_raster_composite_quiet_zone_rejects_reverse_whitespace_at_all_rotations(
        RotationAngle rotation)
    {
        var document = Document(Composite(40, 40, rotation, PrintQuietZone.Uniform(8)), "Composite");
        document.Pages[0].Add(new PrintTextElement(40, 40, "   ", 24,
            fontFamily: "Primary", reverse: true), "ReverseWhitespace");

        var error = Assert.Throws<PrintElementValidationException>(() =>
            EscPosRasterProviderWithFixedFonts().Validate(document));

        Assert.Equal("Composite", error.ElementPath);
        Assert.Contains("静区", error.InnerException?.Message ?? error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RotationAngle.None)]
    [InlineData(RotationAngle.Rotate90)]
    [InlineData(RotationAngle.Rotate180)]
    [InlineData(RotationAngle.Rotate270)]
    public void Escpos_raster_empty_reverse_text_does_not_occupy_composite_quiet_zone(
        RotationAngle rotation)
    {
        var document = Document(Composite(40, 40, rotation, PrintQuietZone.Uniform(8)), "Composite");
        document.Pages[0].Add(new PrintTextElement(40, 40, string.Empty, 24,
            fontFamily: "Primary", reverse: true), "EmptyReverse");

        var provider = EscPosRasterProviderWithFixedFonts();
        provider.Validate(document);
        Assert.NotEmpty(provider.Render(document));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Provider_budget_rejection_does_not_poison_a_later_composite_render(PrintProtocol protocol)
    {
        var provider = Provider(protocol);
        var document = Document(Composite(), "Composite");

        using (var limited = PrintResourceScope.Begin(512))
        {
            Assert.Throws<PrintResourceLimitException>(() => provider.Render(document));
            Assert.Equal(0, limited.CurrentBytes);
        }

        Assert.NotEmpty(provider.Render(document));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Provider_cancellation_and_invalid_input_leave_a_fresh_render_possible(PrintProtocol protocol)
    {
        var provider = Provider(protocol);
        var valid = Document(Composite(), "Valid");
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => provider.Render(valid, cancellation.Token));
        }

        var invalid = Document(Composite(primary: "not-a-gtin"), "Invalid");
        var error = Assert.Throws<PrintElementValidationException>(() => provider.Render(invalid));
        Assert.Equal("Invalid", error.ElementPath);

        Assert.NotEmpty(provider.Render(valid));
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    [InlineData(PrintProtocol.EscPos)]
    public void Reusing_a_composite_element_keeps_duplicate_instance_paths_and_renders(PrintProtocol protocol)
    {
        var element = Composite();
        var document = new PrintDocument();
        var page = document.AddPage(500, 500);
        page.FeedLines = 0;
        page.Add(element, "Components[0]").Add(element, "Components[1]");

        Assert.Equal("Components[0]", page.Elements[0].SourcePath);
        Assert.Equal("Components[1]", page.Elements[1].SourcePath);
        Assert.NotSame(page.Elements[0], page.Elements[1]);
        Assert.NotEmpty(Provider(protocol).Render(document));
    }

    private static PrintGs1CompositeElement Composite(int x = 40, int y = 40,
        RotationAngle rotation = RotationAngle.None, PrintQuietZone quietZone = null,
        string primary = "1234567890123", string secondary = "(91)123456")
        => new PrintGs1CompositeElement(x, y, BarcodeType.Gs1DataBarOmnidirectional,
            primary, secondary, 20, moduleWidth: 2, mode: Gs1CompositeMode.CcA,
            rotation: rotation, quietZone: quietZone);

    private static PrintDocument Document(PrintElement element, string sourcePath,
        int width = 500, int height = 500)
    {
        var document = new PrintDocument();
        var page = document.AddPage(width, height);
        page.FeedLines = 0;
        page.Add(element, sourcePath);
        return document;
    }

    private static IPrintProtocolProvider Provider(PrintProtocol protocol)
    {
        switch (protocol)
        {
            case PrintProtocol.Cpcl:
                return PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII);
            case PrintProtocol.Tspl:
                return PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
                    tsplOptions: new TsplMediaOptions());
            case PrintProtocol.Zpl:
                return PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
                    zplOptions: new ZplMediaOptions());
            case PrintProtocol.EscPos:
                return PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
                    EscPosLayoutMode.Raster);
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null);
        }
    }

    private static IPrintProtocolProvider EscPosRasterProviderWithFixedFonts()
        => PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { RasterFonts = FixedTestFonts.Catalog() });

    private static IPrintProtocolProvider ProviderWithoutImages(PrintProtocol protocol)
    {
        switch (protocol)
        {
            case PrintProtocol.Tspl:
                return PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
                    tsplOptions: new TsplMediaOptions
                    {
                        Profile = new TsplPrinterProfile { SupportsImages = false }
                    });
            case PrintProtocol.Zpl:
                return PrintProtocolProviderFactory.Create(protocol, Encoding.ASCII,
                    zplOptions: new ZplMediaOptions
                    {
                        Profile = new ZplPrinterProfile { SupportsImages = false }
                    });
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null);
        }
    }
}
