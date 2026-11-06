# 候选 CI、旧运行时与性能验收

起点：`0507b372dcd581ddb45e2c868b4403350318e0d5` 加当前未提交 TDD 修改；保留工作开始前两份 XML 修改。用户本轮要求补齐两平台候选 CI、确定旧运行时范围、明确负载/指标/门槛后完成性能验收。

## 固定范围

- 用户已确认：Windows 完整 Core 执行 net7.0、net6.0、netcoreapp3.1；不将 net5.0/netcoreapp2.2 列作本轮验收。此决定不删除测试 TFM 或更改库支持范围。
- Windows/Linux：Core net10、Host net10、现代候选包消费、字体、前端类型/构建/Vitest/完整 Chromium，必须相同 Git 候选及源范围，记录 run ID/attempt/URL/SHA。
- 性能：先冻结可审查契约与固定基线；阈值未经用户确认仅试测，不发布正式 PASS。新负载/方法与旧历史不直接混合。
- 用户已批准精确性能契约，并明确授权提交、推送 `codex/acceptance-20261106` 完成两平台 CI。提交排除原有两份 XML 修改及本地 artifacts。

## Change Impact Analysis

- ChangedFiles：运行时验证脚本、候选证据脚本、两平台 workflow、性能探针/验收配置及说明。
- ChangedProjects：仅工具/验证；生产库、Host、前端逻辑保持上一轮候选。
- ChangedPublicContracts/Providers/TFMs：无。ChangedRuntimePaths：仅验证入口。
- ChangedBuildPackaging：CI 编排与证据身份；ChangedBenchmarkHarness：是，正式负载须两份候选使用相同 harness。
- ChangedDocsOnly：否。AffectedDependents：CI/验收执行者。RiskLevel：MEDIUM。
- L0：PowerShell/YAML/契约解析、源码身份反例；L1：新探针 smoke、旧运行时真实执行；L2/L3：由两平台 CI 跑候选门禁。旧本地 Core/Host/前端回归按未变源码范围复用，不为工具编辑反复运行。L5：一次诊断与一次正式确认，阈值确认前不宣称正式验收。

## 工作项

| ID | 工作项 | 当前状态 | 验收条件 |
| --- | --- | --- | --- |
| C1 | 候选身份和两平台归档工具 | CLOSED | 起止 SHA/scopeHash、必要步骤结果、按 SHA/attempt 归档；静态和反例检查完成 |
| R1 | 三旧运行时完整 Core | CLOSED | 实际 7.0.20/6.0.36/3.1.32 各 4714/4714，零失败/跳过，保存命令/退出码/TRX/testhost |
| P1 | 正式性能契约和采集 | CLOSED | 五负载、3 预热/30 样本、固定基线、源/程序集/环境身份、原始样本齐全 |
| P2 | 性能门槛确认与正式执行 | CLOSED | 用户批准精确契约哈希；formal-r1 全部 PASS，峰值工作集 96.36 MiB |
| C2 | 当前候选远端发布并执行 CI | OPEN_ACTIONABLE | 已获明确授权；提交并推送候选，核对两平台相同 SHA/scopeHash，下载真实运行产物 |
| C3 | Linux 隐藏文件身份采集 | CLOSED | 首轮日志确认根因，FileInfo 长度读取；Windows Hidden 端到端 RED/exit 1 → GREEN/exit 0 |
| C4 | Windows 固定字典 checkout 字节 | CLOSED | 精确 LF 属性；autocrlf=true 实际 checkout RED → GREEN，已有哈希测试 1/1 通过 |
| C5 | Linux 自动系统字体扫描 | CLOSED | 损坏 loca 表反例 RED 两失败 → GREEN；Raster/字体 838/838，显式字体错误和取消保留 |

Completed：C1/R1/P1/P2/C3/C4/C5，详细结果见 `docs/acceptance-2026-11-06.md`。Open Actionable：C2 候选发布与 CI 取证。Blocked Approval：none。Blocked External：两平台候选 Actions 运行结果。Not Applicable：net5/netcore2.2 本轮执行、实机验收、包发布。Accepted Limitations：Host helper 进程内性能范围，不含 Kestrel/路由/准入或生产并发。Verified Boundaries：none。Deferred：未请求的产品功能。

Round：4（第二轮真实 Linux CI 提供新的字体加载根因，符合新外部证据例外）；Implementation：7/7（原 4 项加 C3/C4/C5）；Release Gates：旧运行时与性能 2/3，远端两平台 CI 1 项待完成。

Validation Run：L0 差异检查；L1 损坏字体 RED 两失败、一取消通过；L2 Raster/字体 838/838；L3/L4 修复后相同候选两平台完整 CI；L5 复用正式五负载 PASS。Reused Evidence：baseline build/measurement 保持固定，正式性能五负载不调用自动系统字体选择，按未变相关路径复用；新候选功能以远端完整门禁为准。

CI 修复影响分析：ChangedFiles 为 `.gitattributes` 与 `Write-CandidateEvidence.ps1` 及报告；ChangedProjects/PublicContracts/RuntimePaths/Providers/TFMs/BenchmarkHarness 均无；ChangedBuildPackaging 为固定资源 checkout 换行。RiskLevel LOW。L0 真 Git checkout 字节反例与 PowerShell 解析，L1 Hidden 端到端及现有字典哈希测试；L4 修复后同提交两平台完整 CI；L5 复用已测本机相同生产源码/字典字节/harness，不重复性能。

字体修复影响分析：ChangedFiles 为 `EscPosRasterCanvas.cs`、`EscPosRasterGlyphTests.cs` 及报告；ChangedProjects Core/Core.Tests；ChangedPublicContracts/TFMs/BuildPackaging/BenchmarkHarness 无；ChangedRuntimePaths 为 ESC/POS Raster 自动系统字体选择；ChangedProviders ESC/POS Raster；AffectedDependents 为字体/Provider 测试及最终候选 CI；RiskLevel MEDIUM。精确影响记录在 `font-fallback/impact.json`。字体库的两种异常基类相互独立，首次仅捕获 FontException 的失败保留，核对实际继承链后收敛到明确字体异常，不吞掉取消或其他系统错误。

No-Progress Check：CHANGED（第二轮远端运行提供新的字体加载根因及真实 RED/GREEN）；Next Action：发布最小修复并取得两平台 CI 证据；Goal Status：RUNNING。前两轮失败和跳过不转写为 PASS，后续仅处理真实可执行问题。
