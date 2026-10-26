# 第四阶段独立复核：首轮

日期：2026-10-26。执行者：Codex / GPT-6-Luna / max；主代理负责契约审查及 HTTP 反例，luna_worker 只读核对原始门禁日志。本次不修改生产代码，不重复前三阶段已关闭审查。

## 结论与证据

F3/F4 暂不关闭。委派完成只表示执行结束：虽然既有门禁通过，本轮发现 8 项可执行契约缺陷及 1 项构建证据缺口。进入第四阶段第 1/3 轮集成修正，尚未进入 F5/F6。

确认原日志：Core net10/net6/netcoreapp3.1 各 4693，通过的真实旧运行时为 6.0.36/3.1.32；Host 322；Vitest 456；Chromium 208（workers=1/retries=0），后续 GS1 2；五 TFM XML 10；候选包实际消费成功（消费者 .NET 10.0.11，选择生产 net7.0 资产，包匹配）；案例日志 22 条成功记录。原始首次失败保留。这些通过结果不覆盖下面的新反例。

HTTP 反例在执行方保留的本机 Host `http://127.0.0.1:5187` 运行。请求与响应保存于 `artifacts/optimization-expansion-20261025/phase4/main-review/`：`host-contract-probes.json`、`repeat-binding-probe.json`、`query-placement-roundtrip.json`、`diagnostic-index-probe.json`。源码与响应相互印证；不是仅根据类名或文件存在判断完成。

## OPEN_ACTIONABLE

| ID / 优先级 | 位置与触发 | 实际影响 / 证据 | 修正与验收条件 |
| --- | --- | --- | --- |
| R1 / P1 | `src/Bing.EasyPrint/Protocols/Gs1DigitalLink.cs:170`，选择最后一个等于 `01` 的路径段 | 合法 `/01/09520123456788/10/01` 返回 400 `gtin-required`；值段被误认为主键 | 按完整主键与限定符结构定位，不把限定符值视为 AI；覆盖 10/21 值为 01、值恰为合法 GTIN、自定义前缀含 01 和错误结构，Build→Parse→Normalize 一致 |
| R2 / P1 | 同文件 `:213`，query 的去重集合仅含 01 | `/01/09520123456788/10/LOT?10=OTHER` 转 Elements 返回 200、两个 10 和 `status=valid` | 路径/query 共享已用 AI 集合；Parse/TryParse/Normalize/Host 一致拒绝，不依赖后续 Build 偶然拒绝 |
| R3 / P1 | 同文件 `:497`、`:520`，手动 authority/path 拆分缺少 URI 结构验证 | `https://[bad/01/09520123456788` 返回有效，Build/转换生成非法 URI | 严格验证 authority、port、原始路径/fragment/转义；保留先按原始分隔符处理再解码的语义，不让通用 URI 规范化静默改变内容。合法前缀/IPv6/端口往返，非法 authority/反斜线/百分号等明确拒绝 |
| R4 / P1 | `web/Bing.EasyPrint.Designer/src/components/Gs1AiEditor.vue:231`，新建输入初始化 QueryParameters=[]，转换时总是发送现有列表覆盖粘贴 URI | 用实际前端形状请求包含 campaign=A、campaign=B 的 URI，返回值静默丢掉全部 campaign；旧 BaseUri/Fragment 也可能被无意覆盖 | URI→结构化默认保留源 URI；仅在用户明确选择覆盖时应用已有配置。真实浏览器从新建及已有编辑状态粘贴含重复 query/fragment/不同基址 URI，保存重载/渲染保留原语义；转换失败不改源配置 |
| R5 / P1 | `tools/DesignerHost/DesignerRequestProcessor.Gs1.cs:331`，返回所有项 Placement=Auto | 输入 `...?10=LOT` 的转换 output 不变，返回结构化配置再校验输出却变为 `/10/LOT`；见 query-placement-roundtrip | 返回的结构化数据保留原 qualifier/dataAttribute 位置；覆盖 10/21/235 查询及路径，转换 output 与保存后实际编码一致 |
| R6 / P1 | 同文件 `:92`、`:165`，校验遍历使用同一个根 Binding | Rows.RepeatSource=Items、AI01 Name=Gtin、data.Items=[{Gtin:...}]：gs1-validate 报字段不存在，而 preview 成功且正确输出。组件参数/变量也未沿渲染上下文执行 | 复用共享绑定/实例遍历语义，覆盖重复/嵌套重复、$root/$item、变量、组件实例参数和条件。校验逐实例结果/诊断与实际渲染一致并带实例标识，不能直接校验未实例化组件定义；继续预算/取消与无数据 pending 契约 |
| R7 / P2 | `src/Bing.EasyPrint.Templates/Rendering/Gs1StructuredInput.cs:206` | Items=[01,10] 的动态10超长，响应 ai=10 但 itemIndex=0/Items[0]，前端会同时把错误标给01 | 将 qualifiers/dataAttributes 中的局部索引映射回原 Items；扩展 query/primary/secondary 字段路径不混淆。加入乱序项目、各类错误的具体 index/path/AI 断言 |
| R8 / P2 | `Gs1AiEditor.vue:107`、`:300` | DigitalLinkRole.Unsupported 无条件禁用添加；此角色仅描述 Digital Link，误禁止 Elements 中合法的03/8014/8200 | 按当前模式判断支持范围；Elements 可选择固定字典合法 AI，DigitalLink 对排除项明确拒绝。真实浏览器覆盖 Elements 添加8200、切换DL失败保留内容及恢复Elements |
| R9 / P2 | 阶段报告生产构建只引用终端执行记录 | 证据目录只有首次失败，没有持久化成功构建日志；其余报告数字核对通过 | 本轮前端变更后将 npm run build 成功输出保存到独立 round1 目录，补真实命令/退出码/证据链接；不得删除或改写首次失败 |

