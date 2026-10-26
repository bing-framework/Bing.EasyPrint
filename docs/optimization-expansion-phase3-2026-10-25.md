# 第三阶段 F1/F2 实施报告

日期：2026-10-25。范围：第三阶段 F1 字段格式化、F2 批量生成。状态：**历史 3 轮后按用户续作要求完成 F2 R5/R8 资源预检收口（续作第 3 轮已完成输入解压单缓冲与压缩/输出副本预算），待主代理验收；F1 已 CLOSED，F2 保持 OPEN_ACTIONABLE；未提交、未推送、未发布**。F3—F6 未进入。外部 CI 保持 `BLOCKED_EXTERNAL`，实机验收 `NOT_APPLICABLE`。

## Change Impact Analysis

- ChangedProjects：Templates、Core tests、DesignerHost、DesignerHost.Tests、Designer Web、Templates samples。
- ChangedPublicContracts：`BgField.Format`/`BgFieldFormat` 为增量模板契约；Host 新增 `/api/designer/batch`、`field-format`、`batch-export` 特性和批量响应头。未改变既有公开成员签名、默认行为、生产 TFM、版本或依赖。
- ChangedRuntimePaths：模板字段绑定显示文本、Host 预览显示值、批量分块渲染、ZIP/manifest/errors 生成、前端批量解析与合并。
- ChangedProviders：未修改内置 Provider 编码；批量复用现有协议选择和 O4 注册协议/收尾输出。
- ChangedTFMs：Core/Templates 生产五框架不变；Host 保持 net10.0。
- ChangedBuildPackaging：未改产品版本、API 基线或 ImageSharp 依赖。
- RiskLevel：HIGH（跨 Templates/Host/前端共享契约和批量资源边界）。

## F1：字段格式化

实现：

- `BgField.Format` 是可 JSON 往返的 `BgFieldFormat`：`Text`、`Number`、`Currency`、`Date`、`Enum`。
- 真实绑定入口 `Binding.Text` 对所有 `BgField`（含条码、QR、Composite 主/次数据）应用格式；无 `Format` 完全走旧 `Convert.ToString(InvariantCulture)` 路径。
- 固定顺序：取值 → 实际 `null` → 类型转换/枚举映射 → 格式化 → 补齐 → 前后缀。空字符串不等同 null。
- 数字使用 `decimal`；默认 InvariantCulture；文化必须显式配置。日期使用显式 Pattern/InputFormat，保留 offset，不读取当前时间，不做隐式本地时区转换。
- 枚举未命中、数字/日期转换失败、配置越界、输出超过当前文本预算均明确失败，并保留绑定字段路径/模板元素路径。
- Host 预览布局返回 `displayText`（与 `text` 同源），前端画布优先使用该权威值；前端只编辑格式配置，不复制金额/日期算法。
- 设计器 `FieldFormatEditor` 支持主字段及 Composite 次字段编辑；项目 v1/v2/裸模板和 JSON 保存沿既有路径保留 `Format`。
- 旧 Host 缺少 `field-format` 时，客户端明确拒绝请求，不静默打印未格式化值。

定向验证：

- `tests/Bing.EasyPrint.Tests/FieldFormatTests.cs`：顺序、decimal 精度、日期 offset/文化、null/空串、枚举映射、失败路径、JSON 往返、QR 共享字段。
- `tests/DesignerHost.Tests/FieldFormatHostTests.cs`：真实 Host `displayText`、特性协商、无效格式 400 + 字段路径。
- `web/Bing.EasyPrint.Designer/e2e/fieldBatch.spec.ts`：设计器编辑格式、真实 Host 预览、保存 JSON 往返。
- `web/Bing.EasyPrint.Designer/src/api.test.ts`：缺 `field-format` 时旧 Host 降级。

## F2：批量生成

实现：

