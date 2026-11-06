# TDD 开发与回归测试

本指南用于新增行为与缺陷修复。已有测试可以提供回归保护，但只有保留了“实现前失败 → 最小实现后通过”的证据，才将该项记为 TDD。已经存在的开发历史不追认为测试先行。

## 一次工作循环

1. 从功能文档写出可观测的验收条件：给定输入、调用公开入口、预期输出或错误及定位。先确认它属于已支持范围。
2. 检查已有用例，补充缺失场景及必要的正向控制。例如：拒绝仅有星期名称的日期输入，同时保留包含数字日号和星期名称的合法输入。
3. **Red**：先运行新增定向测试，生产实现保持原样。保存源码/测试身份、命令、退出码、TRX 或日志；核实失败来自行为断言。构建失败、权限、旧 Host、错误夹具都不能算产品红灯。
4. **Green**：只修复该验收条件对应的根因，运行相同测试并保存新日志，不覆盖红灯。如果新增测试第一次直接通过，记录为覆盖补充，不人为破坏代码制造红灯。
5. **Refactor / Review**：需要时消除本次修改引入的重复；检查是否改变支持范围、异常定位、预算、取消及旧行为。没有必要的重构时直接审查。
6. 按影响范围运行回归。测试通过后记录剩余边界；只有明确可执行的问题才继续下一轮修复。

每个证据条目至少包含：需求/缺陷编号、测试名称、基准提交、相关源码和测试 SHA-256、TFM/实际运行时、命令及退出码、红绿日志路径。最终源码身份和验证结果应绑定在同一 manifest 中。非行为失败单独记录，保留原始输出。

## 当前扩展功能与测试入口

下表描述回归入口，不表示所有历史功能均按 TDD 开发，也不等同穷尽覆盖。

| 功能 | 核心/Host/前端单元或契约测试 | 集成或工具入口 | 本轮新增证据 |
| --- | --- | --- | --- |
| O1 Host .NET10 | `tests/DesignerHost.Tests/HostContractTests.cs` | `tools/Verify-DesignerHost.ps1` | 既有回归 |
| O2 合并预览 | `PreviewBundleHostTests.cs` | 设计器预览及 Host 像素/取消/预算断言 | 既有回归 |
| O3 跨平台与消费 | 运行时和包消费验证 | `.github/workflows/designer-cross-platform.yml`、`tools/Verify-ProtocolRuntimes.ps1`、`tools/Verify-ModernConsumption.ps1` | 外部 Actions 结果另行验收 |
| O4 新协议注册 | `ProtocolRegistryTests.cs`、`TemplateCustomProtocolTests.cs`；Host `ProtocolRegistryHostTests.cs`、`IndependentProtocolHostTests.cs` | `e2e/customProtocol.spec.ts` | 既有回归 |
| F1 字段格式化 | `FieldFormatTests.cs`、Host `FieldFormatHostTests.cs` | `e2e/fieldBatch.spec.ts` | 日期输入完整性与输出长度：Red → Green |
| F2 批量生成 | Host `BatchHostTests.cs`；前端 `src/batch.test.ts`、`batchEditorState.test.ts` | `e2e/fieldBatch.spec.ts` | JSONL 行号：Red → Green；成功恢复与编号控制 |
| F3 Digital Link | `Gs1DigitalLinkTests.cs`、`Gs1StructuredTemplateTests.cs`、Host `Gs1DigitalLinkHostTests.cs`、前端 `gs1Api.test.ts` | `e2e/gs1AiEditor.spec.ts` | 既有回归，历史局部红绿记录见第四阶段报告 |
| F4 AI 编辑器 | 复用核心目录/校验和 Host GS1 测试 | `e2e/gs1AiEditor.spec.ts` | 既有回归 |
| F5 PDF/SVG | Host `Phase5ExportContractTests.cs`、`Phase5ExportReviewRegressionTests.cs` | `e2e/phase5-editor.spec.ts` | 既有回归 |
| F6 编辑状态 | 前端 `editorIdentity.test.ts`、`editorOperations.test.ts`、`editorLayout.test.ts` | `e2e/phase5-editor.spec.ts` | 既有回归 |

省略目录的 C# 文件位于 `tests/Bing.EasyPrint.Tests/` 或表中明确的 `tests/DesignerHost.Tests/`；前端与 E2E 路径相对 `web/Bing.EasyPrint.Designer/`。

## 定向验证命令

在仓库根目录执行核心日期测试；将结果目录的 `red` 改为 `green` 保存不同阶段，不能覆盖前一阶段：

