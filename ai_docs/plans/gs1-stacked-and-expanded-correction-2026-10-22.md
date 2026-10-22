# Expanded 标准修正与 Stacked Composite 实施计划

2026-10-22 按“继续实现，缺口能力”推进审查中可直接实现的两项编码缺口。基线 `b11a730`，起始工作区干净。保留五个目标框架、公开成员签名和枚举值；不提交、推送或发布。

## 当前阶段

| 编号 | 内容与验收 | 状态 |
| --- | --- | --- |
| S1 | 普通 Expanded 与 Composite 共享标准方法选择，修正五字符定位符及压缩 GTIN 位流；独立解码与固定参考向量验证 | CLOSED |
| S2 | 使用既有 Gs1DataBarStacked=30，增加 Composite Auto/CC-A/CC-B、二列组件、连接标志、分隔和 5:7 条高；取消、预算和失败恢复 | CLOSED |
| S3 | 四协议图片输出、模板往返、Host 可选能力协商、设计器编辑/预览/导出/保存重载及旧 Host 降级 | CLOSED |
| S4 | C#/JSON 案例、包消费、中文注释、使用/开发文档和 README 同步；修正本次已确认的陈旧注释 | CLOSED |
| S5 | 小范围回归后一次最终五框架 XML 构建、两真实运行时、Host、Web、案例和候选包验收 | CLOSED |

Stacked 的 LinearBarHeight 表示两条线性数据行的总高度（不含内部一模块分隔行），按 5:7 比例分配整数打印点；不足 12 倍模块宽度时明确拒绝。保持其他制式既有尺寸语义。二维组件各行仍为 2 倍模块宽度，Composite 分隔行与 Stacked 内部分隔行各为 1 倍模块宽度。

## 影响与证据

ChangedProjects: Core、Templates、DesignerHost、Web、测试和案例。
ChangedPublicContracts: 既有类型新增受支持配对；Host 追加可选 `gs1-composite-stacked` 特性和能力数组值；普通 Expanded 错误码图修正，输出字节/尺寸可能变化，无签名和枚举值修改。
ChangedRuntimePaths: Expanded 标准编码、Stacked Composite 编码/测量/光栅、跨层协商。ChangedTFMs/Packaging/BenchmarkHarness: 无。RiskLevel: HIGH。

参考固定 Zint 2.15.0 `4896136bdbebb1a0908a5cc419513efa5bf4bf70` 的 rss.c、composite.c 和测试向量，保留许可。证据写入 `artifacts/gs1-stacked-20261022/`。先记录定向失败，再实现、定向通过和最终验收；不运行无关性能矩阵。

## 其余范围

本阶段不是“全部条码制式完成”：Expanded Stacked、GS1-128 A/B、EAN/UPC Composite 仍为后续能力；完整 GS1 AI 业务语义与复杂文字需单独定义范围。真实设备与跨平台环境为 BLOCKED_EXTERNAL；设备传输及完整协议插件扩展为 ACCEPTED_LIMITATION；图片依赖大版本迁移为 DEFERRED。不得将本地码图验证写成实机扫码通过。

## 收口与证据

S1：4 组公开固定模块串、25 组独立解码、3 组原始字段边界与旧 Expanded 回归通过；修正错误码图会改变部分实际尺寸。S2：5 组固定 Stacked 全矩阵、条高/静区/四角旋转逐像素验证、容量、取消和失败恢复通过。S3：四协议输出、模板 V2/V3、Host 能力及真实浏览器往返/旧 Host 降级通过。S4：C#/JSON/包消费案例与当前文档同步，保留历史结果。S5：最终五框架 XML 10/10，两真实运行时各 3529/3529，Host 159/159，Vitest 301/301，Chromium 160/160，均无失败或跳过，候选包消费 PASS。

复核中增加了原始字段数字格式判定，仅重跑受影响 Expanded 回归、XML、两运行时和打包消费；未改变的 Host/浏览器/样例证据按影响范围复用，首次结果保留。详见[验收报告](../../docs/gs1-stacked-verification-2026-10-22.md)。本计划 5/5 CLOSED、OPEN_ACTIONABLE=0，停止本轮修复；其余制式与外部边界未冒充完成。
