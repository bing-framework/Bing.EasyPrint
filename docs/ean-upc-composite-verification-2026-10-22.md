# EAN/UPC Composite 阶段验收（2026-10-22）

本轮新增 EAN-8/EAN-13/UPC-A/UPC-E + Auto/CC-A/CC-B，覆盖核心编码、四协议图片输出、V2/V3 模板、Host 和设计器。公共成员签名、枚举值和 HTTP 字段结构保持兼容；Host 扩展能力数组与协商特性。不带附加码是本阶段范围，不能据此宣称全部 Composite 能力完成。

当前代码基于 `b11a730`，包含此前尚未提交的 GS1-128 A/B、Stacked、ExpandedStacked 等变更。本轮没有提交、推送、发布或修改版本。候选包只在本地消费，原有未提交文件与历史记录保留。

## 覆盖矩阵

| 能力 | 实现、契约与证据 | 结论 |
| --- | --- | --- |
| 四种线性组件与 CC-A/B | [内部零售编码](../src/Bing.EasyPrint/Protocols/Gs1CompositeEncoder.EanUpc.cs)、[19 组固定 Zint 矩阵](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeEanUpcVectors.cs)逐模块比较，含 EAN-8 A/B 不同宽度、UPC-E 号码系统与不同容量 | CLOSED |
| 数字、校验位与 UPC-E 零压缩 | 长度/ASCII/附加码/错误校验拒绝，6/7/8 位 UPC-E、0/1 系统、压缩分支回归 | CLOSED |
| 三条 2X 分隔及布局 | [核心专项](../tests/Bing.EasyPrint.Tests/Gs1CompositeEanUpcTests.cs)覆盖四角逐像素比对、非对称静区、实际尺寸、四协议精确贴边/裁切拒绝/失败后重用 | CLOSED |
| 取消、预算与输入错误 | 取消令牌传播、临时预算归零、限制拒绝、Auto 回退；既有 Composite 全套输出事务测试保留 | CLOSED |
| V2/V3 模板与错误归属 | 四类型 × 三模式 × 两版本 JSON 往返与编码尺寸一致；非法 CC-C 在预检拒绝；Host 保留字段/元素路径 | CLOSED |
| Host 与设计器 | `gs1-composite-ean-upc`、能力数组及请求/响应校验；旧 Host 禁用四类型并显示原因；四类型编辑预览导出保存重载 | CLOSED |
| 案例与包消费 | 四个新 JSON 案例，十种 Composite 案例共 40 组四协议 C#/JSON/往返一致；本地候选包核对来源及四制式 A/B/Auto 消费 | CLOSED |
| 手册和 XML | [专用指南](gs1-composite-ean-upc.md)、核心/模板/设计器/开发手册、README 与状态说明同步；缺失或无效 XML 为错误 | CLOSED |

## 验证记录

以下命令在仓库根目录运行，前端命令除外。原始日志保存在 [阶段证据目录](../artifacts/ean-upc-composite-20261022)，最终后端入口为 [final-backend.ps1](../artifacts/ean-upc-composite-20261022/final-backend.ps1)。验证时用独立 `DirectoryBuildTargetsPath` 保存 XML，未覆盖跟踪中的生成文档。

