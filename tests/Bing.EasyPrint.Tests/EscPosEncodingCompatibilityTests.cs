using System;
using System.Text;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证 ESC/POS 的文本编码满足命令帧所需的 ASCII 控制字节布局。
/// </summary>
public sealed class EscPosEncodingCompatibilityTests
{
    [Theory]
    [InlineData("ascii")]
    [InlineData("utf-8")]
    public void Command_accepts_ascii_compatible_encodings(string webName)
    {
        var encoding = Encoding.GetEncoding(webName);

        Assert.Equal(new byte[] { 0x41, 0x0D, 0x0A }, encoding.GetBytes("A\r\n"));

        var bytes = new EscPosPrintCommand(encoding)
            .Text("A")
            .Build()
            .GetBytes();

        Assert.Equal(new byte[] { 0x41, 0x0A }, bytes);
    }

    [Theory]
    [InlineData("utf-16")]
    [InlineData("utf-32")]
    public void Command_rejects_non_ascii_compatible_encodings_before_output(string webName)
    {
        var encoding = Encoding.GetEncoding(webName);

        var error = Assert.Throws<NotSupportedException>(() => new EscPosPrintCommand(encoding));

        Assert.Contains("ASCII", error.Message);
        Assert.Contains("CR/LF", error.Message);
    }

    [Theory]
    [InlineData("utf-16")]
    [InlineData("utf-32")]
    public void Provider_factory_rejects_non_ascii_compatible_encodings_before_rendering(string webName)
    {
        var encoding = Encoding.GetEncoding(webName);

        var error = Assert.Throws<NotSupportedException>(() =>
            PrintProtocolProviderFactory.Create(PrintProtocol.EscPos, encoding));

        Assert.Contains("ASCII", error.Message);
    }

    [Theory]
    [InlineData("utf-16")]
    [InlineData("utf-32")]
    public void Command_factory_rejects_non_ascii_compatible_encodings_before_output(string webName)
    {
        var encoding = Encoding.GetEncoding(webName);

        var error = Assert.Throws<NotSupportedException>(() =>
            ProtocolCommandFactory.Create(PrintProtocol.EscPos, encoding));

        Assert.Contains("ASCII", error.Message);
    }

    [Fact]
    public void SupportsEncoding_matches_command_constructor_wire_safety()
    {
        var commandCapabilities = new EscPosPrintCommand(Encoding.ASCII).Capabilities;
        var providerCapabilities = PrintProtocolProviderFactory
            .Create(PrintProtocol.EscPos, Encoding.ASCII)
            .Capabilities;

        AssertEncodingSupport(commandCapabilities);
        AssertEncodingSupport(providerCapabilities);
    }

    private static void AssertEncodingSupport(PrintCapabilities capabilities)
    {
        Assert.True(capabilities.SupportsEncoding(Encoding.ASCII));
        Assert.True(capabilities.SupportsEncoding(Encoding.UTF8));
        Assert.False(capabilities.SupportsEncoding(Encoding.Unicode));
        Assert.False(capabilities.SupportsEncoding(Encoding.UTF32));
        Assert.False(capabilities.SupportsEncoding(null));
    }
}
