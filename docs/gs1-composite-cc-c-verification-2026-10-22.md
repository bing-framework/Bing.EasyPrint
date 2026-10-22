# GS1 Composite CC-C 实施与验收（2026-10-22）

## 结果

CC-C 阶段的 5 项本地工作全部完成。基线为 `b326b4911f0b7e47978c465c29cbd7eb9cd37396`，开始时工作区干净；交付包含当前未提交代码、测试、案例和文档。未提交、推送或发布包。

新增 `Gs1CompositeMode.CcC = 3`，复用既有 `UccEan128` 线性制式；旧枚举数值、公开成员签名和五个目标框架保持。DataBar 继续支持 Auto/A/B；GS1-128 支持 Auto/C，Auto 固定选择 CC-C，容量不足时明确报告 CC-C 错误。跨组合不支持时快速拒绝。使用方式见[CC-C 使用手册](gs1-composite-cc-c.md)，实施范围见[计划](../ai_docs/plans/gs1-composite-cc-c-2026-10-22.md)。

## 修改和覆盖

| 范围 | 实现 | 验收依据 |
| --- | --- | --- |
| 核心编码 | GS1-128 B/C 选择、FNC1、连接标志、CC-C 字节压缩、纠错、行标识、列数及容量 | 8 组固定 Zint 完整矩阵；独立 Code128 解码器校验 B/C 尾态和 FNC1；保留原 A/B 6 组矩阵 |
| 图片与 Provider | 二维行高 3X、分隔行 X，整体静区和旋转，四协议共用图片输出 | 逐像素旋转/静区对照、四协议与显式光栅输出相等、越界及预算失败后恢复 |
| 模板 | V2/V3 Type 11、主次字段、模式配对、实际尺寸与 JSON 往返 | C#/JSON/重载输出一致，非法配对在绑定前拒绝；保留错误字段路径 |
| Host | 能力数组追加 GS1-128/CcC，增加 `gs1-composite-cc-c` 特性，保留基础特性 | 四协议预览/导出、错误结构、Auto 容量诊断；Native 仍不支持 Composite |
| 设计器 | 配对模式、导入校验、新特性请求、旧 Host 降级 | 真实 Host 编辑、CG 输出、项目往返；通过移除响应特性和能力模拟旧 Host，验证 DataBar 保留而 CC-C 禁用/拒绝 |
| 案例与文档 | 新增四协议 C#/JSON 案例、公共注释、使用页，更新 README 和开发/使用手册 | 严格 XML、可执行手册片段、示例程序、链接与编码检查 |
| 包 | 五框架候选包，附第三方来源许可 | 独立包消费通过；包内程序集对应 XML 同名，Core 包包含 ThirdPartyNotices.txt |

