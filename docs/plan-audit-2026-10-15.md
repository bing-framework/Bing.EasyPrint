# 计划完成状态核对与拟清理清单

核对日期：2026-10-15。基准提交：`1f590cb92317f928ab689dc65c0e6ca85d22cf56`，分支 `feat/print-template-designer`。

用户确认以 `ai_docs/plans` 替代不存在的 `ai_docs/tasks`。目录仅有下列两份计划；逐项清单以核对候选中的条目为单位，不将历史 R1–R154 的轮次数当作独立任务数。初次核对保留原文，用户随后确认清理，现已迁出完成记录并精简两份计划。本文的“原位置”及源码/文档行号均绑定上述候选，清理后的维护位置和验证结果见末节；未完成、待确认项继续保留。

| 文件 | 整体结论 | 已执行处理 |
| --- | --- | --- |
| [多协议打印与旋转](../ai_docs/plans/multi-protocol-todo-progress.md) | 部分完成：51 个能力条目中 50 个代码实现已确认，T8 当前完整门禁待确认；另有 18 个未勾选条目及后续范围 | 保留并精简为剩余任务入口；完成记录按 G1–G6 迁入现有文档 |
| [中文 XML 注释](../ai_docs/plans/chinese-comments-plan.md) | 未整体完成：七个批次均不能对当前范围宣布验收通过；批次 1 有历史复核记录但仍存在单位说明缺口，批次 2–6 有当前反例，批次 7 严格 token 验收无初始快照 | 保留当前 C1–C7、Q1–Q6 状态；稳定规则及历史统计迁入开发文档 |

## 判断标准与可复核证据

- **完成（代码）**：当前实现路径与对应测试断言相符；只表示该行限定范围，不推导为硬件、发布或完整标准支持。
- **部分完成**：实现或工具存在，但该任务的部分验收条件未闭环。
- **未完成**：已知要求尚缺实现、设备实测或其他验收证据；外部阻塞、待审批、延期仍须保留。
- **待确认**：证据不足或只有历史记录；不改成完成，不纳入拟删除范围。
- **接受限制／条件项**：保留契约说明，不把已接受的边界自动变为开发任务。

### 本会话已执行且可复用的验证

本报告前的分组提交操作实际执行了下列命令。此次检查确认 HEAD 仍为上述基准，`git diff 1f590cb -- src tests tools web` 无输出，检查开始时工作区干净，因此复用这些未变化范围的证据；本次核对没有重新运行这些测试。

| 验证命令 | 实际结果 | 适用范围 |
| --- | --- | --- |
| `dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net6.0 --no-restore --filter FullyQualifiedName~Gs1Composite --verbosity minimal` | 102/102，0 跳过 | Composite 编码、Provider、模板 |
| `dotnet test tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj -f net6.0 --no-restore --verbosity minimal` | 3360/3360，0 跳过 | Core/Templates 的 .NET 6 自动测试 |
| `dotnet test tests/DesignerHost.Tests/DesignerHost.Tests.csproj --no-restore -p:EnableDefaultContentItems=false --verbosity minimal` | 135/135，0 跳过 | .NET 8 Host 请求、能力、预览、输出等测试；沿用避开 artifacts 内容复制的命令行属性 |
| `npm test`，工作目录 `web/Bing.EasyPrint.Designer` | 20 文件、267/267 | 前端单元测试；不包含浏览器 E2E |
| `npm run build`，同上 | vue-tsc 与 Vite 成功；主包超过 500 kB 提示 | 类型检查、前端生产构建 |
| `dotnet build src/Bing.EasyPrint.Templates/Bing.EasyPrint.Templates.csproj --no-restore --verbosity minimal` | Core/Templates 的 net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1 均成功，0 警告、0 错误 | 普通构建；未开启计划要求的严格注释验收 |

此次另执行 `dotnet --list-runtimes`：标准安装目录没有 .NET 7。没有执行设备打印、扫描、跨平台 CI、包消费者重建、性能门禁、浏览器 E2E 或完整语法树注释审计。[历史验证记录](multi-protocol-verification.md) R87 部分（第 774 行起）与两份计划中的历史数字均保留为历史证据，不当作当前版本新运行结果。

## 多协议计划：51 个能力条目逐项核对

“原位置”指基准提交中 `multi-protocol-todo-progress.md` 行号。表内测试链接指向可复核断言所在文件；对应 .NET 6、Host 和前端单元测试运行结果见上表。G1–G6 是后文的大功能编号。

