# 优化与能力扩展第一阶段实施报告（O1/O2/O3）

## 1. 范围与结论

本报告只记录第一阶段 O1（Host 升级）、O2（预览合并）、O3（跨平台验证）的实现与本地验证。总计划状态由主代理维护，本报告不把十项总计划标记为完成。

- 起点提交：`6c707be4092dada2dabe345a9eb92e1e907c1acb`
- 工作区源码身份：HEAD 仍为 `6c707be`，另有本阶段未提交改动；代码、测试、脚本、工作流和活动的非报告文件合并哈希为 `74fdfec68284b06346fac04b3bfade190220e4332d022d22cc18b9335f917fe1`。该哈希不包含本报告、`docs/` 和其他文档改动。
- 实现：O1、O2、O3 本地可执行部分完成。
- 测试：第一阶段受影响门禁全部通过；Windows 本地同时完成真实 `net6.0`、`netcoreapp3.1` 验证。
- 外部门禁：GitHub Actions Windows/Linux 实际运行未执行，状态为 `BLOCKED_EXTERNAL`；不以本地 Windows 结果替代。
- 实机验收：按用户要求排除。
- 版本、包版本、API 基线、跟踪中的生成 XML 均未发布或提交；核心库和模板库仍保留原五个生产 TFM。

## 2. 变更与契约

### 2.1 O1：DesignerHost 升级到 .NET 10

- `tools/DesignerHost/DesignerHost.csproj` 和 `tests/DesignerHost.Tests/DesignerHost.Tests.csproj` 的目标框架由 `net8.0` 改为 `net10.0`。
- `tools/Verify-DesignerHost.ps1` 默认框架改为 `net10.0`，默认 TRX 名改为 `designer-host-net10.trx`。
- 当前操作文档同步到 .NET 10，包括 README、`docs/development.md`、`docs/designer-host.md`、`docs/designer.md` 和当前 FNC1 验证入口。历史 `net8.0` 验收报告和计数保留原日期与原结果。
- 官方 .NET 支持策略（2026-10-13 更新）给出的 .NET 10 最新修补版本为 `10.0.13`，对应 SDK `10.0.402`。本机全局安装为 SDK `10.0.400`、运行时 `10.0.11`；本轮使用隔离目录 `C:\Users\jianx\AppData\Local\Temp\opencode\dotnet-sdk-10.0.402` 中的官方 SDK ZIP 验证，未安装、卸载或改写全局 SDK。
- .NET 10 SDK 的 NuGet 审计会把已有 ImageSharp 2.1.11 的 `NU1902`/`NU1903` 作为错误。按硬约束未升级 ImageSharp；Host 和 Host 测试项目使用 `WarningsNotAsErrors` 保留这两个警告但不再提升为错误，使构建继续且剩余告警仍可见。安全告警和后续依赖迁移继续按既有已接受限制跟踪。
- 未升级 ImageSharp、ImageSharp.Drawing、ZXing.Net、Newtonsoft.Json 或其他核心依赖。

### 2.2 O2：单次渲染预览包

新增 `POST /api/designer/preview-bundle` 和协商特性 `preview-bundle`。请求沿用 `/preview` 的模板、数据、资源、协议和输出配置，并支持：

- `pageIndex`：要生成图片的当前页，省略为 0；负值或超出页范围返回 `InvalidPageIndex`。
- `imageTypes`：可选元素类型数组；省略表示生成当前协议支持的全部类型，空数组表示不生成图片；非 0–11、重复项或超过 12 项返回 `InvalidImageTypes`。

每次请求只调用一次 `TemplateRenderer.RenderWithLayoutAndPrimitives`，复用该次布局和 primitive ownership。ZIP 契约固定为：

