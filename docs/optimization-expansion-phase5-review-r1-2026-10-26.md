# 第五阶段独立复核：修正第 1 轮

日期：2026-10-26。审查对象为执行线程交付的 `phase5-final-candidate-20261026`，不重开已关闭的前四阶段。主代理负责契约/代码复核和定向反例，luna_worker 只读盘点原始证据。

## 结论

当前 `IMPLEMENTATION_STATUS=NEEDS_FIX`，整体实现仍为 **8/10**。F5/F6 不予关闭；本轮登记 R1—R4 和 E1，均为 `OPEN_ACTIONABLE`。这不是因外部 CI 缺失而触发修复：已有本机可重复的导出错误、锁失效及历史事务缺口。

执行侧历史测试数量经独立盘点确认：Core 三个真实运行时各 4705/4705，Host 357/357，XML 10/10，Vitest 474/474，Chromium 222/222。它们证明覆盖范围内通过，不否定下列新反例。前次全量首败及所有原始结果保持原样。

## 影响分析与本轮验证

- Changed scope：本轮只新增复核文档、更新计划及写入 artifacts 诊断文件；未改生产实现、测试或依赖。
- Affected projects/runtime paths：DesignerHost SVG 单条/批量入口；Vue 节点身份、编组、历史事务；风险 HIGH（输出内容/尺寸及用户编辑保护）。
- Public contracts/TFMs/packages：不要求签名、枚举、版本、五框架或 API 基线变更。
- L0：读取相关实现、测试与报告；L1/L3：调用交付候选 Host 的真实 HTTP 入口，以及使用真实 Vue `reactive/watch` 和生产身份/编组函数的 Node 探针。
- 未重复 L2/L4/L5。历史全量证据仅在相关源码未变范围复用。
- HTTP Host 从 `artifacts/optimization-expansion-20261025/phase5/final-candidate/host-tests/bin` 执行 `dotnet DesignerHost.dll`，临时地址 `http://127.0.0.1:5193`。
- 本轮反例保存于 [main-review-r1](../artifacts/optimization-expansion-20261025/phase5/main-review-r1/)。Node 探针命令：`node artifacts/optimization-expansion-20261025/phase5/main-review-r1/identity-probe.cjs`。

## 可执行问题

### R1 / P1：Code49 SVG 重复计入内置左边距，裁掉条纹

状态：`OPEN_ACTIONABLE`。

位置：`tools/DesignerHost/DesignerRequestProcessor.Export.cs`，`AddBarcode` 中 `intrinsicLeft`、`left` 与 Code49 分支（约 431—462 行）；参照 `EscPosRasterCanvas.DrawCode49Core`。

触发：ESC/POS Raster，Code49，`ABCD1234`，模块 2 dot、行高 20 dot、默认静区，调用 `/api/designer/element-svg`。

证据：`code49-request.json`、`code49.svg`。HTTP 200，但 viewBox 为 `0 0 162 46`，顶部矩形从 `M20 0h162` 延伸至 x=182，条纹又从 x=40 开始，并有 x=172、宽 8 的条纹越界。`left` 已包含 intrinsicLeft，分支再次叠加，导致左侧空白和右侧裁切。

建议：分别表达外部静区、Code49 内部 padding 和实际编码原点，与共享光栅实现一致，不能靠扩大 viewBox 掩盖错位。

验收：默认/显式静区、四旋转的 Code49 SVG 与原共享码图逐模块一致；几何不得越出 viewBox；有效码图完整。补充其他存在专属分支的码制覆盖，避免仅以 Code128 四旋转代表全部一维/堆叠码。

### R2 / P1：SVG 改用另一协议重新布局，且批量丢失来源 DPI

状态：`OPEN_ACTIONABLE`。

位置：`DesignerRequestProcessor.Export.cs:157` 的 `CreateBarcodeSvgOptions`、`ElementSvg`；`DesignerRequestProcessor.Batch.cs` SVG 重新加载/渲染与 DPI 获取（约 99、210—218 行）。

触发一：TSPL，模板高 100，设备文字行高校准 60，重复两条记录，每行文本后接 Code128。`tspl-pagination-request.json` 指定 `pageIndex=1, elementIndex=1` 和原 sourcePath。

证据：`tspl-pagination-preview.txt` 为 HTTP 200，两页分别含 FIRST/SECOND；`tspl-pagination-element-svg.txt` 为 HTTP 400 `InvalidPageIndex`。新入口强制 ESC/POS Raster 并丢掉设备字体校准，将原目标的两页排成一页，原预览索引失效。只有 sourcePath 检查不足以区分重复实例。

触发二：300×300 DPI、Code128 `DPI-300`，同一模板单条 SVG 与 `exportFormats:["svg"]` 批量结果。

证据：`single-300.svg` 与 `batch-300.svg` 的 viewBox 均为 `0 0 264 20`，单条尺寸 `22.352mm × 1.693333mm`，批量却为 `33.032512mm × 2.502463mm`，等同退回 203 DPI。原请求和 ZIP 保存为 `batch-300-request.json`、`batch-300.zip`。

建议：元素选择、页/重复实例和输出默认参数以原目标渲染为权威；共享软件编码只替代码图编码，不替换整个模板目标的布局。来源 DPI、静区、旋转、条宽和页/份数身份在单条、批量保持一致。不能用放弃 TSPL/ZPL 支持、忽略 pageIndex 或仅拷贝一个 DPI 属性关闭此项。

验收：上述两例通过；补覆盖 TSPL/ZPL 校准、重复实例和跨页选择、ESC/POS 非方形 DPI 四旋转；单条 SVG 与批量对应 SVG 几何/物理尺寸相同，manifest 页和实例对应原打印输出。保留取消、预算拒绝和无部分 ZIP 的契约。

