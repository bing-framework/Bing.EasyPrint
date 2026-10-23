# GS1 关联与二维条码集成复核

- 日期：2026-10-23；主代理独立复核，OpenCode 负责后续实现。
- 当前结论：CLOSED；2026-10-24 主代理独立复核通过，R1—R6 全部关闭，OPEN_ACTIONABLE=0。以下首次和第二次复现记录保留；当前结论及证据见末尾最终验收。
- 候选：HEAD `89b117f31438507f64b395f8e31df3d01f766d31` 加本次收到完成通知后的工作区，包含必须保留的上一阶段变更。
- 原有全绿测试是有效的运行记录，但未覆盖以下输入，不能据此证明本阶段能力完整。

## 复现方法与证据

本次只构建独立 Host 输出并运行定向探针，没有重复执行全量门禁。探针的 `success` 表示调用是否返回，不等同于验收通过。

```powershell
dotnet build tools/DesignerHost/DesignerHost.csproj -c Release --no-restore -o artifacts/gs1-associations-review-20261023/host -p:GenerateDocumentationFile=false
dotnet run --project artifacts/gs1-associations-review-20261023/probe/ReviewProbe.csproj -c Release
```

证据目录：`artifacts/gs1-associations-review-20261023/`，包含 `build.log`、`probe-results.log`、探针源码及独立构建输出。构建通过，5 项既有 ImageSharp 依赖告警；未改变跟踪 XML。

## 可执行问题

### R1 / P1：GS1 QR 额外编码了开头 GS 字符

