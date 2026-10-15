# 多协议打印与旋转任务总览

整理日期：2026-10-14；实现与测试记录截止 R154（2026-10-13）。

本文件合并原多协议与旋转进度文档，作为该计划唯一任务入口。按当前工作区的实现及回归用例判断完成状态，包含尚未提交的实现；“已实现”不代表已发布或完成设备验收。删除重复轮次、过时待办及被后续能力取代的限制，保留有效未完成要求的原文和优先级。独立的 [中文 XML 注释计划](chinese-comments-plan.md) 完整保留，不纳入打印功能完成计数。

## 目标与范围

沿用既有多协议打印方案：先完善反白、CPCL Helper 兼容与旋转/HRI/静区/重复组件/换行分页组合，再实现 Omnidirectional/Limited × CC-A/CC-B，最后贯通公共元素、四协议、模板、Host 与设计器。以托管编码、独立完整矩阵、单元与必要集成测试闭环为代码完成标准。真实设备校准与验收暂缓；Host 隔离/监听、正式性能阈值和跨平台环境建设不在本次实现范围。

## 已完成任务及实现依据

### 原有公共能力（T1–T8、S1–S8）

以下均已实现；历史本地门禁是对应当时源码的记录，设备样本与当前发布验证仍独立判断。

| 原条目 | 已实现能力 | 代码或测试依据（仓库相对路径） |
| --- | --- | --- |
| T1 | ESC/POS Profile 贯通命令、Provider、模板、Host、设计器与保存 | `src/Bing.EasyPrint/Protocols/EscPosPrinterProfile.cs`；`src/Bing.EasyPrint/Printing/PrintProtocolProviders.cs` |
| T2 | 重采样高度、图片参数及追加前校验 | `src/Bing.EasyPrint/Printing/PrintImageRasterizer.cs`；`tests/Bing.EasyPrint.Tests/PrintImageRotationTests.cs` |
| T3 | 语义字体与旧 CPCL 字体参数适配 | `src/Bing.EasyPrint/PrintFont.cs`；`src/Bing.EasyPrint.Templates/Rendering/TemplateFontResolver.cs`；`tests/Bing.EasyPrint.Tests/TemplateSemanticFontTests.cs` |
| T4 | 未知字体样式静态预检 | `src/Bing.EasyPrint.Templates/Rendering/TemplateProtocolCapabilities.cs`；`tests/Bing.EasyPrint.Tests/TemplateFontStylePreflightTests.cs` |
| T5 | 细粒度能力、Host 与设计器过滤 | `src/Bing.EasyPrint/PrintCapabilities.cs`；`tools/DesignerHost/DesignerRequestProcessor.cs`；`web/Bing.EasyPrint.Designer/src/capabilities.ts` |
| T6 | 重复实例、组件定义/实例与诊断归属 | `src/Bing.EasyPrint.Templates/Rendering/TemplateBindingDiagnostic.cs`；`tests/Bing.EasyPrint.Tests/TemplateBindingDiagnosticTests.cs` |
| T7 | CPCL/Profile 横纵 DPI 契约 | `tests/Bing.EasyPrint.Tests/ProtocolProfileCapabilitiesTests.cs`；`tests/Bing.EasyPrint.Tests/CpclTemplateParityTests.cs` |
| T8 | 固定字体、本地运行时与隔离包消费者门禁 | `tests/Bing.EasyPrint.Tests/FixedTestFonts.cs`；`tools/Verify-ProtocolRuntimes.ps1`；[历史验证报告](../../docs/multi-protocol-verification.md) |
| S1 | 原生文字、主机固定字体及设备字体校准接口 | `src/Bing.EasyPrint/Protocols/RasterFontCatalog.cs`；`src/Bing.EasyPrint.Templates/Rendering/DeviceFontCalibration.cs` |
| S2 | 标签 Profile、切刀契约与独立输出文件 | `src/Bing.EasyPrint/Protocols/TsplCutterProfile.cs`；`src/Bing.EasyPrint/Protocols/ZplCutterProfile.cs` |
| S3 | Job 份数顺序、页/份/任务进纸裁切与输出预算 | `src/Bing.EasyPrint/Printing/PrintJobRenderer.cs`；`tests/Bing.EasyPrint.Tests/PrintJobTests.cs` |
| S4 | 统一静区、页面与相邻检查、回滚及保存 | `src/Bing.EasyPrint/Printing/PrintQuietZoneValidator.cs`；`tests/Bing.EasyPrint.Tests/ProtocolQuietZoneTests.cs` |
| S5 | 引擎自有存储预算、取消恢复、Host 请求预算与准入 | `src/Bing.EasyPrint/Core/PrintStorageBudget.cs`；`tests/DesignerHost.Tests/HostAdmissionTests.cs`；`tests/DesignerHost.Tests/HostRequestBudgetTests.cs` |
| S6 | 元素归属副本、图片数组与资源快照 | `src/Bing.EasyPrint/Printing/PrintDocument.cs`；`tests/Bing.EasyPrint.Tests/TemplateProviderOwnershipTests.cs` |
| S7 | 显式 Native 混合收据、游标、占位及模板接入 | `tests/Bing.EasyPrint.Tests/EscPosNativeFlowTests.cs`；`tests/Bing.EasyPrint.Tests/TemplateEscPosNativeParameterCombinationTests.cs` |
| S8 | Host 特性协商、严格请求、稳定错误与绑定诊断 | `tools/DesignerHost/DesignerContract.cs`；`tools/DesignerHost/DesignerRequestProcessor.cs`；`web/Bing.EasyPrint.Designer/src/api.ts` |

