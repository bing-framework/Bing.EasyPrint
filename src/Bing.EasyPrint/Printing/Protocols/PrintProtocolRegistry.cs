using System;
using System.Collections.Generic;
using System.Threading;

namespace Bing.EasyPrint.Printing;

/// <summary>
/// 保存启动阶段显式注册的新协议；注册表冻结后不可修改，描述和能力以只读快照返回。
/// </summary>
public sealed class PrintProtocolRegistry
{
    private readonly object _gate = new object();
    private readonly Dictionary<string, PrintProtocolRegistration> _registrations =
        new Dictionary<string, PrintProtocolRegistration>(StringComparer.Ordinal);
    private PrintProtocolDescriptor[] _descriptors = Array.Empty<PrintProtocolDescriptor>();
    private bool _frozen;

    /// <summary>获取注册表是否已冻结。</summary>
    public bool IsFrozen
    {
        get { lock (_gate) return _frozen; }
    }

    /// <summary>
    /// 注册一个协议；重复标识、空注册或冻结后注册明确失败。
    /// </summary>
    /// <param name="registration">协议注册项；不能为 null。</param>
    /// <exception cref="ArgumentNullException"><paramref name="registration"/> 为 null 时抛出。</exception>
    /// <exception cref="InvalidOperationException">注册表已冻结或标识重复时抛出。</exception>
    public void Register(PrintProtocolRegistration registration)
    {
        if (registration == null) throw new ArgumentNullException(nameof(registration));
        lock (_gate)
        {
            if (_frozen) throw new InvalidOperationException("打印协议注册表已冻结，不能继续注册。");
            if (_registrations.ContainsKey(registration.Id))
                throw new InvalidOperationException($"打印协议标识重复：{registration.Id}。");
            _registrations.Add(registration.Id, registration);
            RebuildDescriptorsLocked();
        }
    }

    /// <summary>冻结注册表；冻结后注册、替换或删除均被拒绝。</summary>
    public void Freeze()
    {
        lock (_gate) _frozen = true;
    }

    /// <summary>获取当前协议的只读描述快照。</summary>
    public IReadOnlyList<PrintProtocolDescriptor> Descriptors
    {
        get { lock (_gate) return Array.AsReadOnly((PrintProtocolDescriptor[])_descriptors.Clone()); }
    }

    /// <summary>
    /// 创建指定协议的后端会话；未知标识、空配置工厂结果或无效 JSON 配置明确失败。
    /// </summary>
    /// <param name="protocolId">已注册的协议标识。</param>
    /// <param name="configurationJson">可选的 JSON 配置快照；调用方修改原对象不会影响已传入字符串。</param>
    /// <returns>由调用方负责释放的后端会话。</returns>
    /// <exception cref="ArgumentException"><paramref name="protocolId"/> 为空时抛出。</exception>
    /// <exception cref="KeyNotFoundException">协议标识未注册时抛出。</exception>
    public IPrintProtocolBackend CreateBackend(string protocolId, string configurationJson = null)
    {
        if (string.IsNullOrWhiteSpace(protocolId))
            throw new ArgumentException("协议标识不能为空。", nameof(protocolId));
        PrintProtocolRegistration registration;
        lock (_gate)
        {
            if (!_registrations.TryGetValue(protocolId, out registration))
                throw new KeyNotFoundException($"未注册的打印协议：{protocolId}。");
        }
        var configurationSnapshot = configurationJson ?? string.Empty;
        registration.ConfigurationValidator?.Invoke(configurationSnapshot);
        var backend = registration.BackendFactory(configurationSnapshot) ??
            throw new InvalidOperationException($"协议 {protocolId} 的后端工厂未返回会话。");
        if (!string.Equals(backend.ProtocolId, protocolId, StringComparison.Ordinal))
        {
            backend.Dispose();
            throw new InvalidOperationException($"协议 {protocolId} 的后端返回了不匹配的标识 {backend.ProtocolId}。");
        }
        return backend;
    }

    /// <summary>使用注册时的配置校验器验证 JSON 快照；未知 ID 明确失败。</summary>
    /// <param name="protocolId">已注册的协议标识。</param>
    /// <param name="configurationJson">配置 JSON 快照。</param>
    public void ValidateConfiguration(string protocolId, string configurationJson = null)
    {
        if (string.IsNullOrWhiteSpace(protocolId))
            throw new ArgumentException("协议标识不能为空。", nameof(protocolId));
        PrintProtocolRegistration registration;
        lock (_gate)
        {
            if (!_registrations.TryGetValue(protocolId, out registration))
                throw new KeyNotFoundException($"未注册的打印协议：{protocolId}。");
        }
        registration.ConfigurationValidator?.Invoke(configurationJson ?? string.Empty);
    }

    /// <summary>判断指定协议标识是否已注册。</summary>
    /// <param name="protocolId">协议标识。</param>
    public bool Contains(string protocolId)
    {
        if (string.IsNullOrWhiteSpace(protocolId)) return false;
        lock (_gate) return _registrations.ContainsKey(protocolId);
    }

    private void RebuildDescriptorsLocked()
    {
        var descriptors = new List<PrintProtocolDescriptor>(_registrations.Count);
        foreach (var registration in _registrations.Values)
            descriptors.Add(new PrintProtocolDescriptor(registration));
        descriptors.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        _descriptors = descriptors.ToArray();
    }
}

/// <summary>
/// 将既有 <see cref="IPrintProtocolProvider"/> 适配为统一后端契约；用于内置协议适配与兼容回归。
/// </summary>
public sealed class PrintProtocolProviderBackend : IPrintProtocolBackend
{
    private readonly IPrintProtocolProvider _provider;
    private readonly Action _dispose;

    /// <summary>
    /// 初始化一个既有 Provider 的后端适配器。
    /// </summary>
    /// <param name="protocolId">注册表使用的稳定协议标识。</param>
    /// <param name="provider">既有 Provider；不能为 null。</param>
    /// <param name="dispose">可选的会话释放回调。</param>
    public PrintProtocolProviderBackend(string protocolId, IPrintProtocolProvider provider, Action dispose = null)
    {
        if (string.IsNullOrWhiteSpace(protocolId))
            throw new ArgumentException("协议标识不能为空。", nameof(protocolId));
        ProtocolId = protocolId;
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _dispose = dispose;
    }

    /// <inheritdoc />
    public string ProtocolId { get; }

    /// <inheritdoc />
    public void Validate(PrintDocument document, CancellationToken cancellationToken)
    {
        if (_provider is IPrintCancelableProtocolProvider cancelable)
            cancelable.Validate(document, cancellationToken);
        else
        {
            cancellationToken.ThrowIfCancellationRequested();
            _provider.Validate(document);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PrintPageOutput> Render(PrintDocument document, CancellationToken cancellationToken)
        => _provider.RenderOutput(document, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _dispose?.Invoke();
}