## 变更影响和验证安排

- ChangedFiles/Projects：Core DigitalLink parser；Templates GS1 绑定/诊断；Host 转换与校验；Vue AI 编辑器及相应测试/手册。
- ChangedPublicContracts：修正已批准新增 API 的校验、转换和诊断契约；旧 API/枚举/默认行为、生产五 TFM 不变。
- ChangedRuntimePaths：结构化 GS1 预检、绑定、转换、浏览器持久化；涉及渲染上下文的共享提取须保护旧路径。
- ChangedProviders/TFMs/BuildPackaging/BenchmarkHarness：不新增协议，不改 TFM/版本/依赖，不执行无关性能矩阵。
- RiskLevel：HIGH；先上述反例 L1，再受影响项目 L2/L3，阶段收口执行既定门禁；未变范围可明确复用，涉及公共共享绑定路径时扩大回归。
- 原始证据不可覆盖。新证据目录 `artifacts/optimization-expansion-20261025/phase4/integration-r1/`。阶段报告更新结果而非宣称首轮一遍通过。

## 当前执行摘要

- Task：optimization-expansion-2026-10-25 / phase4。
- Round：首次独立 Review，下一步集成 Fix 1/3。
- Implementation：6/10；F3/F4 未验收；Release Gates 尚未全部关闭。
- Validation Run：L0 源码/契约审查；L1 本机 HTTP 反例；本轮未重跑 L2—L5。
- Reused Evidence：委派方现有日志经独立盘点，只证明对应已覆盖场景；前三阶段未变范围复用。
- Completed：门禁证据盘点、请求/响应反例归档。
- Open Actionable：R1—R9，F3/F4；F5/F6 顺序待实现。
- Blocked Approval：无。
- Blocked External：Windows/Linux Actions 实际运行。
- Not Applicable：实机验收（用户排除），本轮发布。
- Accepted Limitations：第一版 GTIN、离线未压缩 Digital Link，不访问 resolver；现有设备字体限制。
- Verified Boundaries：本轮无新增容量结论。
- Deferred：既定传输、复杂字体塑形、富文本、新厂商协议及图片依赖大版本迁移。
- No-Progress Check：CHANGED，新确定性反例。
- Next Action：同一 Codex/gpt-6-luna/max 线程完成 R1—R9 第 1/3 轮修正后交主代理复核。
- Goal Status：RUNNING。
