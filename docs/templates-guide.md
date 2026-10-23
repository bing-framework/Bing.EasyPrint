# 模板渲染指南

DataBar Truncated 与 Stacked Omnidirectional 已接入 Composite Auto/CC-A/CC-B；新增 `BarcodeType.Gs1DataBarStackedOmnidirectional=32` 支持普通 ESC/POS Raster。Truncated Composite 条高至少 13X；Stacked Omnidirectional 的条高为每条数据行高度、至少 33X，另有三个内部分隔行。完整输入、尺寸与旧 Host 协商见[使用说明](gs1-composite-databar-completion.md)。

EAN-8/EAN-13/UPC-A/UPC-E Composite 已接入四种图片协议、V2/V3 模板和设计器，支持 Auto/CC-A/CC-B 及严格 2/5 位附加码；主码校验位仍按原规则处理。旧 Host 保留纯数字输入，附加码模板格式与协商见[使用说明](gs1-composite-ean-upc-addons.md)。

ExpandedStacked 已支持普通 ESC/POS Raster 条码及四协议 Composite CC-A/CC-B；省略或 `null` 默认每行两个字符对，普通条码可配置 1–11 列、Composite 可配置 2–11 列，也可配置 2–11 条数据行上限（与列数互斥）。显式配置列数/行数需 `gs1-databar-expanded-stacked-columns` / `gs1-databar-expanded-stacked-rows`，Host 同时声明对应范围。详细契约见[列数配置指南](expanded-stacked-columns.md)、[最大行数与 GS1 AI 字典](expanded-stacked-rows-and-gs1-ai.md)和 [ExpandedStacked 使用说明](gs1-composite-expanded-stacked.md)。

## 功能说明

`Bing.EasyPrint.Templates` 将版本化模板、业务数据和受控资源转换为 CPCL、ESC/POS、TSPL 或 ZPL 的逐页输出。默认协议为 CPCL。模板支持 V1、V2、V3 模型、分页、重复明细、条件、页眉页脚、布局轨迹和任务级输出；GS1 Composite Type 11 元素支持 V2/V3，并在目标 Provider 能力允许时生成。当前 EAN-8/EAN-13/UPC-A/UPC-E、DataBar Omnidirectional/Truncated/Limited、单行 Expanded、Stacked、StackedOmnidirectional 和 ExpandedStacked 支持 CC-A/CC-B，EAN/UPC 主字段可附加严格 2/5 位数字码，GS1-128 支持 CC-A/CC-B/CC-C；各制式输入、尺寸和能力协商限制见 [GS1-128 + CC-A/CC-B 使用说明](gs1-composite-gs1-128-ab.md)、[GS1-128 + CC-C Composite 使用说明](gs1-composite-cc-c.md)、[单行 Expanded 使用说明](gs1-composite-expanded.md)、[Stacked 使用说明](gs1-composite-stacked.md)及[EAN/UPC 附加码使用说明](gs1-composite-ean-upc-addons.md)。

## 基本用法

```csharp
using System.Text;
using Bing.EasyPrint;
using Bing.EasyPrint.Templates;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var loader = new TemplateLoader();
var template = loader.LoadJson(json);
var result = new TemplateRenderer().RenderWithLayout(
    template,
    order,
    resources: resourceProvider,
    options: new TemplateRenderOptions
    {
        Protocol = PrintProtocol.Tspl,
        Encoding = Encoding.GetEncoding("gbk")
    },
    cancellationToken: cancellationToken);

foreach (var page in result.Pages)
{
    byte[] data = page.Data;
    // 交给应用自己的打印传输层。
}
```

`TemplateLoader.SaveJson` 保存经过结构校验的模板并保留版本。`Render` 返回协议页；`RenderWithLayout` 还保留设计器和诊断使用的布局元素。

## 核心实现逻辑

1. `TemplateLoader` 限制 JSON 字节数、深度和对象结构，不执行模板代码。
2. `TemplateRenderer` 解析字段、条件、重复来源和资源，再计算换行、分页与固定区域。
3. 布局被转换为统一打印模型，并通过目标 Provider 的能力与几何预检。
4. 成功后生成 `TemplateRenderResult`；页面包含协议字节、输出段、布局轨迹和可选任务元数据。

关键文件：`TemplateLoader.cs`、`TemplateRenderer.cs`、`TemplateRenderOptions.cs`、`TemplateProtocolCapabilities.cs`、`TemplateRenderResult.cs` 和 `TemplateTableFactory.cs`。

## 资源与绑定

图片通过 `ITemplateResourceProvider` 按资源键读取。渲染器不根据模板内容读取任意文件或 URL。设计器模板包把资源放入 ZIP 并经过路径、数量和大小校验。

CPCL 与模板图片入口在 `Image.Identify` 前预检 TIFF 目录结构；截断、越界或循环目录进入图片输入错误路径，模板诊断在资源键可知时保留 `ResourceName`。预检不保证可中断任意同步解码；单图字节数、像素数和工作资源上限仍然生效。GIF/TIFF 等多帧资源仍只处理首帧。

字段绑定支持常量值和对象路径；重复区从集合来源生成实例。绑定和资源异常会携带模板路径，Host 还会将其转换为结构化诊断。

