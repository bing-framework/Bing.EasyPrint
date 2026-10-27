# 优化扩展验收后的配套完善

日期：2026-10-26。用户授权“继续完善”。范围为上一轮审查确认的两项配套问题，不扩展原计划明确延期的产品能力。原十项实施及历史验证结果保持原记录。

| 编号 | 工作项 | 状态 | 验收条件 |
| --- | --- | --- | --- |
| P1 | BatchConsumer 导出协商与结果核对 | CLOSED | 每块要求三项特性，校验版本、分块 manifest、记录状态及实际导出文件；12 项进程回归通过；真实旧 Host 拒绝且原文件保留；真实当前 Host 生成 2 PDF + 4 SVG |
| P2 | 示例协议文档勘误 | CLOSED | 状态文档与扩展指南已对齐源码：类型 1/3/4/5/7、元素位图 3/4，以及整页 PNG、图片、GS1 QR 的不支持边界；链接及 diff 检查通过 |
| E1 | Windows Actions 执行证据 | BLOCKED_EXTERNAL | 获得对应候选的真实运行及测试结果 |
| E2 | Linux Actions 执行证据 | BLOCKED_EXTERNAL | 获得对应候选的真实运行及测试结果 |

## 分工与影响分析

- Codex / gpt-6-luna / max：`samples/BatchConsumer/` 与定向验证脚本；luna_worker：两处协议文档；主代理：契约、计划、真实 Host 集成及最终复核。
- ChangedFiles：BatchConsumer 程序/README、定向回归脚本、两处协议文档和本轮计划/报告。
- ChangedProjects：仅 BatchConsumer 示例；ChangedPublicContracts / Providers / TFMs / BuildPackaging / BenchmarkHarness：无。
- ChangedRuntimePaths：示例 HTTP 协商、响应及 ZIP 校验、失败时的结果保存；AffectedDependents：示例调用者。
- RiskLevel：MEDIUM。先示例编译与模拟 Host 进程级反例，再真实 Host 消费；未改生产库、Host、前端，复用其已有门禁，不重跑全量。
- 本轮最多三轮修复，同一问题连续两轮无进展即停止。保留工作区原有两份 XML 修改；不提交、推送、发布、改版本或 API 基线。

## 执行记录

- 起点：`4f440f9987e0ad6a3ad13e4f6e7439b121df18b7`。
- 原始证据：`artifacts/optimization-expansion-20261025/post-review-20261026/`。
- 已通过 GitHub API 只读查询工作流运行记录，返回 404，无法据此确认是否执行或通过；两平台保持 BLOCKED_EXTERNAL，不反复查询、不以本地结果替代。
- Round：1；Implementation：2/2；Open Actionable：无；Next Action：STOP；Goal Status：STOPPED_BLOCKED（仅两平台外部 CI 证据）。
- 本机已有 5187 旧 Host 声明 batch-export，但缺少 PDF/SVG 特性；增加必需特性后实际返回 HTTP 409 / ContractFeatureUnavailable。首次探针误将预期写为 400，按现有契约校正判断，保留原响应，未重发相同请求。
- 2026-10-27 收口：消费者 Release 构建与最终 12 项回归通过，失败 0；主代理明确使用 .NET 6.0.36 消费现有已验收 .NET 10 Host，2 条/2 页/2 PDF/4 SVG、63698 原始字节、exit 0。原始脚本开发失败记录保留；最终证据与复用说明见[本轮修复报告](../../docs/post-review-polish-2026-10-27.md)。
