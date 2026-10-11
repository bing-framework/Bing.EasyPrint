using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 ESC/POS Native Rows 模式的条码参数和严格能力边界。</summary>
public sealed class TemplateEscPosNativeParameterMatrixTests
{
    [Theory]
    [InlineData(BarcodeType.Code128, "1234", 73)]
    [InlineData(BarcodeType.Ean13, "4006381333931", 67)]
    public void Rows_template_preserves_hri_reserve_height_and_native_barcode(
        BarcodeType type, string data, byte symbology)
    {
        var result = new TemplateRenderer().RenderWithLayout(
            BarcodeTemplate(new BpBarcodeElement
            {
                BarcodeType = type,
                Height = 40,
                Width = 240,
                LineWidth = 1,
                Ratio = 2,
                HumanReadable = true,
                HumanReadableReserveHeight = 16,
                Field = new BgField { Value = data }
            }),
            null,
            options: Options());

        var page = Assert.Single(result.Pages);
        var element = Assert.Single(page.Elements.Where(item => item.Kind == "element"));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", element.SourcePath);
        Assert.Equal(4, element.Type);
        Assert.Equal(56, element.Height);
        Assert.Equal(56, page.Height);
        Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x48, 0x02 }),
            "HRI 应显式选择为条码下方显示。");
        Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x6B, symbology }),
            $"Native 输出缺少 {type} 对应的 GS k {symbology} 指令。");
    }

    [Fact]
    public void Rows_template_allows_explicit_zero_quiet_zone()
    {
        var result = new TemplateRenderer().RenderWithLayout(
            BarcodeTemplate(new BpBarcodeElement
            {
                BarcodeType = BarcodeType.Code128,
                Height = 40,
                Width = 240,
                LineWidth = 1,
                Ratio = 2,
                HumanReadable = false,
                QuietZone = PrintQuietZone.Uniform(0),
                Field = new BgField { Value = "1234" }
            }),
            null,
            options: Options());

        var page = Assert.Single(result.Pages);
        var element = Assert.Single(page.Elements.Where(item => item.Kind == "element"));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", element.SourcePath);
        Assert.Equal(40, element.Height);
        Assert.True(ContainsSequence(page.Data, new byte[] { 0x1D, 0x6B, 73 }));
    }

    [Fact]
    public void Rows_template_rejects_nonzero_quiet_zone_before_opening_next_resource()
    {
        var opened = 0;
        var template = InvalidParameterTemplate(new BpBarcodeElement
        {
            BarcodeType = BarcodeType.Code128,
            Height = 40,
            Width = 220,
            LineWidth = 1,
            Ratio = 2,
            HumanReadable = false,
            QuietZone = PrintQuietZone.Uniform(2),
            Field = new BgField { Value = "1234" }
        });

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, null, new CountingResources(() => opened++), Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("静区", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    [Fact]
    public void Rows_template_rejects_rotated_barcode_before_opening_next_resource()
    {
        var opened = 0;
        var template = InvalidParameterTemplate(new BpBarcodeElement
        {
            BarcodeType = BarcodeType.Code128,
            Height = 40,
            Width = 220,
            LineWidth = 1,
            Ratio = 2,
            Rotation = RotationAngle.Rotate90,
            HumanReadable = false,
            Field = new BgField { Value = "1234" }
        });

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, null, new CountingResources(() => opened++), Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("条码旋转", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    [Fact]
    public void Rows_template_rejects_rotated_text_before_opening_next_resource()
    {
        var opened = 0;
        var template = InvalidParameterTemplate(new BpFontElement
        {
            FontSize = 24,
            Rotation = RotationAngle.Rotate90,
            Field = new BgField { Value = "ROTATED" }
        });

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().RenderWithLayout(
            template, null, new CountingResources(() => opened++), Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("文字旋转", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    private static TemplateRenderOptions Options() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false,
        EscPosDevice = new Protocols.EscPosDeviceOptions
        {
            NativeFlow = EscPosNativeFlowMode.Rows
        }
    };

    private static BpTemplate BarcodeTemplate(BpBarcodeElement barcode) => new BpTemplate
    {
        Version = 2,
        Width = 280,
        Height = 56,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Height = 56,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 280,
                        Elements = new List<BpElement> { barcode }
                    }
                }
            }
        }
    };

    private static BpTemplate InvalidParameterTemplate(BpElement invalid) => new BpTemplate
    {
        Version = 2,
        Width = 280,
        Height = 80,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Height = 56,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 280,
                        Elements = new List<BpElement>
                        {
                            invalid,
                            new BpImageElement
                            {
                                Width = 8,
                                Height = 1,
                                Field = new BgField { Value = "unused-resource" }
                            }
                        }
                    }
                }
            }
        }
    };

    private static bool ContainsSequence(byte[] value, byte[] sequence)
    {
        for (var start = 0; start <= value.Length - sequence.Length; start++)
            if (value.Skip(start).Take(sequence.Length).SequenceEqual(sequence)) return true;
        return false;
    }

    private sealed class CountingResources : ITemplateResourceProvider
    {
        private readonly Action _onOpen;

        internal CountingResources(Action onOpen) => _onOpen = onOpen;

        public Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            _onOpen();
            return null;
        }
    }
}
