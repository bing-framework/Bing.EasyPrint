# 新打印协议扩展指南

本文说明 O4 建立的新协议接入方式。目标协议以稳定字符串 ID 注册，不修改 `PrintProtocol` 枚举、内置 Provider switch 或设计器静态协议列表。内置 CPCL、ESC/POS、TSPL、ZPL 的公开 API、默认选项和既有输出保持原路径。

## 分层边界

| 层 | 职责 | 主要入口 |
| --- | --- | --- |
| Core | 冻结注册表、后端会话、有序页面/收尾输出、预算 | `PrintProtocolRegistry`、`IPrintProtocolBackend`、`PrintProtocolOutputComposer` |
| Templates | 自定义文字度量、分页、元素转换、布局归属 | `ITemplateProtocolAdapter`、`ITemplateTextMetrics`、`TemplateRenderOptions` |
| Host | 可信程序集启动注册、目录、能力、预览/导出 | `DesignerProtocolRegistry`、`/api/designer/protocol-registry` |
| Designer | 动态协议选项、配置表单、v2 项目、降级 | `src/api.ts`、`src/App.vue`、`src/designerProject.ts` |

协议 ID 只能包含 ASCII 字母、数字、点、下划线和连字符。ID 是请求、目录和项目文件的稳定标识，不是显示名称。

## Core 注册与后端

注册项在 Host 启动阶段显式创建，注册表随后冻结：

```csharp
var registry = new PrintProtocolRegistry();
registry.Register(new PrintProtocolRegistration(
    "demo.protocol",
    "Demo Protocol",
    configurationJson => new DemoBackend(configurationJson),
    configSchemaJson,
    constraintsJson,
    capabilitiesJson));
registry.Freeze();

using var backend = registry.CreateBackend("demo.protocol", configurationJson);
var outputs = PrintProtocolOutputComposer.Render(backend, document,
    finishing: null, maxOutputBytes: 32 * 1024 * 1024,
    maxPages: 100, cancellationToken);
```

约束：

- 重复 ID、空 ID、冻结后注册、未注册 ID 和工厂返回空会话均明确失败。
- 描述、配置和 JSON 能力以字符串快照保存；调用方后续修改输入对象不影响注册表。
- 后端每次请求创建并在 `using` 中释放；预检、取消、异常或预算失败不返回部分输出。
- 新协议 `Render` 会先把 `PrintPage.Copies` 展开为实际物理副本，并在分配副本、创建编码结果前按 `maxPages` 做溢出安全计数。`outputs.PageOrigins` 与 `outputs.Pages` 一一对应，提供源页和副本序号；页面顺序即发送顺序。旧内置 Provider 的原生 copies 路径使用内部 `RenderLegacy`，不经过该展开。
- 后端返回的页面数必须与规范化后的物理页数一致。页面若携带旧 `FeedLines`、`CutAfterPrint` 或 `CutOptions`，新协议入口会明确拒绝，必须使用 `finishing`。
- 需要任务收尾时实现 `IPrintProtocolFinishingBackend`，声明 `SupportsFinishing`，并提供预检、准确字节长度和独立控制文件。`PrintProtocolFinishingPlacement.PageEnd` 会在每个实际物理副本后执行；`copies>1` 时先展开副本，再做页尾收尾，`JobEnd` 只执行一次，预算计入全部控制文件。`RenderFinishing` 返回后、成功返回结果前还会再次观察取消令牌。同一名称的控制条目在一个 ZIP 中只能出现一次。

## 模板适配

`ITemplateProtocolAdapter` 必须真正决定测量和分页，而不是包装旧 CPCL/TSPL 渲染：

```csharp
public sealed class DemoProtocolAdapter : ITemplateProtocolAdapter
{
    public string ProtocolId => "demo.protocol";
    public TemplateCustomProtocolCapabilities Capabilities { get; }
    public ITemplateTextMetrics CreateTextMetrics(TemplateTextMeasurementRequest request) => ...;
    public IPrintProtocolBackend CreateBackend(string configurationJson) => ...;
}
```

