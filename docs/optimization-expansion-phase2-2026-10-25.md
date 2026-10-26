# 第二阶段 O4 实施报告：完整新协议注册

日期：2026-10-25。范围：只实施第二阶段 O4，不进入 F1—F6。状态：**初版复核 NEEDS_FIX；集成修正第 1/2 轮记录保留；第 3 轮（3/3）本地完成，待主代理复核；未提交、未推送、未发布**。外部 CI 仍为 `BLOCKED_EXTERNAL`，本报告不把本地证据等同于外部 CI 或真实设备验收。

## 目标与结论

O4 要建立协议中立的注册与渲染入口，使新协议作为独立程序集注册，贯穿 Core、Templates、Host 和设计器，同时保持内置协议行为和旧公开契约不变。本轮已完成：

- Core 冻结注册表、协议后端会话、配置/描述快照、有序页面输出、可选收尾和预算组合器。
- Templates 自定义适配器、文字度量、分页/换行、元素范围校验和布局所有权复用。
- Host 启动显式注册 `demo.protocol`、目录端点、`protocolId`/`protocolConfig` 请求、能力/预览/导出/预览包接入和结构化错误。
- 设计器目录发现、Schema 配置表单、项目 v2、未知协议保留/禁用、旧 Host 降级。
- 独立示例程序集 `samples/Protocols/DemoProtocol`，支持文字、线段、矩形、一维码、QR 和可选收尾。

O4 没有修改 `PrintProtocol` 枚举，没有增加上传可执行插件的能力，没有改动内置 Provider 默认字节，也没有实施 F6 锁定/排列。

## Change Impact Analysis

- ChangedProjects：Core、Templates、DesignerHost、DesignerHost.Tests、Bing.EasyPrint.Tests、Designer Web、独立示例 `DemoProtocol`。
- ChangedPublicContracts：新增 `PrintProtocolRegistry`、`PrintProtocolRegistration/Descriptor`、`IPrintProtocolBackend`、`IPrintProtocolFinishingBackend`、`PrintProtocolOutputComposer`、`ITemplateProtocolAdapter`、`ITemplateTextMetrics` 及模板选项字段；既有公开成员和枚举保持不变。
- ChangedRuntimePaths：Host 启动注册、目录/能力协商、自定义模板渲染、后端页面输出、项目 v2 保存/加载、旧 Host 降级。
- ChangedProviders：新增示例协议；内置 CPCL/ESC/POS/TSPL/ZPL 仍走原 Provider 工厂。`PrintProtocolProviderBackend` 仅为注册表适配和回归入口。
- ChangedTFMs：Core/Templates 生产五框架不变；Host 保持 `net10.0`；示例为 `netstandard2.0`。
- ChangedBuildPackaging：Host 测试项目增加示例引用；未改产品版本、API 基线或 ImageSharp 依赖。
- Risk：HIGH（跨 Core/Templates/Host/前端共享契约）。已按定向契约测试、完整回归和浏览器验证逐层收口。

## 实现摘要

### Core

- `PrintProtocolRegistration` 校验稳定 ASCII ID、显示名称、工厂和可选 JSON 快照；重复 ID、冻结后注册、未知 ID、工厂返回空会话和返回错误 ID 都明确失败。
- `PrintProtocolRegistry.Freeze` 后目录只读；`Descriptors` 每次返回独立副本，字符串快照不暴露工厂。
- `PrintProtocolOutputComposer` 统一预检、页面输出、收尾位置、长度校验、页尾重复收尾和输出预算；取消/异常不返回部分结果。
- `PrintProtocolProviderBackend` 将旧 `IPrintProtocolProvider` 适配为统一后端；回归验证页面字节顺序不变。

### Templates

