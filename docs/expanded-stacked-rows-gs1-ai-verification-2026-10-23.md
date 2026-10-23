# ExpandedStacked 最大行数与完整 GS1 AI 字典验收（2026-10-23）

本轮接续 [ExpandedStacked 列数能力](expanded-stacked-columns-verification-2026-10-23.md)，实现最大数据行数配置和固定版本完整 GS1 AI 字典。工作区开始时保留既有列数/附加码阶段内容；未重置、清理、提交、推送、发布包或修改版本号。独立参考仍使用 Zint 2.15.0 提交 `4896136bdbebb1a0908a5cc419513efa5bf4bf70`；新行为由本轮独立矩阵和测试验证。计划见[清单](../ai_docs/plans/expanded-stacked-rows-gs1-ai-2026-10-23.md)。

## 实现与契约

- 普通 Type=4 / BarcodeType=31 与 Composite Type=11 / LinearType=31 新增 `ExpandedStackedRows` / `WithExpandedStackedRows`，范围 2–11 条**数据行**；三条行间分隔行不计入上限。省略两者仍为默认两列。
- 行数与显式列数互斥；同时配置在模型、模板 JSON、Host 请求和设计器中明确失败。最大行数是上限：默认两列不因设置上限而变窄；两列无法满足时才增加列数；超过 11 列或实际行数仍超限时编码前拒绝，不截断数据。
- 新增按行测量、Canvas 绘制和 `EscPosPrintCommand` 命令入口；模板、Provider、静区、旋转、Host 预览和设计器使用同一实际编码尺寸。
- Host 新增 `gs1-databar-expanded-stacked-rows`；顶层 `expandedStackedRows` 和 `gs1Composite.expandedStackedRows` 返回 `{minimum:2, maximum:11, default:null}`。旧 Host 默认布局仍可用，显式行数被保留并阻止预览/输出。
- GS1 括号 AI 解析切换到固定版本 GS1 Barcode Syntax Dictionary 提交 `d1702ffb2fa5060f8460f7d581b01e377deaa1d6`（2025-02-09，Apache-2.0），展开后 536 个已定义 AI。字典文件嵌入程序集，SHA-256 与测试绑定。
- 实现固定/变长、长度、组件、`N/X/Y/Z` 字符集、FNC1 边界及字典 Linter；未知 AI、非法字符、校验位、日期/时间、国家/货币代码、IBAN、优惠券、包装类型等有明确诊断。跨 AI `req`/`ex` 关联保留追溯但不做全量业务推断；具体码制容量仍由编码器校验。单个历史括号展示值继续走既有单元素协议兼容路径；多元素、扩展 AI 或显式规范化入口使用完整字典。

## 功能覆盖矩阵

| 功能 | 实现 | 契约与测试 | 案例与文档 |
| --- | --- | --- | --- |
| 普通最大行数 2–11 | 共享 Expanded 编码按上限解析列数 | 20 组独立 Zint 行数矩阵；边界、奇偶列、短末行、最大列数 | 普通最大行数 C#/JSON/往返；[指南](expanded-stacked-rows-and-gs1-ai.md) |
| Composite 最大行数 2–11 | 线性组件配置，CC-A/B 仍为四列 | 40 组独立 Zint 矩阵，覆盖 A/B、短/长主数据、2–11 行 | 四协议 C#/JSON/往返；设计器案例 |
| 互斥与失败原子性 | 模型/模板/Host/设计器统一拒绝 | 模型双方互斥、JSON 双方/类型/范围/小数/字符串、超容量、取消、资源释放 | 错误说明和更新方法 |
| 尺寸与跨层 | Provider、模板、静区、旋转共用编码 | 四协议×四旋转×静区、精确尺寸、HRI 邻接、Host 预览 | Host/开发/设计器手册 |
| 完整 AI 字典 | 嵌入固定提交与 536 AI | 字典 SHA-256、家族覆盖、代表性正反例、混合 AI/FNC1、未知 AI、跨码制 | AI 字典边界、更新方法 |
| 默认兼容 | 旧签名与 null 两列 | 既有 ExpandedStacked/列数/附加码测试；默认与显式两列字节相等 | 旧案例继续执行 |
| Host | 五模式范围与特性协商 | 新增 8 项 Host 测试 | Host/开发手册 |
| 设计器 | 行数编辑、互斥、项目往返、旧 Host 降级 | Vitest 与 Chromium E2E | 设计器手册 |
| 包消费 | 行数/字典新入口 | 本地候选包 net6.0 消费 | 消费脚本和开发手册 |

## 验证证据

所有本轮证据位于 `artifacts/expanded-stacked-rows-20261023`，汇总见 `verification.json`。按影响范围先定向后阶段验收，不运行大型性能矩阵；历史未受影响证据可复用。