| ID／原位置 | 任务与状态 | 实现／验证依据 | 归类 |
| --- | --- | --- | --- |
| T1／19 | ESC/POS Profile 贯通：完成（代码） | [命令构造与快照](../src/Bing.EasyPrint/Protocols/EscPosPrintCommand.cs)校验 DPI、布局和 Profile；[Profile 测试](../tests/Bing.EasyPrint.Tests/EscPosPrinterProfileTests.cs)含 `Provider_forwards_esc_star_profile_to_raster_output`；Host 与前端能力转发已核对 | G1 |
| T2／20 | 重采样、图片参数和追加前校验：完成（代码） | [Profile 测试](../tests/Bing.EasyPrint.Tests/EscPosPrinterProfileTests.cs)第 138、233、252 行分别覆盖非方形 DPI 重采样、失败原子性、极端高度；[图片输入测试](../tests/Bing.EasyPrint.Tests/CoreRegressionTests.cs)第 61、77、91 行覆盖原图、流与上限 | G2 |
| T3／21 | 语义字体与旧 CPCL 适配：完成（代码） | [TemplateFontResolver](../src/Bing.EasyPrint.Templates/Rendering/TemplateFontResolver.cs)；[语义字体测试](../tests/Bing.EasyPrint.Tests/TemplateSemanticFontTests.cs)第 34、47、63、142 行比较旧/新字节、旋转及 JSON 往返 | G2 |
| T4／22 | 未知字体样式预检：完成（代码） | [TemplateGraph](../src/Bing.EasyPrint.Templates/Rendering/TemplateGraph.cs)静态校验；[预检测试](../tests/Bing.EasyPrint.Tests/TemplateFontStylePreflightTests.cs)第 20 行验证资源和数据访问前拒绝，第 66 行验证显式位图开关 | G2 |
| T5／23 | 细粒度能力与入口过滤：完成（代码） | [PrintCapabilities](../src/Bing.EasyPrint/PrintCapabilities.cs)、[Host 能力响应](../tools/DesignerHost/DesignerRequestProcessor.cs)、[前端过滤](../web/Bing.EasyPrint.Designer/src/capabilities.ts)；[Host 契约测试](../tests/DesignerHost.Tests/HostContractTests.cs)覆盖图片/HRI/QR allowlist | G1、G5 |
| T6／24 | 重复与组件诊断归属：完成（代码） | [TemplateBindingDiagnostic](../src/Bing.EasyPrint.Templates/Rendering/TemplateBindingDiagnostic.cs)；[诊断测试](../tests/Bing.EasyPrint.Tests/TemplateBindingDiagnosticTests.cs)第 128 行断言重复实例的路径与索引；[Provider 归属测试](../tests/Bing.EasyPrint.Tests/TemplateProviderOwnershipTests.cs)覆盖跨页失败 | G2 |
| T7／25 | CPCL 与 Profile DPI：完成（代码） | [能力测试](../tests/Bing.EasyPrint.Tests/ProtocolProfileCapabilitiesTests.cs)第 13 行核对页头；[CPCL 对等测试](../tests/Bing.EasyPrint.Tests/CpclTemplateParityTests.cs)比较模板、Provider 和 direct 命令 | G1 |
| T8／26 | 固定字体、运行时及包消费者门禁：部分完成／待确认 | [固定字体测试](../tests/Bing.EasyPrint.Tests/FixedTestFontsTests.cs)与 [运行时脚本](../tools/Verify-ProtocolRuntimes.ps1)、[消费者脚本](../tools/Verify-PackageConsumer.ps1)存在；R87 记录当时的矩阵通过。当前源码的完整运行时及包消费者证据未重建，标准路径缺 .NET 7 | G6 |
| S1／27 | 原生/固定字体及校准接口：完成（代码） | [RasterFontCatalog](../src/Bing.EasyPrint/Protocols/RasterFontCatalog.cs)、[DeviceFontCalibration](../src/Bing.EasyPrint.Templates/Rendering/DeviceFontCalibration.cs)；[校准测试](../tests/Bing.EasyPrint.Tests/TemplateDeviceFontCalibrationTests.cs)覆盖逐字体及文本单元 advance/墨迹 | G2 |
| S2／28 | 标签 Profile、切刀及独立文件：完成（代码） | [TSPL 切刀测试](../tests/Bing.EasyPrint.Tests/TsplCutterTests.cs)、[ZPL 切刀测试](../tests/Bing.EasyPrint.Tests/ZplCutterTests.cs)第 52 行验证延迟切刀独立控制段，保留旧 Build 限制 | G1 |
| S3／29 | Job 份数、顺序、进纸/切纸和预算：完成（代码） | [PrintJobRenderer](../src/Bing.EasyPrint/Printing/PrintJobRenderer.cs)在展开前校验页数和输出；[Job 测试](../tests/Bing.EasyPrint.Tests/PrintJobTests.cs)第 26、48、95、322 行覆盖顺序、收尾和预算 | G1、G4 |
| S4／30 | 静区、相邻检查及保存：完成（代码） | [PrintQuietZoneValidator](../src/Bing.EasyPrint/Printing/PrintQuietZoneValidator.cs)；[协议静区测试](../tests/Bing.EasyPrint.Tests/ProtocolQuietZoneTests.cs)第 78、114 行及 [模板静区测试](../tests/Bing.EasyPrint.Tests/TemplateQuietZoneTests.cs)第 16、129 行 | G2 |
| S5／31 | 存储预算、取消、请求预算和准入：完成（代码） | [PrintStorageBudget](../src/Bing.EasyPrint/Core/PrintStorageBudget.cs)；[工作集测试](../tests/Bing.EasyPrint.Tests/PrintWorkingBudgetTests.cs)含分配前预留；[Host 准入测试](../tests/DesignerHost.Tests/HostAdmissionTests.cs)覆盖 FIFO、取消、释放；进程隔离单列未完成 | G4 |
| S6／32 | 元素归属、数组与资源快照：完成（代码） | [PrintDocument](../src/Bing.EasyPrint/Printing/PrintDocument.cs)的独立 SourcePath 和 Raster 副本；[快照测试](../tests/Bing.EasyPrint.Tests/PrintDocumentSnapshotTests.cs)第 10、80、97 行 | G4 |
| S7／33 | Native 顺序收据和模板接入：完成（代码） | [Native flow 测试](../tests/Bing.EasyPrint.Tests/EscPosNativeFlowTests.cs)、[组合参数测试](../tests/Bing.EasyPrint.Tests/TemplateEscPosNativeParameterCombinationTests.cs)第 24、52、76 行覆盖 HRI、模块和拒绝路径 | G1、G2 |
| S8／34 | Host 协商、严格请求、错误与绑定：完成（代码） | [DesignerContract](../tools/DesignerHost/DesignerContract.cs)；[HostContractTests](../tests/DesignerHost.Tests/HostContractTests.cs)第 247、265、286、311、359 行验证特性、版本、未知选项和绑定路径；[api.ts](../web/Bing.EasyPrint.Designer/src/api.ts)核对响应契约 | G5 |
| L1／40 | 图片旋转与填充矩形：完成（代码） | [图片旋转测试](../tests/Bing.EasyPrint.Tests/PrintImageRotationTests.cs)第 19、59、85 行比较像素和 Provider 输出；[填充测试](../tests/Bing.EasyPrint.Tests/TemplateFilledRectangleTests.cs)第 26、90、120 行覆盖工厂、往返与旧轮廓字节 | G2 |
| L2／41 | 旋转布局、换行和分页：完成（代码） | [TemplateRenderer](../src/Bing.EasyPrint.Templates/Rendering/TemplateRenderer.cs)按旋转尺寸布局；[旋转测试](../tests/Bing.EasyPrint.Tests/TemplateRotationTests.cs)第 64、84、103、366 行覆盖行宽、端点、整块分页及空行 | G2 |
| L3／42 | 原生锚点及旧命令兼容：完成（代码） | [PrintProtocolProviders](../src/Bing.EasyPrint/Printing/PrintProtocolProviders.cs)转换原点；[ProtocolRotationTests](../tests/Bing.EasyPrint.Tests/ProtocolRotationTests.cs)、[TemplateRotationTests](../tests/Bing.EasyPrint.Tests/TemplateRotationTests.cs)第 27 行核对布局与原生命令 | G1、G2 |
| L4／43 | HRI、静区与组合分页：完成（代码） | [BarcodeHumanReadableTests](../tests/Bing.EasyPrint.Tests/BarcodeHumanReadableTests.cs)、[跨页测试](../tests/Bing.EasyPrint.Tests/TemplateCrossPageBarcodeTests.cs)、[旋转测试](../tests/Bing.EasyPrint.Tests/TemplateRotationTests.cs)第 147、346 行 | G2、G3 |
| L5／44 | Native/Raster 文字样式与显式 opt-in：完成（代码） | [Native 样式测试](../tests/Bing.EasyPrint.Tests/EscPosNativeTextStyleTests.cs)、[TSPL/ZPL 位图样式测试](../tests/Bing.EasyPrint.Tests/TsplZplRasterizedTextStyleTests.cs)第 15、36、56、76 行覆盖默认拒绝与显式允许 | G2 |
| L6／45 | 各协议条码扩展：完成（代码限定矩阵） | [能力矩阵](../src/Bing.EasyPrint/PrintCapabilities.cs)、[Provider 映射](../src/Bing.EasyPrint/Printing/PrintProtocolProviders.cs)、[模块测试](../tests/Bing.EasyPrint.Tests/ProtocolBarcodeModuleTests.cs)；不意味着任意制式/协议组合均支持 | G3 |
| L7／46 | Raster 特殊条码主机编码：完成（代码限定矩阵） | [EscPosRasterCanvas](../src/Bing.EasyPrint/Protocols/EscPosRasterCanvas.cs)有 Code11、Telepen、China Post、DataBar 等编码入口；[制式测试](../tests/Bing.EasyPrint.Tests/EscPosRasterBarcodeTypeTests.cs)、[Expanded 测试](../tests/Bing.EasyPrint.Tests/EscPosRasterGs1DataBarExpandedTests.cs) | G3 |
| L8／47 | GS1 AI/FNC1 共享严格解析：完成（代码限定子集） | [Gs1ApplicationIdentifierParser](../src/Bing.EasyPrint/Protocols/Gs1ApplicationIdentifierParser.cs)第 195、277、337 行处理配对、校验与定义；[Scan4Transport 测试](../tests/Bing.EasyPrint.Tests/Gs1Scan4TransportTests.cs)、[容量测试](../tests/Bing.EasyPrint.Tests/Gs1BarcodeCapacityTests.cs) | G3 |
| L9／48 | 字体与原生边界校准接口：完成（代码） | [DeviceFontCalibration](../src/Bing.EasyPrint.Templates/Rendering/DeviceFontCalibration.cs)、[HRI overhang](../src/Bing.EasyPrint/Protocols/NativeBarcodeHumanReadableOverhang.cs)；[校准测试](../tests/Bing.EasyPrint.Tests/TemplateDeviceFontCalibrationTests.cs)第 118、147、173、212、290 行；实际机型校准另保留 | G2 |
| L10／49 | Designer 旋转与 JSON/项目往返：完成（代码），浏览器验收待确认 | [layout.ts](../web/Bing.EasyPrint.Designer/src/layout.ts)、[PropertyInspector](../web/Bing.EasyPrint.Designer/src/components/PropertyInspector.vue)；[imageRotation.spec.ts](../web/Bing.EasyPrint.Designer/e2e/imageRotation.spec.ts)第 39、87、151 行有 E2E 用例，本次与前次分组提交均未执行这些 E2E | G5 |
| R141–R143／57 | 四协议受限 Raw、共享状态和原子性：完成（既定白名单） | [ProtocolRestrictedRawTests](../tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs)第 43、170、314 行；[CPCL 测试](../tests/Bing.EasyPrint.Tests/CPCL/CPCLRestrictedRawTests.cs)第 257、286、317 行 | G1、G4 |
| R144／58 | TSPL 二进制 BITMAP 边界：完成（代码） | [TSPL Raw](../src/Bing.EasyPrint/Protocols/TsplPrintCommand.RestrictedRaw.cs)第 242 行明确拒绝 ASCII HEX Raw；[回归](../tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs)第 485 行断言结构化 API 输出二进制 | G1 |
| R145／59 | ZPL 格式闭合生命周期：完成（代码） | [ZPL Raw](../src/Bing.EasyPrint/Protocols/ZplPrintCommand.RestrictedRaw.cs)检查 ^XA/^XZ；[回归](../tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs)第 189、205 行覆盖未闭合恢复及配对错误原子性 | G1、G4 |
| R146／60 | ESC/POS Raw 游标及失败恢复：完成（代码） | [ESC/POS Raw](../src/Bing.EasyPrint/Protocols/EscPosPrintCommand.RestrictedRaw.cs)；[回归](../tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs)第 329、345 行覆盖对齐不清宽度、Tab 越界即时拒绝 | G1、G4 |
| R147／61 | ZPL 切刀、Kiosk 与分段状态：完成（代码） | [ZPL Raw](../src/Bing.EasyPrint/Protocols/ZplPrintCommand.RestrictedRaw.cs)第 487、501 行校验配对/时序；[ZplCutterTests](../tests/Bing.EasyPrint.Tests/ZplCutterTests.cs)第 52、101、116、148 行 | G1 |
| R148／62 | CPCL 完整 Raw 追加拒绝及重置：完成（代码） | [CPCL Raw](../src/Bing.EasyPrint/CPCL/CPCLPrintCommand.RestrictedRaw.cs)；[回归](../tests/Bing.EasyPrint.Tests/CPCL/CPCLRestrictedRawTests.cs)第 224 行验证拒绝、原子性与 page reset 恢复 | G1、G4 |
| R149／63 | CPCL/TSPL 行分隔拒绝原子性：完成（代码） | [CPCL 测试](../tests/Bing.EasyPrint.Tests/CPCL/CPCLRestrictedRawTests.cs)第 70 行要求生成 PRINT 前 CRLF；[TSPL 测试](../tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs)第 91 行检查跨次命令分隔 | G1 |
| R150／64 | Raw tokenizer 工作集预算：完成（代码） | [RestrictedRawSyntax](../src/Bing.EasyPrint/Protocols/RestrictedRawSyntax.cs)第 26–34 行按输入长度预留；[回归](../tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs)第 13 行验证分配前失败与原子性 | G4 |
| R151／65 | Host 标签 QR allowlist 请求修正：完成（代码） | [HostContractTests](../tests/DesignerHost.Tests/HostContractTests.cs)第 191 行实际发 JSON 并验证两协议 rotation allowlist；包含在已通过的 135 项中 | G5 |
| R152／66 | AI 253/254/255：完成（限定字段） | [解析器](../src/Bing.EasyPrint/Protocols/Gs1ApplicationIdentifierParser.cs)第 175、195、277、337–339 行；[Scan4Transport 测试](../tests/Bing.EasyPrint.Tests/Gs1Scan4TransportTests.cs)第 82–142 行验证长度、校验、配对、FNC1 和协议字节 | G3 |
| R153-1／70 | 反白字背景、字形及四角旋转：完成（代码） | [EscPosRasterReverseTextTests](../tests/Bing.EasyPrint.Tests/EscPosRasterReverseTextTests.cs)第 21、46 行比较真实位图并验证空串不绘背景 | G2 |
| R153-2／71 | TSPL/ZPL 反白显式位图准入：完成（代码） | [反白测试](../tests/Bing.EasyPrint.Tests/EscPosRasterReverseTextTests.cs)第 127 行、[样式测试](../tests/Bing.EasyPrint.Tests/TsplZplRasterizedTextStyleTests.cs)第 76 行核对能力和开关 | G1、G2 |
| R153-3／72 | 反白换行、分页及失败边界回归：完成（代码） | [反白测试](../tests/Bing.EasyPrint.Tests/EscPosRasterReverseTextTests.cs)第 67、82 行断言越界清理、旋转块整块分页 | G2、G4 |
| R153-4／73 | CPCL Helper 映射与拒绝：完成（代码） | [CpclHelperBarcodeTypeTests](../tests/Bing.EasyPrint.Tests/CpclHelperBarcodeTypeTests.cs)第 28、50、58 行分别验证原标记、未映射有效枚举、无效枚举 | G1 |
| R154-1／79 | Composite 独立元素及 Auto/CC-A/CC-B：完成（代码） | [PrintGs1CompositeElement](../src/Bing.EasyPrint/Printing/PrintGs1CompositeElement.cs)、[Gs1CompositeMode](../src/Bing.EasyPrint/Printing/Gs1CompositeMode.cs)；[编码测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeEncoderTests.cs)第 102、227 行 | G3 |
| R154-2／80 | 主 GTIN、Limited 与次 AI 校验：完成（代码） | [Gs1CompositeEncoder](../src/Bing.EasyPrint/Protocols/Gs1CompositeEncoder.cs)规范化并调用共享解析器；[编码测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeEncoderTests.cs)第 41、64、74、90 行 | G3 |
| R154-3／81 | 托管二维组件、链接、分隔和对齐：完成（代码） | [编码器](../src/Bing.EasyPrint/Protocols/Gs1CompositeEncoder.cs)、[固定表](../src/Bing.EasyPrint/Protocols/Gs1CompositeTables.cs)保留来源及许可；运行路径无外部编码进程 | G3 |
| R154-4／82 | 六个独立完整矩阵与容量回归：完成（自动测试） | [固定向量](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeVectors.cs)记录 Zint tag/commit；[编码测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeEncoderTests.cs)第 20 行逐模块比较，第 102、123 行验证容量 | G3、G6 |
| R154-5／83 | 实际尺寸、旋转、静区、预算和取消：完成（代码） | [编码测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeEncoderTests.cs)第 146、188、204、215 行；[Provider 测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeProviderTests.cs)第 81、101 行验证精确边缘及静区 | G2、G4 |
| R154-6／84 | 反白空格静区漏判修复：完成（代码） | [PrintQuietZoneValidator](../src/Bing.EasyPrint/Printing/PrintQuietZoneValidator.cs)选择反白布局边界；[Provider 测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeProviderTests.cs)第 118、137 行覆盖四方向空格黑底与真正空串 | G2 |
| R154-7／85 | 四协议共用位图并拒绝 Native/禁图：完成（代码） | [PrintProtocolProviders](../src/Bing.EasyPrint/Printing/PrintProtocolProviders.cs)的 PrepareComposite；[Provider 测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeProviderTests.cs)第 30、46、63 行比较输出与前置拒绝 | G1、G3 |
| R154-8／86 | Type=11 V2/V3 双字段与原子分页：完成（代码） | [模板元素](../src/Bing.EasyPrint.Templates/Bing/EasyPrint/Elements/BpGs1CompositeElement.cs)、[TemplateRenderer](../src/Bing.EasyPrint.Templates/Rendering/TemplateRenderer.cs)；[模板测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeTemplateTests.cs)第 20、114、125、151 行 | G2、G3 |
| R154-9／87 | 错误归属、静区及双字段往返：完成（代码） | [模板测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeTemplateTests.cs)第 47、58、196 行核对字段、定义/实例索引、JSON、布局和输出 | G2 |
| R154-10／88 | Host Composite 能力、预览及 Render ZIP：完成（代码） | [Gs1CompositeHostTests](../tests/DesignerHost.Tests/Gs1CompositeHostTests.cs)第 28、63、98、121、150 行覆盖四协议、禁用能力、实际尺寸、错误与 ZIP | G5 |
| R154-11／89 | Designer 编辑、协商与包往返：完成（代码），交互验收待确认 | [PropertyInspector](../web/Bing.EasyPrint.Designer/src/components/PropertyInspector.vue)提供双字段和参数编辑；[gs1Composite.test.ts](../web/Bing.EasyPrint.Designer/src/gs1Composite.test.ts)验证模型/ZIP 往返、Host 特性门控和离线待验证占位；未执行 Composite 浏览器 E2E | G5 |

