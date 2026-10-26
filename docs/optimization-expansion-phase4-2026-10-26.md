# 第四阶段 F3/F4 实施报告

## 状态

F3 GTIN Digital Link 与 F4 可视化 AI 编辑器已完成集成修正第 2/3 轮。第 1 轮的独立复核确认 R1—R5、R8、R9 关闭，R6/R7 仅部分修正，E1 证据归属不充分；本轮已修复 R6/R7 并补齐 testhost 运行时证据，交主代理独立验收。本报告不关闭阶段。前三阶段已验收范围未重新打开，本次未进入 F5/F6。

实现按 `ai_docs/plans/optimization-expansion-phase4-contracts-2026-10-26.md` 执行。没有修改总计划或阶段契约，没有改变版本号、公开 API 基线或五个生产 TFM；没有暂存、提交、推送或发布。

## 集成修正第 1/3 轮

独立复核发现，首轮门禁虽通过，但未覆盖下列确定性反例；因此首轮不能声称没有可执行 finding。R6/R7 的首轮改动是部分修正，E1 是报告证据归属问题。首轮实际通过范围和原始数量仍保留在下方，不能视为已覆盖这些行为。

| 复核项 | 修正与回归 |
| --- | --- |
| R1 主键路径定位 | 按完整 `/01/{14 位 GTIN}` 结构定位，不把后续限定符值 `01` 或恰好等于合法 GTIN 的值误判为主键；覆盖自定义前缀含 `/01/`、坏结构及 Build→Parse→Normalize。 |
| R2 跨位置重复 AI | path 与 query 共用已用 AI 集合；Parse、TryParse、Normalize 和 Host 都拒绝重复项。 |
| R3 原始 URI 结构 | 校验 authority、port、反斜线、百分号转义、原始 path/query/fragment；在拆分原始路径后只解码一次，不借通用 URI 规范化吞掉非法输入；覆盖合法 IPv6、端口、编码斜杠前缀往返。 |
| R4 粘贴 URI 保真 | 编辑器转换默认保留粘贴 URI 的基址、路径前缀、重复 query 和 fragment；显式勾选覆盖后才使用编辑器已有设置。Chromium 覆盖新建及已有配置的粘贴、导出、保存重载和引擎预览。 |
| R5 Host Placement | 结构化转换保留 AI 10/21/235 的路径或 query 位置；Host 回归及浏览器实际预览确认输出位置一致。 |
| R6 绑定实例一致（部分） | 首轮提取渲染与校验共享的 `TemplateBindingTraversal`，覆盖有数据时重复/嵌套重复、条件、组件参数和逐实例标识；独立复核随后确认省略 data 时仍会过早解析动态路径，且编辑器首个有效实例会覆盖后续实例错误。第 2 轮已补无数据静态扫描、pending 与字段全实例错误优先聚合。 |
| R7 原始错误索引（部分） | 首轮动态 AI10 超长和 primary 校验位错误能映射原始 Items；独立复核发现 primary 空值、字符和长度错误仍丢失 AI。第 2 轮传递已知来源 AI01，并覆盖 primary 位于非首项。 |
| R8 模式筛选 | Unsupported 仅在 Digital Link 模式禁用；Elements 仍可选合法 AI 8200，切换模式拒绝后保留配置并可恢复。 |
| R9 构建证据 | 本轮 `npm run build` 的完整成功输出、Node/npm/Vite 版本和退出码已保存为 `artifacts/optimization-expansion-20261025/phase4/integration-r1/frontend-build-success.log`。 |

本轮门禁与实际运行环境：

