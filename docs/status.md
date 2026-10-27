# 待实现与待确认

2026-10-25 O4 已建立新协议注册、模板适配、Host 目录和设计器 v2 接入，独立示例为 `demo.protocol`。它不是新增内置设备协议，也不改变 `PrintProtocol` 枚举；示例支持条码/QR 元素类型 `3/4` 的软件位图，不支持整页 PNG、图片或 GS1 QR，未知协议和旧 Host 会保留配置并禁用对应输出。当前阶段报告见[第二阶段 O4 实施报告](optimization-expansion-phase2-2026-10-25.md)，接入步骤见[新打印协议扩展指南](protocol-extension-guide.md)。

DataBar Truncated 与 Stacked Omnidirectional 已接入 Composite Auto/CC-A/CC-B；新增 `BarcodeType.Gs1DataBarStackedOmnidirectional=32` 支持普通 ESC/POS Raster。Truncated Composite 条高至少 13X；Stacked Omnidirectional 的条高为每条数据行高度、至少 33X，另有三个内部分隔行。完整输入、尺寸与旧 Host 协商见[使用说明](gs1-composite-databar-completion.md)。 本轮验证见[阶段报告](databar-completion-verification-2026-10-22.md)，旧报告保留其日期及原结果。

EAN-8/EAN-13/UPC-A/UPC-E Composite 已接入四种图片协议、V2/V3 模板和设计器，支持 Auto/CC-A/CC-B 及严格 2/5 位附加码；三条分隔行各为 2X，主码校验规则保持原样。输入、尺寸和案例见[EAN/UPC Composite 使用说明](gs1-composite-ean-upc.md)及[附加码使用说明](gs1-composite-ean-upc-addons.md)。基础阶段与附加码阶段报告分别见[基础报告](ean-upc-composite-verification-2026-10-22.md)和[附加码报告](ean-upc-addons-verification-2026-10-22.md)。

ExpandedStacked 已支持普通 ESC/POS Raster 条码及四协议 Composite CC-A/CC-B；省略或 `null` 默认每行两个字符对，普通条码可配置 1–11 列，Composite 可配置 2–11 列。也可按 2–11 条数据行上限布局；行数与列数互斥，分隔行不计入上限。显式配置需要 `gs1-databar-expanded-stacked-columns` / `gs1-databar-expanded-stacked-rows` 和 Host 声明的范围。完整契约与示例见 [列数配置指南](expanded-stacked-columns.md) 及[最大行数与 GS1 AI 字典](expanded-stacked-rows-and-gs1-ai.md)，基础编码范围见[ExpandedStacked 使用说明](gs1-composite-expanded-stacked.md)。

