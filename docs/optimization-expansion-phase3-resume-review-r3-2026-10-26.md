# 第三阶段续作第3轮最终复核

日期：2026-10-26。**F2 CLOSED，第三阶段本地收口，总计划6/10 CLOSED。** R5/R8的既定反例和验收范围已闭环；不继续扩大资源审计或重开已关闭根因。外部Windows/Linux CI仍BLOCKED_EXTERNAL，实机验收NOT_APPLICABLE。

## 实现与独立证据

- Host计数writer分配前预留/释放、CSV直接UTF8写入，复用续作第1轮确认及317项Host证据；本轮Host未改。
- 输入ZIP使用internalStream写入按声明长度预分配的单个目标，检查超长/短缺及取消；移除entry.async累积分块再concat的双副本。每条目解压前检查累计工作预算，manifest另计解码/model额度。
- 输出端沿用有界流收集，计Deflate工作数组、输出chunk、累计结果和Blob副本；最终metadata在构造前分别计算UTF16/UTF8。
- 主代理重放旧1MiB/1.5MiB反例：接受，输入ZIP1630、输出1837字节；只捕获一个1048576字节大数组，调用栈来自readEntry预分配，没有StreamHelper.concat。结果与“保留async双副本必须拒绝”不同，是验收允许的单缓冲方案。
- 主代理补充部分解压后取消：目标写入首个16384字节数据块后触发取消，实际函数拒绝并报告Aborted，不返回ZIP。执行方的第二entry取消测试则在第二个目标分配时取消，二者不混写。

主代理证据：`artifacts/optimization-expansion-20261025/phase3/resume-20261026-r3/main-review/`，命令 `node inflate-concat.cjs`、`node cancel-partial.cjs` 均exit0。沿用旧探针文件名便于比对，本次结果note已改成单目标数组说明；不改写旧轮次结果。探针记录显式大数组和控制流，不是进程峰值测量。

## 验证范围与限制

luna_worker只读核对本轮原始件：build完成，Vitest23文件452/452，批量Chromium5/5，workers1/retries0。定向首轮18项有1项阈值断言失败：加强输入预算后20000更早拒绝；改用200000验证最终metadata阶段，最终batch20/20。首败日志 `first-failure-metadata-threshold.stdout.log` 保留，未单独保留该命令数值退出码，不声称完整shell记录。

本轮边界证据位于 `phase3/resume-20261026-r3/`：

| 输入与门禁 | 观察 |
| --- | --- |
| 最终metadata9080字节 | 7000输入预算拒绝；200000最终metadata/压缩工作预算拒绝；500000成功 |
| 31.5MiB零填充原始页 | 33030144原始字节，输入ZIP32732、输出32908字节，成功 |
| 31.99/32MiB零填充原始页 | 33543946/33554432字节，工作预算拒绝 |
| 31MiB固定种子随机输入 | ZIP32516400字节，输入与临时副本门禁拒绝；未进入解压 |
| 不可压缩512KiB输出 | 沿用本轮保留测试，较小工作额度拒绝、充分额度成功 |

预算是明确缓冲的估计与限额管理，不是浏览器整个堆的硬上限。高压缩数据接受不代表接近32MiB不可压缩ZIP也可生成；可能先遇工作空间门禁。上述拒绝案例不能称为都命中最终ZIP大小门禁。保留这些限制，不通过提高上限让所有大输入成功。

本轮只改前端，类型/构建/Vitest/批量E2E为新证据；Core三个实际运行时各4666、五框架XML10/10、候选包及案例按未改范围复用。历史Core取消首败TRX及第2轮首败日志缺失事实保持。没有重复无关核心全量；最终整体验收仍按总计划执行。

## 状态

- Completed：O1—O4、F1/F2；第三阶段R1—R8全部CLOSED。
- Open Actionable：原计划F3/F4（下一阶段），F5/F6（随后阶段）。
- Blocked External：实际Windows/Linux Actions。Blocked Approval：无。
- Not Applicable：实机验收、发布。Accepted Limitation：无持久队列、批量业务入口为对象数组、旧job拒绝、显式预算非进程堆上限。
- Deferred：设备传输、复杂字体塑形、富文本、更多厂商协议、ImageSharp大版本迁移。
- No-Progress：CHANGED；续作3/3完成。Next Action：停止第三阶段修复，进入已授权第四阶段F3/F4；Goal RUNNING。
