# 优化扩展第一阶段独立复核

日期：2026-10-25。范围：O1/O2/O3 交付候选；起点 `6c707be4092dada2dabe345a9eb92e1e907c1acb`。当前结论：`PASS_LOCAL`，集成修正第 1 轮后 P1-R1—R11 全部收口，放行第二阶段。外部 CI 证据仍单列，不代表跨平台实际验收已完成。

## 第 1 轮收口复核

- R1/R2：页面切换统一进入 refreshPreview，取消在途请求并推进失效序号；丢弃图片结果显式释放 URL。旧 Host 越界页采用结构化 InvalidPageIndex，恢复第一页。受控延迟、连续翻页、旧 Host 2→1 页用例已包含在 E2E 197/197 中。
- R3/R4：元数据预留与实际 manifest JSON 缓冲分别计费；渲染后取消、metadata 阶段预算拒绝、受控图片编码异常均覆盖实际包生成路径。主代理复跑当前 Host 测试程序集的 PreviewBundleHostTests：9 通过、0 失败、0 跳过，TRX 为 `artifacts/optimization-expansion-20261025/phase1/main-review-r1/host/preview-bundle-review.trx`。
- R5：WarningsNotAsErrors 保留 NU1902/NU1903 警告；依赖版本未更改。
- R6—R11：luna_worker 独立只读核对字体样式、CI 串行参数、逐步退出码、证据上传、消费运行时字段及历史证据缺口声明，均满足本地验收；Linux 与 Actions 实跑继续 BLOCKED_EXTERNAL。
- 主代理运行 `npm test -- src/api.test.ts src/previewLifecycle.test.ts`：51 通过、0 失败、0 跳过；`git diff --check` 通过。
- 独立核对本轮完整门禁：Host 271/271（.NET 10.0.13）、Vitest 417/417、Chromium 197/197（1 worker、0 retries）、现代包消费 PASS。核心与五框架未变范围复用本阶段已有明确证据。
- 当前待修问题：无。历史现代消费首次失败原始日志无法恢复已明确披露，不重造证据；不据此继续自动修复。

下表保留初审时的缺陷、位置与验收条件；其中 OPEN_ACTIONABLE 是初审状态，当前状态统一为 CLOSED。后续只有新代码或新证据揭示回归才重新打开。

## 已核查证据

- 执行方报告：`docs/optimization-expansion-phase1-2026-10-25.md`，原始清单：`artifacts/optimization-expansion-20261025/phase1/evidence-manifest.json`。
- 主代理对交付 Host 测试程序集独立运行 `PreviewBundleHostTests`：6 通过、0 失败、0 跳过，目标 `net10.0`。使用隔离 SDK `10.0.402` 的 `dotnet vstest`；TRX：`artifacts/optimization-expansion-20261025/phase1/main-review-r0/host/preview-bundle-review.trx`。
- 主代理运行 `npm test -- src/api.test.ts`：48 通过、0 失败、0 跳过。此范围验证现有 API 测试，未覆盖下述翻页/缩页条件。
- `git diff --check` 通过；有 CRLF/LF 归一化提示，无空白错误。
- 已读 Host 单次渲染、ownership 与释放路径，以及前端 bundle、回退和翻页逻辑。下述代码路径缺口需增加有区分力的回归用例，不能以全量通过数替代。

## 初审修正清单（历史）

