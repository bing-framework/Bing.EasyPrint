# 资源限制与错误处理

## 功能说明

项目在 Core、模板和 DesignerHost 三层限制输入、图片、页面、输出及工作内存估计，避免未受控内容导致无界分配。工作内存额度是显式预留估计，不是进程实际峰值内存。

## Core 资源作用域

`PrintResourceScope.Begin(maxBytes, cancellationToken)` 在当前异步上下文建立可嵌套预算。Core 的命令、Provider、任务渲染和图片路径会在已接入位置预留资源；没有活动作用域时 `Reserve`/`Retain` 不记账。

```csharp
using Bing.EasyPrint.Printing;

using var scope = PrintResourceScope.Begin(32L * 1024 * 1024, cancellationToken);
var pages = provider.Render(document, cancellationToken);
Console.WriteLine(scope.PeakBytes);
```

- `LimitTo` 只能收紧当前限制。
- 作用域必须按 LIFO 顺序释放，活动子作用域必须先结束。
- 超限抛出 `PrintResourceLimitException`，提供资源名、请求量、当前量和上限。
- 取消令牌在预留和渲染过程中保持原取消语义。

关键文件：`src/Bing.EasyPrint/Printing/PrintResourceScope.cs` 及 Core 中的预算缓冲区、集合和图片辅助类。

## 模板限制

`TemplateRenderOptions` 提供以下主要限制：

| 属性 | 默认值 | 作用 |
| --- | ---: | --- |
| `MaxJsonBytes` | 1 MiB | 模板 JSON 大小 |
| `MaxDepth` | 16 | 模板结构深度，最大可配置为 128 |
| `MaxDetailRows` | 10000 | 单次渲染的明细行数 |
| `MaxPages` | 100 | 实际输出页数，包含任务份数 |
| `MaxOutputBytes` | 32 MiB | 最终协议输出总量 |
| `MaxWorkingBytes` | 未设置 | 单次渲染的显式工作内存估计 |
| `MaxImageBytes` | 10 MiB | 单张图片编码数据 |
| `MaxImagePixels` | 1600 万 | 单张图片解码像素数 |
| `MaxPageHeight` | 4096 点 | 单页高度 |

`MaxOutputBytes` 和 `MaxWorkingBytes` 解决不同问题，不能互相替代。图片阈值 `ImageThreshold` 的合法范围是 0–255。

## DesignerHost 限制与准入

Host 的固定请求体上限为 44 MiB；业务 `data` JSON 上限为 1 MiB，普通资源最多 64 个、合计最多 32 MiB。固定 Raster 字体还有独立数量和总字节限制。

以下环境变量控制服务器额度：

| 环境变量 | 作用 |
| --- | --- |
| `EASYPRINT_DESIGNER_MAX_WORKING_BYTES` | 单请求工作内存上限 |
| `EASYPRINT_DESIGNER_MAX_TOTAL_WORKING_BYTES` | 所有活动请求的总预留上限 |
| `EASYPRINT_DESIGNER_MAX_CONCURRENT_REQUESTS` | 最大活动请求数 |
| `EASYPRINT_DESIGNER_MAX_QUEUED_REQUESTS` | 最大等待请求数 |

额度或并发不足且无法排队时返回 HTTP 503；请求或资源过大返回 413；媒体类型错误返回 415；输入、模板、绑定或资源内容错误返回 400；契约协商失败返回 409。500 响应不暴露内部异常和文件路径。

关键文件：

- `tools/DesignerHost/DesignerRequestExecution.cs`
- `tools/DesignerHost/DesignerRequestAdmission.cs`
- `tools/DesignerHost/DesignerRequestProcessor.cs`
- `tools/DesignerHost/BudgetedHostStream.cs`
- `tools/DesignerHost/DesignerError.cs`

## 已知限制与注意事项

- 预留估计只覆盖代码已接入的缓冲区、集合和图片，不等于 GC 堆、进程工作集或设备缓存。
- 没有活动 `PrintResourceScope` 且模板未设置 `MaxWorkingBytes` 时，不增加默认累计工作内存限制；协议自身和模板的页数、图片、输出限制仍生效。
- Host 的服务器总额度需要结合部署实例内存和并发压测配置，仓库不提供通用生产值。
- 客户端取消或传输中断不能保证已经发送到打印机的字节被撤回。
