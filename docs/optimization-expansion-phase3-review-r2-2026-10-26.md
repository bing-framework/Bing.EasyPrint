# 第三阶段：集成修正第 2 轮独立复核

日期：2026-10-26。结论 **NEEDS_FIX**。P3-R3/R4/R6/R7 已 CLOSED；R1/R2/R5/R8 仍 OPEN_ACTIONABLE。下一轮为本阶段最后一次集成修正 **3/3**。总计划仍 4/10，不进入 F3—F6。

## 已验证修正

主代理以交付 Host DLL（SDK10.0.402/runtime10.0.13）重放上轮反例，并加载实际前端源模块；未修改生产代码/测试。

- R1：前端真实解析+序列化与直接 API 在 de-DE 下都输出 `123456789012345,6789`；非法数字词法被拒绝。带 offset 原文得到正确格式，无 Format 的旧日期显示也恢复。
- R2：字面量年份 `'y'MM-dd`、无 offset 的可选 K 输入都明确400；不再补入当前年份或本地时区。
- R5：普通成功/partial 的 header 和 manifest RemainingBytes 一致；原 12KB 字段名/10KB预算反例已有拒绝测试和证据。
- R6 CLOSED：完整 clone 已进入实际运行路径；独立运行真实组件脚本、阻塞首块并修改外部嵌套模板，第二块保持 BEFORE。真实26条第二块取消用例已通过，卸载清理源码存在。组件脚本诊断替换网络和合并，不冒充浏览器生命周期测试。
- R7 CLOSED：合法 EAN13 后 `abc` 现在 partial，成功文件保留、失败序号/元素路径正确；格式和绑定错误仍汇总。Host 已补首条成功后的内部、资源、取消失败，终止且无 ZIP。上轮 R3/R4 继续关闭。

## 最后一轮修正范围

### P3-R1：未使用的大数仍破坏旧路径

位置：`tools/DesignerHost/DesignerRequestProcessor.cs` 的 Prepare/ParseDataToken，以及 Batch 对 legacy/raw 数据的构造。

实际请求均无 Format：`data={"Value":1e40}` 返回400/TemplateInvalid；更直接的 `data={"Value":"OK","Unused":1e40}` 也返回400，根因是先把整个数据树按 decimal 解析，超出 decimal 的**未使用**属性已经导致失败，legacy 路径根本到不了。旧默认 Double 可以接收该值。

验收：raw 保真层应允许保留数值原文或延迟转换；只有实际绑定到 Number/Currency 等需要 decimal 的字段才转换和检查溢出，错误带该元素/字段路径。未格式化/未使用的大数、其他合法旧数据不应因新 raw 层而失败。不能简单在无 Format 的模板跳过 raw 以遗漏“同一模板部分字段格式化、另有未用大数”的情况。保留已通过的 de-DE、数字身份、非法 JSON 和 legacy 日期回归。

### P3-R2：长度估计仍会拒绝合法格式结果

位置：`BgFieldFormatter.EstimateText/EstimateNumber`。

两个小规模反例：

1. HTTP `Kind=Number, Pattern=f90, Value=1.2`，默认预算，返回400“字段格式输出长度超过预检上限”。标准格式检测接受小写，估计分支只处理大写，落入 `pattern.Length+64` 而实际输出92字符。
2. 直接加载交付程序集调用实际 formatter，Text 的 SByte=-128、Int16=-32768、Int32=-2147483648 全部被同一上限错误拒绝；估计3/5/10忘记负号。此为 PowerShell .NET10.0.8 的内部入口诊断，不替代公共渲染回归。

验收：统一合法标准格式大小写，覆盖有符号整数、符号/地区分组/小数、字符串文字及已支持的格式范围；估计必须足够且先预留，不能用事后错误代替合法输出。小预算的短结果仍可用，高精度大分配仍提前拒绝。对上述反例通过公共模板入口补测试，保持五框架一致；不扩展无关格式能力。

### P3-R5：最终合并 metadata 仍越过累计额度

位置：`web/Bing.EasyPrint.Designer/src/batch.ts` 的 mergeBatchArchives；Host CountingBufferWriter/BuildErrorsCsv 的工作预算。

主代理受控反例：两个合法 ZIP，各包含一条失败记录及2000字符错误消息，调用**实际** mergeBatchArchives(chunks,8500)。输入 ZIP 共1150字节，最终成功返回652字节 ZIP，但解压后 manifest=4924、errors.csv=4156，共 **9080字节 > 8500**。原因是仅计输入 metadata，最终重新格式化清单/重建错误表的字节与空间没有检查。现有最终 blob.size 检查只约束压缩后大小。

