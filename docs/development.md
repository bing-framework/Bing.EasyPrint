# 开发与验证

新增行为和修复按 [TDD 开发与回归测试](tdd-guide.md) 保存真实红绿证据；近期扩展功能的测试入口和本轮补测结果也由该指南索引。

当前候选的 Windows/Linux CI 证据要求、用户确定的旧运行时范围及已批准的性能负载、指标、门槛与实测结果见 [候选验收记录](acceptance-2026-11-06.md)。正式性能入口为 `tools/Invoke-PerformanceAcceptance.ps1`，门禁复算入口为 `tools/Test-PerformanceAcceptance.ps1`。

DataBar Truncated 与 Stacked Omnidirectional 已接入 Composite Auto/CC-A/CC-B；新增 `BarcodeType.Gs1DataBarStackedOmnidirectional=32` 支持普通 ESC/POS Raster。Truncated Composite 条高至少 13X；Stacked Omnidirectional 的条高为每条数据行高度、至少 33X，另有三个内部分隔行。完整输入、尺寸与旧 Host 协商见[使用说明](gs1-composite-databar-completion.md)。 本轮验证见[阶段报告](databar-completion-verification-2026-10-22.md)，旧报告保留其日期及原结果。

EAN-8/EAN-13/UPC-A/UPC-E Composite 已接入四种图片协议、V2/V3 模板和设计器，支持 Auto/CC-A/CC-B 及严格 2/5 位附加码；主码校验规则不变。基础输入、尺寸和案例见[EAN/UPC Composite 使用说明](gs1-composite-ean-upc.md)，附加码约束与协商见[附加码使用说明](gs1-composite-ean-upc-addons.md)。既有基础报告与本轮附加码报告分别见[基础报告](ean-upc-composite-verification-2026-10-22.md)和[附加码报告](ean-upc-addons-verification-2026-10-22.md)。

ExpandedStacked 已支持普通 ESC/POS Raster 条码及四协议 Composite CC-A/CC-B；省略或 `null` 默认每行两个字符对，普通条码可配置 1–11 列、Composite 可配置 2–11 列。显式配置列数/行数需 `gs1-databar-expanded-stacked-columns` / `gs1-databar-expanded-stacked-rows`，Host 同时声明对应范围。详细契约见[列数配置指南](expanded-stacked-columns.md)和 [ExpandedStacked 使用说明](gs1-composite-expanded-stacked.md)，历史阶段报告保留其原范围。

当前 GS1-128 Composite 支持显式 CC-A/CC-B，Auto 仍选择 CC-C；A/B 的 Host 门控特性为 `gs1-composite-gs1-128-ab`。详细契约见[GS1-128 A/B 使用说明](gs1-composite-gs1-128-ab.md)，本阶段结果见[A/B 阶段验收报告](gs1-128-ab-verification-2026-10-22.md)。下文各历史阶段的计数保留原日期与范围。

2026-10-22 新增单行 DataBar Expanded 与 DataBar Stacked + CC-A/CC-B Composite。使用和输入限制见[Expanded 使用说明](gs1-composite-expanded.md)及[Stacked 使用说明](gs1-composite-stacked.md)，阶段验证分别记录在[Expanded 报告](gs1-composite-expanded-verification-2026-10-22.md)和[Stacked 报告](gs1-stacked-verification-2026-10-22.md)。下文较早轮次结果保留其当时范围。

GTIN Digital Link、Host GS1 AI 目录/验证/转换和结构化 AI 编辑器使用固定 GS1 Syntax Dictionary，不更新版本或默认模板行为。核心/模板/Host 的定向验证类为 `Gs1DigitalLinkTests`、`Gs1StructuredTemplateTests`、`Gs1DigitalLinkHostTests`；浏览器用例位于 `e2e/gs1AiEditor.spec.ts`。完整契约与 C#/JSON 案例见[GS1 Digital Link 与结构化 AI](gs1-digital-link.md)。

## 项目与依赖

