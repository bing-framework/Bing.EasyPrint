using System;

namespace Bing.EasyPrint.Printing;

/// <summary>
/// 描述一个可在宿主启动时显式注册的新协议；配置和描述在注册时复制，调用方后续修改输入对象不会改变注册快照。
/// </summary>
public sealed class PrintProtocolRegistration
{
    /// <summary>获取稳定的协议标识；只允许 ASCII 字母、数字、点、下划线和连字符。</summary>
    public string Id { get; }

    /// <summary>获取供设计器显示的协议名称。</summary>
    public string DisplayName { get; }

    /// <summary>获取 JSON Schema 形式的配置字段描述快照；空字符串表示无配置字段。</summary>
    public string ConfigSchemaJson { get; }

    /// <summary>获取 JSON 形式的约束描述快照；空字符串表示无额外约束。</summary>
    public string ConstraintsJson { get; }

    /// <summary>获取 JSON 形式的能力描述快照；由目录原样返回给设计器。</summary>
    public string CapabilitiesJson { get; }

    /// <summary>获取是否表示既有内置协议。</summary>
    public bool IsBuiltIn { get; }

    /// <summary>获取协议中立能力快照；旧内置注册可为 null。</summary>
    public PrintProtocolCapabilities Capabilities { get; }

    internal Func<string, IPrintProtocolBackend> BackendFactory { get; }
    internal Action<string> ConfigurationValidator { get; }

    /// <summary>
    /// 初始化一个协议注册项。
    /// </summary>
    /// <param name="id">稳定协议标识；不能为空或包含非 ASCII 标识字符。</param>
    /// <param name="displayName">设计器显示名称。</param>
    /// <param name="backendFactory">按 JSON 配置快照创建后端会话的工厂；不能为 null。</param>
    /// <param name="configSchemaJson">可选 JSON Schema 描述。</param>
    /// <param name="constraintsJson">可选约束 JSON 描述。</param>
    /// <param name="capabilitiesJson">可选能力 JSON 描述。</param>
    /// <param name="isBuiltIn">是否为既有内置协议。</param>
    /// <param name="capabilities">可选的协议中立能力快照。</param>
    /// <param name="configurationValidator">可选的配置校验委托；在创建后端前调用。</param>
    /// <exception cref="ArgumentException">协议标识或工厂无效时抛出。</exception>
    public PrintProtocolRegistration(string id, string displayName,
        Func<string, IPrintProtocolBackend> backendFactory, string configSchemaJson = null,
        string constraintsJson = null, string capabilitiesJson = null, bool isBuiltIn = false,
        PrintProtocolCapabilities capabilities = null, Action<string> configurationValidator = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("协议标识不能为空。", nameof(id));
        foreach (var character in id)
            if (!(character >= 'a' && character <= 'z' || character >= 'A' && character <= 'Z' ||
                  character >= '0' && character <= '9' || character == '.' || character == '_' ||
                  character == '-'))
                throw new ArgumentException("协议标识只允许 ASCII 字母、数字、点、下划线和连字符。", nameof(id));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("协议显示名称不能为空。", nameof(displayName));
        Id = id;
        DisplayName = displayName;
        BackendFactory = backendFactory ?? throw new ArgumentNullException(nameof(backendFactory));
        ConfigSchemaJson = configSchemaJson ?? string.Empty;
        ConstraintsJson = constraintsJson ?? string.Empty;
        CapabilitiesJson = capabilitiesJson ?? string.Empty;
        IsBuiltIn = isBuiltIn;
        Capabilities = capabilities;
        ConfigurationValidator = configurationValidator;
    }
}

/// <summary>
/// 注册表的只读协议描述快照；不暴露后端工厂。
/// </summary>
public sealed class PrintProtocolDescriptor
{
    /// <summary>获取稳定的协议标识。</summary>
    public string Id { get; }

    /// <summary>获取设计器显示名称。</summary>
    public string DisplayName { get; }

    /// <summary>获取 JSON Schema 描述快照。</summary>
    public string ConfigSchemaJson { get; }

    /// <summary>获取约束 JSON 快照。</summary>
    public string ConstraintsJson { get; }

    /// <summary>获取能力 JSON 快照。</summary>
    public string CapabilitiesJson { get; }

    /// <summary>获取是否为既有内置协议。</summary>
    public bool IsBuiltIn { get; }

    /// <summary>获取协议中立能力快照；旧内置注册可为 null。</summary>
    public PrintProtocolCapabilities Capabilities { get; }

    internal PrintProtocolDescriptor(PrintProtocolRegistration registration)
    {
        Id = registration.Id;
        DisplayName = registration.DisplayName;
        ConfigSchemaJson = registration.ConfigSchemaJson;
        ConstraintsJson = registration.ConstraintsJson;
        CapabilitiesJson = registration.CapabilitiesJson;
        IsBuiltIn = registration.IsBuiltIn;
        Capabilities = registration.Capabilities;
    }

    internal PrintProtocolDescriptor(string id, string displayName, string configSchemaJson,
        string constraintsJson, string capabilitiesJson, bool isBuiltIn,
        PrintProtocolCapabilities capabilities = null)
    {
        Id = id;
        DisplayName = displayName;
        ConfigSchemaJson = configSchemaJson;
        ConstraintsJson = constraintsJson;
        CapabilitiesJson = capabilitiesJson;
        IsBuiltIn = isBuiltIn;
        Capabilities = capabilities;
    }
}