## 多协议计划：18 个未勾选条目与补充范围

以下逐行保留原要求，即使可以合并为同一功能，也不因此将原条目置为完成。状态细分仅用于说明完成条件。

| ID／原位置 | 要求 | 核对结论与缺少的完成依据 | 归类 |
| --- | --- | --- | --- |
| U01／100 | 具体机型 glyph advance、组合字形、字体/墨迹校准 | 未完成／BLOCKED_EXTERNAL；校准 API 和人工数值测试已实现，仍缺型号、固件、手册/打印样本、实测误差记录 | G2 |
| U02／101 | 设备容量约束 | 未完成／BLOCKED_EXTERNAL；引擎预算不等同设备容量，缺目标机型、DPI、固件和介质容量实测 | G1、G4 |
| U03／102 | Host 进程隔离和监听边界 | 未完成／BLOCKED_APPROVAL；[Program.cs](../tools/DesignerHost/Program.cs)第 8 行可配置监听地址，路由在同一 Web 进程执行；请求预算不能证明隔离。缺部署信任边界与隔离验收契约 | G5 |
| U04／103 | 正式性能、吞吐、容量及超时阈值 | 未完成／BLOCKED_APPROVAL；有 [PerformanceProbe](../tools/PerformanceProbe/Program.cs) 不代表批准了负载与指标，缺批准阈值及对应正式结果 | G6 |
| U05／104 | 跨平台运行时/字库验证 | 未完成／BLOCKED_EXTERNAL；本地 Windows 证据不能覆盖 Linux/macOS，缺相应环境执行报告 | G6 |
| U06／105 | HRI 四边、字形墨迹与旋转实测 | 未完成／BLOCKED_EXTERNAL；代码允许角度校准，不提供真实设备打印/扫描证据。与 U01 同类，原要求保留 | G2 |
| U07／124 | CPCL 受限 Raw 的 B RSS 子集 | 延期／DEFERRED，不能标完成；Composite 位图入口不等于 B RSS 语法支持。缺分段/载荷、footprint 和固件契约 | G1、G3 |
| U08／125 | CPCL MaxiCode 及设备字体/条码边界验收 | 未完成／BLOCKED_EXTERNAL；[CPCLRestrictedRawTests](../tests/Bing.EasyPrint.Tests/CPCL/CPCLRestrictedRawTests.cs)第 166 行仅验证保守范围，仍缺固件样本/实测 | G1 |
| U09／126 | 任意 WriteRaw 不参与结构化边界预检 | 接受限制／ACCEPTED_LIMITATION；保留说明。[CPCL Raw](../src/Bing.EasyPrint/CPCL/CPCLPrintCommand.RestrictedRaw.cs)与测试已有 opaque Raw 后重建状态规则，但这不会使任意 Raw 变成可验证的结构化命令 | G1 |
| U10／132 | 热敏/针式打印与扫描验收 | 未完成／BLOCKED_EXTERNAL；[协议实机清单](protocols.md)第 891 行要求型号、固件、介质、传输和扫描结果，本会话未执行 | G1、G6 |
| U11／133 | ESC/P、ESC/P2 或私有协议 Provider | 待确认／条件项；四协议能力不能证明该设备兼容。取得目标设备实际协议和样本后再决定是否实施 | G1 |
| U12／139 | Profile 按机型细化 | 部分完成／DEFERRED；通用能力开关和 allowlist 有测试，缺各机型支持矩阵与组合能力样本。可与 U02/U10 协调但不删除原要求 | G1 |
| U13／140 | GS1/FNC1 业务语义与扫描返回值 | 部分完成／DEFERRED；AI 校验和编码已完成限定子集，缺扫描器返回、业务关系及设备回读证据 | G3 |
| U14／141 | 双向/复杂塑形、组合、TTC、缺字与粗体完整范围 | 部分完成／DEFERRED；固定字体、真实粗体资源及显式替代策略已有代码，不能证明复杂塑形/双向布局和全部字体容器支持 | G2 |
| U15／142 | 日文/厂商代码页和固件差异 | 未完成／DEFERRED；已有编码契约不等于设备字库验收，缺设备字符集、代码页和日文字形输出样本 | G1、G2 |
| U16／143 | 跨平台 CI 与系统字库 | 未完成／BLOCKED_EXTERNAL；与 U05 合并管理，保留“CI”和“默认系统字库”两个验收维度 | G6 |
| U17／144 | 发送中断、重试、重复打印和恢复规范 | 未完成／DEFERRED；[协议说明](protocols.md)第 773、801、804、827、891 行将设备状态和文件顺序交给发送方，未提供已完成的发送方恢复规范 | G1、G4 |
| U18／150 | 当前 .NET 7 测试执行 | 待确认／BLOCKED_EXTERNAL；`dotnet --list-runtimes` 标准安装目录无 .NET 7，当前未执行该 testhost；历史便携运行时记录不能替代本候选验证 | G6 |