`ITemplateTextMetrics` 的 `Advance`、`Measure`、`LineHeight` 和 `MeasureBounds` 会直接参与换行、分页和占位。自定义能力声明：

- `ElementTypes`、`TextSizes`、`TextRotations`、`FontKinds`、`MaxTextSize`、字体和图片旋转支持范围；
- `SupportsCoordinates`、`SupportsFilledRectangles`、`SupportsBoldText`、`SupportsUnderlineText`、`SupportsReverseText`；
- `BarcodeTypes`、`BarcodeRotations`、`SupportsExplicitBarcodeModuleWidth`、`SupportsWideModuleWidth`、`SupportsBarcodeHumanReadable`；
- `SupportsQrCode`、`QrCodeRotations`、`SupportsGs1QrCode`、`SupportsExplicitQuietZone`；
- `MaxPageWidthDots`、`MaxPageHeightDots`；
- `SupportsSoftwareBitmap`、`SoftwareBitmapElementTypes` 和明确的 `SoftwareBitmapReason`；
- `SupportsFinishing`、`SupportsExtraFeed`。
- `SupportsHostFontFamily`、`SupportsLegacyDeviceFont`；设备字体还须声明 `FontKinds` 包含 `Device`，主机字体须包含 `Host`。

`TextSizes` 为空表示在 `MaxTextSize` 内任意正整数，不是无限字号；`SupportsTextSize` 同时检查离散集合和最大值。字体选择在 `TemplateFontResolver` 中按 `FontKinds` 与对应开关校验：声明支持的设备字体交给适配器测量并进入 `PrintTextElement.DeviceFontName`，未声明才在编码前失败。

核心注册项与模板层应使用同一能力来源。`PrintProtocolCapabilities` 是核心事实快照，`TemplateCustomProtocolCapabilities.FromCore` 投影模板层能力；`WithMaxTextSize` 和 `WithExplicitQuietZone` 可在解析配置后生成一致的派生快照。`samples/Protocols/DemoProtocol` 的注册项和适配器已改为同一 `CreateCoreCapabilities(config)` 来源。

未声明能力必须早拒绝，不能静默降级到内置协议。普通 QR 与 GS1 QR 是独立能力；未声明 `SupportsGs1QrCode` 时，`Gs1=true` 必须在编码前失败。示例程序集支持文字、线段、矩形、一维码和 QR；能力快照声明软件位图元素类型为 `3/4`，声明 GS1 QR、图片和完整页面 PNG 不支持，其他元素按声明明确拒绝。

Host 使用 `TemplateCustomProtocolValidator` 校验元素类型、字号、旋转、字体、页面尺寸和自定义约束，然后复用现有绑定、重复、条件、组件和布局所有权逻辑。预览包仍只渲染一次；不支持软件位图的元素返回原因，不伪造 PNG。`MaxPages` 检查覆盖份数展开后的实际物理页数，超过限制或取消时不会调用后端渲染。

## Host 注册与请求

`tools/DesignerHost/DesignerProtocolRegistry.cs` 是唯一启动注册点。新增协议只需增加可信程序集引用和注册项，不改核心枚举或前端静态列表。

`GET /api/designer/protocol-registry` 返回冻结目录。下面是节选示意，完整 `configSchema` 字段和 `capabilities` 以端点实际响应及 `DemoProtocolExtension.ConfigSchemaJson` 为准：

```json
{
  "contract": "1",
  "protocols": [
    {
      "id": "demo.protocol",
      "displayName": "Demo Protocol",
      "isBuiltIn": false,
      "configSchema": { "type": "object", "properties": { "characterWidth": {}, "dpi": {} } },
      "constraints": { "maxPageHeight": 100000, "elementTypes": [1, 3, 4, 5, 7] },
      "capabilities": { "dpi": 203, "elementTypes": [1, 3, 4, 5, 7], "supportsCoordinates": true }
    }
  ]
}
```

自定义请求使用 `protocolId` 和可选 `protocolConfig`：

