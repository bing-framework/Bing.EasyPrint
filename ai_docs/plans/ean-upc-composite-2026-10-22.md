# EAN/UPC Composite 实施计划（2026-10-22）

继续现有缺口阶段，保留所有未提交内容、五框架及公共 API 签名。不提交、推送或发布，按用户要求不进行实机验收。

## 冻结范围与契约

- 使用既有枚举 Ean8=4、Ean13=5、UpcA=6、UpcE=7，新增 Composite Auto/CC-A/CC-B 组合；CC-C 明确拒绝。
- EAN-8 接受 7/8 位、EAN-13 接受 12/13 位、UPC-A 接受 11/12 位；完整输入校验校验位。
- UPC-E 接受 6/7/8 位，6 位隐含号码系统 0，7 位含号码系统，8 位另含校验位；号码系统仅 0/1，并验证零压缩规则。
- 输入限 ASCII 数字，不支持附加码、括号 AI、截断或静默补零。
- 三条分隔行各为 2X，线性条高仍以 dot 表示且不含分隔行；二维列数 EAN-8=3、EAN-13/UPC-A=4、UPC-E=2。
- Host 追加 `gs1-composite-ean-upc`，旧 Host 明确拒绝新组合。四种图片协议支持，ESC/POS Native 不支持。

## 影响与验证

ChangedProjects：核心库、模板库（契约文档）、Host、设计器、测试、案例。ChangedPublicContracts：扩大既有参数支持范围，不改签名/枚举/HTTP结构。ChangedRuntimePaths：Composite 输入规范化、编码、分隔/尺寸/旋转/预算、协商。ChangedProviders：四图片协议共享路径。ChangedTFMs/BuildPackaging/BenchmarkHarness：无。RiskLevel：HIGH（标准码图与跨层契约）。

先固定 Zint 2.15.0 的独立向量并运行局部回归，再执行最终五框架严格 XML、双运行时核心测试、Host、前端构建/Vitest/Chromium（零重试）、模板与候选包消费。复用未受影响图片解析等专项证据，不跑大型性能矩阵。

| 项目 | 状态 | 验收 |
| --- | --- | --- |
| U1 编码与输入校验 | CLOSED | 四制式 A/B 独立码图、补校验/校验拒绝、UPC-E 压缩规则 |
| U2 共享输出契约 | CLOSED | 三条 2X 分隔、尺寸/旋转/静区、预算/取消/恢复、四协议/模板 |
| U3 Host 与设计器 | CLOSED | 新特性、编辑预览导出保存重载、旧 Host 降级 |
| U4 案例与文档 | CLOSED | C#/JSON/往返/包消费、中文 XML、指南/README 同步 |
| U5 最终验收 | CLOSED | 完整检查及中文报告与证据 |

证据目录：`artifacts/ean-upc-composite-20261022`。主代理负责编码和整合；luna_worker 只拥有前端及其测试。

## 其他状态

- NOT_APPLICABLE：实机验收（用户明确排除）、新能力历史 before 对比。
- ACCEPTED_LIMITATION：设备传输/驱动、完整插件工厂扩展。
- DEFERRED：图片依赖大版本迁移（旧框架兼容）；附加码、剩余 DataBar Composite、真实条码预览、完整 AI 字典及复杂字体排版进入后续阶段。
- BLOCKED_APPROVAL/BLOCKED_EXTERNAL/VERIFIED_BOUNDARY：本阶段暂无。

## 实施摘要与最终证据

U1：新增内部 EAN/UPC 数字规范化、校验与分隔布局，复用已有 ZXing 与托管 CC-A/B；19 组独立标准矩阵通过。U2：共享实际尺寸与行高接入四协议/V2/V3，覆盖像素、旋转、静区、预算、取消、边界与恢复。U3：Host/设计器新特性、输入/模式校验及旧 Host 降级，新增五项浏览器案例。U4：四个零售 JSON、C# 驱动、候选包消费及当前手册同步。U5：最终五框架 XML 10/10；核心两运行时各 3650/3650；Host 183/183；Vitest 351/351；Chromium 170/170（零重试）；40 组模板往返和候选包消费通过。

详见[阶段报告](../../docs/ean-upc-composite-verification-2026-10-22.md)、[统一证据](../../artifacts/ean-upc-composite-20261022/verification.json)和[使用指南](../../docs/gs1-composite-ean-upc.md)。首轮测试数据容量与浏览器诊断定位失败均保留记录，修正原因与最终证据明确区分。

Completed：U1–U5（5/5）。Open Actionable：0。Blocked Approval/External：无。Not Applicable/Accepted Limitations/Deferred：沿用上文分类。Verified Boundary：容量与预算超限正确拒绝。No-Progress：CHANGED。Next Action：STOP。Goal Status：COMPLETED。
