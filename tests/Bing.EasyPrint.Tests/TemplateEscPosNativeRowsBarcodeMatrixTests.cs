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
/// 验证 ESC/POS Native Rows 模式的常用一维条码能够从模板模型进入对应的 GS k 制式。
/// </summary>
public sealed class TemplateEscPosNativeRowsBarcodeMatrixTests
{
    private static readonly BarcodeCase[] Cases =
    {
        new BarcodeCase(BarcodeType.Code39, "ABC-12", "ABC-12", 69,
            "Rows[0].Cells[0].Elements[0]"),
        new BarcodeCase(BarcodeType.Code93, "CODE93", "CODE93", 72,
            "Rows[0].Cells[0].Elements[0]"),
        new BarcodeCase(BarcodeType.Codabar, "A12B", "A12B", 71,
            "Rows[0].Cells[0].Elements[0]"),
        new BarcodeCase(BarcodeType.I2OF5, "1234", "1234", 70,
            "Rows[0].Cells[0].Elements[0]"),
        new BarcodeCase(BarcodeType.I2OF5C, "1234", "012348", 70,
            "Rows[0].Cells[0].Elements[0]"),
        new BarcodeCase(BarcodeType.UccEan128, "0101234567890128", "0101234567890128", 74,
            "Rows[0].Cells[0].Elements[0]")
    };

    [Fact]
    public void Rows_capabilities_expose_the_native_barcode_matrix()
    {
        var capabilities = TemplateProtocolCapabilities.Create(Options());

        Assert.Equal(EscPosNativeFlowMode.Rows, capabilities.NativeFlow);
        Assert.All(Cases, item => Assert.Contains(item.Type, capabilities.BarcodeTypes));
    }

    [Fact]
    public void Rows_template_preserves_source_paths_sizes_and_gs_k_payloads()
    {
        foreach (var item in Cases)
        {
            var result = new TemplateRenderer().RenderWithLayout(
                BarcodeTemplate(item), null, options: Options());

            Assert.Equal(PrintProtocol.EscPos, result.Protocol);
            var page = Assert.Single(result.Pages);
            var element = Assert.Single(page.Elements
                .Where(candidate => candidate.Kind == "element" && candidate.Type == 4));
            Assert.Equal(item.SourcePath, element.SourcePath);
            Assert.Equal(4, element.Type);
            Assert.Equal(500, element.Width);
            Assert.Equal(40, element.Height);
            Assert.InRange(element.Y, 0, page.Height - element.Height);

            Assert.True(ContainsSequence(page.Data, NativeBarcode(item.Symbology, item.Payload)),
                $"Native Rows 输出缺少 {item.Type} 对应的 GS k {item.Symbology} 及其 payload。");
        }
    }

    [Fact]
    public void Rows_template_rejects_invalid_codabar_with_element_path()
    {
        var template = BarcodeTemplate(new BarcodeCase(
            BarcodeType.Codabar, "123", "123", 71,
            "Rows[0].Cells[0].Elements[0]"));

        var error = Assert.Throws<TemplateRenderException>(() =>
            new TemplateRenderer().RenderWithLayout(template, null, options: Options()));

        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
        Assert.Contains("Codabar", error.Message, StringComparison.Ordinal);
    }

    private static TemplateRenderOptions Options() => new TemplateRenderOptions
    {
        Protocol = PrintProtocol.EscPos,
        EscPosLayout = EscPosLayoutMode.Native,
        Encoding = Encoding.ASCII,
        FeedLines = 0,
        CutAfterPrint = false,
        EscPosDevice = new EscPosDeviceOptions
        {
            NativeFlow = EscPosNativeFlowMode.Rows
        }
    };

    private static BpTemplate BarcodeTemplate(BarcodeCase item) => new BpTemplate
    {
        Version = 2,
        Width = 600,
        Height = 40,
        Rows = new List<BpRow> { BarcodeRow(item) }
    };

    private static BpRow BarcodeRow(BarcodeCase item) => new BpRow
    {
        Section = "Body",
        Height = 40,
        Cells = new List<BpCell>
        {
            new BpCell
            {
                Width = 600,
                Elements = new List<BpElement> { Barcode(item.Type, item.Value) }
            }
        }
    };

    private static BpBarcodeElement Barcode(BarcodeType type, string value) => new BpBarcodeElement
    {
        BarcodeType = type,
        LineWidth = 1,
        Ratio = 2,
        Width = 500,
        Height = 40,
        HumanReadable = false,
        Field = new BgField { Value = value }
    };

    private static byte[] NativeBarcode(byte symbology, string data)
    {
        var payload = Encoding.ASCII.GetBytes(data);
        return new byte[] { 0x1D, 0x48, 0, 0x1D, 0x68, 40, 0x1D, 0x77, 2,
            0x1D, 0x6B, symbology, (byte)payload.Length }
            .Concat(payload)
            .ToArray();
    }

    private static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        for (var start = 0; start <= data.Length - sequence.Length; start++)
        {
            var matched = true;
            for (var offset = 0; offset < sequence.Length; offset++)
            {
                if (data[start + offset] == sequence[offset]) continue;
                matched = false;
                break;
            }
            if (matched) return true;
        }
        return false;
    }

    private sealed class BarcodeCase
    {
        internal BarcodeCase(BarcodeType type, string value, string payload, byte symbology,
            string sourcePath)
        {
            Type = type;
            Value = value;
            Payload = payload;
            Symbology = symbology;
            SourcePath = sourcePath;
        }

        internal BarcodeType Type { get; }
        internal string Value { get; }
        internal string Payload { get; }
        internal byte Symbology { get; }
        internal string SourcePath { get; }
    }
}
