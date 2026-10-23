# ExpandedStacked 列数能力验收（2026-10-23）

本轮接续 [EAN/UPC 附加码阶段](ean-upc-addons-verification-2026-10-22.md)，实现普通 Raster 与 Composite 的 ExpandedStacked 自定义列数。基线为 `dc79eab9b27aaa53fd89f887b55613d777b64184` 加开始时的 44 个未提交文件；旧变更已在本地证据目录快照，未重置工作区、提交、推送或发布。执行计划见[清单](../ai_docs/plans/expanded-stacked-columns-2026-10-23.md)。

## 实现与契约

- 普通 Type=4 / BarcodeType=31：1–11 列，仅 ESC/POS Raster；Composite Type=11 / LinearType=31：2–11 列，四协议图片输出。
- 列数表示每行字符对数上限；默认两列。同步二进制补齐、行方向、短末行和三个分隔行布局，保留既有默认输出。
- 保留旧公共签名，新增不可变副本 `WithExpandedStackedColumns`、列数属性、直接 Raster 绘制和测量入口。副本保留坐标、静区、旋转、HRI 和诊断路径。
- 模板、预检、旋转、静区、Host 实际尺寸和输出共用编码结果；非法类型、范围及 JSON 隐式类型转换在输出前失败。
- Host 增加范围与 `gs1-databar-expanded-stacked-columns` 特性；旧 Host 仍可使用缺省两列，显式列数要求协商。设计器支持编辑及项目往返。

## 功能覆盖矩阵

| 功能 | 实现 | 契约与测试 | 案例与文档 |
| --- | --- | --- | --- |
| 普通列数 1–11 | 共享 Expanded 编码与行布局 | 28 组 Zint 全矩阵，奇偶列、单列、短末行、最大列数 | 普通单列 C#/JSON/往返；使用指南 |
| Composite 列数 2–11 | 线性组件配置，CC-A/B 仍为四列 | 40 组固定 Zint 独立生成全矩阵，十种列数×短/长输入×A/B | 四协议三列 C#/JSON/往返 |
| 尺寸与失败原子性 | Provider、模板和静区传递列数 | 精确边界、四协议×四旋转×四列数、HRI、邻接静区、取消、预算和恢复 | 新公共入口及测量示例 |
| 默认兼容 | 旧签名与 null 两列 | 既有 ExpandedStacked 测试、默认与显式两列字节相等 | 旧案例仍执行 |
| JSON 输入 | 结构校验与严格整数检查 | 小数/字符串/布尔/范围、其他制式、null、大小写别名重复 | JSON 样例与错误说明 |
| Host | 五模式范围与特性协商 | 13 项新增 Host 测试 | Host/开发手册 |
| 设计器 | 列数编辑、能力约束及项目往返 | Vitest 与 Chromium E2E | 设计器手册 |
| 包消费 | 新属性、测量、命令及模型入口 | 本地候选包 net6.0 消费 | 消费脚本与开发手册 |

## 验证证据

所有本地证据位于 `artifacts/expanded-stacked-columns-20261023`，汇总见该目录的 `verification.json`；验证按影响范围先定向后阶段完整验收，不运行大型性能矩阵。下表记录本次实际结果；首次失败单独保留。