- 新增 `POST /api/designer/batch`，单块最多 25 条、整批最多 1000 条、实际物理页最多 1000、累计结果最多 32 MiB；服务端校验 `recordOffset`、`totalRecords`、`remainingPages`、`remainingBytes`。
- 同一批次逐条复用模板解析后的选项、协议选择和渲染逻辑；自定义协议继续使用实际物理副本、source/copy、segments 和独立收尾。
- `copies`/`copiesField` 二选一；与大于 1 的基础 copies 冲突时明确拒绝；旧 `job` 在批量入口明确拒绝，不静默相乘。
- 输入/绑定/编码错误按记录汇总；内部异常、取消和整批资源超限终止任务。达到页/字节限制不提高预算，不返回截断成功包。
- ZIP 使用 `records/000000/` 稳定目录，保留页面 segment 和独立收尾条目；`manifest.json` 记录 status、记录、文件、物理页和剩余额度；`errors.csv` 做公式起始字符和 CSV 引号/换行转义。
- 全失败状态为 `all_failed`，只有错误报告；部分成功为 `partial`。UI 分开显示，不把全失败标成成功。
- 前端 `batch.ts` 支持 UTF-8 CSV（BOM、分隔符、引号、嵌入换行、重复表头/列宽错误）、JSON 对象数组、JSONL；CSV 默认字符串、保留前导零并可编辑字段映射。
- `BatchPanel.vue` 提供导入、映射、记录选择、份数字段、真实 Host 预览、进度、取消、失败筛选、结果下载；每 25 条顺序提交，合并 ZIP 后再次检查 32 MiB。
- 取消清空结果且不自动下载；合并/失败状态不伪造成功。

定向验证：

- `tests/DesignerHost.Tests/BatchHostTests.cs`：1/25/26/1000 边界、混合成功失败、全失败、页/字节预算、copiesField、自定义协议 PageEnd 独立收尾和稳定文件顺序。
- `src/batch.test.ts`：CSV/JSON/JSONL、BOM、前导零、映射、重复/畸形结构、公式转义。
- `src/api.test.ts`：`batch-export` 协商与分块请求契约。
- `e2e/fieldBatch.spec.ts`：真实 Host CSV 映射批量、部分失败、ZIP manifest/errors；另验证格式编辑与 Host 显示。

## 集成修正第 1 轮（2026-10-26）

依据 `docs/optimization-expansion-phase3-review-2026-10-26.md` 的 8 项 Open Actionable 完成修正；复核文档本身未修改。

| 复核项 | 修正 | 判别性验证 |
| --- | --- | --- |
| P3-R1 精度/时区 | Host 模板数据改用显式 `JsonTextReader`（`DateParseHandling.None`、`FloatParseHandling.Decimal`），禁止隐式 `DateTime` 转换；前端新增无损 JSON 解析，高精度数字按字符串保留，普通数字仍为 number。 | 高精度 decimal 经预览、批量、前端保存往返后保持原值；`-05:00` offset 不被本地时区改写。 |
| P3-R2 格式失败与预检 | 格式化前验证 Pattern、精度、日期输入和补齐长度，先预留再写出；仅 `BgFieldFormatException` 包装为 `TemplateRenderException`，无 `Format` 旧绑定仍抛原 `ArgumentException`；日期 `InputFormat` 必须同时包含年、月、日。 | `MM-dd`、`yyyy-MM` 等缺日输入明确 400；无格式旧绑定异常类型不变；格式失败不产生部分输出。 |
| P3-R3 份数基线 | `copies` 基线保存为不可变请求默认值，记录级 `copies` 不泄漏到下一条；`copiesField` 只接受严格整数。 | `copies=3` 后下一条默认 1，总物理页 4；`1.6`、bool、null 均 `all_failed`/`BatchInvalidCopies`。 |
| P3-R4 CSV/原型安全 | CSV 严格校验引号闭合；保留含字段分隔符的全空记录并跳过纯空物理行；字段路径拒绝 `__proto__`、`prototype`、`constructor`。 | 畸形引号失败；`,` 空记录不被丢弃；原型路径 400，frontend 解析抛错。 |
| P3-R5 批量预算 | 服务端在渲染前按记录元数据预检页/字节，最终 ZIP 再校验；客户端合并 ZIP 前累计实际预算并受取消信号约束，避免先渲染后超限。 | 低字节预算在预览/批量前拒绝；剩余额度随分块递减，最终包不超 32 MiB。 |
| P3-R6 取消与运行快照 | 批量 UI 固定本次运行快照、`runId` 和 controller；取消、卸载或新任务立即失效旧请求，旧响应不能复活或触发下载；运行中禁用会改变输入的控件。 | 26 条分块中取消后无新请求/下载；重新运行使用新快照；卸载时终止在途任务。 |
| P3-R7 异常分类 | `IsRecordFailure` 沿内部异常链拒绝 `OperationCanceledException`、`PrintResourceLimitException`、`InvalidOperationException`；内部、资源、取消均终止整批，不降级为记录错误。 | 包装异常反射测试覆盖各终止类型；记录级输入/绑定错误仍可汇总。 |
| P3-R8 判别性测试 | 新增 Host/前端/E2E 用例覆盖上述反例，保留首次失败证据。 | Core、Host、Vitest、Chromium、样例及候选包全部重跑。 |