| 门禁 | 实际结果 | 运行时/证据 |
| --- | --- | --- |
| Core 全量测试 | net10.0、net6.0、netcoreapp3.1 各 4,705/4,705；失败/跳过均为 0 | .NET SDK 10.0.400；`l2-core-net10.log`、`l2-core-net6.log`、`l2-core-netcoreapp31.log` |
| Core/Host 聚焦回归 | Core GS1 33/33；Host GS1 20/20 | `l1-core-r1-expanded.log`、`l1-host-preview-green.log` |
| DesignerHost 全量测试 | 目标 TFM net10.0，337/337；失败/跳过为 0 | `l2-host-full.log`；首轮没有保存 Host testhost patch，不能归因到 .NET 10.0.11 |
| Vitest | 24 个文件、456/456 | Node v22.22.0、Vitest 5.0.2；`vitest-full.log` |
| Chromium E2E | 全量 211/211 通过；workers=1、retries=0。另一次 GS1 定向红测有 2 个失败，不属于全量运行 | Node v22.22.0、Chromium；`chromium-full.log`、`chromium-results/`；定向红测见 `l1-red-browser.log` |
| GS1 编辑器 Chromium | 3/3，包括 URI 粘贴/保存重载/引擎预览、显式覆盖和 AI 模式筛选 | 实际 Host 5188、Vite 5174；`l1-browser-gs1-green.log` |
| 生产前端构建 | `npm run build` 成功，1,979 modules，退出码 0 | Node v22.22.0、npm 10.9.4、Vite 8.3.1；`frontend-build-success.log` |
| XML 文档门禁 | Core/Templates 五个生产 TFM 共 10/10；失败/跳过为 0 | Release 严格 XML 警告；`l4-xml-documentation-gate.log`、`xml-documentation-verification/20261026-112423-112-1e11d0d5/` |
| 现代包消费者 | Core/Templates 包构建及消费通过 | SDK 10.0.400、消费者 .NET 10.0.11、net10.0；`modern-consumer-gate.log`、`modern-consumer/20261026-032519-2a9736cd/` |
| C#/JSON 案例 | Digital Link C# 与 JSON 模板输出字节一致，保存往返成功；样例程序完整执行 | `samples/Templates` net6.0，运行时 .NET 6.0.36；`samples-gs1-r1.log`、`samples/` |

首轮 Core/Host 的目标框架和通过数量有日志佐证，但没有 TRX、runsettings 或 testhost 运行时清单。Core 日志中的 `AVAILABLE_RUNTIME` 只列出安装项；`RUNTIME=.NET 10.0.8` 是 PowerShell 宿主，不是 Core testhost。首轮 Host 的 `.NET 10.0.11` 来自现代包消费者，不能移作 Host 证据。现代消费者自己的 .NET 10.0.11 证据仍有效，见 `modern-consumer-gate.log`。本轮新运行的 Host 与 Templates testhost 均为 .NET 10.0.11；该版本只依据 testhost 自身生成的日志确认：`integration-r2/host-green-final-results/l1-host-green-final.diag.host.26-10-26_11-54-40_05717_5.log:2` 与 `integration-r2/template-gs1-results/l2-template-gs1-tests.diag.host.26-10-26_11-57-23_82608_5.log:2`。同目录的 `.diag.log` 是外层 VSTest 诊断日志，不作为 testhost 运行时来源。

最小反例先红后修：初始 Core/Host 回归失败和浏览器源 URI/AI 模式失败分别保存在 `l1-red-core.log`、`l1-red-host.log`、`l1-red-browser.log`；首次浏览器运行接到既存旧 Host、预览夹具高度不足等排查输出仍保存在本目录的 `l1-*.log`。没有覆盖 `codex-continuation/`、`main-review/` 或旧 `phase4/sources/`。本机测试门禁均通过；Windows/Linux 外部 CI 尚未运行，实机验收按用户要求排除。

## 集成修正第 2/3 轮

本轮只处理 R6、R7 和 E1。R1—R5、R8、R9 沿用独立复核结论，不重新审计或重跑其专项用例；共享 `TemplateBindingTraversal` 与渲染器没有改动。

