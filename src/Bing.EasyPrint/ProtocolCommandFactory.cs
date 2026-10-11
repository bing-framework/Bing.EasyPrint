using System;
using System.Text;
using System.Threading;
using Bing.EasyPrint.CPCL;
using Bing.EasyPrint.Protocols;

namespace Bing.EasyPrint;

/// <summary>按打印协议创建命令实例。</summary>
public static class ProtocolCommandFactory
{
    /// <summary>
    /// 创建指定协议的命令实例。
    /// </summary>
    /// <param name="protocol">目标打印协议。</param>
    /// <param name="encoding">文本编码；为空时使用协议默认编码。</param>
    /// <param name="escPosLayoutMode">ESC/POS 的布局模式。</param>
    /// <param name="escPosOptions">ESC/POS 设备选项；仅适用于 ESC/POS。</param>
    /// <param name="tsplOptions">TSPL 介质和设备选项；仅适用于 TSPL。</param>
    /// <param name="zplOptions">ZPL 介质和设备选项；仅适用于 ZPL。</param>
    /// <returns>协议命令实例。</returns>
    public static IProtocolPrintCommand Create(PrintProtocol protocol, Encoding encoding = null,
        EscPosLayoutMode escPosLayoutMode = EscPosLayoutMode.Native,
        EscPosDeviceOptions escPosOptions = null,
        TsplMediaOptions tsplOptions = null,
        ZplMediaOptions zplOptions = null)
    {
        if (!Enum.IsDefined(typeof(EscPosLayoutMode), escPosLayoutMode))
            throw new ArgumentOutOfRangeException(nameof(escPosLayoutMode));
        if (protocol != PrintProtocol.EscPos && escPosOptions != null)
            throw new ArgumentException("ESC/POS 设备选项只能用于 ESC/POS 协议。", nameof(escPosOptions));
        if (protocol != PrintProtocol.Tspl && tsplOptions != null)
            throw new ArgumentException("TSPL 介质选项只能用于 TSPL 协议。", nameof(tsplOptions));
        if (protocol != PrintProtocol.Zpl && zplOptions != null)
            throw new ArgumentException("ZPL 介质选项只能用于 ZPL 协议。", nameof(zplOptions));

        switch (protocol)
        {
            case PrintProtocol.Cpcl:
            {
                var command = new CPCLPrintCommand(encoding ?? Encoding.GetEncoding("gbk"));
                return new ProtocolCommandAdapter<CPCLPrintCommand>(command, PrintProtocol.Cpcl,
                    cancellationToken => command.Build(cancellationToken));
            }
            case PrintProtocol.EscPos:
                return new EscPosPrintCommand(encoding, escPosLayoutMode, escPosOptions);
            case PrintProtocol.Tspl:
                return new TsplPrintCommand(encoding, tsplOptions);
            case PrintProtocol.Zpl:
                return new ZplPrintCommand(encoding, zplOptions);
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "不支持的打印协议。");
        }
    }

    private sealed class ProtocolCommandAdapter<TCommand> : ICancelableProtocolPrintCommand
        where TCommand : IPrintCommand<TCommand>
    {
        private readonly TCommand _command;
        private readonly Func<CancellationToken, IBufferWriter> _buildWithCancellation;

        public ProtocolCommandAdapter(TCommand command, PrintProtocol protocol,
            Func<CancellationToken, IBufferWriter> buildWithCancellation = null)
        {
            _command = command;
            _buildWithCancellation = buildWithCancellation;
            Protocol = protocol;
            Capabilities = new PrintCapabilities(protocol, 200, true, true, true, true, false,
                supportsReverseText: protocol == PrintProtocol.Cpcl);
        }

        public PrintProtocol Protocol { get; }
        public PrintCapabilities Capabilities { get; }
        public IBufferWriter Build() => _command.Build();

        public IBufferWriter Build(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var writer = _buildWithCancellation != null
                ? _buildWithCancellation(cancellationToken)
                : (_command is ICancelableProtocolPrintCommand cancellable
                    ? cancellable.Build(cancellationToken)
                    : _command.Build());
            cancellationToken.ThrowIfCancellationRequested();
            return writer;
        }
    }
}