- `ITemplateProtocolAdapter` 由宿主启动解析，真正提供 `CreateTextMetrics` 和 `CreateBackend`。
- `ITemplateTextMetrics` 的宽度、行高和边界参与现有 Wrap、分页、坐标和旋转占位；未另写简化模板解释器。
- `TemplateCustomProtocolValidator` 校验元素、字号、旋转、字体和页高；绑定、重复、条件和布局所有权沿用现有 Session。
- 预览包只在自定义能力声明软件位图且元素受支持时生成图片；不支持时返回原因，不伪造像素。

### Host 与示例

- `DesignerProtocolRegistry` 在启动时注册内置适配、`demo.protocol` 和目录后冻结。
- `GET /api/designer/protocol-registry` 返回 ID、显示名、内置标记、配置 Schema、约束和能力 JSON。
- 自定义请求按 `protocolId` 存在判断，和旧 `protocol` 及内置专用字段互斥；未知 ID、无效配置和冲突返回结构化错误。
- 示例程序集支持 `1/3/4/5/7` 元素（文字、QR、一维码、线段、矩形）和可选收尾；改变字符宽度、行高、页高、条码/QR 模块宽度会改变实际布局。示例不声明图片、GS1 QR 和完整页面 PNG，但声明 QR/条码元素软件位图。

### 设计器

- 读取目录并生成扩展协议选项和配置字段；保留高级 JSON 配置。
- 项目 v2 保存 `protocolId`/`protocolConfig`，兼容 v1 和裸模板。
- 未知协议保留选择、配置和项目内容，禁用预览/导出/下载；旧 Host 缺少 `protocol-registry` 时只保留内置协议。
- 自定义能力不再携带旧协议字段；请求头声明 `protocol-registry`。

## 集成修正第 1 轮（历史记录，P2-R1—R10）

主代理初版复核结论为 `NEEDS_FIX`。第 1 轮按 P2-R1—R10 重新实现，未进入 F1/F2，未修改总计划、phase2-contracts 或独立 review 文档。以下计数和证据仅代表第 1 轮，不替代第 2 轮最终结果。

| ID | 本轮结果 | 实现与证据 |
| --- | --- | --- |
| P2-R1 | LOCAL PASS | 新增 `TemplateCustomRenderResult`/`TemplateCustomPage`，不含 `PrintProtocol`；`TemplateRenderer.Render` 拒绝自定义选项，必须走 `RenderCustomProtocol*`。HTTP 自定义能力不再返回 `protocol=Cpcl`；前端 custom `printProtocol=null`，不再伪造 Cpcl。`ProtocolRegistryHostTests`、`customProtocol.spec.ts`。 |
| P2-R2 | LOCAL PASS | `ITemplateProtocolAdapter` 新增 barcode/QR 测量与转换请求，`DocumentPrintCanvas` 不再对 custom 一维码/QR 无条件抛错；示例声明并实现 Code128/Code39/Ean13/QR 布局与输出。Host 能力来自适配器字段，不再用样例无关的硬编码 false。真实 Host QR E2E 与 Host `element-png`/bundle 测试证明软件码图。 |
| P2-R3 | LOCAL PASS | `Session` 自动页高使用适配器 `MaxPageHeightDots`；同一 4×24 dot 记录在 `maxPageHeight=60` 下产生 2 页、每页 ≤60，在大值下一页。Host 自动页高回归覆盖。 |
| P2-R4 | LOCAL PASS | Host 新增通用 `finishing` 请求和旧 feed/cut 映射；composer 先验证收尾声明/请求/长度，再调用 `Render`，控制文件在 `FinishingFiles` 中独立于物理页；Host ZIP 写独立 `finish-*` 文件和 manifest。前端增加收尾配置。未声明收尾的 HTTP 请求在编码前 400；真实 Host E2E 检查 `finish-001.demo-protocol`。 |
| P2-R5 | LOCAL PASS | 核心注册项新增 `PrintProtocolCapabilities`、配置校验委托和稳定 ID；模板自定义渲染通过冻结 `PrintProtocolRegistry.CreateBackend`，内置普通路径经 `PrintProtocolProviderBackend` 与同一 composer；注册表/适配器/后端共用配置校验。内置字节回归继续通过。 |
| P2-R6 | LOCAL PASS | `protocolId` 按字段存在性处理；空白/未知/协议字段冲突和内置专用字段统一由 `PrepareOptions` 拒绝。Capabilities 也先走 `PrepareOptions`。Host 空白 ID HTTP 回归返回 400。 |
| P2-R7 | LOCAL PASS | 保留 mock 降级用例，新增真实 Host 成功用例：目录发现、修改 `qrModuleWidth` 引起 QR 实际布局宽度变化、真实预览包、导出 ZIP 内容、保存/重导入 v2、收尾文件；Host 另有 QR 元素 PNG/bundle 测试。 |
| P2-R8 | LOCAL PASS | 项目 v2 增加可选 `editorState` JSON 容器；保存/读取/深复制并兼容 v1/裸模板，未知协议仍保留/禁用。未实现 F6 锁定/排列。 |
| P2-R9 | LOCAL PASS（文档修正） | 报告区分初版 4640/4641 与修正后 4642；`runtime-smoke` 明确标为早期证据；legacy 首次长路径失败日志已被覆盖、未伪造保留，新报告如实声明缺失；生产 XML 框架为 net7/net6/netcoreapp3.1/netstandard2.0/netstandard2.1，net5/coreapp2.2 只是测试目标，不列为生产五框架；指南配置字段与 `DemoProtocol` 实际 Schema 对齐。 |
| P2-R10 | LOCAL PASS | 增加 demo v2 JSON/README 可运行案例；真实 Host C#/HTTP/浏览器往返；候选包 net10、net6.0、netcoreapp3.1 消费均通过；保留既有 3.1 结果并新增明确 net6 结果。 |

