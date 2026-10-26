# 第五阶段 F5/F6 执行报告

## 当前状态

初轮报告曾记录 F5/F6 实现和本地验证已完成；之后由修正 1/3 及主代理独立复核逐项校正。本报告最新状态以主代理 `docs/optimization-expansion-phase5-review-r2-2026-10-26.md` 为准：F5、R1、R2 及 R3/R4 已验收子项保持 CLOSED；本轮只处理 OPEN_ACTIONABLE R3-B、R4-B、E1-README。修正 2/3 的实现和本地证据已更新，等待主代理独立复核；本报告不自行关闭 F5/F6 或总计划。F1—F4 按主代理既有结论保持 CLOSED，本阶段未进入后续功能范围。

生产 Core/Templates 五 TFM、既有公开 API、枚举、默认值和包版本保持原值；PDFsharp 6.2.4 仅用于 net10 Host。未暂存、提交、推送或发布，也未更新 API 基线。

## 实现摘要

### F5 导出

- `DesignerRequestProcessor.Export.cs` 新增独立 `page-pdf` 与 `element-svg` 端点；Host 对外协商 `page-pdf-export`、`barcode-svg-export`。前端支持单页/全页 PDF 与选定条码 SVG 下载；旧 Host 缺能力时禁用入口并保留设置。
- PDF 仅接受 ESC/POS Raster，复用渲染后的布局元素和 Core `PrintRasterPreview` 共享单色绘制结果。页面以每轴 dot 数和 DPI 转换为 PDF point，保留完整页面边界、份数与顺序；页数、输出和临时缓冲受现有预算与取消检查约束，失败不会返回半份 PDF。
- SVG 从共享条码 primitive 的编码结果生成 rect/path 几何，viewBox 使用 dot 尺寸，width/height 按 X/Y DPI 转物理单位；保留静区与旋转边界，不嵌位图、不输出设备 HRI。字体、图片及软件编码器不支持的类型会拒绝。
- 批量 `exportFormats` 为 opt-in，未指定时保持旧输出。PDF/SVG 文件进入累积预算和 ZIP manifest，记录格式、记录、页、元素、份数及相对路径；取消、超限或全请求失败时不提供部分归档。
- `tools/DesignerHost/DesignerHost.csproj` 固定引用 PDFsharp 6.2.4。官方 v6.2.4 标签许可证核对为 MIT，证据见 `artifacts/optimization-expansion-20261025/phase5/pdfsharp-6.2.4-package-license-verification.md`。

### F6 编辑状态

- DesignerProject v2 的 `editorState.nodeStates` 维护稳定节点 ID 与锁。导入验证唯一 ID、完整路径和布尔锁值；对象重排保持 ID，复制创建新 ID，删除回收映射，编组/拆组及撤销/重做重建路径映射；锁由祖先继承，树仍允许显式解锁。打印模板 JSON 不带编辑器状态。
- 画布、属性、树、快捷键、结构操作和组件重命名等入口共享锁判断。排列命令仅接受同一容器的绝对定位节点，按旋转后的占位边界计算对齐与均分；均分固定首尾并直接计算各位置，避免逐项舍入累积，失败不改模板。
- 历史保存原始业务文本（含无效 JSON）、解析状态、映射、选择及编辑身份。快照最多 80 个，按已配置总字节上限裁剪较旧记录；大业务数据采用共享不可变负载，避免每次动作复制完整批次。单个导入、映射或排列作为单一撤销动作，恢复后取消旧预览。

## 初轮候选验证（修正前）

以下计数只描述主代理复核前的初轮候选与当时测试范围，不代表修正 1/3 的当前源码结果，也不覆盖后续反例。

