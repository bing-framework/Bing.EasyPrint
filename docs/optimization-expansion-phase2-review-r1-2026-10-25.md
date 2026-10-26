# 第二阶段 O4：集成修正第 1 轮独立复核

> 保留 r1 历史记录；当前结论见 [r2 复核](optimization-expansion-phase2-review-r2-2026-10-25.md)。

日期：2026-10-25。结论：`NEEDS_FIX`，有实质进展，但 O4 尚未满足验收。下一轮为集成修正 **2/3**，不进入 F1/F2。初版记录保留在 [初版复核](optimization-expansion-phase2-review-2026-10-25.md)。

## 验证范围与证据

主代理检查本轮 Core/Templates/Host/设计器源码、样例和回归断言，并启动交付的 net10 Host DLL，在端口 5197 执行定向 HTTP 验证；进程已停止。使用 SDK 10.0.402 所附运行时启动，未修改源码或测试。没有重复全量测试。

- 正式确认：`artifacts/optimization-expansion-20261025/phase2/main-review-r1/host-probes-confirmed.json`，包含请求快照、HTTP 状态、JSON 和 ZIP 条目内容。
- 可复核命令：同目录 `confirm.ps1`。原先 `host-probes.json` 是诊断记录，部分 request 对象在后续请求中被修改；请求与响应对应关系以 confirmed 文件为准，不将诊断文件混用作最终证据。
- 独立 luna_worker 核对：Core net10/net6/netcoreapp3.1 各 4642/4642；Host 281/281；XML 10/10；Vitest 421/421；Chromium 201/201、1 worker；现代/net6/3.1 包消费 PASS，包哈希一致。net10 消费项目使用包内 net7.0 资产，不意味着生产库新增 net10 目标。
- 原始结果：`integration-r1-core-latest/core-net10.trx`、`integration-r1-host-latest/host-net10.trx`、`integration-r1-runtime-final/20261025-123522-09ace8fd/`、`integration-r1-xml-documentation-final/verification.json`、`integration-r1-e2e-current.stdout.log` 等，均在 phase2 证据目录。

以上计数可信，但现有断言未覆盖下列已复现行为。

## 原问题状态

| ID | 状态 | 本轮结论 |
| --- | --- | --- |
| P2-R1 | CLOSED | 对外新增独立 custom 入口/结果，HTTP 不再伪装 Cpcl，前端 custom 协议为 null；内部复用旧容器不暴露错误协议值。 |
| P2-R2 | OPEN_ACTIONABLE | 新增 QR/一维码适配契约已有进展；声明、预检、度量与输出仍不一致，见下方 R2-a/b/c。 |
| P2-R3 | CLOSED | 确认 4×24 dot 自动页高内容：max60 为两页各 48，max200 为一页 96。 |
| P2-R4 | OPEN_ACTIONABLE | 未声明收尾的 HTTP 已正确 400，且 preflight 位于 Render 前；但 ZIP 重复条目、控制边界与份数语义仍错误，见 R4-a/b。 |
| P2-R5 | OPEN_ACTIONABLE | 自定义会话已通过冻结 registry 创建，内置普通渲染接入共同 composer；能力仍是相互独立的 Core/Template/JSON 副本，Host 还存在固定值，见 R5。 |
| P2-R6 | OPEN_ACTIONABLE | 空白 ID 已拒绝；实际字段存在性仍未处理 null，新增 finishing 可被旧目标静默忽略，见 R6。 |
| P2-R7 | CLOSED | 已新增不 mock 的真实 Host 浏览器成功路径并执行；后续对本轮缺陷补强断言归入相应问题，不能只断言文件存在。 |
| P2-R8 | CLOSED | v2 editorState 容器与深复制/往返单测已存在，兼容旧项目，未提前扩展 F6。 |
| P2-R9 | OPEN_ACTIONABLE | 报告/指南仍保留与本轮实现相反的样例能力描述，并少报定向 E2E。 |
| P2-R10 | OPEN_ACTIONABLE | net6 候选消费已补齐；新增 JSON 文件自身及既有模板 C#/JSON/往返案例仍缺本轮执行证据。 |

