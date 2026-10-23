# ExpandedStacked 最大行数与完整 GS1 AI 字典实施计划（2026-10-23）

基线为当前工作区 `f69ce66` 加本轮开始时已有的列数/附加码阶段内容。保留五目标框架、现有公共签名、默认两列字节输出和 Backward-compatible 增量 API；不提交、不发布、不改版本号、不更新 API baseline，实机验收按用户要求排除。

## 权威规则与语义

- ExpandedStacked 最大行数采用 Zint 2.15.0（提交 `4896136bdbebb1a0908a5cc419513efa5bf4bf70`）`option_3` 语义：范围 2–11，限制**数据行**数量，行间三条分隔行不计入；默认两列，最大行数只能迫使列数增加，不能使布局低于两列。与显式列数互斥，不静默择一。
- 无法在 11 列内满足行数上限、或实际数据行仍超过上限时，编码前明确失败，不截断、不丢字段。
- 完整 AI 字典固定为 GS1 Barcode Syntax Dictionary 提交 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6`（2025-02-09，Apache-2.0），与 Zint 2.15.0 的 `gs1_lint.h` 生成源一致；展开后 536 个已定义 AI。实现通用 AI 语法与逐字段 Linter，不把跨 AI `req`/`ex` 业务关联冒充为已实现语义。

## Change Impact Analysis

- ChangedFiles/Projects：Core `EscPosRasterCanvas`、`Gs1CompositeEncoder`、统一模型、Provider/静区；Templates 模型/加载/渲染；DesignerHost 能力；Designer 前端；测试、样例、文档、第三方声明。
- ChangedPublicContracts：新增可选 `ExpandedStackedRows` / `WithExpandedStackedRows`、按行测量/绘制/命令入口、Host 特性 `gs1-databar-expanded-stacked-rows`；旧签名、默认两列和原有 AI 规范化字节保持。
- ChangedRuntimePaths：ExpandedStacked 列数解析、行布局、尺寸测量、Composite 图片输出、模板 JSON、Host 能力、设计器编辑/导入导出/旧 Host 降级、括号 AI 解析与逐字段校验。
- ChangedProviders：普通仅 ESC/POS Raster；Composite 影响 CPCL、ESC/POS Raster、TSPL、ZPL。
- ChangedTFMs/BuildPackaging/BenchmarkHarness：无；新增嵌入资源 `gs1-syntax-dictionary-<commit>.txt`。
- AffectedDependents：模板、Host、设计器、候选包消费、文档示例。RiskLevel：HIGH。

## 清单

| 项目 | 状态 | 验收 |
| --- | --- | --- |
| R1 行数核心 | CLOSED | Zint 60 组独立行数矩阵、互斥/边界/失败原子性、默认兼容 |
| R2 跨层传播 | CLOSED | 模型/Provider/静区/模板/Host/设计器、旧 Host 降级、四协议旋转静区 |
| R3 完整 AI 字典 | CLOSED | 固定提交与 SHA-256、536 AI 覆盖、逐字段正反例、FNC1/混合 AI/未知 AI/跨码制 |
| R4 案例/文档 | CLOSED | C#/JSON/往返、包消费、中文 XML、使用/开发/Host/设计器文档 |
| R5 候选验收 | CLOSED | L0/L1、受影响项目、五框架 XML、双运行时、Host、Vitest/构建/Chromium、包消费 |

## 验证策略

先 L0/L1，再受影响项目；避免与运行中的 Host 并行写 .NET 构建目录。最终候选执行五框架严格 XML、`Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1`、Host 默认脚本、设计器类型检查/生产构建/Vitest/Chromium 零重试、模板案例和本地 net6 候选包消费。历史证据可复用；本轮变更范围之外的证据不无故重跑。

NOT_APPLICABLE：实机验收、新增配置的历史性能对比。ACCEPTED_LIMITATION：设备传输/驱动、完整 Provider 工厂插件。DEFERRED：真实码图预览、复杂字体排版、图片依赖大版本迁移。

## 当前边界

- 跨 AI `req`/`ex` 关联属性仅保留追溯，除既有明确规则外不做全量业务推断。
- 具体码制容量、扫描语义、GS1 Digital Link URI/EPCIS 不在本轮。
- 字典更新若引入新 Linter，必须实现后重新生成/加载；加载器拒绝未实现 Linter。

R1–R5 已实现并验收：Zint 60 组独立行数矩阵；完整字典 536 AI；核心 net6.0/netcoreapp3.1 各 3997、Host 220、Vitest 408、Chromium 183（零重试）、五框架 XML 10/10、模板案例和 net6 候选包消费通过。首次失败及修正见[阶段报告](../../docs/expanded-stacked-rows-gs1-ai-verification-2026-10-23.md)，原始汇总见 `artifacts/expanded-stacked-rows-20261023/verification.json`。本轮 OPEN_ACTIONABLE=0，结束本阶段实施。
