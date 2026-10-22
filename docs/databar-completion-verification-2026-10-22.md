# DataBar 补齐阶段验收（2026-10-22）

本轮接续 EAN/UPC 阶段，补齐 Truncated Composite、Stacked Omnidirectional Composite 及普通 ESC/POS Raster Stacked Omnidirectional。沿用当前未提交工作区，未提交、推送或发布，未变更包版本及五目标框架。实机验收按用户要求排除。

实施范围和影响分析见[计划](../ai_docs/plans/databar-completion-2026-10-22.md)，输入、尺寸、示例与限制见[使用说明](gs1-composite-databar-completion.md)。本报告独立记录本轮结果，历史报告的范围和计数不变。

## 实现与覆盖矩阵

| 能力 | 实现与契约 | 核心/模板验证 | Host/浏览器/案例 |
| --- | --- | --- | --- |
| Truncated Composite | 既有枚举 26；Auto/A/B；100X 宽、H>=13X | 独立 Omnidirectional 完整码图、同形连接位、最低条高、容量/取消/预算 | 四协议预览/导出，V2/V3 往返，独立 JSON/C# 案例 |
| Stacked Omnidirectional | 新增枚举 32；普通 Raster 50X，Composite 56X；每行 H>=33X | 7 普通+5 Composite 固定向量；finder=3、三个分隔、连接位与 A/B 对齐 | 普通 Raster 能力，四协议 Composite，编辑/保存重载 |
| 尺寸与输出 | 两等高数据行；旋转、静区及普通 HRI 一起变换 | 四方向逐像素、精确边界/越界、Provider 图片等价与失败恢复 | Host 返回实际尺寸，模板使用完整高度 |
| 模式与输入 | GTIN 三种形式；CC-C 拒绝；普通32仅13位 payload | 校验位、非法主数据、容量、最小条高、溢出 | 结构化字段/路径诊断、旧 Host 缺特性降级 |
| 协商与兼容 | 新增两个独立 feature，能力数组尾部追加；既有枚举/签名/HTTP 路由不变 | Native/其他普通 Provider 拒绝32，Composite沿四协议图片路径 | 老数组仍识别，不能静默启用新组合 |
| 资源及依赖 | 临时预算在异常/取消后释放，不返回部分页 | 小预算拒绝、取消传播与恢复 | 五框架保留；图片依赖告警单列 |

编码及布局位于[核心实现](../src/Bing.EasyPrint/Protocols/EscPosRasterCanvas.StackedOmnidirectional.cs)和[Composite 编码器](../src/Bing.EasyPrint/Protocols/Gs1CompositeEncoder.cs)，不引入运行时外部编码器。主要断言位于[DataBarCompletionTests](../tests/Bing.EasyPrint.Tests/DataBarCompletionTests.cs)、[Provider 回归](../tests/Bing.EasyPrint.Tests/Gs1CompositeProviderTests.cs)、[模板回归](../tests/Bing.EasyPrint.Tests/Gs1CompositeTemplateTests.cs)及[Host 契约](../tests/DesignerHost.Tests/Gs1CompositeHostTests.cs)。

## 本次验证