| 门禁 | 结果 | 实际进程/证据 |
| --- | --- | --- |
| Core/Templates 严格 XML | 5 个生产 TFM 各构建两个项目，共 10/10 通过 | `artifacts/optimization-expansion-20261025/phase5/final-candidate/xml-documentation/20261026-150000-039-feded384/`；目标为 net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1 |
| Core 全量运行时 | net6.0、netcoreapp3.1、net10.0 各 4,705/4,705；失败/跳过 0 | SDK 10.0.400；实际 testhost 分别为 .NET 6.0.36、3.1.32、10.0.11。每个 TFM 保存 TRX、runsettings、外层 `.diag.log` 和 testhost 自身 `.diag.host.*.log`；见 `core-runtime-r1/20261026-070056-77a7da27/` |
| DesignerHost 全量 | 357/357；失败/跳过 0 | 目标 net10.0，实际 testhost .NET 10.0.11。命令为 `tools/Verify-DesignerHost.ps1 -OutputDirectory artifacts/optimization-expansion-20261025/phase5/final-candidate/host-tests/bin -ResultsDirectory artifacts/optimization-expansion-20261025/phase5/final-candidate/host-tests/results -DiagnosticPath artifacts/optimization-expansion-20261025/phase5/final-candidate/designer-host-net10.diag.log -NoRestore`；TRX 与精确 `*.diag.host.*.log` 在 `host-tests/results/` |
| 前端生产构建 | `npm run build` 成功，1,982 modules | Node 22.22.0、Vite 8.3.1；`final-candidate/frontend/build-r1.log` |
| Vitest | 27 个测试文件，474/474 通过 | Vitest 5.0.2；`final-candidate/frontend/vitest-r1.log` |
| Chromium E2E | 222/222 通过，4.5 分钟 | Node 22.22.0、Chromium 153.0.8010.12，workers=1、retries=0；`final-candidate/frontend/chromium-e2e-verified.log`。最终运行包含 F5 下载、旧 Host 能力降级，以及 F6 v2 身份/锁、组件引用、无效业务文本撤销和批量映射历史用例 |
| F5 SVG 独立验证 | 10 个 SVG 向量样例；Chromium 栅格与 Host 共享 element PNG 的黑白模块差异均为 0；7 个向量有独立解码结果 | 样例为 Code128 四方向、QR、DataMatrix、PDF417、Composite A/B/C。独立解码仅计 Code128 四方向、QR、DataMatrix、PDF417；Composite A/B/C 只做模块等值比较，不声称独立解码。Playwright 1.63.0、Chromium 153、ZXing-C++ 3.1.1。`integration-r3/independent-r3/collection.json` 与 `independent-verification.json` |
| F5 PDF 独立验证 | 7 页检查通过、7 个码值解码通过；空白页及非 8 倍数宽度包含在内 | PDFium/pypdfium2 5.14.0 与 Poppler 26.07.0；多页两份的 XObject 像素与来源页面完全相同，Poppler 二值像素变化 0。`integration-r3/independent-r3/independent-verification.json` |
| Phase 5 样例 | JSON 2 个、节点 5 个、唯一 ID 5 个、锁定节点 1 个；Host 预览 1 页、PDF 2 页、SVG 成功 | `sample-postfix-r5/`；PDF 7,557 bytes，SVG 4,185 bytes。PDFium 对两页 400×400、203 DPI 页面渲染均为 0 差异；Chromium 对 82×82 SVG 的模块二值差异为 0；ZXing-C++从 SVG 和 PDF 页面均解出 `https://id.gs1.org/01/09520123456788/10/LOT-2026`，见 `sample-postfix-r5/independent-r4/independent-verification.json` |
| 初轮完整模板与批量案例 | Templates 样例 21 条一致性检查、136 个输出文件；BatchConsumer 3/3 记录成功、3 页、608 bytes、ZIP 5 项。该初轮消费者请求未带 `exportFormats`，不证明 PDF/SVG opt-in | 实际样例进程均为 .NET 6.0.36，命令和 hosttrace 见 `final-candidate/sample-agent-verification/`；opt-in 案例由本轮补齐 |
| 当前源码候选包消费 | net6 与现代 net10 均通过；Core/Templates 候选包 SHA-256 与还原包匹配 | 包版本保持 1.0.0。net6 选中 net6.0 资产、实际消费者 .NET 6.0.36；net10 选中 net7.0 资产、实际消费者 .NET 10.0.11。证据见 `package-net6-shortpath/consumer-verification.json` 与 `package-net10/20261026-075002-49c7b651/consumer-verification.json` |

