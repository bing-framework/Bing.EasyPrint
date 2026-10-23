# GS1 完整关联校验与二维条码阶段验收

- 日期：2026-10-23
- 范围：G1 固定字典 `req`/`ex` 执行；G2 严格关联入口与作用域；G3 GS1 QR / GS1 DataMatrix 共享编码；G4 模板/Host/设计器；G5 案例、文档与本阶段门禁
- 当前状态：CLOSED；2026-10-24 主代理独立验证通过，G1—G5 和 R1—R6 全部关闭，OPEN_ACTIONABLE=0。以下保留首轮、第一次修复和第二轮结果；最终验收见第 10 节。不提交/不发布/不做实机验收。当前证据见 [复核记录](gs1-associations-2d-review-2026-10-23.md) 末尾。

证据路径约定：本报告的 `fix01/` 位于 `artifacts/gs1-associations-review-20261023/fix01/`，`fix02/` 位于 `artifacts/gs1-associations-final-review-20261023/fix02/`；均相对于仓库根目录。

## 1. G1/G2 关联校验

### 规则
- 固定字典提交：`d1702ffb2fa5060f8460f7d581b01e377deaa1d6`。
- 逗号表示备选，`+` 表示组内 AND，`n` 匹配一个数字位置。
- 同一 AI 的重复 `req` 属性逐项应用；`ex` 不排斥 AI 自身。
- 关联校验只作用于显式数据集合，不自动合并整页独立条码。
- 字段边界按字典的预定义长度属性处理；固定长度但没有 `*` 的 AI 在后续元素前仍需要 FNC1/ASCII 29。

### 公开入口
`Gs1AssociationValidator.Validate(string)` 校验单个载体；`ValidateCarriers(IEnumerable<string>)` 合并多个载体；Composite 使用内部 `ValidateComposite` 合并主次数据。`TemplateRenderOptions.ValidateGs1Associations` 默认 `false`，显式开启后才执行完整关联校验；新 GS1 二维模式即使未开启严格关联，也始终执行逐字段语法、校验位、非 AI 和分隔符校验。

每个载体先执行纯逐字段解析，`ValidateCarriers` 合并后才执行关联规则，因此 `4300` 与 `00`、`254` 与 `414` 可以分散在不同载体；括号式与规范化输入等价。`(37)` 同时要求 `(00)` 以及 `(02)` 或 `(8026)`，所以真实通过示例需要 `(00)…(02)…(37)…`。

Composite 的 EAN/UPC/DataBar 原始 GTIN 主码会贡献隐式 `(01)`；只有 `(10)LOT` 或 `(20)12` 的次码通过显式合并后才能满足主标识，独立次码不会被误判为完整对象。`(02)` 缺 `(37)`、互斥主标识、重复 req、逗号/加号、重复实例、自匹配以及 Composite 分散主次数据均有回归。

### 证据
- `tests/Bing.EasyPrint.Tests/Gs1AssociationValidatorTests.cs`
- 固定原始字典非注释属性盘点：`req=194`、`ex=84`；重复 req 源行覆盖和 `7258`/`8030` 加号规则由测试确认。
- 本轮 GS1 关联、二维编码和模板定向测试 33/33 通过：`artifacts/gs1-associations-review-20261023/fix01/core-targeted-after-fix4.log`。
- 首轮 Core TRX 包含本阶段 15 条新增用例，作为历史证据保留；不再引用无独立留档的“14/14”。

## 2. G3 GS1 QR / DataMatrix

- 共享 `Gs1TwoDimensionalEncoder` 使用 ZXing.Net 0.16.11：QR `GS1_FORMAT` 写入 FNC1 first-position 模式头，payload 不再额外拼接 ASCII 29；DataMatrix `GS1_FORMAT` + `DATA_MATRIX_COMPACT`。
- 字母数字模式下的字面 `%` 编码为 `%%`，`%%` 编码为 `%%%%`；含 ASCII 29 或非字母数字字符时使用字节模式，保留原始 `%` 和分隔符。该处理避免把字面 `%` 解码成额外 FNC1。
- 独立 ZXing 解码验证：GS1 QR `]Q3` 且 payload 无额外开头 GS；GS1 DataMatrix `]d2`；`%`、ASCII 29 和规范化 AI 串按模式正确还原。
- 普通 QR/DataMatrix 路径保持普通码；PDF417 `Gs1=true` 明确拒绝；GS1 QR 的 `UnitWidth` 保持 1–32 契约。
- GS1 二维码图片路径覆盖 CPCL、ESC/POS Raster、TSPL、ZPL；ESC/POS Native 明确拒绝。

