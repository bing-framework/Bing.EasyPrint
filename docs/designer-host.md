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

## HTTP 接口

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/designer/health` | 健康状态与契约描述 |
| GET | `/api/designer/samples` | 内置模板样例 |
| POST | `/api/designer/capabilities` | 按协议、布局和 Profile 返回能力 |
| POST | `/api/designer/preview` | 返回布局与诊断信息 |
| POST | `/api/designer/render` | 返回实际协议输出 |

Host 通过 `X-EasyPrint-Contract-Version`、`X-EasyPrint-Features` 和 `X-EasyPrint-Required-Features` 协商契约。错误响应使用稳定的 HTTP 状态、`code`、`phase`、可选 `fieldPath` 和资源诊断。

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
- 更完整的界面与模板包行为见[设计器详细说明](designer.md)。
