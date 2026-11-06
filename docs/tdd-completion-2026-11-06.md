# TDD 补测与验证记录（2026-11-06）

本轮为当前扩展功能补充了 17 个参数化或独立测试用例，并修复了测试暴露的日期格式与 JSONL 定位问题。新增缺陷测试保留了真实 Red → Green 记录；正向控制及浏览器恢复流程属于覆盖补充。既有功能的开发历史不追认为 TDD，也不以本报告宣称穷尽覆盖。

基准提交：`0507b372dcd581ddb45e2c868b4403350318e0d5`。验证环境为 Windows x64、SDK `10.0.400`；Core 诊断日志确认实际运行时分别为 `.NET 10.0.11`、`.NET 6.0.36` 和 `.NET Core 3.1.32`。本地原始证据位于 `artifacts/tdd-completion-20261106/`，候选源码、测试与日志的 SHA-256 及已获取的退出码见该目录的 `manifest.json`。`artifacts/` 按仓库规则忽略，原始文件保留在当前工作区。

## 本轮行为与测试

| 项目 | 修复后的行为 | 新增用例 |
| --- | --- | --- |
| 日期输入完整性 | `YYYY` 为字面量，`ddd`/`dddd` 为星期名称，均不能替代必须存在的数字年或日；错误保留字段及模板位置 | Core 3、Host 3 |
| 日期输出长度预检 | 长年份补零、重复 `zzz` 时使用足够的长度上界，合法输出不会被预检误拒绝 | Core 2、Host 2 |
| 日期正向控制 | 完整年月日、附加星期、引号字面量与 `%` 单字符标记仍可使用 | Core 4 |
| JSONL 错误定位 | LF/CRLF 空白行计入源文件物理行号；成功记录 `index` 仍从 0 连续编号 | 前端 2，并加强既有成功用例 |
| 导入恢复 | 错误文件显示第 3 行并禁用生成；上传修正文件后清除错误、恢复两条记录及生成操作 | 浏览器 1 |

生产修改仅涉及 `BgFieldFormatter.cs` 和前端 `batch.ts`，未改变公共签名、协议、TFM、版本或产品容量限制。使用规则分别补入[字段格式说明](field-formatting.md)与[批量生成说明](batch-generation.md)。完整 O1—O4、F1—F6 回归入口见 [TDD 指南](tdd-guide.md)。

## 真实红绿记录

以下路径相对 `artifacts/tdd-completion-20261106/`。失败数按执行用例计数，Core 和 Host 对同一根因的验证不是不同缺陷。

| 层级 | Red：修改实现前 | Green：修复后 | 原始证据 |
| --- | --- | --- | --- |
| Core 日期 | 22 项：17 通过、5 断言失败 | 22/22 通过 | `core/red-executed.log`、`core/red/core-date-red.trx` → `core/green-final.log`、`core/green-final/core-date-green.trx` |
| Host 日期契约 | 12 项：7 通过、5 断言失败 | 12/12 通过 | `host-red.log`、`host/red/host-date-red.trx` → `host-green-serial.log`、`host/green-serial/host-date-green.trx` |
| 前端 JSONL | 21 项：20 通过、1 断言失败；期望物理第 3 行，实际报告第 2 条记录 | 扩充后 22/22 通过 | `frontend/red-jsonl-line.log` → `frontend/green-batch-final.log` |

Core 红灯时已记录基准实现和当时测试哈希，见 `core/red-identity.json`。前端首个红灯后将文案从“条记录”明确为“行”，保留物理行号断言，再补 CRLF 非对象控制。Host 与前端未保存红灯时完整测试文件快照或哈希；前端日志也未保存完整调用命令和进程退出码。不能用最终文件冒充红灯身份，或从通过摘要补写未保存的退出码；manifest 明确标注这一证据粒度。原始失败日志、基准提交及本轮执行顺序保留，后续按指南在 Red 阶段同步冻结身份。