### 证据
- `tests/Bing.EasyPrint.Tests/Gs1TwoDimensionalEncoderTests.cs`
- `tests/Bing.EasyPrint.Tests/Gs1TwoDimensionalTemplateTests.cs`
- `tests/DesignerHost.Tests/Gs1TwoDimensionalHostTests.cs`
- 修复后独立探针：`artifacts/gs1-associations-review-20261023/fix01/probe-after-fix-fixed-host.log`；原始首败探针和 `probe-results.log` 保留未改。

## 3. G4 模板 / Host / 设计器

- Type 3 `BpQrCodeElement.Gs1`、Type 10 `BpMatrixCodeElement.Gs1` 默认 false；JSON 缺省/false 保持普通模式，true 持久化。
- 新特性：`gs1-two-dimensional`、`gs1-association-validation`；Host 能力对象 `gs1TwoDimensional: { qr, dataMatrix, native, associationValidation }`，请求 `validateGs1Associations`。
- QR 和 DataMatrix 的严格关联执行一致，错误均带模板元素路径；严格检查在编码/分配前完成，不产生部分输出。
- 同模板重复 GS1 QR 使用含文本、纠错级别、模块宽度、尺寸和静区的完整缓存键；相同实例复用，同一文本不同纠错级别不合并。
- `UnitWidth` 的 0、负数、33 均在编码/分配前拒绝；1/32、旋转和非对称静区有定向回归。
- 设计器属性面板、输出设置严格开关、预览、元素/页面 PNG、CPCL 导出、JSON/项目往返均有浏览器覆盖；新增 DataMatrix 严格关联失败后修正通过的回归。

### 证据
- `web/Bing.EasyPrint.Designer/e2e/gs1TwoDimensional.spec.ts`（4/4）
- `web/Bing.EasyPrint.Designer/src/capabilities.test.ts`
- `web/Bing.EasyPrint.Designer/src/api.test.ts`

## 4. G5 案例与文档

- `samples/Templates/gs1-two-dimensional.json`
- `samples/Templates/Gs1TwoDimensionalSamples.cs`
- `docs/gs1-associations-2d.md`
- `README.md`、`docs/README.md`、`docs/status.md`、`docs/templates-guide.md`、`docs/templates.md`、`docs/designer.md`、`docs/designer-host.md`、`docs/development.md`

示例运行会生成 CPCL/ESC/POS Raster/TSPL/ZPL 图片输出并校验 JSON 往返，同时断言 Native 拒绝。本轮新增严格关联有效输出，并断言缺少 `(37)` 的 GS1 DataMatrix 被拒绝。

## 5. 首轮门禁（修复前，历史证据）

以下数字只记录执行方首轮运行，不能作为本次修复后的验收结论。首轮 E2E 日志没有保存调用命令，无法由该日志独立确认 `--retries=0`；不再把该记录写成最终证据。

| 门禁 | 首轮结果 | 证据与说明 |
| --- | --- | --- |
| Core `net6.0` | 4599/4599；.NET 6.0.36 | `artifacts/gs1-assoc-2d-core-net6-final.log`；`artifacts/protocol-runtime-verification/20261023-145048-11e0a09f/runtime-verification.json` |
| Core `netcoreapp3.1` | 4599/4599；.NET Core 3.1.32 | `artifacts/gs1-assoc-2d-runtimes.log`；同一首轮运行时报告 |
| Host 全量 | 252/252 | `artifacts/gs1-assoc-2d-host-default.log`；TRX 在 `artifacts/gs1-assoc-2d-host-results-final` |
| 前端类型/生产构建 | 通过 | `artifacts/gs1-assoc-2d-web-build-final.log` |
| Vitest | 20 个文件、412/412 | `artifacts/gs1-assoc-2d-web-unit-final.log` |
| Chromium 全量 | 191/191（历史日志未留命令） | `artifacts/gs1-assoc-2d-e2e-full.log` |
| XML 五框架 | 10 个构建全部通过 | `artifacts/xml-documentation-verification/20261023-225014-535-82fc1960/verification.json`；不存在 `artifacts/gs1-assoc-2d-xml.log`，已删除该错误引用 |
| 示例 | 通过；131 个文件 | `artifacts/gs1-associations-2d-20261023/samples-final` |
| 候选包 net6.0 | 通过；.NET 6.0.36 | `artifacts/gs1-assoc-2d-package-consumer/results-20261023-225312/consumer-net6.0/consumer-verification.json` |

