# 单行 GS1 DataBar Expanded Composite 实施计划

2026-10-22 根据“继续下一阶段”推进 CC-C 后续清单中的 Expanded。基线 `c230b9f`，工作区初始干净。保留五框架、既有公开签名和枚举值，不提交、推送或发布。

## 范围与契约

- 使用既有 `Gs1DataBarExpanded = 28`，支持 Auto/CC-A/CC-B，Auto 先 A 后 B；拒绝 CC-C。
- 主数据必须为括号 GS1 AI，复用现有 Expanded 压缩方法和容量校验。新增连接标志、分隔图案与横向对齐；不改变普通 Expanded 的历史输出。
- 模板复用 Type=11；四种图片输出协议支持，ESC/POS Native 不支持。行高、静区、旋转与现有 A/B 一致。
- Host 追加可选 `gs1-composite-expanded` 特性与线性类型；设计器兼容旧 A/B 和 CC-C Host，缺特性时明确禁用新选项。
- 本阶段仅单行 Expanded；Stacked、多行高度控制和其他线性组件另行实施。

## 清单

| 项目 | 验收 | 状态 |
| --- | --- | --- |
| E1 核心 | 固定 Zint 独立矩阵、连接标志、分隔和对齐、容量与资源恢复 | CLOSED |
| E2 模板/Provider | 四协议输出、V2/V3 往返、尺寸/旋转/错误诊断 | CLOSED |
| E3 Host/设计器 | 能力协商、编辑预览导出重载、旧 Host 降级 | CLOSED |
| E4 案例/文档 | C#/JSON 案例、包消费、中文公共注释及手册同步 | CLOSED |
| E5 集成 | 五框架严格 XML、真实运行时、Host、Web 和候选包 | CLOSED |

## 影响分析

ChangedProjects: Core、Templates、DesignerHost、Web、测试及案例。
ChangedPublicContracts: 既有枚举新增受支持配对，HTTP 可选特性和能力数组追加值；无签名、路由或枚举值变更。
ChangedRuntimePaths: Expanded Composite 编码、模板绑定、能力协商与编辑。
ChangedProviders: 复用四协议图片输出；ChangedTFMs/BenchmarkHarness/BuildPackaging: 无。
RiskLevel: HIGH（编码标准及跨层协商）。先独立模块向量和小范围测试，再受影响项目，最终一次完整阶段验收；不运行无关性能矩阵。

证据目录 `artifacts/composite-expanded-20261022/`。参考固定 Zint 2.15.0 commit `4896136bdbebb1a0908a5cc419513efa5bf4bf70` 的 rss.c/composite.c/test_composite.c，保留许可。

真实打印机字体/DPI/进纸/扫码/恢复验收为 BLOCKED_EXTERNAL；设备传输及完整插件扩展为 ACCEPTED_LIMITATION；图片依赖大版本迁移为 DEFERRED。只对本计划 OPEN_ACTIONABLE 继续修复。

## 实施证据

E1：设置链接标志、标准压缩方法选择和定位符；独立完整矩阵与解码通过。E2：四协议精确光栅对照、V2/V3 字段和字节往返、错误路径通过。E3：Host 增量协商与旧 Host 降级、真实浏览器编辑导出重载通过。E4：公开注释、C#/JSON 示例、README 和使用/开发文档同步，候选包消费通过。详细结果见[阶段报告](../../docs/gs1-composite-expanded-verification-2026-10-22.md)。

## 收口

E5：五框架严格 XML 构建 10/10，实际 .NET 6.0.36 与 3.1.32 各 3478/3478，Host 155/155，Vitest 294/294，Chromium 158/158（无重试、失败、跳过或 flaky），模板案例和本地包消费通过。5/5 CLOSED，OPEN_ACTIONABLE=0，停止本轮实现。实机仍 BLOCKED_EXTERNAL；其他边界与依赖告警见报告。