| 项目 | 目标框架 | 主要依赖 |
| --- | --- | --- |
| `src/Bing.EasyPrint` | net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1 | ImageSharp、ImageSharp.Drawing、ZXing.Net |
| `src/Bing.EasyPrint.Templates` | 同核心库 | 核心库、Newtonsoft.Json、ZXing.Net |
| `tools/DesignerHost` | net10.0 | Templates、ASP.NET Core |
| `web/Bing.EasyPrint.Designer` | Node/Vite | Vue、Konva、JSZip、Lucide |

测试项目位于 `tests/Bing.EasyPrint.Tests`、`tests/DesignerHost.Tests` 和 `tests/PackageConsumer`；性能/资源探针位于 `tools/PerformanceProbe`。

2026-10-22 的 CC-C 后续阶段已完成本地验收，当前命令、覆盖矩阵及结果见[CC-C 阶段报告](gs1-composite-cc-c-verification-2026-10-22.md)。本页较早的 3397/140/277/154 计数属于同日修复阶段的历史记录。

## 常用命令

```powershell
dotnet build src/Bing.EasyPrint/Bing.EasyPrint.csproj
dotnet build src/Bing.EasyPrint.Templates/Bing.EasyPrint.Templates.csproj
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net6.0
./tools/Verify-DesignerHost.ps1
./tools/Verify-SystemFonts.ps1
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net10.0
./tools/Verify-ModernConsumption.ps1 -TargetFramework net10.0
./tools/Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1
./tools/Verify-XmlDocumentation.ps1
dotnet run --project samples/Templates/Templates.csproj -- artifacts/template-samples
```

```powershell
cd web/Bing.EasyPrint.Designer
npm ci
npx playwright install chromium
npm run build
npm run test
npm run test:e2e
```

具体改动应优先运行对应的定向测试，再按影响范围扩展到项目测试和多目标框架构建。文档改动使用 Markdown、链接和编码检查即可。

`Verify-DesignerHost.ps1` 将 Host 测试输出和 TRX 写入独立的 `artifacts` 目录，并串行执行构建。Host 项目已将项目目录内的 `artifacts/**` 与 `output/**` 追加到 `DefaultItemExcludesInProjectFolder`；四个示例 JSON 仍由显式 `Content` 项加入，因此默认脚本可在保留生成物时运行。首次运行浏览器验收前执行 `npm ci` 和 `npx playwright install chromium`。

GS1 关联与 GS1 二维编码定向验证：`dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net6.0 --filter "FullyQualifiedName~Gs1AssociationValidatorTests|FullyQualifiedName~Gs1TwoDimensionalEncoderTests|FullyQualifiedName~Gs1TwoDimensionalTemplateTests|FullyQualifiedName~Gs1CompositeTemplateTests"`；Host：`dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj -f net10.0 --filter "FullyQualifiedName~Gs1TwoDimensionalHostTests|FullyQualifiedName~Gs1CompositeHostTests"`；浏览器：`npx playwright test e2e/gs1TwoDimensional.spec.ts e2e/ccC.spec.ts --workers=1 --retries=0`。过滤表达式的引号用于防止 PowerShell 将 `|` 解释为管道。

FNC1 官方对照使用离线夹具，不联网下载：`dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net6.0 --filter FullyQualifiedName~Gs1SyntaxDictionaryTests`。真实码图和 PNG 导出使用 `dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj -f net10.0 --filter FullyQualifiedName~RasterPreviewHostTests`，其中用 ZXing 独立解码 Code128、EAN13、QR、DataMatrix 和 PDF417，用 Composite 外部向量核对尺寸。浏览器定向用例为 `npx playwright test e2e/rasterPreviewPng.spec.ts --workers=1 --retries=0`；完整计数和已知限制见[FNC1 与预览导出验收](fnc1-preview-png-verification-2026-10-23.md)。

`Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` 会分别执行完整 Core 测试并从 testhost 诊断日志确认实际加载的运行时；报告和日志保存在 `artifacts/protocol-runtime-verification/<run-id>/`。只有实际运行时与目标框架匹配且全部测试通过才记为通过，缺少旧运行时不能用其他运行时替代。需要固定运行时目录时加 `-PreferPortableRuntimes`，脚本只使用 `-RuntimeDirectory` 中已准备的官方 Windows x64 运行时 ZIP，不下载也不安装。

系统字体门禁先运行 `./tools/Verify-SystemFonts.ps1`：Windows 检查常规、粗体和 CJK 字体文件，Linux/macOS 通过 `fc-match` 确认 DejaVu Sans 常规/粗体及 Noto Sans CJK；缺失时立即失败，不跳过系统字体用例。CI 在 Linux 使用 `fonts-dejavu-core` 和 `fonts-noto-cjk`。

现代包消费使用 `./tools/Verify-ModernConsumption.ps1 -TargetFramework net10.0 -ResultsDirectory <证据目录>`。脚本在短临时工作目录中打包当前源码、重定向 XML 文档输出并运行 `net10.0` 消费者，避免 Windows 长路径和覆盖跟踪 XML；通过后把消费者证据复制到指定证据目录。跨平台工作流为 `.github/workflows/designer-cross-platform.yml`，Windows 额外运行真实 `net6.0`/`netcoreapp3.1` 运行时验证，Linux 只运行现代运行时，不把本机结果冒充远端 CI 结果。两平台 E2E 固定 `--workers=1 --retries=0`；build、Vitest、Playwright JSON/附件和现代消费 stdout/stderr 均写入 always 上传的证据目录。现代消费汇总分别记录 pwsh 与实际 consumer 运行时。

### 本地候选包消费

以下命令从当前源码生成本地候选包并运行 `net6.0` 消费验证，不发布 NuGet。每轮使用新的输出目录；候选包版本沿用 `1.0.0`，不为验证改动项目版本。

```powershell
$run = Get-Date -Format 'yyyyMMdd-HHmmss'
$candidate = Join-Path $PWD "artifacts/review-fixes-20261022/candidate-pack-$run"
New-Item -ItemType Directory -Force -Path $candidate | Out-Null
$targets = Join-Path $candidate 'Documentation.targets'
@'
<Project>
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'Bing.EasyPrint' Or '$(MSBuildProjectName)' == 'Bing.EasyPrint.Templates'">
    <DocumentationFile>$(MSBuildThisFileDirectory)xml/$(TargetFramework)/$(MSBuildProjectName).xml</DocumentationFile>
  </PropertyGroup>
</Project>
'@ | Set-Content -Encoding utf8 $targets
$previousTargets = $env:DirectoryBuildTargetsPath
try {
  $env:DirectoryBuildTargetsPath = $targets
  dotnet pack src/Bing.EasyPrint/Bing.EasyPrint.csproj -c Release -m:1 -o $candidate
  if ($LASTEXITCODE -ne 0) { throw 'Core pack failed.' }
  dotnet pack src/Bing.EasyPrint.Templates/Bing.EasyPrint.Templates.csproj -c Release -m:1 -o $candidate
  if ($LASTEXITCODE -ne 0) { throw 'Templates pack failed.' }
} finally {
  $env:DirectoryBuildTargetsPath = $previousTargets
}
./tools/Verify-PackageConsumer.ps1 -CandidatePackagesDirectory $candidate `
  -ResultsDirectory "artifacts/review-fixes-20261022/package-consumer-$run" -TargetFramework net6.0