| 验证 | 命令/框架 | 结果与证据 |
| --- | --- | --- |
| 行数核心矩阵 | `dotnet test ... --filter FullyQualifiedName~ExpandedStackedRowsTests` | 80 通过，0 失败/跳过；60 组 Zint 矩阵；`targeted/` 或测试输出 |
| AI 字典定向 | `--filter FullyQualifiedName~Gs1SyntaxDictionaryTests` | 38 通过；固定 SHA-256、536 AI、正反例、FNC1/混合 AI |
| 核心完整 net6.0 | `dotnet test ... -f net6.0` | 3997 通过，0 失败/跳过；`artifacts/protocol-runtime-verification/20261023-032540-874b766f/net6.0.trx` |
| 双运行时 | `Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | 各 3997 通过、0 失败/跳过；实际运行时 6.0.36 / 3.1.32；`artifacts/protocol-runtime-verification/20261023-032540-874b766f/runtime-verification.json` |
| 五框架严格 XML | `tools/Verify-XmlDocumentation.ps1` | 10/10 通过；`artifacts/xml-documentation-verification/20261023-112510-352-c3b04cb5/verification.json`；跟踪 XML 未修改 |
| Host 全集 | `tools/Verify-DesignerHost.ps1` 默认参数 | 220 通过，0 失败/跳过；`artifacts/designer-host-verification-tests/designer-host-net8.trx` |
| 前端单元 | `npm test` | 408 通过、0 失败/跳过；`web-unit.log` |
| 前端生产构建 | `npm run build` | vue-tsc 与 Vite 通过；`web-build.log` |
| Chromium 全集 | `npx playwright test --workers=1 --retries=0 --reporter=dot,json` | 183 通过，0 失败/跳过/flaky，重试关闭；`chromium-full-final.log`（首次 5 项旧 Host 模拟失败保留在 `chromium-full.log`） |
| 模板案例 | `dotnet run --project samples/Templates/Templates.csproj -c Release -- artifacts/expanded-stacked-rows-20261023/samples-final` | 新增普通行数与 Composite 行数 C#/JSON/往返通过；`samples-final.log` |
| 文档片段 | 新指南 C# 片段与字典入口 | 普通 596×76、Composite 输出、完整 AI 混合输入通过；`docs-smoke-final.log` |
| 候选包消费 | `dotnet pack` 两库 + `Verify-PackageConsumer.ps1 -TargetFramework net6.0` | 两包哈希匹配，net6.0 6.0.36 PASS；`consumer-rc-final/consumer-net6.0/consumer-verification.json` |
| 静态检查 | 链接、UTF-8、跟踪 XML、生成物 | 70 文件、328 本地 Markdown 链接，0 错误；`static-checks.json`；`git diff --check` 通过 |

## 首次失败及处理

1. `Gs1Scan4TransportTests` 首轮 12 项失败：字典引入后暴露出固定长度 AI 不应因字典 `*` 标记而插入 FNC1，以及 4325 的 `9999` 不是合法 HHMI。改为按实际总长度可变性决定 FNC1，并把测试时间改为合法 `2359`，新增非法 `9999` 覆盖；未放宽字典校验。
2. `ExpandedStandardTests`/`Gs1CompositeExpandedTests` 4 项反射调用失败：私有编码器新增最大行数参数，测试参数数量不匹配；补传 `null`，保留全部位序断言。
3. 行数首轮 10 项失败：初始实现允许最大行数把布局降到 1 列，偏离 Zint 默认两列语义；改为最小两列，并修正 Host 预览期望。
4. 包消费首轮失败：新增完整 AI 入口的页面宽度/高度不足；扩大测试页面，未修改编码器。
5. Chromium 首轮 183 中 5 项失败：旧 Host 模拟删除了列数能力但未删除新增行数字段，触发能力响应契约拒绝；同步删除行数字段与特性，保留原断言。定向 14 项通过后完整 183 项零重试通过。

## 边界与后续状态

| 状态 | 事项 |
| --- | --- |
| CLOSED | R1–R5：行数核心/跨层/Host/设计器、完整字典、案例文档、最终候选验收；证据见上表 |
| OPEN_ACTIONABLE | 本轮为 0，按停止条件结束 |
| ACCEPTED_LIMITATION | 跨 AI `req`/`ex` 业务关联不做全量推断；设备传输/驱动及完整 Provider 插件沿用既有边界；实机扫描语义需设备验收 |
| DEFERRED | 真实码图预览、复杂字体排版、图片依赖大版本迁移 |
| NOT_APPLICABLE | 实机验收（用户明确排除）、新增配置的历史性能对比 |

严格区分本地软件验证、外部设备证据与发布；本轮不发布包。历史报告保留其原日期与范围，未改写为当前结果。
