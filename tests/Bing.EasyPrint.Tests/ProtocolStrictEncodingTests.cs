using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证新协议拒绝编码替换回退，同时保留 CPCL 的历史编码行为。
/// </summary>
public sealed class ProtocolStrictEncodingTests
{
    [Fact]
    public void EscPos_native_text_rejects_replacement_fallback_before_append()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        var error = Assert.Throws<ArgumentException>(() => command.Text("中文"));

        Assert.Contains("无法使用", error.Message);
        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void EscPos_qr_rejects_replacement_fallback_before_append()
    {
        var command = new EscPosPrintCommand(Encoding.ASCII).Text("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.QrCode("中文"));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Tspl_text_rejects_replacement_fallback_without_consuming_pending_media_options()
    {
        var command = new TsplPrintCommand(Encoding.ASCII,
            new TsplMediaOptions { GapDistance = 2, GapOffset = 1 });

        Assert.Throws<ArgumentException>(() => command.Text(1, 2, "中文"));

        var output = Encoding.ASCII.GetString(command.Text(1, 2, "OK").Build().GetBytes());
        Assert.Contains("GAP 2,1\r\n", output);
        Assert.Contains("TEXT 1,2,\"0\",0,1,1,\"OK\"\r\n", output);
    }

    [Fact]
    public void Zpl_text_rejects_replacement_fallback_without_mutating_start()
    {
        var command = new ZplPrintCommand(Encoding.ASCII).Start();

        Assert.Throws<ArgumentException>(() => command.Text(1, 2, "中文"));

        Assert.Equal(Encoding.ASCII.GetBytes("^XA\r\n^XZ\r\n"), command.End().Build().GetBytes());
    }

    [Theory]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void WriteRaw_text_rejects_unencodable_characters_without_mutating_output(PrintProtocol protocol)
    {
        var command = Create(protocol);
        command.WriteRaw("before");
        var before = command.Build().GetBytes();

        Assert.Throws<ArgumentException>(() => command.WriteRaw("中文"));

        Assert.Equal(before, command.Build().GetBytes());
    }

    [Fact]
    public void Utf8_text_is_written_without_replacement()
    {
        var value = "中文";
        var expected = Encoding.UTF8.GetBytes(value + "\n");

        var bytes = new EscPosPrintCommand(Encoding.UTF8)
            .Text(value)
            .Build()
            .GetBytes();

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Gbk_text_is_written_without_replacement()
    {
        var encoding = GetGbkEncoding();
        var value = "中文";

        var bytes = new TsplPrintCommand(encoding)
            .Text(1, 2, value)
            .Build()
            .GetBytes();

        var expected = encoding.GetBytes($"TEXT 1,2,\"0\",0,1,1,\"{value}\"\r\n");
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Raw_bytes_bypass_text_encoding_validation()
    {
        var raw = new byte[] { 0x00, 0xFF, 0x1B };

        var bytes = new ZplPrintCommand(Encoding.ASCII)
            .WriteRawBytes(raw)
            .Build()
            .GetBytes();

        Assert.Equal(raw, bytes);
    }

    [Fact]
    public void Cpcl_keeps_replacement_fallback_behavior()
    {
        var bytes = new CPCLPrintCommand(Encoding.ASCII)
            .Text(0, 0, 0, 0, "中文")
            .Build()
            .GetBytes();

        Assert.Contains((byte)'?', bytes);
    }

    private static CommandHarness Create(PrintProtocol protocol)
    {
        switch (protocol)
        {
            case PrintProtocol.EscPos:
            {
                var command = new EscPosPrintCommand(Encoding.ASCII);
                return new CommandHarness(command, value => command.WriteRaw(value));
            }
            case PrintProtocol.Tspl:
            {
                var command = new TsplPrintCommand(Encoding.ASCII);
                return new CommandHarness(command, value => command.WriteRaw(value));
            }
            case PrintProtocol.Zpl:
            {
                var command = new ZplPrintCommand(Encoding.ASCII);
                return new CommandHarness(command, value => command.WriteRaw(value));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null);
        }
    }

    private static Encoding GetGbkEncoding()
    {
        Type providerType = null;
        try
        {
            providerType = Assembly.Load(new AssemblyName("System.Text.Encoding.CodePages"))
                .GetType("System.Text.CodePagesEncodingProvider");
        }
        catch (FileNotFoundException)
        {
            // The legacy target may not carry the optional code-page assembly.
        }
        var provider = providerType?.GetProperty("Instance")?.GetValue(null) as EncodingProvider;
        if (provider != null)
            Encoding.RegisterProvider(provider);

        return Encoding.GetEncoding(936);
    }

    private sealed class CommandHarness
    {
        private readonly IProtocolPrintCommand _command;
        private readonly Action<string> _writeRaw;

        public CommandHarness(IProtocolPrintCommand command, Action<string> writeRaw)
        {
            _command = command;
            _writeRaw = writeRaw;
        }

        public void WriteRaw(string value) => _writeRaw(value);

        public IBufferWriter Build() => _command.Build();
    }
}
