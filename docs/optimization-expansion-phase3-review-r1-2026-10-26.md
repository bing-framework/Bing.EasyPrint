# 第三阶段：集成修正第 1 轮独立复核

日期：2026-10-26。结论 **NEEDS_FIX**。P3-R3、R4 CLOSED；P3-R1、R2、R5、R6、R7、R8 仍 OPEN_ACTIONABLE。整体保持 4/10，下一步为集成修正 **2/3**，不进入 F3—F6。

## 已确认的进展

- R3：HTTP 记录 0 指定 3 份、记录 1 缺省，现正确输出 3+1 共 4 页；源码冻结 baseCopies，严格整数校验及对应测试已加入。
- R4：危险原型路径明确拒绝；坏引号拒绝；含分隔符的全空记录保留，后续序号不再前移。
- R1/R2：原先默认文化高精度及带 offset 输入、Q2 诊断、直接缺年月日配置已修正，但还不能据此关闭完整契约。
- R6：任务标识、独立 controller、取消后最终发布检查、卸载清理及控件禁用已实现；原结果复活问题得到修正，仍有快照遗漏。
- R7：资源/取消/InvalidOperationException 沿链拒绝，真实后端资源和内部异常测试已增加；普通编码错误分类出现反向回归。

## 本轮必须继续修正的事项

### P3-R1：保留数字类型和旧无格式语义，不能把“无损”实现为换类型

位置：`web/Bing.EasyPrint.Designer/src/losslessJson.ts`；Host `ParseDataToken`；App/batch 数据解析和序列化。

实际反例：`123456789012345.6789` 使用 Culture=de-DE、Pattern=0.0000。直接 HTTP JSON 数字显示 `123456789012345,6789`，通过实际前端解析器后变成字符串，Host 按德国文化解析成 `1234567890123456789,0000`。JSON 数字与用户输入的区域数字字符串不可混淆。

前端扫描器还把非法 JSON 数字 `1234567890123456e`、`-1234567890123456.` 引号化成字符串并接受。必须先遵守 JSON 数字语法，不能修补非法输入后宣称解析成功。

Host 全局禁用日期解析还改变了无 Format 旧路径：相同日期输入，旧 `JToken.Parse` + Invariant 转换显示 `01/02/2024 16:04:05`，当前为原 ISO 文本 `2024-01-02T03:04:05-05:00`。新默认更直观也不能无授权改变已承诺的兼容行为。

验收：原 JSON 数字以数值语义无损传到格式化，跨文化与直接 API 相同；显式区域字符串仍按配置文化解析。选择可区分的内部表示/请求序列化方式，不能全局按字符串替代数字。非法 JSON 原文拒绝。覆盖无 Format 旧数字/日期及条件绑定，确认兼容；格式化入口可保留原值但不能全局改变旧行为。不得通过要求用户手动改输入规避。

### P3-R2：日期格式需要识别有效 token，预算仍需可信上界

位置：`BgFieldFormatter.ValidatePatternSyntax/ParseDate/EstimateCore`。

实际反例：InputFormat=`'y'MM-dd`、输入 `y01-02` 仍输出 `2026-01-02`；Contains('y') 把字面量当成年份。InputFormat=`yyyy-MM-dd'T'HH:mm:ssK`、无 offset 输入 `2024-01-02T03:04:05` 仍输出 `+08:00`，DateTimeOffset.ParseExact 为可选 K 补入本地时区。

修正需要识别引号/转义/标准格式及输入中实际存在的年月日/offset。输出时区字段也必须检查实际 offset，不能仅凭 Pattern.Contains 判定。没有 offset 时可拒绝时区输出或使用明确固定语义，不能隐式读本机时区。

资源预留顺序已前移，短巨大 precision 已拒绝；但 `Pattern.Length*2+128` 仍非通用上界，日期校验用 ToString 先分配，Text 对任意非字符串只估 256。应按实际支持的语法和输入做有依据的长度/工作预算，保留合法短结果；不得用把 MaxOutputBytes 从小值提高到1024的测试证明低预算兼容。

### P3-R5：预算口径不一致，metadata 仍能绕过限额

实际 HTTP：同一成功批次 header RemainingBytes=33553414，而 manifest RemainingBytes=33554270，分别扣 ZIP 与协议字节。直接消费者按清单继续与浏览器按响应头继续会得到不同累计限制。

实际低额度反例：Field.Name 是 12000 个 x，data={}，remainingBytes=10000，返回 200；解压后 manifest=24456 字节、errors.csv=24140 字节，共 **48596 字节**，ZIP=1157 字节。固定 errors.Count*4096 估计没有覆盖真实错误长度，且 SerializeToUtf8Bytes/StringBuilder 没有将实际 metadata 生成前的空间计入资源作用域。

前端 workingBytes 只累计当前遍历到的块和页面数组；manifest/errors 解压、解析、合并序列化及最终压缩缓冲未计入，解压后才检查 bytes 长度。所有 chunks 从一开始已在内存，不能逐个加入时才认为其被持有。最终 ZIP 检查虽已加上，但不能替代工作内存/累计原始数据约束。

验收：定义并统一 header/manifest 的计量；协议原始输出、元数据、最终 ZIP 和工作缓冲各自有明确约束及提前预留/释放。错误消息长于固定估计也应受控拒绝。调用方使用任一文档化结果继续分块必须得到一致剩余额度。补低额度实际合并、metadata/全失败、压缩前后差异，以及既定 32 MiB 的判别性边界；不放宽限制。

### P3-R6：运行快照只浅拷贝，模板编辑仍会混入下一块

位置：`BatchPanel.vue` 的 `sourceBase={...props.baseBody}` 和 `sourceRecords`；`App.vue` batchBaseBody 经 buildPreviewBody 直接保留 `template.value` 引用。