- `manifest.json`
  - `contract`：契约版本和特性列表。
  - `layout.pageIndex`、`layout.pageCount`、`layout.pages`：`layout.pages` 与 `/preview` 的 `pages` 同形，保留全部页布局。
  - `images`：`key`、`file`、`pageIndex`、`elementIndex`、`type`、`sourcePath`、`width`、`height`。`key` 与设计器 `previewElementKey` 一致。
  - `diagnostics`：单元素码图失败，包含 `pageIndex`、`elementIndex`、`type`、`sourcePath`、`code`、`phase: PreviewImage` 和 `message`。
- `images/0001.png` 等 PNG 条目。

失败和资源规则：

- 普通单元素码图失败只写入 `diagnostics`，不删除布局或其他图片。
- 取消、整体渲染错误和资源预算超限在返回完整响应前失败，不返回半个 ZIP。
- ZIP、PNG、RGBA、布局清单和临时缓冲共用现有请求工作预算；布局/图片清单元数据估计和 manifest 序列化 scratch 以 `Host.PreviewBundleMetadata` 计入，实际 manifest JSON 缓冲以 `Host.PreviewBundleManifestBuffer`/`Host.PreviewBundleManifestOutput` 计入。
- 旧 `/preview`、`/element-png`、`/page-png` 保持原契约；没有跨请求缓存。

前端行为：

- `api.ts` 协商到 `preview-bundle` 后只发一次当前页请求；未协商到时保留旧 `/preview` 加逐元素 `/element-png` 链路。
- 保留原有 400ms 去抖、`AbortController`、失效请求判断和 object URL 释放。
- 切换页面时只请求目标页图片；旧 Host 回退链路也只请求目标页元素 PNG。
- 普通元素码图失败显示在诊断区域，布局仍保留。
- 预览包清单和图片在浏览器端解析为现有布局与 object URL；没有引入服务器端共享缓存。

### 2.3 O3：跨平台验证

- `tests/Bing.EasyPrint.Tests` 增加 `net10.0` 验证目标；生产核心库和模板库仍为 `net7.0;net6.0;netcoreapp3.1;netstandard2.0;netstandard2.1`。
- 新增 `.github/workflows/designer-cross-platform.yml`：
  - Windows：.NET 10 Host、核心 net10、真实 net6/netcoreapp3.1、前端 build/Vitest/Chromium E2E、现代包消费。
  - Linux：.NET 10 Host、核心 net10、前端 build/Vitest/Chromium E2E、现代包消费；不运行或冒充 net6/netcoreapp3.1。
  - 两条作业都先安装或验证常规、粗体和 CJK 系统字体；Linux 使用 `fonts-dejavu-core` 和 `fonts-noto-cjk`，固定字体继续使用 `tests/Fixtures/Fonts`。
- 新增 `tools/Verify-SystemFonts.ps1`：Windows 检查字体文件签名和 CJK 资源，Linux/macOS 通过 `fc-match` 验证 DejaVu Sans 常规/粗体及 Noto Sans CJK；失败时不静默跳过。
- 新增 `tools/Verify-ModernConsumption.ps1`：在短临时工作目录打包当前源码、重定向 XML 输出、运行 `net10.0` 包消费者，再把消费者证据复制到指定证据目录，避免 Windows 长路径和覆盖跟踪 XML。
- `Verify-PackageConsumer.ps1` 支持 `net10.0`，并修正目录型 dotnet 路径、`HOME` 回退和路径分隔符以支持现代跨平台消费。
- `Verify-ProtocolRuntimes.ps1` 支持 `net10.0` 和 `-PreferPortableRuntimes`；Windows 旧框架仍验证 testhost 实际运行时，缺少旧运行时不替代。
- 新增 `tools/PreviewBundleProbe`：固定 net10 多元素模板，同一候选比较旧多请求链路与 bundle 的请求次数、耗时和分配；不设置性能阈值。

## 3. 覆盖矩阵