主代理反例已复制到独立目录 `phase3/integration-r1-main-review/` 重放（原 `phase3/main-review/` 未改写）：probe exit 0，确认份数不泄漏、高精度与 offset 保留、Q2 错误路径、CSV 原型污染拒绝；日期探针 `date-input-results.json` 另验证 `MM-dd`/`yyyy-MM` 均 400 且完整日期 200。原始失败记录保留在 `phase3/integration-r1-*` 首次运行日志中。

## 集成修正第 2 轮（2026-10-26）

依据 `docs/optimization-expansion-phase3-review-r1-2026-10-26.md` 修正 R1/R2/R5/R6/R7/R8；R3/R4 保持 CLOSED，不重复修改。

| 复核项 | 修正 | 判别性验证 |
| --- | --- | --- |
| P3-R1 数字身份/旧语义 | 前端改用严格 JSON 解析器和 `JsonNumber` 内部包装，只对高精度数字保留原始词法；请求序列化输出原始 JSON 数字，普通数字仍为 number，用户字符串仍为字符串。Host 同时构造 legacy（旧日期/Double）与 raw（ISO 原文/decimal）数据，格式化字段读 raw，无 Format 读 legacy。 | 非法 `1234567890123456e`、`-1234567890123456.` 拒绝；de-DE `123456789012345.6789` 显示 `123456789012345,6789`；无 Format 日期/数字保持旧输出。 |
| P3-R2 日期 token/预算 | 用引号/转义感知的 token 扫描验证年月日和 offset；`K` 输入无实际 offset 时拒绝；输出时区占位符检查实际 offset。按实际 decimal 位数、格式精度和 pattern literal 计算上界，日期 pattern 在预留后才执行 ToString 语法校验。 | `'y'MM-dd`、无 offset 的 `K` 输入 400；`+08:00` 正常保留；小预算 128 仍允许短结果并拒绝 `F100`。 |
| P3-R5 统一预算 | `remainingBytes` 统一为“协议原始输出 + manifest/errors UTF-8 字节”，响应头与 manifest 同值；manifest 用计数 writer 获取实际 JSON 字节，errors CSV 按实际 UTF-8 字符计算；元数据生成前预留工作预算。客户端先累计所有已持有 chunk，再用 ZIP 条目未压缩大小预检，合并后检查最终 ZIP。 | 10 KiB 配额下 12,000 字符字段名实际元数据超限返回 400；正常批次头/manifest 一致；全失败元数据、双块初始内存、合并取消和 32 MiB 边界有测试。 |
| P3-R6 完整快照 | 运行前深拷贝完整 base body、模板嵌套字段、协议配置和记录数据；保留 runId/controller/卸载失效。 | 主代理 snapshot probe 在首块等待期间修改模板和协议嵌套值，第二块仍为 BEFORE；浏览器 26 条跨块取消后无下载并可重启。 |
| P3-R7 分类来源 | 已知条码/QR/矩阵编码错误在元素边界用异常 Data 标记；`IsRecordFailure` 优先终止取消/资源/真实内部异常，只有标记输入错误或绑定字段错误按记录继续。 | EAN13 合法+`abc`、QR 合法+超长数据均 partial；首条成功后内部异常/资源/取消无 ZIP。 |
| P3-R8 验收补齐 | 1000 条 40 块累计字节和头/manifest 一致性；真实 26 条第二块取消；合并 metadata、初始 chunk、最终 ZIP 和取消测试；独立 `samples/BatchConsumer`。 | Core/Host/Vitest/Chromium 定向与业务样例均执行；保存再导入 de-DE 格式仍显示原精度。 |

