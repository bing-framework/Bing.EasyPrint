# 文档与代码对照清单

盘点日期：2026-10-13。范围包括根目录、`docs`、`ai_docs/plans`、`src`、`tools`、`web`、`samples` 和 `tests`；构建产物、依赖目录和临时目录不计入项目文档。

## 对照结果

| 功能或资料 | 代码状态 | 文档状态 | 依据与处理 |
| --- | --- | --- | --- |
| CPCL 兼容命令和组件 | 已实现 | 完整 | `src/Bing.EasyPrint/CPCL`；见 [核心打印与协议](core-printing.md) 和 [协议详细说明](protocols.md) |
| ESC/POS Native/Raster | 已实现 | 完整 | `Protocols/EscPosPrintCommand.cs`、`EscPosRasterCanvas.cs`；已记录布局差异和 Profile |
| TSPL、ZPL 命令 | 已实现 | 完整 | `Protocols/TsplPrintCommand.cs`、`ZplPrintCommand.cs`；已记录介质、字体和切刀边界 |
| 四协议受限 Raw | 已实现 | 完整 | `IRestrictedRawPrintCommand.cs` 及四个 `*.RestrictedRaw.cs`；已记录可信/受限入口差异 |
| 统一打印模型与 Provider | 已实现 | 完整 | `Printing/PrintDocument.cs`、`PrintProtocolProviders.cs`；已记录页面、元素、预检和渲染 |
| 能力查询和设备 Profile | 已实现 | 完整 | `PrintCapabilities.cs`、`Protocols/*Profile.cs`；能力不等同于实机证明 |
| 打印任务和输出段 | 已实现 | 完整 | `PrintJobRenderer.cs`、`PrintPageOutput.cs`；已记录多份、收尾与独立段 |
| 版本化模板与价签 | 已实现 | 完整 | `src/Bing.EasyPrint.Templates`；见 [模板渲染指南](templates-guide.md) |
| Vue 设计器和 DesignerHost | 已实现 | 完整 | `web/Bing.EasyPrint.Designer`、`tools/DesignerHost`；见 [设计器与 Host](designer-host.md) |
| `IEasyPrint`、工厂和验证接口 | 已实现 | 原先缺少集中说明，已补齐 | 新增 [公开 API 入口](public-api.md) |
| Core/模板/Host 资源预算 | 已实现 | 原先分散，已补齐 | 新增 [资源限制与错误处理](resource-limits.md) |
| 构建、测试、示例 | 已实现 | 已更新 | [开发与验证](development.md) 与 `samples/Templates` |
| 根目录 README | 已实现入口 | 原文件过时且为历史编码，已更新 | 已改为 UTF-8 中文入口并链接 `docs` |
| 英文文档 | 未发现独立英文文档 | 无需单独翻译 | 根 README 的英文标题和旧英文模板入口已统一改为中文；协议/API 标识保留原名 |
| 历史计划与验证记录 | 历史资料 | 已标明时效 | `ai_docs/plans`、`multi-protocol-verification.md` 和早期报告不作为当前能力依据 |
| 打印机连接、驱动、队列、状态 | 未实现 | 已标注 | 见 [待实现与待确认](status.md) |
| 独立 ESC/P、PCL 或私有协议 Provider | 未实现 | 已标注 | 仅 ESC/POS Raster 提供 `ESC *` 位图路径，不能替代独立协议 |
| 真实设备校准与验收 | 无法由代码确认 | 待确认 | 字体、DPI、HRI、走纸、切刀和扫描结果需按型号实测 |

## 文档分层

- 当前入口：`README.md`、`docs/README.md`。
- 模块指南：架构、公开 API、核心打印、模板、设计器/Host、资源限制、开发验证和状态清单。
- 详细参考：`protocols.md`、`templates.md`、`designer.md`。
- 历史证据：`multi-protocol-verification.md`、`implementation-report.md`、`component-validation-benchmark.md` 和 `ai_docs/plans`。

## 核对原则

- 当前行为以源码和测试为准，历史测试数量不外推到当前分支。
- 模板文档的最高轮次低于协议文档不等于模板缺失；直接命令能力与模板格式分别记录。
- Profile 和能力快照只描述调用方声明及本地预检，不证明目标固件兼容。
- 文档中的示例只演示字节生成，不包含设备连接和发送。