| Finding | 本轮修改与覆盖 |
| --- | --- |
| R6：缺失 data、条件/组件/重复上下文 | `DesignerRequestProcessor.Gs1.cs` 在未提供 data 时走静态结构遍历，不展开 `RepeatSource`、不求值绑定条件、不绑定动态组件路径；检查 GS1 静态配置、组合条件和组件参数配置。动态实例依赖返回 `pending-data`；静态 GS1 值在 pending 前仍执行静态解析检查。提供 data 时继续使用原 `Rows`/`Elements` 绑定遍历，缺少实际 RepeatSource 仍是 invalid。Host 回归覆盖重复、条件、组件参数、AI 9999 静态非法配置以及提供 `{}` 后缺 Items；GS1 指南补充省略数据与提供但缺路径的状态边界。 |
| R6：编辑器实例状态 | `Gs1AiEditor.vue` 聚合同一字段的全部校验项和错误；任一字段相关错误优先于成功项，并按 `instanceIndices` 显示实例序号。没有活动实例或所有项待数据时显示 pending。Chromium 覆盖省略 data、空 Items、恢复有效、首个 GTIN 有效而第二个无效。 |
| R7：主键来源 AI | `Gs1StructuredInput.ResolveDigitalLink` 在 AI 已知时将 `sourceAi` 传入诊断；保留既有原 Items 索引与 qualifier 映射。Host 回归覆盖空值、字符、长度、校验位错误以及 AI 01 位于原 Items[1]。 |
| E1：运行时证据 | 修正首轮报告中 Core PowerShell 与 Host 消费者运行时归属；本轮必要 C# 测试运行保存外层 VSTest `.diag.log`、testhost 自身生成的 `.diag.host.*.log` 和 TRX。外层日志用于记录 VSTest 调度诊断；实际 testhost .NET patch 仅依据 `.diag.host.*.log`。没有为修订报告重跑三套全量测试。 |

Round 2 定向门禁：

| 门禁 | 结果 | 运行时/证据 |
| --- | --- | --- |
| Host R6/R7 红测 | 修改前 5 项中 4 失败、1 通过；修改后 5/5 通过，失败/跳过为 0 | 目标 net10.0；testhost .NET 10.0.11；`l1-host-red-targeted-isolated.trx`、`l1-host-green-final.trx`；外层日志为 `.diag.log`，通过运行时由 `host-green-final-results/l1-host-green-final.diag.host.26-10-26_11-54-40_05717_5.log:2` 证明 |
| Templates GS1 诊断 | `Gs1StructuredTemplateTests` 6/6 通过，失败/跳过为 0 | 目标 net10.0；testhost .NET 10.0.11；`l2-template-gs1.trx`；外层日志 `l2-template-gs1-tests.diag.log`，testhost 运行时由 `template-gs1-results/l2-template-gs1-tests.diag.host.26-10-26_11-57-23_82608_5.log:2` 证明 |
| Host/Templates 编译 | DesignerHost net10.0 隔离构建成功；Templates net7.0 成功 | `l2-host-green-build.log`、`l1-templates-net7-build.log` |
| GS1 API Vitest | 1 个文件、4/4 通过，失败/跳过为 0 | Node v22.22.0、Vitest 5.0.2；`l2-vitest-gs1.log` |
| GS1 编辑器 Chromium | 2/2 通过，失败/跳过为 0；workers=1、retries=0 | 实际隔离 Host 5191、Vite 5176；`l2-browser-gs1-green.log`、`host-green-run.log` |
| 前端生产构建 | `npm run build` 成功，1,979 modules，退出码 0 | Node v22.22.0、npm 10.9.4、Vite 8.3.1；`frontend-build-success.log` |

首轮通过的 Core 三 TFM 全量结果、非 GS1 Chromium、消费者、案例和五 TFM XML 结果继续复用；本轮没有改这些范围涉及的共享渲染路径或公开 XML/API 契约。实机验收排除；Windows/Linux 外部 CI 未执行。