首轮之后主代理独立复现 R1—R6，原始复现证据保留在 `artifacts/gs1-associations-review-20261023/`；首败探针 `probe/Program.cs` 和 `probe-results.log` 未被删除或改写。

## 6. 第一次集成修复 R1—R6

| 编号 | 状态 | 修复与验收 |
| --- | --- | --- |
| R1 | CLOSED | 去掉 QR 数据前的额外 GS；`GS1_FORMAT` 只负责 FNC1 模式头。按编码模式处理字面 `%`/`%%`，GTIN 解码与同版本 ZXing 参考一致，完整 payload 有独立期望。首败：`fix01/core-targeted-before-fix.log`；修复：`fix01/core-targeted-after-fix4.log`、`fix01/probe-after-fix-fixed-host.log`。 |
| R2 | CLOSED | `MatrixCode` 的 GS1 DataMatrix 分支接入同一严格关联校验，错误带元素路径且无部分输出；QR/DM 一致。模板、Host、DataMatrix 浏览器失败后修正均覆盖。首败：`fix01/host-targeted-before-fix.log`；修复：`fix01/host-default-after-fix.log`、`fix01/e2e-gs1-after-fix2.log`。 |
| R3 | CLOSED | GS1 QR 缓存键包含文本、纠错级别、模块宽度、尺寸和静区；命中复用，不再 `Add` 冲突；相同内容不同纠错级别不合并，预算和 ownership 仍由会话管理。首败：`fix01/core-targeted-before-fix.log`；修复：`core-targeted-after-fix4.log`、Host 重复实例回归。 |
| R4 | CLOSED（第二轮实际链路闭环） | 第一次修复只修了解析/校验入口；第二轮在 Composite 测量、绘制和文档复制全过程保留合并关联作用域，主次数据改用纯逐字段规范化。四图片协议的 TemplateRenderer 和 Host Preview/Render 均验证 `(00)+(4300)`、`(414)+(254)` 成功，非法合并带元素路径拒绝；独立旧入口默认检查保留。 |
| R5 | CLOSED（第二轮旋转占位闭环） | `UnitWidth` 1–32 原修复保留；第二轮按旋转后实际占位检查单元格宽度。90/270 下 450×40 可放入 400 宽模板，布局为 40×450、相邻元素 Y=450；反向 40×450 在布局阶段以 `GS1 QR` 诊断拒绝。Host 元素 PNG 尺寸与 ZXing 解码通过。 |
| R6 | CLOSED | 新增候选包 `Gs1AssociationValidator`、GS1 二维模板、严格关联、重复实例和 UnitWidth 消费；新增严格关联样例。报告修正不存在的 XML 日志引用和“14/14”无留档说法，首轮 E2E 明确标注未留命令。包消费者首次因证据目录超过 Windows 260 字符路径失败，日志保留；改用短目录后通过，代码未改。 |

修复后探针仍需注意：`rawNotGs1`、`fixedLengthMissingSeparator426`、`dataMatrixStrictMissingAssociation` 和 `qrUnitZero` 均按预期失败；`duplicateQr` 和两个 Carrier 组合通过。探针的 `success` 字段只表示调用是否返回，不是验收结论。

## 7. 第一次修复后门禁（历史）

本轮代码已改变 Core、Templates、Host、样例和包消费者；前端应用代码未改变，构建/Vitest 证据明确复用首轮记录，新增浏览器用例重新运行。