候选身份为 `phase5-final-candidate-20261026`，基准提交 `6c707be4092dada2dabe345a9eb92e1e907c1acb`，暂存区为空。Core 包 SHA-256 为 `D7BB11AA3321F824F20C1AE657FAC9585348B5D84F2C39DB62EA19B5B6D05683`，Templates 包为 `9B917FFDD2426640A1D584B19024D59C342ED6B81C348987D35A535112F6BBA7`；两份 nupkg 已保存至 `final-candidate/candidate-packages/`。Host DLL 与前端 CSS/JS 哈希、门禁汇总见 `final-candidate/candidate-manifest.json`。

## 集成修正 1/3 当前源码证据

本轮只修复 R1—R4 与 E1。Host net10 生产代码、Vue 编辑器代码、DesignerHost 回归测试、BatchConsumer 案例及其 README 有变更；Core/Templates 生产代码、五个生产 TFM、既有 API/枚举/默认值/版本和包依赖未变。旧 Core/XML/包消费证据只适用于这些未变范围。

| 项目 | 当前结果 | 证据 |
| --- | --- | --- |
| R1 Code49 SVG | 修正内部 leading quiet zone 被重复计入矢量起点；0/90/180/270 度及默认/显式静区共 8/8，用 SVG 矩形与共享整页 PNG 的对应码图区域逐 dot 对照 | 初始反例和探索失败保留于 `red-host*`、`green-r1-pagepixels-2/`、`green-r1-final-pixels/`、`red-r1-pixel-coordinate/`、`red-r1-raster-bounds/`；绿色 TRX 为 `green-r1-code49-pixels/phase5-r1-code49-pixels.trx` |
| R2 单条/批量 SVG | SVG 使用原协议选项和同一次共享布局；批量保留 TSPL 字体行高、多页、重复实例、300 DPI、份数与 manifest。与 R1 合并的最终定向组 11/11 通过，其中 R2 三项 | `green-r1-host-regressions-final/phase5-r1-host-regressions.trx`；候选及已加载程序集绑定见 `live-host/identity.json` |
| R3 结构锁和身份 | 编组前检查每个选中子树；多步结构变更用同步 watcher 结构事务；行编组移动原对象并保留子 ID/锁 | Vitest 27 文件/476 项通过；目标 Chromium 5/5、完整 227/227 通过，见 `green-frontend-vitest/`、`green-chromium/`、`green-chromium-full/` |
| R4 导入和排列历史 | JSON/ZIP 导入保留导入前快照并作为单次历史转换；排列修改前后强制提交快照 | 同一 Chromium 回归组覆盖 JSON/ZIP 撤销重做及无等待连续排列逐步撤销；完整浏览器结果 227/227 |
| E1 批量案例 | BatchConsumer 显式请求 `exportFormats: ["pdf", "svg"]`、ESC/POS Raster；2/2 记录成功、2 页、63,698 raw bytes，PDF 2 项、SVG 4 项，manifest 的 6 项与 ZIP 文件逐项匹配 | README 案例输出/manifest 校验见 `readme-batch-example-r1-green/`；首次缺协议设置的失败（exit `-532462766`）保留于 `readme-batch-example/`，之后显式设置协议/布局并成功，属于 superseded 首败 |
| Host 默认验证 | 无参数 `tools/Verify-DesignerHost.ps1` 入口通过，368/368；保留默认 net10 构建输出、TRX 与精确 testhost 诊断。修正回归组另行 fresh 通过 11/11，失败/跳过均 0 | `default-host-validation/`、`artifacts/designer-host-verification-{bin,tests}/`；默认 TRX 的 testhost 日志为 `designer-host-net10.diag.host.*.log`，此前默认输出副本在 `preexisting-default-output/` |
| 前端 | `npm run build` 成功，1,982 modules；Vitest 27 文件/476 项通过；Chromium 目标 5/5，完整 227/227（4.7 分钟），workers=1、retries=0 | Node 22.22.0、Playwright 1.63.0；`green-frontend-build/`、`green-frontend-vitest/`、`green-chromium/`、`green-chromium-full/` |

