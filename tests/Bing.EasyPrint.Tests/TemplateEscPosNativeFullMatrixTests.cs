using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 ESC/POS Native 能力快照声明的全部一维条码制式能够在 Rows 和 Sequential
/// 模板路径中保持 HRI、模块参数、原生 GS k 制式和连续行布局轨迹一致。
/// </summary>
public sealed class TemplateEscPosNativeFullMatrixTests
{
    private static readonly BarcodeCase[] Cases =
    {
        new BarcodeCase(BarcodeType.Code128, "1234", 73),
        new BarcodeCase(BarcodeType.Code39, "ABC-12", 69),
        new BarcodeCase(BarcodeType.Code93, "CODE93", 72),
        new BarcodeCase(BarcodeType.Ean8, "96385074", 68),
        new BarcodeCase(BarcodeType.Ean13, "4006381333931", 67),
        new BarcodeCase(BarcodeType.UpcA, "03600029145", 65),
        new BarcodeCase(BarcodeType.UpcE, "425261", 66),
        new BarcodeCase(BarcodeType.Codabar, "A12B", 71),
        new BarcodeCase(BarcodeType.I2OF5, "1234", 70),
        new BarcodeCase(BarcodeType.I2OF5C, "1234", 70),
        new BarcodeCase(BarcodeType.I2OF5G, "12345678901", 70),
        new BarcodeCase(BarcodeType.UccEan128, "0101234567890128", 74),
        new BarcodeCase(BarcodeType.Gs1DataBarOmnidirectional, "1234567890123", 75),
        new BarcodeCase(BarcodeType.Gs1DataBarTruncated, "1234567890123", 76),
        new BarcodeCase(BarcodeType.Gs1DataBarLimited, "0123456789012", 77),
        new BarcodeCase(BarcodeType.Gs1DataBarExpanded, "(01)\u001D12345678901231", 78),
        new BarcodeCase(BarcodeType.Code128Auto, "ABC123", 79)
    };

