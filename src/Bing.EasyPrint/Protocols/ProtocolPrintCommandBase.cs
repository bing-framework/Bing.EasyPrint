using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Bing.EasyPrint.Core;
using Bing.EasyPrint.Printing;

namespace Bing.EasyPrint.Protocols;

/// <summary>协议命令的通用动作记录和可重复构建实现。</summary>
public abstract class ProtocolPrintCommandBase<TCommand> : IPrintCommand<TCommand>, ICancelableProtocolPrintCommand
    where TCommand : ProtocolPrintCommandBase<TCommand>
{
    private readonly BudgetedList<Action<IBufferWriter, CancellationToken>> _actions =
        new BudgetedList<Action<IBufferWriter, CancellationToken>>("Command.Actions", itemBytes: 96);
    private int _actionRecordingSuppression;
    protected Encoding Encoding { get; }

    /// <summary>获取命令所属协议。</summary>
    public abstract PrintProtocol Protocol { get; }

    /// <summary>获取协议能力描述。</summary>
    public abstract PrintCapabilities Capabilities { get; }

    protected ProtocolPrintCommandBase(Encoding encoding)
    {
        Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
    }

    protected TCommand Self => (TCommand)this;

    protected virtual TCommand Add(Action<IBufferWriter> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (_actionRecordingSuppression != 0) return Self;
        _actions.AddFactory(() => (writer, _) => action(writer));
        return Self;
    }

    /// <summary>预留动作持有的数据；保留原 Add 虚方法的协议状态校验。</summary>
    protected TCommand AddWithStorage(Action<IBufferWriter> action, long retainedBytes)
    {
        if (_actionRecordingSuppression != 0) return Self;
        using var operation = PrintResourceScope.BeginTemporary();
        PrintStorageBudget.Reserve(retainedBytes, "Command.RecordData");
        var result = Add(action);
        operation?.CommitReservations();
        return result;
    }

    /// <summary>记录构建时可取消的动作；失败或取消不修改已记录动作。</summary>
    protected virtual TCommand AddCancellable(Action<IBufferWriter, CancellationToken> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (_actionRecordingSuppression != 0) return Self;
        _actions.Add(action);
        return Self;
    }

    /// <summary>在临时命令副本中执行结构化校验而不记录输出动作。</summary>
    protected IDisposable SuppressActionRecording()
    {
        _actionRecordingSuppression++;
        return new ActionRecordingScope(this);
    }

    private sealed class ActionRecordingScope : IDisposable
    {
        private ProtocolPrintCommandBase<TCommand> _owner;

        public ActionRecordingScope(ProtocolPrintCommandBase<TCommand> owner) => _owner = owner;

        public void Dispose()
        {
            var owner = _owner;
            if (owner == null) return;
            owner._actionRecordingSuppression--;
            _owner = null;
        }
    }

    protected static void Validate(string value, string name)
    {
        if (value == null) throw new ArgumentNullException(name);
        if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
            throw new ArgumentException("指令参数不能包含换行符。", name);
    }

    /// <summary>校验会被嵌入协议字段的文本，拒绝控制符和指定分隔符。</summary>
    protected static void ValidateField(string value, string name, params char[] forbidden)
    {
        Validate(value, name);
        if (value.IndexOf('\0') >= 0 || value.IndexOf('\u001b') >= 0)
            throw new ArgumentException("协议字段不能包含控制字符。", name);
        if (forbidden == null) return;
        foreach (var character in forbidden)
            if (value.IndexOf(character) >= 0)
                throw new ArgumentException("协议字段包含保留分隔符。", name);
    }

    public TCommand WriteRaw(string raw)
    {
        if (raw == null) throw new ArgumentNullException(nameof(raw));
        ValidateEncodable(raw, nameof(raw));
        return AddWithStorage(w => w.Write(raw), PrintStorageBudget.TextBytes(raw));
    }

    /// <summary>记录文本 Raw，并在动作不可失败地提交前完成关联元数据提交。</summary>
    protected TCommand WriteRawWithCommit(string raw, Action beforeCommit)
    {
        if (raw == null) throw new ArgumentNullException(nameof(raw));
        if (beforeCommit == null) throw new ArgumentNullException(nameof(beforeCommit));
        ValidateEncodable(raw, nameof(raw));
        if (_actionRecordingSuppression != 0) return Self;

        using var operation = PrintResourceScope.BeginTemporary();
        PrintStorageBudget.Reserve(PrintStorageBudget.TextBytes(raw), "Command.RecordData");
        _actions.AddFactory(() => (writer, _) => writer.Write(raw), beforeCommit: beforeCommit);
        operation?.CommitReservations();
        return Self;
    }

    public TCommand WriteRawLine(string raw)
    {
        Validate(raw, nameof(raw));
        ValidateEncodable(raw, nameof(raw));
        return AddWithStorage(w => { w.Write(raw); w.Write("\r\n"); }, PrintStorageBudget.TextBytes(raw));
    }

    /// <summary>写入原始二进制数据。</summary>
    public TCommand WriteRawBytes(byte[] bytes)
        => WriteRawBytes(bytes, CancellationToken.None);

    /// <summary>复制原始字节并支持取消；失败时不追加动作。</summary>
    public TCommand WriteRawBytes(byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        using var operation = PrintResourceScope.BeginTemporary();
        var copy = BudgetedBuffer.Copy(bytes, "Command.RawSnapshot", cancellationToken);
        var result = Add(w => w.Write(copy));
        operation?.CommitReservations();
        return result;
    }

    public virtual IBufferWriter Build()
        => Build(CancellationToken.None);

    /// <summary>可取消构建；取消后仍可使用新令牌重新构建完整命令。</summary>
    public virtual IBufferWriter Build(CancellationToken cancellationToken)
        => BuildRecordedActions(0, _actions.Count, cancellationToken);

    /// <summary>已记录动作数，供有独立文件边界的协议使用。</summary>
    protected int RecordedActionCount => _actions.Count;

    /// <summary>构建指定动作区间，不修改记录；用于保持控制文件边界。</summary>
    protected IBufferWriter BuildRecordedActions(int start, int count)
        => BuildRecordedActions(start, count, CancellationToken.None);

    /// <summary>构建可取消的动作区间，不修改记录。</summary>
    protected IBufferWriter BuildRecordedActions(int start, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = PrintResourceScope.BeginTemporary();
        var writer = new BufferWriter(Encoding, cancellationToken);
        for (var index = start; index < start + count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _actions[index](writer, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        operation?.CommitReservations();
        return writer;
    }

    protected TCommand WriteEncoded(byte[] bytes) => AddWithStorage(w => w.Write(bytes), bytes?.LongLength ?? 0);

    /// <summary>
    /// 校验文本能否使用当前协议编码完整表示。
    /// </summary>
    /// <remarks>
    /// 协议命令必须在记录动作前拒绝替换回退，否则打印机收到的字节可能已经
    /// 把原始字符静默变成问号。该检查只存在于新协议基类，因此不会改变 CPCL
    /// 现有的替换回退行为。原始二进制写入不经过此方法。
    /// </remarks>
    protected void ValidateEncodable(string value, string name)
    {
        if (value == null) throw new ArgumentNullException(name);

        try
        {
            var strictEncoding = (Encoding)Encoding.Clone();
            strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
            strictEncoding.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                $"参数包含无法使用 {Encoding.WebName} 编码的字符；协议命令拒绝替换回退。",
                name,
                exception);
        }
    }

    /// <summary>严格编码文本，用于需要在追加动作前取得字节的协议命令。</summary>
    protected byte[] GetEncodedBytes(string value, string name)
    {
        ValidateEncodable(value, name);
        return BudgetedBuffer.Create(Encoding.GetByteCount(value), "Command.EncodedText",
            bytes => Encoding.GetBytes(value, 0, value.Length, bytes, 0));
    }
}