### 旋转、HRI、条码、布局与校准（R1–R140）

| 能力 | 当前实现与边界 | 代码或测试依据（仓库相对路径） |
| --- | --- | --- |
| 图片旋转与填充矩形 | 图片四角度共用位图路径；TSPL/ZPL 原生填充及公开实心工厂 | `src/Bing.EasyPrint/Printing/PrintImageRotation.cs`；`tests/Bing.EasyPrint.Tests/TemplateFilledRectangleTests.cs` |
| 旋转布局链路 | 文字 Width 为旋转前行宽；旋转后占位参与对齐、结束坐标和分页；旋转文字块不可拆页，空行保留间隔、不输出空字形命令 | `src/Bing.EasyPrint.Templates/Rendering/TemplateRenderer.cs`；`tests/Bing.EasyPrint.Tests/TemplateRotationTests.cs` |
| 原生锚点与兼容 | TSPL 把占位左上角换算为旋转锚点；ZPL 原生方向映射；旧 CPCL/TSPL 命令保留原有角度与字节语义 | `src/Bing.EasyPrint/Printing/PrintProtocolProviders.cs`；`tests/Bing.EasyPrint.Tests/ProtocolRotationTests.cs` |
| HRI、显式静区及组合分页 | 原生协议/主机位图各自按能力输出；静区、HRI 预留与角度占位联合检查，重复组件、换行、分页已有组合回归 | `tests/Bing.EasyPrint.Tests/BarcodeHumanReadableTests.cs`；`tests/Bing.EasyPrint.Tests/TemplateCrossPageBarcodeTests.cs`；`tests/Bing.EasyPrint.Tests/TemplateQuietZoneTests.cs` |
| 文字样式 | Native 顺序文字支持粗体/单线下划线/反白；Raster 支持四角度样式；TSPL/ZPL 显式位图开关贯通模板与设计器 | `tests/Bing.EasyPrint.Tests/EscPosNativeTextStyleTests.cs`；`tests/Bing.EasyPrint.Tests/TsplZplRasterizedTextStyleTests.cs`；下节 R153 |
| 协议条码扩展 | 零售码、Code39/93、I2OF5 校验变体、MSI、GS1-128、邮政码等按各协议能力映射；未支持组合明确拒绝 | `src/Bing.EasyPrint/PrintCapabilities.cs`；`src/Bing.EasyPrint/Printing/PrintProtocolProviders.cs`；`tests/Bing.EasyPrint.Tests/ProtocolBarcodeModuleTests.cs`；[协议说明](../../docs/protocols.md) |
| Raster 特殊制式 | PLANET、FIM A–E、IMb、Code11、Plessey、ITF14、China Post、Telepen/Telepenn、Code49 与 DataBar Omnidirectional/Truncated/Limited/Expanded/Stacked 已有主机编码 | `src/Bing.EasyPrint/Protocols/EscPosRasterCanvas.cs`；`tests/Bing.EasyPrint.Tests/EscPosRasterBarcodeTypeTests.cs`；`tests/Bing.EasyPrint.Tests/EscPosRasterGs1DataBarExpandedTests.cs` |
| GS1 AI 与 FNC1 | 共享严格解析、长度/字符集/校验位、日期及变量字段 FNC1；常用压缩 AI、Scan4Transport、公司内部 AI 与 253/254/255 子集已接入，未知 AI 拒绝 | `src/Bing.EasyPrint/Protocols/Gs1ApplicationIdentifierParser.cs`；`tests/Bing.EasyPrint.Tests/Gs1Scan4TransportTests.cs`；`tests/Bing.EasyPrint.Tests/Gs1BarcodeCapacityTests.cs` |
| 设备字体与原生边界接口 | direct/template 页面边界、逐字体文本单元 advance/墨迹、HRI 四边及逐角度覆盖均已实现；数值由调用方实测提供 | `src/Bing.EasyPrint.Templates/Rendering/DeviceFontCalibration.cs`；`src/Bing.EasyPrint/Protocols/NativeBarcodeHumanReadableOverhang.cs`；`tests/Bing.EasyPrint.Tests/TemplateDeviceFontCalibrationTests.cs` |
| Designer 旋转与往返 | 角度编辑、预览、旋转占位回写、JSON/项目包保存；离线结束坐标框不重复旋转 | `web/Bing.EasyPrint.Designer/src/layout.ts`；`web/Bing.EasyPrint.Designer/src/components/PropertyInspector.vue`；`web/Bing.EasyPrint.Designer/e2e/imageRotation.spec.ts` |

