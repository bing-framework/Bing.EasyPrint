# EAN/UPC Composite 附加码实施计划（2026-10-22）

基线 dc79eab9b27aaa53fd89f887b55613d777b64184，工作区初始干净。沿用五目标框架，不提交、推送或发布。实机验收按用户要求排除。

## 契约与影响分析

仅扩展 Type=11 的 EAN-8/EAN-13/UPC-A/UPC-E Composite：主数据接受 `主码+2位或5位ASCII数字`。主码的长度、校验位和 UPC-E 零压缩规则保持；附加码不补零、不修剪、不容忍多个加号。支持 Auto/CC-A/CC-B，拒绝 CC-C；普通 Type=4 契约不变。

附加码使用固定 Zint 2.15.0 commit 4896136bdbebb1a0908a5cc419513efa5bf4bf70 的 upcean.c 奇偶表和布局规则。UPC-A 间距9X，其他7X；三条2X分隔仍仅围住主码。高度仍为 H+(2R+6)X，无 HRI。不改变公共 .NET 签名、枚举或 HTTP 字段。

Host 追加 `gs1-composite-ean-upc-addons` feature 与 `ean-upc-digits-with-addon` primaryFormat。前端只有需要附加码时要求新 feature；旧 Host 继续支持纯主码并明确拒绝附加码，不改写用户数据。

- ChangedProjects：Core、Templates、Host、Designer、Tests、Samples、Docs。
- ChangedPublicContracts：扩大 Type=11 输入及能力协商；签名不变。
- ChangedRuntimePaths：主数据预检、线性编码、分隔定位、尺寸与边界、模板绑定、Host 能力、设计器校验与项目往返。
- ChangedProviders：CPCL / ESC/POS Raster / TSPL / ZPL 共享图片路径。Native 不支持 Composite。
- ChangedTFMs / BuildPackaging / BenchmarkHarness：无。AffectedDependents：模板、Host、包消费。RiskLevel：HIGH。

## 状态清单

| 项目 | 状态 | 验收条件 |
| --- | --- | --- |
| A1 编码与输入 | CLOSED | 固定独立完整矩阵逐模块一致，2/5位奇偶规则、校验位、严格错误输入 |
| A2 输出与模板 | CLOSED | 四协议、旋转静区、精确边界、预算取消恢复、V2/V3往返 |
| A3 Host与设计器 | CLOSED | 能力协商、编辑预览导出保存重载、旧Host降级 |
| A4 案例与文档 | CLOSED | C#/JSON/往返、包消费、中文XML、手册与README |
| A5 阶段验收 | CLOSED | 五框架XML、双运行时、Host、Vitest/构建/Chromium零重试、差异编码检查 |

证据目录 artifacts/ean-upc-addons-20261022。先定向 L0/L1，再阶段 L2/L3/L4；不运行无关性能矩阵。主代理负责核心与集成，luna_worker 独立负责前端与案例文档。遵循 GOAL_RULES；仅 OPEN_ACTIONABLE 进入修复，无进展两轮/同根因三轮即停止。

NOT_APPLICABLE：实机验收、新能力历史性能对比。ACCEPTED_LIMITATION：设备传输/驱动、完整 Provider 工厂插件体系。DEFERRED：ExpandedStacked 自定义列数、真实码图预览、完整 AI 字典、复杂排版、图片依赖大版本迁移（旧框架兼容）。不把本阶段完成宣称为全部潜在功能完成。

最终验证完成于2026-10-23：核心双运行时各3722通过、Host199、Vitest388、Chromium177（零失败/跳过/重试）、严格XML10/10；16类×4协议案例及候选包net6.0消费通过。README/新指南实际片段通过，静态检查通过，原有跟踪XML未修改。首两次新增预算测试的作用域断言错误已记录并更正，未放宽生产预算。A1–A5 CLOSED，OPEN_ACTIONABLE=0，停止本阶段。证据见[阶段报告](../../docs/ean-upc-addons-verification-2026-10-22.md)。
