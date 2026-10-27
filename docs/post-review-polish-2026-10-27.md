# 批量消费案例与协议文档修复报告

执行时间：2026-10-26 至 2026-10-27。用户授权“继续完善”，范围为[本轮计划](../ai_docs/plans/post-review-polish-2026-10-26.md)的两项配套问题。两项均 CLOSED；原十项主功能继续保持已验收状态。外部 Windows/Linux CI 仍无可核验结果。

## 修改与验收

| 项目 | 修改 | 验收 | 状态 |
| --- | --- | --- | --- |
| P1 案例能力契约 | 每块只发送一次三项必需特性；成功响应必须明确契约版本 1 和全部特性；核对分块 offset/记录数、有效记录状态及 completed 记录的 PDF/SVG 清单、文件存在性和非空长度 | 12 项独立进程回归通过；真实旧 Host 非零退出并保留原文件；真实当前 Host 双格式导出通过 | CLOSED |
| P1 结果保存 | 写入同目录临时 ZIP，全部校验和归档结束后替换结果；协议/文件错误清理临时文件；partial/all_failed 仍输出结果和错误报告并返回 1 | 缺失/空文件用例、既有文件保留、部分成功和全部失败用例通过 | CLOSED |
| P2 文档一致性 | 状态文档和扩展指南对齐 DemoProtocol：类型 1/3/4/5/7，元素位图 3/4；整页 PNG、图片、GS1 QR 不支持 | 对照能力声明，相关链接和差异检查通过 | CLOSED |

代码与回归由 [Codex / gpt-6-luna / max](codex://threads/01a178dd-c088-7023-9276-aff1ef13e9bf) 实现；协议文档由 luna_worker 修正；主代理独立检查差异、原始分块请求与真实 Host 行为。示例 README 已补协商、失败语义和脚本入口。

## 验证结果与证据

证据根目录：`artifacts/optimization-expansion-20261025/post-review-20261026/`。最终回归目录为 `batch-consumer/run-20261026T160742Z-93ee241f/`，最终构建日志为 `batch-consumer/build-20261027T000722-4bc4c8b4/build.log`。

| 验证 | 命令 / 环境 | 结果 |
| --- | --- | --- |
| 示例构建 | `dotnet build samples/BatchConsumer/BatchConsumer.csproj --configuration Release`；SDK 10.0.400，目标 net6.0 | 通过；既有 net6 EOL 警告保留，未变更目标框架 |
| 进程回归 | `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Verify-BatchConsumer.ps1 -NoBuild`；Windows PowerShell 5.1.19041.6456 | 12 通过、0 失败、0 跳过 |
| 真实当前 Host | `verify-live-host.ps1 -ConsumerAssembly samples/BatchConsumer/bin/Release/net6.0/BatchConsumer.dll -EvidenceName live-host-accepted`；脚本位于证据根目录，消费者显式 `--fx-version 6.0.36`，Host 为 .NET 10.0.11 | 2/2 记录、2 页、2 PDF + 4 SVG，63698 原始字节，exit 0；[摘要](../artifacts/optimization-expansion-20261025/post-review-20261026/live-host-accepted/summary.json) |
| 真实旧 Host | 5187 已运行服务缺 PDF/SVG 特性；用同一模板和两条记录调用修正后的消费者 | HTTP 409 / ContractFeatureUnavailable，消费者 exit 1，已有目标字节保持；[摘要](../artifacts/optimization-expansion-20261025/post-review-20261026/old-host-consumer-summary.json) |
| 静态检查 | `git diff --check`、变更文件 UTF-8 与相关链接检查 | 通过；既有行尾提示保留 |
| 外部 CI 查询 | GitHub 工作流 runs API，只读查询一次 | 404，无法确认执行状态；[原始记录](../artifacts/optimization-expansion-20261025/post-review-20261026/external-ci-check.json) |

十二项进程用例覆盖：旧 Host 拒绝、忽略请求头且缺少响应特性、错误契约版本、缺 PDF、缺 SVG、清单指向缺失 ZIP 项、空导出、缺失记录状态、未知记录状态、26 条双格式成功、partial、all_failed。主代理核对最终 `requests.ndjson`，两次请求的 offset 为 0/25、记录数为 25/1，各携带一次完整特性头。真实旧 Host 证据复用未变的非成功响应分支；当前 Host 消费在最终记录状态校验加入后重新确认，先前各次结果保留。

本次成功 Host 使用既有验收二进制，SHA-256 为 `53D4FF35046C1C180BAFA677D8CB4BF97796FDE8A31733E49A67E92C3A6BF10C`，与[前次最终验收](optimization-expansion-final-review-2026-10-26.md)一致。本轮生产 Host、Core、Templates 和前端源码未修改，复用对应原有门禁；没有重新跑五框架/全量核心/Host/Chromium/性能矩阵。两个原有 XML 工作区修改保留。

首次脚本验证失败及后续开发运行均保留在独立目录，涉及 PowerShell 5.1 的子进程输出、空字节数组和 JSON 数组展开处理；最终有效结果不覆盖这些记录。主代理旧 Host 探针最初误写 HTTP 400 预期，实际契约为 409，已按保存的原始响应校正判断。后台常驻启动被自动审批拒绝后，改用有固定观察窗口、finally 清理自建进程的验证脚本，真实 Host 消费完成；原有 5187 服务未停止。

## 本轮收口

- Round：1；Implementation：2/2；本轮局部验证通过；两平台外部门禁 0/2 有可核验记录。
- Completed：P1、P2；Open Actionable：无；Blocked Approval：无。
- Blocked External：Windows/Linux Actions 实际候选运行证据。API 404 不等于测试失败或确认未运行。
- Not Applicable：实机验收（用户排除）、本轮发布。
- Accepted Limitations：当前 PDF/SVG、GTIN Digital Link、批量规模及编辑器布局边界保持原计划范围。
- Verified Boundaries：校验错误不发布新结果且保留既有文件；部分/全失败保持独立状态与错误报告。
- Deferred：设备传输、复杂字体塑形、富文本、更多厂商协议实现、图片依赖大版本迁移。
- Validation Run：L0 构建/差异/文档；L1 进程回归 12 项；L3 新旧 Host 消费；L2/L4/L5 本轮未触发。
- Reused Evidence：未变生产库、Host、前端及包消费的既有验证；No-Progress Check：CHANGED。
- Next Action：STOP；IMPLEMENTATION_STATUS=PASS；TEST_STATUS=PASS（本轮范围）；EXTERNAL_GATE_STATUS=BLOCKED_EXTERNAL；RELEASE_STATUS=NOT_REQUESTED；GOAL_STATUS=STOPPED_BLOCKED。

未暂存、提交、推送、发布、改产品版本或覆盖 API 基线。