主代理反例重放见 `phase3/integration-r2-main-review/results.json`；snapshot probe 见同目录 `snapshot-results.json`；业务消费输出与 exit code 在 `phase3/integration-r2-business-batch/`。
## 集成修正第 3 轮（2026-10-26，最后一轮）

依据 `docs/optimization-expansion-phase3-review-r2-2026-10-26.md` 修复 R1/R2/R5/R8；R3/R4/R6/R7 保持 CLOSED，未重开或扩大范围。

| 复核项 | 修正 | 判别性验证 |
| --- | --- | --- |
| P3-R1 未使用大数 | Host raw 数据不再 eager decimal，而是构建包含 `JsonRawNumber` 的原始词法树；只有格式化字段实际绑定时才转换为 decimal，溢出带字段路径。legacy 数据仍按旧 Double/DateTime 解析。 | 无 Format `Value=1e40`、`Value="OK",Unused=1e40` 均 200；格式化 `1e40` 字段 400 且定位 `Amount`；混合模板中未用大数不破坏首条成功。 |
| P3-R2 合法长度估计 | 标准数字格式统一大小写；Text 有符号 sbyte/short/int 最小值包含负号；长度估计按实际值和格式精度，先校验/预留再生成。 | 公共 Host 入口验证小写 `f90`、sbyte/short/int 最小值 200；小预算短结果通过，`F100` 和巨精度提前拒绝。 |
| P3-R5 最终 metadata/工作预算 | 合并函数在生成最终 ZIP 前重建并测量最终 manifest/errors UTF-8 字节，计入累计输出和已持有工作缓冲；仍检查最终 ZIP。Host 为计数 writer 固定数组、CSV UTF-16/UTF-8 输出预留工作预算。 | 两块各一条失败记录、2000 字符消息、`maxBytes=7000` 拒绝；当时 `20000` 成功且最终 metadata 4924+4156=9080 字节被计入（续作 R5 修正后 `20000` 改为先命中最终工作空间门禁，见续作节）；输入 metadata、初始 chunk、最终 ZIP 和取消分支均有测试。 |
| P3-R8 验收补齐 | 增加实际最终 metadata 超限、第二块合并期间取消、真实有界 32 MiB 成功分支；保留 1000 条/40 块、26 条跨块取消和 BatchConsumer 未变证据复用。 | 最终合并取消在第二块读取时触发；最终 metadata 超限在生成 ZIP 前拒绝；真实 4096 字节页面在默认 32 MiB 下成功。 |

执行方重放主代理 R1/R2/R5 探针见 `phase3/integration-r3-main-review/`；最终合并 metadata 探针 `metadata-final-results.json` 得到 9080 字节，7000 拒绝、当时 20000 成功（续作修正后 20000 先命中最终工作空间门禁），并验证第二块取消和真实 32 MiB 成功分支；边界测试见 `integration-r3-targeted/web-batch-test.stdout.log`。第 2 轮网络/构建首次失败日志未完整保存的历史不补造；本轮 Core 首次全量出现 1 项 `EscPosCanvasCancellationTests` 时序失败，原始 TRX/日志已保留在 `integration-r3-core-first-failure/`，隔离及全量重跑均通过。

## 续作第 1 轮：F2 R5/R8 资源预检收口（2026-10-26）

依据 `docs/optimization-expansion-phase3-review-r3-2026-10-26.md`，只处理 R5/R8；R1/R2/R3/R4/R6/R7 保持 CLOSED，不进入 F3—F6。

