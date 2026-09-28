using System;
using System.Text;
using Bing.EasyPrint.CPCL;
using Xunit;

namespace Bing.EasyPrint.Tests;

public class ComponentValidationTests
{
    private static CPCLPrintCommand Command() => new CPCLPrintCommand(Encoding.UTF8).SetPage(200, 100);

    [Fact]
    public void Component_constructors_reject_invalid_content_before_it_can_be_added()
    {
        Assert.Equal("text", Assert.Throws<ArgumentException>(() => new TextComponent("A\nPRINT")).ParamName);
        Assert.Equal("type", Assert.Throws<ArgumentException>(() => new Barcode1DComponent("128 2", "123")).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentException>(() => new Barcode1DComponent("128", "123\rCUT")).ParamName);
        Assert.Equal("errorLevel", Assert.Throws<ArgumentException>(() => new QRCodeComponent("data", "M\nPRINT")).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentException>(() => new QRCodeComponent("data\0", "M")).ParamName);
    }

    [Fact]
    public void Draw_components_reject_invalid_content_without_changing_command()
    {
        var command = Command().DrawText(0, 0, "valid", 24, 0, false, false, false);
        var before = command.Build().GetBytes();

        Assert.Equal("text", Assert.Throws<ArgumentException>(() => command.DrawText(0, 0, "bad\nPRINT", 24, 0, false, false, false)).ParamName);
        Assert.Equal("type", Assert.Throws<ArgumentException>(() => command.DrawBarcode1D("128 2", 0, 0, "123", 1, 20, 2, 0)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentException>(() => command.DrawBarcode1D("128", 0, 0, "123\nCUT", 1, 20, 2, 0)).ParamName);
        Assert.Equal("errorLevel", Assert.Throws<ArgumentException>(() => command.DrawQrCode(0, 0, "data", 2, "M\nPRINT", 0)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentException>(() => command.DrawQrCode(0, 0, "data\nPRINT", 2, "M", 0)).ParamName);

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Null_and_blank_inputs_keep_their_existing_behavior()
    {
        var command = Command();
        Assert.Equal("type", Assert.Throws<ArgumentException>(() => command.DrawBarcode1D(null, 0, 0, "123", 1, 20, 2, 0)).ParamName);
        Assert.Equal("errorLevel", Assert.Throws<ArgumentException>(() => command.DrawQrCode(0, 0, "data", 2, (string)null, 0)).ParamName);
        Assert.Same(command, command.DrawText(0, 0, null, 24, 0, false, false, false));
        Assert.Same(command, command.DrawBarcode1D(BarcodeType.Code128, 0, 0, " ", 1, 20, 2, RotationAngle.None));
        Assert.Same(command, command.DrawQrCode(0, 0, " ", QrCodeUnitSize.Size2, QrCodeCorrectionLevel.M, RotationAngle.None));
    }

    [Theory]
    [InlineData("\u2003", "协议参数不能为空。")]
    [InlineData("128\u2003A", "协议参数必须是单个标记。")]
    [InlineData("128 \nPRINT", "结构化打印内容不能包含协议控制字符。")]
    [InlineData("\t", "结构化打印内容不能包含协议控制字符。")]
    public void Token_validation_preserves_error_priority(string type, string expectedMessage)
    {
        var error = Assert.Throws<ArgumentException>(() => Command().DrawBarcode1D(type, 0, 0, "123", 1, 20, 2, 0));
        Assert.Equal("type", error.ParamName);
        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Mixed_components_produce_identical_bytes_on_repeated_build()
    {
        var command = Command()
            .DrawText(0, 0, "text", 24, 0, false, false, false)
            .DrawBarcode1D("128", 0, 20, "123456", 1, 20, 2, 0)
            .DrawQrCode(60, 0, "QR-123", 2, "M", 0);

        var first = command.Build().GetBytes();
        Assert.Equal(first, command.Build().GetBytes());
        Assert.Equal(first, command.Build().GetBytes());
    }

    [Fact]
    public void Split_line_checks_character_and_current_page_width_at_build()
    {
        var command = Command().DrawSplitLine(40, 0, '\n');
        var characterError = Assert.Throws<ArgumentException>(() => command.Build());
        Assert.Equal("Symbol", characterError.ParamName);

        var split = Assert.IsType<SplitLineComponent>(command.Content.Content[0]);
        split.Symbol = '-';
        command.CommandInfo.Width = 30;
        var widthError = Assert.Throws<ArgumentOutOfRangeException>(() => command.Build());
        Assert.Equal("X", widthError.ParamName);

        command.CommandInfo.Width = 200;
        Assert.Contains("T 24 0 40 0", command.Build().ToString());
    }

    [Fact]
    public void Text_area_rejects_late_control_character_without_partial_content()
    {
        var command = Command();
        var before = command.Build().GetBytes();

        var error = Assert.Throws<ArgumentException>(() => command.DrawTextArea(0, 0, 24, 100, "many valid letters\nPRINT", 24, 0, 0));

        Assert.Equal("text", error.ParamName);
        Assert.Equal(before, command.Build().GetBytes());
    }
}
