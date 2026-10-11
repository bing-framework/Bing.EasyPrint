using System;
using System.Text;
using System.Threading;
using Bing.EasyPrint;
using Xunit;

namespace Bing.EasyPrint.Tests;

/// <summary>验证协议命令可取消构建契约对新旧实现的兼容行为。</summary>
public sealed class ProtocolCommandCancellationContractTests
{
    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Factory_commands_support_pre_cancel_none_parity_and_recovery(PrintProtocol protocol)
    {
        var command = ProtocolCommandFactory.Create(protocol, Encoding.ASCII);
        Assert.IsAssignableFrom<ICancelableProtocolPrintCommand>(command);

        var legacy = command.Build().GetBytes();
        var explicitNone = command.Build(CancellationToken.None).GetBytes();
        Assert.Equal(legacy, explicitNone);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => command.Build(cancellation.Token));
        Assert.Equal(legacy, command.Build().GetBytes());
    }

    [Fact]
    public void Legacy_command_is_not_called_when_extension_token_is_pre_cancelled()
    {
        var command = new LegacyProtocolCommand();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => command.Build(cancellation.Token));
        Assert.Equal(0, command.BuildCalls);
    }

    [Fact]
    public void Legacy_command_is_checked_after_build_when_it_cancels_during_build()
    {
        using var cancellation = new CancellationTokenSource();
        var command = new LegacyProtocolCommand(cancellation.Cancel);

        Assert.Throws<OperationCanceledException>(() => command.Build(cancellation.Token));
        Assert.Equal(1, command.BuildCalls);
    }

    [Theory]
    [InlineData(PrintProtocol.Cpcl)]
    [InlineData(PrintProtocol.EscPos)]
    [InlineData(PrintProtocol.Tspl)]
    [InlineData(PrintProtocol.Zpl)]
    public void Built_writer_uses_its_token_but_already_copied_output_remains_valid(PrintProtocol protocol)
    {
        var command = ProtocolCommandFactory.Create(protocol, Encoding.ASCII);
        using var cancellation = new CancellationTokenSource();
        var writer = command.Build(cancellation.Token);
        var snapshot = writer.GetBytes();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => writer.GetBytes());
        Assert.Equal(command.Build().GetBytes(), snapshot);
    }

    private sealed class LegacyProtocolCommand : IProtocolPrintCommand
    {
        private readonly Action _onBuild;

        public LegacyProtocolCommand(Action onBuild = null)
        {
            _onBuild = onBuild;
            Capabilities = new PrintCapabilities(PrintProtocol.Cpcl, 200,
                true, true, true, true, false);
        }

        public int BuildCalls { get; private set; }
        public PrintProtocol Protocol => PrintProtocol.Cpcl;
        public PrintCapabilities Capabilities { get; }

        public IBufferWriter Build()
        {
            BuildCalls++;
            _onBuild?.Invoke();
            return null;
        }
    }
}