Composite 的主数据使用继承的 `Field`，次数据必须提供 `SecondaryField`。EAN/UPC 主数据可为纯零售数字，或按严格格式追加一个 `+` 和 2/5 位 ASCII 附加码；其他 DataBar Omnidirectional/Truncated/Limited/单行 Expanded/Stacked/StackedOmnidirectional/ExpandedStacked 只接受 Auto/CC-A/CC-B，GS1-128 (`UccEan128`) 接受 Auto/CC-A/CC-B/CC-C，Auto 直接选择 CC-C；DataBar+CC-C 及其他未声明配对在导入验证和渲染预检时拒绝。EAN/UPC 输入与旧 Host 协商见[附加码使用说明](gs1-composite-ean-upc-addons.md)。Stacked 的 `LinearBarHeight` 是上下两条数据行的总高，不含内部一模块分隔行，且不得小于 `12 × ModuleWidth`。模块宽度和线性条高均为打印点数，整个符号作为一个布局块，旋转后的编码尺寸用于单元格和页面边界检查。模板静区随完整符号旋转；设计器画布显示选择/布局占位，不显示可扫描的编码位图，在线 Host 预览返回实际编码尺寸。旧 Host 仅具备 `gs1-composite` 时保留其已声明组合；GS1-128 Auto/CC-C 需要 `gs1-composite-cc-c`，显式 CC-A/CC-B 还需 `gs1-composite-gs1-128-ab` 及 `UccEan128`、`CcA`、`CcB` 能力，EAN/UPC 需要 `gs1-composite-ean-upc`，附加码需 `gs1-composite-ean-upc-addons`；ExpandedStacked 需要 `gs1-composite-expanded-stacked`；Expanded 和 Stacked 分别需要对应协商特性和线性制式能力。缺失时设计器禁用并提示，服务端对强制请求明确拒绝。详细协议与模板示例见 [GS1-128 + CC-A/CC-B 使用说明](gs1-composite-gs1-128-ab.md)、[GS1-128 + CC-C 使用说明](gs1-composite-cc-c.md)、[Expanded 使用说明](gs1-composite-expanded.md)、[Stacked 使用说明](gs1-composite-stacked.md)及[EAN/UPC 附加码使用说明](gs1-composite-ean-upc-addons.md)。

## 配置

`TemplateRenderOptions` 的主要配置包括：

- `Protocol`、`EscPosLayout` 和 `Encoding`。
- `EscPosDevice`、`TsplMedia`、`ZplMedia`。
- 页面级 `Copies`、`FeedLines`、`CutAfterPrint`，或互斥的 `Job` 策略。
- `DeviceFontCalibrations` 与 `RequireDeclaredNativeBounds`。
- `MaxJsonBytes`、`MaxDepth`、`MaxDetailRows`、`MaxPages`、`MaxOutputBytes`、`MaxWorkingBytes`、`MaxImageBytes`、`MaxImagePixels`、`MaxPageHeight`。

编辑器可先调用 `TemplateProtocolCapabilities.Create(options)` 获取当前协议、布局和 Profile 的能力快照。该快照用于约束 UI 和静态校验，最终内容仍以实际渲染预检为准。

结构化 GS1 条码由 `Gs1ApplicationIdentifierParser` 统一解析括号 AI 和 FNC1 边界，AI 集合与逐字段规则来自固定提交 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6` 的完整 GS1 Barcode Syntax Dictionary（536 个已定义 AI），包括 253/254/255 与 Scan4Transport 等族。模板条码与协议 Provider 共用这些规则；跨 AI 的 `req`/`ex` 业务关联、具体码制容量和扫描语义仍由编码器或业务层负责，边界见[最大行数与 GS1 AI 字典](expanded-stacked-rows-and-gs1-ai.md)。

自定义 `IPrintProtocolProvider` 可用于直接渲染统一 `PrintDocument`，但 `TemplateRenderer` 当前固定创建内置 Provider，不能通过 `TemplateRenderOptions` 注入自定义 Provider 或工厂。自定义 Provider 的任务收尾和部分切纸编码也不自动获得内置实现；新增协议要接入模板/Host，需扩展其明确的 Provider 分发与收尾路径。

## 价签与示例

`PriceLabelTemplateFactory` 提供 15/70/90 mm 价签预设，`PriceLabelRenderer` 负责价签数据适配与渲染。可执行示例位于 `samples/Templates`，包含固定标签、分页小票、V3 流式模板、任务输出、TSPL/ZPL 切刀和 GS1-128/CC-A/CC-B/CC-C、Expanded/Stacked CC-A/B Composite 案例；见 [C# 案例](../samples/Templates/Gs1CompositeSamples.cs)、[GS1-128 CC-A 模板 JSON](../samples/Templates/gs1-composite-gs1-128-cc-a.json)、[GS1-128 CC-B 模板 JSON](../samples/Templates/gs1-composite-gs1-128-cc-b.json)、[CC-C 模板 JSON](../samples/Templates/gs1-composite-cc-c.json)、[Expanded 模板 JSON](../samples/Templates/gs1-composite-expanded.json)和[Stacked 模板 JSON](../samples/Templates/gs1-composite-stacked.json)。

```powershell
dotnet run --project samples/Templates/Templates.csproj -- artifacts/template-samples
```

该命令只生成文件，不连接打印机。

## 依赖与限制

- 模板库依赖核心库、Newtonsoft.Json 13.0.3 和 ZXing.Net 0.16.11。
- 默认输出 CPCL；跨协议模板必须按目标协议能力设计，不能假设同一模板在四协议中具有完全相同的物理结果。
- TSPL/ZPL 设备字体的真实字宽、墨迹和 HRI 需要 Profile 或校准数据；严格模式会拒绝未声明边界。
- ESC/POS Native 适合顺序小票；坐标图形、图片、旋转和复杂布局应使用 Raster。
- ZPL 的整页 `ZplMediaOptions.Orientation` 只接受默认、`None` 或 `Down`；`Left`/`Right` 配置会在预检阶段明确拒绝。
- 详细元素和兼容规则见[模板详细说明](templates.md)。
- 输入、图片、输出与工作内存限制见[资源限制与错误处理](resource-limits.md)。