补充范围同样不得在清理中丢失：原第 107 行的 CC-C、Expanded/Stacked Composite、其他线性组件为 DEFERRED；第 156 行的注释验收按下表 C1–C7 跟踪；第 113–118 行的协议旋转表达、显式文字位图开关、GS1 子集、TextElements 匹配、第三方预算/取消边界、无内置 Composite HRI 均保留为能力限制。它们不因现有回归通过而自动转成“全部标准支持”。

## 中文注释计划：七批次与明确问题

以下对每批检查了可验证的实现证据和反例。存在一个明确反例即可否定“该批已全部完成”，所以没有为了统计完整缺陷数量重新开发审计器或执行十次严格重建。此处列举的是完成状态依据，不是所有注释问题清单。

| ID／原位置 | 批次 | 当前状态与依据 | 完成前尚需满足 |
| --- | --- | --- | --- |
| C1／28、40 | 接口、抽象类与公共契约 | 部分完成／未达到整批完成。[IPrintComponent](../src/Bing.EasyPrint/IPrintComponent.cs)第 15 行已明确页面单位，第 235 行说明流所有权，第 282–290 行说明文本区域零值和高度行为；但第 242–249 行 DrawText 的 x/y 仅写“起始x/y坐标”，未说明打印点，字体大小单位和旋转约束也未完整阐明。计划第 40 行只证明历史本轮修订范围 | 完成当前所有公共契约的单位、约束、所有权与错误说明核对 |
| C2／29、43 | DTO、Options、枚举 | 未完成。[Gs1CompositeMode](../src/Bing.EasyPrint/Printing/Gs1CompositeMode.cs)第 3–11 行、[BpGs1CompositeElement](../src/Bing.EasyPrint.Templates/Bing/EasyPrint/Elements/BpGs1CompositeElement.cs)使用单行摘要，未达到计划统一三行摘要验收。[MetadataType](../src/Bing.EasyPrint/CPCL/Metadata/MetadataType.cs)第 4–16 行仍为“元数据类型／命令／组件”，原计划明确列为待核对 | 复核当前 DTO/Options/枚举默认值、单位、空值与版本语义；确认 MetadataType 类别文案 |
| C3／30、43 | 构造函数与属性 | 未完成。[RawMetadata](../src/Bing.EasyPrint/CPCL/Metadata/RawMetadata.cs)第 22–33 行两个 raw 参数分别称“原始命令”“命令”，newLine 仅称“是否换行”，与第 42–45 行实际追加换行的行为及单参数默认 false 未形成完整说明；[TemplateRenderOptions](../src/Bing.EasyPrint.Templates/Rendering/TemplateRenderOptions.cs)第 25–39 行有单行摘要及未以访问语义起头的属性 | 明确 raw/换行契约；统一构造句式、属性访问语义和三行摘要 |
| C4／31 | 实现、重写与重载 | 未完成。[RawMetadata.Build](../src/Bing.EasyPrint/CPCL/Metadata/RawMetadata.cs)第 39–40 行已使用 inheritdoc；但当前 [PrintProtocolProviders](../src/Bing.EasyPrint/Printing/PrintProtocolProviders.cs)第 1149 行 ValidateElement override 没有 XML 文档，其基类第 667 行也缺独立文档，不能推定全部实现均有有效继承来源 | 核对上游文档、所有实现/override 和重载一致性，覆盖扩展后的当前项目范围 |
| C5／32 | private/internal/static 方法 | 未完成。[QrMetrics](../src/Bing.EasyPrint.Templates/Rendering/QrMetrics.cs)第 13、16 行两个 internal RasterExtent 重载没有 XML 摘要、参数及非 void 返回说明；该文件属于计划明确列出的范围 | 补足独立方法文档并校验参数、泛型和返回语义 |
| C6／33 | 字段、常量和键 | 未完成。[TemplateLoader](../src/Bing.EasyPrint.Templates/Rendering/TemplateLoader.cs)第 158 行 QuietZoneError 常量缺 XML 文档，普通方法内注释不能替代该声明文档 | 对当前字段/常量/缓存及配置键按生命周期、键组成与容量核对 |
| C7／34、81、85–90 | 最终语法树审计、严格构建与 token 一致性 | 未完成／部分待确认。历史记录明确仅审计 88 文件、858 声明；当前 `rg --files src -g '*.cs'` 得到 171 个文件（未按旧排除规则精算，不能冒充声明审计）。历史工具 `artifacts/comment-audit/CommentAudit.csproj` 当前不存在。计划第 90 行明确未保存实施前完整 token 快照 | 重建可复核的当前范围/语法树及严格 XML 构建证据；历史 token 等价缺口需保留“未独立验证”，不能事后用当前状态伪造初始快照 |

