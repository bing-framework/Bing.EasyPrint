# GS1 Composite CC-C 实施计划

用户于 2026-10-22 选择下一阶段实现 CC-C。基线 `b326b49`，开始时工作区干净；保留五个目标框架、既有成员签名与枚举数值，不提交或发布包。

## 范围与契约

- 新增 `Gs1CompositeMode.CcC = 3`；复用 `BarcodeType.UccEan128` 表达 GS1-128。
- DataBar 保持 Auto/CC-A/CC-B；GS1-128 支持 Auto/CC-C，Auto 直接选择 CC-C。其他配对明确拒绝。本轮不增加 GS1-128 的 A/B 或 Expanded/Stacked Composite。
- GS1-128 主数据使用共享严格括号 AI 解析器，规范化后最多 48 字符；次数据沿用解析器和容量校验。CC-C 二维行高为 3X，分隔行为 X；不内置 HRI，沿用静区与旋转。
- 新 Host 保留 `gs1-composite`，增加 `gs1-composite-cc-c` 协商特性；旧 Host 仍能编辑已有 DataBar Composite，CC-C 必须获得新特性和能力声明。
- 参考固定 Zint 2.15.0（4896136bdbebb1a0908a5cc419513efa5bf4bf70），保留来源与许可；测试使用独立完整模块矩阵。

## 执行清单

| 项目 | 验收 | 状态 |
| --- | --- | --- |
| C1 核心编码 | CC-C 容量/纠错/行标识、GS1-128 连接标志、完整独立向量、旧 A/B 不变 | CLOSED |
| C2 模板/Provider | JSON 往返、合法配对、四协议输出、尺寸/旋转/边界、取消和预算恢复 | CLOSED |
| C3 Host/设计器 | 能力与特性、合法编辑及导入导出、旧 Host 降级、真实预览 | CLOSED |
| C4 文档/案例 | 公共注释、使用手册、样例、开发验收与后续状态同步 | CLOSED |
| C5 集成验收 | 严格五框架构建、受影响核心/Host 单测、Web 类型/构建/单测/E2E、包消费 | CLOSED |

## 影响分析与验证

ChangedProjects: Core、Templates、DesignerHost、Web、对应测试和案例。
ChangedPublicContracts: 追加枚举值；保留原值和签名，新增可选 HTTP 特性，能力数组追加值。
ChangedRuntimePaths: Composite 编码、布局、光栅输出、浏览器能力协商。
ChangedProviders: 四协议共用 Composite 光栅路径；ESC/POS Native 继续不支持。
ChangedTFMs/BenchmarkHarness: 无；ChangedBuildPackaging: Core 包加入第三方许可声明；RiskLevel: HIGH（编码标准与跨层能力）。

先 L0/L1 独立向量与配对测试，再 L2 核心/Host/Web，阶段收口 L3/L4 多框架和消费/浏览器。沿用资源预算，不建立新的性能阈值或大型矩阵。证据写入 `artifacts/cc-c-20261022/`。

真实打印机扫描回读为 BLOCKED_EXTERNAL；图片依赖迁移为 DEFERRED；设备传输与完整 Provider 插件为 ACCEPTED_LIMITATION。只对本计划 OPEN_ACTIONABLE 继续实现。

## 收口

2026-10-22 本地实施 5/5 CLOSED，OPEN_ACTIONABLE=0。最终结果与复用范围见[阶段报告](../../docs/gs1-composite-cc-c-verification-2026-10-22.md)。核心两运行时各 3435、Host 148、Vitest 288、Chromium 156 项通过；严格五框架构建 10/10。最终复核发现的 Auto 容量诊断已先复现再修正，并刷新后端门禁。实机为 BLOCKED_EXTERNAL；其余范围边界保持，停止本轮实现。