| 范围 | 覆盖证据 |
| --- | --- |
| 一次渲染 | `PreviewBundleHostTests.Bundle_renders_once_and_matches_element_png_pixels`：注入渲染委托计数为 1，多元素 PNG 与原 `/element-png` 逐像素一致 |
| 多页当前页 | `Bundle_selects_the_requested_page_and_only_emits_that_pages_images`：请求第 2 页，清单含 2 页布局但图片只属于第 2 页 |
| 普通元素失败 | `Bundle_keeps_layout_when_an_ordinary_element_has_no_software_image`：布局保留、有效元素图片保留、失败进入 `PreviewImage` 诊断 |
| 真实编码异常 | `Bundle_keeps_layout_when_real_element_encoding_throws`：在真实 primitive 映射和编码阶段注入单元素异常，其他元素仍输出图片，失败进入 `PreviewImage/InternalError` 诊断 |
| 取消与预算 | `Bundle_cancellation_releases_resources_without_output`、`Bundle_cancellation_after_render_releases_resources_without_output`、`Bundle_metadata_budget_failure_rejects_before_partial_zip`、`Bundle_resource_budget_failure_does_not_return_a_partial_zip` |
| 元数据预算 | `Bundle_metadata_budget_failure_rejects_before_partial_zip`：高压缩率 1000 元素布局、`imageTypes=[]`，同一限制下 `/preview` 成功而 bundle 在 `Host.PreviewBundleMetadata` / `Host.PreviewBundleManifestBuffer` 阶段 413，无 ZIP 且预算归零 |
| 参数拒绝 | `Bundle_rejects_invalid_image_types_and_page_index` |
| 协商 | Host 测试检查 `preview-bundle` 响应特性；前端 Vitest 检查必需特性并确认只发 1 次 bundle 请求 |
| 旧 Host 回退 | Vitest 检查未协商时走 `/preview` 加目标页 `/element-png`；浏览器用例检查缺少 PNG 能力时保持布局并禁用下载；旧 Host 2→1 页缩页恢复第一页并保留引擎预览与当前页图片 |
| 页切换/竞态 | `e2e/previewBundle.spec.ts` 和 `e2e/previewRaces.spec.ts`：当前页只发目标页图片、连续翻页和整页刷新在途时翻页均不覆盖最终页，旧请求取消 |
| 浏览器联动 | 集成修正后完整 Chromium E2E 197/197，`retries=0`、`workers=1`；含新增预览竞态、缩页和原 raster/GS1/Composite 用例 |
| Host 升级 | Host 271/271；TRX 内适配器环境显示 `.NET 10.0.13`，NuGet 审计警告保留为 warning |
| 字体 | `Verify-SystemFonts.ps1` 在 Windows 通过；Linux 常规样式按实际 `Book`/非粗体匹配，实机 Linux 执行仍为外部缺口 |
| 现代消费 | `Verify-ModernConsumption.ps1 -TargetFramework net10.0`：汇总分别记录 pwsh 与 consumer 运行时，consumer 为 `.NET 10.0.13`，候选包哈希匹配，消费者 PASS |
| 旧消费冒烟 | 同一候选包 `net6.0` 消费者：`.NET 6.0.36`，PASS |
| 旧框架 | Windows 真实 `net6.0` 4626/4626、`netcoreapp3.1` 4626/4626，运行时位置由 testhost 诊断日志核对 |
| 五框架 XML | Core/Templates × 5 TFM 共 10/10 通过，文档警告作为错误 |

## 4. 精确命令、实际框架与结果

隔离工具链：

```powershell
$dotnet = 'C:\Users\jianx\AppData\Local\Temp\opencode\dotnet-sdk-10.0.402\dotnet.exe'
& $dotnet --info
# SDK 10.0.402, Microsoft.NETCore.App 10.0.13
```

Host 与测试：

```powershell
$env:PATH='C:\Users\jianx\AppData\Local\Temp\opencode\dotnet-sdk-10.0.402;' + $env:PATH
$env:DOTNET_ROOT='C:\Users\jianx\AppData\Local\Temp\opencode\dotnet-sdk-10.0.402'
pwsh ./tools/Verify-DesignerHost.ps1 `
  -OutputDirectory artifacts/optimization-expansion-20261025/phase1/integration-r1-host-final-20261025-163021/bin `
  -ResultsDirectory artifacts/optimization-expansion-20261025/phase1/integration-r1-host-final-20261025-163021/tests
```