计划第 40–41 行的“批次 1 本轮复核／2–7 本轮未执行”与第 85–90 行的全量历史审计属于不同范围的证据，应分别保留。历史零缺失数字不覆盖后续新增声明；普通五框架构建通过也不能关闭 C7，因为 `common.props` 屏蔽了 CS1591。

计划“已确认的文案与继承问题”的六条具体状态另列于此，避免只核对七批次而遗漏明确问题。

| ID／原位置 | 明确问题 | 当前状态与可验证依据 |
| --- | --- | --- |
| Q1／49 | fluent 接口参数、单位与返回契约 | 部分完成。[IBufferWriter](../src/Bing.EasyPrint/IBufferWriter.cs)第 9–33 行已说明返回当前写入器；[BufferWriter](../src/Bing.EasyPrint/Core/BufferWriter.cs)第 65–69 行实际接受 null 并直接返回，但接口参数未说明此空值行为；坐标单位缺口见 C1 |
| Q2／50 | CPCL 实现及 Build 文档继承 | 指定 CPCL 范围已完成。[CPCLPrintCommand.Component](../src/Bing.EasyPrint/CPCL/CPCLPrintCommand.Component.cs)第 14、22、193 行、[BoxComponent](../src/Bing.EasyPrint/CPCL/Components/BoxComponent.cs)第 34 行和 [RawMetadata.Build](../src/Bing.EasyPrint/CPCL/Metadata/RawMetadata.cs)第 39 行使用 inheritdoc；[MetadataBase](../src/Bing.EasyPrint/CPCL/Metadata/MetadataBase.cs)第 14–18 行提供上游 Build 契约。这不代表 C4 的全部当前实现已完成 |
| Q3／51 | 流所有权、选项与默认编码 | 部分完成，存在明确文案错误。[TemplateRenderResult](../src/Bing.EasyPrint.Templates/Rendering/TemplateRenderResult.cs)第 166–172 行说明由渲染器释放流，[TemplateRenderer](../src/Bing.EasyPrint.Templates/Rendering/TemplateRenderer.cs)第 1937 行使用 using；但 [TemplateRenderOptions](../src/Bing.EasyPrint.Templates/Rendering/TemplateRenderOptions.cs)第 99–105 行称未指定编码时使用 GBK，渲染器第 119–120 行实际对 ZPL 默认使用 UTF-8，其余协议使用 GBK，文档尚不准确 |
| Q4／52 | VCompressedGraphics 方向及 ImageComponent 残缺字 | 部分完成／方向待确认。[ImageComponent](../src/Bing.EasyPrint/CPCL/Components/ImageComponent.cs)第 236–240 行当前像素限制文案完整，未再见原残缺字；[CPCLPrintCommand.Graphics](../src/Bing.EasyPrint/CPCL/CPCLPrintCommand.Graphics.cs)第 120–135 行称纵向压缩并输出 VCG。代码只能证明命令字，尚缺协议手册或设备证据验证方向含义 |
| Q5／53 | Graphics/Text 重复段落、断词与空标签 | 未完成。[CPCLPrintCommand.Graphics](../src/Bing.EasyPrint/CPCL/CPCLPrintCommand.Graphics.cs)第 74–76、92–94、110–112、128–130 行仍重复手册段落，存在“十六进制 数据”“图 形”等断词，压缩方法仍沿用展开图形的描述；[CPCLPrintCommand.Text](../src/Bing.EasyPrint/CPCL/CPCLPrintCommand.Text.cs)也有重复说明。本次两文件窄范围检查未复现空 param/returns，不据此声称全项目空标签清零 |
| Q6／54 | 不臆造协议或业务规则 | 保留约束／待确认。此项是持续适用的核对规则，不能作为一次性功能勾销；Q4 的 VCG 方向、设备参数与固件行为须以协议或设备证据确认。本轮未检索外部协议资料，也未将推断写回源码 |