R87 曾对当时源码完成五运行时各 2705/2705 及六个当前包消费者验证，关闭早期旧 TFM CopyLocal 工具链阻塞。该历史结果不能替代 R154 当前代码的缺失运行时验证。旋转旧文档的“语义字体/CPCL 适配”和“组件归属”待办分别由 T3、T6 实现及以上测试关闭；其 Raster/二维码/图片非零旋转拒绝快照不再作为当前限制。

### 受限 Raw 与剩余代码收口（R141–R152）

| 轮次 | 已实现行为 | 依据（仓库相对路径） |
| --- | --- | --- |
| R141–R143 | 四协议受限 Raw 几何、状态共享及拒绝原子性 | `tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs`；`tests/Bing.EasyPrint.Tests/CPCL/CPCLRestrictedRawTests.cs` |
| R144 | TSPL BITMAP 二进制载荷、受限字符串及结构化 HEX 契约 | `src/Bing.EasyPrint/Protocols/TsplPrintCommand.RestrictedRaw.cs` |
| R145 | ZPL 格式闭合与结构化续写生命周期 | `src/Bing.EasyPrint/Protocols/ZplPrintCommand.RestrictedRaw.cs` |
| R146 | ESC/POS Raw 行游标几何与失败恢复 | `src/Bing.EasyPrint/Protocols/EscPosPrintCommand.RestrictedRaw.cs` |
| R147 | ZPL 裁切、Kiosk 配对与输出分段状态 | `src/Bing.EasyPrint/Protocols/ZplPrintCommand.RestrictedRaw.cs` |
| R148 | CPCL 完整 Raw 追加拒绝、即时失败与重置恢复 | `src/Bing.EasyPrint/CPCL/CPCLPrintCommand.RestrictedRaw.cs` |
| R149 | CPCL/TSPL 行分隔与拒绝原子性 | `tests/Bing.EasyPrint.Tests/ProtocolRestrictedRawTests.cs` |
| R150 | 四协议 tokenizer 临时预算与失败原子性 | `src/Bing.EasyPrint/Protocols/RestrictedRawSyntax.cs`；`tests/Bing.EasyPrint.Tests/PrintWorkingBudgetTests.cs` |
| R151 | Host 标签 QR allowlist 测试请求契约修正 | `tests/DesignerHost.Tests/HostContractTests.cs` |
| R152 | GS1 AI 253/254/255 键校验、配对、变量 FNC1、跨 Provider 编码与 HRI | `src/Bing.EasyPrint/Protocols/Gs1ApplicationIdentifierParser.cs`；`tests/Bing.EasyPrint.Tests/Gs1Scan4TransportTests.cs` |

