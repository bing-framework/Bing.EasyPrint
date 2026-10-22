# ExpandedStacked 阶段修复与验收（2026-10-22）

本阶段补齐固定两列的 GS1 DataBar ExpandedStacked 普通条码与 Composite 链路，保留当前未提交内容、五框架和已有公共签名/枚举值。新枚举追加为 31。按用户要求不执行实机验收，未提交、推送或发布包。

## 修改与覆盖矩阵

| 能力 | 实现与契约 | 本次证据 |
| --- | --- | --- |
| 标准编码 | 复用 Expanded 方法选择、数据字符与校验字符；新增跨行填充、交替方向、短末行和三条分隔行 | `ExpandedStackedVectors` 的 14 组普通及 4 组 Composite 固定上游完整矩阵逐模块一致，覆盖 1–6 数据行 |
| 普通条码 | 仅 ESC/POS Raster；102 模块宽；Height 为每条数据行高度 | 四方向逐像素断言、实际宽高、精确页面边界、能力声明、Raster 命令 |
| Composite | CPCL/ESC Raster/TSPL/ZPL；Auto 先 A 后 B，显式 A/B，不接受 C | 固定 A/B 矩阵、Auto 容量转换、四协议与显式 Raster 输出一致、四方向及静区像素 |
| 错误与资源 | 未知 AI、错误 GTIN、超容量、非法模式/行高均拒绝；取消和预算异常后可重用 | 字段错误、取消传播、预算释放及后续成功输出断言 |
| 模板 | 普通 Type 4、Composite Type 11；V2/V3；按实际多行高度布局 | JSON 保存重载字节与布局一致，四协议案例比较 |
| Host/设计器 | 新增 `gs1-composite-expanded-stacked`，旧 Host 明确禁用；普通按能力数组开放 | Host 能力/预览/导出测试，前端契约和真实 Host 浏览器案例 |
| 文档与包 | 中文 XML、使用指南、开发手册、README、候选包消费 | 五框架严格 XML、指南/README 可运行示例、net6.0 包消费 |

实现位置：核心 [分行编码器](../src/Bing.EasyPrint/Protocols/EscPosRasterCanvas.ExpandedStacked.cs)、[Composite 编码器](../src/Bing.EasyPrint/Protocols/Gs1CompositeEncoder.cs)、[模板尺寸](../src/Bing.EasyPrint.Templates/Rendering/TemplateRenderer.cs)；测试入口为 [ExpandedStackedTests](../tests/Bing.EasyPrint.Tests/ExpandedStackedTests.cs)。[使用说明](gs1-composite-expanded-stacked.md)给出高度公式、输入边界、协商与可运行示例。

## 验证命令与结果

生成物目录为 `artifacts/expanded-stacked-20261022`，不覆盖历史阶段报告。最终状态 **PASS**；综合证据为该目录的 `verification.json`，完整浏览器最终报告为 `web-e2e-final-v2.json`。

