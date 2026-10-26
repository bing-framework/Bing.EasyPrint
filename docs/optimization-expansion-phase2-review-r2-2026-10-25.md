# 第二阶段 O4：集成修正第 2 轮独立复核

> 历史候选记录。当前结论已由 [r3 最终复核](optimization-expansion-phase2-review-r3-2026-10-25.md)更新为 PASS_LOCAL；保留下文原始问题和证据。

日期：2026-10-25。当前结论：`NEEDS_FIX`。原问题 7 项 CLOSED、3 项 OPEN_ACTIONABLE；进入最后一轮集成修正 **3/3**，不进入 F1/F2。

## 已验证的进展

主代理检查修正源码，并用交付的 Host DLL 在端口 5197 重放 r1 的 HTTP 反例。进程已停止；源码/测试未由主代理修改。证据：`artifacts/optimization-expansion-20261025/phase2/main-review-r2/host-probes-confirmed.json`，同目录 `confirm.ps1` 为命令。

- ZIP 收尾条目唯一且非空，独立边界标记正确；HTTP copies=2/PageEnd 返回两个页面文件、两个收尾文件，manifest 顺序为 page1→finish1→page2→finish2，source/copy 索引正确。
- 未声明收尾、Gs1 QR、Bold/Reverse 均明确 400；null protocolId 和旧 Cpcl 携带 finishing 均明确 400。
- HELLO/M/module4 的 QR 布局修正为 116×116；90 度条码布局 40×202，预览包确有 PNG 且 diagnostics 为空。
- max60 自动分页仍为两页，max200 为一页。先前已关闭问题未发现回归。

独立 luna_worker 核对原始证据：Core 三个运行时各 4643/4643，最终 Host 287/287（286 是早期记录），XML 10/10，Vitest 421/421，Chromium 201/201。现代/net6/3.1 消费均 PASS、退出码 0、候选包哈希一致。net10 消费包内 net7 资产，说明正确；首次长路径失败原始日志保留。

真实浏览器用例已经读取仓库 `demo-project.json`；模板案例日志包含 C#/JSON/往返核对及完成输出。案例目录是根目录 103 个文件、包含子目录共 132 个，不应将 103 写成总数。模板案例缺单独退出码 sidecar，当前以完成输出和产物交叉佐证；下轮明确记录退出码，不为补历史日志重新制造执行记录。

## 当前状态

| Finding | 状态 | 结论 |
| --- | --- | --- |
| P2-R1 | CLOSED | 独立 custom 入口/结果及外部身份不回归。 |
| P2-R2 | OPEN_ACTIONABLE | 上轮 GS1 拒绝、样式拒绝、默认 QR 尺寸、旋转条码已修复；能力上限仍未实际执行，见 R2/R5。 |
| P2-R3 | CLOSED | 自动分页修复已复核。 |
| P2-R4 | OPEN_ACTIONABLE | HTTP 顺序和 ZIP 已修复；核心直接调用仍不按份收尾，副本展开绕过页数上限且没有取消检查。 |
| P2-R5 | OPEN_ACTIONABLE | Host 硬编码已有修正，但权威能力契约未闭环；新独立适配器测试只是序列化，不是实际注册/布局/输出。 |
| P2-R6 | CLOSED | 上轮 null/空白/新旧 finishing 冲突反例已明确拒绝。 |
| P2-R7 | CLOSED | 真实 Host 浏览器案例存在且通过。 |
| P2-R8 | CLOSED | v2 状态容器不回归。 |
| P2-R9 | CLOSED | 先前支持范围矛盾已修正；摘要遗漏、案例统计口径等小修随下一报告同步。 |
| P2-R10 | CLOSED | 实际仓库 JSON、C#/JSON/往返案例和 net6 消费证据均已补齐。 |

## 最后一轮必须修正的行为

### P2-R4（P1）：物理副本展开遗漏资源/取消约束，且只存在于模板层

位置：`TemplateRenderer.cs` 的 RenderCustom 新增 physicalDocument/physicalSources 循环；`PrintProtocolFinishing.cs` 的 PrintProtocolOutputComposer 仍按 document.Pages.Count 生成控制。

实际证据：

