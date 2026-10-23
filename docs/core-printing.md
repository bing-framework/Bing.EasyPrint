# 核心打印与协议

DataBar Truncated 与 Stacked Omnidirectional 已接入 Composite Auto/CC-A/CC-B；新增 `BarcodeType.Gs1DataBarStackedOmnidirectional=32` 支持普通 ESC/POS Raster。Truncated Composite 条高至少 13X；Stacked Omnidirectional 的条高为每条数据行高度、至少 33X，另有三个内部分隔行。完整输入、尺寸与旧 Host 协商见[使用说明](gs1-composite-databar-completion.md)。

EAN-8/EAN-13/UPC-A/UPC-E Composite 已接入四种图片协议、V2/V3 模板和设计器，支持 Auto/CC-A/CC-B 及严格 2/5 位附加码；三条分隔行各为 2X，主码校验位沿用原规则。旧 Host 通过 `gs1-composite-ean-upc` 保留纯数字输入；带附加码的完整契约见[EAN/UPC 附加码使用说明](gs1-composite-ean-upc-addons.md)。

ExpandedStacked 已支持普通 ESC/POS Raster 条码及四协议 Composite CC-A/CC-B；省略或 `null` 默认每行两个字符对，普通条码可配置 1–11 列、Composite 可配置 2–11 列。显式配置列数需 `gs1-databar-expanded-stacked-columns`，Host 同时声明对应范围。详细契约见[列数配置指南](expanded-stacked-columns.md)和 [ExpandedStacked 使用说明](gs1-composite-expanded-stacked.md)。

## 功能说明

核心库支持 CPCL、ESC/POS、TSPL 和 ZPL。它提供三层入口：原有 CPCL API、各协议独立命令 API，以及协议无关的 `PrintDocument` + Provider API。

## 直接命令 API

具体命令类型为 `CPCLPrintCommand`、`EscPosPrintCommand`、`TsplPrintCommand` 和 `ZplPrintCommand`。`ProtocolCommandFactory.Create` 可按协议返回 `IProtocolPrintCommand`，但协议特有的绘制方法仍应通过具体类型调用。

`DefaultEasyPrint` 和 `IEasyPrint` 扩展方法用于默认构造；需要设备 Profile 或介质选项时不要依赖默认扩展。入口选择见[公开 API 入口](public-api.md)。

```csharp
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Protocols;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var bytes = new EscPosPrintCommand(Encoding.GetEncoding("gbk"))
    .Initialize()
    .Text("订单完成")
    .Feed(2)
    .Build()
    .GetBytes();
```

关键文件：`ProtocolCommandFactory.cs`、`IProtocolPrintCommand.cs`、`Protocols/*PrintCommand.cs` 和 `CPCL/CPCLPrintCommand*.cs`。

### Raw 入口

- `WriteRaw`、`WriteRawLine` 和 `WriteRawBytes` 是可信逃生口，调用方负责内容安全和状态一致性。
- `WriteRestrictedRaw(string)` 只接受当前协议可解析、可验证的文本子集；四个命令类型均实现 `IRestrictedRawPrintCommand<T>`。
- 受限 Raw 会检查语法、能力、状态和几何边界；未知命令、厂商私有扩展、资源下载及无法确定边界的二进制载荷会被拒绝。
- 验证失败使用参数异常，并保持批次原子性。完整允许范围见[协议详细说明](protocols.md)。

## 协议无关模型

`PrintDocument` 包含一个或多个 `PrintPage`；页面可加入文字、线、矩形、条码、二维码和图片元素。Provider 在输出前调用同一套能力和边界校验。

普通 ESC/POS Raster `BarcodeType.Gs1DataBarExpandedStacked` 和 `PrintGs1CompositeElement` 可通过 `WithExpandedStackedColumns(columns)` 选择每行字符对数；普通条码范围为 1–11，Composite 为 2–11，省略配置默认两列。配置字段、尺寸与 Host 协商见[ExpandedStacked 列数指南](expanded-stacked-columns.md)。

```csharp
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Printing;

var document = new PrintDocument();
var page = document.AddPage(576, 320);
page.Add(new PrintTextElement(20, 20, "SKU-001", 24, width: 180, height: 24));

var provider = PrintProtocolProviderFactory.Create(PrintProtocol.Zpl, Encoding.UTF8);
var pages = provider.Render(document);
byte[] firstPage = pages[0];
```

构造器的完整参数以 `src/Bing.EasyPrint/Printing/PrintDocument.cs` 为准。元素的 `SourcePath` 会进入能力和几何异常，便于模板或调用方定位问题。

## 协议差异