Host 首次最小构建因用户已有 `DesignerHost (PID 36700)` 锁定默认 `tools/DesignerHost/bin/Debug/net10.0` 而失败，日志保存在 `green-minimal/`；未结束该进程。定向回归随后在独立 `BaseOutputPath` 通过。默认 Host 脚本之后成功完成。

README 案例使用的 live Host 是观测到的既有进程 PID 78844，本轮没有启动或停止它。其命令行为 `dotnet H:\Bing_Framework\Bing.EasyPrint\artifacts\designer-host-verification-bin\DesignerHost.dll`，健康接口返回 ready；候选 DLL SHA-256 为 `53D4FF35046C1C180BAFA677D8CB4BF97796FDE8A31733E49A67E92C3A6BF10C`，Core/Templates 依赖哈希及实际加载的 `.NETCore.App/10.0.11/coreclr.dll` 路径见 `live-host/identity.json`。批量消费者的 app runtime 则由 host trace 确认为 .NET 6.0.36；外层 `dotnet run` SDK 进程的 .NET 10.0.11 不作为消费者运行时证据。

### Phase 5 样例独立结果

`samples/Phase5/export-template.json` 与 v2 项目中的模板同构。400×400 点页面上，标题在 `(20,20)`、Code128 在 `(20,80)`，Digital Link QR 在 `(20,180)`；上下分行避免与 Code128 显式静区相交。SVG 示例的固定索引为 Code128 的数组项 1；QR 为数组项 2。文档已同步该布局与请求字段。

样例 PDFium 检查读取两页媒体框为 141.8719×141.8719 pt，与 400×400 dots、203 DPI 换算一致；XObject 与 Host `page-png` 完全相同，PDFium 逐页渲染像素变化 0。Poppler 203 DPI 结果为 401×401（边界取整）；该样例的精确点阵比较使用 PDFium 的 400×400 设备点输出。样例 SVG 无嵌入 `<image>`，含一条路径；Chromium 灰度边缘有 566 个抗锯齿像素差异，阈值化后的黑白模块差异为 0，且独立解码通过。

### 本轮实际命令与候选

以下为本轮针对 R1—R4/E1 的命令和结果。源码候选使用主代理复核时对应的工作树；Host 候选路径、Host/Core/Templates SHA-256、HTTP ready 响应以及加载的 CoreCLR 见 `integration-r1/live-host/identity.json`。

```powershell
dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~Phase5ExportReviewRegressionTests --logger "trx;LogFileName=phase5-r1-host-regressions.trx" --results-directory artifacts/optimization-expansion-20261025/phase5/integration-r1/green-r1-host-regressions-final --diag artifacts/optimization-expansion-20261025/phase5/integration-r1/green-r1-host-regressions-final/outer.diag.log -m:1 /p:BuildInParallel=false /p:BaseOutputPath=H:\Bing_Framework\Bing.EasyPrint\artifacts\optimization-expansion-20261025\phase5\integration-r1\green-r1-host-regressions-final\build\
.\tools\Verify-DesignerHost.ps1
```

第一条定向 Host 组为 11/11，0 failed、0 skipped；其精确 `outer.diag.host.*.log` 显示测试进程使用 .NET 10.0.11。无参数 Host 验证脚本为 368/368，完整日志、TRX、testhost 诊断和默认构建输出保存在 `integration-r1/default-host-validation/` 与 `artifacts/designer-host-verification-{bin,tests}/`。

