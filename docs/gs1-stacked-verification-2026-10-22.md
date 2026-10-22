# Expanded 标准修正与 Stacked Composite 验收

日期：2026-10-22。基线 `b11a730`，开始时工作区干净。本轮完成普通 Expanded 编码修正，以及 DataBar Stacked Composite 的核心、模板、Host、设计器和案例链路；不代表全部 GS1 Composite 制式已实现。未提交、推送或发布包，保留原有五框架、公开成员签名、枚举数值及 HTTP 路由。

实施清单见[本阶段计划](../ai_docs/plans/gs1-stacked-and-expanded-correction-2026-10-22.md)，使用方法见[Stacked 手册](gs1-composite-stacked.md)。机器可读结果见[本次验证汇总](../artifacts/gs1-stacked-20261022/verification.json)。

## 实现结果与兼容性

1. **普通 Expanded 标准修正（CLOSED）**：五字符符号使用标准定位符表；压缩方法 3–14 不再重复写入隐含的 GTIN 首位 9。普通与 Composite 路径共享完整载荷的编码方法选择；非 9 前导、超出专用重量范围或固定重量/日期后仍有数据时使用通用编码。保留合法旧输入和 HRI，并支持重量后的括号 AI 尾部。兼容原始字段中的负号、字母和无效货币字段不会误入数字压缩。部分输出码图和宽度改变，已有固定布局应重新预览。
2. **Stacked Composite（CLOSED）**：既有 `Gs1DataBarStacked=30` 支持 Auto/CC-A/CC-B。加入二列 A/B 容量、纠错、连接标志、上下数据行、内外分隔及横向对齐。线性组件宽 50 模块，完整 Composite 宽 56 模块，不含额外静区。总条高至少为 `12X`，上行取 `floor(5H/12)`，下行取余量，两个分隔行各为 `X`。显式 A 超容量报错，Auto 可回退 B；取消、失败和预算不足后可继续使用。
3. **跨层接入（CLOSED）**：四种图片输出路径、模板 V2/V3、Host 预览与 ZIP 输出、设计器编辑/项目重载均接入。Host 增加可选 `gs1-composite-stacked` 特性，能力数组末尾追加 `Gs1DataBarStacked`；前端兼容原 A/B、CC-C 和 Expanded 三代响应，旧 Host 明确禁用新组合。
4. **文档与案例（CLOSED）**：新增 C#/JSON Stacked 案例、包消费检查和独立使用手册；同步 README、核心打印、模板、设计器、Host 和开发指南。补正 `SupportsGs1Composite` 范围及 `TemplateRenderOptions.Encoding` 默认值说明：ZPL 为 UTF-8，其他协议为 GBK。历史报告及其日期、当时结果保留。

## 功能覆盖矩阵