- 位置：`Gs1TwoDimensionalEncoder.EncodeQr`，以及 `Gs1TwoDimensionalEncoderTests` 的解码期望。
- 触发：`(01)12345678901231`，`Gs1=true`。
- 观察：当前解码为 `\u001D0112345678901231`、`]Q3`、25×25；直接使用同版本 ZXing 的 `GS1_FORMAT` 编码规范化数据 `0112345678901231` 解码为该数据本身、`]Q3`、21×21。
- 根因：在已有 FNC1 mode header 之外又把 ASCII 29 拼入数据；现有测试把错误的开头 GS 当成预期。GS1 QR 的首位模式标志与数据分隔符应分别处理。参见 [ZXing QR 编码器](https://github.com/micjahn/ZXing.Net/blob/master/Source/lib/qrcode/encoder/Encoder.cs) 和 [解码器](https://github.com/micjahn/ZXing.Net/blob/master/Source/lib/qrcode/decoder/DecodedBitStreamParser.cs)；实际版本行为以本地 0.16.11 探针为证。
- 修正与验收：去掉额外数据字节，正确处理数字/字母数字/字节模式的字面 `%`、`%%` 与字段分隔；使用独立的完整 payload 期望和 GS1 身份断言，不能仅断言 `]Q3`。Host PNG 和协议输出应一致。

### R2 / P1：DataMatrix 未执行严格关联开关

- 位置：`TemplateRenderer.MatrixCode`。
- 触发：Type 10、`Format=DataMatrix`、`Gs1=true`、`(02)12345678901231`，`ValidateGs1Associations=true`。
- 观察：渲染成功并返回 25016 字节；相同数据的 GS1 QR 正确拒绝缺少 AI 37。
- 根因：GS1 MatrixCode 分支没有调用关联校验器。
- 修正与验收：严格模式下 QR/DM 同样拒绝缺失与互斥关系，返回元素位置且无部分输出；false/缺省保留兼容。覆盖模板及真实 Host，增加至少一个 DM 浏览器失败与修正成功案例。

### R3 / P1：重复 GS1 QR 发生缓存键冲突

- 位置：`TemplateRenderer.Gs1Qr` 中 `images.Add(cacheKey, image)`。
- 触发：同一模板放两个内容、尺寸、静区相同的 GS1 QR（或重复行渲染相同内容）。
- 观察：抛出 `TemplateRenderException`，提示“索引中已存在相同键”。
- 根因：以内容/尺寸构造键，却无条件 Add；未实现命中复用，键还没有完整表达纠错等编码参数。
- 修正与验收：正确复用完整编码键，或按实例管理资源；重复/分页/同尺寸不同纠错配置均成功且码图正确，ownership 不串位，成功/异常/取消释放预算。

### R4 / P1：新解析路径的关联作用域和 FNC1 校验不一致

- 位置：`Gs1ElementStringParser.Parse`、`Gs1AssociationValidator.ValidateCarriers`、`Gs1TwoDimensionalEncoder.Normalize`。
- 触发及观察：
  - `ValidateCarriers(["(00)123456789012345675", "(4300)ACME"])` 提前拒绝 4300 缺 00；同样数据改成规范化形式则通过。
  - `ValidateCarriers(["(414)1234567890128", "(254)EXT"])` 提前拒绝 254 缺 414。
  - `Validate("4261240112345678901231")` 通过，尽管固定长度 AI 426 不是预定义长度，后续 AI 前仍需要 FNC1。
  - 新 GS1 QR 接受 `NOT-GS1`，生成带 GS1 标识的非 AI 数据。
- 根因：括号输入复用带历史关联检查的 Normalize，未先按载体分别做纯语法再合并；规范化输入按固定长度推断边界，未落实 RequiresSeparator；二维编码器的原始输入直接绕过 AI 语法校验。
- 修正与验收：为新入口共用纯逐字段解析与分隔规则，完整 req/ex 在显式合并后执行；保留旧入口的既有兼容语义。括号/规范化形式对等，验证上述组合和 Composite 分散主次数据、未知 AI/坏校验位/缺失或多余分隔符；严格开关只控制关联，不能令新 GS1 模式生成任意非 AI 数据。

### R5 / P2：GS1 QR 绕过模块宽度契约

- 位置：`TemplateRenderer.Gs1Qr`，正常 QR 参数检查之前的 GS1 分支。
- 触发：`UnitWidth=0`、`Width=Height=100`、合法 GS1 数据。
- 观察：返回成功及 25016 字节，绘图循环没有画任何黑模块。
- 修正与验收：使用既有 1..32 范围并在分配/编码前失败；负数、0、33 拒绝，1/32 有明确尺寸行为。补显式尺寸、非对称静区和旋转后的布局边界定向回归，避免返回空白成功。

### R6 / P2：验收报告及新能力消费证据不完整

- 已核实：Core 两真实运行时各 4599/4599，Host 252/252，Vitest 412，Chromium 191，XML 10，案例 131 文件，候选包常规消费成功。Core TRX 包含本阶段 15 条新增用例，Host 包含 8 条新增用例。
- 缺口：报告所引用 `artifacts/gs1-assoc-2d-xml.log` 不存在（实际 XML 汇总和子日志存在）；“定向 14/14”没有对应证据，与最终 15 条新增用例混用；E2E 日志未保存调用命令，无法由留档确认 `--retries=0`。既有前端构建日志中的首次编译失败需与最终结果区分，不能删除历史。
- 消费缺口：现有 PackageConsumer 没有调用 `Gs1AssociationValidator`、新 GS1 QR/DM 或 `ValidateGs1Associations`；样例启用了 Gs1，但没有运行严格关联示例。全绿常规消费不代表新增包 API 已验收。
- 修正与验收：补最小新能力消费及严格关联案例；修正报告路径、计数和证据范围。后续必要测试保存 UTF-8 命令、参数、运行时及结果；保留首次失败。勿仅为日志描述重新跑未变化代码的完整门禁。

## 本轮状态与下一步

- Completed：首次独立复核、定向复现、门禁证据盘点。
- Open Actionable：R1—R6；交同一 OpenCode 线程完成第一次集成修复，先补能暴露失败的回归，再按源码影响扩大验证。
- Blocked Approval / Blocked External：无；实机验收按用户要求排除。
- Not Applicable：实机验收；新增能力的历史性能对比。
- Accepted Limitations：GS1 二维使用图片路径；不自动合并页面条码；传输与插件边界保持。
- Deferred：Digital Link、PDF/SVG、批量导入、富文本、依赖大版本迁移。
- No-Progress Check：CHANGED；新增独立复现证据。当前为首次集成修复，未达到轮次上限。
- Goal Status：RUNNING；只有以上可执行问题驱动继续，不扩展新功能阶段。

## 第一次修复后的独立复核

主代理重新从当前源码构建到 `artifacts/gs1-associations-final-review-20261023/host`（`GenerateDocumentationFile=false`），未覆盖首次失败构建。原始探针输入未改，结果保存为 `original-probe-results.log`；追加链路验证保存为 `extended-probe-results.log`，源码在同目录 `probe/`。

- R1 CLOSED：GTIN 返回 `0112345678901231`、`]Q3`、21×21，与同版本参考相同；字面 `%`、`%%`、小写字节模式以及字段间 GS 均正确，不再多出 leading GS。
- R2 CLOSED：QR 和 DataMatrix 对 `(02)12345678901231` 开启严格关联时均拒绝缺少 37。
- R3 CLOSED：两个相同 GS1 QR 已能实际渲染，缓存键包含纠错与模块宽度，源码命中复用。
- R4 OPEN_ACTIONABLE：新校验器已修复，但 Composite 的真实编码链路仍使用旧的独立关联检查。
- R5 OPEN_ACTIONABLE：UnitWidth 已修复，但 GS1 QR 旋转后占位宽度仍未用于单元格边界校验。
- R6 CLOSED：独立证据核对确认 Core 两运行时各 4617、Host 254、Chromium 192（命令明确 retries=0）、XML 10、样例 132 和新增 API 包消费；前端源码未改，Vitest 412/构建复用合理。首次 focused E2E 为 3 通过/1 失败，后续 4/4，失败日志保留，截图已被后续运行覆盖。该门禁结果不替代 R4/R5 尚缺的实际链路验证。

### R4 剩余：校验器通过，Composite 实际输出仍失败

`Gs1CompositeEncoder.cs:96` 对次码调用旧 `NormalizeForProtocol`，`NormalizePrimary` 也仍使用该路径。新增测试只直接调用 `ValidateComposite`，没有验证后续编码。

真实渲染输入为 400×500 模板、Type 11、LinearType 11、Mode 3、ModuleWidth 1、LinearBarHeight 40、ESC/POS Raster、`ValidateGs1Associations=true`：

- 主码 `(00)123456789012345675`、次码 `(4300)ACME`：校验器通过，渲染抛出次码必须包含 00。
- 主码 `(414)1234567890128`、次码 `(254)EXT`：校验器通过，渲染抛出次码必须包含 414。
- 对照主码 `(01)12345678901231`、次码 `(20)12`：渲染成功，排除了模板尺寸或模式不支持。

验收：在真实 Composite 编码路径将纯语法和关联作用域分离，合法分散主次数据可在声明支持的模式输出；保留旧独立入口的兼容语义。增加 TemplateRenderer/Host 的预览和输出回归，不能只验证内部校验器。无效合并仍需定位拒绝，无部分输出。

### R5 剩余：旋转后可放下的 GS1 QR 被错误拒绝

`TemplateRenderer.cs:1135` 先检查 `w > available`，随后才计算 `quarterTurn` 和旋转后的占位。与同文件图片/条码分支使用实际占位宽度的做法不一致。

真实输入：400×500 模板，GS1 QR `(01)12345678901231`、UnitWidth 1、Width 450、Height 40、Rotation 90。编码区与默认静区可装入原始 450×40 图片，旋转后占位为 40×450，能放入页面；当前却报告预留尺寸不足。反向 40×450 旋转后宽 450 的输入最终会被 Provider 拒绝，但应在布局阶段按占位宽度明确拒绝。

验收：保留符号自身尺寸和静区检查，同时按旋转后宽度检查单元格；90/270 两个方向验证可放下与越界反例，断言实际布局尺寸/元素 PNG 和相邻元素位置，不能只断言输出非空。

本次仅继续 R4/R5 两处残留；为第二轮集成修复。R1/R2/R3 不重复打开，已有未变源码的测试证据可复用。不运行性能矩阵或无关新功能。No-Progress Check=CHANGED（第一次修复已有实际进展，新增链路证据定位了未修范围）。

## 最终验收（2026-10-24）

主代理从当前源码重新构建独立 Host 输出，并原样复制此前探针到新的证据目录运行，避免使用执行方旧二进制或只依赖其完成消息：

```powershell
dotnet build tools/DesignerHost/DesignerHost.csproj -c Release --no-restore -o artifacts/gs1-associations-final-review-20261024/host -p:GenerateDocumentationFile=false
dotnet run --project artifacts/gs1-associations-final-review-20261024/probe/ReviewProbe.csproj -c Release
```

构建通过，0 错误，保留 5 项既有 ImageSharp 2.1.11 依赖告警。`build.log`、`probe-results.log` 和未改写的探针源码位于 `artifacts/gs1-associations-final-review-20261024/`；此前失败证据保持原样。

- R4：真实 `TemplateRenderer` 输出中，`(00)+(4300)`、`(414)+(254)` 和标准 Composite 均成功；纯语法解析与合并关联作用域经测量、绘制和文档复制传到编码器。默认独立入口保留旧语义。
- R5：450×40 的 QR 旋转 90 度后可放入 400×500 页面；反向 40×450 的输入因旋转后越界在布局阶段拒绝。模板/Host 回归另外覆盖 270 度、布局尺寸、相邻元素位置和 PNG 解码。
- 原始回归：GTIN 的 GS1 QR 解码为完整 `0112345678901231`、`]Q3`、21×21，无多余开头 GS；百分号和分隔符保持正确。严格 QR/DM 缺失关联、非 GS1 输入、缺少分隔符和 UnitWidth=0 均按预期拒绝；重复 QR 成功。

探针中的 `success=false` 对上述非法输入属于预期行为。执行方第二轮门禁与可复用证据范围见[阶段报告](gs1-associations-2d-verification-2026-10-23.md) 第 8、10 节；不把受影响 E2E 的 9/9 写成新的全量运行结果。

最终分类：R1—R6 与 G1—G5 为 CLOSED；OPEN_ACTIONABLE=0，停止本阶段修复。实机验收按用户要求排除；Native 的 GS1 二维图片路径、显式关联作用域和现有 Provider 扩展边界为 ACCEPTED_LIMITATION；图片依赖大版本迁移为 DEFERRED。没有提交、推送、发布或进入其他功能阶段。
