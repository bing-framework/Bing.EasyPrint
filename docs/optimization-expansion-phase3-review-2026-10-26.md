# 第三阶段 F1/F2 初版独立复核

日期：2026-10-26。结论 **NEEDS_FIX**：8 组 OPEN_ACTIONABLE，F1/F2 不关闭，整体仍为 4/10。本次为初版复核，下一轮是本阶段集成修正 **1/3**；不进入 F3—F6。

主代理读取真实代码并运行小规模 HTTP/API/解析器诊断；luna_worker 只读核对原始证据。没有修改生产代码或测试，没有重复全量门禁。诊断 Host 使用交付的 Debug/net10.0 DLL、隔离 SDK 10.0.402/runtime 10.0.13，端口 5197，进程已经停止。

## 问题与验收条件

### P3-R1 / P1：原始业务值在格式化前已经丢失精度或时区

位置：`tools/DesignerHost/DesignerRequestProcessor.cs:953`、`DesignerRequestProcessor.Batch.cs:105` 的默认 `JToken.Parse`；`web/Bing.EasyPrint.Designer/src/batch.ts:154`、`:173` 的 JSON.parse 及设计器业务 JSON 请求链。

实测：HTTP 数值 `123456789012345.6789`、格式 `0.0000` 显示为 `123456789012346.0000`。HTTP 日期字符串 `2024-01-02T03:04:05-05:00` 显示成 `2024-01-02 16:04 +08:00`。前端 JSON 数值重新序列化已变为 `123456789012345.67`。直接 C# 字符串单测不能证明真实 JSON 链保持 decimal/offset。

修正与验收：保留原始数字和日期语义直到权威格式化；覆盖业务 JSON 编辑、JSON/JSONL 导入、预览及批量，不仅改服务端。不得将全部类型无差别转成字符串破坏既有绑定/条件。无格式配置的旧默认输出应有回归证据。高精度 JSON 数字、带不同 offset 的 ISO 文本端到端精确往返，固定不同文化/时区下结果一致。

### P3-R2 / P1：格式预检晚于分配，且格式错误与日期缺省不符合契约

位置：`src/Bing.EasyPrint.Templates/Rendering/BgFieldFormatter.cs:56`、`:74`、`:172`、`:203`。

数字 ToString 在预算预留之前执行；只限制 Pattern 字符数，`F999999999` 这样的短精度配置可请求巨大字符串，固定 `Pattern.Length * 2 + 512` 不是正确上界。未实际执行危险格式；源码已明确分配顺序。实测 `Q2` 返回 HTTP 500/InternalError，字段/元素路径均为空；`InputFormat=MM-dd`、值 `01-02` 输出 `2026-01-02`，补入了当前年份。

修正与验收：先验证格式语法、精度和可信输出上界并预留预算，再生成字符串；不得以后台超时或扩大预算规避。非法格式归入带字段/元素路径的输入错误，批量中保留对应记录诊断。日期输入缺失年月日或 offset 时明确拒绝或使用文档化固定语义，不依赖系统当前日期、时区；输出 offset 格式也不得从 unspecified DateTime 隐式取本地时区。用安全的小额度/计数证明高精度配置分配前被拒绝，覆盖低预算下合法短结果，防止保守估计误拒绝所有数字格式。

### P3-R3 / P1：批量份数跨记录泄漏，并接受非整数份数

位置：`tools/DesignerHost/DesignerRequestProcessor.Batch.cs:94`、`:114`、`:250`。

实测：记录 0 设置 copies=3、记录 1 未设置，返回两条都是 3 份、总计 6 页，后者应使用请求默认 1 份，总计 4 页。原因是 options.Copies 被逐条改写，又作为下一条 fallback。copiesField=1.6 被 Value<int> 四舍五入为 2 并成功输出。

修正与验收：冻结请求级默认值，每条仅从自身和不可变默认值取份数；严格接受合法整数，不对小数/布尔值隐式转换。覆盖覆盖值→缺省值、失败记录→下一条、请求 copies/copiesField/记录 copies 冲突及内置/注册协议的实际输出和物理页数。

