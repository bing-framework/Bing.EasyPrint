# ExpandedStacked 可配置列数实施计划（2026-10-23）

基线 dc79eab9b27aaa53fd89f887b55613d777b64184；开始时保留上一阶段 44 个未提交文件，快照见 `artifacts/expanded-stacked-columns-20261023/starting-state.json`。保留五框架，不提交、推送或发布；实机验收按用户要求排除。

## 范围和契约

普通 ESC/POS Raster Type=4 / BarcodeType=31 支持每行 1–11 列，Composite Type=11 / LinearType=31 支持 2–11 列。列指每行的字符对数上限；短末行不强制填满。省略或 null 沿用两列，显式非法值、其他制式上的列数立即报错，不静默纠正。编码遵循固定 Zint 2.15.0 commit 4896136bdbebb1a0908a5cc419513efa5bf4bf70 的补齐、行方向与分隔图案规则。

保留旧公共构造函数和方法签名，增加不可变模型的 `ExpandedStackedColumns` / `WithExpandedStackedColumns`，以及直接 Raster 绘制、测量入口。模板添加可选同名字段。Host 增加 `gs1-databar-expanded-stacked-columns` 协商特性及范围；旧 Host 继续支持缺省两列，拒绝显式列数请求。前端编辑、验证、预览和项目往返同步。

## Change Impact Analysis

- ChangedFiles/Projects：Core 编码、统一模型、Provider/静区；Templates 模型/渲染；Host；Designer；相关测试、案例、文档。
- ChangedPublicContracts：新增可选能力，旧签名和默认输出保持；不改 API 基线、版本或协议枚举。
- ChangedRuntimePaths：二进制补齐、行布局、尺寸测量、旋转/静区/预算/取消、模板绑定与能力协商。
- ChangedProviders：普通只影响 ESC/POS Raster；Composite 影响四种图片输出协议。
- ChangedTFMs/BuildPackaging/BenchmarkHarness：无。ChangedDocsOnly：否。
- AffectedDependents：模板、Host、设计器、候选包消费。RiskLevel：HIGH。

## 清单

| 项目 | 状态 | 验收 |
| --- | --- | --- |
| C1 编码与入口 | CLOSED | 独立普通全矩阵、Composite 外部参考、1/奇数/偶数/11列及默认兼容 |
| C2 跨层传播 | CLOSED | 四协议 Composite、普通 Raster、精确尺寸/旋转/静区/取消预算、模板往返 |
| C3 Host/设计器 | CLOSED | 范围协商、编辑预览导出保存导入、旧 Host 降级 |
| C4 案例/文档 | CLOSED | C#/JSON/往返、包消费、中文 XML、指南和导航 |
| C5 最终候选 | CLOSED | 五框架 XML、双运行时、Host、Vitest/构建/Chromium 零重试、差异生成物检查通过 |

先 L0/L1 定向验证，阶段收口执行 L2–L4。主代理负责核心和最终集成；luna_worker 独立负责前端及后续文档窄任务。不运行无关性能矩阵，遵循 GOAL_RULES 无进展与轮次停止条件。

NOT_APPLICABLE：实机验收、此新增配置的历史性能对比。ACCEPTED_LIMITATION：设备传输/驱动及完整 Provider 工厂插件。DEFERRED：真实码图预览、完整 AI 字典、复杂排版、图片依赖大版本迁移（保持旧框架）。

C1–C5 已实现并验收：普通 28 组与 Composite 40 组独立完整矩阵，核心新增 155 项、Host 新增 13 项；设计器列数编辑/保存重载/旧 Host 降级通过，C#/JSON/往返、包消费与文档片段通过。最终结果：五框架 XML 10/10；net6.0 与 netcoreapp3.1 各 3877 通过；Host 212；Vitest 399；类型检查和生产构建通过。首次失败及修正过程见[阶段报告](../../docs/expanded-stacked-columns-verification-2026-10-23.md)。Chromium 完整验收在修正 5 个旧 Host 模拟响应后 180 项通过，重试、失败、跳过和 flaky 均为零。差异、链接、编码及生成物检查通过，本阶段 OPEN_ACTIONABLE=0，结束本阶段实施。