    [Fact]
    public void Rows_template_covers_every_declared_native_barcode_with_hri_parameters()
    {
        var capabilities = TemplateProtocolCapabilities.Create(Options(EscPosNativeFlowMode.Rows));
        Assert.All(Cases, item => Assert.Contains(item.Type, capabilities.BarcodeTypes));

        foreach (var item in Cases)
        {
            var result = new TemplateRenderer().RenderWithLayout(
                SingleRowTemplate(item), null, options: Options(EscPosNativeFlowMode.Rows));

            var page = Assert.Single(result.Pages);
            var element = Assert.Single(page.Elements.Where(value => value.Kind == "element" && value.Type == 4));
            Assert.Equal("Rows[0].Cells[0].Elements[0]", element.SourcePath);
            Assert.Equal(56, element.Height);
            Assert.True(page.Height >= element.Height);
            Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x48, 2 }),
                $"Rows 输出缺少 {item.Type} 的 HRI 位置参数。");
            Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x68, 40, 0x1D, 0x77, 2 }),
                $"Rows 输出缺少 {item.Type} 的条高或模块宽度参数。");
            Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x6B, item.Symbology }),
                $"Rows 输出缺少 {item.Type} 对应的 GS k {item.Symbology} 制式。");
        }
    }

    [Fact]
    public void Sequential_template_covers_the_full_native_matrix_with_continuous_rows()
    {
        var options = Options(EscPosNativeFlowMode.Sequential);
        var result = new TemplateRenderer().RenderWithLayout(
            SequentialTemplate(), null, options: options);

        Assert.Single(result.Pages);
        var elements = result.Pages.SelectMany(page => page.Elements)
            .Where(value => value.Kind == "element" && value.Type == 4)
            .ToArray();
        Assert.Equal(Cases.Length, elements.Length);

        var output = result.Pages.SelectMany(page => page.Data).ToArray();
        foreach (var (item, index) in Cases.Select((value, index) => (value, index)))
        {
            var trace = Assert.Single(elements.Where(value =>
                value.SourcePath == $"Rows[{index}].Cells[0].Elements[0]"));
            Assert.Equal(56, trace.Height);
            Assert.True(ContainsSequence(output, new byte[] { 0x1D, 0x48, 2, 0x1D, 0x68, 40, 0x1D, 0x77, 2,
                0x1D, 0x6B, item.Symbology }),
                $"Sequential 输出缺少 {item.Type} 的 HRI、尺寸参数或 GS k {item.Symbology} 制式。");
        }

        Assert.Equal(Cases.Length, CountSequence(output, new byte[] { 0x1D, 0x48, 2 }));
    }

    [Fact]
    public void Sequential_template_covers_the_full_native_matrix_across_pages()
    {
        var options = Options(EscPosNativeFlowMode.Sequential);
        var result = new TemplateRenderer().RenderWithLayout(
            PagedSequentialTemplate(), null, options: options);

        Assert.True(result.PageCount > 1);
        Assert.Equal(Cases.Length, result.Pages.SelectMany(page => page.Elements)
            .Count(value => value.Kind == "element" && value.Type == 4));

        var output = result.Pages.SelectMany(page => page.Data).ToArray();
        var elements = result.Pages.SelectMany((page, pageIndex) => page.Elements
                .Where(value => value.Kind == "element" && value.Type == 4)
                .Select(value => (PageIndex: pageIndex, Element: value)))
            .ToArray();

        foreach (var (item, index) in Cases.Select((value, index) => (value, index)))
        {
            var trace = Assert.Single(elements.Where(value =>
                value.Element.SourcePath == $"Rows[{index}].Cells[0].Elements[0]"));
            Assert.InRange(trace.PageIndex, 0, result.PageCount - 1);
            Assert.Equal(56, trace.Element.Height);
            Assert.InRange(trace.Element.Y, 0,
                result.Pages[trace.PageIndex].Height - trace.Element.Height);
            Assert.True(ContainsSequence(output, new byte[] { 0x1D, 0x48, 2, 0x1D, 0x68, 40,
                0x1D, 0x77, 2, 0x1D, 0x6B, item.Symbology }),
                $"跨页 Sequential 输出缺少 {item.Type} 的 HRI、尺寸参数或 GS k {item.Symbology} 制式。");
        }

        Assert.Equal(Cases.Length, CountSequence(output, new byte[] { 0x1D, 0x48, 2 }));
        Assert.All(result.Pages, page => Assert.NotEmpty(page.Data));
    }

    [Fact]
    public void Rows_native_full_matrix_rejects_nonzero_quiet_zone_with_element_paths()
    {
        foreach (var item in Cases)
        {
            var error = Assert.Throws<TemplateRenderException>(() =>
                new TemplateRenderer().RenderWithLayout(
                    SingleRowTemplate(item, quietZone: PrintQuietZone.Uniform(1)), null,
                    options: Options(EscPosNativeFlowMode.Rows)));

            Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
            Assert.Contains("静区", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Rows_native_full_matrix_rejects_rotated_barcode_with_element_paths()
    {
        foreach (var item in Cases)
        {
            var error = Assert.Throws<TemplateRenderException>(() =>
                new TemplateRenderer().RenderWithLayout(
                    SingleRowTemplate(item, rotation: RotationAngle.Rotate90), null,
                    options: Options(EscPosNativeFlowMode.Rows)));

            Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
            Assert.Contains("条码旋转", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Rows_native_full_matrix_rejects_non_origin_rows_with_element_paths()
    {
        foreach (var item in Cases)
        {
            var error = Assert.Throws<TemplateRenderException>(() =>
                new TemplateRenderer().RenderWithLayout(
                    TwoRowTemplate(item), null, options: Options(EscPosNativeFlowMode.Rows)));

            Assert.Equal("Rows[1].Cells[0].Elements[0]", error.SourcePath);
            Assert.Contains("绝对坐标", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Sequential_native_full_matrix_preserves_automatic_width_and_dynamic_height()
    {
        var result = new TemplateRenderer().RenderWithLayout(
            DynamicSizeTemplate(), null, options: Options(EscPosNativeFlowMode.Sequential));

        var page = Assert.Single(result.Pages);
        var elements = page.Elements
            .Where(value => value.Kind == "element" && value.Type == 4)
            .ToArray();
        Assert.Equal(Cases.Length, elements.Length);

        foreach (var (item, index) in Cases.Select((value, index) => (value, index)))
        {
            var trace = Assert.Single(elements.Where(value =>
                value.SourcePath == $"Rows[{index}].Cells[0].Elements[0]"));
            var expectedHeight = DynamicHeight(index) + DynamicReserve(index);
            Assert.True(trace.Width > 0, $"{item.Type} 的自动条码宽度必须大于零。");
            Assert.Equal(expectedHeight, trace.Height);
            Assert.InRange(trace.Y, 0, page.Height - trace.Height);
            Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x68, (byte)DynamicHeight(index) }),
                $"{item.Type} 缺少动态条高 GS h 参数。");
        }
    }

    [Fact]
    public void Sequential_native_full_matrix_accepts_zero_quiet_zone_with_dynamic_dimensions()
    {
        foreach (var (item, index) in Cases.Select((value, index) => (value, index)))
        {
            var result = new TemplateRenderer().RenderWithLayout(
                CombinationTemplate(item, index, RotationAngle.None, nonZeroQuietZone: false), null,
                options: Options(EscPosNativeFlowMode.Sequential));

            var page = Assert.Single(result.Pages);
            var trace = Assert.Single(page.Elements.Where(value => value.Kind == "element" && value.Type == 4));
            Assert.Equal("Rows[0].Cells[0].Elements[0]", trace.SourcePath);
            Assert.True(trace.Width > 0);
            Assert.Equal(DynamicHeight(index) + DynamicReserve(index), trace.Height);
            Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x48, 2, 0x1D, 0x68,
                (byte)DynamicHeight(index), 0x1D, 0x77, 2, 0x1D, 0x6B, item.Symbology }));
        }
    }

    [Fact]
    public void Sequential_native_full_matrix_rejects_all_unsupported_parameter_combinations()
    {
        var combinations = new[]
        {
            (Rotation: RotationAngle.None, NonZeroQuietZone: true),
            (Rotation: RotationAngle.Rotate90, NonZeroQuietZone: false),
            (Rotation: RotationAngle.Rotate90, NonZeroQuietZone: true)
        };

        foreach (var (item, index) in Cases.Select((value, index) => (value, index)))
        foreach (var combination in combinations)
        {
            var error = Assert.Throws<TemplateRenderException>(() =>
                new TemplateRenderer().RenderWithLayout(
                    CombinationTemplate(item, index, combination.Rotation, combination.NonZeroQuietZone),
                    null, options: Options(EscPosNativeFlowMode.Sequential)));

            Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
            Assert.True(error.Message.Contains("静区", StringComparison.Ordinal) ||
                        error.Message.Contains("条码旋转", StringComparison.Ordinal),
                $"{item.Type} 的组合边界错误未说明静区或旋转原因：{error.Message}");
        }
    }

    [Fact]
    public void Native_full_matrix_accepts_a_profile_allowlist_for_every_declared_barcode()
    {
        var profile = FullMatrixProfile();
        var options = Options(EscPosNativeFlowMode.Rows, profile);
        var capabilities = TemplateProtocolCapabilities.Create(options);

        Assert.All(Cases, item => Assert.Contains(item.Type, capabilities.BarcodeTypes));
        foreach (var item in Cases)
        {
            var result = new TemplateRenderer().RenderWithLayout(
                SingleRowTemplate(item), null, options: options);
            var page = Assert.Single(result.Pages);
            var element = Assert.Single(page.Elements.Where(value => value.Kind == "element" &&
                value.Type == 4));

            Assert.Equal("Rows[0].Cells[0].Elements[0]", element.SourcePath);
            Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x6B, item.Symbology }));
        }
    }

    [Fact]
    public void Native_full_matrix_profile_can_disable_all_native_barcodes_before_rendering()
    {
        var profile = FullMatrixProfile();
        profile.SupportsNativeBarcode = false;
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(
                SingleRowTemplate(Cases[0]), null,
                options: Options(EscPosNativeFlowMode.Rows, profile)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("条码", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_full_matrix_profile_rejects_hri_type_and_position_outside_allowlists()
    {
        var typeProfile = FullMatrixProfile();
        typeProfile.SupportedNativeBarcodeHumanReadableTypes = new[] { BarcodeType.Code128 };
        var typeError = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(
                SingleRowTemplate(new BarcodeCase(BarcodeType.Code39, "ABC-12", 69)), null,
                options: Options(EscPosNativeFlowMode.Rows, typeProfile)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", typeError.SourcePath);
        Assert.Contains("可读文字", typeError.Message, StringComparison.Ordinal);

        var positionProfile = FullMatrixProfile();
        positionProfile.SupportedNativeBarcodeHumanReadablePositions =
            new[] { EscPosBarcodeTextPosition.Above };
        var positionError = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(
                SingleRowTemplate(Cases[0]), null,
                options: Options(EscPosNativeFlowMode.Rows, positionProfile)));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", positionError.SourcePath);
        Assert.Contains("可读文字", positionError.Message, StringComparison.Ordinal);
    }

    private static TemplateRenderOptions Options(EscPosNativeFlowMode flow,
        EscPosPrinterProfile profile = null) => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false,
        EscPosDevice = new EscPosDeviceOptions { NativeFlow = flow, Profile = profile }
    };

    private static EscPosPrinterProfile FullMatrixProfile() => new EscPosPrinterProfile
    {
        Name = "Native full matrix",
        PrintableWidthDots = 10000,
        SupportsNativeBarcode = true,
        SupportsNativeBarcodeHumanReadable = true,
        SupportedNativeBarcodeHumanReadableTypes = Cases.Select(item => item.Type)
            .Distinct().ToArray(),
        SupportedNativeBarcodeHumanReadablePositions =
            new[] { EscPosBarcodeTextPosition.Below }
    };

    private static BpTemplate SingleRowTemplate(BarcodeCase item,
        RotationAngle rotation = RotationAngle.None, PrintQuietZone quietZone = null) => new BpTemplate
    {
        Version = 2,
        Width = 10000,
        Height = 100,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Section = "Body",
                Height = 100,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 10000,
                        Elements = new List<BpElement> { Barcode(item, rotation, quietZone) }
                    }
                }
            }
        }
    };

    private static BpTemplate DynamicSizeTemplate() => new BpTemplate
    {
        Version = 2,
        Width = 10000,
        Height = 2000,
        Rows = Cases.Select((item, index) => new BpRow
        {
            Section = "Body",
            Height = DynamicHeight(index) + DynamicReserve(index),
            Cells = new List<BpCell>
            {
                new BpCell
                {
                    Width = 10000,
                    Elements = new List<BpElement>
                    {
                        Barcode(item, height: DynamicHeight(index), reserve: DynamicReserve(index))
                    }
                }
            }
        }).ToList()
    };

    private static BpTemplate CombinationTemplate(BarcodeCase item, int index,
        RotationAngle rotation, bool nonZeroQuietZone) => new BpTemplate
    {
        Version = 2,
        Width = 10000,
        Height = 200,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Section = "Body",
                Height = DynamicHeight(index) + DynamicReserve(index),
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 10000,
                        Elements = new List<BpElement>
                        {
                            Barcode(item, rotation,
                                nonZeroQuietZone ? PrintQuietZone.Uniform(1) : PrintQuietZone.Uniform(0),
                                DynamicHeight(index), DynamicReserve(index))
                        }
                    }
                }
            }
        }
    };

    private static BpTemplate TwoRowTemplate(BarcodeCase item) => new BpTemplate
    {
        Version = 2,
        Width = 10000,
        Height = 200,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Section = "Body",
                Height = 56,
                Cells = new List<BpCell>
                {
                    new BpCell { Width = 10000, Elements = new List<BpElement> { Barcode(item) } }
                }
            },
            new BpRow
            {
                Section = "Body",
                Height = 56,
                Cells = new List<BpCell>
                {
                    new BpCell { Width = 10000, Elements = new List<BpElement> { Barcode(item) } }
                }
            }
        }
    };

    private static BpTemplate SequentialTemplate() => new BpTemplate
    {
        Version = 2,
        Width = 10000,
        Height = 2000,
        Rows = Cases.Select((item, index) => new BpRow
        {
            Section = "Body",
            Height = 56,
            Cells = new List<BpCell>
            {
                new BpCell
                {
                    Width = 10000,
                    Elements = new List<BpElement> { Barcode(item) }
                }
            }
        }).ToList()
    };

    private static BpTemplate PagedSequentialTemplate() => new BpTemplate
    {
        Version = 2,
        Width = 10000,
        Height = 336,
        Rows = Cases.Select(item => new BpRow
        {
            Section = "Body",
            Height = 56,
            Cells = new List<BpCell>
            {
                new BpCell
                {
                    Width = 10000,
                    Elements = new List<BpElement> { Barcode(item) }
                }
            }
        }).ToList()
    };

    private static int DynamicHeight(int index) => 28 + index % 4 * 8;

    private static int DynamicReserve(int index) => 8 + index % 3 * 4;

    private static BpBarcodeElement Barcode(BarcodeCase item,
        RotationAngle rotation = RotationAngle.None, PrintQuietZone quietZone = null,
        int height = 40, int reserve = 16) => new BpBarcodeElement
    {
        BarcodeType = item.Type,
        LineWidth = 1,
        Ratio = 2,
        NarrowModuleWidth = 2,
        Width = 0,
        Height = height,
        Rotation = rotation,
        HumanReadable = true,
        HumanReadableReserveHeight = reserve,
        QuietZone = quietZone,
        Field = new BgField { Value = item.Data }
    };

    private static bool ContainsSequence(byte[] data, byte[] sequence)
        => CountSequence(data, sequence) > 0;

    private static int CountSequence(byte[] data, byte[] sequence)
    {
        var count = 0;
        for (var start = 0; start <= data.Length - sequence.Length; start++)
        {
            if (!data.Skip(start).Take(sequence.Length).SequenceEqual(sequence)) continue;
            count++;
        }
        return count;
    }

    private sealed class BarcodeCase
    {
        internal BarcodeCase(BarcodeType type, string data, byte symbology)
        {
            Type = type;
            Data = data;
            Symbology = symbology;
        }

        internal BarcodeType Type { get; }
        internal string Data { get; }
        internal byte Symbology { get; }
    }
}