修正后本地计数：

- Core：`4642/4642`（.NET 10.0.13、6.0.36、3.1.32）。
- Host：`281/281`。
- XML：五生产框架 Core/Templates `10/10`。
- Vitest：`421/421`；生产构建 PASS。
- Chromium：`201/201`，`workers=1`、`retries=0`。
- Package：modern net10、net6.0、netcoreapp3.1 PASS。

证据：`artifacts/optimization-expansion-20261025/phase2/integration-r1-*`。外部 CI 仍未运行，保持 `BLOCKED_EXTERNAL`。

## 集成修正第 2 轮（2/3）

本轮逐项关闭 [第 1 轮独立复核](optimization-expansion-phase2-review-r1-2026-10-25.md) 的剩余问题，保留已关闭的 P2-R1/R3/R7/R8；不进入 F1/F2，不修改总计划、phase2-contracts、初版复核或 r1 复核文档，不覆盖 r1/main-review-r1 证据。

| ID | 本轮结果 | 实现与最终证据 |
| --- | --- | --- |
| P2-R2 | LOCAL PASS | GS1 QR 改为独立能力 `SupportsGs1QrCode`；示例明确声明不支持，`Gs1=true` 在编码前 400，Host `gs1TwoDimensional` 为 null，不再降级普通 QR。QR 度量只加一次静区，`HELLO/M/module4` 为 116 点，与共享 Raster 一致。旋转一维码使用未旋转编码尺寸：`Code128/ABC123/40/90°` 布局 `40×202`，元素 PNG 与 bundle 均实际生成。Bold/Reverse/Underline、显式静区/模块/HRI、填充矩形、坐标布局等未声明能力由共享 validator 和样例后端提前拒绝。`ProtocolRegistryHostTests` 新增准确尺寸、原始 PNG、GS1 与样式反例。 |
| P2-R4 | LOCAL PASS | ZIP 只创建一次控制条目；测试直接检查 `ZipArchive.Entries` 唯一性及首个条目非空。`copies×PageEnd` 先展开实际物理副本，`PageEnd` 在每个副本后执行，`JobEnd` 仅一次；manifest 记录 source/copy/order 和 `preserveFileBoundaries:true`、`outputVersion:2`。未声明收尾仍在校验阶段拒绝，直接核心/示例 backends 通过旧 `FeedLines`/`CutAfterPrint` 时也失败。新增副本顺序、控制边界、预算和非零 RenderCalls 前拒绝断言。 |
| P2-R5 | LOCAL PASS | 请求能力字段改为来自适配器快照，移除了 Host `supportsCoordinates=true` 等固定值；目录默认能力与请求配置能力分离，`dpi=300`/页高变化可见。模板指南明确 `dpi` 在 Schema 中。新增不同于样例默认值的独立适配器测试：DPI 300、坐标 false、fontKinds `[1,2]`、maxTextSize 96、GS1 QR true，验证 Host 输出随声明变化。`TemplateCustomProtocolCapabilities` 仍是模板层独立快照类型，但不再作为 Host 固定值来源；该双类型边界作为剩余架构风险明示。 |
| P2-R6 | LOCAL PASS | 请求持有字段存在性标记；`protocolId:null`、`protocolConfig:null`、`finishing:null`、空白 ID、未知 ID、新旧字段冲突和内置协议携带 finishing 均明确 400。自定义 `finishing` 与旧 feed/cut 冲突拒绝；合法省略仍保留旧默认。能力、预览、导出、预览包和元素 PNG 共用同一准入路径。 |
| P2-R9 | LOCAL PASS | 报告和指南改正“样例不支持条码/QR/软件位图”等旧描述；定向 E2E 为 4/4（3 mock + 1 真实 Host）。生产 XML 框架固定为 net7/net6/netcoreapp3.1/netstandard2.0/netstandard2.1；net5/coreapp2.2 仍只作测试目标。 |
| P2-R10 | LOCAL PASS | E2E 直接读取 `samples/Protocols/DemoProtocol/demo-project.json`，执行导入、真实预览包、QR 配置修改、导出 ZIP、保存和重导入。`samples/Templates` 的 C#/JSON/往返/GS1/裁切案例已实际运行，根目录 103 个文件、含子目录共 132 个文件。现代 net10、net6.0、netcoreapp3.1 候选包消费均 PASS；net6/3.1 首次长路径 `MSB3030` 环境失败日志保留，短路径重跑成功，不冒充产品失败。 |

