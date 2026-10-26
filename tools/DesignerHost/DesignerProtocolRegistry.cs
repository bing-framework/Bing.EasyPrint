using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;
using Bing.EasyPrint.Templates;
using DemoProtocol;

/// <summary>
/// DesignerHost 启动阶段显式注册的协议目录；内置协议仅用于目录/后端适配，新协议 ID 不能替代旧 protocol 选择器。
/// </summary>
internal sealed class DesignerProtocolRegistry
{
    private readonly PrintProtocolRegistry _registry;
    private readonly Dictionary<string, Func<string, ITemplateProtocolAdapter>> _adapters;

    private DesignerProtocolRegistry(PrintProtocolRegistry registry,
        Dictionary<string, Func<string, ITemplateProtocolAdapter>> adapters)
    {
        _registry = registry;
        _adapters = adapters;
    }

    internal IReadOnlyList<PrintProtocolDescriptor> Descriptors => _registry.Descriptors;

    internal bool IsFrozen => _registry.IsFrozen;

    internal PrintProtocolRegistry CoreRegistry => _registry;

    internal bool Contains(string id) => _registry.Contains(id);

    internal bool TryGetAdapter(string id, string configurationJson, out ITemplateProtocolAdapter? adapter,
        out Exception? error)
    {
        adapter = null;
        error = null;
        if (!_adapters.TryGetValue(id, out var factory))
        {
            error = new ArgumentException($"协议 {id} 没有可用的模板布局适配器。", nameof(id));
            return false;
        }
        try
        {
            _registry.ValidateConfiguration(id, configurationJson ?? string.Empty);
            adapter = factory(configurationJson ?? string.Empty);
            return true;
        }
        catch (Exception exception)
        {
            error = exception;
            return false;
        }
    }

    internal IPrintProtocolBackend CreateBackend(string id, string configurationJson)
        => _registry.CreateBackend(id, configurationJson);

    internal static DesignerProtocolRegistry CreateForTests(PrintProtocolRegistration registration,
        Func<string, ITemplateProtocolAdapter> adapterFactory)
    {
        var registry = new PrintProtocolRegistry();
        registry.Register(registration);
        registry.Freeze();
        return new DesignerProtocolRegistry(registry,
            new Dictionary<string, Func<string, ITemplateProtocolAdapter>>
            {
                [registration.Id] = adapterFactory
            });
    }

    internal static DesignerProtocolRegistry CreateDefault()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var registry = new PrintProtocolRegistry();
        var encoding = Encoding.GetEncoding("gbk");
        RegisterBuiltIn(registry, "cpcl", "CPCL", PrintProtocol.Cpcl, encoding);
        RegisterBuiltIn(registry, "escpos", "ESC/POS", PrintProtocol.EscPos, encoding);
        RegisterBuiltIn(registry, "tspl", "TSPL", PrintProtocol.Tspl, encoding);
        RegisterBuiltIn(registry, "zpl", "ZPL", PrintProtocol.Zpl, encoding);
        registry.Register(DemoProtocolExtension.CreateRegistration());
        registry.Freeze();
        return new DesignerProtocolRegistry(registry, new Dictionary<string, Func<string, ITemplateProtocolAdapter>>
        {
            ["demo.protocol"] = DemoProtocolExtension.CreateAdapter
        });
    }

    private static void RegisterBuiltIn(PrintProtocolRegistry registry, string id, string displayName,
        PrintProtocol protocol, Encoding encoding)
    {
        registry.Register(new PrintProtocolRegistration(id, displayName, configurationJson =>
        {
            var provider = PrintProtocolProviderFactory.Create(protocol, encoding, EscPosLayoutMode.Native,
                null, null, null, false);
            return new PrintProtocolProviderBackend(id, provider);
        }, "{}", "{}", "{\"legacyProtocol\":\"" + protocol + "\"}", isBuiltIn: true));
    }
}