### 反白文字与 CPCL Helper（R153，已实现）

- [x] ESC/POS Raster 新增反白文字重载：以实际文字布局外框绘制黑底、白字；背景、粗体和下划线整体支持 0/90/180/270 度旋转；空字符串不绘制背景。
- [x] TSPL/ZPL 仅在 `AllowRasterizedTextStyles` 开启且图片能力可用时光栅化反白；默认仍使用设备内置字体。Provider、模板度量、静态预检、Host 能力响应及设计器选项同步该能力。
- [x] 更新旧 ESC/POS Raster 模板反白测试，覆盖真实位图输出；新增反白换行、旋转整块分页、标签协议 opt-in 及失败边界回归。
- [x] CPCL Helper 保持既有支持类型的命令标记；有效但未映射的枚举统一抛出 `NotSupportedException`，无效枚举抛出参数异常。

实现与测试入口：`src/Bing.EasyPrint/Protocols/EscPosRasterCanvas.cs`、`src/Bing.EasyPrint/CPCL/CPCLPrintCommand.Barcode.cs`、`tests/Bing.EasyPrint.Tests/EscPosRasterReverseTextTests.cs`、`tests/Bing.EasyPrint.Tests/TsplZplRasterizedTextStyleTests.cs`、`tests/Bing.EasyPrint.Tests/CpclHelperBarcodeTypeTests.cs`。

### GS1 Composite（R154，已实现）

- [x] 新增独立 `PrintGs1CompositeElement` 和 `Gs1CompositeMode`；默认 Auto，先 CC-A 后 CC-B，显式模式超限报错；保留普通 BarcodeType 数值和构造函数。
- [x] 13 位主数据、有效 14 位 GTIN、单个 AI 01 校验；Limited 范围校验；次数据复用共享严格括号 AI 解析器，保留长度、字符集、未知 AI 拒绝和 FNC1 规则。
- [x] 托管 CC-A/CC-B 压缩、纠错、码字、行指示、链接型 DataBar、分隔图案和组件对齐；不依赖外部可执行文件。移植表与编码依据固定 Zint 2.15.0（commit `4896136bdbebb1a0908a5cc419513efa5bf4bf70`），保留来源及许可。
- [x] 四种组合使用六个独立完整模块矩阵逐模块比对，覆盖日期、数字、字母、变量字段 FNC1 和较大 CC-B；补充输入、容量边界及自动模式测试。
- [x] 真实编码尺寸、四角度旋转、非对称静区、精确贴边和静区冲突；编码工作集、模块矩阵、图片快照和输出复用现有预算/取消链路，失败后可重试；不内置 HRI。
- [x] 修复组合回归揭示的反白文字静区漏判：ESC/POS Raster 非空空格的黑底按实际反白布局占位校验，四角度冲突均拒绝；真正空字符串仍不占位。此修复复用 R153 的既有测量实现。
- [x] CPCL/TSPL/ZPL/ESC/POS Raster 共用 Composite 位图及原有图片输出路径；Native 与禁图 Profile 在解析和编码前拒绝。
- [x] `BpGs1CompositeElement` Type=11 支持 V2/V3；继承 Field 绑定主数据，SecondaryField 绑定次数据；V1 明确要求升级。整个符号作为不可拆分块参与布局与分页，整页超限失败。
- [x] 模板主/次字段错误保留字段路径、组件定义/实例及重复索引；JSON 显式静区、旋转与双字段往返、布局和输出一致性通过回归。
- [x] Host 新增 `gs1-composite` 特性和复合码描述；能力查询、真实预览尺寸、错误归属及四协议 Render ZIP 集成测试通过。
- [x] 设计器支持新增、双字段编辑、线性制式、模式、模块宽度、条高、旋转、静区和 JSON/项目 ZIP 往返；新增入口要求 Host 特性及能力已确认，旧 Host/查询未完成时禁用；离线预览显示占位与待验证，在线尺寸沿用 Host 编码结果。

