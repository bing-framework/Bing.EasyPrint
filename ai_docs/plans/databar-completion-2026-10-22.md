# DataBar 剩余 Composite 能力实施计划（2026-10-22）

继续补齐 Truncated 与 Stacked Omnidirectional。本轮保留现有未提交修改、五目标框架和现有公共签名。不提交、推送或发布；实机验收按用户要求排除。

## 契约与影响分析

- 既有 Truncated=26 接入 Composite；追加 Gs1DataBarStackedOmnidirectional=32，不改变 0–31。
- 两者 Composite 均支持 Auto/CC-A/CC-B，CC-C 明确拒绝；主数据沿用 GTIN 13 位 payload / 有效 14 位校验串 / 括号 AI01，次数据沿用严格 AI 子集。
- Truncated Composite 宽100X，数据条高H>=13X，总高H+(2R+1)X（R为二维行数）。
- Stacked Omnidirectional 为两条等高数据行，每行H>=33X，中间三个1X分隔。普通Raster宽50X、高2H+3X；Composite宽56X、高2H+(2R+4)X，增加一条外部分隔。
- 普通新32仅ESC/POS Raster；Composite均由CPCL/ESC/POS Raster/TSPL/ZPL图片路径输出；Native明确拒绝。
- Host 新增 gs1-composite-truncated、gs1-composite-stacked-omnidirectional；能力数组追加两类型，旧Host禁用并提示。新类型条高编辑必须按每数据行计算。
- ChangedProjects：Core、Templates、Host、Designer、Tests、Samples、Docs。ChangedPublicContracts：追加枚举/扩大组合支持，签名及HTTP字段不变。ChangedRuntimePaths：分隔/布局/尺寸/静区/预算/取消/能力协商。ChangedProviders：四协议共享图片路径及Raster普通条码。ChangedTFMs/BuildPackaging/BenchmarkHarness：无。RiskLevel：HIGH。

## 验收清单

| 项目 | 状态 | 验收条件 |
| --- | --- | --- |
| D1 核心编码 | CLOSED | 固定Zint向量逐模块一致，finder=3特例、连接位和Truncated同形码图 |
| D2 输出与模板 | CLOSED | 行高、静区、旋转、HRI、精确边界、取消/预算/失败恢复、V2/V3往返 |
| D3 Host与前端 | CLOSED | 类型/模式能力、编辑预览导出保存重载、旧Host降级 |
| D4 案例与文档 | CLOSED | C#/JSON/往返、包消费、中文XML、当前手册/README |
| D5 最终验证 | CLOSED | 五框架严格XML、双运行时Core、Host、Vitest/构建/Chromium零重试及静态检查 |

证据：artifacts/databar-completion-20261022。先L0/L1，再阶段L2/L3/L4，不运行无关L5性能矩阵。主代理负责架构/编码/验收，luna_worker仅拥有前端。遵循GOAL_RULES，仅OPEN_ACTIONABLE允许继续修复。

NOT_APPLICABLE：实机验收及新能力历史性能对比。ACCEPTED_LIMITATION：设备传输/驱动、完整插件工厂扩展。DEFERRED：附加码、ExpandedStacked自定义列数、实际码图预览、完整AI字典、复杂排版、图片依赖大版本迁移（旧框架兼容）。BLOCKED_APPROVAL/EXTERNAL：无。

阶段证据：核心定向326通过；双运行时各3685通过；Host193通过；严格XML10/10；模板12类×4协议48组；候选包及真实手册片段通过。D3/D5已闭环：前端358项、完整Chromium174项全部通过，retries=0；首轮失败均保留证据，未跳过或放宽生产校验。OPEN_ACTIONABLE=0；五项CLOSED，停止本轮。详细结果见[本轮报告](../../docs/databar-completion-verification-2026-10-22.md)。