```json
{
  "template": { "...": "..." },
  "data": {},
  "protocolId": "demo.protocol",
  "protocolConfig": { "characterWidth": 10, "lineHeight": 30, "maxPageHeight": 600 }
}
```

`protocolId` 与旧 `protocol` 按请求字段实际存在判断互斥。带有 `protocolId` 时，内置协议专用字段（`escPosDevice`、`tsplMedia`、`zplMedia`、`escPosLayoutMode`、`encoding`、设备字体校准、`cutOptions`、`job` 等）也会被拒绝，避免静默混用。未知 ID、无效配置、字段冲突和缺失软件位图分别返回结构化错误；Host 不把新协议映射为 CPCL 输出。

`/api/designer/capabilities`、`/preview`、`/render`、`/preview-bundle` 和 `/element-png` 支持自定义请求。收尾通过 `finishing: { feedAmount, feedUnit, cut, placement }` 传入；`JobEnd`/`PageEnd` 控制文件在 ZIP 中独立写出，不计入物理页，`PageEnd` 按实际物理副本执行且与旧 feed/cut 字段互斥。旧 `feedLines`/`cutAfterPrint` 只映射为 JobEnd 收尾请求，不写入页面字段。示例协议对 QR 和条码声明元素软件位图，但不声明完整页面 PNG；Host 分别提供元素 PNG 和明确拒绝页面 PNG。旧 Host 没有 `protocol-registry` 特性时，设计器不生成扩展协议入口。

## 设计器项目 v2

新项目保存为版本 2 envelope，读取 v1 和裸模板：

```json
{
  "format": "Bing.EasyPrint.DesignerProject",
  "version": 2,
  "template": { "Version": 2, "Width": 576, "Height": 360, "Rows": [] },
  "outputSettings": {
    "outputTarget": "Cpcl",
    "protocolId": "demo.protocol",
    "protocolConfig": { "characterWidth": 10, "lineHeight": 30, "maxPageHeight": 600 }
  }
}
```

- 目录存在时，设计器从 `configSchema` 生成基础字段，并保留高级 JSON 编辑。
- 未知协议 ID 或旧 Host 缺少目录时保留 `protocolId`/`protocolConfig`，显示“未知协议”并禁用预览、渲染和下载；不会回退替换为内置协议。
- v1/裸模板没有 `protocolId`，继续按原内置协议行为加载。
- 保存、撤销/重做和重新加载均保留协议快照；本阶段不实现 F6 的锁定/排列能力。

## 示例协议

`samples/Protocols/DemoProtocol` 使用 `demo.protocol`，支持元素类型 `1`（文字）、`3`（QR）、`4`（一维码）、`5`（线段）、`7`（矩形）；类型 `3/4` 提供元素软件位图，并支持可选收尾控制文件。示例配置：

```json
{
  "characterWidth": 10,
  "lineHeight": 30,
  "maxPageHeight": 600,
  "supportsFinishing": true,
  "dpi": 203
}
```

`characterWidth`、`lineHeight`、`maxPageHeight`、`barcodeModuleWidth`、`qrModuleWidth` 和 `dpi` 会实际改变断行、坐标、码图尺寸、页数或能力快照。Schema 中 `dpi` 只接受 `203/300/600`，`additionalProperties=false`。示例声明一维码/QR 和对应元素软件位图，但不声明图片、GS1 QR、完整页面 PNG；请求这些能力会失败并说明原因。它只用于演示注册和模板/Host 集成，不代表真实设备固件。

## 验证入口

```powershell
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -c Debug -f net10.0
dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj -c Debug
cd web/Bing.EasyPrint.Designer
npm run build
npm test
npx playwright test --workers=1 --retries=0
```

跨运行时和 package consumer 验证使用 `tools/Verify-ProtocolRuntimes.ps1`、`tools/Verify-XmlDocumentation.ps1`、`tools/Verify-DesignerHost.ps1`、`tools/Verify-ModernConsumption.ps1`。阶段证据保存在 `artifacts/optimization-expansion-20261025/phase2/`。软件测试和浏览器测试不等于真实打印机固件验收。
