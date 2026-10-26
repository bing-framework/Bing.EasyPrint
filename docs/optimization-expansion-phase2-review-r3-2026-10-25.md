# 第二阶段 O4：最终独立复核

日期：2026-10-25。集成修正轮次 3/3。结论：**PASS_LOCAL，O4 CLOSED**。原 P2-R1—R10 全部关闭，本阶段 OPEN_ACTIONABLE=0，停止 O4 修复循环；按总计划推进第三阶段 F1/F2。历史 r1/r2 结论仅对应当时的候选。

## 本轮关闭的契约

| Finding | 状态 | 源码与独立验证 |
| --- | --- | --- |
| P2-R2 | CLOSED | SupportsTextSize 同时执行离散集和 MaxTextSize；字体来源按 FontKinds 验证。HTTP 字号 512 成功、513 被拒绝。已有 GS1/样式拒绝、QR 静区及旋转条码反例重放通过。 |
| P2-R4 | CLOSED | Core composer 在编码前规范化物理副本，模板在创建后端前检查实际页数，循环和回调后观察取消；旧入口使用 RenderLegacy 保留原生份数。HTTP 100 份成功、101 份拒绝；直接 Core 两份得到两页、两次 PageEnd 及对应 source/copy。 |
| P2-R5 | CLOSED | FromCore 将同一配置能力投影到模板；示例注册与布局共用能力来源。独立目标测试实际经冻结注册表、配置、设备字体测量、输出和 Host 预览，上限外在编码前拒绝。 |

P2-R1/R3/R6/R7/R8/R9/R10 沿用已关闭结论。本轮 HTTP 重放还确认收尾 ZIP 条目唯一非空、顺序正确；未声明收尾、null 协议标识、新旧参数冲突均拒绝；配置后的分页和元素 PNG 正常。

## 验证与证据

变更影响：Core 共同输出、模板自定义协议限制/字体、Host 注册契约；未改生产 TFM。风险 HIGH。主代理复核以上源码并执行 L1 和 HTTP/API 反例；阶段 L2—L4 使用执行方原始证据，经 luna_worker 独立盘点。未运行大型性能矩阵。

证据根目录：`artifacts/optimization-expansion-20261025/phase2/`。

| 验证 | 实际环境与结果 | 原始证据 |
| --- | --- | --- |
| Core 全量 | .NET 10.0.13、6.0.36、3.1.32 各 4653/4653，失败/跳过 0 | integration-r3-core、integration-r3-runtime |
| Host 默认入口 | net10.0，290/290，失败/跳过 0 | integration-r3-host-final、同名 stdout |
| 严格 XML / 五框架 | Core/Templates 共 10/10 构建 | integration-r3-xml/verification.json |
| Chromium | 201/201，workers=1、retries=0 | integration-r3-e2e.stdout.log |
| 前端构建 / Vitest | 复用 r2 不变前端范围：PASS、421/421 | integration-r2-designer-build.stdout.log、integration-r2-designer-vitest.stdout.log |
| 候选包消费 | net10/net6/netcoreapp3.1 均 PASS、exit 0，同一候选包；net10 使用包内 net7 资产 | integration-r3-modern、integration-r3-consumer-net6、integration-r3-c31 |
| 模板案例 | exit 0；根 103、递归 132 个生成文件，并非 132 个测试用例 | integration-r3-template-samples、同级 .stdout.log/.stderr.log/.exit-code.txt |
| 主代理定向 Core | net10.0，27/27 | main-review-r3/core/targeted-core.trx |
| 主代理定向 Host | net10.0，19/19 | main-review-r3/host/targeted-host.trx |
| 主代理 HTTP / 直接 API | 上述反例全部符合预期 | main-review-r3/confirm.ps1、host-probes-confirmed.json、limit-probes.json、core-copies-probe.json |

主代理定向命令使用隔离 SDK `10.0.402` 的 dotnet，分别对核心及 Host 测试执行 `test -c Debug -f net10.0 --no-build --no-restore`；过滤器分别是 `FullyQualifiedName~ProtocolRegistryTests|FullyQualifiedName~TemplateCustomProtocolTests` 和 `FullyQualifiedName~ProtocolRegistryHostTests|FullyQualifiedName~IndependentProtocolHostTests`。两个命令 exit 0。HTTP 使用同一 Host DLL；直接 Core 探针由 PowerShell 的 .NET 10.0.8 执行，单独标识，不冒充 10.0.13 全量结果。

模板目录含 119 个打印输出、13 个 JSON；stdout 明确记录 18 条 Composite、2 条 Raster C#/JSON 一致性及 1 条 GS1 QR/DataMatrix 往返检查。r3 early 与 final Host 都是 290；287/286 仅属于 r2 历史记录。

前端复用有执行方说明、日志及文件时间支持，但旧 source-identity 仅含 HEAD/dirtyFiles，不能独立重建当时完整源码集合；保留该证据粒度限制，不补造历史哈希。最终候选仍需计划规定的新鲜完整验收。三种消费的候选与恢复包哈希已核对一致。

## 状态与后续

- Implementation：4/10（O1—O4 CLOSED）。本阶段本地门禁通过；外部运行证据独立记录，不宣称全部发布门禁已通过。
- Completed：第二阶段注册、布局、能力、收尾、设计器、独立案例和兼容路径。
- Open Actionable：本阶段无；总计划 F1—F6 待实施。
- Blocked Approval：无。
- Blocked External：GitHub Actions Windows/Linux 实际运行。
- Not Applicable：实机验收（用户排除）；本轮发布。
- Accepted Limitations：示例协议不声明真实设备兼容或整页 PNG；固件专属像素边界保留。
- Verified Boundaries：100/101 份、512/513 字号、取消与预算拒绝。
- Deferred：传输、复杂字体塑形、富文本、新厂商协议实现、ImageSharp 大版本迁移。
- No-Progress Check：CHANGED，最后三项均取得修正证据；无第四轮 O4 修复。
- Next Action：按[第三阶段契约](../ai_docs/plans/optimization-expansion-phase3-contracts-2026-10-25.md)实施 F1/F2。总任务 RUNNING。

未暂存、提交、推送、发布、改变版本或覆盖 API 基线。文档收口后只做差异/链接检查，复用上述代码验证。