主代理执行真实 BatchPanel script 的 run，控制第一块的 HTTP promise：第 0 块 template.Field.Value=BEFORE，在等待期间修改外部模板，第二块 offset=25 变为 AFTER。诊断替换网络及 ZIP 合并，不是浏览器 E2E；实际问题来自源码浅引用。全局属性面板仍可编辑模板，禁用 BatchPanel 本身控件不足以解决。

验收：运行固定完整模板/资源/协议配置/记录数据及份数，使用有预算的独立快照，或变更时可靠取消；不在请求发送时读取外部可变嵌套值。真实多块测试在第1块后修改模板/协议嵌套配置，后续必须保持原快照或取消，不能混合输出。取消/重启/卸载已修正的行为保留。

### P3-R7：严格分类误将普通编码错误升级为整批失败

实际 HTTP：EAN13 模板（Type=4, BarcodeType=5, Height=40），记录 0 `6901234567892`，记录 1 `abc`。当前整批返回400/TemplateInvalid“该制式仅接受 ASCII 数字”，丢弃首条成功输出；契约要求 partial、失败记录明细且保留成功条目。源码 IsRecordFailure 不再接受由正常输入校验抛出的 ArgumentException/TemplateRenderException。

验收：区分已知输入/编码错误与内部异常，而不是只扩大或缩小异常类名单。至少合法+非法 EAN/QR/格式/绑定分别得到 partial；真正内部异常、资源或取消在第一条完成后中止，不返回 ZIP，后续不执行且资源释放。维持当前真实后端异常回归；反射分类测试不能替代整个批量路径。

### P3-R8：补齐剩余验收并纠正报告归属

真实 26 条 JSONL 两块、40块共1000记录/页测试已有进展；1000循环仅累计页数，字节额度每块重置默认。前端 merge 测试仅 maxBytes=1，在第一块 ZIP 大小检查处失败，不验证最终合并、解压/metadata、取消或32MiB边界。取消浏览器用例为3条请求前延迟取消，不是报告所说26条块间取消。

补本报告 R1/R2/R5/R6/R7 的判别性回归、整个字节累计、合并中取消、格式保存再导入后的高精度/类型/offset 一致性，以及可独立运行的业务批量消费示例。所有代码路径至少有可复核证据或明确未验收状态，不以参数声明代替执行。保留首次失败和具体命令/退出码，不重复未受影响的全量测试。

执行方复制并运行 main-review/probe，属于“执行方重放主代理编写的探针”，不能称“主代理本轮验证通过8项”；该脚本只是打印结果且不覆盖全部8项。主代理当前独立证据见下文。

## 验证证据、影响与状态

证据根目录：`artifacts/optimization-expansion-20261025/phase3/main-review-r1/`。

- `probe.cjs` / `results.json`：当前 Host 真 HTTP 与真实前端源模块，exit 0；包含当前成功与失败行为，不将 exit0当全部验收通过。
- `snapshot-probe.cjs` / `snapshot-results.json`：真实组件脚本+受控网络/合并替身，显示 BEFORE→AFTER 混批；未模拟组件挂载，onUnmounted 警告与该诊断无关，不用于声明卸载验收。
- `metadata-budget.json`：上述12000字符字段名与10KB额度请求，记录响应和解压实际字节。
- `legacy-parsing.json`：PowerShell .NET10.0.8 下原 JToken/Invariant 路径对照；不是重新构建历史版本的全量测试。

HTTP Host 为 SDK10.0.402/runtime10.0.13 的交付 DLL；诊断进程已停止。`git diff --check` exit0。主代理不改生产/测试，未重复全量门禁。

luna_worker 原始证据盘点：Core 三个真实运行时各4663/4663，Host308/308，XML10/10，Vitest432/432，Chromium206/206（命令含workers=1/retries=0），定向5/5，样例exit0；最终 `EasyPrintModern-20261025-181507-e0fe517b` 候选在net10/net6/3.1消费均PASS/exit0且包哈希匹配。modern目录还有中间候选，不能混用其记录；最后相关源码约02:07、最终包/消费约02:15，时间支持新鲜度，不声称历史工件有源码树内容快照。Host已覆盖第二条抛取消和首条成功后的资源异常，内部异常目前为单条；32MiB实际边界和合并中预算仍缺判别性证据。上述原始通过不替代本轮反例。

Change Impact：Templates格式/绑定、Host数据与批量计量/异常、前端无损数据/快照/合并；风险HIGH。先L1逐项反例及回归，再L2/L3受影响项目；阶段候选执行规定门禁。生产TFM/依赖大版本/版本/API基线不变；不跑无关L5矩阵。

- Implementation：4/10；本阶段2组CLOSED、6组OPEN_ACTIONABLE。
- Completed：R3/R4、其他问题的上述局部修正；不重复打开已关闭根因。
- Open Actionable：R1/R2/R5/R6/R7/R8。
- Blocked Approval：无。
- Blocked External：Windows/Linux Actions实际执行。
- Not Applicable：实机验收、发布。
- Accepted Limitations：无持久队列，批量API对象数组和旧job拒绝不变。
- Verified Boundaries：已通过的单块25/26、累计1000页、低额度拒绝仅按实际测试范围引用。
- Deferred：总计划明确的传输/塑形/富文本/厂商协议/图片迁移；F3—F6仍为后续待实施。
- No-Progress：CHANGED，已关闭两项；其他问题有代码变更但仍有明确同根因反例，不扩大范围。
- Next Action：OpenCode/deepseek-v4.1-flash 修正2/3，证据用 integration-r2-*，复核后再决定收口。总任务RUNNING。
