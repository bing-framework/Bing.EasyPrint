using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 ESC/POS Native 的兼容行模式和显式 Sequential 混合收据模式。
/// </summary>
public sealed class EscPosNativeFlowTests
{
    [Fact]
    public void Capabilities_expose_native_flow_and_reject_invalid_mode_combinations()
    {
        var rows = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Rows });
        var sequential = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Sequential });

        Assert.Equal(EscPosNativeFlowMode.Rows, rows.Capabilities.NativeFlow);
        Assert.Equal(EscPosNativeFlowMode.Sequential, sequential.Capabilities.NativeFlow);
        Assert.Null(PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Raster).Capabilities.NativeFlow);
        Assert.Throws<ArgumentException>(() => PrintProtocolProviderFactory.Create(PrintProtocol.EscPos,
            Encoding.ASCII, EscPosLayoutMode.Raster,
            new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Sequential }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EscPosPrintCommand(Encoding.ASCII,
            EscPosLayoutMode.Native,
            new EscPosDeviceOptions { NativeFlow = (EscPosNativeFlowMode)99 }));
    }

    [Fact]
    public void Rows_mode_preserves_the_existing_native_bytes()
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 180);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintTextElement(0, 0, "TITLE", 24), "Rows[0].Title");
        page.Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128, "1234", 60,
            narrowWidth: 2, wideWidth: 2, reservedWidth: 250), "Rows[1].Barcode");

        var legacy = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native);
        var explicitRows = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Rows });

        Assert.Equal(legacy.Render(document)[0], explicitRows.Render(document)[0]);
    }

    [Fact]
    public void Sequential_mixed_receipt_matches_an_independent_native_command()
    {
        var options = new EscPosDeviceOptions
        {
            NativeFlow = EscPosNativeFlowMode.Sequential,
            NativeTabWidthDots = 80
        };
        var document = new PrintDocument();
        var page = document.AddPage(320, 230);
        page.FeedLines = 0;
        page.CutAfterPrint = false;
        page.Add(new PrintTextElement(0, 0, "TITLE", 24), "Receipt.Title");
        page.Add(new PrintTextElement(0, 26, "ITEM", 24), "Receipt.Detail.Name");
        page.Add(new PrintTextElement(80, 26, "1", 24), "Receipt.Detail.Quantity");
        page.Add(new PrintBarcodeElement(0, 52, BarcodeType.Code128, "1234", 60,
            narrowWidth: 2, wideWidth: 2, reservedWidth: 250), "Receipt.Barcode");
        page.Add(new PrintQrCodeElement(0, 112, "QR", 4, QrCodeCorrectionLevel.M,
            width: 84, height: 84), "Receipt.Qr");
        page.Add(new PrintTextElement(0, 196, "TOTAL", 24), "Receipt.Footer");

        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, options);
        var actual = provider.Render(document)[0];

        var expected = new EscPosPrintCommand(Encoding.ASCII, EscPosLayoutMode.Native, options)
            .Initialize()
            .WriteText("TITLE")
            .NewLine()
            .WriteText("ITEM")
            .Tab()
            .WriteText("1")
            .NewLine()
            .Barcode("1234", 73, 60, 2, 0)
            .QrCode("QR", 4, 49)
            .WriteText("TOTAL")
            .NewLine()
            .Build()
            .GetBytes();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Sequential_reports_page_element_and_source_path_for_reverse_cursor()
    {
        var document = new PrintDocument();
        document.AddPage(320, 230)
            .Add(new PrintTextElement(0, 0, "TITLE", 24), "first-page");
        var second = document.AddPage(320, 230);
        second.Add(new PrintBarcodeElement(0, 52, BarcodeType.Code128, "1234", 60,
            narrowWidth: 2, wideWidth: 2, reservedWidth: 250), "page2.barcode");
        second.Add(new PrintTextElement(0, 100, "BACK", 24), "page2.footer");

        var provider = SequentialProvider();
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Render(document));

        Assert.Equal("page2.footer", error.ElementPath);
        Assert.Equal(1, error.PageIndex);
        Assert.Equal(1, error.ElementIndex);
    }

    [Fact]
    public void Sequential_rejects_non_line_gap_before_emitting_output()
    {
        var document = new PrintDocument();
        var page = document.AddPage(320, 120);
        page.Add(new PrintTextElement(0, 0, "TITLE", 24), "Receipt.Title");
        page.Add(new PrintBarcodeElement(0, 53, BarcodeType.Code128, "1234", 60,
            narrowWidth: 2, wideWidth: 2, reservedWidth: 250), "Receipt.Barcode");

        var error = Assert.Throws<PrintCapabilityException>(() => SequentialProvider().Render(document));

        Assert.Equal("Receipt.Barcode", error.ElementPath);
        Assert.Equal(0, error.PageIndex);
        Assert.Equal(1, error.ElementIndex);
        Assert.Contains("整行", error.Message);
    }

    [Fact]
    public void Sequential_requires_declared_positive_code_placeholders()
    {
        var barcodeDocument = new PrintDocument();
        barcodeDocument.AddPage(320, 120).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "1234", 60, narrowWidth: 2, wideWidth: 2), "Receipt.Barcode");
        var barcodeError = Assert.Throws<PrintElementValidationException>(() =>
            SequentialProvider().Render(barcodeDocument));
        Assert.Equal("Receipt.Barcode", barcodeError.ElementPath);
        Assert.Equal(0, barcodeError.PageIndex);
        Assert.Equal(0, barcodeError.ElementIndex);

        var qrDocument = new PrintDocument();
        qrDocument.AddPage(320, 120).Add(new PrintQrCodeElement(0, 0, "QR", 4,
            QrCodeCorrectionLevel.M, width: 84, height: 0), "Receipt.Qr");
        var qrError = Assert.Throws<PrintElementValidationException>(() => SequentialProvider().Render(qrDocument));
        Assert.Equal("Receipt.Qr", qrError.ElementPath);
        Assert.Equal(0, qrError.PageIndex);
        Assert.Equal(0, qrError.ElementIndex);
    }

    [Fact]
    public void Sequential_requires_hri_reserve_height_when_hri_is_enabled()
    {
        var document = new PrintDocument();
        document.AddPage(320, 120).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "1234", 60, narrowWidth: 2, wideWidth: 2, humanReadable: true,
            reservedWidth: 250), "Receipt.Barcode");

        var error = Assert.Throws<PrintElementValidationException>(() => SequentialProvider().Render(document));

        Assert.Equal("Receipt.Barcode", error.ElementPath);
        Assert.Contains("预留高度", error.Message);
    }

    [Fact]
    public void Sequential_rejects_nonzero_quiet_zone_and_unsupported_graphics()
    {
        var quietDocument = new PrintDocument();
        quietDocument.AddPage(320, 120).Add(new PrintBarcodeElement(0, 0, BarcodeType.Code128,
            "1234", 60, PrintQuietZone.Uniform(1), narrowWidth: 2, wideWidth: 2,
            reservedWidth: 250), "Receipt.Barcode");
        var quietError = Assert.Throws<PrintCapabilityException>(() => SequentialProvider().Render(quietDocument));
        Assert.Equal("Receipt.Barcode", quietError.ElementPath);

        var graphicDocument = new PrintDocument();
        graphicDocument.AddPage(320, 120).Add(new PrintImageElement(0, 0, 8, 1, new byte[] { 0x80 }),
            "Receipt.Image");
        var graphicError = Assert.Throws<PrintCapabilityException>(() => SequentialProvider().Render(graphicDocument));
        Assert.Equal("Receipt.Image", graphicError.ElementPath);
    }

    [Fact]
    public void Sequential_honors_device_profile_native_command_capabilities()
    {
        var options = new EscPosDeviceOptions
        {
            NativeFlow = EscPosNativeFlowMode.Sequential,
            Profile = new EscPosPrinterProfile { Name = "ReceiptWithoutQr", SupportsNativeQrCode = false }
        };
        var document = new PrintDocument();
        document.AddPage(320, 120).Add(new PrintQrCodeElement(0, 0, "QR", 4,
            QrCodeCorrectionLevel.M, width: 84, height: 84), "Receipt.Qr");

        var error = Assert.Throws<PrintCapabilityException>(() => PrintProtocolProviderFactory.Create(
            PrintProtocol.EscPos, Encoding.ASCII, EscPosLayoutMode.Native, options).Render(document));

        Assert.Equal("Receipt.Qr", error.ElementPath);
        Assert.Contains("原生二维码", error.Message);
    }

    [Fact]
    public void Sequential_observes_cancellation_before_rendering()
    {
        var document = new PrintDocument();
        document.AddPage(320, 120).Add(new PrintTextElement(0, 0, "TITLE", 24));
        var tokenSource = new CancellationTokenSource();
        tokenSource.Cancel();

        Assert.Throws<OperationCanceledException>(() => SequentialProvider().Render(document, tokenSource.Token));
    }

    [Fact]
    public void Sequential_is_rejected_for_raster_layout_even_when_requested_through_template_options()
    {
        Assert.Throws<ArgumentException>(() => new EscPosPrintCommand(Encoding.ASCII,
            EscPosLayoutMode.Raster, new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Sequential }));

        var template = new BpTemplate { Width = 320, Height = 40, Rows = new List<BpRow>
        {
            TextRow("TITLE", 24)
        } };
        Assert.Throws<ArgumentException>(() => new TemplateRenderer().Render(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                Encoding = Encoding.ASCII,
                EscPosLayout = EscPosLayoutMode.Raster,
                EscPosDevice = new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Sequential }
            }));
    }

    [Fact]
    public void Version3_sequential_template_tracks_mixed_receipt_layout_and_auto_height()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 320,
            Height = 0,
            AutoHeight = true,
            Rows = new List<BpRow>
            {
                TextRow("TITLE", 26),
                new BpRow { Height = 26, Cells = new List<BpCell>
                {
                    new BpCell { Width = 160, Elements = new List<BpElement> { Text("ITEM") } },
                    new BpCell { Width = 160, Elements = new List<BpElement> { Text("1") } }
                } },
                new BpRow { Height = 60, Cells = new List<BpCell> { new BpCell { Width = 320,
                    Elements = new List<BpElement> { new BpBarcodeElement { Height = 60, Width = 250,
                        BarcodeType = BarcodeType.Code128, LineWidth = 1, Ratio = 2,
                        HumanReadable = false, Field = Value("1234") } } } } },
                new BpRow { Height = 84, Cells = new List<BpCell> { new BpCell { Width = 320,
                    Elements = new List<BpElement> { new BpQrCodeElement { UnitWidth = 4,
                        ErrorLevel = "M", Width = 84, Height = 84, QuietZone = PrintQuietZone.Uniform(0),
                        Field = Value("QR") } } } } },
                TextRow("TOTAL", 26)
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template, null, options: new TemplateRenderOptions
        {
            Protocol = PrintProtocol.EscPos,
            Encoding = Encoding.ASCII,
            EscPosLayout = EscPosLayoutMode.Native,
            Copies = 1,
            FeedLines = 0,
            CutAfterPrint = false,
            EscPosDevice = new EscPosDeviceOptions
            {
                NativeFlow = EscPosNativeFlowMode.Sequential,
                NativeTabWidthDots = 160
            }
        });

        var page = Assert.Single(result.Pages);
        Assert.Equal(222, page.Height);
        Assert.Equal(new[] { 0, 26, 26, 52, 112, 196 },
            page.Elements.Where(element => element.Kind == "element").Select(element => element.Y));
        Assert.Equal(new[] { 1, 1, 1, 4, 3, 1 },
            page.Elements.Where(element => element.Kind == "element").Select(element => element.Type));
        Assert.Contains((byte)'T', page.Data);
        Assert.True(ContainsSequence(page.Data, Encoding.ASCII.GetBytes("TOTAL")));
    }

    [Fact]
    public void Version3_sequential_template_can_page_long_repeated_text()
    {
        var template = new BpTemplate
        {
            Version = 3,
            Width = 160,
            Height = 80,
            Rows = new List<BpRow>
            {
                TextRow("HEADER", 26, 160),
                new BpRow { Height = 26, RepeatSource = "Items", Cells = new List<BpCell> { new BpCell
                {
                    Width = 160,
                    Elements = new List<BpElement> { new BpFontElement { FontSize = 24,
                        Field = new BgField { Name = "Name" } } }
                } } },
                TextRow("FOOTER", 26, 160)
            }
        };

        var result = new TemplateRenderer().RenderWithLayout(template,
            new { Items = Enumerable.Range(0, 8).Select(index => new { Name = "ROW" + index }) },
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                Encoding = Encoding.ASCII,
                EscPosLayout = EscPosLayoutMode.Native,
                FeedLines = 0,
                CutAfterPrint = false,
                EscPosDevice = new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Sequential }
            });

        Assert.True(result.PageCount > 1);
        Assert.All(result.Pages, page => Assert.NotEmpty(page.Data));
        var layoutText = result.Pages.SelectMany(page => page.Elements)
            .Where(element => element.Type == 1)
            .Select(element => element.Text);
        foreach (var index in Enumerable.Range(0, 8)) Assert.Contains("ROW" + index, layoutText);
    }

    [Fact]
    public void Sequential_template_rejects_native_image_before_opening_resource()
    {
        var opened = 0;
        var template = new BpTemplate
        {
            Version = 3,
            Width = 160,
            Height = 80,
            Rows = new List<BpRow> { new BpRow { Height = 24, Cells = new List<BpCell> { new BpCell
            {
                Width = 160,
                Elements = new List<BpElement> { new BpImageElement { Width = 8, Height = 1,
                    Field = Value("logo") } }
            } } } }
        };

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(template, null,
            new CountingResources(() => opened++), new TemplateRenderOptions
            {
                Protocol = PrintProtocol.EscPos,
                Encoding = Encoding.ASCII,
                EscPosLayout = EscPosLayoutMode.Native,
                EscPosDevice = new EscPosDeviceOptions { NativeFlow = EscPosNativeFlowMode.Sequential }
            }));

        Assert.Contains("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Equal(0, opened);
    }

    private static IPrintProtocolProvider SequentialProvider()
        => PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, Encoding.ASCII,
            EscPosLayoutMode.Native, new EscPosDeviceOptions
            {
                NativeFlow = EscPosNativeFlowMode.Sequential,
                NativeTabWidthDots = 80
            });

    private static BpRow TextRow(string value, int height, int width = 320)
        => new BpRow { Height = height, Cells = new List<BpCell> { new BpCell { Width = width,
            Elements = new List<BpElement> { Text(value) } } } };

    private static BpFontElement Text(string value)
        => new BpFontElement { FontSize = 24, Field = Value(value) };

    private static BgField Value(string value) => new BgField { Value = value };

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
            if (data.Skip(start).Take(sequence.Length).SequenceEqual(sequence)) return true;
        return false;
    }

    private sealed class CountingResources : ITemplateResourceProvider
    {
        private readonly Action _onOpen;

        internal CountingResources(Action onOpen) => _onOpen = onOpen;

        public System.IO.Stream OpenRead(string resourceKey, CancellationToken cancellationToken)
        {
            _onOpen();
            return null;
        }
    }
}