## 剩余问题与验收条件

### R4-a（P1）：ZIP 收尾文件重复，清单错误声明可合并

位置：`tools/DesignerHost/DesignerRequestProcessor.cs` 的 Render 控制文件循环连续调用两次 `archive.CreateEntry(control.File, ...)`；同方法的 `hasIndependentOutputSegments` 仅检查页面 segments。

确认请求：`finishing-duplicate-zip`。响应 200，但有两个 `finish-001.demo-protocol`，第一个长度 0，第二个 17；`job.json` 却是 `preserveFileBoundaries:false`、`outputVersion:null`，`files` 仅列页面。依赖首个同名条目的消费者读取到空收尾，依赖有序清单的消费者可能漏发或合并控制文件。

验收：ZIP 路径唯一；每个控制文件内容准确且非空（请求有 FEED/CUT 时）；协议中立清单明确独立边界及全部实际发送顺序，页尾控制必须插在对应页/份后，任务末尾放在末尾。使用原始 ZipArchive 条目计数和内容断言，不能依赖 JSZip 同名覆盖或只检查 GetEntry 非 null。

### R4-b（P1）：PageEnd 未覆盖实际份数，核心快捷控制仍可绕过声明

位置：`PrintProtocolFinishing.cs` 的 composer 按 `document.Pages.Count` 生成控制文件；`DemoProtocolExtension.cs` 的 Backend.Render 在同一页面文件内循环 `page.Copies`，且直接输出 page.FeedLines/CutAfterPrint，Validate 没有拒绝未声明控制。

确认请求：`copies-pageend`，copies=2、PageEnd，页面文件包含两个 `DEMO 1`，却仅有一份收尾命令，无法在两份之间插入。即使移除重复 ZIP 条目，该顺序仍错误。直接核心使用时 supportsFinishing=false 的文档快捷 feed/cut 仍会进入 Render 输出（源码路径证据）。

验收：明确定义并实现物理页、逻辑页和份数；PageEnd 在每个实际副本后执行，JobEnd 仅一次，清单保留 source/copy 索引，预算计入全部文件；旧快捷字段不能绕过声明。补直接 Core 和 HTTP 的多页×多份、JobEnd/PageEnd、拒绝时 RenderCalls=0、取消/异常释放与无部分结果用例。不能用拒绝合法多份请求规避计划要求。

### R2-a（P1）：错误声明 GS1 QR 并静默丢弃 Gs1

位置：`DesignerRequestProcessor.CustomCapabilities` 将 `gs1TwoDimensional.qr` 设为 SupportsQrCode；`TemplateRenderer.Element` 提前进入 CustomQrCode；Demo 的 Measure/ConvertQrCode 未读取 Gs1；PrepareOptions 把 ValidateGs1Associations 固定为 false。

确认请求：`Gs1:true`、内容 `NOT-GS1` 返回 200，输出普通 `Q 0 0 4 M NOT-GS1`。合法的 GS1 输入同样走普通 QR 路径，丢失 FNC1 语义。

验收：普通 QR 和 GS1 QR 单独声明能力；未声明 GS1 时预检明确拒绝，不能降级普通二维码；声明支持则复用共享 GS1 编码/诊断并验证 FNC1。关联校验请求不得静默丢弃。新增非法输入、合法输入独立解码或编码矩阵验证及真实 Host 协商测试。

### R2-b（P1）：QR 静区重复计入，旋转条码度量/转换不一致

位置：Demo `MeasureQrCode` 的 ZXing 默认矩阵已含四模块静区，随后又加 8×module；`ConvertBarcode` 使用旋转后的 request.Height 当作原始条高、request.Width 当作原始 reservedWidth。

确认：HELLO、M、模块 4 时自定义布局为 148×148，同一共享软件 QR 路径为 116×116。Code128 ABC123、条高 40、旋转 90 的布局为 40×202，但 preview-bundle 的 images=[]，诊断为绘制内容超出 ESC/POS Raster 画布边界。