| 功能 | 实现与契约 | 验证证据 |
| --- | --- | --- |
| 普通 Expanded | [方法选择与位流](../src/Bing.EasyPrint/Protocols/EscPosRasterCanvas.cs)，公开签名不变 | [标准测试](../tests/Bing.EasyPrint.Tests/ExpandedStandardTests.cs)：4 组独立固定模块串、25 组独立解码、3 组原始字段边界；[原回归](../tests/Bing.EasyPrint.Tests/EscPosRasterGs1DataBarExpandedTests.cs) 97 项 |
| Stacked 编码 | [统一元素](../src/Bing.EasyPrint/Printing/PrintGs1CompositeElement.cs)、[编码与行高](../src/Bing.EasyPrint/Protocols/Gs1CompositeEncoder.cs) | [核心测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeStackedTests.cs) 16 项：5 组完整矩阵、Auto/容量/恢复、GTIN 与最小高度、5 组错误输入、4 个旋转角逐像素对照 |
| 独立标准来源 | 固定 Zint 2.15.0 commit `4896136bdbebb1a0908a5cc419513efa5bf4bf70` | [Stacked 向量及许可](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeStackedVectors.cs)，来自 [test_composite.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/tests/test_composite.c) 的 11/18/21/34/37；普通 Expanded 来自 [test_rss.c](https://github.com/zint/zint/blob/4896136bdbebb1a0908a5cc419513efa5bf4bf70/backend/tests/test_rss.c) 的 test_examples 1/10/11/15 |
| 四协议输出 | CPCL、TSPL、ZPL、ESC/POS Raster；Native 仍拒绝 Composite | [Provider 测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeProviderTests.cs)：与显式光栅字节比较，越界拒绝后恢复 |
| 模板 | Type 11，主次字段、模式、总条高与实际尺寸 | [模板测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeTemplateTests.cs)：V2/V3 序列化和打印字节往返，非法 CC-C 配对拒绝 |
| Host | 可选协商、实际尺寸、四协议 ZIP 输出 | [Host 测试](../tests/DesignerHost.Tests/Gs1CompositeHostTests.cs)，默认脚本全部通过 |
| 浏览器 | 创建 Stacked，修改模块宽/条高/次数据，预览、输出、保存和重新导入，旧 Host 降级 | [Stacked E2E](../web/Bing.EasyPrint.Designer/e2e/stacked.spec.ts)，宽度明确断言 `56 × ModuleWidth`；[前端契约测试](../web/Bing.EasyPrint.Designer/src/gs1Composite.test.ts) |
| 案例及包 | [C#](../samples/Templates/Gs1CompositeSamples.cs)、[JSON](../samples/Templates/gs1-composite-stacked.json)、[包消费者](../tests/PackageConsumer/Program.cs) | 三种 Composite 案例各四协议 C#/JSON/往返一致；本地候选包实际 .NET 6 消费通过 |

## 命令与结果

所有命令从仓库根目录执行；Web 命令在 `web/Bing.EasyPrint.Designer` 执行。普通构建设置 `DirectoryBuildTargetsPath` 指向[独立 XML 输出配置](../artifacts/gs1-stacked-20261022/Documentation.targets)，严格 XML 脚本自行建立独立目录，未覆盖跟踪中的 XML 文件。

| 检查与命令 | 本次结果（通过 / 失败 / 跳过） | 证据 |
| --- | --- | --- |
| `./tools/Verify-XmlDocumentation.ps1` | Core + Templates：10 / 0 / 0；net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1；无缺失/无效 XML 诊断 | [最终清单](../artifacts/xml-documentation-verification/20261022-194731-682-83aaaf20/verification.json) |
| `./tools/Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | 每框架 3529 / 0 / 0；实际运行时 .NET 6.0.36、3.1.32，无滚动替代 | [运行时清单](../artifacts/gs1-stacked-20261022/runtimes-final/20261022-114745-1f5be9e0/runtime-verification.json) |
| `./tools/Verify-DesignerHost.ps1 -ResultsDirectory artifacts/gs1-stacked-20261022/host-final` | net8.0：159 / 0 / 0；默认内容机制与现有生成物保留 | [TRX](../artifacts/gs1-stacked-20261022/host-final/designer-host-net8.trx) |
| `npm run build` | 类型检查及生产构建通过 | [日志](../artifacts/gs1-stacked-20261022/web-build.log) |
| `npm test -- --reporter=json --outputFile=../../artifacts/gs1-stacked-20261022/web-unit-final.json` | Vitest：301 / 0 / 0 | [JSON](../artifacts/gs1-stacked-20261022/web-unit-final.json) |
| `npx playwright test --workers=1 --retries=0 --reporter=json` | Chromium：160 / 0 / 0；flaky=0，首次完整运行全部通过 | [JSON](../artifacts/gs1-stacked-20261022/web-e2e-final.json)、[日志](../artifacts/gs1-stacked-20261022/web-e2e-final.log) |
| `dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/gs1-stacked-20261022/samples` | CC-C、Expanded、Stacked 共 12 组 C#/JSON/往返一致，既有案例同时执行通过 | [输出](../artifacts/gs1-stacked-20261022/samples)、[日志](../artifacts/gs1-stacked-20261022/final-backend.log) |
| README 首个 C# 快速示例、Stacked 手册 C# 示例分别提取为 net6.0 程序运行 | 均通过，Stacked 示例生成 1011 字节 | [README 运行](../artifacts/gs1-stacked-20261022/readme-smoke/run.log)、[Stacked 运行](../artifacts/gs1-stacked-20261022/docs-smoke/run.log) |
| 从源码分别 `dotnet pack` Core/Templates，再执行 `Verify-PackageConsumer.ps1 -CandidatePackagesDirectory artifacts/gs1-stacked-20261022/packages-rc2 -ResultsDirectory artifacts/gs1-stacked-20261022/consumer-rc2 -TargetFramework net6.0` | 两个本地候选包，消费者完整流程 PASS；未发布，版本未改 | [消费清单](../artifacts/gs1-stacked-20261022/consumer-rc2/consumer-net6.0/consumer-verification.json)、[完整命令及日志](../artifacts/gs1-stacked-20261022/final-backend-v2.log) |
| 桌面 1440×900、移动端 390×844 与静态检查 | 无控制台错误、框架错误覆盖层或横向溢出；差异、UTF-8 和链接检查通过 | [界面检查](../artifacts/gs1-stacked-20261022/visual-qa.json)、[静态检查](../artifacts/gs1-stacked-20261022/static-checks.json) |

Chromium 使用真实 Host `127.0.0.1:16187` 和 Vite `127.0.0.1:16173`，`CI=1`，不复用外部服务、不重试。当前会话无 Browser 插件，使用项目现有 Playwright。设计器预览仍是实际尺寸与布局占位，不表示在画布上显示可扫描的真实编码位图。

## 初始失败与证据复用

- [初始红测](../artifacts/gs1-stacked-20261022/red.trx)：28 项中 26 失败、2 通过，独立解码和新类型拒绝复现原缺口。
- 原 Expanded 回归最初有 10 项失败：错误历史码图、过窄的案例页面及旧重量范围断言。更新后的固定码图增加独立解码及公开参考向量约束，保留字段/HRI/字节/宽度断言；页面只调整案例尺寸，生产越界校验保留。100000 重量改为验证通用编码，非法数字仍保留拒绝测试。[断言修正记录](../artifacts/gs1-stacked-20261022/expanded-golden-corrections.json)。
- 新资源测试最初使用过大尺寸，先触发整数溢出而非预算限制；调整为超过预算且不溢出的输入后，仍断言预算异常、零残留与后续成功。没有删除、跳过或放宽断言。
- [定向 328 项通过](../artifacts/gs1-stacked-20261022/focused-green.trx)后，首轮全量两运行时各 3526 项通过。复核发现原始字段需要独立的数字格式判定，增加 3 项测试；[受影响 169 项通过](../artifacts/gs1-stacked-20261022/raw-fields-green.trx)，再完成最终 XML、两运行时各 3529 项、重新打包和消费。首次结果保留于 `final-backend.log`，最终结果为 `final-backend-v2.log`。
- Host、样例和 Chromium 的已有通过证据复用：末次修正仅影响普通 Expanded 的非数字原始字段压缩选择；这些 Stacked/Composite 端到端用例使用严格解析后的合法 AI，未改变 HTTP、Stacked 布局、前端或模板契约。未运行无关性能矩阵。

## 后续事项与停止条件

| 状态 | 事项 |
| --- | --- |
| CLOSED | 本计划 S1–S5；普通 Expanded 标准修正、Stacked Composite 全链路、相应案例及当前文档 |
| DEFERRED | Expanded Stacked、GS1-128 + CC-A/CC-B、EAN/UPC Composite 为后续制式阶段，尚未实现 |
| DEFERRED | 完整 AI 字典及跨 AI 业务关系、复杂文字塑形/双向布局，需独立确定输入与验收范围 |
| BLOCKED_EXTERNAL | 实机扫码、字体、DPI、介质进纸、切纸和中断恢复；需目标机型、固件、介质及扫描器。Linux/macOS 字库与运行时、缺少的 .NET 7 执行环境也未由本机结果关闭 |
| ACCEPTED_LIMITATION | 设备连接/传输/驱动/队列/状态管理和完整协议插件体系不在当前产品范围；模板工厂仍有内置 Provider 边界 |
| ACCEPTED_LIMITATION | ZXing 0.16.11 对 Alpha FNC1 后切换存在既有解码器限制，保留独立标准位流测试；不为适配解码器改坏生产输出，见 Expanded 历史报告 |
| DEFERRED | 图片依赖大版本迁移：保留旧框架，ImageSharp 2.1.11 的 NU1902/NU1903 告警仍存在；本轮不宣称消除依赖漏洞 |

构建还报告既有测试依赖/旧目标框架警告，以及前端超过 500 kB 的 chunk 提示。它们没有被忽略为“已修复”，也未触发无关迁移或性能矩阵。公共 XML 编译检查通过不等于仓库 C1–C7 注释计划全部完成。

本阶段 OPEN_ACTIONABLE=0，按 GOAL_RULES 停止修复循环。新增制式、外部设备与保留边界继续分别跟踪。