首测失败均保留且与通过记录分开：原 Host 输出目录被活动服务锁定；错误地禁用 ProjectReference 构建时使用了旧程序集，产生 13 失败/12 通过，该次结果不作产品结论；首个静态非法配置断言误期待统一 `status` 字段，实际既有模板加载错误返回 `errors` 包络，随后按契约修正断言。真实浏览器红测确认缺 data 时状态错误、混合实例被显示为 valid；通过结果见上表。完整命令、失败/通过/跳过数、实际运行时与文件路径见 `artifacts/optimization-expansion-20261025/phase4/integration-r2/commands-and-results.md`。

## 实现

- Core 固定 GS1 AI 字典提交及 536 项目录，区分不支持 Digital Link 的 AI；实现 GTIN Digital Link 构建、解析、规范化和 Elements 转换，并保留 BaseUri 前缀、fragment、有序重复扩展参数。严格处理百分号转义、UTF-8、query 分隔符、保留键和 URI GTIN 规则。
- 模板增加 `BgField.Gs1Input` 结构化来源，沿用现有字段绑定上下文与括号 AI 编解码器；检查 AI 角色、顺序、格式、条码/模式兼容性、BaseUri、扩展参数及可选的关联校验。省略该来源时仍走既有默认绑定。
- DesignerHost 提供 AI 目录、服务端校验和 Elements/Digital Link 转换端点，并返回可供保存的结构化输入。编辑器通过能力协商要求 Host 同时支持目录、结构化输入和 Digital Link。
- Vue 编辑器提供目录搜索、按顺序编辑 AI、静态值或字段绑定、格式选项、Digital Link 基址/fragment、重复 query 参数、Host 校验及文本转换。过期校验会取消；转换失败保留原配置。QR / DataMatrix 的 Digital Link 模式使用普通 URI QR/DataMatrix 编码。
- 新增 C# 与 JSON 双实现案例，比较两者最终 CPCL 字节；新增用户指南，并更新 README、模板、设计器、Host、开发和文档索引。

## 首轮自审记录（历史范围）

最初只读检查围绕 Core URI/parser 提出 6 个检查点。其记录曾认为 AI 排除项（`03`、`8014`、`8200`）、URI GTIN 14 位、任意路径前缀下定位 `/01/`、以及结果集合/元素深拷贝只读快照已有直接实现和测试，并补正扩展参数值 `=` 及兼容尾随斜线。该检查范围没有覆盖 Host 的真实转换/重复绑定上下文、真实编辑器 URI 生命周期及成功构建日志。独立 HTTP 反例随后发现 R1—R9，故该记录不代表完整复核，也不能据此宣称没有可执行 finding。

## 首次实现门禁历史

下列数字是集成修正前实际通过的原首轮范围，原日志继续保留；主代理独立复核证明这些场景未涵盖 R1—R9。

| 门禁 | 实际结果 | 证据 |
| --- | --- | --- |
| Core 全量测试（net10.0） | 4,693/4,693 通过；审计修正后复验 | `core-net10-revalidation.log` |
| Core 协议运行时（net6.0、netcoreapp3.1） | 两框架各 4,693/4,693 通过；审计修正后复验 | `core-net6-revalidation.log`、`core-netcoreapp31-revalidation.log` |
| DesignerHost 全量测试（net10.0） | 322/322 通过；审计修正后复验 | `host-net10-revalidation.log` |
| 前端全量 Vitest | 24 个测试文件、456 项通过 | `vitest-full.log` |
| Playwright Chromium 全量 E2E | 208/208 通过；workers=1、retries=0 | `chromium-full.log` |
| GS1 编辑器 E2E 复验 | 2/2 通过；审计修正后真实 Host 复验 | `gs1-e2e-revalidation.log` |
| 五生产 TFM XML 文档门禁 | 10/10 目标通过 | `xml-documentation-gate.log`、`xml-documentation-verification/` |
| 现代 .NET 消费者门禁 | 通过，运行时 `.NET 10.0.11` | `modern-consumer-gate.log`、`modern-consumer/` |
| Vite / TypeScript 生产构建 | 通过（Vite 8.3.1） | 终端执行记录；首次失败保存在 `first-frontend-build-failure.txt` |
| C# 与 JSON 案例字节一致性 | 22/22 案例通过，含 Digital Link；审计修正后复验 | `samples-revalidation.log`、`samples-revalidation/` |
| 编辑器桌面与 390px 窄屏视觉检查 | 面板动画完成后编辑器完整位于视口内，无内容水平溢出 | `visual-qa-success.log`、`gs1-editor-desktop.png`、`gs1-editor-mobile.png` |

