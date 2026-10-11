using System;
using System.Linq;
using System.Text;
using Bing.EasyPrint.Protocols;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>
/// 验证新协议命令的原始注入边界和结构化字段校验。
/// </summary>
public sealed class ProtocolRawInjectionTests
{
    [Theory]
    [InlineData(PrintProtocol.EscPos, "\r")]
    [InlineData(PrintProtocol.EscPos, "\n")]
    [InlineData(PrintProtocol.Tspl, "\r")]
    [InlineData(PrintProtocol.Tspl, "\n")]
    [InlineData(PrintProtocol.Zpl, "\r")]
    [InlineData(PrintProtocol.Zpl, "\n")]
    public void WriteRawLine_rejects_line_break_without_mutating_recorded_output(
        PrintProtocol protocol, string lineBreak)
    {
        var command = Create(protocol);
        command.WriteRaw("before");
        var before = command.Bytes();

        Assert.Throws<ArgumentException>(() => command.WriteRawLine("raw" + lineBreak + "injected"));

        Assert.Equal(before, command.Bytes());
    }

    [Theory]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void WriteRaw_and_WriteRawBytes_preserve_explicit_input(PrintProtocol protocol)
    {
        var command = Create(protocol);
        var rawText = "RAW\r\n^FS\0";
        var rawBytes = new byte[] { 0x00, 0x1B, 0x0A, 0xFF };
        var expected = Encoding.ASCII.GetBytes(rawText).Concat(rawBytes).ToArray();

        command.WriteRaw(rawText);
        command.WriteRawBytes(rawBytes);
        rawBytes[0] = 0x7F;

        Assert.Equal(expected, command.Bytes());
    }

    [Theory]
    [InlineData(PrintProtocol.EscPos, "\u001B@")]
    [InlineData(PrintProtocol.EscPos, "bad\nPRINT")]
    [InlineData(PrintProtocol.Tspl, "bad\"CUT")]
    [InlineData(PrintProtocol.Tspl, "bad\nPRINT")]
    [InlineData(PrintProtocol.Zpl, "bad^FS")]
    [InlineData(PrintProtocol.Zpl, "bad~DG")]
    public void Structured_text_rejects_protocol_controls_or_delimiters_without_mutating_output(
        PrintProtocol protocol, string value)
    {
        var command = Create(protocol);
        command.WriteRaw("before");
        var before = command.Bytes();

        Assert.Throws<ArgumentException>(() => command.WriteStructuredText(value));

        Assert.Equal(before, command.Bytes());
    }

    private static CommandHarness Create(PrintProtocol protocol)
    {
        switch (protocol)
        {
            case PrintProtocol.EscPos:
            {
                var command = new EscPosPrintCommand(Encoding.ASCII);
                return new CommandHarness(command,
                    raw => command.WriteRaw(raw),
                    raw => command.WriteRawLine(raw),
                    bytes => command.WriteRawBytes(bytes),
                    text => command.Text(text));
            }
            case PrintProtocol.Tspl:
            {
                var command = new TsplPrintCommand(Encoding.ASCII);
                return new CommandHarness(command,
                    raw => command.WriteRaw(raw),
                    raw => command.WriteRawLine(raw),
                    bytes => command.WriteRawBytes(bytes),
                    text => command.Text(0, 0, text));
            }
            case PrintProtocol.Zpl:
            {
                var command = new ZplPrintCommand(Encoding.ASCII);
                return new CommandHarness(command,
                    raw => command.WriteRaw(raw),
                    raw => command.WriteRawLine(raw),
                    bytes => command.WriteRawBytes(bytes),
                    text => command.Text(0, 0, text));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null);
        }
    }

    private sealed class CommandHarness
    {
        private readonly IProtocolPrintCommand command;
        private readonly Action<string> writeRaw;
        private readonly Action<string> writeRawLine;
        private readonly Action<byte[]> writeRawBytes;
        private readonly Action<string> writeStructuredText;

        internal CommandHarness(IProtocolPrintCommand command, Action<string> writeRaw,
            Action<string> writeRawLine, Action<byte[]> writeRawBytes, Action<string> writeStructuredText)
        {
            this.command = command;
            this.writeRaw = writeRaw;
            this.writeRawLine = writeRawLine;
            this.writeRawBytes = writeRawBytes;
            this.writeStructuredText = writeStructuredText;
        }

        internal void WriteRaw(string value) => writeRaw(value);
        internal void WriteRawLine(string value) => writeRawLine(value);
        internal void WriteRawBytes(byte[] value) => writeRawBytes(value);
        internal void WriteStructuredText(string value) => writeStructuredText(value);
        internal byte[] Bytes() => command.Build().GetBytes();
    }
}
