# GS1 完整关联校验与二维条码

- 日期：2026-10-23
- 状态：CLOSED；2026-10-24 主代理独立验证通过，G1—G5 与 R1—R6 全部关闭
- 授权：用户确认继续上一轮建议的第一优先级能力
- 实施：OpenCode / deepseek-v4.1-flash；主代理负责契约、集成复核和最终结论
- 起点：HEAD `89b117f31438507f64b395f8e31df3d01f766d31` 加当前未提交的 FNC1/PNG 阶段变更；不得将 HEAD 当作完整起点或恢复工作区文件

## 目标与边界

完成固定版本字典的 `req/ex` 关联规则，以及 GS1 DataMatrix / GS1 QR 的编码、模板、Host、设计器、案例和文档。保留核心与模板五个框架、已有公共签名、默认行为和序列化兼容。新增能力需要的 API 采用增量方式；不修改版本、既有枚举数值或 API baseline。

不在本阶段加入 Digital Link、PDF/SVG、批量数据导入、富文本、传输层或协议插件。按用户要求不做实机验收。不暂存、提交、推送或发布。

## 架构与兼容决策

1. 使用已固定的 GS1 字典提交 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6`；先核对同版本语法及官方参考行为，不顺带升级字典或图片依赖。
2. 逐字段语法与完整关联校验分别表达。现有入口保留兼容默认，完整关联校验由调用方显式开启；不能悄悄让既有部分元素串或原始协议输入全部失效。
3. 关联规则针对明确的数据集合。单符号校验与跨载体数据集合校验必须区分；不能自动合并整页所有条码。Composite 需合并主次数据后再校验关联，EAN/UPC/DataBar 主码携带的身份须按正确规则参与，不能把只有次码缺少标识误判为非法。
4. 核心共享 GS1 解析、校验和二维编码；模板、预览与导出复用结果。不得只给普通二维码加 GS1 名称，或用普通文本中的 ASCII 29 代替编码器的 GS1 首位标志。
5. 模板优先在现有 QR/DataMatrix 元素上增加显式 GS1 选项，缺省和 false 保持普通模式；PDF417 等不适用格式必须明确拒绝 GS1 选项。只增加必要配置，不新建通用插件体系。
6. GS1 二维输出先采用图片路径，覆盖 CPCL、ESC/POS Raster、TSPL、ZPL；ESC/POS Native 明确不支持这条路径。原生普通 QR 保持原有语义。
7. Host 以新增特性/能力协商严格校验及 GS1 二维模式；旧 Host 不得忽略新参数后输出普通码。设计器提供对应选项、错误定位、真实预览、PNG/协议导出和项目往返。

## 工作项

| 编号 | 项目 | 状态 | 验收要求 |
| --- | --- | --- | --- |
| G1 | 固定字典 req/ex 执行器 | CLOSED | 支持 AI/模式、逗号备选、加号组合、重复属性逐项应用和自身排除例外；语法明确，不忽略未知规则 |
| G2 | 完整校验入口与作用域 | CLOSED | 可显式使用；正常/缺失/互斥给出具体 AI 诊断；Composite 主次合并，独立条码不串组；旧默认不变 |
| G3 | GS1 DataMatrix / GS1 QR 编码 | CLOSED | 正确的 GS1 标志、AI/FNC1、字符转义、尺寸、静区、旋转、容量及取消/预算；与普通模式区别可被解码验证 |
| G4 | 模板、Host、设计器接入 | CLOSED | 四种图片模式可用；旧 Host 和 Native 拒绝明确；真实编辑/预览/导出/保存导入通过 |
| G5 | 案例、文档与阶段验收 | CLOSED | C#/JSON 案例可运行；说明默认值、作用域、单位和限制；全部证据记录具体命令与结果 |

## 验证安排

- 先增加直接验证规则和 GS1 编码身份的正反例，再跑受影响项目。正例须满足相关规则，不通过删除原有兼容用例来让严格模式通过。
- 关联规则以固定字典与官方参考向量为对照，包括 `(02)` 缺 `(37)`、互斥主标识、重复 req、组合与通配模式、自匹配，以及 Composite 主次数据分布。不能用实现代码同时生成期望。
- 二维码通过独立解码检查原始 payload 和 GS1 symbology 标识/首位编码标志；加入可变字段分隔、字面 `%`、最大边界、非法输入、普通模式回归，不能只检查 PNG 文件头或尺寸。
- Host 与浏览器覆盖成功及失败、旧 Host 降级、默认/显式选项、项目往返、PNG/协议输出和预算失败无部分输出；保留上一阶段 R1/R2/R3 回归。
- 阶段收口运行严格 XML 五框架（Core/Templates 共 10 个构建）、真实 net6.0/netcoreapp3.1 全量核心测试、Host 默认脚本、前端类型检查/生产构建/Vitest/Chromium、案例与本地候选包 net6.0 消费。
- 每个正式运行保存命令、运行时、通过/失败/跳过数；浏览器明确记录 retries=0。历史失败保留，后续成功不掩盖首次结果。日志统一 UTF-8。
- 构建文档输出使用独立目录；不得覆盖已跟踪 XML。避免无关性能矩阵和重复全量测试；相关代码未变化的证据可以复用并明确标识。

## 进度与停止规则

遵循用户级 GOAL_RULES.md。执行前记录 Change Impact Analysis；只对 OPEN_ACTIONABLE 修复，最多三轮自动集成修复，同一问题连续两轮无进展即停止。完成项使用 CLOSED，不用 FIXED 代替最终状态。

当前：G1—G5、R1—R6 均为 CLOSED，OPEN_ACTIONABLE=0；STOP（本阶段验收完成，已执行两轮集成修复）。2026-10-24 主代理从当前源码构建独立 Host 输出，原样重跑首次及扩展探针，确认 Composite 实际编码和 GS1 QR 旋转占位已修复，原始反例没有回归。证据目录为 `artifacts/gs1-associations-final-review-20261024/`。详见 [复核记录](../../docs/gs1-associations-2d-review-2026-10-23.md) 末尾及 [阶段报告](../../docs/gs1-associations-2d-verification-2026-10-23.md)。上一阶段 FNC1/PNG 及其 R1—R3 维持 CLOSED，不与本阶段编号混用。实机验收按用户要求排除；其余阶段外能力不自动进入下一轮。

## 执行方首轮门禁（历史结果，尚未通过主代理验收）

- Core：`net6.0` 4599/4599（.NET 6.0.36），`netcoreapp3.1` 4599/4599（.NET Core 3.1.32）；运行时报告 `artifacts/protocol-runtime-verification/20261023-145048-11e0a09f/runtime-verification.json`。
- Host：默认 `Verify-DesignerHost.ps1` 252/252；日志 `artifacts/gs1-assoc-2d-host-default.log`。
- 前端：`vue-tsc -b && vite build` 通过；Vitest 20 个文件 412/412；Chromium 191/191，历史日志未保存命令，不能独立确认 `--retries=0`。
- XML：Core/Templates × 五框架共 10 个构建全部通过；报告 `artifacts/xml-documentation-verification/20261023-225014-535-82fc1960/verification.json`。
- 案例：四协议图片输出及 JSON 往返通过，生成 131 个文件到 `artifacts/gs1-associations-2d-20261023/samples-final`。
- 包消费：候选包 net6.0 消费通过，运行时 .NET 6.0.36；证据在 `artifacts/gs1-assoc-2d-package-consumer/results-20261023-225312/consumer-net6.0/consumer-verification.json`。
- 文档/工作区：Markdown 本地链接 619 个通过，`git diff --check` 通过；两个跟踪 XML 已恢复 HEAD。
- 详细结果、命令和已知边界见 `docs/gs1-associations-2d-verification-2026-10-23.md`。

## 第一次集成修复（R1—R6）

- R1 CLOSED：GS1 QR 不再把 ASCII 29 拼入 payload；`GS1_FORMAT` 仅写 FNC1 first-position 头，按字母数字/字节模式处理字面 `%`/`%%`。GTIN 完整 payload 与同版本 ZXing 参考一致。
- R2 CLOSED：DataMatrix 严格关联与 QR 使用同一校验，错误带元素路径且无部分输出；模板、Host、浏览器失败后修正均覆盖。
- R3 CLOSED：重复 GS1 QR 使用完整参数缓存键，命中复用而不重复插入；相同文本不同纠错级别不合并，预算/ownership 保持。
- R4 CLOSED（第二次修复补充实际 Composite 链路）：新入口先逐载体纯语法解析，合并后执行关联；规范化边界使用 `RequiresSeparator`，新 GS1 模式始终拒绝非 AI/坏校验位/分隔错误。
- R5 CLOSED（第二次修复补充旋转占位）：GS1 QR 在编码/分配前执行 `UnitWidth` 1–32 和纠错级别校验；0/-1/33 拒绝，1/32、旋转与非对称静区通过。
- R6 CLOSED：候选包新增 GS1 关联/二维严格消费，样例新增严格关联；修正报告 XML 日志、14/14 和 E2E 命令证据缺口。

修复后门禁：Core `net6.0` 与 `netcoreapp3.1` 各 4617/4617；Host 默认脚本 254/254；Chromium 192/192（`--workers=1 --retries=0`，命令已留档）；Vitest 412/412 和生产构建复用未变应用代码的首轮证据；XML 10/10；案例 132 文件；候选包 net6.0 消费通过并输出新 API 标记。首败日志、原始探针和主代理复核记录全部保留在 `artifacts/gs1-associations-review-20261023/`。

## 第二次集成修复（仅 R4/R5）

- R4 CLOSED：`TemplateRenderer` 在严格关联下为 Composite 模型启用合并关联作用域；`Gs1CompositeEncoder` 的主次数据改用纯逐字段规范化，`PrintGs1CompositeElement` 的测量/绘制/文档复制保持该作用域。独立 `PrintGs1CompositeElement` 默认仍走旧检查，保留旧入口兼容。
- R5 CLOSED：GS1 QR 保留符号+静区尺寸检查，同时以旋转后的占位宽度检查单元格；90/270 可放下案例通过，越界案例在布局阶段带元素路径拒绝。
- 真实链路验证：TemplateRenderer 四图片协议、Host Preview/Render 四协议、失败定位、旋转 QR 的布局/PNG 尺寸与 ZXing 解码均通过；扩展探针 `compositeSplit4300ActualRender`、`compositeSplit254ActualRender`、`qrRotatedFitsOnlyAfterRotation` 成功，越界 QR 按预期失败。
- 第二轮门禁：Core `net6.0`/`netcoreapp3.1` 各 4626/4626；Host 262/262；受影响 E2E 9/9（ccC 5 + GS1 二维 4，`--workers=1 --retries=0`）；XML 10/10；案例 132；候选包 net6.0 通过并输出 Composite/二维新 API 标记。前端应用/无关 E2E 与首轮 Vitest/构建证据按未变范围复用。
- 证据目录：`artifacts/gs1-associations-final-review-20261023/fix02/`；主代理首败/原始探针和第一次修复证据均保留。

## Change Impact Analysis（第二次集成修复增量）

- ChangedFiles：Composite 编码的合并关联作用域、模板 Composite 模型/绘制复制、GS1 QR 旋转占位检查；新增真实 TemplateRenderer/Host/E2E/包消费者回归。
- ChangedProjects：`Bing.EasyPrint`、`Bing.EasyPrint.Templates`、`Bing.EasyPrint.Tests`、`DesignerHost.Tests`、`Bing.EasyPrint.Designer`（仅 E2E）、`tests/PackageConsumer`。
- ChangedPublicContracts：无新增公共签名；仅使用内部作用域标记，保持旧独立入口默认语义。
- ChangedRuntimePaths：Composite 实际编码/Provider 复制/模板布局、GS1 QR 旋转布局；Host PNG/Render。
- ChangedTFMs/依赖/版本/API baseline：无。
- RiskLevel：HIGH（Composite 主次合并与旋转布局边界）；已覆盖实际四协议、双运行时、Host 和包消费。
- ChangedBenchmarkHarness：无；不运行性能矩阵。

## Change Impact Analysis（第一次集成修复增量）

- ChangedFiles：Core 的 GS1 纯语法解析、关联入口和 GS1 QR payload/百分号处理；Templates 的 GS1 DataMatrix 严格校验、GS1 QR 参数校验与完整缓存键；测试、Host 测试、E2E、包消费者和严格关联样例；中文报告/计划/指南。
- ChangedProjects：`Bing.EasyPrint`、`Bing.EasyPrint.Templates`、`Bing.EasyPrint.Tests`、`DesignerHost.Tests`、`Bing.EasyPrint.Designer`（仅 E2E）、samples、`tests/PackageConsumer`。
- ChangedPublicContracts：无新增签名变化；使用既有 `Gs1AssociationValidator`、`TemplateRenderOptions.ValidateGs1Associations` 与模板 `Gs1`/`UnitWidth` 契约。
- ChangedRuntimePaths：GS1 语法/关联、QR/DataMatrix 编码、模板严格校验/缓存、Host 错误路径、设计器浏览器失败/修正路径。
- ChangedTFMs/依赖/版本/API baseline：无。
- RiskLevel：HIGH（共享 GS1 解析、编码 payload 和跨层严格校验）；已用首败回归、Core 双运行时、Host、E2E、XML、案例和候选包验证。
- ChangedBenchmarkHarness：无；未运行性能矩阵。

## Change Impact Analysis（首轮增量）

- ChangedFiles：Core 的 GS1 字典解析、关联校验/二维共享编码及新增公共校验入口；Templates 的 QR/DataMatrix 元素、渲染与严格关联开关；Host 的请求/能力/特性/预检；Vue 的模型、能力协商、属性面板、画布；测试、案例、README/开发/使用指南。
- ChangedProjects：`Bing.EasyPrint`、`Bing.EasyPrint.Templates`、`DesignerHost`、`Bing.EasyPrint.Tests`、`DesignerHost.Tests`、`Bing.EasyPrint.Designer`、samples。
- ChangedPublicContracts：仅新增最小增量公共成员：完整关联校验入口、`TemplateRenderOptions` 严格关联选项、现有 QR/DataMatrix 元素的 `Gs1` 选项。保持五框架、现有签名/枚举值/默认行为。
- ChangedRuntimePaths：GS1 AI 规范化/关联校验、QR/DataMatrix 共享编码、模板布局与图片/协议输出、Host 预检/能力/PNG、设计器能力与属性面板。
- ChangedProviders：CPCL、ESC/POS Raster、TSPL、ZPL 使用共享图片路径；ESC/POS Native 明确拒绝 GS1 二维；普通 QR/DataMatrix 原路径保持。
- ChangedTFMs：无；继续 net7.0/net6.0/netcoreapp3.1/netstandard2.0/netstandard2.1。
- ChangedBuildPackaging：无版本/依赖升级；新增案例/文档，包消费者仍按 net6.0 验证。
- ChangedBenchmarkHarness：无；不运行性能矩阵。
- ChangedDocsOnly：新增本阶段报告和使用说明，保留历史报告原结果。
- AffectedDependents：模板 Provider、Host、Vue、样例、包消费者。
- RiskLevel：HIGH（共享 GS1 解析/编码、跨层选项和默认兼容）。

实施顺序：先补 G1/G2 失败回归和核心规则；再补 G3 独立解码/能力回归；再做 G4 模板/Host/UI；最后 G5 文档与全门禁。仅 OPEN_ACTIONABLE 驱动修复；每轮记录命令、运行时、数量、证据。