实现与测试入口：`src/Bing.EasyPrint/Printing/PrintGs1CompositeElement.cs`、`Gs1CompositeMode.cs`；`src/Bing.EasyPrint/Protocols/Gs1CompositeEncoder.cs`、`Gs1CompositeTables.cs`；`src/Bing.EasyPrint.Templates/Bing/EasyPrint/Elements/BpGs1CompositeElement.cs`；`src/Bing.EasyPrint.Templates/Rendering/TemplateRenderer.cs`、`TemplateLoader.cs`；`tools/DesignerHost/DesignerContract.cs`、`DesignerRequestProcessor.cs`；`web/Bing.EasyPrint.Designer/src/model.ts`、`api.ts`、`capabilities.ts`、`layout.ts`、`tree.ts`、`components/PropertyInspector.vue`、`components/CanvasWorkspace.vue`。模板及模型 XML 文档同步该契约。

独立向量与回归：`tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeVectors.cs`、`Gs1CompositeEncoderTests.cs`、`Gs1CompositeProviderTests.cs`、`Gs1CompositeTemplateTests.cs`；`tests/DesignerHost.Tests/Gs1CompositeHostTests.cs`；`web/Bing.EasyPrint.Designer/src/gs1Composite.test.ts`、`model.test.ts`、`archive.test.ts`。


## 未完成任务

### 未完成 TODO（保留原优先级编号）

2. [ ] 具体 TSPL/ZPL glyph advance、组合字形、字体选择和墨迹校准。`BLOCKED_EXTERNAL / DEFERRED_DEVICE`；依赖 R139 校准 API 及目标设备/样本；缺少设备，本轮按用户要求暂缓。
3. [ ] 设备容量约束。`BLOCKED_EXTERNAL / DEFERRED_DEVICE`；依赖目标机型、DPI、固件和介质；真实边界与容量未测，本轮暂缓。
4. [ ] DesignerHost 进程隔离和监听边界。`BLOCKED_APPROVAL / OUT_OF_SCOPE`；依赖部署信任边界、监听地址及隔离验收契约；用户已明确排除本次实施范围。
5. [ ] 正式性能、吞吐、容量和超时门槛。`BLOCKED_APPROVAL / OUT_OF_SCOPE`；依赖批准的负载及阈值；本轮不建立性能基线或额外资源矩阵。
6. [ ] 跨平台运行时/字库验证。`BLOCKED_EXTERNAL / OUT_OF_SCOPE`；依赖对应 OS、运行时、字体环境；尚无环境执行证据，环境建设不在本次范围。
7. [ ] 具体机型 HRI 四边、字形墨迹和旋转实测。`BLOCKED_EXTERNAL / DEFERRED_DEVICE`；依赖 R138 角度边界配置与设备样本；本轮暂缓。