## 共性能力与大功能分类

以下为已归并的可复用能力目录；协议文档维护 G1–G3，开发文档维护 G4–G5 和注释治理，验证文档维护 G6 的候选证据与原编号索引。

| 大功能 | 可复用能力 | 合并相近内容 | 必须保留的未完成边界 |
| --- | --- | --- | --- |
| G1 多协议输出与设备契约 | Provider、Profile/快照、能力矩阵、Native/Raster 准入、Job/切刀/进纸、独立输出段、受限 Raw 状态事务 | T1/T5/T7、S2/S3/S7、L3、R141–R151 中的协议部分、R153-4、R154-7 | 设备矩阵、容量、Raw 受限子集、真实打印/扫描、发送顺序及恢复 |
| G2 模板布局、字体与图像渲染 | 语义字体、实际尺寸度量、旋转坐标/占位、静区、整块分页、双字段绑定、错误定位、图片重采样 | T2–T4/T6、S1/S4、L1–L5/L9、R153-1–3、R154-5/6/8/9 | 实机字体/HRI 校准、复杂塑形与双向布局、字库环境 |
| G3 条码与 GS1 编码 | 公共制式矩阵、共享 AI/FNC1 校验、容量边界、托管 CC-A/CC-B、固定独立完整矩阵、共用光栅输出 | L6–L8、R152、R154-1–4/7/8；合并重复完成记录，保留来源许可 | 完整 AI 字典/业务关系、扫描回读、CC-C 和其他 Composite 子型 |
| G4 资源与失败恢复 | 输出/存储/工作集预算、分配前预留、临时租约、取消传播、快照所有权、失败原子性、Host 准入 | S3/S5/S6、Raw 原子性和 tokenizer、R153-3、R154-5 | 计数不等于进程堆上限；第三方内部资源、设备容量和发送恢复仍独立 |
| G5 Host 与设计器闭环 | 版本/特性协商、严格请求、稳定诊断、动态工具入口、属性编辑、真实在线尺寸、离线占位、JSON/ZIP 往返 | T5、S8、L10、R151、R154-10/11 | 进程隔离/监听批准、浏览器交互验收、旧 Host 兼容边界 |
| G6 验证与 API 文档治理 | 固定字体/向量、golden-byte、跨 Provider 对比、runtime/consumer 脚本、XML 契约与继承、证据绑定候选 | T8、R154-4、中文注释七批次和分散历史构建记录 | 当前完整运行时/consumer、跨平台 CI、严格注释审计和 token 验收、正式性能阈值 |