### P3-R4 / P1：CSV 映射污染原型，解析器静默改写非法或空记录

位置：`web/Bing.EasyPrint.Designer/src/batch.ts:259`、CSV 状态循环及 `row.some` 过滤。

实测：映射到 `__proto__.easyPrintReviewProbe` 后，全新 `{}` 可读到注入值（诊断进程已清理该属性）。`"x"oops,y` 被接收为 `xoops/y`；未引用字段中的引号也被接受。输入 `a,b\n,\nx,y` 丢掉第一条合法空字段记录，后续序号改变。

修正与验收：映射只遍历自有数据属性，隔离/拒绝危险原型路径，并明确路径冲突规则；不能修改 Object.prototype。CSV 使用严格引号状态，拒绝闭引号后的非分隔字符等畸形结构；保留有明确字段分隔的全空记录和原始序号，区分空物理行。覆盖 BOM、引号内换行、空字段、嵌套映射冲突、原型字段等输入。

### P3-R5 / P1：累计输出和合并临时内存未形成完整预算约束

位置：`DesignerRequestProcessor.Batch.cs:189`—`:223`；`web/Bing.EasyPrint.Designer/src/batch.ts:89`；`components/BatchPanel.vue:164`。

服务端 RemainingBytes 仅扣协议输出，不扣清单/错误表/归档开销；metadata 先通过 SerializeToUtf8Bytes/StringBuilder 分配，再交给受预算流。客户端将每块 ZIP、解压数组、合并归档同时保留，没有累计临时预算；`mergeBatchArchives` 和调用方均未检查最终 ZIP 大小，与报告“合并后再次检查 32 MiB”不符。全失败块输出计数为零但仍有错误数据，需明确计费。

修正与验收：明确原始输出、ZIP/元数据、临时工作内存的计量并在分配/合并前执行；客户端检查实际累计数据和最终结果，不只信剩余额度头。保持既定 32 MiB 上限，不放宽默认值。用可控小额度验证多块累计、不可压缩数据/大量错误/metadata 跨界、最终 ZIP 超限及无残缺下载；按计划证明 32 MiB 精确边界。避免将整批界限误实现为每块界限。

### P3-R6 / P1：取消及编辑中的批量任务会恢复旧结果或混用新数据

位置：`web/Bing.EasyPrint.Designer/src/components/BatchPanel.vue:83`、`:106`、`:129`、`:147`、`:164`、`:183`。

源码证据：merge 不接收 AbortSignal；await merge 后无取消/任务身份检查而直接发布结果。cancel 将状态立即设为 cancelled，可重新开始；旧任务 finally 会清空共享 abortController，新旧任务会相互影响。循环每块重新读取 selectedRecords/copiesField，而选择/导入/映射未禁用；reload 可将仍运行任务设 idle。映射错误保留上一份 records，run 不检查 parseError，可能打印旧映射。

修正与验收：每次运行捕获不可变输入/设置和独立 controller/身份，取消、卸载、重新导入或新任务使旧结果失效；合并期间观察取消，最终赋值前再次检查。修改中禁止相关操作或安全取消，不可混批。无效映射阻止生成。用受控 promise 覆盖请求中/块间/合并中取消、立即重启、旧 finally、选择/映射变化；真实浏览器验证至少取消后无结果复活。

### P3-R7 / P1：嵌套资源/取消/内部异常被当成普通记录错误继续

位置：`tools/DesignerHost/DesignerRequestProcessor.Batch.cs:268` 的 IsRecordFailure。

直接调用实际分类器：TemplateRenderException 包装 PrintResourceLimitException、InvalidOperationException、OperationCanceledException 时均返回 true。特别是资源分支即使未进入 true，最终 `error is ArgumentException` 仍命中（TemplateRenderException 继承 ArgumentException）。声明的 InternalErrorException 没有实际包装调用，无法保护后端异常。

