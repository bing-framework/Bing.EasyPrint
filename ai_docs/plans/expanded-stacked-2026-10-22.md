# ExpandedStacked 缺口补齐计划（2026-10-22）

本轮补齐普通 GS1 DataBar ExpandedStacked 与 Composite 的完整软件链路。保留当前未提交修改、五个目标框架和已有公共 API，不提交、推送或发布。实机验收按用户要求不纳入本轮。

## 契约与影响分析

- 追加 `BarcodeType.Gs1DataBarExpandedStacked = 31`，已有枚举值不变。
- 固定每行两个字符对（最多四个符号字符），支持 1–6 条数据行；列数自定义不在本阶段范围内。
- 普通条码通过 ESC/POS Raster 输出；Composite 通过 CPCL、ESC/POS Raster、TSPL、ZPL 输出。
- 主数据沿用 Expanded AI/容量规则；Composite 支持 Auto/CC-A/CC-B，显式拒绝 CC-C。
- 新类型的条高表示每条数据行高度（dot），至少为 1X；行间三个分隔行分别为 1X。建议条高至少 34X，库不代替应用校验行业扫码高度要求。既有类型高度语义不变。
- Host 追加 `gs1-composite-expanded-stacked` 协商特性；旧 Host 明确禁用新 Composite 类型。
- 影响面：Expanded 二进制填充和布局、尺寸/边界/旋转、资源预算、Provider 能力、模板与 Host、设计器、案例及文档。
- 复用上一阶段未受影响的 TIFF、ZPL 方向、Host 内容排除等专项证据；最终全套验收覆盖共享编码器回归。

## 清单

| 项目 | 状态 | 验收 |
| --- | --- | --- |
| E1 编码与分隔行 | CLOSED | 固定 Zint 2.15.0 向量逐模块一致，短行/反向/填充/容量边界 |
| E2 普通与 Composite 输出 | CLOSED | 尺寸、旋转、边界、取消、预算、四协议及模板往返 |
| E3 Host 与设计器 | CLOSED | 能力契约、协商、编辑/预览/导出/保存重载及旧 Host 降级 |
| E4 案例与文档 | CLOSED | C#/JSON/往返、包消费、中文 XML、README 与指南一致 |
| E5 最终验收 | CLOSED | 五框架严格 XML 构建、net6/netcoreapp3.1、Host、Vitest、构建、Chromium |

生成物和证据写入 `artifacts/expanded-stacked-20261022`；历史报告保留原结果，新结果单独记录。只针对 OPEN_ACTIONABLE 修复，遵守 GOAL_RULES 的轮次及无进展限制。

## 其他能力状态

- EAN/UPC Composite、Truncated/StackedOmnidirectional Composite、实际条码预览、完整 AI 字典及复杂字体排版仍需后续独立阶段，不以本轮完成替代。
- 设备传输、完整协议插件扩展：ACCEPTED_LIMITATION。
- 图片依赖大版本迁移：DEFERRED，保留旧框架兼容，现有告警如实记录。

## 修改与证据

- E1：新增内部 ExpandedStacked 分行/分隔实现，共享 Expanded 字符与校验编码；18 组固定 Zint 矩阵通过。[核心代码](../../src/Bing.EasyPrint/Protocols/EscPosRasterCanvas.ExpandedStacked.cs)。
- E2：接入普通 Raster 与四协议 Composite，修正新类型模板多行占位与静区尺寸，已有类型保持原语义。[专项测试](../../tests/Bing.EasyPrint.Tests/ExpandedStackedTests.cs)。
- E3：追加 Host 协商、前端类型/属性/导入导出和旧 Host 降级，普通条高编辑不再从旋转后的总占位反推。[浏览器测试](../../web/Bing.EasyPrint.Designer/e2e/expandedStacked.spec.ts)。
- E4：新增 C#/JSON 案例与包消费，更新 XML 和当前手册，保留历史计数。[使用指南](../../docs/gs1-composite-expanded-stacked.md)。
- E5：命令、首次失败及修正、最终计数统一记录于[验收报告](../../docs/expanded-stacked-verification-2026-10-22.md)，详细日志位于本阶段 artifacts 目录。