```powershell
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net10.0 --no-restore -p:GenerateDocumentationFile=false --filter FullyQualifiedName~FieldFormatTests --logger 'trx;LogFileName=core-date.trx' --results-directory artifacts/tdd/core/red
```

Host 使用独立输出并串行构建，避免正在运行的 Host 或多框架输出竞争：

```powershell
dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj -f net10.0 --no-restore -p:GenerateDocumentationFile=false -m:1 -p:BuildInParallel=false --output artifacts/tdd/host-red-bin --filter FullyQualifiedName~FieldFormatHostTests --logger 'trx;LogFileName=host-date.trx' --results-directory artifacts/tdd/host/red
```

前端定向、项目回归和浏览器验证：

```powershell
Push-Location web/Bing.EasyPrint.Designer
npm run test -- src/batch.test.ts
npm run test
npm run build
npx playwright test e2e/fieldBatch.spec.ts --workers=1 --retries=0
Pop-Location
```

首次缺少依赖时按[开发指南](development.md)准备 SDK、npm 依赖和 Chromium；`--no-restore` 只适用于已还原的项目。浏览器应使用当前候选 Host；修改端口时同时设置 `EASYPRINT_DESIGNER_API_URL` 和 `EASYPRINT_DESIGNER_WEB_PORT`，避免复用旧 Host。

## 保存一次红灯的身份与退出码

下面在仓库根目录执行。先增加测试、保持生产实现原样，再运行此段；`red` 目录应是本次任务的新目录。Green 阶段换目录重新采集，不覆盖前一阶段。命令参数、执行前的源码/测试哈希、日志和真实退出码分别保存，不能仅凭日志摘要推断退出码。

```powershell
$tddDir = 'artifacts/tdd/example/core/red'
New-Item -ItemType Directory -Path $tddDir -ErrorAction Stop | Out-Null
$testArguments = @(
    'test', 'tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj',
    '-f', 'net10.0', '--no-restore', '-p:GenerateDocumentationFile=false',
    '-m:1', '-p:BuildInParallel=false',
    '--filter', 'FullyQualifiedName~FieldFormatTests',
    '--logger', 'trx;LogFileName=result.trx', '--results-directory', $tddDir
)
$identity = [ordered]@{
    phase = 'red'
    baseCommit = (git rev-parse HEAD)
    sdk = (dotnet --version)
    command = 'dotnet'
    arguments = $testArguments
    files = @(
        Get-FileHash -Algorithm SHA256 -LiteralPath @(
            'src/Bing.EasyPrint.Templates/Rendering/BgFieldFormatter.cs',
            'tests/Bing.EasyPrint.Tests/FieldFormatTests.cs'
        ) | Select-Object Path, Hash
    )
}
$identity | ConvertTo-Json -Depth 6 |
    Set-Content -Encoding utf8 -LiteralPath (Join-Path $tddDir 'identity.json')
& dotnet @testArguments *> (Join-Path $tddDir 'run.log')
$tddExitCode = $LASTEXITCODE
[ordered]@{ exitCode = $tddExitCode; endedAt = [DateTimeOffset]::Now.ToString('o') } |
    ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $tddDir 'exit.json')
Get-Content -LiteralPath (Join-Path $tddDir 'run.log') -Tail 25
```

其他模块替换文件与筛选器；前端也应在运行 npm 前记录对应 `batch.ts`、`batch.test.ts` 的哈希，并在命令结束后立即读取 `$LASTEXITCODE`。需要确认实际 .NET 运行时而不只 TFM 时，增加 `--diag` 并保存其 testhost 日志。复现已修复缺陷时，当前代码直接通过是预期；不要倒改生产实现制造红灯。

## 回归范围与完成标准

- 测试自身调整：定向用例；共享测试基础设施改变时才扩大范围。
- 模板共享行为：对应 Core 测试、Host 消费链路；阶段收口运行受影响项目全量。日期解析这类运行时相关行为还应在现有支持的旧运行时定向执行。
- 前端纯解析：模块测试、Vitest 全量、类型/生产构建；用户可见诊断补真实浏览器交互验证。
- 协议/公共 API/项目图改变：按实际影响扩大 Provider、包消费及多框架验证。
- 性能、容量和外部平台：沿用独立验收规则。功能测试通过不能替代正式性能阈值或远端 CI 执行证据。

本轮真实红绿、完整测试计数、夹具勘误和环境首败见[2026-11-06 补测报告](tdd-completion-2026-11-06.md)。
