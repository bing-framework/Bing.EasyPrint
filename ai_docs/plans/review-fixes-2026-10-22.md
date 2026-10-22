# 工程审查修复与验收计划（2026-10-22）

## 目标与约束

修复 F1—F8，并补齐 GS1 Composite 浏览器案例。保留五个目标框架和 .NET 公共 API，不迁移图片依赖大版本、不增加设备传输或协议插件体系；保留开始执行前的未提交修改，不提交、推送或发布包。

## 执行清单

| 项目 | 实现及验收 | 当前状态 |
|---|---|---|
| F1 | 能力缺省/null合法，非Native数组拒绝；五模式真实Host及单测 | CLOSED |
| F2 | Core共享TIFF预检，CPCL/模板在识别前调用；大小端、Classic/BigTIFF、截断、越界、循环、取消、资源与合法图片回归 | CLOSED |
| F3 | ZPL方向统一为默认/None/Down；非法旧配置明确拒绝，验证PON/POI | CLOSED |
| F4 | 包消费ZPL标签宽320、高40，更新精确断言；当前候选包完整消费通过 | CLOSED |
| F5 | 修正Code39支持预期及唯一标签定位，保留不支持能力断言 | CLOSED |
| F6 | Host默认项排除artifacts/output，保留显式案例；默认脚本在既有生成物下通过 | CLOSED |
| F7 | 补齐117项公共XML注释；独立严格五框架验证脚本 | CLOSED |
| F8 | 同步状态、开发、使用手册及README受影响内容，保留历史结果 | CLOSED |
| Composite | 真实Host创建/编辑/预览/导出/项目往返，旧Host特性降级 | CLOSED |
| F9（验收发现） | Canvas过滤上限遗漏Type 11；显示Composite占位和Host尺寸，增加画布节点回归断言 | CLOSED |

## 实施与验证

主代理负责跨层契约、图片检查、集成及最终验证；luna_worker负责互不重叠的注释、文档或案例子任务。每项先局部测试，最终执行Core/Templates五框架严格XML构建、net6.0及netcoreapp3.1完整核心测试、Host默认脚本、Web构建/Vitest/Chromium、模板案例、README示例及net6.0候选包消费。图片复现沿用隔离进程5秒观察窗口，不通过提高超时或后台线程掩盖失败。

原审查证据：`artifacts/review-20261022/review-evidence.json`。新证据写入 `artifacts/review-fixes-20261022/`，最终报告：`docs/review-fixes-2026-10-22.md`。测试输出记录命令、候选、框架、实际运行时、通过/失败/跳过和原因。

## 边界与停止

- 真实打印机字体、DPI、进纸、切纸、扫码、恢复验收：BLOCKED_EXTERNAL。
- 设备传输和完整插件扩展：ACCEPTED_LIMITATION。
- 图片依赖大版本迁移及其余告警：DEFERRED，保留旧框架，本次不宣称依赖全部安全。
- 遵守GOAL_RULES，仅OPEN_ACTIONABLE触发修复；无关证据复用、不执行大型性能矩阵，达到无进展或轮次上限时报告并停止。
