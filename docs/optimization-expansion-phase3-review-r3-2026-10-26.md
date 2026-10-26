# 第三阶段：第 3 轮最终独立复核

日期：2026-10-26。实现结论 **NEEDS_FIX**，自动执行 **STOPPED_ROUND_LIMIT**（本计划停止原因标签）。第三阶段已达到 3/3 次集成修正上限，不再启动第四轮，不进入 F3—F6。

总计划 **5/10 CLOSED**：O1—O4、F1；F2 保持 OPEN_ACTIONABLE。阶段问题 **6 CLOSED / 2 OPEN_ACTIONABLE**：R1/R2/R3/R4/R6/R7 关闭，R5/R8 尚未满足验收。外部 CI 独立保持 BLOCKED_EXTERNAL。

## 已验证的修正

- **R1 CLOSED**：交付 Host 的普通数字 `1e40` 返回200、显示 `1E+40`；未用 `Unused=1e40` 不再破坏 `Value="OK"`。raw 层保留词法，实际数字格式绑定才转 decimal。公共 Host/批量测试覆盖溢出字段定位及混合模板；直接 HTTP 和真实前端解析/序列化在 de-DE 下均保留精度。旧日期显示及 offset 行为无回归。
- **R2 CLOSED**：`f90` 默认预算下返回200；核心公共模板测试 `Lowercase_standard_number_and_signed_text_min_values_fit_estimates` 覆盖小写标准格式及 SByte/Int16/Int32 最小值。原来漏掉的负号和小写分支已修正。低预算与非法日期路径的既有证据继续有效。
- **R5 部分修复**：两个真实 ZIP 的最终 metadata 为9080字节，8500限额现在明确拒绝，不再返回超限结果。header 与 manifest 的剩余额度仍一致。但最终输出大小检查不等于临时缓冲预检，详见剩余问题。
- **R8 部分修复**：新增第二块读取期间取消的测试，确实发生在合并开始后；原始首败日志保留情况已澄清。保留已关闭的份数隔离、CSV、快照及异常分类结论；本轮重放确认混合 EAN13 和格式错误仍为 partial，成功文件保留。

## 剩余问题与建议

### P3-R5 — P1 — 工作缓冲仍未完整执行分配前预算

状态：**OPEN_ACTIONABLE**。这是上一轮同一根因的未完成部分，不是新增能力要求。

位置与证据：

- `web/Bing.EasyPrint.Designer/src/batch.ts:147` 先 `JSON.stringify`、构建 errors CSV，再由两个 `TextEncoder.encode` 创建数组，直到151行才检查预算。主代理对真实函数做只记录、不改变返回值的 TextEncoder 包装：8500字节限额下，拒绝之前已依次分配4924和4156字节的输出数组，且对应字符串已经存在。此证据说明检查顺序，不声称测得进程峰值或两个临时数组的同时存活量。
- 同文件104行的 `chunk.arrayBuffer()`、114行的解码/JSON模型，以及156行生成 ZIP 所需工作缓冲，没有与文档声明相应的预留。`workingBytes` 只累计部分输入/解压/输出字节，不能据此宣称所有显式大缓冲都受预算约束。
- `tools/DesignerHost/DesignerRequestProcessor.Batch.cs:211` 对计数 scratch 固定预留4096；419行 `Ensure(sizeHint)` 可分配更大的数组，没有追加预留。大字段名/错误消息触发 JSON writer 大块请求时，固定预留不足。224行 CSV 额度只按 UTF-8 字节乘3，425—437行却同时涉及 StringBuilder、ToString 的 UTF-16副本和 UTF-8输出，不能完整覆盖声明的构造过程。

影响：结果可能最终被拒绝，但拒绝前仍分配超出已预留额度的工作缓冲；高压缩率的大清单/错误表无法依靠现有逻辑实现承诺的资源预检。当前没有实测 OOM，不能把该缺口写成已发生 OOM。

建议与验收：在生成前进行有界计数，按明确的生命周期预留显式缓冲；计数 writer 扩容必须先申请相应额度或使用真正固定容量的计数机制。CSV 可流式输出以减少副本。前端将最终输出限制与工作空间计量明确区分，保留已有32MiB产品边界，不以事后拒绝代替预检。使用小预算验证“超限拒绝前未分配目标大缓冲”，充分预算下结果保持一致；取消、失败释放和无部分 ZIP 行为不回归。

### P3-R8 — P2 — 32 MiB 边界证据及文档结论仍不充分

状态：**OPEN_ACTIONABLE**。

`web/Bing.EasyPrint.Designer/src/batch.test.ts:134` 使用4096字节页面，在32MiB限额下成功，只能证明普通成功路径；147行仍把1字节 Blob 的 size 改为32MiB+1，只能证明入口分支。两者均不能证明真实内容在32MiB附近的最终合并边界。小限额的最终 metadata 测试有效，但不是完整的既定规模常量确认。

`docs/batch-generation.md:29` 声称合并缓冲、UTF-16构造和最终文本均被累计计入，与 R5 源码不符；阶段报告把“32 MiB”列入已验证边界亦过宽。