| 门禁 | 结果 | 证据 |
| --- | --- | --- |
| Core `net6.0` | 4617/4617；.NET 6.0.36 | `fix01/core-full-net6.log`；`fix01/runtimes-after-fix.log` |
| Core `netcoreapp3.1` | 4617/4617；.NET Core 3.1.32 | `fix01/runtimes-after-fix.log`；`artifacts/protocol-runtime-verification/20261023-153442-81fcb9a6/runtime-verification.json` |
| Host 全量 | 254/254；默认 `Verify-DesignerHost.ps1` | `fix01/host-default-after-fix.log`；TRX 在 `fix01/host-results` |
| 前端类型/构建 | 应用代码未变，复用首轮通过证据 | `artifacts/gs1-assoc-2d-web-build-final.log` |
| Vitest | 应用代码未变，复用首轮 412/412 | `artifacts/gs1-assoc-2d-web-unit-final.log` |
| Chromium 全量 | 192/192；命令含 `--workers=1 --retries=0` | `fix01/e2e-full-after-fix.log` |
| XML 五框架 | 10/10 通过 | `fix01/xml-after-fix.log`；`artifacts/xml-documentation-verification/20261023-234525-636-ba45d30f/verification.json` |
| 示例 | 通过；132 个文件，含严格关联输出 | `fix01/samples`；`fix01/samples-after-fix.log` |
| 候选包 net6.0 | 通过；新 API 消费输出明确 | `fix01/package-consumer-final.log`；`artifacts/r6pc-20261023-234459/consumer-net6.0/consumer-verification.json` |
| 文档/工作区 | Markdown 本地链接 622 个通过；`git diff --check` 通过；两个跟踪 XML 已恢复 HEAD | 本报告与阶段计划 |

跟踪 XML 的构建使用独立输出或最终恢复；最终恢复后两个跟踪 XML 不进入本轮差异。

## 8. 第二次集成修复（仅 R4/R5）

第二轮只修改 Composite 实际编码作用域和 GS1 QR 旋转占位，未重开 R1/R2/R3/R6，也未改动关联字典、百分号处理或 PDF/传输范围。

| 项目 | 结果 | 证据 |
| --- | --- | --- |
| Composite 真实编码 | `TemplateRenderer` 四图片协议与 Host Preview/Render 四协议均通过 `(00)+(4300)`、`(414)+(254)`；非法合并带 `Rows[0].Cells[0].Elements[0]` 拒绝 | `fix02/core-r4-r5-after-fix2.log`；`fix02/host-r4-r5-after-fix2.log` |
| Composite 旧入口兼容 | 独立 `PrintGs1CompositeElement` 默认仍执行旧检查；内部合并作用域只由严格模板路径启用 | `Gs1CompositeTemplateTests.Standalone_composite_keeps_legacy_independent_carrier_checks_by_default` |
| GS1 QR 旋转布局 | 90/270 下 450×40 放入 400 宽模板，布局 40×450、相邻元素 Y=450；反向 40×450 在布局阶段拒绝 | `fix02/core-r4-r5-after-fix2.log`；`fix02/host-r4-r5-after-fix2.log` |
| QR PNG 解码 | 元素 PNG 尺寸 40×450；低模块 PNG 最近邻放大 4 倍后 ZXing 解码为 `0112345678901231`、`]Q3` | `fix02/host-r4-r5-after-fix2.log` |
| 扩展探针 | 分散 Composite 两个成功控制项、标准 Composite 和旋转可放下项成功；越界 QR 按预期失败 | `fix02/extended-probe-after-fix.log`；主代理原始/extended 探针保留 |

上表的定向日志记录修复过程；最终 Core 测试源码的验收依据为 `artifacts/protocol-runtime-verification/20261023-162756-708f7732/net6.0.trx` 和同目录 `netcoreapp3.1.trx`。这两个最终运行均包含 Composite 合并、90/270 度旋转占位及越界拒绝用例，时间晚于测试源码最后修改，避免将较早的定向日志误当作最终候选证据。

首败证据：主代理的 `artifacts/gs1-associations-final-review-20261023/extended-probe-results.log` 记录 R4 Composite 失败和 R5 旋转拒绝；第二轮第一次运行 `fix02/core-r4-r5-after-fix.log` 仍暴露 Provider 文档复制丢失合并作用域（4 个失败），修复后同组 103/103 通过。失败日志均保留，不覆盖为通过。

第二轮门禁：

