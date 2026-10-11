using System;
using System.Threading;

namespace Bing.EasyPrint;

/// <summary>
/// 可按协议识别的打印命令。
/// </summary>
/// <remarks>
/// 该接口用于工厂和模板适配层识别协议能力；具体绘图 API 仍由各协议命令类型提供。
/// </remarks>
public interface IProtocolPrintCommand
{
    /// <summary>获取命令所属协议。</summary>
    PrintProtocol Protocol { get; }

    /// <summary>获取协议能力描述。</summary>
    PrintCapabilities Capabilities { get; }

    /// <summary>构建完整的打印指令。</summary>
    IBufferWriter Build();
}

/// <summary>
/// 可选的可取消协议命令构建契约。
/// </summary>
/// <remarks>
/// 该接口保持可选，既有实现只需继续实现 <see cref="IProtocolPrintCommand"/>；
/// 调用方可通过同文件中的扩展方法统一调用新旧命令。
/// </remarks>
public interface ICancelableProtocolPrintCommand : IProtocolPrintCommand
{
    /// <summary>使用指定取消令牌构建完整打印指令。</summary>
    /// <param name="cancellationToken">构建期间使用的取消令牌。</param>
    /// <returns>包含完整指令的缓冲区写入器。</returns>
    IBufferWriter Build(CancellationToken cancellationToken);
}

/// <summary>提供协议命令的可取消构建兼容入口。</summary>
public static class ProtocolPrintCommandExtensions
{
    /// <summary>
    /// 使用指定取消令牌构建协议命令；旧实现通过调用前后检查保持兼容。
    /// </summary>
    /// <param name="command">待构建的协议命令。</param>
    /// <param name="cancellationToken">构建期间使用的取消令牌。</param>
    /// <returns>包含完整指令的缓冲区写入器。</returns>
    public static IBufferWriter Build(this IProtocolPrintCommand command,
        CancellationToken cancellationToken)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        cancellationToken.ThrowIfCancellationRequested();

        var cancellable = command as ICancelableProtocolPrintCommand;
        var writer = cancellable == null
            ? command.Build()
            : cancellable.Build(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return writer;
    }
}