实际结果：`net10.0`，271 通过，0 失败，0 跳过。TRX：`artifacts/optimization-expansion-20261025/phase1/integration-r1-host-final-20261025-163021/tests/designer-host-net10.trx`。

核心 net10：

```powershell
& $dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net10.0 `
  --logger "trx;LogFileName=core-net10.trx" `
  --results-directory artifacts/optimization-expansion-20261025/phase1/core-net10-20261025-153810 `
  --diag artifacts/optimization-expansion-20261025/phase1/core-net10-20261025-153810/core-net10.log `
  -m:1 /p:BuildInParallel=false --no-restore
```

实际结果：`net10.0`，实际运行时 `10.0.13`，4626 通过，0 失败，0 跳过。TRX：`artifacts/optimization-expansion-20261025/phase1/core-net10-20261025-153810/core-net10.trx`。

旧框架真实运行时：

```powershell
pwsh ./tools/Verify-ProtocolRuntimes.ps1 -Frameworks @('net6.0','netcoreapp3.1') `
  -ResultsDirectory artifacts/optimization-expansion-20261025/phase1/legacy-runtimes-20261025-153725
```

实际结果：`net6.0` 4626/4626、`netcoreapp3.1` 4626/4626；运行时位置分别为对应共享框架版本。报告：`artifacts/optimization-expansion-20261025/phase1/legacy-runtimes-20261025-153725/20261025-073725-17f0f5b8/runtime-verification.json`。

前端：

```powershell
cd web/Bing.EasyPrint.Designer
npm run build
npm test
npx playwright test --retries=0 --workers=1
```

实际结果：生产构建成功（保留既有 500 kB 主块提示）；Vitest 417/417；Chromium E2E 197/197、0 失败、0 跳过、`retries=0`、`workers=1`。日志：`artifacts/optimization-expansion-20261025/phase1/integration-r1-frontend-build-final.log`、`integration-r1-vitest-final.log`、`integration-r1-e2e-final2.log`。核心生产代码和五框架 XML 未在本轮变更，仍复用 `core-net10-20261025-153810`、旧框架运行报告和 `xml-documentation-20261025-154307`。

五框架 XML：

```powershell
pwsh ./tools/Verify-XmlDocumentation.ps1
```

实际结果：Core/Templates × `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1` 共 10/10 通过。证据已复制到 `artifacts/optimization-expansion-20261025/phase1/xml-documentation-20261025-154307/`。

现代包消费：

```powershell
pwsh ./tools/Verify-ModernConsumption.ps1 -TargetFramework net10.0 `
  -ResultsDirectory artifacts/optimization-expansion-20261025/phase1/integration-r1-modern-consumer-20261025-162041 `
  -DotNetPath C:\Users\jianx\AppData\Local\Temp\opencode\dotnet-sdk-10.0.402
```

实际结果：包哈希匹配，PASS。汇总现在分别记录 `pwshFrameworkDescription=.NET 10.0.8` 与 `consumerFrameworkDescription=.NET 10.0.13`，目标消费运行时以 consumer 证据为准。证据：`artifacts/optimization-expansion-20261025/phase1/integration-r1-modern-consumer-20261025-162041/`；stdout/stderr 和退出码：`integration-r1-modern-console-20261025-162041.log`、`integration-r1-modern-consumer-20261025-162041.exit-code.txt`。

旧消费脚本冒烟（复用同一候选包，真实 `net6.0` 运行时）：

```powershell
pwsh ./tools/Verify-PackageConsumer.ps1 `
  -CandidatePackagesDirectory <modern-candidate-packages> `
  -ResultsDirectory artifacts/optimization-expansion-20261025/phase1/legacy-consumer-smoke `
  -TargetFramework net6.0
```

实际结果：`FrameworkDescription=.NET 6.0.36`，PASS；证据：`artifacts/optimization-expansion-20261025/phase1/legacy-consumer-smoke/consumer-net6.0/consumer-verification.json`。

