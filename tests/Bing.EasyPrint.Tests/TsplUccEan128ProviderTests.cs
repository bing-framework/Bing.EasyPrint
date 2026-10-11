using System;
using System.Collections.Generic;
using System.Text;
using Bing.EasyPrint.Elements;
using Bing.EasyPrint.Model;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Bing.EasyPrint.Templates;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证 TSPL EAN128（UCC/EAN-128）的 FNC1 编码、Provider、模板和严格边界。</summary>
public sealed class TsplUccEan128ProviderTests
{
    [Fact]
    public void Direct_command_emits_ean128_and_converts_group_separator_to_fnc1()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, "0101234567890128\u001d10ABC", 60, 0, 0, 2, 4)
            .Build()
            .GetBytes());

        Assert.Equal(
            "BARCODE 10,20,\"EAN128\",60,0,0,2,4,\"0101234567890128!10210ABC\"\r\n",
            output);
    }

    [Fact]
    public void Direct_command_accepts_ascii_data_without_adding_literal_group_separator()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .Barcode(1, 2, "EAN128", 40, 1, 0, 2, 2, "0101234567890128")
            .Build()
            .GetBytes());

        Assert.Contains("\"EAN128\",40,1,0,2,2,\"0101234567890128\"", output);
        Assert.DoesNotContain('\u001d', output);
    }

    [Fact]
    public void Direct_command_normalizes_bracketed_ai_sequence_before_tspl_fnc1_escape()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, "(01)01234567890128(10)ABC(17)250101",
                40, 1, 0, 2, 2)
            .Build().GetBytes());

        Assert.Contains("\"EAN128\",40,1,0,2,2,\"010123456789012810ABC!10217250101\"", output);
    }

    [Fact]
    public void Direct_command_decodes_literal_parenthesis_in_bracketed_ai_value()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, "(91)AB\\(C)D(17)250101", 40, 1, 0, 2, 2)
            .Build().GetBytes());

        Assert.Contains("\"EAN128\",40,1,0,2,2,\"91AB(C)D!10217250101\"", output);
    }

    [Fact]
    public void Direct_command_normalizes_supported_compressed_ai_before_tspl_fnc1_escape()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, "(01)01234567890128(10)ABC(3103)000123",
                40, 1, 0, 2, 2)
            .Build().GetBytes());

        Assert.Contains("\"EAN128\",40,1,0,2,2,\"010123456789012810ABC!1023103000123\"", output);
    }

    [Fact]
    public void Direct_command_normalizes_amount_and_discount_ai_before_tspl_fnc1_escape()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20,
                "(3909)1(3910)8401(3947)1234(3959)123456(17)250101", 40, 1, 0, 2, 2)
            .Build().GetBytes());

        Assert.Contains(
            "\"EAN128\",40,1,0,2,2,\"39091!10239108401!10239471234395912345617250101\"",
            output);
    }

    [Fact]
    public void Direct_command_normalizes_currency_price_ai_before_tspl_fnc1_escape()
    {
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, "(01)01234567890128(3930)84012345(10)ABC",
                40, 1, 0, 2, 2)
            .Build().GetBytes());

        Assert.Contains("\"EAN128\",40,1,0,2,2,\"0101234567890128393084012345!10210ABC\"", output);
    }

    [Fact]
    public void Direct_command_accepts_ai_91_company_internal_data_up_to_90_characters()
    {
        var value = new string('A', 90);
        var output = Encoding.ASCII.GetString(new TsplPrintCommand(Encoding.ASCII)
            .BarcodeUccEan128(10, 20, $"(91){value}(17)250101", 40, 1, 0, 2, 2)
            .Build().GetBytes());

        Assert.Contains($"\"EAN128\",40,1,0,2,2,\"91{value}!10217250101\"", output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("A10123")]
    [InlineData("010\u0001ABC")]
    [InlineData("010!102ABC")]
    [InlineData("(01)01234567890128(1234)ABC")]
    [InlineData("(01)01234567890128(3103)12345")]
    [InlineData("(01)01234567890128(3930)840")]
    public void Direct_command_rejects_invalid_data_without_polluting_output(string data)
    {
        var command = new TsplPrintCommand(Encoding.ASCII).Text(1, 2, "before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeUccEan128(10, 20, data, 40, 0));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Provider_and_capabilities_expose_ucc_ean128()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII);

        Assert.True(provider.Capabilities.SupportsBarcodeType(BarcodeType.UccEan128));
        Assert.True(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.UccEan128));

        var output = Encoding.ASCII.GetString(provider.Render(BarcodeDocument(
            "0101234567890128\u001d10ABC", humanReadable: false))[0]);
        Assert.Contains("BARCODE 0,0,\"EAN128\",40,0,0,2,2,\"0101234567890128!10210ABC\"\r\n", output);
    }

    [Fact]
    public void Provider_renders_bracketed_ai_sequence_after_width_preflight()
    {
        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Tspl, Encoding.ASCII)
            .Render(BarcodeDocument("(01)01234567890128(10)ABC(17)250101"))[0]);

        Assert.Contains("\"EAN128\",40,0,0,2,2,\"010123456789012810ABC!10217250101\"", output);
    }

    [Fact]
    public void Provider_renders_compressed_ai_sequence_after_width_preflight()
    {
        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Tspl, Encoding.ASCII)
            .Render(BarcodeDocument("(01)01234567890128(10)ABC(3103)000123", humanReadable: false))[0]);

        Assert.Contains("BARCODE 0,0,\"EAN128\",40,0,0,2,2,\"010123456789012810ABC!1023103000123\"\r\n",
            output);
    }

    [Fact]
    public void Template_renders_variable_amount_ai_with_fnc1_after_width_preflight()
    {
        const string data = "39091\u001D39108401";
        var result = new TemplateRenderer().RenderWithLayout(Template(data), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var output = Encoding.ASCII.GetString(Assert.Single(result.Pages).Data);
        Assert.Contains(
            "\"EAN128\",40,0,0,2,2,\"39091!10239108401\"",
            output);
    }

    [Fact]
    public void Provider_rejects_invalid_data_with_element_path()
    {
        var error = Assert.Throws<PrintElementValidationException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII)
                .Validate(BarcodeDocument("010\u0001ABC", humanReadable: false)));

        Assert.Equal(PrintProtocol.Tspl, error.Protocol);
        Assert.Equal("Barcode", error.ElementPath);
        Assert.Contains("GS1-128", error.Message);
    }

    [Fact]
    public void Profile_can_disable_ucc_ean128_before_rendering()
    {
        var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII,
            tsplOptions: new TsplMediaOptions
            {
                Profile = new TsplPrinterProfile { SupportsNativeBarcode = false }
            });

        Assert.False(provider.Capabilities.SupportsBarcodeType(BarcodeType.UccEan128));
        Assert.False(provider.Capabilities.SupportsCommandBarcodeType(BarcodeType.UccEan128));
        var error = Assert.Throws<PrintCapabilityException>(() => provider.Validate(
            BarcodeDocument("0101234567890128", humanReadable: false)));
        Assert.Equal("Barcode", error.ElementPath);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(180, 180)]
    [InlineData(270, 270)]
    public void Provider_preserves_native_rotation_for_ucc_ean128(int rotation, int encodedRotation)
    {
        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Tspl, Encoding.ASCII)
            .Render(BarcodeDocument("0101234567890128", (RotationAngle)rotation, false))[0]);

        Assert.Contains($"\"EAN128\",40,0,{encodedRotation},2,2,\"0101234567890128\"", output);
    }

    [Fact]
    public void Template_maps_ucc_ean128_and_preserves_element_path_on_invalid_data()
    {
        var result = new TemplateRenderer().RenderWithLayout(Template("0101234567890128\u001d10ABC"), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        Assert.Contains("\"EAN128\",40,0,0,2,2,\"0101234567890128!10210ABC\"",
            Encoding.ASCII.GetString(Assert.Single(result.Pages).Data));

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template("010\u0001ABC"), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Tspl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            }));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    private static PrintDocument BarcodeDocument(string data, RotationAngle rotation = RotationAngle.None,
        bool humanReadable = false)
    {
        var document = new PrintDocument();
        document.AddPage(1000, 1000).Add(new PrintBarcodeElement(0, 0,
            BarcodeType.UccEan128, data, 40, 2, 2, rotation,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate Template(string data) => new BpTemplate
    {
        Version = 1,
        Width = 1200,
        Height = 120,
        Rows = new List<BpRow>
        {
            new BpRow
            {
                Height = 120,
                Cells = new List<BpCell>
                {
                    new BpCell
                    {
                        Width = 1200,
                        Elements = new List<BpElement>
                        {
                            new BpBarcodeElement
                            {
                                BarcodeType = BarcodeType.UccEan128,
                                Height = 40,
                                Width = 1100,
                                NarrowModuleWidth = 2,
                                WideModuleWidth = 2,
                                HumanReadable = false,
                                Field = new BgField { Value = data }
                            }
                        }
                    }
                }
            }
        }
    };
}