```powershell
npm run build
npm test
EASYPRINT_DESIGNER_API_URL=http://127.0.0.1:5201 EASYPRINT_DESIGNER_WEB_PORT=5183 npm run test:e2e -- --workers=1 --retries=0 --output ../../artifacts/optimization-expansion-20261025/phase5/integration-r1/green-chromium-full/results
dotnet run --project samples/BatchConsumer/BatchConsumer.csproj -- http://127.0.0.1:5201 samples/Phase5/export-template.json H:\Bing_Framework\Bing.EasyPrint\artifacts\optimization-expansion-20261025\phase5\integration-r1\readme-batch-example-r1-green\records.json H:\Bing_Framework\Bing.EasyPrint\artifacts\optimization-expansion-20261025\phase5\integration-r1\readme-batch-example-r1-green\phase5-batch-result.zip
```

`npm run build` 产出 1,982 modules，Vitest 27 文件/476 项，完整 Chromium 227/227；Node 为 22.22.0、Playwright 为 1.63.0。BatchConsumer README 案例 2/2 成功、2 页、6 项导出，完整命令行日志、net6.0 app runtime trace、ZIP 与 manifest 校验均在 `integration-r1/readme-batch-example-r1-green/`；README 首次运行的失败输入和日志仍在 `integration-r1/readme-batch-example/`。

### 实际命令

最终验证使用的主要命令如下；完整 stdout/stderr、运行时报告和 TRX 以表内证据目录为准。

```powershell
./tools/Verify-XmlDocumentation.ps1
./tools/Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1,net10.0 -ResultsDirectory artifacts/optimization-expansion-20261025/phase5/final-candidate/core-runtime-r1
.\tools\Verify-DesignerHost.ps1 -OutputDirectory artifacts/optimization-expansion-20261025/phase5/final-candidate/host-tests/bin -ResultsDirectory artifacts/optimization-expansion-20261025/phase5/final-candidate/host-tests/results -DiagnosticPath artifacts/optimization-expansion-20261025/phase5/final-candidate/designer-host-net10.diag.log -NoRestore
./tools/Verify-ModernConsumption.ps1 -TargetFramework net10.0 -ResultsDirectory artifacts/optimization-expansion-20261025/phase5/final-candidate/package-net10
```

在 `web/Bing.EasyPrint.Designer` 运行：

```powershell
npm run build
npm test
npm run test:e2e -- --workers=1 --retries=0 --output ../../artifacts/optimization-expansion-20261025/phase5/final-candidate/frontend/chromium-e2e-verified
```

当前候选 net6 消费命令为 `tools/Verify-PackageConsumer.ps1 -CandidatePackagesDirectory <net10 pack 输出目录> -TargetFramework net6.0 -ResultsDirectory %TEMP%\p5net6`。结果随后复制到阶段证据目录；候选 nupkg 与两种消费者还原得到的包哈希一致。样例 Host 请求和独立 PDF/SVG 验证命令/脚本与各自输出保存在 `final-candidate/sample-postfix-r5/`。

## 首败与证据适用范围

- 第五阶段 SVG/PDF 集成首测的模块对齐、导出样例静区重叠、样例脚本按不存在 `elementIndex` 字段读取索引、Poppler 尺寸向上取整及 Chromium 灰度抗锯齿差异均保留在各自首次运行目录；最终结果在独立后缀目录，不覆盖首败。
- 初轮复核前的浏览器门禁为 222/222；修正 1/3 涉及前端生产源码，因此重新执行了 build、Vitest 与完整 Chromium。当前源码结果为 build 成功、27 文件/476 项、完整 E2E 227/227（workers=1、retries=0），见 `integration-r1/green-frontend-*` 和 `integration-r1/green-chromium-full/`。早期 mock 生命周期失败仍保留在初轮证据，不计入本轮结果。
- R1 共享 PNG 对照先后发现测试坐标假设错误：预览布局宽高不是 Code49 矢量 viewBox；随后确认默认 Code49 的 logical layout origin 与实际绘制原点相差 leading quiet offset。两次失败诊断及修正前记录保留于 `green-r1-pagepixels-2/`、`green-r1-final-pixels/`、`red-r1-pixel-coordinate/` 和 `red-r1-raster-bounds/`；最终测试按实际绘制起点裁切，8/8 逐点通过。
- net6 候选消费首次使用过长的仓库内输出路径，MSBuild 找不到临时解压依赖；失败和 `-NoRestore` 重试均保留于 `final-candidate/package-net6/`。将中间输出移到 `%TEMP%\p5net6` 后，当前同一候选包哈希的 net6 构建及实际运行通过。没有更改生产代码来绕过验证。
- Core/Host/前端构建仍显示既有 ImageSharp 与其他 NuGet 安全公告；net6 消费有 `NETSDK1138` 和测试程序 `CS8600` 警告，前端 bundle 有大于 500 kB 的提示。无错误，未更新依赖、调整阈值或开展无关性能矩阵。