```

上述临时 targets 将 XML 按项目及框架隔离到候选目录，避免项目引用共用同一 XML 路径或覆盖跟踪文件。消费者脚本会核对还原出的 `.nupkg` 与候选文件一致，再执行消费程序；结果位于 `artifacts/review-fixes-20261022/package-consumer-<run-id>/`。字体、协议及模板场景见 `tests/PackageConsumer/Program.cs`。

## G4 资源与失败恢复

打印、模板与 Host 共用预算、取消和所有权规则，原计划中的相关完成记录在此合并维护；协议行为见[协议文档 G1–G3](protocols.md)，逐项依据见[核对报告](plan-audit-2026-10-15.md)。

- 引擎分别限制输出、存储和工作集；Job 在展开前检查输出/页数，Raw tokenizer 按输入长度在分配前预留临时空间。Composite 的编码工作集、模块矩阵、图片快照及输出复用同一预算/取消链路，失败释放后可重试。
- 文档持有独立元素和图片数组副本，资源路径/快照归属固定；模板跨页失败与取消恢复不得污染后续渲染。临时租约和会话资源必须在成功、失败、取消路径释放，反白分页与静区失败也适用。
- 受限 Raw 校验采用失败原子性，不把失败输入写入命令或持久状态；其格式闭合、游标与重置规则以协议契约为准。
- Host 有请求预算、工作内存预算和 FIFO 准入；等待取消、退出释放与下一请求恢复有测试。请求限额和并发准入不等于进程隔离，隔离与监听信任边界仍未验收。
- 第三方同步调用只能在边界检查取消，内部对象/缓冲未完全计入预算，计数不等于实际进程堆上限；入口与归档容量不同，超限严格拒绝。Core 的共享 TIFF 检查器会在 CPCL 与模板入口调用 `Image.Identify` 前检查目录结构和偏移，并在遍历目录时观察取消；这不代表能中断任意格式的同步解码。ImageSharp 2.1.11 大版本迁移仍受五个目标框架约束，本次预检只针对已复现的 TIFF 目录问题，不宣称清除了该依赖的全部告警或安全边界；迁移与[相关安全公告](https://github.com/advisories/GHSA-wmxv-xphr-5c9g)继续单独跟踪。设备容量、发送中断、重试、重复打印及设备状态恢复由计划中的独立要求跟踪。

实现/测试入口：[存储预算](../src/Bing.EasyPrint/Core/PrintStorageBudget.cs)、[Raw tokenizer](../src/Bing.EasyPrint/Protocols/RestrictedRawSyntax.cs)、[工作集测试](../tests/Bing.EasyPrint.Tests/PrintWorkingBudgetTests.cs)、[文档快照测试](../tests/Bing.EasyPrint.Tests/PrintDocumentSnapshotTests.cs)、[Provider 归属测试](../tests/Bing.EasyPrint.Tests/TemplateProviderOwnershipTests.cs)、[Host 准入](../tests/DesignerHost.Tests/HostAdmissionTests.cs)、[请求预算](../tests/DesignerHost.Tests/HostRequestBudgetTests.cs)。

## G5 Host 与设计器闭环

- Host 通过版本/特性协商和严格 DTO 校验请求，返回稳定错误、字段路径与绑定诊断。能力响应与前端过滤共用协议准入规则，标签 QR rotation allowlist 的 JSON 请求已有集成回归。
- 设计器支持角度与属性编辑、旋转占位/结束坐标回写及 JSON/项目 ZIP 往返；离线结束坐标框不重复旋转。在线预览使用 Host 实际度量，模型测试和归档测试不替代浏览器交互验收。
- GS1 Composite 基础入口要求 `gs1-composite` 特性和能力确认；GS1-128 Auto/CC-C 要求 `gs1-composite-cc-c`，显式 CC-A/CC-B 还要求 `gs1-composite-gs1-128-ab` 以及 `UccEan128`、`CcA`、`CcB` 能力；EAN/UPC 组合还要求 `gs1-composite-ean-upc` 与四种零售线性类型及 Auto/CcA/CcB 能力，附加码另要求 `gs1-composite-ean-upc-addons`；Expanded 和 Stacked 分别要求 `gs1-composite-expanded` 和 `gs1-composite-stacked`。新 Host 在 `linearTypes` 的既有 Expanded、Stacked 项之后追加 `Gs1DataBarExpandedStacked`，再追加 `Ean8`、`Ean13`、`UpcA`、`UpcE`，本阶段末尾追加 `Gs1DataBarTruncated`、`Gs1DataBarStackedOmnidirectional`，共 12 项且 `modes` 保持原值；`primaryFormats` 在 `ean-upc-digits` 后追加 `ean-upc-digits-with-addon`。固定 `Field.Value` 附加码由浏览器发送独立必需特性并检查响应；动态 `Field.Name` 在 Host 完成绑定后严格校验，旧 Host 按纯数字格式拒绝带加号的动态值，同时保留纯数字请求。当前组合为 EAN-8/EAN-13/UPC-A/UPC-E、DataBar Omnidirectional/Limited/Expanded/Stacked/ExpandedStacked × Auto/CC-A/CC-B、GS1-128 × Auto/CC-A/CC-B/CC-C；EAN/UPC Composite 不支持 CC-C。非法组合导入或服务端预检会拒绝。画布在新 Host 下按元素显示真实码图 PNG；旧 Host 保持布局占位并标注“示意”。详细契约见 [GS1-128 CC-A/CC-B 使用说明](gs1-composite-gs1-128-ab.md)、[GS1-128 + CC-C Composite 使用说明](gs1-composite-cc-c.md)、[Expanded 使用说明](gs1-composite-expanded.md)、[Stacked 使用说明](gs1-composite-stacked.md)和[EAN/UPC 附加码使用说明](gs1-composite-ean-upc-addons.md)。
- ExpandedStacked 列数/行数使用 `ExpandedStackedColumns` / `ExpandedStackedRows`：省略或 `null` 默认 2 列，普通 ESC/POS Raster 列范围为 1–11，Composite 为 2–11；行数范围均为 2–11，且与列数互斥。显式配置（包括 2 列）要求对应特性及范围；旧 Host 可处理默认配置，但不能预览或导出显式配置。验收需覆盖字段往返、能力过滤、真实 Host 尺寸及旧 Host 降级，契约见[列数配置指南](expanded-stacked-columns.md)和[最大行数与 GS1 AI 字典](expanded-stacked-rows-and-gs1-ai.md)。
- 2026-10-22 早期五模式能力、DataBar Composite 交互和完整 Chromium E2E 结果仍按[修复验收报告](review-fixes-2026-10-22.md)留作历史记录；这些结果发生在 CC-C 集成前，不代表新路径验收。CC-C 定向入口：[核心向量测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeCcCTests.cs)、[固定向量](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeCcCVectors.cs)、[Host 测试](../tests/DesignerHost.Tests/Gs1CompositeHostTests.cs)；Stacked 阶段完整验收范围见[Stacked 阶段报告](gs1-stacked-verification-2026-10-22.md)。运行 [C#/JSON/往返样例](../samples/Templates/Gs1CompositeSamples.cs) 的命令见 `dotnet run --project samples/Templates/Templates.csproj -- artifacts/template-samples`。

实现/测试入口：[Host 契约](../tools/DesignerHost/DesignerContract.cs)、[请求处理器](../tools/DesignerHost/DesignerRequestProcessor.cs)、[Host 契约测试](../tests/DesignerHost.Tests/HostContractTests.cs)、[Composite Host 测试](../tests/DesignerHost.Tests/Gs1CompositeHostTests.cs)、[前端能力](../web/Bing.EasyPrint.Designer/src/capabilities.ts)、[属性编辑](../web/Bing.EasyPrint.Designer/src/components/PropertyInspector.vue)、[Composite 单元测试](../web/Bing.EasyPrint.Designer/src/gs1Composite.test.ts)、[旋转 E2E 用例](../web/Bing.EasyPrint.Designer/e2e/imageRotation.spec.ts)。

## 中文 XML 注释治理

当前待办和逐项状态由[中文注释计划](../ai_docs/plans/chinese-comments-plan.md)维护；逐项源码依据见[2026-10-15 核对报告](plan-audit-2026-10-15.md)。以下规则和数字记录扫描约定与历史证据；本轮严格门禁结果在下文单独记录。

### 扫描与文档约定

- 基线扫描范围为 `src/Bing.EasyPrint` 和 `src/Bing.EasyPrint.Templates`。排除 `bin`、`obj`、`*.g.cs`、`*.generated.cs`、`*.Designer.cs`、EF Core Migration/ModelSnapshot、自动生成客户端、代理代码和第三方源码；该基线不含测试、工具和设计器。新一轮应明确记录是否扩展范围。
- 按语法树声明计数；字段按变量计数，partial 类型按声明计数；不计隐式构造函数、访问器和本地函数。方法访问级别与 static 统计有重叠，类别缺陷数字不可直接求和。
- 对入选声明写准确的简体中文 XML 摘要；独立摘要使用三行格式。构造函数统一为“初始化一个 `<see cref="当前类型"/>` 类型的实例。”；属性按可见访问器使用“获取……”“设置……”“获取或设置……”或“获取或初始化……”，受限 setter 的说明放入 remarks。公共契约应反映单位、输入与空值约束、默认值、返回语义、资源所有权及错误行为。避免用泛泛描述替代实际契约；不为不存在的 Entity 层新增类型。
- 重写成员只有在基类或接口文档准确且适用于当前实现时才使用 `<inheritdoc />`；核对继承来源、重载差异、`param`、`typeparam`、非 void/无结果 Task 返回语义和 cref。不得留空标签，也不得把代码无法证明的设备或业务规则写成确定事实。
- 局部方法内常量不强制 XML 声明注释。字段、类级常量、缓存键和配置键按实际作用范围、生命周期、键组成及容量说明。
- `common.props` 会屏蔽 `CS1591`，普通构建不能证明 XML 文档完整。对 Core 与 Templates 的五个目标框架使用专用门禁，显式清除该抑制并将缺失、格式和引用诊断提升为错误。

### 严格多框架构建

在仓库根目录执行：

```powershell
./tools/Verify-XmlDocumentation.ps1
```

脚本按每个 TFM 先重建 Core，再以 `BuildProjectReferences=false` 重建 Templates；每个项目/框架使用独立 XML 路径，避免覆盖跟踪中的 XML 文档。它显式清空 `NoWarn`，将缺失及无效 XML 文档诊断提升为错误；任一构建失败或依赖项被跳过都会以非零退出。每次运行在 `artifacts/xml-documentation-verification/<run-id>/` 保存逐构建 `build.log` 与 `verification.json`。2026-10-22 本轮修复执行结果为 Core/Templates 五个目标框架 10/10 重建通过，生成的十份 XML 均解析成功；run ID 为 `20261022-111941-700-3eb238f1`。构建门禁不能替代人工核对注释是否准确；C# 非注释 token 相等须在编辑前保存可比较基线。

### 2026-09-28 历史基线与完成依据

下列数值来自旧计划的扫描与实施记录，只描述其当时范围，不是当前文件集的重新扫描结果。旧记录同时包含“仅完成批次 1、批次 2–7 未执行”和全量 88 文件实施审计两种表述；应保留为不同历史记录，不能据此判定当前批次已完成。批次 1 记录具体称修订了一个接口及其 15 个方法，新增 `<inheritdoc />` 0 处，核心库五个目标框架 Release 重建通过、XML 诊断为 0，未运行真实打印。

| 项目 | 文件 | 声明 | 缺少摘要或继承注释 |
| --- | ---: | ---: | ---: |
| `Bing.EasyPrint` | 47 | 477 | 36 |
| `Bing.EasyPrint.Templates` | 41 | 381 | 197 |
| 合计 | 88 | 858 | 233 |

| 声明类别 | 总数 | 缺失 |
| --- | ---: | ---: |
| 类、接口、结构体、枚举 | 102 | 13 |
| 构造函数 | 29 | 15 |
| 方法 | 330 | 99 |
| 属性 | 234 | 48 |
| 字段 | 61 | 58 |
| 枚举成员 | 102 | 0 |

旧记录还统计了：public 方法缺失 25/243、internal 15/16、private 59/71、static 56/88（访问级别与 static 统计交叠）；100 处单行摘要需改三行；186 个已有属性摘要缺访问语义前缀；14 个构造函数摘要不符句式；20 个 override 无 `<inheritdoc />`（12 个有重复独立摘要、8 个缺注释）；已有注释中 7 个声明缺部分参数标签、1 个缺泛型参数标签、201 个方法缺返回标签，另有 3 个空返回标签和 1 个空参数标签。这些数字可能互相重叠，不是独立问题总数。

旧实施记录称 `artifacts/comment-audit/CommentAudit.csproj` 对 88 个文件、858 项声明审计后，各类注释缺失、格式问题、标签错配和未继承 override 均为 0；两库五框架共十次 Release 重建通过，XML 诊断为 0。该审计项目当前不存在，且源码范围已变化，所以这些只作为历史结果，不替代 2026-10-22 的本轮扫描与门禁。历史记录另说明逐框架覆盖 `TargetFrameworks` 后构建成功、一次 net6.0 定向测试恢复后无结果并停止、未运行真实打印。

历史实施未保存完整的实施前 C# token 快照，因此不能证明当时前后 token 完全相等；旧记录只说明未有意修改业务语句或公开签名。今后须在每轮编辑前保留机器可比较的基线。

已确认的局部完成依据：

- 指定 CPCL Build 文档范围已使用有效继承：[`CPCLPrintCommand.Component.cs`](../src/Bing.EasyPrint/CPCL/CPCLPrintCommand.Component.cs)、[`BoxComponent.cs`](../src/Bing.EasyPrint/CPCL/Components/BoxComponent.cs)、[`RawMetadata.cs`](../src/Bing.EasyPrint/CPCL/Metadata/RawMetadata.cs)；上游契约见 [`MetadataBase.cs`](../src/Bing.EasyPrint/CPCL/Metadata/MetadataBase.cs)。这不表示其他 Provider/override 已完成审计。
- [`ImageComponent.cs`](../src/Bing.EasyPrint/CPCL/Components/ImageComponent.cs) 的像素限制文案现已完整，原残缺字子项完成。VCG 方向仍需协议手册或设备证据，不因该文案修复而关闭。

## 验证重点

- 协议命令：精确字节、输入校验、Build 幂等、失败后的状态恢复和取消。
- 统一模型：能力预检、元素路径、旋转与页面边界、输出预算。
- 模板：版本往返、绑定、分页、资源、布局轨迹和四协议输出。
- Host：严格 DTO、契约协商、结构化错误、请求/工作内存/并发限制。
- Web：Vitest 覆盖状态和归档逻辑；Playwright 覆盖 Host 联动流程。
- 包：`tests/PackageConsumer` 验证 NuGet 消费面。

## 生成物与环境注意事项

- `obj`、`bin`、`output`、`artifacts`、`node_modules`、`.pnpm-store` 和 `.tmp-*` 不属于源文档或功能代码，不应纳入提交。
- 前端仓库已跟踪 `package-lock.json`，使用 npm 和 `npm ci`；不要同时提交其他包管理器生成的锁文件。
- 旧目标框架可能需要本机安装相应 targeting pack/runtime。
- NuGet 漏洞索引不可用时可能出现 `NU1900`；不能把网络失败当作测试通过或功能失败。
- Host 项目已排除其目录下的 `artifacts/**` 与 `output/**` 默认内容项；若修改 Host 项目内容规则，仍须确认四个显式案例 JSON 被复制且生成目录不会进入内容列表。
- 历史结果见[多协议验证记录](multi-protocol-verification.md)，当前分支状态必须以本次实际命令为准。
- 真实打印机联调不属于本地自动化测试结果。
