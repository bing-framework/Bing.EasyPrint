# 候选 CI、运行时与性能验收

## 验收范围

用户已确认：旧运行时只验收 Windows 上 `.NET 7.0.20`、`.NET 6.0.36`、`.NET Core 3.1.32` 的完整 Core 测试，零失败、零跳过，并以 testhost 诊断日志确认实际运行时。`net5.0`、`netcoreapp2.2` 不纳入本轮门禁；不删除这些可选测试目标，也不改变库的五 TFM 支持声明。

Windows/Linux 的 Core、Host、现代包消费、字体、前端类型/构建/单元和完整 Chromium 由 `designer-cross-platform.yml` 执行。两平台必须匹配同一提交和源码范围哈希，并保留运行 URL、run ID/attempt、TRX、前端日志、Playwright JSON 和起止候选身份。用户已授权将候选提交并推送到 `codex/acceptance-20261106`；本地 Windows 测试不能替代远端 Linux 结果。

最终代码候选为 `f59d2cc21e13a1e23159ae54d7f18a022e7b7fa3`，源码范围 SHA-256 为 `d0801e7ec992799012c7484bbfa3f4ea95c4f3260bb10aa422b18ca725f42fda`。[Actions #42245718926](https://github.com/bing-framework/Bing.EasyPrint/actions/runs/42245718926)，attempt 1，Windows/Linux 及整体 workflow 均为 success。后续收口只修改验收文档，复用该候选的相同源码范围，不将文档提交冒充已执行的 CI 提交。

## 已批准的性能契约

配置：[performance-acceptance.json](../tools/performance-acceptance.json)。用户在新测量前明确回复“批准该契约，继续正式验收”，批准的 SHA-256 为 `ACEB043C7D53218A0142A7618BF53DA052DE68536C32E819A4E650ED7E576087`。配置保留提案原文以保持该哈希；实际批准由独立 `performance-approval.json` 回执绑定，不能通过修改配置中的状态自行批准。

环境固定为同一台 Windows x64、.NET 10、Release，基线提交 `0507b372dcd581ddb45e2c868b4403350318e0d5`；基线与候选使用相同的新 harness 和输入。每个负载预热 3 次，正式采集 30 个独立单次样本；串行执行，请求并发/实际并发均为 1，队列 0。每个进程 180 秒观察窗口，进程峰值工作集上限 512 MiB。

| 固定负载 | 中位耗时上限 | P95 上限 | 每次托管分配上限 |
| --- | ---: | ---: | ---: |
| CPCL 1000 个 ASCII 文字元素 | 100 ms | 200 ms | 16 MiB |
| CPCL 512×256 固定 RGBA 图片 | 100 ms | 200 ms | 16 MiB |
| 100 行显式日期格式模板 | 100 ms | 200 ms | 16 MiB |
| 800×600，8 个 Code128 + 2 个 QR 的合并预览 | 100 ms | 250 ms | 32 MiB |
| 25 条独立记录，400×120，Code128，原始指令 + PDF + SVG | 2000 ms | 5000 ms | 256 MiB |

所有绝对门槛均须满足；另要求候选中位数不超过固定基线的 `1.20 倍 + 1 ms`，每次分配不超过基线的 `1.10 倍 + 64 KiB`。固定少量噪声容差用于低延迟场景，不在测量后调整。保留原始延迟、nearest-rank P95、实测区间吞吐量、GC 分配和三代 GC 次数、输出大小、峰值工作集。吞吐量和 GC 次数作为报告指标，门槛由延迟、分配和峰值工作集约束。

每次操作都验证结果；Core 输出逐字节一致，预览 ZIP 必须有 10 张图片，批量必须 25 成功/0 失败并含 25 PDF 与 25 SVG。缺样本、跳过负载、异常、身份或环境不匹配均失败。基线不具等价能力时标记 N/A，不能伪造 before。

这轮性能范围为 Core 及真实 Host `DesignerRequestExecution`/processor helper 的进程内测量，使用 `DefaultHttpContext`；不经过 Kestrel、路由、中间件、服务器准入，不包含网络 RTT、浏览器渲染、任意并发、实机吞吐和生产负载分布。资源预算属于逻辑保护契约，不能替代实测进程内存。当前容量限制保持 25 条/块、1000 条/批和 32 MiB，不因性能结果放宽。功能语义由单元和集成测试保证；性能过程中的自参考字节和归档数量检查用于发现不稳定或不完整输出。

## 当前证据

证据写入 `artifacts/acceptance-20261106/`，按仓库规则留在本地，不覆盖历史记录。

| 门禁 | 结果 | 证据 |
| --- | --- | --- |
| Windows .NET 7.0.20 完整 Core | PASS，4717/4717，零失败/跳过 | `remote-ci/windows-r4/`，TRX 与实际 testhost 运行时交叉核对 |
| Windows .NET 6.0.36 完整 Core | PASS，4717/4717，零失败/跳过 | 同上，确认实际 6.0.36 testhost |
| Windows .NET Core 3.1.32 完整 Core | PASS，4717/4717，零失败/跳过 | 同上，确认实际 3.1.32 testhost |
| Windows/Linux .NET 10 Core | PASS，各 4717/4717，零失败/跳过 | `remote-ci/windows-r4/`、`remote-ci/linux-r4/` 的完整 TRX |
| Windows/Linux .NET 10 Host | PASS，各 373/373，零失败/跳过 | 同上，Host TRX |
| Windows/Linux 前端单元 | PASS，各 478/478 | 同上，Vitest 原始日志 |
| Windows/Linux 完整 Chromium | PASS，各 231/231，零失败/跳过/flaky，零重试 | 同上，Playwright JSON |
| Windows/Linux 字体、现代包消费、类型/生产构建 | PASS | 同上，必要步骤结果及原始日志 |
| 本机正式性能 | PASS，五负载全部通过 | `performance/formal-r1/acceptance.json` |
| 两平台候选身份与产物 | PASS，同提交/源码哈希/run/attempt，起止稳定 | `remote-ci/verification-r4.json`，`final-evidence-manifest.json` |

性能实测环境为 Windows 10.0.19045 x64、.NET 10.0.11、Release；基线/候选独立进程各 3 次预热、30 个正式样本，运行约 6.19/6.21 秒。下表的每次分配取原始样本中位数，吞吐量按操作采样耗时计算，不含显式 GC、预热或结果校验时间。

| 负载 | 基线中位数 ms | 候选中位数 ms | 候选 P95 ms | 候选分配 MiB/次 | 操作/秒 | 门禁 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| 1000 个文字元素 | 0.618 | 0.500 | 0.761 | 0.89 | 1713.15 | PASS |
| 512×256 图片 | 11.378 | 11.122 | 11.629 | 1.00 | 109.20 | PASS |
| 100 行日期模板 | 1.893 | 2.079 | 2.759 | 2.08 | 459.28 | PASS |
| 8 Code128 + 2 QR 预览 | 10.863 | 10.274 | 11.699 | 2.42 | 96.28 | PASS |
| 25 条 PDF/SVG 批量 | 118.624 | 128.004 | 171.850 | 43.88 | 7.30 | PASS |

候选峰值工作集为 96.36 MiB，基线为 92.57 MiB；所有输出检查通过。上述数字表示本契约内的门禁通过，不据此承诺其他机器或生产吞吐量。

正式测量绑定的本地候选源码哈希为 `6c0b8ad5526df640d9e911063a7f37c38cb68e2a29cee0ae6c66d1c61d42b887`，采集程序 SHA-256 为 `3E9F9F13A9B8CB0B2024EDF1B9AB59E67537F7D143F0108DCEF37FF17318D2FF`。`candidate-source-final.json`、`baseline-identity.json`、`build-manifest-final.json` 和每进程 `execution.json` 共同绑定源码、构建日志、落地及实际加载的三份产品程序集。`ci-r3-performance-promotion.json` 证明生产差异仅限自动系统字体选择方法、其余 Raster 内容哈希一致，并逐负载说明未调用该方法；`ci-r4-promotion.json` 证明其后仅 E2E 收尾改变。正式性能按未变相关路径复用，原测量身份保持不变。

发布前的 GitHub 仓库 API 查询返回 200，工作流列表为空，见 `github-repository-access.json`；此前指定 workflow API 返回 404 的原始记录保留在 `github-actions-query.json`。用户授权后已发布候选，真实 CI 由 push 触发。工作流加入提交 SHA/源码哈希的起止校验，按 SHA 和 attempt 归档，并将失败或跳过的必要步骤判为失败。

首轮 [Actions #42238007958](https://github.com/bing-framework/Bing.EasyPrint/actions/runs/42238007958) 对提交 `202d7bb46c2ffc948f9253249bc1c84ddb40524e` 的两平台结果均为失败。Linux 在身份采集阶段对隐藏 `.gitignore` 读取长度失败；改用 `FileInfo.Length`，Windows Hidden 文件端到端反例实现 RED/exit 1 → GREEN/exit 0。Windows 完整 Core 为 4713 通过/1 失败/0 跳过，固定字典哈希因 Git checkout 的 CRLF 转换失配；精确路径 `.gitattributes` 固定 LF 后，真实 `core.autocrlf=true` checkout 哈希恢复为固定值，现有嵌入资源哈希测试 1/1 通过。未改变字典内容、预期哈希或门槛。

首轮日志及失败产物保留于 `remote-ci/`，隐藏文件和换行反例分别位于 `gate-fixtures/candidate-hidden-*.json`、`eol-regression/result.json`。这些修复仅影响采集工具与 checkout 字节，不改变本机正式性能测量的生产源码、固定字典字节或 harness；性能证据按未变相关范围复用。最终通过须以修复后同一候选的两平台真实结果判定，不能将首轮跳过的步骤记作通过。

第二轮 [Actions #42239528357](https://github.com/bing-framework/Bing.EasyPrint/actions/runs/42239528357) 对提交 `6d0d16637f40585653d89ec636af5d4e2c54d3c9` 的 Linux 起止候选身份均通过，Core 为 4713 通过/1 失败。系统字体扫描遇到元数据可读、字形表不可读的字体时，抛出 `MissingFontTableException`，提前中断默认字体回退。新增用固定 TTF 移除 `loca` 表的反例，两用例先得到相同异常；修复仅让自动扫描跳过字体库的 `FontException`/`InvalidFontFileException`，显式字形检查保留错误，取消继续传播。受影响的 Raster/字体测试 838/838、零失败/跳过。RED、首次修复未覆盖独立异常继承链的失败及最终 GREEN 均保留于 `font-fallback/`。

此次字体修复不调用性能契约中的五条路径：ASCII 文字及图片为 CPCL，日期模板为原生指令，预览和批量为不带人眼可读文字的条码。`AcceptanceProbe.cs`、相关输入及 harness 未变，按 `REUSED_UNCHANGED_RELEVANT_PATH` 复用 `formal-r1`，不宣称新程序集已经重新测量。最终真实 CI 已对修复候选执行现代及三旧运行时的完整功能门禁。

第三轮 [Actions #42241931887](https://github.com/bing-framework/Bing.EasyPrint/actions/runs/42241931887) 对 `d1b7ca25e85690576b276662fa6e65fe8a95d058` 的 Windows 全部门禁通过，Linux Core、Host、包消费、构建、Vitest 通过，完整 Chromium 为 230 通过/1 失败/0 跳过。失败为旧 Host CC-C 用例完成业务断言后，未结束的异步路由遇到已释放响应；在该 spec 增加 `afterEach` 等待 `page.unrouteAll({ behavior: 'wait' })`，保留原断言、超时和零重试设置。本机同文件五个真实/旧 Host 浏览器流程 5/5 通过，证据在 `browser-route/`；最终 Linux 结论仍以修复后完整 CI 为准。

最终第四轮的两份归档均从 GitHub 下载并按 API digest 验证：Windows artifact `13519686356`，SHA-256 `C7F337114FC0D4065181C4DE72F1AFB760F149494DA66FD411525CD6DD097826`；Linux artifact `13519461063`，SHA-256 `6673D8305B67D5A62CA49A271D62BC1B5DB08313752BCDE2588D4CFDD7F18B2B`。原始 ZIP、逐步结果、TRX、运行时诊断、前端日志及 Playwright JSON 均保留；三次失败运行没有被最终成功覆盖。最初本地三旧运行时各 4714 项的记录继续作为历史证据，最终候选采用远端各 4717 项的新证据。

本轮门禁 3/3 完成：两平台候选 CI、三旧运行时、已批准的正式性能范围。没有待修复或待批准事项。结果不扩展到实机打印、Linux 性能、Kestrel/网络、未批准的并发负载或 net5/netcoreapp2.2。TDD 指南及功能测试映射见 [tdd-guide.md](tdd-guide.md)，前期日期/JSONL 的红绿记录见 [tdd-completion-2026-11-06.md](tdd-completion-2026-11-06.md)；字体新增三用例使本次系列补充合计 20 用例，真实 RED 与修复边界分别记录，历史实现不追认为 TDD。

## 复验入口

旧运行时完整验证：

```powershell
./tools/Verify-ProtocolRuntimes.ps1 -Frameworks net7.0,net6.0,netcoreapp3.1 -NoRestore -ResultsDirectory artifacts/acceptance-20261106/legacy
```

性能入口为 `tools/Invoke-PerformanceAcceptance.ps1`，必须传入独立的基线/候选构建目录、固定契约的真实批准回执、两份源码身份及绑定实际程序集的构建清单；结果目录必须不存在。执行命令和进程退出码保存在本轮 `performance-formal-r1.log`、`performance-formal-r1-exit.json` 与 `formal-r1/*.execution.json`。只重算门禁时调用 `tools/Test-PerformanceAcceptance.ps1`，不会重复测量。门禁反例使用单独标为 synthetic 的 fixture，不能当作性能数据。

本轮只变更验收工具和工作流，上一轮生产/单元测试源码范围未变，因此复用 [TDD 补全记录](tdd-completion-2026-11-06.md) 中本地现代回归证据；最终远端候选必须新跑 Windows/Linux 门禁。
