# 优化扩展续作清单

2026-10-26，用户在第三阶段3/3停止报告后要求“继续实现后续的能力”。这次指令恢复执行；历史报告和失败证据保持原样。总计划仍5/10 CLOSED。

## 当前批次：批量资源预检收口

状态：CLOSED；用户续作第3/3轮独立验收通过，历史第三阶段原3轮记录保留。最新结论见 `docs/optimization-expansion-phase3-resume-review-r3-2026-10-26.md`。总计划6/10，进入第四阶段。

- [x] R5：Host计数writer扩容前预留资源，正确释放；CSV避免未计量的StringBuilder/ToString/UTF8副本。续作第1轮源码复核确认。
- [x] R5：前端单缓冲解压、有界输出收集和metadata预检；保留输出限制。
- [x] R8：小限额反例及单目标分配、部分写入后取消验证；充分额度成功。
- [x] R8：真实高压缩/不可压缩32MiB邻近工作空间边界，明确限制与拒绝阶段。
- [x] 批量指南与实施报告更新，区分结果大小、显式预算和进程堆。
- [x] 主代理独立复核通过，F2 CLOSED。

详细根因、位置、反例、验收条件见 `docs/optimization-expansion-phase3-review-r3-2026-10-26.md`。R1/R2/R3/R4/R6/R7保持CLOSED。

续作第1轮已复核，最新结论见 `docs/optimization-expansion-phase3-resume-review-r1-2026-10-26.md`：Host修正、长度计数和真实边界数据有进展；前端压缩器64KiB预留低于实际267160字节state数组，generateAsync输出累计/拼接及metadata文本副本仍漏计。进入续作第2轮，只修这些剩余项及对应证据，整体仍5/10。

续作第2轮已复核：输出流收集、压缩state和metadata预算已修；输入解压entry.async仍产生累计分块和concat双副本，1MiB页面/1.5MiB预算实际错误接受。见 `docs/optimization-expansion-phase3-resume-review-r2-2026-10-26.md`。进入续作最后第3/3轮，仅修输入解压预算和对应测试；整体仍5/10。

实现方向：优先复用已有预算作用域。Host按缓冲生命周期申请额度，或采用有界流式计数/输出；前端先计数UTF8/JSON/CSV长度再生成，不用完整编码结果反推预算。JSZip输入、解压和压缩显式缓冲采取可说明的上界和生命周期计量，必要时使用已有库的流式接口。无需估算整个JS引擎堆，不增加无关依赖，不提高32MiB产品限制；较早命中工作空间门禁应明确报告，不能假称可生成接近32MiB的任意输出。

影响分析：Host批量输出与前端ZIP合并、对应测试和文档；风险HIGH。无生产TFM、.NET公共签名、版本或API基线变更。先L1预算/取消回归，再受影响Host完整测试、前端类型/构建/Vitest及批量浏览器测试；阶段收口按总计划执行门禁，未变核心/模板/包范围的既有证据按范围复用，不为文档或Host私有修改重复三运行时核心全量。

新证据独立写入 `artifacts/optimization-expansion-20261025/phase3/resume-20261026-r1/`，保存首败原始输出。此前Core取消时序首败保持可见，不能用重跑通过覆盖；本轮不扩展成无关测试重构。

## 后续能力（原批准范围）

1. F2验收后：F3 GTIN Digital Link、F4可视化AI编辑器。主代理先确认跨层契约，OpenCode实现，沿用固定AI字典及官方URI Syntax 1.7.0，不联网解析resolver。
2. 第四阶段验收后：F5位图PDF/码图SVG、F6锁定/排列/数据撤销。
3. 最终集成、手册与案例验收。外部Windows/Linux CI保持BLOCKED_EXTERNAL；实机验收NOT_APPLICABLE。

OpenCode/deepseek-v4.1-flash负责代码、测试、案例和使用文档；主代理负责架构、总计划与独立复核。窄范围证据盘点委派luna_worker。保留未提交修改，不暂存、提交、推送、发布或修改产品版本。