修正与验收：优先沿异常链识别取消、资源超限和真实内部失败，整批终止；只白名单明确的输入/绑定/编码失败可按记录继续。不能仅按最外层 ArgumentException 判断所有错误。注入真实注册后端/布局失败，验证在部分成功后遇到资源、内部异常和取消都没有可下载部分 ZIP，剩余记录未编码，预算及资源释放；同时普通坏记录仍产生 partial/all_failed。

### P3-R8 / P2：验收覆盖与报告边界声明过宽

当前批量 Host 7 项：25 条成功、26 条单块拒绝；1000 仅是 offset=975 的最后 25 条声明，并非实际完成 1000 条。取消只在调用前取消；没有批量中途取消/内部异常测试。浏览器新增 3 项不覆盖真实 26 条分块、JSONL 多块及保存再导入后的权威格式显示。

修正与验收：补齐实际 1/25/26/1000 分块、1001 拒绝、跨块限额、混合/全失败、取消和内部异常。大边界可轻量后端，至少小批次真实编码；不要求无关性能矩阵。补真实 Host JSONL/多块、格式保存重载、旧 Host 降级以及可运行的业务批量调用案例。明确代码实现/测试通过/未验证三者，不把 totalRecords 参数校验当完整流程。

## 已有证据与复用

原始门禁实数已核对：Core net10/net6/netcoreapp3.1 各 4661/4661，实际运行时 10.0.13/6.0.36/3.1.32；Host 299/299；严格 XML 10/10；Vitest 429/429；Chromium 204/204；模板根 105、总 134 个文件、exit 0；三种候选包消费 PASS/exit 0，包哈希一致。这些通过不能覆盖上述反例。

主代理证据：`artifacts/optimization-expansion-20261025/phase3/main-review/probe.cjs`（命令 `node <该文件>`，exit 0）、`results.json`、`date-missing-year.json`、`error-classification.json`。分类器诊断使用 PowerShell .NET 10.0.8 加载交付程序集，以反射调用实际 IsRecordFailure，不冒充 10.0.13 全量验证。危险格式没有执行大分配；UI 竞态及合并预算为直接源码证据。

原始 E2E 三份全量日志均成功，没有发现失败运行；报告“映射修正后”应说明触发来源，不能补造首次失败。日志能证明单 worker，但需在后续记录完整命令证明 retries=0；前端 build 保留进程退出码，区分 PowerShell 包装的 stderr 警告与真正失败。最终证据时间跨到 10/26，报告应注明日期。不要为了补历史命令/退出码重复所有门禁。

## 变更影响与下一轮

- ChangedProjects：Templates 绑定与格式化、Host 数据/批量执行、前端数据/批量工作流及相关案例/手册。
- ChangedPublicContracts：既有新增配置保持增量，不破坏旧无格式默认行为；必要的批量预算字段明确文档化。
- ChangedRuntimePaths：数值/日期解析、份数、输入解析、资源预检、异常/取消、分块合并。
- ChangedTFMs / BuildPackaging：无；五框架、版本、依赖大版本、API 基线不变。
- RiskLevel：HIGH。先逐项 L1；共享路径和阶段收口再执行 L2—L4。不运行 L5 大型矩阵。
- Completed：初版主体和已有门禁；本轮复核/证据盘点。
- Open Actionable：P3-R1—R8，关联 F1/F2，按紧密相关根因修复。
- Blocked Approval：无。
- Blocked External：外部 Windows/Linux Actions。
- Not Applicable：实机验收、发布。
- Accepted Limitations：无持久队列；HTTP 接收 records 对象数组，客户端解析 CSV/JSONL；旧 job 明确拒绝。
- Verified Boundaries：当前仅有已实测的单块 25/26、低剩余额度拒绝等，不宣称全部边界完成。
- Deferred：总计划中明确延期的传输、塑形、富文本、厂商协议、图片依赖迁移。F3—F6 是后续待实施，不是延期。
- No-Progress Check：CHANGED（初版交付并定位具体反例）。
- Next Action：OpenCode / deepseek-v4.1-flash 修正第 1/3 轮；使用 `phase3/integration-r1-*` 保存新证据，保留初版产物。主代理复核后决定收口或剩余修正。
- Goal Status：RUNNING；Implementation 4/10。