最小门禁先于阶段门禁执行。项目已有 NuGet 弃用和依赖安全公告；本次未扩大到无关依赖更新或大型矩阵。构建和测试使用 `DocumentationFile=` 或独立输出隔离，避免覆盖仓库中既有 XML 文档；XML 门禁结果单独保存。

### 实际命令

首次实现阶段的命令记录如下；结果和目录属于首次运行，供历史追溯：

```powershell
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net10.0 -p:DocumentationFile=
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net6.0 -p:DocumentationFile=
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f netcoreapp3.1 -p:DocumentationFile=
dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj -f net10.0 -p:DocumentationFile=
./tools/Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1 -ResultsDirectory artifacts/optimization-expansion-20261025/phase4/codex-continuation/runtime -NoBuild -NoRestore
./tools/Verify-XmlDocumentation.ps1
./tools/Verify-ModernConsumption.ps1 -TargetFramework net10.0 -ResultsDirectory artifacts/optimization-expansion-20261025/phase4/codex-continuation/modern-consumer
dotnet run --project samples/Templates/Templates.csproj -p:DocumentationFile= -- artifacts/optimization-expansion-20261025/phase4/codex-continuation/samples
```

在 `web/Bing.EasyPrint.Designer` 目录运行：

```powershell
npm run build
npm test
npm run test:e2e -- --workers=1 --retries=0
npx playwright test e2e/gs1AiEditor.spec.ts --workers=1 --retries=0
```

