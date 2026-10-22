# GS1-128 Composite CC-A/CC-B 实现与验收

日期：2026-10-22。基线为 `b11a730` 加上一阶段未提交的 Expanded 修正和 Stacked 实现；本轮保留既有变更。范围见[实施计划](../ai_docs/plans/gs1-128-ab-2026-10-22.md)，使用方法见[GS1-128 A/B 指南](gs1-composite-gs1-128-ab.md)。

## 实现结果

新增 `UccEan128` 与显式 `CcA`、`CcB` 配对，使用四列二维组件、与最终 B/C 码集相应的连接码字，以及按 Code 128 码字位置计算的横向对齐。正负偏移、分隔行、尺寸、静区和旋转都进入共享编码/光栅路径。显式模式超容量直接报错，GS1-128 `Auto` 保持 CC-C。保留五目标框架、公开成员签名和枚举值。

Host 追加 `gs1-composite-gs1-128-ab` 可选协商特性，HTTP 路由及 JSON 结构不变。设计器根据特性与能力数组启用 A/B，导入保留原模式；预览/导出带所需特性并校验响应。旧 Host 缺少此特性时 A/B 不可用，Auto/CC-C 仍按已协商能力工作。

## 功能与证据矩阵

| 维度 | 覆盖和结果 | 入口 |
| --- | --- | --- |
| 编码实现 | 9 组固定完整矩阵，含四列 A/B 公开例子与 7 组短主数据对齐边界 | [固定向量](../tests/Bing.EasyPrint.Tests/Fixtures/Gs1CompositeGs1128AbVectors.cs) |
| 接口及默认值 | A/B 显式模式、Auto=CC-C、括号等价；ZXing 独立解码 4 种主数据并检查末尾连接码字 | [编码测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeGs1128AbTests.cs) |
| 资源及几何 | A/B 四角旋转逐像素、静区、2X 行高；容量/预取消/内存失败后的预算释放与复用 | 同上；沿用共享取消和图片预算路径 |
| 模块归属与四协议 | 复用内部 Composite 编码器和图片输出；CPCL、TSPL、ZPL、ESC/POS Raster 与显式位图字节相等，越界失败后可重用 | [Provider 测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeProviderTests.cs) |
| 模板与 Host | V2/V3 字段/模式/精确字节往返；三模式四协议预览/导出；A/B 容量错误保留 Secondary 字段及元素路径 | [模板测试](../tests/Bing.EasyPrint.Tests/Gs1CompositeTemplateTests.cs)、[Host 测试](../tests/DesignerHost.Tests/Gs1CompositeHostTests.cs) |
| 设计器 | 特性门控、模式选项、导入校验、请求及响应协商；真实 Host A/B 编辑、预览、CPCL 导出和项目保存重载；旧 Host 降级 | [单测](../web/Bing.EasyPrint.Designer/src/gs1Composite.test.ts)、[浏览器用例](../web/Bing.EasyPrint.Designer/e2e/ccC.spec.ts) |
| 案例/文档 | 两份新 JSON 与 C# 四协议输出一致；候选包消费显式 A/B；公共注释、README、开发及使用指南同步 | [案例](../samples/Templates/Gs1CompositeSamples.cs)、[包消费](../tests/PackageConsumer/Program.cs) |

参考固定 Zint 2.15.0 提交 `4896136bdbebb1a0908a5cc419513efa5bf4bf70` 的 `backend/composite.c`、`code128.c` 和 `backend/tests/test_composite.c`；新增向量保留上游 BSD 许可。固定矩阵由上游公开测试提取，并非使用本项目编码结果生成期望值。

## 验证记录

证据根目录为 [`artifacts/gs1-128-ab-20261022`](../artifacts/gs1-128-ab-20261022/)，完整后端命令与日志见 [final-backend.ps1](../artifacts/gs1-128-ab-20261022/final-backend.ps1)、[final-backend.log](../artifacts/gs1-128-ab-20261022/final-backend.log)。