| 检查与命令 | 结果 | 证据 |
| --- | --- | --- |
| `tools/Verify-XmlDocumentation.ps1` | Core/Templates × net7.0/net6.0/netcoreapp3.1/netstandard2.0/netstandard2.1，10/10，零失败/跳过 | [严格 XML 清单](../artifacts/xml-documentation-verification/20261022-220445-454-fdaec05a/verification.json) |
| `tools/Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | 各 3650/3650，零失败/跳过；实际 .NET 6.0.36、3.1.32 | [运行时清单](../artifacts/ean-upc-composite-20261022/runtimes-final/20261022-140501-87c693cd/runtime-verification.json) |
| `tools/Verify-DesignerHost.ps1`（保留默认输出参数和现有生成目录） | net8.0，183/183，零失败/跳过 | [Host TRX](../artifacts/ean-upc-composite-20261022/host-final/designer-host-net8.trx) |
| 设计器目录 `npm test -- --reporter=json ...` | 351/351，零失败/跳过 | [Vitest JSON](../artifacts/ean-upc-composite-20261022/web-unit-final.json) |
| `npm run build` | vue-tsc 类型检查和 Vite 生产构建通过 | [构建日志](../artifacts/ean-upc-composite-20261022/web-build-final.log) |
| `npx playwright test --workers=1 --retries=0 --reporter=json` | 170/170，零失败/跳过/flaky/重试 | [Chromium 日志](../artifacts/ean-upc-composite-20261022/web-e2e-final.log) |
| `dotnet run --project samples/Templates/Templates.csproj -c Release -- <stage>/samples` | 十种 Composite × 四协议，40 组 C#/JSON/往返一致；原有模板案例继续通过 | [后端日志](../artifacts/ean-upc-composite-20261022/final-backend.log) |
| 两项目 `dotnet pack -c Release -m:1 -o <candidate>`；`tools/Verify-PackageConsumer.ps1 -CandidatePackagesDirectory <candidate> -TargetFramework net6.0` | PASS，候选包匹配，版本沿用 1.0.0，未发布 | [消费清单](../artifacts/ean-upc-composite-20261022/consumer-rc/consumer-net6.0/consumer-verification.json) |
| 从指南与 README 实际提取代码并 `dotnet run -c Release` | 分别输出 3660、14 字节，通过 | [指南日志](../artifacts/ean-upc-composite-20261022/docs-smoke.log)、[README 日志](../artifacts/ean-upc-composite-20261022/readme-smoke.log) |
| 真实 Host 桌面 1440×900 / 手机 390×844 可视验证 | 属性可用、无横向溢出、无页面/控制台错误；截图人工检查通过 | [可视检查](../artifacts/ean-upc-composite-20261022/visual-qa.json) |

测试前已在[实施计划](../ai_docs/plans/ean-upc-composite-2026-10-22.md)冻结影响面。L1 为 289 项 Composite 专项、9 项校验位补充及 56 项 Host 专项。最终阶段扩大到上述 L2/L3/L4，不运行 L5 大型性能矩阵。未受影响的 TIFF 隔离复现、ZPL 方向及 Host 内容排除专项沿用上一阶段证据；本轮完整回归继续覆盖相关共享路径。net7.0 为编译验证，未以其他运行时冒充执行通过。

## 首次失败与修正

- 核心首轮 48 通过、1 失败：[原始 TRX](../artifacts/ean-upc-composite-20261022/targeted-initial.trx)。新回退测试使用的 62 位混合字母数据超出 UPC-E 两列 CC-B 容量；换为已落在 CC-A/CC-B 容量之间的 70 位数字，再确认 Auto=CcB。未提高容量限制，生产编码未因该失败调整。
- 浏览器新增用例首轮 4 通过、1 失败：[原始 JSON](../artifacts/ean-upc-composite-20261022/web-e2e-targeted.json)、[错误上下文](../artifacts/ean-upc-composite-20261022/e2e-first-error.md)。旧 Host 正确返回缺失能力诊断，页面还显示错误码/阶段，测试的全文精确定位不成立；改为在“打印诊断”区域核对完整消息及错误码，保留禁用和请求特性断言。没有跳过用例、增加等待或开启自动重试。
- 文档独立对照发现 Host/开发手册详细段落漏列新增特性和四个类型，已同步。历史阶段报告计数不改写为本轮结果。

## 剩余事项与停止条件

- **CLOSED**：本轮 U1–U5 全部关闭，编码、共享输出、Host/设计器、案例与文档、最终验收完整闭环。
- **OPEN_ACTIONABLE**：0。
- **NOT_APPLICABLE**：实机验收（用户要求忽略）、新能力历史性能对比。
- **ACCEPTED_LIMITATION**：设备传输/驱动和完整协议插件工厂扩展，仍由宿主负责；不在本阶段新增。
- **DEFERRED**：EAN/UPC 的 2/5 位附加码、DataBar Truncated/StackedOmnidirectional Composite、ExpandedStacked 自定义列数、真实码图预览、完整 AI 字典及复杂字体排版，应按独立阶段推进。
- **DEFERRED**：图片依赖大版本升级，保留五框架兼容；ImageSharp 2.1.11 的五项既有依赖告警仍存在。旧框架 EOL、既有包消费 nullable 警告和 Vite 大 chunk 告警如实保留。
- **BLOCKED_APPROVAL/BLOCKED_EXTERNAL**：本轮没有要求继续等待的事项。跨 OS 字体和真实设备证据不在本次完成声明中。
- **VERIFIED_BOUNDARY**：超过编码容量或资源预算正确拒绝；不是放宽阈值的理由。

本轮最终状态：Implementation 5/5，Required Gates 11/11（上表十类及差异/编码/链接检查）。[统一证据清单](../artifacts/ean-upc-composite-20261022/verification.json)绑定本轮明确的原始结果。[静态检查](../artifacts/ean-upc-composite-20261022/static-checks.json)覆盖 86 个当前修改/未跟踪文件，未见无效 UTF-8、替换字符或失效本地链接，跟踪 XML 改动为零，`git diff --check` 通过。文档最终汇总不改变代码，复用此前同一代码候选的验收结果。

Round 2（实现及验收修正）已完成；No-Progress=CHANGED，IMPLEMENTATION_STATUS=PASS，TEST_STATUS=PASS，EXTERNAL_GATE_STATUS=NOT_APPLICABLE，RELEASE_STATUS=NOT_REQUESTED，GOAL_STATUS=COMPLETED，Next Action=STOP。无本轮 OPEN_ACTIONABLE，不因后续 DEFERRED 能力自动开启下一轮。
