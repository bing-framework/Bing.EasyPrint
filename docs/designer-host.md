# 设计器与 Host

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

GS1 Composite 是 Type 11 模板元素，仅支持 V2/V3。能力响应的 `gs1Composite.linearTypes` 可包含 `UccEan128`，`modes` 可包含 `CcC`；组合规则仍是 DataBar Omnidirectional/Limited × Auto/CC-A/CC-B，GS1-128 × Auto/CC-C。基础功能要求 `gs1-composite`；GS1-128/CC-C 另要求并协商 `gs1-composite-cc-c`。旧 Host 只有基础特性时，既有 DataBar Composite 可继续使用，GS1-128/CC-C 选项禁用；preview/render 对包含新组合的请求要求两个特性，缺少时返回明确的功能缺失错误。非法配对在导入验证和服务端预检时拒绝。2026-10-22 的既有 DataBar 浏览器验收和完整 Chromium E2E 154/154 发生在 CC-C 集成前，后续 CC-C 的完整 Chromium 回归为 156/156，见[CC-C 阶段报告](gs1-composite-cc-c-verification-2026-10-22.md)。画布显示选择/布局占位，不绘制可扫描的编码位图；命令及验收结论见[开发与验证](development.md)和[本轮修复验收报告](review-fixes-2026-10-22.md)。

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