第 2 轮最终本地计数：

- Core：`4643/4643`（.NET 10.0.13、6.0.36、3.1.32）。
- Host：`287/287`（新增独立非默认适配器能力测试）。
- XML：五生产框架 Core/Templates `10/10`。
- Vitest：`421/421`；生产构建 PASS。
- Chromium：`201/201`，`workers=1`、`retries=0`。
- 模板样例：PASS，根目录 103 个文件，含子目录共 132 个文件（均为文件数，不是测试用例数）。
- Package：modern net10、net6.0、netcoreapp3.1 PASS。

第 2 轮证据根：`artifacts/optimization-expansion-20261025/phase2/integration-r2-*`。外部 CI 仍未运行，保持 `BLOCKED_EXTERNAL`。

## 集成修正第 3 轮（3/3）

本轮只处理第 2 轮复核剩余的 R2/R4/R5；R1/R3/R6/R7/R8/R9/R10 保持关闭，不重开，不进入 F1/F2，不启动第 4 轮。证据使用 `integration-r3-*`，r1/r2/main-review 原样保留。

| ID | 本轮结果 | 实现与证据 |
| --- | --- | --- |
| P2-R4（A：页数/取消） | LOCAL PASS | `PrintProtocolOutputComposer` 新增共同物理副本规范化和 `ValidatePageCount`：以 `long` 溢出安全计数 `Copies`，在分配副本和调用编码器前按 `MaxPages` 拒绝；每个源页、副本和元素复制都观察取消。模板层在创建后端前调用同一计数，100 通过、101 拒绝；拒绝时后端工厂/`RenderCalls=0`。测试还覆盖自定义 `MaxPages=2`、`RenderFinishing` 回调中取消、`Render` 回调中取消、回调异常以及临时预留归零。 |
| P2-R4（B：Core/PageEnd） | LOCAL PASS | `PrintProtocolOutputComposer.Render` 现在是共同新协议入口：展开 `PrintPage.Copies`，`PageEnd` 在每个物理副本后生成控制文件，`JobEnd` 仅一次；`PrintProtocolOutputResult.PageOrigins` 给出与页面输出一一对应的 source/copy。页面旧 `FeedLines`/`CutAfterPrint`/`CutOptions` 在编码前拒绝。旧内置 Provider 路径改用内部 `RenderLegacy`，保留原生 copies/字节行为；`Legacy_composer_preserves_native_page_copies_and_controls` 断言不展开。 |
| P2-R2/P2-R5（C：能力执行） | LOCAL PASS | Core 和模板 `SupportsTextSize` 同时检查正数、`MaxTextSize` 和离散集合；`FontKinds` 参与 `TemplateFontResolver` 校验，声明 `Device` 且 `SupportsLegacyDeviceFont=true` 时设备字体进入测量和 `PrintTextElement.DeviceFontName`，未声明才拒绝。`PrintProtocolCapabilities.WithMaxTextSize`/`WithExplicitQuietZone` 派生配置快照，`TemplateCustomProtocolCapabilities.FromCore` 投影模板层能力；Demo 注册项与适配器共用 `CreateCoreCapabilities(config)`。独立 Host 测试通过冻结注册表、配置 Schema/validator、模板设备字体、度量、后端输出和 Host `/preview` 协商，64 成功、65 在编码前 400；另有样例 `MaxTextSize=512` 时 FontSize 513 的 Host 400 反例。第 2 轮的 GS1 拒绝、QR 116×116、旋转条码 PNG 回归通过。 |
| 文档/证据（R9 小修） | LOCAL PASS | 模板样例统计统一为“根目录 103、含子目录 132”，不把根文件数写成总数；第 3 轮模板案例保存独立 stdout/stderr 与 `exit-code.txt`，不再依赖完成输出推断退出码；报告摘要补入条码/QR。 |

