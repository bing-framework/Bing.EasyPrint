# 当前功能 TDD 测试补全

起点：`0507b372dcd581ddb45e2c868b4403350318e0d5`。用户要求补全当前测试及对应 TDD；已有历史测试只作为回归证据，不追认为 TDD。

## 范围与验收

- 检查最近 O1—O4、F1—F6 的既有需求到测试映射；优先补充可确认的边界缺口。
- 主代理负责核心字段格式及 Host 集成；luna_worker 独立负责前端批量导入模块及其测试。
- 新发现的行为问题先写测试，保留真实红灯，再最小修复、绿灯、回归。直接通过的新增测试标记为覆盖补充。
- 提供后续可执行的 TDD 指南、需求映射及本轮证据报告；不伪造历史先后顺序。
- 不改变产品支持范围、版本、公共 API 基线、性能或容量阈值。外部 CI、旧运行时及实机缺口沿用已有状态。

## 影响分析

- ChangedFiles：字段格式回归测试、可能涉及的 BgFieldFormatter、Host 字段格式测试；前端 batch 模块及测试；指南/计划/报告。
- ChangedProjects：Core 测试（含 Templates）、DesignerHost 测试、Vue 设计器。
- ChangedPublicContracts：无签名变更；修复必须符合已有文档契约。
- ChangedRuntimePaths：日期输入与显示长度预检、批量数据解析。
- ChangedProviders / ChangedTFMs / ChangedBuildPackaging / ChangedBenchmarkHarness：无。
- ChangedDocsOnly：否。AffectedDependents：模板与 Host 消费者、批量导入用户。RiskLevel：MEDIUM。
- 验证：先新增用例 L1 红/绿，再对应项目 L2；跨模板/Host 的日期行为补 L3 集成。最终检查点验证当前 Core/Host 全量和前端单元/类型/构建；仅局部测试/解析修改不重跑无关性能矩阵。

## 当前进度

- Completed：本轮 5/5 工作项完成：日期 Core/Host 红绿、JSONL 红绿与控制、浏览器恢复流程、受影响回归、指南/映射/证据。最终 Core4714、Host373、Vitest478 全量通过；日期在 net6.0/netcoreapp3.1 各22项通过；字段/批量浏览器6项通过。
- Open Actionable：无。
- Blocked Approval：正式性能/容量通过阈值（沿用旧计划，本轮不自行批准）。
- Blocked External：两平台 CI 和 .NET7 实际运行证据（沿用已有缺口）。
- Not Applicable：追溯性证明全项目历史 TDD；实机验收及发布。
- Accepted Limitations：既有协议、软件码图及设备字体边界。
- Verified Boundaries：日期格式数字年月日校验及诊断、长年份/重复时区合法输出、JSONL 物理行号及连续记录编号、修正文件后的恢复；详情见 `docs/tdd-completion-2026-11-06.md`。
- Deferred：本轮无关功能扩展。
- Round：1；No-Progress Check：CHANGED；Next Action：STOP；Goal Status：STOPPED_BLOCKED（本轮本地补测完成，沿用整体发布外部门禁）。

## 验证与证据

- L0：类型/生产构建及文档、差异检查；L1：日期 Core22/Host12、前端 batch22；L2：Core4714/Host373/Vitest478；L3：当前 Host + Chromium6，旧运行时日期各22。
- L4：未执行全解决方案全框架矩阵；L5：未重跑无关性能/资源矩阵。本轮未请求发布，未据此改变正式性能验收状态。
- 真实首败、夹具调整、环境失败与最终证据：`docs/tdd-completion-2026-11-06.md`、`artifacts/tdd-completion-20261106/manifest.json`。
- 基准4705/368/476仅作既有数量对照，当前回归使用本轮新鲜结果；前端纯源码/单元测试在全量完成后未变，浏览器及文档修改不使其失效。

同一问题连续两轮无进展即停止；最多三轮 Review/Fix；只有 OPEN_ACTIONABLE 才触发后续修复。