## 状态分类与边界

- R1—R4、E1：本轮本地实现和对应回归证据已闭环，交主代理独立复核；这里的 finding 状态不代表 F5/F6 或总计划已经放行。
- F5/F6 阶段：等待主代理独立复核；本报告不宣告阶段完成或关闭。
- Blocked External：Windows/Linux GitHub Actions 实际运行仍未提供；本机结果不替代外部门禁。
- Not Applicable：实机打印机/扫描设备验收按用户要求排除；本阶段不发布包。
- Deferred：设备传输、复杂字体塑形、图片依赖升级等继续遵循总计划范围。

最终证据均在 `artifacts/optimization-expansion-20261025/phase5/`，第四阶段及其旧证据目录未覆盖。该报告交由主代理独立验收；总计划、阶段契约和独立复核状态由主代理维护。

## 集成修正 2/3（当前）

本轮 Change Impact Analysis：只修改 Designer 前端 `src/App.vue` 的排列可用性/锁定子树判断及 `e2e/phase5-review-r1.spec.ts` 回归；没有改 Core、Templates、Host、公开 API、生产 TFM、包或版本。Host/Core/XML/包产物未变，沿用此前经主代理确认的门禁证据。本轮新生产前端源码经过 build、Vitest 和完整 Chromium 验证。

| Finding | 修复与结果 | 本轮证据 |
| --- | --- | --- |
| R4-B | 安排命令绑定 `previewRevision` 与成功布局版本。模板、数据和输出配置触发预览时立即使旧布局失效；请求在途、去抖等待或预览失败时禁用排列，显示原因；执行入口再次校验。左右连续排列以当前 40/30 dot 几何从 X=70/10 得到 X=10/20；上下连续排列得到 Y=10/16；同方向重复排列幂等，每个有变化的动作独立撤销。有效 Host 预览在途和失败场景均确认不会按旧布局改坐标。 | 定向 3/3、完整 Chromium 230/230；`integration-r2/green-targeted-r3.log`、`integration-r2/chromium-full-corrected.log` |
| R3-B | 排列启用态和执行时都遍历实际选择子树/组件实例并检查继承锁。锁定后代使排列禁用且按钮标题给出锁定路径；绕过 UI 调用也会拒绝并提示，不写模板、不增历史。回归使用有效 CPCL Host 预览，锁定文字 A 的未锁组不能从 X=20 排到 X=100，导出的模板及项目历史保持不变。 | 定向 3/3、完整 Chromium 230/230；回归位于 `e2e/phase5-review-r1.spec.ts` 的 R3-B 用例 |
| E1-README | 修正上一轮证据范围说明：`integration-r1/readme-batch-example-r1-green/` 验证的是 `samples/BatchConsumer/README.md`，不代表仓库根 `README.md:39` 快速示例。本轮在独立生成目录以 net6.0 实际构建并执行根 README 原始片段，逐字节核对输出。根 README 本轮未编辑。 | `integration-r2/readme-root-example/commands.md`、`verification.json`、`readme-example.bin` |

### 前端门禁