1. `limit-probes.json`：单页文字模板、copies=101、未放宽任何资源配置，preview 返回 200/101 页，超过 TemplateRenderOptions 默认 MaxPages=100。展开发生在后端校验前，循环内也没有 cancellation 检查，合法的大整数份数会提前分配大量页面。
2. `core-copies-probe.json`：通过 PowerShell 当前 .NET 10.0.8 直接加载交付程序集，注册 Demo、创建一个 page.Copies=2 的 PrintDocument，调用 composer 的 PageEnd。结果仍为一个页面文件，内含两次 DEMO/PAGE，仅一个收尾文件。此为直接 API 定向诊断，不冒充 net10.0.13 全量验证。
3. composer 在 RenderFinishing 返回后没有最终取消检查；现有 cancellation 测试仅覆盖调用前已取消，不覆盖回调执行期间取消。

验收：

- 在分配副本和调用编码器前以溢出安全计算检查实际页数，遵守 MaxPages/输出预算；展开循环观察取消。覆盖边界 100 通过、101 拒绝（或调用者明确配置的其他边界），拒绝时 RenderCalls=0，无部分结果、预留释放。
- 在共同的新协议执行入口规范化物理副本与 source/copy 元数据，使直接 Core 和 Templates/Host 对 PageEnd/JobEnd 的语义一致。保留旧内置接口的原生 copies/字节默认行为，不能不加区分地展开所有 legacy 输出，也不能通过拒绝合法多份规避。
- 覆盖取消发生于 Render/RenderFinishing 回调、复制展开期间，以及异常后的释放。最终返回前观察令牌。

### P2-R2 / P2-R5（P1）：能力声明没有成为实际渲染约束

位置：`TemplateCustomProtocolCapabilities.SupportsTextSize`、`TemplateCustomProtocolValidator.ValidateFont`、`TemplateFontResolver.Resolve` 的 custom 分支；Core/模板能力构造和 Host 映射。

实际与源码证据：

- `limit-probes.json`：示例能力 textSize.maximum=512，FontSize=513 的 HTTP 模板仍返回 200。SupportsTextSize 仅检查离散集合，空集合恒 true，没有检查 MaxTextSize。
- Host 新测试独立适配器声明 FontKinds=[1,2]，但 custom TemplateFontResolver 无条件拒绝所有设备字体，即使明确声明支持；FontKinds 也没有参与来源校验。
- `Custom_capabilities_use_non_default_adapter_configuration_and_limits` 直接调用 CustomCapabilities 序列化一个未注册 adapter；该 adapter 的所有测量/转换方法都 `throw new NotSupportedException()`，因此只能证明字段映射，不能证明第三方目标可渲染。
- Core 与模板继续分别手工构建能力；Core 注释说共享快照，但没有配置后的共同有效快照/一致性约束。问题不是“两种类型存在”本身，而是相同声明在不同路径得到不同结果。不能把未完成的 R5 自行降为已接受风险。

验收：

- 范围、离散值、字体来源、设备字体及开关在配置校验/布局/后端预检中一致；声明支持的设备字体交给协议适配器测量和转换，未声明才早拒绝，不依赖内置枚举猜测。
- 可保留层次不同的公开类型，但从同一配置解析后的能力来源投影/校验，避免独立手工维护同一事实；目录默认描述和请求有效能力有明确一致性关系。
- 提供可实际运行的独立目标，至少使用非默认字号上限和一个已声明的设备字体，走注册→配置→模板测量→实际输出以及 Host 协商；验证上限内正常、上限外编码前拒绝。不得以序列化测试或调整文档将能力缩水代替实现。
- 上轮已修复的 GS1 拒绝、普通 QR/条码准确图像继续回归，不扩展无关码制或厂商实现。

## 执行约束与停止条件

Implementation 总进度仍为 3/10。本轮有明确进展，No-Progress=CHANGED；O4 原问题仅剩 R2/R4/R5。外部 CI 为 BLOCKED_EXTERNAL，实机验收 NOT_APPLICABLE，延期事项不变。

最后一轮先精准回归，再依变更影响执行阶段验收；禁止覆盖 r1/r2 原始证据，使用 integration-r3-*，记录真实运行时及退出码。当前成功证据可按不变范围复用，不能替代新增反例。代码/测试继续由 OpenCode / deepseek-v4.1-flash 实现。达到 3/3 后如仍有未解决项，明确报告并按计划停止，不自行启动第 4 轮或进入 F1/F2。