| 门禁 | 结果 | 证据 |
| --- | --- | --- |
| Core `net6.0` | 4626/4626；.NET 6.0.36 | `fix02/runtimes-final.log`；`artifacts/protocol-runtime-verification/20261023-162756-708f7732/runtime-verification.json` |
| Core `netcoreapp3.1` | 4626/4626；.NET Core 3.1.32 | 同一运行时报告 |
| Host 全量 | 262/262 | `fix02/host-full.log`；TRX 在 `fix02/host-results` |
| 受影响 E2E | 9/9；`--workers=1 --retries=0` | `fix02/e2e-composite-strict.log`（ccC 5 + GS1 二维 4） |
| 前端构建/Vitest | 应用源码未变，复用首轮通过证据 | `artifacts/gs1-assoc-2d-web-build-final.log`；`artifacts/gs1-assoc-2d-web-unit-final.log` |
| XML 五框架 | 10/10 | `fix02/xml.log`；`artifacts/xml-documentation-verification/20261024-002646-161-294e9b14/verification.json` |
| 示例 | 通过；132 个文件 | `fix02/samples`；`fix02/samples.log` |
| 候选包 net6.0 | 通过；Composite 与二维新 API 标记明确 | `fix02/package-consumer-final.log`；`artifacts/r6pc2-20261024-002827/consumer-net6.0/consumer-verification.json` |
| 工作区 | Markdown 本地链接 622 个通过；`git diff --check` 通过；跟踪 XML 未变 | 本轮报告与计划 |

说明：上一轮全量 Chromium 192/192 的应用代码未变，但本轮新增 1 个严格 Composite 浏览器用例；本轮只重跑受影响的 `ccC.spec.ts` 与 `gs1TwoDimensional.spec.ts` 共 9/9，不把 193 当作已执行的全量计数。

## 9. 已知边界

- 未做真实打印机/扫码验收；GS1 二维是主机软件图片路径，不宣称设备原生 GS1 命令。
- GS1 QR/DataMatrix 图片路径依赖图像协议；ESC/POS Native 明确拒绝。
- 关联校验不实现全量跨 AI `req`/`ex` 之外的业务对象关系，也不自动合并页面条码。
- Digital Link、PDF/SVG、批量导入、传输层和插件体系不在本阶段。

## 10. 主代理最终验收（2026-10-24）

从当前源码构建独立 Host 输出后，原样重跑首次及扩展探针，确认 R4/R5 的实际渲染缺口已消除，并复查 R1/R2/R3 的原始反例。构建 0 错误；5 项既有 ImageSharp 2.1.11 依赖告警仍保留。主代理证据：`artifacts/gs1-associations-final-review-20261024/build.log`、`probe-results.log`、`probe/`；具体命令和正反例见[复核记录](gs1-associations-2d-review-2026-10-23.md) 最终验收节。

最终候选的 Core 两个真实运行时各 4626/4626、Host 262/262、XML 五框架 10/10、受影响 E2E 9/9、模板案例 132 文件及候选包 net6.0 新 API 消费均通过。前端应用源码未改变，类型检查/构建、Vitest 412/412 复用首轮证据；未受影响的浏览器范围复用第一次修复后全量 192/192，新增 Composite 用例与受影响 GS1 二维用例在第二轮以 `--retries=0` 重新执行。该复用不代表执行了新的 193 条全量 E2E。

收尾修正开发手册两条 `dotnet test --filter` 命令的引号，并由 PowerShell 解析器确认没有意外管道。5 份收尾文档 UTF-8 解码通过，86 个本地链接全部存在，`git diff --check` 通过，跟踪 XML 无差异，暂存区为空；没有提交、推送或发布。

| 状态 | 事项与处理 |
| --- | --- |
| CLOSED | 本阶段 G1—G5、集成问题 R1—R6，已有实现、真实链路回归和独立探针证据。 |
| OPEN_ACTIONABLE | 0；本阶段停止，不自动扩展新的功能范围。 |
| ACCEPTED_LIMITATION | GS1 二维使用四种图片协议；ESC/POS Native 明确拒绝。关联校验以显式数据集合为作用域，Provider/模板工厂保留既有扩展边界。 |
| DEFERRED | ImageSharp 大版本迁移：保留现有五框架，剩余依赖告警不在本阶段消除。Digital Link、PDF/SVG、批量导入等需另行确定范围。 |
| EXCLUDED_BY_USER | 真实打印机验收按用户要求忽略；本报告只确认软件验证，不包含设备输出/扫码证据。 |