| 门禁 | 结果 | 实际运行时/证据 |
| --- | --- | --- |
| 生产 build | `npm run build` 退出码 0，1,982 modules；仅保留既有 bundle 大小提示 | Node 22.22.0、Vite 8.3.1；`integration-r2/diagnose-build.log` |
| Vitest | 27 个文件、476/476 通过，失败/跳过 0 | Node 22.22.0、Vitest 5.0.2；`integration-r2/vitest.log` |
| R3-B/R4-B 定向浏览器 | 3/3 通过，失败/跳过 0；含同轴左右、上下、重复幂等、逐步撤销、预览在途/失败、锁定后代 | Node 22.22.0、Playwright 1.63.0、Chromium 153.0.8010.12；`integration-r2/green-targeted-r3.log` 与 `green-targeted-r3/results/` |
| 完整 Chromium | 230/230 通过，失败/跳过 0，4.8 分钟；workers=1、retries=0 | Node 22.22.0、Playwright 1.63.0、Chromium 153.0.8010.12；`integration-r2/chromium-full-corrected.log` 与 `chromium-full-corrected/results/` |

首个完整浏览器运行 `chromium-full.log` 为 225 passed / 5 failed，不能作为候选验证：Playwright 的 Host 健康检查指向 5194，但已启动 Vite 的 proxy 在启动时未收到 `EASYPRINT_DESIGNER_API_URL`，所以应用请求实际落到既有 5187 旧 Host。该次日志、五个失败上下文和截图均保留。随后在启动 Vite 时将 proxy 显式设为 5194，先通过 `5185/api/designer/health` 核实代理响应包含 `page-pdf-export` 与 `barcode-svg-export`，再执行上表唯一有效的完整候选运行。未重试测试、调高超时或改变 retries。

完整运行复用 `artifacts/designer-host-verification-bin/DesignerHost.dll`，SHA-256 `53D4FF35046C1C180BAFA677D8CB4BF97796FDE8A31733E49A67E92C3A6BF10C`。5194 Host 为本轮启动的进程，实载 CoreCLR 路径 `C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.11\coreclr.dll`；旧 5187 Host 未停止。新启动 Vite 分别使用 5184（错误 proxy 首轮）与 5185（有效候选），既有 5173 Vite 未停止。Playwright Browser 插件不可用，因此本地按已加载的前端调试流程使用项目 Playwright/Chromium 执行。

### 根 README 快速示例

执行范围是仓库根 `README.md` 的 `## 快速示例` 中 `EscPosPrintCommand`、GBK、`订单完成`、`Feed(2)` 原代码片段。隔离项目目标为 `.NETCoreApp,Version=v6.0`；运行中的消费者进程报告 .NET `6.0.36`，命令退出码 0。生成 14 bytes：`1B40B6A9B5A5CDEAB3C90A1B6402`，独立核对 `ESC @`、GBK 字符串、换行及 `ESC d 2` 均一致。SDK 为 10.0.400，候选包 `Bing.EasyPrint 1.0.0` SHA-256 `D7BB11AA3321F824F20C1AE657FAC9585348B5D84F2C39DB62EA19B5B6D05683`。初次默认缓存解析失败及首个验证器脚本错误未覆盖，均在 `readme-root-example/` 留存；随后隔离 package cache 的 restore、运行、字节校验全部成功。完整命令和实际进程运行时见该目录 `commands.md` 与 `verification.json`。

### 本轮命令

PowerShell 命令、环境目标、退出码和日志位置汇总在 `artifacts/optimization-expansion-20261025/phase5/integration-r2/commands.md`。本轮不重跑 Host/Core/XML/包消费矩阵；上述边界内前端门禁为当前源码结果，先前未变生产范围继续引用其原始证据。当前只有 R3-B、R4-B、E1-README 提交主代理独立复核，F5/F6 阶段整体及总计划仍不在本报告中关闭。实机排除；外部 CI 无本机替代结果，仍标记 `BLOCKED_EXTERNAL`。