GS1 括号 AI 输入已切换到固定版本 GS1 Barcode Syntax Dictionary（提交 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6`）的完整 536 个已定义 AI，逐字段校验长度、字符集、固定/变长、分隔符和字典 Linter。FNC1 已按官方 `*` 语义修正：没有 `*` 的固定长度 AI 后跟元素时也插入 FNC1。具体码制容量、跨 AI 业务关联和实机扫描语义仍由编码器或调用方负责，见[AI 字典边界](expanded-stacked-rows-and-gs1-ai.md)及[官方对照与预览导出](fnc1-preview-png.md)。

设计器已接入真实软件码图预览和 PNG 下载：Host 新增 `/api/designer/element-png`、`/api/designer/page-png` 及 `raster-element-png` / `raster-page-png` 特性；ESC/POS Raster 支持单页 PNG 和多页 PNG ZIP，其他协议只支持可准确编码的边框、QR、矩阵、Composite、图片和形状，不宣称原生文字、Native HRI 或 1D 宽条/旋转。元素 PNG 按布局/primitive ownership 取齐全部绘制指令，raster 快照、RGBA、PNG 和 ZIP 计入资源预算。设备字体/固件专属完整页明确不可用，详见[预览与 PNG 指南](fnc1-preview-png.md)。

固定字典的 `req`/`ex` 已提供显式核心校验入口，并接入 Composite 主次合并、EAN/UPC/DataBar 隐式 `(01)`、模板/Host 严格开关和设计器选项。现有 QR/DataMatrix 元素新增默认关闭的 `Gs1` 模式，使用共享 GS1 编码生成 GS1 QR（`]Q3`）和 GS1 DataMatrix（`]d2`），通过 CPCL、ESC/POS Raster、TSPL、ZPL 图片路径输出；Native 明确拒绝。普通模式、旧 Host 和默认行为保持兼容，详见[GS1 关联与二维条码](gs1-associations-2d.md)。

当前 GS1-128 Composite 支持显式 CC-A/CC-B，Auto 仍选择 CC-C；旧 Host 缺少 `gs1-composite-gs1-128-ab` 时仍可使用已声明的 Auto/CC-C。接口和协商细节见[GS1-128 A/B 使用说明](gs1-composite-gs1-128-ab.md)。下文各历史阶段的计数保留原日期与范围。

2026-10-22 单行 Expanded Composite 的[实施计划](../ai_docs/plans/gs1-composite-expanded-2026-10-22.md)和[阶段报告](gs1-composite-expanded-verification-2026-10-22.md)保留其当时范围；本轮还修正 Expanded 历史编码问题并接入 DataBar Stacked + CC-A/CC-B Composite，当前软件能力和验证范围分别见[Expanded 使用说明](gs1-composite-expanded.md)、[Stacked 使用说明](gs1-composite-stacked.md)及[Stacked 阶段报告](gs1-stacked-verification-2026-10-22.md)。此前的自动化计数仅属于 Expanded 阶段，不代表本轮 Stacked 或目标设备扫描验收。ExpandedStacked 已接入普通 Raster 与 Composite，见[使用说明](gs1-composite-expanded-stacked.md)；EAN/UPC Composite 已接入，见[使用说明](gs1-composite-ean-upc.md)。

本页只记录代码和现有验证资料尚不能证明完成的事项。历史轮次中的 TODO 以当前代码重新核对为准。

## 待实现

### 后续软件能力

EAN/UPC 的四种基本 Composite 组合及 2/5 位附加码、DataBar Composite、ExpandedStacked 自定义列数/最大行数、固定版本完整 GS1 AI 字典、真实软件码图预览和元素/ESC/POS Raster 页面 PNG 已接入。后续独立工作包括复杂字体排版和更多设备原生像素能力，仍为 `DEFERRED`。目标打印机兼容性和扫码结果需要实际设备验收；软件向量及自动化测试不替代实机结果。

### 打印传输与设备管理

项目当前只生成字节，没有 USB、串口、网络、系统驱动、设备发现、队列、重试、状态查询或打印回执模块。依据：核心公开入口止于 `IBufferWriter`、`byte[]` 和 `PrintPageOutput`，`src/` 中没有传输实现。

处理方向：由宿主应用实现传输层；若后续纳入本仓库，应先定义异步发送、取消、重试、幂等和分段输出顺序契约。

### ESC/P 与厂商私有协议

针式打印机只在 ESC/POS Raster 中提供通用 `ESC *` 位图配置。仅支持 ESC/P、PCL 或厂商私有指令的设备没有独立 Provider。依据：`PrintProtocol` 和两个工厂只包含 CPCL、ESC/POS、TSPL、ZPL。

处理方向：取得目标机型协议手册和抓包样本后新增独立协议、能力与 golden-byte 测试，不能将 ESC/POS 结果直接视为兼容。

## 已接受边界

### CPCL 旧 Helper 的条码映射范围

`src/Bing.EasyPrint/CPCL/Helper.cs` 对已定义但尚未映射的 `BarcodeType` 抛出 `NotSupportedException`；非法枚举值抛出 `ArgumentOutOfRangeException`。结构化 Provider 和协议命令已有更完整的按协议能力校验，但旧转换帮助方法并未覆盖所有枚举值。

这是旧 API 的受控支持边界，不属于待修复异常语义。若要支持新的条码制式，应单独扩展映射并补充对应转换测试。

## 待确认

### 真实设备兼容与校准

CPCL、ESC/POS、TSPL、ZPL 的字节与模拟边界有自动化覆盖，但真实热敏/针式打印机的 DPI、字体、代码页、HRI、图片阈值、走纸、裁切和扫描结果不能由仓库测试证明。

处理方向：按设备型号、固件版本和耗材建立验收表，将实测 Profile 与样张纳入外部设备测试记录。

### 设备 Profile 数据来源

Profile 会收窄能力并参与预检，但库不会自动发现设备，也不验证调用方填写的宽度、固件、字体或切刀参数。

处理方向：为正式支持的型号维护可追溯到厂商手册或实测样张的配置；未知设备保持严格默认。

### 已有阶段自动化验收记录

GS1-128 显式 CC-A/CC-B 的最新完整验收见[A/B 阶段报告](gs1-128-ab-verification-2026-10-22.md)。以下 CC-C 和较早修复阶段记录保留原范围，不代表 A/B 阶段验收结果。

2026-10-22 后续 CC-C 阶段已完成本地验收：Core 两运行时各 3435 项、Host 148 项、Vitest 288 项、Chromium 156 项通过；五框架严格 XML 构建、四协议案例及候选包消费通过。新增能力与证据见[CC-C 阶段报告](gs1-composite-cc-c-verification-2026-10-22.md)。下段保留同日较早的修复阶段记录。

2026-10-23 GS1 完整关联校验与二维条码阶段首轮为 4599/4599、Host 252/252、Chromium 191/191；主代理独立复核发现 R1—R6。第一次集成修复后 Core 各 4617/4617、Host 254/254、Chromium 192/192；第二次定向复核仅剩 R4 Composite 实际编码和 R5 GS1 QR 旋转占位，第二轮修复后 Core 各 4626/4626、Host 262/262、受影响 E2E 9/9（`--workers=1 --retries=0`）、五框架 XML 10/10、案例 132 文件和候选包 net6.0 Composite/二维新 API 消费通过；Vitest 412/412 与生产构建因应用代码未变复用首轮证据。2026-10-24 主代理从当前源码独立构建并原样重跑探针，确认剩余真实链路问题已修复，G1—G5 与 R1—R6 均 CLOSED，OPEN_ACTIONABLE=0；本阶段软件验收完成，实机验收按用户要求排除，未发布。实现范围、命令和证据见[GS1 关联与二维条码阶段报告](gs1-associations-2d-verification-2026-10-23.md)；README 与使用说明见[GS1 关联与二维条码](gs1-associations-2d.md)。

截至 2026-10-22，本轮自动化验收已完成。Core 的 `net6.0` 与 `netcoreapp3.1` 完整测试各为 3397/3397 通过；Core 与 Templates 的严格 XML 门禁为 10/10 通过。Host 的 140 项测试、五模式能力加载、旧 Host Composite 降级及 Composite Host 预览/导出/项目往返断言已通过。前端生产构建与 Vitest 277 项通过；新增五模式能力及 Composite 定向 E2E 为 3/3 通过；ZPL、Code39 与复选框案例在完整回归中通过。完整 154 项 Chromium E2E 为 154 通过、0 失败、0 跳过、0 flaky，`retries=0`。命令和结果见[本轮修复验收报告](review-fixes-2026-10-22.md)。旧分轮证据见 `multi-protocol-verification.md`，不替代本轮结果。

处理方向：发布前按[开发与验证](development.md)重新运行当前分支门禁，并单独记录网络、SDK、浏览器或设备造成的阻塞。

### 条码业务语义与固件差异

库校验已支持的编码格式和长度，但 GS1 应用标识组合、行业校验规则、部分堆叠码行数及固件特有上限仍取决于业务和设备。

处理方向：由业务层校验领域语义，使用目标设备扫描回读测试确认固件行为；厂商扩展继续通过可信 Raw 或新 Provider 显式接入。