参考来源固定为 [Zint 2.15.0 Composite](https://github.com/zint/zint/blob/2.15.0/backend/composite.c)、[GS1-128](https://github.com/zint/zint/blob/2.15.0/backend/code128.c) 与[独立测试矩阵](https://github.com/zint/zint/blob/2.15.0/backend/tests/test_composite.c)，commit 为 `4896136bdbebb1a0908a5cc419513efa5bf4bf70`。生产运行不调用外部编码进程。对应许可保留于源码、向量及核心包声明。

## 本次验证

原始结果目录：`artifacts/cc-c-20261022/`；汇总为该目录的 `verification.json`。下表为最终有效证据，先前诊断结果保留，不覆盖或混称为首次通过。

| 检查 | 命令/入口 | 结果及证据 |
| --- | --- | --- |
| 五框架严格 XML 构建 | `./tools/Verify-XmlDocumentation.ps1` | 10/10 通过；`artifacts/xml-documentation-verification/20261022-165323-447-6ef94050/verification.json` |
| 核心 net6.0 | `Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | 3435 通过，0 失败/跳过；实际 .NET 6.0.36 |
| 核心 netcoreapp3.1 | 同上 | 3435 通过，0 失败/跳过；实际 .NET Core 3.1.32，不滚动到新运行时 |
| Host | `./tools/Verify-DesignerHost.ps1 -ResultsDirectory artifacts/cc-c-20261022/host-final` | 148 通过，0 失败/跳过；`host-final/designer-host-net8.trx` |
| 类型和生产构建 | `npm run build` | PASS；`web-build.log` |
| Vitest | `npm test -- --reporter=json --outputFile=…` | 288 通过，0 失败/跳过；`web-unit.json` |
| Chromium | `npx playwright test --workers=1 --retries=0 --reporter=json` | 156 通过，0 失败/跳过/flaky；`web-e2e-final.json` |
| 模板案例 | `dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/cc-c-20261022/samples` | PASS；新增四协议 CC-C C#/JSON/重载一致；`samples.log` |
| 手册 C# 片段 | 提取使用页代码块为独立 net6.0 项目运行 | PASS，生成 2633 字节；`docs-smoke.log` |
| 本地包消费 | 两库 Release pack 后 `Verify-PackageConsumer.ps1 -TargetFramework net6.0` | PASS；`consumer-rc/consumer-net6.0/consumer-verification.json`；新增四协议 CcC/Auto 消费断言 |
| 包内容 | 检查最终 `packages-rc` | 两包各五框架及匹配 XML；Core 许可声明存在；`package-contents.json` |
| 静态检查 | 差异、UTF-8、本地 Markdown 文件链接 | PASS；`static-checks.json`；跟踪中的 XML 未变化 |

最终后端验证的完整命令和结果保存在 `final-backend.ps1` 与 `final-backend.log`；运行时报告为 `runtimes-final/20261022-085337-0ae03519/runtime-verification.json`。

浏览器使用仓库已有 Playwright（当前没有 Browser 技能），Web 为 `http://127.0.0.1:16173`，Host 为 `http://127.0.0.1:16187`。在 1440×900 与 390×844 视口确认页面身份、非空内容、无框架覆盖层、无应用控制台错误/警告、属性可编辑、画布占位与移动属性面板；截图路径及结果见 `visual-qa.json`。画布依然是不可扫描的布局占位。

## 诊断与证据复用

实施中的初始断言曾误认为共享解析器校验 GTIN 校验位、模板结构错误统一包装为渲染异常；已按既有真实契约修正。新增浏览器测试补了准确可访问名称，并直接断言 option 的 DOM disabled 属性。包检查发现默认跨框架外层项目没有收集 Update 声明的许可项，改为显式 Include；XML 输出按框架目录隔离并保留程序集同名文件。

最终算法复核发现 GS1-128 Auto 超容量误走 CC-B 回退。回归先复现错误，再将回退限制为 DataBar；新增核心及 Host 断言后，刷新上述五框架构建、两运行时完整测试、Host 与包消费。该修正只影响原先失败的超容量诊断，前端源码、正常数据编码和案例输入均未变化，因此复用已通过的 Web、截图、案例与手册片段证据；没有重复大型无关验证。

## 当前状态与后续

- **CLOSED**：C1 核心、C2 模板/Provider、C3 Host/设计器、C4 文档/案例、C5 集成验收，5/5。
- **OPEN_ACTIONABLE**：无，本轮停止。
- **BLOCKED_EXTERNAL**：真实打印机与扫描器。需按型号/固件记录 DPI、纸宽、模块宽度、静区、行高、进纸/切纸、扫描回读及发送中断后的恢复结果。
- **ACCEPTED_LIMITATION**：GS1-128 Composite 仅 Auto/C；DataBar 仅 Auto/A/B；无内置 HRI；共享 AI 解析器是受限格式校验，不替代业务规则；设备传输和完整 Provider 插件体系不属于本轮。
- **DEFERRED**：Expanded/Stacked Composite、其他线性组件、图片依赖大版本迁移；保留五框架兼容，既有 NU1902/NU1903、NU5104 与旧框架支持告警仍存在。
- **NOT_APPLICABLE**：性能提升对比及大型资源矩阵；本轮不修改预算阈值、不宣称性能改善。
- **BLOCKED_APPROVAL / VERIFIED_BOUNDARY**：无新增事项。

`IMPLEMENTATION_STATUS=PASS`；`TEST_STATUS=PASS`；`EXTERNAL_GATE_STATUS=BLOCKED_EXTERNAL`。本地阶段完成，实机验收另行安排。