CC-C、Expanded/Stacked Composite、其他线性组件属于已确认计划的后续范围，不作为本轮缺陷或新增 TODO 插队。当前代码范围没有已确认的 `OPEN_ACTIONABLE` 缺口；下一轮保持第 2 项优先，但仅在设备/样本就绪后执行。若仍无设备，等待新批准的代码 TODO，不扩展已完成 Composite 的范围。

## 保留的范围与验收边界

### 协议表达与预算边界

- 旋转不是所有协议/元素的通用原生承诺：CPCL 统一条码仅支持 0/270 度，90/180 度明确拒绝；ESC/POS Native 只接受可表达的顺序布局，不支持 Composite/坐标位图。能力查询与静态预检是实际准入依据。
- TSPL/ZPL 默认原生字体策略不变；粗体/下划线/反白需要显式 `AllowRasterizedTextStyles`，且 Profile 必须允许图片，不静默降级。普通条码的 HRI 和校验语义按各制式保持；Composite 无内置 HRI。
- 已实现 AI 子集不等于完整 GS1 字典或所有业务关联规则；独立矩阵与字节回归不证明真实扫描结果。固定 Zint 2.15.0（commit `4896136bdbebb1a0908a5cc419513efa5bf4bf70`）的来源与许可保留在编码表和独立向量文件中。
- `TextElements` 使用运行时 `StringInfo` 文本单元精确匹配，不做 Unicode 规范化；数据表达的是主机布局校准，不代表库已验证任一机型的实际字形或墨迹。

第三方同步操作只能在调用边界检查取消，内部对象与缓冲未完全计入预算；当前计数不等于实际进程堆上限。固定字体不混合逐字替代、不合成粗体，不证明复杂塑形/双向布局；入口/归档容量不同，超限严格拒绝。静区和声明占位不证明固件实际尺寸或可扫描性。发送中断和 raw 绕过后的设备状态由发送方管理。

### 受限 Raw 与可信 Raw 限制（保留原文）

以下为有效的兼容/验收边界，不因 Type=11 Composite 或受限 Raw 本地校验完成而关闭，也不插入当前优先队列。

- [ ] CPCL `B RSS` 复合命令明确不属于 R141 受限 Raw 子集；其分段/载荷边界和 footprint 需要更精确的固件语法与 GS1 Composite 输入契约。
- [ ] CPCL MaxiCode 与设备字体/条码边界采用计划要求的保守 footprint；固件差异可能造成拒绝或设备端表现差异，仍需具体机型验收。
- [ ] `WriteRaw`/任意协议注入不参与结构化页面边界预检；模板 Provider 继续由模板层的布局、Profile 和声明占位检查负责。

R143 已实现可信 `WriteRaw` 使几何状态失效；后续受限绘制须通过结构化或受限 Raw 的显式状态命令重建。依据：`src/Bing.EasyPrint/CPCL/CPCLPrintCommand.RestrictedRaw.cs`；可信 `B RSS` 入口仍见 `src/Bing.EasyPrint/CPCL/CPCLPrintCommand.Barcode.cs`。现有可信 Raw 兼容入口没有被受限白名单替换。

### 外部验收要求（保留原文）

- [ ] 真实热敏/针式机验收：型号、固件、实际协议、代码页、DPI、纸宽、字距、打印与扫描结果、进纸和裁切；按 `docs/protocols.md` 实机清单执行。
- [ ] 条件项：针式设备若是 ESC/P、ESC/P2 或私有协议，取得手册/样本后另建 Provider；不能从 ESC/POS Profile 推断兼容。

### 历史延后要求（保留原文）

以下不插入当前六项优先队列，也不因整理文档启动开发；其中已有子能力不代表整个验收目标完成。原要求完整保留，补充实现状态供后续决定。