系统字体：

```powershell
pwsh ./tools/Verify-SystemFonts.ps1 -OutputDirectory artifacts/optimization-expansion-20261025/phase1/fonts-check
```

实际结果：Windows 常规、粗体和 CJK 字体检查 PASS；证据：`artifacts/optimization-expansion-20261025/phase1/fonts-check/system-font-verification.json`。

## 5. 定向性能证据

固定模板：同一 `net10.0` 候选、同一 ESC/POS Raster 页面、8 个 Code128 元素和 2 个 QR 元素；旧链路为 1 次 `/preview` 加逐元素 `/element-png`，新链路为 1 次 `/preview-bundle`。每个样本 3 次迭代，共 5 个样本，取中位数；不设置性能阈值，不把 net8 与 net10 混为预览收益。

集成修正增加了 manifest 元数据和序列化缓冲预算预留，旧候选的诊断/确认对比不再是同一源码候选，不能用于证明修正后性能。修正后只重跑一次针对性确认：

| 运行 | 链路 | 端点调用 | 中位耗时 | 中位分配 | 响应字节 |
| --- | --- | ---: | ---: | ---: | ---: |
| 集成修正后确认 | 旧多请求 | 12 | 45.388 ms | 11,591,696 B | 6,973 |
| 集成修正后确认 | preview-bundle | 1 | 8.412 ms | 1,208,773 B | 5,105 |

证据：`artifacts/optimization-expansion-20261025/phase1/integration-r1-preview-probe-final/preview-bundle-integration-r1-confirmation-final.json`。旧候选数据保留在 `preview-probe-diagnostic/`、`preview-probe-confirmation/`，仅作历史记录，不冒充当前候选。这些值只说明同一候选和固定负载下的调用次数与测量结果，不作为发布阈值。

## 6. 首次失败、修复与处置

1. **核心 net10 首次全量 2 项失败**：两个 `ProtocolRestrictedRawTests` 断言消息包含大写 `TSPL`。net10 对含别名 `TSPL = Tspl` 的枚举格式化返回 `Tspl`，net6 当时返回 `TSPL`；这是测试对运行时字符串形式的依赖。改为大小写不敏感匹配后，同一 net10 完整套件 4626/4626 通过。失败证据保留在 `core-net10-20261025-153401/core-net10.trx`。
2. **现代消费首次失败与原始证据缺口**：证据目录过深导致 Windows 长路径下包内 DLL 复制失败（`MSB3030`）。独立复核确认 `modern-consumer-diagnostic/` 只保留项目生成文件，未找到原始命令 stdout/stderr、binlog 或失败结果记录；因此原始失败日志标记为**证据缺失**，不重造历史输出。脚本随后改为短临时工作目录打包和消费；集成修正后的新运行从开始保存 stdout/stderr 和退出码，见 `integration-r1-modern-console-20261025-162041.log` 与 `.exit-code.txt`。
3. **完整 Chromium E2E 并行运行 1 项失败**：12 workers 下 `rasterPreviewPng.spec.ts` 的测试路由读取 `route.fetch()` 响应时出现 `apiResponse.json: Response has been disposed`；单文件串行重跑通过。集成修正后按当前文档和 CI 统一使用 `--workers=1 --retries=0`，完整运行 197/197 通过。未增加重试或超时。并行失败日志保留在 `e2e-full-final2.log`，不把串行结果冒充并行运行。
4. **.NET 10 构建的 NuGet 审计错误**：ImageSharp 2.1.11 的 `NU1902`/`NU1903` 在 Host 项目 `TreatWarningsAsErrors` 下阻断构建。集成修正将 `NoWarn` 改为 `WarningsNotAsErrors`，保留审计警告但不提升为错误；依赖版本不变，构建输出仍可见剩余告警。
5. **跟踪 XML 被构建覆盖**：常规构建会写 `src/Bing.EasyPrint*/Bing.EasyPrint*.xml`。所有验证后已用 Git 恢复这两个跟踪文件；最终工作区不包含生成 XML 改动。