第 3 轮最终本地计数：

- Core：`4653/4653`（.NET 10.0.13、6.0.36、3.1.32）。
- Host：`290/290`。
- XML：五生产框架 Core/Templates `10/10`。
- Chromium：`201/201`，`workers=1`、`retries=0`；Vite/Vitest 前端源未变更，复用第 2 轮 `421/421` 与生产构建 PASS。
- 模板样例：根目录 103、含子目录 132；退出码 0。
- Package：modern net10、net6.0、netcoreapp3.1 PASS。

第 3 轮早期 `integration-r3-host/` 与最终 `integration-r3-host-final/` 均为 290 通过；287/286 仅属于 r2 历史记录。第 3 轮证据根：`artifacts/optimization-expansion-20261025/phase2/integration-r3-*`。O4 仍需主代理复核，不自行宣称 CLOSED；外部 CI 保持 `BLOCKED_EXTERNAL`。
## 验收矩阵

| 验收项 | 结果 | 证据 |
| --- | --- | --- |
| 注册表冻结、重复/未知 ID、快照独立 | PASS | `ProtocolRegistryTests` |
| 内置 Provider 适配字节/顺序一致 | PASS | `ProtocolRegistryTests.Builtin_provider_adapter_keeps_page_bytes_and_order` |
| 收尾预检、长度、JobEnd/PageEnd、预算、取消 | PASS | `ProtocolRegistryTests` 收尾用例 |
| 自定义测量改变换行/布局、绑定/重复复用 | PASS | `TemplateCustomProtocolTests` |
| 不支持元素在 backend 前拒绝、backend 每请求释放 | PASS | `TemplateCustomProtocolTests` |
| Host 目录、能力、预览、导出、预览包 | PASS | `ProtocolRegistryHostTests` |
| 未知 ID、错误配置、新旧字段冲突 | PASS | `ProtocolRegistryHostTests` |
| 示例 QR/条码软件位图、旋转尺寸、Page PNG 拒绝、未声明 GS1/样式/收尾拒绝 | PASS | `ProtocolRegistryHostTests` |
| copies×PageEnd 物理副本顺序、唯一 ZIP 控制条目、manifest source/copy | PASS | `ProtocolRegistryHostTests` |
| Core composer 物理副本、PageOrigins、100/101 上限、回调取消/异常释放 | PASS | `ProtocolRegistryTests` |
| 旧内置 Provider 原生 copies/旧控制字段不展开 | PASS | `ProtocolRegistryTests.Legacy_composer_preserves_native_page_copies_and_controls` |
| 目录默认与请求配置能力分离、非默认坐标/字体/限制适配器 | PASS | `ProtocolRegistryHostTests`、`IndependentProtocolHostTests` |
| 最大字号、FontKinds、设备字体测量/输出及 Host 协商 | PASS | `TemplateCustomProtocolTests`、`IndependentProtocolHostTests` |
| 示例收尾直接链路与旧快捷字段拒绝 | PASS | `ProtocolRegistryHostTests` |
| 设计器目录、Schema 表单、预览请求、v2 保存 | PASS | `customProtocol.spec.ts` |
| 未知协议保留/禁用、旧 Host 降级 | PASS | `customProtocol.spec.ts` |
| 内置行为/完整设计器回归 | PASS | 完整 Core/Host/Vitest/Chromium |