- [ ] Profile 细化：按机型补充可用条码、HRI、文字样式、二维码方向等组合能力；依赖 R2–R17 的语义矩阵。
- [ ] GS1/FNC1 业务语义：确定输入表达、校验、编码和扫描器返回值，不能只验证打印字节。
- [ ] 复杂文字与字体范围：双向文字、塑形、组合字符、TTC、缺字替代和真实粗体策略。
- [ ] 日文及厂商代码页：补充设备字符集、代码页、设备字体和固件差异样本。
- [ ] 跨平台 CI 与默认系统字库：补充 Linux/macOS 运行时、字体和输出差异验证。
- [ ] 发送方恢复规范：明确分段顺序、中断、重试重复打印和设备状态恢复；不扩大本组件的连接管理范围。

状态核对：Profile 的机型能力契约、GS1 输入/编码、固定字体/真实粗体资源及字体替代已有代码；目标设备能力样本、扫描器返回、复杂塑形/双向布局、日文/厂商样本、Linux/macOS 运行时/字库和发送方恢复规范不能据此视为完成。它们仍需外部资料或后续明确范围。

### 当前本地运行时缺口

- [ ] .NET 7 测试执行：当前机器没有对应运行时，已完成该目标的编译。

该项沿用旋转文档的外部阻塞原文。2026-10-14 实际执行 `dotnet --list-runtimes`，标准安装路径中确无 .NET 7；R87 的历史五运行时证据不能关闭当前工作区的执行缺口。

### 尚未确认的注释验收

`chinese-comments-plan.md` 原文不变：批次 1 已复核，批次 2–7 本轮未重新审计；`RawMetadata` 构造参数与 `MetadataType` 类别文案仍是待核对项。历史注释审计不证明当前新增声明已全部覆盖；严格的实施前后非注释 token 相等验收仍未独立验证。不可将整个注释计划标为完成。

## 验证记录与本次整理

### R154 既有验证记录（2026-10-13，非本次重跑）

- Composite 定向 Core/Templates：`net6.0` 102/102；加入静区/反白组合后的相关精准回归 186/186，旧 DataBar 定向兼容测试亦通过。
- Core/Templates 完整测试：`net6.0` 3360/3360；`netcoreapp3.1` 3360/3360。
- Core/Templates 五 TFM 构建：`net7.0`、`net6.0`、`netcoreapp3.1`、`netstandard2.0`、`netstandard2.1`，均 0 警告、0 错误；未宣称缺失 .NET 7 运行时下的测试通过。
- Host `net8.0`：Composite 定向 13/13，完整 135/135。沿用命令行 `EnableDefaultContentItems=false` 避开既有 artifacts 内容递归复制，未修改项目/CI 配置。
- 前端 Vitest 完整 267/267；类型检查与生产构建通过。构建仍提示既有主块超过 500 kB，未为此引入拆包工作。
- 按本轮“仅必要正确性检查”要求，不执行浏览器 E2E 或额外视觉/界面检查；真实设备验收未执行。
- 未新增依赖、工具链、workflow、job、matrix 或基线；未修改版本、提交或发布。测试期间新建的两个临时 Host obj 目录已清理，可由构建重新生成。
- 遵循 GOAL_RULES：只有具体可修问题（Type 11 静区 JSON 校验、组合测试中的反白空格黑底静区漏判）触发修复；设备、环境及未批准阈值不触发重复 Fix。

本次仅整合任务文档：合并 `print-rotation-progress.md` 的有效契约、完成依据与设备/运行时缺口后删除该重复入口；压缩多协议账本的重复历史快照；中文注释计划保持原文。检查文档路径、有效待办原文保留与差异空白，不重复业务构建、测试或界面检查。未修改生产代码、测试、CI、依赖、版本或 Git 提交状态。

历史细节与实机清单继续参阅 [多协议验证记录](../../docs/multi-protocol-verification.md)、[协议说明](../../docs/protocols.md) 和 [开发与验证](../../docs/development.md)。这些历史门禁不是当前源码自动通过的声明。