## 7. 已知限制与外部缺口

- GitHub Actions Windows/Linux 只在本地完成 YAML 解析和静态检查，未实际运行；状态 `BLOCKED_EXTERNAL`。Linux 字体、路径大小写、编码和 Chromium 结果仍待远端作业验证。
- 本机 Windows 系统字体检查通过，但没有在无字体环境验证失败路径；CI 中使用独立安装的 Linux 字体包。
- ImageSharp 2.1.11 及 ImageSharp.Drawing 1.0.0-beta15 的已有安全告警未处理；升级仍受五生产 TFM 约束，继续单独跟踪。
- 预览包只生成当前页图片，不建立跨请求缓存；重复请求仍重新渲染，这是本次契约。
- 设计器并行 E2E 的 Playwright 路由处置问题属于测试基础设施；本地文档和 CI 已统一使用 `--workers=1 --retries=0`，未把它伪装成产品通过。
- 首次现代消费的原始 `MSB3030` 命令输出/binlog 未保存，已作为原始证据缺口记录；集成修正后的运行已保存 stdout/stderr 和退出码。
- 实机打印、设备字体、固件专属像素行为和真实扫码结果不在本阶段范围。
- 未暂存、提交、推送、发布、打标签或修改产品/包版本。

## 8. 集成修正第 1 轮：R1—R11 处置

| ID | 本地状态 | 修正与证据 |
| --- | --- | --- |
| P1-R1 | CLOSED | 页面切换改为走统一的 `refreshPreview(targetPage)` 请求归属；`invalidatePreview` 统一中止在途整页/翻页请求，迟到结果在 `previewRequest` 失效检查后释放已创建 object URL。`e2e/previewRaces.spec.ts` 新增受控延迟的“整页刷新在途翻页”和“连续翻页”用例；原编辑打断用例保留；`previewLifecycle.test.ts` 验证丢弃结果逐项释放 object URL。日志：`integration-r1-e2e-final2.log`。 |
| P1-R2 | CLOSED | 旧 Host 回退缩页改为结构化 `DesignerValidationError`（`InvalidPageIndex`），App 统一恢复第一页并重新请求当前页图片。Vitest 增加 2→1 页结构化错误用例；Chromium 用例验证无 bundle 协商的真实 Host 回退从第 2 页缩到 1 页后仍显示引擎预览和 pageIndex 0 元素图片。 |
| P1-R3 | CLOSED | 新增 `Host.PreviewBundleMetadata`（布局/元素/图片清单元数据估计 + 序列化 scratch）和 `Host.PreviewBundleManifestBuffer`/`Host.PreviewBundleManifestOutput`（实际 JSON 缓冲）预算；manifest 不再直接写压缩 entry。`Bundle_metadata_budget_failure_rejects_before_partial_zip` 使用高压缩率 1000 元素布局和 `imageTypes=[]`，证明 `/preview` 成功而 bundle 在 metadata/manifest buffer 阶段 413、无部分 ZIP、预算归零。 |
| P1-R4 | CLOSED | `Bundle_cancellation_after_render_releases_resources_without_output` 在真实 render 返回并产生 primitives 后取消，验证无输出和预算归零；`Bundle_metadata_budget_failure_rejects_before_partial_zip` 证明包生成阶段预算失败；`Bundle_keeps_layout_when_real_element_encoding_throws` 通过受控单元素编码异常验证其他元素仍输出图片、失败进入 `PreviewImage` 诊断。 |
| P1-R5 | CLOSED | Host/Host 测试项目由全面 `NoWarn` 改为 `WarningsNotAsErrors=NU1902;NU1903`；构建继续且 ImageSharp 审计警告仍可见，依赖版本未变。 |
| P1-R6 | CLOSED（代码）/ BLOCKED_EXTERNAL（Linux 实跑） | Linux 常规字体不再强制字面 `Regular`，接受 DejaVu Sans 的 `Book` 等非粗体常规样式，并排除 Bold/Oblique/Italic；粗体和 CJK 仍明确校验。Windows 字体脚本 PASS；Linux 实际运行仍待外部 CI。 |
| P1-R7 | CLOSED（工作流）/ BLOCKED_EXTERNAL（实跑） | Windows/Linux E2E 均显式 `--workers=1 --retries=0`，与本地稳定门禁一致；外部实际运行待 CI。 |
| P1-R8 | CLOSED（工作流）/ BLOCKED_EXTERNAL（实跑） | Windows 前端 build 和 Vitest 拆为独立 step，并逐条检查 `$LASTEXITCODE`；Linux 同样拆分。 |
| P1-R9 | CLOSED（工作流）/ BLOCKED_EXTERNAL（实跑） | Playwright 使用 `--reporter=list,json`，JSON 输出到 ci 证据目录；`test-results`、前端 build/Vitest 日志和 E2E 日志纳入 `if: always()` 上传。 |
| P1-R10 | CLOSED | `Verify-ModernConsumption.ps1` 汇总分别记录 `pwshFrameworkDescription` 和从 `consumer-verification.json` 读取的 `consumerFrameworkDescription`/`coreLibLocation`/`host`。新运行：pwsh `.NET 10.0.8`，consumer `.NET 10.0.13`。 |
| P1-R11 | CLOSED（缺口声明）/ 历史证据缺失 | 独立盘点确认首次 `MSB3030` 原始 stdout/binlog 未保存；报告已明确标记原始证据缺失，不重造历史。集成修正后的现代消费运行从开始保存 stdout/stderr、退出码和 JSON 证据。 |