验收：度量与转换共用未旋转编码尺寸，静区仅加一次；0/90/180/270 和非对称静区均保证布局/PNG/实际编码占位一致；测试应核对准确尺寸、像素或模块而不是只检查宽度随配置增大。样例输出保留其宣称支持的模块、旋转等参数，不能只在预览中生效。

### R2-c（P1）：未声明样式被接受并悄悄丢弃

位置：TemplateCustomProtocolValidator.ValidateFont 未验证 Bold/Reverse/Underline；同类宽模块/HRI/填充/绝对坐标等声明也未完整验证。Demo Backend.Validate 仅验证文字角度与字体来源。

确认：Bold=true、Reverse=true 返回 200，输出与无样式的 `T 0 0 24 A` 相同，尽管能力声明均为 false。

验收：对每个已公开能力开关，支持时准确表达、不支持时在编码前拒绝；共享模板入口和直接核心入口一致。针对样式和条码参数提供反例，不以 UI 禁用代替服务端验证。

### R5（P2）：能力元数据仍重复，第三方声明不能完整传至 Host

位置：Core PrintProtocolCapabilities 和 TemplateCustomProtocolCapabilities 仍分别持有能力；Demo 的 CreateCoreCapabilities、Adapter 构造和 CapabilitiesJson 独立复制；Host CustomCapabilities 仍写死 supportsCoordinates=true、fontKinds=[0]、supportsExplicitQuietZone=false、textSize.max=512 等。

影响：第三方声明非默认字体来源或无坐标能力不能在 Host/设计器准确体现，样例仅覆盖默认子集不足以证明完整注册。目录 schema 不含 DPI 且 additionalProperties=false，服务端却接受 Dpi；目录默认能力与请求配置后的能力也没有明确区分契约。

验收：提供一个权威的配置解析/能力快照来源供 Core、模板和 Host 使用，明确目录默认描述与请求有效能力的关系；避免相同事实三处手工维护。独立协议测试使用区别于样例默认值的坐标、字体及限制，证明配置、能力、预检和 UI 一致，错误配置在任何输出前失败。无需更改已有公共 API 基线。

### R6（P2）：新增字段仍可能被静默忽略

位置：PrepareOptions 使用 request.ProtocolId != null 判断而不是 JSON 字段存在性；旧协议分支忽略 request.Finishing。

确认：`protocolId:null` 能力请求返回内置 Cpcl；Cpcl 携带 finishing feed3/cuttrue 返回 200、只有普通页面，无错误也无收尾。

验收：统一字段存在性与冲突校验，覆盖缺省、null、空串、空白和新旧冲突，保留合法旧省略默认。自定义 finishing 与旧目标/旧快捷字段冲突明确拒绝，不静默忽略；所有能力/预览/导出/PNG 入口一致。

### R9 / R10（P2）：同步当前说明并执行实际案例

- 报告第 13/48/166 行及指南第 141 行仍称样例不支持条码/二维码/软件位图，与新实现矛盾；O4 定向 E2E 实际 4/4（3 mock + 1 real），不是 3/3。
- `demo-project.json` 存在不等于已运行；E2E 使用内联对象。读取仓库真实 JSON 运行导入、渲染、保存/重载；运行 `samples/Templates` 的 C#/JSON/往返案例并保存本轮命令、退出码、产物和检查。
- net6 消费证据已通过，不再将其列为未完成；若下轮变更相关产物，更新候选验证。历史首次失败日志缺失继续如实披露，不重新制造日志。

## 下一轮边界

Implementation：3/10，O4 未关闭。本轮 4 项原 Finding 关闭，6 项保持 OPEN_ACTIONABLE，有进展，不触发无进展停止。外部 CI 为 BLOCKED_EXTERNAL；实机验收 NOT_APPLICABLE；既定延期范围不变。

第 2 轮仅处理本报告剩余问题。先运行能复现缺陷的定向测试，再按 Core/Templates/Host/前端实际变更范围扩大；阶段候选仍须完整验收，Chromium retries=0。不得仅删声明来缩小完整协议注册目标，但示例未实现的可选 GS1 能力可以准确声明不支持并拒绝。禁止提交、发布、版本变更、覆盖旧 API 基线或升级核心图片依赖。