| 命令/范围 | 框架 | 通过 | 失败 | 跳过 |
| --- | --- | ---: | ---: | ---: |
| 新 A/B 编码定向（实现后） | net6.0 | 20 | 0 | 0 |
| 全部 Composite 定向（修正测试前置后） | net6.0 | 225 | 0 | 0 |
| `Verify-XmlDocumentation.ps1`：Core、Templates 严格 XML 构建 | net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1 | 10 | 0 | 0 |
| `Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | 实际 .NET 6.0.36 | 3552 | 0 | 0 |
| 同上 | 实际 .NET Core 3.1.32 | 3552 | 0 | 0 |
| `Verify-DesignerHost.ps1` 默认内容机制 | net8.0 | 169 | 0 | 0 |
| `npm test` 完整 Vitest | Web | 308 | 0 | 0 |
| `npm run build`：vue-tsc 与生产构建 | Web | PASS | 0 | 0 |
| `npx playwright test e2e/ccC.spec.ts --workers=1 --retries=0`（修正前置后） | Chromium/真实 Host | 4 | 0 | 0 |
| `npx playwright test --workers=1 --retries=0 --reporter=json` 完整套件 | Chromium/真实 Host | 162 | 0 | 0 |
| `dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/gs1-128-ab-20261022/samples` | net6.0 | 20 组 Composite 四协议 C#/JSON/往返一致 | 0 | 0 |
| 当前源码 `dotnet pack` 两库后运行 `Verify-PackageConsumer.ps1 -TargetFramework net6.0` | net6.0 | PASS | 0 | 0 |
| README 快速示例独立执行 | net6.0 | PASS | 0 | 0 |
| 新指南 C# 代码块独立执行 | net6.0 | PASS，输出 2057 字节 | 0 | 0 |

严格 XML 输出在独立生成目录，结果见[构建证据](../artifacts/xml-documentation-verification/20261022-202251-733-c5b4ab33/verification.json)。两真实运行时的宿主路径、TRX 和计数见[运行时证据](../artifacts/gs1-128-ab-20261022/runtimes-final/20261022-122307-38d705a5/runtime-verification.json)；没有把 net7.0 编译通过写成 net7.0 运行通过。候选包仅保存在本地，消费脚本验证了实际选中的 net6.0 包资产，见[消费证据](../artifacts/gs1-128-ab-20261022/consumer-rc/consumer-net6.0/consumer-verification.json)。

首次结果保留：新增定向测试在实现前 20 项均因未支持配对失败；首次 Composite 扩大验证 224 通过、1 失败，是新增 Stacked 非法配对用例条高 20 先触发最低 24 点限制，改为合法条高 40 后检查不支持模式；首次浏览器定向 3 通过、1 失败，是用例错误期待切换制式覆盖已有 GTIN。现已先断言旧值保留，再输入括号式 AI。没有删除断言、跳过用例或开启重试掩盖失败，相关 before、composite、web-e2e-targeted 原日志均保留。

界面检查使用本项目 Playwright（当前未提供 Browser 插件工具），桌面 1440×900 和移动端 390×844 的 CC-B 属性、布局占位及预览正常；没有页面错误、控制台错误或框架错误浮层。见[界面检查记录](../artifacts/gs1-128-ab-20261022/visual-qa.json)。

## 剩余范围与停止条件

完整 Chromium 首次运行 162/162 通过，flaky=0，重试为 0；[汇总证据](../artifacts/gs1-128-ab-20261022/verification.json)保留逐项结果。ExpandedStacked、EAN/UPC Composite 仍属后续能力，不据本阶段结果声称全部制式已实现。完整 AI 业务关系和复杂文字支持不在本轮范围。

设备传输、完整协议插件扩展保持 ACCEPTED_LIMITATION；为保留旧目标框架，ImageSharp 大版本迁移保持 DEFERRED，既存 ImageSharp/旧测试依赖的 NuGet 告警未在本轮消除。Vite 的大型 chunk 告警保持记录。实机验收按用户指令为本轮 NOT_APPLICABLE，不安排、不作为本阶段阻塞，也不声称已通过。没有提交、推送或发布。


最终静态检查：当前工作区变更文件 UTF-8 解码、Markdown 本地文件链接和 `git diff --check` 通过；跟踪中的 XML 生成物没有变化，生成包、测试结果和截图未加入源码。见[静态检查证据](../artifacts/gs1-128-ab-20261022/static-checks.json)。上一阶段未提交的源码、测试及文档保留；本阶段计划 A1—A5 全部 CLOSED，OPEN_ACTIONABLE=0，停止本阶段修正。

| 后续事项 | 状态 |
| --- | --- |
| ExpandedStacked、EAN/UPC Composite | DEFERRED：待后续阶段实施，尚未实现 |
| 设备传输、完整协议插件扩展 | ACCEPTED_LIMITATION |
| 图片依赖大版本迁移 | DEFERRED：维持旧框架兼容，本轮未迁移 |
| 实机验收 | NOT_APPLICABLE：用户明确排除本轮范围 |
