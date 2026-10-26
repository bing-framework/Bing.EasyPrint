# 第四阶段修正 2/3 独立验收

日期：2026-10-26。范围限 R6/R7 与证据归属 E1；R1—R5、R8、R9 沿用上一轮关闭结论，不重复打开。

## 实现复核

- R6 CLOSED：无数据时静态遍历验证配置并将动态重复/条件/组件依赖标记为待数据；提供数据后仍沿共享实例绑定遍历，不把缺路径视为成功。前端按同字段全部结果聚合，错误优先，保留instanceIndices；没有活动实例显示pending，不虚构已验证。
- R7 CLOSED：模板将已知sourceAi传递到诊断，GTIN空值/字符/长度/校验位的来源AI和原Items索引保留；原有AI10索引修正不回退。
- E1 CLOSED：报告已区分首轮目标框架与未记录的testhost patch；本轮TRX和VSTest诊断用于实际测试进程版本，消费者记录仅用于消费者。原全量211成功与另一次定向红测2失败分开保留。

主代理从本轮 `integration-r2/host-green-bin` 启动临时5192 Host，重放上一轮的三个原始请求。有效重复记录返回valid；省略data返回pending/pending-data；第一项有效、第二项INVALID返回invalid、ai=01、itemIndex=0、instanceIndices=[1]。证据：`artifacts/optimization-expansion-20261025/phase4/main-review-r2/repeat-validation-confirmed.json`。已检查Vue错误优先汇总源码及真实Chromium测试断言：缺数据、空Items、有效恢复、多实例错误优先、实例2定位和修复恢复均覆盖。

本轮首次尝试连接执行者5191服务被拒（服务已退出），`main-review-r2/repeat-validation.json`为空响应，不是产品结果。启动参数 `--urls` 被项目专用环境配置覆盖，首次绑定5187冲突；随后按项目 `EASYPRINT_DESIGNER_API_URL` 在5192成功启动。临时服务重放后终止，既存服务未改动。仅confirmed文件是本轮HTTP验收证据。

## 验证与复用

执行方本轮：Host定向5/5、Templates GS1 6/6、GS1 API Vitest4/4、真实Chromium2/2（workers1/retries0）、Host/Templates和前端生产构建通过。C#定向运行保存TRX和诊断，testhost为.NET10.0.11。新增反例首败及构建/旧程序集诊断保留，不记作当前候选通过结果。

本轮没有修改Core编码器、共享TemplateBindingTraversal或渲染器、生产TFM/API/XML契约。复用修正1的Core三TFM各4705、Host337、Vitest456、完整Chromium211、XML10和候选消费/案例作为既有范围证据，并由本轮定向结果覆盖新改动。该组合不是当前候选重新全量运行；R7改过Templates内部诊断，本轮仅构建net7资产并在net10测试，旧包消费不能证明当前R7候选包已消费，低TFM的R7回归尚未新跑。第五阶段最终集成必须执行总计划要求的完整候选门禁，补齐这些证据和实际运行时/TRX留存。首轮旧框架运行patch未记录的历史事实保持可见。

luna_worker核实本轮计数及首败。运行时准确位置为 `integration-r2/host-green-final-results/l1-host-green-final.diag.host.26-10-26_11-54-40_05717_5.log:2` 与 `integration-r2/template-gs1-results/l2-template-gs1-tests.diag.host.26-10-26_11-57-23_82608_5.log:2`，它们是testhost生成日志，不应与外层`.diag.log`混称。以上限制在最终候选验证中补齐，不把本地功能验收写成当前候选所有TFM/包已全量通过。

## 功能覆盖与边界

| 项目 | 结论 |
| --- | --- |
| GTIN Digital Link | 固定字典、GTIN/限定符/query校验、URI保真、普通QR/DataMatrix、结构化来源及诊断已闭环 |
| GS1 AI 编辑 | 单一Host目录、搜索与静态/动态编辑、转换、保存重载、待数据/实例错误、旧Host降级已闭环 |
| 案例和文档 | C#/JSON/浏览器案例及GS1指南、README/开发/使用导航已有对应更新，阶段报告保留首次失败和证据适用范围 |
| 外部CI | Windows/Linux Actions实际运行仍BLOCKED_EXTERNAL，不影响本地阶段实施关闭 |
| 设备验收 | 用户明确排除，NOT_APPLICABLE |

## 当前执行摘要

- Task：optimization-expansion / phase4；Round：2/3，停止本阶段自动修复。
- Implementation：8/10；F3/F4 CLOSED。
- Validation Run：L0代码与契约审查，L1独立HTTP重放；执行方本轮定向测试和构建如上。没有无关性能矩阵。
- Completed：R1—R9及E1；四阶段本地实现。
- Open Actionable：本阶段无；总计划F5/F6仍待实现，另有最终候选验收工作。
- Blocked Approval：无。Blocked External：Windows/Linux CI实际运行。
- Not Applicable：实机验收及本轮发布；新增能力无历史同构性能基线。
- Accepted Limitations：仅GTIN未压缩离线Digital Link、既定原生设备字体范围。
- Verified Boundaries：继承前三阶段预算拒绝/取消证据；本轮无新增容量宣称。
- Deferred：设备传输、复杂字体塑形、富文本、更多厂商协议、图片依赖大版本迁移。
- No-Progress Check：CHANGED，剩余反例闭环。
- Next Action：停止第四阶段Fix；按第五阶段契约启动F5/F6，实现完成后最终集成验收。
- Goal Status：RUNNING（总计划）；第四阶段本地验收完成，不表示外部发布门禁通过。
