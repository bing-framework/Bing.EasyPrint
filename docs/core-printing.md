# 核心打印与协议

## 功能说明

核心库支持 CPCL、ESC/POS、TSPL 和 ZPL。它提供三层入口：原有 CPCL API、各协议独立命令 API，以及协议无关的 `PrintDocument` + Provider API。

## 直接命令 API

具体命令类型为 `CPCLPrintCommand`、`EscPosPrintCommand`、`TsplPrintCommand` 和 `ZplPrintCommand`。`ProtocolCommandFactory.Create` 可按协议返回 `IProtocolPrintCommand`，但协议特有的绘制方法仍应通过具体类型调用。

```csharp
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

```csharp
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
- Raw 可信入口可以绕过结构化校验，只适合已审查的固定指令。
- 项目不发送字节到设备，也不读取打印状态。