## 验证结果

所有命令均在当前工作区执行，未提交/推送。源码 HEAD：`6c707be4092dada2dabe345a9eb92e1e907c1acb`，工作区包含第一阶段、O4 第 1/2/3 轮未提交变更。以下为第 3 轮最终结果；第 1/2 轮计数仅保留在上方历史节。

### XML 文档构建

```powershell
$env:PATH='C:\Users\jianx\AppData\Local\Temp\opencode\dotnet-sdk-10.0.402;' + $env:PATH
tools/Verify-XmlDocumentation.ps1
```

结果：`PASS`，Core/Templates × 5 目标框架共 10/10 构建通过，XML 警告码全部按错误处理。证据：`artifacts/optimization-expansion-20261025/phase2/integration-r3-xml/verification.json`、`integration-r3-xml.stdout.log`。

### 运行时完整测试

```powershell
# net10.0：隔离 SDK 10.0.402 / runtime 10.0.13
dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -c Debug -f net10.0

# net6.0 与 netcoreapp3.1：系统 SDK host，实际运行时分别为 6.0.36 / 3.1.32
tools/Verify-ProtocolRuntimes.ps1 -Frameworks @('net6.0','netcoreapp3.1')
```

| 运行时 | 总数 | 通过 | 失败 | 跳过 |
| --- | ---: | ---: | ---: | ---: |
| .NET 10.0.13 | 4653 | 4653 | 0 | 0 |
| .NET 6.0.36 | 4653 | 4653 | 0 | 0 |
| .NET Core 3.1.32 | 4653 | 4653 | 0 | 0 |

证据：`phase2/integration-r3-core/`、`phase2/integration-r3-runtime/`。r1/r2 证据只作历史，不代表第 3 轮。源码身份：`phase2/source-identity.json`。

### Host

```powershell
tools/Verify-DesignerHost.ps1 -NoRestore
```

结果：290/290 通过，0 失败，0 跳过。第 3 轮早期 `integration-r3-host/` 与最终 `integration-r3-host-final/` 均为 290；287/286 仅属于 r2 历史记录。证据：`phase2/integration-r3-host-final/`、`integration-r3-host-final.stdout.log`；r2 记录保留，不覆盖。

### 前端与浏览器

```powershell
cd web/Bing.EasyPrint.Designer
npm run build
npm test
npx playwright test --workers=1 --retries=0
```