### R3 / P1：嵌套锁可被编组绕过并清除，行编组重建子节点 ID

状态：`OPEN_ACTIONABLE`。

位置：`web/Bing.EasyPrint.Designer/src/App.vue:752` 的同步 reconcile watch、`groupCurrentCell`（约 2213）；`editorIdentity.ts:reconcile`；`componentsModel.ts:groupCellElements/groupConsecutiveRows`。

触发：未锁定组合包含已锁定子元素，再将该组合和同级元素编组。当前入口只检查所选顶层路径，不检查其子树。分次 splice 删除原节点时，同步 watch 将 lockedById 项删除，插回时默认为未锁定。

证据：`identity-probe.cjs` 使用实际 Vue reactive/watch，复现 App 的 `deep:true,flush:'sync'`；`identity-probe.json` 中三个入口 guard 均 false（未检测到锁），子节点 n4 从 `locked:true` 变为 `locked:false`。同一探针调用行编组，原子节点 n9 变成 n18；根因是 `groupConsecutiveRows` clone 原有行。现有身份单测手工在操作完成后 reconcile，未覆盖真实同步观察顺序。

建议：结构变更应有明确的原子边界，所有入口对相关锁定子树做一致检查；保留移动/编组/拆组的原节点身份，复制才生成新 ID；中间删除不应被当成最终删除清理锁状态。不要单纯延后 watch 导致下一操作漏锁。

验收：真实 Vue 操作和浏览器中，嵌套锁保护生效；未锁定节点的元素/行编组、拆组及移动保留 ID；复制新 ID、最终删除清理映射；保存重载/撤销重做保持身份与锁。不允许靠删除锁、重建全部 ID 或跳过用例通过。

### R4 / P2：项目导入清空撤销历史，排列未形成独立事务

状态：`OPEN_ACTIONABLE`。

位置：`App.vue:replaceTemplate/resetHistoryBaseline`（约 855—875）、`importFile`（约 2624—2650）、`arrangeSelection`（约 2106）及 250ms 历史 watch。

证据：JSON/v2 导入调用 replaceTemplate 内的 resetHistoryBaseline，并在 applyEditorState 后再次重置；ZIP 导入也经 replaceTemplate 清空旧历史。因此导入前项目无法一步撤销恢复。排列只修改坐标并依赖 250ms 防抖记录，没有在前后提交事务，与相邻编辑/下一排列可能合并。与本阶段“一次排列、映射或导入对应一次撤销操作”的约定不符。

建议：区分初次加载基线和用户导入事务；历史恢复应包含导入影响的模板、资源、输出配置、数据及编辑状态。排列在使用有效布局时完成一个原子历史提交，不能与紧邻操作合并；若布局过期，先获取有效布局或明确禁用操作。

验收：已有项目导入 v1/v2 JSON 或 ZIP 后，一次 undo 恢复旧状态、一次 redo 恢复新状态；导入失败不污染状态。连续两次排列及排列紧接编辑，在不增加等待的测试中可逐次撤销。保留 80 次/总字节预算和无效业务 JSON 可撤销行为。

### E1 / P2：最终证据和报告仍有范围夸大及计划门禁遗漏

状态：`OPEN_ACTIONABLE`。

证据盘点：

- “10 种 SVG / 独立解码 7 种”实际为 10 个向量：Code128 四方向、QR、DataMatrix、PDF417、Composite A/B/C；解码只有 7 个向量，Composite 没有独立 decoder 结果。应称向量/样本并准确列覆盖。
- `sample-agent-verification` 的 BatchConsumer 请求未带 exportFormats，不能证明新增批量 PDF/SVG；应补真实 opt-in 导出案例/manifest 验证，与 R2 回归合并。
- README 快速示例尚无实际构建/运行记录；最终计划明确要求，需执行原示例并记录结果。
- Host 357 项通过来自显式 OutputDirectory/ResultsDirectory/-NoRestore 命令。补计划要求的默认验证入口结果，保留已有生成物，不以隔离输出命令充当默认验证。
- 样例 15:33 preview 失败与 15:54 成功并列，应标明后者 supersede 前者及对应候选，不抹掉失败。live Host 的启动证据应记录 CWD/加载 DLL，避免未绑定的 PID 被声称与 candidate manifest 同一二进制。

验收：修正报告计量口径和适用范围；补齐以上有限案例/入口证据，保存命令、退出码、实际 runtime 和明确候选范围。可复用未变化的 Core/XML/包消费，不为文字修正重跑全矩阵；若相关生产库变动则按影响重验。

## 本轮收口与下一动作

- Completed：前四阶段 8/10；第五阶段已有实现和大部分门禁，未验收关闭。
- Open Actionable：R1—R4、E1（本轮有明确根因/验收，不用总通过数替代）。
- Blocked Approval：无。
- Blocked External：Windows/Linux Actions 实际执行证据。
- Not Applicable：实机验收按用户要求排除；本轮不发布。
- Accepted Limitations：设备原生 HRI 不包含在码图 SVG；未声明的软件像素能力明确拒绝。
- Verified Boundaries：既有预算/取消证据按未变范围保留。
- Deferred：设备传输、复杂字体塑形、富文本、厂商协议、图片依赖大版本迁移。
- No-Progress Check：CHANGED，首次独立复核取得新反例。执行侧内部集成失败保留，不冒充首次通过。
- Next Action：同一 Codex/gpt-6-luna/max 线程执行第五阶段修正 1/3；先最小回归，扩大到受影响 Host/前端，之后交独立复核。不开新功能、不重开前四阶段。
- Goal Status：RUNNING。