合并方式：同一能力按 G1–G6 主章节维护，任务入口保留引用；本报告保存核对时的证据快照，原有协议轮次保留兼容细节。实现证据与设备验收分别记录。

## 确认后实际清理的文件

| 路径 | 实际处理 | 保留或迁入内容 |
| --- | --- | --- |
| `ai_docs/plans/multi-protocol-todo-progress.md` | 保留并精简：173 → 79 行 | 保留 U01–U18、原优先级、延期子型及限制引用；显式保留 T8、L10、R154-11 待确认验收，完成记录迁出 |
| `ai_docs/plans/chinese-comments-plan.md` | 保留并精简：91 → 36 行 | 保留七批次顺序、十三项状态及 token 缺口；Q2 和 Q4 的已完成子项仅作追溯，不再混作未完成要求 |
| [协议文档](protocols.md) | 合并 G1–G3 | 接收协议准入、Job/Raw、布局字体/静区、条码/Composite 稳定契约、来源许可及能力限制；原轮次兼容记录保留 |
| [开发文档](development.md) | 合并 G4–G5 与注释治理 | 接收预算/取消/所有权、Host/设计器闭环、XML 扫描规范与严格构建命令、旧 88 文件/858 声明统计及证据缺口 |
| [验证记录](multi-protocol-verification.md) | 合并 G6 与原编号索引 | 51 条能力逐一指定主维护位置；接收候选身份、命令/结果、独立向量与设备验收缺口，分开本会话结果和 R154 历史记录 |
| `docs/plan-audit-2026-10-15.md` | 保留本报告并更新清理结果 | 保存逐项依据、分类、迁移清单及实际验证结果，不新增 plans 任务文件 |

