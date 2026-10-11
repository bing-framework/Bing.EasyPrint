using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 ESC/POS Native Sequential 模式在模板层拒绝不支持的条码和文字旋转。
/// </summary>
public sealed class TemplateEscPosNativeSequentialRotationBoundaryTests
{
    [Fact]
    public void Sequential_template_rejects_rotated_barcode_before_opening_image_resource()
    {
        var opened = 0;
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(
                InvalidTemplate(new BpBarcodeElement
                {
                    BarcodeType = BarcodeType.Code39,
                    LineWidth = 1,
                    Ratio = 2,
                    NarrowModuleWidth = 3,
                    Width = 500,
                    Height = 40,
                    Rotation = RotationAngle.Rotate90,
                    HumanReadable = false,
                    Field = new BgField { Value = "ABC-12" }
                }),
                null,
                new CountingResources(() => opened++),
                Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("条码旋转", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    [Fact]
    public void Sequential_template_rejects_rotated_text_before_opening_image_resource()
    {
        var opened = 0;
        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(
                InvalidTemplate(new BpFontElement
                {
                    FontSize = 24,
                    Rotation = RotationAngle.Rotate90,
                    Field = new BgField { Value = "ROTATED" }
                }),
                null,
                new CountingResources(() => opened++),
                Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("文字旋转", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    private static TemplateRenderOptions Options() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = System.Text.Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false,
        EscPosDevice = new EscPosDeviceOptions
        {
            NativeFlow = EscPosNativeFlowMode.Sequential
        }
    };

    private static BpTemplate InvalidTemplate(BpElement invalid) => new BpTemplate
    {
        Version = 2,
        Width = 600,
        Height = 80,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Section = "Body",
                Height = 56,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 600,
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