阶段报告中的产物路径均相对于仓库根目录。XML 门禁覆盖生产目标 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`；运行时测试实际执行 `net6.0` 与 `netcoreapp3.1`，Host/Core 新框架测试执行 `net10.0`。

集成修正第 1/3 轮命令、SDK/运行时、退出码和实际通过/失败/跳过数量见 `artifacts/optimization-expansion-20261025/phase4/integration-r1/` 中逐项日志。门禁命令包括：

```powershell
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net10.0 -p:DocumentationFile=
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net6.0 -p:DocumentationFile=
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f netcoreapp3.1 -p:DocumentationFile=
dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj -f net10.0 -p:DocumentationFile=
npm run build
npm test
npm run test:e2e -- --workers=1 --retries=0 --output artifacts/optimization-expansion-20261025/phase4/integration-r1/chromium-results
./tools/Verify-XmlDocumentation.ps1
./tools/Verify-ModernConsumption.ps1 -TargetFramework net10.0 -ResultsDirectory artifacts/optimization-expansion-20261025/phase4/integration-r1/modern-consumer
dotnet run --project samples/Templates/Templates.csproj -p:DocumentationFile= -- artifacts/optimization-expansion-20261025/phase4/integration-r1/samples
```

集成修正第 2/3 轮的实际命令、筛选器、退出码、计数、testhost 运行时和首败索引见 `artifacts/optimization-expansion-20261025/phase4/integration-r2/commands-and-results.md`。该轮只运行 R6/R7 新增 Host 用例、Templates GS1 诊断类、GS1 API 单测、两条 GS1 Chromium 用例及生产前端构建。

## 首次失败与处理

| 首次失败 | 原因与处理 | 记录 |
| --- | --- | --- |
| TypeScript 首次构建失败 | 诊断 DTO 不含 AI 索引字段、条码类型推导为过窄字面量联合、测试 DTO 使用 PascalCase；分别按现有诊断契约、显式集合类型和真实 camelCase Host JSON 修正 | `first-frontend-build-failure.txt` |
| GS1 API 首次 Vitest 失败 | Mock 响应字段大小写与 Host 实际 JSON 不一致；改为契约中的 camelCase | `first-gs1-vitest-failure.txt` |
| GS1 编辑器首次 Playwright 失败 | 测试在选择受支持的 QR 元素前就等待仅在编辑器挂载时请求的目录端点；先加载/选择 QR 再等待请求 | `first-gs1-playwright-failure.txt`、保留的失败截图和 context |
| 首次窄屏截图几何断言 | 检查器仍处于滑入动画，测得的是过渡中的坐标；改为等待窄屏检查器完成既有 CSS 过渡后测量，并重新截图。未发现编辑器内部横向溢出 | `first-gs1-visual-qa-failure.log`、`first-gs1-visual-qa-mobile.png`、`first-gs1-visual-qa-mobile-context.md` |
| Core 审计修正首次编译失败 | 新的路径变量与诊断路径局部变量重名；改名后 Core 三框架重新通过 | `gs1-core-revalidation.log` |
| Core 扩展参数规范化首次断言失败 | 测试要求 `=` 原样保留，但安全的 URI 输出会把值中的 `=` 编为 `%3D`；修正语义等价输出断言 | `gs1-core-revalidation-r1.log`、通过记录 `gs1-core-revalidation-r2.log` |
| Core / 模板 / Host 初次聚焦运行未通过 | 早期测试入口/目标框架和测试夹具问题在后续同框架聚焦及全量门禁前完成调整；原始输出按顺序保留 | `01-core-focused-first.txt` 至 `11-host-decoder-net10.txt` |
| 集成修正 R1—R3 首次反例回归失败 | 先保存 qualifier 值为 `01`、跨 path/query 重复 AI、非法 authority 等反例的失败，再修正结构解析与重复检测 | `l1-red-core.log`、`l1-red-host.log`、`l1-red-host-retry.log` |
| 集成修正 R4/R8 首次浏览器失败 | 真实浏览器证实粘贴 URI 被已有设置覆盖且 Elements AI 被错误禁用；保留红测截图/context 后修正默认保留与按模式筛选 | `l1-red-browser.log`、`e2e-red-results/` |
| 集成修正 R5 首次浏览器验证接到旧 Host | 旧 Vite 代理仍连既存 5187；使用隔离 Host 5188 和 Vite 5174 后确认转换与渲染的 query placement 一致 | `l1-browser-gs1-green.log`、`l1-browser-placement-response.json`；旧 Host 输出保留 |
| 集成修正 R6 首次实例预览诊断 | 组件模板高度不足使真实预览先返回 TemplateInvalid；修正仅限测试夹具后，比较 Host 校验与 renderer 预览输出 | `l1-component-preview-diagnostic.log`、`l1-host-preview-green.log` |
| 集成修正第 2 轮 Host 输出锁定 | 5187 上已有 Host 占用默认编译输出；保留失败日志后改用隔离输出，不停止或覆盖既存服务 | `l1-host-red.log` |
| 集成修正第 2 轮旧引用诊断 | 禁止项目引用构建后误用旧输出，整类 25 项产生 13 失败/12 通过；排除此结果并改为隔离新 Host DLL，仅筛选本轮 5 个新增用例 | `l1-host-red-repro.log`、最终 `l1-host-green-final.trx` |
| 集成修正第 2 轮静态非法夹具断言 | TemplateLoader 返回既有 `errors` 响应形状，不包含校验 `status`；断言改为确认 AI 9999 诊断存在 | `l1-host-green-targeted.log`、最终 `l1-host-green-final.trx` |
| 集成修正第 2 轮浏览器红测 | 缺 data 显示空错误状态；首个有效重复实例覆盖第二项失败。修正实例聚合和 pending 映射后，缺数据/空 Items/恢复及混合失败定位均通过 | `l1-browser-red-r6r7.log`、`l2-browser-gs1-green.log`、`chromium-red-r6r7/` |

上述均已在修正后对应门禁通过；这些初次失败记录仍保留，没有删除。

## 剩余事项与边界

- `OPEN_ACTIONABLE=0`（本地第 2/3 轮）：R6/R7 新增反例已修复并通过定向回归，E1 报告已更正并补入 testhost 进程证据；R1—R5、R8、R9 沿用已关闭结论。F3/F4 未验收，等待主代理独立复核。本报告不关闭阶段或总计划。
- 外部 CI：Windows/Linux Actions 尚未执行，单独列为外部待验证事项。
- 实体扫描设备/打印机验收由用户排除；Chromium 验证了 Host 输出、保存重载、引擎预览和 Digital Link URI 语义。
- 解析、规范化与转换均为本地操作，不访问 Digital Link resolver；Normalize 不选择 canonical 域名。
- 旧 Host 缺少三项 GS1 能力中的任一项时，前端按协商结果停用 AI 编辑器。
- 构建日志含既存 ImageSharp NuGet 安全公告和前端 bundle 体积提示；门禁通过，本轮未改依赖或扩大到性能矩阵。

## 证据目录

完整门禁日志、运行时报告、消费者报告、文档 XML 输出、样例输出以及首次失败记录位于：

`artifacts/optimization-expansion-20261025/phase4/integration-r1/`

第 2/3 轮的代码红绿门禁、TRX、testhost 诊断日志、浏览器结果和命令台账位于：

`artifacts/optimization-expansion-20261025/phase4/integration-r2/`

原首轮通过范围与首次实现日志保留在 `artifacts/optimization-expansion-20261025/phase4/codex-continuation/`；主代理的独立 HTTP 反例及复核报告保留在 `artifacts/optimization-expansion-20261025/phase4/main-review/`，本轮未覆盖。

源码文献和冻结来源文件保留在原 `artifacts/optimization-expansion-20261025/phase4/sources/`，本次未覆盖。

## 后续候选证据补记

主代理独立验收已确认第四阶段 F3/F4 本地实现 CLOSED；本补记只校正后续候选证据引用，不重开前三阶段或第四阶段 finding，也不因文档修订重跑无关门禁。

修正1/2轮记录的 R1 全量结果、未记录的 testhost patch，以及旧消费者范围均是其各自候选的历史证据。它们不作为修正2之后当前 R7 模板产物的低 TFM 或包消费证明。第五阶段最终候选另外执行了五生产 TFM 的严格 Core/Templates XML 门禁（10/10），并以当前源码打包后让 net6 与现代 net10 消费同一组包：net6 选择 net6.0 资产并在 .NET 6.0.36 运行，net10 选择 net7.0 资产并在 .NET 10.0.11 运行；两种消费者都核对了候选包与还原包 SHA-256。准确命令、运行时、包哈希和日志见 `docs/optimization-expansion-phase5-2026-10-26.md` 及 `artifacts/optimization-expansion-20261025/phase5/final-candidate/candidate-packages/`、`package-net6-shortpath/`、`package-net10/20261026-075002-49c7b651/`。

阶段4修正2/3轮的 testhost patch 来源仍仅为 `*.diag.host.*.log`；外层 `*.diag.log` 是 VSTest 调度诊断，不能称作 testhost 运行时证明。第一轮 Chromium 211/211 成功与另一次定向红测 2 项失败仍分别记录，不能合并成一个运行计数。原有各轮命令、测试数量和首败保持历史原样。