| 复核项 | 修正 | 判别性验证 |
| --- | --- | --- |
| R5 Host 分配顺序 | `CountingBufferWriter` 初始 4 KiB 和每次扩容都在 `new byte[]` 前按生命周期预留，并释放旧租约；`BuildErrorsCsv` 不再构造 StringBuilder/ToString/额外 UTF-8 副本，而是按实际 UTF-8 字节数直接写入预分配数组。 | Host 317/317；批量错误表/头/manifest 一致性回归通过。 |
| R5 前端最终 metadata | 在生成最终文本前无大结果分配地计算 JSON/CSV UTF-8 长度；不再先 `TextEncoder.encode`。`arrayBuffer` 临时缓冲、解码 manifest 模型、最终 metadata 和 64 KiB JSZip 流式压缩预留均列入显式工作预算；最终 ZIP 另检查。 | 8500 拒绝时 `TextEncoder` 已分配数组为空；9080 字节最终 metadata 在 120000 额度成功；7000 在输入阶段拒绝，20000 命中最终元数据/压缩工作空间门禁并拒绝。 |
| R8 边界/取消 | 保留第二块合并中取消；新增真实有界内容 31.5/31.99/32 MiB 探针和最终 metadata 超限探针。 | 31.5 MiB 接受；31.99/32 MiB 由工作空间门禁拒绝；输出上限未放宽。最终 metadata 的 7000/20000/120000 分支分别覆盖输入、最终工作空间和成功。 |

本轮证据：`phase3/resume-20261026-r1/`，包括 `allocation-order-results.json`、`metadata-final-results.json`、`boundary-results.json`、Host/Vitest/fieldBatch 日志。核心/模板/包图未改，复用 r3 的 Core 4666、XML 10/10、候选包消费、样例和业务批量证据；本轮新鲜运行 Host 317、Vitest 449、批量 E2E 5/5。首次 Core 取消时序失败仍保留在 `integration-r3-core-first-failure/`；r2 首败原始日志缺失的历史说明继续有效。

## 续作第 2 轮：前端压缩/输出副本预算（2026-10-26）

依据 `docs/optimization-expansion-phase3-resume-review-r1-2026-10-26.md`，仅修改前端 R5/R8；Host、Core/Templates 和已 CLOSED 根因未变。

| 复核项 | 修正 | 判别性验证 |
| --- | --- | --- |
| 压缩器固定工作区 | 用 JSZip 实际依赖 pako `Deflate` 的 state 上界 **267,160 字节**替代无依据 64 KiB 常数，并额外计入 64 KiB 输出块。 | 20000 额度最终 metadata 拒绝时 `deflateInstances=0`、`TextEncoder` 分配为空；8500 仍在目标大缓冲前拒绝。 |
| 输出累计/拼接 | 改用 `generateInternalStream` 有界收集输出块，累计输出和最终 Blob 副本分别计量；不调用 `generateAsync` 的 accumulate/concat。 | 固定种子 512 KiB 不可压缩载荷在 2,000,000 额度拒绝、3,000,000 成功且输出 >400 KiB。 |
| metadata 字符串/编码 | 分别计算 manifest/errors 的 UTF-16 字符串、CSV lines/join 构造副本和 UTF-8 编码缓冲；在生成最终字符串前检查，并计入 9080 字节案例。 | 7000 输入阶段拒绝、20000 最终元数据/压缩工作空间拒绝、500000 成功且最终未压缩 metadata 9080。 |
| 32 MiB 邻近 | 明确区分高压缩率原始负载与不可压缩 ZIP；不使用零填充 31.5 MiB 原始内容冒充近 32 MiB ZIP 成功。 | 31.5 MiB 高压缩率原始内容接受但 ZIP 仅约 33 KB；31.99/32 MiB 工作空间拒绝；31 MiB 不可压缩随机内容输入 ZIP 约 32.5 MiB，在默认 32 MiB 下由工作空间门禁拒绝。 |

证据：`phase3/resume-20261026-r2/`；本文件更新了批量指南的预算口径。Host 317/317 与 Core/XML/候选包证据未变，按范围复用；本轮新鲜前端构建/Vitest 450、批量 E2E 5/5。

## 续作第 3 轮：输入解压单缓冲（2026-10-26，最后第 3/3 轮）

依据 `docs/optimization-expansion-phase3-resume-review-r2-2026-10-26.md`，仅修改前端输入解压 R5/R8；输出端、Host 和已关闭根因未重开。

