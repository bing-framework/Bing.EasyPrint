# 设计器与 Host

DataBar Truncated 与 Stacked Omnidirectional 已接入 Composite Auto/CC-A/CC-B；新增 `BarcodeType.Gs1DataBarStackedOmnidirectional=32` 支持普通 ESC/POS Raster。Truncated Composite 条高至少 13X；Stacked Omnidirectional 的条高为每条数据行高度、至少 33X，另有三个内部分隔行。完整输入、尺寸与旧 Host 协商见[使用说明](gs1-composite-databar-completion.md)。

EAN-8/EAN-13/UPC-A/UPC-E Composite 已接入四种图片协议、V2/V3 模板和设计器，支持 Auto/CC-A/CC-B 及严格 2/5 位附加码；主码校验规则不变。固定值与动态绑定的 Host 协商差异见[EAN/UPC 附加码使用说明](gs1-composite-ean-upc-addons.md)。

ExpandedStacked 已支持普通 ESC/POS Raster 条码及四协议 Composite CC-A/CC-B；省略或 `null` 默认每行两个字符对，普通条码可配置 1–11 列、Composite 可配置 2–11 列。显式配置列数/行数需 `gs1-databar-expanded-stacked-columns` / `gs1-databar-expanded-stacked-rows`，Host 同时声明对应范围。详细契约见[列数配置指南](expanded-stacked-columns.md)和 [ExpandedStacked 使用说明](gs1-composite-expanded-stacked.md)。

## 功能说明

仓库提供 Vue 设计器和 ASP.NET Core Host。设计器编辑 V1/V2/V3 模板、业务样例、协议与设备选项，显示布局预览并导出协议页面；Host 复用模板库执行严格校验和渲染。两者均不连接打印机或业务数据库。

## 启动

```powershell
dotnet run --project tools/DesignerHost/DesignerHost.csproj
```

Host 默认监听 `http://127.0.0.1:5187`。设计器默认由 Vite 启动：

```powershell
cd web/Bing.EasyPrint.Designer
npm ci
npm run dev
```

开发代理和端口以 `vite.config.ts` 为准。Playwright 配置会同时启动 Host 与 Vite。

首次执行浏览器验收时，在设计器目录执行 `npm ci` 后运行 `npx playwright install chromium` 安装 Chromium 浏览器。完整验证命令见[开发与验证](development.md)。