建议与验收：保留有效的小预算和取消测试，补一次有界真实内容的32MiB邻近接受/拒绝证据，明确命中的是输出还是工作预算门禁，不伪造 Blob.size。对 R5 添加能否定当前分配顺序的测试。随后按实际验证范围修正文档，不将4KB成功称为32MiB边界通过。无需大型性能矩阵。

## 本次独立验证

影响分析：只复核 Templates 数字格式、Host raw 数据与批量缓冲、前端 ZIP合并；跨层风险HIGH，无公共签名、TFM、版本、依赖改动。主代理未修改生产代码/测试，未重新运行全量门禁。

主代理证据目录：`artifacts/optimization-expansion-20261025/phase3/main-review-r3/`。

- `node .../remaining-probes.cjs`：exit0，大数/未用大数/f90均200，8500预算拒绝9080字节metadata。
- `node .../probe.cjs`：exit0，前端/API精度、offset、日期拒绝、份数、partial、CSV/JSON及头/manifest一致性重放。
- `node .../allocation-order.cjs`：exit0，`allocation-order-results.json` 记录预算拒绝前的实际编码分配顺序。为定向诊断，不冒充完整内存测量。
- HTTP 使用交付 Host DLL 与隔离 .NET10 SDK中的 dotnet，运行时10.0.13；诊断端口5197进程已停止。
- `git diff --check`：exit0；仅现有CRLF→LF提示，没有空白错误。未提交、推送或发布。

全量验收证据和首次失败应同时保留；重跑通过不能改写第一次结果。执行方报告及 `integration-r3-*` 原始件是阶段门禁来源，主代理上述探针是独立反例复核，二者不混写。

luna_worker 只读核对原始件：

| 门禁 | 结果 | phase3下的证据 |
| --- | --- | --- |
| Core net10 / net6 / netcoreapp3.1 | 最终各4666通过、0失败、0跳过 | `integration-r3-core/core-net10.trx`、`integration-r3-runtime-net6/net6.0.trx`、`integration-r3-runtime-c31/netcoreapp3.1.trx` |
| Host | 317通过、0失败、0跳过 | `integration-r3-host/designer-host-net10.trx` |
| 五框架XML | 10/10 | `integration-r3-xml/verification.json` |
| Vitest / Chromium | 448 / 206通过；定向E2E5通过 | `integration-r3-designer-vitest.stdout.log`、`integration-r3-e2e-final/chromium.stdout.log`、`integration-r3-e2e-fieldBatch/chromium.stdout.log` |
| 模板 / 业务案例 | exit0；105根目录/134总文件；业务3成功0失败3页 | `integration-r3-template-samples.exit-code.txt`、`integration-r3-business-batch/` |
| 候选包消费 | 三运行时PASS、exit0、CandidatePackagesMatched=True | `integration-r3-modern/20261025-192846-b53fa83b/consumer-verification.json`、`integration-r3/consumer-net6/consumer-verification.json`、`integration-r3/c31/consumer-verification.json` |

消费运行时为10.0.13、6.0.36、3.1.32。Chromium workers=1、retries=0。模板文件数不是用例数。

**首次Core全量不是全绿**：`EscPosCanvasCancellationTests.Cancellation_during_fill_restores_the_previous_bitmap` 在第69行预期取消异常、实际无异常；首次TRX/日志保存在 `integration-r3-core-first-failure/`。最终全量该用例通过，但这不抹掉首败。测试在首次像素写入后发取消，呈现时序敏感；执行方称另有隔离重跑通过，盘点未找到单独r3隔离日志/TRX，因此只采信可定位的最终全量重跑。第2轮71项首败及业务首次exit1只有文字原因，原始日志未保存，继续如实保留该历史证据缺口。

## 停止与后续状态

- Completed：O1—O4、F1；P3-R1/R2/R3/R4/R6/R7。
- Open Actionable：F2 的 R5/R8；F3—F6仍是已授权但尚未实施的后续阶段。
- Blocked Approval：无新增产品决策要求。
- Blocked External：Windows/Linux Actions 实际执行证据。
- Not Applicable：实机验收、发布。
- Accepted Limitations：无持久队列；业务接口接收对象数组；批量入口拒绝旧job。
- Verified Boundaries：8500限额拒绝9080字节最终metadata、第二块合并取消及先前有原始证据的块/记录/页数边界。**不包含完整32MiB工作空间边界。**
- Deferred：设备传输、复杂字体塑形、富文本、更多厂商协议实现、图片依赖大版本迁移。
- No-Progress：CHANGED，R1/R2关闭，R5输出检查有进展；停止原因是已达到用户计划三轮上限，并非没有进展。
- Next Action：STOP；保留上述具体问题和验收条件供后续续作，不自动派发第四轮或扩大到下一阶段。

执行方称未找到 GOAL_RULES；主代理可读取用户级 `$CODEX_HOME/GOAL_RULES.md`，本次仍按该规则和更明确的项目计划停止。执行方环境差异不豁免轮次及证据约束。