| 复核项 | 修正 | 判别性验证 |
| --- | --- | --- |
| 输入解压双副本 | `manifestEntry`/页面不再调用 `async('uint8array')`；用 `internalStream('uint8array')` 写入按 `_data.uncompressedSize` 预分配的唯一目标数组。分配前计入目标数组、pako Inflate state 12,768 字节、64 KiB 输出块、输入 ZIP/arrayBuffer 和累计工作字节。 | 1 MiB 单页、1.5 MiB 额度成功；捕获 >=1 MiB Uint8Array 分配只有一个目标数组，无 concat 副本。 |
| 多 entry/取消/失败 | 每条目处理前独立检查；取消检查覆盖 data/end；目标只在本条目作用域，取消或失败不发布 ZIP；manifest 额外计入目标、UTF-16 解码和模型上界。 | 64 KiB 首条后第二页 1 MiB 解压时取消得到 AbortError；多 entry 顺序与结果保持。 |
| 真实边界 | 更新高压缩率/不可压缩及默认 32 MiB 的拒绝阶段说明。 | 31.5 MiB 高压缩率原始内容接受但 ZIP 仅约 33 KB，不称近 32 MiB ZIP 成功；31.99/32 MiB 工作空间拒绝；31 MiB 不可压缩输入 ZIP 约 32.5 MiB，工作空间拒绝。 |

本轮新鲜前端构建/Vitest `452/452`、批量 E2E `5/5`；Host 317、Core/XML/候选包证据未变，按范围复用。证据见 `phase3/resume-20261026-r3/`；旧 async 的 1 MiB/1.5 MiB 接受记录保留在 r2 main-review，不作为本轮修复后证据。

## 验证结果

集成修正后的最终本地门禁：

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| Core net10 / net6 / netcoreapp3.1 | 各 `4666/4666`，失败/跳过 0 | `phase3/integration-r3-core/`、`integration-r3-runtime-net6/`、`integration-r3-runtime-c31/` |
| Host 默认脚本 | net10.0 `317/317`，失败/跳过 0 | `phase3/resume-20261026-r1/host/`、`host.stdout.log` |
| 严格 XML 五框架 | Core/Templates `10/10` | `phase3/integration-r3-xml/verification.json`、`integration-r3-xml.stdout.log` |
| 前端构建 | PASS | `phase3/resume-20261026-r3/web-build.stdout.log` |
| Vitest | `452/452`（23 个文件） | `phase3/resume-20261026-r3/web-vitest.stdout.log` |
| Chromium | `206/206`，workers=1、retries=0 | `phase3/integration-r3-e2e-final/chromium.stdout.log` |
| F1/F2 定向 E2E | `5/5` | `phase3/resume-20261026-r3/e2e-fieldBatch/chromium.stdout.log` |
| 模板 C#/JSON/往返样例 | exit 0；根目录 105、含子目录 134（文件数，非用例数） | `phase3/integration-r3-template-samples/` |
| 候选包消费 | net10/net6.0/netcoreapp3.1 PASS，exit 0，同一候选包 | `phase3/integration-r3-modern/`、`integration-r3/consumer-net6/`、`integration-r3/c31/` |
| 主代理反例重放 | probe exit 0；R1/R2/R5/R7 反例符合预期 | `phase3/integration-r2-main-review/results.json` |
| 第 3 轮执行方重放 | remaining/probe exit 0；大数 200、f90 200、metadata 拒绝 | `phase3/integration-r3-main-review/` |
| 完整快照 probe | 第二块仍为 BEFORE | `phase3/integration-r2-main-review/snapshot-results.json` |
| 独立业务批量消费 | exit 0，3 成功/0 失败/3 页，exit code 与 ZIP 保留 | `phase3/integration-r3-business-batch/`；本轮未改服务端批量输出语义，复用 |
| F2 R5/R8 资源预检 | Host 317（复用）；Vitest 452；批量 E2E 5/5；31.5 MiB 高压缩原始内容接受、31.99/32 MiB 工作空间拒绝、31 MiB 不可压缩 ZIP 拒绝 | `phase3/resume-20261026-r3/` |
| 最终分配顺序/压缩工作 | TextEncoder 空、deflateInstances=0；9080 metadata 200000 拒绝、500000 成功；1 MiB 单页仅一个目标数组 | `phase3/resume-20261026-r3/allocation-order-final-metadata-results.json`、`metadata-final-results.json`、`inflate-stream-results.json` |

