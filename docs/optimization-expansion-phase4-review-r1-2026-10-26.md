# 第四阶段集成修正 1/3 独立复核

日期：2026-10-26。本轮复核限首轮 R1—R9 及修改的直接回归面，不重新打开前三阶段。

## 结论

R1—R5、R8、R9 关闭；R6 与 R7 已有实质修正，但下述验收路径仍不完整。F3/F4 暂不关闭，整体 6/10，下一步仅执行集成修正 2/3。

执行方报告 Core 三目标各4705、Host337、Vitest456、完整Chromium211、GS1聚焦3、XML10及候选消费/案例通过，luna_worker核对计数/退出码和产物一致。首败与后续成功日志已按阶段保存，不以总通过数代替针对反例的判断。

证据说明 E1（OPEN_ACTIONABLE，文档）：integration-r1 的Core日志中AVAILABLE_RUNTIME仅证明安装情况；RUNTIME .NET10.0.8来自PowerShell宿主，不能代表testhost。Host日志未记录测试进程实际patch，报告却用消费者清单的10.0.11描述Host。本目录也没有本轮TRX/runsettings/runtime验证清单。必须修正报告，明确已证实目标框架/计数与未记录patch的区别，后续必要运行使用现有验证入口留存实际运行时/TRX；不得为改报告重复三套全量测试，也不得推测或移用消费者进程证据。现代包消费者10.0.11有独立证据，不受此缺口影响。全量Chromium211没有失败；另一次定向红测2失败保留，不能描述为全量首败。

## 已关闭与有效修正

| 项目 | 独立复核 |
| --- | --- |
| R1 | 实际5188 Host接受 `/01/09520123456788/10/01`，返回原语义；源码按有效14位主键候选定位，新增Core往返测试覆盖值与前缀 |
| R2 | 原跨path/query重复10反例返回400 duplicate-ai，统一seen集合 |
| R3 | 原非法authority反例返回400 base-uri-host；新增authority/port/原始组件检查及IPv6/合法转义回归 |
| R4 | 前端默认URI转换不再发送现有BaseUri/query/fragment覆盖，显式覆盖单独选项；真实浏览器回归保真。旧显式queryParameters=[]的HTTP请求仍覆盖，属于请求明确语义，不是本轮失败 |
| R5 | `?10=LOT`返回Placement=DataAttribute，回填后校验output仍是`?10=LOT`；实际HTTP重放确认 |
| R6 已完成部分 | 重复项绑定、嵌套条件、变量和组件参数提取共享遍历；原有真实数据重复行反例已变为valid，带instanceIndices；但缺数据及前端聚合仍有问题 |
| R7 已完成部分 | 动态AI10超长现在准确返回Items[1]/ai10；主键校验位错误也有覆盖，但主键字符/长度错误仍缺AI |
| R8 | Unsupported限制与当前Mode关联，Elements可以添加8200；保留真实浏览器模式切换回归 |
| R9 | integration-r1/frontend-build-success.log已持久保存生产构建成功证据 |

独立HTTP重放根目录：`artifacts/optimization-expansion-20261025/phase4/main-review-r1/`。完整响应在 `host-contract-replay.json`、`query-placement-roundtrip.json`、`diagnostic-replay.json`。首次打印HTTP摘要的PowerShell表达式因成功响应没有errors数组报索引错误；完整响应此前已写入文件，随后只读重读摘要成功。这是检查脚本输出问题，不能记作产品测试失败或抹掉HTTP结果。

## OPEN_ACTIONABLE：仅剩两项

### R6：缺数据与多实例状态仍不正确（P1）

1. `tools/DesignerHost/DesignerRequestProcessor.Gs1.cs` 的 Rows 遍历在 Field 检查 hasData 之前解析 RepeatSource。省略data的合法重复行模板返回 `invalid`、`字段不存在：Items`，而不是pending。条件、DataPath及组件参数中的动态路径也需使用一致的待数据语义；不能把真正静态非法配置当pending。
2. `web/Bing.EasyPrint.Designer/src/components/Gs1AiEditor.vue:211` 仅find第一个同路径成功项，优先显示valid。两个实例中第一个GTIN正确、第二个为INVALID时，Host正确返回 `status=invalid`、一个valid item和instanceIndices=[1]的错误，前端表达式却算出valid。用户会同时看到“有效”和错误内容。

证据：`repeat-validation.json`保存original/missing-data/mixed三个请求和响应。`frontend-status-expression.json`重放源码相同条件优先级，记录Host invalid而UI状态valid；这是源码表达式验证，不冒充真实浏览器测试。

验收：无data时，对于依赖动态数据才能展开的合法重复/组件/条件路径返回待验证，仍检查静态配置，提供data但缺实际路径仍invalid；同字段所有实例或明确选中的实例状态一致，任一相关失败不能被另一个成功覆盖；保存instanceIndices用于定位。增加真实Host及真实浏览器混合成功失败、缺数据、恢复有效的回归。没有活动实例时不能虚构“已验证有效”。不要以跳过全部校验或删除相关错误解决。

### R7：主键字符/长度错误未携带已知AI（P2）

`Gs1StructuredInput.ResolveDigitalLink`已用sourceAi推导原Items索引，但抛错仍传入 `error.Ai`。NormalizeGtin的字符/长度分支没有AI，因此原始请求AI01绑定为INVALID时，返回itemIndex=0但ai=null。同一主键仅校验位错误有ai01，诊断契约不一致。

证据同 `repeat-validation.json` mixed第二实例；HTTP错误code=gtin-characters、fieldPath=Field.Gs1Input.Items[0]、ai=null。验收：对于已知AI01的空值/字符/长度/校验位错误都给ai01及正确原Items索引；primary不在首项时亦一致。保留旧异常类型/签名。其他已正确的qualifier/dataAttribute索引不退化。

## 下一轮范围及状态

- Task：optimization-expansion / phase4；Round：已复核Fix1/3，下一步Fix2/3。
- Implementation：6/10。Completed：R1—R5、R8、R9；不再重复审计这些已闭环根因。
- Open Actionable：R6、R7及报告证据说明E1；第五阶段F5/F6仍顺序待实施。
- Changed Scope：Host无数据校验遍历、Vue字段实例状态聚合、Templates已知AI诊断；必要Core诊断赋值。不新增公共配置/API，不改生产五TFM。
- RiskLevel：MEDIUM。L1先复现这三条边界，按实际修改范围做Host/前端完整单元、GS1真实浏览器、Templates诊断回归与必要XML检查。若共享渲染遍历未变，可复用本轮核心全量、候选消费及非GS1完整Chromium范围，不能因为更新报告而全量重跑。
- Validation Run：本轮L0源码审查、L1独立HTTP重放及前端状态表达式检查；没有重复L2—L5。
- Blocked Approval：无。Blocked External：Windows/Linux Actions。
- Not Applicable：用户排除的实机验收、本轮发布。
- Accepted Limitations：GTIN/未压缩/离线DigitalLink及既定设备字体边界。
- Verified Boundaries：本轮无新增。Deferred：既定传输、字体塑形、富文本、新厂商协议及图片依赖大版本迁移。
- No-Progress Check：CHANGED，原反例已有修正且范围明显收窄；不是同一候选无进展重跑。
- Next Action：同一Codex/gpt-6-luna/max线程仅修正R6/R7并回交独立验收。
- Goal Status：RUNNING。
