# 设计器与 Host

DataBar Truncated 与 Stacked Omnidirectional 已接入 Composite Auto/CC-A/CC-B；新增 `BarcodeType.Gs1DataBarStackedOmnidirectional=32` 支持普通 ESC/POS Raster。Truncated Composite 条高至少 13X；Stacked Omnidirectional 的条高为每条数据行高度、至少 33X，另有三个内部分隔行。完整输入、尺寸与旧 Host 协商见[使用说明](gs1-composite-databar-completion.md)。

EAN-8/EAN-13/UPC-A/UPC-E Composite 已接入四种图片协议、V2/V3 模板和设计器，支持 Auto/CC-A/CC-B；三条分隔行各为 2X，主数据为数字，旧 Host 通过 `gs1-composite-ean-upc` 协商降级。输入、尺寸和案例见[EAN/UPC Composite 使用说明](gs1-composite-ean-upc.md)。

ExpandedStacked 已支持普通 ESC/POS Raster 条码及四协议 Composite CC-A/CC-B；固定每行两个字符对，条高表示每条数据行高度，行间另加三个 1X 分隔行。Host 使用新增 `gs1-composite-expanded-stacked` 特性协商。详细契约与示例见 [ExpandedStacked 使用说明](gs1-composite-expanded-stacked.md)。

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
| POST | `/api/designer/capabilities` | 按协议、布局和 Profile 返回能力 |
| POST | `/api/designer/preview` | 返回布局与诊断信息 |
| POST | `/api/designer/render` | 返回实际协议输出 |

Host 通过 `X-EasyPrint-Contract-Version`、`X-EasyPrint-Features` 和 `X-EasyPrint-Required-Features` 协商契约。错误响应使用稳定的 HTTP 状态、`code`、`phase`、可选 `fieldPath` 和资源诊断。

能力响应中的 `supportedNativeBarcodeHumanReadablePositions` 只描述 ESC/POS Native 的设备可读文字位置。字段可缺省或为 `null`；提供数组时（包括空数组）只允许出现在 ESC/POS Native 能力中，数组成员仍须是已知位置且不能重复。Host 的现有响应结构不变；设计器兼容字段缺省和 `null`，其他协议不得携带该数组。

GS1 Composite 是 Type 11 模板元素，仅支持 V2/V3。能力响应的 `gs1Composite.linearTypes` 声明可用线性组件，`modes` 声明可用二维模式：EAN-8/EAN-13/UPC-A/UPC-E 与 DataBar Omnidirectional/Truncated/Limited、单行 Expanded、Stacked、StackedOmnidirectional 和 ExpandedStacked 支持 Auto/CC-A/CC-B，GS1-128 (`UccEan128`) 支持 Auto/CC-A/CC-B/CC-C。Expanded 主数据和次数据都必须是括号式 GS1 AI；Stacked 主数据使用 GTIN，次数据使用支持的括号式 GS1 AI 子集；业务层仍需负责 AI 之间的关系校验。

Truncated 与 Stacked Omnidirectional 分别还要求 `gs1-composite-truncated`、`gs1-composite-stacked-omnidirectional`。基础 DataBar Composite 使用 `gs1-composite`；GS1-128 Auto/CC-C 还要求 `gs1-composite-cc-c`，显式 GS1-128 CC-A/CC-B 还要求 `gs1-composite-gs1-128-ab`，并且 `gs1Composite.linearTypes`、`modes` 必须分别包含 `UccEan128`、`CcA` 和 `CcB`；EAN/UPC 组合还要求 `gs1-composite-ean-upc`，以及 `Ean8/Ean13/UpcA/UpcE` 与 Auto/CcA/CcB 能力；Expanded 和 Stacked 分别还要求 `gs1-composite-expanded` 和 `gs1-composite-stacked`。能力响应的 `modes` 可声明 `Auto`、`CcA`、`CcB`、`CcC`，但数组本身不表达组合支持，显式 GS1-128 A/B 还必须协商新增特性。新 Host 的 `gs1Composite.linearTypes` 在现有 Omnidirectional、Limited、UccEan128、Expanded、Stacked 项后追加 `Gs1DataBarExpandedStacked`，再追加 `Ean8`、`Ean13`、`UpcA`、`UpcE`，本阶段末尾追加 `Gs1DataBarTruncated`、`Gs1DataBarStackedOmnidirectional`；`modes` 和 `secondaryFormat` 声明不变，EAN/UPC 阶段在 `primaryFormats` 末尾追加 `ean-upc-digits`。旧 Host 缺少对应特性或线性/模式能力时，设计器禁用相应选项并提示，服务端也会拒绝强制提交的请求；项目导入保留 A/B 模式，不静默改写，preview/render 请求携带必需特性并验证响应以防降级。旧能力响应中已声明的 Auto/CC-C、DataBar 和 Expanded 组合仍按原契约使用。完整示例见[GS1-128 CC-A/CC-B 使用说明](gs1-composite-gs1-128-ab.md)、[Expanded 使用说明](gs1-composite-expanded.md)与[Stacked 使用说明](gs1-composite-stacked.md)。非法线性制式/模式配对在导入验证和服务端预检时拒绝。

Stacked 使用两列 CC-A/CC-B；线性 DataBar 宽 50 模块，完整 Composite 固定宽 56 模块（55 模块二维组件加 1 模块横向对齐，不含显式静区），模式和次数据只改变高度。条高按上下数据行的总高计算，且至少为模块宽的 12 倍；它不是 ExpandedStacked。四协议 Raster 通过主机图形路径输出，ESC/POS Native 不支持；Composite 不内置 HRI。画布显示选择/布局占位，不绘制可扫描的编码位图，Host 在线预览返回实际编码尺寸。2026-10-22 的既有 DataBar 浏览器验收和完整 Chromium E2E 154/154 发生在 CC-C 集成前，后续 CC-C 的完整 Chromium 回归为 156/156，见[CC-C 阶段报告](gs1-composite-cc-c-verification-2026-10-22.md)；本轮 Stacked 验收见[Stacked 阶段报告](gs1-stacked-verification-2026-10-22.md)。这些软件结果不代表目标固件实机扫描已验收。命令见[开发与验证](development.md)和[修复验收报告](review-fixes-2026-10-22.md)。

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

- Host 使用 .NET 8 和 Templates；Web 使用 Vue 3、Vite、TypeScript、Konva、JSZip 和 Lucide Vue。
- 反向代理必须透传契约请求头和响应头；跨域部署由部署方配置 CORS/网关。
- 预览坐标不能替代真实设备的字体、DPI、走纸和切刀校准。
- 导出的页面或输出段需要调用方按顺序发送；设计器不提供打印队列。
- Host 使用严格 DTO，未知固定字段会被拒绝；业务 `data` 和资源键保持开放。
- Host 项目在 Web SDK 默认内容 glob 中排除项目内 `artifacts/**` 与 `output/**`；样例模板仍通过四个显式内容项复制到输出目录。
- 更完整的界面与模板包行为见[设计器详细说明](designer.md)。
