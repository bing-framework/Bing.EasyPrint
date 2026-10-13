# 模板渲染指南

## 功能说明

`Bing.EasyPrint.Templates` 将版本化模板、业务数据和受控资源转换为 CPCL、ESC/POS、TSPL 或 ZPL 的逐页输出。默认协议为 CPCL。模板支持 V1、V2、V3 模型、分页、重复明细、条件、页眉页脚、布局轨迹和任务级输出。

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

字段绑定支持常量值和对象路径；重复区从集合来源生成实例。绑定和资源异常会携带模板路径，Host 还会将其转换为结构化诊断。

## 配置

`TemplateRenderOptions` 的主要配置包括：

- `Protocol`、`EscPosLayout` 和 `Encoding`。
- `EscPosDevice`、`TsplMedia`、`ZplMedia`。
- 页面级 `Copies`、`FeedLines`、`CutAfterPrint`，或互斥的 `Job` 策略。
- `DeviceFontCalibrations` 与 `RequireDeclaredNativeBounds`。
- `MaxJsonBytes`、`MaxDepth`、`MaxDetailRows`、`MaxPages`、`MaxOutputBytes`、`MaxWorkingBytes`、`MaxImageBytes`、`MaxImagePixels`、`MaxPageHeight`。

编辑器可先调用 `TemplateProtocolCapabilities.Create(options)` 获取当前协议、布局和 Profile 的能力快照。该快照用于约束 UI 和静态校验，最终内容仍以实际渲染预检为准。

结构化 GS1 条码由 `Gs1ApplicationIdentifierParser` 统一解析括号 AI 和 FNC1 边界，当前包含 AI `253`、`254`、`255` 及 Scan4Transport 等已声明子集。模板条码与协议 Provider 共用这些规则；这不是完整 GS1 AI 字典，业务关系仍应由业务层校验。

## 价签与示例

`PriceLabelTemplateFactory` 提供 15/70/90 mm 价签预设，`PriceLabelRenderer` 负责价签数据适配与渲染。可执行示例位于 `samples/Templates`，包含固定标签、分页小票、V3 流式模板、任务输出和 TSPL/ZPL 切刀示例。

```powershell
dotnet run --project samples/Templates -- artifacts/template-samples
```

该命令只生成文件，不连接打印机。

## 依赖与限制

- 模板库依赖核心库、Newtonsoft.Json 13.0.3 和 ZXing.Net 0.16.11。
- 默认输出 CPCL；跨协议模板必须按目标协议能力设计，不能假设同一模板在四协议中具有完全相同的物理结果。
- TSPL/ZPL 设备字体的真实字宽、墨迹和 HRI 需要 Profile 或校准数据；严格模式会拒绝未声明边界。
- ESC/POS Native 适合顺序小票；坐标图形、图片、旋转和复杂布局应使用 Raster。
- 详细元素和兼容规则见[模板详细说明](templates.md)。
- 输入、图片、输出与工作内存限制见[资源限制与错误处理](resource-limits.md)。