还存在同根因的工作预算遗漏：解析出的 UTF-16 文本/JSON模型、最终 stringify/errorsCsv 与压缩工作缓冲未预留；Host计数 writer 的初始/扩容数组在metadata预留前分配，BuildErrorsCsv 的 StringBuilder/替换/ToString 与 UTF-8 输出同时存在，预留只覆盖最终UTF-8长度。需要按明确预算规则覆盖，不要求估算整个运行时，只要求不漏本功能显式创建的大缓冲。

验收：合并清单/错误表计入最终累计未压缩输出和工作空间，大小在实际生成前可判断并受控拒绝；原始输出、metadata、压缩ZIP、临时缓冲口径在文档明确。上述8500反例必须拒绝，合理充分额度必须成功。Header/manifest的已修正一致性保持。使用小额度/真实小ZIP验证具体分支，针对既定32MiB补边界证据，不能只伪造 Blob.size 或在首块入口失败就宣称最终合并门禁通过。

### P3-R8：完成剩余判别性验证并如实记录历史

已具备：1000条40次Host请求累计字节和页数；26条真实浏览器两块输出/第二块取消；独立 BatchConsumer 实际HTTP、3条单块、exit0。这些不需为补文档重复运行。

仍需：实际合并metadata超限路径（上例）、合并进行期间取消（当前Vitest为预取消）、最终输出预算分支与明确的32MiB边界。可用可注入小额度测试算法、再对真实有界内容做一次规模常量确认；不要运行大型无关矩阵或伪造内容长度。新测试应能否定当前剩余反例。

R2首次Core有71项失败、业务样例首次exit1的**原因文字**已记录，但原始首次日志未保留；不要声称原始日志完整，也不要重新制造历史失败。最终成功记录可用，报告注明粒度限制；本轮先保存首次输出再修，禁止覆盖。执行方复制主代理探针的结果应称“执行方重放”，主代理自己的本轮证据见下文。

## 验证与证据

主代理目录：`artifacts/optimization-expansion-20261025/phase3/main-review-r2/`。

- `probe.cjs` / `results.json`：当前请求使用真实 stringifyJsonPreservingPrecision，重放上轮 HTTP、JSON/CSV 反例，exit0；原 r1 文件未覆盖。
- `remaining-probes.cjs` / `remaining-results.json`：未用大数、小写f90及实际两块metadata合并反例，exit0。首次诊断的TS转译未开esModuleInterop，merge无法调用；工具输出保留该环境错误，修正探针转译选项后才得到上述有效结果，不算产品失败/通过。
- `snapshot-probe.cjs` / `snapshot-results.json`：真实组件脚本的两块模板内容均BEFORE；缺组件挂载的onUnmounted警告不用于生命周期验收。
- `text-negative.json`：实际formatter对三个有符号整数的错误结果。

Host诊断进程已停止。主代理未重复全量测试；读取代码与上述L1诊断只聚焦剩余根因。

luna_worker核对最终原始件：Core net10/net6/3.1各4665/4665、Host314/314、XML10/10、Vitest445/445、Chromium206/206（workers1/retries0）、定向5/5、模板exit0（105根/134总文件）、业务消费3/3 exit0。实际运行时10.0.13/6.0.36/3.1.32，三框架消费候选包哈希一致；打包时间在相关源码修改后。没有历史源码树快照，不补造。

## 状态和执行约束

- Implementation：4/10；本阶段Finding 4 CLOSED、4 OPEN_ACTIONABLE。
- Completed：R3/R4/R6/R7及其他条目的已验证子项。
- Open Actionable：R1未用大数兼容，R2正确长度估计，R5最终metadata/工作预算，R8对应验收与报告。
- Blocked Approval：无。
- Blocked External：外部Windows/Linux Actions。
- Not Applicable：实机验收、发布。
- Accepted Limitations：无持久队列，HTTP对象数组与旧job拒绝。
- Verified Boundaries：已验证的块/页/字节累计、取消和输入拒绝以原始用例为准。
- Deferred：传输、塑形、富文本、厂商协议、ImageSharp迁移；F3—F6仍待实施。
- Change Impact：Templates格式/Host raw数据、批量工作预算、前端ZIP合并及测试/文档；风险HIGH，无TFM/产品版本/API基线更改。先L1，再受影响项目及阶段门禁，复用未变证据。
- No-Progress：CHANGED，R6/R7关闭；其他事项代码已有实质进展但验收仍有反例。
- Next Action：OpenCode/deepseek-v4.1-flash执行最后一轮3/3，使用integration-r3-*；本轮后仍有缺口则按上限停止，不能自行进入第四轮或后续阶段。总任务RUNNING。