证据目录：[artifacts/databar-completion-20261022](../artifacts/databar-completion-20261022)。正式后端入口：[final-backend.ps1](../artifacts/databar-completion-20261022/final-backend.ps1)。构建使用独立 `Documentation.targets` 输出 XML，避免改写跟踪中的文档生成物。

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| 核心定向：net6.0，DataBarCompletion/Gs1Composite | 326 通过，0 失败/跳过 | [TRX](../artifacts/databar-completion-20261022/targeted-final.trx) |
| Host 定向：Gs1CompositeHostTests | 66 通过，0 失败/跳过 | [TRX](../artifacts/databar-completion-20261022/host-targeted.trx) |
| `Verify-XmlDocumentation.ps1` | Core/Templates × 五框架 10/10；无缺失或无效 XML 诊断 | [清单](../artifacts/xml-documentation-verification/20261022-224959-458-8abbcc1a/verification.json) |
| `Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | 各 3685 通过，0 失败/跳过；实际 6.0.36 / 3.1.32 | [运行时清单](../artifacts/databar-completion-20261022/runtimes-final/20261022-143849-5c0f26f8/runtime-verification.json) |
| `Verify-DesignerHost.ps1` 默认命令 | 193 通过，0 失败/跳过；保留现有生成物 | [TRX](../artifacts/databar-completion-20261022/host-final/designer-host-net8.trx) |
| 模板案例 | 12 种 Composite × 四协议 = 48 组 C#/JSON/往返输出一致 | [后端日志](../artifacts/databar-completion-20261022/backend-final.log) |
| 两包源码候选打包 + `Verify-PackageConsumer.ps1 -TargetFramework net6.0` | PASS；包含新32普通条码及两种 Composite A/B/Auto | [消费清单](../artifacts/databar-completion-20261022/consumer-final/consumer-net6.0/consumer-verification.json) |
| 实际手册/README C# 片段 | 2812 / 14 字节，PASS | [手册日志](../artifacts/databar-completion-20261022/docs-smoke.log)、[README 日志](../artifacts/databar-completion-20261022/readme-smoke.log) |
| `npm test -- --reporter=json` | 358 通过，0失败/跳过 | [Vitest](../artifacts/databar-completion-20261022/web-unit-final.json) |
| `npm run build` | 类型检查及生产构建PASS | [日志](../artifacts/databar-completion-20261022/web-build-final.log) |
| Chromium完整E2E，`--workers=1 --retries=0` | 174通过，0失败/跳过/flaky，零重试 | [最终日志](../artifacts/databar-completion-20261022/web-e2e-final.log) |
| 桌面/移动视觉检查 | 无控制台错误、无横向溢出，检查两个视口 | [视觉记录](../artifacts/databar-completion-20261022/visual-qa.json) |
| UTF-8、文档相对链接、差异与生成物 | PASS；无跟踪XML变更 | [静态结果](../artifacts/databar-completion-20261022/static-checks.json) |

前端 Vitest 为 358 通过、0 失败/跳过；`npm run build`（含 vue-tsc）通过。Vite 保留单包约731.21kB的体积告警。桌面1440×900与移动390×844截图已检查，无脚本/控制台错误或横向溢出，画布仍为布局占位。Chromium 最终完整174项全部通过（[JSON结果](../artifacts/databar-completion-20261022/web-e2e-final.json)），retries=0、flaky=0、skip=0。差异、UTF-8与本地文档链接检查通过；跟踪中的XML没有变化。

完整后端验收后仅补充了两处公共 XML 说明（能力属性改为引用完整契约、Truncated 枚举说明追加 Composite）。重新执行严格 XML 10/10，并重新生成 `packages-final`、通过 `consumer-final`；运行时逻辑未变更，复用本轮完整核心/Host结果。

首次定向核心测试为 323 通过、3 失败：旧构造测试将现已支持的 Truncated 作为不支持案例，已改用仍不支持的 Code39；两个新模板测试沿用不足的旧页高，改用可容纳完整符号的 300 点页面。生产边界校验保持不变，修正后 326/326 通过。初次记录保留为 [targeted-initial.trx](../artifacts/databar-completion-20261022/targeted-initial.trx)，不将其隐藏为首次成功。

首轮浏览器定向执行 4 项，2 通过、2 失败：旧 Host 导入后未选择树元素，导致属性控件不存在；普通条码启用 HRI 但 fixture 未提供预留高度，Host 正确拒绝。修正交互和 fixture 后保留所有能力/尺寸/往返断言。随后全量收集发现旧 Host mock 重复声明 `body`，并顺带纠正普通条码能力过滤（只删除新增32，不删除已有EAN/UPC和26）。记录保留在 [first-run-errors](../artifacts/databar-completion-20261022/first-run-errors) 和 [收集失败日志](../artifacts/databar-completion-20261022/web-e2e-collection-first.log)。修正后 `playwright test --list` 收集174项，未增加超时、跳过或自动重试。

首轮完整执行结果为172通过、2失败（[完整记录](../artifacts/databar-completion-20261022/web-e2e-full-first.json)）：降级诊断应断言中文错误及 ContractFeatureUnavailable，同时在请求头断言内部 feature；普通 HRI 的12点预留不足以容纳实际墨迹，调整为已有Raster案例使用的20点。随后定向执行3通过、1失败（[记录](../artifacts/databar-completion-20261022/web-e2e-targeted-second.json)）：原宽度断言遗漏模板默认左右各10X静区，修正为140点并独立断言扣除静区后本体50X。生产布局和字形校验未放宽。每次失败均有新根因证据，不提高等待时间或跳过断言。

普通条码后续两次定向各为3通过/1失败，分别定位到：原始模板JSON不携带协议，重新导入恢复默认CPCL，测试须显式再选Raster；末尾制式控件应复用已成功使用的可访问combobox定位，而非getByLabel。已修正并保留尺寸、协议、字段及往返等价断言，证据为 [第三次](../artifacts/databar-completion-20261022/web-e2e-targeted-third.json) 和 [第四次](../artifacts/databar-completion-20261022/web-e2e-targeted-fourth.json)。

## 后续事项与停止条件

- `DEFERRED`：EAN/UPC 2/5 位附加码、ExpandedStacked 自定义列数、设计器真实码图预览、完整 GS1 AI 字典、复杂字体排版。这些是后续能力，未纳入本轮固定范围。
- `DEFERRED`：图片依赖大版本迁移。为兼容五框架保留 ImageSharp 2.1.11，其既有五项安全告警仍存在；本轮未宣称消除。旧运行时/EOL 与测试依赖告警、包消费原有 nullable 警告也保留。
- `ACCEPTED_LIMITATION`：设备传输/驱动和完整协议插件工厂扩展；当前 Provider 可接统一模型，模板工厂与部分收尾仍有内置实现边界。
- `NOT_APPLICABLE`：实机验收、无关大型性能矩阵；未将编码向量和浏览器通过视为设备扫码证据。
- 五目标框架编译不等于在五种运行时执行。实际运行验证仅使用本机 6.0.36 与 3.1.32，无运行时滚动替代。

本轮D1–D5均为`CLOSED`，`OPEN_ACTIONABLE=0`，无审批或外部阻塞。停止扩展范围；后续能力按独立阶段推进。
