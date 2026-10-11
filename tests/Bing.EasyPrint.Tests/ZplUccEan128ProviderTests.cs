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

/// <summary>验证 ZPL GS1-128（UCC/EAN-128）的 FNC1 编码、统一模板映射和严格校验。</summary>
public sealed class ZplUccEan128ProviderTests
{
    [Fact]
    public void Direct_command_emits_initial_fnc1_and_code128_subset_c_invocation()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeUccEan128(10, 20, "0101234567890128", 40, false, 2)
            .End()
            .Build()
            .GetBytes());

        Assert.Contains("^FO10,20^BY2^BCN,40,N,N,N^FD>;>80101234567890128^FS\r\n", output);
    }

    [Fact]
    public void Direct_command_converts_group_separator_to_following_fnc1()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeUccEan128(0, 0, "0101234567890128\u001d10ABC", 40, false, 2)
            .End()
            .Build()
            .GetBytes());

        Assert.Contains("^FD>;>80101234567890128>810ABC^FS", output);
    }

    [Fact]
    public void Direct_command_normalizes_bracketed_ai_sequence_before_zpl_fnc1_escape()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeUccEan128(10, 20, "(01)01234567890128(10)ABC(17)250101",
                40, false, 2)
            .End()
            .Build().GetBytes());

        Assert.Contains("^FD>;>8010123456789012810ABC>817250101^FS", output);
    }

    [Fact]
    public void Direct_command_decodes_literal_parenthesis_in_bracketed_ai_value()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeUccEan128(10, 20, "(91)AB\\(C)D(17)250101", 40, false, 2)
            .End()
            .Build().GetBytes());

        Assert.Contains("^FD>;>891AB(C)D>817250101^FS", output);
    }

    [Fact]
    public void Direct_command_normalizes_supported_compressed_ai_before_zpl_fnc1_escape()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeUccEan128(10, 20, "(01)01234567890128(10)ABC(3103)000123",
                40, false, 2)
            .End()
            .Build().GetBytes());

        Assert.Contains("^FD>;>8010123456789012810ABC>83103000123^FS", output);
    }

    [Fact]
    public void Direct_command_normalizes_price_ai_before_zpl_fnc1_escape()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeUccEan128(10, 20, "(01)01234567890128(3920)12345(10)ABC",
                40, false, 2)
            .End()
            .Build().GetBytes());

        Assert.Contains("^FD>;>80101234567890128392012345>810ABC^FS", output);
    }

    [Fact]
    public void Direct_command_accepts_ai_91_company_internal_data_up_to_90_characters()
    {
        var value = new string('A', 90);
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeUccEan128(10, 20, $"(91){value}(17)250101", 40, false, 2)
            .End()
            .Build().GetBytes());

        Assert.Contains($"^FD>;>891{value}>817250101^FS", output);
    }

    [Fact]
    public void Direct_command_normalizes_amount_and_discount_ai_before_zpl_fnc1_escape()
    {
        var output = Encoding.ASCII.GetString(new ZplPrintCommand(Encoding.ASCII)
            .Start()
            .BarcodeUccEan128(10, 20,
                "(3909)1(3910)8401(3947)1234(3959)123456(17)250101", 40, false, 2)
            .End()
            .Build().GetBytes());

        Assert.Contains(
            "^FD>;>839091>839108401>839471234395912345617250101^FS",
            output);
    }

    [Fact]
    public void Direct_command_escapes_literal_invocation_prefix_without_polluting_output()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        command.BarcodeUccEan128(10, 20, "01>234", 40, false, 2);
        var output = Encoding.ASCII.GetString(command.Build().GetBytes());

        Assert.Equal("before", Encoding.ASCII.GetString(before));
        Assert.Contains("^FD>;>801>0234^FS", output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("A10123")]
    [InlineData("010^123")]
    [InlineData("010\u0001ABC")]
    [InlineData("(01)01234567890128(1234)ABC")]
    [InlineData("(01)01234567890128(3103)12345")]
    [InlineData("(01)01234567890128(3930)840")]
    public void Direct_command_rejects_invalid_data_without_appending_output(string data)
    {
        var command = new ZplPrintCommand(Encoding.ASCII).WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.ThrowsAny<ArgumentException>(() => command.BarcodeUccEan128(10, 20, data, 40, false, 2));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Capabilities_expose_ucc_ean128_for_zpl_and_tspl()
    {
        var zpl = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.ASCII).Capabilities;
        var tspl = PrintProtocolProviderFactory.Create(PrintProtocol.Tspl, Encoding.ASCII).Capabilities;

        Assert.True(zpl.SupportsBarcodeType(BarcodeType.UccEan128));
        Assert.True(zpl.SupportsCommandBarcodeType(BarcodeType.UccEan128));
        Assert.True(tspl.SupportsBarcodeType(BarcodeType.UccEan128));
    }

    [Fact]
    public void Provider_maps_ucc_ean128_to_zpl_code128_with_fnc1()
    {
        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Zpl, Encoding.ASCII)
            .Render(BarcodeDocument("0101234567890128"))[0]);

        Assert.Contains("^FO0,0^BY2^BCN,40,Y,N,N^FD>;>80101234567890128^FS\r\n", output);
    }

    [Fact]
    public void Provider_renders_bracketed_ai_sequence_after_width_preflight()
    {
        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Zpl, Encoding.ASCII)
            .Render(BarcodeDocument("(01)01234567890128(10)ABC(17)250101"))[0]);

        Assert.Contains("^FO0,0^BY2^BCN,40,Y,N,N^FD>;>8010123456789012810ABC>817250101^FS\r\n",
            output);
    }

    [Fact]
    public void Provider_renders_compressed_ai_sequence_after_width_preflight()
    {
        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Zpl, Encoding.ASCII)
            .Render(BarcodeDocument("(01)01234567890128(10)ABC(3103)000123", 0, false))[0]);

        Assert.Contains("^FO0,0^BY2^BCN,40,N,N,N^FD>;>8010123456789012810ABC>83103000123^FS\r\n",
            output);
    }

    [Fact]
    public void Template_renders_variable_amount_ai_with_fnc1_after_width_preflight()
    {
        const string data = "39091\u001D39108401";
        var template = Template(data);
        template.Width = 1200;
        template.Rows[0].Cells[0].Width = 1200;
        ((BpBarcodeElement)template.Rows[0].Cells[0].Elements[0]).Width = 1100;
        var result = new TemplateRenderer().RenderWithLayout(template, null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        var output = Encoding.ASCII.GetString(Assert.Single(result.Pages).Data);
        Assert.Contains("^BCN,40,Y,N,N^FD>;>839091>839108401^FS", output);
    }

    [Fact]
    public void Template_maps_ucc_ean128_and_preserves_element_path_on_invalid_data()
    {
        var result = new TemplateRenderer().RenderWithLayout(Template("0101234567890128"), null,
            options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            });

        Assert.Contains("^BCN,40,Y,N,N^FD>;>80101234567890128^FS\r\n",
            Encoding.ASCII.GetString(Assert.Single(result.Pages).Data));

        var error = Assert.Throws<TemplateRenderException>(() => new TemplateRenderer().Render(
            Template("010^123"), null, options: new TemplateRenderOptions
            {
                Protocol = PrintProtocol.Zpl,
                Encoding = Encoding.ASCII,
                FeedLines = 0,
                CutAfterPrint = false
            }));
        Assert.Equal("Rows[0].Cells[0].Elements[0]", error.SourcePath);
    }

    [Theory]
    [InlineData(0, "N")]
    [InlineData(90, "R")]
    [InlineData(180, "I")]
    [InlineData(270, "B")]
    public void Provider_keeps_native_orientation_for_ucc_ean128(int rotation, string orientation)
    {
        var output = Encoding.ASCII.GetString(PrintProtocolProviderFactory
            .Create(PrintProtocol.Zpl, Encoding.ASCII)
            .Render(BarcodeDocument("0101234567890128", rotation))[0]);

        Assert.Contains($"^BC{orientation},40,Y,N,N^FD>;>80101234567890128^FS", output);
    }

    private static PrintDocument BarcodeDocument(string data, int rotation = 0,
        bool? humanReadable = null)
    {
        var document = new PrintDocument();
        document.AddPage(1000, 1000).Add(new PrintBarcodeElement(0, 0,
            BarcodeType.UccEan128, data, 40, 2, 2, (RotationAngle)rotation,
            humanReadable: humanReadable), "Barcode");
        return document;
    }

    private static BpTemplate Template(string data) => new BpTemplate
    {
        Version = 1,
        Width = 600,
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
                        Width = 600,
                        Elements = new List<BpElement>
                        {
                            new BpBarcodeElement
                            {
                                BarcodeType = BarcodeType.UccEan128,
                                Height = 40,
                                Width = 500,
                                HumanReadable = true,
                                Field = new BgField { Value = data }
                            }
                        }
                    }
                }
            }
        }
    };
}