| 验证 | 命令/入口 | 结果 |
| --- | --- | --- |
| 核心与模板五框架 | `./tools/Verify-XmlDocumentation.ps1` | Core/Templates × net7.0、net6.0、netcoreapp3.1、netstandard2.0、netstandard2.1，10/10 构建通过；缺失/无效 XML 诊断为零 |
| 两运行时完整核心测试 | `./tools/Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | 实际 .NET 6.0.36 和 3.1.32，各 3586 通过、0 失败、0 跳过 |
| Host 默认脚本 | `./tools/Verify-DesignerHost.ps1` | net8.0，173 通过、0 失败、0 跳过 |
| 设计器 | `npm test -- --reporter=json`、`npm run build` | Vitest 316/316，失败/跳过均为零；vue-tsc 类型检查及生产构建通过 |
| Chromium | `npx playwright test --workers=1 --retries=0 --reporter=json` | 165/165；失败、跳过、flaky 均为零；重试关闭 |
| 案例 | `dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/expanded-stacked-20261022/samples` | 6 变体 × 4 协议共 24 组 C#/JSON/往返输出一致 |
| 候选包 | 两库 `dotnet pack -c Release -m:1 -o <candidate>`，随后 `Verify-PackageConsumer.ps1 -CandidatePackagesDirectory <candidate> -TargetFramework net6.0` | 全流程 PASS；候选包仅本地生成 |
| 文档示例 | 将指南与 README 首个 C# 示例分别提取到临时消费项目执行 | 均通过；新指南输出 6040 字节 |

XML 证据：`artifacts/xml-documentation-verification/20261022-210210-665-c47c77f5/verification.json`。两运行时证据：`runtimes-final-v2/20261022-130424-c5c83e40/runtime-verification.json`；Host：`host-final/designer-host-net8.trx`；包消费：`consumer-rc/consumer-net6.0/consumer-verification.json`。

## 首次失败与修正记录

- 首轮定向测试因内部二进制编码方法新增布局参数，旧反射测试传参不足而失败；更新两处测试调用，原语义断言保留。
- 固定向量提取脚本首次使用系统默认 GBK 读取 UTF-8 上游文件失败，修正编码后生成；未使用本实现输出生成预期矩阵。
- 新普通模板测试首次未指定 ASCII 编码，在隔离执行下报缺少 GBK provider；测试显式使用 ASCII 后通过。
- 首次两运行时完整测试各 3584 通过、1 失败：旧 CPCL Helper 测试将枚举 31 视为越界。追加新制式后将越界值改为 32，并补 31 的已定义但未映射断言；修正后各 3586/3586 通过。保留 `final-backend.log` 和首次 TRX，没有把修正后的结果写回首次记录。
- 新浏览器用例首轮 Composite 与旧 Host 降级 2 项通过，普通条码 1 项超时：裸模板导入会恢复 CPCL，调整为导入后选择 Raster。下一次诊断发现 `getByLabel` 精确匹配不适用于该下拉框，改为按实际可访问角色/名称定位；保留操作与输出断言，未增加等待时间。一次路径错误启动的诊断运行已中止，不计为通过。
- 首次完整 Chromium 回归 163 通过、2 失败：旧 Expanded/Stacked Host 模拟移除了原制式，却保留新 ExpandedStacked，违反历史能力响应结构。同步移除新类型与特性，保留旧 Host 降级断言，不修改生产能力校验。首次报告保留为 `web-e2e-final.json`。
- 桌面 1440×900 与移动 390×844 可见新制式与 CC-B 参数，移动端无横向溢出；视觉验证无 console/page error。首次视觉助手执行时服务已退出，最终在完整 E2E 服务运行期间完成。截图保存在本地 visualizations 目录。

## 后续事项与边界

- 本轮 E1–E5 均为 `CLOSED`，状态见[实施清单](../ai_docs/plans/expanded-stacked-2026-10-22.md)，最终证据汇总于本阶段 `verification.json`。
- `OPEN_ACTIONABLE`（后续阶段）：EAN/UPC Composite、Truncated Composite、StackedOmnidirectional 普通与 Composite、实际条码位图预览、完整 GS1 AI 字典及跨字段业务校验、复杂字体排版。ExpandedStacked 的可配置列数/最大行数尚未提供。
- `ACCEPTED_LIMITATION`：设备传输/驱动及完整协议插件工厂扩展，保持既定范围。
- `DEFERRED`：ImageSharp 大版本迁移，保留现有五框架；2.1.11 的既有依赖安全告警仍存在，结构预检和本阶段条码实现不消除这些告警。
- `NOT_APPLICABLE`：实机验收，本轮按用户要求忽略。五框架构建不代表 .NET 7 的运行时测试，也不代表 Linux/macOS 字体验收。

新增能力固定为两列布局；完整项目仍有上述后续缺口，本阶段通过不等同于所有能力已经实现。