- 前端源未变更，生产构建与 Vitest 复用第 2 轮：PASS、21 个文件、421/421。
- Chromium E2E：201/201 通过，`workers=1`、`retries=0`、0 失败、0 跳过。
- O4 定向 E2E：4/4（3 个 mock 降级/未知协议用例 + 1 个真实 Host 目录/配置/预览/导出/保存重导入用例）。

证据：`phase2/integration-r3-e2e.stdout.log`；前端构建/Vitest 复用 `integration-r2-designer-build.stdout.log`、`integration-r2-designer-vitest.stdout.log`。

### 候选包消费

- modern：`net10.0`，`.NET 10.0.13`，PASS；消费项目使用候选包中的 net7.0 资产，不代表生产库新增 net10 目标。证据：`phase2/integration-r3-modern/`。
- net6.0：PASS，实际运行时 `.NET 6.0.36`；证据：`phase2/integration-r3-consumer-net6/consumer-verification.json`、`integration-r3-consumer-net6.stdout.log`。
- legacy：`netcoreapp3.1`，`.NET Core 3.1.32`，PASS；证据：`phase2/integration-r3-c31/consumer-verification.json`、`integration-r3-c31.stdout.log`。
- 上述 net6/3.1 候选包与 modern 运行使用同一候选包目录，包哈希校验为 PASS。r2 首次长路径失败日志继续保留；r3 使用短结果目录后复制验证 JSON。
- 模板样例：`dotnet run --project samples/Templates/Templates.csproj -c Release -f net6.0 --no-restore -- <output>`，退出码 `0`，根目录 103、含子目录 132；证据：`phase2/integration-r3-template-samples/`、`integration-r3-template-samples.stdout.log`、`.stderr.log`；退出码完整路径为 `artifacts/optimization-expansion-20261025/phase2/integration-r3-template-samples.exit-code.txt`。
## 证据与调试边界

- net10 的 net6 首次尝试使用了只含 10.0.13 的隔离 SDK host，缺少 net6 运行时，未执行测试；随后用系统 SDK host 的 6.0.36 重跑并通过。该失败尝试保留在 `phase2/runtime-verification/`，未删除或覆盖。
- legacy package consumer 首次输出目录路径超过 260 字符，Windows 短路径复制失败；改用短临时结果目录后 `netcoreapp3.1` 运行通过，manifest 已复制入证据目录。该问题属于验证路径环境，不是产品代码失败。
- 第 2 轮 net6/3.1 消费首次同样因结果目录过深触发 `MSB3030`；`*.failed-first.stdout.log` 保留原始输出，短目录成功运行后把 `consumer-verification.json` 和成功 stdout 复制到 `integration-r2-*`，未删除首次失败。第 3 轮候选包使用短目录，退出码 0。
- 第 3 轮模板案例单独保存 `integration-r3-template-samples.stdout.log`、`.stderr.log` 和 `.exit-code.txt`（0）；不伪造第 2 轮缺失的退出码 sidecar。
- 生产五框架严格 XML 构建为 `net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`；`net5.0`、`netcoreapp2.2` 仅作为额外测试目标，不属于生产五框架。本机没有 net7/net5/netcoreapp2.2 真实运行时，未虚报运行结果。
- 示例协议声明 QR/条码元素软件位图，但不声明完整页面 PNG；受支持元素 PNG/预览包可用，页面 PNG 或未声明元素明确拒绝。
- 真实打印机固件、字体像素、走纸、裁切和扫描未验收；外部 GitHub Actions 仍 `BLOCKED_EXTERNAL`。
- 未执行 commit、push、发布或版本变更；未修改 `PrintProtocol` 枚举、API 基线或 ImageSharp 依赖。

## 后续

O4 交主代理复核。若发现新问题，只修复 O4 范围内契约/集成/证据；不混入 F1—F6。外部 CI 缺口单独保持 `BLOCKED_EXTERNAL`，不以本地通过替代。