**整文件归档：0；整文件删除：0。** 两份计划均含未完成或待确认项，按已确认方案保留文件，只清除已迁出的重复完成记录。任务文件仍为 2 个，合计 264 → 115 行，减少 149 行（约 56%）；未额外建立归档任务副本。

## 清理后的验证结果

- **完成记录无遗漏**：验证文档 G1–G6 索引包含原 51 个能力编号，50 个确认代码实现和 T8 部分完成均有去向；详细实现/断言继续保存在本报告。交叉功能以主维护位置引用，不重复生成任务。
- **未完成要求保留**：U01–U18 共 18 条均在原打印计划，前 17 条原要求正文逐字保留；U18 仅把“没有运行时”限定为已实际检查的“标准安装目录”，保留编译完成、测试待确认与历史证据不替代当前候选的边界。T8、L10 和 R154-11 的缺失验收显式列出；CC-C、Expanded/Stacked Composite 等后续范围保留。
- **注释状态无误清理**：当前计划仍含 C1–C7 及 Q1–Q6，单位、空值、MetadataType、RawMetadata、默认编码、继承、方法/常量文档、Graphics/Text、VCG 和严格 token 验收缺口均保留。Q2 的限定完成范围不关闭 C4，Q4 的文案子项完成不关闭方向待确认。
- **文件与内容范围**：仅两份计划及四份 docs 文档发生变化；没有删除或归档文件，未改变源码、测试、工具或前端。暂存区未修改，本次整理未创建提交。
- **文档验证**：六份文件均通过严格 UTF-8 解码、行尾空白检查；238 个本地 Markdown 目标及使用的标题锚点检查无缺失，`git diff --check` 通过。业务构建/测试沿用上方已执行证据，本轮文档清理未重跑。

此次验收结论是文档迁移和任务保留完整；两份计划的功能/设备/注释验收状态不会因为清理文档而自动改变。