| ID | 状态 / 优先级 | 位置与触发条件 | 影响与验收条件 |
| --- | --- | --- | --- |
| P1-R1 | OPEN_ACTIONABLE / P1 | `web/Bing.EasyPrint.Designer/src/App.vue:1539,1562,1618,1639`：整页刷新在途时用户翻页。翻页只取消 `previewPageAbort`，与整页请求共用未更新的 `previewRequest`。 | 两个请求都能通过失效判断；先完成的目标页会被迟到的整页结果切回旧页。统一请求归属/失效机制；用受控延迟覆盖两个完成顺序、连续翻页及编辑打断。丢弃结果须释放已创建的 object URL。 |
| P1-R2 | OPEN_ACTIONABLE / P2 | `web/Bing.EasyPrint.Designer/src/api.ts:825`：旧 Host 回退中当前页为第 2 页，修改数据使总页数减为 1。新范围检查抛普通 Error。 | App 仅对结构化 `InvalidPageIndex` 恢复第一页，普通 Error 降级本地近似布局，健康旧 Host 的准确布局/码图丢失。统一缩页恢复；覆盖无 bundle 协商的真实 Host 回退、2→1 页，仍显示引擎预览与当前页图片。 |
| P1-R3 | OPEN_ACTIONABLE / P1 | `tools/DesignerHost/DesignerRequestProcessor.cs:208,264,287`：manifest 直接序列化至压缩 entry，`CreatePreviewPages` 新增整套数组，图片/诊断清单也无元数据预算预留。 | 当前只通过压缩后 ZIP 计费，不能证明计划要求的布局元数据和临时缓冲预算。为 manifest 元数据和序列化缓冲明确计费并保留取消检查；用高压缩率布局且 `imageTypes=[]` 验证 metadata 阶段预算拒绝、无部分 ZIP、预算归零，不仅验证大位图超限。避免仅靠放大预算通过。 |
| P1-R4 | OPEN_ACTIONABLE / P2 | `tests/DesignerHost.Tests/PreviewBundleHostTests.cs:136`：取消测试在请求执行前取消，未进入渲染/ZIP；普通元素失败用例只经过 unsupported 分支。 | 目前不足以支持渲染后释放和编码异常保留布局的报告结论。增加渲染产生 primitives 后取消，以及开始包生成后的取消/预算失败；验证无部分输出和释放。增加实际单元素编码异常而其他元素仍成功的回归。 |
| P1-R5 | OPEN_ACTIONABLE / P2 | `tools/DesignerHost/DesignerHost.csproj:7` 与 Host 测试项目：`NoWarn` 全面隐藏 NU1902/NU1903。 | 保留旧依赖是已批准边界，隐藏整个审计类别会使其未来告警也不可见。改为保留警告但不提升为错误的窄配置（如追加 `WarningsNotAsErrors`），依赖版本不变，报告继续列明剩余告警。 |
| P1-R6 | OPEN_ACTIONABLE / P1 | `tools/Verify-SystemFonts.ps1:74-85`：Linux 将 DejaVu Sans 常规样式强制匹配 `Regular`，指定字体包常规款为 `Book`。 | Ubuntu 字体门禁会拒绝正常安装。按实际字体族和非粗体常规属性确认，不强制 Regular 字面值；保留粗体/CJK 明确校验及缺失时失败。依据：[DejaVu 上游](https://dejavu-fonts.github.io/)。实际 Linux 执行仍单列外部缺口。 |
| P1-R7 | OPEN_ACTIONABLE / P2 | `.github/workflows/designer-cross-platform.yml:70,117`：E2E 未指定已验证的 `--workers=1`。 | 与报告声明的稳定门禁不同；并行首次日志已出现响应被释放失败。两平台显式使用 `--workers=1 --retries=0`，保留首次失败，不靠重试掩盖。 |
| P1-R8 | OPEN_ACTIONABLE / P2 | 同 workflow `:65-67`：Windows 单个 pwsh step 连续运行 build 和 test，未检查前一原生命令退出码。 | build 失败可被后续 test 成功覆盖。拆成独立 step 或逐条 fail-fast 检查。依据：[GitHub shell 规则](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax)。 |
| P1-R9 | OPEN_ACTIONABLE / P2 | 同 workflow `:75,122`：只上传 ci-* 目录，Playwright 默认将附件放在 `web/Bing.EasyPrint.Designer/test-results`。 | 外部首次失败上下文未进入交付证据。将 Playwright 结果与可机器读取的计数报告纳入 always 上传目录，前端构建/Vitest 日志也保留；不得仅依靠总状态说明。 |
| P1-R10 | OPEN_ACTIONABLE / P2 | `tools/Verify-ModernConsumption.ps1:109-110`：汇总 runtime 取当前 pwsh 的 FrameworkDescription。 | 实际 consumer 为 .NET 10.0.13，汇总却写 .NET 10.0.8。将 pwsh 运行时单独命名，目标 runtime 从 consumer-verification.json 读取。新证据与旧记录区分，不覆盖历史结果。 |
| P1-R11 | OPEN_ACTIONABLE / P2 | 阶段报告 §6 声称 `modern-consumer-diagnostic/` 保留首次 MSB3030 诊断，但独立盘点只找到项目生成文件，未找到错误日志、binlog 或失败结果记录。 | 若原始输出存在，精确引用/保存；若已丢失，明确标记原始证据缺口，不重造历史失败。新验证需从开始保存 stdout/stderr 和退出码。 |

## 验证影响与下一步

本轮仅修改 Host 预览包、前端预览生命周期、相关回归及 CI/审计配置。先跑这些路径的定向测试，再 Host 完整测试、前端构建/Vitest 与受影响 E2E；阶段收口完整 Chromium 使用 `--retries=0`，保留首次失败。未改核心/模板生产代码时复用本候选五框架和核心测试证据，不重跑无关性能矩阵。若预算实现改变性能路径，定向性能数据须明确旧证据是否仍适用。

独立 luna_worker 已核对：核心首次 4624/4626、修复后 4626/4626（10.0.13）；Host 268/268（10.0.13）；旧框架各 4626/4626（6.0.36 / 3.1.32）；Vitest 414/414；首次并行 E2E 193 通过、1 失败，最终串行 194/194。计数和主要运行时证据与报告一致，现代消费汇总字段误用见 R10。

外部 Windows/Linux Actions 未运行仍为 `BLOCKED_EXTERNAL`；不把外部缺口当作本地修复，也不将确定性的 workflow/script 错误归为外部阻塞。实机不适用。本清单已在第一轮集成修正中收口；当前下一步为第二阶段 O4。
