# 公开 API 入口

## 功能说明

公开 API 分为命令创建、统一模型渲染、模板渲染和能力/验证四类。选择入口时应先判断是否需要协议特有指令、跨协议模型或业务模板。

## 命令创建

`IEasyPrint.CreateCommand<T>()` 保留原有泛型入口，要求命令具有公开无参构造函数。扩展方法提供四协议的默认命令实例：

```csharp
IEasyPrint easyPrint = new DefaultEasyPrint();
var cpcl = easyPrint.CreateCPCLCommand();
var escPos = easyPrint.CreateEscPosCommand();
var tspl = easyPrint.CreateTsplCommand();
var zpl = easyPrint.CreateZplCommand();
```

需要显式编码、ESC/POS 布局或设备选项时，直接构造具体命令，或使用 `ProtocolCommandFactory.Create`：

```csharp
IProtocolPrintCommand command = ProtocolCommandFactory.Create(
    PrintProtocol.EscPos,
    Encoding.GetEncoding("gbk"),
    EscPosLayoutMode.Raster,
    escPosOptions: deviceOptions);

PrintCapabilities capabilities = command.Capabilities;
byte[] bytes = command.Build(cancellationToken).GetBytes();
```

工厂返回的 `IProtocolPrintCommand` 只公开协议、能力和构建。需要 `Text`、`Barcode`、`QrCode`、介质或裁切等协议特有方法时，应使用具体命令类型。

关键文件：

- `src/Bing.EasyPrint/IEasyPrint.cs`
- `src/Bing.EasyPrint/DefaultEasyPrint.cs`
- `src/Bing.EasyPrint/Extensions/Extensions.IEasyPrint.cs`
- `src/Bing.EasyPrint/ProtocolCommandFactory.cs`
- `src/Bing.EasyPrint/IProtocolPrintCommand.cs`

## 统一模型与 Provider

`PrintProtocolProviderFactory.Create` 按协议和设备选项创建 Provider。`Validate` 严格抛出异常；`Check` 扩展返回首个 `PrintValidationIssue`，适合编辑器预检；`Render` 返回逐页字节，`RenderOutput` 保留独立输出段。

```csharp
var document = new PrintDocument();
document.AddPage(576, 320)
    .Add(new PrintTextElement(20, 20, "SKU-001", 24, width: 180, height: 24), "Rows[0]");

var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.UTF8);
var validation = provider.Check(document, cancellationToken);
if (!validation.IsValid)
    throw new InvalidOperationException(validation.FirstIssue.Message);

var pages = provider.Render(document, cancellationToken);
```

关键文件：`Printing/IPrintProtocolProvider.cs`、`Printing/PrintProtocolProviders.cs`、`Printing/PrintValidationResult.cs` 和 `Printing/PrintPageOutput.cs`。

## 模板入口

`TemplateLoader` 负责版本化 JSON 的加载和保存；`TemplateRenderer.Render` 返回页面输出，`RenderWithLayout` 额外返回布局轨迹。`TemplateProtocolCapabilities.Create` 用于编辑前的能力查询。示例见[模板渲染指南](templates-guide.md)。

## 错误模型

- 参数格式或范围错误：`ArgumentException` 或 `ArgumentOutOfRangeException`。
- 协议能力不支持：`PrintCapabilityException` 或 `NotSupportedException`。
- 元素预检失败：`PrintElementValidationException`，包含协议、元素路径和页/元素索引。
- 模板失败：`TemplateRenderException`，保留模板路径和底层错误。
- 资源预算超限：`PrintResourceLimitException`。
- 取消：保持 `OperationCanceledException`，不转换为能力或输入错误。

## 依赖与配置

命令和 Provider 位于 `Bing.EasyPrint`；模板 API 位于 `Bing.EasyPrint.Templates`。GBK 等代码页需要注册 `CodePagesEncodingProvider`。设备选项必须只传给对应协议，错误混用会在创建或渲染前被拒绝。

## 已知限制

- `IEasyPrint` 扩展方法只使用默认构造配置，不表达设备 Profile。
- `IProtocolPrintCommand` 是通用构建契约，不提供各协议完整绘图 API。
- `Check` 只返回首个本地可判定问题，不替代真实打印机验收。
- 输出传输、重试和设备状态不属于公开 API。