| 验证 | 命令/框架 | 结果与证据 |
| --- | --- | --- |
| 编码及跨层定向 | `dotnet test ... -c Release -f net6.0 --filter FullyQualifiedName~ExpandedStackedColumnsTests` | 153 通过，0 失败/跳过；`targeted/columns-second.trx` |
| 原有两列回归 | `--filter FullyQualifiedName~ExpandedStackedTests` | 30 通过；`targeted/existing-expanded-stacked.trx` |
| 后补边界 | JSON/null、静区/HRI、结构校验 | `targeted/columns-boundaries.trx` 与 `columns-load-structure.trx`；最终全集包含修复 |
| Host 定向 | `net8.0 --filter FullyQualifiedName~column` | 13 通过；`host-targeted/columns-first.trx` |
| 五框架严格 XML | `tools/Verify-XmlDocumentation.ps1` | 10/10 通过，缺失及无效 XML 诊断为零；`artifacts/xml-documentation-verification/20261023-005347-747-cd080874/verification.json` |
| 双运行时全集 | `Verify-ProtocolRuntimes.ps1 -Frameworks net6.0,netcoreapp3.1` | 各 3877 通过、0 失败/跳过；实际运行时 6.0.36 / 3.1.32，未滚动；`runtimes-final-confirmed/20261022-165945-ced72736/runtime-verification.json` |
| Host 全集 | `tools/Verify-DesignerHost.ps1` 默认参数 | 212 通过，0 失败/跳过；保留生成物且使用默认内容收集，`host-final/designer-host-net8.trx` |
| 前端单元与构建 | `npm test`、`npm run build` | Vitest 399 通过、0 失败/跳过；vue-tsc/Vite 通过，`web-unit-final.json`、`web-build-final.log` |
| Chromium 全集 | `npx playwright test --workers=1 --retries=0 --reporter=dot,json` | 180 通过，0 失败/跳过/flaky，重试关闭，耗时 247.6 秒；`web-e2e-final-confirmed.json` |
| 案例与候选包 | 模板案例、两库 `dotnet pack`、`Verify-PackageConsumer.ps1 -TargetFramework net6.0` | 17 类 Composite×4 协议，加普通单列 1 组，共 69 组 C#/JSON/往返通过；两个本地 1.0.0 候选包消费 PASS，`samples/`、`packages-rc/`、`consumer-rc/consumer-net6.0/consumer-verification.json` |
| 实际文档片段 | 新指南与 README 首个 C# 代码块 | 新指南测量 302×76、输出 3476 字节；README 输出 14 字节，`docs-smoke.log`、`readme-smoke.log` |
| 视觉检查 | 1440×900 桌面与 390×844 移动视口 | 列数控件可见、无水平溢出、无页面/控制台错误；`visual-qa.json`，截图在本地可视化目录 |
| 工作区静态检查 | 链接、UTF-8、差异及生成物 | 58 个阶段变更文件、307 个本地 Markdown 链接，0 错误；无 BOM 改变、原文件缺失或跟踪 XML 改动，`static-checks.json`；`git diff --check` 通过 |

## 首次失败及处理

1. `targeted/columns-first.trx`：113 项中 10 失败。六组上游普通向量含当前括号解析器未支持的额外 AI 17，测试改用已有固定长度字段的规范化原始输入入口；四组命令对比遗漏 `Initialize()`，补齐后保持完整字节断言。未修改矩阵或编码器来迁就期望。随后包含新增 Composite 向量的 153 项全通过。
2. `targeted/columns-boundaries.trx`：9 项中 1 失败。发现普通列数校验放在协议渲染分支，独立 `LoadJson` 会遗漏其他制式上的配置。将校验移至 `TemplateGraph` 结构校验；`columns-load-structure.trx` 确认通过。
3. `runtimes-final/20261022-165405-7c67925d`：两运行时各 3873 通过、4 失败、0 跳过。原因是两处旧测试以反射调用私有编码器，未传新增内部列数参数；已补传默认 2，保留全部位序和内容断言。公共方法签名未变；严格 XML 证据不受测试调用修改影响。
4. 浏览器新增用例首轮三项未等到成功预览：测试把普通 Type31 导入后的 CPCL 阶段当作可预览模式，并存在 Composite 测试数据/等待条件问题。第二轮 Composite/旧 Host 通过，普通用例的期望尺寸遗漏默认左右各 10X 静区；按含静区 244×117 → 342×76 更正后通过。最终新增三项 3/3 通过，`web-e2e-frontend-final.json`；没有增加超时或启用重试。
5. 首次完整 Chromium 为 175 通过、5 失败，`web-e2e-final.json`。五个旧 Host 模拟响应移除了 ExpandedStacked 线性类型但保留真实 Host 新增的列数范围，触发正常的契约拒绝。模拟响应同步移除新范围与特性，保留原断言；`web-e2e-host-compat.json` 五项定向确认通过，最终完整结果另列。

## 边界与后续状态

本轮仅解决列数能力。保留五目标框架和原有依赖，ImageSharp 2.1.11 及旧测试运行时包的已知告警仍存在；不宣称消除依赖风险。Vite 大包提示和现有包消费空值告警按原有边界记录。

| 状态 | 事项 |
| --- | --- |
| CLOSED | C1–C5：编码、跨层传播、Host/设计器、案例和文档、最终候选验收；证据见上表 |
| OPEN_ACTIONABLE | 本阶段为 0，按停止条件结束；后续能力保持独立范围 |
| ACCEPTED_LIMITATION | 设备传输/驱动、完整 Provider 工厂插件；应用继续管理传输，自定义 Provider 沿用现有模型入口 |
| DEFERRED | 真实条码位图预览、最大行数配置、完整 GS1 AI 字典、复杂字体排版，分别作为后续能力任务；本次列数能力不依赖它们 |
| DEFERRED | 图片依赖大版本迁移：为保持旧目标框架，另行规划兼容迁移并处理剩余告警 |
| NOT_APPLICABLE | 实机验收（用户明确排除）、新列数能力的历史性能对比 |

严格区分本地软件验证、外部设备证据与发布；本轮不发布包。