本轮定向和收口验证：

- L1 Host：`integration-r1-host-final-20261025-163021`，271 通过、0 失败、0 跳过。
- 前端构建：`integration-r1-frontend-build-final.log`，成功（保留既有主块提示）。
- Vitest：`integration-r1-vitest-final.log`，417/417。
- 受影响 E2E：`e2e/previewRaces.spec.ts` 4/4；完整 Chromium 阶段收口：`integration-r1-e2e-final2.log`，197/197，`workers=1`、`retries=0`。
- 核心 net10、旧框架、五框架 XML：核心/模板生产代码未改，复用 `core-net10-20261025-153810`、`legacy-runtimes-20261025-153725`、`xml-documentation-20261025-154307`。
- 现代消费：`integration-r1-modern-consumer-20261025-162041`，PASS。
- 字体：`integration-r1-fonts/`，Windows PASS。
- 性能：`integration-r1-preview-probe-final/preview-bundle-integration-r1-confirmation-final.json`；旧候选性能记录仅作历史，不用于当前候选结论。
- 首次失败保留：并行 E2E `e2e-full-final2.log`；现代消费原始输出缺口按 R11 明确记录。

## 9. 证据索引

总清单：`artifacts/optimization-expansion-20261025/phase1/evidence-manifest.json`。

- Host（集成修正后）：`integration-r1-host-final-20261025-163021/`
- Core net10（复用）：`core-net10-20261025-153810/`
- 旧框架（复用）：`legacy-runtimes-20261025-153725/`
- 前端构建/Vitest：`integration-r1-frontend-build-final.log`、`integration-r1-vitest-final.log`
- Chromium E2E：`integration-r1-e2e-final2.log`（历史并行首次失败：`e2e-full-final2.log`）
- XML（复用）：`xml-documentation-20261025-154307/`
- 现代消费：`integration-r1-modern-consumer-20261025-162041/`
- 现代消费控制台：`integration-r1-modern-console-20261025-162041.log`、`integration-r1-modern-consumer-20261025-162041.exit-code.txt`
- 旧消费冒烟：`legacy-consumer-smoke/`
- 系统字体：`integration-r1-fonts/`
- 预览性能：`integration-r1-preview-probe-final/`；历史：`preview-probe-diagnostic/`、`preview-probe-confirmation/`
