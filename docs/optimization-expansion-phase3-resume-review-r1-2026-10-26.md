# 第三阶段续作第1轮独立复核

2026-10-26。结论：NEEDS_FIX；总计划5/10，F2尚未放行。R5的Host计数器/CSV修正已确认，前端工作预算仍有同根因遗漏；R8真实边界证据已补，但预算模型修正后须更新对应结论。没有重开已CLOSED问题。

## 已确认进展

- CountingBufferWriter在初始分配和扩容前申请资源，替换后释放旧租约，异常释放新租约；CSV直接写入已计数的UTF8数组，移除了StringBuilder/ToString副本。
- 前端最终JSON/CSV的长度计数在构造最终文本前执行。独立重放8500反例，TextEncoder分配记录为空；该例本次命中输入工作预算，不能单独证明最终压缩预算正确。
- 提供了真实31.5/31.99/32MiB内容ZIP，不再只有伪造Blob.size和4KB普通成功。第二块读取期间取消仍有证据。

## R5剩余：前端压缩与输出副本预算低估（OPEN_ACTIONABLE，P1）

位置：`web/Bing.EasyPrint.Designer/src/batch.ts:90`、216、253—263。

1. `zipStreamWorkBytes=65536`不是当前依赖的工作缓冲上界。主代理加载当前JSZip实际使用的pako，按 `flate.js` 相同默认值创建Deflate，只统计其state的TypedArray：pending_buf/window/prev/head各65536，另有树等数组共5016，合计 **267160字节**。尚未计输出chunk、最终ZIP、字符串和运行时对象。证据为 `resume-20261026-r1/main-review/compression-work-results.json`，不能把64KiB当成覆盖压缩器的预留。
2. `generateAsync({type:'blob',streamFiles:true})`依然调用StreamHelper.accumulate。当前依赖 `node_modules/jszip/lib/stream/StreamHelper.js:87` 保存每个输出块，98行concat；57行再 `new Uint8Array(totalLength)`，随后转Blob。当前工作预算没有覆盖累计压缩输出及最终拼接副本。streamFiles只改变条目输出过程，不让generateAsync变成不持有最终结果的消费者。
3. 最终metadata仅按UTF8长度预留一次，但实际持有manifestText/errorsText，并在ZIP写出时编码；CSV的lines/join过程也有临时字符串。输入manifestBytes、解码文本与模型的计量同样需按生命周期明确，不能只乘2后宣称所有显式缓冲都覆盖。无需估算整个JS堆，只需正确计入本功能明确创建/依赖创建的大缓冲。

影响：旧9080字节超限输出已被拒绝，但工作空间门禁仍能在实际显式缓冲超过预留时允许生成；新31.5MiB接受结果不能证明所声明的工作空间上限。未声称实测进程峰值或OOM。

建议：采用有界输出流收集及提前预留，或为现有generateAsync的压缩工作区、累计输出、concat/Blob副本计算可说明的上界；考虑足够保守且测试可验证的余量，而不是把64KiB改成另一个未经核对的常数。metadata分别计数UTF16长度与UTF8长度，按同时存活缓冲申请/释放。保留32MiB结果/显式工作预算，不提高上限；工作空间先拒绝合法大输出属于已说明边界。

验收：使用不可压缩小载荷验证压缩输出增长/拼接分支；用metadata为主的输入验证最终字符串和编码缓冲；在目标分配之前拒绝，充分额度内容一致。压缩器上界依据实际固定依赖；取消不发布结果。保留小测试，再执行一次真实32MiB邻近边界确认，明确拒绝阶段。不需要大型矩阵。

## 验证范围与下一步

主代理命令：`node .../main-review/compression-work.cjs` 和 `node .../main-review/allocation-order.cjs`，均exit0。仅定向诊断，无产品/测试代码修改。下一轮影响范围收窄为前端batch.ts、相关测试和文档；Host已修范围及未变核心/模板证据复用。默认只跑前端定向/完整Vitest、类型构建和批量浏览器用例，不重跑三个运行时核心全量。

luna_worker只读核对：Host317/317、Vitest449/449（23文件）、build完成、E2E5/5（命令workers1/retries0）。命令清单列9条最终验证，另有首轮探针件；非所有命令都有独立退出码文件，不宣称完整shell日志。真实边界使用零填充数组：31.5MiB为33030144字节，输入ZIP32732、输出ZIP32908字节；31.99/32MiB分别33543946/33554432字节并在工作预算门禁拒绝。这是高压缩率原始负载边界，不是接近32MiB的不可压缩ZIP输出边界。metadata首败日志/JSON、边界初版JSON以及历史Core取消首败TRX均保留；历史Core首败目录未找到独立stdout日志，不扩大证据声明。

当前续作1轮，代码有实际进展；按续作清单进入第2轮，仅修R5剩余与R8对应验证。原3轮停止历史保持。后续F3/F4须待F2独立验收后开始；不自动放宽门槛。
