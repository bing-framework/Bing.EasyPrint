# Bing.EasyPrint 文档索引

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
| 设备连接、驱动、队列与状态查询 | 待实现 | 当前无传输层；见 [状态清单](status.md) |
| 实机字体、DPI、走纸、裁切与扫描验收 | 待确认 | 需要目标型号和固件；见 [状态清单](status.md) |

## 当前模块文档

| 文档 | 适用场景 | 主要代码入口 |
| --- | --- | --- |
| [架构与模块](architecture.md) | 了解项目边界、数据流和模块依赖 | `src/`、`tools/`、`web/` |
| [核心打印与协议](core-printing.md) | 直接生成 CPCL、ESC/POS、TSPL、ZPL 字节，或使用统一打印模型 | `src/Bing.EasyPrint` |
| [模板渲染指南](templates-guide.md) | 加载 V1/V2/V3 模板、绑定数据、分页和生成多协议输出 | `src/Bing.EasyPrint.Templates` |
| [设计器与 Host](designer-host.md) | 运行浏览器设计器、调用预览/导出 API、配置资源限制 | `web/Bing.EasyPrint.Designer`、`tools/DesignerHost` |
| [开发与验证](development.md) | 构建、测试、示例和依赖说明 | `tests/`、`samples/`、`tools/PerformanceProbe` |
| [待实现与待确认](status.md) | 查看尚未落地或必须由设备验证的事项 | 全项目 |

## 详细参考

- [协议详细说明](protocols.md)：协议选项、Profile、条码、裁切、Raw 校验和设备边界的详细记录。
- [模板详细说明](templates.md)：模板版本、元素、布局、字体校准和历史兼容细节。
- [设计器详细说明](designer.md)：设计器交互、项目文件、模板包与 Host 契约细节。

## 历史证据

- [多协议验证记录](multi-protocol-verification.md)：按迭代轮次保存的测试范围和已知环境问题。
- [实现报告](implementation-report.md)：早期实现阶段的总结，不代表当前完整能力清单。
- [组件校验基准](component-validation-benchmark.md)：特定提交与测试集的冻结基准。

## 使用边界

项目只生成打印协议字节，不负责端口、USB/串口/网络连接、驱动、设备发现、队列重试或打印状态查询。协议输出必须使用目标打印机手册和实机样张校准；详见[待实现与待确认](status.md)。
