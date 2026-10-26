# 第三阶段续作第2轮独立复核

日期：2026-10-26。结论NEEDS_FIX；总计划5/10，F2尚未放行。本轮输出端的压缩state、累计输出、最终Blob副本及最终metadata构造预算已有实质修正，剩余R5/R8收窄到**输入ZIP解压的累计/拼接副本与对应验证**。

## 本轮确认

- 最终输出改为 `generateInternalStream` 收集，退出 `generateAsync` 累计并concat的路径；收集过程和创建Blob前分别检查输出块与副本预算。
- 压缩state按当前依赖实际267160字节计入，另计输出chunk。最终metadata在生成前计UTF16和UTF8，20000预算下拒绝先于创建Deflate及TextEncoder大数组。
- 新增固定种子512KiB不可压缩内容，验证最终输出增长分支；真实高压缩31.5/31.99/32MiB与31MiB随机输入有独立探针，未把约33KB的最终ZIP称为近32MiB ZIP成功。
- Host、Core/Templates及已关闭问题未变，继续复用已确认范围。

## 剩余R5：输入解压仍按单副本计量（OPEN_ACTIONABLE，P1）

位置：`web/Bing.EasyPrint.Designer/src/batch.ts:300`—321；`manifestEntry.async('uint8array')` 和页面 `entry.async('uint8array')`。

预检 `projected` 只累计条目未压缩长度一次。当前JSZip的 `zipObject.async` 实际调用 `internalStream(type).accumulate`；`StreamHelper` 同时持有累计解压分块，随后再分配同等大小的完整Uint8Array并复制。因此页面长度为N时，拼接点至少存在两份N，还不含输入ZIP、解压器工作区或metadata。

主代理独立反例：**1048576字节零填充页面，工作预算1572864字节（1.5MiB）**，输入ZIP1630字节；实际 `mergeBatchArchives` 成功返回1837字节ZIP。对Uint8Array构造只记录、不改变返回值的包装捕获了 `StreamHelper.js:57` 新分配1048576字节拼接数组；该行执行时其dataArray仍持有同等长度的分块。仅这两份显式数组已超过1.5MiB预算。

证据：`artifacts/optimization-expansion-20261025/phase3/resume-20261026-r2/main-review/inflate-concat.cjs`、`inflate-concat-results.json`。运行 `node .../main-review/inflate-concat.cjs`，exit0。此为分配路径诊断，不是进程内存采样，也未声称OOM。

验收与建议：在解压前为累计分块、最终数组、实际Inflate工作区及仍存活的输入缓冲预留，或使用已有internalStream直接写入预留数组以消除累计/拼接副本。manifest解压及后续解码同样适用，按阶段明确字节数组、UTF16解码字符串与模型字符串的计量。不要为通过边界用例提高32MiB预算；工作门禁提前拒绝属于可说明的既定限制。1MiB/1.5MiB反例若仍采用async拼接必须在大分配前拒绝；若改为单缓冲流式方案，须给出不会产生双副本且预检覆盖剩余工作空间的证据。充分额度内容与顺序一致，取消/失败不发布ZIP。

## R8对应收口

保留本轮最终压缩/metadata测试；新增解压阶段判别性用例及取消覆盖，避免只看输出阶段。真实32MiB邻近结果按修正后模型重新确认一次，高压缩/不可压缩与拒绝阶段明确区分。已有31.5MiB接受记录保留为旧候选观察，不能继续宣称它证明完整工作空间预算。

luna_worker只读核对构建完成、Vitest450/450、批量E2E5/5（retries0），Host317复用。零填充边界数据与报告一致。31MiB随机输入ZIP为32516400字节，在读取前的输入+arrayBuffer预算即被拒绝，不能作为解压覆盖证据。512KiB随机负载有2000000拒绝/3000000成功且输出大于409600字节的断言，未记录精确输出字节数。输出端确已移除generateAsync；输入entry.async内部仍调用accumulate，不能混为一谈。没有专门证明超限entry解压前拒绝的原始用例，这是本次补测范围。

## 执行边界

影响范围：前端batch.ts的输入解压、局部helper、相应用例和说明；风险HIGH。先定向解压/取消，再前端类型/构建/Vitest、批量Chromium（workers1/retries0）。Host与核心/模板未变范围复用，不重跑三运行时核心全量或无关矩阵。

No-Progress：CHANGED，输出路径已修。下一步为用户续作第3/3轮（历史首次执行的3轮独立保留），仅完成上述剩余项。该轮后仍有缺口则停止报告，不自行开启更多循环或F3—F6。