| 协议 | 布局 | 主要输出 | 设备配置 |
| --- | --- | --- | --- |
| CPCL | 坐标 | 标签、文字、线框、码图、图片 | 原有 CPCL 参数与统一能力校验 |
| ESC/POS Native | 顺序 | 文字、进纸、裁切、设备原生条码/二维码 | `EscPosDeviceOptions`、`EscPosPrinterProfile` |
| ESC/POS Raster | 坐标转位图 | `GS v 0` 或 `ESC *` 单色图像 | 图片指令、DPI、纸宽、固定字体资源 |
| TSPL | 坐标 | `SIZE`、`TEXT`、`BARCODE`、`QRCODE`、`BITMAP` | `TsplMediaOptions`、标签/切刀 Profile |
| ZPL | 坐标格式 | `^XA/^XZ`、字段、条码、`^GFA` | `ZplMediaOptions`、标签/切刀 Profile |

能力查询使用 `PrintCapabilities`。统一模型与直接命令的能力并不完全相同，例如 ESC/POS Raster 可由主机绘制码图，但不等于设备支持原生 `GS k`。

`PrintGs1CompositeElement` 将 GS1 线性组件和 CC-A、CC-B 或 CC-C 次码作为一个不可拆分符号处理，尺寸由编码结果确定。组合受限：DataBar Omnidirectional/Truncated/Limited/单行 Expanded/Stacked/StackedOmnidirectional/ExpandedStacked 支持 Auto、CC-A、CC-B；GS1-128 (`BarcodeType.UccEan128`) 支持 Auto、CC-A、CC-B 和 CC-C，且 Auto 直接选择 CC-C。其他配对会被拒绝。Stacked 的线性 DataBar 宽 50 模块，完整 Composite 固定宽 56 模块（55 模块二维组件加 1 模块横向对齐，不含显式静区），模式和次数据只改变高度；条高是上下数据行总高且至少为模块宽的 12 倍。该功能只在能力同时声明图片和坐标时可用；ESC/POS Native 不支持，调用方应先检查 `PrintCapabilities.SupportsGs1Composite`。完整输入、模式和 Host 协商说明见 [GS1-128 + CC-A/CC-B 使用说明](gs1-composite-gs1-128-ab.md)、[GS1-128 + CC-C Composite 使用说明](gs1-composite-cc-c.md)、[单行 Expanded 使用说明](gs1-composite-expanded.md)及[Stacked 使用说明](gs1-composite-stacked.md)。标签介质方向遵循各协议自身规则：`ZplMediaOptions.Orientation` 省略时沿用打印机当前方向，显式 `None` 输出 `^PON`，`Down` 输出 `^POI`；ZPL 整页方向不接受 `Left`/`Right`，但字段元素的旋转角度仍由各自命令处理。

应用可以直接向统一模型提供自定义 `IPrintProtocolProvider`；静态 `PrintProtocolProviderFactory` 只创建内置四协议 Provider。`TemplateRenderer` 目前固定使用该内置工厂，`TemplateRenderOptions` 没有 Provider/工厂注入点。自定义 Provider 也不能直接复用内置任务收尾与部分切纸编码；请求这些功能时会按不支持处理。实现接口本身不会自动把新协议接入模板或 Host。

## 打印任务与输出段

`PrintJobOptions` 描述整份文档的份数、Collated/Uncollated 顺序、进纸单位和执行范围、裁切范围及显式裁切参数。`PrintJobRenderer` 输出带源页、份数和收尾标志的结果。页面级份数/进纸/裁切不能与任务策略叠加。

部分 ZPL 切刀策略会产生必须独立发送的控制段。调用方应读取 `PrintPageOutput.Segments`，按顺序分别传输；不能假设每页始终只有一个连续字节数组。

## 依赖与配置

- 目标框架：`net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`。
- 图片和绘图：ImageSharp 2.1.11、ImageSharp.Drawing 1.0.0-beta15。
- 主机生成码图：ZXing.Net 0.16.11。
- GBK 等代码页需要应用注册 `CodePagesEncodingProvider`，并确保打印机使用同一代码页。

## 限制与注意事项

- 默认严格拒绝无法表达的样式、旋转、条码、切纸和介质操作；不要把 `Capabilities` 当作真实固件证明。
- ESC/POS Native 不能表达通用绝对坐标；需要线框、任意坐标或主机字体时使用 Raster。
- Raster 输出会增加字节量和主机计算成本，并受图片块、总输出及工作内存预算限制。
- CPCL 图片识别前会检查 TIFF 头和目录链边界；异常 TIFF 结构进入既有图片输入错误路径。该检查只预检 TIFF 目录结构，不能中断 ImageSharp 对任意合法或其他格式图片的同步解码；图片字节、像素和工作资源限制仍须配合使用。
- Core、模板和 Host 的预算并不等价，配置方式见[资源限制与错误处理](resource-limits.md)。
- Raw 可信入口可以绕过结构化校验，只适合已审查的固定指令。
- 项目不发送字节到设备，也不读取打印状态。