## HTTP 接口

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/designer/health` | 健康状态与契约描述 |
| GET | `/api/designer/samples` | 内置模板样例 |
| GET | `/api/designer/protocol-registry` | 启动时冻结的协议目录、配置 Schema、约束和能力 JSON |
| POST | `/api/designer/capabilities` | 按协议、布局和 Profile 返回能力 |
| POST | `/api/designer/preview` | 返回布局与诊断信息 |
| POST | `/api/designer/render` | 返回实际协议输出 |
| POST | `/api/designer/element-png` | 返回单个元素的真实软件位图 `image/png` |
| POST | `/api/designer/page-png` | ESC/POS Raster 单页 PNG；多页 PNG ZIP |
| POST | `/api/designer/preview-bundle` | 单次渲染返回当前页布局清单、元素 PNG 和逐元素诊断 ZIP |
| GET | `/api/designer/gs1-catalog` | 固定版本的完整 GS1 AI 目录 |
| POST | `/api/designer/gs1-validate` | 使用当前模板和数据权威校验结构化来源 |
| POST | `/api/designer/gs1-convert` | 转换括号 Elements 与 Digital Link |

Host 通过 `X-EasyPrint-Contract-Version`、`X-EasyPrint-Features` 和 `X-EasyPrint-Required-Features` 协商契约。错误响应使用稳定的 HTTP 状态、`code`、`phase`、可选 `fieldPath` 和资源诊断。

GS1 编辑器要求 `gs1-ai-catalog`、`gs1-structured-input`、`gs1-digital-link` 三项特性，并以 Host 返回的固定 536 项 AI 目录为唯一元数据来源。目录响应含字典提交、Digital Link 角色/分支、长度、组件及关联规则；验证端点返回 valid、pending 或 invalid，并携带模板路径和 AI 项诊断。转换接口返回可写回模板的结构化结果。反向代理需要透传现有契约请求和响应头。详细字段与示例见[GS1 Digital Link 与结构化 AI](gs1-digital-link.md)。

新增 `protocol-registry` 特性。`GET /api/designer/protocol-registry` 只返回启动时显式注册并冻结的目录；`tools/DesignerHost/DesignerProtocolRegistry.cs` 是内置适配和独立示例的注册点。`POST` 请求可用 `protocolId` 与 `protocolConfig` 选择新协议；`protocolId` 和旧 `protocol` 按字段实际存在互斥，新协议不能携带 ESC/POS、TSPL、ZPL 专用字段、设备字体校准、`cutOptions` 或 `job`。未知 ID、无效配置和冲突返回结构化错误。`/capabilities`、`/preview`、`/render`、`/preview-bundle`、`/element-png` 均支持新协议；示例协议对 QR/条码声明元素软件位图，但未声明完整页面 PNG；Host 提供受支持元素的 `/element-png`，并明确拒绝 `/page-png` 或未声明元素。旧 Host 缺少 `protocol-registry` 时设计器不提供扩展协议入口，未知协议项目保留 ID/配置并禁用预览与下载。完整接入步骤见[新打印协议扩展指南](protocol-extension-guide.md)。

Host 新增 `field-format` 特性；`BgField.Format` 的显示值由服务端绑定和预览返回（`displayText`），设计器只编辑配置。新增 `batch-export` 特性和 `POST /api/designer/batch`：单请求最多 25 条记录，客户端累计最多 1000 条/1000 实际物理页/32 MiB；响应为含稳定记录目录、`manifest.json` 和 `errors.csv` 的 ZIP。批量重放单条模板/协议/收尾路径，独立控制文件保持独立条目，页数按实际副本计算；详细契约见[批量生成](batch-generation.md)。

自定义能力响应中的 `textSize.maximum`、`fontKinds`、`supportsHostFontFamily`、`supportsLegacyDeviceFont`、`supportsCoordinates`、码制/旋转/静区和补齐开关均来自冻结适配器快照；Host 不再写入与声明无关的固定值。请求配置先经注册项 validator，再创建同一配置解析的适配器。新协议实际物理副本在编码前按 `maxPages` 校验，`PageEnd` 按副本执行；响应页面归属保留 source/copy 语义。

新增 `raster-element-png`、`raster-page-png` 特性。能力响应新增 `rasterPreview`：`softwareBitmap`、`elementPng`、`elementTypes`、`pagePng`、`pagePngReason`、`dpiX`、`dpiY`。`elementPng` 复用真实编码器与光栅路径；ESC/POS Raster 覆盖文本、1D、QR、DataMatrix/PDF417、GS1 Composite、图片、形状和边框，其他协议覆盖边框、QR、二维点阵、Composite、图片和基础形状，不宣称原生文字、Native HRI 或 1D 宽条/旋转语义。元素选择通过布局元素与绘制 primitive 的 ownership 映射取齐全部绘制指令，不按裸索引或仅 `sourcePath` 关联。`pagePng` 只对 ESC/POS Raster 为 `true`，其他协议返回 `false` 和明确原因，不输出 DOM 截图或假完整页。请求可带 `pageIndex`、`elementIndex`、`sourcePath`；`sourcePath` 必须匹配。PNG 按实际 DPI 写入 `pHYs`；raster 快照、RGBA、PNG、ZIP 均计入 Host 资源作用域，取消沿用现有请求中止语义。旧 Host 缺少特性时设计器保持旧几何预览并禁用下载。真机打印、设备字体和固件专属语义仍不在软件位图承诺内，详见[FNC1 与预览导出](fnc1-preview-png.md)。

新增 `preview-bundle` 特性。请求沿用 `/preview` 的模板、数据、资源和输出配置，并可带当前页 `pageIndex`（省略为 0）和可选 `imageTypes`（元素类型数组；省略表示支持的全部类型，空数组表示不生成图片）。每次请求只调用一次 `RenderWithLayoutAndPrimitives`，复用该次布局和 primitive ownership。ZIP 固定包含 `manifest.json` 与 `images/0001.png` 等图片条目：`manifest.layout` 含 `pageIndex`、`pageCount` 和与 `/preview` 同形的 `pages` 布局数组；`manifest.images` 列出 `key`、`file`、`pageIndex`、`elementIndex`、`type`、`sourcePath`、`width` 和 `height`；`manifest.diagnostics` 记录单元素码图失败（含 `code`、`phase: PreviewImage`、页面和元素定位）。`key` 与设计器 `previewElementKey` 一致。普通单元素失败不会删除布局或其他图片；取消、整体渲染失败和资源预算超限在写出完整响应前失败，不返回半个 ZIP。ZIP、PNG、RGBA、布局清单、manifest 元数据和序列化缓冲均计入请求工作预算。`pageIndex` 超出页范围返回 `InvalidPageIndex`；`imageTypes` 包含非 0–11、重复项或超过 12 项返回 `InvalidImageTypes`。`/preview` 和 `/element-png` 保持原契约；旧 Host 缺少 `preview-bundle` 时设计器回退到旧的预览加元素 PNG 链路。设计器切换页面时只请求目标页图片。

新增 `gs1-two-dimensional`、`gs1-association-validation` 特性。能力响应新增 `gs1TwoDimensional: { qr, dataMatrix, native, associationValidation }`；Host 请求新增可选 `validateGs1Associations`。Type 3 QR / Type 10 DataMatrix 的 `Gs1=true` 使用共享 GS1 编码并走图片协议，Native 和旧 Host 明确拒绝或禁用；普通模式不要求新特性。详见[GS1 关联与二维条码](gs1-associations-2d.md)。

ExpandedStacked 的显式列数（包括显式 2）要求 `gs1-databar-expanded-stacked-columns`。能力响应仅在支持的普通 ESC/POS Raster Type 31 下提供顶层 `expandedStackedColumns: { minimum: 1, maximum: 11, default: 2 }`；Composite 通过 `gs1Composite.expandedStackedColumns: { minimum: 2, maximum: 11, default: 2 }` 声明。省略或 `null` 表示默认布局，不要求新特性；显式最大行数要求 `gs1-databar-expanded-stacked-rows`，对应顶层 `expandedStackedRows` / `gs1Composite.expandedStackedRows` 范围为 2–11，默认 `null`。旧 Host 的显式列数或行数会被设计器保留并明确拦截。模板属性名使用 `ExpandedStackedColumns` / `ExpandedStackedRows`；设计器拒绝其他大小写别名和重复声明，避免字段在旧 Host 请求中丢失。

能力响应中的 `supportedNativeBarcodeHumanReadablePositions` 只描述 ESC/POS Native 的设备可读文字位置。字段可缺省或为 `null`；提供数组时（包括空数组）只允许出现在 ESC/POS Native 能力中，数组成员仍须是已知位置且不能重复。Host 的现有响应结构不变；设计器兼容字段缺省和 `null`，其他协议不得携带该数组。

GS1 Composite 是 Type 11 模板元素，仅支持 V2/V3。能力响应的 `gs1Composite.linearTypes` 声明可用线性组件，`modes` 声明可用二维模式：EAN-8/EAN-13/UPC-A/UPC-E 与 DataBar Omnidirectional/Truncated/Limited、单行 Expanded、Stacked、StackedOmnidirectional 和 ExpandedStacked 支持 Auto/CC-A/CC-B，GS1-128 (`UccEan128`) 支持 Auto/CC-A/CC-B/CC-C。EAN/UPC 主数据可选带一个严格的 2/5 位附加码；Expanded 主数据和次数据都必须是括号式 GS1 AI；Stacked 主数据使用 GTIN，次数据使用固定版本 GS1 Syntax Dictionary 的完整 536 个已定义 AI 规则；跨 AI 的 req/ex 业务关联仍由业务层负责。

Truncated 与 Stacked Omnidirectional 分别还要求 `gs1-composite-truncated`、`gs1-composite-stacked-omnidirectional`。基础 DataBar Composite 使用 `gs1-composite`；GS1-128 Auto/CC-C 还要求 `gs1-composite-cc-c`，显式 GS1-128 CC-A/CC-B 还要求 `gs1-composite-gs1-128-ab`，并且 `gs1Composite.linearTypes`、`modes` 必须分别包含 `UccEan128`、`CcA` 和 `CcB`；EAN/UPC 组合还要求 `gs1-composite-ean-upc`，以及 `Ean8/Ean13/UpcA/UpcE` 与 Auto/CcA/CcB 能力；EAN/UPC 附加码另要求 `gs1-composite-ean-upc-addons`；Expanded 和 Stacked 分别还要求 `gs1-composite-expanded` 和 `gs1-composite-stacked`。能力响应的 `modes` 可声明 `Auto`、`CcA`、`CcB`、`CcC`，但数组本身不表达组合支持，显式 GS1-128 A/B 还必须协商新增特性。新 Host 的 `gs1Composite.linearTypes` 在现有 Omnidirectional、Limited、UccEan128、Expanded、Stacked 项后追加 `Gs1DataBarExpandedStacked`，再追加 `Ean8`、`Ean13`、`UpcA`、`UpcE`，本阶段末尾追加 `Gs1DataBarTruncated`、`Gs1DataBarStackedOmnidirectional`，共 12 项；`modes` 和 `secondaryFormat` 保持不变，`primaryFormats` 在 `ean-upc-digits` 后追加 `ean-upc-digits-with-addon`。固定 `Field.Value` 附加码由设计器发送独立必需特性并校验 Host 响应；动态 `Field.Name` 在 Host 完成绑定后严格校验，设计器不提前推断数据值；旧 Host 的纯数字预检拒绝动态带加号的值，并保留纯数字输入。旧 Host 缺少对应特性或线性/模式能力时，设计器禁用相应选项并提示，服务端也会拒绝强制提交的请求；项目导入保留 A/B 模式，不静默改写，preview/render 请求携带必需特性并验证响应以防降级。旧能力响应中已声明的 Auto/CC-C、DataBar 和 Expanded 组合仍按原契约使用。完整示例见[GS1-128 CC-A/CC-B 使用说明](gs1-composite-gs1-128-ab.md)、[Expanded 使用说明](gs1-composite-expanded.md)、[Stacked 使用说明](gs1-composite-stacked.md)与[EAN/UPC 附加码使用说明](gs1-composite-ean-upc-addons.md)。非法线性制式/模式配对在导入验证和服务端预检时拒绝。

Stacked 使用两列 CC-A/CC-B；线性 DataBar 宽 50 模块，完整 Composite 固定宽 56 模块（55 模块二维组件加 1 模块横向对齐，不含显式静区），模式和次数据只改变高度。条高按上下数据行的总高计算，且至少为模块宽的 12 倍；它不是 ExpandedStacked。四协议 Raster 通过主机图形路径输出，ESC/POS Native 不支持；Composite 不内置 HRI。画布在新 Host 下显示 Host 返回的真实码图 PNG，旧 Host 保持选择/布局占位；元素 PNG 和 ESC/POS Raster 页面 PNG 可下载，其他协议不提供假完整页。2026-10-22 的既有 DataBar 浏览器验收和完整 Chromium E2E 154/154 发生在 CC-C 集成前，后续 CC-C 的完整 Chromium 回归为 156/156，见[CC-C 阶段报告](gs1-composite-cc-c-verification-2026-10-22.md)；本轮 Stacked 验收见[Stacked 阶段报告](gs1-stacked-verification-2026-10-22.md)。这些软件结果不代表目标固件实机扫描已验收。命令见[开发与验证](development.md)和[修复验收报告](review-fixes-2026-10-22.md)。

## 关键文件

- `tools/DesignerHost/Program.cs`：路由和 Kestrel 请求体上限。
- `tools/DesignerHost/DesignerRequestProcessor.cs`：严格 JSON DTO、选项映射、能力、预览和输出。
- `tools/DesignerHost/BudgetedHostStream.cs`：输入/输出流的工作内存计量。
- `tools/DesignerHost/DesignerRequestAdmission.cs`：服务器总额度、并发和等待队列。
- `web/Bing.EasyPrint.Designer/src/App.vue`：画布、属性、数据、协议和校准编辑。
- `web/Bing.EasyPrint.Designer/src/api.ts`：请求、取消、契约与能力校验。
- `web/Bing.EasyPrint.Designer/src/archive.ts`：模板包、资源和输出 ZIP。

## 配置

| 环境变量 | 作用 |
| --- | --- |
| `EASYPRINT_DESIGNER_API_URL` | Host 监听地址，默认 `http://127.0.0.1:5187` |
| `EASYPRINT_DESIGNER_MAX_WORKING_BYTES` | 单请求服务器工作内存上限 |
| `EASYPRINT_DESIGNER_MAX_TOTAL_WORKING_BYTES` | 所有活动请求的总工作内存上限 |
| `EASYPRINT_DESIGNER_MAX_CONCURRENT_REQUESTS` | 最大并发请求数 |
| `EASYPRINT_DESIGNER_MAX_QUEUED_REQUESTS` | 最大等待请求数 |

HTTP 请求体上限由 `DesignerRequestProcessor.MaxRequestBytes` 固定为 44 MiB。环境变量解析失败会在启动时明确报错；达到并发或额度上限时返回 503。

资源数量、字节上限和 HTTP 错误映射见[资源限制与错误处理](resource-limits.md)。

## 依赖与限制

- Host 使用 .NET 10 和 Templates；Web 使用 Vue 3、Vite、TypeScript、Konva、JSZip 和 Lucide Vue。
- 反向代理必须透传契约请求头和响应头；跨域部署由部署方配置 CORS/网关。
- 预览坐标不能替代真实设备的字体、DPI、走纸和切刀校准。
- 导出的页面或输出段需要调用方按顺序发送；设计器不提供打印队列。
- Host 使用严格 DTO，未知固定字段会被拒绝；业务 `data` 和资源键保持开放。
- Host 项目在 Web SDK 默认内容 glob 中排除项目内 `artifacts/**` 与 `output/**`；样例模板仍通过四个显式内容项复制到输出目录。
- 更完整的界面与模板包行为见[设计器详细说明](designer.md)。
