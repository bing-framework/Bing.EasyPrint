# Bing.EasyPrint 文档索引

- [Truncated / Stacked Omnidirectional 使用说明](gs1-composite-databar-completion.md)
- [DataBar 补齐阶段验收](databar-completion-verification-2026-10-22.md)

- [EAN/UPC Composite 使用说明](gs1-composite-ean-upc.md)
- [EAN/UPC Composite 附加码使用说明](gs1-composite-ean-upc-addons.md)
- [EAN/UPC Composite 阶段验收](ean-upc-composite-verification-2026-10-22.md)
- [EAN/UPC Composite 附加码阶段验收](ean-upc-addons-verification-2026-10-22.md)
- [ExpandedStacked 列数配置与 Host 协商](expanded-stacked-columns.md)
- [ExpandedStacked 最大行数与完整 GS1 AI 字典](expanded-stacked-rows-and-gs1-ai.md)
- [ExpandedStacked 列数配置阶段验收](expanded-stacked-columns-verification-2026-10-23.md)

本目录是项目当前功能的开发者入口。代码是最终依据；带 `Rxx` 标题的文档记录历史迭代与验证证据，不应替代当前模块说明。

## 盘点结果

| 功能模块 | 当前状态 | 代码或文档依据 |
| --- | --- | --- |
| CPCL 兼容 API | 已实现 | `src/Bing.EasyPrint/CPCL` |
| ESC/POS Native/Raster、TSPL、ZPL 命令 | 已实现 | `src/Bing.EasyPrint/Protocols` |
| 受限 Raw 语法、状态与几何校验 | 已实现 | `IRestrictedRawPrintCommand.cs`、各协议 Restricted Raw 解析器 |
| 统一页面模型、Provider、能力与任务输出 | 已实现 | `src/Bing.EasyPrint/Printing`、`PrintCapabilities.cs` |
| V1/V2/V3 模板、分页、绑定、资源与价签 | 已实现 | `src/Bing.EasyPrint.Templates` |
| Vue 设计器、Host 能力/预览/渲染接口 | 已实现 | `web/Bing.EasyPrint.Designer`、`tools/DesignerHost` |
| 命令工厂、Provider 和校验 API | 已实现 | `IEasyPrint`、`ProtocolCommandFactory`、`PrintProtocolProviderFactory` |
| Core、模板和 Host 资源限制 | 已实现 | `PrintResourceScope`、`TemplateRenderOptions`、Host 准入控制 |
| 设备连接、驱动、队列与状态查询 | 待实现 | 当前无传输层；见 [状态清单](status.md) |
| 实机字体、DPI、走纸、裁切与扫描验收 | 待确认 | 需要目标型号和固件；见 [状态清单](status.md) |

## 当前模块文档

| 文档 | 适用场景 | 主要代码入口 |
| --- | --- | --- |
| [架构与模块](architecture.md) | 了解项目边界、数据流和模块依赖 | `src/`、`tools/`、`web/` |
| [公开 API 入口](public-api.md) | 选择命令、Provider、模板与验证 API | `IEasyPrint`、工厂和结果类型 |
| [核心打印与协议](core-printing.md) | 直接生成 CPCL、ESC/POS、TSPL、ZPL 字节，或使用统一打印模型 | `src/Bing.EasyPrint` |
| [模板渲染指南](templates-guide.md) | 加载 V1/V2/V3 模板、绑定数据、分页和生成多协议输出 | `src/Bing.EasyPrint.Templates` |
| [设计器与 Host](designer-host.md) | 运行浏览器设计器、调用预览/导出 API、配置资源限制 | `web/Bing.EasyPrint.Designer`、`tools/DesignerHost` |
| [资源限制与错误处理](resource-limits.md) | 配置 Core、模板与 Host 的资源边界 | `PrintResourceScope`、模板限制和 Host 准入 |
| [开发与验证](development.md) | 构建、测试、示例和依赖说明 | `tests/`、`samples/`、`tools/PerformanceProbe` |
| [待实现与待确认](status.md) | 查看尚未落地或必须由设备验证的事项 | 全项目 |
| [文档与代码对照清单](documentation-audit.md) | 查看本次盘点结论和文档时效 | 全项目 |

## 详细参考

- [协议详细说明](protocols.md)：协议选项、Profile、条码、裁切、Raw 校验和设备边界的详细记录。
- [模板详细说明](templates.md)：模板版本、元素、布局、字体校准和历史兼容细节。
- [ExpandedStacked 普通条码与 Composite](gs1-composite-expanded-stacked.md)：输入、每行高度、A/B 与基础能力协商。
- [ExpandedStacked 列数配置](expanded-stacked-columns.md)：默认列数、普通/Composite 范围、模板字段和新 Host 协商。
- [ExpandedStacked 最大行数与 GS1 AI 字典](expanded-stacked-rows-and-gs1-ai.md)：行数互斥语义、固定字典提交、逐字段校验和更新方法。
- [ExpandedStacked 阶段验收](expanded-stacked-verification-2026-10-22.md)：功能矩阵、运行时及浏览器验证记录。
- [GS1-128 CC-A/CC-B Composite](gs1-composite-gs1-128-ab.md)：显式 A/B、兼容 Auto/CC-C 与旧 Host 协商。
- [单行 Expanded Composite](gs1-composite-expanded.md)：Expanded + CC-A/CC-B 的输入、尺寸、模板与能力协商。
- [Stacked Composite](gs1-composite-stacked.md)：DataBar Stacked + CC-A/CC-B 的输入、尺寸、模板与 Host 能力协商。
- [设计器详细说明](designer.md)：设计器交互、项目文件、模板包与 Host 契约细节。

## 验证记录

- [GS1-128 A/B 阶段验收](gs1-128-ab-verification-2026-10-22.md)：本阶段最新自动化验收记录。
- [ExpandedStacked 最大行数与完整 AI 字典验收](expanded-stacked-rows-gs1-ai-verification-2026-10-23.md)：本轮能力、矩阵与原始证据。
- [多协议验证记录](multi-protocol-verification.md)：按迭代轮次保存的测试范围和已知环境问题。
- [实现报告](implementation-report.md)：早期实现阶段的总结，不代表当前完整能力清单。
- [组件校验基准](component-validation-benchmark.md)：特定提交与测试集的冻结基准。

## 使用边界

项目只生成打印协议字节，不负责端口、USB/串口/网络连接、驱动、设备发现、队列重试或打印状态查询。协议输出必须使用目标打印机手册和实机样张校准；详见[待实现与待确认](status.md)。