阶段定向首次运行和原初版失败计数的证据在 `phase3/integration-r1-*` 保留；第 3 轮 Core 首次全量的一次 `EscPosCanvasCancellationTests` 时序失败已单独保留在 `integration-r3-core-first-failure/`，隔离和全量重跑通过；第 2 轮 Core 71 项与业务首次 exit 1 的原始首败日志未保存，报告不声称其日志完整。未变化的候选包不再重复全量消费，只重跑同一候选包的受影响 TFM。运行时最终用官方 Program Files 运行时直跑并记录 testhost 实际位置（net6 `6.0.36`、netcoreapp3.1 `3.1.32`）；先前 `Verify-ProtocolRuntimes.ps1` 因默认 `artifacts/test-runtimes` 不存在而中止的尝试保留为 `integration-r1-runtime-verifier-config-failed`，不属于代码失败。

## 状态分类

- Implementation：F1 已 CLOSED；F2 的 R5/R8 已完成续作第 3 轮输入解压单缓冲、压缩/输出副本预算收口，等待主代理验收；执行方不自行标记 F2 CLOSED。总计划保持 5/10。
- Open Actionable：R1/R2/R3/R4/R6/R7 已 CLOSED；本轮只保留 F2 R5/R8 的验收项，等待主代理复核。若复核仍有同根因缺口，按续作清单停止报告，不自行进入 F3—F6。
- Blocked Approval：无。
- Blocked External：GitHub Actions Windows/Linux 实际运行。
- Not Applicable：实机验收（用户排除）。
- Accepted Limitations / Deferred：无持久队列；批量业务 API 接收已解析对象；旧 job 在批量入口拒绝；PDF/SVG 和 F3—F6 不在本阶段。
- Verified Boundaries：25/26 分块、1000 记录/页、32 MiB、份数基线隔离、PageEnd 独立收尾、完整日期输入、offset 实际存在性、无损 decimal/offset 并保留旧无 Format 语义、未使用大数不触发 raw 转换、合法小写/有符号格式长度、CSV 空记录/原型拒绝、完整运行快照、取消失效旧运行、EAN/QR/格式/绑定 partial、内部/资源/取消终止、统一头/manifest 预算及最终合并 metadata 预算、合并期间取消均有本地证据。
- No-Progress Check：CHANGED；续作第 1 轮只修改 R5/R8，显式分配顺序和 32 MiB 邻近边界已有新证据。
## 边界与未完成

- 外部 CI 实际运行：`BLOCKED_EXTERNAL`；本报告不把本地 Windows 测试当作外部 CI。
- 实机打印、固件字体、走纸/裁切/扫描：`NOT_APPLICABLE`（用户排除）。
- 批量接口是无持久队列的 HTTP 分块接口；业务直接调用方需要按“records 对象数组”提交；CSV/JSON/JSONL 文本解析在设计器客户端。
- 批量入口明确拒绝旧 `job`，避免与份数字段静默相乘；需要任务级策略的场景仍走单条既有接口。
- PDF/SVG（F5）、持久队列、跨请求批次状态和 F3/F4/F6 不在本阶段。
- F1/F2 交付待主代理下一轮独立复核；执行方不自行标记整体计划完成，也不启动第四阶段。

## 证据与复核入口

- 报告：本文件、[字段格式化](field-formatting.md)、[批量生成](batch-generation.md)。
- 代码：`BgFieldFormat`、`BgFieldFormatter`、`Binding.Text`、`DesignerRequestProcessor.Batch.cs`、`BatchPanel.vue`、`batch.ts`。
- 原始证据根：`artifacts/optimization-expansion-20261025/phase3/`；第 1 轮证据在 `phase3/integration-r1-*`，第 2 轮证据在 `phase3/integration-r2-*`，第 3 轮证据在 `phase3/integration-r3-*`。

未暂存、提交、推送、发布、改变版本或覆盖 API 基线。