正向日期控制、第二个 JSONL 场景和新增浏览器流程用于验证修复边界，不单独宣称实现前红灯。浏览器用例是在修复后补充的集成验证。

## 最终回归

| 验证范围 | 结果 | 证据 |
| --- | --- | --- |
| Core，net10.0，全量 | 4714/4714，无失败、无跳过 | `core/final-net10.log`、`core/final-net10/core-net10.trx` |
| Host，net10.0，全量 | 373/373，无失败、无跳过 | `host-final.log`、`host/final/host-net10.trx` |
| 前端 Vitest，全量 | 27 个文件，478/478 | `frontend/full-vitest-final.log` |
| 前端类型与生产构建 | `vue-tsc -b && vite build` 通过 | `frontend/build-final.log` |
| Core 日期，net6.0，定向 | 22/22 | `core/net6.log`、`core/net6/core-date-net6.trx` |
| Core 日期，netcoreapp3.1，定向 | 22/22 | `core/netcore31.log`、`core/netcore31/core-date-netcore31.trx` |
| Chromium + 本轮 Host，字段/批量集成 | 6/6 | `browser.log` |

旧运行时只重跑了日期定向用例；本轮浏览器范围是 `e2e/fieldBatch.spec.ts`，不是所有 E2E。前端构建保留已有的单包超过 500 kB 提示。测试期间关闭 XML 文档生成，原有两份 XML 修改的哈希与工作开始时一致。

## 非产品首败与夹具勘误

- `core/red.log`：默认沙箱无法写入既有 `obj`，测试未开始；另行执行的 `core/red-executed.log` 才是产品红灯。
- `core/green.log` / `core/green/core-date-green.trx`：21/22 通过。修复后重复时区输出可正常格式化，但原 400 dot 夹具将其换行，触发测试的单元素断言；将该输出测试夹具宽度改为 1600 dot 后通过。没有因此放宽生产限制。
- `host-green.log`：多框架构建写入同一独立输出目录时发生 `GenerateDepsFile` 文件竞争，测试未开始；改为 `-m:1 -p:BuildInParallel=false` 后通过。
- 浏览器 6 项执行完成后，Vite 子进程清理停滞。核验命令行及端口后仅停止本轮 Vite，见 `browser-cleanup.log`；Playwright 随后正常退出，工具返回的退出码 0 记录于 manifest。本轮临时 Host 随后关闭。

以上日志均保留，未被最终绿灯覆盖。

## 当前状态与后续边界

- Completed：日期 Core/Host 红绿、JSONL 红绿及边界控制、真实浏览器恢复流程、受影响回归、功能测试映射与 TDD 指南，本轮 5/5 工作项完成。
- Open Actionable：无；独立审查未发现本轮需要继续修复的问题。
- Blocked Approval：沿用整体项目的正式性能/容量阈值待批准状态；本轮不批准或改变阈值。
- Blocked External：沿用 Windows/Linux 外部 CI 与 .NET 7 实际运行证据缺口；本地通过不替代这些结果。
- Not Applicable：回溯证明所有历史开发均为 TDD；本轮未请求发布。
- Accepted Limitations：沿用既有协议、软件码图、设备字体及实机边界。
- Verified Boundaries：不完整日期拒绝且定位正确；合法日期输出保留；JSONL 跳过空白行后正确定位并可恢复；旧运行时日期定向通过。
- Deferred：与本次边界修复无关的功能扩展及完整性能/资源矩阵。

Round：1。Implementation：5/5。Release Gates：本轮未执行发布验收，外部门禁保留原状态。L0 构建与文档检查、L1 定向、L2 受影响项目回归、L3 Host/浏览器链路已完成；L4 未运行全解决方案全框架矩阵，L5 未重跑性能/容量矩阵。前端回归后只补充浏览器测试与文档，前端单元/构建按源码范围未变复用。No-Progress Check：CHANGED；Next Action：STOP；Goal Status：STOPPED_BLOCKED（本轮补测完成，整体发布仍有上述外部门禁）。
